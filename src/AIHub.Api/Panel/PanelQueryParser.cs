using System.Buffers.Text;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using AIHub.Application.Logs;
using AIHub.Domain.Common;

namespace AIHub.Api.Panel;

/// <summary>
/// Panel sorgu dizesinin yalnızca BİÇİM doğrulaması: tarih ayrıştırma ve imleç çözme. Aralık sırası,
/// 31 gün sınırı ve LogLevel geçerliliği iş kuralıdır, Application'dadır (docs/architecture.md, FD-4).
/// </summary>
internal static partial class PanelQueryParser
{
    public static Result<LogListQuery> Parse(IQueryCollection query)
    {
        var errors = new List<Error>();

        var from = ReadTimestamp(query, "from", errors);
        var to = ReadTimestamp(query, "to", errors);

        LogCursor? cursor = null;
        if (query["cursor"].ToString() is { Length: > 0 } cursorText)
        {
            cursor = DecodeCursor(cursorText);
            if (cursor is null)
            {
                errors.Add(new Error("cursor.invalid", "Sayfa işareti geçersiz; listeyi baştan açın.", "cursor"));
            }
        }

        var levels = query["level"].Where(l => !string.IsNullOrWhiteSpace(l)).Select(l => l!.Trim()).ToArray();
        var service = query["service"].ToString();
        var exceptionType = query["exceptionType"].ToString();

        return errors.Count > 0
            ? Result<LogListQuery>.Failure(errors)
            : Result<LogListQuery>.Success(new LogListQuery(
                string.IsNullOrWhiteSpace(service) ? null : service, levels, from, to, cursor, exceptionType));
    }

    /// <summary>
    /// İmleç dışarıya opaktır: "&lt;yön&gt;:&lt;UTC ticks&gt;:&lt;id&gt;:&lt;pencere başı ticks&gt;:&lt;pencere sonu ticks&gt;", base64url.
    /// </summary>
    public static string EncodeCursor(LogCursor cursor)
    {
        var direction = cursor.Direction == LogPageDirection.Older ? 'o' : 'n';
        var text = string.Create(
            CultureInfo.InvariantCulture,
            $"{direction}:{cursor.ReceivedAt.UtcTicks}:{cursor.Id:N}:{cursor.From.UtcTicks}:{cursor.To.UtcTicks}");
        return Base64Url.EncodeToString(Encoding.ASCII.GetBytes(text));
    }

    private static DateTimeOffset? ReadTicks(string text) =>
        long.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out var ticks)
        && ticks >= DateTimeOffset.MinValue.UtcTicks && ticks <= DateTimeOffset.MaxValue.UtcTicks
            ? new DateTimeOffset(ticks, TimeSpan.Zero)
            : null;

    private static LogCursor? DecodeCursor(string encoded)
    {
        byte[] bytes;
        try
        {
            bytes = Base64Url.DecodeFromChars(encoded);
        }
        catch (FormatException)
        {
            return null;
        }

        var parts = Encoding.ASCII.GetString(bytes).Split(':');
        if (parts.Length != 5
            || parts[0] is not ("o" or "n")
            || ReadTicks(parts[1]) is not { } receivedAt
            || !Guid.TryParseExact(parts[2], "N", out var id)
            || ReadTicks(parts[3]) is not { } from
            || ReadTicks(parts[4]) is not { } to)
        {
            return null;
        }

        return new LogCursor(receivedAt, id, parts[0] == "o" ? LogPageDirection.Older : LogPageDirection.Newer, from, to);
    }

    private static DateTimeOffset? ReadTimestamp(IQueryCollection query, string name, List<Error> errors)
    {
        var text = query[name].ToString();
        if (text.Length == 0)
        {
            return null;
        }

        // Ingestion'daki kuralla aynı: saat dilimi zorunlu, yoksa zaman sessizce kayardı (Spec 0001, AC-5).
        if (ExplicitOffset().IsMatch(text)
            && DateTimeOffset.TryParse(text, CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsed))
        {
            // Sınırda tek temsile, UTC'ye çevrilir: Application hiçbir zaman ofsetli duvar saatiyle hesap yapmaz (review 4, R4-01).
            return parsed.ToUniversalTime();
        }

        errors.Add(new Error($"{name}.invalid", $"{name} saat dilimi içeren ISO 8601 biçiminde olmalıdır (ör. 2026-10-27T09:00:00Z).", name));
        return null;
    }

    [GeneratedRegex(@"^\d{4}-\d{2}-\d{2}T\d{2}:\d{2}(:\d{2}(\.\d+)?)?(Z|[+\-]\d{2}:\d{2})$", RegexOptions.CultureInvariant)]
    private static partial Regex ExplicitOffset();
}
