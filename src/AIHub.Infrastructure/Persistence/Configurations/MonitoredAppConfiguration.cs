using AIHub.Domain.Monitoring;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AIHub.Infrastructure.Persistence.Configurations;

internal sealed class MonitoredAppConfiguration : IEntityTypeConfiguration<MonitoredApp>
{
    public void Configure(EntityTypeBuilder<MonitoredApp> builder)
    {
        builder.ToTable("monitored_apps");
        builder.HasKey(a => a.Id);

        builder.Property(a => a.Id).HasColumnName("id").ValueGeneratedNever();
        builder.Property(a => a.ServiceName).HasColumnName("service_name").HasMaxLength(MonitoringLimits.ServiceNameMaxLength).IsRequired();
        builder.Property(a => a.HealthUrl).HasColumnName("health_url").HasMaxLength(MonitoringLimits.HealthUrlMaxLength).IsRequired();
        // Enum'lar metin olarak saklanır: veritabanında okunur kalır, sıra değişince anlam kaymaz.
        builder.Property(a => a.Status).HasColumnName("status").HasConversion<string>().HasMaxLength(16).IsRequired();
        builder.Property(a => a.ConsecutiveFailures).HasColumnName("consecutive_failures").IsRequired();
        builder.Property(a => a.LastCheckedAt).HasColumnName("last_checked_at");
        builder.Property(a => a.LastFailureReason).HasColumnName("last_failure_reason").HasConversion<string>().HasMaxLength(32);
        builder.Property(a => a.LastFailureStatusCode).HasColumnName("last_failure_status_code");
        builder.Property(a => a.DownSince).HasColumnName("down_since");
        builder.Property(a => a.NextCheckAt).HasColumnName("next_check_at").IsRequired();
        builder.Property(a => a.CreatedAt).HasColumnName("created_at").IsRequired();
        builder.Property(a => a.CreatedBy).HasColumnName("created_by").IsRequired();
        builder.Property(a => a.UpdatedAt).HasColumnName("updated_at");
        builder.Property(a => a.UpdatedBy).HasColumnName("updated_by");

        // Bir ServiceName en fazla bir kez izlenir; karşılaştırma log kayıtlarındaki gibi birebirdir (Spec 0005, R-1).
        builder.HasIndex(a => a.ServiceName).IsUnique().HasDatabaseName("ux_monitored_apps_service_name");
        builder.HasIndex(a => a.NextCheckAt).HasDatabaseName("ix_monitored_apps_next_check_at");
    }
}
