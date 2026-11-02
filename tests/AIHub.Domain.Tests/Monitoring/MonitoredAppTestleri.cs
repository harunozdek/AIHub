using AIHub.Domain.Monitoring;

namespace AIHub.Domain.Tests.Monitoring;

// Spec 0005 — R-5, AC-6, AC-9: durum kuralı.
public class MonitoredAppTestleri
{
    private static readonly DateTimeOffset Simdi = new(2026, 11, 2, 9, 0, 0, TimeSpan.Zero);
    private static readonly Guid Ekleyen = Guid.CreateVersion7();

    private static MonitoredApp YeniUygulama(string ad = "odeme-servisi") =>
        MonitoredApp.Create(Guid.CreateVersion7(), ad, HealthUrl.Create("http://odeme.kurum.local/health").Value, Simdi, Ekleyen).Value;

    [Fact]
    public void Yeni_uygulama_Bilinmiyor_durumundadir_ve_hemen_denetime_alinir()
    {
        var uygulama = YeniUygulama();

        Assert.Equal(HealthStatus.Unknown, uygulama.Status);
        Assert.Equal(Simdi, uygulama.NextCheckAt);
        Assert.Equal(Ekleyen, uygulama.CreatedBy);
        Assert.Null(uygulama.LastCheckedAt);
    }

    [Fact]
    public void Ilk_basarili_denetim_Ayakta_yapar_ve_son_denetim_zamanini_kaydeder()
    {
        var uygulama = YeniUygulama();

        uygulama.RecordSuccess(Simdi.AddSeconds(1));

        Assert.Equal(HealthStatus.Up, uygulama.Status);
        Assert.Equal(Simdi.AddSeconds(1), uygulama.LastCheckedAt);
    }

    [Fact]
    public void Art_arda_iki_basarisizlik_Down_yapmaz_ucuncusu_Down_yapar_ve_baslangicini_kaydeder()
    {
        var uygulama = YeniUygulama();
        uygulama.RecordSuccess(Simdi);

        uygulama.RecordFailure(HealthFailureReason.HttpStatus, 503, Simdi.AddSeconds(30));
        uygulama.RecordFailure(HealthFailureReason.Timeout, null, Simdi.AddSeconds(60));
        Assert.Equal(HealthStatus.Up, uygulama.Status);
        Assert.Null(uygulama.DownSince);

        uygulama.RecordFailure(HealthFailureReason.ConnectionFailed, null, Simdi.AddSeconds(90));

        Assert.Equal(HealthStatus.Down, uygulama.Status);
        Assert.Equal(Simdi.AddSeconds(90), uygulama.DownSince);
        Assert.Equal(HealthFailureReason.ConnectionFailed, uygulama.LastFailureReason);
    }

    [Fact]
    public void Down_iken_sonraki_basarisizliklar_Down_baslangicini_degistirmez()
    {
        var uygulama = YeniUygulama();
        for (var i = 1; i <= 5; i++)
        {
            uygulama.RecordFailure(HealthFailureReason.Timeout, null, Simdi.AddSeconds(30 * i));
        }

        Assert.Equal(HealthStatus.Down, uygulama.Status);
        Assert.Equal(Simdi.AddSeconds(90), uygulama.DownSince);
    }

    [Fact]
    public void Down_iken_tek_basarili_denetim_Ayakta_yapar_ve_sayaci_sifirlar()
    {
        var uygulama = YeniUygulama();
        for (var i = 1; i <= 3; i++)
        {
            uygulama.RecordFailure(HealthFailureReason.Timeout, null, Simdi.AddSeconds(30 * i));
        }

        uygulama.RecordSuccess(Simdi.AddSeconds(120));

        Assert.Equal(HealthStatus.Up, uygulama.Status);
        Assert.Null(uygulama.DownSince);
        Assert.Equal(0, uygulama.ConsecutiveFailures);
    }

    [Fact]
    public void Iki_basarisizlik_bir_basari_iki_basarisizlik_Down_yapmaz()
    {
        var uygulama = YeniUygulama();

        uygulama.RecordFailure(HealthFailureReason.Timeout, null, Simdi.AddSeconds(30));
        uygulama.RecordFailure(HealthFailureReason.Timeout, null, Simdi.AddSeconds(60));
        uygulama.RecordSuccess(Simdi.AddSeconds(90));
        uygulama.RecordFailure(HealthFailureReason.Timeout, null, Simdi.AddSeconds(120));
        uygulama.RecordFailure(HealthFailureReason.Timeout, null, Simdi.AddSeconds(150));

        Assert.Equal(HealthStatus.Up, uygulama.Status);
    }

    [Fact]
    public void Bilinmiyor_iken_iki_basarisizlik_Bilinmiyor_birakir_ucuncusu_Down_yapar()
    {
        var uygulama = YeniUygulama();

        uygulama.RecordFailure(HealthFailureReason.Redirect, null, Simdi.AddSeconds(30));
        uygulama.RecordFailure(HealthFailureReason.Redirect, null, Simdi.AddSeconds(60));
        Assert.Equal(HealthStatus.Unknown, uygulama.Status);

        uygulama.RecordFailure(HealthFailureReason.Redirect, null, Simdi.AddSeconds(90));
        Assert.Equal(HealthStatus.Down, uygulama.Status);
    }

    [Fact]
    public void Durum_kodu_yalnizca_HTTP_durumu_nedeninde_saklanir()
    {
        var uygulama = YeniUygulama();

        uygulama.RecordFailure(HealthFailureReason.HttpStatus, 503, Simdi);
        Assert.Equal(503, uygulama.LastFailureStatusCode);

        uygulama.RecordFailure(HealthFailureReason.Timeout, 503, Simdi.AddSeconds(30));
        Assert.Null(uygulama.LastFailureStatusCode);
    }

    [Fact]
    public void Adres_degisince_durum_Bilinmiyor_olur_sayac_ve_Down_sifirlanir_degistiren_kaydedilir()
    {
        var uygulama = YeniUygulama();
        for (var i = 1; i <= 3; i++)
        {
            uygulama.RecordFailure(HealthFailureReason.Timeout, null, Simdi.AddSeconds(30 * i));
        }

        var degistiren = Guid.CreateVersion7();
        uygulama.ChangeUrl(HealthUrl.Create("https://yeni.kurum.local/saglik").Value, degistiren, Simdi.AddMinutes(5));

        Assert.Equal(HealthStatus.Unknown, uygulama.Status);
        Assert.Equal(0, uygulama.ConsecutiveFailures);
        Assert.Null(uygulama.DownSince);
        Assert.Null(uygulama.LastFailureReason);
        Assert.Equal("https://yeni.kurum.local/saglik", uygulama.HealthUrl);
        Assert.Equal(degistiren, uygulama.UpdatedBy);
        Assert.Equal(Simdi.AddMinutes(5), uygulama.UpdatedAt);
        Assert.Equal(Simdi.AddMinutes(5), uygulama.NextCheckAt);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void ServiceName_bos_olamaz(string? ad)
    {
        var sonuc = MonitoredApp.Create(Guid.CreateVersion7(), ad, HealthUrl.Create("http://a.local/").Value, Simdi, Ekleyen);

        Assert.Equal("serviceName", Assert.Single(sonuc.Errors).Field);
    }

    [Fact]
    public void ServiceName_kirpilir_ve_200_karakteri_asarsa_reddedilir()
    {
        var adres = HealthUrl.Create("http://a.local/").Value;

        Assert.Equal("odeme", YeniUygulama("  odeme ").ServiceName);
        Assert.True(MonitoredApp.Create(Guid.CreateVersion7(), new string('a', 200), adres, Simdi, Ekleyen).IsSuccess);
        Assert.False(MonitoredApp.Create(Guid.CreateVersion7(), new string('a', 201), adres, Simdi, Ekleyen).IsSuccess);
    }

    [Fact]
    public void Sabitler_spec_degerleridir()
    {
        Assert.Equal(TimeSpan.FromSeconds(30), MonitoringLimits.CheckInterval);
        Assert.Equal(TimeSpan.FromSeconds(5), MonitoringLimits.Timeout);
        Assert.Equal(3, MonitoringLimits.DownThreshold);
        Assert.Equal(200, MonitoringLimits.MaxMonitoredApps);
    }
}
