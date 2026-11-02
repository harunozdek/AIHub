using System.Net;
using System.Net.Http.Json;
using AIHub.Api.Tests.Altyapi;
using Npgsql;

namespace AIHub.Api.Tests;

// Review 1, F-01 (R-05 ile QA'nın yeniden ürettiği): eşzamanlı yanlış şifre denemeleri kilidi atlatmaz.
// Spec 0004, R-3 / AC-4: art arda 5 başarısızlıkta hesap 15 dakika kilitlenir. Düzeltmeden önce 8 koşunun
// 6'sında kırmızıydı (sayaç 4, kilit yok, doğru şifre 200).
[Trait("Category", "Integration")]
[Collection(IngestionCollection.Name)]
public sealed class KilitYarisiTestleri(ContainerFixture containers) : IAsyncLifetime
{
    private const string Sifre = "dogru-sifre-123";
    private const int EsZamanliDeneme = 10;

    private AIHubFactory _factory = null!;

    public ValueTask InitializeAsync()
    {
        _factory = new AIHubFactory(containers) { ConsumerEnabled = false };
        _ = _factory.Services;
        return ValueTask.CompletedTask;
    }

    public ValueTask DisposeAsync() => _factory.DisposeAsync();

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task AC4_Eszamanli_on_yanlis_denemeden_sonra_dogru_sifre_reddedilir()
    {
        var ad = "k-" + Guid.NewGuid().ToString("N")[..10];
        var ekle = await _factory.PanelIstemcisi().PostAsJsonAsync("/api/users", new { userName = ad, displayName = ad, password = Sifre }, Ct);
        Assert.Equal(HttpStatusCode.Created, ekle.StatusCode);

        var yanitlar = await Task.WhenAll(Enumerable.Range(0, EsZamanliDeneme).Select(_ =>
            _factory.CreateClient().PostAsJsonAsync("/api/auth/login", new { userName = ad, password = "yanlis-sifre-123" }, Ct)));
        Assert.All(yanitlar, y => Assert.Equal(HttpStatusCode.Unauthorized, y.StatusCode));

        var durum = await DurumAsync(ad);
        var dogru = await _factory.CreateClient().PostAsJsonAsync("/api/auth/login", new { userName = ad, password = Sifre }, Ct);

        Assert.True(
            dogru.StatusCode == HttpStatusCode.Unauthorized,
            $"{EsZamanliDeneme} eşzamanlı yanlış denemeden sonra doğru şifre {(int)dogru.StatusCode} aldı; kilit beklenirdi. DB (doğru girişten önce): {durum}");
    }

    private async Task<string> DurumAsync(string ad)
    {
        await using var ds = NpgsqlDataSource.Create(containers.Postgres.GetConnectionString());
        await using var cmd = ds.CreateCommand("SELECT failed_login_count, locked_until FROM users WHERE user_name = $1");
        cmd.Parameters.AddWithValue(ad);
        await using var r = await cmd.ExecuteReaderAsync(Ct);
        await r.ReadAsync(Ct);
        return $"failed_login_count={r.GetInt32(0)}, locked_until={(r.IsDBNull(1) ? "null" : r.GetFieldValue<DateTimeOffset>(1).ToString("O"))}";
    }
}
