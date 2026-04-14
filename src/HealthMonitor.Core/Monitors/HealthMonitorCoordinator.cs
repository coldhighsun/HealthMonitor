using HealthMonitor.Core.Abstractions;
using HealthMonitor.Core.Configuration;

namespace HealthMonitor.Core.Monitors;

/// <summary>
/// Holds all registered <see cref="NamedHealthMonitor"/> instances and drives their
/// degradation-check ticks from the hosted service.
/// </summary>
internal sealed class HealthMonitorCoordinator
{
    /// <summary>
    /// List of all registered monitors, initialized from DI. The coordinator holds strong references to these monitors
    /// </summary>
    private readonly List<NamedHealthMonitor> _monitors;

    /// <summary>
    /// Initializes the coordinator with a set of monitor registrations from DI. For each registration, a new <see cref="NamedHealthMonitor"/>
    /// </summary>
    /// <param name="registrations">Set of monitor registrations from DI, each containing options for a monitor instance.</param>
    /// <param name="clock">Injected system time provider for monitor event timestamps.</param>
    public HealthMonitorCoordinator(
        IEnumerable<HealthMonitorRegistration> registrations,
        ISystemTimeProvider clock)
    {
        _monitors = registrations
            .Select(r => new NamedHealthMonitor(r.Options, clock))
            .ToList();

        Monitors = _monitors.AsReadOnly();

        MinCheckInterval = registrations
            .Select(r => r.Options.CheckInterval)
            .DefaultIfEmpty(TimeSpan.FromSeconds(5))
            .Min();
    }

    /// <summary>
    /// The shortest <see cref="HealthMonitorOptions.CheckInterval"/> across all monitors,
    /// used by the hosted service to size its polling loop delay.
    /// </summary>
    public TimeSpan MinCheckInterval
    {
        get;
    }

    /// <summary>
    /// Read-only list of all registered monitors. The coordinator holds strong references to these monitors,
    /// </summary>
    public IReadOnlyList<IHealthMonitor> Monitors
    {
        get;
    }

    /// <summary>
    /// Runs a degradation check tick on every registered monitor.
    /// </summary>
    public void TickAll()
    {
        foreach (var monitor in _monitors)
        {
            monitor.Tick();
        }
    }
}