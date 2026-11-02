using System.Security.Claims;
using System.Text;
using AIHub.Application.Abstractions;
using AIHub.Domain.Users;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace AIHub.Api.Auth;

internal sealed class PanelTokenOptions
{
    public const string SectionName = "Auth";

    public const string Issuer = "aihub";
    public const string Audience = "aihub-panel";
    public const string StampClaim = "stamp";

    /// <summary>Sır: user-secrets veya ortam değişkeni (docs/security.md). En az 32 bayt (HS256).</summary>
    public string? TokenSigningKey { get; set; }

    public static SymmetricSecurityKey KeyFrom(string? signingKey)
    {
        if (string.IsNullOrEmpty(signingKey) || Encoding.UTF8.GetByteCount(signingKey) < 32)
        {
            throw new InvalidOperationException("Auth:TokenSigningKey yapılandırılmamış veya 32 bayttan kısa.");
        }

        return new SymmetricSecurityKey(Encoding.UTF8.GetBytes(signingKey));
    }
}

/// <summary>
/// Panel oturum token'ı: backend imzalar ve yalnızca backend doğrular; panel taşır (ADR 0002, plan 0004).
/// Süre girişten itibaren mutlak 8 saattir (Spec 0004, R-10). Damga, oturumun güncelliğini taşır (R-8, R-9).
/// </summary>
internal sealed class PanelTokenIssuer(IOptions<PanelTokenOptions> options) : IPanelTokenIssuer
{
    private readonly SigningCredentials _credentials = new(PanelTokenOptions.KeyFrom(options.Value.TokenSigningKey), SecurityAlgorithms.HmacSha256);

    public PanelToken Issue(User user, DateTimeOffset now, DateTimeOffset expiresAt)
    {
        var token = new JsonWebTokenHandler().CreateToken(new SecurityTokenDescriptor
        {
            Issuer = PanelTokenOptions.Issuer,
            Audience = PanelTokenOptions.Audience,
            Subject = new ClaimsIdentity(
            [
                new Claim("sub", user.Id.ToString()),
                new Claim("name", user.DisplayName),
                new Claim(PanelTokenOptions.StampClaim, user.SecurityStamp.ToString()),
            ]),
            IssuedAt = now.UtcDateTime,
            NotBefore = now.UtcDateTime,
            Expires = expiresAt.UtcDateTime,
            SigningCredentials = _credentials,
        });
        return new PanelToken(token, expiresAt);
    }
}
