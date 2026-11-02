using AIHub.Application.Abstractions;
using AIHub.Application.Users;
using AIHub.Domain.Users;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;

namespace AIHub.Api.Tests.Altyapi;

/// <summary>Uygulamayı süreç içinde, gerçek container'lara bağlı olarak ayağa kaldırır.</summary>
public sealed class AIHubFactory(ContainerFixture containers) : WebApplicationFactory<Program>
{
    public const string BootstrapUserName = "ilk-yonetici";
    public const string BootstrapPassword = "ilk-yonetici-sifresi";
    public const string PanelTestUserName = "panel-testi";
    public const string PanelTestPassword = "panel-testi-sifresi";

    private User? _panelUser;

    public string ConsumerName { get; init; } = "test-" + Guid.NewGuid().ToString("N");

    public bool ConsumerEnabled { get; init; } = true;

    public bool MigrateOnStartup { get; init; } = true;

    /// <summary>
    /// Sağlık denetimi worker'ı (Spec 0005). Varsayılan kapalı: testler aynı veritabanını paylaşır ve bir
    /// testin eklediği uygulamayı başka testin worker'ı denetlememeli.
    /// </summary>
    public bool MonitoringEnabled { get; init; }

    /// <summary>Testlerde kısa denetim aralığı ve zaman aşımı; varsayılanlar ayrı bir testte doğrulanır (plan 0005, risk 1, 2).</summary>
    public IReadOnlyDictionary<string, string?> MonitoringSettings { get; init; } = new Dictionary<string, string?>
    {
        ["Monitoring:TickInterval"] = "00:00:00.200",
        ["Monitoring:CheckInterval"] = "00:00:00.500",
        ["Monitoring:Timeout"] = "00:00:01",
    };

    public string? PostgresConnectionOverride { get; init; }

    public string? RedisConnectionOverride { get; init; }

    /// <summary>Boş bırakılırsa ilgili ilk yönetici ayarı verilmez (Spec 0004, AC-6).</summary>
    public IReadOnlyDictionary<string, string?> AuthSettings { get; init; } = new Dictionary<string, string?>
    {
        ["Auth:TokenSigningKey"] = TestToken.SigningKey,
        ["Auth:BootstrapAdmin:UserName"] = BootstrapUserName,
        ["Auth:BootstrapAdmin:DisplayName"] = "İlk Yönetici",
        ["Auth:BootstrapAdmin:Password"] = BootstrapPassword,
    };

    public Action<IServiceCollection>? TestServices { get; init; }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.UseSetting("ConnectionStrings:Postgres", PostgresConnectionOverride ?? containers.Postgres.GetConnectionString());
        builder.UseSetting("ConnectionStrings:Redis", RedisConnectionOverride ?? containers.Redis.GetConnectionString());
        builder.UseSetting("Database:MigrateOnStartup", MigrateOnStartup.ToString());
        builder.UseSetting("Ingestion:ConsumerName", ConsumerName);
        builder.UseSetting("Ingestion:ConsumerEnabled", ConsumerEnabled.ToString());
        builder.UseSetting("Ingestion:PollInterval", "00:00:00.050");
        builder.UseSetting("Ingestion:MaxRetryDelay", "00:00:00.500");
        // Testlerde hız için düşük tur; üretim değeri ayrı bir testte doğrulanır (plan 0004, risk 6).
        builder.UseSetting("Auth:PasswordHash:Iterations", "1000");
        builder.UseSetting("Monitoring:Enabled", MonitoringEnabled.ToString());
        foreach (var (key, value) in MonitoringSettings)
        {
            builder.UseSetting(key, value);
        }

        foreach (var (key, value) in AuthSettings)
        {
            builder.UseSetting(key, value);
        }

        if (TestServices is not null)
        {
            builder.ConfigureTestServices(TestServices);
        }
    }

    /// <summary>
    /// Panel uçlarını çağıran testlerin kullanıcısı: depoda yoksa eklenir, varsa güncel hali okunur
    /// (damgası token'a girer). Bütün testler aynı adı paylaşır; pasifleştiren testler kendi kullanıcısını kullanır.
    /// </summary>
    public User PanelKullanicisi()
    {
        if (_panelUser is not null)
        {
            return _panelUser;
        }

        using var scope = Services.CreateScope();
        var users = scope.ServiceProvider.GetRequiredService<IUserRepository>();
        var normalized = UserName.Normalize(PanelTestUserName);
        var user = users.FindByNormalizedNameAsync(normalized, CancellationToken.None).GetAwaiter().GetResult();
        if (user is null)
        {
            scope.ServiceProvider.GetRequiredService<CreateUserHandler>()
                .HandleAsync(new CreateUserCommand(PanelTestUserName, "Panel Testi", PanelTestPassword, null), CancellationToken.None)
                .GetAwaiter().GetResult();
            user = users.FindByNormalizedNameAsync(normalized, CancellationToken.None).GetAwaiter().GetResult()!;
        }

        return _panelUser = user;
    }
}
