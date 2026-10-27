using System.Net;
using AIHub.Api.Tests.Altyapi;
using AIHub.Application.Abstractions;
using AIHub.Domain.Logs;
using Microsoft.Extensions.DependencyInjection;

namespace AIHub.Api.Tests;

// Spec 0001 — AC-9 (kayıpsızlık) ve AC-10 (kuyruk / anahtar deposu erişilemez).
[Trait("Category", "Integration")]
[Collection(IngestionCollection.Name)]
public sealed class DayaniklilikTestleri(ContainerFixture containers)
{
    private static readonly TimeSpan Uzun = TimeSpan.FromSeconds(60);

    [Fact]
    public async Task Depoya_yazilamayan_bozuk_girdi_dead_lettera_tasinir_ve_arkasindaki_kayitlari_durdurmaz()
    {
        var servis = TestVerisi.YeniServisAdi();
        await using var factory = new AIHubFactory(containers);
        var client = await factory.AnahtarliIstemciAsync(containers, servis);

        // Api sınırını atlayıp kuyruğa doğrudan, depo tarafından kalıcı olarak reddedilecek bir girdi
        // (NUL içeren Message → SQLSTATE 22021) yazılır; ardından normal bir gönderim yapılır.
        var zehirli = new AIHub.Infrastructure.Queue.QueuedLogEntry(Guid.CreateVersion7(), servis, "Error", "a\0b",
            null, null, null, null, DateTimeOffset.UtcNow);
        await using (var redis = await StackExchange.Redis.ConnectionMultiplexer.ConnectAsync(containers.Redis.GetConnectionString()))
        {
            await redis.GetDatabase().StreamAddAsync(TestVerisi.StreamKey, "payload", System.Text.Json.JsonSerializer.Serialize(zehirli));
        }

        Assert.Equal(HttpStatusCode.Accepted, (await client.GonderAsync(new { level = "Info", message = "normal" })).StatusCode);

        Assert.True(
            await TestVerisi.BekleAsync(async () => (await TestVerisi.KayitlariOkuAsync(containers, servis)).Any(k => k.Message == "normal"), TimeSpan.FromSeconds(5)),
            "Bozuk girdinin arkasındaki kayıt 5 saniye içinde yazılmadı.");
        Assert.True(await TestVerisi.BekleAsync(
            async () => (await TestVerisi.DeadLetterIcerigiAsync(containers)).Any(p => p.Contains(zehirli.Id.ToString(), StringComparison.Ordinal)),
            TimeSpan.FromSeconds(5)));
        Assert.DoesNotContain(await TestVerisi.KayitlariOkuAsync(containers, servis), k => k.Id == zehirli.Id);
    }

    [Fact]
    public async Task AC9_Depo_yazim_sirasinda_erisilemezse_kabul_edilenler_geri_gelince_birer_kez_yazilir()
    {
        var servis = TestVerisi.YeniServisAdi();

        // 1) Depo ayaktayken kabul: tüketici kapalı, kayıtlar kuyrukta bekler.
        await using (var kabul = new AIHubFactory(containers) { ConsumerEnabled = false })
        {
            var client = await kabul.AnahtarliIstemciAsync(containers, servis);
            for (var i = 0; i < 3; i++)
            {
                Assert.Equal(HttpStatusCode.Accepted, (await client.GonderAsync(new { level = "Error", message = $"kayit-{i}" })).StatusCode);
            }
        }

        // 2) Depo gerçekten erişilemez olur; tüketici kayıtları okur ama yazamaz → onaylamaz.
        // Dondurulmuş container'ın port eşlemesi okunamaz: bağlantı dizesi önceden alınır.
        var postgres = containers.Postgres.GetConnectionString();
        await containers.Postgres.PauseAsync(TestContext.Current.CancellationToken);
        var tuketici = new AIHubFactory(containers) { MigrateOnStartup = false, PostgresConnectionOverride = postgres };
        try
        {
            _ = tuketici.CreateClient();
            Assert.True(
                await TestVerisi.BekleAsync(async () => await TestVerisi.BekleyenSayisiAsync(containers, tuketici.ConsumerName) >= 3, Uzun),
                "Tüketici, depo erişilemezken kayıtları beklemede tutmadı.");
        }
        finally
        {
            // 3) Depo geri gelir.
            await containers.Postgres.UnpauseAsync(TestContext.Current.CancellationToken);
        }

        try
        {
            Assert.True(
                await TestVerisi.BekleAsync(async () => (await TestVerisi.KayitlariOkuAsync(containers, servis)).Count == 3, Uzun),
                "Depo geri geldikten sonra kabul edilmiş kayıtlar yazılmadı.");
            await Task.Delay(500, TestContext.Current.CancellationToken);
            var kayitlar = await TestVerisi.KayitlariOkuAsync(containers, servis);
            Assert.Equal(["kayit-0", "kayit-1", "kayit-2"], kayitlar.Select(k => k.Message).Order());
            Assert.Equal(3, kayitlar.Select(k => k.Id).Distinct().Count());
            Assert.True(await TestVerisi.BekleAsync(async () => await TestVerisi.BekleyenSayisiAsync(containers, tuketici.ConsumerName) == 0, Uzun));
        }
        finally
        {
            await tuketici.DisposeAsync();
        }
    }

    [Fact]
    public async Task AC9_Surec_kayitlari_islemeden_yeniden_baslarsa_bekleyen_kayitlar_kaybolmaz()
    {
        var servis = TestVerisi.YeniServisAdi();
        var tuketiciAdi = "yeniden-baslatma-" + Guid.NewGuid().ToString("N")[..8];

        // 1) Kayıtları okuyup yazamadan (depo yazımı hep başarısız) kapanan süreç.
        await using (var ilk = new AIHubFactory(containers)
        {
            ConsumerName = tuketiciAdi,
            TestServices = s => s.AddScoped<ILogEntryRepository, HepBasarisizDepo>(),
        })
        {
            var client = await ilk.AnahtarliIstemciAsync(containers, servis);
            Assert.Equal(HttpStatusCode.Accepted, (await client.GonderAsync(new { level = "Critical", message = "a" })).StatusCode);
            Assert.Equal(HttpStatusCode.Accepted, (await client.GonderAsync(new { level = "Critical", message = "b" })).StatusCode);
            Assert.True(await TestVerisi.BekleAsync(async () => await TestVerisi.BekleyenSayisiAsync(containers, tuketiciAdi) >= 2, Uzun));
        }

        Assert.Empty(await TestVerisi.KayitlariOkuAsync(containers, servis));

        // 2) Aynı tüketici adıyla yeniden başlayan süreç bekleyenleri yazar.
        await using var ikinci = new AIHubFactory(containers) { ConsumerName = tuketiciAdi };
        _ = ikinci.CreateClient();

        Assert.True(await TestVerisi.BekleAsync(async () => (await TestVerisi.KayitlariOkuAsync(containers, servis)).Count == 2, Uzun));
        await Task.Delay(500, TestContext.Current.CancellationToken);
        Assert.Equal(["a", "b"], (await TestVerisi.KayitlariOkuAsync(containers, servis)).Select(k => k.Message).Order());
    }

    [Fact]
    public async Task AC10_Kuyruk_erisilemezse_503_ve_Retry_After_alir_ic_ayrinti_yok_kayit_olusmaz()
    {
        var servis = TestVerisi.YeniServisAdi();
        await using var factory = new AIHubFactory(containers)
        {
            ConsumerEnabled = false,
            RedisConnectionOverride = "127.0.0.1:1,connectTimeout=500",
        };
        var client = await factory.AnahtarliIstemciAsync(containers, servis);

        var yanit = await client.GonderAsync(new { level = "Error", message = "kuyruk yok" });

        Assert.Equal(HttpStatusCode.ServiceUnavailable, yanit.StatusCode);
        Assert.Equal(TimeSpan.FromSeconds(5), yanit.Headers.RetryAfter?.Delta);
        Assert.Equal("application/problem+json", yanit.Content.Headers.ContentType?.MediaType);
        IcAyrintiIcermez(await yanit.Content.ReadAsStringAsync(TestContext.Current.CancellationToken), "127.0.0.1:1", "Redis");
        Assert.Empty(await TestVerisi.KayitlariOkuAsync(containers, servis));
    }

    [Fact]
    public async Task AC10_Anahtar_deposu_erisilemezse_503_ve_Retry_After_alir_ic_ayrinti_yok()
    {
        const string olmayanDepo = "Host=127.0.0.1;Port=1;Username=yok;Password=GizliParola9;Timeout=2";
        await using var factory = new AIHubFactory(containers)
        {
            ConsumerEnabled = false,
            MigrateOnStartup = false,
            PostgresConnectionOverride = olmayanDepo,
        };
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Api-Key", "aih_herhangi_bir_anahtar_000001");

        var yanit = await client.GonderAsync(new { level = LogLevel.Error.ToString(), message = "depo yok" });

        Assert.Equal(HttpStatusCode.ServiceUnavailable, yanit.StatusCode);
        Assert.Equal(TimeSpan.FromSeconds(5), yanit.Headers.RetryAfter?.Delta);
        Assert.Equal("application/problem+json", yanit.Content.Headers.ContentType?.MediaType);
        IcAyrintiIcermez(await yanit.Content.ReadAsStringAsync(TestContext.Current.CancellationToken), "GizliParola9", "Port=1", "aih_herhangi_bir_anahtar_000001");
    }

    [Fact]
    public async Task AC10_Anahtar_deposu_yanit_vermezse_yaklasik_2_sn_icinde_503_alir()
    {
        var servis = TestVerisi.YeniServisAdi();
        var postgres = containers.Postgres.GetConnectionString();
        await using var factory = new AIHubFactory(containers) { ConsumerEnabled = false, PostgresConnectionOverride = postgres };
        var client = await factory.AnahtarliIstemciAsync(containers, servis);
        var isaret = "isaret-" + Guid.NewGuid();

        // Depo donuk: TCP bağlantısı kurulur ama yanıt gelmez — "yavaş/erişilemez" depo (review F-6).
        await containers.Postgres.PauseAsync(TestContext.Current.CancellationToken);
        HttpResponseMessage yanit;
        var sure = System.Diagnostics.Stopwatch.StartNew();
        try
        {
            yanit = await client.GonderAsync(new { level = "Error", message = isaret });
            sure.Stop();
        }
        finally
        {
            await containers.Postgres.UnpauseAsync(TestContext.Current.CancellationToken);
        }

        Assert.Equal(HttpStatusCode.ServiceUnavailable, yanit.StatusCode);
        Assert.Equal(TimeSpan.FromSeconds(5), yanit.Headers.RetryAfter?.Delta);
        Assert.True(sure.Elapsed < TimeSpan.FromSeconds(5), $"503 yanıtı {sure.Elapsed} sürdü.");
        IcAyrintiIcermez(await yanit.Content.ReadAsStringAsync(TestContext.Current.CancellationToken), "Password", "Host=");
        Assert.DoesNotContain(await TestVerisi.StreamIcerigiAsync(containers), p => p.Contains(isaret, StringComparison.Ordinal));
    }

    [Fact]
    public async Task Redis_tuketici_grubunu_kaybederse_grup_yeniden_kurulur_ve_yazim_surer()
    {
        var servis = TestVerisi.YeniServisAdi();
        await using var factory = new AIHubFactory(containers);
        var client = await factory.AnahtarliIstemciAsync(containers, servis);
        Assert.Equal(HttpStatusCode.Accepted, (await client.GonderAsync(new { level = "Info", message = "once" })).StatusCode);
        Assert.True(await TestVerisi.BekleAsync(async () => (await TestVerisi.KayitlariOkuAsync(containers, servis)).Count == 1, TimeSpan.FromSeconds(5)));

        // Stream silinince grup da gider (ör. Redis verisini kaybetti); tüketici NOGROUP alır.
        await using (var redis = await StackExchange.Redis.ConnectionMultiplexer.ConnectAsync(containers.Redis.GetConnectionString()))
        {
            await redis.GetDatabase().KeyDeleteAsync(TestVerisi.StreamKey);
        }

        Assert.Equal(HttpStatusCode.Accepted, (await client.GonderAsync(new { level = "Info", message = "sonra" })).StatusCode);
        Assert.True(
            await TestVerisi.BekleAsync(async () => (await TestVerisi.KayitlariOkuAsync(containers, servis)).Count == 2, TimeSpan.FromSeconds(10)),
            "Grup kaybından sonraki kayıt yazılmadı.");
    }

    private static void IcAyrintiIcermez(string govde, params string[] yasakli)
    {
        Assert.DoesNotContain("Exception", govde);
        Assert.DoesNotContain("   at ", govde);
        foreach (var metin in yasakli)
        {
            Assert.DoesNotContain(metin, govde);
        }
    }
}

internal sealed class HepBasarisizDepo : ILogEntryRepository
{
    public Task AddIfAbsentAsync(LogEntry entry, CancellationToken cancellationToken) =>
        throw new TimeoutException("Test: depo yazımı başarısız.");
}
