using AIHub.Domain.Monitoring;

namespace AIHub.Domain.Tests.Monitoring;

// Spec 0005 — R-3, AC-3: adres kuralı.
public class HealthUrlTestleri
{
    [Theory]
    [InlineData("http://odeme.kurum.local/health")]
    [InlineData("https://10.0.0.5:8443/saglik?derin=1")]
    [InlineData("http://localhost:5000/health")]
    [InlineData("http://[::1]:5000/health")]
    public void Http_ve_https_mutlak_adresler_kabul_edilir(string adres)
    {
        var sonuc = HealthUrl.Create(adres);

        Assert.True(sonuc.IsSuccess);
        Assert.Equal(adres, sonuc.Value.Value);
    }

    [Theory]
    [InlineData(null, "healthUrl.required")]
    [InlineData("", "healthUrl.required")]
    [InlineData("ftp://odeme.kurum.local/health", "healthUrl.scheme")]
    [InlineData("file:///etc/passwd", "healthUrl.scheme")]
    [InlineData("/health", "healthUrl.scheme")]
    [InlineData("odeme.kurum.local/health", "healthUrl.scheme")]
    [InlineData("gopher://odeme.kurum.local/", "healthUrl.scheme")]
    [InlineData("http://kullanici:sifre@odeme.kurum.local/health", "healthUrl.credentials")]
    [InlineData("http://kullanici@odeme.kurum.local/health", "healthUrl.credentials")]
    public void Kural_disi_adres_alan_adiyla_reddedilir(string? adres, string kod)
    {
        var sonuc = HealthUrl.Create(adres);

        Assert.False(sonuc.IsSuccess);
        Assert.Equal(kod, Assert.Single(sonuc.Errors).Code);
        Assert.Equal("healthUrl", sonuc.Errors[0].Field);
    }

    [Fact]
    public void Adres_2048_karakterde_kabul_edilir_2049_karakterde_reddedilir()
    {
        const string Bas = "http://odeme.kurum.local/";
        var tam = Bas + new string('a', 2048 - Bas.Length);

        Assert.True(HealthUrl.Create(tam).IsSuccess);
        Assert.Equal("healthUrl.length", Assert.Single(HealthUrl.Create(tam + "a").Errors).Code);
    }
}
