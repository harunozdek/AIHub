using AIHub.Domain.Common;

namespace AIHub.Application.Abstractions;

public interface IApiKeyStore
{
    /// <summary>
    /// Ham anahtarın bağlı olduğu ServiceName'i verir. Anahtar tanınmıyorsa veya iptal edilmişse
    /// başarılı sonuç <c>null</c> taşır — ikisi dışarıdan ayırt edilemez. Depo erişilemezse
    /// <see cref="Ingestion.IngestionErrors.KeyStoreUnavailable"/> döner (Spec 0001, R-8).
    /// </summary>
    Task<Result<string?>> FindServiceNameAsync(string rawApiKey, CancellationToken cancellationToken);
}
