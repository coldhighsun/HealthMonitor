namespace HealthMonitor.Events;

/// <summary>Base class for all health monitor event arguments.</summary>
public abstract class HealthEventArgs : EventArgs
{
    /// <summary>The logical name of the monitor that fired the event.</summary>
    public string MonitorName { get; }

    /// <summary>UTC wall-clock timestamp when the state transition occurred.</summary>
    public DateTimeOffset Timestamp { get; }

    protected HealthEventArgs(string monitorName, DateTimeOffset timestamp)
    {
        MonitorName = monitorName;
        Timestamp = timestamp;
    }
}
