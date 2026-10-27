using AIHub.Application.Abstractions;
using AIHub.Application.Ingestion;
using AIHub.Application.Logs;
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

/// <summary>Bellekte okuma deposu; gerçek deponun sıralama ve keyset sözleşmesini taklit eder.</summary>
internal sealed class FakeLogEntryReader(IEnumerable<LogEntry> entries) : ILogEntryReader
{
    private readonly List<LogEntry> _entries = [.. entries];

    public bool Unavailable { get; init; }

    public LogListCriteria? LastCriteria { get; private set; }

    public void Add(LogEntry entry) => _entries.Add(entry);

    public Task<Result<IReadOnlyList<LogEntry>>> ListAsync(LogListCriteria criteria, CancellationToken cancellationToken)
    {
        LastCriteria = criteria;
        if (Unavailable)
        {
            return Task.FromResult(Result<IReadOnlyList<LogEntry>>.Failure(LogQueryErrors.StoreUnavailable));
        }

        var matching = _entries.Where(e =>
            e.ReceivedAt >= criteria.From && e.ReceivedAt <= criteria.To
            && (criteria.ServiceName is null || e.ServiceName == criteria.ServiceName)
            && (criteria.Levels.Count == 0 || criteria.Levels.Contains(e.Level)));

        IEnumerable<LogEntry> page = criteria.Cursor switch
        {
            null => matching.OrderByDescending(Key),
            { Direction: LogPageDirection.Older } c => matching.Where(e => Key(e).CompareTo((c.ReceivedAt, c.Id)) < 0).OrderByDescending(Key),
            { } c => matching.Where(e => Key(e).CompareTo((c.ReceivedAt, c.Id)) > 0).OrderBy(Key).Take(criteria.Limit).OrderByDescending(Key),
        };

        IReadOnlyList<LogEntry> result = page.Take(criteria.Limit).ToArray();
        return Task.FromResult(Result<IReadOnlyList<LogEntry>>.Success(result));
    }

    public Task<Result<LogEntry?>> FindAsync(Guid id, CancellationToken cancellationToken) =>
        Task.FromResult(Result<LogEntry?>.Success(_entries.SingleOrDefault(e => e.Id == id)));

    public Task<Result<IReadOnlyList<string>>> ListServiceNamesAsync(CancellationToken cancellationToken) =>
        Task.FromResult(Result<IReadOnlyList<string>>.Success(_entries.Select(e => e.ServiceName).Distinct().Order().ToArray()));

    private static (DateTimeOffset, Guid) Key(LogEntry e) => (e.ReceivedAt, e.Id);
}
