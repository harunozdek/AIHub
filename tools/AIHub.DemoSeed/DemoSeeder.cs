using System.Data.Common;
using AIHub.Application;
using AIHub.Application.Abstractions;
using AIHub.Application.Ingestion;
using AIHub.Domain.Common;
using AIHub.Domain.Logs;
using AIHub.Infrastructure;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Npgsql;

namespace AIHub.DemoSeed;

/// <summary>
/// Yerel ortama örnek log kayıtları yükler (Spec 0006, R-10). Kayıtlar gerçek kabul kurallarından geçer
/// (<see cref="IngestLogHandler"/>: doğrulama ve maskeleme). Alınma zamanı geriye tarihlenir; gerçek kabul yolu onu
/// her zaman "şimdi" yaptığı için HTTP ve kuyruk atlanır, kayıt doğrudan depoya yazılır (plan 0006, risk 1).
/// </summary>
public static class DemoSeeder
{
    public const int ExitOk = 0;
    public const int ExitError = 1;
    public const int ExitRefused = 2;

    public static async Task<int> RunAsync(string environmentName, IConfiguration configuration, TextWriter output, CancellationToken cancellationToken)
    {
        if (!string.Equals(environmentName, Environments.Development, StringComparison.Ordinal))
        {
            output.WriteLine($"Örnek veri yalnızca Development ortamında yüklenir; ortam: {environmentName}. Hiçbir kayıt yazılmadı.");
            return ExitRefused;
        }

        // İkinci kilit: ortam yanlışlıkla Development verilse de üretim veritabanına yazılmaz.
        var remoteHost = RemotePostgresHost(configuration.GetConnectionString("Postgres"));
        if (remoteHost is not null)
        {
            output.WriteLine($"Örnek veri yalnızca yerel PostgreSQL'e yüklenir; sunucu: {remoteHost}. Hiçbir kayıt yazılmadı.");
            return ExitRefused;
        }

        try
        {
            var services = new ServiceCollection().AddLogging().AddApplication().AddInfrastructure(configuration);
            await using var provider = services.BuildServiceProvider();
            await provider.MigrateDatabaseAsync();

            await using var scope = provider.CreateAsyncScope();
            var clock = new SeedClock();
            var handler = new IngestLogHandler(new DirectQueue(scope.ServiceProvider.GetRequiredService<ILogEntryRepository>()), clock);

            var written = 0;
            foreach (var (receivedAt, command) in DemoVeri.Create(DateTimeOffset.UtcNow))
            {
                clock.UtcNow = receivedAt;
                var result = await handler.HandleAsync(command, cancellationToken);
                if (!result.IsSuccess)
                {
                    output.WriteLine($"Örnek kayıt kabul edilmedi: {string.Join(" ", result.Errors.Select(e => e.Message))}");
                    return ExitError;
                }

                written++;
            }

            output.WriteLine($"{written} örnek kayıt yüklendi ({DemoVeri.Services.Count} servis, son 24 saat).");
            return ExitOk;
        }
        catch (Exception ex) when (ex is InvalidOperationException or DbException or TimeoutException || ex.InnerException is DbException)
        {
            output.WriteLine($"Örnek veri yüklenemedi: {ex.Message}");
            return ExitError;
        }
    }

    private static readonly string[] LocalHosts = ["localhost", "127.0.0.1", "::1"];

    /// <summary>Bağlantı dizesindeki ilk yerel olmayan sunucu; hepsi yerelse ya da dize yoksa null.</summary>
    private static string? RemotePostgresHost(string? connectionString)
    {
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            return null; // Eksik ayar aşağıda AddInfrastructure'ın kendi hatasıyla raporlanır.
        }

        var hosts = new NpgsqlConnectionStringBuilder(connectionString).Host ?? "localhost";
        return hosts.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
            .Select(StripPort)
            .FirstOrDefault(h => !LocalHosts.Contains(h, StringComparer.OrdinalIgnoreCase));
    }

    private static string StripPort(string host)
    {
        if (host.StartsWith('['))
        {
            return host[1..host.IndexOf(']', StringComparison.Ordinal)];
        }

        var colon = host.IndexOf(':', StringComparison.Ordinal);
        return colon >= 0 && colon == host.LastIndexOf(':') ? host[..colon] : host;
    }

    /// <summary>Her kaydın alınma zamanını tek tek veren saat.</summary>
    private sealed class SeedClock : IClock
    {
        public DateTimeOffset UtcNow { get; set; }
    }

    /// <summary>Kuyruk yerine doğrudan kalıcı depo: kuyruk tüketicisinin yaptığı yazmanın aynısı.</summary>
    private sealed class DirectQueue(ILogEntryRepository repository) : ILogQueue
    {
        public async Task<Result<Guid>> EnqueueAsync(LogEntry entry, CancellationToken cancellationToken)
        {
            await repository.AddIfAbsentAsync(entry, cancellationToken);
            return Result<Guid>.Success(entry.Id);
        }
    }
}
