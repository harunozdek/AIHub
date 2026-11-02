using AIHub.Application.Abstractions;
using AIHub.Infrastructure.Persistence;
using AIHub.Infrastructure.Queue;
using AIHub.Infrastructure.Security;
using AIHub.Infrastructure.Time;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using StackExchange.Redis;

namespace AIHub.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        // Bağlantı dizeleri depoda değil: geliştirmede user-secrets, üretimde ortam değişkeni (docs/security.md).
        var postgres = configuration.GetConnectionString("Postgres")
            ?? throw new InvalidOperationException("ConnectionStrings:Postgres yapılandırılmamış.");
        var redisConnection = configuration.GetConnectionString("Redis")
            ?? throw new InvalidOperationException("ConnectionStrings:Redis yapılandırılmamış.");

        // Fabrika: anahtar sorgusu kendi kısa ömürlü bağlamını kullanır (ApiKeyStore). Bağlamın kendisi de
        // scoped olarak kayıtlı kalır (repository).
        services.AddDbContextFactory<AIHubDbContext>(o => o.UseNpgsql(postgres));
        services.AddScoped<ILogEntryRepository, LogEntryRepository>();
        services.AddScoped<IApiKeyStore, ApiKeyStore>();
        services.AddScoped<ILogEntryReader, LogEntryReader>();
        services.AddSingleton<IClock, SystemClock>();
        services.AddScoped<IUserRepository, UserRepository>();
        services.AddScoped<IMonitoredAppRepository, MonitoredAppRepository>();
        services.Configure<PasswordHashOptions>(configuration.GetSection(PasswordHashOptions.SectionName));
        services.AddSingleton<IPasswordHasher, Pbkdf2PasswordHasher>();

        services.Configure<IngestionOptions>(configuration.GetSection(IngestionOptions.SectionName));
        services.AddSingleton<IConnectionMultiplexer>(_ =>
        {
            var redisOptions = ConfigurationOptions.Parse(redisConnection);
            redisOptions.AbortOnConnectFail = false;
            // Redis yokken komutlar kuyrukta bekletilmez, hemen başarısız olur: gönderen servis 503 alır
            // ve kendisi yeniden dener (Spec 0001, R-8). Bekletmek, kabul yanıtını geciktirirdi.
            redisOptions.BacklogPolicy = BacklogPolicy.FailFast;
            return ConnectionMultiplexer.Connect(redisOptions);
        });
        services.AddSingleton<ILogQueue, RedisLogQueue>();

        var ingestion = configuration.GetSection(IngestionOptions.SectionName).Get<IngestionOptions>() ?? new IngestionOptions();
        if (ingestion.ConsumerEnabled)
        {
            services.AddHostedService<LogStreamConsumer>();
        }

        return services;
    }

    /// <summary>Migration'ları uygular. Yalnızca yapılandırma açıkça istediğinde çağrılır.</summary>
    public static async Task MigrateDatabaseAsync(this IServiceProvider services)
    {
        await using var scope = services.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<AIHubDbContext>().Database.MigrateAsync();
    }
}
