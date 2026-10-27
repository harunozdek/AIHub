using Microsoft.AspNetCore.Diagnostics;

namespace AIHub.Api.Errors;

/// <summary>
/// Beklenmeyen her hatayı ayrıntısız bir 500 ProblemDetails'a çevirir: exception mesajı, yığın izi
/// veya bağlantı dizesi yanıta asla girmez (docs/security.md, Input &amp; output). Ayrıntı yalnızca
/// süreç loguna yazılır — AIHub'ın kendi ingestion hattına değil (docs/conventions.md).
/// </summary>
internal sealed class GlobalExceptionHandler(IProblemDetailsService problemDetails, ILogger<GlobalExceptionHandler> logger)
    : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        logger.LogError(exception, "Beklenmeyen hata: {Method} {Path}", httpContext.Request.Method, httpContext.Request.Path);

        httpContext.Response.StatusCode = StatusCodes.Status500InternalServerError;
        return await problemDetails.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = httpContext,
            Exception = null,
            ProblemDetails = { Status = StatusCodes.Status500InternalServerError, Title = "Beklenmeyen bir hata oluştu." },
        });
    }
}
