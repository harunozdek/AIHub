using AIHub.Application.Logs;
using AIHub.Domain.Common;

namespace AIHub.Application.Dashboard;

/// <summary>
/// Dashboard penceresinin doğrulama hataları (Spec 0011). Adresteki tanımsız bir seçimi "Son 24 saat"e çevirmek
/// panelin işidir (CB-1); API hatalı isteği sessizce düzeltmez (plan 0011, onaylı eski risk 4).
/// </summary>
public static class DashboardErrors
{
    public static Error UnknownWindow(string value) =>
        new("window.invalid", $"'{value}' tanımlı bir zaman penceresi değil; {string.Join(", ", DashboardWindow.Presets.Select(p => p.Key))} olmalıdır.", "window");

    public static readonly Error WindowWithRange =
        new("window.withRange", "Zaman penceresi ile başlangıç/bitiş birlikte verilemez.", "window");

    public static readonly Error RangeIncomplete =
        new("range.incomplete", "Tarih aralığı için başlangıç ve bitiş birlikte verilmelidir.", "from");

    public static readonly Error RangeReversed = LogQueryErrors.RangeReversed;

    public static readonly Error RangeTooLong = LogQueryErrors.RangeTooLong;

    public static readonly Error FromInFuture =
        new("from.inFuture", "Başlangıç tarihi gelecekte olamaz.", "from");
}
