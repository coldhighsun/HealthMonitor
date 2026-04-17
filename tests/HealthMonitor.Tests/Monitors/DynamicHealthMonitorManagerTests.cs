using HealthMonitor.Core.Configuration;
using HealthMonitor.Core.Events;
using HealthMonitor.Core.Monitors;
using HealthMonitor.Tests.Fakes;

namespace HealthMonitor.Tests.Monitors;

public class DynamicHealthMonitorManagerTests
{
    [Fact]
    public void Add_AfterDispose_ThrowsObjectDisposedException()
    {
        var manager = new DynamicHealthMonitorManager();
        manager.Dispose();

        Assert.Throws<ObjectDisposedException>(() => manager.Add("svc"));
    }

    [Fact]
    public void Add_AppliesProvidedOptions()
    {
        using var manager = new DynamicHealthMonitorManager();
        var opts = new HealthMonitorOptions
        {
            DegradedThreshold = TimeSpan.FromMinutes(5),
            CheckInterval = TimeSpan.FromSeconds(10),
        };

        // Just verify it doesn't throw and name is applied
        var monitor = manager.Add("svc", opts);
        Assert.Equal("svc", monitor.Name);
    }

    [Fact]
    public void Add_DuplicateName_IsCaseInsensitive()
    {
        using var manager = new DynamicHealthMonitorManager();
        manager.Add("SVC");

        Assert.Throws<ArgumentException>(() => manager.Add("svc"));
    }

    [Fact]
    public void Add_DuplicateName_ThrowsArgumentException()
    {
        using var manager = new DynamicHealthMonitorManager();
        manager.Add("svc");

        Assert.Throws<ArgumentException>(() => manager.Add("svc"));
    }

    [Fact]
    public void Add_MonitorIsInitiallyHealthy()
    {
        using var manager = new DynamicHealthMonitorManager();

        var monitor = manager.Add("svc");

        Assert.True(monitor.IsHealthy);
    }

    [Fact]
    public void Add_ReturnsMonitorWithCorrectName()
    {
        using var manager = new DynamicHealthMonitorManager();

        var monitor = manager.Add("svc");

        Assert.Equal("svc", monitor.Name);
    }

    [Fact]
    public void Dispose_CalledTwice_DoesNotThrow()
    {
        var manager = new DynamicHealthMonitorManager();
        manager.Dispose();

        var ex = Record.Exception(manager.Dispose);
        Assert.Null(ex);
    }

    [Fact]
    public async Task ManagerDegraded_FiresForLaterAddedMonitor()
    {
        using var manager = new DynamicHealthMonitorManager(new FakeSystemTimeProvider());

        HealthDegradedEventArgs? captured = null;
        // Subscribe BEFORE adding the monitor
        manager.Degraded += (_, e) => captured = e;

        manager.Add("late", new HealthMonitorOptions
        {
            DegradedThreshold = TimeSpan.FromMilliseconds(100),
            CheckInterval = TimeSpan.FromMilliseconds(50),
        });

        await PollUntil(() => captured is not null, timeout: TimeSpan.FromSeconds(5));

        Assert.NotNull(captured);
        Assert.Equal("late", captured.MonitorName);
    }

    [Fact]
    public async Task ManagerDegraded_FiresWhenMonitorDegrades()
    {
        using var manager = new DynamicHealthMonitorManager(new FakeSystemTimeProvider());

        HealthDegradedEventArgs? captured = null;
        manager.Degraded += (_, e) => captured = e;

        manager.Add("svc", new HealthMonitorOptions
        {
            DegradedThreshold = TimeSpan.FromMilliseconds(100),
            CheckInterval = TimeSpan.FromMilliseconds(50),
        });

        // Wait long enough for the real Timer + stopwatch to fire degradation
        await PollUntil(() => captured is not null, timeout: TimeSpan.FromSeconds(5));

        Assert.NotNull(captured);
        Assert.Equal("svc", captured.MonitorName);
    }

    [Fact]
    public async Task ManagerRecovered_FiresWhenMonitorRecovers()
    {
        using var manager = new DynamicHealthMonitorManager(new FakeSystemTimeProvider());

        HealthDegradedEventArgs? degraded = null;
        HealthRecoveredEventArgs? recovered = null;
        manager.Degraded += (_, e) => degraded = e;
        manager.Recovered += (_, e) => recovered = e;

        var monitor = manager.Add("svc", new HealthMonitorOptions
        {
            DegradedThreshold = TimeSpan.FromMilliseconds(100),
            CheckInterval = TimeSpan.FromMilliseconds(50),
        });

        // Wait for degradation first
        await PollUntil(() => degraded is not null, timeout: TimeSpan.FromSeconds(5));

        // Signal to recover
        monitor.Signal();

        await PollUntil(() => recovered is not null, timeout: TimeSpan.FromSeconds(2));

        Assert.NotNull(recovered);
        Assert.Equal("svc", recovered.MonitorName);
    }

    [Fact]
    public async Task ManagerSender_IsTheIndividualMonitor()
    {
        using var manager = new DynamicHealthMonitorManager(new FakeSystemTimeProvider());

        object? capturedSender = null;
        manager.Degraded += (sender, _) => capturedSender = sender;

        var monitor = manager.Add("svc", new HealthMonitorOptions
        {
            DegradedThreshold = TimeSpan.FromMilliseconds(100),
            CheckInterval = TimeSpan.FromMilliseconds(50),
        });

        await PollUntil(() => capturedSender is not null, timeout: TimeSpan.FromSeconds(5));

        Assert.Same(monitor, capturedSender);
    }

    [Fact]
    public void Monitors_IsEmptyAfterDispose()
    {
        var manager = new DynamicHealthMonitorManager();
        manager.Add("a");
        manager.Add("b");

        manager.Dispose();

        Assert.Empty(manager.Monitors);
    }

    [Fact]
    public void Monitors_ReflectsAllAddedMonitors()
    {
        using var manager = new DynamicHealthMonitorManager();
        manager.Add("a");
        manager.Add("b");
        manager.Add("c");

        var names = manager.Monitors.Select(m => m.Name).ToHashSet();

        Assert.Equal(["a", "b", "c"], names);
    }

    [Fact]
    public void Remove_ExistingMonitor_ReturnsTrue()
    {
        using var manager = new DynamicHealthMonitorManager();
        manager.Add("svc");

        Assert.True(manager.Remove("svc"));
    }

    [Fact]
    public void Remove_IsCaseInsensitive()
    {
        using var manager = new DynamicHealthMonitorManager();
        manager.Add("SVC");

        Assert.True(manager.Remove("svc"));
        Assert.Null(manager.TryGet("SVC"));
    }

    [Fact]
    public void Remove_MonitorDisappearsFromMonitorsList()
    {
        using var manager = new DynamicHealthMonitorManager();
        manager.Add("a");
        manager.Add("b");

        manager.Remove("a");

        var names = manager.Monitors.Select(m => m.Name).ToList();
        Assert.DoesNotContain("a", names);
        Assert.Contains("b", names);
    }

    [Fact]
    public void Remove_NonExistentMonitor_ReturnsFalse()
    {
        using var manager = new DynamicHealthMonitorManager();

        Assert.False(manager.Remove("missing"));
    }

    [Fact]
    public async Task Remove_StopsEventForwarding()
    {
        using var manager = new DynamicHealthMonitorManager(new FakeSystemTimeProvider());

        var firedCount = 0;
        manager.Degraded += (_, _) => firedCount++;

        manager.Add("svc", new HealthMonitorOptions
        {
            DegradedThreshold = TimeSpan.FromMilliseconds(100),
            CheckInterval = TimeSpan.FromMilliseconds(50),
        });

        // Wait for at least one degradation to confirm the path works
        await PollUntil(() => firedCount > 0, timeout: TimeSpan.FromSeconds(5));

        manager.Remove("svc");
        var countAtRemoval = firedCount;

        // Wait a bit — no further events should arrive
        await Task.Delay(200, TestContext.Current.CancellationToken);

        Assert.Equal(countAtRemoval, firedCount);
    }

    [Fact]
    public void TryGet_AfterRemove_ReturnsNull()
    {
        using var manager = new DynamicHealthMonitorManager();
        manager.Add("svc");
        manager.Remove("svc");

        Assert.Null(manager.TryGet("svc"));
    }

    [Fact]
    public void TryGet_ExistingMonitor_ReturnsMonitor()
    {
        using var manager = new DynamicHealthMonitorManager();
        manager.Add("svc");

        var result = manager.TryGet("svc");

        Assert.NotNull(result);
        Assert.Equal("svc", result.Name);
    }

    [Fact]
    public void TryGet_IsCaseInsensitive()
    {
        using var manager = new DynamicHealthMonitorManager();
        manager.Add("SVC");

        Assert.NotNull(manager.TryGet("svc"));
    }

    [Fact]
    public void TryGet_NonExistentMonitor_ReturnsNull()
    {
        using var manager = new DynamicHealthMonitorManager();

        Assert.Null(manager.TryGet("missing"));
    }

    private static async Task PollUntil(Func<bool> condition, TimeSpan timeout)
    {
        var deadline = DateTime.UtcNow + timeout;
        while (!condition() && DateTime.UtcNow < deadline)
            await Task.Delay(20);
    }
}