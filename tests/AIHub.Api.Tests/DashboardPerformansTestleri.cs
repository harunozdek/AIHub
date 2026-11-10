using System.Diagnostics;
using System.Net;
using AIHub.Api.Tests.Altyapi;
using Npgsql;

namespace AIHub.Api.Tests;

// Kendi container'ları olan ayrı bir koleksiyon: 24 saate sığan 1 milyon satır, 0002'nin performans verisine
// (30 güne yayılmış) karışmaz. DisableParallelization: ölçüme paralel testlerin yükü girmez.
[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class DashboardPerformansCollection : ICollectionFixture<ContainerFixture>
{
    public const string Name = "DashboardPerformans";
}

// Spec 0006, AC-12 — son 24 saatte 1 milyon kayıt varken dashboard'un log verisi en geç 2 saniyede gelir.
[Trait("Category", "Integration")]
[Collection(DashboardPerformansCollection.Name)]
public sealed class DashboardPerformansTestleri(ContainerFixture containers) : IAsyncLifetime
{
    private const int KayitSayisi = 1_000_000;
    private static readonly TimeSpan Esik = TimeSpan.FromSeconds(2);
    private readonly AIHubFactory _factory = new(containers) { ConsumerEnabled = false };

    public async ValueTask InitializeAsync()
    {
        _ = _factory.CreateClient();

        // Kayıtlar son 23 saate eşit yayılır (ölçüm anında pencerenin içinde kalır). 50 servis, dört LogLevel ve
        // her 3 kayıttan birinde 40 türden bir ExceptionType.
        await using var ds = NpgsqlDataSource.Create(containers.Postgres.GetConnectionString());
        await using var yukle = ds.CreateCommand(
            """
            INSERT INTO log_entries (id, service_name, level, message, exception_type, received_at)
            SELECT gen_random_uuid(),
                   'dash-svc-' || (i % 50),
                   (ARRAY['Info','Warning','Error','Critical'])[1 + i % 4],
                   'dashboard performans kaydı ' || i,
                   CASE WHEN i % 3 = 0 THEN 'System.Ornek' || (i % 40) || 'Exception' END,
                   now() - i * interval '82.8 milliseconds'
            FROM generate_series(1, $1) AS i
            """);
        yukle.CommandTimeout = 300;
        yukle.Parameters.AddWithValue(KayitSayisi);
        await yukle.ExecuteNonQueryAsync();

        await using var analiz = ds.CreateCommand("ANALYZE log_entries");
        await analiz.ExecuteNonQueryAsync();
    }

    public ValueTask DisposeAsync() => _factory.DisposeAsync();

    [Fact]
    public async Task Son_24_saatte_bir_milyon_kayitta_dashboard_2_sn_icinde_gelir()
    {
        var client = _factory.PanelIstemcisi();

        // Isınma: JIT, bağlantı havuzu ve token doğrulama ön belleği; ölçülen sorgunun kendisidir.
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/panel/dashboard", TestContext.Current.CancellationToken)).StatusCode);

        // Ölçüm 3 koşunun medyanıdır (plan 0002, R-12 ile aynı yöntem).
        var sureler = new List<TimeSpan>();
        for (var i = 0; i < 3; i++)
        {
            var saat = Stopwatch.StartNew();
            var yanit = await client.GetAsync("/api/panel/dashboard", TestContext.Current.CancellationToken);
            var govde = await yanit.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
            saat.Stop();

            Assert.Equal(HttpStatusCode.OK, yanit.StatusCode);
            Assert.Contains("\"dash-svc-", govde, StringComparison.Ordinal);
            Assert.Contains("\"System.Ornek", govde, StringComparison.Ordinal);
            sureler.Add(saat.Elapsed);
        }

        var medyan = sureler.Order().ElementAt(1);
        Assert.True(medyan < Esik, $"Dashboard'un medyan süresi {medyan.TotalMilliseconds:F0} ms (ölçümler: {string.Join(", ", sureler.Select(s => $"{s.TotalMilliseconds:F0}"))} ms).");
        TestContext.Current.TestOutputHelper?.WriteLine($"dashboard: medyan {medyan.TotalMilliseconds:F0} ms");
    }
}
