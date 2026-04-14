namespace HealthMonitor.Core.Events;

/// <summary>
/// Base class for all health monitor event arguments.
/// </summary>
/// <param name="monitorName">Unique logical name of the monitor that fired the event.</param>
/// <param name="timestamp">UTC wall-clock timestamp when the state transition occurred.</param>
public abstract class HealthEventArgs(string monitorName, DateTimeOffset timestamp) : EventArgs
{
    /// <summary>
    /// The logical name of the monitor that fired the event.
    /// </summary>
    public string MonitorName { get; } = monitorName;

    /// <summary>
    /// UTC wall-clock timestamp when the state transition occurred.
    /// </summary>
    public DateTimeOffset Timestamp { get; } = timestamp;
}