using ApexApp.Infrastructure.Services;

namespace ApexApp.Api.Workers;

/// <summary>
/// Periodic reconciliation for time-based automatic badges: NEW expiry, HIT
/// rolling windows and LOW_STOCK changes that happened outside API requests.
/// </summary>
public sealed class BadgeRecalculationWorker(
    IServiceScopeFactory scopeFactory,
    IConfiguration configuration,
    ILogger<BadgeRecalculationWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var minutes = configuration.GetValue<int?>(
            "Badges:RecalculationIntervalMinutes") ?? 15;

        if (minutes <= 0)
        {
            minutes = 15;
        }

        var interval = TimeSpan.FromMinutes(minutes);

        try
        {
            await Task.Delay(TimeSpan.FromSeconds(10), stoppingToken);
        }
        catch (OperationCanceledException)
        {
            return;
        }

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await using var scope = scopeFactory.CreateAsyncScope();

                var maintenance = scope.ServiceProvider
                    .GetRequiredService<IBadgeMaintenanceService>();

                await maintenance.RecalculateAllAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception exception)
            {
                logger.LogError(
                    exception,
                    "Periodic badge recalculation failed");
            }

            try
            {
                await Task.Delay(interval, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                return;
            }
        }
    }
}
