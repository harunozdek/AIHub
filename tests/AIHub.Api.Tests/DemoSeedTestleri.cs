using System.Net.Http.Json;
using System.Text.Json;
using AIHub.Api.Tests.Altyapi;
using AIHub.DemoSeed;
using Microsoft.Extensions.Configuration;
using Npgsql;

namespace AIHub.Api.Tests;

// Spec 0006, R-10 — örnek veri aracı: Development'ta dağılımı yükler ve tekrar koşuda ekler (AC-10); başka
// ortamda hiçbir kayıt yazmadan reddeder (AC-11). Her test kendi boş veritabanında çalışır.
[Trait("Category", "Integration")]
[Collection(IngestionCollection.Name)]
public sealed class DemoSeedTestleri(ContainerFixture containers) : IAsyncLifetime
{
    private readonly List<AIHubFactory> _factoryler = [];

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public ValueTask InitializeAsync() => ValueTask.CompletedTask;

    public async ValueTask DisposeAsync()
    {
        foreach (var factory in _factoryler)
        {
            await factory.DisposeAsync();
        }
    }

    private IConfiguration Ayarlar(string veritabani) => new ConfigurationBuilder()
        .AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["ConnectionStrings:Postgres"] = veritabani,
            ["ConnectionStrings:Redis"] = containers.Redis.GetConnectionString(),
        })
        .Build();

    private static async Task<T> TekDegerAsync<T>(string veritabani, string sql)
    {
        await using var ds = NpgsqlDataSource.Create(veritabani);
        await using var cmd = ds.CreateCommand(sql);
        return (T)(await cmd.ExecuteScalarAsync(Ct))!;
    }

    private static async Task KomutAsync(string veritabani, string sql)
    {
        await using var ds = NpgsqlDataSource.Create(veritabani);
        await using var cmd = ds.CreateCommand(sql);
        await cmd.ExecuteNonQueryAsync(Ct);
    }

    [Fact]
    public async Task Development_ortaminda_dagilim_olusur_dashboard_bolumleri_dolar_ikinci_kosu_ekler()
    {
        var veritabani = await TestVerisi.BosVeritabaniAsync(containers, Ct);
        var cikti = new StringWriter();
        var once = DateTimeOffset.UtcNow;

        Assert.Equal(DemoSeeder.ExitOk, await DemoSeeder.RunAsync("Development", Ayarlar(veritabani), cikti, Ct));

        Assert.Equal((long)DemoVeri.RecordCount, await TekDegerAsync<long>(veritabani, "SELECT count(*) FROM log_entries"));
        Assert.True(await TekDegerAsync<long>(veritabani, "SELECT count(DISTINCT service_name) FROM log_entries WHERE service_name LIKE 'demo-%'") >= 6);
        Assert.Equal(0L, await TekDegerAsync<long>(veritabani, "SELECT count(*) FROM log_entries WHERE service_name NOT LIKE 'demo-%'"));
        Assert.Equal(4L, await TekDegerAsync<long>(veritabani, "SELECT count(DISTINCT level) FROM log_entries"));
        Assert.True(await TekDegerAsync<long>(veritabani, "SELECT count(*) FROM log_entries WHERE exception_type IS NOT NULL AND stack_trace IS NOT NULL") > 0);
        // Son 24 saate yayılır: en eskisi 20 saatten eski, en yenisi 1 saatten yeni, hiçbiri pencerenin dışında değil.
        var enEski = await TekDegerAsync<DateTime>(veritabani, "SELECT min(received_at) FROM log_entries");
        var enYeni = await TekDegerAsync<DateTime>(veritabani, "SELECT max(received_at) FROM log_entries");
        Assert.InRange(new DateTimeOffset(enEski, TimeSpan.Zero), once.AddHours(-24), once.AddHours(-20));
        Assert.InRange(new DateTimeOffset(enYeni, TimeSpan.Zero), once.AddHours(-1), DateTimeOffset.UtcNow);
        // Kabul kuralları uygulanır: mesajdaki parola ve token maskelenir.
        Assert.Equal(0L, await TekDegerAsync<long>(veritabani, "SELECT count(*) FROM log_entries WHERE message LIKE '%Demo123!%' OR message LIKE '%eyJhbGciOi.demo.token%'"));
        Assert.Contains($"{DemoVeri.RecordCount} örnek kayıt yüklendi", cikti.ToString(), StringComparison.Ordinal);

        // Dashboard'un hiçbir log bölümü boş kalmaz.
        var factory = new AIHubFactory(containers) { ConsumerEnabled = false, PostgresConnectionOverride = veritabani };
        _factoryler.Add(factory);
        var ozet = await factory.PanelIstemcisi().GetFromJsonAsync<JsonElement>("/api/panel/dashboard", Ct);
        Assert.All(ozet.GetProperty("levels").EnumerateArray(), l => Assert.True(l.GetProperty("count").GetInt64() > 0));
        Assert.True(ozet.GetProperty("topErrorServices").GetArrayLength() > 0);
        Assert.Equal(5, ozet.GetProperty("topExceptionTypes").GetArrayLength());

        // İkinci koşu yeni kayıtlar ekler; ilk koşunun kayıtları değişmez (anlık görüntüyle birebir karşılaştırılır).
        await KomutAsync(veritabani, "CREATE TABLE ilk_kosu AS SELECT * FROM log_entries");
        Assert.Equal(DemoSeeder.ExitOk, await DemoSeeder.RunAsync("Development", Ayarlar(veritabani), new StringWriter(), Ct));
        Assert.Equal(2L * DemoVeri.RecordCount, await TekDegerAsync<long>(veritabani, "SELECT count(*) FROM log_entries"));
        Assert.Equal((long)DemoVeri.RecordCount, await TekDegerAsync<long>(veritabani,
            "SELECT count(*) FROM ilk_kosu i JOIN log_entries l ON l.id = i.id AND l.service_name = i.service_name AND l.level = i.level AND l.message = i.message AND l.received_at = i.received_at"));
    }

    [Theory]
    [InlineData("Production")]
    [InlineData("Staging")]
    [InlineData("Testing")]
    public async Task Development_disindaki_ortamda_hic_kayit_yazmadan_reddeder(string ortam)
    {
        var veritabani = await TestVerisi.BosVeritabaniAsync(containers, Ct);
        var factory = new AIHubFactory(containers) { ConsumerEnabled = false, PostgresConnectionOverride = veritabani };
        _factoryler.Add(factory);
        _ = factory.Services; // Şema hazır olsun: "hiç kayıt yok" gerçek bir tabloda ölçülür.
        var cikti = new StringWriter();

        var sonuc = await DemoSeeder.RunAsync(ortam, Ayarlar(veritabani), cikti, Ct);

        Assert.Equal(DemoSeeder.ExitRefused, sonuc);
        Assert.Equal(0L, await TekDegerAsync<long>(veritabani, "SELECT count(*) FROM log_entries"));
        Assert.Contains("yalnızca Development", cikti.ToString(), StringComparison.Ordinal);
        Assert.Contains(ortam, cikti.ToString(), StringComparison.Ordinal);
    }
}
