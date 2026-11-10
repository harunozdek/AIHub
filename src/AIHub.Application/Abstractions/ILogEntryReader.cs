using AIHub.Application.Dashboard;
using AIHub.Application.Logs;
using AIHub.Domain.Common;
using AIHub.Domain.Logs;

namespace AIHub.Application.Abstractions;

/// <summary>
/// Panelin okuma deposu. Yazma deposundan (<see cref="ILogEntryRepository"/>) ayrıdır ve hiçbir yazma
/// metodu yoktur: panel bir kaydı değiştiremez veya silemez (Spec 0002, R-7). Depo okunamazsa
/// <see cref="LogQueryErrors.StoreUnavailable"/> döner.
/// </summary>
public interface ILogEntryReader
{
    /// <summary>
    /// Ölçüte uyan en fazla <see cref="LogListCriteria.Limit"/> kaydı, en yeni önce döner. İmleç
    /// <see cref="LogPageDirection.Newer"/> ise imlece en yakın daha yeni kayıtlar seçilir.
    /// </summary>
    Task<Result<IReadOnlyList<LogEntry>>> ListAsync(LogListCriteria criteria, CancellationToken cancellationToken);

    Task<Result<LogEntry?>> FindAsync(Guid id, CancellationToken cancellationToken);

    /// <summary>Depoda en az bir kaydı olan servis adları, alfabetik sırayla.</summary>
    Task<Result<IReadOnlyList<string>>> ListServiceNamesAsync(CancellationToken cancellationToken);

    /// <summary>[from, to] alınma penceresinde kaydı olan her derecenin kayıt sayısı (Spec 0006, R-3).</summary>
    Task<Result<IReadOnlyList<LevelCount>>> ListLevelCountsAsync(DateTimeOffset from, DateTimeOffset to, CancellationToken cancellationToken);

    /// <summary>
    /// Penceredeki Error ve Critical kayıtlarına göre en çok hata üreten <paramref name="limit"/> servis: sayıya
    /// göre çoktan aza, eşitlikte ServiceName alfabetik (Spec 0006, R-4).
    /// </summary>
    Task<Result<IReadOnlyList<ServiceErrorCount>>> ListTopErrorServicesAsync(DateTimeOffset from, DateTimeOffset to, int limit, CancellationToken cancellationToken);

    /// <summary>
    /// Penceredeki ExceptionType'ı olan kayıtların en sık <paramref name="limit"/> türü: sayıya göre çoktan aza,
    /// eşitlikte son görülen önce (Spec 0006, R-5).
    /// </summary>
    Task<Result<IReadOnlyList<ExceptionTypeCount>>> ListTopExceptionTypesAsync(DateTimeOffset from, DateTimeOffset to, int limit, CancellationToken cancellationToken);
}

public sealed record LogListCriteria(
    string? ServiceName,
    IReadOnlyList<LogLevel> Levels,
    DateTimeOffset From,
    DateTimeOffset To,
    LogCursor? Cursor,
    int Limit,
    string? ExceptionType = null);
