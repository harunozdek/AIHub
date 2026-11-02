using AIHub.Domain.Monitoring;

namespace AIHub.Application.Monitoring;

/// <summary>"İzlenen uygulamalar" ekranının bir satırı (Spec 0005, R-8). Ekleyen ve değiştiren görünen addır.</summary>
public sealed record MonitoredAppSummary(
    Guid Id,
    string ServiceName,
    string HealthUrl,
    HealthStatus Status,
    DateTimeOffset? LastCheckedAt,
    HealthFailureReason? LastFailureReason,
    int? LastFailureStatusCode,
    DateTimeOffset? DownSince,
    DateTimeOffset CreatedAt,
    string? CreatedBy,
    DateTimeOffset? UpdatedAt,
    string? UpdatedBy);
