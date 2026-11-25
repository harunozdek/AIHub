using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using AIHub.Api.Tests.Altyapi;
using AIHub.Application.Abstractions;
using AIHub.Domain.Logs;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;

namespace AIHub.Api.Tests;

// Spec 0006 — dashboard'un log özeti (AC-2, AC-3, AC-4) ve listeye geçişte sayının tutması (AC-6). Kesin
// sayılar için her test kendi boş veritabanında çalışır (plan 0006, risk 3).
[Trait("Category", "Integration")]
[Collection(IngestionCollection.Name)]
public sealed class DashboardTestleri(ContainerFixture containers) : IAsyncLifetime
{
    private const string Uc = "/api/panel/dashboard";

    private readonly List<AIHubFactory> _factoryler = [];

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public ValueTask InitializeAsync() => ValueTask.CompletedTask;

    public async ValueTask DisposeAsync()
    {
        foreach (var factory in _factoryler)
        {
            await factory.DisposeAsync();
        }
    }

    /// <summary>Boş bir veritabanı, onun üzerinde bir host ve panel istemcisi. Saat verilirse sunucunun saati odur.</summary>
    private async Task<(HttpClient Istemci, string Veritabani)> HazirlaAsync(DateTimeOffset? saat = null)
    {
        var veritabani = await TestVerisi.BosVeritabaniAsync(containers, Ct);
        var factory = new AIHubFactory(containers)
        {
            ConsumerEnabled = false,
            PostgresConnectionOverride = veritabani,
            TestServices = saat is { } an ? s => s.AddSingleton<IClock>(new SabitSaat(an)) : null,
        };
        _factoryler.Add(factory);
        return (factory.PanelIstemcisi(), veritabani);
    }

    private static async Task EkleAsync(string veritabani, string servis, DateTimeOffset alinma, string level = "Error", string? exceptionType = null, int adet = 1)
    {
        await using var ds = NpgsqlDataSource.Create(veritabani);
        await using var cmd = ds.CreateCommand(
            """
            INSERT INTO log_entries (id, service_name, level, message, exception_type, received_at)
            SELECT gen_random_uuid(), $1, $2, 'm', $3, $4 - i * interval '1 millisecond'
            FROM generate_series(0, $5 - 1) AS i
            """);
        cmd.Parameters.AddWithValue(servis);
        cmd.Parameters.AddWithValue(level);
        cmd.Parameters.AddWithValue((object?)exceptionType ?? DBNull.Value);
        cmd.Parameters.AddWithValue(alinma.ToUniversalTime());
        cmd.Parameters.AddWithValue(adet);
        await cmd.ExecuteNonQueryAsync(Ct);
    }

    private static async Task<JsonElement> OzetAsync(HttpClient istemci)
    {
        var yanit = await istemci.GetAsync(Uc, Ct);
        Assert.Equal(HttpStatusCode.OK, yanit.StatusCode);
        Assert.Equal("no-store", yanit.Headers.CacheControl?.ToString());
        return await yanit.Content.ReadFromJsonAsync<JsonElement>(cancellationToken: Ct);
    }

    [Fact]
    public async Task Derece_sayilari_son_24_saati_kapsar_kaydi_olmayan_derece_0_doner()
    {
        var (istemci, db) = await HazirlaAsync();
        var simdi = DateTimeOffset.UtcNow;
        await EkleAsync(db, "odeme", simdi.AddMinutes(-1), "Error", adet: 2);
        await EkleAsync(db, "odeme", simdi.AddHours(-3), "Info");
        await EkleAsync(db, "stok", simdi.AddHours(-23).AddMinutes(-58), "Critical");
        await EkleAsync(db, "stok", simdi.AddHours(-24).AddMinutes(-1), "Warning");
        await EkleAsync(db, "stok", simdi.AddDays(-3), "Error", adet: 5);

        var ozet = await OzetAsync(istemci);

        var dereceler = ozet.GetProperty("levels").EnumerateArray()
            .Select(l => (l.GetProperty("level").GetString(), l.GetProperty("count").GetInt64())).ToArray();
        Assert.Equal([("Critical", 1L), ("Error", 2L), ("Warning", 0L), ("Info", 1L)], dereceler);
        var from = ozet.GetProperty("from").GetDateTimeOffset();
        var to = ozet.GetProperty("to").GetDateTimeOffset();
        Assert.Equal(TimeSpan.FromHours(24), to - from);
        Assert.InRange(to, simdi.AddMinutes(-1), DateTimeOffset.UtcNow.AddMinutes(1));
    }

    [Fact]
    public async Task Hata_ureten_servisler_coktan_aza_esitlikte_alfabetik_ilk_10_doner()
    {
        var (istemci, db) = await HazirlaAsync();
        var simdi = DateTimeOffset.UtcNow.AddMinutes(-5);
        // 11 servis: "s01" 11 hata … "s11" 1 hata; "s05" ile "s04" eşit olsun diye "s05"e bir Critical eklenir.
        for (var i = 1; i <= 11; i++)
        {
            await EkleAsync(db, $"s{i:00}", simdi, "Error", adet: 12 - i);
        }

        await EkleAsync(db, "s05", simdi, "Critical");
        await EkleAsync(db, "aa-yalnizca-bilgi", simdi, "Info", adet: 50);
        await EkleAsync(db, "ab-yalnizca-uyari", simdi, "Warning", adet: 50);

        var servisler = (await OzetAsync(istemci)).GetProperty("topErrorServices").EnumerateArray()
            .Select(s => (s.GetProperty("serviceName").GetString(), s.GetProperty("count").GetInt64())).ToArray();

        Assert.Equal(
            [("s01", 11L), ("s02", 10L), ("s03", 9L), ("s04", 8L), ("s05", 8L), ("s06", 6L), ("s07", 5L), ("s08", 4L), ("s09", 3L), ("s10", 2L)],
            servisler);
    }

    // AC-3 (review 1, F-3): eşitlikte sıra sunucunun collation'ına bağlı değildir. Veritabanı bilerek ICU en-US ile
    // açılır; orada "kargo" "Odeme"den önce gelir, bayt sırasında tersidir. "demo-b" ile "demoa" iki sırada da
    // aynıdır; noktalama işaretinin harften önce geldiğini sabitler.
    [Fact]
    public async Task Hata_ureten_servislerde_esitlik_sirasi_veritabani_collationindan_bagimsizdir()
    {
        var ad = "icu_" + Guid.NewGuid().ToString("N")[..12];
        await using (var baglanti = new NpgsqlConnection(containers.Postgres.GetConnectionString()))
        {
            await baglanti.OpenAsync(Ct);
            await using var komut = new NpgsqlCommand($"CREATE DATABASE {ad} TEMPLATE template0 LOCALE_PROVIDER icu ICU_LOCALE 'en-US' LOCALE 'C'", baglanti);
            await komut.ExecuteNonQueryAsync(Ct);
        }

        var db = new NpgsqlConnectionStringBuilder(containers.Postgres.GetConnectionString()) { Database = ad }.ConnectionString;
        var factory = new AIHubFactory(containers) { ConsumerEnabled = false, PostgresConnectionOverride = db };
        _factoryler.Add(factory);
        var istemci = factory.PanelIstemcisi();
        var simdi = DateTimeOffset.UtcNow.AddMinutes(-5);
        foreach (var servis in new[] { "kargo", "Odeme", "demoa", "demo-b" })
        {
            await EkleAsync(db, servis, simdi, "Error", adet: 3);
        }

        var servisler = (await OzetAsync(istemci)).GetProperty("topErrorServices").EnumerateArray()
            .Select(s => s.GetProperty("serviceName").GetString()).ToArray();

        Assert.Equal(["Odeme", "demo-b", "demoa", "kargo"], servisler);
    }

    [Fact]
    public async Task Sik_exceptionlar_ilk_5_esitlikte_son_gorulen_once_exceptionsiz_kayit_sayilmaz()
    {
        var (istemci, db) = await HazirlaAsync();
        var simdi = DateTimeOffset.UtcNow;
        await EkleAsync(db, "odeme", simdi.AddMinutes(-10), exceptionType: "A", adet: 4);
        await EkleAsync(db, "odeme", simdi.AddMinutes(-30), exceptionType: "B", adet: 3);
        await EkleAsync(db, "odeme", simdi.AddMinutes(-2), exceptionType: "C", adet: 3);
        await EkleAsync(db, "odeme", simdi.AddMinutes(-40), exceptionType: "D", adet: 2);
        await EkleAsync(db, "odeme", simdi.AddMinutes(-50), exceptionType: "E", adet: 2);
        await EkleAsync(db, "odeme", simdi.AddMinutes(-60), exceptionType: "F", adet: 1);
        await EkleAsync(db, "odeme", simdi.AddMinutes(-1), exceptionType: null, adet: 20);
        await EkleAsync(db, "odeme", simdi.AddDays(-2), exceptionType: "Eski", adet: 30);

        var turler = (await OzetAsync(istemci)).GetProperty("topExceptionTypes").EnumerateArray().ToArray();

        Assert.Equal(["A", "C", "B", "D", "E"], turler.Select(t => t.GetProperty("exceptionType").GetString()));
        Assert.Equal([4L, 3L, 3L, 2L, 2L], turler.Select(t => t.GetProperty("count").GetInt64()));
        var cSonGorulme = turler[1].GetProperty("lastSeenAt").GetDateTimeOffset();
        Assert.InRange(cSonGorulme, simdi.AddMinutes(-2).AddSeconds(-1), simdi.AddMinutes(-2).AddSeconds(1));
    }

    [Fact]
    public async Task Bos_depoda_dort_derece_0_ve_listeler_bos_doner()
    {
        var (istemci, _) = await HazirlaAsync();

        var ozet = await OzetAsync(istemci);

        Assert.All(ozet.GetProperty("levels").EnumerateArray(), l => Assert.Equal(0, l.GetProperty("count").GetInt64()));
        Assert.Equal(4, ozet.GetProperty("levels").GetArrayLength());
        Assert.Equal(0, ozet.GetProperty("topErrorServices").GetArrayLength());
        Assert.Equal(0, ozet.GetProperty("topExceptionTypes").GetArrayLength());
    }

    // AC-6: dashboard'daki sayı, aynı pencere ve filtrelerle açılan listede sayfalar boyunca sayılan kayıtlara eşittir.
    [Fact]
    public async Task Ozetteki_sayilar_ayni_pencere_ve_filtrelerle_listede_sayfalar_boyunca_tutar()
    {
        var (istemci, db) = await HazirlaAsync();
        var simdi = DateTimeOffset.UtcNow;
        await EkleAsync(db, "odeme", simdi.AddMinutes(-3), "Error", "System.TimeoutException", adet: 60);
        await EkleAsync(db, "odeme", simdi.AddMinutes(-4), "Critical", adet: 7);
        await EkleAsync(db, "odeme", simdi.AddMinutes(-5), "Info", adet: 9);
        await EkleAsync(db, "stok", simdi.AddMinutes(-6), "Error", "System.TimeoutException", adet: 5);

        var ozet = await OzetAsync(istemci);
        var pencere = $"from={Uri.EscapeDataString(ozet.GetProperty("from").GetString()!)}&to={Uri.EscapeDataString(ozet.GetProperty("to").GetString()!)}";
        var derece = ozet.GetProperty("levels").EnumerateArray().Single(l => l.GetProperty("level").GetString() == "Error").GetProperty("count").GetInt64();
        var servis = ozet.GetProperty("topErrorServices")[0];
        var tur = ozet.GetProperty("topExceptionTypes")[0];

        Assert.Equal(derece, await SayfalarBoyuncaSayAsync(istemci, $"{pencere}&level=Error"));
        Assert.Equal(servis.GetProperty("count").GetInt64(), await SayfalarBoyuncaSayAsync(istemci, $"{pencere}&service=odeme&level=Error&level=Critical"));
        Assert.Equal(tur.GetProperty("count").GetInt64(), await SayfalarBoyuncaSayAsync(istemci, $"{pencere}&exceptionType={Uri.EscapeDataString(tur.GetProperty("exceptionType").GetString()!)}"));
        Assert.Equal((65L, 67L, 65L), (derece, servis.GetProperty("count").GetInt64(), tur.GetProperty("count").GetInt64()));
    }

    // AC-6 ve R-5 (review 1, F-2): boş, yalnızca boşluk ve başında/sonunda boşluk taşıyan ExceptionType'lar kabulde
    // (LogEntry.Create) tek kurala gelir; sayım ile süzme aynı değeri görür. Kayıtlar gerçek depo üzerinden yazılır.
    [Fact]
    public async Task Bos_ve_bosluklu_ExceptionType_sayilmaz_kirpilmis_deger_listede_ayni_sayiyi_verir()
    {
        var db = await TestVerisi.BosVeritabaniAsync(containers, Ct);
        var simdi = DateTimeOffset.UtcNow;
        var factory = new AIHubFactory(containers) { ConsumerEnabled = false, PostgresConnectionOverride = db };
        _factoryler.Add(factory);
        using (var scope = factory.Services.CreateScope())
        {
            var depo = scope.ServiceProvider.GetRequiredService<ILogEntryRepository>();
            foreach (var (girilen, dakika) in new[] { (" System.TimeoutException", 1), ("System.TimeoutException  ", 2), ("System.TimeoutException", 3), ("", 4), ("   ", 5), ("\t", 6) })
            {
                var kayit = LogEntry.Create(Guid.CreateVersion7(), "yeni", LogLevel.Error, "m", girilen, null, null, null, simdi.AddMinutes(-dakika)).Value;
                await depo.AddIfAbsentAsync(kayit, Ct);
            }
        }

        var istemci = factory.PanelIstemcisi();
        var ozet = await OzetAsync(istemci);
        var pencere = $"from={Uri.EscapeDataString(ozet.GetProperty("from").GetString()!)}&to={Uri.EscapeDataString(ozet.GetProperty("to").GetString()!)}";

        var tur = Assert.Single(ozet.GetProperty("topExceptionTypes").EnumerateArray());
        Assert.Equal("System.TimeoutException", tur.GetProperty("exceptionType").GetString());
        Assert.Equal(3L, tur.GetProperty("count").GetInt64());
        Assert.Equal(3L, await SayfalarBoyuncaSayAsync(istemci, $"{pencere}&exceptionType={Uri.EscapeDataString("System.TimeoutException")}"));
    }

    // Spec 0007, CB-1: pencere 24 eşit dilime bölünür; sınırdaki kayıtlar doğru dilime düşer, kaydı olmayan dilim
    // ve derece 0'dır, derecelerin dilim toplamı derece sayısına eşittir. Saat milisaniye altı hane taşır: pencere
    // sonu milisaniyeye kırpılır (plan 0007, risk 1).
    [Fact]
    public async Task Saatlik_trend_24_dilim_sinir_kayitlari_dogru_dilime_duser_derece_toplamlari_derece_sayilarina_esittir()
    {
        var saat = SaatMilisaniyeAltiHaneyle();
        var (istemci, db) = await HazirlaAsync(saat);
        var to = saat.AddTicks(-(saat.Ticks % TimeSpan.TicksPerMillisecond));
        var from = to.AddHours(-24);
        var birMikro = TimeSpan.FromTicks(TimeSpan.TicksPerMicrosecond);
        await EkleAsync(db, "a", from, "Info");
        await EkleAsync(db, "a", from.AddHours(1) - birMikro, "Error");
        await EkleAsync(db, "a", from.AddHours(1), "Critical");
        await EkleAsync(db, "a", from.AddHours(12).AddMinutes(30), "Warning", adet: 3);
        await EkleAsync(db, "a", to, "Error");
        await EkleAsync(db, "a", from - birMikro, "Error");

        var ozet = await OzetAsync(istemci);

        Assert.Equal(from, ozet.GetProperty("from").GetDateTimeOffset());
        Assert.Equal(to, ozet.GetProperty("to").GetDateTimeOffset());
        var dilimler = ozet.GetProperty("slices").EnumerateArray().ToArray();
        Assert.Equal(24, dilimler.Length);
        for (var i = 0; i < 24; i++)
        {
            Assert.Equal(from.AddHours(i), dilimler[i].GetProperty("from").GetDateTimeOffset());
            Assert.Equal(i == 23 ? to : from.AddHours(i + 1) - birMikro, dilimler[i].GetProperty("to").GetDateTimeOffset());
            Assert.Equal(["Critical", "Error", "Warning", "Info"], dilimler[i].GetProperty("levels").EnumerateArray().Select(l => l.GetProperty("level").GetString()));
        }

        var beklenen = new Dictionary<(int, string), long> { [(0, "Info")] = 1, [(0, "Error")] = 1, [(1, "Critical")] = 1, [(12, "Warning")] = 3, [(23, "Error")] = 1 };
        for (var i = 0; i < 24; i++)
        {
            foreach (var l in dilimler[i].GetProperty("levels").EnumerateArray())
            {
                var derece = l.GetProperty("level").GetString()!;
                Assert.Equal(beklenen.GetValueOrDefault((i, derece)), l.GetProperty("count").GetInt64());
            }
        }

        foreach (var l in ozet.GetProperty("levels").EnumerateArray())
        {
            var derece = l.GetProperty("level").GetString();
            var dilimToplami = dilimler.Sum(d => d.GetProperty("levels").EnumerateArray().Single(x => x.GetProperty("level").GetString() == derece).GetProperty("count").GetInt64());
            Assert.Equal(l.GetProperty("count").GetInt64(), dilimToplami);
        }
    }

    // Spec 0007, CB-2: her dilimin toplamı, dilimin from/to değerleriyle açılan listede sayfalar boyunca sayılan
    // kayıtlara eşittir; tam dilim başlangıcındaki ve bitişindeki kayıt iki dilimde birden sayılmaz.
    [Fact]
    public async Task Her_dilimin_toplami_ayni_aralikla_listede_sayfalar_boyunca_tutar()
    {
        var saat = SaatMilisaniyeAltiHaneyle();
        var (istemci, db) = await HazirlaAsync(saat);
        var to = saat.AddTicks(-(saat.Ticks % TimeSpan.TicksPerMillisecond));
        var from = to.AddHours(-24);
        await EkleAsync(db, "a", from.AddHours(3).AddMinutes(40), "Error", adet: 60);
        await EkleAsync(db, "b", from.AddHours(4), "Info");
        await EkleAsync(db, "b", from.AddHours(4) - TimeSpan.FromTicks(TimeSpan.TicksPerMicrosecond), "Critical");
        await EkleAsync(db, "c", to, "Warning", adet: 2);

        var ozet = await OzetAsync(istemci);

        var dilimler = ozet.GetProperty("slices").EnumerateArray().ToArray();
        var toplamlar = new List<long>();
        foreach (var i in new[] { 2, 3, 4, 22, 23 })
        {
            var dilim = dilimler[i];
            var aralik = $"from={Uri.EscapeDataString(dilim.GetProperty("from").GetString()!)}&to={Uri.EscapeDataString(dilim.GetProperty("to").GetString()!)}";
            var toplam = dilim.GetProperty("levels").EnumerateArray().Sum(l => l.GetProperty("count").GetInt64());
            Assert.Equal(toplam, await SayfalarBoyuncaSayAsync(istemci, aralik));
            toplamlar.Add(toplam);
        }

        Assert.Equal([0L, 61L, 1L, 0L, 2L], toplamlar);
    }

    private static async Task<JsonElement> OzetAsync(HttpClient istemci, string sorgu)
    {
        var yanit = await istemci.GetAsync($"{Uc}?{sorgu}", Ct);
        Assert.Equal(HttpStatusCode.OK, yanit.StatusCode);
        return await yanit.Content.ReadFromJsonAsync<JsonElement>(cancellationToken: Ct);
    }

    private static long DereceSayisi(JsonElement ozet, string derece) =>
        ozet.GetProperty("levels").EnumerateArray().Single(l => l.GetProperty("level").GetString() == derece).GetProperty("count").GetInt64();

    // Spec 0011, CB-1: her hazır pencere yüklenme anından geriye sabit uzunluktadır; dilim sayısı CB-3 tablosundandır.
    [Theory]
    [InlineData("24h", 24, 24)]
    [InlineData("48h", 48, 24)]
    [InlineData("7d", 7 * 24, 28)]
    [InlineData("14d", 14 * 24, 28)]
    [InlineData("30d", 30 * 24, 30)]
    [InlineData("90d", 90 * 24, 30)]
    [InlineData("180d", 180 * 24, 30)]
    [InlineData("365d", 365 * 24, 73)]
    public async Task Hazir_pencere_sabit_uzunlukta_ve_dilim_tablosuna_uygun_doner(string pencere, int saat, int dilimSayisi)
    {
        var an = SaatMilisaniyeAltiHaneyle();
        var (istemci, _) = await HazirlaAsync(an);
        var to = an.AddTicks(-(an.Ticks % TimeSpan.TicksPerMillisecond));

        var ozet = await OzetAsync(istemci, $"window={pencere}");

        Assert.Equal(pencere, ozet.GetProperty("window").GetString());
        Assert.Equal(to, ozet.GetProperty("to").GetDateTimeOffset());
        Assert.Equal(to.AddHours(-saat), ozet.GetProperty("from").GetDateTimeOffset());
        Assert.Equal(dilimSayisi, ozet.GetProperty("slices").GetArrayLength());
    }

    [Fact]
    public async Task Pencere_verilmezse_son_24_saat_doner()
    {
        var (istemci, _) = await HazirlaAsync();

        var ozet = await OzetAsync(istemci);

        Assert.Equal("24h", ozet.GetProperty("window").GetString());
        Assert.Equal(TimeSpan.FromHours(24), ozet.GetProperty("to").GetDateTimeOffset() - ozet.GetProperty("from").GetDateTimeOffset());
    }

    // Plan 0011, risk 4: API hatalı isteği sessizce düzeltmez; alan adıyla 400 döner.
    [Theory]
    [InlineData("window=1y", "window")]
    [InlineData("window=", "window")]
    [InlineData("window=24h&from=2026-11-01T00:00:00Z&to=2026-11-02T00:00:00Z", "window")]
    [InlineData("from=2026-11-01T00:00:00Z", "from")]
    [InlineData("from=2026-11-02T00:00:00Z&to=2026-11-01T00:00:00Z", "to")]
    [InlineData("from=2025-11-01T00:00:00Z&to=2026-11-01T01:00:00.001Z", "from")]
    [InlineData("from=2026-11-01T00:00:00&to=2026-11-02T00:00:00Z", "from")]
    public async Task Tanimsiz_pencere_ve_gecersiz_aralik_alan_adiyla_400_alir(string sorgu, string alan)
    {
        var (istemci, _) = await HazirlaAsync(new DateTimeOffset(2026, 11, 25, 12, 0, 0, TimeSpan.Zero));

        var yanit = await istemci.GetAsync($"{Uc}?{sorgu}", Ct);

        Assert.Equal(HttpStatusCode.BadRequest, yanit.StatusCode);
        var problem = await yanit.Content.ReadFromJsonAsync<JsonElement>(cancellationToken: Ct);
        Assert.True(problem.GetProperty("errors").TryGetProperty(alan, out _), $"Hata '{alan}' alanını belirtmedi: {problem}");
    }

    [Fact]
    public async Task Gelecekte_baslayan_aralik_400_alir()
    {
        var (istemci, _) = await HazirlaAsync();
        var yarin = DateTimeOffset.UtcNow.AddDays(1).ToString("yyyy-MM-ddTHH:mm:ssZ", System.Globalization.CultureInfo.InvariantCulture);
        var obur = DateTimeOffset.UtcNow.AddDays(2).ToString("yyyy-MM-ddTHH:mm:ssZ", System.Globalization.CultureInfo.InvariantCulture);

        var yanit = await istemci.GetAsync($"{Uc}?from={yarin}&to={obur}", Ct);

        Assert.Equal(HttpStatusCode.BadRequest, yanit.StatusCode);
    }

    // CB-2: seçilen pencere bütün log bölümlerine uygulanır; 2 gün önceki kayıt 24 saatte yok, 48 saatte var.
    [Fact]
    public async Task Secilen_pencere_derece_servis_ve_exception_bolumlerine_uygulanir()
    {
        var (istemci, db) = await HazirlaAsync();
        var simdi = DateTimeOffset.UtcNow;
        await EkleAsync(db, "yakin", simdi.AddHours(-1), "Error", "System.YakinException");
        await EkleAsync(db, "uzak", simdi.AddHours(-40), "Critical", "System.UzakException", adet: 3);

        var gun = await OzetAsync(istemci, "window=24h");
        var ikiGun = await OzetAsync(istemci, "window=48h");

        Assert.Equal(0, DereceSayisi(gun, "Critical"));
        Assert.Equal(["yakin"], gun.GetProperty("topErrorServices").EnumerateArray().Select(s => s.GetProperty("serviceName").GetString()));
        Assert.Equal(["System.YakinException"], gun.GetProperty("topExceptionTypes").EnumerateArray().Select(t => t.GetProperty("exceptionType").GetString()));

        Assert.Equal(3, DereceSayisi(ikiGun, "Critical"));
        Assert.Equal(["uzak", "yakin"], ikiGun.GetProperty("topErrorServices").EnumerateArray().Select(s => s.GetProperty("serviceName").GetString()));
        Assert.Equal(["System.UzakException", "System.YakinException"], ikiGun.GetProperty("topExceptionTypes").EnumerateArray().Select(t => t.GetProperty("exceptionType").GetString()));
    }

    // CB-3: 365 günde 73 dilim (5 gün); sınırdaki kayıt doğru dilime düşer; dilim toplamı derece sayısına eşittir.
    [Fact]
    public async Task Bir_yillik_pencere_73_dilim_sinir_kaydi_dogru_dilimde_toplamlar_tutar()
    {
        var an = SaatMilisaniyeAltiHaneyle();
        var (istemci, db) = await HazirlaAsync(an);
        var to = an.AddTicks(-(an.Ticks % TimeSpan.TicksPerMillisecond));
        var from = to.AddDays(-365);
        var birMikro = TimeSpan.FromTicks(TimeSpan.TicksPerMicrosecond);
        await EkleAsync(db, "a", from, "Info");
        await EkleAsync(db, "a", from.AddDays(5) - birMikro, "Error");
        await EkleAsync(db, "a", from.AddDays(5), "Critical");
        await EkleAsync(db, "a", to, "Warning");
        await EkleAsync(db, "a", from - birMikro, "Error");

        var ozet = await OzetAsync(istemci, "window=365d");

        var dilimler = ozet.GetProperty("slices").EnumerateArray().ToArray();
        Assert.Equal(73, dilimler.Length);
        Assert.Equal(from.AddDays(5), dilimler[1].GetProperty("from").GetDateTimeOffset());
        Assert.Equal(from.AddDays(5) - birMikro, dilimler[0].GetProperty("to").GetDateTimeOffset());
        Assert.Equal(to, dilimler[72].GetProperty("to").GetDateTimeOffset());
        long Dilimde(int i, string derece) => dilimler[i].GetProperty("levels").EnumerateArray().Single(l => l.GetProperty("level").GetString() == derece).GetProperty("count").GetInt64();
        Assert.Equal((1L, 1L, 1L, 1L), (Dilimde(0, "Info"), Dilimde(0, "Error"), Dilimde(1, "Critical"), Dilimde(72, "Warning")));
        Assert.Equal([1L, 1L, 1L, 1L], ozet.GetProperty("levels").EnumerateArray().Select(l => l.GetProperty("count").GetInt64()));
    }

    // CB-7: serbest aralık; dilim boyu uzunluğa göre (10 gün → 12 saat), son dilim aralığın sonunda biter, gelecekteki
    // bitiş yüklenme anına kırpılır. CB-4: dilim ve derece sayıları aynı aralıkla açılan listede tutar.
    [Fact]
    public async Task Serbest_aralik_dilimlenir_kirpilir_ve_listeyle_tutar()
    {
        var an = SaatMilisaniyeAltiHaneyle();
        var (istemci, db) = await HazirlaAsync(an);
        var simdi = an.AddTicks(-(an.Ticks % TimeSpan.TicksPerMillisecond));
        var from = new DateTimeOffset(simdi.UtcDateTime.Date, TimeSpan.Zero).AddDays(-20);
        var to = from.AddDays(10).AddHours(5);
        await EkleAsync(db, "a", from, "Error", adet: 1);
        await EkleAsync(db, "a", to, "Error", adet: 1);
        await EkleAsync(db, "a", to.AddMilliseconds(1), "Error", adet: 1);
        await EkleAsync(db, "a", from.AddDays(3), "Warning", adet: 55);

        string Iso(DateTimeOffset t) => Uri.EscapeDataString(t.ToString("O", System.Globalization.CultureInfo.InvariantCulture));
        var ozet = await OzetAsync(istemci, $"from={Iso(from)}&to={Iso(to)}");
        var kirpilmis = await OzetAsync(istemci, $"from={Iso(simdi.AddDays(-1))}&to={Iso(simdi.AddDays(3))}");

        Assert.Equal("custom", ozet.GetProperty("window").GetString());
        Assert.Equal(from, ozet.GetProperty("from").GetDateTimeOffset());
        Assert.Equal(to, ozet.GetProperty("to").GetDateTimeOffset());
        var dilimler = ozet.GetProperty("slices").EnumerateArray().ToArray();
        Assert.Equal(21, dilimler.Length);
        Assert.Equal(from.AddDays(10), dilimler[20].GetProperty("from").GetDateTimeOffset());
        Assert.Equal(2, DereceSayisi(ozet, "Error"));

        var aralik = $"from={Iso(from)}&to={Iso(to)}";
        Assert.Equal(DereceSayisi(ozet, "Warning"), await SayfalarBoyuncaSayAsync(istemci, $"{aralik}&level=Warning"));

        Assert.Equal(simdi, kirpilmis.GetProperty("to").GetDateTimeOffset());
    }

    // CB-8: son hatalar (Error, Critical) ve son uyarılar (Warning) ayrı; penceredeki en yeni en fazla 10, en yeni önce;
    // mesajın ilk satırının en fazla 120 karakteri; pencere dışındaki kayıt yer almaz.
    [Fact]
    public async Task Son_hatalar_ve_uyarilar_ayri_en_yeni_10_ve_kisa_onizleme_doner()
    {
        var (istemci, db) = await HazirlaAsync();
        var simdi = DateTimeOffset.UtcNow;
        await EkleAsync(db, "a", simdi.AddMinutes(-30), "Error", adet: 8);
        await EkleAsync(db, "b", simdi.AddMinutes(-10), "Critical", adet: 4);
        await EkleAsync(db, "c", simdi.AddMinutes(-5), "Info", adet: 3);
        await EkleAsync(db, "d", simdi.AddHours(-30), "Warning");
        await using (var ds = NpgsqlDataSource.Create(db))
        await using (var cmd = ds.CreateCommand(
            "INSERT INTO log_entries (id, service_name, level, message, received_at) VALUES (gen_random_uuid(), 'e', 'Warning', $1, $2)"))
        {
            cmd.Parameters.AddWithValue(new string('u', 130) + "\nikinci satır");
            cmd.Parameters.AddWithValue(simdi.AddMinutes(-1).ToUniversalTime());
            await cmd.ExecuteNonQueryAsync(Ct);
        }

        var ozet = await OzetAsync(istemci, "window=24h");

        var hatalar = ozet.GetProperty("recentErrors").EnumerateArray().ToArray();
        Assert.Equal(10, hatalar.Length);
        Assert.Equal([.. Enumerable.Repeat("b", 4), .. Enumerable.Repeat("a", 6)], hatalar.Select(h => h.GetProperty("serviceName").GetString()));
        var zamanlar = hatalar.Select(h => h.GetProperty("receivedAt").GetDateTimeOffset()).ToArray();
        Assert.Equal(zamanlar.OrderDescending(), zamanlar);

        var uyari = Assert.Single(ozet.GetProperty("recentWarnings").EnumerateArray());
        Assert.Equal(new string('u', 120), uyari.GetProperty("messagePreview").GetString());
        Assert.True(uyari.GetProperty("messageTruncated").GetBoolean());
        Assert.Equal("Warning", uyari.GetProperty("level").GetString());
    }

    /// <summary>Şimdiye yakın, milisaniye altı haneleri dolu bir an: kırpma ve dilim sınırları gerçekten sınanır.</summary>
    private static DateTimeOffset SaatMilisaniyeAltiHaneyle()
    {
        var simdi = DateTimeOffset.UtcNow;
        return simdi.AddTicks(-(simdi.Ticks % TimeSpan.TicksPerMillisecond) + 4567);
    }

    private sealed class SabitSaat(DateTimeOffset an) : IClock
    {
        public DateTimeOffset UtcNow => an;
    }

    private static async Task<long> SayfalarBoyuncaSayAsync(HttpClient istemci, string sorgu)
    {
        long toplam = 0;
        string? imlec = null;
        do
        {
            var adres = $"/api/panel/logs?{sorgu}{(imlec is null ? "" : "&cursor=" + Uri.EscapeDataString(imlec))}";
            var sayfa = await istemci.GetFromJsonAsync<JsonElement>(adres, Ct);
            toplam += sayfa.GetProperty("items").GetArrayLength();
            imlec = sayfa.GetProperty("older").GetString();
        }
        while (imlec is not null);

        return toplam;
    }
}
