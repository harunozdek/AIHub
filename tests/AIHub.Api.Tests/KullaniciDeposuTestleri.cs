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
        // Kural bütün kullanıcı tablosuna bakar: test kendi boş veritabanında çalışır, ortak veriyi bozmaz.
        await using var factory = new AIHubFactory(containers)
        {
            ConsumerEnabled = false,
            PostgresConnectionOverride = await TestVerisi.BosVeritabaniAsync(containers, TestContext.Current.CancellationToken),
        };

        async Task<Guid> EkleAsync(string ad)
        {
            await using var scope = factory.Services.CreateAsyncScope();
            var sonuc = await scope.ServiceProvider.GetRequiredService<CreateUserHandler>()
                .HandleAsync(new CreateUserCommand(ad, ad, Sifre, null), TestContext.Current.CancellationToken);
            return sonuc.Value;
        }

        async Task<bool> PasiflestirAsync(Guid hedef, Guid yapan)
        {
            await using var scope = factory.Services.CreateAsyncScope();
            var sonuc = await scope.ServiceProvider.GetRequiredService<SetUserActiveHandler>()
                .HandleAsync(new SetUserActiveCommand(hedef, false, yapan), TestContext.Current.CancellationToken);
            return sonuc.IsSuccess;
        }

        var a = await EkleAsync("yarisan-a");
        var b = await EkleAsync("yarisan-b");
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            // Açılışta oluşan ilk yönetici pasifleşir: aktif yalnızca a ve b kalır.
            var yonetici = (await scope.ServiceProvider.GetRequiredService<ListUsersHandler>().HandleAsync(TestContext.Current.CancellationToken))
                .Single(u => u.UserName == AIHubFactory.BootstrapUserName);
            Assert.True(await PasiflestirAsync(yonetici.Id, a));
        }

        async Task<int> AktifSayisiAsync()
        {
            await using var scope = factory.Services.CreateAsyncScope();
            return await scope.ServiceProvider.GetRequiredService<AIHubDbContext>().Users.CountAsync(u => u.IsActive, TestContext.Current.CancellationToken);
        }

        // Bağlantı havuzu önceden ısıtılır: soğuk havuzda bağlantı açılışı iki çağrıyı kendiliğinden sıraya
        // sokar ve yarış hiç oluşmaz. Yarış birkaç tur tekrarlanır; kilit olmadan turların biri bozulur.
        await Task.WhenAll(Enumerable.Range(0, 4).Select(_ => AktifSayisiAsync()));
        for (var tur = 0; tur < 5; tur++)
        {
            await using (var scope = factory.Services.CreateAsyncScope())
            {
                foreach (var id in new[] { a, b })
                {
                    await scope.ServiceProvider.GetRequiredService<SetUserActiveHandler>()
                        .HandleAsync(new SetUserActiveCommand(id, true, a), TestContext.Current.CancellationToken);
                }
            }

            var sonuclar = await Task.WhenAll(PasiflestirAsync(a, b), PasiflestirAsync(b, a));

            Assert.Equal(1, await AktifSayisiAsync());
            Assert.Single(sonuclar, s => s);
        }
    }
}
