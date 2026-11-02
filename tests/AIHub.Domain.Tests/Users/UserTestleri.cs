using AIHub.Domain.Users;

namespace AIHub.Domain.Tests.Users;

public class UserTestleri
{
    private static readonly DateTimeOffset Simdi = new(2026, 11, 2, 9, 0, 0, TimeSpan.Zero);

    private static User YeniKullanici(string ad = "ayse.yilmaz") =>
        User.Create(Guid.CreateVersion7(), UserName.Create(ad).Value, "Ayşe Yılmaz", "hash", Simdi, null).Value;

    [Theory]
    [InlineData("abc")]
    [InlineData("ayse.yilmaz")]
    [InlineData("A_b-c.9")]
    public void Kullanici_adi_izin_verilen_karakter_ve_uzunlukta_kabul_edilir(string ad)
    {
        Assert.True(UserName.Create(ad).IsSuccess);
        Assert.True(UserName.Create(new string('a', UserLimits.UserNameMaxLength)).IsSuccess);
    }

    [Theory]
    [InlineData(null, "userName.length")]
    [InlineData("", "userName.length")]
    [InlineData("ab", "userName.length")]
    [InlineData("ayşe", "userName.characters")]
    [InlineData("İsmail", "userName.characters")]
    [InlineData("ayse yilmaz", "userName.characters")]
    [InlineData("ayse@kurum", "userName.characters")]
    public void Kullanici_adi_kisa_bos_veya_izin_disi_karakterliyse_alan_adiyla_reddedilir(string? ad, string kod)
    {
        var sonuc = UserName.Create(ad);

        Assert.False(sonuc.IsSuccess);
        Assert.Equal(kod, Assert.Single(sonuc.Errors).Code);
        Assert.Equal("userName", sonuc.Errors[0].Field);
    }

    [Fact]
    public void Kullanici_adi_64_karakteri_asarsa_reddedilir()
    {
        Assert.False(UserName.Create(new string('a', UserLimits.UserNameMaxLength + 1)).IsSuccess);
    }

    [Fact]
    public void Kullanici_adi_buyuk_kucuk_harften_bagimsiz_ayni_normalize_degere_iner()
    {
        Assert.Equal(UserName.Create("Ayse.Yilmaz").Value.Normalized, UserName.Create("ayse.YILMAZ").Value.Normalized);
        Assert.Equal("Ayse.Yilmaz", UserName.Create("Ayse.Yilmaz").Value.Value);
    }

    [Theory]
    [InlineData(11, false)]
    [InlineData(12, true)]
    [InlineData(128, true)]
    [InlineData(129, false)]
    public void Sifre_12_ile_128_karakter_arasinda_olmalidir(int uzunluk, bool gecerli)
    {
        var hata = PasswordPolicy.Validate(new string('x', uzunluk));

        Assert.Equal(gecerli, hata is null);
        if (!gecerli)
        {
            Assert.Equal("password", hata!.Field);
        }
    }

    [Fact]
    public void Gorunen_ad_bos_veya_100_karakterden_uzunsa_reddedilir()
    {
        var ad = UserName.Create("ayse").Value;

        Assert.False(User.Create(Guid.CreateVersion7(), ad, " ", "h", Simdi, null).IsSuccess);
        Assert.False(User.Create(Guid.CreateVersion7(), ad, new string('a', 101), "h", Simdi, null).IsSuccess);
        Assert.True(User.Create(Guid.CreateVersion7(), ad, new string('a', 100), "h", Simdi, null).IsSuccess);
    }

    [Fact]
    public void Art_arda_bes_basarisiz_denemede_hesap_15_dakika_kilitlenir_ve_sure_dolunca_acilir()
    {
        var kullanici = YeniKullanici();

        for (var i = 0; i < 4; i++)
        {
            kullanici.RecordFailedLogin(Simdi);
        }

        Assert.False(kullanici.IsLockedAt(Simdi));
        kullanici.RecordFailedLogin(Simdi);

        Assert.True(kullanici.IsLockedAt(Simdi));
        Assert.True(kullanici.IsLockedAt(Simdi.AddMinutes(15).AddTicks(-1)));
        Assert.False(kullanici.IsLockedAt(Simdi.AddMinutes(15)));
    }

    [Fact]
    public void Basarili_giris_sayaci_sifirlar_dort_yanlis_bir_dogru_dort_yanlis_kilitlemez()
    {
        var kullanici = YeniKullanici();

        for (var i = 0; i < 4; i++)
        {
            kullanici.RecordFailedLogin(Simdi);
        }

        kullanici.RecordSuccessfulLogin(Simdi);
        for (var i = 0; i < 4; i++)
        {
            kullanici.RecordFailedLogin(Simdi);
        }

        Assert.False(kullanici.IsLockedAt(Simdi));
        Assert.Equal(Simdi, kullanici.LastLoginAt);
    }

    [Fact]
    public void Kilitliyken_yapilan_denemeler_kilidi_uzatmaz()
    {
        var kullanici = YeniKullanici();
        for (var i = 0; i < 5; i++)
        {
            kullanici.RecordFailedLogin(Simdi);
        }

        kullanici.RecordFailedLogin(Simdi.AddMinutes(14));

        Assert.False(kullanici.IsLockedAt(Simdi.AddMinutes(15)));
    }

    [Fact]
    public void Pasiflestirme_ve_sifre_degisikligi_guvenlik_damgasini_yeniler()
    {
        var kullanici = YeniKullanici();
        var ilk = kullanici.SecurityStamp;

        kullanici.ChangePassword("yeni-hash");
        var ikinci = kullanici.SecurityStamp;
        kullanici.Deactivate(Guid.CreateVersion7(), Simdi);

        Assert.NotEqual(ilk, ikinci);
        Assert.NotEqual(ikinci, kullanici.SecurityStamp);
        Assert.False(kullanici.IsActive);
        Assert.Equal("yeni-hash", kullanici.PasswordHash);
    }

    [Fact]
    public void Yeniden_etkinlestirilen_kullanicinin_pasiflestirme_kaydi_temizlenir()
    {
        var kullanici = YeniKullanici();
        kullanici.Deactivate(Guid.CreateVersion7(), Simdi);

        kullanici.Activate();

        Assert.True(kullanici.IsActive);
        Assert.Null(kullanici.DeactivatedAt);
        Assert.Null(kullanici.DeactivatedBy);
    }
}
