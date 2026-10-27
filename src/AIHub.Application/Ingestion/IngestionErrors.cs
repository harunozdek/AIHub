using AIHub.Domain.Common;

namespace AIHub.Application.Ingestion;

public static class IngestionErrors
{
    /// <summary>Kuyruk erişilemez; gönderen servis daha sonra yeniden denemelidir. İç ayrıntı taşımaz.</summary>
    public static readonly Error QueueUnavailable =
        new("queue.unavailable", "Kayıt şu anda kabul edilemiyor; lütfen daha sonra yeniden deneyin.");
}
