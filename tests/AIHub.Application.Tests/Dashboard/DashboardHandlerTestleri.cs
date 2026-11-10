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

    [Fact]
    public async Task Depo_okunamazsa_StoreUnavailable_doner()
    {
        var okuyucu = new FakeLogEntryReader([]) { Unavailable = true };

        var sonuc = await Handler(okuyucu).HandleAsync(CancellationToken.None);

        Assert.False(sonuc.IsSuccess);
        Assert.Equal(LogQueryErrors.StoreUnavailable, Assert.Single(sonuc.Errors));
    }
}
