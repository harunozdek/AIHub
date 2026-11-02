using AIHub.Application.Abstractions;

namespace AIHub.Application.Users;

/// <summary>
/// Her panel isteğinde: token'ın sahibi hâlâ aktif mi, damgası güncel mi? Pasifleştirme ve şifre
/// değişikliği damgayı yenilediği için eski oturumlar burada düşer (Spec 0004, R-8, R-9).
/// </summary>
public sealed class PanelSessionValidator(IUserRepository users)
{
    public async Task<bool> IsValidAsync(Guid userId, Guid securityStamp, CancellationToken cancellationToken)
    {
        var user = await users.FindByIdAsync(userId, cancellationToken);
        return user is { IsActive: true } && user.SecurityStamp == securityStamp;
    }
}
