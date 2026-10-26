using System.Xml.Linq;

namespace AIHub.Architecture.Tests;

/// <summary>
/// Bir `.csproj` dosyasının bağımlılıklarını okur.
/// <para>
/// Neden derlenmiş assembly yerine proje dosyası (plan 0001, R-2): C# derleyicisi, bir
/// assembly'den hiç tip kullanılmadığında o referansı derlenmiş metadata'dan ELER. Assembly
/// tabanlı bir mimari test bu yüzden ihlali sessizce kaçırabilir — `Application`'a
/// `Infrastructure` referansı eklenip hiç tip kullanılmazsa test yeşil kalır. Proje dosyasını
/// okumak derleyici davranışına bağlı değildir ve boş projelerde de anlamlıdır.
/// </para>
/// </summary>
internal sealed class ProjeDosyasi
{
    private readonly XDocument _belge;

    public string Ad { get; }

    private ProjeDosyasi(string ad, XDocument belge)
    {
        Ad = ad;
        _belge = belge;
    }

    public static ProjeDosyasi Yukle(string depoYoluIcindekiYol)
    {
        var tamYol = Path.Combine(DepoKoku.Bul(), depoYoluIcindekiYol);
        Assert.True(File.Exists(tamYol), $"Proje dosyası bulunamadı: {depoYoluIcindekiYol}");

        return new ProjeDosyasi(Path.GetFileName(tamYol), XDocument.Load(tamYol));
    }

    /// <summary>Bu projenin referans verdiği diğer projelerin dosya adları.</summary>
    public IReadOnlyList<string> ProjeReferanslari() =>
        [.. Dugumler("ProjectReference").Select(yol => Path.GetFileName(yol.Replace('\\', '/')))];

    /// <summary>Bu projenin NuGet paket referanslarının adları.</summary>
    public IReadOnlyList<string> PaketReferanslari() => [.. Dugumler("PackageReference")];

    private IEnumerable<string> Dugumler(string dugumAdi) =>
        _belge.Descendants(dugumAdi)
            .Select(dugum => dugum.Attribute("Include")?.Value)
            .Where(deger => !string.IsNullOrWhiteSpace(deger))
            .Select(deger => deger!);
}
