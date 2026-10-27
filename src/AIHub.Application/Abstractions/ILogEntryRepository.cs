using AIHub.Domain.Logs;

namespace AIHub.Application.Abstractions;

/// <summary>
/// LogEntry deposu. Güncelleme metodu bilerek yoktur: saklanan kayıt değişmez (docs/domain.md).
/// </summary>
public interface ILogEntryRepository
{
    /// <summary>
    /// Kaydı ekler; aynı Id ile kayıt zaten varsa hiçbir şey yapmaz. Kuyruktan aynı kaydın ikinci kez
    /// teslim edilmesi böylece ikinci bir satır üretmez (Spec 0001, R-6).
    /// </summary>
    Task AddIfAbsentAsync(LogEntry entry, CancellationToken cancellationToken);
}
