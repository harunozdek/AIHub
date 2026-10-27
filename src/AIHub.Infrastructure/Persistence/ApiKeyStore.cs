using System.Data.Common;
using AIHub.Application.Abstractions;
using AIHub.Application.Ingestion;
using AIHub.Domain.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace AIHub.Infrastructure.Persistence;

/// <summary>
/// Her istekte veritabanına bakar; önbellek yoktur, bu yüzden iptal anında etkilidir (plan 0001, R-7
/// ve değişiklik A).
/// </summary>
internal sealed class ApiKeyStore(AIHubDbContext db, ILogger<ApiKeyStore> logger) : IApiKeyStore
{
    public async Task<Result<string?>> FindServiceNameAsync(string rawApiKey, CancellationToken cancellationToken)
    {
        var hash = ApiKeyHasher.Hash(rawApiKey);

        try
        {
            var serviceName = await db.ServiceApiKeys
                .AsNoTracking()
                .Where(k => k.KeyHash == hash && k.RevokedAt == null)
                .Select(k => k.ServiceName)
                .FirstOrDefaultAsync(cancellationToken);
            return Result<string?>.Success(serviceName);
        }
        // Npgsql EF stratejisi geçici bağlantı hatasını InvalidOperationException içine sarar; iç hata da bakılır.
        catch (Exception ex) when (ex is DbException or TimeoutException || ex.InnerException is DbException or TimeoutException)
        {
            logger.LogWarning(ex, "Anahtar deposu erişilemez; gönderim reddedildi.");
            return Result<string?>.Failure(IngestionErrors.KeyStoreUnavailable);
        }
    }
}
