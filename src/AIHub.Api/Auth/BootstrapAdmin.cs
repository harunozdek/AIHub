using System.Data.Common;
using AIHub.Application.Users;

namespace AIHub.Api.Auth;

/// <summary>
/// Açılışta ilk yönetici (Spec 0004, R-6, AC-6). Hiç kullanıcı yokken ve ayarlar eksikken uygulama,
/// eksik ayarın adını söyleyerek başlamaz. Depo o an erişilemiyorsa karar verilemez: açılış durmaz,
/// uyarı yazılır (0002'deki "depo erişilemezse 503" davranışı korunur).
/// </summary>
internal static class BootstrapAdmin
{
    public const string SectionName = "Auth:BootstrapAdmin";

    public static async Task EnsureAsync(IServiceProvider services, IConfiguration configuration, ILogger logger)
    {
        await using var scope = services.CreateAsyncScope();
        var section = configuration.GetSection(SectionName);
        var command = new BootstrapAdminCommand(section["UserName"], section["DisplayName"], section["Password"]);

        try
        {
            var users = scope.ServiceProvider.GetRequiredService<AIHub.Application.Abstractions.IUserRepository>();
            if (await users.AnyAsync(CancellationToken.None))
            {
                return;
            }
        }
        catch (Exception ex) when (ex is DbException or TimeoutException || ex.InnerException is DbException)
        {
            logger.LogWarning("İlk yönetici kontrolü yapılamadı: kullanıcı deposu erişilemiyor.");
            return;
        }

        var missing = new[] { "UserName", "DisplayName", "Password" }.Where(k => string.IsNullOrWhiteSpace(section[k])).ToArray();
        if (missing.Length > 0)
        {
            throw new InvalidOperationException(
                $"Hiç kullanıcı yok ve ilk yönetici ayarları eksik: {string.Join(", ", missing.Select(k => $"{SectionName}:{k}"))}.");
        }

        var result = await scope.ServiceProvider.GetRequiredService<BootstrapAdminHandler>().HandleAsync(command, CancellationToken.None);
        if (!result.IsSuccess)
        {
            // Şifrenin kendisi hiçbir zaman mesaja girmez; yalnızca kural ihlalinin nedeni.
            throw new InvalidOperationException(
                $"İlk yönetici oluşturulamadı ({SectionName}): {string.Join(" ", result.Errors.Select(e => e.Message))}");
        }

        if (result.Value)
        {
            logger.LogInformation("İlk yönetici hesabı oluşturuldu: {UserName}.", command.UserName);
        }
    }
}
