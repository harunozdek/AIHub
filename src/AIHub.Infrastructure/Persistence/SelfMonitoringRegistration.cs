namespace AIHub.Infrastructure.Persistence;

/// <summary>
/// AIHub'ın kendi kaydının "bir kez eklendi" işareti (Spec 0008, R-5). İzlenen uygulama silinse de işaret kalır;
/// sonraki açılışlarda kayıt yeniden eklenmez.
/// </summary>
public sealed class SelfMonitoringRegistration(string serviceName, DateTimeOffset registeredAt)
{
    public string ServiceName { get; private set; } = serviceName;

    public DateTimeOffset RegisteredAt { get; private set; } = registeredAt;
}
