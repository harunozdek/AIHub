namespace AIHub.Application.Logs;

/// <summary>
/// Bir liste sayfası; satırlar en yeni önce. <see cref="Older"/> ve <see cref="Newer"/> o yönde kayıt
/// yoksa <c>null</c>'dır. <see cref="From"/> ve <see cref="To"/> uygulanan (varsayılanı çözülmüş) aralıktır.
/// </summary>
public sealed record LogPage(
    IReadOnlyList<LogSummary> Items,
    LogCursor? Older,
    LogCursor? Newer,
    DateTimeOffset From,
    DateTimeOffset To);
