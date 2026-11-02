using AIHub.Domain.Common;

namespace AIHub.Domain.Users;

/// <summary>Şifre kuralı: 12–128 karakter (Spec 0004, R-5). Kuralı karşılamayan şifre nedeniyle reddedilir.</summary>
public static class PasswordPolicy
{
    public static Error? Validate(string? password, string field = "password")
    {
        if (password is null || password.Length < UserLimits.PasswordMinLength || password.Length > UserLimits.PasswordMaxLength)
        {
            return new Error(
                "password.length",
                $"Şifre {UserLimits.PasswordMinLength}–{UserLimits.PasswordMaxLength} karakter olmalıdır.",
                field);
        }

        return null;
    }
}
