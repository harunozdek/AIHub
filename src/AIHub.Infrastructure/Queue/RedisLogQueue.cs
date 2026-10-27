using System.Text.Json;
using AIHub.Application.Abstractions;
using AIHub.Application.Ingestion;
using AIHub.Domain.Common;
using AIHub.Domain.Logs;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using StackExchange.Redis;

namespace AIHub.Infrastructure.Queue;

internal sealed class RedisLogQueue(
    IConnectionMultiplexer redis,
    IOptions<IngestionOptions> options,
    ILogger<RedisLogQueue> logger) : ILogQueue
{
    public async Task<Result<Guid>> EnqueueAsync(LogEntry entry, CancellationToken cancellationToken)
    {
        var payload = JsonSerializer.Serialize(QueuedLogEntry.From(entry));

        try
        {
            await redis.GetDatabase().StreamAddAsync(options.Value.StreamKey, [new NameValueEntry("payload", payload)]);
            return Result<Guid>.Success(entry.Id);
        }
        catch (Exception ex) when (ex is RedisException or TimeoutException)
        {
            // Kendi hatamız kendi logumuza düşmez: yalnızca süreç logu (docs/conventions.md).
            logger.LogWarning(ex, "Kayıt kuyruğa yazılamadı; gönderim reddedildi.");
            return Result<Guid>.Failure(IngestionErrors.QueueUnavailable);
        }
    }
}
