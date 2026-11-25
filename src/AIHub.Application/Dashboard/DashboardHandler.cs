using AIHub.Application.Abstractions;
using AIHub.Application.Logs;
using AIHub.Domain.Common;
using AIHub.Domain.Logs;

namespace AIHub.Application.Dashboard;

/// <summary>
/// Dashboard'un log özeti: son 24 saatte derece sayıları, en çok hata üreten servisler ve en sık exception
/// türleri (Spec 0006, R-2 … R-5). Sıralama ve sınırlar okuma deposunun sözleşmesidir. Depo okunamazsa
/// <see cref="LogQueryErrors.StoreUnavailable"/> döner (R-9).
/// </summary>
public sealed class DashboardHandler(ILogEntryReader reader, IClock clock)
{
    public static readonly TimeSpan Window = TimeSpan.FromHours(24);
    public const int TopServiceCount = 10;
    public const int TopExceptionTypeCount = 5;
    public const int SliceCount = 24;
    public static readonly TimeSpan SliceLength = Window / SliceCount;

    /// <summary>PostgreSQL alınma zamanını mikrosaniye hassasiyetle saklar; dilim sonu bir sonraki dilimden 1 µs öncedir.</summary>
    private static readonly TimeSpan OneMicrosecond = TimeSpan.FromTicks(TimeSpan.TicksPerMicrosecond);

    /// <summary>Ekrandaki sıra: en ağır derece önce.</summary>
    private static readonly LogLevel[] LevelOrder = [LogLevel.Critical, LogLevel.Error, LogLevel.Warning, LogLevel.Info];

    public async Task<Result<DashboardSummary>> HandleAsync(CancellationToken cancellationToken)
    {
        // Milisaniyeye kırpılır: dilim sınırları veritabanının mikrosaniye hassasiyetinde tam karşılık bulur ve
        // listeye taşınan from/to aynı anı gösterir (plan 0007, risk 1).
        var now = clock.UtcNow.ToUniversalTime();
        var to = now.AddTicks(-(now.Ticks % TimeSpan.TicksPerMillisecond));
        var from = to - Window;

        var levels = await reader.ListLevelCountsAsync(from, to, cancellationToken);
        if (!levels.IsSuccess)
        {
            return Result<DashboardSummary>.Failure(levels.Errors);
        }

        var services = await reader.ListTopErrorServicesAsync(from, to, TopServiceCount, cancellationToken);
        if (!services.IsSuccess)
        {
            return Result<DashboardSummary>.Failure(services.Errors);
        }

        var exceptionTypes = await reader.ListTopExceptionTypesAsync(from, to, TopExceptionTypeCount, cancellationToken);
        if (!exceptionTypes.IsSuccess)
        {
            return Result<DashboardSummary>.Failure(exceptionTypes.Errors);
        }

        var slices = await reader.ListSliceLevelCountsAsync(from, to, SliceLength, SliceCount, cancellationToken);
        if (!slices.IsSuccess)
        {
            return Result<DashboardSummary>.Failure(slices.Errors);
        }

        var counts = levels.Value.ToDictionary(c => c.Level, c => c.Count);
        var allLevels = LevelOrder.Select(l => new LevelCount(l, counts.GetValueOrDefault(l))).ToArray();

        return Result<DashboardSummary>.Success(
            new DashboardSummary(from, to, allLevels, services.Value, exceptionTypes.Value, Hourly(from, to, slices.Value)));
    }

    /// <summary>24 dilimin hepsi, her dilimde dört derece; kaydı olmayan 0 (Spec 0007, CB-1).</summary>
    private static TrendSlice[] Hourly(DateTimeOffset from, DateTimeOffset to, IReadOnlyList<SliceLevelCount> counts)
    {
        var bySlice = counts.ToLookup(c => c.Slice);
        return Enumerable.Range(0, SliceCount).Select(i =>
        {
            var sliceFrom = from + i * SliceLength;
            var sliceTo = i == SliceCount - 1 ? to : sliceFrom + SliceLength - OneMicrosecond;
            var inSlice = bySlice[i].ToDictionary(c => c.Level, c => c.Count);
            return new TrendSlice(sliceFrom, sliceTo, LevelOrder.Select(l => new LevelCount(l, inSlice.GetValueOrDefault(l))).ToArray());
        }).ToArray();
    }
}
