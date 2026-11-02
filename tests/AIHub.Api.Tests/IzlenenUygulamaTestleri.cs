using System.Diagnostics;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using AIHub.Api.Tests.Altyapi;

namespace AIHub.Api.Tests;

// Spec 0005 — izlenen uygulamaların HTTP yüzeyi ve arka plan denetimi (AC-1…AC-5, AC-7, AC-9, AC-11).
// Worker'lı testler kendi boş veritabanında, kısa aralıkla çalışır (plan 0005, risk 1).
[Trait("Category", "Integration")]
[Collection(IngestionCollection.Name)]
public sealed class IzlenenUygulamaTestleri(ContainerFixture containers) : IAsyncLifetime
{
    private const string Uc = "/api/monitored-apps";

    private readonly List<AIHubFactory> _factoryler = [];
    private SaglikHedefi _hedef = null!;

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public async ValueTask InitializeAsync() => _hedef = await SaglikHedefi.BaslatAsync();

    public async ValueTask DisposeAsync()
    {
        foreach (var factory in _factoryler)
        {
            await factory.DisposeAsync();
        }

        await _hedef.DisposeAsync();
    }

    private AIHubFactory Factory(string? veritabani = null, bool worker = false, IReadOnlyDictionary<string, string?>? ayarlar = null)
    {
        var factory = ayarlar is null
            ? new AIHubFactory(containers) { ConsumerEnabled = false, MonitoringEnabled = worker, PostgresConnectionOverride = veritabani }
            : new AIHubFactory(containers) { ConsumerEnabled = false, MonitoringEnabled = worker, PostgresConnectionOverride = veritabani, MonitoringSettings = ayarlar };
        _factoryler.Add(factory);
        _ = factory.Services;
        return factory;
    }

    private static string YeniAd() => "s-" + Guid.NewGuid().ToString("N")[..10];

    private static async Task<Guid> EkleAsync(HttpClient istemci, string ad, string adres)
    {
        var yanit = await istemci.PostAsJsonAsync(Uc, new { serviceName = ad, healthUrl = adres }, Ct);
        Assert.Equal(HttpStatusCode.Created, yanit.StatusCode);
        return (await yanit.Content.ReadFromJsonAsync<JsonElement>(Ct)).GetProperty("id").GetGuid();
    }

    private static async Task<JsonElement?> SatirAsync(HttpClient istemci, Guid id)
    {
        var liste = await istemci.GetFromJsonAsync<JsonElement>(Uc, Ct);
        return liste.EnumerateArray().Cast<JsonElement?>().FirstOrDefault(s => s!.Value.GetProperty("id").GetGuid() == id);
    }

    private static async Task<JsonElement> BekleAsync(HttpClient istemci, Guid id, Func<JsonElement, bool> kosul, string ne)
    {
        var sure = Stopwatch.StartNew();
        while (sure.Elapsed < TimeSpan.FromSeconds(20))
        {
            if (await SatirAsync(istemci, id) is { } satir && kosul(satir))
            {
                return satir;
            }

            await Task.Delay(100, Ct);
        }

        throw new TimeoutException($"Beklenen durum oluşmadı: {ne}. Son satır: {await SatirAsync(istemci, id)}");
    }

    private static string Durum(JsonElement satir) => satir.GetProperty("status").GetString()!;

    private static async Task<JsonElement> HatalarAsync(HttpResponseMessage yanit) =>
        (await yanit.Content.ReadFromJsonAsync<JsonElement>(Ct)).GetProperty("errors");

    [Theory]
    [InlineData("GET", Uc)]
    [InlineData("POST", Uc)]
    [InlineData("PUT", Uc + "/0195f000-0000-7000-8000-000000000000")]
    [InlineData("DELETE", Uc + "/0195f000-0000-7000-8000-000000000000")]
    public async Task AC1_Oturumsuz_istek_401_alir(string yontem, string adres)
    {
        var istek = new HttpRequestMessage(new HttpMethod(yontem), adres) { Content = JsonContent.Create(new { }) };

        Assert.Equal(HttpStatusCode.Unauthorized, (await Factory().CreateClient().SendAsync(istek, Ct)).StatusCode);
    }

    [Fact]
    public async Task AC2_Eklenen_uygulama_Bilinmiyor_durumunda_ekleyen_ve_zamanla_listelenir_ayni_ad_reddedilir()
    {
        var istemci = Factory().PanelIstemcisi();
        var ad = YeniAd();

        var id = await EkleAsync(istemci, ad, "http://odeme.kurum.local/health");
        var satir = (await SatirAsync(istemci, id))!.Value;
        var ikinci = await istemci.PostAsJsonAsync(Uc, new { serviceName = ad, healthUrl = "http://baska.kurum.local/" }, Ct);

        Assert.Equal("Unknown", Durum(satir));
        Assert.Equal("Panel Testi", satir.GetProperty("createdBy").GetString());
        Assert.True(satir.GetProperty("createdAt").GetDateTimeOffset() > DateTimeOffset.UtcNow.AddMinutes(-1));
        Assert.Equal(HttpStatusCode.BadRequest, ikinci.StatusCode);
        Assert.True((await HatalarAsync(ikinci)).TryGetProperty("serviceName", out _));
    }

    [Theory]
    [InlineData("ftp://odeme.kurum.local/health")]
    [InlineData("/health")]
    [InlineData("http://kullanici:sifre@odeme.kurum.local/health")]
    [InlineData("http://169.254.169.254/latest/meta-data")]
    [InlineData("http://[fe80::1]/health")]
    [InlineData("http://localhost/" + "x")]
    public async Task AC3_Kural_disi_adres_alan_adiyla_reddedilir(string adres)
    {
        if (adres.StartsWith("http://localhost/", StringComparison.Ordinal))
        {
            // Sınırdaki uzunluk: 2049 karakter reddedilir.
            adres = adres + new string('a', 2049 - adres.Length);
        }

        var yanit = await Factory().PanelIstemcisi().PostAsJsonAsync(Uc, new { serviceName = YeniAd(), healthUrl = adres }, Ct);

        Assert.Equal(HttpStatusCode.BadRequest, yanit.StatusCode);
        Assert.True((await HatalarAsync(yanit)).TryGetProperty("healthUrl", out _));
    }

    [Fact]
    public async Task AC3_Iki_bin_kirk_sekiz_karakterlik_adres_kabul_edilir_bos_ServiceName_reddedilir()
    {
        var istemci = Factory().PanelIstemcisi();
        const string Bas = "http://localhost/";

        await EkleAsync(istemci, YeniAd(), Bas + new string('a', 2048 - Bas.Length));
        var bos = await istemci.PostAsJsonAsync(Uc, new { serviceName = " ", healthUrl = "http://a.local/" }, Ct);

        Assert.Equal(HttpStatusCode.BadRequest, bos.StatusCode);
        Assert.True((await HatalarAsync(bos)).TryGetProperty("serviceName", out _));
    }

    [Fact]
    public async Task AC11_Iki_yuzuncu_uygulamadan_sonra_ekleme_409_ve_neden_alir()
    {
        var istemci = Factory(await TestVerisi.BosVeritabaniAsync(containers, Ct)).PanelIstemcisi();
        for (var i = 0; i < 200; i++)
        {
            await EkleAsync(istemci, $"servis-{i}", $"http://servis-{i}.kurum.local/health");
        }

        var yanit = await istemci.PostAsJsonAsync(Uc, new { serviceName = "fazla", healthUrl = "http://fazla.kurum.local/" }, Ct);

        Assert.Equal(HttpStatusCode.Conflict, yanit.StatusCode);
        Assert.Contains("En fazla 200", (await yanit.Content.ReadFromJsonAsync<JsonElement>(Ct)).GetProperty("title").GetString());
    }

    [Fact]
    public async Task AC4_AC5_AC7_Worker_oturumsuz_denetler_Ayakta_Down_ve_yeniden_Ayakta_olur()
    {
        var istemci = Factory(await TestVerisi.BosVeritabaniAsync(containers, Ct), worker: true).PanelIstemcisi();
        var id = await EkleAsync(istemci, YeniAd(), _hedef.Http("/saglik"));

        var ayakta = await BekleAsync(istemci, id, s => Durum(s) == "Up", "Ayakta");
        Assert.NotEqual(JsonValueKind.Null, ayakta.GetProperty("lastCheckedAt").ValueKind);

        _hedef.DurumAyarla("/saglik", 503);
        var down = await BekleAsync(istemci, id, s => Durum(s) == "Down", "Down");
        Assert.Equal("HttpStatus", down.GetProperty("lastFailureReason").GetString());
        Assert.Equal(503, down.GetProperty("lastFailureStatusCode").GetInt32());
        Assert.NotEqual(JsonValueKind.Null, down.GetProperty("downSince").ValueKind);

        _hedef.DurumAyarla("/saglik", 200);
        var yeniden = await BekleAsync(istemci, id, s => Durum(s) == "Up", "yeniden Ayakta");
        Assert.Equal(JsonValueKind.Null, yeniden.GetProperty("downSince").ValueKind);
    }

    [Fact]
    public async Task AC5_Yonlendirme_nedeniyle_Down_olur_yonlendirilen_adrese_istek_gitmez()
    {
        var istemci = Factory(await TestVerisi.BosVeritabaniAsync(containers, Ct), worker: true).PanelIstemcisi();
        var id = await EkleAsync(istemci, YeniAd(), _hedef.Http("/yonlendir"));

        var down = await BekleAsync(istemci, id, s => Durum(s) == "Down", "Down");

        Assert.Equal("Redirect", down.GetProperty("lastFailureReason").GetString());
        Assert.Equal(0, _hedef.Sayi("/hedef"));
    }

    [Fact]
    public async Task AC7_Yeniden_baslatmada_durum_ve_Down_baslangici_korunur()
    {
        var veritabani = await TestVerisi.BosVeritabaniAsync(containers, Ct);
        var ilk = Factory(veritabani, worker: true);
        var istemci = ilk.PanelIstemcisi();
        _hedef.DurumAyarla("/kapali", 503);
        var id = await EkleAsync(istemci, YeniAd(), _hedef.Http("/kapali"));
        var down = await BekleAsync(istemci, id, s => Durum(s) == "Down", "Down");
        await ilk.DisposeAsync();
        _factoryler.Remove(ilk);

        var sonraki = (await SatirAsync(Factory(veritabani).PanelIstemcisi(), id))!.Value;

        Assert.Equal("Down", Durum(sonraki));
        Assert.Equal(down.GetProperty("downSince").GetDateTimeOffset(), sonraki.GetProperty("downSince").GetDateTimeOffset());
    }

    [Fact]
    public async Task AC7_Iki_ornek_ayni_anda_calisirken_uygulama_aralik_basina_bir_kez_denetlenir()
    {
        var veritabani = await TestVerisi.BosVeritabaniAsync(containers, Ct);
        var ayarlar = new Dictionary<string, string?>
        {
            ["Monitoring:TickInterval"] = "00:00:00.050",
            ["Monitoring:CheckInterval"] = "00:00:01",
            ["Monitoring:Timeout"] = "00:00:01",
        };
        var istemci = Factory(veritabani, worker: true, ayarlar).PanelIstemcisi();
        Factory(veritabani, worker: true, ayarlar);
        var sure = Stopwatch.StartNew();
        await EkleAsync(istemci, YeniAd(), _hedef.Http("/iki-ornek"));

        await Task.Delay(TimeSpan.FromSeconds(5), Ct);

        var aralikSayisi = (int)Math.Floor(sure.Elapsed.TotalSeconds) + 1;
        Assert.InRange(_hedef.Sayi("/iki-ornek"), 3, aralikSayisi);
    }

    [Fact]
    public async Task AC9_Adres_degisince_Bilinmiyor_olur_degistiren_gorunur_cikarilan_artik_denetlenmez()
    {
        var istemci = Factory(await TestVerisi.BosVeritabaniAsync(containers, Ct), worker: true).PanelIstemcisi();
        var ad = YeniAd();
        _hedef.DurumAyarla("/eski", 503);
        var id = await EkleAsync(istemci, ad, _hedef.Http("/eski"));
        await BekleAsync(istemci, id, s => Durum(s) == "Down", "Down");

        // Yeni adresin ilk denetimi yavaş: durum, değişimden hemen sonra okunurken hâlâ Bilinmiyor'dur.
        _hedef.GecikmeAyarla("/yeni-yavas", TimeSpan.FromMilliseconds(800));
        var degistir = await istemci.PutAsJsonAsync($"{Uc}/{id}", new { healthUrl = _hedef.Http("/yeni-yavas") }, Ct);
        var satir = (await SatirAsync(istemci, id))!.Value;

        Assert.Equal(HttpStatusCode.NoContent, degistir.StatusCode);
        Assert.Equal("Unknown", Durum(satir));
        Assert.Equal("Panel Testi", satir.GetProperty("updatedBy").GetString());
        Assert.NotEqual(JsonValueKind.Null, satir.GetProperty("updatedAt").ValueKind);
        await BekleAsync(istemci, id, s => Durum(s) == "Up", "yeni adresle Ayakta");

        Assert.Equal(HttpStatusCode.NoContent, (await istemci.DeleteAsync($"{Uc}/{id}", Ct)).StatusCode);
        Assert.Null(await SatirAsync(istemci, id));
        var sayi = _hedef.Sayi("/yeni-yavas");
        await Task.Delay(TimeSpan.FromSeconds(2), Ct);
        Assert.Equal(sayi, _hedef.Sayi("/yeni-yavas"));
        Assert.Equal(HttpStatusCode.NotFound, (await istemci.DeleteAsync($"{Uc}/{id}", Ct)).StatusCode);

        var yeniden = await EkleAsync(istemci, ad, _hedef.Http("/eski"));
        Assert.Equal("Unknown", Durum((await SatirAsync(istemci, yeniden))!.Value));
    }
}
