using AIHub.Application.Abstractions;
using AIHub.Domain.Monitoring;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace AIHub.Infrastructure.Persistence;

internal sealed class MonitoredAppRepository(AIHubDbContext db) : IMonitoredAppRepository
{
    /// <summary>Ekleme sınırının (200) yarışta aşılmaması için eklemelerin sıraya girdiği advisory lock anahtarı.</summary>
    private const long AddLockKey = 0x41_49_48_4D_4F_4E_41; // "AIHMONA"

    public async Task<IReadOnlyList<MonitoredApp>> ListAsync(CancellationToken cancellationToken) =>
        await db.MonitoredApps.AsNoTracking().ToListAsync(cancellationToken);

    public Task<MonitoredApp?> FindByIdAsync(Guid id, CancellationToken cancellationToken) =>
        db.MonitoredApps.SingleOrDefaultAsync(a => a.Id == id, cancellationToken);

    public async Task<AddMonitoredAppOutcome> AddAsync(MonitoredApp app, int maxApps, CancellationToken cancellationToken)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        await db.Database.ExecuteSqlInterpolatedAsync($"SELECT pg_advisory_xact_lock({AddLockKey})", cancellationToken);
        if (await db.MonitoredApps.AnyAsync(a => a.ServiceName == app.ServiceName, cancellationToken))
        {
            return AddMonitoredAppOutcome.ServiceNameTaken;
        }

        if (await db.MonitoredApps.CountAsync(cancellationToken) >= maxApps)
        {
            return AddMonitoredAppOutcome.LimitReached;
        }

        db.MonitoredApps.Add(app);
        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException ex) when (ex.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation })
        {
            db.Entry(app).State = EntityState.Detached;
            return AddMonitoredAppOutcome.ServiceNameTaken;
        }

        await transaction.CommitAsync(cancellationToken);
        return AddMonitoredAppOutcome.Added;
    }

    public async Task RemoveAsync(MonitoredApp app, CancellationToken cancellationToken)
    {
        db.MonitoredApps.Remove(app);
        await db.SaveChangesAsync(cancellationToken);
    }

    public Task SaveChangesAsync(CancellationToken cancellationToken) => db.SaveChangesAsync(cancellationToken);

    /// <summary>
    /// Tek sorgu: zamanı gelen satırları kilitler, kilitli olanları atlar ve sonraki denetim zamanını ileri
    /// alır. Aynı anda çalışan ikinci bir AIHub örneği aynı satırı o aralıkta alamaz (ADR 0003). Sonraki
    /// zaman satırın kendi çizelgesinden ilerler: worker turunun gecikmesi aralığa eklenmez, aralıklar
    /// kaymaz. Çizelge çok gerideyse (ör. yeniden başlatma) şimdiden başlar (review 1, F-01). Biraz
    /// gerideyse iki denetim arası yarım aralıktan kısa olmaz; art arda gelen denetimler Down eşiğini
    /// kısaltmaz (review 2, N-1).
    /// </summary>
    public async Task<IReadOnlyList<DueCheck>> ClaimDueAsync(DateTimeOffset now, TimeSpan interval, int max, CancellationToken cancellationToken)
    {
        var minGap = interval / 2;
        var rows = await db.Database.SqlQuery<DueRow>($"""
            UPDATE monitored_apps SET next_check_at = CASE
                WHEN next_check_at + {interval} > {now} THEN GREATEST(next_check_at + {interval}, {now} + {minGap})
                ELSE {now} + {interval}
            END
            WHERE id IN (
                SELECT id FROM monitored_apps
                WHERE next_check_at <= {now}
                ORDER BY next_check_at
                LIMIT {max}
                FOR UPDATE SKIP LOCKED)
            RETURNING id AS "Id", health_url AS "HealthUrl"
            """).ToListAsync(cancellationToken);
        return rows.Select(r => new DueCheck(r.Id, r.HealthUrl)).ToArray();
    }

    public Task<bool> RecordResultAsync(Guid id, string checkedUrl, Action<MonitoredApp> apply, CancellationToken cancellationToken) =>
        UnderRowLockAsync(id, app => app.HealthUrl == checkedUrl, apply, cancellationToken);

    public Task<bool> UpdateLockedAsync(Guid id, Action<MonitoredApp> apply, CancellationToken cancellationToken) =>
        UnderRowLockAsync(id, _ => true, apply, cancellationToken);

    /// <summary>
    /// Satır kilidi, ardından güncel halin okunması: değişiklik, o arada başka bir işlemin yazdığı değerlerin
    /// üzerine uygulanır. Kilitsiz okumada EF yalnızca okunan anlık görüntüden farklı sütunları yazar ve
    /// araya giren yazma kaybolurdu (review 1, F-02).
    /// </summary>
    private async Task<bool> UnderRowLockAsync(Guid id, Func<MonitoredApp, bool> applies, Action<MonitoredApp> apply, CancellationToken cancellationToken)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        await db.Database.ExecuteSqlInterpolatedAsync($"SELECT 1 FROM monitored_apps WHERE id = {id} FOR UPDATE", cancellationToken);
        var app = await db.MonitoredApps.SingleOrDefaultAsync(a => a.Id == id, cancellationToken);
        if (app is null)
        {
            return false;
        }

        // Bağlam satırı daha önce izlemiş olabilir: kilit alındıktan sonraki hal okunur.
        await db.Entry(app).ReloadAsync(cancellationToken);
        if (!applies(app))
        {
            return false;
        }

        apply(app);
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return true;
    }

    private sealed class DueRow
    {
        public Guid Id { get; init; }

        public string HealthUrl { get; init; } = "";
    }
}
