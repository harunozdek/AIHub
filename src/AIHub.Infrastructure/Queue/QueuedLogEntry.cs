using AIHub.Domain.Common;
using AIHub.Domain.Logs;

namespace AIHub.Infrastructure.Queue;

/// <summary>Stream girdisindeki kaydın tel biçimi. İçerik kuyruğa girmeden önce maskelenmiştir.</summary>
internal sealed record QueuedLogEntry(
    Guid Id,
    string ServiceName,
    string Level,
    string Message,
    string? ExceptionType,
    string? StackTrace,
    string? Metadata,
    DateTimeOffset? OccurredAt,
    DateTimeOffset ReceivedAt)
{
    public static QueuedLogEntry From(LogEntry entry) => new(
        entry.Id,
        entry.ServiceName,
        entry.Level.ToString(),
        entry.Message,
        entry.ExceptionType,
        entry.StackTrace,
        entry.Metadata,
        entry.OccurredAt,
        entry.ReceivedAt);

    public Result<LogEntry> ToLogEntry()
    {
        if (!LogLevels.TryParse(Level, out var level))
        {
            return Result<LogEntry>.Failure(new Error("level.invalid", "Kuyruktaki kaydın LogLevel değeri geçersiz.", "level"));
        }

        return LogEntry.Create(Id, ServiceName, level, Message, ExceptionType, StackTrace, Metadata, OccurredAt, ReceivedAt);
    }
}
