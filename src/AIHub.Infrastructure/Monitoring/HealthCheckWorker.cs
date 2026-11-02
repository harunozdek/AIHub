using AIHub.Application.Monitoring;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace AIHub.Infrastructure.Monitoring;

/// <summary>
/// Arka plan denetimi (Spec 0005, R-4, AC-7): panelde oturum açık olmasa da her turda zamanı gelen
/// uygulamaları denetler. Durum veritabanındadır; worker bellekte durum tutmaz, yeniden başlatmada
/// kaybolan bir şey olmaz. Depo erişilemezse tur atlanır ve sonraki turda yeniden denenir.
/// </summary>
internal sealed class HealthCheckWorker(
    IServiceScopeFactory scopeFactory,
    IOptions<MonitoringOptions> options,
    ILogger<HealthCheckWorker> logger) : BackgroundService
{
    private readonly MonitoringOptions _options = options.Value;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(_options.TickInterval);
        do
        {
            try
            {
                await using var scope = scopeFactory.CreateAsyncScope();
                await scope.ServiceProvider.GetRequiredService<RunHealthChecksHandler>()
                    .HandleAsync(_options.CheckInterval, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Sağlık denetimi turu tamamlanamadı; sonraki turda yeniden denenecek.");
            }
        }
        while (await timer.WaitForNextTickAsync(stoppingToken));
    }
}
