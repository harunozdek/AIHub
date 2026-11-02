using AIHub.Application.Abstractions;

namespace AIHub.Application.Monitoring;

/// <summary>
/// Bir denetim turu (Spec 0005, R-4, R-5): zamanı gelenleri sahiplen, aynı anda en fazla
/// <see cref="MaxConcurrentProbes"/> denetim yap, sonuçları kaydet. Denetimler depoya dokunmaz;
/// sonuçlar tur sonunda sırayla yazılır. Bu arada adresi değişen ya da silinen uygulamanın sonucu
/// atılır (plan 0005, risk 5).
/// </summary>
public sealed class RunHealthChecksHandler(IMonitoredAppRepository apps, IHealthProbe probe, IClock clock)
{
    /// <summary>200 uygulamanın hepsi zaman aşımına düşse de tur 50 sn'de biter (plan 0005, risk 6).</summary>
    public const int MaxConcurrentProbes = 20;

    /// <summary>Bir turda sahiplenilen en fazla satır; sınırdaki bütün uygulamalar tek turda denetlenir.</summary>
    public const int ClaimBatchSize = 200;

    /// <returns>Kaydedilen sonuç sayısı.</returns>
    public async Task<int> HandleAsync(TimeSpan checkInterval, CancellationToken cancellationToken)
    {
        var due = await apps.ClaimDueAsync(clock.UtcNow, checkInterval, ClaimBatchSize, cancellationToken);
        if (due.Count == 0)
        {
            return 0;
        }

        var results = new (DueCheck Check, ProbeResult Result, DateTimeOffset At)[due.Count];
        await Parallel.ForEachAsync(
            Enumerable.Range(0, due.Count),
            new ParallelOptions { MaxDegreeOfParallelism = MaxConcurrentProbes, CancellationToken = cancellationToken },
            async (i, ct) =>
            {
                var check = due[i];
                var result = await probe.ProbeAsync(new Uri(check.HealthUrl), ct);
                results[i] = (check, result, clock.UtcNow);
            });

        var recorded = 0;
        foreach (var (check, result, at) in results)
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
            }, cancellationToken);
            recorded += applied ? 1 : 0;
        }

        return recorded;
    }
}
