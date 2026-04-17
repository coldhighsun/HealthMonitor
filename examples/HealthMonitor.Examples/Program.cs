using HealthMonitor.Core.Abstractions;
using HealthMonitor.Core.Extensions;
using HealthMonitor.Core.Monitors;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Serilog;

// ─────────────────────────────────────────────────────────────────
// HealthMonitor — usage examples
//
// Run with:  dotnet run --project examples/HealthMonitor.Examples
//
// Demo 1 – DI-based monitors (quote feeds)
//   Two monitors are registered at startup. A background task drives
//   Signal() calls; the slow feed stalls intentionally so you can see
//   Degraded / Recovered events.
//
// Demo 2 – DynamicHealthMonitorManager (runtime registration)
//   A manager is created standalone (no DI). Monitors are added,
//   signalled, and removed while the app runs — each uses its own
//   Timer and per-monitor options.
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
        // ── Demo 1: DI-based monitors ──────────────────────────────

        // Expects a signal at least every 3 seconds.
        services.AddHealthMonitor("fast-feed", opt =>
        {
            opt.DegradedThreshold = TimeSpan.FromSeconds(3);
            opt.CheckInterval = TimeSpan.FromSeconds(1);
        });

        // Expects a signal at least every 10 seconds.
        services.AddHealthMonitor("slow-feed", opt =>
        {
            opt.DegradedThreshold = TimeSpan.FromSeconds(10);
            opt.CheckInterval = TimeSpan.FromSeconds(2);
        });

        services.AddHostedService<HealthEventLogger>();
        services.AddHostedService<QuoteFeedSimulator>();
        services.AddHostedService<StatusPrinter>();

        // ── Demo 2: DynamicHealthMonitorManager ────────────────────
        services.AddHostedService<DynamicMonitorDemo>();
    })
    .Build();

await host.RunAsync();

// ─────────────────────────────────────────────────────────────────
// Demo 1 helpers
// ─────────────────────────────────────────────────────────────────

// ─────────────────────────────────────────────────────────────────
// Demo 2 – DynamicHealthMonitorManager
//
// Subscribes Degraded / Recovered on the manager itself — events from
// all monitors (including those added later) are forwarded centrally.
//
// Timeline (approximate):
//   t=0s   "api-gateway" and "worker" added and start receiving signals
//   t=15s  "cache" added dynamically — its events flow through manager too
//   t=20s  "worker" stops signalling — will degrade after 8 s
//   t=35s  "worker" resumes — fires Recovered
//   t=40s  "cache" removed — its timer and event forwarding disposed
// ─────────────────────────────────────────────────────────────────
internal sealed class DynamicMonitorDemo(ILogger<DynamicMonitorDemo> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation(new('─', 60));
        logger.LogInformation("Demo 2 (DynamicHealthMonitorManager) starting...");

        using var manager = new DynamicHealthMonitorManager();

        // Subscribe once on the manager — covers all current and future monitors
        manager.Degraded += (sender, e) =>
            logger.LogWarning("[Dynamic/{Name}] *** DEGRADED ***  (was healthy for {Duration:mm\\:ss})",
                e.MonitorName, e.HealthyDuration);
        manager.Recovered += (sender, e) =>
            logger.LogInformation("[Dynamic/{Name}] *** RECOVERED *** (was degraded for {Duration:mm\\:ss})",
                e.MonitorName, e.DegradedDuration);

        // Add two monitors at startup
        var gateway = manager.Add("api-gateway", new()
        {
            DegradedThreshold = TimeSpan.FromSeconds(6),
            CheckInterval = TimeSpan.FromSeconds(2),
        });

        var worker = manager.Add("worker", new()
        {
            DegradedThreshold = TimeSpan.FromSeconds(8),
            CheckInterval = TimeSpan.FromSeconds(2),
        });

        _ = SignalLoop(gateway, TimeSpan.FromSeconds(3), stoppingToken);

        var workerSignalling = true;
        _ = Task.Run(async () =>
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                if (workerSignalling)
                    worker.Signal();
                await Task.Delay(TimeSpan.FromSeconds(3), stoppingToken).ConfigureAwait(false);
            }
        }, stoppingToken);

        // t=15s: add "cache" dynamically — manager events fire for it automatically
        await Task.Delay(TimeSpan.FromSeconds(15), stoppingToken).ConfigureAwait(false);
        if (stoppingToken.IsCancellationRequested)
            return;

        var cache = manager.Add("cache", new()
        {
            DegradedThreshold = TimeSpan.FromSeconds(5),
            CheckInterval = TimeSpan.FromSeconds(1),
        });
        _ = SignalLoop(cache, TimeSpan.FromSeconds(2), stoppingToken);
        logger.LogInformation("[Dynamic] 'cache' added at runtime. Active: {Names}",
            string.Join(", ", manager.Monitors.Select(m => m.Name)));

        // t=20s: pause worker signals → will degrade
        await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken).ConfigureAwait(false);
        if (stoppingToken.IsCancellationRequested)
            return;

        workerSignalling = false;
        logger.LogInformation("[Dynamic] 'worker' signals paused — expect Degraded in ~8 s");

        // t=35s: resume worker → fires Recovered
        await Task.Delay(TimeSpan.FromSeconds(15), stoppingToken).ConfigureAwait(false);
        if (stoppingToken.IsCancellationRequested)
            return;

        workerSignalling = true;
        worker.Signal();
        logger.LogInformation("[Dynamic] 'worker' signals resumed — expect Recovered");

        // t=40s: remove cache — timer and event forwarding disposed
        await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken).ConfigureAwait(false);
        if (stoppingToken.IsCancellationRequested)
            return;

        manager.Remove("cache");
        logger.LogInformation("[Dynamic] 'cache' removed. Active: {Names}",
            string.Join(", ", manager.Monitors.Select(m => m.Name)));

        await Task.Delay(Timeout.InfiniteTimeSpan, stoppingToken).ConfigureAwait(false);
    }

    private static async Task SignalLoop(IHealthMonitor monitor, TimeSpan interval, CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            monitor.Signal();
            await Task.Delay(interval, ct).ConfigureAwait(false);
        }
    }
}

internal sealed class HealthEventLogger(IEnumerable<IHealthMonitor> monitors, ILogger<HealthEventLogger> logger)
    : IHostedService
{
    public Task StartAsync(CancellationToken cancellationToken)
    {
        foreach (var monitor in monitors)
        {
            var name = monitor.Name;
            monitor.Degraded += (_, e) =>
                logger.LogWarning("[DI/{Monitor}] *** DEGRADED ***  (was healthy for {Duration:mm\\:ss})",
                    name, e.HealthyDuration);

            monitor.Recovered += (_, e) =>
                logger.LogInformation("[DI/{Monitor}] *** RECOVERED *** (was degraded for {Duration:mm\\:ss})",
                    name, e.DegradedDuration);
        }

        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}

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
        }
    }
}

internal sealed class StatusPrinter(IEnumerable<IHealthMonitor> monitors, ILogger<StatusPrinter> logger)
    : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("Demo 1 (DI-based) running. Watching quote feeds...");
        logger.LogInformation(new('─', 60));

        while (!stoppingToken.IsCancellationRequested)
        {
            var parts = monitors.Select(m =>
                $"{m.Name}: {(m.IsHealthy ? "HEALTHY" : "DEGRADED")}");

            logger.LogInformation("[DI status] {Parts}", string.Join(" | ", parts));

            await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken).ConfigureAwait(false);
        }
    }
}