using System.Text.RegularExpressions;

namespace AIHub.Domain.Masking;

/// <summary>
/// Düz metindeki hassas veriyi türünü belirten yer tutucuyla değiştirir (BR-3). Metnin geri kalanı
/// değişmez. Desenler NonBacktracking ile derlenir: 256 KB'lık girdide doğrusal süre, felaket geri
/// izleme yok (plan 0001, R-4). Sıra önemlidir: önce yapısal sırlar (URI parolası, Bearer), sonra
/// genel desenler — böylece bir sır iki kez, yanlış türle maskelenmez.
/// </summary>
public static class SensitiveDataMasker
{
    private const RegexOptions Options =
        RegexOptions.NonBacktracking | RegexOptions.CultureInvariant | RegexOptions.ExplicitCapture;

    private static readonly (Regex Pattern, MaskKind Kind)[] Rules =
    [
        // postgres://kullanici:parola@host — host ve kullanıcı korunur, yalnızca parola maskelenir.
        (new Regex(@"(?<pre>[A-Za-z][A-Za-z0-9+.\-]*://[^\s:/@]+:)(?<secret>[^\s@/]+)(?<post>@)", Options), MaskKind.Password),
        // Password=...; / Pwd=... — bağlantı dizesinin geri kalanı (Host, Database) korunur.
        (new Regex(@"(?<pre>\b(password|pwd)\s*=\s*)(?<secret>[^;\s'""]+)", Options | RegexOptions.IgnoreCase), MaskKind.Password),
        (new Regex(@"(?<pre>\bbearer\s+)(?<secret>[A-Za-z0-9\-._~+/]+=*)", Options | RegexOptions.IgnoreCase), MaskKind.BearerToken),
        // Başında Bearer olmayan JWT (header.payload.signature).
        (new Regex(@"(?<secret>\beyJ[A-Za-z0-9_\-]+\.[A-Za-z0-9_\-]+\.[A-Za-z0-9_\-]*)", Options), MaskKind.BearerToken),
        // AIHub'ın kendi anahtar biçimi.
        (new Regex(@"(?<secret>\baih_[A-Za-z0-9_\-]{16,})", Options), MaskKind.ApiKey),
        (new Regex(@"(?<pre>\b(x-api-key|api[_\-]?key|client_secret|secret_key|access_key)[""']?\s*[:=]\s*[""']?)(?<secret>[^\s""',;&]+)", Options | RegexOptions.IgnoreCase), MaskKind.ApiKey),
        (new Regex(@"(?<secret>[A-Za-z0-9._%+\-]+@[A-Za-z0-9.\-]+\.[A-Za-z]{2,})", Options), MaskKind.Email),
    ];

    private static readonly Regex TcknCandidate = new(@"\b[1-9][0-9]{10}\b", Options);

    /// <summary>Metadata anahtar adı bunlardan biriyse değerin tamamı maskelenir (plan 0001, Q-3).</summary>
    private static readonly Dictionary<string, MaskKind> SensitiveKeyNames = new(StringComparer.OrdinalIgnoreCase)
    {
        ["password"] = MaskKind.Password,
        ["pwd"] = MaskKind.Password,
        ["api_key"] = MaskKind.ApiKey,
        ["apikey"] = MaskKind.ApiKey,
        ["api-key"] = MaskKind.ApiKey,
        ["x-api-key"] = MaskKind.ApiKey,
        ["client_secret"] = MaskKind.ApiKey,
        ["secret_key"] = MaskKind.ApiKey,
        ["access_key"] = MaskKind.ApiKey,
        ["authorization"] = MaskKind.BearerToken,
    };

    public static string Mask(string input)
    {
        if (string.IsNullOrEmpty(input))
        {
            return input;
        }

        var result = input;
        foreach (var (pattern, kind) in Rules)
        {
            var placeholder = MaskKinds.Placeholder(kind);
            result = pattern.Replace(result, m => m.Groups["pre"].Value + placeholder + m.Groups["post"].Value);
        }

        return TcknCandidate.Replace(result, m => TcknValidator.IsValid(m.ValueSpan) ? MaskKinds.Placeholder(MaskKind.Tckn) : m.Value);
    }

    /// <summary>Anahtar adı hassassa, değerin yerine konacak yer tutucuyu verir.</summary>
    public static bool TryGetPlaceholderForKey(string key, out string placeholder)
    {
        if (SensitiveKeyNames.TryGetValue(key, out var kind))
        {
            placeholder = MaskKinds.Placeholder(kind);
            return true;
        }

        placeholder = "";
        return false;
    }
}
