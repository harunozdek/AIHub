using AIHub.Domain.Common;
using AIHub.Domain.Monitoring;

namespace AIHub.Application.Monitoring;

public static class MonitoringErrors
{
    public static readonly Error ServiceNameTaken = new("serviceName.taken", "Bu ServiceName zaten izleniyor.", "serviceName");

    public static readonly Error LimitReached = new(
        "monitoredApps.limit",
        $"En fazla {MonitoringLimits.MaxMonitoredApps} uygulama izlenebilir. Yeni uygulama eklemek için önce birini izlemeden çıkarın.");

    public static readonly Error AddressBlocked = new(
        "healthUrl.blocked",
        "Sağlık adresi engellenen bir adrese çözülüyor (169.254.x.x ya da link-local).",
        "healthUrl");

    public static readonly Error NotFound = new("monitoredApp.notFound", "İzlenen uygulama bulunamadı.");
}
