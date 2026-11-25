using AIHub.Application.Logs;
using AIHub.Domain.Logs;

namespace AIHub.Application.Dashboard;

/// <summary>
/// Dashboard'un log bölümleri (Spec 0006, R-2 … R-5; Spec 0011). Pencere alınma zamanına uygulanır ve log listesiyle
/// aynı sınırları kullanır: [From, To]. <see cref="Window"/> hazır pencerenin anahtarı ya da
/// <see cref="DashboardWindow.CustomKey"/>'dir. Derece sayıları dört dereceyi de içerir; kaydı olmayan 0'dır ve
/// dilimlerin toplamına eşittir (Spec 0011, CB-3). Slices, pencerenin trend dilimleridir (Spec 0007 CB-1, Spec 0011
/// CB-3). RecentErrors (Error, Critical) ve RecentWarnings (Warning) penceredeki en yeni en fazla 10 kayıttır, en yeni
/// önce (Spec 0011, CB-8).
/// </summary>
public sealed record DashboardSummary(
    string Window,
    DateTimeOffset From,
    DateTimeOffset To,
    IReadOnlyList<LevelCount> Levels,
    IReadOnlyList<ServiceErrorCount> TopErrorServices,
    IReadOnlyList<ExceptionTypeCount> TopExceptionTypes,
    IReadOnlyList<TrendSlice> Slices,
    IReadOnlyList<LogSummary> RecentErrors,
    IReadOnlyList<LogSummary> RecentWarnings);

public sealed record LevelCount(LogLevel Level, long Count);

/// <summary>Bir servisin penceredeki Error ve Critical kayıtlarının toplamı.</summary>
public sealed record ServiceErrorCount(string ServiceName, long Count);

public sealed record ExceptionTypeCount(string ExceptionType, long Count, DateTimeOffset LastSeenAt);

/// <summary>
/// Trendin bir dilimi (Spec 0007, CB-1; Spec 0011, CB-3): [From, To] alınma aralığında dört derecenin sayısı. To, bir
/// sonraki dilimin başlangıcından 1 µs öncedir; son dilim pencerenin sonuyla biter. Böylece dilim, log listesinin
/// [from, to] süzmesiyle birebir aynı kayıtları kapsar (Spec 0007, CB-2).
/// </summary>
public sealed record TrendSlice(DateTimeOffset From, DateTimeOffset To, IReadOnlyList<LevelCount> Levels);

/// <summary>Penceredeki bir dilimin (0'dan başlayan sırası) bir derecedeki kayıt sayısı.</summary>
public sealed record SliceLevelCount(int Slice, LogLevel Level, long Count);
