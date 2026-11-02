namespace AIHub.Domain.Users;

/// <summary>Spec 0004: kullanıcı adı, görünen ad ve şifre sınırları; giriş kilitleme kuralı.</summary>
public static class UserLimits
{
    public const int UserNameMinLength = 3;

    public const int UserNameMaxLength = 64;

    public const int DisplayNameMaxLength = 100;

    public const int PasswordMinLength = 12;

    public const int PasswordMaxLength = 128;

    /// <summary>Art arda bu kadar başarısız denemeden sonra hesap kilitlenir (R-3).</summary>
    public const int MaxFailedLogins = 5;

    public static readonly TimeSpan LockoutDuration = TimeSpan.FromMinutes(15);

    /// <summary>Oturum girişten itibaren mutlak olarak bu kadar sürer (R-10).</summary>
    public static readonly TimeSpan SessionLifetime = TimeSpan.FromHours(8);
}
