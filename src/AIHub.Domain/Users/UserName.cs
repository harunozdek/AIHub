using AIHub.Domain.Common;

namespace AIHub.Domain.Users;

/// <summary>
/// Kullanıcı adı: 3–64 karakter; harf, rakam, <c>.</c>, <c>_</c>, <c>-</c>. Büyük-küçük harf duyarsız
/// benzersizdir: karşılaştırma ve tekillik <see cref="Normalized"/> üzerinden yapılır (Spec 0004, R-1, R-7).
/// </summary>
public sealed record UserName
{
    private UserName(string value)
    {
        Value = value;
        Normalized = Normalize(value);
    }

    /// <summary>Kullanıcının yazdığı biçim; listede böyle görünür.</summary>
    public string Value { get; }

    public string Normalized { get; }

    public static string Normalize(string value) => value.ToLowerInvariant();

    public static Result<UserName> Create(string? value)
    {
        if (string.IsNullOrEmpty(value)
            || value.Length < UserLimits.UserNameMinLength
            || value.Length > UserLimits.UserNameMaxLength)
        {
            return Result<UserName>.Failure(new Error(
                "userName.length",
                $"Kullanıcı adı {UserLimits.UserNameMinLength}–{UserLimits.UserNameMaxLength} karakter olmalıdır.",
                "userName"));
        }

        // Yalnızca ASCII: Türkçe "İ"/"ı" gibi harflerin küçük harfe çevrimi kültüre göre değişir ve
        // iki farklı ad aynı normalize değere düşebilir.
        if (!value.All(c => char.IsAsciiLetterOrDigit(c) || c is '.' or '_' or '-'))
        {
            return Result<UserName>.Failure(new Error(
                "userName.characters",
                "Kullanıcı adı yalnızca harf (a–z), rakam, nokta, alt çizgi ve tire içerebilir.",
                "userName"));
        }

        return Result<UserName>.Success(new UserName(value));
    }
}
