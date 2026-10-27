using AIHub.Application.Ingestion;
using AIHub.Application.Logs;
using Microsoft.Extensions.DependencyInjection;

namespace AIHub.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddScoped<IngestLogHandler>();
        services.AddScoped<PersistLogHandler>();
        services.AddScoped<LogListHandler>();
        return services;
    }
}
