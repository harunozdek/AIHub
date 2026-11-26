using AIHub.Domain.Logs;

namespace AIHub.Application.Logs;

/// <summary>
/// Liste satırı: Message'ın yalnızca ilk satırı, en fazla <see cref="PreviewMaxLength"/> karakter (Spec 0002, R-2).
/// Dashboard'un son kayıt satırları aynı kuralı daha kısa bir uzunlukla kullanır (Spec 0011, CB-8; plan 0011, risk 6).
/// </summary>
public sealed record LogSummary(
    Guid Id,
    DateTimeOffset ReceivedAt,
    string ServiceName,
    LogLevel Level,
    string MessagePreview,
    bool MessageTruncated,
    string? ExceptionType)
{
    public const int PreviewMaxLength = 200;

    public static LogSummary From(LogEntry entry) => From(entry, PreviewMaxLength);

    public static LogSummary From(LogEntry entry, int previewMaxLength)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(previewMaxLength, 2);
        var (preview, truncated) = Preview(entry.Message, previewMaxLength);
        return new LogSummary(entry.Id, entry.ReceivedAt, entry.ServiceName, entry.Level, preview, truncated, entry.ExceptionType);
    }

    private static (string Preview, bool Truncated) Preview(string message, int maxLength)
    {
        var lineEnd = message.AsSpan().IndexOfAny('\r', '\n');
        var firstLine = lineEnd < 0 ? message : message[..lineEnd];
        var truncated = lineEnd >= 0 && !string.IsNullOrWhiteSpace(message[lineEnd..]);

        if (firstLine.Length > maxLength)
        {
            // Bir surrogate çiftini ortadan bölmemek için kesim bir karakter geri çekilir.
            var cut = char.IsHighSurrogate(firstLine[maxLength - 1]) ? maxLength - 1 : maxLength;
            return (firstLine[..cut], true);
        }

        return (firstLine, truncated);
    }
}
