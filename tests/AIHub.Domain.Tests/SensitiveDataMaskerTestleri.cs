using AIHub.Domain.Masking;

namespace AIHub.Domain.Tests;

// Spec 0001: AC-7 (her desen türüyle maskelenir), AC-8 (hassas olmayan metin değişmez).
public class SensitiveDataMaskerTestleri
{
    [Theory]
    [InlineData("Kullanıcı ali.veli@ornek.com.tr giriş yapamadı", "Kullanıcı [MASKED:email] giriş yapamadı", "ali.veli@ornek.com.tr")]
    [InlineData("Müşteri TCKN 10000000146 bulunamadı", "Müşteri TCKN [MASKED:tckn] bulunamadı", "10000000146")]
    [InlineData("kimlik TC10000000146 ve user_11111111110", "kimlik TC[MASKED:tckn] ve user_[MASKED:tckn]", "10000000146")]
    [InlineData("Authorization: Bearer abc.DEF-123_xyz~+/= reddedildi", "Authorization: Bearer [MASKED:bearer-token] reddedildi", "abc.DEF-123_xyz")]
    [InlineData("token eyJhbGciOiJIUzI1NiJ9.eyJzdWIiOiIxIn0.c2lnbmF0dXJl süresi doldu", "token [MASKED:bearer-token] süresi doldu", "eyJhbGciOiJIUzI1NiJ9")]
    [InlineData("Host=db01;Database=odeme;Username=app;Password=S3cr3t!x;Pooling=true", "Host=db01;Database=odeme;Username=app;Password=[MASKED:password];Pooling=true", "S3cr3t!x")]
    [InlineData("Server=db01;Pwd = gizli123", "Server=db01;Pwd = [MASKED:password]", "gizli123")]
    [InlineData("Host=db;Password=\"Gizli1\";Database=x", "Host=db;Password=\"[MASKED:password]\";Database=x", "Gizli1")]
    [InlineData("Host=db;Password='p@ss w0rd;x';Database=x", "Host=db;Password='[MASKED:password]';Database=x", "p@ss w0rd")]
    [InlineData("Host=db;Password=\"a\"\"b;c\";Database=x", "Host=db;Password=\"[MASKED:password]\";Database=x", "b;c")]
    [InlineData("Host=db;Password='a''b;c';Database=x", "Host=db;Password='[MASKED:password]';Database=x", "b;c")]
    [InlineData("baglanti Password=\"abc def;ghi", "baglanti Password=\"[MASKED:password]", "def;ghi")]
    [InlineData("satir1 Password='abc def\nsatir2", "satir1 Password='[MASKED:password]\nsatir2", "abc def")]
    [InlineData("baglanti postgres://app:S3cr3t@db01.ic:5432/odeme kurulamadi", "baglanti postgres://app:[MASKED:password]@db01.ic:5432/odeme kurulamadi", "S3cr3t")]
    [InlineData("istemci aih_Zx81kQ0pLmN4vB7cT2yR anahtariyla geldi", "istemci [MASKED:api-key] anahtariyla geldi", "aih_Zx81kQ0pLmN4vB7cT2yR")]
    [InlineData("cagri api_key=sk_live_9f8e7d6c basarisiz", "cagri api_key=[MASKED:api-key] basarisiz", "sk_live_9f8e7d6c")]
    [InlineData("header X-Api-Key: 0123456789abcdef", "header X-Api-Key: [MASKED:api-key]", "0123456789abcdef")]
    [InlineData("{\"client_secret\":\"s3cr3tv4lue\"}", "{\"client_secret\":\"[MASKED:api-key]\"}", "s3cr3tv4lue")]
    public void Her_hassas_desen_turunu_belirten_yer_tutucuyla_maskelenir_ve_ham_deger_kalmaz(string girdi, string beklenen, string hamDeger)
    {
        var sonuc = SensitiveDataMasker.Mask(girdi);

        Assert.Equal(beklenen, sonuc);
        Assert.DoesNotContain(hamDeger, sonuc);
    }

    [Theory]
    [InlineData("Sipariş 12345678901 kargoya verildi")] // 11 hane ama kontrol hanesi geçersiz
    [InlineData("NullReferenceException at OdemeServisi.Tahsil(Int32 tutar) line 42")]
    [InlineData("Kayıt 3fa85f64-5717-4562-b3fc-2c963f66afa6 işlendi, hash e3b0c44298fc1c149afbf4c8996fb924")]
    [InlineData("Telefon 05321234567 ve 123456789012 (12 hane) değişmez")]
    [InlineData("")]
    public void Hassas_olmayan_metin_ve_gecersiz_TCKN_degismeden_kalir(string girdi)
    {
        Assert.Equal(girdi, SensitiveDataMasker.Mask(girdi));
    }

    [Fact]
    public void Birden_fazla_hassas_deger_ayni_metinde_ayri_ayri_maskelenir()
    {
        var girdi = "a@b.co ve 10000000146 ve Password=x1";

        var sonuc = SensitiveDataMasker.Mask(girdi);

        Assert.Equal("[MASKED:email] ve [MASKED:tckn] ve Password=[MASKED:password]", sonuc);
    }

    [Fact]
    public void Maskeleme_tekrar_uygulandiginda_sonuc_degismez()
    {
        var bir = SensitiveDataMasker.Mask("Bearer abc postgres://u:p@h a@b.co api_key=zz aih_0123456789abcdefXYZ");

        Assert.Equal(bir, SensitiveDataMasker.Mask(bir));
    }

    [Fact]
    public void Buyuk_girdide_dogrusal_surede_tamamlanir()
    {
        // 256 KB'lık, geri izlemeyi kışkırtan girdi (çok sayıda '@' ve '.' içeren uzun bir kelime).
        var girdi = string.Concat(Enumerable.Repeat("a.a@a", 256 * 1024 / 5));

        var sure = System.Diagnostics.Stopwatch.StartNew();
        SensitiveDataMasker.Mask(girdi);
        sure.Stop();

        Assert.True(sure.Elapsed < TimeSpan.FromSeconds(5), $"Maskeleme {sure.Elapsed} sürdü.");
    }

    [Theory]
    [InlineData("password", "[MASKED:password]")]
    [InlineData("API_KEY", "[MASKED:api-key]")]
    [InlineData("Authorization", "[MASKED:bearer-token]")]
    [InlineData("db_password", "[MASKED:password]")]
    [InlineData("webhookSecret", "[MASKED:api-key]")]
    public void Hassas_anahtar_adi_degerin_tamaminin_maskelenecegini_bildirir(string anahtar, string beklenen)
    {
        Assert.True(SensitiveDataMasker.TryGetPlaceholderForKey(anahtar, out var yerTutucu));
        Assert.Equal(beklenen, yerTutucu);
    }

    [Fact]
    public void Hassas_olmayan_anahtar_adi_icin_yer_tutucu_yoktur()
    {
        Assert.False(SensitiveDataMasker.TryGetPlaceholderForKey("siparisNo", out _));
    }
}
