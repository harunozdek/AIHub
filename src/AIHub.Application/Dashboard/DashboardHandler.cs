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

    /// <summary>Ekrandaki sıra: en ağır derece önce.</summary>
    private static readonly LogLevel[] LevelOrder = [LogLevel.Critical, LogLevel.Error, LogLevel.Warning, LogLevel.Info];

    public async Task<Result<DashboardSummary>> HandleAsync(CancellationToken cancellationToken)
    {
        var to = clock.UtcNow.ToUniversalTime();
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

        var counts = levels.Value.ToDictionary(c => c.Level, c => c.Count);
        var allLevels = LevelOrder.Select(l => new LevelCount(l, counts.GetValueOrDefault(l))).ToArray();

        return Result<DashboardSummary>.Success(new DashboardSummary(from, to, allLevels, services.Value, exceptionTypes.Value));
    }
}
