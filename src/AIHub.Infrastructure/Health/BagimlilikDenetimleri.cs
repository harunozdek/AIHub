using AIHub.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Npgsql;
using StackExchange.Redis;

namespace AIHub.Infrastructure.Health;

/// <summary>
/// Backend'in sağlık adresinin bağımlılık denetimleri (Spec 0008, R-1). Her denetim kendi süresiyle sınırlıdır:
/// yanıt vermeyen bir bağımlılık adresi kilitlemez ve yanıt 0005'in denetim zaman aşımından (5 sn) önce döner.
/// Sonuç ayrıntı taşımaz; hata yalnızca süreç loguna yazılır (R-3).
/// </summary>
public static class BagimlilikDenetimleri
{
    public static readonly TimeSpan Timeout = TimeSpan.FromSeconds(2);

    /// <summary>
    /// Denetimin bütün işi — bağlantının kurulması ve kapatılması dahil — arka planda koşar; süre dolunca denetim
    /// beklemeden başarısız olur. Sürücülerin iptali dinlemediği yerler (ör. el sıkışmada bekleyen sunucu, bağlam
    /// kapatılırken açılmakta olan bağlantı, ilk Redis bağlantısının eşzamanlı kurulması) yanıtı geciktirmez.
    /// </summary>
    internal static async Task<HealthCheckResult> SinirliAsync(Func<CancellationToken, Task> denetim, CancellationToken cancellationToken)
    {
        using var sure = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        sure.CancelAfter(Timeout);
        await Task.Run(() => denetim(sure.Token), CancellationToken.None).WaitAsync(Timeout, cancellationToken);
        return HealthCheckResult.Healthy();
    }
}

/// <summary>
/// Log deposuna (PostgreSQL) bağlanıp tek bir sorgu çalıştırır. Bağlantı dizesi denetime özeldir: Npgsql yanıt
/// vermeyen sunucuda iptali dinlemez, bu yüzden açma ve sorgu da sürücü tarafında süreyle sınırlanır; süre dolunca
/// iptal yanıtı beklenmeden bağlantı kapatılır (review 1, F-5). Havuz kullanılmaz: askıdaki sunucuya bağlı boşta
/// bir bağlantı sorguda takılırdı (ölçülen ~6 sn); her denetim yeni bağlantı açar, açma en geç süre sonunda düşer ve
/// denetim ana havuzdan bağlantı almaz.
/// </summary>
internal sealed class PostgresHealthCheck(IDbContextFactory<AIHubDbContext> contexts) : IHealthCheck
{
    public Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default) =>
        BagimlilikDenetimleri.SinirliAsync(async ct =>
        {
            await using var db = await contexts.CreateDbContextAsync(ct);
            db.Database.SetConnectionString(DenetimBaglantisi(db.Database.GetConnectionString()));
            await db.Database.ExecuteSqlRawAsync("SELECT 1", ct);
        }, cancellationToken);

    internal static string DenetimBaglantisi(string? baglanti)
    {
        var saniye = (int)BagimlilikDenetimleri.Timeout.TotalSeconds;
        return new NpgsqlConnectionStringBuilder(baglanti)
        {
            Timeout = saniye,
            CommandTimeout = saniye,
            CancellationTimeout = -1,
            Pooling = false,
        }.ConnectionString;
    }
}

/// <summary>Kuyruğa (Redis) PING gönderir. Bağlantı ilk kullanımda kurulur; bu da süreye dahildir.</summary>
internal sealed class RedisHealthCheck(IServiceProvider services) : IHealthCheck
{
    public Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default) =>
        BagimlilikDenetimleri.SinirliAsync(
            _ => services.GetRequiredService<IConnectionMultiplexer>().GetDatabase().PingAsync(),
            cancellationToken);
}
