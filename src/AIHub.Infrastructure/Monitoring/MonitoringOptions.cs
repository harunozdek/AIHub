using AIHub.Domain.Monitoring;

namespace AIHub.Infrastructure.Monitoring;

/// <summary>
/// Denetim ayarları. Varsayılanlar spec 0005'in sabitleridir ve ekrandan değiştirilmez. Aralık ve zaman
/// aşımı yalnızca testlerde kısaltılır (plan 0005, risk 1 ve 2).
/// </summary>
public sealed class MonitoringOptions
{
    public const string SectionName = "Monitoring";

    public bool Enabled { get; set; } = true;

    /// <summary>Worker'ın zamanı gelen uygulamalara bakma sıklığı; denetim aralığından kısa olmalıdır.</summary>
    public TimeSpan TickInterval { get; set; } = TimeSpan.FromSeconds(5);

    public TimeSpan CheckInterval { get; set; } = MonitoringLimits.CheckInterval;

    public TimeSpan Timeout { get; set; } = MonitoringLimits.Timeout;
}
