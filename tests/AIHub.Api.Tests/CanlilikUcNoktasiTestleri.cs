using System.Net;
using Microsoft.AspNetCore.Mvc.Testing;

namespace AIHub.Api.Tests;

/// <summary>
/// Spec 0001, AC-7. Süreç içi çalışır: gerçek ağ yoktur ve fabrika xUnit tarafından dispose
/// edilir — koşu sonunda arkada süreç kalmaz (docs/testing.md, Resource hygiene).
/// </summary>
public sealed class CanlilikUcNoktasiTestleri : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _fabrika;

    public CanlilikUcNoktasiTestleri(WebApplicationFactory<Program> fabrika)
    {
        _fabrika = fabrika;
    }

    [Fact]
    public async Task Canlilik_UcNoktasi_KimlikDogrulamasiz_200_Doner()
    {
        using var istemci = _fabrika.CreateClient();

        var yanit = await istemci.GetAsync("/health");

        Assert.Equal(HttpStatusCode.OK, yanit.StatusCode);
    }

    [Fact]
    public async Task Canlilik_Yaniti_Ortam_Surum_Veya_Bagimlilik_Bilgisi_Sizdirmaz()
    {
        using var istemci = _fabrika.CreateClient();

        var yanit = await istemci.GetAsync("/health");
        var govde = await yanit.Content.ReadAsStringAsync();

        // Gövde sabittir: başka hiçbir şey sızamayacak kadar dar.
        Assert.Equal("Healthy", govde);

        // Sabitin kendisi değişirse yakalanacak açık yasaklar (docs/security.md, Input & output).
        string[] sizmamasiGerekenler =
        [
            "Development", "Production", "Staging",   // ortam adı
            "net10", "10.0", "Version",               // sürüm
            "Postgres", "Npgsql", "Redis",            // bağımlılık
        ];

        foreach (var yasak in sizmamasiGerekenler)
        {
            Assert.DoesNotContain(yasak, govde, StringComparison.OrdinalIgnoreCase);
        }
    }
}
