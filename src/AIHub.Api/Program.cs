using System.Data.Common;
using AIHub.Api;
using AIHub.Api.Auth;
using AIHub.Api.Errors;
using AIHub.Api.Ingestion;
using AIHub.Api.Panel;
using AIHub.Application;
using AIHub.Application.Abstractions;
using AIHub.Application.Logs;
using AIHub.Application.Users;
using AIHub.Infrastructure;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.IdentityModel.Tokens;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<GlobalExceptionHandler>();
builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);

// İmza anahtarı sırdır; eksik veya kısaysa uygulama açılışta durur (Spec 0004, AC-6; docs/security.md).
builder.Services.Configure<PanelTokenOptions>(builder.Configuration.GetSection(PanelTokenOptions.SectionName));
var panelSigningKey = PanelTokenOptions.KeyFrom(builder.Configuration[$"{PanelTokenOptions.SectionName}:TokenSigningKey"]);
builder.Services.AddSingleton<IPanelTokenIssuer, PanelTokenIssuer>();

const string StoreUnavailableItem = "panel.storeUnavailable";

builder.Services
    .AddAuthentication(ApiKeyAuthenticationHandler.SchemeName)
    .AddScheme<AuthenticationSchemeOptions, ApiKeyAuthenticationHandler>(ApiKeyAuthenticationHandler.SchemeName, null)
    // Panel kullanıcısı: AIHub'ın kendi imzaladığı oturum token'ı (ADR 0002). Süre girişten itibaren mutlak;
    // saat kayması payı yok (R-10). Her istekte kullanıcının aktifliği ve damgası denetlenir (R-8, R-9).
    .AddJwtBearer(AuthSchemes.Panel, options =>
    {
        options.MapInboundClaims = false;
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidIssuer = PanelTokenOptions.Issuer,
            ValidAudience = PanelTokenOptions.Audience,
            IssuerSigningKey = panelSigningKey,
            ValidAlgorithms = [SecurityAlgorithms.HmacSha256],
            ClockSkew = TimeSpan.Zero,
            NameClaimType = "name",
        };
        options.Events = new JwtBearerEvents
        {
            OnTokenValidated = async context =>
            {
                var principal = context.Principal!;
                if (!Guid.TryParse(principal.FindFirst("sub")?.Value, out var userId)
                    || !Guid.TryParse(principal.FindFirst(PanelTokenOptions.StampClaim)?.Value, out var stamp))
                {
                    context.Fail("Token kullanıcı kimliği veya damga taşımıyor.");
                    return;
                }

                try
                {
                    var validator = context.HttpContext.RequestServices.GetRequiredService<PanelSessionValidator>();
                    if (!await validator.IsValidAsync(userId, stamp, context.HttpContext.RequestAborted))
                    {
                        context.Fail("Oturum artık geçerli değil.");
                    }
                }
                catch (Exception ex) when (ex is DbException or TimeoutException || ex.InnerException is DbException)
                {
                    // Depo erişilemiyor: oturum geçersiz sayılmaz, istek "yeniden dene" alır (0002, AC-9).
                    context.HttpContext.Items[StoreUnavailableItem] = true;
                    context.Fail("Oturum doğrulanamadı: depo erişilemiyor.");
                }
            },
            OnChallenge = async context =>
            {
                context.HandleResponse();
                var problems = context.HttpContext.RequestServices.GetRequiredService<IProblemDetailsService>();
                if (context.HttpContext.Items.ContainsKey(StoreUnavailableItem))
                {
                    context.Response.StatusCode = StatusCodes.Status503ServiceUnavailable;
                    context.Response.Headers.RetryAfter = ProblemResponses.RetryAfterSeconds;
                    await problems.WriteAsync(new ProblemDetailsContext
                    {
                        HttpContext = context.HttpContext,
                        ProblemDetails = { Status = StatusCodes.Status503ServiceUnavailable, Title = LogQueryErrors.StoreUnavailable.Message },
                    });
                    return;
                }

                context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                await problems.WriteAsync(new ProblemDetailsContext
                {
                    HttpContext = context.HttpContext,
                    ProblemDetails = { Status = StatusCodes.Status401Unauthorized, Title = AuthSchemes.PanelUnauthenticated.Message },
                });
            },
        };
    });
// Her yüzey yalnızca kendi şemasıyla doğrulanır. Varsayılan ret: kimliği doğrulanmamış istek hiçbir
// uç noktaya erişmez (docs/security.md).
builder.Services.AddAuthorizationBuilder()
    .AddPolicy(AuthSchemes.IngestionPolicy, p => p.AddAuthenticationSchemes(ApiKeyAuthenticationHandler.SchemeName).RequireAuthenticatedUser())
    .AddPolicy(AuthSchemes.PanelPolicy, p => p.AddAuthenticationSchemes(AuthSchemes.Panel).RequireAuthenticatedUser())
    .SetFallbackPolicy(new AuthorizationPolicyBuilder().RequireAuthenticatedUser().Build());

var app = builder.Build();

app.UseExceptionHandler();
app.UseAuthentication();
app.UseAuthorization();

if (app.Configuration.GetValue<bool>("Database:MigrateOnStartup"))
{
    await app.Services.MigrateDatabaseAsync();
}

await BootstrapAdmin.EnsureAsync(app.Services, app.Configuration, app.Logger);

app.MapLogsEndpoints();
app.MapPanelEndpoints();
app.MapAuthEndpoints();

app.Run();
