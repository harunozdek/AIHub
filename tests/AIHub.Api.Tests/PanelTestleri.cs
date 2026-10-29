using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text.Json;
using AIHub.Api.Tests.Altyapi;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;

namespace AIHub.Api.Tests;

// Spec 0002 — panelin salt okunur yüzeyi: kimlik ayrımı, liste, filtre, keyset sayfalama, ayrıntı, hata durumları.
[Trait("Category", "Integration")]
[Collection(IngestionCollection.Name)]
public sealed class PanelTestleri(ContainerFixture containers) : IAsyncLifetime
{
    private readonly AIHubFactory _factory = new(containers) { ConsumerEnabled = false };

    public ValueTask InitializeAsync()
    {
        // Host başlar ve migration'lar uygulanır; doğrudan depoya yazan testler tabloyu hazır bulur.
        _ = _factory.CreateClient();
        return ValueTask.CompletedTask;
    }

    public ValueTask DisposeAsync() => _factory.DisposeAsync();

    private static async Task<JsonElement> JsonAsync(HttpResponseMessage yanit) =>
        await yanit.Content.ReadFromJsonAsync<JsonElement>(cancellationToken: TestContext.Current.CancellationToken);

    private static string Liste(string servis, string? ek = null) =>
        $"/api/panel/logs?service={Uri.EscapeDataString(servis)}{(ek is null ? "" : "&" + ek)}";

    private static string Iso(DateTimeOffset t) => Uri.EscapeDataString(t.ToUniversalTime().ToString("O"));

    private static Guid[] Kimlikler(JsonElement sayfa) =>
        sayfa.GetProperty("items").EnumerateArray().Select(i => i.GetProperty("id").GetGuid()).ToArray();

    public static TheoryData<string> YetkisizDurumlar => ["yok", "api-anahtari", "yabanci-imza", "suresi-dolmus", "yanlis-audience"];

    [Theory]
    [MemberData(nameof(YetkisizDurumlar))]
    public async Task Tokensiz_veya_API_anahtariyla_log_okuma_401_alir_ve_icerik_donmez(string durum)
    {
        var servis = TestVerisi.YeniServisAdi();
        var isaret = "gizli-icerik-" + Guid.NewGuid();
        var id = await TestVerisi.KayitEkleAsync(containers, servis, DateTimeOffset.UtcNow.AddMinutes(-1), message: isaret);

        var client = durum switch
        {
            "yok" => _factory.CreateClient(),
            "api-anahtari" => await _factory.AnahtarliIstemciAsync(containers, servis),
            "yabanci-imza" => _factory.PanelIstemcisi(TestToken.Create(key: new RsaSecurityKey(RSA.Create(2048)))),
            "suresi-dolmus" => _factory.PanelIstemcisi(TestToken.Create(lifetime: TimeSpan.FromMinutes(-10))),
            _ => _factory.PanelIstemcisi(TestToken.Create(audience: "baska-uygulama")),
        };

        foreach (var adres in new[] { Liste(servis), $"/api/panel/logs/{id}", "/api/panel/services" })
        {
            var yanit = await client.GetAsync(adres, TestContext.Current.CancellationToken);

            Assert.Equal(HttpStatusCode.Unauthorized, yanit.StatusCode);
            Assert.Equal("application/problem+json", yanit.Content.Headers.ContentType?.MediaType);
            var govde = await yanit.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
            Assert.DoesNotContain(isaret, govde, StringComparison.Ordinal);
            Assert.DoesNotContain(servis, govde, StringComparison.Ordinal);
        }
    }

    // R-7 (review 1, F-07): panel yüzeyindeki her uç yalnızca okur. Kayıt değiştiren veya silen bir uç
    // eklenirse (POST/PUT/PATCH/DELETE) bu test kırmızı olur.
    [Fact]
    public void Panel_yuzeyindeki_her_uc_yalnizca_GET_kabul_eder()
    {
        var uclar = _factory.Services.GetRequiredService<EndpointDataSource>().Endpoints
            .OfType<RouteEndpoint>()
            .Where(u => u.RoutePattern.RawText?.StartsWith("/api/panel", StringComparison.Ordinal) == true)
            .ToArray();

        Assert.Equal(3, uclar.Length);
        Assert.All(uclar, u => Assert.Equal(["GET"], u.Metadata.GetMetadata<IHttpMethodMetadata>()?.HttpMethods ?? []));
    }

    [Fact]
    public async Task Panel_tokeni_ingestion_yuzeyinde_401_alir()
    {
        var isaret = "isaret-" + Guid.NewGuid();
        var client = _factory.PanelIstemcisi();

        var yanit = await client.GonderAsync(new { level = "Error", message = isaret });

        Assert.Equal(HttpStatusCode.Unauthorized, yanit.StatusCode);
        Assert.DoesNotContain(await TestVerisi.StreamIcerigiAsync(containers), p => p.Contains(isaret, StringComparison.Ordinal));
        Assert.Equal(0, await TestVerisi.MesajliKayitSayisiAsync(containers, isaret));
    }

    [Fact]
    public async Task Varsayilan_liste_son_24_saati_en_yeni_once_dondurur()
    {
        var servis = TestVerisi.YeniServisAdi();
        var simdi = DateTimeOffset.UtcNow;
        await TestVerisi.KayitEkleAsync(containers, servis, simdi.AddHours(-25), message: "eski");
        var a = await TestVerisi.KayitEkleAsync(containers, servis, simdi.AddHours(-23), level: "Warning", message: "a");
        var b = await TestVerisi.KayitEkleAsync(containers, servis, simdi.AddHours(-2), message: "b\nikinci satır", exceptionType: "System.TimeoutException");
        var c = await TestVerisi.KayitEkleAsync(containers, servis, simdi.AddMinutes(-1), level: "Critical", message: new string('x', 500));

        var yanit = await _factory.PanelIstemcisi().GetAsync(Liste(servis), TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, yanit.StatusCode);
        Assert.Contains("no-store", yanit.Headers.CacheControl?.ToString() ?? "", StringComparison.Ordinal);
        var sayfa = await JsonAsync(yanit);
        Assert.Equal([c, b, a], Kimlikler(sayfa));
        var satirlar = sayfa.GetProperty("items").EnumerateArray().ToArray();
        Assert.Equal(servis, satirlar[1].GetProperty("serviceName").GetString());
        Assert.Equal("Error", satirlar[1].GetProperty("level").GetString());
        Assert.Equal("b", satirlar[1].GetProperty("messagePreview").GetString());
        Assert.True(satirlar[1].GetProperty("messageTruncated").GetBoolean());
        Assert.Equal("System.TimeoutException", satirlar[1].GetProperty("exceptionType").GetString());
        Assert.Equal(200, satirlar[0].GetProperty("messagePreview").GetString()!.Length);
        Assert.True(satirlar[0].GetProperty("messageTruncated").GetBoolean());
        Assert.Equal(JsonValueKind.Null, satirlar[2].GetProperty("exceptionType").ValueKind);
        Assert.Equal(JsonValueKind.Null, sayfa.GetProperty("older").ValueKind);
        Assert.Equal(JsonValueKind.Null, sayfa.GetProperty("newer").ValueKind);
    }

    [Fact]
    public async Task Filtreler_birlikte_uygulanir()
    {
        var servis = TestVerisi.YeniServisAdi();
        var baska = TestVerisi.YeniServisAdi();
        var simdi = DateTimeOffset.UtcNow;
        var uyan1 = await TestVerisi.KayitEkleAsync(containers, servis, simdi.AddHours(-3), level: "Error");
        var uyan2 = await TestVerisi.KayitEkleAsync(containers, servis, simdi.AddHours(-2), level: "Critical");
        await TestVerisi.KayitEkleAsync(containers, servis, simdi.AddHours(-2), level: "Info");
        await TestVerisi.KayitEkleAsync(containers, servis, simdi.AddHours(-10), level: "Error"); // aralık dışı
        await TestVerisi.KayitEkleAsync(containers, baska, simdi.AddHours(-2), level: "Error"); // başka servis

        var adres = Liste(servis, $"level=Error&level=Critical&from={Iso(simdi.AddHours(-5))}&to={Iso(simdi)}");
        var sayfa = await JsonAsync(await _factory.PanelIstemcisi().GetAsync(adres, TestContext.Current.CancellationToken));

        Assert.Equal([uyan2, uyan1], Kimlikler(sayfa));
    }

    [Fact]
    public async Task Servis_secenekleri_yalnizca_kaydi_olan_servislerdir()
    {
        var kayitli = TestVerisi.YeniServisAdi();
        var kayitsiz = TestVerisi.YeniServisAdi();
        await TestVerisi.KayitEkleAsync(containers, kayitli, DateTimeOffset.UtcNow.AddDays(-40));
        await TestVerisi.AnahtarEkleAsync(containers, kayitsiz);

        var sonuc = await JsonAsync(await _factory.PanelIstemcisi().GetAsync("/api/panel/services", TestContext.Current.CancellationToken));

        var servisler = sonuc.GetProperty("services").EnumerateArray().Select(s => s.GetString()).ToArray();
        Assert.Contains(kayitli, servisler);
        Assert.DoesNotContain(kayitsiz, servisler);
        Assert.Equal(servisler.Order(StringComparer.Ordinal), servisler);
    }

    [Fact]
    public async Task Yuz_yirmi_kayit_50_50_20_sayfalanir_yeni_kayit_kaydirmaz()
    {
        var servis = TestVerisi.YeniServisAdi();
        var simdi = DateTimeOffset.UtcNow;
        var kimlikler = new List<Guid>();
        for (var i = 0; i < 120; i++)
        {
            kimlikler.Add(await TestVerisi.KayitEkleAsync(containers, servis, simdi.AddMinutes(-121 + i)));
        }

        var client = _factory.PanelIstemcisi();
        async Task<JsonElement> SayfaAsync(string? ek) =>
            await JsonAsync(await client.GetAsync(Liste(servis, ek), TestContext.Current.CancellationToken));

        var sayfa1 = await SayfaAsync(null);
        // Kullanıcı 1. sayfadayken 5 yeni kayıt gelir.
        var yeniler = new List<Guid>();
        for (var i = 0; i < 5; i++)
        {
            yeniler.Add(await TestVerisi.KayitEkleAsync(containers, servis, DateTimeOffset.UtcNow.AddMilliseconds(i)));
        }

        var sayfa2 = await SayfaAsync($"cursor={sayfa1.GetProperty("older").GetString()}");
        var sayfa3 = await SayfaAsync($"cursor={sayfa2.GetProperty("older").GetString()}");

        Assert.Equal([50, 50, 20], new[] { Kimlikler(sayfa1).Length, Kimlikler(sayfa2).Length, Kimlikler(sayfa3).Length });
        var gorulen = Kimlikler(sayfa1).Concat(Kimlikler(sayfa2)).Concat(Kimlikler(sayfa3));
        Assert.Equal(Enumerable.Reverse(kimlikler), gorulen);
        Assert.Equal(JsonValueKind.Null, sayfa3.GetProperty("older").ValueKind);

        // Geri yön: önceki sayfalar yine aynı kayıtlardır. Liste, açıldığı andaki pencereyi gösterir (imleç
        // pencereyi taşır, review 2 R2-02): sonradan gelen kayıtlar bu sayfalara girmez, liste yeniden
        // açılınca en başta görünür (triage review 2, seçenek A, harun.ozdek).
        var geri2 = await SayfaAsync($"cursor={sayfa3.GetProperty("newer").GetString()}");
        Assert.Equal(Kimlikler(sayfa2), Kimlikler(geri2));
        var geri1 = await SayfaAsync($"cursor={geri2.GetProperty("newer").GetString()}");
        Assert.Equal(Kimlikler(sayfa1), Kimlikler(geri1));
        Assert.Equal(JsonValueKind.Null, geri1.GetProperty("newer").ValueKind);

        var yenidenAcilan = await SayfaAsync(null);
        Assert.Equal(Enumerable.Reverse(yeniler), Kimlikler(yenidenAcilan).Take(5));
        Assert.Equal(Kimlikler(sayfa1).Take(45), Kimlikler(yenidenAcilan).Skip(5));
    }

    [Theory]
    [InlineData("from=2026-10-27T10:00:00Z&to=2026-10-27T09:00:00Z", "to")]
    [InlineData("from=2026-09-01T00:00:00Z&to=2026-10-27T00:00:00Z", "from")]
    [InlineData("level=Debug", "level")]
    [InlineData("from=2026-10-27T10:00:00", "from")]
    [InlineData("cursor=bozuk-isaret", "cursor")]
    // Review 3, R3-02: pencere taşımayan eski 3 parçalı imleç (geçerli base64url) zararsızca reddedilir.
    [InlineData("cursor=bzo2Mzg5NzAwMDAwMDAwMDAwMDA6MDE5M2ExYjJjM2Q0N2U1ZjhhOWIwYzFkMmUzZjRhNWI", "cursor")]
    // Review 3, R3-04: varsayılan başlangıç tarih aralığının altına düşen bitiş 500 değil, 400 alır.
    [InlineData("to=0001-01-01T00:00:00Z", "to")]
    // Review 4, R4-01: aynı sınır uç ofsetlerle de (UTC'de alt sınırdan 23 saat) 400 alır.
    [InlineData("to=0001-01-01T09:00:00-14:00", "to")]
    [InlineData("to=0001-01-02T13:00:00%2B14:00", "to")]
    public async Task Gecersiz_filtre_alan_adiyla_400_alir(string ek, string alan)
    {
        var yanit = await _factory.PanelIstemcisi().GetAsync($"/api/panel/logs?{ek}", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.BadRequest, yanit.StatusCode);
        var problem = await JsonAsync(yanit);
        Assert.True(problem.GetProperty("errors").TryGetProperty(alan, out _), $"Hata '{alan}' alanını belirtmedi: {problem}");
    }

    [Fact]
    public async Task Tam_31_gunluk_aralik_kabul_edilir()
    {
        var yanit = await _factory.PanelIstemcisi().GetAsync(
            "/api/panel/logs?from=2026-09-26T00:00:00Z&to=2026-10-27T00:00:00Z", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, yanit.StatusCode);
    }

    // Review 4, R4-01: UTC'de alt sınırdan 26 saat uzak ama duvar saati 24 saat geri gidemeyen bitiş eskiden 500
    // alıyordu; tek temsille (UTC) geçerli bir penceredir. Uygulanan aralık UTC döner.
    [Fact]
    public async Task Negatif_ofsetli_cok_eski_bitis_500_almaz_pencere_UTC_doner()
    {
        var yanit = await _factory.PanelIstemcisi().GetAsync("/api/panel/logs?to=0001-01-01T12:00:00-14:00", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, yanit.StatusCode);
        var sayfa = await JsonAsync(yanit);
        Assert.Equal(new DateTimeOffset(1, 1, 1, 2, 0, 0, TimeSpan.Zero), sayfa.GetProperty("from").GetDateTimeOffset());
        Assert.Equal(new DateTimeOffset(1, 1, 2, 2, 0, 0, TimeSpan.Zero), sayfa.GetProperty("to").GetDateTimeOffset());
    }

    [Fact]
    public async Task Ayrinti_tum_alanlari_dondurur()
    {
        var servis = TestVerisi.YeniServisAdi();
        var alinma = new DateTimeOffset(2026, 10, 27, 8, 0, 0, 123, TimeSpan.Zero);
        var id = await TestVerisi.KayitEkleAsync(
            containers,
            servis,
            alinma,
            level: "Critical",
            message: "Tahsilat başarısız\nikinci satır",
            exceptionType: "System.InvalidOperationException",
            stackTrace: "at Odeme.Tahsil()\n   at Program.Main()",
            metadataJson: """{"siparisNo": 42, "kanal": {"ad": "web"}}""",
            kaynakZamani: new DateTimeOffset(2026, 10, 27, 10, 59, 0, TimeSpan.FromHours(3)));

        var yanit = await _factory.PanelIstemcisi().GetAsync($"/api/panel/logs/{id}", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, yanit.StatusCode);
        var kayit = await JsonAsync(yanit);
        Assert.Equal(id, kayit.GetProperty("id").GetGuid());
        Assert.Equal(servis, kayit.GetProperty("serviceName").GetString());
        Assert.Equal("Critical", kayit.GetProperty("level").GetString());
        Assert.Equal("Tahsilat başarısız\nikinci satır", kayit.GetProperty("message").GetString());
        Assert.Equal("System.InvalidOperationException", kayit.GetProperty("exceptionType").GetString());
        Assert.Equal("at Odeme.Tahsil()\n   at Program.Main()", kayit.GetProperty("stackTrace").GetString());
        Assert.Equal(42, kayit.GetProperty("metadata").GetProperty("siparisNo").GetInt32());
        Assert.Equal("web", kayit.GetProperty("metadata").GetProperty("kanal").GetProperty("ad").GetString());
        Assert.Equal(new DateTimeOffset(2026, 10, 27, 7, 59, 0, TimeSpan.Zero), kayit.GetProperty("occurredAt").GetDateTimeOffset());
        Assert.Equal(alinma, kayit.GetProperty("receivedAt").GetDateTimeOffset());
    }

    [Fact]
    public async Task Istege_bagli_alanlari_bos_kayit_bos_degerlerle_doner()
    {
        var id = await TestVerisi.KayitEkleAsync(containers, TestVerisi.YeniServisAdi(), DateTimeOffset.UtcNow.AddMinutes(-1));

        var kayit = await JsonAsync(await _factory.PanelIstemcisi().GetAsync($"/api/panel/logs/{id}", TestContext.Current.CancellationToken));

        foreach (var alan in new[] { "exceptionType", "stackTrace", "metadata", "occurredAt" })
        {
            Assert.Equal(JsonValueKind.Null, kayit.GetProperty(alan).ValueKind);
        }
    }

    [Theory]
    [InlineData("01900000-0000-7000-8000-000000000000")]
    [InlineData("bozuk-kimlik")]
    public async Task Var_olmayan_ve_bozuk_kimlik_404_alir(string id)
    {
        var yanit = await _factory.PanelIstemcisi().GetAsync($"/api/panel/logs/{id}", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.NotFound, yanit.StatusCode);
        Assert.Equal("application/problem+json", yanit.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task Depo_erisilemezse_ic_ayrintisiz_503_alir()
    {
        const string olmayanDepo = "Host=127.0.0.1;Port=1;Username=yok;Password=GizliParola9;Timeout=2";
        await using var factory = new AIHubFactory(containers) { MigrateOnStartup = false, ConsumerEnabled = false, PostgresConnectionOverride = olmayanDepo };
        var client = factory.PanelIstemcisi();

        foreach (var adres in new[] { "/api/panel/logs", $"/api/panel/logs/{Guid.CreateVersion7()}", "/api/panel/services" })
        {
            var yanit = await client.GetAsync(adres, TestContext.Current.CancellationToken);

            Assert.Equal(HttpStatusCode.ServiceUnavailable, yanit.StatusCode);
            Assert.Equal(TimeSpan.FromSeconds(5), yanit.Headers.RetryAfter?.Delta);
            var govde = await yanit.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
            foreach (var ic in new[] { "GizliParola9", "Port=1", "127.0.0.1", "Npgsql", "   at " })
            {
                Assert.DoesNotContain(ic, govde, StringComparison.Ordinal);
            }
        }
    }
}
