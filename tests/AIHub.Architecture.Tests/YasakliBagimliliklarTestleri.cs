namespace AIHub.Architecture.Tests;

/// <summary>
/// docs/architecture.md, "Forbidden dependencies". Spec 0001: AC-4 (FD-1), AC-5 (FD-2),
/// AC-6 (FD-3). FD-4 ("Api iş mantığı içermez") review ile uygulanır — güvenilir bir otomatik
/// vekil bulunamadı ve yanlış bir testin verdiği güven, testin hiç olmamasından kötü kabul edildi.
/// </summary>
public sealed class YasakliBagimliliklarTestleri
{
    private const string Domain = "src/AIHub.Domain/AIHub.Domain.csproj";
    private const string Application = "src/AIHub.Application/AIHub.Application.csproj";
    private const string Infrastructure = "src/AIHub.Infrastructure/AIHub.Infrastructure.csproj";
    private const string Api = "src/AIHub.Api/AIHub.Api.csproj";

    /// <summary>EF Core / PostgreSQL erişimini ele veren paket adı parçaları (FD-3).</summary>
    private static readonly string[] KaliciligiEleVerenPaketIzleri =
    [
        "EntityFrameworkCore",
        "Npgsql",
    ];

    [Fact]
    public void FD1_Domain_Hicbir_Projeye_Veya_Pakete_Bagimli_Degildir()
    {
        var domain = ProjeDosyasi.Yukle(Domain);

        Assert.Empty(domain.ProjeReferanslari());
        Assert.Empty(domain.PaketReferanslari());
    }

    [Fact]
    public void FD2_Application_Infrastructure_Referansi_Vermez()
    {
        var application = ProjeDosyasi.Yukle(Application);

        Assert.DoesNotContain("AIHub.Infrastructure.csproj", application.ProjeReferanslari());
    }

    [Fact]
    public void FD2_Bagimlilik_Yonu_Iceri_Dogrudur()
    {
        Assert.Equal(["AIHub.Domain.csproj"], ProjeDosyasi.Yukle(Application).ProjeReferanslari());
        Assert.Equal(["AIHub.Application.csproj"], ProjeDosyasi.Yukle(Infrastructure).ProjeReferanslari());

        // Api, Domain'e doğrudan değil, Application üzerinden erişir.
        Assert.DoesNotContain("AIHub.Domain.csproj", ProjeDosyasi.Yukle(Api).ProjeReferanslari());
    }

    [Theory]
    [InlineData(Application)]
    [InlineData(Api)]
    public void FD3_EF_Core_Turleri_Infrastructure_Disina_Cikmaz(string projeYolu)
    {
        var proje = ProjeDosyasi.Yukle(projeYolu);

        foreach (var paket in proje.PaketReferanslari())
        {
            foreach (var iz in KaliciligiEleVerenPaketIzleri)
            {
                Assert.DoesNotContain(iz, paket, StringComparison.OrdinalIgnoreCase);
            }
        }
    }
}
