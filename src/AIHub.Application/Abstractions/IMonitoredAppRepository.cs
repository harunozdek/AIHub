using AIHub.Domain.Monitoring;

namespace AIHub.Application.Abstractions;

/// <summary>Sahiplenilmiş bir denetim: denetlenen adres, sonuç kaydedilirken hâlâ aynı mı diye karşılaştırılır.</summary>
public sealed record DueCheck(Guid Id, string HealthUrl);

public enum AddMonitoredAppOutcome
{
    Added,
    ServiceNameTaken,
    LimitReached,
}

public interface IMonitoredAppRepository
{
    Task<IReadOnlyList<MonitoredApp>> ListAsync(CancellationToken cancellationToken);

    Task<MonitoredApp?> FindByIdAsync(Guid id, CancellationToken cancellationToken);

    /// <summary>
    /// Ekler. Aynı ServiceName zaten izleniyorsa ya da sınır doluysa eklemez. Sayma ve ekleme eşzamanlı
    /// eklemelere karşı sıraya girer: sınır yarışta aşılmaz (Spec 0005, AC-2, AC-11).
    /// </summary>
    Task<AddMonitoredAppOutcome> AddAsync(MonitoredApp app, int maxApps, CancellationToken cancellationToken);

    Task RemoveAsync(MonitoredApp app, CancellationToken cancellationToken);

    /// <summary>Bu depodan okunan uygulamalardaki değişiklikleri kalıcılaştırır.</summary>
    Task SaveChangesAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Zamanı gelmiş uygulamaları sahiplenir ve sonraki denetim zamanlarını <paramref name="interval"/>
    /// kadar ileri alır. Aynı satırı o aralıkta ikinci bir AIHub örneği alamaz (Spec 0005, R-4; ADR 0003).
    /// </summary>
    Task<IReadOnlyList<DueCheck>> ClaimDueAsync(DateTimeOffset now, TimeSpan interval, int max, CancellationToken cancellationToken);

    /// <summary>
    /// Denetim sonucunu satır kilidi altında uygular. Uygulama silinmişse ya da adresi
    /// <paramref name="checkedUrl"/>'den farklıysa sonuç atılır ve <c>false</c> döner (plan 0005, risk 5).
    /// </summary>
    Task<bool> RecordResultAsync(Guid id, string checkedUrl, Action<MonitoredApp> apply, CancellationToken cancellationToken);
}
