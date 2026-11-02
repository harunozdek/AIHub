using AIHub.Application.Abstractions;
using AIHub.Domain.Common;
using AIHub.Domain.Monitoring;

namespace AIHub.Application.Monitoring;

public sealed record AddMonitoredAppCommand(string? ServiceName, string? HealthUrl, Guid ActorId);

/// <summary>
/// İzlenen uygulama ekler (Spec 0005, R-1–R-3, AC-2, AC-3, AC-11). Alan hataları birlikte döner. Yeni
/// uygulama Bilinmiyor durumundadır ve hemen denetime alınır.
/// </summary>
public sealed class AddMonitoredAppHandler(IMonitoredAppRepository apps, IHostAddressResolver resolver, IClock clock)
{
    public async Task<Result<Guid>> HandleAsync(AddMonitoredAppCommand command, CancellationToken cancellationToken)
    {
        var now = clock.UtcNow;
        var url = HealthUrl.Create(command.HealthUrl);
        var errors = new List<Error>();
        if (MonitoredApp.ValidateServiceName(command.ServiceName) is { } nameError)
        {
            errors.Add(nameError);
        }

        errors.AddRange(url.Errors);
        if (errors.Count > 0)
        {
            return Result<Guid>.Failure(errors);
        }

        if (await HealthAddressCheck.IsBlockedAsync(resolver, url.Value, cancellationToken))
        {
            return Result<Guid>.Failure(MonitoringErrors.AddressBlocked);
        }

        var app = MonitoredApp.Create(Guid.CreateVersion7(now), command.ServiceName, url.Value, now, command.ActorId).Value;
        return await apps.AddAsync(app, MonitoringLimits.MaxMonitoredApps, cancellationToken) switch
        {
            AddMonitoredAppOutcome.Added => Result<Guid>.Success(app.Id),
            AddMonitoredAppOutcome.ServiceNameTaken => Result<Guid>.Failure(MonitoringErrors.ServiceNameTaken),
            _ => Result<Guid>.Failure(MonitoringErrors.LimitReached),
        };
    }
}
