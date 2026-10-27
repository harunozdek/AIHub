using AIHub.Domain.Logs;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AIHub.Infrastructure.Persistence.Configurations;

internal sealed class LogEntryConfiguration : IEntityTypeConfiguration<LogEntry>
{
    public void Configure(EntityTypeBuilder<LogEntry> builder)
    {
        builder.ToTable("log_entries");
        builder.HasKey(e => e.Id);

        builder.Property(e => e.Id).HasColumnName("id").ValueGeneratedNever();
        builder.Property(e => e.ServiceName).HasColumnName("service_name").IsRequired();
        builder.Property(e => e.Level).HasColumnName("level").HasConversion<string>().HasMaxLength(16).IsRequired();
        // Sınır ham girdiye Api'de uygulanır; maskelenmiş metin uzayabileceği için sütun text (review F-3).
        builder.Property(e => e.Message).HasColumnName("message").IsRequired();
        builder.Property(e => e.ExceptionType).HasColumnName("exception_type").HasMaxLength(LogEntryLimits.ExceptionTypeMaxLength);
        builder.Property(e => e.StackTrace).HasColumnName("stack_trace");
        builder.Property(e => e.Metadata).HasColumnName("metadata").HasColumnType("jsonb");
        builder.Property(e => e.OccurredAt).HasColumnName("occurred_at");
        builder.Property(e => e.ReceivedAt).HasColumnName("received_at").IsRequired();

        builder.HasIndex(e => new { e.ServiceName, e.ReceivedAt }).HasDatabaseName("ix_log_entries_service_name_received_at");
        // Keyset sayfalama (received_at, id) sırasıyla okur; indeks aynı sırada (plan 0002).
        builder.HasIndex(e => new { e.ReceivedAt, e.Id }).HasDatabaseName("ix_log_entries_received_at_id");
    }
}
