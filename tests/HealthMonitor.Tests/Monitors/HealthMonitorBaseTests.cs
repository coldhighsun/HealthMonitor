using HealthMonitor.Abstractions;
using HealthMonitor.Configuration;
using HealthMonitor.Events;
using HealthMonitor.Monitors;
using HealthMonitor.Tests.Fakes;

namespace HealthMonitor.Tests.Monitors;

public class HealthMonitorBaseTests
{
    private static (
        NamedHealthMonitor monitor,
        FakeSystemTimeProvider clock,
        FakeStopwatch signalWatch,
        FakeStopwatch checkWatch,
        FakeStopwatch stateWatch)
        CreateMonitor(TimeSpan? degradedThreshold = null, TimeSpan? checkInterval = null)
    {
        var clock       = new FakeSystemTimeProvider();
        var signalWatch = new FakeStopwatch();
        var checkWatch  = new FakeStopwatch();
        var stateWatch  = new FakeStopwatch();
        var options = new HealthMonitorOptions
        {
            Name               = "test",
            DegradedThreshold  = degradedThreshold ?? TimeSpan.FromSeconds(30),
            CheckInterval      = checkInterval     ?? TimeSpan.FromSeconds(5),
        };
        var monitor = new NamedHealthMonitor(options, clock, signalWatch, checkWatch, stateWatch);
        return (monitor, clock, signalWatch, checkWatch, stateWatch);
    }

    // ── Initial state ──────────────────────────────────────────────────────────

    [Fact]
    public void InitialState_IsHealthy()
    {
        var (monitor, _, _, _, _) = CreateMonitor();
        Assert.True(monitor.IsHealthy);
    }

    // ── Degraded ───────────────────────────────────────────────────────────────

    [Fact]
    public void Tick_WhenSignalStale_FiresDegraded()
    {
        var (monitor, _, signalWatch, checkWatch, _) = CreateMonitor(degradedThreshold: TimeSpan.FromSeconds(30));

        HealthDegradedEventArgs? args = null;
        ((IHealthMonitor)monitor).Degraded += (_, e) => args = e;

        signalWatch.Advance(TimeSpan.FromSeconds(31)); // signal is stale
        checkWatch.Advance(TimeSpan.FromSeconds(5));   // check interval elapsed
        monitor.Tick();

        Assert.NotNull(args);
        Assert.Equal("test", args.MonitorName);
        Assert.False(monitor.IsHealthy);
    }

    [Fact]
    public void Tick_WhenSignalFresh_DoesNotDegrade()
    {
        var (monitor, _, signalWatch, checkWatch, _) = CreateMonitor(degradedThreshold: TimeSpan.FromSeconds(30));

        var firedCount = 0;
        ((IHealthMonitor)monitor).Degraded += (_, _) => firedCount++;

        signalWatch.Advance(TimeSpan.FromSeconds(10)); // still within threshold
        checkWatch.Advance(TimeSpan.FromSeconds(5));
        monitor.Tick();

        Assert.Equal(0, firedCount);
        Assert.True(monitor.IsHealthy);
    }

    [Fact]
    public void Tick_RespectsCheckInterval_DoesNotFireBeforeInterval()
    {
        var (monitor, _, signalWatch, checkWatch, _) = CreateMonitor(checkInterval: TimeSpan.FromSeconds(10));

        var firedCount = 0;
        ((IHealthMonitor)monitor).Degraded += (_, _) => firedCount++;

        signalWatch.Advance(TimeSpan.FromSeconds(31));
        checkWatch.Advance(TimeSpan.FromSeconds(5)); // less than 10 s check interval
        monitor.Tick();

        Assert.Equal(0, firedCount);
    }

    [Fact]
    public void Tick_DoesNotFireDuplicateDegraded_WhenAlreadyDegraded()
    {
        var (monitor, _, signalWatch, checkWatch, _) = CreateMonitor();

        var firedCount = 0;
        ((IHealthMonitor)monitor).Degraded += (_, _) => firedCount++;

        signalWatch.Advance(TimeSpan.FromSeconds(31));

        checkWatch.Advance(TimeSpan.FromSeconds(5));
        monitor.Tick(); // first transition → fires

        checkWatch.Advance(TimeSpan.FromSeconds(5));
        monitor.Tick(); // already degraded → no new event

        Assert.Equal(1, firedCount);
    }

    [Fact]
    public void Degraded_Args_ContainCorrectHealthyDuration()
    {
        var (monitor, _, signalWatch, checkWatch, stateWatch) = CreateMonitor();

        HealthDegradedEventArgs? args = null;
        ((IHealthMonitor)monitor).Degraded += (_, e) => args = e;

        stateWatch.Advance(TimeSpan.FromMinutes(10)); // was healthy for 10 min
        signalWatch.Advance(TimeSpan.FromSeconds(31));
        checkWatch.Advance(TimeSpan.FromSeconds(5));
        monitor.Tick();

        Assert.NotNull(args);
        Assert.Equal(TimeSpan.FromMinutes(10), args.HealthyDuration);
    }

    [Fact]
    public void Degraded_Args_Timestamp_UsesWallClock()
    {
        var (monitor, clock, signalWatch, checkWatch, _) = CreateMonitor();

        HealthDegradedEventArgs? args = null;
        ((IHealthMonitor)monitor).Degraded += (_, e) => args = e;

        var expectedTime = clock.UtcNow;
        signalWatch.Advance(TimeSpan.FromSeconds(31));
        checkWatch.Advance(TimeSpan.FromSeconds(5));
        monitor.Tick();

        Assert.NotNull(args);
        Assert.Equal(expectedTime, args.Timestamp);
    }

    // ── Signal / Recovered ─────────────────────────────────────────────────────

    [Fact]
    public void Signal_WhenHealthy_ResetsTimer_DoesNotFireRecovered()
    {
        var (monitor, _, signalWatch, _, _) = CreateMonitor();

        var firedCount = 0;
        ((IHealthMonitor)monitor).Recovered += (_, _) => firedCount++;

        signalWatch.Advance(TimeSpan.FromSeconds(10));
        ((IHealthMonitor)monitor).Signal();

        Assert.Equal(0, firedCount);
        Assert.True(monitor.IsHealthy);
    }

    [Fact]
    public void Signal_WhenDegraded_FiresRecoveredImmediately()
    {
        var (monitor, _, signalWatch, checkWatch, _) = CreateMonitor();

        HealthRecoveredEventArgs? recovered = null;
        ((IHealthMonitor)monitor).Recovered += (_, e) => recovered = e;

        // Degrade first
        signalWatch.Advance(TimeSpan.FromSeconds(31));
        checkWatch.Advance(TimeSpan.FromSeconds(5));
        monitor.Tick();
        Assert.False(monitor.IsHealthy);

        // Signal arrives — should recover immediately, no Tick needed
        ((IHealthMonitor)monitor).Signal();

        Assert.NotNull(recovered);
        Assert.True(monitor.IsHealthy);
    }

    [Fact]
    public void Recovered_Args_ContainCorrectDegradedDuration()
    {
        var (monitor, _, signalWatch, checkWatch, stateWatch) = CreateMonitor();

        HealthRecoveredEventArgs? recovered = null;
        ((IHealthMonitor)monitor).Recovered += (_, e) => recovered = e;

        // Degrade
        signalWatch.Advance(TimeSpan.FromSeconds(31));
        checkWatch.Advance(TimeSpan.FromSeconds(5));
        monitor.Tick(); // stateWatch restarted here

        // Simulate 2 minutes of degraded time, then signal arrives
        stateWatch.Advance(TimeSpan.FromMinutes(2));
        ((IHealthMonitor)monitor).Signal();

        Assert.NotNull(recovered);
        Assert.Equal(TimeSpan.FromMinutes(2), recovered.DegradedDuration);
    }

    [Fact]
    public void Signal_AfterRecovery_ResetsSignalTimer_PreventsImmediateReDegradation()
    {
        var (monitor, _, signalWatch, checkWatch, _) = CreateMonitor(degradedThreshold: TimeSpan.FromSeconds(30));

        // Degrade → recover
        signalWatch.Advance(TimeSpan.FromSeconds(31));
        checkWatch.Advance(TimeSpan.FromSeconds(5));
        monitor.Tick();
        ((IHealthMonitor)monitor).Signal(); // signal resets signalWatch to 0
        Assert.True(monitor.IsHealthy);

        // A short time later — should still be healthy
        signalWatch.Advance(TimeSpan.FromSeconds(10));
        checkWatch.Advance(TimeSpan.FromSeconds(5));

        var degradedCount = 0;
        ((IHealthMonitor)monitor).Degraded += (_, _) => degradedCount++;
        monitor.Tick();

        Assert.Equal(0, degradedCount);
    }
}
