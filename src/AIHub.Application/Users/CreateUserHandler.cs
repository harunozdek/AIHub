using AIHub.Application.Abstractions;
using AIHub.Domain.Common;
using AIHub.Domain.Users;

namespace AIHub.Application.Users;

public sealed record CreateUserCommand(string? UserName, string? DisplayName, string? Password, Guid? CreatedBy);

/// <summary>Yeni kullanıcı ekler; bütün alan hataları birlikte döner (Spec 0004, R-7, AC-7).</summary>
public sealed class CreateUserHandler(IUserRepository users, IPasswordHasher hasher, IClock clock)
{
    public async Task<Result<Guid>> HandleAsync(CreateUserCommand command, CancellationToken cancellationToken)
    {
        var errors = new List<Error>();
        var userName = UserName.Create(command.UserName);
        errors.AddRange(userName.Errors);
        if (PasswordPolicy.Validate(command.Password) is { } passwordError)
        {
            errors.Add(passwordError);
        }

        if (string.IsNullOrWhiteSpace(command.DisplayName) || command.DisplayName.Length > UserLimits.DisplayNameMaxLength)
        {
            errors.Add(new Error("displayName.length", $"Görünen ad boş olamaz ve en fazla {UserLimits.DisplayNameMaxLength} karakter olabilir.", "displayName"));
        }

        if (errors.Count > 0)
        {
            return Result<Guid>.Failure(errors);
        }

        if (await users.FindByNormalizedNameAsync(userName.Value.Normalized, cancellationToken) is not null)
        {
            return Result<Guid>.Failure(UserErrors.UserNameTaken);
        }

        var now = clock.UtcNow;
        var created = User.Create(Guid.CreateVersion7(now), userName.Value, command.DisplayName, hasher.Hash(command.Password!), now, command.CreatedBy);
        if (!created.IsSuccess)
        {
            return Result<Guid>.Failure(created.Errors);
        }

        return await users.AddAsync(created.Value, cancellationToken)
            ? Result<Guid>.Success(created.Value.Id)
            : Result<Guid>.Failure(UserErrors.UserNameTaken);
    }
}
