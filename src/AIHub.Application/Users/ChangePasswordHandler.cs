using AIHub.Application.Abstractions;
using AIHub.Domain.Common;
using AIHub.Domain.Users;

namespace AIHub.Application.Users;

/// <param name="SessionExpiresAt">İsteği yapan oturumun bitişi; yeni token bu bitişi taşır (R-10, review 1, F-02).</param>
public sealed record ChangePasswordCommand(Guid UserId, DateTimeOffset SessionExpiresAt, string? CurrentPassword, string? NewPassword);

/// <summary>
/// Kullanıcı kendi şifresini mevcut şifresini doğrulayarak değiştirir. Damga yenilenir: diğer oturumlar
/// düşer, bu oturum için yeni bir token döner (Spec 0004, R-9, AC-9). Oturum uzamaz: yeni token'ın bitişi
/// eski oturumunkidir, girişten itibaren mutlak 8 saat korunur (R-10).
/// </summary>
public sealed class ChangePasswordHandler(IUserRepository users, IPasswordHasher hasher, IPanelTokenIssuer tokens, IClock clock)
{
    public async Task<Result<PanelToken>> HandleAsync(ChangePasswordCommand command, CancellationToken cancellationToken)
    {
        var user = await users.FindByIdAsync(command.UserId, cancellationToken);
        if (user is null || !user.IsActive)
        {
            return Result<PanelToken>.Failure(UserErrors.NotFound);
        }

        if (string.IsNullOrEmpty(command.CurrentPassword)
            || command.CurrentPassword.Length > UserLimits.PasswordMaxLength
            || !hasher.Verify(command.CurrentPassword, user.PasswordHash))
        {
            return Result<PanelToken>.Failure(UserErrors.CurrentPasswordInvalid);
        }

        if (PasswordPolicy.Validate(command.NewPassword, "newPassword") is { } policyError)
        {
            return Result<PanelToken>.Failure(policyError);
        }

        user.ChangePassword(hasher.Hash(command.NewPassword!));
        await users.SaveChangesAsync(cancellationToken);
        var now = clock.UtcNow;
        var latest = now + UserLimits.SessionLifetime;
        var expiresAt = command.SessionExpiresAt < latest ? command.SessionExpiresAt : latest;
        return Result<PanelToken>.Success(tokens.Issue(user, now, expiresAt));
    }
}
