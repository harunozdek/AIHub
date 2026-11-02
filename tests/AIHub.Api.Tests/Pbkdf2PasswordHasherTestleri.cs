using AIHub.Infrastructure.Security;
using Microsoft.Extensions.Options;

namespace AIHub.Api.Tests;

// Spec 0004, R-4, AC-5: şifre yalnızca yavaş ve tuzlu bir hash olarak saklanır.
public class Pbkdf2PasswordHasherTestleri
{
    private static Pbkdf2PasswordHasher Hasher(int tur = 1_000) =>
        new(Options.Create(new PasswordHashOptions { Iterations = tur }));

    [Fact]
    public void Dogru_sifre_dogrulanir_yanlis_sifre_reddedilir()
    {
        var hasher = Hasher();
        var hash = hasher.Hash("dogru-sifre-123");

        Assert.True(hasher.Verify("dogru-sifre-123", hash));
        Assert.False(hasher.Verify("dogru-sifre-124", hash));
        Assert.False(hasher.Verify("DOGRU-SIFRE-123", hash));
    }

    [Fact]
    public void Ayni_sifrenin_iki_hashi_farklidir_ve_sifreyi_icermez()
    {
        var hasher = Hasher();

        var bir = hasher.Hash("dogru-sifre-123");
        var iki = hasher.Hash("dogru-sifre-123");

        Assert.NotEqual(bir, iki);
        Assert.DoesNotContain("dogru-sifre-123", bir);
    }

    [Fact]
    public void Tur_sayisi_hashte_saklanir_tur_degisse_de_eski_hash_dogrulanir()
    {
        var eski = Hasher(1_000).Hash("dogru-sifre-123");

        Assert.StartsWith("v1$1000$", eski);
        Assert.True(Hasher(2_000).Verify("dogru-sifre-123", eski));
    }

    [Theory]
    [InlineData("")]
    [InlineData("dogru-sifre-123")]
    [InlineData("v2$1000$AAAA$AAAA")]
    [InlineData("v1$abc$AAAA$AAAA")]
    [InlineData("v1$1000$!!!$AAAA")]
    [InlineData("v1$0$AAAA$AAAA")]
    public void Taninmayan_bicimdeki_hash_reddedilir_istisna_firlatmaz(string hash)
    {
        Assert.False(Hasher().Verify("dogru-sifre-123", hash));
    }

    [Fact]
    public void Uretim_varsayilani_600_bin_turdur()
    {
        Assert.Equal(600_000, new PasswordHashOptions().Iterations);
        Assert.StartsWith("v1$600000$", new Pbkdf2PasswordHasher(Options.Create(new PasswordHashOptions())).Hash("dogru-sifre-123"));
    }
}
