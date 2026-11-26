using AIHub.Application.Dashboard;
using AIHub.Application.Logs;

namespace AIHub.Application.Tests.Dashboard;

// Spec 0011: CB-1 (sekiz hazır pencere, sabit uzunluklar, tanımsız değer), CB-3 (dilim tablosu), CB-7 (serbest aralık:
// dilim boyu seçimi, kısa son dilim, doğrulama, gelecekteki bitişin kırpılması).
public class DashboardWindowTestleri
{
    private static readonly DateTimeOffset Simdi = new(2026, 11, 25, 12, 0, 0, TimeSpan.Zero);

    public static TheoryData<string, double, double, int> Tablo => new()
    {
        // pencere, uzunluk (saat), dilim (saat), dilim sayısı — spec'in iki tablosu
        { "24h", 24, 1, 24 },
        { "48h", 48, 2, 24 },
        { "7d", 7 * 24, 6, 28 },
        { "14d", 14 * 24, 12, 28 },
        { "30d", 30 * 24, 24, 30 },
        { "90d", 90 * 24, 3 * 24, 30 },
        { "180d", 180 * 24, 6 * 24, 30 },
        { "365d", 365 * 24, 5 * 24, 73 },
    };

    [Theory]
    [MemberData(nameof(Tablo))]
    public void Hazir_pencere_yuklenme_anindan_geriye_sabit_uzunluktur_ve_dilim_tablosuna_uyar(string anahtar, double saat, double dilimSaat, int dilimSayisi)
    {
        var pencere = DashboardWindow.ForPreset(anahtar, Simdi);

        Assert.True(pencere.IsSuccess);
        Assert.Equal(anahtar, pencere.Value.Key);
        Assert.Equal(Simdi, pencere.Value.To);
        Assert.Equal(Simdi - TimeSpan.FromHours(saat), pencere.Value.From);
        Assert.Equal(TimeSpan.FromHours(dilimSaat), pencere.Value.SliceLength);
        Assert.Equal(dilimSayisi, pencere.Value.SliceCount);
        Assert.Equal(pencere.Value.To - pencere.Value.From, pencere.Value.SliceLength * pencere.Value.SliceCount);
    }

    [Fact]
    public void Tam_olarak_sekiz_hazir_pencere_vardir()
    {
        Assert.Equal(["24h", "48h", "7d", "14d", "30d", "90d", "180d", "365d"], DashboardWindow.Presets.Select(p => p.Key));
    }

    [Fact]
    public void Pencere_verilmezse_son_24_saattir()
    {
        var pencere = DashboardWindow.ForPreset(null, Simdi);

        Assert.Equal("24h", pencere.Value.Key);
        Assert.Equal(Simdi.AddHours(-24), pencere.Value.From);
    }

    [Theory]
    [InlineData("1y")]
    [InlineData("24s")]
    [InlineData("")]
    [InlineData("custom")]
    public void Tanimsiz_pencere_reddedilir(string anahtar)
    {
        var pencere = DashboardWindow.ForPreset(anahtar, Simdi);

        Assert.False(pencere.IsSuccess);
        Assert.Equal("window.invalid", Assert.Single(pencere.Errors).Code);
    }

    // CB-7: uzunluğu aşmayan en kısa hazır pencerenin dilimi (ör. 10 gün → 14 günlük satırın 12 saatlik dilimi).
    [Theory]
    [InlineData(1 * 24, 1)]
    [InlineData(2 * 24, 2)]
    [InlineData(3 * 24, 6)]
    [InlineData(10 * 24, 12)]
    [InlineData(14 * 24, 12)]
    [InlineData(15 * 24, 24)]
    [InlineData(31 * 24, 3 * 24)]
    [InlineData(181 * 24, 5 * 24)]
    [InlineData(365 * 24, 5 * 24)]
    public void Serbest_araligin_dilimi_uzunlugu_asmayan_en_kisa_hazir_pencereninkidir(double saat, double dilimSaat)
    {
        Assert.Equal(TimeSpan.FromHours(dilimSaat), DashboardWindow.SliceLengthFor(TimeSpan.FromHours(saat)));
    }

    [Fact]
    public void Serbest_aralikta_son_dilim_araligin_sonunda_kesilir()
    {
        // 10 gün + 5 saat: 12 saatlik dilimler; 20 tam dilim ve 5 saatlik bir son dilim.
        var from = Simdi.AddDays(-20);
        var to = from.AddDays(10).AddHours(5);

        var pencere = DashboardWindow.ForRange(from, to, Simdi).Value;

        Assert.Equal(DashboardWindow.CustomKey, pencere.Key);
        Assert.Equal(from, pencere.From);
        Assert.Equal(to, pencere.To);
        Assert.Equal(TimeSpan.FromHours(12), pencere.SliceLength);
        Assert.Equal(21, pencere.SliceCount);
    }

    [Fact]
    public void Serbest_aralikta_gelecekteki_bitis_yuklenme_anina_kirpilir()
    {
        var from = Simdi.AddDays(-3);

        var pencere = DashboardWindow.ForRange(from, Simdi.AddDays(2), Simdi).Value;

        Assert.Equal(Simdi, pencere.To);
        Assert.Equal(TimeSpan.FromHours(6), pencere.SliceLength);
        Assert.Equal(12, pencere.SliceCount);
    }

    [Fact]
    public void Serbest_aralik_tek_gun_olabilir()
    {
        var from = Simdi.AddDays(-2);

        var pencere = DashboardWindow.ForRange(from, from.AddDays(1), Simdi).Value;

        Assert.Equal(TimeSpan.FromHours(1), pencere.SliceLength);
        Assert.Equal(24, pencere.SliceCount);
    }

    [Fact]
    public void Bitisi_baslangictan_once_olan_aralik_reddedilir()
    {
        var pencere = DashboardWindow.ForRange(Simdi.AddDays(-1), Simdi.AddDays(-2), Simdi);

        Assert.Equal("to.beforeFrom", Assert.Single(pencere.Errors).Code);
    }

    [Fact]
    public void Baslangici_gelecekte_olan_aralik_reddedilir()
    {
        var pencere = DashboardWindow.ForRange(Simdi.AddDays(1), Simdi.AddDays(2), Simdi);

        Assert.Equal("from.inFuture", Assert.Single(pencere.Errors).Code);
    }

    // Risk 4: 365 gün + 1 saat kabul (yaz saatli dilimde 365 yerel gün), 1 µs fazlası red; sınır log listesiyle aynı.
    [Fact]
    public void En_uzun_aralik_log_listesinin_sinirina_esittir()
    {
        var from = Simdi.AddDays(-400);
        var enUzun = from + TimeSpan.FromDays(365) + TimeSpan.FromHours(1);

        Assert.Equal(LogListHandler.MaxRange, DashboardWindow.MaxCustomLength);
        Assert.True(DashboardWindow.ForRange(from, enUzun, Simdi).IsSuccess);
        var fazla = DashboardWindow.ForRange(from, enUzun.AddTicks(TimeSpan.TicksPerMicrosecond), Simdi);
        Assert.Equal("from.rangeTooLong", Assert.Single(fazla.Errors).Code);
    }

    [Fact]
    public void Serbest_aralik_sinirlari_UTC_ile_hesaplanir()
    {
        var from = new DateTimeOffset(2026, 11, 1, 0, 0, 0, TimeSpan.FromHours(3));
        var to = new DateTimeOffset(2026, 11, 2, 0, 0, 0, TimeSpan.FromHours(3));

        var pencere = DashboardWindow.ForRange(from, to, Simdi).Value;

        Assert.Equal(TimeSpan.Zero, pencere.From.Offset);
        Assert.Equal(new DateTimeOffset(2026, 10, 31, 21, 0, 0, TimeSpan.Zero), pencere.From);
        Assert.Equal(24, pencere.SliceCount);
    }
}
