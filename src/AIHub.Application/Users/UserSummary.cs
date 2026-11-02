namespace AIHub.Application.Users;

/// <summary>Kullanıcılar ekranının satırı; şifre hash'i, sayaç ve damga taşımaz (Spec 0004, R-11).</summary>
public sealed record UserSummary(
    Guid Id,
    string UserName,
    string DisplayName,
    bool IsActive,
    DateTimeOffset CreatedAt,
    string? CreatedBy,
    DateTimeOffset? LastLoginAt,
    DateTimeOffset? DeactivatedAt,
    string? DeactivatedBy);
