using AIHub.Application.Abstractions;
using AIHub.Application.Logs;
using AIHub.Domain.Common;
using AIHub.Domain.Logs;

namespace AIHub.Application.Dashboard;

/// <summary>Dashboard isteği: hazır pencere (<see cref="Window"/>) ya da serbest aralık (<see cref="From"/>, <see cref="To"/>).</summary>
public sealed record DashboardQuery(string? Window = null, DateTimeOffset? From = null, DateTimeOffset? To = null);

/// <summary>
/// Dashboard'un log özeti: seçilen pencerede derece sayıları, en çok hata üreten servisler, en sık exception türleri,
/// trend dilimleri (Spec 0006 R-2 … R-5, Spec 0007, Spec 0011 CB-1 … CB-3, CB-7) ve son hatalar ile son uyarılar
/// (Spec 0011, CB-8). Sıralama ve sınırlar okuma deposunun sözleşmesidir. Okuma sorguları eşzamanlı çalışır
/// (plan 0011, CB-5). Depo okunamazsa <see cref="LogQueryErrors.StoreUnavailable"/> döner (R-9).
/// </summary>
public sealed class DashboardHandler(ILogEntryReader reader, IClock clock)
{
    /// <summary>Varsayılan pencere (Spec 0006 R-2, Spec 0011 PB-1).</summary>
    public static readonly TimeSpan Window = TimeSpan.FromHours(24);
    public const int TopServiceCount = 10;
    public const int TopExceptionTypeCount = 5;
    public const int RecentCount = 10;

    /// <summary>Son kayıt satırında mesajın ilk satırının en fazla bu kadar karakteri (Spec 0011, CB-8, S-4).</summary>
    public const int RecentPreviewMaxLength = 120;

    /// <summary>PostgreSQL alınma zamanını mikrosaniye hassasiyetle saklar; dilim sonu bir sonraki dilimden 1 µs öncedir.</summary>
    private static readonly TimeSpan OneMicrosecond = TimeSpan.FromTicks(TimeSpan.TicksPerMicrosecond);

    /// <summary>Ekrandaki sıra: en ağır derece önce.</summary>
    private static readonly LogLevel[] LevelOrder = [LogLevel.Critical, LogLevel.Error, LogLevel.Warning, LogLevel.Info];

    private static readonly LogLevel[] ErrorLevels = [LogLevel.Error, LogLevel.Critical];
    private static readonly LogLevel[] WarningLevels = [LogLevel.Warning];

    /// <summary>Son 24 saat (Spec 0006; Spec 0011, PB-1).</summary>
    public Task<Result<DashboardSummary>> HandleAsync(CancellationToken cancellationToken) =>
        HandleAsync(new DashboardQuery(), cancellationToken);

    public async Task<Result<DashboardSummary>> HandleAsync(DashboardQuery query, CancellationToken cancellationToken)
    {
        // Milisaniyeye kırpılır: dilim sınırları veritabanının mikrosaniye hassasiyetinde tam karşılık bulur ve
        // listeye taşınan from/to aynı anı gösterir (plan 0007, risk 1).
        var now = clock.UtcNow.ToUniversalTime();
        now = now.AddTicks(-(now.Ticks % TimeSpan.TicksPerMillisecond));

        var window = Resolve(query, now);
        if (!window.IsSuccess)
        {
            return Result<DashboardSummary>.Failure(window.Errors);
        }

        var (key, from, to, sliceLength, sliceCount) = window.Value;

        // Sorgular eşzamanlı başlar; okuma deposu her biri için ayrı bağlam kullanır (plan 0011, CB-5).
        var services = reader.ListTopErrorServicesAsync(from, to, TopServiceCount, cancellationToken);
        var exceptionTypes = reader.ListTopExceptionTypesAsync(from, to, TopExceptionTypeCount, cancellationToken);
        var slices = reader.ListSliceLevelCountsAsync(from, to, sliceLength, sliceCount, cancellationToken);
        var errors = reader.ListAsync(Recent(from, to, ErrorLevels), cancellationToken);
        var warnings = reader.ListAsync(Recent(from, to, WarningLevels), cancellationToken);
        await Task.WhenAll(services, exceptionTypes, slices, errors, warnings);

        var failed = new[] { services.Result.Errors, exceptionTypes.Result.Errors, slices.Result.Errors, errors.Result.Errors, warnings.Result.Errors }
            .FirstOrDefault(e => e.Count > 0);
        if (failed is not null)
        {
            return Result<DashboardSummary>.Failure(failed);
        }

        var trend = Slices(from, to, sliceLength, sliceCount, slices.Result.Value);

        // Derece sayıları dilimlerin toplamıdır: "dilim toplamı = derece sayısı" yapı gereği tutar (CB-3).
        var levels = LevelOrder
            .Select(l => new LevelCount(l, trend.Sum(s => s.Levels.Single(c => c.Level == l).Count)))
            .ToArray();

        return Result<DashboardSummary>.Success(new DashboardSummary(
            key,
            from,
            to,
            levels,
            services.Result.Value,
            exceptionTypes.Result.Value,
            trend,
            Previews(errors.Result.Value),
            Previews(warnings.Result.Value)));
    }

    private static Result<DashboardWindow> Resolve(DashboardQuery query, DateTimeOffset now)
    {
        if (query.From is null && query.To is null)
        {
            return DashboardWindow.ForPreset(query.Window, now);
        }

        if (query.Window is not null)
        {
            return Result<DashboardWindow>.Failure(DashboardErrors.WindowWithRange);
        }

        if (query.From is not { } from || query.To is not { } to)
        {
            return Result<DashboardWindow>.Failure(DashboardErrors.RangeIncomplete);
        }

        return DashboardWindow.ForRange(from, to, now);
    }

    private static LogListCriteria Recent(DateTimeOffset from, DateTimeOffset to, IReadOnlyList<LogLevel> levels) =>
        new(null, levels, from, to, null, RecentCount);

    private static LogSummary[] Previews(IReadOnlyList<LogEntry> entries) =>
        entries.Select(e => LogSummary.From(e, RecentPreviewMaxLength)).ToArray();

    /// <summary>Bütün dilimler, her dilimde dört derece; kaydı olmayan 0. Son dilim pencerenin sonuyla biter (CB-3, CB-7).</summary>
    private static TrendSlice[] Slices(DateTimeOffset from, DateTimeOffset to, TimeSpan sliceLength, int sliceCount, IReadOnlyList<SliceLevelCount> counts)
    {
        var bySlice = counts.ToLookup(c => c.Slice);
        return Enumerable.Range(0, sliceCount).Select(i =>
        {
            var sliceFrom = from + i * sliceLength;
            var sliceTo = i == sliceCount - 1 ? to : sliceFrom + sliceLength - OneMicrosecond;
            var inSlice = bySlice[i].ToDictionary(c => c.Level, c => c.Count);
            return new TrendSlice(sliceFrom, sliceTo, LevelOrder.Select(l => new LevelCount(l, inSlice.GetValueOrDefault(l))).ToArray());
        }).ToArray();
    }
}
