namespace HealthMonitor.Events;

/// <summary>
/// Event arguments raised when a monitor transitions to the degraded state
/// (no signal received within <see cref="Configuration.HealthMonitorOptions.DegradedThreshold"/>).
/// </summary>
public sealed class HealthDegradedEventArgs : HealthEventArgs
{
    /// <summary>How long the monitor was in a healthy state before degrading.</summary>
    public TimeSpan HealthyDuration { get; }

    public HealthDegradedEventArgs(string monitorName, DateTimeOffset timestamp, TimeSpan healthyDuration)
        : base(monitorName, timestamp)
    {
        HealthyDuration = healthyDuration;
    }
}
