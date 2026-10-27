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
}

public sealed record LogListCriteria(
    string? ServiceName,
    IReadOnlyList<LogLevel> Levels,
    DateTimeOffset From,
    DateTimeOffset To,
    LogCursor? Cursor,
    int Limit);
