using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace AIHub.Infrastructure.Persistence;

/// <summary>Yalnızca `dotnet ef migrations add` içindir; veritabanına bağlanmaz, sır içermez.</summary>
internal sealed class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<AIHubDbContext>
{
    public AIHubDbContext CreateDbContext(string[] args) =>
        new(new DbContextOptionsBuilder<AIHubDbContext>().UseNpgsql("Host=design-time-only").Options);
}
