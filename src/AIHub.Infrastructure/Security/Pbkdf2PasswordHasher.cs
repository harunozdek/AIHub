using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using AIHub.Application.Abstractions;
using Microsoft.Extensions.Options;

namespace AIHub.Infrastructure.Security;

public sealed class PasswordHashOptions
{
    public const string SectionName = "Auth:PasswordHash";

    /// <summary>OWASP'ın PBKDF2-HMAC-SHA256 için önerdiği değer. Testler hız için düşürür.</summary>
    public const int DefaultIterations = 600_000;

    public int Iterations { get; set; } = DefaultIterations;
}

/// <summary>
/// PBKDF2-HMAC-SHA256, 16 bayt rastgele tuz, 32 bayt çıktı; yerleşik <see cref="Rfc2898DeriveBytes"/>
/// ile, yeni bağımlılık olmadan (Spec 0004, R-4; plan 0004). Saklanan biçim sürümlü ve tur sayısını
/// taşır: <c>v1$tur$tuz$hash</c>. Tur sayısı sonradan artırılsa da eski hash'ler doğrulanır.
/// </summary>
public sealed class Pbkdf2PasswordHasher(IOptions<PasswordHashOptions> options) : IPasswordHasher
{
    private const string Version = "v1";
    private const int SaltSize = 16;
    private const int HashSize = 32;

    public string Hash(string password)
    {
        var iterations = options.Value.Iterations;
        var salt = RandomNumberGenerator.GetBytes(SaltSize);
        var hash = Derive(password, salt, iterations);
        return string.Join('$', Version, iterations.ToString(CultureInfo.InvariantCulture), Convert.ToBase64String(salt), Convert.ToBase64String(hash));
    }

    public bool Verify(string password, string passwordHash)
    {
        var parts = passwordHash.Split('$');
        if (parts.Length != 4 || parts[0] != Version
            || !int.TryParse(parts[1], NumberStyles.None, CultureInfo.InvariantCulture, out var iterations) || iterations < 1)
        {
            return false;
        }

        byte[] salt;
        byte[] expected;
        try
        {
            salt = Convert.FromBase64String(parts[2]);
            expected = Convert.FromBase64String(parts[3]);
        }
        catch (FormatException)
        {
            return false;
        }

        return CryptographicOperations.FixedTimeEquals(Derive(password, salt, iterations), expected);
    }

    private static byte[] Derive(string password, byte[] salt, int iterations) =>
        Rfc2898DeriveBytes.Pbkdf2(Encoding.UTF8.GetBytes(password), salt, iterations, HashAlgorithmName.SHA256, HashSize);
}
