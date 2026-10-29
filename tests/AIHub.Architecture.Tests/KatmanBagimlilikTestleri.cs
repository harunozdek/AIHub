using System.Reflection;

namespace AIHub.Architecture.Tests;

// docs/architecture.md, Forbidden dependencies: FD-1 … FD-4.
// Derlenmiş assembly'nin referans listesine bakılır: kodda gerçekten kullanılan her tür buraya düşer.
public class KatmanBagimlilikTestleri
{
    private static readonly string[] EfCoreVeSurucu = ["Microsoft.EntityFrameworkCore", "Npgsql"];

    private static string[] Referanslar(string assemblyAdi) =>
        Assembly.Load(assemblyAdi).GetReferencedAssemblies().Select(a => a.Name ?? "").ToArray();

    [Fact]
    public void FD1_Domain_hicbir_katmana_ve_dis_pakete_bagimli_degildir()
    {
        var referanslar = Referanslar("AIHub.Domain");

        var ihlaller = referanslar
            .Where(r => !(r.StartsWith("System", StringComparison.Ordinal) || r is "netstandard" or "mscorlib"))
            .ToArray();

        Assert.Empty(ihlaller);
    }

    [Fact]
    public void FD2_Application_Infrastructure_ve_Api_katmanlarini_referans_etmez()
    {
        var referanslar = Referanslar("AIHub.Application");

        Assert.DoesNotContain("AIHub.Infrastructure", referanslar);
        Assert.DoesNotContain("AIHub.Api", referanslar);
        Assert.DoesNotContain(referanslar, r => r.StartsWith("Microsoft.AspNetCore", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData("AIHub.Application")]
    [InlineData("AIHub.Api")]
    public void FD3_EF_Core_turleri_yalnizca_Infrastructure_icinde_gecer(string assemblyAdi)
    {
        var referanslar = Referanslar(assemblyAdi);

        Assert.DoesNotContain(referanslar, r => EfCoreVeSurucu.Any(ef => r.StartsWith(ef, StringComparison.Ordinal)));
    }

    [Fact]
    public void FD4_Api_altyapiya_dogrudan_erismez_yalnizca_DI_ile_baglar()
    {
        var referanslar = Referanslar("AIHub.Api");

        // Api kuyruk, depo veya maskeleme kararını kendisi vermez: Redis'e doğrudan dokunmaz ve
        // Domain'in maskeleme türlerini kullanmaz — bunlar Application'ın işidir.
        Assert.DoesNotContain("StackExchange.Redis", referanslar);
        var maskelemeKullananTurler = Assembly.Load("AIHub.Api").GetTypes()
            .Where(t => t.GetFields(BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic)
                .Any(f => f.FieldType.Namespace == "AIHub.Domain.Masking"))
            .ToArray();
        Assert.Empty(maskelemeKullananTurler);
    }

    [Fact]
    public void Okuma_deposu_yazma_metodu_icermez()
    {
        // Panel salt okunurdur (Spec 0002, R-7): okuma arayüzünde yalnızca okuma metodları vardır ve
        // uygulaması yazma deposunu (ILogEntryRepository) da uygulamaz.
        var okuma = Assembly.Load("AIHub.Application").GetType("AIHub.Application.Abstractions.ILogEntryReader", throwOnError: true)!;
        var yazmaAdlari = okuma.GetMethods()
            .Select(m => m.Name)
            .Where(ad => !(ad.StartsWith("List", StringComparison.Ordinal) || ad.StartsWith("Find", StringComparison.Ordinal)))
            .ToArray();
        Assert.Empty(yazmaAdlari);

        var uygulamalar = Assembly.Load("AIHub.Infrastructure").GetTypes().Where(t => t.IsClass && okuma.IsAssignableFrom(t)).ToArray();
        Assert.NotEmpty(uygulamalar);
        Assert.All(uygulamalar, t => Assert.DoesNotContain(t.GetInterfaces(), i => i.Name == "ILogEntryRepository"));
    }
}
