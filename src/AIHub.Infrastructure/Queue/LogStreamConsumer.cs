using System.Text.Json;
using AIHub.Application.Ingestion;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Npgsql;
using StackExchange.Redis;

namespace AIHub.Infrastructure.Queue;

/// <summary>
/// Stream'den okuyup kalıcı depoya yazar. Kayıpsızlık (Spec 0001, R-7) şuna dayanır: bir girdi
/// yalnızca depo yazımı başarıyla bittikten sonra onaylanır (XACK). Onaylanmamış girdi bu tüketicinin
/// bekleyen listesinde kalır ve her turda önce o liste okunur — depo geri geldiğinde de, süreç
/// yeniden başladığında da (aynı ConsumerName ile). Depo idempotent olduğu için yeniden teslim
/// ikinci satır üretmez.
/// </summary>
internal sealed class LogStreamConsumer(
    IConnectionMultiplexer redis,
    IServiceScopeFactory scopeFactory,
    IOptions<IngestionOptions> options,
    ILogger<LogStreamConsumer> logger) : BackgroundService
{
    private readonly IngestionOptions _options = options.Value;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var retryDelay = _options.PollInterval;
        var groupReady = false;

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var db = redis.GetDatabase();
                // Grup bir kez kurulur; Redis grubu kaybederse (NOGROUP) yeniden kurulur (review F-9).
                if (!groupReady)
                {
                    await EnsureGroupAsync(db);
                    groupReady = true;
                }

                var entries = await ReadNextBatchAsync(db);
                if (entries.Length == 0)
                {
                    await Task.Delay(_options.PollInterval, stoppingToken);
                    continue;
                }

                foreach (var entry in entries)
                {
                    await ProcessAsync(db, entry, stoppingToken);
                }

                retryDelay = _options.PollInterval;
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }
            catch (RedisServerException ex) when (ex.Message.StartsWith("NOGROUP", StringComparison.Ordinal))
            {
                logger.LogWarning("Tüketici grubu bulunamadı; yeniden oluşturulacak.");
                groupReady = false;
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Kuyruk işlenemedi; {Delay} sonra yeniden denenecek. Onaylanmamış kayıtlar beklemede kalır.", retryDelay);
                await Task.Delay(retryDelay, stoppingToken).ContinueWith(_ => { }, TaskScheduler.Default);
                retryDelay = TimeSpan.FromTicks(Math.Min(retryDelay.Ticks * 2, _options.MaxRetryDelay.Ticks));
            }
        }
    }

    private async Task EnsureGroupAsync(IDatabase db)
    {
        try
        {
            // "0": grup sonradan oluşursa, o ana kadar kuyruğa girmiş kayıtlar da işlenir.
            await db.StreamCreateConsumerGroupAsync(_options.StreamKey, _options.ConsumerGroup, "0", createStream: true);
        }
        catch (RedisServerException ex) when (ex.Message.StartsWith("BUSYGROUP", StringComparison.Ordinal))
        {
        }
    }

    private async Task<StreamEntry[]> ReadNextBatchAsync(IDatabase db)
    {
        // 1) Bu tüketicinin daha önce okuyup onaylamadığı kayıtlar (depo hatası, yeniden başlatma).
        var pending = await db.StreamReadGroupAsync(_options.StreamKey, _options.ConsumerGroup, _options.ConsumerName, "0", _options.BatchSize);
        if (pending.Length > 0)
        {
            return pending;
        }

        // 2) Başka adla çalışmış ve artık yaşamayan bir tüketicide takılı kalmış kayıtlar.
        var claimed = await db.StreamAutoClaimAsync(_options.StreamKey, _options.ConsumerGroup, _options.ConsumerName,
            (long)_options.ClaimIdleAfter.TotalMilliseconds, "0-0", _options.BatchSize);
        if (claimed.ClaimedEntries.Length > 0)
        {
            return claimed.ClaimedEntries;
        }

        // 3) Yeni kayıtlar.
        return await db.StreamReadGroupAsync(_options.StreamKey, _options.ConsumerGroup, _options.ConsumerName, ">", _options.BatchSize);
    }

    private async Task ProcessAsync(IDatabase db, StreamEntry entry, CancellationToken cancellationToken)
    {
        var payload = entry["payload"];
        var queued = payload.IsNullOrEmpty ? null : JsonSerializer.Deserialize<QueuedLogEntry>(payload.ToString());
        var logEntry = queued?.ToLogEntry();

        if (logEntry is null || !logEntry.IsSuccess)
        {
            // Okunamayan girdi kuyruğu tıkamasın; incelenmek üzere ayrı stream'e taşınır.
            await DeadLetterAsync(db, entry, payload, "çözülemedi");
        }
        else
        {
            try
            {
                await using var scope = scopeFactory.CreateAsyncScope();
                var handler = scope.ServiceProvider.GetRequiredService<PersistLogHandler>();
                await handler.HandleAsync(logEntry.Value, cancellationToken);
            }
            catch (Exception ex) when (IsPermanentDataError(ex))
            {
                // Depo bu girdiyi hiçbir denemede kabul etmeyecek: bekleyenlerin başında kalırsa
                // arkasındaki tüm kayıtları durdururdu (review F-1). Bağlantı hataları buraya düşmez,
                // yeniden denenir (R-7).
                logger.LogError(ex, "Stream girdisi {EntryId} depoya yazılamaz.", entry.Id);
                await DeadLetterAsync(db, entry, payload, "depo reddetti");
            }
        }

        // Yalnızca depo yazımı bittikten sonra: onayla ve stream'den sil (tek tüketici grubu var).
        await db.StreamAcknowledgeAsync(_options.StreamKey, _options.ConsumerGroup, entry.Id);
        await db.StreamDeleteAsync(_options.StreamKey, [entry.Id]);
    }

    private async Task DeadLetterAsync(IDatabase db, StreamEntry entry, RedisValue payload, string reason)
    {
        logger.LogError("Stream girdisi {EntryId} dead-letter stream'e taşındı: {Reason}.", entry.Id, reason);
        await db.StreamAddAsync(_options.DeadLetterStreamKey,
            [new NameValueEntry("source_id", entry.Id), new NameValueEntry("reason", reason), new NameValueEntry("payload", payload)]);
    }

    /// <summary>SQLSTATE sınıfı 22 (data exception): veri kendisi geçersiz, yeniden denemek sonucu değiştirmez.</summary>
    private static bool IsPermanentDataError(Exception ex) =>
        (ex as PostgresException ?? ex.InnerException as PostgresException)?.SqlState.StartsWith("22", StringComparison.Ordinal) == true;
}
