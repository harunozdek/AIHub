using AIHub.Domain.Users;

namespace AIHub.Application.Abstractions;

/// <summary>Başarılı girişte panelin oturumda taşıdığı, backend'in imzaladığı token (ADR 0002).</summary>
public interface IPanelTokenIssuer
{
    /// <summary>Girişte: süre şimdiden itibaren mutlak 8 saattir (Spec 0004, R-10).</summary>
    PanelToken Issue(User user, DateTimeOffset now) => Issue(user, now, now + UserLimits.SessionLifetime);

    /// <summary>Var olan oturum için yeni token (ör. şifre değişikliği): bitiş eski oturumunkidir, uzamaz.</summary>
    PanelToken Issue(User user, DateTimeOffset now, DateTimeOffset expiresAt);
}

public sealed record PanelToken(string Token, DateTimeOffset ExpiresAt);
