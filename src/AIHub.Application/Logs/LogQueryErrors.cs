using AIHub.Domain.Common;

namespace AIHub.Application.Logs;

public static class LogQueryErrors
{
    public static readonly Error RangeReversed =
        new("to.beforeFrom", "Bitiş zamanı başlangıç zamanından önce olamaz.", "to");

    public static readonly Error RangeTooLong =
        new("from.rangeTooLong", $"Zaman aralığı en fazla {LogListHandler.MaxRangeDays} gün olabilir.", "from");

    public static Error UnknownLevel(string value) =>
        new("level.invalid", $"'{value}' tanımlı bir LogLevel değil; Info, Warning, Error veya Critical olmalıdır.", "level");

    /// <summary>Log deposu okunamıyor; panel kullanıcısı sonra yeniden dener. İç ayrıntı taşımaz (Spec 0002, R-9).</summary>
    public static readonly Error StoreUnavailable =
        new("store.unavailable", "Loglar şu anda okunamıyor; lütfen daha sonra yeniden deneyin.");
}
