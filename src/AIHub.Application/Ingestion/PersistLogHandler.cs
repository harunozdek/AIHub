using AIHub.Application.Abstractions;
using AIHub.Domain.Logs;

namespace AIHub.Application.Ingestion;

/// <summary>
/// Kuyruk tüketicisinin kullanım senaryosu: kuyruktan gelen (zaten maskelenmiş) kaydı kalıcı depoya
/// yazar. Depo hatası exception olarak yukarı çıkar; tüketici kaydı onaylamaz ve yeniden dener (R-7).
/// AI analizi tetikleme (BR-5) ileride bu noktaya eklenir.
/// </summary>
public sealed class PersistLogHandler(ILogEntryRepository repository)
{
    public Task HandleAsync(LogEntry entry, CancellationToken cancellationToken) =>
        repository.AddIfAbsentAsync(entry, cancellationToken);
}
