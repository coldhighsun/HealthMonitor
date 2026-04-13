using HealthMonitor.Abstractions;
using HealthMonitor.Configuration;

namespace HealthMonitor.Monitors;

/// <summary>
/// Holds all registered <see cref="NamedHealthMonitor"/> instances and drives their
/// degradation-check ticks from the hosted service.
/// </summary>
internal sealed class HealthMonitorCoordinator
{
    private readonly List<NamedHealthMonitor> _monitors;

    public IReadOnlyList<IHealthMonitor> Monitors { get; }

    /// <summary>
    /// The shortest <see cref="HealthMonitorOptions.CheckInterval"/> across all monitors,
    /// used by the hosted service to size its polling loop delay.
    /// </summary>
    public TimeSpan MinCheckInterval { get; }

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

    /// <summary>Runs a degradation check tick on every registered monitor.</summary>
    public void TickAll()
    {
        foreach (var monitor in _monitors)
            monitor.Tick();
    }
}
