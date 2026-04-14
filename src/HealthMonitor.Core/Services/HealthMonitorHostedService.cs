using HealthMonitor.Core.Monitors;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace HealthMonitor.Core.Services;

/// <summary>
/// Background service that drives degradation-check ticks for all registered health monitors.
/// </summary>
/// <param name="coordinator">Injected coordinator that manages all monitors and their check intervals.</param>
/// <param name="logger">Injected logger for logging monitor ticks and errors.</param>
internal sealed class HealthMonitorHostedService(
    HealthMonitorCoordinator coordinator,
    ILogger<HealthMonitorHostedService> logger)
    : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation(
            "HealthMonitor started with {Count} monitor(s), check loop every {Interval}.",
            coordinator.Monitors.Count,
            coordinator.MinCheckInterval);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                coordinator.TickAll();
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Unhandled exception during health monitor tick.");
            }

            try
            {
                await Task.Delay(coordinator.MinCheckInterval, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }

        logger.LogInformation("HealthMonitor stopped.");
    }
}