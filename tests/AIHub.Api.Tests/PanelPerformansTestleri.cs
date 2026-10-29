using System.Diagnostics;
using System.Net;
using AIHub.Api.Tests.Altyapi;
using Npgsql;

namespace AIHub.Api.Tests;

// Kendi container'ları olan ayrı bir koleksiyon: 1 milyon satır öteki testlerin deposuna karışmaz ve
// paralel koşan testlerin yükü ölçüme girmez (DisableParallelization: bu koleksiyon tek başına koşar).
[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class PerformansCollection : ICollectionFixture<ContainerFixture>
{
    public const string Name = "Performans";
}

// Spec 0002, AC-3 — 1 milyon kayıtlı bir depoda ilk sayfa en geç 1 saniyede gelir.
[Trait("Category", "Integration")]
[Collection(PerformansCollection.Name)]
public sealed class PanelPerformansTestleri(ContainerFixture containers) : IAsyncLifetime
{
    private const int KayitSayisi = 1_000_000;
    private static readonly TimeSpan Esik = TimeSpan.FromSeconds(1);
    private readonly AIHubFactory _factory = new(containers) { ConsumerEnabled = false };

    public async ValueTask InitializeAsync()
    {
        // Host başlar ve migration'lar uygulanır; ardından satırlar tek SQL ifadesiyle yüklenir.
        _ = _factory.CreateClient();

        // Kayıtlar son 30 güne eşit yayılır: varsayılan 24 saatlik pencerede ~33 bin kayıt kalır.
        // 20 servis ve dört LogLevel dönüşümlü dağılır.
        await using var ds = NpgsqlDataSource.Create(containers.Postgres.GetConnectionString());

        // Her test durumu ayrı örnektir; depo koleksiyonla paylaşılır, satırlar yalnızca bir kez yüklenir.
        await using var say = ds.CreateCommand("SELECT count(*) FROM log_entries");
        if ((long)(await say.ExecuteScalarAsync())! >= KayitSayisi)
        {
            return;
        }

        await using var yukle = ds.CreateCommand(
            """
            INSERT INTO log_entries (id, service_name, level, message, received_at)
            SELECT gen_random_uuid(),
                   'perf-svc-' || (i % 20),
                   (ARRAY['Info','Warning','Error','Critical'])[1 + i % 4],
                   'performans kaydı ' || i,
                   now() - i * interval '2.592 seconds'
            FROM generate_series(1, $1) AS i
            """);
        yukle.CommandTimeout = 300;
        yukle.Parameters.AddWithValue(KayitSayisi);
        await yukle.ExecuteNonQueryAsync();

        await using var analiz = ds.CreateCommand("ANALYZE log_entries");
        await analiz.ExecuteNonQueryAsync();
    }

    public ValueTask DisposeAsync() => _factory.DisposeAsync();

    [Theory]
    [InlineData("/api/panel/logs")]
    [InlineData("/api/panel/logs?service=perf-svc-7&level=Error&level=Critical")]
    public async Task Bir_milyon_kayitta_ilk_sayfa_1_sn_icinde_gelir(string adres)
    {
        var client = _factory.PanelIstemcisi();

        // Isınma: ilk istek JIT, bağlantı havuzu ve token doğrulama ön belleğini kurar; ölçülen sorgunun kendisidir.
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync(adres, TestContext.Current.CancellationToken)).StatusCode);

        // Ölçüm 3 koşunun medyanıdır (plan 0002, R-12).
        var sureler = new List<TimeSpan>();
        for (var i = 0; i < 3; i++)
        {
            var saat = Stopwatch.StartNew();
            var yanit = await client.GetAsync(adres, TestContext.Current.CancellationToken);
            var govde = await yanit.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
            saat.Stop();

            Assert.Equal(HttpStatusCode.OK, yanit.StatusCode);
            Assert.Contains("\"older\":\"", govde, StringComparison.Ordinal); // tam bir sayfa geldi, arkasında başka sayfa var
            sureler.Add(saat.Elapsed);
        }

        var medyan = sureler.Order().ElementAt(1);
        Assert.True(medyan < Esik, $"İlk sayfanın medyan süresi {medyan.TotalMilliseconds:F0} ms (ölçümler: {string.Join(", ", sureler.Select(s => $"{s.TotalMilliseconds:F0}"))} ms).");
        TestContext.Current.TestOutputHelper?.WriteLine($"{adres}: medyan {medyan.TotalMilliseconds:F0} ms");
    }
}
