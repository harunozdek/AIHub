using AIHub.Domain.Monitoring;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AIHub.Infrastructure.Persistence.Configurations;

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
