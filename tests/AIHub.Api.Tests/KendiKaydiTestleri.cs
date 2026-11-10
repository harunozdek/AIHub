using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using AIHub.Api.Tests.Altyapi;
using AIHub.Domain.Monitoring;
using Npgsql;

namespace AIHub.Api.Tests;

// Spec 0008 — açılışta AIHub'ın kendi iki kaydı, gerçek veritabanında: ekleme ve "Sistem" (AC-5), boş ve geçersiz
// adres (AC-6), var olan kayda dokunulmaz ve iki örnek aynı anda açılınca kayıt bir kez bulunur (AC-7), silme
// kalıcıdır (AC-8), depo erişilemezken açılış durmaz (R-4). Her test kendi boş veritabanında çalışır.
[Trait("Category", "Integration")]
[Collection(IngestionCollection.Name)]
public sealed class KendiKaydiTestleri(ContainerFixture containers) : IAsyncLifetime
{
    private const string ApiAdresi = "http://localhost:5009/health";
    private const string PanelAdresi = "http://localhost:3000/health";

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

    /// <summary>
    /// Verilen veritabanında, verilen adreslerle açılan bir host. Ayarlar factory'nin ayar sözlüğüyle verilir:
    /// izleme ayarlarının varsayılanları korunur, SelfMonitoring adresleri eklenir.
    /// </summary>
    private AIHubFactory Host(string veritabani, string? api, string? panel, bool migrate = true)
    {
        var ayarlar = new Dictionary<string, string?>(new AIHubFactory(containers).MonitoringSettings)
        {
            ["SelfMonitoring:ApiHealthUrl"] = api,
            ["SelfMonitoring:PanelHealthUrl"] = panel,
        };
        var factory = new AIHubFactory(containers)
        {
            ConsumerEnabled = false,
            MigrateOnStartup = migrate,
            PostgresConnectionOverride = veritabani,
            MonitoringSettings = ayarlar,
        };
        _factoryler.Add(factory);
        return factory;
    }

    private static async Task<JsonElement[]> UygulamalarAsync(AIHubFactory factory)
    {
        var liste = await factory.PanelIstemcisi().GetFromJsonAsync<JsonElement>("/api/monitored-apps", Ct);
        return [.. liste.EnumerateArray()];
    }

    private static JsonElement[] Kendileri(JsonElement[] uygulamalar) =>
        [.. uygulamalar.Where(a => a.GetProperty("serviceName").GetString()!.StartsWith("AIHub.", StringComparison.Ordinal))];

    [Fact]
    public async Task AC5_Bos_veritabaninda_acilista_iki_kayit_Sistem_tarafindan_eklenir()
    {
        var db = await TestVerisi.BosVeritabaniAsync(containers, Ct);

        var kendileri = Kendileri(await UygulamalarAsync(Host(db, ApiAdresi, PanelAdresi)));

        Assert.Equal([SystemActor.ApiServiceName, SystemActor.PanelServiceName], kendileri.Select(a => a.GetProperty("serviceName").GetString()).Order());
        Assert.All(kendileri, a =>
        {
            Assert.Equal("Sistem", a.GetProperty("createdBy").GetString());
            Assert.Equal("Unknown", a.GetProperty("status").GetString());
        });
        Assert.Equal(ApiAdresi, kendileri.Single(a => a.GetProperty("serviceName").GetString() == SystemActor.ApiServiceName).GetProperty("healthUrl").GetString());
    }

    [Fact]
    public async Task AC6_Bos_adres_eklenmez_kurala_uymayan_adres_eklenmez_uygulama_acilir()
    {
        var bos = await TestVerisi.BosVeritabaniAsync(containers, Ct);
        var gecersiz = await TestVerisi.BosVeritabaniAsync(containers, Ct);

        var yalnizPanel = Kendileri(await UygulamalarAsync(Host(bos, "", PanelAdresi)));
        var gecersizApi = Host(gecersiz, "http://169.254.169.254/health", PanelAdresi);
        var gecersizKendileri = Kendileri(await UygulamalarAsync(gecersizApi));

        Assert.Equal([SystemActor.PanelServiceName], yalnizPanel.Select(a => a.GetProperty("serviceName").GetString()));
        Assert.Equal([SystemActor.PanelServiceName], gecersizKendileri.Select(a => a.GetProperty("serviceName").GetString()));
        Assert.Equal(HttpStatusCode.OK, (await gecersizApi.CreateClient().GetAsync("/health", Ct)).StatusCode);
    }

    [Fact]
    public async Task AC7_Ayni_adla_var_olan_kayit_degismez()
    {
        var db = await TestVerisi.BosVeritabaniAsync(containers, Ct);
        _ = Host(db, null, null).CreateClient(); // yalnızca şema
        var ekleyen = Guid.CreateVersion7();
        await using (var ds = NpgsqlDataSource.Create(db))
        {
            await using var cmd = ds.CreateCommand(
                """
                INSERT INTO monitored_apps (id, service_name, health_url, status, consecutive_failures, next_check_at, created_at, created_by)
                VALUES (gen_random_uuid(), 'AIHub.Api', 'http://elle.kurum.local/saglik', 'Down', 3, now(), now() - interval '1 day', $1)
                """);
            cmd.Parameters.AddWithValue(ekleyen);
            await cmd.ExecuteNonQueryAsync(Ct);
        }

        var api = Kendileri(await UygulamalarAsync(Host(db, ApiAdresi, PanelAdresi, migrate: false)))
            .Single(a => a.GetProperty("serviceName").GetString() == SystemActor.ApiServiceName);

        Assert.Equal("http://elle.kurum.local/saglik", api.GetProperty("healthUrl").GetString());
        Assert.Equal("Down", api.GetProperty("status").GetString());
        Assert.NotEqual("Sistem", api.GetProperty("createdBy").GetString());
    }

    [Fact]
    public async Task AC7_Iki_ornek_ayni_anda_acilinca_her_kayit_bir_kez_bulunur()
    {
        var db = await TestVerisi.BosVeritabaniAsync(containers, Ct);
        _ = Host(db, null, null).CreateClient(); // şema; eşzamanlı migration bu testin konusu değil
        var birinci = Host(db, ApiAdresi, PanelAdresi, migrate: false);
        var ikinci = Host(db, ApiAdresi, PanelAdresi, migrate: false);

        await Task.WhenAll(Task.Run(() => birinci.CreateClient(), Ct), Task.Run(() => ikinci.CreateClient(), Ct));

        var kendileri = Kendileri(await UygulamalarAsync(birinci));
        Assert.Equal([SystemActor.ApiServiceName, SystemActor.PanelServiceName], kendileri.Select(a => a.GetProperty("serviceName").GetString()).Order());
    }

    [Fact]
    public async Task AC8_Silinen_kayit_sonraki_acilista_eklenmez()
    {
        var db = await TestVerisi.BosVeritabaniAsync(containers, Ct);
        var ilk = Host(db, ApiAdresi, PanelAdresi);
        var panel = Kendileri(await UygulamalarAsync(ilk)).Single(a => a.GetProperty("serviceName").GetString() == SystemActor.PanelServiceName);
        var silme = await ilk.PanelIstemcisi().DeleteAsync($"/api/monitored-apps/{panel.GetProperty("id").GetString()}", Ct);
        Assert.True(silme.IsSuccessStatusCode, $"Silme {silme.StatusCode} döndü.");

        var kendileri = Kendileri(await UygulamalarAsync(Host(db, ApiAdresi, PanelAdresi, migrate: false)));

        Assert.Equal([SystemActor.ApiServiceName], kendileri.Select(a => a.GetProperty("serviceName").GetString()));
    }

    [Fact]
    public async Task R4_Depo_erisilemezken_acilis_durmaz()
    {
        var yok = $"{containers.Postgres.GetConnectionString()};Database=yok_{Guid.NewGuid():N}";

        var factory = Host(yok, ApiAdresi, PanelAdresi, migrate: false);

        Assert.Equal(HttpStatusCode.ServiceUnavailable, (await factory.CreateClient().GetAsync("/health", Ct)).StatusCode);
    }
}
