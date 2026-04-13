using HealthMonitor.Abstractions;
using HealthMonitor.Configuration;
using HealthMonitor.Detection;
using HealthMonitor.Events;

namespace HealthMonitor.Monitors;

/// <summary>
/// Concrete public implementation of <see cref="IHealthMonitor"/>.
/// </summary>
internal sealed class NamedHealthMonitor : HealthMonitorBase, IHealthMonitor
{
    public string Name { get; }

    bool IHealthMonitor.IsHealthy => IsHealthy;

    event EventHandler<HealthDegradedEventArgs> IHealthMonitor.Degraded
    {
        add => Degraded += value;
        remove => Degraded -= value;
    }

    event EventHandler<HealthRecoveredEventArgs> IHealthMonitor.Recovered
    {
        add => Recovered += value;
        remove => Recovered -= value;
    }

    /// <summary>Production constructor — uses <see cref="RealStopwatch"/> instances.</summary>
    public NamedHealthMonitor(HealthMonitorOptions options, ISystemTimeProvider clock)
        : this(options, clock, new RealStopwatch(), new RealStopwatch(), new RealStopwatch())
    {
    }

    /// <summary>Testable constructor — accepts injected <see cref="IStopwatch"/> instances.</summary>
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

    void IHealthMonitor.Signal() => Signal();
}
