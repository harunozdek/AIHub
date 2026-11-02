using AIHub.Domain.Common;

namespace AIHub.Domain.Monitoring;

/// <summary>
/// İzlenen uygulama: bir ServiceName ve bir sağlık adresi (Spec 0005, R-1). Durum kuralı burada tutulur
/// (R-5): yeni ya da adresi değişen uygulama Bilinmiyor'dur; art arda <see cref="MonitoringLimits.DownThreshold"/>
/// başarısız denetim Down yapar; tek bir başarılı denetim Ayakta yapar ve sayacı sıfırlar.
/// </summary>
public sealed class MonitoredApp
{
    private MonitoredApp(Guid id, string serviceName, string healthUrl, DateTimeOffset createdAt, Guid createdBy)
    {
        Id = id;
        ServiceName = serviceName;
        HealthUrl = healthUrl;
        Status = HealthStatus.Unknown;
        CreatedAt = createdAt;
        CreatedBy = createdBy;
        NextCheckAt = createdAt;
    }

    public Guid Id { get; private set; }

    public string ServiceName { get; private set; }

    public string HealthUrl { get; private set; }

    public HealthStatus Status { get; private set; }

    public int ConsecutiveFailures { get; private set; }

    public DateTimeOffset? LastCheckedAt { get; private set; }

    public HealthFailureReason? LastFailureReason { get; private set; }

    /// <summary>Son başarısızlık bir HTTP durum koduysa o kod (ör. 503).</summary>
    public int? LastFailureStatusCode { get; private set; }

    /// <summary>Uygulama Down iken, Down olduğu an.</summary>
    public DateTimeOffset? DownSince { get; private set; }

    /// <summary>Denetimin bir sonraki zamanı; sahiplenme bu alanla yapılır (ADR 0003).</summary>
    public DateTimeOffset NextCheckAt { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public Guid CreatedBy { get; private set; }

    public DateTimeOffset? UpdatedAt { get; private set; }

    public Guid? UpdatedBy { get; private set; }

    public static Result<MonitoredApp> Create(Guid id, string? serviceName, HealthUrl healthUrl, DateTimeOffset now, Guid createdBy)
    {
        var name = serviceName?.Trim();
        if (string.IsNullOrEmpty(name))
        {
            return Result<MonitoredApp>.Failure(new Error("serviceName.required", "ServiceName boş olamaz.", "serviceName"));
        }

        if (name.Length > MonitoringLimits.ServiceNameMaxLength)
        {
            return Result<MonitoredApp>.Failure(new Error(
                "serviceName.length",
                $"ServiceName en fazla {MonitoringLimits.ServiceNameMaxLength} karakter olabilir.",
                "serviceName"));
        }

        return Result<MonitoredApp>.Success(new MonitoredApp(id, name, healthUrl.Value, now.ToUniversalTime(), createdBy));
    }

    /// <summary>Yeni adres: durum Bilinmiyor'a döner ve uygulama hemen denetime alınır (AC-9).</summary>
    public void ChangeUrl(HealthUrl healthUrl, Guid by, DateTimeOffset now)
    {
        now = now.ToUniversalTime();
        HealthUrl = healthUrl.Value;
        Status = HealthStatus.Unknown;
        ConsecutiveFailures = 0;
        LastCheckedAt = null;
        LastFailureReason = null;
        LastFailureStatusCode = null;
        DownSince = null;
        NextCheckAt = now;
        UpdatedAt = now;
        UpdatedBy = by;
    }

    public void RecordSuccess(DateTimeOffset now)
    {
        LastCheckedAt = now.ToUniversalTime();
        ConsecutiveFailures = 0;
        Status = HealthStatus.Up;
        DownSince = null;
    }

    public void RecordFailure(HealthFailureReason reason, int? statusCode, DateTimeOffset now)
    {
        now = now.ToUniversalTime();
        LastCheckedAt = now;
        LastFailureReason = reason;
        LastFailureStatusCode = reason == HealthFailureReason.HttpStatus ? statusCode : null;
        ConsecutiveFailures++;
        if (ConsecutiveFailures >= MonitoringLimits.DownThreshold && Status != HealthStatus.Down)
        {
            Status = HealthStatus.Down;
            DownSince = now;
        }
    }
}
