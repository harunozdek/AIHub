using System.Collections.Concurrent;
using System.Data.Common;
using System.Diagnostics;
using System.Net;
using AIHub.Api.Tests.Altyapi;
using AIHub.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;

namespace AIHub.Api.Tests;

// Review 1, F-5 (ve bağlantılı F-6): QA'nın R-05 yeniden üretimi, düzeltmenin kabul testi olarak kalır. Düzeltmeden
// önce havuzdan alınan bağlantı ~60 sn, yeni açılan 15 sn tutuluyordu. PostgreSQL yanıt vermezken /health 2 sn'de 503 döner;
// bu test arka plandaki denetimin de sürenin sonunda bittiğini (bağlantısını kapattığını) sınar. Girişimin
// başlangıcı, iptal sinyalinin geldiği an, açmanın bitişi ve bağlantının bırakılması bir EF bağlantı
// interceptor'ı ile ölçülür (üretim koduna dokunulmaz). Container finally içinde geri alınır.
[Trait("Category", "Integration")]
[Collection(IngestionCollection.Name)]
public sealed class SaglikDenetimiSizintiTestleri(ContainerFixture containers)
{
    private static readonly TimeSpan Butce = TimeSpan.FromSeconds(2);
    private static readonly TimeSpan Pay = TimeSpan.FromSeconds(1);
    private static readonly TimeSpan GozlemSiniri = TimeSpan.FromSeconds(90);

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task F5_Log_deposu_yanit_vermezken_health_denetimi_baglantisini_sure_sonunda_birakir()
    {
        var olcer = new BaglantiOlcer();
        await using var factory = new AIHubFactory(containers)
        {
            ConsumerEnabled = false,
            TestServices = s => s.ConfigureDbContext<AIHubDbContext>(o => o.AddInterceptors(olcer)),
        };
        var istemci = factory.CreateClient();
        Assert.Equal(HttpStatusCode.OK, (await istemci.GetAsync("/health", Ct)).StatusCode);

        const int IstekSayisi = 3;
        List<Girisim> girisimler;
        var yanitSureleri = new List<double>();
        await containers.Postgres.PauseAsync(Ct);
        try
        {
            olcer.Baslat();
            for (var i = 0; i < IstekSayisi; i++)
            {
                var sw = Stopwatch.StartNew();
                var yanit = await istemci.GetAsync("/health", Ct);
                yanitSureleri.Add(sw.Elapsed.TotalSeconds);
                Assert.Equal(HttpStatusCode.ServiceUnavailable, yanit.StatusCode);
            }

            var acikKalan = olcer.Acik();
            // Duraklatma sürerken bağlantıların bırakılması beklenir (en fazla GozlemSiniri).
            var gozlem = Stopwatch.StartNew();
            while (olcer.Acik() > 0 && gozlem.Elapsed < GozlemSiniri)
            {
                await Task.Delay(100, Ct);
            }

            girisimler = olcer.Anlik();
            var cikti = TestContext.Current.TestOutputHelper;
            cikti?.WriteLine($"Yanıt süreleri (sn): {string.Join(", ", yanitSureleri.Select(x => x.ToString("F2")))}");
            cikti?.WriteLine($"Son yanıt anında bağlantı tutan denetim: {acikKalan}; {gozlem.Elapsed.TotalSeconds:F1} sn gözlemden sonra: {olcer.Acik()}");
            foreach (var g in girisimler)
            {
                cikti?.WriteLine(Yaz(g));
            }
        }
        finally
        {
            await containers.Postgres.UnpauseAsync(Ct);
        }

        Assert.Equal(IstekSayisi, girisimler.Count);
        Assert.All(girisimler, g => Assert.True(
            g.BirakmaSn is { } b && b <= (Butce + Pay).TotalSeconds,
            $"Denetimin bağlantısı bütçeden ({Butce.TotalSeconds} sn) sonra da tutuldu — {Yaz(g)}"));
    }

    private static string Yaz(Girisim g) =>
        $"iptal sinyali {Sn(g.IptalSn)}, açma bitişi {Sn(g.AcmaBitisSn)} ({g.AcmaSonucu ?? "-"}), " +
        $"bağlantı bırakıldı {Sn(g.BirakmaSn)}";

    private static string Sn(double? x) => x is { } v ? v.ToString("F2") + " sn" : "YOK";

    private sealed record Girisim(double? IptalSn, double? AcmaBitisSn, string? AcmaSonucu, double? BirakmaSn);

    private sealed class BaglantiOlcer : DbConnectionInterceptor
    {
        private sealed class Kayit
        {
            public readonly Stopwatch Saat = Stopwatch.StartNew();
            public double? IptalSn;
            public double? AcmaBitisSn;
            public string? AcmaSonucu;
            public double? BirakmaSn;
        }

        private readonly ConcurrentDictionary<Guid, Kayit> _kayitlar = new();
        private volatile bool _acik;

        public void Baslat()
        {
            _kayitlar.Clear();
            _acik = true;
        }

        public int Acik() => _kayitlar.Values.Count(k => k.BirakmaSn is null);

        public List<Girisim> Anlik() =>
            _kayitlar.Values.Select(k => new Girisim(k.IptalSn, k.AcmaBitisSn, k.AcmaSonucu, k.BirakmaSn)).ToList();

        public override ValueTask<InterceptionResult> ConnectionOpeningAsync(
            DbConnection connection, ConnectionEventData eventData, InterceptionResult result, CancellationToken cancellationToken = default)
        {
            if (_acik)
            {
                var kayit = new Kayit();
                _kayitlar[eventData.ConnectionId] = kayit;
                cancellationToken.Register(() => kayit.IptalSn ??= kayit.Saat.Elapsed.TotalSeconds);
            }

            return ValueTask.FromResult(result);
        }

        public override Task ConnectionOpenedAsync(DbConnection connection, ConnectionEndEventData eventData, CancellationToken cancellationToken = default)
        {
            AcmaBitti(eventData.ConnectionId, "açıldı");
            return Task.CompletedTask;
        }

        public override Task ConnectionFailedAsync(DbConnection connection, ConnectionErrorEventData eventData, CancellationToken cancellationToken = default)
        {
            AcmaBitti(eventData.ConnectionId, eventData.Exception.GetType().Name);
            return Task.CompletedTask;
        }

        public override Task ConnectionDisposedAsync(DbConnection connection, ConnectionEndEventData eventData)
        {
            if (_kayitlar.TryGetValue(eventData.ConnectionId, out var kayit))
            {
                kayit.BirakmaSn ??= kayit.Saat.Elapsed.TotalSeconds;
            }

            return Task.CompletedTask;
        }

        private void AcmaBitti(Guid id, string sonuc)
        {
            if (_kayitlar.TryGetValue(id, out var kayit) && kayit.AcmaBitisSn is null)
            {
                kayit.AcmaSonucu = sonuc;
                kayit.AcmaBitisSn = kayit.Saat.Elapsed.TotalSeconds;
            }
        }
    }
}
