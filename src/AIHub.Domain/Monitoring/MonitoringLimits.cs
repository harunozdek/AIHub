namespace AIHub.Domain.Monitoring;

/// <summary>Spec 0005: denetim aralığı, zaman aşımı, Down eşiği ve boyut sınırları. Ekrandan değiştirilmez.</summary>
public static class MonitoringLimits
{
    /// <summary>Her uygulama bu aralıkta bir kez denetlenir (R-4).</summary>
    public static readonly TimeSpan CheckInterval = TimeSpan.FromSeconds(30);

    /// <summary>
    /// Worker'ın zamanı gelen uygulamalara bakma sıklığı. Bir denetim zamanından en fazla bu kadar geç başlar;
    /// "en geç iki dakika" hesabına girer (AC-7; review 1, F-01).
    /// </summary>
    public static readonly TimeSpan TickInterval = TimeSpan.FromSeconds(5);

    /// <summary>Yanıt bu sürede gelmezse denetim zaman aşımına uğrar (R-4).</summary>
    public static readonly TimeSpan Timeout = TimeSpan.FromSeconds(5);

    /// <summary>Art arda bu kadar başarısız denetim uygulamayı Down yapar (R-5).</summary>
    public const int DownThreshold = 3;

    public const int MaxMonitoredApps = 200;

    public const int HealthUrlMaxLength = 2048;

    /// <summary>Yalnızca girdinin büyüklüğünü sınırlar; ServiceName'in biçim kuralı yoktur (plan 0005, risk 3).</summary>
    public const int ServiceNameMaxLength = 200;
}
