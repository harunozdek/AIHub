using System.Reflection;
using AIHub.Domain.Logs;

namespace AIHub.Domain.Tests;

public class LogEntryTestleri
{
    private static readonly DateTimeOffset Simdi = new(2026, 10, 27, 9, 0, 0, TimeSpan.Zero);

    [Theory]
    [InlineData("Info", LogLevel.Info)]
    [InlineData("warning", LogLevel.Warning)]
    [InlineData("ERROR", LogLevel.Error)]
    [InlineData("Critical", LogLevel.Critical)]
    public void LogLevel_yalnizca_dort_tanimli_degeri_kabul_eder(string metin, LogLevel beklenen)
    {
        Assert.True(LogLevels.TryParse(metin, out var seviye));
        Assert.Equal(beklenen, seviye);
    }

    [Theory]
    [InlineData("Debug")]
    [InlineData("Trace")]
    [InlineData("WARN")]
    [InlineData("1")]
    [InlineData("")]
    [InlineData(null)]
    public void LogLevel_tanimsiz_bos_veya_sayisal_metni_reddeder(string? metin)
    {
        Assert.False(LogLevels.TryParse(metin, out _));
    }

    [Fact]
    public void Gecerli_alanlarla_olusan_kayit_tum_alanlari_UTC_zamanla_tasir()
    {
        var kaynakZamani = new DateTimeOffset(2026, 10, 27, 11, 30, 0, TimeSpan.FromHours(3));

        var sonuc = LogEntry.Create(Guid.CreateVersion7(), "odeme-servisi", LogLevel.Error, "Tahsilat başarısız",
            "System.InvalidOperationException", "at X.Y()", "{\"siparis\":42}", kaynakZamani, Simdi);

        Assert.True(sonuc.IsSuccess);
        var kayit = sonuc.Value;
        Assert.Equal("odeme-servisi", kayit.ServiceName);
        Assert.Equal(LogLevel.Error, kayit.Level);
        Assert.Equal(TimeSpan.Zero, kayit.OccurredAt!.Value.Offset);
        Assert.Equal(kaynakZamani, kayit.OccurredAt);
        Assert.Equal(Simdi, kayit.ReceivedAt);
    }

    [Theory]
    [InlineData("", "mesaj", "serviceName")]
    [InlineData("odeme-servisi", "   ", "message")]
    public void Bos_ServiceName_veya_Message_reddedilir(string servis, string mesaj, string alan)
    {
        var sonuc = LogEntry.Create(Guid.CreateVersion7(), servis, LogLevel.Info, mesaj, null, null, null, null, Simdi);

        Assert.False(sonuc.IsSuccess);
        Assert.Contains(sonuc.Errors, e => e.Field == alan);
    }

    [Fact]
    public void ExceptionType_sinirda_kabul_sinir_asildiginda_reddedilir()
    {
        var sinirda = LogEntry.Create(Guid.CreateVersion7(), "s", LogLevel.Info,
            "m", new string('e', LogEntryLimits.ExceptionTypeMaxLength), null, null, null, Simdi);
        var asan = LogEntry.Create(Guid.CreateVersion7(), "s", LogLevel.Info,
            "m", new string('e', LogEntryLimits.ExceptionTypeMaxLength + 1), null, null, null, Simdi);

        Assert.True(sinirda.IsSuccess);
        Assert.False(asan.IsSuccess);
        Assert.Contains(asan.Errors, e => e.Field == "exceptionType");
    }

    [Theory]
    [InlineData("", null)]
    [InlineData("   ", null)]
    [InlineData("\t\n", null)]
    [InlineData(" System.TimeoutException ", "System.TimeoutException")]
    [InlineData("System.TimeoutException", "System.TimeoutException")]
    public void ExceptionType_bos_ise_yok_sayilir_degilse_kirpilarak_saklanir(string girilen, string? saklanan)
    {
        var sonuc = LogEntry.Create(Guid.CreateVersion7(), "s", LogLevel.Error, "m", girilen, null, null, null, Simdi);

        Assert.True(sonuc.IsSuccess);
        Assert.Equal(saklanan, sonuc.Value.ExceptionType);
    }

    // Message sınırı ham metne Api sınırında uygulanır (AIHub.Api.Tests › AC5_Message_ve_ExceptionType_siniri_asilinca_400_alir);
    // maskelenmiş, yer tutucularla uzamış metin Domain'de reddedilmez (review F-3).
    [Fact]
    public void Maskeleme_ile_sinirin_otesine_uzamis_Message_kabul_edilir()
    {
        var uzamis = new string('m', LogEntryLimits.MessageMaxLength) + "[MASKED:tckn]";

        var sonuc = LogEntry.Create(Guid.CreateVersion7(), "s", LogLevel.Info, uzamis, null, null, null, null, Simdi);

        Assert.True(sonuc.IsSuccess);
        Assert.Equal(uzamis, sonuc.Value.Message);
    }

    [Fact]
    public void Saklanan_kayit_degistirilemez_hicbir_ozelligin_public_setteri_yoktur()
    {
        var setterliOzellikler = typeof(LogEntry).GetProperties(BindingFlags.Instance | BindingFlags.Public)
            .Where(p => p.SetMethod is { IsPublic: true })
            .Select(p => p.Name)
            .ToArray();

        Assert.Empty(setterliOzellikler);
    }
}
