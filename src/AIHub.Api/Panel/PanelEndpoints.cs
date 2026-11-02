using AIHub.Api.Auth;
using AIHub.Application.Abstractions;
using AIHub.Application.Logs;

namespace AIHub.Api.Panel;

/// <summary>
/// Panelin salt okunur yüzeyi (Spec 0002). Yalnızca panel kimliğiyle (OIDC bearer) erişilir; servis API
/// anahtarı burada geçmez (R-1). Yanıtlar önbelleğe alınmaz: çıkıştan sonra log içeriği tarayıcıda kalmaz.
/// </summary>
internal static class PanelEndpoints
{
    public static IEndpointRouteBuilder MapPanelEndpoints(this IEndpointRouteBuilder app)
    {
        var panel = app.MapGroup("/api/panel")
            .RequireAuthorization(AuthSchemes.PanelPolicy)
            .AddEndpointFilter(async (context, next) =>
            {
                context.HttpContext.Response.Headers.CacheControl = "no-store";
                return await next(context);
            });

        panel.MapGet("/logs", ListAsync);
        panel.MapGet("/logs/{id}", GetAsync);
        panel.MapGet("/services", ListServicesAsync);
        return app;
    }

    private static async Task<IResult> ListAsync(HttpContext context, LogListHandler handler, CancellationToken cancellationToken)
    {
        var query = PanelQueryParser.Parse(context.Request.Query);
        if (!query.IsSuccess)
        {
            return ProblemResponses.Validation(query.Errors, "Sorgu doğrulanamadı.");
        }

        var page = await handler.HandleAsync(query.Value, cancellationToken);
        if (page.IsSuccess)
        {
            return Results.Ok(LogPageResponse.Create(page.Value));
        }

        return page.Errors.Contains(LogQueryErrors.StoreUnavailable)
            ? ProblemResponses.RetryLater(context, LogQueryErrors.StoreUnavailable)
            : ProblemResponses.Validation(page.Errors, "Sorgu doğrulanamadı.");
    }

    // Bozuk kimlik ile var olmayan kimlik dışarıdan ayırt edilmez: ikisi de 404 (Spec 0002, AC-9).
    private static async Task<IResult> GetAsync(string id, HttpContext context, ILogEntryReader reader, CancellationToken cancellationToken)
    {
        if (!Guid.TryParse(id, out var logId))
        {
            return ProblemResponses.NotFound("Kayıt bulunamadı.");
        }

        var entry = await reader.FindAsync(logId, cancellationToken);
        if (!entry.IsSuccess)
        {
            return ProblemResponses.RetryLater(context, LogQueryErrors.StoreUnavailable);
        }

        return entry.Value is { } found
            ? Results.Ok(LogDetailResponse.From(found))
            : ProblemResponses.NotFound("Kayıt bulunamadı.");
    }

    private static async Task<IResult> ListServicesAsync(HttpContext context, ILogEntryReader reader, CancellationToken cancellationToken)
    {
        var services = await reader.ListServiceNamesAsync(cancellationToken);
        return services.IsSuccess
            ? Results.Ok(new ServiceListResponse(services.Value))
            : ProblemResponses.RetryLater(context, LogQueryErrors.StoreUnavailable);
    }
}
