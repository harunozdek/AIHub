namespace AIHub.Domain.Logs;

/// <summary>Kaydın önem derecesi. Yalnızca dört değer vardır; Debug/Trace kabul edilmez (docs/domain.md).</summary>
public enum LogLevel
{
    Info,
    Warning,
    Error,
    Critical,
}

public static class LogLevels
{
    /// <summary>
    /// Metni dört tanımlı değerden birine çevirir. Büyük/küçük harf duyarsızdır; sayısal metinler
    /// ("1") ve tanımsız adlar reddedilir — <see cref="Enum.TryParse{TEnum}(string, out TEnum)"/> ikisini de kabul ederdi.
    /// </summary>
    public static bool TryParse(string? text, out LogLevel level)
    {
        foreach (var candidate in Enum.GetValues<LogLevel>())
        {
            if (string.Equals(candidate.ToString(), text, StringComparison.OrdinalIgnoreCase))
            {
                level = candidate;
                return true;
            }
        }

        level = default;
        return false;
    }
}
