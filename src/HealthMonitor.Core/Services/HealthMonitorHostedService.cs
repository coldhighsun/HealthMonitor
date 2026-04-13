using HealthMonitor.Monitors;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace HealthMonitor.Services;

/// <summary>
/// Background service that drives degradation-check ticks for all registered health monitors.
/// </summary>
internal sealed class HealthMonitorHostedService : BackgroundService
{
    private readonly HealthMonitorCoordinator _coordinator;
    private readonly ILogger<HealthMonitorHostedService> _logger;

    public HealthMonitorHostedService(
        HealthMonitorCoordinator coordinator,
        ILogger<HealthMonitorHostedService> logger)
    {
        _coordinator = coordinator;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation(
            "HealthMonitor started with {Count} monitor(s), check loop every {Interval}.",
            _coordinator.Monitors.Count,
            _coordinator.MinCheckInterval);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                _coordinator.TickAll();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unhandled exception during health monitor tick.");
            }

            try
            {
                await Task.Delay(_coordinator.MinCheckInterval, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }

        _logger.LogInformation("HealthMonitor stopped.");
    }
}
