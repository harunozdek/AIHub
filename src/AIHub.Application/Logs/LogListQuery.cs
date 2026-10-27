namespace AIHub.Application.Logs;

/// <summary>
/// Panel liste isteği. LogLevel değerleri ham metin olarak gelir; geçerlilik kararı Application'dadır
/// (docs/architecture.md, FD-4). Zaman aralığı alınma zamanına uygulanır (Spec 0002, Constraints).
/// </summary>
public sealed record LogListQuery(
    string? ServiceName,
    IReadOnlyList<string> Levels,
    DateTimeOffset? From,
    DateTimeOffset? To,
    LogCursor? Cursor);
