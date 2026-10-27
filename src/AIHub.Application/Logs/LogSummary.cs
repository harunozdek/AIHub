using AIHub.Domain.Logs;

namespace AIHub.Application.Logs;

/// <summary>Liste satırı: Message'ın yalnızca ilk satırı, en fazla <see cref="PreviewMaxLength"/> karakter (Spec 0002, R-2).</summary>
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

    public static LogSummary From(LogEntry entry)
    {
        var (preview, truncated) = Preview(entry.Message);
        return new LogSummary(entry.Id, entry.ReceivedAt, entry.ServiceName, entry.Level, preview, truncated, entry.ExceptionType);
    }

    private static (string Preview, bool Truncated) Preview(string message)
    {
        var lineEnd = message.AsSpan().IndexOfAny('\r', '\n');
        var firstLine = lineEnd < 0 ? message : message[..lineEnd];
        var truncated = lineEnd >= 0 && !string.IsNullOrWhiteSpace(message[lineEnd..]);

        if (firstLine.Length > PreviewMaxLength)
        {
            // Bir surrogate çiftini ortadan bölmemek için kesim bir karakter geri çekilir.
            var cut = char.IsHighSurrogate(firstLine[PreviewMaxLength - 1]) ? PreviewMaxLength - 1 : PreviewMaxLength;
            return (firstLine[..cut], true);
        }

        return (firstLine, truncated);
    }
}
