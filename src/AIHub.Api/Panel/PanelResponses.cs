using System.Text.Json;
using AIHub.Application.Dashboard;
using AIHub.Application.Logs;
using AIHub.Domain.Logs;

namespace AIHub.Api.Panel;

/// <summary>Panel okuma yüzeyinin dışa açık sözleşmesi. Zamanlar UTC'dir; yerel saate çevirmek panelin işidir.</summary>
internal sealed record LogPageResponse(
    IReadOnlyList<LogSummaryResponse> Items,
    string? Older,
    string? Newer,
    DateTimeOffset From,
    DateTimeOffset To)
{
    public static LogPageResponse Create(LogPage page) => new(
        page.Items.Select(LogSummaryResponse.From).ToArray(),
        page.Older is null ? null : PanelQueryParser.EncodeCursor(page.Older),
        page.Newer is null ? null : PanelQueryParser.EncodeCursor(page.Newer),
        page.From.ToUniversalTime(),
        page.To.ToUniversalTime());
}

internal sealed record LogSummaryResponse(
    Guid Id,
    DateTimeOffset ReceivedAt,
    string ServiceName,
    string Level,
    string MessagePreview,
    bool MessageTruncated,
    string? ExceptionType)
{
    public static LogSummaryResponse From(LogSummary s) =>
        new(s.Id, s.ReceivedAt.ToUniversalTime(), s.ServiceName, s.Level.ToString(), s.MessagePreview, s.MessageTruncated, s.ExceptionType);
}

internal sealed record LogDetailResponse(
    Guid Id,
    string ServiceName,
    string Level,
    string Message,
    string? ExceptionType,
    string? StackTrace,
    JsonElement? Metadata,
    DateTimeOffset? OccurredAt,
    DateTimeOffset ReceivedAt)
{
    public static LogDetailResponse From(LogEntry e) => new(
        e.Id,
        e.ServiceName,
        e.Level.ToString(),
        e.Message,
        e.ExceptionType,
        e.StackTrace,
        // JsonElement.Parse (.NET 10) belgeyi kendisi yönetir; kiralanan tamponlar havuza döner (review 1, F-12).
        e.Metadata is null ? null : JsonElement.Parse(e.Metadata),
        e.OccurredAt?.ToUniversalTime(),
        e.ReceivedAt.ToUniversalTime());
}

internal sealed record ServiceListResponse(IReadOnlyList<string> Services);

/// <summary>Dashboard'un log bölümleri (Spec 0006). Pencere UTC'dir; derece adları metin olarak döner.</summary>
internal sealed record DashboardResponse(
    DateTimeOffset From,
    DateTimeOffset To,
    IReadOnlyList<LevelCountResponse> Levels,
    IReadOnlyList<ServiceErrorCountResponse> TopErrorServices,
    IReadOnlyList<ExceptionTypeCountResponse> TopExceptionTypes)
{
    public static DashboardResponse Create(DashboardSummary s) => new(
        s.From.ToUniversalTime(),
        s.To.ToUniversalTime(),
        s.Levels.Select(l => new LevelCountResponse(l.Level.ToString(), l.Count)).ToArray(),
        s.TopErrorServices.Select(e => new ServiceErrorCountResponse(e.ServiceName, e.Count)).ToArray(),
        s.TopExceptionTypes.Select(t => new ExceptionTypeCountResponse(t.ExceptionType, t.Count, t.LastSeenAt.ToUniversalTime())).ToArray());
}

internal sealed record LevelCountResponse(string Level, long Count);

internal sealed record ServiceErrorCountResponse(string ServiceName, long Count);

internal sealed record ExceptionTypeCountResponse(string ExceptionType, long Count, DateTimeOffset LastSeenAt);
