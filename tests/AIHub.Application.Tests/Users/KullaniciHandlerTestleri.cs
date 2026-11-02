using AIHub.Application.Users;

namespace AIHub.Application.Tests.Users;

public class KullaniciHandlerTestleri
{
    private const string Sifre = "dogru-sifre-123";

    private static readonly DateTimeOffset Simdi = new(2026, 11, 2, 9, 0, 0, TimeSpan.Zero);

    private readonly SahteKullaniciDeposu _depo = new();
    private readonly SahteHasher _hasher = new();
    private readonly AyarlanabilirSaat _saat = new(Simdi);

    private LoginHandler Giris() => new(_depo, _hasher, new SahteTokenUretici(), _saat);

    private Task<AIHub.Domain.Common.Result<LoginResult>> GirisYap(string ad, string sifre) =>
        Giris().HandleAsync(new LoginCommand(ad, sifre), CancellationToken.None);

    [Fact]
    public async Task Dogru_sifreyle_buyuk_kucuk_harften_bagimsiz_giris_yapilir_ve_8_saatlik_token_doner()
    {
        var ayse = _depo.Ekle("ayse.yilmaz");

        var sonuc = await GirisYap("Ayse.YILMAZ", Sifre);

        Assert.True(sonuc.IsSuccess);
        Assert.Equal(ayse.Id, sonuc.Value.UserId);
        Assert.Equal(Simdi.AddHours(8), sonuc.Value.Token.ExpiresAt);
        Assert.Equal(Simdi, ayse.LastLoginAt);
    }

    [Fact]
    public async Task Yanlis_sifre_olmayan_kullanici_pasif_ve_kilitli_hesap_ayni_hatayi_alir()
    {
        _depo.Ekle("ayse");
        _depo.Ekle("pasif", aktif: false);
        var kilitli = _depo.Ekle("kilitli");
        for (var i = 0; i < 5; i++)
        {
            kilitli.RecordFailedLogin(Simdi);
        }

        var sonuclar = new[]
        {
            await GirisYap("ayse", "yanlis-sifre-123"),
            await GirisYap("olmayan", Sifre),
            await GirisYap("pasif", Sifre),
            await GirisYap("kilitli", Sifre),
        };

        Assert.All(sonuclar, s => Assert.Equal(UserErrors.InvalidCredentials, Assert.Single(s.Errors)));
    }

    [Fact]
    public async Task Olmayan_kullanici_icin_de_hash_dogrulamasi_yapilir()
    {
        await GirisYap("olmayan", Sifre);

        Assert.Equal(1, _hasher.VerifyCount);
    }

    [Fact]
    public async Task Bes_yanlis_denemeden_sonra_dogru_sifre_15_dakika_reddedilir_sonra_kabul_edilir()
    {
        _depo.Ekle("ayse");
        for (var i = 0; i < 5; i++)
        {
            Assert.False((await GirisYap("ayse", "yanlis-sifre-123")).IsSuccess);
        }

        _saat.UtcNow = Simdi.AddMinutes(14).AddSeconds(59);
        Assert.False((await GirisYap("ayse", Sifre)).IsSuccess);

        _saat.UtcNow = Simdi.AddMinutes(15);
        Assert.True((await GirisYap("ayse", Sifre)).IsSuccess);
    }

    [Fact]
    public async Task Dort_yanlis_bir_dogru_dort_yanlis_hesabi_kilitlemez()
    {
        _depo.Ekle("ayse");
        for (var i = 0; i < 4; i++)
        {
            await GirisYap("ayse", "yanlis-sifre-123");
        }

        Assert.True((await GirisYap("ayse", Sifre)).IsSuccess);
        for (var i = 0; i < 4; i++)
        {
            await GirisYap("ayse", "yanlis-sifre-123");
        }

        Assert.True((await GirisYap("ayse", Sifre)).IsSuccess);
    }

    [Fact]
    public async Task Sinirdan_uzun_sifre_hashlenmeden_reddedilir()
    {
        _depo.Ekle("ayse");

        var sonuc = await GirisYap("ayse", new string('x', 129));

        Assert.Equal(UserErrors.InvalidCredentials, Assert.Single(sonuc.Errors));
        Assert.Equal(0, _hasher.VerifyCount);
    }

    [Theory]
    [InlineData("ab", "dogru-sifre-123", "userName")]
    [InlineData("ayşe", "dogru-sifre-123", "userName")]
    [InlineData("ayse", "kisa-sifre1", "password")]
    public async Task Kullanici_ekleme_kurala_uymayan_alani_alan_adiyla_reddeder(string ad, string sifre, string alan)
    {
        var sonuc = await new CreateUserHandler(_depo, _hasher, _saat)
            .HandleAsync(new CreateUserCommand(ad, "Ayşe", sifre, null), CancellationToken.None);

        Assert.Contains(sonuc.Errors, e => e.Field == alan);
        Assert.Empty(_depo.Users);
    }

    [Fact]
    public async Task Alinmis_kullanici_adi_buyuk_kucuk_harf_farkiyla_da_reddedilir()
    {
        _depo.Ekle("ayse.yilmaz");

        var sonuc = await new CreateUserHandler(_depo, _hasher, _saat)
            .HandleAsync(new CreateUserCommand("AYSE.Yilmaz", "Başka Ayşe", Sifre, null), CancellationToken.None);

        Assert.Equal(UserErrors.UserNameTaken, Assert.Single(sonuc.Errors));
    }

    [Fact]
    public async Task Eklenen_kullanici_baslangic_sifresiyle_giris_yapar_ve_ekleyen_kaydedilir()
    {
        var ekleyen = _depo.Ekle("yonetici");

        var sonuc = await new CreateUserHandler(_depo, _hasher, _saat)
            .HandleAsync(new CreateUserCommand("mehmet", "Mehmet Kaya", Sifre, ekleyen.Id), CancellationToken.None);

        Assert.True(sonuc.IsSuccess);
        Assert.True((await GirisYap("mehmet", Sifre)).IsSuccess);
        var liste = await new ListUsersHandler(_depo).HandleAsync(CancellationToken.None);
        var mehmet = Assert.Single(liste, u => u.UserName == "mehmet");
        Assert.Equal("yonetici", mehmet.CreatedBy);
        Assert.Equal(Simdi, mehmet.CreatedAt);
    }

    [Fact]
    public async Task Kullanici_kendini_pasiflestiremez()
    {
        var ayse = _depo.Ekle("ayse");
        _depo.Ekle("mehmet");

        var sonuc = await new SetUserActiveHandler(_depo, _saat)
            .HandleAsync(new SetUserActiveCommand(ayse.Id, false, ayse.Id), CancellationToken.None);

        Assert.Equal(UserErrors.CannotDeactivateSelf, Assert.Single(sonuc.Errors));
        Assert.True(ayse.IsActive);
    }

    [Fact]
    public async Task Son_aktif_kullanici_pasiflestirilemez()
    {
        var ayse = _depo.Ekle("ayse");
        var pasif = _depo.Ekle("pasif", aktif: false);

        // Pasif bir kullanıcının oturumu kalmış olsa bile son aktif kullanıcıyı kapatamaz.
        var sonuc = await new SetUserActiveHandler(_depo, _saat)
            .HandleAsync(new SetUserActiveCommand(ayse.Id, false, pasif.Id), CancellationToken.None);

        Assert.Equal(UserErrors.CannotDeactivateLastActive, Assert.Single(sonuc.Errors));
        Assert.True(ayse.IsActive);
    }

    [Fact]
    public async Task Pasiflestirilen_kullanici_giris_yapamaz_ve_eski_oturumu_gecersiz_olur_etkinlestirince_giris_yapar()
    {
        var ayse = _depo.Ekle("ayse");
        var mehmet = _depo.Ekle("mehmet");
        var dogrulayici = new PanelSessionValidator(_depo);
        var eskiDamga = mehmet.SecurityStamp;
        var handler = new SetUserActiveHandler(_depo, _saat);

        Assert.True((await handler.HandleAsync(new SetUserActiveCommand(mehmet.Id, false, ayse.Id), CancellationToken.None)).IsSuccess);

        Assert.False(await dogrulayici.IsValidAsync(mehmet.Id, eskiDamga, CancellationToken.None));
        Assert.False((await GirisYap("mehmet", Sifre)).IsSuccess);
        Assert.Equal(ayse.Id, mehmet.DeactivatedBy);

        Assert.True((await handler.HandleAsync(new SetUserActiveCommand(mehmet.Id, true, ayse.Id), CancellationToken.None)).IsSuccess);
        Assert.True((await GirisYap("mehmet", Sifre)).IsSuccess);
    }

    [Fact]
    public async Task Sifre_degisince_eski_sifre_ve_eski_oturumlar_gecersiz_yeni_token_gecerli()
    {
        var ayse = _depo.Ekle("ayse");
        var dogrulayici = new PanelSessionValidator(_depo);
        var eskiDamga = ayse.SecurityStamp;

        var sonuc = await new ChangePasswordHandler(_depo, _hasher, new SahteTokenUretici(), _saat)
            .HandleAsync(new ChangePasswordCommand(ayse.Id, Sifre, "yepyeni-sifre-456"), CancellationToken.None);

        Assert.True(sonuc.IsSuccess);
        Assert.False(await dogrulayici.IsValidAsync(ayse.Id, eskiDamga, CancellationToken.None));
        Assert.True(await dogrulayici.IsValidAsync(ayse.Id, ayse.SecurityStamp, CancellationToken.None));
        Assert.EndsWith(ayse.SecurityStamp.ToString(), sonuc.Value.Token);
        Assert.False((await GirisYap("ayse", Sifre)).IsSuccess);
        Assert.True((await GirisYap("ayse", "yepyeni-sifre-456")).IsSuccess);
    }

    [Theory]
    [InlineData("yanlis-sifre-123", "yepyeni-sifre-456", "currentPassword")]
    [InlineData(Sifre, "kisa", "newPassword")]
    public async Task Sifre_degisikligi_mevcut_sifre_yanlissa_veya_yeni_sifre_kurala_uymuyorsa_yapilmaz(string mevcut, string yeni, string alan)
    {
        var ayse = _depo.Ekle("ayse");
        var damga = ayse.SecurityStamp;

        var sonuc = await new ChangePasswordHandler(_depo, _hasher, new SahteTokenUretici(), _saat)
            .HandleAsync(new ChangePasswordCommand(ayse.Id, mevcut, yeni), CancellationToken.None);

        Assert.Equal(alan, Assert.Single(sonuc.Errors).Field);
        Assert.Equal(damga, ayse.SecurityStamp);
        Assert.True((await GirisYap("ayse", Sifre)).IsSuccess);
    }

    [Fact]
    public async Task Ilk_yonetici_yalnizca_hic_kullanici_yokken_olusur_varken_hicbir_sey_degismez()
    {
        var handler = new BootstrapAdminHandler(_depo, new CreateUserHandler(_depo, _hasher, _saat));
        var komut = new BootstrapAdminCommand("admin", "Yönetici", Sifre);

        Assert.True((await handler.HandleAsync(komut, CancellationToken.None)).Value);
        var admin = Assert.Single(_depo.Users);
        var hash = admin.PasswordHash;

        Assert.False((await handler.HandleAsync(komut with { Password = "baska-sifre-789" }, CancellationToken.None)).Value);
        Assert.Single(_depo.Users);
        Assert.Equal(hash, admin.PasswordHash);
        Assert.Null(admin.CreatedBy);
    }
}
