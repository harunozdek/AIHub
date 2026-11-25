using AIHub.Domain.Logs;

namespace AIHub.Application.Dashboard;

/// <summary>
/// Dashboard'un log bölümleri (Spec 0006, R-2 … R-5). Pencere alınma zamanına uygulanır ve log listesiyle
/// aynı sınırları kullanır: [From, To]. Derece sayıları dört dereceyi de içerir; kaydı olmayan 0'dır.
/// Hourly, pencerenin 24 saatlik dilimleridir (Spec 0007, CB-1).
/// </summary>
public sealed record DashboardSummary(
    DateTimeOffset From,
    DateTimeOffset To,
    IReadOnlyList<LevelCount> Levels,
    IReadOnlyList<ServiceErrorCount> TopErrorServices,
    IReadOnlyList<ExceptionTypeCount> TopExceptionTypes,
    IReadOnlyList<TrendSlice> Hourly);

public sealed record LevelCount(LogLevel Level, long Count);

/// <summary>Bir servisin penceredeki Error ve Critical kayıtlarının toplamı.</summary>
public sealed record ServiceErrorCount(string ServiceName, long Count);

public sealed record ExceptionTypeCount(string ExceptionType, long Count, DateTimeOffset LastSeenAt);

/// <summary>
/// Saatlik trendin bir dilimi (Spec 0007, CB-1): [From, To] alınma aralığında dört derecenin sayısı. To, bir
/// sonraki dilimin başlangıcından 1 µs öncedir; son dilim pencerenin sonuyla biter. Böylece dilim, log
/// listesinin [from, to] süzmesiyle birebir aynı kayıtları kapsar (CB-2).
/// </summary>
public sealed record TrendSlice(DateTimeOffset From, DateTimeOffset To, IReadOnlyList<LevelCount> Levels);

/// <summary>Penceredeki bir dilimin (0'dan başlayan sırası) bir derecedeki kayıt sayısı.</summary>
public sealed record SliceLevelCount(int Slice, LogLevel Level, long Count);
