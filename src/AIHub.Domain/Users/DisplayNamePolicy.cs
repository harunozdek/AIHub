using AIHub.Domain.Common;

namespace AIHub.Domain.Users;

/// <summary>Görünen ad kuralı: boş olamaz, en fazla <see cref="UserLimits.DisplayNameMaxLength"/> karakter (Spec 0004, R-7). Tek kaynak (review 1, F-08).</summary>
public static class DisplayNamePolicy
{
    public static Error? Validate(string? displayName)
    {
        if (string.IsNullOrWhiteSpace(displayName) || displayName.Length > UserLimits.DisplayNameMaxLength)
        {
            return new Error(
                "displayName.length",
                $"Görünen ad boş olamaz ve en fazla {UserLimits.DisplayNameMaxLength} karakter olabilir.",
                "displayName");
        }

        return null;
    }
}
