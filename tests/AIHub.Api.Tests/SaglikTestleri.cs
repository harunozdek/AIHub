using System.Diagnostics;
using System.Net;
using AIHub.Api.Tests.Altyapi;

namespace AIHub.Api.Tests;

// Spec 0008 — backend'in sağlık adresi: bağımlılıklar yerindeyken "Healthy" (AC-1), biri erişilemezken ya da yanıt
// vermezken 5 sn'den önce başarısızlık (AC-2), yanıtta ayrıntı yok ve önbelleğe alınmaz (AC-4). Yanıt vermeyen
// bağımlılık, koleksiyonun container'ı duraklatılarak üretilir (DayaniklilikTestleri ile aynı desen); finally
// içinde geri alınır.
[Trait("Category", "Integration")]
[Collection(IngestionCollection.Name)]
public sealed class SaglikTestleri(ContainerFixture containers)
{
    private const string Adres = "/health";
    private static readonly TimeSpan Sinir = TimeSpan.FromSeconds(5);

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static readonly string[] IcAyrintilar = ["Host=", "Npgsql", "Redis", "Exception", "   at ", "localhost", "127.0.0.1", "postgres"];

    [Fact]
    public async Task AC1_Bagimliliklar_yerindeyken_girissiz_Healthy_doner()
    {
        await using var factory = new AIHubFactory(containers) { ConsumerEnabled = false };

        var yanit = await factory.CreateClient().GetAsync(Adres, Ct);

        Assert.Equal(HttpStatusCode.OK, yanit.StatusCode);
        Assert.Equal("Healthy", await yanit.Content.ReadAsStringAsync(Ct));
        Assert.Contains("no-store", yanit.Headers.CacheControl?.ToString());
    }

    [Fact]
    public async Task AC2_AC4_Log_deposu_yoksa_ayrintisiz_Unhealthy_ve_503_doner()
    {
        var yok = $"{containers.Postgres.GetConnectionString()};Database=yok_{Guid.NewGuid():N}";
        await using var factory = new AIHubFactory(containers) { ConsumerEnabled = false, MigrateOnStartup = false, PostgresConnectionOverride = yok };

        await BasarisizlikBekleAsync(factory.CreateClient());
    }

    [Fact]
    public async Task AC2_AC4_Kuyruga_baglanilamiyorsa_ayrintisiz_Unhealthy_ve_503_doner()
    {
        await using var factory = new AIHubFactory(containers) { ConsumerEnabled = false, RedisConnectionOverride = "127.0.0.1:1" };

        await BasarisizlikBekleAsync(factory.CreateClient());
    }

    [Fact]
    public async Task AC2_Log_deposu_yanit_vermezken_5_sn_icinde_Unhealthy_doner()
    {
        // Duraklatılmış container'ın port eşlemesi okunamaz: host önce açılır.
        await using var factory = new AIHubFactory(containers) { ConsumerEnabled = false };
        var istemci = factory.CreateClient();
        Assert.Equal(HttpStatusCode.OK, (await istemci.GetAsync(Adres, Ct)).StatusCode);

        await containers.Postgres.PauseAsync(Ct);
        try
        {
            await BasarisizlikBekleAsync(istemci);
        }
        finally
        {
            await containers.Postgres.UnpauseAsync(Ct);
        }
    }

    [Fact]
    public async Task AC2_Kuyruk_yanit_vermezken_5_sn_icinde_Unhealthy_doner()
    {
        await using var factory = new AIHubFactory(containers) { ConsumerEnabled = false };
        var istemci = factory.CreateClient();
        Assert.Equal(HttpStatusCode.OK, (await istemci.GetAsync(Adres, Ct)).StatusCode);

        await containers.Redis.PauseAsync(Ct);
        try
        {
            await BasarisizlikBekleAsync(istemci);
        }
        finally
        {
            await containers.Redis.UnpauseAsync(Ct);
        }
    }

    private static async Task BasarisizlikBekleAsync(HttpClient istemci)
    {
        var sure = Stopwatch.StartNew();
        var yanit = await istemci.GetAsync(Adres, Ct);
        sure.Stop();

        Assert.Equal(HttpStatusCode.ServiceUnavailable, yanit.StatusCode);
        Assert.True(sure.Elapsed < Sinir, $"Sağlık yanıtı {sure.Elapsed.TotalSeconds:F1} sn sürdü (sınır {Sinir.TotalSeconds} sn).");
        var govde = await yanit.Content.ReadAsStringAsync(Ct);
        Assert.Equal("Unhealthy", govde);
        Assert.All(IcAyrintilar, ic => Assert.DoesNotContain(ic, govde, StringComparison.OrdinalIgnoreCase));
        Assert.Contains("no-store", yanit.Headers.CacheControl?.ToString());
    }
}
