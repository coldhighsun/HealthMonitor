using HealthMonitor.Abstractions;
using HealthMonitor.Configuration;
using HealthMonitor.Extensions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace HealthMonitor.Tests.Services;

public class DependencyInjectionTests
{
    private static ServiceProvider BuildProvider(Action<IServiceCollection> configure)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        configure(services);
        return services.BuildServiceProvider();
    }

    [Fact]
    public void AddHealthMonitor_RegistersSingleMonitor()
    {
        using var provider = BuildProvider(s => s.AddHealthMonitor("quotes"));

        var monitors = provider.GetRequiredService<IEnumerable<IHealthMonitor>>().ToList();
        Assert.Single(monitors);
        Assert.Equal("quotes", monitors[0].Name);
    }

    [Fact]
    public void AddHealthMonitor_RegistersMultipleMonitors()
    {
        using var provider = BuildProvider(s =>
        {
            s.AddHealthMonitor("quotes");
            s.AddHealthMonitor("orders");
        });

        var monitors = provider.GetRequiredService<IEnumerable<IHealthMonitor>>().ToList();
        Assert.Equal(2, monitors.Count);
        Assert.Contains(monitors, m => m.Name == "quotes");
        Assert.Contains(monitors, m => m.Name == "orders");
    }

#if NET8_0_OR_GREATER
    [Fact]
    public void AddHealthMonitor_KeyedResolution_ReturnsCorrectMonitor()
    {
        using var provider = BuildProvider(s =>
        {
            s.AddHealthMonitor("quotes");
            s.AddHealthMonitor("orders");
        });

        var quotes = provider.GetRequiredKeyedService<IHealthMonitor>("quotes");
        var orders = provider.GetRequiredKeyedService<IHealthMonitor>("orders");

        Assert.Equal("quotes", quotes.Name);
        Assert.Equal("orders", orders.Name);
    }
#endif

    [Fact]
    public void AddHealthMonitor_AllowsCustomOptions()
    {
        using var provider = BuildProvider(s =>
            s.AddHealthMonitor("quotes", opt =>
            {
                opt.DegradedThreshold = TimeSpan.FromSeconds(5);
                opt.CheckInterval     = TimeSpan.FromSeconds(1);
            }));

        var registrations = provider.GetRequiredService<IEnumerable<HealthMonitorRegistration>>().ToList();
        Assert.Single(registrations);
        Assert.Equal(TimeSpan.FromSeconds(5), registrations[0].Options.DegradedThreshold);
    }

    [Fact]
    public void AddHealthMonitor_RegistersHostedService()
    {
        using var provider = BuildProvider(s => s.AddHealthMonitor("quotes"));

        var hostedServices = provider.GetRequiredService<IEnumerable<IHostedService>>().ToList();
        Assert.Contains(hostedServices, hs => hs.GetType().Name == "HealthMonitorHostedService");
    }

    [Fact]
    public void AddHealthMonitor_InitialState_AllMonitorsHealthy()
    {
        using var provider = BuildProvider(s =>
        {
            s.AddHealthMonitor("quotes");
            s.AddHealthMonitor("orders");
        });

        var monitors = provider.GetRequiredService<IEnumerable<IHealthMonitor>>().ToList();
        Assert.All(monitors, m => Assert.True(m.IsHealthy));
    }
}
