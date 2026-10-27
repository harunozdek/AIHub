using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using AIHub.Application.Ingestion;
using AIHub.Domain.Common;
using AIHub.Domain.Logs;
using LogLevel = AIHub.Domain.Logs.LogLevel;

namespace AIHub.Api.Ingestion;

/// <summary>
/// Gövdenin sınırda doğrulanması (docs/security.md, Input &amp; output): tanımsız LogLevel, boş
/// Message, nesne olmayan Metadata ve bozuk/saat dilimsiz Timestamp Application'a hiç ulaşmaz.
/// Gövdedeki "serviceName" alanı okunmaz — ServiceName kimlik doğrulamasından gelir (AC-3).
/// </summary>
internal sealed partial record LogRequest(
    LogLevel Level,
    string Message,
    string? ExceptionType,
    string? StackTrace,
    JsonObject? Metadata,
    DateTimeOffset? OccurredAt)
{
    public IngestLogCommand ToCommand(string authenticatedServiceName) =>
        new(authenticatedServiceName, Level, Message, ExceptionType, StackTrace, Metadata, OccurredAt);

    public static Result<LogRequest> Parse(ReadOnlySpan<byte> body)
    {
        JsonNode? root;
        try
        {
            root = JsonNode.Parse(body);
        }
        catch (JsonException)
        {
            return Fail("body", "Gövde geçerli bir JSON değil.");
        }

        if (root is not JsonObject obj)
        {
            return Fail("body", "Gövde bir JSON nesnesi olmalıdır.");
        }

        var errors = new List<Error>();

        var levelText = ReadString(obj, "level", errors);
        var level = LogLevel.Info;
        if (levelText is null || !LogLevels.TryParse(levelText, out level))
        {
            errors.Add(new Error("level.invalid", "level Info, Warning, Error veya Critical olmalıdır.", "level"));
        }

        var message = ReadString(obj, "message", errors);
        if (string.IsNullOrWhiteSpace(message))
        {
            errors.Add(new Error("message.required", "message boş olamaz.", "message"));
        }
        else if (message.Length > LogEntryLimits.MessageMaxLength)
        {
            errors.Add(new Error("message.tooLong", $"message en fazla {LogEntryLimits.MessageMaxLength} karakter olabilir.", "message"));
        }

        var exceptionType = ReadString(obj, "exceptionType", errors);
        if (exceptionType is not null && exceptionType.Length > LogEntryLimits.ExceptionTypeMaxLength)
        {
            errors.Add(new Error("exceptionType.tooLong", $"exceptionType en fazla {LogEntryLimits.ExceptionTypeMaxLength} karakter olabilir.", "exceptionType"));
        }

        var stackTrace = ReadString(obj, "stackTrace", errors);
        var occurredAt = ReadTimestamp(obj, errors);

        JsonObject? metadata = null;
        if (obj["metadata"] is { } metadataNode)
        {
            if (metadataNode is JsonObject metadataObject)
            {
                metadata = metadataObject;
                if (ContainsNul(metadataObject))
                {
                    errors.Add(NulError("metadata"));
                }
            }
            else
            {
                errors.Add(new Error("metadata.notObject", "metadata bir JSON nesnesi olmalıdır.", "metadata"));
            }
        }

        return errors.Count > 0
            ? Result<LogRequest>.Failure(errors)
            : Result<LogRequest>.Success(new LogRequest(level, message!, exceptionType, stackTrace, metadata, occurredAt));
    }

    private static string? ReadString(JsonObject obj, string name, List<Error> errors)
    {
        var node = obj[name];
        if (node is null)
        {
            return null;
        }

        if (node is JsonValue value && value.GetValueKind() == JsonValueKind.String)
        {
            var text = value.GetValue<string>();
            if (text.Contains('\0'))
            {
                errors.Add(NulError(name));
            }

            return text;
        }

        errors.Add(new Error($"{name}.notString", $"{name} metin olmalıdır.", name));
        return null;
    }

    // PostgreSQL text/jsonb NUL karakterini saklayamaz; kabul edilseydi kayıt hiçbir zaman yazılamazdı
    // (review F-1). Metadata'da hem anahtarlara hem string değerlere bakılır.
    private static Error NulError(string field) =>
        new($"{field}.nulCharacter", $"{field} NUL karakteri (\\u0000) içeremez.", field);

    private static bool ContainsNul(JsonNode? node) => node switch
    {
        JsonObject obj => obj.Any(p => p.Key.Contains('\0') || ContainsNul(p.Value)),
        JsonArray array => array.Any(ContainsNul),
        JsonValue value when value.GetValueKind() == JsonValueKind.String => value.GetValue<string>().Contains('\0'),
        _ => false,
    };

    private static DateTimeOffset? ReadTimestamp(JsonObject obj, List<Error> errors)
    {
        if (obj["timestamp"] is null)
        {
            return null;
        }

        var text = ReadString(obj, "timestamp", errors);
        // Saat dilimi zorunludur: dilimsiz zamanı UTC saymak, örn. Türkiye saatini sessizce 3 saat
        // kaydırırdı (Spec 0001, AC-5; plan 0001, Q-1).
        if (text is not null
            && ExplicitOffset().IsMatch(text)
            && DateTimeOffset.TryParse(text, CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsed))
        {
            return parsed;
        }

        if (text is not null)
        {
            errors.Add(new Error("timestamp.invalid", "timestamp saat dilimi içeren ISO 8601 biçiminde olmalıdır (ör. 2026-10-27T09:00:00Z).", "timestamp"));
        }

        return null;
    }

    private static Result<LogRequest> Fail(string field, string message) =>
        Result<LogRequest>.Failure(new Error($"{field}.invalid", message, field));

    [GeneratedRegex(@"^\d{4}-\d{2}-\d{2}T\d{2}:\d{2}(:\d{2}(\.\d+)?)?(Z|[+\-]\d{2}:\d{2})$", RegexOptions.CultureInvariant)]
    private static partial Regex ExplicitOffset();
}
