using System.Security.Claims;
using System.Text.Encodings.Web;
using AIHub.Application.Abstractions;
using AIHub.Application.Ingestion;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;

namespace AIHub.Api.Auth;

/// <summary>
/// Servis API anahtarıyla kimlik doğrulama (docs/security.md). ServiceName anahtara bağlı değerden
/// alınır ve claim olarak taşınır; istek gövdesindeki ada hiç bakılmaz.
/// </summary>
internal sealed class ApiKeyAuthenticationHandler(
    IOptionsMonitor<AuthenticationSchemeOptions> options,
    ILoggerFactory logger,
    UrlEncoder encoder,
    IApiKeyStore apiKeyStore,
    IProblemDetailsService problemDetails)
    : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    public const string SchemeName = "ApiKey";
    public const string HeaderName = "X-Api-Key";
    public const string ServiceNameClaim = "service_name";

    private bool _keyStoreUnavailable;

    protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        var rawKey = Request.Headers[HeaderName].ToString();
        if (string.IsNullOrWhiteSpace(rawKey))
        {
            return AuthenticateResult.NoResult();
        }

        var lookup = await apiKeyStore.FindServiceNameAsync(rawKey, Context.RequestAborted);
        if (!lookup.IsSuccess)
        {
            _keyStoreUnavailable = true;
            return AuthenticateResult.Fail("Anahtar deposu erişilemez.");
        }

        if (lookup.Value is not { } serviceName)
        {
            return AuthenticateResult.Fail("Geçersiz veya iptal edilmiş API anahtarı.");
        }

        var identity = new ClaimsIdentity([new Claim(ServiceNameClaim, serviceName)], SchemeName);
        return AuthenticateResult.Success(new AuthenticationTicket(new ClaimsPrincipal(identity), SchemeName));
    }

    protected override async Task HandleChallengeAsync(AuthenticationProperties properties)
    {
        // Anahtar deposu erişilemezse istemci "yetkisiz" değil "sonra yeniden dene" almalı (Spec 0001, R-8).
        if (_keyStoreUnavailable)
        {
            Response.StatusCode = StatusCodes.Status503ServiceUnavailable;
            Response.Headers.RetryAfter = ProblemResponses.RetryAfterSeconds;
            await problemDetails.WriteAsync(new ProblemDetailsContext
            {
                HttpContext = Context,
                ProblemDetails = { Status = StatusCodes.Status503ServiceUnavailable, Title = IngestionErrors.KeyStoreUnavailable.Message },
            });
            return;
        }

        Response.StatusCode = StatusCodes.Status401Unauthorized;
        await problemDetails.WriteAsync(new ProblemDetailsContext
        {
            HttpContext = Context,
            ProblemDetails = { Status = StatusCodes.Status401Unauthorized, Title = "Geçerli bir API anahtarı gerekli." },
        });
    }
}
