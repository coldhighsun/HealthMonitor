using HealthMonitor.Abstractions;
using HealthMonitor.Configuration;
using HealthMonitor.Events;

namespace HealthMonitor.Monitors;

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
internal abstract class HealthMonitorBase
{
    private readonly ISystemTimeProvider _clock;
    private readonly HealthMonitorOptions _options;
    private readonly IStopwatch _signalStopwatch;
    private readonly IStopwatch _checkStopwatch;
    private readonly IStopwatch _stateStopwatch;
    private readonly object _lock = new();

    private bool _isDegraded;

    protected HealthMonitorBase(
        ISystemTimeProvider clock,
        HealthMonitorOptions options,
        IStopwatch signalStopwatch,
        IStopwatch checkStopwatch,
        IStopwatch stateStopwatch)
    {
        _clock = clock;
        _options = options;
        _signalStopwatch = signalStopwatch;
        _checkStopwatch = checkStopwatch;
        _stateStopwatch = stateStopwatch;
    }

    public bool IsHealthy { get { lock (_lock) return !_isDegraded; } }

    public event EventHandler<HealthDegradedEventArgs>? Degraded;
    public event EventHandler<HealthRecoveredEventArgs>? Recovered;

    /// <summary>
    /// Reports a heartbeat. Resets the degradation timer.
    /// Fires <see cref="Recovered"/> immediately if the monitor was degraded.
    /// </summary>
    public void Signal()
    {
        HealthRecoveredEventArgs? recoveredArgs = null;

        lock (_lock)
        {
            _signalStopwatch.Restart();

            if (_isDegraded)
            {
                var degradedDuration = _stateStopwatch.Elapsed;
                _isDegraded = false;
                _stateStopwatch.Restart();
                recoveredArgs = new HealthRecoveredEventArgs(_options.Name, _clock.UtcNow, degradedDuration);
            }
        }

        // Fire outside the lock to avoid holding it during event handler execution
        if (recoveredArgs is not null)
            OnRecovered(recoveredArgs);
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
            if (_checkStopwatch.Elapsed < _options.CheckInterval)
                return;

            _checkStopwatch.Restart();

            if (!_isDegraded && _signalStopwatch.Elapsed >= _options.DegradedThreshold)
            {
                var healthyDuration = _stateStopwatch.Elapsed;
                _isDegraded = true;
                _stateStopwatch.Restart();
                degradedArgs = new HealthDegradedEventArgs(_options.Name, _clock.UtcNow, healthyDuration);
            }
        }

        if (degradedArgs is not null)
            OnDegraded(degradedArgs);
    }

    protected virtual void OnDegraded(HealthDegradedEventArgs args)
        => Degraded?.Invoke(this, args);

    protected virtual void OnRecovered(HealthRecoveredEventArgs args)
        => Recovered?.Invoke(this, args);
}
