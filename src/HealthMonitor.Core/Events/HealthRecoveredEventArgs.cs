namespace HealthMonitor.Events;

/// <summary>
/// Event arguments raised when a monitor recovers from the degraded state
/// (a <see cref="Abstractions.IHealthMonitor.Signal"/> is received while degraded).
/// </summary>
public sealed class HealthRecoveredEventArgs : HealthEventArgs
{
    /// <summary>How long the monitor was in a degraded state before recovering.</summary>
    public TimeSpan DegradedDuration { get; }

    public HealthRecoveredEventArgs(string monitorName, DateTimeOffset timestamp, TimeSpan degradedDuration)
        : base(monitorName, timestamp)
    {
        DegradedDuration = degradedDuration;
    }
}
