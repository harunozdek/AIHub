using AIHub.Application.Abstractions;
using AIHub.Domain.Users;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace AIHub.Infrastructure.Persistence;

internal sealed class UserRepository(AIHubDbContext db) : IUserRepository
{
    /// <summary>Kullanıcı yönetimi yazmalarının sıraya girdiği PostgreSQL advisory lock anahtarı.</summary>
    private const long UserAdminLockKey = 0x41_49_48_55_42_55_53; // "AIHUBUS"

    public Task<User?> FindByNormalizedNameAsync(string normalizedUserName, CancellationToken cancellationToken) =>
        db.Users.SingleOrDefaultAsync(u => u.NormalizedUserName == normalizedUserName, cancellationToken);

    public Task<User?> FindByIdAsync(Guid id, CancellationToken cancellationToken) =>
        db.Users.SingleOrDefaultAsync(u => u.Id == id, cancellationToken);

    public Task<bool> AnyAsync(CancellationToken cancellationToken) => db.Users.AnyAsync(cancellationToken);

    public Task<int> CountActiveAsync(CancellationToken cancellationToken) => db.Users.CountAsync(u => u.IsActive, cancellationToken);

    public async Task<IReadOnlyList<User>> ListAsync(CancellationToken cancellationToken) =>
        await db.Users.AsNoTracking().ToListAsync(cancellationToken);

    public async Task<bool> AddAsync(User user, CancellationToken cancellationToken)
    {
        db.Users.Add(user);
        try
        {
            await db.SaveChangesAsync(cancellationToken);
            return true;
        }
        catch (DbUpdateException ex) when (ex.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation })
        {
            // Aynı ad o arada eklendi: bağlamda kalan kayıt sonraki kaydetmeleri bozmasın.
            db.Entry(user).State = EntityState.Detached;
            return false;
        }
    }

    public Task SaveChangesAsync(CancellationToken cancellationToken) => db.SaveChangesAsync(cancellationToken);

    /// <summary>
    /// İşlem süresince tutulan advisory lock: iki eşzamanlı pasifleştirme "son aktif kullanıcı"
    /// kuralını birlikte aşamaz (Spec 0004, R-8; plan 0004). İç içe çağrı mevcut işleme katılır.
    /// </summary>
    public async Task<T> RunSerializedAsync<T>(Func<Task<T>> action, CancellationToken cancellationToken)
    {
        if (db.Database.CurrentTransaction is not null)
        {
            return await action();
        }

        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        await db.Database.ExecuteSqlInterpolatedAsync($"SELECT pg_advisory_xact_lock({UserAdminLockKey})", cancellationToken);
        var result = await action();
        await transaction.CommitAsync(cancellationToken);
        return result;
    }

    /// <summary>
    /// `SELECT … FOR UPDATE` ile satır kilidi, ardından yeniden okuma: kilidi bekleyen istek, önceki
    /// isteğin yazdığı sayacı ve kilidi görür (Spec 0004, R-3; review 1, F-01).
    /// </summary>
    public async Task<T> RunLockedAsync<T>(User user, Func<Task<T>> action, CancellationToken cancellationToken)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        await db.Database.ExecuteSqlInterpolatedAsync($"SELECT 1 FROM users WHERE id = {user.Id} FOR UPDATE", cancellationToken);
        await db.Entry(user).ReloadAsync(cancellationToken);
        var result = await action();
        await transaction.CommitAsync(cancellationToken);
        return result;
    }
}
