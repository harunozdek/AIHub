using System.Net;
using AIHub.Application.Abstractions;
using AIHub.Application.Monitoring;
using AIHub.Application.Tests.Users;
using AIHub.Domain.Monitoring;
using AIHub.Domain.Users;

namespace AIHub.Application.Tests.Monitoring;

// Spec 0005 — yönetim ve denetim turu (AC-2…AC-7, AC-9, AC-11).
public class IzlemeHandlerTestleri
{
    private static readonly DateTimeOffset Simdi = new(2026, 11, 2, 9, 0, 0, TimeSpan.Zero);
    private const string Adres = "http://odeme.kurum.local/health";

    private readonly SahteIzlenenUygulamaDeposu _depo = new();
    private readonly SahteKullaniciDeposu _kullanicilar = new();
    private readonly SahteCozucu _cozucu = new();
    private readonly SahteProbe _probe = new();
    private readonly AyarlanabilirSaat _saat = new(Simdi);
    private readonly User _ayse;

    public IzlemeHandlerTestleri()
    {
        _ayse = _kullanicilar.Ekle("ayse");
    }

    private Task<AIHub.Domain.Common.Result<Guid>> EkleAsync(string? ad = "odeme-servisi", string? adres = Adres) =>
        new AddMonitoredAppHandler(_depo, _cozucu, _saat).HandleAsync(new AddMonitoredAppCommand(ad, adres, _ayse.Id), CancellationToken.None);

    private Task<int> TurAsync() =>
        new RunHealthChecksHandler(_depo, _probe, _saat).HandleAsync(MonitoringLimits.CheckInterval, CancellationToken.None);

    private MonitoredApp Uygulama(Guid id) => _depo.Apps.Single(a => a.Id == id);

    [Fact]
    public async Task Eklenen_uygulama_Bilinmiyor_durumunda_ekleyenle_listelenir()
    {
        var sonuc = await EkleAsync();

        Assert.True(sonuc.IsSuccess);
        var satir = Assert.Single(await new ListMonitoredAppsHandler(_depo, _kullanicilar).HandleAsync(CancellationToken.None));
        Assert.Equal("odeme-servisi", satir.ServiceName);
        Assert.Equal(HealthStatus.Unknown, satir.Status);
        Assert.Equal("ayse", satir.CreatedBy);
        Assert.Equal(Simdi, satir.CreatedAt);
    }

    [Fact]
    public async Task Ayni_ServiceName_ikinci_kez_eklenemez()
    {
        await EkleAsync();

        var sonuc = await EkleAsync(adres: "http://baska.kurum.local/health");

        Assert.Equal(MonitoringErrors.ServiceNameTaken, Assert.Single(sonuc.Errors));
        Assert.Single(_depo.Apps);
    }

    [Fact]
    public async Task Bos_ServiceName_ve_kural_disi_adres_birlikte_alan_adlariyla_reddedilir()
    {
        var sonuc = await EkleAsync(ad: " ", adres: "ftp://a.local/");

        Assert.Equal(["serviceName", "healthUrl"], sonuc.Errors.Select(e => e.Field));
        Assert.Empty(_depo.Apps);
    }

    [Theory]
    [InlineData("http://169.254.169.254/latest/meta-data")]
    [InlineData("http://[fe80::1]/health")]
    [InlineData("http://metadata.kurum.local/health")]
    public async Task Engelli_adrese_cozulen_adres_kaydedilmez(string adres)
    {
        _cozucu.Kayitlar["metadata.kurum.local"] = [IPAddress.Parse("10.0.0.1"), IPAddress.Parse("169.254.169.254")];

        var sonuc = await EkleAsync(adres: adres);

        Assert.Equal(MonitoringErrors.AddressBlocked, Assert.Single(sonuc.Errors));
        Assert.Empty(_depo.Apps);
    }

    [Fact]
    public async Task Cozulemeyen_ad_kabul_edilir_engel_denetim_aninda_uygulanir()
    {
        var sonuc = await EkleAsync(adres: "http://henuz-kurulmadi.kurum.local/health");

        Assert.True(sonuc.IsSuccess);
    }

    [Fact]
    public async Task Iki_yuzuncu_uygulamadan_sonra_yeni_uygulama_eklenemez()
    {
        for (var i = 0; i < MonitoringLimits.MaxMonitoredApps; i++)
        {
            Assert.True((await EkleAsync(ad: $"servis-{i}")).IsSuccess);
        }

        var sonuc = await EkleAsync(ad: "fazla");

        Assert.Equal(MonitoringErrors.LimitReached, Assert.Single(sonuc.Errors));
        Assert.Equal(MonitoringLimits.MaxMonitoredApps, _depo.Apps.Count);
    }

    [Fact]
    public async Task Iki_xx_donen_adres_ilk_turdan_sonra_Ayakta_olur_ve_son_denetim_zamani_guncellenir()
    {
        var id = (await EkleAsync()).Value;

        Assert.Equal(1, await TurAsync());

        Assert.Equal(HealthStatus.Up, Uygulama(id).Status);
        Assert.Equal(Simdi, Uygulama(id).LastCheckedAt);
    }

    [Fact]
    public async Task Basarisizlik_nedeni_ve_durum_kodu_kaydedilir()
    {
        var id = (await EkleAsync()).Value;
        _probe.Sirala(Adres, ProbeResult.Failure(HealthFailureReason.HttpStatus, 503));

        await TurAsync();

        Assert.Equal(HealthFailureReason.HttpStatus, Uygulama(id).LastFailureReason);
        Assert.Equal(503, Uygulama(id).LastFailureStatusCode);
    }

    [Fact]
    public async Task Uygulama_ayni_aralikta_bir_kez_denetlenir_sonraki_aralikta_yeniden_denetlenir()
    {
        await EkleAsync();

        Assert.Equal(1, await TurAsync());
        _saat.UtcNow = Simdi.AddSeconds(29);
        Assert.Equal(0, await TurAsync());
        _saat.UtcNow = Simdi.AddSeconds(30);
        Assert.Equal(1, await TurAsync());

        Assert.Equal(2, _probe.Probed.Count);
    }

    [Fact]
    public async Task Ucuncu_basarisiz_turda_Down_olur_ve_baslangic_zamani_kaydedilir()
    {
        var id = (await EkleAsync()).Value;
        var zamanAsimi = ProbeResult.Failure(HealthFailureReason.Timeout);
        _probe.Sirala(Adres, zamanAsimi, zamanAsimi, zamanAsimi);

        for (var tur = 0; tur < 3; tur++)
        {
            _saat.UtcNow = Simdi.AddSeconds(30 * tur);
            await TurAsync();
            Assert.Equal(tur < 2 ? HealthStatus.Unknown : HealthStatus.Down, Uygulama(id).Status);
        }

        Assert.Equal(Simdi.AddSeconds(60), Uygulama(id).DownSince);
    }

    // AC-7: "durduktan sonra en geç iki dakika" gerçek sabitlerle. Uygulama son başarılı denetimden hemen
    // sonra durur; sonraki üç denetimin her biri zaman aşımına kadar sürer, panel 15 sn'de bir yoklar.
    [Fact]
    public void En_kotu_durumda_Down_uyarisi_iki_dakika_icinde_gorunur()
    {
        var panelYoklamaAraligi = TimeSpan.FromSeconds(15);
        var enKotu = (MonitoringLimits.DownThreshold * MonitoringLimits.CheckInterval) + MonitoringLimits.Timeout + panelYoklamaAraligi;

        Assert.True(enKotu <= TimeSpan.FromMinutes(2), $"En kötü süre {enKotu}.");
    }

    [Fact]
    public async Task Adres_degisince_durum_Bilinmiyor_olur_degistiren_kaydedilir_ve_hemen_yeniden_denetlenir()
    {
        var id = (await EkleAsync()).Value;
        await TurAsync();
        var mehmet = _kullanicilar.Ekle("mehmet");
        _saat.UtcNow = Simdi.AddSeconds(5);

        var sonuc = await new UpdateMonitoredAppHandler(_depo, _cozucu, _saat)
            .HandleAsync(new UpdateMonitoredAppCommand(id, "http://yeni.kurum.local/health", mehmet.Id), CancellationToken.None);

        Assert.True(sonuc.IsSuccess);
        Assert.Equal(HealthStatus.Unknown, Uygulama(id).Status);
        var satir = Assert.Single(await new ListMonitoredAppsHandler(_depo, _kullanicilar).HandleAsync(CancellationToken.None));
        Assert.Equal("mehmet", satir.UpdatedBy);
        Assert.Equal(Simdi.AddSeconds(5), satir.UpdatedAt);
        // Aralık dolmadan (5 sn sonra) yeniden denetlenir: adres değişimi uygulamayı hemen denetime alır.
        Assert.Equal(1, await TurAsync());
        Assert.Contains("http://yeni.kurum.local/health", _probe.Probed);
    }

    [Fact]
    public async Task Denetim_surerken_adres_degisirse_eski_adresin_sonucu_atilir()
    {
        var id = (await EkleAsync()).Value;
        _probe.Sirala(Adres, ProbeResult.Failure(HealthFailureReason.ConnectionFailed));
        _depo.BeforeRecord = () => Uygulama(id).ChangeUrl(HealthUrl.Create("http://yeni.kurum.local/health").Value, _ayse.Id, Simdi);

        Assert.Equal(0, await TurAsync());

        Assert.Equal(HealthStatus.Unknown, Uygulama(id).Status);
        Assert.Equal(0, Uygulama(id).ConsecutiveFailures);
    }

    [Fact]
    public async Task Denetim_surerken_silinen_uygulamanin_sonucu_atilir()
    {
        var id = (await EkleAsync()).Value;
        _depo.BeforeRecord = () => _depo.Apps.Clear();

        Assert.Equal(0, await TurAsync());
    }

    [Fact]
    public async Task Izlemeden_cikarilan_uygulama_listeden_cikar_ve_denetlenmez()
    {
        var id = (await EkleAsync()).Value;

        Assert.True((await new RemoveMonitoredAppHandler(_depo).HandleAsync(id, CancellationToken.None)).IsSuccess);

        Assert.Empty(await new ListMonitoredAppsHandler(_depo, _kullanicilar).HandleAsync(CancellationToken.None));
        Assert.Equal(0, await TurAsync());
        Assert.Empty(_probe.Probed);
    }

    [Fact]
    public async Task Yeniden_eklenen_ServiceName_eski_durumu_tasimaz()
    {
        var id = (await EkleAsync()).Value;
        var zamanAsimi = ProbeResult.Failure(HealthFailureReason.Timeout);
        _probe.Sirala(Adres, zamanAsimi, zamanAsimi, zamanAsimi);
        for (var tur = 0; tur < 3; tur++)
        {
            _saat.UtcNow = Simdi.AddSeconds(30 * tur);
            await TurAsync();
        }

        await new RemoveMonitoredAppHandler(_depo).HandleAsync(id, CancellationToken.None);
        var yeni = (await EkleAsync()).Value;

        Assert.Equal(HealthStatus.Unknown, Uygulama(yeni).Status);
        Assert.Null(Uygulama(yeni).DownSince);
    }

    [Fact]
    public async Task Olmayan_uygulama_degistirilemez_ve_cikarilamaz()
    {
        var degistir = await new UpdateMonitoredAppHandler(_depo, _cozucu, _saat)
            .HandleAsync(new UpdateMonitoredAppCommand(Guid.CreateVersion7(), Adres, _ayse.Id), CancellationToken.None);
        var cikar = await new RemoveMonitoredAppHandler(_depo).HandleAsync(Guid.CreateVersion7(), CancellationToken.None);

        Assert.Equal(MonitoringErrors.NotFound, Assert.Single(degistir.Errors));
        Assert.Equal(MonitoringErrors.NotFound, Assert.Single(cikar.Errors));
    }

    [Fact]
    public async Task Liste_Down_satirlarini_once_sonra_ServiceName_sirasini_verir()
    {
        var b = (await EkleAsync(ad: "b-servisi", adres: "http://b.local/")).Value;
        await EkleAsync(ad: "a-servisi", adres: "http://a.local/");
        var c = (await EkleAsync(ad: "c-servisi", adres: "http://c.local/")).Value;
        var zamanAsimi = ProbeResult.Failure(HealthFailureReason.Timeout);
        _probe.Sirala("http://c.local/", zamanAsimi, zamanAsimi, zamanAsimi, zamanAsimi);
        _probe.Sirala("http://b.local/", ProbeResult.Success, zamanAsimi, zamanAsimi, zamanAsimi);
        for (var tur = 0; tur < 4; tur++)
        {
            _saat.UtcNow = Simdi.AddSeconds(30 * tur);
            await TurAsync();
        }

        var liste = await new ListMonitoredAppsHandler(_depo, _kullanicilar).HandleAsync(CancellationToken.None);

        Assert.Equal(HealthStatus.Down, Uygulama(b).Status);
        Assert.Equal(HealthStatus.Down, Uygulama(c).Status);
        Assert.Equal(["c-servisi", "b-servisi", "a-servisi"], liste.Select(s => s.ServiceName));
    }
}
