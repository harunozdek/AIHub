using AIHub.Application.Abstractions;
using AIHub.Domain.Common;

namespace AIHub.Application.Users;

public sealed record SetUserActiveCommand(Guid UserId, bool Active, Guid ActorId);

/// <summary>
/// Pasifleştirme ve yeniden etkinleştirme. Kullanıcı kendini ve son aktif kullanıcıyı
/// pasifleştiremez; pasifleştirme damgayı yeniler, açık oturum sonraki istekte düşer (Spec 0004, R-8).
/// </summary>
public sealed class SetUserActiveHandler(IUserRepository users, IClock clock)
{
    public Task<Result<bool>> HandleAsync(SetUserActiveCommand command, CancellationToken cancellationToken)
    {
        if (!command.Active && command.UserId == command.ActorId)
        {
            return Task.FromResult(Result<bool>.Failure(UserErrors.CannotDeactivateSelf));
        }

        return users.RunSerializedAsync(async () =>
        {
            var user = await users.FindByIdAsync(command.UserId, cancellationToken);
            if (user is null)
            {
                return Result<bool>.Failure(UserErrors.NotFound);
            }

            if (user.IsActive == command.Active)
            {
                return Result<bool>.Success(true);
            }

            if (command.Active)
            {
                user.Activate();
            }
            else
            {
                if (await users.CountActiveAsync(cancellationToken) <= 1)
                {
                    return Result<bool>.Failure(UserErrors.CannotDeactivateLastActive);
                }

                user.Deactivate(command.ActorId, clock.UtcNow);
            }

            await users.SaveChangesAsync(cancellationToken);
            return Result<bool>.Success(true);
        }, cancellationToken);
    }
}
