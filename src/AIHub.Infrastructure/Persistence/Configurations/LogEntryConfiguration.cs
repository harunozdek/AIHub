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
        // Dashboard'un derece ve servis özetleri tabloya dokunmadan (index-only) hesaplanır (plan 0006). exception_type
        // eklenmez: 512 karakterlik çok baytlı bir değer service_name ile birlikte btree satır sınırını aşabilir.
        builder.HasIndex(e => e.ReceivedAt)
            .IncludeProperties(e => new { e.Level, e.ServiceName })
            .HasDatabaseName("ix_log_entries_received_at_dashboard");

        // Plan 0011, risk 1: en sık exception'lar tabloya dokunmadan hesaplanır. Ayrı ve kısmi: service_name'i taşımadığı
        // için en uzun exception_type (512 karakter × 4 bayt) btree satır sınırının altında kalır; yalnızca exception'lı
        // kayıtları içerir.
        builder.HasIndex(e => e.ReceivedAt, "ix_log_entries_received_at_exception_type")
            .IncludeProperties(e => e.ExceptionType)
            .HasFilter("exception_type IS NOT NULL");

        // Plan 0011, risk 2: son hatalar ve son uyarılar (Spec 0011, CB-8) penceredeki en yeni 10 kaydı doğrudan okur;
        // penceresinde hiç hata/uyarı olmayan milyonlarca kayıt taranmaz. Info kayıtları bu indekslere girmez.
        builder.HasIndex(e => new { e.ReceivedAt, e.Id }, "ix_log_entries_errors_received_at_id")
            .HasFilter("level IN ('Error', 'Critical')");
        builder.HasIndex(e => new { e.ReceivedAt, e.Id }, "ix_log_entries_warnings_received_at_id")
            .HasFilter("level = 'Warning'");
    }
}
