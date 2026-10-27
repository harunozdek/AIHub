using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using AIHub.Api.Tests.Altyapi;
using AIHub.Domain.Logs;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;

namespace AIHub.Api.Tests;

// Spec 0001 — kabul, doğrulama, kimlik ve kalıcılığın uçtan uca davranışı (gerçek PostgreSQL + Redis).
[Trait("Category", "Integration")]
[Collection(IngestionCollection.Name)]
public sealed class IngestionTestleri(ContainerFixture containers) : IAsyncLifetime
{
    private static readonly TimeSpan KaliciligaKadar = TimeSpan.FromSeconds(5);
    private readonly AIHubFactory _factory = new(containers);

    public ValueTask InitializeAsync() => ValueTask.CompletedTask;

    public ValueTask DisposeAsync() => _factory.DisposeAsync();

    [Fact]
    public async Task AC1_Gecerli_kayit_kabul_edilir_ve_5_sn_icinde_tum_alanlariyla_saklanir()
    {
        var servis = TestVerisi.YeniServisAdi();
        var client = await _factory.AnahtarliIstemciAsync(containers, servis);

        var yanit = await client.GonderAsync(new
        {
            level = "Error",
            message = "Tahsilat başarısız",
            exceptionType = "System.InvalidOperationException",
            stackTrace = "at Odeme.Tahsil()",
            timestamp = "2026-10-27T11:30:00+03:00",
            metadata = new { siparisNo = 42, kanal = "web" },
        });

        Assert.Equal(HttpStatusCode.Accepted, yanit.StatusCode);
        var id = (await yanit.Content.ReadFromJsonAsync<JsonElement>(cancellationToken: TestContext.Current.CancellationToken)).GetProperty("id").GetGuid();
        Assert.True(await TestVerisi.BekleAsync(async () => (await TestVerisi.KayitlariOkuAsync(containers, servis)).Count == 1, KaliciligaKadar),
            "Kabul edilen kayıt 5 saniye içinde depoda görünmedi.");
        var kayit = Assert.Single(await TestVerisi.KayitlariOkuAsync(containers, servis));
        Assert.Equal(id, kayit.Id);
        Assert.Equal(servis, kayit.ServiceName);
        Assert.Equal("Error", kayit.Level);
        Assert.Equal("Tahsilat başarısız", kayit.Message);
        Assert.Equal("System.InvalidOperationException", kayit.ExceptionType);
        Assert.Equal("at Odeme.Tahsil()", kayit.StackTrace);
        Assert.Equal(42, JsonDocument.Parse(kayit.Metadata!).RootElement.GetProperty("siparisNo").GetInt32());
        Assert.Equal(new DateTimeOffset(2026, 10, 27, 8, 30, 0, TimeSpan.Zero), kayit.OccurredAt);
        Assert.True(DateTimeOffset.UtcNow - kayit.ReceivedAt < TimeSpan.FromMinutes(1));
    }

    public static TheoryData<string> GecersizAnahtarDurumlari => ["yok", "taninmayan", "iptal"];

    [Theory]
    [MemberData(nameof(GecersizAnahtarDurumlari))]
    public async Task AC2_Anahtarsiz_taninmayan_veya_iptal_edilmis_anahtar_401_alir_ve_kayit_olusmaz(string durum)
    {
        var servis = TestVerisi.YeniServisAdi();
        var client = _factory.CreateClient();
        switch (durum)
        {
            case "taninmayan":
                client.DefaultRequestHeaders.Add("X-Api-Key", "aih_boyle_bir_anahtar_yok_000000");
                break;
            case "iptal":
                client.DefaultRequestHeaders.Add("X-Api-Key", await TestVerisi.AnahtarEkleAsync(containers, servis, iptalEdilmis: true));
                break;
        }

        var isaret = "isaret-" + Guid.NewGuid();
        var yanit = await client.GonderAsync(new { level = "Error", message = isaret });

        Assert.Equal(HttpStatusCode.Unauthorized, yanit.StatusCode);
        Assert.Equal("application/problem+json", yanit.Content.Headers.ContentType?.MediaType);
        // Sızan bir girdi başka bir ServiceName ile yazılabilir ve tüketici onu stream'den silebilir:
        // stream hemen, depo ise tüketicinin yazmasına yetecek beklemeden sonra ve servis adından
        // bağımsız olarak mesaj işaretiyle taranır (verify G-1).
        Assert.DoesNotContain(await TestVerisi.StreamIcerigiAsync(containers), p => p.Contains(isaret, StringComparison.Ordinal));
        await Task.Delay(1000, TestContext.Current.CancellationToken);
        Assert.Equal(0, await TestVerisi.MesajliKayitSayisiAsync(containers, isaret));
        Assert.Empty(await TestVerisi.KayitlariOkuAsync(containers, servis));
    }

    [Fact]
    public async Task AC3_Govdedeki_serviceName_yok_sayilir_anahtarin_servisi_saklanir()
    {
        var servis = TestVerisi.YeniServisAdi();
        var client = await _factory.AnahtarliIstemciAsync(containers, servis);

        var yanit = await client.GonderAsync(new { serviceName = "baska-servis", level = "Info", message = "merhaba" });

        Assert.Equal(HttpStatusCode.Accepted, yanit.StatusCode);
        Assert.True(await TestVerisi.BekleAsync(async () => (await TestVerisi.KayitlariOkuAsync(containers, servis)).Count == 1, KaliciligaKadar));
        Assert.Empty(await TestVerisi.KayitlariOkuAsync(containers, "baska-servis"));
    }

    [Theory]
    [InlineData("\"Debug\"")]
    [InlineData("\"\"")]
    [InlineData("null")]
    [InlineData("3")]
    public async Task AC4_Tanimsiz_bos_veya_eksik_LogLevel_400_alir(string levelJson)
    {
        var servis = TestVerisi.YeniServisAdi();
        var client = await _factory.AnahtarliIstemciAsync(containers, servis);

        var yanit = await client.GonderHamAsync($$"""{"level": {{levelJson}}, "message": "m"}""");

        Assert.Equal(HttpStatusCode.BadRequest, yanit.StatusCode);
        var problem = await yanit.Content.ReadFromJsonAsync<JsonElement>(cancellationToken: TestContext.Current.CancellationToken);
        Assert.True(problem.GetProperty("errors").TryGetProperty("level", out _));
        await Task.Delay(300, TestContext.Current.CancellationToken);
        Assert.Empty(await TestVerisi.KayitlariOkuAsync(containers, servis));
    }

    [Fact]
    public async Task AC4_Level_alani_hic_yoksa_400_alir()
    {
        var client = await _factory.AnahtarliIstemciAsync(containers, TestVerisi.YeniServisAdi());

        var yanit = await client.GonderHamAsync("""{"message": "m"}""");

        Assert.Equal(HttpStatusCode.BadRequest, yanit.StatusCode);
    }

    [Theory]
    [InlineData("Info")]
    [InlineData("Warning")]
    [InlineData("Error")]
    [InlineData("Critical")]
    public async Task AC4_Dort_gecerli_LogLevel_kabul_edilir_ve_aynen_saklanir(string level)
    {
        var servis = TestVerisi.YeniServisAdi();
        var client = await _factory.AnahtarliIstemciAsync(containers, servis);

        var yanit = await client.GonderAsync(new { level, message = "m" });

        Assert.Equal(HttpStatusCode.Accepted, yanit.StatusCode);
        Assert.True(await TestVerisi.BekleAsync(async () => (await TestVerisi.KayitlariOkuAsync(containers, servis)).Count == 1, KaliciligaKadar));
        Assert.Equal(level, Assert.Single(await TestVerisi.KayitlariOkuAsync(containers, servis)).Level);
    }

    [Theory]
    [InlineData("""{"level":"Info"}""", "message")]
    [InlineData("""{"level":"Info","message":"   "}""", "message")]
    [InlineData("""{"level":"Info","message":"m","metadata":[1,2]}""", "metadata")]
    [InlineData("""{"level":"Info","message":"m","metadata":"metin"}""", "metadata")]
    [InlineData("""{"level":"Info","message":"m","timestamp":"dün akşam"}""", "timestamp")]
    [InlineData("""{"level":"Info","message":"m","timestamp":"2026-10-27T10:00:00"}""", "timestamp")]
    [InlineData("""{"level":"Info","message":"m","timestamp":12345}""", "timestamp")]
    [InlineData("""{"level":"Info","message":"m","exceptionType":42}""", "exceptionType")]
    [InlineData("""{"level":"Info","message":"a\u0000b"}""", "message")]
    [InlineData("""{"level":"Info","message":"m","stackTrace":"at X\u0000"}""", "stackTrace")]
    [InlineData("""{"level":"Info","message":"m","metadata":{"k":["ok","x\u0000y"]}}""", "metadata")]
    [InlineData("""{"level":"Info","message":"m","metadata":{"a\u0000b":1}}""", "metadata")]
    [InlineData("""{"level":"Info","message":"\ud800"}""", "message")]
    [InlineData("""{"level":"Info","message":"m","metadata":{"k":"\udc00"}}""", "metadata")]
    [InlineData("""{"level":"Info","message":"m","metadata":{"\udc00":1}}""", "body")]
    [InlineData("""{"\ud800":1,"level":"Info","message":"m"}""", "body")]
    [InlineData("""{"level":"Error","level":"Info","message":"m"}""", "body")]
    [InlineData("""{"level":"Info","message":"m","metadata":{"k":1,"k":2}}""", "body")]
    [InlineData("""bozuk json""", "body")]
    [InlineData("""[1,2,3]""", "body")]
    public async Task AC5_Gecersiz_gonderim_alan_adiyla_400_alir_ve_kayit_olusmaz(string govde, string alan)
    {
        var servis = TestVerisi.YeniServisAdi();
        var client = await _factory.AnahtarliIstemciAsync(containers, servis);

        var yanit = await client.GonderHamAsync(govde);

        Assert.Equal(HttpStatusCode.BadRequest, yanit.StatusCode);
        var problem = await yanit.Content.ReadFromJsonAsync<JsonElement>(cancellationToken: TestContext.Current.CancellationToken);
        Assert.True(problem.GetProperty("errors").TryGetProperty(alan, out _), $"Hata '{alan}' alanını belirtmedi: {problem}");
        await Task.Delay(300, TestContext.Current.CancellationToken);
        Assert.Empty(await TestVerisi.KayitlariOkuAsync(containers, servis));
    }

    [Fact]
    public async Task AC5_Gecersiz_UTF8_bayti_iceren_govde_400_alir_ve_kayit_olusmaz()
    {
        var servis = TestVerisi.YeniServisAdi();
        var client = await _factory.AnahtarliIstemciAsync(containers, servis);
        // 0xC3'ten sonra devam baytı yerine '(' gelir; gövde geçerli UTF-8 değildir (review 2, B-1).
        byte[] govde = [.. """{"level":"Info","message":"m"""u8, 0xC3, (byte)'(', .. "\"}"u8];

        var yanit = await client.GonderBaytAsync(govde);

        Assert.Equal(HttpStatusCode.BadRequest, yanit.StatusCode);
        var problem = await yanit.Content.ReadFromJsonAsync<JsonElement>(cancellationToken: TestContext.Current.CancellationToken);
        Assert.True(problem.GetProperty("errors").TryGetProperty("message", out _), $"Hata 'message' alanını belirtmedi: {problem}");
        await Task.Delay(300, TestContext.Current.CancellationToken);
        Assert.Empty(await TestVerisi.KayitlariOkuAsync(containers, servis));
    }

    [Fact]
    public async Task AC5_Message_ve_ExceptionType_siniri_asilinca_400_alir()
    {
        var client = await _factory.AnahtarliIstemciAsync(containers, TestVerisi.YeniServisAdi());

        var uzunMesaj = await client.GonderAsync(new { level = "Info", message = new string('m', LogEntryLimits.MessageMaxLength + 1) });
        var uzunTip = await client.GonderAsync(new { level = "Info", message = "m", exceptionType = new string('e', LogEntryLimits.ExceptionTypeMaxLength + 1) });

        Assert.Equal(HttpStatusCode.BadRequest, uzunMesaj.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, uzunTip.StatusCode);
    }

    [Fact]
    public async Task AC5_Tam_sinirdaki_gonderim_kabul_edilir_bir_bayt_fazlasi_413_alir()
    {
        var servis = TestVerisi.YeniServisAdi();
        var client = await _factory.AnahtarliIstemciAsync(containers, servis);

        var sinirda = await client.GonderHamAsync(TamBoyuttaGovde(LogEntryLimits.RequestBodyMaxBytes));
        var fazla = await client.GonderHamAsync(TamBoyuttaGovde(LogEntryLimits.RequestBodyMaxBytes + 1));

        Assert.Equal(HttpStatusCode.Accepted, sinirda.StatusCode);
        Assert.Equal(HttpStatusCode.RequestEntityTooLarge, fazla.StatusCode);
        Assert.True(await TestVerisi.BekleAsync(async () => (await TestVerisi.KayitlariOkuAsync(containers, servis)).Count == 1, KaliciligaKadar));
        await Task.Delay(300, TestContext.Current.CancellationToken);
        Assert.Single(await TestVerisi.KayitlariOkuAsync(containers, servis));
    }

    [Fact]
    public async Task AC5_Message_ve_ExceptionType_tam_sinirda_kabul_edilir()
    {
        var client = await _factory.AnahtarliIstemciAsync(containers, TestVerisi.YeniServisAdi());

        var yanit = await client.GonderAsync(new
        {
            level = "Info",
            message = new string('m', LogEntryLimits.MessageMaxLength),
            exceptionType = new string('e', LogEntryLimits.ExceptionTypeMaxLength),
        });

        Assert.Equal(HttpStatusCode.Accepted, yanit.StatusCode);
    }

    [Fact]
    public async Task AC5_Sinirdaki_Message_hassas_veri_icerse_de_kabul_edilir_ve_maskeli_saklanir()
    {
        var servis = TestVerisi.YeniServisAdi();
        var client = await _factory.AnahtarliIstemciAsync(containers, servis);
        var ham = new string('m', LogEntryLimits.MessageMaxLength - 12) + " 10000000146";

        var yanit = await client.GonderAsync(new { level = "Info", message = ham });

        Assert.Equal(HttpStatusCode.Accepted, yanit.StatusCode);
        Assert.True(await TestVerisi.BekleAsync(async () => (await TestVerisi.KayitlariOkuAsync(containers, servis)).Count == 1, KaliciligaKadar));
        var kayit = Assert.Single(await TestVerisi.KayitlariOkuAsync(containers, servis));
        Assert.EndsWith(" [MASKED:tckn]", kayit.Message);
        Assert.DoesNotContain("10000000146", kayit.Message);
    }

    [Fact]
    public async Task AC6_Yalnizca_zorunlu_alanlarla_kabul_edilir_istege_bagli_alanlar_bos_saklanir()
    {
        var servis = TestVerisi.YeniServisAdi();
        var client = await _factory.AnahtarliIstemciAsync(containers, servis);

        var yanit = await client.GonderAsync(new { level = "Warning", message = "yalnızca zorunlu" });

        Assert.Equal(HttpStatusCode.Accepted, yanit.StatusCode);
        Assert.True(await TestVerisi.BekleAsync(async () => (await TestVerisi.KayitlariOkuAsync(containers, servis)).Count == 1, KaliciligaKadar));
        var kayit = Assert.Single(await TestVerisi.KayitlariOkuAsync(containers, servis));
        Assert.Null(kayit.ExceptionType);
        Assert.Null(kayit.StackTrace);
        Assert.Null(kayit.Metadata);
        Assert.Null(kayit.OccurredAt);
        Assert.True(DateTimeOffset.UtcNow - kayit.ReceivedAt < TimeSpan.FromMinutes(1));
    }

    [Fact]
    public async Task AC7_Ham_hassas_deger_ne_kuyrukta_ne_depoda_bulunur()
    {
        var servis = TestVerisi.YeniServisAdi();
        // Önce tüketicisi kapalı bir host: girdi stream'de durur ve içeriği incelenebilir.
        await using var tuketicisiz = new AIHubFactory(containers) { ConsumerEnabled = false };
        var client = await tuketicisiz.AnahtarliIstemciAsync(containers, servis);
        var hamDegerler = new[] { "ali@ornek.com", "10000000146", "tok3nDegeri", "Gizli1", "aih_0123456789abcdefXYZ", "anahtar.adi@ornek.com" };

        var yanit = await client.GonderAsync(new
        {
            level = "Error",
            message = "ali@ornek.com için anahtar aih_0123456789abcdefXYZ",
            stackTrace = "at Db.Open(Host=db01;Password=Gizli1;)",
            metadata = new Dictionary<string, object>
            {
                ["musteri"] = new { tckn = 10000000146, eposta = "ali@ornek.com" },
                ["authorization"] = "Bearer tok3nDegeri",
                // Hassas veri anahtar adında: gerçek stream ve depoda da maskelenmeli (review 2, B-5).
                ["anahtar.adi@ornek.com"] = "anahtar-adi-degeri",
            },
        });

        Assert.Equal(HttpStatusCode.Accepted, yanit.StatusCode);
        var stream = string.Join("\n", await TestVerisi.StreamIcerigiAsync(containers));
        Assert.Contains("[MASKED:email]", stream);
        foreach (var ham in hamDegerler)
        {
            Assert.DoesNotContain(ham, stream);
        }

        // Sonra tüketici çalışır ve kaydı depoya yazar.
        _ = _factory.CreateClient();
        Assert.True(await TestVerisi.BekleAsync(async () => (await TestVerisi.KayitlariOkuAsync(containers, servis)).Count == 1, KaliciligaKadar));
        var kayit = Assert.Single(await TestVerisi.KayitlariOkuAsync(containers, servis));
        var depodaki = kayit.Message + kayit.StackTrace + kayit.Metadata;
        Assert.Equal("[MASKED:email] için anahtar [MASKED:api-key]", kayit.Message);
        Assert.Equal("at Db.Open(Host=db01;Password=[MASKED:password];)", kayit.StackTrace);
        Assert.Contains("[MASKED:tckn]", kayit.Metadata);
        Assert.Contains("[MASKED:bearer-token]", kayit.Metadata);
        Assert.Contains("anahtar-adi-degeri", kayit.Metadata);
        foreach (var ham in hamDegerler)
        {
            Assert.DoesNotContain(ham, depodaki);
        }
    }

    [Fact]
    public async Task AC11_Hicbir_hata_yanitinda_anahtar_baglanti_dizesi_veya_yigin_izi_bulunmaz()
    {
        var gizliAnahtar = "aih_gizli_anahtar_sizmamali_0001";
        await using var patlayan = new AIHubFactory(containers)
        {
            ConsumerEnabled = false,
            TestServices = s => s.AddScoped<AIHub.Application.Abstractions.IApiKeyStore, PatlayanAnahtarDeposu>(),
        };
        var client = await _factory.AnahtarliIstemciAsync(containers, TestVerisi.YeniServisAdi());
        var yetkisiz = _factory.CreateClient();
        yetkisiz.DefaultRequestHeaders.Add("X-Api-Key", gizliAnahtar);
        var patlayanIstemci = patlayan.CreateClient();
        patlayanIstemci.DefaultRequestHeaders.Add("X-Api-Key", gizliAnahtar);

        var yanitlar = new[]
        {
            await yetkisiz.GonderAsync(new { level = "Info", message = "m" }),                                   // 401
            await client.GonderHamAsync("""{"level":"Debug","message":"Password=Gizli1"}"""),                   // 400
            await client.GonderHamAsync(TamBoyuttaGovde(LogEntryLimits.RequestBodyMaxBytes + 1)),               // 413
            await patlayanIstemci.GonderAsync(new { level = "Info", message = "m" }),                            // 500
        };

        Assert.Equal(
            [HttpStatusCode.Unauthorized, HttpStatusCode.BadRequest, HttpStatusCode.RequestEntityTooLarge, HttpStatusCode.InternalServerError],
            yanitlar.Select(y => y.StatusCode));
        foreach (var yanit in yanitlar)
        {
            Assert.Equal("application/problem+json", yanit.Content.Headers.ContentType?.MediaType);
            var govde = await yanit.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
            Assert.DoesNotContain(gizliAnahtar, govde);
            Assert.DoesNotContain("Gizli1", govde);
            Assert.DoesNotContain(PatlayanAnahtarDeposu.SizmamasiGerekenMetin, govde);
            Assert.DoesNotContain("   at ", govde);
            Assert.DoesNotContain("Exception", govde);
        }
    }

    [Fact]
    public async Task Saklanmis_kayit_degistirilemez_UPDATE_veritabaninda_reddedilir()
    {
        var servis = TestVerisi.YeniServisAdi();
        var client = await _factory.AnahtarliIstemciAsync(containers, servis);
        await client.GonderAsync(new { level = "Info", message = "ilk hali" });
        Assert.True(await TestVerisi.BekleAsync(async () => (await TestVerisi.KayitlariOkuAsync(containers, servis)).Count == 1, KaliciligaKadar));

        await using var ds = NpgsqlDataSource.Create(containers.Postgres.GetConnectionString());
        await using var update = ds.CreateCommand("UPDATE log_entries SET message = 'degisti' WHERE service_name = $1");
        update.Parameters.AddWithValue(servis);

        await Assert.ThrowsAsync<PostgresException>(() => update.ExecuteNonQueryAsync(TestContext.Current.CancellationToken));
        Assert.Equal("ilk hali", Assert.Single(await TestVerisi.KayitlariOkuAsync(containers, servis)).Message);
    }

    /// <summary>ASCII bir gövde: stackTrace dolgusuyla tam <paramref name="bayt"/> uzunluğunda.</summary>
    private static string TamBoyuttaGovde(int bayt)
    {
        const string bas = """{"level":"Info","message":"boyut","stackTrace":" """;
        const string son = "\"}";
        var govde = bas.TrimEnd() + new string('x', bayt - Encoding.UTF8.GetByteCount(bas.TrimEnd() + son)) + son;
        Assert.Equal(bayt, Encoding.UTF8.GetByteCount(govde));
        return govde;
    }
}

internal sealed class PatlayanAnahtarDeposu : AIHub.Application.Abstractions.IApiKeyStore
{
    public const string SizmamasiGerekenMetin = "Host=gizli-db;Password=SuperGizli";

    public Task<AIHub.Domain.Common.Result<string?>> FindServiceNameAsync(string rawApiKey, CancellationToken cancellationToken) =>
        throw new InvalidOperationException($"Beklenmeyen hata, baglanti: {SizmamasiGerekenMetin}");
}
