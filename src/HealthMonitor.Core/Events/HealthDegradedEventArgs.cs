namespace HealthMonitor.Core.Events;

/// <summary>
/// Event arguments raised when a monitor transitions to the degraded state
/// (no signal received within <see cref="Configuration.HealthMonitorOptions.DegradedThreshold"/>).
/// </summary>
/// <param name="healthyDuration">How long the monitor was in a healthy state before degrading.</param>
/// <param name="monitorName">Inherited from <see cref="HealthEventArgs"/>.</param>
/// <param name="timestamp">Inherited from <see cref="HealthEventArgs"/>.</param>
public sealed class HealthDegradedEventArgs(string monitorName, DateTimeOffset timestamp, TimeSpan healthyDuration)
    : HealthEventArgs(monitorName, timestamp)
{
    /// <summary>How long the monitor was in a healthy state before degrading.</summary>
    public TimeSpan HealthyDuration
    {
        get;
    } = healthyDuration;
}