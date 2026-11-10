using AIHub.Domain.Logs;

namespace AIHub.Application.Dashboard;

/// <summary>
/// Dashboard'un log bölümleri (Spec 0006, R-2 … R-5). Pencere alınma zamanına uygulanır ve log listesiyle
/// aynı sınırları kullanır: [From, To]. Derece sayıları dört dereceyi de içerir; kaydı olmayan 0'dır.
/// </summary>
public sealed record DashboardSummary(
    DateTimeOffset From,
    DateTimeOffset To,
    IReadOnlyList<LevelCount> Levels,
    IReadOnlyList<ServiceErrorCount> TopErrorServices,
    IReadOnlyList<ExceptionTypeCount> TopExceptionTypes);

public sealed record LevelCount(LogLevel Level, long Count);

/// <summary>Bir servisin penceredeki Error ve Critical kayıtlarının toplamı.</summary>
public sealed record ServiceErrorCount(string ServiceName, long Count);

public sealed record ExceptionTypeCount(string ExceptionType, long Count, DateTimeOffset LastSeenAt);
