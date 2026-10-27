using AIHub.Application.Abstractions;
using AIHub.Domain.Common;
using AIHub.Domain.Logs;

namespace AIHub.Application.Logs;

/// <summary>
/// Panel liste sorgusu: filtreyi doğrular, varsayılan son 24 saati uygular ve keyset sayfasını kurar
/// (Spec 0002, R-2 … R-4, R-8). Okuma deposundan bir fazla kayıt istenir; fazlalık o yönde başka
/// sayfa olduğunu gösterir.
/// </summary>
public sealed class LogListHandler(ILogEntryReader reader, IClock clock)
{
    public const int PageSize = 50;
    public const int MaxRangeDays = 31;
    public static readonly TimeSpan DefaultRange = TimeSpan.FromHours(24);

    public async Task<Result<LogPage>> HandleAsync(LogListQuery query, CancellationToken cancellationToken)
    {
        var errors = new List<Error>();

        var levels = new List<LogLevel>();
        foreach (var text in query.Levels)
        {
            if (LogLevels.TryParse(text, out var level))
            {
                levels.Add(level);
            }
            else
            {
                errors.Add(LogQueryErrors.UnknownLevel(text));
            }
        }

        var to = query.To ?? clock.UtcNow;
        var from = query.From ?? to - DefaultRange;
        if (to < from)
        {
            errors.Add(LogQueryErrors.RangeReversed);
        }
        else if (to - from > TimeSpan.FromDays(MaxRangeDays))
        {
            errors.Add(LogQueryErrors.RangeTooLong);
        }

        if (errors.Count > 0)
        {
            return Result<LogPage>.Failure(errors);
        }

        var criteria = new LogListCriteria(
            string.IsNullOrWhiteSpace(query.ServiceName) ? null : query.ServiceName,
            levels.Distinct().ToArray(),
            from,
            to,
            query.Cursor,
            PageSize + 1);

        var fetched = await reader.ListAsync(criteria, cancellationToken);
        if (!fetched.IsSuccess)
        {
            return Result<LogPage>.Failure(fetched.Errors);
        }

        return Result<LogPage>.Success(BuildPage(fetched.Value, query.Cursor, from, to));
    }

    private static LogPage BuildPage(IReadOnlyList<LogEntry> fetched, LogCursor? cursor, DateTimeOffset from, DateTimeOffset to)
    {
        var hasMore = fetched.Count > PageSize;
        var towardNewer = cursor?.Direction == LogPageDirection.Newer;

        // Kayıtlar en yeni önce gelir. Daha yeniye giderken fazlalık en baştaki (imleçten en uzak) kayıttır.
        var page = !hasMore ? fetched : towardNewer ? fetched.Skip(1).ToArray() : fetched.Take(PageSize).ToArray();
        var items = page.Select(LogSummary.From).ToArray();

        if (items.Length == 0)
        {
            return new LogPage(items, null, null, from, to);
        }

        var newest = items[0];
        var oldest = items[^1];
        var olderExists = towardNewer ? true : hasMore;
        var newerExists = towardNewer ? hasMore : cursor is not null;

        return new LogPage(
            items,
            olderExists ? new LogCursor(oldest.ReceivedAt, oldest.Id, LogPageDirection.Older) : null,
            newerExists ? new LogCursor(newest.ReceivedAt, newest.Id, LogPageDirection.Newer) : null,
            from,
            to);
    }
}
