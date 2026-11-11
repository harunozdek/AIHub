using AIHub.Application.Monitoring;
using AIHub.Application.Tests.Users;
using AIHub.Domain.Monitoring;

namespace AIHub.Application.Tests.Monitoring;

// Spec 0008 — AIHub'ın kendi kayıtları: boş ve geçersiz adres (AC-6), ekleyen "Sistem" (AC-5), var olan kayda
// dokunulmaz (AC-7), silme kalıcıdır (AC-8), sınır doluysa sonraki açılışta yeniden denenir. Eşzamanlılık ve
// gerçek işaret tablosu Api testlerindedir.
public class KendiKaydiHandlerTestleri
{
    private static readonly DateTimeOffset Simdi = new(2026, 11, 10, 9, 0, 0, TimeSpan.Zero);
    private const string ApiAdresi = "http://aihub-api.kurum.local/health";
    private const string PanelAdresi = "http://aihub-panel.kurum.local/health";

    private readonly SahteIzlenenUygulamaDeposu _depo = new();
    private readonly SahteCozucu _cozucu = new();

    private Task<IReadOnlyList<SelfRegistration>> KaydetAsync(string? api = ApiAdresi, string? panel = PanelAdresi) =>
        new RegisterSelfMonitoringHandler(_depo, _cozucu, new AyarlanabilirSaat(Simdi))
            .HandleAsync(new RegisterSelfMonitoringCommand(api, panel), CancellationToken.None);

    private static SelfRegistrationResult Sonuc(IReadOnlyList<SelfRegistration> sonuclar, string ad) =>
        sonuclar.Single(s => s.ServiceName == ad).Result;

    [Fact]
    public async Task Iki_adres_verilince_iki_kayit_Sistem_tarafindan_Bilinmiyor_durumunda_eklenir()
    {
        var sonuclar = await KaydetAsync();

        Assert.All(sonuclar, s => Assert.Equal(SelfRegistrationResult.Added, s.Result));
        Assert.Equal([SystemActor.ApiServiceName, SystemActor.PanelServiceName], _depo.Apps.Select(a => a.ServiceName));
        Assert.All(_depo.Apps, a =>
        {
            Assert.Equal(SystemActor.Id, a.CreatedBy);
            Assert.Equal(HealthStatus.Unknown, a.Status);
        });
        Assert.Equal([ApiAdresi, PanelAdresi], _depo.Apps.Select(a => a.HealthUrl));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task Bos_adres_eklenmez_digerinin_eklenmesini_etkilemez(string? panel)
    {
        var sonuclar = await KaydetAsync(panel: panel);

        Assert.Equal(SelfRegistrationResult.Added, Sonuc(sonuclar, SystemActor.ApiServiceName));
        Assert.Equal(SelfRegistrationResult.NotConfigured, Sonuc(sonuclar, SystemActor.PanelServiceName));
        Assert.Equal([SystemActor.ApiServiceName], _depo.Apps.Select(a => a.ServiceName));
    }

    [Theory]
    [InlineData("ftp://aihub.kurum.local/health")]
    [InlineData("http://kullanici:sifre@aihub.kurum.local/health")]
    [InlineData("http://169.254.169.254/health")]
    [InlineData("bir adres değil")]
    public async Task Kurala_uymayan_adres_eklenmez_nedeni_doner_isaret_konmaz(string adres)
    {
        var sonuclar = await KaydetAsync(api: adres);

        var api = sonuclar.Single(s => s.ServiceName == SystemActor.ApiServiceName);
        Assert.Equal(SelfRegistrationResult.InvalidAddress, api.Result);
        Assert.False(string.IsNullOrWhiteSpace(api.Reason));
        Assert.DoesNotContain(_depo.Apps, a => a.ServiceName == SystemActor.ApiServiceName);
        Assert.DoesNotContain(SystemActor.ApiServiceName, _depo.Registered);
    }

    [Fact]
    public async Task Ayni_adla_var_olan_kayda_dokunulmaz()
    {
        var elle = MonitoredApp.Create(Guid.CreateVersion7(), SystemActor.ApiServiceName, HealthUrl.Create("http://elle.kurum.local/saglik").Value, Simdi.AddDays(-1), Guid.CreateVersion7()).Value;
        _depo.Apps.Add(elle);

        var sonuclar = await KaydetAsync();

        Assert.Equal(SelfRegistrationResult.ExistingAppKept, Sonuc(sonuclar, SystemActor.ApiServiceName));
        var api = Assert.Single(_depo.Apps, a => a.ServiceName == SystemActor.ApiServiceName);
        Assert.Same(elle, api);
        Assert.Equal("http://elle.kurum.local/saglik", api.HealthUrl);
    }

    [Fact]
    public async Task Ikinci_acilista_eklenmez_silinen_kayit_geri_gelmez()
    {
        await KaydetAsync();
        _depo.Apps.RemoveAll(a => a.ServiceName == SystemActor.PanelServiceName);

        var sonuclar = await KaydetAsync();

        Assert.All(sonuclar, s => Assert.Equal(SelfRegistrationResult.AlreadyRegistered, s.Result));
        Assert.Equal([SystemActor.ApiServiceName], _depo.Apps.Select(a => a.ServiceName));
    }

    [Fact]
    public async Task Sinir_doluysa_eklenmez_yer_acilinca_sonraki_acilista_eklenir()
    {
        for (var i = 0; i < MonitoringLimits.MaxMonitoredApps; i++)
        {
            _depo.Apps.Add(MonitoredApp.Create(Guid.CreateVersion7(), $"servis-{i}", HealthUrl.Create($"http://s{i}.kurum.local/health").Value, Simdi, Guid.CreateVersion7()).Value);
        }

        var dolu = await KaydetAsync(panel: null);
        Assert.Equal(SelfRegistrationResult.LimitReached, Sonuc(dolu, SystemActor.ApiServiceName));

        _depo.Apps.RemoveAt(0);
        var sonra = await KaydetAsync(panel: null);
        Assert.Equal(SelfRegistrationResult.Added, Sonuc(sonra, SystemActor.ApiServiceName));
    }
}
