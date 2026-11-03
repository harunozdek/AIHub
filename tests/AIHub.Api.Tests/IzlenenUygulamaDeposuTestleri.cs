using AIHub.Api.Tests.Altyapi;
using AIHub.Application.Abstractions;
using AIHub.Application.Monitoring;
using AIHub.Domain.Monitoring;
using Microsoft.Extensions.DependencyInjection;

namespace AIHub.Api.Tests;

// Spec 0005 — R-4 ve AC-7 (bir aralıkta bir kez, birden çok örnekte de), AC-11 (sınır yarışta aşılmaz),
// plan 0005 risk 5 (adres değiştiyse eski sonuç atılır). Kendi boş veritabanında çalışır.
[Trait("Category", "Integration")]
[Collection(IngestionCollection.Name)]
public sealed class IzlenenUygulamaDeposuTestleri(ContainerFixture containers) : IAsyncLifetime
{
    private static readonly DateTimeOffset Simdi = new(2026, 11, 2, 9, 0, 0, TimeSpan.Zero);
    private AIHubFactory _factory = null!;

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public async ValueTask InitializeAsync()
    {
        _factory = new AIHubFactory(containers)
        {
            ConsumerEnabled = false,
            PostgresConnectionOverride = await TestVerisi.BosVeritabaniAsync(containers, Ct),
        };
        _ = _factory.Services;
    }

    public ValueTask DisposeAsync() => _factory.DisposeAsync();

    private async Task<T> DepoIleAsync<T>(Func<IMonitoredAppRepository, Task<T>> islem)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        return await islem(scope.ServiceProvider.GetRequiredService<IMonitoredAppRepository>());
    }

    private static MonitoredApp Uygulama(string ad) =>
        MonitoredApp.Create(Guid.CreateVersion7(), ad, HealthUrl.Create($"http://{ad}.kurum.local/health").Value, Simdi, Guid.CreateVersion7()).Value;

    [Fact]
    public async Task Ayni_anda_sahiplenen_baglantilar_her_uygulamayi_bir_kez_alir()
    {
        const int Adet = 40;
        for (var i = 0; i < Adet; i++)
        {
            Assert.Equal(AddMonitoredAppOutcome.Added, await DepoIleAsync(d => d.AddAsync(Uygulama($"u{i}"), 200, Ct)));
        }

        for (var tur = 0; tur < 5; tur++)
        {
            var an = Simdi.AddMinutes(tur);
            var sonuclar = await Task.WhenAll(Enumerable.Range(0, 8).Select(_ =>
                Task.Run(() => DepoIleAsync(d => d.ClaimDueAsync(an, TimeSpan.FromSeconds(30), 200, Ct)), Ct)));

            var alinan = sonuclar.SelectMany(s => s).Select(s => s.Id).ToList();
            Assert.Equal(Adet, alinan.Count);
            Assert.Equal(Adet, alinan.Distinct().Count());
        }
    }

    [Fact]
    public async Task Sahiplenilen_uygulama_aralik_dolmadan_yeniden_alinmaz()
    {
        await DepoIleAsync(d => d.AddAsync(Uygulama("tek"), 200, Ct));

        Assert.Single(await DepoIleAsync(d => d.ClaimDueAsync(Simdi, TimeSpan.FromSeconds(30), 200, Ct)));
        Assert.Empty(await DepoIleAsync(d => d.ClaimDueAsync(Simdi.AddSeconds(29), TimeSpan.FromSeconds(30), 200, Ct)));
        Assert.Single(await DepoIleAsync(d => d.ClaimDueAsync(Simdi.AddSeconds(30), TimeSpan.FromSeconds(30), 200, Ct)));
    }

    // Review 1, F-01: sonraki denetim çizelgeden ilerler; worker turunun gecikmesi aralığa eklenmez.
    [Fact]
    public async Task Gec_sahiplenme_sonraki_denetimi_kaydirmaz_cok_geride_kalan_simdiden_baslar()
    {
        await DepoIleAsync(d => d.AddAsync(Uygulama("cizelge"), 200, Ct));
        var aralik = TimeSpan.FromSeconds(30);

        // Zamanı Simdi; worker 4 sn geç uyanır. Sonraki denetim yine Simdi + 30'dur, Simdi + 34 değil.
        Assert.Single(await DepoIleAsync(d => d.ClaimDueAsync(Simdi.AddSeconds(4), aralik, 200, Ct)));
        Assert.Single(await DepoIleAsync(d => d.ClaimDueAsync(Simdi.AddSeconds(30), aralik, 200, Ct)));

        // Çizelge (Simdi + 60) çok geride kaldı (ör. yeniden başlatma): sonraki denetim şimdiden bir aralık sonradır.
        Assert.Single(await DepoIleAsync(d => d.ClaimDueAsync(Simdi.AddMinutes(10), aralik, 200, Ct)));
        Assert.Empty(await DepoIleAsync(d => d.ClaimDueAsync(Simdi.AddMinutes(10).AddSeconds(29), aralik, 200, Ct)));
        Assert.Single(await DepoIleAsync(d => d.ClaimDueAsync(Simdi.AddMinutes(10).AddSeconds(30), aralik, 200, Ct)));
    }

    // Review 1, F-02: adres değişimi, aynı anda kaydedilen eski adresin sonucunu kaybetmez, onun yerine
    // geçer. Sonuç satır kilidini tutarken (Down uygulanmış, henüz commit edilmemiş) adres değiştirilir.
    [Fact]
    public async Task Sonuc_kaydi_surerken_adres_degisirse_durum_Bilinmiyor_olur()
    {
        var uygulama = Uygulama("yaris");
        await DepoIleAsync(d => d.AddAsync(uygulama, 200, Ct));
        for (var i = 1; i <= 2; i++)
        {
            await DepoIleAsync(d => d.RecordResultAsync(uygulama.Id, uygulama.HealthUrl, a => a.RecordFailure(HealthFailureReason.Timeout, null, Simdi.AddSeconds(i)), Ct));
        }

        using var kilitAlindi = new ManualResetEventSlim();
        using var birak = new ManualResetEventSlim();
        var kayit = Task.Run(() => DepoIleAsync(d => d.RecordResultAsync(uygulama.Id, uygulama.HealthUrl, a =>
        {
            a.RecordFailure(HealthFailureReason.Timeout, null, Simdi.AddSeconds(3));
            kilitAlindi.Set();
            birak.Wait(TimeSpan.FromSeconds(10), Ct);
        }, Ct)), Ct);
        Assert.True(kilitAlindi.Wait(TimeSpan.FromSeconds(10), Ct));

        var degisim = Task.Run(async () =>
        {
            await using var scope = _factory.Services.CreateAsyncScope();
            return await scope.ServiceProvider.GetRequiredService<UpdateMonitoredAppHandler>()
                .HandleAsync(new UpdateMonitoredAppCommand(uygulama.Id, "http://10.0.0.9/health", Guid.CreateVersion7()), Ct);
        }, Ct);
        // Değişim kilidi beklerken (ya da kilitsiz okuduysa yazmayı beklerken) kayıt commit edilir.
        await Task.Delay(TimeSpan.FromMilliseconds(500), Ct);
        birak.Set();

        Assert.True(await kayit);
        Assert.True((await degisim).IsSuccess);
        var okunan = Assert.Single(await DepoIleAsync(d => d.ListAsync(Ct)));
        Assert.Equal("http://10.0.0.9/health", okunan.HealthUrl);
        Assert.Equal(HealthStatus.Unknown, okunan.Status);
        Assert.Null(okunan.DownSince);
        Assert.Equal(0, okunan.ConsecutiveFailures);
    }

    [Fact]
    public async Task Ayni_anda_eklemelerde_sinir_asilmaz_ve_ayni_ad_bir_kez_eklenir()
    {
        var sonuclar = await Task.WhenAll(Enumerable.Range(0, 12).Select(i =>
            Task.Run(() => DepoIleAsync(d => d.AddAsync(Uygulama(i % 2 == 0 ? "ayni" : $"farkli{i}"), 5, Ct)), Ct)));

        Assert.Equal(5, sonuclar.Count(s => s == AddMonitoredAppOutcome.Added));
        Assert.Equal(5, (await DepoIleAsync(d => d.ListAsync(Ct))).Count);
        Assert.Single(await DepoIleAsync(d => d.ListAsync(Ct)), a => a.ServiceName == "ayni");
    }

    [Fact]
    public async Task Sonuc_kaydi_adres_degistiyse_atilir_degismediyse_durum_kalici_olur()
    {
        var uygulama = Uygulama("kayit");
        await DepoIleAsync(d => d.AddAsync(uygulama, 200, Ct));

        Assert.False(await DepoIleAsync(d => d.RecordResultAsync(uygulama.Id, "http://eski.kurum.local/", a => a.RecordSuccess(Simdi), Ct)));
        for (var i = 1; i <= 3; i++)
        {
            Assert.True(await DepoIleAsync(d => d.RecordResultAsync(
                uygulama.Id, uygulama.HealthUrl, a => a.RecordFailure(HealthFailureReason.HttpStatus, 503, Simdi.AddSeconds(30 * i)), Ct)));
        }

        var okunan = Assert.Single(await DepoIleAsync(d => d.ListAsync(Ct)));
        Assert.Equal(HealthStatus.Down, okunan.Status);
        Assert.Equal(Simdi.AddSeconds(90), okunan.DownSince);
        Assert.Equal(HealthFailureReason.HttpStatus, okunan.LastFailureReason);
        Assert.Equal(503, okunan.LastFailureStatusCode);
    }
}
