using AIHub.Application.Abstractions;
using Microsoft.EntityFrameworkCore;

namespace AIHub.Infrastructure.Persistence;

/// <summary>
/// Her istekte veritabanına bakar; önbellek yoktur, bu yüzden iptal anında etkilidir (plan 0001, R-7).
/// </summary>
internal sealed class ApiKeyStore(AIHubDbContext db) : IApiKeyStore
{
    public Task<string?> FindServiceNameAsync(string rawApiKey, CancellationToken cancellationToken)
    {
        var hash = ApiKeyHasher.Hash(rawApiKey);

        return db.ServiceApiKeys
            .AsNoTracking()
            .Where(k => k.KeyHash == hash && k.RevokedAt == null)
            .Select(k => k.ServiceName)
            .FirstOrDefaultAsync(cancellationToken);
    }
}
