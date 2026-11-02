using AIHub.Application.Ingestion;
using AIHub.Application.Logs;
using AIHub.Application.Monitoring;
using AIHub.Application.Users;
using Microsoft.Extensions.DependencyInjection;

namespace AIHub.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddScoped<IngestLogHandler>();
        services.AddScoped<PersistLogHandler>();
        services.AddScoped<LogListHandler>();
        services.AddScoped<LoginHandler>();
        services.AddScoped<CreateUserHandler>();
        services.AddScoped<SetUserActiveHandler>();
        services.AddScoped<ChangePasswordHandler>();
        services.AddScoped<ListUsersHandler>();
        services.AddScoped<BootstrapAdminHandler>();
        services.AddScoped<PanelSessionValidator>();
        services.AddScoped<AddMonitoredAppHandler>();
        services.AddScoped<UpdateMonitoredAppHandler>();
        services.AddScoped<RemoveMonitoredAppHandler>();
        services.AddScoped<ListMonitoredAppsHandler>();
        services.AddScoped<RunHealthChecksHandler>();
        return services;
    }
}
