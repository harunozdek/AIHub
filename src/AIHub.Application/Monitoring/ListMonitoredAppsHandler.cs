using AIHub.Application.Abstractions;
using AIHub.Domain.Monitoring;

namespace AIHub.Application.Monitoring;

/// <summary>
/// İzlenen uygulamalar: Down olanlar en üstte, en uzun süredir Down olan önce; ardından ServiceName
/// sırası. Ekleyen ve değiştiren görünen adlara çevrilir (Spec 0005, R-8, AC-10).
/// </summary>
public sealed class ListMonitoredAppsHandler(IMonitoredAppRepository apps, IUserRepository users)
{
    public async Task<IReadOnlyList<MonitoredAppSummary>> HandleAsync(CancellationToken cancellationToken)
    {
        var all = await apps.ListAsync(cancellationToken);
        var names = (await users.ListAsync(cancellationToken)).ToDictionary(u => u.Id, u => u.DisplayName);
        // AIHub'ın kendi eklediği kayıtlar "Sistem" görünür (Spec 0008, R-6).
        string? NameOf(Guid? id) => id == SystemActor.Id ? SystemActor.DisplayName : id is { } value && names.TryGetValue(value, out var name) ? name : null;

        return all
            .OrderBy(a => a.Status == HealthStatus.Down ? 0 : 1)
            .ThenBy(a => a.DownSince ?? DateTimeOffset.MaxValue)
            .ThenBy(a => a.ServiceName, StringComparer.Ordinal)
            .Select(a => new MonitoredAppSummary(
                a.Id,
                a.ServiceName,
                a.HealthUrl,
                a.Status,
                a.LastCheckedAt,
                a.LastFailureReason,
                a.LastFailureStatusCode,
                a.DownSince,
                a.CreatedAt,
                NameOf(a.CreatedBy),
                a.UpdatedAt,
                NameOf(a.UpdatedBy)))
            .ToArray();
    }
}
