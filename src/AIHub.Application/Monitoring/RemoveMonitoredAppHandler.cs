using AIHub.Application.Abstractions;
using AIHub.Domain.Common;

namespace AIHub.Application.Monitoring;

/// <summary>İzlemeden çıkarır: uygulama artık denetlenmez ve uyarı üretmez (Spec 0005, R-2, AC-9).</summary>
public sealed class RemoveMonitoredAppHandler(IMonitoredAppRepository apps)
{
    public async Task<Result<bool>> HandleAsync(Guid id, CancellationToken cancellationToken)
    {
        var app = await apps.FindByIdAsync(id, cancellationToken);
        if (app is null)
        {
            return Result<bool>.Failure(MonitoringErrors.NotFound);
        }

        await apps.RemoveAsync(app, cancellationToken);
        return Result<bool>.Success(true);
    }
}
