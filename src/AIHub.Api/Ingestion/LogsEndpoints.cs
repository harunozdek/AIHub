using System.Buffers;
using AIHub.Api.Auth;
using AIHub.Application.Ingestion;
using AIHub.Domain.Logs;

namespace AIHub.Api.Ingestion;

internal static class LogsEndpoints
{
    public static IEndpointRouteBuilder MapLogsEndpoints(this IEndpointRouteBuilder app)
    {
        // Yalnızca servis API anahtarı: panel token'ı bu yüzeyde 401 alır (Spec 0002, R-1).
        app.MapPost("/api/logs", IngestAsync).RequireAuthorization(AuthSchemes.IngestionPolicy);
        return app;
    }

    /// <summary>Doğrular, kuyruğa yazar ve depoyu beklemeden 202 döner (BR-1).</summary>
    private static async Task<IResult> IngestAsync(HttpContext context, IngestLogHandler handler, CancellationToken cancellationToken)
    {
        var serviceName = context.User.FindFirst(ApiKeyAuthenticationHandler.ServiceNameClaim)!.Value;

        var body = await ReadBodyAsync(context.Request, LogEntryLimits.RequestBodyMaxBytes, cancellationToken);
        if (body is null)
        {
            return ProblemResponses.PayloadTooLarge(LogEntryLimits.RequestBodyMaxBytes);
        }

        var request = LogRequest.Parse(body);
        if (!request.IsSuccess)
        {
            return ProblemResponses.Validation(request.Errors);
        }

        var result = await handler.HandleAsync(request.Value.ToCommand(serviceName), cancellationToken);
        if (result.IsSuccess)
        {
            return Results.Accepted(uri: null, value: new { id = result.Value });
        }

        return result.Errors.Contains(IngestionErrors.QueueUnavailable)
            ? ProblemResponses.RetryLater(context, IngestionErrors.QueueUnavailable)
            : ProblemResponses.Validation(result.Errors);
    }

    /// <summary>
    /// Gövdeyi en fazla <paramref name="maxBytes"/> bayt okur; aşılırsa <c>null</c>. Sınır sunucu
    /// ayarına (Kestrel/TestServer) bırakılmaz, burada açıkça uygulanır (plan 0001, değişiklik 8).
    /// </summary>
    private static async Task<byte[]?> ReadBodyAsync(HttpRequest request, int maxBytes, CancellationToken cancellationToken)
    {
        if (request.ContentLength > maxBytes)
        {
            return null;
        }

        var buffer = new ArrayBufferWriter<byte>();
        var chunk = new byte[16 * 1024];
        int read;
        while ((read = await request.Body.ReadAsync(chunk, cancellationToken)) > 0)
        {
            if (buffer.WrittenCount + read > maxBytes)
            {
                return null;
            }

            buffer.Write(chunk.AsSpan(0, read));
        }

        return buffer.WrittenSpan.ToArray();
    }
}
