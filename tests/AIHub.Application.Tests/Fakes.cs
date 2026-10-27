using AIHub.Application.Abstractions;
using AIHub.Application.Ingestion;
using AIHub.Domain.Common;
using AIHub.Domain.Logs;

namespace AIHub.Application.Tests;

internal sealed class FakeClock(DateTimeOffset now) : IClock
{
    public DateTimeOffset UtcNow { get; } = now;
}

internal sealed class FakeLogQueue : ILogQueue
{
    public bool Unavailable { get; init; }

    public List<LogEntry> Enqueued { get; } = [];

    public Task<Result<Guid>> EnqueueAsync(LogEntry entry, CancellationToken cancellationToken)
    {
        if (Unavailable)
        {
            return Task.FromResult(Result<Guid>.Failure(IngestionErrors.QueueUnavailable));
        }

        Enqueued.Add(entry);
        return Task.FromResult(Result<Guid>.Success(entry.Id));
    }
}
