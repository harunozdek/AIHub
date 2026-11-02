namespace AIHub.Domain.Monitoring;

/// <summary>İzlenen uygulamanın durumu (Spec 0005, R-5).</summary>
public enum HealthStatus
{
    Unknown,
    Up,
    Down,
}
