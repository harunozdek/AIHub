using System.Security.Claims;
using System.Security.Cryptography;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace AIHub.Api.Tests.Altyapi;

/// <summary>
/// Panel token'ları testte üretilen bir RSA anahtarıyla imzalanır; Api bu anahtara factory üzerinden
/// güvenir. Testler gerçek bir kimlik sağlayıcıya hiç gitmez (docs/testing.md, determinizm).
/// </summary>
public static class TestToken
{
    public const string Issuer = "https://test-idp.aihub.local";
    public const string Audience = "aihub-api";

    public static readonly RsaSecurityKey SigningKey = new(RSA.Create(2048)) { KeyId = "aihub-test" };

    public static string Create(string audience = Audience, TimeSpan? lifetime = null, SecurityKey? key = null)
    {
        var now = DateTime.UtcNow;
        var expires = now + (lifetime ?? TimeSpan.FromMinutes(10));
        return new JsonWebTokenHandler().CreateToken(new SecurityTokenDescriptor
        {
            Issuer = Issuer,
            Audience = audience,
            Subject = new ClaimsIdentity([new Claim("sub", "panel-kullanicisi"), new Claim("name", "Panel Kullanıcısı")]),
            IssuedAt = expires < now ? expires.AddMinutes(-10) : now,
            NotBefore = expires < now ? expires.AddMinutes(-10) : now,
            Expires = expires,
            SigningCredentials = new SigningCredentials(key ?? SigningKey, SecurityAlgorithms.RsaSha256),
        });
    }
}
