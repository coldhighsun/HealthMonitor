using HealthMonitor.Core.Abstractions;
using HealthMonitor.Core.Configuration;
using HealthMonitor.Core.Events;

namespace HealthMonitor.Core.Monitors;

/// <summary>
/// Core state machine for a health monitor.
/// <para>
/// Three independent <see cref="IStopwatch"/> instances drive timing:
/// <list type="bullet">
///   <item><term>signalStopwatch</term><description>Restarted on every <see cref="Signal"/> call. When its elapsed time exceeds <see cref="HealthMonitorOptions.DegradedThreshold"/> the monitor degrades.</description></item>
///   <item><term>checkStopwatch</term><description>Guards the background check interval — degradation detection is skipped until enough time has elapsed.</description></item>
///   <item><term>stateStopwatch</term><description>Restarted on every state transition. Provides <c>HealthyDuration</c> and <c>DegradedDuration</c> values in events.</description></item>
/// </list>
/// <see cref="ISystemTimeProvider"/> is used exclusively for the wall-clock <c>Timestamp</c> on events.
/// </para>
/// <para>
/// Thread safety: <see cref="Signal"/> may be called from any thread concurrently with the
/// background <see cref="Tick"/>. A lock guards all shared state.
/// </para>
/// </summary>
/// <param name="checkStopwatch">Injected stopwatch instance for check interval timing.</param>
/// <param name="clock">Injected system time provider for event timestamps.</param>
/// <param name="options">Injected options for this monitor instance.</param>
/// <param name="signalStopwatch">Injected stopwatch instance for degradation timing.</param>
/// <param name="stateStopwatch">Injected stopwatch instance for state duration tracking.</param>
internal abstract class HealthMonitorBase(
    ISystemTimeProvider clock,
    HealthMonitorOptions options,
    IStopwatch signalStopwatch,
    IStopwatch checkStopwatch,
    IStopwatch stateStopwatch)
{
    /// <summary>
    /// Lock object to guard all shared state. Both <see cref="Signal"/> and <see cref="Tick"/> acquire this lock,
    /// </summary>
    private readonly object _lock = new();

    /// <summary>
    /// Indicates whether the monitor is currently degraded. Guarded by <see cref="_lock"/>.
    /// </summary>
    private bool _isDegraded;

    /// <summary>
    /// Occurs when the health status of the monitored component degrades below the acceptable threshold.
    /// </summary>
    public event EventHandler<HealthDegradedEventArgs>? Degraded;

    /// <summary>
    /// Occurs when health has been fully recovered.
    /// </summary>
    public event EventHandler<HealthRecoveredEventArgs>? Recovered;

    /// <summary>
    /// Gets a value indicating whether the current state is healthy.
    /// </summary>
    public bool IsHealthy
    {
        get
        {
            lock (_lock)
                return !_isDegraded;
        }
    }

    /// <summary>
    /// Reports a heartbeat. Resets the degradation timer.
    /// Fires <see cref="Recovered"/> immediately if the monitor was degraded.
    /// </summary>
    public void Signal()
    {
        HealthRecoveredEventArgs? recoveredArgs = null;

        lock (_lock)
        {
            signalStopwatch.Restart();

            if (_isDegraded)
            {
                var degradedDuration = stateStopwatch.Elapsed;
                _isDegraded = false;
                stateStopwatch.Restart();
                recoveredArgs = new(options.Name, clock.UtcNow, degradedDuration);
            }
        }

        // Fire outside the lock to avoid holding it during event handler execution
        if (recoveredArgs is not null)
        {
            OnRecovered(recoveredArgs);
        }
    }

    /// <summary>
    /// Called by the coordinator on every global tick.
    /// Internally skips evaluation until the monitor's own <see cref="HealthMonitorOptions.CheckInterval"/> elapses.
    /// </summary>
    public void Tick()
    {
        HealthDegradedEventArgs? degradedArgs = null;

        lock (_lock)
        {
            if (checkStopwatch.Elapsed < options.CheckInterval)
                return;

            checkStopwatch.Restart();

            if (!_isDegraded && signalStopwatch.Elapsed >= options.DegradedThreshold)
            {
                var healthyDuration = stateStopwatch.Elapsed;
                _isDegraded = true;
                stateStopwatch.Restart();
                degradedArgs = new(options.Name, clock.UtcNow, healthyDuration);
            }
        }

        if (degradedArgs is not null)
            OnDegraded(degradedArgs);
    }

    /// <summary>
    /// Raises the Degraded event to notify subscribers that the system health has transitioned to a degraded state.
    /// </summary>
    /// <param name="args">The event data containing information about the degraded health state.</param>
    protected virtual void OnDegraded(HealthDegradedEventArgs args)
        => Degraded?.Invoke(this, args);

    /// <summary>
    /// Raises the Recovered event to notify subscribers that the system health has transitioned back to a healthy state.
    /// </summary>
    /// <param name="args">The event data containing information about the recovered health state.</param>
    protected virtual void OnRecovered(HealthRecoveredEventArgs args)
        => Recovered?.Invoke(this, args);
}