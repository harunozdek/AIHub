using System.Collections.Concurrent;
using AIHub.Api.Tests.Altyapi;
using AIHub.Application.Abstractions;
using AIHub.Domain.Monitoring;
using Microsoft.Extensions.DependencyInjection;

namespace AIHub.Api.Tests;

// Spec 0012, CB-5 ve plan 0012 risk 2 (self-critique E-2): üretim sabitleriyle (10 sn aralık, 2 sn uyanma, 5 sn zaman
// aşımı) 200 uygulamanın yarısı zaman aşımına kadar beklerken, yarısı hemen yanıt verirken her uygulama her aralıkta
// bir kez denetlenir ve iki denetim arası 5 sn'den kısa olmaz. Gerçek PostgreSQL'de, gerçek worker ile; yalnızca giden
// HTTP isteği sahte bir denetçiyle yerine konur. Ölçüm ~45 sn sürer: full profil.
[Trait("Category", "Integration")]
[Collection(IngestionCollection.Name)]
public sealed class IzlemeTuruPerformansTestleri(ContainerFixture containers) : IAsyncLifetime
{
    private static readonly TimeSpan Olcum = TimeSpan.FromSeconds(45);
    private static readonly TimeSpan IsinmaSonu = TimeSpan.FromSeconds(12);
    private readonly SahteDenetci _denetci = new();
    private AIHubFactory _factory = null!;

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public async ValueTask InitializeAsync()
    {
        _factory = new AIHubFactory(containers)
        {
            ConsumerEnabled = false,
            MonitoringEnabled = true,
            PostgresConnectionOverride = await TestVerisi.BosVeritabaniAsync(containers, Ct),
            // Üretim sabitleri: testlerin kısa varsayılanları (0,2 / 0,5 / 1 sn) bu ölçümde kullanılmaz.
            MonitoringSettings = new Dictionary<string, string?>
            {
                ["Monitoring:TickInterval"] = MonitoringLimits.TickInterval.ToString(),
                ["Monitoring:CheckInterval"] = MonitoringLimits.CheckInterval.ToString(),
                ["Monitoring:Timeout"] = MonitoringLimits.Timeout.ToString(),
            },
            TestServices = s => s.AddSingleton<IHealthProbe>(_denetci),
        };
        _ = _factory.Services;
    }

    public async ValueTask DisposeAsync() => await _factory.DisposeAsync();

    [Fact]
    public async Task Iki_yuz_uygulamanin_yarisi_zaman_asimindayken_her_uygulama_her_aralikta_bir_kez_denetlenir()
    {
        var baslangic = DateTimeOffset.UtcNow;
        var eklenen = 0;
        await using (var scope = _factory.Services.CreateAsyncScope())
        {
            var depo = scope.ServiceProvider.GetRequiredService<IMonitoredAppRepository>();
            // AIHub'ın kendi kaydı (Spec 0008) varsa o da sayılır: toplam 200'ü geçmez.
            var mevcut = (await depo.ListAsync(Ct)).Count;
            for (var i = 0; i < MonitoringLimits.MaxMonitoredApps - mevcut; i++)
            {
                var ad = $"{(i % 2 == 0 ? "yavas" : "hizli")}-{i}";
                var uygulama = MonitoredApp.Create(Guid.CreateVersion7(), ad, HealthUrl.Create($"http://{ad}.kurum.local/health").Value, baslangic, Guid.CreateVersion7()).Value;
                Assert.Equal(AddMonitoredAppOutcome.Added, await depo.AddAsync(uygulama, MonitoringLimits.MaxMonitoredApps, Ct));
                eklenen++;
            }
        }

        await Task.Delay(Olcum, Ct);
        var bitis = DateTimeOffset.UtcNow;

        // Isınmadan sonraki pencere: bütün uygulamalar eklenme anında birlikte sırada beklediğinden ilk tur sayılmaz.
        var pencereBas = baslangic + IsinmaSonu;
        var pencere = bitis - pencereBas;
        var adresler = _denetci.Denetimler.Keys.Where(k => k.EndsWith(".kurum.local", StringComparison.Ordinal)).ToList();
        Assert.True(eklenen >= MonitoringLimits.MaxMonitoredApps - 1, $"Eklenen {eklenen}.");
        Assert.Equal(eklenen, adresler.Count);

        var enKisa = TimeSpan.MaxValue;
        var enUzun = TimeSpan.Zero;
        foreach (var adres in adresler)
        {
            var anlar = _denetci.Denetimler[adres].OrderBy(a => a).ToList();
            for (var i = 1; i < anlar.Count; i++)
            {
                var ara = anlar[i] - anlar[i - 1];
                enKisa = ara < enKisa ? ara : enKisa;
                if (anlar[i - 1] >= pencereBas)
                {
                    enUzun = ara > enUzun ? ara : enUzun;
                }
            }

            // Penceredeki her 10 sn'lik aralıkta bir denetim: pencerenin aralık sayısı kadar (uçlarda ±1).
            var penceredeki = anlar.Count(a => a >= pencereBas && a < bitis);
            var beklenen = pencere / MonitoringLimits.CheckInterval;
            Assert.InRange(penceredeki, (int)Math.Floor(beklenen) - 1, (int)Math.Ceiling(beklenen) + 1);
        }

        TestContext.Current.TestOutputHelper?.WriteLine(
            $"200 uygulama (yarısı 5 sn zaman aşımında): iki denetim arası en kısa {enKisa.TotalSeconds:F2} sn, ısınmadan sonra en uzun {enUzun.TotalSeconds:F2} sn.");
        Assert.True(enKisa >= TimeSpan.FromSeconds(5) - TimeSpan.FromMilliseconds(50), $"İki denetim arası {enKisa} — 5 sn'den kısa.");
        // Gecikme bir uyanma aralığını aşmaz (plan 0012, Risk 2: 10 + 2 sn + tur payı 1 sn). Review 1 F-8: iki aralık
        // sınırı uyanmanın ya da turun birkaç saniye kaymasını yakalamıyordu.
        var sinir = MonitoringLimits.CheckInterval + MonitoringLimits.TickInterval + TimeSpan.FromSeconds(1);
        Assert.True(enUzun <= sinir, $"En uzun ara {enUzun} — {sinir} sınırını aştı.");
    }

    /// <summary>"yavas-" adresleri zaman aşımına kadar bekler ve zaman aşımı döner; ötekiler hemen başarı döner.</summary>
    private sealed class SahteDenetci : IHealthProbe
    {
        public ConcurrentDictionary<string, ConcurrentBag<DateTimeOffset>> Denetimler { get; } = new();

        public async Task<ProbeResult> ProbeAsync(Uri url, CancellationToken cancellationToken)
        {
            Denetimler.GetOrAdd(url.Host, _ => []).Add(DateTimeOffset.UtcNow);
            if (url.Host.StartsWith("yavas-", StringComparison.Ordinal))
            {
                await Task.Delay(MonitoringLimits.Timeout, cancellationToken);
                return ProbeResult.Failure(HealthFailureReason.Timeout);
            }

            return ProbeResult.Success;
        }
    }
}
