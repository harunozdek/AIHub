using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using AIHub.Api.Tests.Altyapi;
using AIHub.Application.Abstractions;
using AIHub.Domain.Monitoring;
using AIHub.Infrastructure.Monitoring;
using Microsoft.Extensions.Options;

namespace AIHub.Api.Tests;

// Spec 0005 — R-4, R-6, AC-4, AC-5; ADR 0003: probe sahte sağlık sunucusuna karşı.
[Trait("Category", "Integration")]
public sealed class HttpHealthProbeTestleri : IAsyncLifetime
{
    private static readonly TimeSpan TestZamanAsimi = TimeSpan.FromSeconds(1);

    private readonly HttpHealthProbe _probe = new(Options.Create(new MonitoringOptions { Timeout = TestZamanAsimi }));
    private SaglikHedefi _hedef = null!;

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public async ValueTask InitializeAsync() => _hedef = await SaglikHedefi.BaslatAsync();

    public async ValueTask DisposeAsync()
    {
        _probe.Dispose();
        await _hedef.DisposeAsync();
    }

    private Task<ProbeResult> DeneAsync(string adres) => _probe.ProbeAsync(new Uri(adres), Ct);

    [Theory]
    [InlineData(200)]
    [InlineData(204)]
    [InlineData(299)]
    public async Task Iki_xx_basaridir(int durum)
    {
        _hedef.DurumAyarla("/saglik", durum);

        Assert.Equal(ProbeResult.Success, await DeneAsync(_hedef.Http("/saglik")));
    }

    [Theory]
    [InlineData(503)]
    [InlineData(500)]
    [InlineData(404)]
    [InlineData(401)]
    public async Task Iki_xx_disi_durum_kodu_nedeni_ve_koduyla_basarisizliktir(int durum)
    {
        _hedef.DurumAyarla("/saglik", durum);

        Assert.Equal(ProbeResult.Failure(HealthFailureReason.HttpStatus, durum), await DeneAsync(_hedef.Http("/saglik")));
    }

    [Fact]
    public async Task Yonlendirme_basarisizliktir_ve_yonlendirilen_adrese_istek_gitmez()
    {
        Assert.Equal(ProbeResult.Failure(HealthFailureReason.Redirect), await DeneAsync(_hedef.Http("/yonlendir")));

        Assert.Equal(1, _hedef.Sayi("/yonlendir"));
        Assert.Equal(0, _hedef.Sayi("/hedef"));
    }

    [Fact]
    public async Task Zaman_asimina_kadar_yanit_vermeyen_adres_zaman_asimidir()
    {
        _hedef.GecikmeAyarla("/yavas", TimeSpan.FromSeconds(10));
        var sure = Stopwatch.StartNew();

        Assert.Equal(ProbeResult.Failure(HealthFailureReason.Timeout), await DeneAsync(_hedef.Http("/yavas")));
        Assert.InRange(sure.Elapsed, TestZamanAsimi, TestZamanAsimi + TimeSpan.FromSeconds(3));
    }

    [Fact]
    public async Task Baglanti_kurulamayan_adres_baglanti_hatasidir()
    {
        using var dinleyici = new TcpListener(IPAddress.Loopback, 0);
        dinleyici.Start();
        var port = ((IPEndPoint)dinleyici.LocalEndpoint).Port;
        dinleyici.Stop();
        // Windows reddedilen bağlantıyı vazgeçmeden önce ~2 sn yeniden dener; üretim zaman aşımı (5 sn) bunu kapsar.
        using var probe = new HttpHealthProbe(Options.Create(new MonitoringOptions()));

        Assert.Equal(ProbeResult.Failure(HealthFailureReason.ConnectionFailed), await probe.ProbeAsync(new Uri($"http://127.0.0.1:{port}/health"), Ct));
    }

    [Fact]
    public async Task Sertifikasi_dogrulanamayan_https_adresi_sertifika_hatasidir()
    {
        Assert.Equal(ProbeResult.Failure(HealthFailureReason.CertificateError), await DeneAsync(_hedef.HttpsTaban + "/saglik"));
    }

    [Theory]
    [InlineData("http://169.254.169.254/latest/meta-data/")]
    [InlineData("http://[fe80::1]/health")]
    [InlineData("http://[::ffff:169.254.169.254]/health")]
    public async Task Engelli_adrese_baglanti_denenmez(string adres)
    {
        var sure = Stopwatch.StartNew();

        Assert.Equal(ProbeResult.Failure(HealthFailureReason.BlockedAddress), await DeneAsync(adres));
        // Bağlantı denenseydi yanıt vermeyen adreste zaman aşımına kadar beklenirdi.
        Assert.True(sure.Elapsed < TestZamanAsimi, $"Süre {sure.Elapsed}.");
    }

    [Fact]
    public async Task Yanit_govdesi_okunmaz_probe_basliklardan_sonra_doner()
    {
        var sure = Stopwatch.StartNew();

        Assert.Equal(ProbeResult.Success, await DeneAsync(_hedef.Http("/buyuk-govde")));
        Assert.True(sure.Elapsed < TestZamanAsimi, $"Süre {sure.Elapsed}.");
    }

    // Review 1, F-04: yanıt kapatılırken gövde arka planda da boşaltılmaz; bağlantı hemen kapanır.
    [Fact]
    public async Task Yanit_kapatilirken_govde_arka_planda_bosaltilmaz()
    {
        Assert.Equal(ProbeResult.Success, await DeneAsync(_hedef.Http("/buyuk-govde")));
        var hemen = _hedef.GonderilenGovde;

        // Varsayılan davranışta istemci 2 sn boyunca 1 MB'a kadar okumaya devam ederdi.
        await Task.Delay(TimeSpan.FromSeconds(2), Ct);

        Assert.True(_hedef.GonderilenGovde - hemen < 256 * 1024, $"Kapatmadan sonra gönderilen: {_hedef.GonderilenGovde - hemen} bayt.");
    }

    [Fact]
    public void Uretim_varsayilani_spec_degerleridir()
    {
        var varsayilan = new MonitoringOptions();

        Assert.Equal(TimeSpan.FromSeconds(5), varsayilan.Timeout);
        Assert.Equal(TimeSpan.FromSeconds(30), varsayilan.CheckInterval);
        Assert.True(varsayilan.Enabled);
    }
}
