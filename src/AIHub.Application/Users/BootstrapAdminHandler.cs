using AIHub.Application.Abstractions;
using AIHub.Domain.Common;

namespace AIHub.Application.Users;

public sealed record BootstrapAdminCommand(string? UserName, string? DisplayName, string? Password);

/// <summary>
/// Hiç kullanıcı yokken yapılandırmadaki ilk hesabı oluşturur. Kullanıcı varsa hiçbir şey yapmaz;
/// mevcut hesapları ve şifrelerini değiştirmez (Spec 0004, R-6). Sonuç: hesap oluşturulduysa <c>true</c>.
/// </summary>
public sealed class BootstrapAdminHandler(IUserRepository users, CreateUserHandler create)
{
    public Task<Result<bool>> HandleAsync(BootstrapAdminCommand command, CancellationToken cancellationToken) =>
        users.RunSerializedAsync(async () =>
        {
            if (await users.AnyAsync(cancellationToken))
            {
                return Result<bool>.Success(false);
            }

            var created = await create.HandleAsync(new CreateUserCommand(command.UserName, command.DisplayName, command.Password, null), cancellationToken);
            return created.IsSuccess ? Result<bool>.Success(true) : Result<bool>.Failure(created.Errors);
        }, cancellationToken);
}
