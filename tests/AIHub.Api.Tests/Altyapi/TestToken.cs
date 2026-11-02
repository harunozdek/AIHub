using System.Security.Claims;
using System.Text;
using AIHub.Domain.Users;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace AIHub.Api.Tests.Altyapi;

/// <summary>
/// Panel oturum token'ları testte, Api'nin kullandığı imza anahtarıyla üretilir (ADR 0002). Geçerli bir
/// token gerçek bir kullanıcıya ve onun güncel damgasına bağlıdır; negatif durumlar parametrelerle kurulur.
/// </summary>
public static class TestToken
{
    public const string SigningKey = "aihub-test-imza-anahtari-en-az-32-bayt-uzunlugunda";
    public const string Issuer = "aihub";
    public const string Audience = "aihub-panel";

    public static string Create(
        User user,
        TimeSpan? lifetime = null,
        string audience = Audience,
        string signingKey = SigningKey,
        Guid? stamp = null)
    {
        var now = DateTime.UtcNow;
        var expires = now + (lifetime ?? TimeSpan.FromMinutes(10));
        return new JsonWebTokenHandler().CreateToken(new SecurityTokenDescriptor
        {
            Issuer = Issuer,
            Audience = audience,
            Subject = new ClaimsIdentity(
            [
                new Claim("sub", user.Id.ToString()),
                new Claim("name", user.DisplayName),
                new Claim("stamp", (stamp ?? user.SecurityStamp).ToString()),
            ]),
            IssuedAt = expires < now ? expires.AddMinutes(-10) : now,
            NotBefore = expires < now ? expires.AddMinutes(-10) : now,
            Expires = expires,
            SigningCredentials = new SigningCredentials(new SymmetricSecurityKey(Encoding.UTF8.GetBytes(signingKey)), SecurityAlgorithms.HmacSha256),
        });
    }

    /// <summary>Depoya hiç yazılmamış bir kullanıcı: depo erişilemezken token üretmek için.</summary>
    public static User BellekteKullanici() =>
        User.Create(Guid.CreateVersion7(), UserName.Create("bellekte").Value, "Bellekte", "yok", DateTimeOffset.UtcNow, null).Value;
}
