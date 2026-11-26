using AIHub.Application.Logs;
using AIHub.Domain.Common;

namespace AIHub.Application.Dashboard;

/// <summary>
/// Dashboard'un zaman penceresi (Spec 0011): alınma zamanına göre [From, To] ve trendin dilimleri. Sekiz hazır pencere
/// yüklenme anından geriye sabit uzunluktadır (CB-1; takvim ayı değil). Serbest aralık (CB-7) kullanıcının seçtiği
/// [from, to]'dur; dilim boyu, uzunluğu aşmayan en kısa hazır pencerenin dilimidir ve son dilim aralığın sonunda
/// kesilir (CB-3, CB-7).
/// </summary>
public sealed record DashboardWindow(string Key, DateTimeOffset From, DateTimeOffset To, TimeSpan SliceLength, int SliceCount)
{
    public const string DefaultKey = "24h";
    public const string CustomKey = "custom";

    /// <summary>Hazır bir pencerenin tanımı: uzunluk ve dilim boyu (spec'in iki tablosu tek yerde).</summary>
    public sealed record Preset(string Key, TimeSpan Length, TimeSpan SliceLength)
    {
        public int SliceCount => (int)(Length / SliceLength);
    }

    /// <summary>Spec 0011, CB-1 ve CB-3 tabloları; kısadan uzuna.</summary>
    public static readonly IReadOnlyList<Preset> Presets =
    [
        new("24h", TimeSpan.FromHours(24), TimeSpan.FromHours(1)),
        new("48h", TimeSpan.FromHours(48), TimeSpan.FromHours(2)),
        new("7d", TimeSpan.FromDays(7), TimeSpan.FromHours(6)),
        new("14d", TimeSpan.FromDays(14), TimeSpan.FromHours(12)),
        new("30d", TimeSpan.FromDays(30), TimeSpan.FromDays(1)),
        new("90d", TimeSpan.FromDays(90), TimeSpan.FromDays(3)),
        new("180d", TimeSpan.FromDays(180), TimeSpan.FromDays(6)),
        new("365d", TimeSpan.FromDays(365), TimeSpan.FromDays(5)),
    ];

    /// <summary>
    /// Serbest aralığın üst sınırı log listesininkiyle aynıdır: dashboard'un açtığı her bağlantıyı liste kabul eder
    /// (CB-4). 365 gün + 1 saat: yaz saatli bir saat diliminde 365 yerel gün 1 saat uzun olabilir (plan 0011, risk 4).
    /// </summary>
    public static TimeSpan MaxCustomLength => LogListHandler.MaxRange;

    /// <summary>Hazır pencere: yüklenme anından (<paramref name="now"/>) geriye.</summary>
    public static Result<DashboardWindow> ForPreset(string? key, DateTimeOffset now)
    {
        var preset = Presets.FirstOrDefault(p => p.Key == (key ?? DefaultKey));
        if (preset is null)
        {
            return Result<DashboardWindow>.Failure(DashboardErrors.UnknownWindow(key!));
        }

        return Result<DashboardWindow>.Success(new DashboardWindow(preset.Key, now - preset.Length, now, preset.SliceLength, preset.SliceCount));
    }

    /// <summary>
    /// Serbest aralık (CB-7): bitiş başlangıçtan önceyse, başlangıç gelecekteyse ya da aralık
    /// <see cref="MaxCustomLength"/>'i aşıyorsa reddedilir. Bitiş yüklenme anından sonraysa yüklenme anına kırpılır.
    /// </summary>
    public static Result<DashboardWindow> ForRange(DateTimeOffset from, DateTimeOffset to, DateTimeOffset now)
    {
        from = from.ToUniversalTime();
        to = to.ToUniversalTime();

        var errors = new List<Error>();
        if (to < from)
        {
            errors.Add(DashboardErrors.RangeReversed);
        }
        else if (to - from > MaxCustomLength)
        {
            errors.Add(DashboardErrors.RangeTooLong);
        }

        if (from > now)
        {
            errors.Add(DashboardErrors.FromInFuture);
        }

        if (errors.Count > 0)
        {
            return Result<DashboardWindow>.Failure(errors);
        }

        var end = to > now ? now : to;
        var length = end - from;
        var slice = SliceLengthFor(length);
        var count = Math.Max(1, (int)Math.Ceiling(length.Ticks / (double)slice.Ticks));
        return Result<DashboardWindow>.Success(new DashboardWindow(CustomKey, from, end, slice, count));
    }

    /// <summary>Uzunluğu aşmayan en kısa hazır pencerenin dilim boyu; 365 günü aşan (yaz saati payı) en uzununki.</summary>
    public static TimeSpan SliceLengthFor(TimeSpan length) =>
        (Presets.FirstOrDefault(p => length <= p.Length) ?? Presets[^1]).SliceLength;
}
