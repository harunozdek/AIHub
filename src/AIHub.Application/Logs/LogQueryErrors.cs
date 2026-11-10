using AIHub.Domain.Common;
using AIHub.Domain.Logs;

namespace AIHub.Application.Logs;

public static class LogQueryErrors
{
    public static readonly Error RangeReversed =
        new("to.beforeFrom", "Bitiş zamanı başlangıç zamanından önce olamaz.", "to");

    public static readonly Error RangeTooLong =
        new("from.rangeTooLong", $"Zaman aralığı en fazla {LogListHandler.MaxRangeDays} gün olabilir.", "from");

    /// <summary>Başlangıç verilmediğinde bitişten 24 saat geriye gidilemiyor (tarih aralığının alt sınırı; review 3, R3-04).</summary>
    public static readonly Error ToOutOfRange =
        new("to.outOfRange", "Bitiş zamanı geçerli tarih aralığının dışında.", "to");

    public static readonly Error ExceptionTypeTooLong =
        new("exceptionType.tooLong", $"ExceptionType en fazla {LogEntryLimits.ExceptionTypeMaxLength} karakter olabilir.", "exceptionType");

    public static Error UnknownLevel(string value) =>
        new("level.invalid", $"'{value}' tanımlı bir LogLevel değil; Info, Warning, Error veya Critical olmalıdır.", "level");

    /// <summary>Log deposu okunamıyor; panel kullanıcısı sonra yeniden dener. İç ayrıntı taşımaz (Spec 0002, R-9).</summary>
    public static readonly Error StoreUnavailable =
        new("store.unavailable", "Loglar şu anda okunamıyor; lütfen daha sonra yeniden deneyin.");
}
