using AIHub.Domain.Monitoring;

namespace AIHub.Application.Abstractions;

/// <summary>Tek bir denetimin sonucu. Başarısızlıkta neden, HTTP durumunda ayrıca kod taşır; gövde taşımaz.</summary>
public sealed record ProbeResult(bool IsSuccess, HealthFailureReason? Reason = null, int? StatusCode = null)
{
    public static readonly ProbeResult Success = new(true);

    public static ProbeResult Failure(HealthFailureReason reason, int? statusCode = null) => new(false, reason, statusCode);
}

/// <summary>Sağlık adresini bir kez dener (Spec 0005, R-4). Engelli adrese bağlanmaz (ADR 0003).</summary>
public interface IHealthProbe
{
    Task<ProbeResult> ProbeAsync(Uri url, CancellationToken cancellationToken);
}
