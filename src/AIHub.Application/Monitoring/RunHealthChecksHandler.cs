using AIHub.Application.Abstractions;
using AIHub.Domain.Monitoring;

namespace AIHub.Application.Monitoring;

/// <summary>
/// Bir denetim turu (Spec 0005, R-4, R-5): zamanı gelenleri sahiplen, hepsini aynı anda dene, her sonucu
/// denetimi biter bitmez kaydet. Tur en fazla bir zaman aşımı kadar sürer; sonraki tur gecikmez
/// (review 1, F-01). Depo bağlamı paylaşıldığı için kayıtlar sırayla yazılır. Bu arada adresi değişen ya
/// da silinen uygulamanın sonucu atılır (plan 0005, risk 5).
/// </summary>
public sealed class RunHealthChecksHandler(IMonitoredAppRepository apps, IHealthProbe probe, IClock clock)
{
    /// <summary>Sınırdaki bütün uygulamalar aynı anda denetlenir: 200 zaman aşımı da tek bir zaman aşımı sürer.</summary>
    public const int MaxConcurrentProbes = MonitoringLimits.MaxMonitoredApps;

    /// <summary>Bir turda sahiplenilen en fazla satır; sınırdaki bütün uygulamalar tek turda denetlenir.</summary>
    public const int ClaimBatchSize = MonitoringLimits.MaxMonitoredApps;

    /// <returns>Kaydedilen sonuç sayısı.</returns>
    public async Task<int> HandleAsync(TimeSpan checkInterval, CancellationToken cancellationToken)
    {
        var due = await apps.ClaimDueAsync(clock.UtcNow, checkInterval, ClaimBatchSize, cancellationToken);
        if (due.Count == 0)
        {
            return 0;
        }

        using var recordGate = new SemaphoreSlim(1, 1);
        var recorded = 0;
        await Parallel.ForEachAsync(
            due,
            new ParallelOptions { MaxDegreeOfParallelism = MaxConcurrentProbes, CancellationToken = cancellationToken },
            async (check, ct) =>
            {
                var result = await probe.ProbeAsync(new Uri(check.HealthUrl), ct);
                var at = clock.UtcNow;
                await recordGate.WaitAsync(ct);
                try
                {
                    var applied = await apps.RecordResultAsync(check.Id, check.HealthUrl, app =>
                    {
                        if (result.IsSuccess)
                        {
                            app.RecordSuccess(at);
                        }
                        else
                        {
                            app.RecordFailure(result.Reason!.Value, result.StatusCode, at);
                        }
                    }, ct);
                    recorded += applied ? 1 : 0;
                }
                finally
                {
                    recordGate.Release();
                }
            });

        return recorded;
    }
}
