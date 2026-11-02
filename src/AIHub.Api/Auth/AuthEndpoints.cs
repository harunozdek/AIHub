using System.Security.Claims;
using AIHub.Application.Users;

namespace AIHub.Api.Auth;

/// <summary>
/// Yerel giriş ve kullanıcı yönetimi (Spec 0004, ADR 0002). Giriş anonimdir; geri kalan her uç panel
/// oturumu ister. Yanıtlar önbelleğe alınmaz.
/// </summary>
internal static class AuthEndpoints
{
    public sealed record LoginRequest(string? UserName, string? Password);

    public sealed record CreateUserRequest(string? UserName, string? DisplayName, string? Password);

    public sealed record ChangePasswordRequest(string? CurrentPassword, string? NewPassword);

    public static IEndpointRouteBuilder MapAuthEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapPost("/api/auth/login", LoginAsync).AllowAnonymous().AddEndpointFilter(NoStore);

        var panel = app.MapGroup("/api")
            .RequireAuthorization(AuthSchemes.PanelPolicy)
            .AddEndpointFilter(NoStore);
        panel.MapGet("/users", ListAsync);
        panel.MapPost("/users", CreateAsync);
        panel.MapPost("/users/{id:guid}/deactivate", (Guid id, ClaimsPrincipal user, SetUserActiveHandler handler, CancellationToken ct) => SetActiveAsync(id, false, user, handler, ct));
        panel.MapPost("/users/{id:guid}/activate", (Guid id, ClaimsPrincipal user, SetUserActiveHandler handler, CancellationToken ct) => SetActiveAsync(id, true, user, handler, ct));
        panel.MapPost("/me/password", ChangePasswordAsync);
        return app;
    }

    private static async ValueTask<object?> NoStore(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        context.HttpContext.Response.Headers.CacheControl = "no-store";
        return await next(context);
    }

    /// <summary>Her başarısızlık aynı durum ve aynı gövdeyi alır (Spec 0004, R-2, AC-3). Gövde loglanmaz.</summary>
    private static async Task<IResult> LoginAsync(LoginRequest? request, LoginHandler handler, CancellationToken cancellationToken)
    {
        var result = await handler.HandleAsync(new LoginCommand(request?.UserName, request?.Password), cancellationToken);
        if (!result.IsSuccess)
        {
            return Results.Problem(statusCode: StatusCodes.Status401Unauthorized, title: UserErrors.InvalidCredentials.Message);
        }

        var login = result.Value;
        return Results.Ok(new { token = login.Token.Token, expiresAt = login.Token.ExpiresAt, displayName = login.DisplayName, userId = login.UserId });
    }

    private static async Task<IResult> ListAsync(ListUsersHandler handler, CancellationToken cancellationToken) =>
        Results.Ok(await handler.HandleAsync(cancellationToken));

    private static async Task<IResult> CreateAsync(CreateUserRequest? request, ClaimsPrincipal user, CreateUserHandler handler, CancellationToken cancellationToken)
    {
        var result = await handler.HandleAsync(
            new CreateUserCommand(request?.UserName, request?.DisplayName, request?.Password, UserIdOf(user)),
            cancellationToken);
        return result.IsSuccess
            ? Results.Created($"/api/users/{result.Value}", new { id = result.Value })
            : ProblemResponses.Validation(result.Errors, "Kullanıcı eklenemedi.");
    }

    private static async Task<IResult> SetActiveAsync(Guid id, bool active, ClaimsPrincipal user, SetUserActiveHandler handler, CancellationToken cancellationToken)
    {
        var result = await handler.HandleAsync(new SetUserActiveCommand(id, active, UserIdOf(user)), cancellationToken);
        if (result.IsSuccess)
        {
            return Results.NoContent();
        }

        return result.Errors.Contains(UserErrors.NotFound)
            ? ProblemResponses.NotFound(UserErrors.NotFound.Message)
            : Results.Problem(statusCode: StatusCodes.Status409Conflict, title: result.Errors[0].Message);
    }

    /// <summary>Damga yenilenir; bu oturumun yeni token'ı döner, diğer oturumlar düşer (Spec 0004, R-9).</summary>
    private static async Task<IResult> ChangePasswordAsync(ChangePasswordRequest? request, ClaimsPrincipal user, ChangePasswordHandler handler, CancellationToken cancellationToken)
    {
        var result = await handler.HandleAsync(
            new ChangePasswordCommand(UserIdOf(user), request?.CurrentPassword, request?.NewPassword),
            cancellationToken);
        return result.IsSuccess
            ? Results.Ok(new { token = result.Value.Token, expiresAt = result.Value.ExpiresAt })
            : ProblemResponses.Validation(result.Errors, "Şifre değiştirilemedi.");
    }

    /// <summary>Panel politikası geçildiyse kimlik oturum doğrulamasında zaten denetlenmiştir.</summary>
    internal static Guid UserIdOf(ClaimsPrincipal user) => Guid.Parse(user.FindFirstValue("sub")!);
}
