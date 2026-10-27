using AIHub.Domain.Common;

namespace AIHub.Api;

/// <summary>Dışa çıkan her hata RFC 9457 ProblemDetails'dır (docs/conventions.md, Error handling).</summary>
internal static class ProblemResponses
{
    public const string RetryAfterSeconds = "5";

    public static IResult Validation(IReadOnlyList<Error> errors) =>
        Results.ValidationProblem(
            errors.GroupBy(e => e.Field ?? "body").ToDictionary(g => g.Key, g => g.Select(e => e.Message).ToArray()),
            title: "Gönderim doğrulanamadı.");

    public static IResult PayloadTooLarge(int maxBytes) =>
        Results.Problem(
            statusCode: StatusCodes.Status413PayloadTooLarge,
            title: $"Gönderim en fazla {maxBytes} bayt olabilir.");

    public static IResult RetryLater(HttpContext context, Error error)
    {
        context.Response.Headers.RetryAfter = RetryAfterSeconds;
        return Results.Problem(statusCode: StatusCodes.Status503ServiceUnavailable, title: error.Message);
    }
}
