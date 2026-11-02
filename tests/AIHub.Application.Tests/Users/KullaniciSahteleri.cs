using AIHub.Application.Abstractions;
using AIHub.Domain.Users;

namespace AIHub.Application.Tests.Users;

internal sealed class AyarlanabilirSaat(DateTimeOffset now) : IClock
{
    public DateTimeOffset UtcNow { get; set; } = now;
}

/// <summary>Hash'i okunur tutar; testler şifrenin hash yerine saklanmadığını Infrastructure'da ayrıca doğrular.</summary>
internal sealed class SahteHasher : IPasswordHasher
{
    public int VerifyCount { get; private set; }

    public string Hash(string password) => "h:" + password;

    public bool Verify(string password, string passwordHash)
    {
        VerifyCount++;
        return passwordHash == "h:" + password;
    }
}

internal sealed class SahteTokenUretici : IPanelTokenIssuer
{
    public PanelToken Issue(User user, DateTimeOffset now, DateTimeOffset expiresAt) =>
        new($"{user.Id}:{user.SecurityStamp}", expiresAt);
}

internal sealed class SahteKullaniciDeposu : IUserRepository
{
    public List<User> Users { get; } = [];

    public int SaveCount { get; private set; }

    public bool NextAddConflicts { get; set; }

    public Task<User?> FindByNormalizedNameAsync(string normalizedUserName, CancellationToken cancellationToken) =>
        Task.FromResult(Users.SingleOrDefault(u => u.NormalizedUserName == normalizedUserName));

    public Task<User?> FindByIdAsync(Guid id, CancellationToken cancellationToken) =>
        Task.FromResult(Users.SingleOrDefault(u => u.Id == id));

    public Task<bool> AnyAsync(CancellationToken cancellationToken) => Task.FromResult(Users.Count > 0);

    public Task<int> CountActiveAsync(CancellationToken cancellationToken) => Task.FromResult(Users.Count(u => u.IsActive));

    public Task<IReadOnlyList<User>> ListAsync(CancellationToken cancellationToken) => Task.FromResult<IReadOnlyList<User>>([.. Users]);

    public Task<bool> AddAsync(User user, CancellationToken cancellationToken)
    {
        if (NextAddConflicts)
        {
            return Task.FromResult(false);
        }

        Users.Add(user);
        return Task.FromResult(true);
    }

    public Task SaveChangesAsync(CancellationToken cancellationToken)
    {
        SaveCount++;
        return Task.CompletedTask;
    }

    public Task<T> RunSerializedAsync<T>(Func<Task<T>> action, CancellationToken cancellationToken) => action();

    public Task<T> RunLockedAsync<T>(User user, Func<Task<T>> action, CancellationToken cancellationToken) => action();

    public User Ekle(string ad, string sifre = "dogru-sifre-123", bool aktif = true)
    {
        var user = User.Create(Guid.CreateVersion7(), UserName.Create(ad).Value, ad, "h:" + sifre, DateTimeOffset.UnixEpoch, null).Value;
        if (!aktif)
        {
            user.Deactivate(Guid.CreateVersion7(), DateTimeOffset.UnixEpoch);
        }

        Users.Add(user);
        return user;
    }
}
