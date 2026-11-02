using System.Collections.Concurrent;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using AIHub.Api.Tests.Altyapi;
using AIHub.Application.Abstractions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace AIHub.Api.Tests;

// Spec 0004 — yerel giriş ve kullanıcı yönetiminin backend yüzü (AC-1, AC-3…AC-10).
[Trait("Category", "Integration")]
[Collection(IngestionCollection.Name)]
public sealed class KullaniciTestleri(ContainerFixture containers) : IAsyncLifetime
{
    private const string Sifre = "dogru-sifre-123";

    private readonly LogYakalayici _loglar = new();
    private AIHubFactory _factory = null!;

    public ValueTask InitializeAsync()
    {
        _factory = new AIHubFactory(containers)
        {
            ConsumerEnabled = false,
            TestServices = s => s.AddSingleton<ILoggerProvider>(_loglar),
        };
        _ = _factory.Services;
        return ValueTask.CompletedTask;
    }

    public ValueTask DisposeAsync() => _factory.DisposeAsync();

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static string YeniAd() => "k-" + Guid.NewGuid().ToString("N")[..10];

    private async Task<string> KullaniciEkleAsync(string? ad = null, string sifre = Sifre)
    {
        ad ??= YeniAd();
        var yanit = await _factory.PanelIstemcisi().PostAsJsonAsync("/api/users", new { userName = ad, displayName = ad, password = sifre }, Ct);
        Assert.Equal(HttpStatusCode.Created, yanit.StatusCode);
        return ad;
    }

    private Task<HttpResponseMessage> GirisAsync(string ad, string sifre, AIHubFactory? factory = null) =>
        (factory ?? _factory).CreateClient().PostAsJsonAsync("/api/auth/login", new { userName = ad, password = sifre }, Ct);

    private async Task<HttpClient> OturumAsync(string ad, string sifre = Sifre)
    {
        var yanit = await GirisAsync(ad, sifre);
        Assert.Equal(HttpStatusCode.OK, yanit.StatusCode);
        var token = (await yanit.Content.ReadFromJsonAsync<JsonElement>(Ct)).GetProperty("token").GetString();
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return client;
    }

    private async Task<Guid> KimlikAsync(string ad)
    {
        var liste = await _factory.PanelIstemcisi().GetFromJsonAsync<JsonElement>("/api/users", Ct);
        return liste.EnumerateArray().Single(u => u.GetProperty("userName").GetString() == ad).GetProperty("id").GetGuid();
    }

    [Theory]
    [InlineData("GET", "/api/users")]
    [InlineData("POST", "/api/users")]
    [InlineData("POST", "/api/me/password")]
    [InlineData("GET", "/api/panel/services")]
    public async Task AC1_Oturumsuz_istek_401_alir(string metot, string adres)
    {
        var yanit = await _factory.CreateClient().SendAsync(new HttpRequestMessage(new HttpMethod(metot), adres) { Content = JsonContent.Create(new { }) }, Ct);

        Assert.Equal(HttpStatusCode.Unauthorized, yanit.StatusCode);
    }

    [Fact]
    public async Task AC2_Kullanici_adi_buyuk_kucuk_harften_bagimsiz_giris_yapar_ve_token_panel_ucunu_acar()
    {
        var ad = await KullaniciEkleAsync();

        var client = await OturumAsync(ad.ToUpperInvariant());

        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/panel/services", Ct)).StatusCode);
    }

    [Fact]
    public async Task AC3_Yanlis_sifre_olmayan_pasif_ve_kilitli_hesap_ayni_durum_ve_ayni_govdeyi_alir()
    {
        var ad = await KullaniciEkleAsync();
        var pasif = await KullaniciEkleAsync();
        Assert.Equal(HttpStatusCode.NoContent, (await _factory.PanelIstemcisi().PostAsync($"/api/users/{await KimlikAsync(pasif)}/deactivate", null, Ct)).StatusCode);
        var kilitli = await KullaniciEkleAsync();
        for (var i = 0; i < 5; i++)
        {
            await GirisAsync(kilitli, "yanlis-sifre-123");
        }

        var yanitlar = new[]
        {
            await GirisAsync(ad, "yanlis-sifre-123"),
            await GirisAsync(YeniAd(), Sifre),
            await GirisAsync(pasif, Sifre),
            await GirisAsync(kilitli, Sifre),
        };

        var govdeler = new List<string>();
        foreach (var yanit in yanitlar)
        {
            Assert.Equal(HttpStatusCode.Unauthorized, yanit.StatusCode);
            var govde = JsonDocument.Parse(await yanit.Content.ReadAsStringAsync(Ct)).RootElement;
            govdeler.Add(string.Join("|", govde.EnumerateObject().Where(p => p.Name != "traceId").Select(p => $"{p.Name}={p.Value}")));
        }

        Assert.Single(govdeler.Distinct());
        Assert.Contains("Kullanıcı adı veya şifre hatalı.", govdeler[0]);
    }

    [Fact]
    public async Task AC4_Bes_yanlis_denemeden_sonra_dogru_sifre_reddedilir()
    {
        var ad = await KullaniciEkleAsync();
        for (var i = 0; i < 5; i++)
        {
            Assert.Equal(HttpStatusCode.Unauthorized, (await GirisAsync(ad, "yanlis-sifre-123")).StatusCode);
        }

        Assert.Equal(HttpStatusCode.Unauthorized, (await GirisAsync(ad, Sifre)).StatusCode);
    }

    [Fact]
    public async Task AC5_Giris_denemelerinin_hicbir_log_satiri_sifreyi_icermez()
    {
        var ad = await KullaniciEkleAsync();
        const string gizli = "cok-gizli-sifre-XYZ-987";

        await GirisAsync(ad, gizli);
        await GirisAsync(YeniAd(), gizli);
        await OturumAsync(ad);

        Assert.NotEmpty(_loglar.Satirlar);
        Assert.DoesNotContain(_loglar.Satirlar, s => s.Contains(gizli, StringComparison.Ordinal) || s.Contains(Sifre, StringComparison.Ordinal));
    }

    [Fact]
    public async Task AC6_Bos_veritabaninda_ilk_yonetici_olusur_ve_giris_yapar_varken_degismez()
    {
        var baglanti = await TestVerisi.BosVeritabaniAsync(containers, Ct);
        await using (var ilk = new AIHubFactory(containers) { ConsumerEnabled = false, PostgresConnectionOverride = baglanti })
        {
            Assert.Equal(HttpStatusCode.OK, (await GirisAsync(AIHubFactory.BootstrapUserName, AIHubFactory.BootstrapPassword, ilk)).StatusCode);
        }

        var farkliSifre = new Dictionary<string, string?>(new AIHubFactory(containers).AuthSettings) { ["Auth:BootstrapAdmin:Password"] = "baska-bir-sifre-456" };
        await using var ikinci = new AIHubFactory(containers) { ConsumerEnabled = false, PostgresConnectionOverride = baglanti, AuthSettings = farkliSifre };

        Assert.Equal(HttpStatusCode.OK, (await GirisAsync(AIHubFactory.BootstrapUserName, AIHubFactory.BootstrapPassword, ikinci)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await GirisAsync(AIHubFactory.BootstrapUserName, "baska-bir-sifre-456", ikinci)).StatusCode);
    }

    [Theory]
    [InlineData("Auth:BootstrapAdmin:Password")]
    [InlineData("Auth:BootstrapAdmin:UserName")]
    public async Task AC6_Bos_veritabaninda_ilk_yonetici_ayari_eksikse_uygulama_ayarin_adini_soyleyerek_baslamaz(string eksik)
    {
        var ayarlar = new Dictionary<string, string?>(new AIHubFactory(containers).AuthSettings) { [eksik] = null };
        await using var factory = new AIHubFactory(containers) { ConsumerEnabled = false, PostgresConnectionOverride = await TestVerisi.BosVeritabaniAsync(containers, Ct), AuthSettings = ayarlar };

        var hata = Assert.ThrowsAny<Exception>(() => factory.CreateClient());

        Assert.Contains(eksik, hata.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task AC6_Imza_anahtari_eksikse_uygulama_ayarin_adini_soyleyerek_baslamaz()
    {
        var ayarlar = new Dictionary<string, string?>(new AIHubFactory(containers).AuthSettings) { ["Auth:TokenSigningKey"] = "kisa" };
        await using var factory = new AIHubFactory(containers) { ConsumerEnabled = false, AuthSettings = ayarlar };

        var hata = Assert.ThrowsAny<Exception>(() => factory.CreateClient());

        Assert.Contains("Auth:TokenSigningKey", hata.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task AC7_Eklenen_kullanici_baslangic_sifresiyle_giris_yapar_listede_ekleyenle_gorunur()
    {
        var ad = await KullaniciEkleAsync();

        await OturumAsync(ad);
        var liste = await _factory.PanelIstemcisi().GetFromJsonAsync<JsonElement>("/api/users", Ct);
        var satir = liste.EnumerateArray().Single(u => u.GetProperty("userName").GetString() == ad);

        Assert.Equal("Panel Testi", satir.GetProperty("createdBy").GetString());
        Assert.True(satir.GetProperty("isActive").GetBoolean());
        Assert.NotEqual(JsonValueKind.Null, satir.GetProperty("lastLoginAt").ValueKind);
        Assert.False(satir.TryGetProperty("passwordHash", out _));
    }

    [Theory]
    [InlineData("ab", Sifre, "userName")]
    [InlineData("ayşe", Sifre, "userName")]
    [InlineData("gecerli-ad", "kisa-sifre1", "password")]
    public async Task AC7_Kurala_uymayan_alan_400_ve_alan_adiyla_reddedilir(string ad, string sifre, string alan)
    {
        var yanit = await _factory.PanelIstemcisi().PostAsJsonAsync("/api/users", new { userName = ad, displayName = "x", password = sifre }, Ct);

        Assert.Equal(HttpStatusCode.BadRequest, yanit.StatusCode);
        Assert.True((await yanit.Content.ReadFromJsonAsync<JsonElement>(Ct)).GetProperty("errors").TryGetProperty(alan, out _));
    }

    [Fact]
    public async Task AC7_Alinmis_kullanici_adi_buyuk_kucuk_harf_farkiyla_reddedilir()
    {
        var ad = await KullaniciEkleAsync();

        var yanit = await _factory.PanelIstemcisi().PostAsJsonAsync("/api/users", new { userName = ad.ToUpperInvariant(), displayName = "x", password = Sifre }, Ct);

        Assert.Equal(HttpStatusCode.BadRequest, yanit.StatusCode);
        Assert.True((await yanit.Content.ReadFromJsonAsync<JsonElement>(Ct)).GetProperty("errors").TryGetProperty("userName", out _));
    }

    [Fact]
    public async Task AC8_Pasiflestirilen_kullanicinin_acik_oturumu_sonraki_istekte_duser_etkinlestirince_giris_yapar()
    {
        var ad = await KullaniciEkleAsync();
        var oturum = await OturumAsync(ad);
        var id = await KimlikAsync(ad);

        Assert.Equal(HttpStatusCode.NoContent, (await _factory.PanelIstemcisi().PostAsync($"/api/users/{id}/deactivate", null, Ct)).StatusCode);

        Assert.Equal(HttpStatusCode.Unauthorized, (await oturum.GetAsync("/api/panel/services", Ct)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await GirisAsync(ad, Sifre)).StatusCode);

        Assert.Equal(HttpStatusCode.NoContent, (await _factory.PanelIstemcisi().PostAsync($"/api/users/{id}/activate", null, Ct)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await GirisAsync(ad, Sifre)).StatusCode);
    }

    [Fact]
    public async Task AC8_Kullanici_kendini_pasiflestiremez()
    {
        var ad = await KullaniciEkleAsync();
        var oturum = await OturumAsync(ad);

        var yanit = await oturum.PostAsync($"/api/users/{await KimlikAsync(ad)}/deactivate", null, Ct);

        Assert.Equal(HttpStatusCode.Conflict, yanit.StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await oturum.GetAsync("/api/panel/services", Ct)).StatusCode);
    }

    [Fact]
    public async Task AC9_Sifre_degisince_eski_sifre_ve_diger_oturum_gecersiz_yeni_token_gecerli()
    {
        var ad = await KullaniciEkleAsync();
        var bu = await OturumAsync(ad);
        var diger = await OturumAsync(ad);

        var yanit = await bu.PostAsJsonAsync("/api/me/password", new { currentPassword = Sifre, newPassword = "yepyeni-sifre-456" }, Ct);
        Assert.Equal(HttpStatusCode.OK, yanit.StatusCode);
        var yeniToken = (await yanit.Content.ReadFromJsonAsync<JsonElement>(Ct)).GetProperty("token").GetString();

        Assert.Equal(HttpStatusCode.Unauthorized, (await diger.GetAsync("/api/panel/services", Ct)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await _factory.PanelIstemcisi(yeniToken).GetAsync("/api/panel/services", Ct)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await GirisAsync(ad, Sifre)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await GirisAsync(ad, "yepyeni-sifre-456")).StatusCode);
    }

    // Review 1, F-02: şifre değişikliği oturumu uzatmaz; yeni token girişteki bitişi taşır (R-10).
    [Fact]
    public async Task AC9_Sifre_degisikligi_oturumu_uzatmaz_yeni_token_girisin_bitisini_tasir()
    {
        var ad = await KullaniciEkleAsync();
        var id = await KimlikAsync(ad);
        await using var scope = _factory.Services.CreateAsyncScope();
        var kullanici = (await scope.ServiceProvider.GetRequiredService<IUserRepository>().FindByIdAsync(id, Ct))!;
        // Oturum 7 saat önce açılmış: bitişine 1 saat var.
        var eski = _factory.Services.GetRequiredService<IPanelTokenIssuer>().Issue(kullanici, DateTimeOffset.UtcNow.AddHours(-7));

        var yanit = await _factory.PanelIstemcisi(eski.Token)
            .PostAsJsonAsync("/api/me/password", new { currentPassword = Sifre, newPassword = "yepyeni-sifre-456" }, Ct);

        Assert.Equal(HttpStatusCode.OK, yanit.StatusCode);
        var yeniBitis = (await yanit.Content.ReadFromJsonAsync<JsonElement>(Ct)).GetProperty("expiresAt").GetDateTimeOffset();
        Assert.Equal(eski.ExpiresAt.ToUnixTimeSeconds(), yeniBitis.ToUnixTimeSeconds());
    }

    [Theory]
    [InlineData("yanlis-sifre-123", "yepyeni-sifre-456", "currentPassword")]
    [InlineData(Sifre, "kisa", "newPassword")]
    public async Task AC9_Mevcut_sifre_yanlissa_veya_yeni_sifre_kurala_uymuyorsa_degismez(string mevcut, string yeni, string alan)
    {
        var ad = await KullaniciEkleAsync();
        var oturum = await OturumAsync(ad);

        var yanit = await oturum.PostAsJsonAsync("/api/me/password", new { currentPassword = mevcut, newPassword = yeni }, Ct);

        Assert.Equal(HttpStatusCode.BadRequest, yanit.StatusCode);
        Assert.True((await yanit.Content.ReadFromJsonAsync<JsonElement>(Ct)).GetProperty("errors").TryGetProperty(alan, out _));
        Assert.Equal(HttpStatusCode.OK, (await oturum.GetAsync("/api/panel/services", Ct)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await GirisAsync(ad, Sifre)).StatusCode);
    }

    [Fact]
    public async Task AC10_Oturum_giristen_mutlak_8_saat_sonra_reddedilir()
    {
        var kullanici = _factory.PanelKullanicisi();
        var tokens = _factory.Services.GetRequiredService<IPanelTokenIssuer>();
        var simdi = DateTimeOffset.UtcNow;

        var bitmisToken = tokens.Issue(kullanici, simdi - TimeSpan.FromHours(8) - TimeSpan.FromSeconds(1));
        var gecerliToken = tokens.Issue(kullanici, simdi - TimeSpan.FromHours(8) + TimeSpan.FromMinutes(1));

        Assert.Equal(simdi - TimeSpan.FromSeconds(1), bitmisToken.ExpiresAt);
        Assert.Equal(HttpStatusCode.Unauthorized, (await _factory.PanelIstemcisi(bitmisToken.Token).GetAsync("/api/panel/services", Ct)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await _factory.PanelIstemcisi(gecerliToken.Token).GetAsync("/api/panel/services", Ct)).StatusCode);
    }

    private sealed class LogYakalayici : ILoggerProvider
    {
        public ConcurrentQueue<string> Satirlar { get; } = new();

        public ILogger CreateLogger(string categoryName) => new Yakalayici(this);

        public void Dispose()
        {
        }

        private sealed class Yakalayici(LogYakalayici sahip) : ILogger
        {
            public IDisposable? BeginScope<TState>(TState state)
                where TState : notnull => null;

            public bool IsEnabled(LogLevel logLevel) => true;

            public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter) =>
                sahip.Satirlar.Enqueue(formatter(state, exception) + " " + exception);
        }
    }
}
