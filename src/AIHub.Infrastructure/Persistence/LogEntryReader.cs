using System.Data.Common;
using AIHub.Application.Abstractions;
using AIHub.Application.Dashboard;
using AIHub.Application.Logs;
using AIHub.Domain.Common;
using AIHub.Domain.Logs;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using LogLevel = AIHub.Domain.Logs.LogLevel;

namespace AIHub.Infrastructure.Persistence;

/// <summary>
/// Panelin salt okunur deposu (Spec 0002, R-7). Keyset koşulu PostgreSQL satır karşılaştırmasıyla
/// yazılır: <c>(received_at, id) &lt; (…)</c> doğrudan <c>ix_log_entries_received_at_id</c> indeksini kullanır.
/// Depo erişilemezse <see cref="LogQueryErrors.StoreUnavailable"/> döner; ayrıntı yalnızca süreç loguna yazılır.
/// Bağlamlar fabrikadan çağrı başına açılır (plan 0011): okumalar eşzamanlı çalışabilir.
/// </summary>
internal sealed class LogEntryReader(IDbContextFactory<AIHubDbContext> contexts, ILogger<LogEntryReader> logger) : ILogEntryReader
{
    public Task<Result<IReadOnlyList<LogEntry>>> ListAsync(LogListCriteria criteria, CancellationToken cancellationToken) =>
        GuardAsync<IReadOnlyList<LogEntry>>(cancellationToken, async db =>
        {
            var cursor = criteria.Cursor;
            var query = cursor switch
            {
                null => db.LogEntries,
                { Direction: LogPageDirection.Older } => db.LogEntries.FromSql(
                    $"SELECT * FROM log_entries WHERE (received_at, id) < ({cursor.ReceivedAt.ToUniversalTime()}, {cursor.Id})"),
                _ => db.LogEntries.FromSql(
                    $"SELECT * FROM log_entries WHERE (received_at, id) > ({cursor.ReceivedAt.ToUniversalTime()}, {cursor.Id})"),
            };

            var from = criteria.From.ToUniversalTime();
            var to = criteria.To.ToUniversalTime();
            query = query.AsNoTracking().Where(e => e.ReceivedAt >= from && e.ReceivedAt <= to);
            if (criteria.ServiceName is { } serviceName)
            {
                query = query.Where(e => e.ServiceName == serviceName);
            }

            if (criteria.Levels.Count > 0)
            {
                // Sabit olarak yazılır (level IN ('Error', 'Critical')): dizi parametresiyle (= ANY(@p)) PostgreSQL kısmi
                // indeksin koşulunu kanıtlayamaz ve son hatalar/uyarılar sorgusu bütün pencereyi tarardı (plan 0011,
                // risk 2). Değerler dört dereceden ibarettir; plan önbelleğine etkisi yoktur.
                var levels = criteria.Levels.Distinct().Order().ToArray();
                query = query.Where(e => EF.Constant(levels).Contains(e.Level));
            }

            if (criteria.ExceptionType is { } exceptionType)
            {
                query = query.Where(e => e.ExceptionType == exceptionType);
            }

            // Daha yeniye giderken imlece en yakın kayıtlar seçilir, sonra en yeni önce çevrilir.
            if (cursor?.Direction == LogPageDirection.Newer)
            {
                var newer = await query.OrderBy(e => e.ReceivedAt).ThenBy(e => e.Id).Take(criteria.Limit).ToListAsync(cancellationToken);
                newer.Reverse();
                return newer;
            }

            return await query.OrderByDescending(e => e.ReceivedAt).ThenByDescending(e => e.Id).Take(criteria.Limit).ToListAsync(cancellationToken);
        });

    public Task<Result<LogEntry?>> FindAsync(Guid id, CancellationToken cancellationToken) =>
        GuardAsync(cancellationToken, db => db.LogEntries.AsNoTracking().SingleOrDefaultAsync(e => e.Id == id, cancellationToken));

    public Task<Result<IReadOnlyList<string>>> ListServiceNamesAsync(CancellationToken cancellationToken) =>
        GuardAsync<IReadOnlyList<string>>(cancellationToken, async db =>
            // "Loose index scan": her adımda bir sonraki servis adı indeksten tek satırla bulunur. PostgreSQL 17'de
            // skip scan yok; düz DISTINCT 1 milyon satırı tarardı (plan 0002, Infrastructure).
            await db.Database.SqlQueryRaw<string>(
                """
                WITH RECURSIVE s AS (
                    (SELECT service_name FROM log_entries ORDER BY service_name LIMIT 1)
                    UNION ALL
                    SELECT (SELECT l.service_name FROM log_entries l WHERE l.service_name > s.service_name ORDER BY l.service_name LIMIT 1)
                    FROM s WHERE s.service_name IS NOT NULL
                )
                SELECT service_name AS "Value" FROM s WHERE service_name IS NOT NULL
                """).ToListAsync(cancellationToken));

    public Task<Result<IReadOnlyList<ServiceErrorCount>>> ListTopErrorServicesAsync(DateTimeOffset from, DateTimeOffset to, int limit, CancellationToken cancellationToken) =>
        GuardAsync<IReadOnlyList<ServiceErrorCount>>(cancellationToken, async db =>
        {
            var top = await InWindow(db, from, to)
                .Where(e => e.Level == LogLevel.Error || e.Level == LogLevel.Critical)
                .GroupBy(e => e.ServiceName)
                .Select(g => new { ServiceName = g.Key, Count = g.LongCount() })
                // Eşitlikte bayt sırası ("C"): sunucunun locale'ından bağımsız, her ortamda aynı (review 1, F-3).
                .OrderByDescending(s => s.Count).ThenBy(s => EF.Functions.Collate(s.ServiceName, "C"))
                .Take(limit)
                .ToListAsync(cancellationToken);
            return top.Select(s => new ServiceErrorCount(s.ServiceName, s.Count)).ToArray();
        });

    public Task<Result<IReadOnlyList<ExceptionTypeCount>>> ListTopExceptionTypesAsync(DateTimeOffset from, DateTimeOffset to, int limit, CancellationToken cancellationToken) =>
        GuardAsync<IReadOnlyList<ExceptionTypeCount>>(cancellationToken, async db =>
        {
            var top = await InWindow(db, from, to)
                .Where(e => e.ExceptionType != null)
                .GroupBy(e => e.ExceptionType!)
                .Select(g => new { ExceptionType = g.Key, Count = g.LongCount(), LastSeenAt = g.Max(e => e.ReceivedAt) })
                .OrderByDescending(t => t.Count).ThenByDescending(t => t.LastSeenAt)
                .Take(limit)
                .ToListAsync(cancellationToken);
            return top.Select(t => new ExceptionTypeCount(t.ExceptionType, t.Count, t.LastSeenAt)).ToArray();
        });

    public Task<Result<IReadOnlyList<SliceLevelCount>>> ListSliceLevelCountsAsync(
        DateTimeOffset from, DateTimeOffset to, TimeSpan sliceLength, int sliceCount, CancellationToken cancellationToken) =>
        GuardAsync<IReadOnlyList<SliceLevelCount>>(cancellationToken, async db =>
        {
            var start = from.ToUniversalTime();
            var end = to.ToUniversalTime();
            var sliceMicroseconds = sliceLength.Ticks / TimeSpan.TicksPerMicrosecond;
            var lastSlice = sliceCount - 1;
            // Dilim tamsayı mikrosaniyeyle hesaplanır: kayan nokta yuvarlaması sınırdaki kaydı komşu dilime kaydırmaz.
            // Pencere koşulu InWindow ile aynıdır; kapsayan indeks (received_at) INCLUDE (level) yeterlidir.
            var rows = await db.Database.SqlQuery<SliceRow>(
                $"""
                SELECT LEAST((extract(epoch FROM received_at - {start}) * 1000000)::bigint / {sliceMicroseconds}, {lastSlice})::int AS "Slice",
                       level AS "Level",
                       count(*) AS "Count"
                FROM log_entries
                WHERE received_at >= {start} AND received_at <= {end}
                GROUP BY 1, 2
                """).ToListAsync(cancellationToken);
            return rows.Select(r => new SliceLevelCount(r.Slice, Enum.Parse<LogLevel>(r.Level), r.Count)).ToArray();
        });

    private sealed record SliceRow(int Slice, string Level, long Count);

    /// <summary>Dashboard penceresi; log listesiyle aynı sınırlar: [from, to] (Spec 0006, AC-6).</summary>
    private static IQueryable<LogEntry> InWindow(AIHubDbContext db, DateTimeOffset from, DateTimeOffset to)
    {
        var start = from.ToUniversalTime();
        var end = to.ToUniversalTime();
        return db.LogEntries.AsNoTracking().Where(e => e.ReceivedAt >= start && e.ReceivedAt <= end);
    }

    /// <summary>
    /// Her okuma kendi bağlamıyla çalışır: dashboard'un sorguları eşzamanlıdır ve bir DbContext aynı anda tek sorgu
    /// yürütebilir (plan 0011, CB-5). Bağlam açılamazsa (bağlantı hatası) da depo okunamıyor sayılır.
    /// </summary>
    private async Task<Result<T>> GuardAsync<T>(CancellationToken cancellationToken, Func<AIHubDbContext, Task<T>> read)
    {
        try
        {
            await using var db = await contexts.CreateDbContextAsync(cancellationToken);
            return Result<T>.Success(await read(db));
        }
        // Npgsql EF stratejisi geçici bağlantı hatasını InvalidOperationException içine sarar; iç hata da bakılır.
        catch (Exception ex) when (ex is DbException or TimeoutException || ex.InnerException is DbException or TimeoutException)
        {
            logger.LogWarning(ex, "Log deposu okunamadı.");
            return Result<T>.Failure(LogQueryErrors.StoreUnavailable);
        }
    }
}
