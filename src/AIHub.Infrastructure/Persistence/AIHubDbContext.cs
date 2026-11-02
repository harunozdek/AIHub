using AIHub.Domain.Logs;
using AIHub.Domain.Users;
using Microsoft.EntityFrameworkCore;

namespace AIHub.Infrastructure.Persistence;

public sealed class AIHubDbContext(DbContextOptions<AIHubDbContext> options) : DbContext(options)
{
    public DbSet<LogEntry> LogEntries => Set<LogEntry>();

    public DbSet<ServiceApiKey> ServiceApiKeys => Set<ServiceApiKey>();

    public DbSet<User> Users => Set<User>();

    protected override void OnModelCreating(ModelBuilder modelBuilder) =>
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AIHubDbContext).Assembly);
}
