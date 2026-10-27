using System.Data.Common;
using AIHub.Application.Abstractions;
using AIHub.Application.Ingestion;
using AIHub.Domain.Common;
using AIHub.Infrastructure.Queue;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace AIHub.Infrastructure.Persistence;

/// <summary>
/// Her istekte veritabanına bakar; önbellek yoktur, bu yüzden iptal anında etkilidir (plan 0001, R-7
/// ve değişiklik A). Sorgu <see cref="IngestionOptions.KeyLookupTimeout"/> içinde bitmezse depo
/// erişilemez sayılır: yavaş bir depo kabul yanıtını sınırsız geciktirmez (review F-6).
/// Bekleme sürücüden bağımsız sınırlanır: donmuş bir sunucuda Npgsql iptale onlarca saniye yanıt
/// vermeyebilir. Sorgu kendi bağlamında koştuğu için geride bırakılması isteğin bağlamını etkilemez.
/// </summary>
internal sealed class ApiKeyStore(
    IDbContextFactory<AIHubDbContext> contexts,
    IOptions<IngestionOptions> options,
    ILogger<ApiKeyStore> logger) : IApiKeyStore
{
    public async Task<Result<string?>> FindServiceNameAsync(string rawApiKey, CancellationToken cancellationToken)
    {
        var hash = ApiKeyHasher.Hash(rawApiKey);
        using var lookupCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        var lookup = LookupAsync(hash, lookupCancellation.Token);

        var finished = await Task.WhenAny(lookup, Task.Delay(options.Value.KeyLookupTimeout, cancellationToken));
        if (finished != lookup)
        {
            cancellationToken.ThrowIfCancellationRequested();
            await lookupCancellation.CancelAsync();
            // Geride bırakılan sorgunun hatası gözlemlenir; sürücü vazgeçtiğinde bağlam kendini kapatır.
            _ = lookup.ContinueWith(t => _ = t.Exception, CancellationToken.None, TaskContinuationOptions.OnlyOnFaulted, TaskScheduler.Default);
            logger.LogWarning("Anahtar deposu {Timeout} içinde yanıt vermedi; gönderim reddedildi.", options.Value.KeyLookupTimeout);
            return Result<string?>.Failure(IngestionErrors.KeyStoreUnavailable);
        }

        try
        {
            return Result<string?>.Success(await lookup);
        }
        // Npgsql EF stratejisi geçici bağlantı hatasını InvalidOperationException içine sarar; iç hata da bakılır.
        catch (Exception ex) when (ex is DbException or TimeoutException || ex.InnerException is DbException or TimeoutException)
        {
            logger.LogWarning(ex, "Anahtar deposu erişilemez; gönderim reddedildi.");
            return Result<string?>.Failure(IngestionErrors.KeyStoreUnavailable);
        }
    }

    private async Task<string?> LookupAsync(byte[] hash, CancellationToken cancellationToken)
    {
        await using var db = await contexts.CreateDbContextAsync(cancellationToken);
        return await db.ServiceApiKeys
            .AsNoTracking()
            .Where(k => k.KeyHash == hash && k.RevokedAt == null)
            .Select(k => k.ServiceName)
            .FirstOrDefaultAsync(cancellationToken);
    }
}
