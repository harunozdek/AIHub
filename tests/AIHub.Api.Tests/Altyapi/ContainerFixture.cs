using Testcontainers.PostgreSql;
using Testcontainers.Redis;

namespace AIHub.Api.Tests.Altyapi;

/// <summary>
/// PostgreSQL ve Redis container'larının TEK yaşam döngüsü sahibi (docs/testing.md): koşu başında
/// başlatır, sonunda — hata dahil — durdurur. Her koşu taze container'larla, yani taze şemayla başlar
/// (Resource hygiene, kural 5).
/// </summary>
public sealed class ContainerFixture : IAsyncLifetime
{
    public PostgreSqlContainer Postgres { get; } = new PostgreSqlBuilder("postgres:17-alpine").Build();

    public RedisContainer Redis { get; } = new RedisBuilder("redis:7-alpine").Build();

    public async ValueTask InitializeAsync() =>
        await Task.WhenAll(Postgres.StartAsync(), Redis.StartAsync());

    public async ValueTask DisposeAsync()
    {
        await Postgres.DisposeAsync();
        await Redis.DisposeAsync();
    }
}

[CollectionDefinition(Name)]
public sealed class IngestionCollection : ICollectionFixture<ContainerFixture>
{
    public const string Name = "Ingestion";
}
