using AIHub.Api.Auth;
using AIHub.Api.Errors;
using AIHub.Api.Ingestion;
using AIHub.Api.Panel;
using AIHub.Application;
using AIHub.Infrastructure;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<GlobalExceptionHandler>();
builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);

builder.Services
    .AddAuthentication(ApiKeyAuthenticationHandler.SchemeName)
    .AddScheme<AuthenticationSchemeOptions, ApiKeyAuthenticationHandler>(ApiKeyAuthenticationHandler.SchemeName, null)
    // Panel kullanıcısı: OIDC sağlayıcısının imzaladığı token. Authority ve audience konfigürasyondur, sır değil
    // (docs/security.md). Authority yoksa hiçbir token doğrulanamaz: panel yüzeyi kapalı kalır.
    .AddJwtBearer(AuthSchemes.Panel, options =>
    {
        options.Authority = builder.Configuration["Panel:Authority"];
        options.Audience = builder.Configuration["Panel:Audience"];
        options.RequireHttpsMetadata = !builder.Environment.IsDevelopment();
        options.MapInboundClaims = false;
        options.Events = new JwtBearerEvents
        {
            OnChallenge = async context =>
            {
                context.HandleResponse();
                context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                await context.HttpContext.RequestServices.GetRequiredService<IProblemDetailsService>().WriteAsync(new ProblemDetailsContext
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

app.MapLogsEndpoints();
app.MapPanelEndpoints();

app.Run();
