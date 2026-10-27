using AIHub.Application.Abstractions;
using AIHub.Domain.Logs;
using Microsoft.EntityFrameworkCore;

namespace AIHub.Infrastructure.Persistence;

internal sealed class LogEntryRepository(AIHubDbContext db) : ILogEntryRepository
{
    // ON CONFLICT DO NOTHING: kuyruk aynı kaydı ikinci kez teslim ederse (ACK'ten önce çökme)
    // ikinci satır oluşmaz ve mevcut satır güncellenmez (Spec 0001, R-6 ve R-9).
    public Task AddIfAbsentAsync(LogEntry entry, CancellationToken cancellationToken) =>
        db.Database.ExecuteSqlInterpolatedAsync(
            $"""
            INSERT INTO log_entries (id, service_name, level, message, exception_type, stack_trace, metadata, occurred_at, received_at)
            VALUES ({entry.Id}, {entry.ServiceName}, {entry.Level.ToString()}, {entry.Message}, {entry.ExceptionType},
                    {entry.StackTrace}, CAST({entry.Metadata} AS jsonb), {entry.OccurredAt}, {entry.ReceivedAt})
            ON CONFLICT (id) DO NOTHING
            """,
            cancellationToken);
}
