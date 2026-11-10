using System.Collections.Concurrent;
using System.Net;
using AIHub.Application.Abstractions;
using AIHub.Domain.Monitoring;

namespace AIHub.Application.Tests.Monitoring;

internal sealed class SahteIzlenenUygulamaDeposu : IMonitoredAppRepository
{
    public List<MonitoredApp> Apps { get; } = [];

    /// <summary>Denetim sürerken (sahiplenme ile kayıt arasında) başka bir isteğin yaptığı değişikliği taklit eder.</summary>
    public Action? BeforeRecord { get; set; }

    public Task<IReadOnlyList<MonitoredApp>> ListAsync(CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<MonitoredApp>>([.. Apps]);

    public Task<MonitoredApp?> FindByIdAsync(Guid id, CancellationToken cancellationToken) =>
        Task.FromResult(Apps.SingleOrDefault(a => a.Id == id));

    public Task<AddMonitoredAppOutcome> AddAsync(MonitoredApp app, int maxApps, CancellationToken cancellationToken)
    {
        if (Apps.Any(a => a.ServiceName == app.ServiceName))
        {
            return Task.FromResult(AddMonitoredAppOutcome.ServiceNameTaken);
        }

        if (Apps.Count >= maxApps)
        {
            return Task.FromResult(AddMonitoredAppOutcome.LimitReached);
        }

        Apps.Add(app);
        return Task.FromResult(AddMonitoredAppOutcome.Added);
    }

    /// <summary>"Bir kez eklendi" işaretleri (Spec 0008, R-5).</summary>
    public HashSet<string> Registered { get; } = [];

    public Task<RegisterOnceOutcome> RegisterOnceAsync(MonitoredApp app, int maxApps, CancellationToken cancellationToken)
    {
        if (Registered.Contains(app.ServiceName))
        {
            return Task.FromResult(RegisterOnceOutcome.AlreadyRegistered);
        }

        var outcome = AddAsync(app, maxApps, cancellationToken).Result switch
        {
            AddMonitoredAppOutcome.Added => RegisterOnceOutcome.Added,
            AddMonitoredAppOutcome.ServiceNameTaken => RegisterOnceOutcome.ServiceNameTaken,
            _ => RegisterOnceOutcome.LimitReached,
        };
        if (outcome != RegisterOnceOutcome.LimitReached)
        {
            Registered.Add(app.ServiceName);
        }

        return Task.FromResult(outcome);
    }

    public Task RemoveAsync(MonitoredApp app, CancellationToken cancellationToken)
    {
        Apps.Remove(app);
        return Task.CompletedTask;
    }

    public Task SaveChangesAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    /// <summary>
    /// Sahiplenmenin ileri aldığı zaman (veritabanında SQL yazar, varlık değişmez) ve o anda varlıkta görülen
    /// değer. Varlığın değeri sonradan değiştiyse (adres değişimi <c>NextCheckAt = now</c> yapar) o kazanır:
    /// veritabanında da son yazan kazanır (review 1, F-06).
    /// </summary>
    private readonly Dictionary<Guid, (DateTimeOffset Scheduled, DateTimeOffset SeenOnEntity)> _schedule = [];

    private DateTimeOffset NextCheckOf(MonitoredApp app) =>
        _schedule.TryGetValue(app.Id, out var s) && s.SeenOnEntity == app.NextCheckAt ? s.Scheduled : app.NextCheckAt;

    public Task<IReadOnlyList<DueCheck>> ClaimDueAsync(DateTimeOffset now, TimeSpan interval, int max, CancellationToken cancellationToken)
    {
        var due = Apps
            .Where(a => NextCheckOf(a) <= now)
            .Take(max)
            .ToList();
        foreach (var app in due)
        {
            // Depodaki SQL ile aynı kural: çizelgeden ilerler, çok gerideyse şimdiden başlar (review 1, F-01);
            // iki denetim arası yarım aralıktan kısa olmaz (review 2, N-1).
            var next = NextCheckOf(app) + interval;
            var floor = now + interval / 2;
            _schedule[app.Id] = (next > now ? (next > floor ? next : floor) : now + interval, app.NextCheckAt);
        }

        return Task.FromResult<IReadOnlyList<DueCheck>>(due.Select(a => new DueCheck(a.Id, a.HealthUrl)).ToArray());
    }

    public Task<bool> UpdateLockedAsync(Guid id, Action<MonitoredApp> apply, CancellationToken cancellationToken)
    {
        var app = Apps.SingleOrDefault(a => a.Id == id);
        if (app is null)
        {
            return Task.FromResult(false);
        }

        apply(app);
        return Task.FromResult(true);
    }

    public Task<bool> RecordResultAsync(Guid id, string checkedUrl, Action<MonitoredApp> apply, CancellationToken cancellationToken)
    {
        BeforeRecord?.Invoke();
        var app = Apps.SingleOrDefault(a => a.Id == id);
        if (app is null || app.HealthUrl != checkedUrl)
        {
            return Task.FromResult(false);
        }

        apply(app);
        return Task.FromResult(true);
    }
}

/// <summary>Adres başına sıradaki sonuçları verir; verilmemişse başarılı döner. Denenen adresleri sayar.</summary>
internal sealed class SahteProbe : IHealthProbe
{
    private readonly ConcurrentDictionary<string, ConcurrentQueue<ProbeResult>> _results = new();

    public ConcurrentBag<string> Probed { get; } = [];

    /// <summary>Denetim sonucu dönmeden hemen önce çağrılır; testler denetimin süresini saate yansıtır.</summary>
    public Action<ProbeResult>? BeforeReturn { get; set; }

    public void Sirala(string url, params ProbeResult[] results) =>
        _results[url] = new ConcurrentQueue<ProbeResult>(results);

    public Task<ProbeResult> ProbeAsync(Uri url, CancellationToken cancellationToken)
    {
        Probed.Add(url.OriginalString);
        var result = _results.TryGetValue(url.OriginalString, out var queue) && queue.TryDequeue(out var next) ? next : ProbeResult.Success;
        BeforeReturn?.Invoke(result);
        return Task.FromResult(result);
    }
}

internal sealed class SahteCozucu : IHostAddressResolver
{
    public Dictionary<string, IPAddress[]> Kayitlar { get; } = [];

    public Task<IReadOnlyList<IPAddress>> ResolveAsync(string host, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<IPAddress>>(Kayitlar.TryGetValue(host, out var ips) ? ips : []);
}
