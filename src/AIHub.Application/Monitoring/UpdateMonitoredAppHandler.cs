using AIHub.Application.Abstractions;
using AIHub.Domain.Common;
using AIHub.Domain.Monitoring;

namespace AIHub.Application.Monitoring;

public sealed record UpdateMonitoredAppCommand(Guid Id, string? HealthUrl, Guid ActorId);

/// <summary>Sağlık adresini değiştirir; durum Bilinmiyor'a döner ve hemen denetime alınır (Spec 0005, R-2, AC-9).</summary>
public sealed class UpdateMonitoredAppHandler(IMonitoredAppRepository apps, IHostAddressResolver resolver, IClock clock)
{
    public async Task<Result<bool>> HandleAsync(UpdateMonitoredAppCommand command, CancellationToken cancellationToken)
    {
        var url = HealthUrl.Create(command.HealthUrl);
        if (!url.IsSuccess)
        {
            return Result<bool>.Failure(url.Errors);
        }

        if (await HealthAddressCheck.IsBlockedAsync(resolver, url.Value, cancellationToken))
        {
            return Result<bool>.Failure(MonitoringErrors.AddressBlocked);
        }

        var app = await apps.FindByIdAsync(command.Id, cancellationToken);
        if (app is null)
        {
            return Result<bool>.Failure(MonitoringErrors.NotFound);
        }

        app.ChangeUrl(url.Value, command.ActorId, clock.UtcNow);
        await apps.SaveChangesAsync(cancellationToken);
        return Result<bool>.Success(true);
    }
}
