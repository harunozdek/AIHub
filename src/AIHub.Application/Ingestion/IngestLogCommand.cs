using System.Text.Json.Nodes;
using AIHub.Domain.Logs;

namespace AIHub.Application.Ingestion;

/// <summary>
/// Api sınırında biçimi doğrulanmış bir gönderim. ServiceName gövdeden değil, kimliği doğrulanmış
/// API anahtarından gelir; gövdedeki ad bu komuta hiç taşınmaz (docs/security.md, AuthN/AuthZ).
/// </summary>
public sealed record IngestLogCommand(
    string AuthenticatedServiceName,
    LogLevel Level,
    string Message,
    string? ExceptionType,
    string? StackTrace,
    JsonObject? Metadata,
    DateTimeOffset? OccurredAt);
