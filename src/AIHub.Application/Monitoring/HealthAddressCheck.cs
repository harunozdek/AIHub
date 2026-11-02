using System.Net;
using AIHub.Application.Abstractions;
using AIHub.Domain.Monitoring;

namespace AIHub.Application.Monitoring;

/// <summary>
/// Kayıt anındaki engel denetimi (Spec 0005, R-3). Ad çözülemiyorsa kayıt kabul edilir; engel denetim
/// anında yine uygulanır (plan 0005, risk 4).
/// </summary>
internal static class HealthAddressCheck
{
    public static async Task<bool> IsBlockedAsync(IHostAddressResolver resolver, HealthUrl url, CancellationToken cancellationToken)
    {
        var host = url.Uri.IdnHost;
        if (IPAddress.TryParse(host, out var literal))
        {
            return HealthAddressPolicy.IsBlocked(literal);
        }

        var addresses = await resolver.ResolveAsync(host, cancellationToken);
        return addresses.Any(HealthAddressPolicy.IsBlocked);
    }
}
