using HealthMonitor.Abstractions;
using HealthMonitor.Extensions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Serilog;

// ─────────────────────────────────────────────────────────────────
// HealthMonitor — usage example: quote feed health
//
// Run with:  dotnet run --project examples/ActivityMonitor.Examples
//
// Simulates two quote feeds. A background task sends periodic
// "quotes" for the fast feed but intentionally stalls the slow feed
// so you can observe Degraded / Recovered events.
//
// Press Ctrl+C to exit.
// ─────────────────────────────────────────────────────────────────

Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Information()
    .WriteTo.Console()
    .CreateLogger();

var host = Host.CreateDefaultBuilder(args)
    .UseSerilog()
    .ConfigureServices(services =>
    {
        // ── Monitor 1: fast feed ───────────────────────────────────
        // Expects a signal at least every 3 seconds.
        services.AddHealthMonitor("fast-feed", opt =>
        {
            opt.DegradedThreshold = TimeSpan.FromSeconds(3);
            opt.CheckInterval = TimeSpan.FromSeconds(1);
        });

        // ── Monitor 2: slow feed ───────────────────────────────────
        // Expects a signal at least every 10 seconds.
        services.AddHealthMonitor("slow-feed", opt =>
        {
            opt.DegradedThreshold = TimeSpan.FromSeconds(10);
            opt.CheckInterval = TimeSpan.FromSeconds(2);
        });

        services.AddHostedService<HealthEventLogger>();
        services.AddHostedService<QuoteFeedSimulator>();
        services.AddHostedService<StatusPrinter>();
    })
    .Build();

await host.RunAsync();

// ─────────────────────────────────────────────────────────────────
// HealthEventLogger — subscribes to Degraded / Recovered events.
// ─────────────────────────────────────────────────────────────────
internal sealed class HealthEventLogger(IEnumerable<IHealthMonitor> monitors, ILogger<HealthEventLogger> logger)
    : IHostedService
{
    public Task StartAsync(CancellationToken cancellationToken)
    {
        foreach (var monitor in monitors)
        {
            var name = monitor.Name;
            monitor.Degraded += (_, e) =>
                logger.LogWarning("[{Monitor}] *** DEGRADED ***  (was healthy for {Duration:mm\\:ss})",
                    name, e.HealthyDuration);

            monitor.Recovered += (_, e) =>
                logger.LogInformation("[{Monitor}] *** RECOVERED *** (was degraded for {Duration:mm\\:ss})",
                    name, e.DegradedDuration);
        }

        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}

// ─────────────────────────────────────────────────────────────────
// QuoteFeedSimulator — drives Signal() calls to simulate quote feeds.
//
// fast-feed: sends a signal every 1 second (healthy).
// slow-feed: sends a signal every 8 seconds, then intentionally
//            stalls for 15 seconds to trigger a Degraded event,
//            then resumes to trigger Recovered.
// ─────────────────────────────────────────────────────────────────
internal sealed class QuoteFeedSimulator : BackgroundService
{
    private readonly IHealthMonitor _fastFeed;
    private readonly IHealthMonitor _slowFeed;

    public QuoteFeedSimulator(IEnumerable<IHealthMonitor> monitors)
    {
        _fastFeed = monitors.Single(m => m.Name == "fast-feed");
        _slowFeed = monitors.Single(m => m.Name == "slow-feed");
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Run both feed simulations concurrently
        await Task.WhenAll(
            SimulateFastFeed(stoppingToken),
            SimulateSlowFeed(stoppingToken));
    }

    private async Task SimulateFastFeed(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            _fastFeed.Signal();
            await Task.Delay(TimeSpan.FromSeconds(1), ct).ConfigureAwait(false);
        }
    }

    private async Task SimulateSlowFeed(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            // Normal phase: signal every 8 s for 24 s
            for (var i = 0; i < 3 && !ct.IsCancellationRequested; i++)
            {
                _slowFeed.Signal();
                await Task.Delay(TimeSpan.FromSeconds(8), ct).ConfigureAwait(false);
            }

            // Stall phase: no signal for 15 s → triggers Degraded
            await Task.Delay(TimeSpan.FromSeconds(15), ct).ConfigureAwait(false);

            // Resume: first signal → triggers Recovered immediately
        }
    }
}

// ─────────────────────────────────────────────────────────────────
// StatusPrinter — prints current health state every second.
// ─────────────────────────────────────────────────────────────────
internal sealed class StatusPrinter(IEnumerable<IHealthMonitor> monitors, ILogger<StatusPrinter> logger)
    : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("HealthMonitor running. Watching quote feeds...");
        logger.LogInformation(new('─', 60));

        while (!stoppingToken.IsCancellationRequested)
        {
            var parts = monitors.Select(m =>
                $"{m.Name}: {(m.IsHealthy ? "HEALTHY" : "DEGRADED")}");

            logger.LogInformation("{Parts}", parts);

            await Task.Delay(TimeSpan.FromSeconds(1), stoppingToken).ConfigureAwait(false);
        }
    }
}