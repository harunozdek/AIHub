using AIHub.Api.Tests.Altyapi;
using AIHub.Application.Users;
using AIHub.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace AIHub.Api.Tests;

// Spec 0004 — AC-5 (veritabanında açık metin yok), AC-8 (son aktif kullanıcı yarışta da korunur).
[Trait("Category", "Integration")]
[Collection(IngestionCollection.Name)]
public sealed class KullaniciDeposuTestleri(ContainerFixture containers) : IAsyncLifetime
{
    private const string Sifre = "dogru-sifre-123";

    private readonly AIHubFactory _factory = new(containers) { ConsumerEnabled = false };

    public ValueTask InitializeAsync()
    {
        _ = _factory.Services;
        return ValueTask.CompletedTask;
    }

    public ValueTask DisposeAsync() => _factory.DisposeAsync();

    private async Task<Guid> EkleAsync(string ad, string sifre = Sifre)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var sonuc = await scope.ServiceProvider.GetRequiredService<CreateUserHandler>()
            .HandleAsync(new CreateUserCommand(ad, ad, sifre, null), TestContext.Current.CancellationToken);
        Assert.True(sonuc.IsSuccess);
        return sonuc.Value;
    }

    [Fact]
    public async Task Veritabaninda_sifre_acik_metin_olarak_bulunmaz_ayni_sifrenin_saklanan_degerleri_farklidir()
    {
        var ek = Guid.NewGuid().ToString("N")[..8];
        var bir = await EkleAsync($"bir-{ek}");
        var iki = await EkleAsync($"iki-{ek}");

        await using var scope = _factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AIHubDbContext>();
        var satirlar = await db.Database
            .SqlQuery<string>($"SELECT password_hash AS \"Value\" FROM users WHERE id IN ({bir}, {iki})")
            .ToListAsync(TestContext.Current.CancellationToken);
        var tumTablo = await db.Database
            .SqlQuery<string>($"SELECT row_to_json(u)::text AS \"Value\" FROM users u")
            .ToListAsync(TestContext.Current.CancellationToken);

        Assert.Equal(2, satirlar.Count);
        Assert.NotEqual(satirlar[0], satirlar[1]);
        Assert.All(tumTablo, satir => Assert.DoesNotContain(Sifre, satir));
    }

    [Fact]
    public async Task Ayni_ad_buyuk_kucuk_harf_farkiyla_iki_kez_eklenemez()
    {
        var ad = $"Tekil-{Guid.NewGuid().ToString("N")[..8]}";
        await EkleAsync(ad);

        await using var scope = _factory.Services.CreateAsyncScope();
        var sonuc = await scope.ServiceProvider.GetRequiredService<CreateUserHandler>()
            .HandleAsync(new CreateUserCommand(ad.ToUpperInvariant(), "x", Sifre, null), TestContext.Current.CancellationToken);

        Assert.Equal(UserErrors.UserNameTaken, Assert.Single(sonuc.Errors));
    }

    [Fact]
    public async Task Iki_kullanici_ayni_anda_birbirini_pasiflestirirse_en_az_biri_aktif_kalir()
    {
        await using (var scope = _factory.Services.CreateAsyncScope())
        {
            // Bu test yalnızca kendi iki kullanıcısıyla çalışır: önceki testlerin kullanıcıları pasifleşir.
            await scope.ServiceProvider.GetRequiredService<AIHubDbContext>().Database.ExecuteSqlRawAsync("UPDATE users SET is_active = false", TestContext.Current.CancellationToken);
        }

        var ek = Guid.NewGuid().ToString("N")[..8];
        var a = await EkleAsync($"a-{ek}");
        var b = await EkleAsync($"b-{ek}");

        async Task<bool> PasiflestirAsync(Guid hedef, Guid yapan)
        {
            await using var scope = _factory.Services.CreateAsyncScope();
            var sonuc = await scope.ServiceProvider.GetRequiredService<SetUserActiveHandler>()
                .HandleAsync(new SetUserActiveCommand(hedef, false, yapan), TestContext.Current.CancellationToken);
            return sonuc.IsSuccess;
        }

        var sonuclar = await Task.WhenAll(PasiflestirAsync(a, b), PasiflestirAsync(b, a));

        await using var son = _factory.Services.CreateAsyncScope();
        var aktif = await son.ServiceProvider.GetRequiredService<AIHubDbContext>().Users.CountAsync(u => u.IsActive, TestContext.Current.CancellationToken);
        Assert.Equal(1, aktif);
        Assert.Single(sonuclar, s => s);
    }
}
