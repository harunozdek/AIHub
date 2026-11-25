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

/// <summary>
/// Dashboard'un log bölümleri (Spec 0006; Spec 0011). Pencere UTC'dir; derece adları metin olarak döner. Window hazır
/// pencerenin anahtarı ya da "custom"dır; Slices trend dilimleridir (eski adı "hourly", plan 0011 onaylı eski risk 5);
/// RecentErrors ve RecentWarnings son kayıt satırlarıdır (CB-8).
/// </summary>
internal sealed record DashboardResponse(
    string Window,
    DateTimeOffset From,
    DateTimeOffset To,
    IReadOnlyList<LevelCountResponse> Levels,
    IReadOnlyList<ServiceErrorCountResponse> TopErrorServices,
    IReadOnlyList<ExceptionTypeCountResponse> TopExceptionTypes,
    IReadOnlyList<TrendSliceResponse> Slices,
    IReadOnlyList<LogSummaryResponse> RecentErrors,
    IReadOnlyList<LogSummaryResponse> RecentWarnings)
{
    public static DashboardResponse Create(DashboardSummary s) => new(
        s.Window,
        s.From.ToUniversalTime(),
        s.To.ToUniversalTime(),
        LevelResponses(s.Levels),
        s.TopErrorServices.Select(e => new ServiceErrorCountResponse(e.ServiceName, e.Count)).ToArray(),
        s.TopExceptionTypes.Select(t => new ExceptionTypeCountResponse(t.ExceptionType, t.Count, t.LastSeenAt.ToUniversalTime())).ToArray(),
        s.Slices.Select(h => new TrendSliceResponse(h.From.ToUniversalTime(), h.To.ToUniversalTime(), LevelResponses(h.Levels))).ToArray(),
        s.RecentErrors.Select(LogSummaryResponse.From).ToArray(),
        s.RecentWarnings.Select(LogSummaryResponse.From).ToArray());

    private static LevelCountResponse[] LevelResponses(IReadOnlyList<LevelCount> levels) =>
        levels.Select(l => new LevelCountResponse(l.Level.ToString(), l.Count)).ToArray();
}

/// <summary>Trendin bir dilimi (Spec 0007; Spec 0011, CB-3); From/To log listesine olduğu gibi taşınır.</summary>
internal sealed record TrendSliceResponse(DateTimeOffset From, DateTimeOffset To, IReadOnlyList<LevelCountResponse> Levels);

internal sealed record LevelCountResponse(string Level, long Count);

internal sealed record ServiceErrorCountResponse(string ServiceName, long Count);

internal sealed record ExceptionTypeCountResponse(string ExceptionType, long Count, DateTimeOffset LastSeenAt);
