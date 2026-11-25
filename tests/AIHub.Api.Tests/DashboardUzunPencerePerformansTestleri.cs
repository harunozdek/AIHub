using System.Diagnostics;
using System.Net;
using AIHub.Api.Tests.Altyapi;
using Npgsql;

namespace AIHub.Api.Tests;

// Kendi container'ları olan ayrı bir koleksiyon: 10 milyon satır, diğer performans koleksiyonlarının verisine
// karışmaz. DisableParallelization: ölçüme paralel testlerin yükü girmez (plan 0011, risk 5).
[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class DashboardUzunPencerePerformansCollection : ICollectionFixture<ContainerFixture>
{
    public const string Name = "DashboardUzunPencerePerformans";
}

// Spec 0011, CB-5 — pencerede 10 milyon kayıt varken (seçenek ne olursa olsun) dashboard'un log verisi en geç
// 5 saniyede gelir; S-3: son hatalar ve son uyarılar da bu sınırın içindedir.
[Trait("Category", "Integration")]
[Collection(DashboardUzunPencerePerformansCollection.Name)]
public sealed class DashboardUzunPencerePerformansTestleri(ContainerFixture containers) : IAsyncLifetime
{
    private const int KayitSayisi = 10_000_000;
    private static readonly TimeSpan Esik = TimeSpan.FromSeconds(5);
    private readonly AIHubFactory _factory = new(containers) { ConsumerEnabled = false };

    // Veri koleksiyonun container'ına bir kez yüklenir: her test durumu yeni bir sınıf örneği oluşturur ve yeniden
    // yüklemek pencereyi 20 milyon kayda çıkarırdı. Koleksiyon paralel çalışmaz; kilit yine de korur.
    private static readonly SemaphoreSlim YuklemeKilidi = new(1, 1);
    private static string? _yuklenenVeritabani;

    public async ValueTask InitializeAsync()
    {
        _ = _factory.CreateClient();

        await YuklemeKilidi.WaitAsync(TestContext.Current.CancellationToken);
        try
        {
            if (_yuklenenVeritabani != containers.Postgres.GetConnectionString())
            {
                await YukleAsync();
                _yuklenenVeritabani = containers.Postgres.GetConnectionString();
            }
        }
        finally
        {
            YuklemeKilidi.Release();
        }
    }

    private async Task YukleAsync()
    {

        // Kayıtlar son 23 saate eşit yayılır: aynı veriyle hem en kısa (24 saat) hem en uzun (365 gün) pencere 10
        // milyon kaydı kapsar (plan 0011, risk 5). 50 servis; dereceler Info, Error, Critical — hiç Warning yok:
        // "son uyarılar" bölümü en kötü durumu, yani penceresinde hiç uyarı olmayan 10 milyon kaydı ölçer (risk 2).
        // Her 3 kayıttan birinde 40 türden bir ExceptionType.
        await using var ds = NpgsqlDataSource.Create(containers.Postgres.GetConnectionString());
        await using var yukle = ds.CreateCommand(
            """
            INSERT INTO log_entries (id, service_name, level, message, exception_type, received_at)
            SELECT gen_random_uuid(),
                   'uzun-svc-' || (i % 50),
                   (ARRAY['Info','Error','Critical'])[1 + i % 3],
                   'uzun pencere performans kaydı ' || i,
                   CASE WHEN i % 3 = 0 THEN 'System.Uzun' || (i % 40) || 'Exception' END,
                   now() - i * interval '8.28 milliseconds'
            FROM generate_series(1, $1) AS i
            """);
        yukle.CommandTimeout = 1800;
        yukle.Parameters.AddWithValue(KayitSayisi);
        await yukle.ExecuteNonQueryAsync();

        await using var analiz = ds.CreateCommand("ANALYZE log_entries");
        analiz.CommandTimeout = 600;
        await analiz.ExecuteNonQueryAsync();
    }

    public ValueTask DisposeAsync() => _factory.DisposeAsync();

    [Theory]
    [InlineData("24h")]
    [InlineData("365d")]
    public async Task Pencerede_on_milyon_kayitta_dashboard_5_sn_icinde_gelir(string pencere)
    {
        var client = _factory.PanelIstemcisi();
        var adres = $"/api/panel/dashboard?window={pencere}";

        // Isınma: JIT, bağlantı havuzu ve token doğrulama ön belleği; ölçülen sorgunun kendisidir.
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync(adres, TestContext.Current.CancellationToken)).StatusCode);

        // Ölçüm 3 koşunun medyanıdır (plan 0002, R-12 ile aynı yöntem).
        var sureler = new List<TimeSpan>();
        for (var i = 0; i < 3; i++)
        {
            var saat = Stopwatch.StartNew();
            var yanit = await client.GetAsync(adres, TestContext.Current.CancellationToken);
            var govde = await yanit.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
            saat.Stop();

            Assert.Equal(HttpStatusCode.OK, yanit.StatusCode);
            Assert.Contains("\"uzun-svc-", govde, StringComparison.Ordinal);
            Assert.Contains("\"System.Uzun", govde, StringComparison.Ordinal);
            sureler.Add(saat.Elapsed);
        }

        var medyan = sureler.Order().ElementAt(1);
        TestContext.Current.TestOutputHelper?.WriteLine($"dashboard {pencere}: medyan {medyan.TotalMilliseconds:F0} ms (ölçümler: {string.Join(", ", sureler.Select(s => $"{s.TotalMilliseconds:F0}"))} ms)");
        Assert.True(medyan < Esik, $"Dashboard'un ({pencere}) medyan süresi {medyan.TotalMilliseconds:F0} ms (ölçümler: {string.Join(", ", sureler.Select(s => $"{s.TotalMilliseconds:F0}"))} ms).");
    }
}
