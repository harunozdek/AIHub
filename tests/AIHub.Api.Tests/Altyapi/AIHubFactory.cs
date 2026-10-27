using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;

namespace AIHub.Api.Tests.Altyapi;

/// <summary>Uygulamayı süreç içinde, gerçek container'lara bağlı olarak ayağa kaldırır.</summary>
public sealed class AIHubFactory(ContainerFixture containers) : WebApplicationFactory<Program>
{
    public string ConsumerName { get; init; } = "test-" + Guid.NewGuid().ToString("N");

    public bool ConsumerEnabled { get; init; } = true;

    public bool MigrateOnStartup { get; init; } = true;

    public string? PostgresConnectionOverride { get; init; }

    public string? RedisConnectionOverride { get; init; }

    public Action<IServiceCollection>? TestServices { get; init; }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.UseSetting("ConnectionStrings:Postgres", PostgresConnectionOverride ?? containers.Postgres.GetConnectionString());
        builder.UseSetting("ConnectionStrings:Redis", RedisConnectionOverride ?? containers.Redis.GetConnectionString());
        builder.UseSetting("Database:MigrateOnStartup", MigrateOnStartup.ToString());
        builder.UseSetting("Ingestion:ConsumerName", ConsumerName);
        builder.UseSetting("Ingestion:ConsumerEnabled", ConsumerEnabled.ToString());
        builder.UseSetting("Ingestion:PollInterval", "00:00:00.050");
        builder.UseSetting("Ingestion:MaxRetryDelay", "00:00:00.500");

        if (TestServices is not null)
        {
            builder.ConfigureTestServices(TestServices);
        }
    }
}
