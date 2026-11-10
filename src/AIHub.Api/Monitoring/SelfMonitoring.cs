using System.Data.Common;
using AIHub.Application.Monitoring;

namespace AIHub.Api.Monitoring;

/// <summary>
/// Açılışta AIHub'ın kendi iki kaydı (Spec 0008, R-4 … R-6). Adresler ortam yapılandırmasındadır; boş adres
/// eklenmez. Hiçbir sonuç açılışı durdurmaz: kurala uymayan adres ve sınır dolu durumu nedeniyle, depo erişilemezse
/// uyarıyla süreç loguna yazılır ve ekleme bir sonraki açılışta yeniden denenir.
/// </summary>
internal static class SelfMonitoring
{
    public const string SectionName = "SelfMonitoring";

    public static async Task EnsureAsync(IServiceProvider services, IConfiguration configuration, ILogger logger)
    {
        var section = configuration.GetSection(SectionName);
        var command = new RegisterSelfMonitoringCommand(section["ApiHealthUrl"], section["PanelHealthUrl"]);
        if (string.IsNullOrWhiteSpace(command.ApiHealthUrl) && string.IsNullOrWhiteSpace(command.PanelHealthUrl))
        {
            return;
        }

        await using var scope = services.CreateAsyncScope();
        IReadOnlyList<SelfRegistration> results;
        try
        {
            results = await scope.ServiceProvider.GetRequiredService<RegisterSelfMonitoringHandler>().HandleAsync(command, CancellationToken.None);
        }
        catch (Exception ex) when (ex is DbException or TimeoutException || ex.InnerException is DbException)
        {
            logger.LogWarning("AIHub'ın kendi izleme kayıtları eklenemedi: depo erişilemiyor. Sonraki açılışta yeniden denenecek.");
            return;
        }

        foreach (var r in results)
        {
            switch (r.Result)
            {
                case SelfRegistrationResult.Added:
                    logger.LogInformation("{ServiceName} izlenen uygulamalara eklendi.", r.ServiceName);
                    break;
                case SelfRegistrationResult.InvalidAddress:
                case SelfRegistrationResult.LimitReached:
                    logger.LogWarning("{ServiceName} izlenen uygulamalara eklenmedi: {Reason}", r.ServiceName, r.Reason);
                    break;
            }
        }
    }
}
