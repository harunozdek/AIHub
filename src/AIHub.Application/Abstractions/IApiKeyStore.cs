namespace AIHub.Application.Abstractions;

public interface IApiKeyStore
{
    /// <summary>
    /// Ham anahtarın bağlı olduğu ServiceName'i verir. Anahtar tanınmıyorsa veya iptal edilmişse
    /// <c>null</c> döner — ikisi dışarıdan ayırt edilemez.
    /// </summary>
    Task<string?> FindServiceNameAsync(string rawApiKey, CancellationToken cancellationToken);
}
