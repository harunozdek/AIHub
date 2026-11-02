using AIHub.Application.Abstractions;

namespace AIHub.Application.Users;

/// <summary>Kullanıcılar ekranı: ekleyen ve pasifleştiren kimlikleri görünen adlara çevrilir (Spec 0004, R-11).</summary>
public sealed class ListUsersHandler(IUserRepository users)
{
    public async Task<IReadOnlyList<UserSummary>> HandleAsync(CancellationToken cancellationToken)
    {
        var all = await users.ListAsync(cancellationToken);
        var names = all.ToDictionary(u => u.Id, u => u.DisplayName);
        string? NameOf(Guid? id) => id is { } value && names.TryGetValue(value, out var name) ? name : null;

        return all
            .OrderBy(u => u.NormalizedUserName, StringComparer.Ordinal)
            .Select(u => new UserSummary(
                u.Id,
                u.UserName,
                u.DisplayName,
                u.IsActive,
                u.CreatedAt,
                NameOf(u.CreatedBy),
                u.LastLoginAt,
                u.DeactivatedAt,
                NameOf(u.DeactivatedBy)))
            .ToArray();
    }
}
