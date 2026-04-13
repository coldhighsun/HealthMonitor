using HealthMonitor.Events;

namespace HealthMonitor.Abstractions;

/// <summary>
/// Represents a single named health monitor.
/// <para>
/// Consumers call <see cref="Signal"/> to report a heartbeat. If no signal arrives within
/// <see cref="Configuration.HealthMonitorOptions.DegradedThreshold"/>, the monitor transitions
/// to the degraded state and fires <see cref="Degraded"/>. The moment a signal is received
/// while degraded, <see cref="Recovered"/> fires immediately — without waiting for the next
/// polling tick.
/// </para>
/// </summary>
public interface IHealthMonitor
{
    /// <summary>Unique logical name of this monitor.</summary>
    string Name { get; }

    /// <summary>Returns <c>true</c> when the monitor is in a healthy state.</summary>
    bool IsHealthy { get; }

    /// <summary>
    /// Reports a heartbeat. Resets the degraded-threshold timer.
    /// If the monitor was degraded, immediately fires <see cref="Recovered"/>.
    /// </summary>
    void Signal();

    /// <summary>
    /// Raised when no signal has been received within
    /// <see cref="Configuration.HealthMonitorOptions.DegradedThreshold"/>.
    /// </summary>
    event EventHandler<HealthDegradedEventArgs> Degraded;

    /// <summary>
    /// Raised immediately when <see cref="Signal"/> is called while the monitor is degraded.
    /// </summary>
    event EventHandler<HealthRecoveredEventArgs> Recovered;
}
