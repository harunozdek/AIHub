using AIHub.Api.Tests.Altyapi;
using AIHub.Application.Abstractions;
using AIHub.Domain.Monitoring;
using Microsoft.Extensions.DependencyInjection;

namespace AIHub.Api.Tests;

// R-05 (review 1, F-03; yeniden üretilemedi, kapatıldı, insan kararı harun.ozdek 2026-11-03) kalıcı güvence: ClaimDueAsync'in `IN (... FOR UPDATE SKIP LOCKED)` biçimi
// yarışta bir satırı kilitleyip güncellemeden bırakıyor mu (o turda kimse almıyor mu)? Spec 0005 R-4, AC-7.
// Mevcut testten daha çok satır, tur ve eşzamanlılık; tüm çağrılar aynı anda bir kapıdan bırakılır.
[Trait("Category", "Integration")]
[Collection(IngestionCollection.Name)]
public sealed class SahiplenmeYarisiTestleri(ContainerFixture containers) : IAsyncLifetime
{
    private static readonly DateTimeOffset Simdi = new(2026, 11, 2, 9, 0, 0, TimeSpan.Zero);
    private static readonly TimeSpan Aralik = TimeSpan.FromSeconds(30);
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

    public async ValueTask DisposeAsync()
    {
        await _factory.DisposeAsync();
        // Eşzamanlı bağlantıların havuzda boşta kalıp sonraki testlerin konteynerinde
        // "53300 too many clients" üretmemesi için havuzlar boşaltılır.
        Npgsql.NpgsqlConnection.ClearAllPools();
    }

    private async Task<T> DepoIleAsync<T>(Func<IMonitoredAppRepository, Task<T>> islem)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        return await islem(scope.ServiceProvider.GetRequiredService<IMonitoredAppRepository>());
    }

    private async Task UygulamalarEkleAsync(int adet)
    {
        for (var i = 0; i < adet; i++)
        {
            var ad = $"y{i}";
            var uygulama = MonitoredApp.Create(
                Guid.CreateVersion7(), ad, HealthUrl.Create($"http://{ad}.kurum.local/health").Value, Simdi, Guid.CreateVersion7()).Value;
            Assert.Equal(AddMonitoredAppOutcome.Added, await DepoIleAsync(d => d.AddAsync(uygulama, 200, Ct)));
        }
    }

    /// <summary>Her çağrı kendi scope'unda (ayrı bağlantı) hazırlanır, kapı açılınca hepsi aynı anda sahiplenir.</summary>
    private async Task<List<Guid>[]> AyniAndaSahiplenAsync(int paralel, DateTimeOffset an, int max)
    {
        var kapi = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var gorevler = Enumerable.Range(0, paralel).Select(_ => Task.Run(async () =>
        {
            await using var scope = _factory.Services.CreateAsyncScope();
            var depo = scope.ServiceProvider.GetRequiredService<IMonitoredAppRepository>();
            await kapi.Task;
            return (await depo.ClaimDueAsync(an, Aralik, max, Ct)).Select(s => s.Id).ToList();
        }, Ct)).ToArray();
        kapi.SetResult();
        return await Task.WhenAll(gorevler);
    }

    [Fact]
    public async Task Cok_turda_cok_baglanti_zamani_gelen_her_uygulamayi_tam_bir_kez_alir()
    {
        const int Adet = 200;
        const int Paralel = 16;
        const int Tur = 60;
        await UygulamalarEkleAsync(Adet);
        var bolunenTur = 0;

        for (var tur = 0; tur < Tur; tur++)
        {
            var an = Simdi.AddMinutes(tur);
            var sonuclar = await AyniAndaSahiplenAsync(Paralel, an, 200);
            if (sonuclar.Count(s => s.Count > 0) > 1)
            {
                bolunenTur++;
            }

            var alinan = sonuclar.SelectMany(s => s).ToList();
            var kalan = await DepoIleAsync(d => d.ClaimDueAsync(an, Aralik, 200, Ct));
            Assert.True(
                alinan.Count == Adet && alinan.Distinct().Count() == Adet && kalan.Count == 0,
                $"tur {tur}: alinan={alinan.Count}, tekil={alinan.Distinct().Count()}, sonradan kalan zamani gelmis={kalan.Count}");
        }

        // Yarış gerçekten oluştu mu: satırlar en az bir turda birden çok bağlantıya bölünmüş olmalı.
        TestContext.Current.TestOutputHelper?.WriteLine($"satirlari birden cok baglantiya bolunen tur: {bolunenTur}/{Tur}");
        Assert.True(bolunenTur > 0, "hicbir turda yaris olusmadi; test zayif");
    }

    [Fact]
    public async Task Kucuk_sinirla_yarista_hicbir_uygulama_iki_kez_alinmaz_ve_kaybolmaz()
    {
        const int Adet = 200;
        const int Paralel = 16;
        const int Tur = 60;
        await UygulamalarEkleAsync(Adet);

        for (var tur = 0; tur < Tur; tur++)
        {
            var an = Simdi.AddMinutes(tur);
            var alinan = (await AyniAndaSahiplenAsync(Paralel, an, 7)).SelectMany(s => s).ToList();
            var kalan = await DepoIleAsync(d => d.ClaimDueAsync(an, Aralik, 200, Ct));

            var hepsi = alinan.Concat(kalan.Select(k => k.Id)).ToList();
            Assert.True(
                alinan.Distinct().Count() == alinan.Count && hepsi.Count == Adet && hepsi.Distinct().Count() == Adet,
                $"tur {tur}: alinan={alinan.Count}, tekil={alinan.Distinct().Count()}, kalan={kalan.Count}, toplam tekil={hepsi.Distinct().Count()}");
        }
    }
}
