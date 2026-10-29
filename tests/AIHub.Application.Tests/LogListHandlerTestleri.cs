using AIHub.Application.Logs;
using AIHub.Domain.Logs;

namespace AIHub.Application.Tests;

// Spec 0002: AC-3 (varsayılan 24 saat, özet), AC-5 (keyset sayfalama), AC-6 (geçersiz filtre), R-9 (arıza).
public class LogListHandlerTestleri
{
    private static readonly DateTimeOffset Simdi = new(2026, 10, 27, 12, 0, 0, TimeSpan.Zero);

    private static LogEntry Kayit(DateTimeOffset alinma, string message = "m", string servis = "odeme-servisi", LogLevel level = LogLevel.Error) =>
        LogEntry.Create(Guid.CreateVersion7(alinma), servis, level, message, null, null, null, null, alinma).Value;

    private static LogListQuery Sorgu(
        string? servis = null,
        string[]? levels = null,
        DateTimeOffset? from = null,
        DateTimeOffset? to = null,
        LogCursor? cursor = null) =>
        new(servis, levels ?? [], from, to, cursor);

    private static LogListHandler Handler(FakeLogEntryReader okuyucu) => new(okuyucu, new FakeClock(Simdi));

    [Fact]
    public async Task Filtre_verilmezse_son_24_saatin_kayitlari_en_yeni_once_doner()
    {
        var eski = Kayit(Simdi.AddHours(-25));
        var a = Kayit(Simdi.AddHours(-3));
        var b = Kayit(Simdi.AddHours(-2));
        var c = Kayit(Simdi.AddMinutes(-1));
        var okuyucu = new FakeLogEntryReader([eski, a, b, c]);

        var sonuc = await Handler(okuyucu).HandleAsync(Sorgu(), CancellationToken.None);

        Assert.True(sonuc.IsSuccess);
        Assert.Equal([c.Id, b.Id, a.Id], sonuc.Value.Items.Select(i => i.Id));
        Assert.Equal(Simdi.AddHours(-24), sonuc.Value.From);
        Assert.Equal(Simdi, sonuc.Value.To);
        Assert.Null(sonuc.Value.Older);
        Assert.Null(sonuc.Value.Newer);
    }

    [Fact]
    public async Task Ozet_ilk_satiri_200_karakterde_kisaltir_ve_isaretler()
    {
        var uzun = Kayit(Simdi.AddMinutes(-3), new string('x', 500));
        var cokSatir = Kayit(Simdi.AddMinutes(-2), "ilk satır\nikinci satır");
        var kisa = Kayit(Simdi.AddMinutes(-1), "tek satır");
        var okuyucu = new FakeLogEntryReader([uzun, cokSatir, kisa]);

        var sonuc = await Handler(okuyucu).HandleAsync(Sorgu(), CancellationToken.None);

        var ozetler = sonuc.Value.Items.ToDictionary(i => i.Id);
        Assert.Equal(new string('x', 200), ozetler[uzun.Id].MessagePreview);
        Assert.True(ozetler[uzun.Id].MessageTruncated);
        Assert.Equal("ilk satır", ozetler[cokSatir.Id].MessagePreview);
        Assert.True(ozetler[cokSatir.Id].MessageTruncated);
        Assert.Equal("tek satır", ozetler[kisa.Id].MessagePreview);
        Assert.False(ozetler[kisa.Id].MessageTruncated);
    }

    [Fact]
    public async Task Ozet_surrogate_ciftini_ortadan_bolmez()
    {
        var message = new string('x', 199) + "😀" + "devam";
        var okuyucu = new FakeLogEntryReader([Kayit(Simdi.AddMinutes(-1), message)]);

        var sonuc = await Handler(okuyucu).HandleAsync(Sorgu(), CancellationToken.None);

        var ozet = Assert.Single(sonuc.Value.Items);
        Assert.Equal(new string('x', 199), ozet.MessagePreview);
        Assert.True(ozet.MessageTruncated);
    }

    [Fact]
    public async Task Ters_aralik_31_gunu_asan_aralik_ve_tanimsiz_LogLevel_reddedilir()
    {
        var okuyucu = new FakeLogEntryReader([]);
        var handler = Handler(okuyucu);

        var ters = await handler.HandleAsync(Sorgu(from: Simdi, to: Simdi.AddHours(-1)), CancellationToken.None);
        var uzun = await handler.HandleAsync(Sorgu(from: Simdi.AddDays(-31).AddSeconds(-1), to: Simdi), CancellationToken.None);
        var seviye = await handler.HandleAsync(Sorgu(levels: ["Error", "Debug"]), CancellationToken.None);

        Assert.Equal("to", Assert.Single(ters.Errors).Field);
        Assert.Equal("from", Assert.Single(uzun.Errors).Field);
        var seviyeHatasi = Assert.Single(seviye.Errors);
        Assert.Equal("level", seviyeHatasi.Field);
        Assert.Contains("Debug", seviyeHatasi.Message, StringComparison.Ordinal);
        // Geçersiz filtrede liste sorgusu hiç yapılmaz (Spec 0002, R-8).
        Assert.Null(okuyucu.LastCriteria);
    }

    [Fact]
    public async Task Tam_31_gunluk_aralik_kabul_edilir()
    {
        var okuyucu = new FakeLogEntryReader([]);

        var sonuc = await Handler(okuyucu).HandleAsync(Sorgu(from: Simdi.AddDays(-31), to: Simdi), CancellationToken.None);

        Assert.True(sonuc.IsSuccess);
        Assert.Equal(Simdi.AddDays(-31), okuyucu.LastCriteria!.From);
    }

    [Fact]
    public async Task Servis_ve_LogLevel_filtresi_okuma_deposuna_aktarilir()
    {
        var okuyucu = new FakeLogEntryReader([
            Kayit(Simdi.AddMinutes(-3), servis: "odeme-servisi", level: LogLevel.Error),
            Kayit(Simdi.AddMinutes(-2), servis: "odeme-servisi", level: LogLevel.Info),
            Kayit(Simdi.AddMinutes(-1), servis: "kargo-servisi", level: LogLevel.Error),
        ]);

        var sonuc = await Handler(okuyucu).HandleAsync(Sorgu(servis: "odeme-servisi", levels: ["error", "Critical"]), CancellationToken.None);

        var satir = Assert.Single(sonuc.Value.Items);
        Assert.Equal("odeme-servisi", satir.ServiceName);
        Assert.Equal(LogLevel.Error, satir.Level);
        Assert.Equal([LogLevel.Error, LogLevel.Critical], okuyucu.LastCriteria!.Levels);
    }

    [Fact]
    public async Task Yuz_yirmi_kayit_50_50_20_sayfalanir_yeni_kayit_sonraki_sayfalari_kaydirmaz()
    {
        var kayitlar = Enumerable.Range(0, 120).Select(i => Kayit(Simdi.AddMinutes(-120 + i))).ToList();
        var okuyucu = new FakeLogEntryReader(kayitlar);
        var handler = Handler(okuyucu);

        var sayfa1 = (await handler.HandleAsync(Sorgu(), CancellationToken.None)).Value;
        // Kullanıcı 1. sayfadayken 5 yeni kayıt gelir: 1. sayfanın çözülmüş bitişinden (To) sonra.
        for (var i = 0; i < 5; i++)
        {
            okuyucu.Add(Kayit(Simdi.AddSeconds(1 + i)));
        }

        var sayfa2 = (await handler.HandleAsync(Sorgu(to: sayfa1.To, cursor: sayfa1.Older), CancellationToken.None)).Value;
        var sayfa3 = (await handler.HandleAsync(Sorgu(to: sayfa1.To, cursor: sayfa2.Older), CancellationToken.None)).Value;

        Assert.Equal([50, 50, 20], new[] { sayfa1.Items.Count, sayfa2.Items.Count, sayfa3.Items.Count });
        var gorulen = sayfa1.Items.Concat(sayfa2.Items).Concat(sayfa3.Items).Select(i => i.Id).ToList();
        Assert.Equal(kayitlar.Select(k => k.Id).Reverse(), gorulen);
        Assert.Null(sayfa1.Newer);
        Assert.Null(sayfa3.Older);

        // Geri yön: 3. sayfadan önceki sayfa yine 2. sayfanın aynısıdır.
        var geri2 = (await handler.HandleAsync(Sorgu(to: sayfa1.To, cursor: sayfa3.Newer), CancellationToken.None)).Value;
        Assert.Equal(sayfa2.Items.Select(i => i.Id), geri2.Items.Select(i => i.Id));
        var geri1 = (await handler.HandleAsync(Sorgu(to: sayfa1.To, cursor: geri2.Newer), CancellationToken.None)).Value;
        Assert.Equal(sayfa1.Items.Select(i => i.Id), geri1.Items.Select(i => i.Id));
        Assert.Null(geri1.Newer);
    }

    [Fact]
    public async Task Depo_okunamazsa_StoreUnavailable_doner()
    {
        var okuyucu = new FakeLogEntryReader([]) { Unavailable = true };

        var sonuc = await Handler(okuyucu).HandleAsync(Sorgu(), CancellationToken.None);

        Assert.Equal(LogQueryErrors.StoreUnavailable, Assert.Single(sonuc.Errors));
    }

    // Review 2, R2-02: varsayılan pencere imleçle taşınır. Sayfalar arasında zaman geçse de pencere
    // kaymaz ve sınırdaki kayıt atlanmaz; sayfa adresine kullanıcının seçmediği aralık yazılmaz.
    [Fact]
    public async Task Varsayilan_pencere_imlecle_tasinir_zaman_gecse_de_sinirdaki_kayit_atlanmaz()
    {
        var sinirdaki = Kayit(Simdi - LogListHandler.DefaultRange + TimeSpan.FromMinutes(1), "sinirdaki");
        var okuyucu = new FakeLogEntryReader([.. Enumerable.Range(0, 50).Select(i => Kayit(Simdi.AddMinutes(-60 + i))), sinirdaki]);

        var sayfa1 = (await Handler(okuyucu).HandleAsync(Sorgu(), CancellationToken.None)).Value;
        // 10 dakika sonra, kullanıcı aralık seçmeden yalnızca imleçle sonraki sayfayı ister.
        var onDakikaSonra = new LogListHandler(okuyucu, new FakeClock(Simdi.AddMinutes(10)));
        var sayfa2 = (await onDakikaSonra.HandleAsync(Sorgu(cursor: sayfa1.Older), CancellationToken.None)).Value;

        Assert.Equal(50, sayfa1.Items.Count);
        Assert.Equal(sinirdaki.Id, Assert.Single(sayfa2.Items).Id);
        Assert.Equal((sayfa1.From, sayfa1.To), (sayfa2.From, sayfa2.To));
    }
}
