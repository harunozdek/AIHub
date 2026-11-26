using AIHub.Application.Dashboard;
using AIHub.Application.Logs;
using AIHub.Domain.Logs;

namespace AIHub.Application.Tests.Dashboard;

// Spec 0006: AC-2 (pencere, dört derece, 0 değerleri), AC-3/AC-4 (sınırlar), R-9 (arıza). Sıralama ve eşitlik
// kuralları okuma deposunun sözleşmesidir; gerçek SQL ile Api entegrasyon testlerinde kanıtlanır.
public class DashboardHandlerTestleri
{
    private static readonly DateTimeOffset Simdi = new(2026, 11, 9, 12, 0, 0, TimeSpan.Zero);

    private static LogEntry Kayit(DateTimeOffset alinma, LogLevel level, string servis = "odeme-servisi", string? exceptionType = null) =>
        LogEntry.Create(Guid.CreateVersion7(alinma), servis, level, "m", exceptionType, null, null, null, alinma).Value;

    private static DashboardHandler Handler(FakeLogEntryReader okuyucu) => new(okuyucu, new FakeClock(Simdi));

    [Fact]
    public async Task Pencere_yuklenme_anindan_geriye_24_saattir()
    {
        var okuyucu = new FakeLogEntryReader([]);

        var sonuc = await Handler(okuyucu).HandleAsync(CancellationToken.None);

        Assert.True(sonuc.IsSuccess);
        Assert.Equal(Simdi.AddHours(-24), sonuc.Value.From);
        Assert.Equal(Simdi, sonuc.Value.To);
        Assert.Equal((Simdi.AddHours(-24), Simdi), okuyucu.LastWindow);
    }

    [Fact]
    public async Task Dort_derece_agirdan_hafife_doner_kaydi_olmayan_0_ve_24_saatten_eski_sayilmaz()
    {
        var okuyucu = new FakeLogEntryReader(
        [
            Kayit(Simdi.AddHours(-1), LogLevel.Error),
            Kayit(Simdi.AddHours(-2), LogLevel.Error),
            Kayit(Simdi.AddHours(-3), LogLevel.Info),
            Kayit(Simdi.AddHours(-24), LogLevel.Critical),
            Kayit(Simdi.AddHours(-25), LogLevel.Warning),
        ]);

        var sonuc = await Handler(okuyucu).HandleAsync(CancellationToken.None);

        Assert.Equal(
            [new LevelCount(LogLevel.Critical, 1), new LevelCount(LogLevel.Error, 2), new LevelCount(LogLevel.Warning, 0), new LevelCount(LogLevel.Info, 1)],
            sonuc.Value.Levels);
    }

    [Fact]
    public async Task Servis_ve_exception_ozetleri_sinirlarla_istenir_ve_doner()
    {
        var okuyucu = new FakeLogEntryReader(
        [
            Kayit(Simdi.AddMinutes(-1), LogLevel.Critical, "a", "TimeoutException"),
            Kayit(Simdi.AddMinutes(-2), LogLevel.Warning, "b", "IOException"),
        ]);

        var sonuc = await Handler(okuyucu).HandleAsync(CancellationToken.None);

        Assert.Equal([DashboardHandler.TopServiceCount, DashboardHandler.TopExceptionTypeCount], okuyucu.RequestedLimits);
        Assert.Equal([new ServiceErrorCount("a", 1)], sonuc.Value.TopErrorServices);
        Assert.Equal(["TimeoutException", "IOException"], sonuc.Value.TopExceptionTypes.Select(t => t.ExceptionType));
    }

    // Spec 0007, CB-1: 24 dilim; her dilimde dört derece, kaydı olmayan 0; dilim sonu bir sonraki dilimden 1 µs
    // öncedir, son dilim pencerenin sonuyla biter (plan 0007, risk 1).
    [Fact]
    public async Task Saatlik_trend_24_dilim_sifirla_doldurulur_sinirlar_listeyle_ortusur()
    {
        var okuyucu = new FakeLogEntryReader(
        [
            Kayit(Simdi.AddHours(-24), LogLevel.Info),
            Kayit(Simdi.AddHours(-23).AddTicks(-TimeSpan.TicksPerMicrosecond), LogLevel.Error),
            Kayit(Simdi.AddHours(-23), LogLevel.Critical),
            Kayit(Simdi, LogLevel.Warning),
        ]);

        var sonuc = await Handler(okuyucu).HandleAsync(CancellationToken.None);

        var dilimler = sonuc.Value.Slices;
        Assert.Equal(24, dilimler.Count);
        Assert.Equal(Simdi.AddHours(-24), dilimler[0].From);
        Assert.Equal(Simdi.AddHours(-23).AddTicks(-TimeSpan.TicksPerMicrosecond), dilimler[0].To);
        Assert.Equal(Simdi.AddHours(-23), dilimler[1].From);
        Assert.Equal(Simdi, dilimler[23].To);
        Assert.All(dilimler, d => Assert.Equal([LogLevel.Critical, LogLevel.Error, LogLevel.Warning, LogLevel.Info], d.Levels.Select(l => l.Level)));
        Assert.Equal([0, 1, 0, 1], dilimler[0].Levels.Select(l => l.Count));
        Assert.Equal([1, 0, 0, 0], dilimler[1].Levels.Select(l => l.Count));
        Assert.Equal([0, 0, 1, 0], dilimler[23].Levels.Select(l => l.Count));
        Assert.All(dilimler.Skip(2).Take(21), d => Assert.All(d.Levels, l => Assert.Equal(0, l.Count)));
    }

    [Fact]
    public async Task Pencere_sonu_milisaniyeye_kirpilir()
    {
        var saat = Simdi.AddTicks(1234567);
        var okuyucu = new FakeLogEntryReader([]);

        var sonuc = await new DashboardHandler(okuyucu, new FakeClock(saat)).HandleAsync(CancellationToken.None);

        Assert.Equal(Simdi.AddMilliseconds(123), sonuc.Value.To);
        Assert.Equal(Simdi.AddMilliseconds(123).AddHours(-24), sonuc.Value.From);
    }

    [Fact]
    public async Task Depo_okunamazsa_StoreUnavailable_doner()
    {
        var okuyucu = new FakeLogEntryReader([]) { Unavailable = true };

        var sonuc = await Handler(okuyucu).HandleAsync(CancellationToken.None);

        Assert.False(sonuc.IsSuccess);
        Assert.Equal(LogQueryErrors.StoreUnavailable, Assert.Single(sonuc.Errors));
    }

    private static LogEntry Mesajli(DateTimeOffset alinma, LogLevel level, string mesaj) =>
        LogEntry.Create(Guid.CreateVersion7(alinma), "odeme-servisi", level, mesaj, null, null, null, null, alinma).Value;

    // Spec 0011, CB-1/CB-2: seçilen pencere bütün bölümlere uygulanır; 2 gün önceki kayıt 24 saatte sayılmaz, 48 saatte sayılır.
    [Fact]
    public async Task Secilen_pencere_bolumlere_uygulanir()
    {
        var okuyucu = new FakeLogEntryReader(
        [
            Kayit(Simdi.AddHours(-1), LogLevel.Error, "a", "TimeoutException"),
            Kayit(Simdi.AddHours(-40), LogLevel.Critical, "b", "IOException"),
        ]);

        var gun = await Handler(okuyucu).HandleAsync(new DashboardQuery("24h"), CancellationToken.None);
        var ikiGun = await Handler(okuyucu).HandleAsync(new DashboardQuery("48h"), CancellationToken.None);

        Assert.Equal("24h", gun.Value.Window);
        Assert.Equal(["a"], gun.Value.TopErrorServices.Select(s => s.ServiceName));
        Assert.Equal(["TimeoutException"], gun.Value.TopExceptionTypes.Select(t => t.ExceptionType));
        Assert.Equal(0, gun.Value.Levels.Single(l => l.Level == LogLevel.Critical).Count);

        Assert.Equal("48h", ikiGun.Value.Window);
        Assert.Equal(Simdi.AddHours(-48), ikiGun.Value.From);
        Assert.Equal(["a", "b"], ikiGun.Value.TopErrorServices.Select(s => s.ServiceName));
        Assert.Equal(1, ikiGun.Value.Levels.Single(l => l.Level == LogLevel.Critical).Count);
        Assert.Equal(24, ikiGun.Value.Slices.Count);
        Assert.Equal((TimeSpan.FromHours(2), 24), okuyucu.LastSlicing);
    }

    [Fact]
    public async Task Tanimsiz_pencere_reddedilir_ve_depo_okunmaz()
    {
        var okuyucu = new FakeLogEntryReader([]);

        var sonuc = await Handler(okuyucu).HandleAsync(new DashboardQuery("1y"), CancellationToken.None);

        Assert.Equal("window", Assert.Single(sonuc.Errors).Field);
        Assert.Null(okuyucu.LastWindow);
    }

    // CB-3: her derecenin bütün dilimlerdeki toplamı derece sayısına eşittir (365 günde 73 dilim).
    [Fact]
    public async Task Derece_sayilari_dilimlerin_toplamina_esittir()
    {
        var okuyucu = new FakeLogEntryReader(
        [
            Kayit(Simdi.AddDays(-365), LogLevel.Info),
            Kayit(Simdi.AddDays(-200), LogLevel.Error),
            Kayit(Simdi.AddDays(-5), LogLevel.Error),
            Kayit(Simdi.AddDays(-5), LogLevel.Warning),
            Kayit(Simdi, LogLevel.Critical),
        ]);

        var sonuc = (await Handler(okuyucu).HandleAsync(new DashboardQuery("365d"), CancellationToken.None)).Value;

        Assert.Equal(73, sonuc.Slices.Count);
        Assert.Equal([1, 2, 1, 1], sonuc.Levels.Select(l => l.Count));
        foreach (var derece in sonuc.Levels)
        {
            Assert.Equal(derece.Count, sonuc.Slices.Sum(s => s.Levels.Single(l => l.Level == derece.Level).Count));
        }
    }

    // CB-7: serbest aralık; dilim boyu uzunluğa göre, son dilim aralığın sonunda biter ve kısa olabilir.
    [Fact]
    public async Task Serbest_aralik_dilimleri_ve_sinirlari()
    {
        var from = Simdi.AddDays(-20);
        var to = from.AddDays(10).AddHours(5);
        var okuyucu = new FakeLogEntryReader(
        [
            Kayit(from, LogLevel.Info),
            Kayit(to, LogLevel.Error),
            Kayit(to.AddTicks(TimeSpan.TicksPerMicrosecond), LogLevel.Critical),
        ]);

        var sonuc = (await Handler(okuyucu).HandleAsync(new DashboardQuery(From: from, To: to), CancellationToken.None)).Value;

        Assert.Equal("custom", sonuc.Window);
        Assert.Equal((from, to), okuyucu.LastWindow);
        Assert.Equal(21, sonuc.Slices.Count);
        Assert.Equal(from.AddDays(10), sonuc.Slices[^1].From);
        Assert.Equal(to, sonuc.Slices[^1].To);
        Assert.Equal(sonuc.Slices[^1].From.AddTicks(-TimeSpan.TicksPerMicrosecond), sonuc.Slices[^2].To);
        Assert.Equal([0, 1, 0, 1], sonuc.Levels.Select(l => l.Count));
        Assert.Equal(1, sonuc.Slices[^1].Levels.Single(l => l.Level == LogLevel.Error).Count);
    }

    [Fact]
    public async Task Serbest_araligin_gelecekteki_bitisi_yuklenme_anina_kirpilir()
    {
        var okuyucu = new FakeLogEntryReader([]);

        var sonuc = (await Handler(okuyucu).HandleAsync(new DashboardQuery(From: Simdi.AddDays(-1), To: Simdi.AddDays(1)), CancellationToken.None)).Value;

        Assert.Equal(Simdi, sonuc.To);
    }

    [Theory]
    [InlineData(true, false, null, "from")]
    [InlineData(false, true, null, "from")]
    [InlineData(true, true, "24h", "window")]
    public async Task Eksik_ya_da_pencereyle_birlikte_verilen_aralik_reddedilir(bool baslangic, bool bitis, string? pencere, string alan)
    {
        var okuyucu = new FakeLogEntryReader([]);
        var sorgu = new DashboardQuery(pencere, baslangic ? Simdi.AddDays(-2) : null, bitis ? Simdi.AddDays(-1) : null);

        var sonuc = await Handler(okuyucu).HandleAsync(sorgu, CancellationToken.None);

        Assert.Equal(alan, Assert.Single(sonuc.Errors).Field);
        Assert.Null(okuyucu.LastWindow);
    }

    // CB-8: hatalar (Error, Critical) ve uyarılar (Warning) ayrı; penceredeki en yeni en fazla 10, en yeni önce.
    [Fact]
    public async Task Son_hatalar_ve_son_uyarilar_ayri_en_yeni_once_en_fazla_10()
    {
        var kayitlar = Enumerable.Range(1, 12).Select(i => Mesajli(Simdi.AddMinutes(-i), i % 2 == 0 ? LogLevel.Error : LogLevel.Critical, $"hata {i}"))
            .Append(Mesajli(Simdi.AddMinutes(-3).AddSeconds(30), LogLevel.Warning, "uyarı 1"))
            .Append(Mesajli(Simdi.AddMinutes(-5).AddSeconds(30), LogLevel.Warning, "uyarı 2"))
            .Append(Mesajli(Simdi.AddMinutes(-1).AddSeconds(30), LogLevel.Info, "bilgi"))
            .Append(Mesajli(Simdi.AddHours(-25), LogLevel.Warning, "pencere dışı"));
        var okuyucu = new FakeLogEntryReader(kayitlar);

        var sonuc = (await Handler(okuyucu).HandleAsync(new DashboardQuery("24h"), CancellationToken.None)).Value;

        Assert.Equal(Enumerable.Range(1, 10).Select(i => $"hata {i}"), sonuc.RecentErrors.Select(r => r.MessagePreview));
        Assert.All(sonuc.RecentErrors, r => Assert.Contains(r.Level, new[] { LogLevel.Error, LogLevel.Critical }));
        Assert.Equal(["uyarı 1", "uyarı 2"], sonuc.RecentWarnings.Select(r => r.MessagePreview));
    }

    // CB-8, S-4: mesajın ilk satırının en fazla 120 karakteri; uzunsa kesildiği işaretlenir.
    [Fact]
    public async Task Son_kayit_onizlemesi_ilk_satirin_120_karakteridir()
    {
        var uzun = new string('a', 130);
        var okuyucu = new FakeLogEntryReader(
        [
            Mesajli(Simdi.AddMinutes(-1), LogLevel.Error, uzun),
            Mesajli(Simdi.AddMinutes(-2), LogLevel.Warning, "ilk satır\nikinci satır"),
        ]);

        var sonuc = (await Handler(okuyucu).HandleAsync(CancellationToken.None)).Value;

        var hata = Assert.Single(sonuc.RecentErrors);
        Assert.Equal(new string('a', 120), hata.MessagePreview);
        Assert.True(hata.MessageTruncated);
        var uyari = Assert.Single(sonuc.RecentWarnings);
        Assert.Equal("ilk satır", uyari.MessagePreview);
        Assert.True(uyari.MessageTruncated);
    }
}
