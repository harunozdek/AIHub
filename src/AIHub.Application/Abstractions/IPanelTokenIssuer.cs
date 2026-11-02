using AIHub.Domain.Users;

namespace AIHub.Application.Abstractions;

/// <summary>Başarılı girişte panelin oturumda taşıdığı, backend'in imzaladığı token (ADR 0002).</summary>
public interface IPanelTokenIssuer
{
    PanelToken Issue(User user, DateTimeOffset now);
}

public sealed record PanelToken(string Token, DateTimeOffset ExpiresAt);
