using System.Data.Common;
using AIHub.Application.Abstractions;
using AIHub.Application.Logs;
using AIHub.Domain.Common;
using AIHub.Domain.Logs;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace AIHub.Infrastructure.Persistence;

/// <summary>
/// Panelin salt okunur deposu (Spec 0002, R-7). Keyset koşulu PostgreSQL satır karşılaştırmasıyla
/// yazılır: <c>(received_at, id) &lt; (…)</c> doğrudan <c>ix_log_entries_received_at_id</c> indeksini kullanır.
/// Depo erişilemezse <see cref="LogQueryErrors.StoreUnavailable"/> döner; ayrıntı yalnızca süreç loguna yazılır.
/// </summary>
internal sealed class LogEntryReader(AIHubDbContext db, ILogger<LogEntryReader> logger) : ILogEntryReader
{
    public Task<Result<IReadOnlyList<LogEntry>>> ListAsync(LogListCriteria criteria, CancellationToken cancellationToken) =>
        GuardAsync<IReadOnlyList<LogEntry>>(async () =>
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
                var levels = criteria.Levels;
                query = query.Where(e => levels.Contains(e.Level));
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
        GuardAsync(() => db.LogEntries.AsNoTracking().SingleOrDefaultAsync(e => e.Id == id, cancellationToken));

    public Task<Result<IReadOnlyList<string>>> ListServiceNamesAsync(CancellationToken cancellationToken) =>
        GuardAsync<IReadOnlyList<string>>(async () =>
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

    private async Task<Result<T>> GuardAsync<T>(Func<Task<T>> read)
    {
        try
        {
            return Result<T>.Success(await read());
        }
        // Npgsql EF stratejisi geçici bağlantı hatasını InvalidOperationException içine sarar; iç hata da bakılır.
        catch (Exception ex) when (ex is DbException or TimeoutException || ex.InnerException is DbException or TimeoutException)
        {
            logger.LogWarning(ex, "Log deposu okunamadı.");
            return Result<T>.Failure(LogQueryErrors.StoreUnavailable);
        }
    }
}
