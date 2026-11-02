using AIHub.Domain.Common;

namespace AIHub.Domain.Monitoring;

/// <summary>
/// Sağlık adresi: mutlak <c>http</c>/<c>https</c>, en fazla 2048 karakter, kullanıcı bilgisi yok
/// (Spec 0005, R-3). Adresin çözüldüğü IP'nin engelli olup olmadığı ayrıca denetlenir
/// (<see cref="HealthAddressPolicy"/>).
/// </summary>
public sealed record HealthUrl
{
    private const string Field = "healthUrl";

    private HealthUrl(Uri uri)
    {
        Uri = uri;
        Value = uri.OriginalString;
    }

    public string Value { get; }

    public Uri Uri { get; }

    public static Result<HealthUrl> Create(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return Fail("healthUrl.required", "Sağlık adresi boş olamaz.");
        }

        if (value.Length > MonitoringLimits.HealthUrlMaxLength)
        {
            return Fail("healthUrl.length", $"Sağlık adresi en fazla {MonitoringLimits.HealthUrlMaxLength} karakter olabilir.");
        }

        if (!Uri.TryCreate(value, UriKind.Absolute, out var uri)
            || (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps)
            || string.IsNullOrEmpty(uri.Host))
        {
            return Fail("healthUrl.scheme", "Sağlık adresi http:// ya da https:// ile başlayan tam bir adres olmalıdır.");
        }

        if (!string.IsNullOrEmpty(uri.UserInfo))
        {
            return Fail("healthUrl.credentials", "Sağlık adresi kullanıcı adı veya şifre içeremez.");
        }

        return Result<HealthUrl>.Success(new HealthUrl(uri));
    }

    private static Result<HealthUrl> Fail(string code, string message) =>
        Result<HealthUrl>.Failure(new Error(code, message, Field));
}
