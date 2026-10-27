using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using AIHub.Infrastructure.Persistence;
using Npgsql;
using StackExchange.Redis;

namespace AIHub.Api.Tests.Altyapi;

public sealed record SaklananKayit(
    Guid Id,
    string ServiceName,
    string Level,
    string Message,
    string? ExceptionType,
    string? StackTrace,
    string? Metadata,
    DateTimeOffset? OccurredAt,
    DateTimeOffset ReceivedAt);

/// <summary>
/// Testlerin veriye doğrudan bakışı. Her test kendi benzersiz servis adıyla çalışır; böylece aynı koşu
/// içindeki testler birbirinin satırlarını görmez.
/// </summary>
public static class TestVerisi
{
    public const string StreamKey = "aihub:logs";

    public static string YeniServisAdi() => "svc-" + Guid.NewGuid().ToString("N")[..12];

    /// <summary>Spec 0002 gelene kadar anahtarları testler doğrudan hazırlar (Spec 0001, Constraints).</summary>
    public static async Task<string> AnahtarEkleAsync(ContainerFixture c, string servisAdi, bool iptalEdilmis = false)
    {
        var hamAnahtar = "aih_" + Convert.ToHexString(RandomNumberGenerator.GetBytes(24)).ToLowerInvariant();
        await using var ds = NpgsqlDataSource.Create(c.Postgres.GetConnectionString());
        await using var cmd = ds.CreateCommand(
            "INSERT INTO service_api_keys (id, service_name, key_hash, created_at, revoked_at) VALUES ($1, $2, $3, now(), $4)");
        cmd.Parameters.AddWithValue(Guid.CreateVersion7());
        cmd.Parameters.AddWithValue(servisAdi);
        cmd.Parameters.AddWithValue(ApiKeyHasher.Hash(hamAnahtar));
        cmd.Parameters.AddWithValue(iptalEdilmis ? DateTimeOffset.UtcNow : DBNull.Value);
        await cmd.ExecuteNonQueryAsync();
        return hamAnahtar;
    }

    /// <summary>Servis adından bağımsız: verilen mesajı taşıyan kayıt sayısı, bütün servislerde.</summary>
    public static async Task<long> MesajliKayitSayisiAsync(ContainerFixture c, string mesaj)
    {
        await using var ds = NpgsqlDataSource.Create(c.Postgres.GetConnectionString());
        await using var cmd = ds.CreateCommand("SELECT count(*) FROM log_entries WHERE message = $1");
        cmd.Parameters.AddWithValue(mesaj);
        return (long)(await cmd.ExecuteScalarAsync())!;
    }

    public static async Task<IReadOnlyList<SaklananKayit>> KayitlariOkuAsync(ContainerFixture c, string servisAdi)
    {
        await using var ds = NpgsqlDataSource.Create(c.Postgres.GetConnectionString());
        await using var cmd = ds.CreateCommand(
            "SELECT id, service_name, level, message, exception_type, stack_trace, metadata::text, occurred_at, received_at " +
            "FROM log_entries WHERE service_name = $1 ORDER BY id");
        cmd.Parameters.AddWithValue(servisAdi);
        await using var reader = await cmd.ExecuteReaderAsync();
        var kayitlar = new List<SaklananKayit>();
        while (await reader.ReadAsync())
        {
            kayitlar.Add(new SaklananKayit(
                reader.GetGuid(0),
                reader.GetString(1),
                reader.GetString(2),
                reader.GetString(3),
                reader.IsDBNull(4) ? null : reader.GetString(4),
                reader.IsDBNull(5) ? null : reader.GetString(5),
                reader.IsDBNull(6) ? null : reader.GetString(6),
                reader.IsDBNull(7) ? null : reader.GetFieldValue<DateTimeOffset>(7),
                reader.GetFieldValue<DateTimeOffset>(8)));
        }

        return kayitlar;
    }

    /// <summary>Stream'de şu an duran (henüz işlenmemiş) girdilerin ham içeriği.</summary>
    public static async Task<IReadOnlyList<string>> StreamIcerigiAsync(ContainerFixture c)
    {
        await using var redis = await ConnectionMultiplexer.ConnectAsync(c.Redis.GetConnectionString());
        var girdiler = await redis.GetDatabase().StreamRangeAsync(StreamKey);
        return girdiler.Select(g => (string?)g["payload"] ?? "").ToArray();
    }

    public static async Task<IReadOnlyList<string>> DeadLetterIcerigiAsync(ContainerFixture c)
    {
        await using var redis = await ConnectionMultiplexer.ConnectAsync(c.Redis.GetConnectionString());
        var girdiler = await redis.GetDatabase().StreamRangeAsync("aihub:logs:dead");
        return girdiler.Select(g => (string?)g["payload"] ?? "").ToArray();
    }

    public static async Task<long> BekleyenSayisiAsync(ContainerFixture c, string tuketiciAdi)
    {
        await using var redis = await ConnectionMultiplexer.ConnectAsync(c.Redis.GetConnectionString());
        var db = redis.GetDatabase();
        if (!await db.KeyExistsAsync(StreamKey))
        {
            return 0;
        }

        var tuketiciler = await db.StreamConsumerInfoAsync(StreamKey, "aihub-persist");
        return tuketiciler.Where(t => t.Name == tuketiciAdi).Sum(t => (long)t.PendingMessageCount);
    }

    public static async Task<bool> BekleAsync(Func<Task<bool>> kosul, TimeSpan sure)
    {
        var bitis = DateTime.UtcNow + sure;
        while (DateTime.UtcNow < bitis)
        {
            if (await kosul())
            {
                return true;
            }

            await Task.Delay(50);
        }

        return await kosul();
    }

    /// <summary>
    /// Host'u başlatır (migration'lar uygulanır), servis için anahtar ekler ve anahtarı taşıyan bir
    /// istemci döner.
    /// </summary>
    public static async Task<HttpClient> AnahtarliIstemciAsync(this AIHubFactory factory, ContainerFixture c, string servisAdi)
    {
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Api-Key", await AnahtarEkleAsync(c, servisAdi));
        return client;
    }

    public static Task<HttpResponseMessage> GonderAsync(this HttpClient client, object govde) =>
        client.PostAsJsonAsync("/api/logs", govde);

    public static Task<HttpResponseMessage> GonderHamAsync(this HttpClient client, string json) =>
        client.PostAsync("/api/logs", new StringContent(json, Encoding.UTF8, "application/json"));

    public static Task<HttpResponseMessage> GonderBaytAsync(this HttpClient client, byte[] govde)
    {
        var icerik = new ByteArrayContent(govde);
        icerik.Headers.ContentType = new("application/json") { CharSet = "utf-8" };
        return client.PostAsync("/api/logs", icerik);
    }
}
