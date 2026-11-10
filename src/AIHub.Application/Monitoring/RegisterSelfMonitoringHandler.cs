using AIHub.Application.Abstractions;
using AIHub.Domain.Monitoring;

namespace AIHub.Application.Monitoring;

/// <summary>Ortam yapılandırmasındaki iki sağlık adresi (Spec 0008, R-4). Boş adres "eklenmez" demektir.</summary>
public sealed record RegisterSelfMonitoringCommand(string? ApiHealthUrl, string? PanelHealthUrl);

public enum SelfRegistrationResult
{
    Added,
    AlreadyRegistered,
    ExistingAppKept,
    NotConfigured,
    InvalidAddress,
    LimitReached,
}

public sealed record SelfRegistration(string ServiceName, SelfRegistrationResult Result, string? Reason = null);

/// <summary>
/// AIHub'ın kendi iki kaydını açılışta bir kez ekler (Spec 0008, R-4 … R-6). Adres 0005'in adres kuralından
/// geçer; kurala uymayan adres eklenmez ve nedeni döner. Ekleyen <see cref="SystemActor"/>'dır. Silinen kayıt
/// yeniden eklenmez (R-5): bu karar depodaki "bir kez eklendi" işaretiyle verilir.
/// </summary>
public sealed class RegisterSelfMonitoringHandler(IMonitoredAppRepository apps, IHostAddressResolver resolver, IClock clock)
{
    public async Task<IReadOnlyList<SelfRegistration>> HandleAsync(RegisterSelfMonitoringCommand command, CancellationToken cancellationToken) =>
    [
        await RegisterAsync(SystemActor.ApiServiceName, command.ApiHealthUrl, cancellationToken),
        await RegisterAsync(SystemActor.PanelServiceName, command.PanelHealthUrl, cancellationToken),
    ];

    private async Task<SelfRegistration> RegisterAsync(string serviceName, string? healthUrl, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(healthUrl))
        {
            return new SelfRegistration(serviceName, SelfRegistrationResult.NotConfigured);
        }

        var url = HealthUrl.Create(healthUrl);
        if (!url.IsSuccess)
        {
            return new SelfRegistration(serviceName, SelfRegistrationResult.InvalidAddress, string.Join(" ", url.Errors.Select(e => e.Message)));
        }

        if (await HealthAddressCheck.IsBlockedAsync(resolver, url.Value, cancellationToken))
        {
            return new SelfRegistration(serviceName, SelfRegistrationResult.InvalidAddress, MonitoringErrors.AddressBlocked.Message);
        }

        var now = clock.UtcNow;
        var app = MonitoredApp.Create(Guid.CreateVersion7(now), serviceName, url.Value, now, SystemActor.Id).Value;
        return (await apps.RegisterOnceAsync(app, MonitoringLimits.MaxMonitoredApps, cancellationToken)) switch
        {
            RegisterOnceOutcome.Added => new SelfRegistration(serviceName, SelfRegistrationResult.Added),
            RegisterOnceOutcome.AlreadyRegistered => new SelfRegistration(serviceName, SelfRegistrationResult.AlreadyRegistered),
            RegisterOnceOutcome.ServiceNameTaken => new SelfRegistration(serviceName, SelfRegistrationResult.ExistingAppKept),
            _ => new SelfRegistration(serviceName, SelfRegistrationResult.LimitReached, MonitoringErrors.LimitReached.Message),
        };
    }
}
