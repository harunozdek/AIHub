using System.Security.Claims;
using AIHub.Api.Auth;
using AIHub.Application.Monitoring;

namespace AIHub.Api.Monitoring;

/// <summary>
/// İzlenen uygulamalar (Spec 0005, R-2, R-8). Panel oturumu ister; yanıtlar önbelleğe alınmaz. Panel okuma
/// yüzeyi (<c>/api/panel/*</c>) yalnızca GET kabul ettiği için yazma uçları bu grupta durur. Uyarı da
/// listeyi okur (plan 0005, risk 7).
/// </summary>
internal static class MonitoringEndpoints
{
    public sealed record AddRequest(string? ServiceName, string? HealthUrl);

    public sealed record UpdateRequest(string? HealthUrl);

    public static IEndpointRouteBuilder MapMonitoringEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/monitored-apps")
            .RequireAuthorization(AuthSchemes.PanelPolicy)
            .AddEndpointFilter(async (context, next) =>
            {
                context.HttpContext.Response.Headers.CacheControl = "no-store";
                return await next(context);
            });

        group.MapGet("/", ListAsync);
        group.MapPost("/", AddAsync);
        group.MapPut("/{id:guid}", UpdateAsync);
        group.MapDelete("/{id:guid}", RemoveAsync);
        return app;
    }

    private static async Task<IResult> ListAsync(ListMonitoredAppsHandler handler, CancellationToken cancellationToken) =>
        Results.Ok((await handler.HandleAsync(cancellationToken)).Select(MonitoredAppResponse.From));

    private static async Task<IResult> AddAsync(AddRequest? request, ClaimsPrincipal user, AddMonitoredAppHandler handler, CancellationToken cancellationToken)
    {
        var result = await handler.HandleAsync(
            new AddMonitoredAppCommand(request?.ServiceName, request?.HealthUrl, AuthEndpoints.UserIdOf(user)),
            cancellationToken);
        if (result.IsSuccess)
        {
            return Results.Created($"/api/monitored-apps/{result.Value}", new { id = result.Value });
        }

        return result.Errors.Contains(MonitoringErrors.LimitReached)
            ? Results.Problem(statusCode: StatusCodes.Status409Conflict, title: MonitoringErrors.LimitReached.Message)
            : ProblemResponses.Validation(result.Errors, "Uygulama eklenemedi.");
    }

    private static async Task<IResult> UpdateAsync(Guid id, UpdateRequest? request, ClaimsPrincipal user, UpdateMonitoredAppHandler handler, CancellationToken cancellationToken)
    {
        var result = await handler.HandleAsync(new UpdateMonitoredAppCommand(id, request?.HealthUrl, AuthEndpoints.UserIdOf(user)), cancellationToken);
        if (result.IsSuccess)
        {
            return Results.NoContent();
        }

        return result.Errors.Contains(MonitoringErrors.NotFound)
            ? ProblemResponses.NotFound(MonitoringErrors.NotFound.Message)
            : ProblemResponses.Validation(result.Errors, "Sağlık adresi değiştirilemedi.");
    }

    private static async Task<IResult> RemoveAsync(Guid id, RemoveMonitoredAppHandler handler, CancellationToken cancellationToken)
    {
        var result = await handler.HandleAsync(id, cancellationToken);
        return result.IsSuccess ? Results.NoContent() : ProblemResponses.NotFound(MonitoringErrors.NotFound.Message);
    }
}

/// <summary>Dışa açık sözleşme: durum ve neden metin olarak, zamanlar UTC (yerel saate çevirmek panelin işidir).</summary>
internal sealed record MonitoredAppResponse(
    Guid Id,
    string ServiceName,
    string HealthUrl,
    string Status,
    DateTimeOffset? LastCheckedAt,
    string? LastFailureReason,
    int? LastFailureStatusCode,
    DateTimeOffset? DownSince,
    DateTimeOffset CreatedAt,
    string? CreatedBy,
    DateTimeOffset? UpdatedAt,
    string? UpdatedBy)
{
    public static MonitoredAppResponse From(MonitoredAppSummary s) => new(
        s.Id,
        s.ServiceName,
        s.HealthUrl,
        s.Status.ToString(),
        s.LastCheckedAt?.ToUniversalTime(),
        s.LastFailureReason?.ToString(),
        s.LastFailureStatusCode,
        s.DownSince?.ToUniversalTime(),
        s.CreatedAt.ToUniversalTime(),
        s.CreatedBy,
        s.UpdatedAt?.ToUniversalTime(),
        s.UpdatedBy);
}
