using AIHub.Domain.Logs;
using Microsoft.EntityFrameworkCore;

namespace AIHub.Infrastructure.Persistence;

public sealed class AIHubDbContext(DbContextOptions<AIHubDbContext> options) : DbContext(options)
{
    public DbSet<LogEntry> LogEntries => Set<LogEntry>();

    public DbSet<ServiceApiKey> ServiceApiKeys => Set<ServiceApiKey>();

    protected override void OnModelCreating(ModelBuilder modelBuilder) =>
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AIHubDbContext).Assembly);
}
