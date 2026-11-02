using AIHub.Application.Abstractions;
using AIHub.Domain.Common;
using AIHub.Domain.Users;

namespace AIHub.Application.Users;

public sealed record LoginCommand(string? UserName, string? Password);

public sealed record LoginResult(Guid UserId, string DisplayName, PanelToken Token);

/// <summary>
/// Kullanıcı adı ve şifreyle giriş (Spec 0004, R-1–R-3). Her başarısızlık aynı hatayı döner. Kullanıcı
/// yoksa da bir hash doğrulaması yapılır: yanıt süresi kullanıcı adının varlığını sızdırmaz (plan risk 2).
/// Hash doğrulaması satır kilidinin dışında yapılır; karar kilit altında, güncel durumla verilir: eşzamanlı
/// yanlış denemeler sayacı kaybetmez, kilit oluşurken yoldaki doğru şifre de reddedilir (review 1, F-01).
/// </summary>
public sealed class LoginHandler(IUserRepository users, IPasswordHasher hasher, IPanelTokenIssuer tokens, IClock clock)
{
    private static string? _dummyHash;

    public async Task<Result<LoginResult>> HandleAsync(LoginCommand command, CancellationToken cancellationToken)
    {
        // Sınırı aşan şifre hash'lenmez: aşırı uzun girdiyle CPU harcatılamaz. Sonuç yine aynı hatadır.
        if (string.IsNullOrEmpty(command.UserName) || string.IsNullOrEmpty(command.Password)
            || command.Password.Length > UserLimits.PasswordMaxLength)
        {
            return Result<LoginResult>.Failure(UserErrors.InvalidCredentials);
        }

        var now = clock.UtcNow;
        var user = await users.FindByNormalizedNameAsync(UserName.Normalize(command.UserName), cancellationToken);
        if (user is null)
        {
            _dummyHash ??= hasher.Hash(Guid.NewGuid().ToString());
            hasher.Verify(command.Password, _dummyHash);
            return Result<LoginResult>.Failure(UserErrors.InvalidCredentials);
        }

        var verifiedHash = user.PasswordHash;
        var passwordOk = hasher.Verify(command.Password, verifiedHash);
        return await users.RunLockedAsync(user, async () =>
        {
            if (!user.IsActive || user.IsLockedAt(now))
            {
                return Result<LoginResult>.Failure(UserErrors.InvalidCredentials);
            }

            // Şifre bu arada değiştiyse karar güncel hash ile verilir.
            if (user.PasswordHash != verifiedHash)
            {
                passwordOk = hasher.Verify(command.Password, user.PasswordHash);
            }

            if (!passwordOk)
            {
                user.RecordFailedLogin(now);
                await users.SaveChangesAsync(cancellationToken);
                return Result<LoginResult>.Failure(UserErrors.InvalidCredentials);
            }

            user.RecordSuccessfulLogin(now);
            await users.SaveChangesAsync(cancellationToken);
            return Result<LoginResult>.Success(new LoginResult(user.Id, user.DisplayName, tokens.Issue(user, now)));
        }, cancellationToken);
    }
}
