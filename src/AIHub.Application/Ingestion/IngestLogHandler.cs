using AIHub.Application.Abstractions;
using AIHub.Domain.Common;
using AIHub.Domain.Logs;
using AIHub.Domain.Masking;

namespace AIHub.Application.Ingestion;

/// <summary>
/// Gönderimi kabul eder: maskeler, LogEntry'yi oluşturur, kuyruğa yazar ve kayıt kimliğini döner.
/// Kalıcı depoyu beklemez (BR-1). Maskeleme kuyruktan ÖNCE yapılır: maskelenmemiş veri Redis'e de
/// yazılmaz (docs/domain.md, invariant).
/// </summary>
public sealed class IngestLogHandler(ILogQueue queue, IClock clock)
{
    public async Task<Result<Guid>> HandleAsync(IngestLogCommand command, CancellationToken cancellationToken)
    {
        var receivedAt = clock.UtcNow;

        var created = LogEntry.Create(
            Guid.CreateVersion7(receivedAt),
            command.AuthenticatedServiceName,
            command.Level,
            SensitiveDataMasker.Mask(command.Message),
            command.ExceptionType,
            command.StackTrace is null ? null : SensitiveDataMasker.Mask(command.StackTrace),
            MetadataMasker.Mask(command.Metadata),
            command.OccurredAt,
            receivedAt);

        if (!created.IsSuccess)
        {
            return Result<Guid>.Failure(created.Errors);
        }

        return await queue.EnqueueAsync(created.Value, cancellationToken);
    }
}
