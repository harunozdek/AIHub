using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using AIHub.Api.Tests.Altyapi;
using Npgsql;

namespace AIHub.Api.Tests;

// Spec 0006 — dashboard'un log özeti (AC-2, AC-3, AC-4) ve listeye geçişte sayının tutması (AC-6). Kesin
// sayılar için her test kendi boş veritabanında çalışır (plan 0006, risk 3).
[Trait("Category", "Integration")]
[Collection(IngestionCollection.Name)]
public sealed class DashboardTestleri(ContainerFixture containers) : IAsyncLifetime
{
    private const string Uc = "/api/panel/dashboard";

    private readonly List<AIHubFactory> _factoryler = [];

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public ValueTask InitializeAsync() => ValueTask.CompletedTask;

    public async ValueTask DisposeAsync()
    {
        foreach (var factory in _factoryler)
        {
            await factory.DisposeAsync();
        }
    }

    /// <summary>Boş bir veritabanı, onun üzerinde bir host ve panel istemcisi.</summary>
    private async Task<(HttpClient Istemci, string Veritabani)> HazirlaAsync()
    {
        var veritabani = await TestVerisi.BosVeritabaniAsync(containers, Ct);
        var factory = new AIHubFactory(containers) { ConsumerEnabled = false, PostgresConnectionOverride = veritabani };
        _factoryler.Add(factory);
        return (factory.PanelIstemcisi(), veritabani);
    }

    private static async Task EkleAsync(string veritabani, string servis, DateTimeOffset alinma, string level = "Error", string? exceptionType = null, int adet = 1)
    {
        await using var ds = NpgsqlDataSource.Create(veritabani);
        await using var cmd = ds.CreateCommand(
            """
            INSERT INTO log_entries (id, service_name, level, message, exception_type, received_at)
            SELECT gen_random_uuid(), $1, $2, 'm', $3, $4 - i * interval '1 millisecond'
            FROM generate_series(0, $5 - 1) AS i
            """);
        cmd.Parameters.AddWithValue(servis);
        cmd.Parameters.AddWithValue(level);
        cmd.Parameters.AddWithValue((object?)exceptionType ?? DBNull.Value);
        cmd.Parameters.AddWithValue(alinma.ToUniversalTime());
        cmd.Parameters.AddWithValue(adet);
        await cmd.ExecuteNonQueryAsync(Ct);
    }

    private static async Task<JsonElement> OzetAsync(HttpClient istemci)
    {
        var yanit = await istemci.GetAsync(Uc, Ct);
        Assert.Equal(HttpStatusCode.OK, yanit.StatusCode);
        Assert.Equal("no-store", yanit.Headers.CacheControl?.ToString());
        return await yanit.Content.ReadFromJsonAsync<JsonElement>(cancellationToken: Ct);
    }

    [Fact]
    public async Task Derece_sayilari_son_24_saati_kapsar_kaydi_olmayan_derece_0_doner()
    {
        var (istemci, db) = await HazirlaAsync();
        var simdi = DateTimeOffset.UtcNow;
        await EkleAsync(db, "odeme", simdi.AddMinutes(-1), "Error", adet: 2);
        await EkleAsync(db, "odeme", simdi.AddHours(-3), "Info");
        await EkleAsync(db, "stok", simdi.AddHours(-23).AddMinutes(-58), "Critical");
        await EkleAsync(db, "stok", simdi.AddHours(-24).AddMinutes(-1), "Warning");
        await EkleAsync(db, "stok", simdi.AddDays(-3), "Error", adet: 5);

        var ozet = await OzetAsync(istemci);

        var dereceler = ozet.GetProperty("levels").EnumerateArray()
            .Select(l => (l.GetProperty("level").GetString(), l.GetProperty("count").GetInt64())).ToArray();
        Assert.Equal([("Critical", 1L), ("Error", 2L), ("Warning", 0L), ("Info", 1L)], dereceler);
        var from = ozet.GetProperty("from").GetDateTimeOffset();
        var to = ozet.GetProperty("to").GetDateTimeOffset();
        Assert.Equal(TimeSpan.FromHours(24), to - from);
        Assert.InRange(to, simdi.AddMinutes(-1), DateTimeOffset.UtcNow.AddMinutes(1));
    }

    [Fact]
    public async Task Hata_ureten_servisler_coktan_aza_esitlikte_alfabetik_ilk_10_doner()
    {
        var (istemci, db) = await HazirlaAsync();
        var simdi = DateTimeOffset.UtcNow.AddMinutes(-5);
        // 11 servis: "s01" 11 hata … "s11" 1 hata; "s05" ile "s04" eşit olsun diye "s05"e bir Critical eklenir.
        for (var i = 1; i <= 11; i++)
        {
            await EkleAsync(db, $"s{i:00}", simdi, "Error", adet: 12 - i);
        }

        await EkleAsync(db, "s05", simdi, "Critical");
        await EkleAsync(db, "aa-yalnizca-bilgi", simdi, "Info", adet: 50);
        await EkleAsync(db, "ab-yalnizca-uyari", simdi, "Warning", adet: 50);

        var servisler = (await OzetAsync(istemci)).GetProperty("topErrorServices").EnumerateArray()
            .Select(s => (s.GetProperty("serviceName").GetString(), s.GetProperty("count").GetInt64())).ToArray();

        Assert.Equal(
            [("s01", 11L), ("s02", 10L), ("s03", 9L), ("s04", 8L), ("s05", 8L), ("s06", 6L), ("s07", 5L), ("s08", 4L), ("s09", 3L), ("s10", 2L)],
            servisler);
    }

    // AC-3 (review 1, F-3): eşitlikte sıra sunucunun collation'ına bağlı değildir. Veritabanı bilerek ICU en-US ile
    // açılır; orada "kargo" "Odeme"den, "demoa" "demo-b"den önce gelir. Bayt sırasında tersidir.
    [Fact]
    public async Task Hata_ureten_servislerde_esitlik_sirasi_veritabani_collationindan_bagimsizdir()
    {
        var ad = "icu_" + Guid.NewGuid().ToString("N")[..12];
        await using (var baglanti = new NpgsqlConnection(containers.Postgres.GetConnectionString()))
        {
            await baglanti.OpenAsync(Ct);
            await using var komut = new NpgsqlCommand($"CREATE DATABASE {ad} TEMPLATE template0 LOCALE_PROVIDER icu ICU_LOCALE 'en-US' LOCALE 'C'", baglanti);
            await komut.ExecuteNonQueryAsync(Ct);
        }

        var db = new NpgsqlConnectionStringBuilder(containers.Postgres.GetConnectionString()) { Database = ad }.ConnectionString;
        var factory = new AIHubFactory(containers) { ConsumerEnabled = false, PostgresConnectionOverride = db };
        _factoryler.Add(factory);
        var istemci = factory.PanelIstemcisi();
        var simdi = DateTimeOffset.UtcNow.AddMinutes(-5);
        foreach (var servis in new[] { "kargo", "Odeme", "demoa", "demo-b" })
        {
            await EkleAsync(db, servis, simdi, "Error", adet: 3);
        }

        var servisler = (await OzetAsync(istemci)).GetProperty("topErrorServices").EnumerateArray()
            .Select(s => s.GetProperty("serviceName").GetString()).ToArray();

        Assert.Equal(["Odeme", "demo-b", "demoa", "kargo"], servisler);
    }

    [Fact]
    public async Task Sik_exceptionlar_ilk_5_esitlikte_son_gorulen_once_exceptionsiz_kayit_sayilmaz()
    {
        var (istemci, db) = await HazirlaAsync();
        var simdi = DateTimeOffset.UtcNow;
        await EkleAsync(db, "odeme", simdi.AddMinutes(-10), exceptionType: "A", adet: 4);
        await EkleAsync(db, "odeme", simdi.AddMinutes(-30), exceptionType: "B", adet: 3);
        await EkleAsync(db, "odeme", simdi.AddMinutes(-2), exceptionType: "C", adet: 3);
        await EkleAsync(db, "odeme", simdi.AddMinutes(-40), exceptionType: "D", adet: 2);
        await EkleAsync(db, "odeme", simdi.AddMinutes(-50), exceptionType: "E", adet: 2);
        await EkleAsync(db, "odeme", simdi.AddMinutes(-60), exceptionType: "F", adet: 1);
        await EkleAsync(db, "odeme", simdi.AddMinutes(-1), exceptionType: null, adet: 20);
        await EkleAsync(db, "odeme", simdi.AddDays(-2), exceptionType: "Eski", adet: 30);

        var turler = (await OzetAsync(istemci)).GetProperty("topExceptionTypes").EnumerateArray().ToArray();

        Assert.Equal(["A", "C", "B", "D", "E"], turler.Select(t => t.GetProperty("exceptionType").GetString()));
        Assert.Equal([4L, 3L, 3L, 2L, 2L], turler.Select(t => t.GetProperty("count").GetInt64()));
        var cSonGorulme = turler[1].GetProperty("lastSeenAt").GetDateTimeOffset();
        Assert.InRange(cSonGorulme, simdi.AddMinutes(-2).AddSeconds(-1), simdi.AddMinutes(-2).AddSeconds(1));
    }

    [Fact]
    public async Task Bos_depoda_dort_derece_0_ve_listeler_bos_doner()
    {
        var (istemci, _) = await HazirlaAsync();

        var ozet = await OzetAsync(istemci);

        Assert.All(ozet.GetProperty("levels").EnumerateArray(), l => Assert.Equal(0, l.GetProperty("count").GetInt64()));
        Assert.Equal(4, ozet.GetProperty("levels").GetArrayLength());
        Assert.Equal(0, ozet.GetProperty("topErrorServices").GetArrayLength());
        Assert.Equal(0, ozet.GetProperty("topExceptionTypes").GetArrayLength());
    }

    // AC-6: dashboard'daki sayı, aynı pencere ve filtrelerle açılan listede sayfalar boyunca sayılan kayıtlara eşittir.
    [Fact]
    public async Task Ozetteki_sayilar_ayni_pencere_ve_filtrelerle_listede_sayfalar_boyunca_tutar()
    {
        var (istemci, db) = await HazirlaAsync();
        var simdi = DateTimeOffset.UtcNow;
        await EkleAsync(db, "odeme", simdi.AddMinutes(-3), "Error", "System.TimeoutException", adet: 60);
        await EkleAsync(db, "odeme", simdi.AddMinutes(-4), "Critical", adet: 7);
        await EkleAsync(db, "odeme", simdi.AddMinutes(-5), "Info", adet: 9);
        await EkleAsync(db, "stok", simdi.AddMinutes(-6), "Error", "System.TimeoutException", adet: 5);

        var ozet = await OzetAsync(istemci);
        var pencere = $"from={Uri.EscapeDataString(ozet.GetProperty("from").GetString()!)}&to={Uri.EscapeDataString(ozet.GetProperty("to").GetString()!)}";
        var derece = ozet.GetProperty("levels").EnumerateArray().Single(l => l.GetProperty("level").GetString() == "Error").GetProperty("count").GetInt64();
        var servis = ozet.GetProperty("topErrorServices")[0];
        var tur = ozet.GetProperty("topExceptionTypes")[0];

        Assert.Equal(derece, await SayfalarBoyuncaSayAsync(istemci, $"{pencere}&level=Error"));
        Assert.Equal(servis.GetProperty("count").GetInt64(), await SayfalarBoyuncaSayAsync(istemci, $"{pencere}&service=odeme&level=Error&level=Critical"));
        Assert.Equal(tur.GetProperty("count").GetInt64(), await SayfalarBoyuncaSayAsync(istemci, $"{pencere}&exceptionType={Uri.EscapeDataString(tur.GetProperty("exceptionType").GetString()!)}"));
        Assert.Equal((65L, 67L, 65L), (derece, servis.GetProperty("count").GetInt64(), tur.GetProperty("count").GetInt64()));
    }

    private static async Task<long> SayfalarBoyuncaSayAsync(HttpClient istemci, string sorgu)
    {
        long toplam = 0;
        string? imlec = null;
        do
        {
            var adres = $"/api/panel/logs?{sorgu}{(imlec is null ? "" : "&cursor=" + Uri.EscapeDataString(imlec))}";
            var sayfa = await istemci.GetFromJsonAsync<JsonElement>(adres, Ct);
            toplam += sayfa.GetProperty("items").GetArrayLength();
            imlec = sayfa.GetProperty("older").GetString();
        }
        while (imlec is not null);

        return toplam;
    }
}
