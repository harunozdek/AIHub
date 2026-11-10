using AIHub.Domain.Monitoring;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

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

internal sealed class SelfMonitoringRegistrationConfiguration : IEntityTypeConfiguration<SelfMonitoringRegistration>
{
    public void Configure(EntityTypeBuilder<SelfMonitoringRegistration> builder)
    {
        builder.ToTable("self_monitoring_registrations");
        builder.HasKey(r => r.ServiceName);
        builder.Property(r => r.ServiceName).HasColumnName("service_name").HasMaxLength(MonitoringLimits.ServiceNameMaxLength);
        builder.Property(r => r.RegisteredAt).HasColumnName("registered_at").IsRequired();
    }
}
