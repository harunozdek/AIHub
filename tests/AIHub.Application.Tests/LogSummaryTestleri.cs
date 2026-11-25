using AIHub.Application.Logs;
using AIHub.Domain.Logs;

namespace AIHub.Application.Tests;

// Önizleme kuralı (Spec 0002 R-2): ilk satır, en fazla N karakter, surrogate çifti bölünmez. Liste 200 karakter
// (PB-4), dashboard'un son kayıt satırları 120 karakter (Spec 0011, CB-8, S-4; plan 0011, risk 6).
public class LogSummaryTestleri
{
    private static readonly DateTimeOffset Simdi = new(2026, 11, 25, 12, 0, 0, TimeSpan.Zero);

    private static LogEntry Kayit(string mesaj) =>
        LogEntry.Create(Guid.CreateVersion7(Simdi), "svc", LogLevel.Error, mesaj, null, null, null, null, Simdi).Value;

    [Fact]
    public void Liste_onizlemesi_200_karakterdir()
    {
        var ozet = LogSummary.From(Kayit(new string('x', 201)));

        Assert.Equal(200, ozet.MessagePreview.Length);
        Assert.True(ozet.MessageTruncated);
    }

    [Theory]
    [InlineData(120, 120, false)]
    [InlineData(121, 120, true)]
    public void Kisa_onizleme_verilen_uzunlukta_kesilir(int uzunluk, int beklenen, bool kesildi)
    {
        var ozet = LogSummary.From(Kayit(new string('x', uzunluk)), 120);

        Assert.Equal(beklenen, ozet.MessagePreview.Length);
        Assert.Equal(kesildi, ozet.MessageTruncated);
    }

    [Fact]
    public void Kisa_onizleme_yalnizca_ilk_satiri_alir()
    {
        var ozet = LogSummary.From(Kayit("birinci\r\nikinci"), 120);

        Assert.Equal("birinci", ozet.MessagePreview);
        Assert.True(ozet.MessageTruncated);
    }

    [Fact]
    public void Kisa_onizleme_surrogate_ciftini_bolmez()
    {
        // 119 karakter + "😀" (iki UTF-16 birimi: 120. ve 121.) + ek: kesim 119'da kalır.
        var ozet = LogSummary.From(Kayit(new string('x', 119) + "😀" + "son"), 120);

        Assert.Equal(119, ozet.MessagePreview.Length);
        Assert.True(ozet.MessageTruncated);
    }
}
