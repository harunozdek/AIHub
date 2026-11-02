using AIHub.Domain.Common;

namespace AIHub.Domain.Users;

/// <summary>
/// Panele giriş yapan kişi (Spec 0004, ADR 0002). Şifre yalnızca hash olarak taşınır (R-4). Giriş
/// kilitleme kuralı (R-3) ve oturumları düşüren güvenlik damgası (R-8, R-9) burada tutulur.
/// </summary>
public sealed class User
{
    private User(
        Guid id,
        string userName,
        string normalizedUserName,
        string displayName,
        string passwordHash,
        Guid securityStamp,
        DateTimeOffset createdAt,
        Guid? createdBy)
    {
        Id = id;
        UserName = userName;
        NormalizedUserName = normalizedUserName;
        DisplayName = displayName;
        PasswordHash = passwordHash;
        SecurityStamp = securityStamp;
        CreatedAt = createdAt;
        CreatedBy = createdBy;
        IsActive = true;
    }

    public Guid Id { get; private set; }

    public string UserName { get; private set; }

    public string NormalizedUserName { get; private set; }

    public string DisplayName { get; private set; }

    public string PasswordHash { get; private set; }

    public bool IsActive { get; private set; }

    public int FailedLoginCount { get; private set; }

    public DateTimeOffset? LockedUntil { get; private set; }

    /// <summary>Değiştiğinde bu kullanıcının bütün açık oturumları sonraki istekte düşer.</summary>
    public Guid SecurityStamp { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    /// <summary>Ekleyen kullanıcı. İlk yönetici kurulumla geldiği için boştur (R-6).</summary>
    public Guid? CreatedBy { get; private set; }

    public DateTimeOffset? DeactivatedAt { get; private set; }

    public Guid? DeactivatedBy { get; private set; }

    public DateTimeOffset? LastLoginAt { get; private set; }

    public static Result<User> Create(
        Guid id,
        UserName userName,
        string? displayName,
        string passwordHash,
        DateTimeOffset now,
        Guid? createdBy)
    {
        if (DisplayNamePolicy.Validate(displayName) is { } displayNameError)
        {
            return Result<User>.Failure(displayNameError);
        }

        return Result<User>.Success(new User(
            id,
            userName.Value,
            userName.Normalized,
            displayName!.Trim(),
            passwordHash,
            Guid.NewGuid(),
            now.ToUniversalTime(),
            createdBy));
    }

    public bool IsLockedAt(DateTimeOffset now) => LockedUntil is { } until && until > now;

    /// <summary>
    /// Art arda <see cref="UserLimits.MaxFailedLogins"/>. başarısızlıkta hesap
    /// <see cref="UserLimits.LockoutDuration"/> kilitlenir ve sayaç sıfırlanır. Kilitliyken yapılan
    /// denemeler kilidi uzatmaz.
    /// </summary>
    public void RecordFailedLogin(DateTimeOffset now)
    {
        if (IsLockedAt(now))
        {
            return;
        }

        FailedLoginCount++;
        if (FailedLoginCount >= UserLimits.MaxFailedLogins)
        {
            LockedUntil = now.ToUniversalTime() + UserLimits.LockoutDuration;
            FailedLoginCount = 0;
        }
    }

    public void RecordSuccessfulLogin(DateTimeOffset now)
    {
        FailedLoginCount = 0;
        LockedUntil = null;
        LastLoginAt = now.ToUniversalTime();
    }

    public void Deactivate(Guid by, DateTimeOffset now)
    {
        IsActive = false;
        DeactivatedBy = by;
        DeactivatedAt = now.ToUniversalTime();
        SecurityStamp = Guid.NewGuid();
    }

    public void Activate()
    {
        IsActive = true;
        DeactivatedBy = null;
        DeactivatedAt = null;
    }

    /// <summary>Şifre değişince damga yenilenir: kullanıcının diğer oturumları düşer (R-9).</summary>
    public void ChangePassword(string newPasswordHash)
    {
        PasswordHash = newPasswordHash;
        SecurityStamp = Guid.NewGuid();
    }
}
