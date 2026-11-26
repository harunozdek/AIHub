namespace AIHub.Domain.Monitoring;

/// <summary>Spec 0005 (aralıklar Spec 0012): denetim aralığı, zaman aşımı, Down eşiği ve boyut sınırları. Ekrandan değiştirilmez.</summary>
public static class MonitoringLimits
{
    /// <summary>Her uygulama bu aralıkta bir kez denetlenir (0005 R-4; Spec 0012, CB-5: 30 sn yerine 10 sn).</summary>
    public static readonly TimeSpan CheckInterval = TimeSpan.FromSeconds(10);

    /// <summary>
    /// Worker'ın zamanı gelen uygulamalara bakma sıklığı. Bir denetim zamanından en fazla bu kadar geç başlar;
    /// "en geç 1 dakika" hesabına girer (0005 AC-7; Spec 0012, CB-5; plan 0012, risk 1: 10 sn aralıkta gecikme en fazla 2 sn).
    /// </summary>
    public static readonly TimeSpan TickInterval = TimeSpan.FromSeconds(2);

    /// <summary>Yanıt bu sürede gelmezse denetim zaman aşımına uğrar (R-4).</summary>
    public static readonly TimeSpan Timeout = TimeSpan.FromSeconds(5);

    /// <summary>Art arda bu kadar başarısız denetim uygulamayı Down yapar (R-5).</summary>
    public const int DownThreshold = 3;

    public const int MaxMonitoredApps = 200;

    public const int HealthUrlMaxLength = 2048;

    /// <summary>Yalnızca girdinin büyüklüğünü sınırlar; ServiceName'in biçim kuralı yoktur (plan 0005, risk 3).</summary>
    public const int ServiceNameMaxLength = 200;
}
