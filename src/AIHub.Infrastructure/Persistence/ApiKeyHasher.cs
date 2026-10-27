using System.Security.Cryptography;
using System.Text;

namespace AIHub.Infrastructure.Persistence;

/// <summary>
/// Anahtarlar yüksek entropili rastgele değerlerdir; bu yüzden tuzsuz SHA-256 yeterlidir ve hash
/// üzerinden indeksli arama yapılabilir. Parola hash'leme (yavaş KDF) burada gerekmez.
/// </summary>
public static class ApiKeyHasher
{
    public static byte[] Hash(string rawApiKey) => SHA256.HashData(Encoding.UTF8.GetBytes(rawApiKey));
}
