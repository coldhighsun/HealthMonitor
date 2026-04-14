using HealthMonitor.Core.Abstractions;
using HealthMonitor.Core.Configuration;
using HealthMonitor.Core.Detection;
using HealthMonitor.Core.Events;

namespace HealthMonitor.Core.Monitors;

/// <summary>
/// Concrete public implementation of <see cref="IHealthMonitor"/>.
/// </summary>
internal sealed class NamedHealthMonitor : HealthMonitorBase, IHealthMonitor
{
    /// <summary>
    /// Production constructor — uses <see cref="RealStopwatch"/> instances.
    /// </summary>
    public NamedHealthMonitor(HealthMonitorOptions options, ISystemTimeProvider clock)
        : this(options, clock, new RealStopwatch(), new RealStopwatch(), new RealStopwatch())
    {
    }

    /// <summary>
    /// Testable constructor — accepts injected <see cref="IStopwatch"/> instances.
    /// </summary>
    internal NamedHealthMonitor(
        HealthMonitorOptions options,
        ISystemTimeProvider clock,
        IStopwatch signalStopwatch,
        IStopwatch checkStopwatch,
        IStopwatch stateStopwatch)
        : base(clock, options, signalStopwatch, checkStopwatch, stateStopwatch)
    {
        Name = options.Name;
    }

    /// <summary>
    /// Occurs when the health status of the monitored component is degraded.
    /// </summary>
    event EventHandler<HealthDegradedEventArgs> IHealthMonitor.Degraded
    {
        add => Degraded += value;
        remove => Degraded -= value;
    }

    /// <summary>
    /// Occurs when the monitored component has recovered from a previously reported unhealthy state.
    /// </summary>
    event EventHandler<HealthRecoveredEventArgs> IHealthMonitor.Recovered
    {
        add => Recovered += value;
        remove => Recovered -= value;
    }

    /// <summary>
    /// Exposes the protected <see cref="HealthMonitorBase.IsHealthy"/> property as the public implementation of <see cref="IHealthMonitor.IsHealthy"/>.
    /// </summary>
    bool IHealthMonitor.IsHealthy => IsHealthy;

    /// <summary>
    /// Gets the name associated with the current instance.
    /// </summary>
    public string Name
    {
        get;
    }

    /// <summary>
    /// Signals the health monitor to indicate that the monitored component is active or responsive.
    /// </summary>
    void IHealthMonitor.Signal() => Signal();
}