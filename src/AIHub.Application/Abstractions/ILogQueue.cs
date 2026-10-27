using AIHub.Domain.Common;
using AIHub.Domain.Logs;

namespace AIHub.Application.Abstractions;

/// <summary>
/// Kabul edilen kayıtların dayanıklı kuyruğu (BR-1). Kuyruğa yazılamazsa kayıt kabul edilmiş
/// sayılmaz: <see cref="IngestionErrors.QueueUnavailable"/> döner (Spec 0001, R-8).
/// </summary>
public interface ILogQueue
{
    Task<Result<Guid>> EnqueueAsync(LogEntry entry, CancellationToken cancellationToken);
}
