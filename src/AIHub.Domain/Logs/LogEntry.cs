using AIHub.Domain.Common;

namespace AIHub.Domain.Logs;

/// <summary>
/// AIHub'a ulaşan ve saklanan tek log kaydı. Oluştuktan sonra değişmez: setter yoktur, düzeltme yeni
/// bir kayıttır (docs/domain.md, Key domain invariants). İçerik buraya maskelenmiş olarak gelir (BR-3).
/// </summary>
public sealed class LogEntry
{
    private LogEntry(
        Guid id,
        string serviceName,
        LogLevel level,
        string message,
        string? exceptionType,
        string? stackTrace,
        string? metadata,
        DateTimeOffset? occurredAt,
        DateTimeOffset receivedAt)
    {
        Id = id;
        ServiceName = serviceName;
        Level = level;
        Message = message;
        ExceptionType = exceptionType;
        StackTrace = stackTrace;
        Metadata = metadata;
        OccurredAt = occurredAt;
        ReceivedAt = receivedAt;
    }

    public Guid Id { get; }

    public string ServiceName { get; }

    public LogLevel Level { get; }

    public string Message { get; }

    public string? ExceptionType { get; }

    public string? StackTrace { get; }

    /// <summary>Şemasız anahtar/değer verisi, JSON nesnesi metni olarak.</summary>
    public string? Metadata { get; }

    /// <summary>Kaynak servisin bildirdiği zaman. Gönderilmediyse boştur.</summary>
    public DateTimeOffset? OccurredAt { get; }

    /// <summary>AIHub'ın kaydı kabul ettiği an (UTC).</summary>
    public DateTimeOffset ReceivedAt { get; }

    public static Result<LogEntry> Create(
        Guid id,
        string serviceName,
        LogLevel level,
        string message,
        string? exceptionType,
        string? stackTrace,
        string? metadata,
        DateTimeOffset? occurredAt,
        DateTimeOffset receivedAt)
    {
        var errors = new List<Error>();

        if (string.IsNullOrWhiteSpace(serviceName))
        {
            errors.Add(new Error("serviceName.required", "ServiceName boş olamaz.", "serviceName"));
        }

        if (!Enum.IsDefined(level))
        {
            errors.Add(new Error("level.invalid", "LogLevel Info, Warning, Error veya Critical olmalıdır.", "level"));
        }

        if (string.IsNullOrWhiteSpace(message))
        {
            errors.Add(new Error("message.required", "Message boş olamaz.", "message"));
        }
        else if (message.Length > LogEntryLimits.MessageMaxLength)
        {
            errors.Add(new Error("message.tooLong", $"Message en fazla {LogEntryLimits.MessageMaxLength} karakter olabilir.", "message"));
        }

        if (exceptionType is not null && exceptionType.Length > LogEntryLimits.ExceptionTypeMaxLength)
        {
            errors.Add(new Error("exceptionType.tooLong", $"ExceptionType en fazla {LogEntryLimits.ExceptionTypeMaxLength} karakter olabilir.", "exceptionType"));
        }

        if (errors.Count > 0)
        {
            return Result<LogEntry>.Failure(errors);
        }

        return Result<LogEntry>.Success(new LogEntry(
            id,
            serviceName,
            level,
            message,
            exceptionType,
            stackTrace,
            metadata,
            occurredAt?.ToUniversalTime(),
            receivedAt.ToUniversalTime()));
    }
}
