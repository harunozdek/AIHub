namespace AIHub.Domain.Logs;

/// <summary>Spec 0001, Constraints: boyut sınırları.</summary>
public static class LogEntryLimits
{
    public const int MessageMaxLength = 32_768;

    public const int ExceptionTypeMaxLength = 512;

    /// <summary>Tek bir gönderimin gövdesi, bayt cinsinden (256 KB).</summary>
    public const int RequestBodyMaxBytes = 256 * 1024;
}
