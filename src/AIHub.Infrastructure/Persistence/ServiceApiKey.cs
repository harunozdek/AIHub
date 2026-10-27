namespace AIHub.Infrastructure.Persistence;

/// <summary>
/// Bir servise bağlı API anahtarı. Ham anahtar saklanmaz, yalnızca SHA-256 hash'i (docs/security.md).
/// Anahtar yönetimi (oluşturma/iptal arayüzü) Spec 0002'nin konusudur.
/// </summary>
public sealed class ServiceApiKey
{
    public Guid Id { get; set; }

    public required string ServiceName { get; set; }

    public required byte[] KeyHash { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset? RevokedAt { get; set; }
}
