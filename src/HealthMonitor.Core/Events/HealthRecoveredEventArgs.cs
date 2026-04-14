namespace HealthMonitor.Core.Events;

/// <summary>
/// Event arguments raised when a monitor recovers from the degraded state
/// (a <see cref="Abstractions.IHealthMonitor.Signal"/> is received while degraded).
/// </summary>
/// <param name="degradedDuration">The duration for which the monitor was in a degraded state.</param>
/// <param name="monitorName">Inherited from <see cref="HealthEventArgs"/>.</param>
/// <param name="timestamp">Inherited from <see cref="HealthEventArgs"/>.</param>
public sealed class HealthRecoveredEventArgs(string monitorName, DateTimeOffset timestamp, TimeSpan degradedDuration)
    : HealthEventArgs(monitorName, timestamp)
{
    /// <summary>
    /// How long the monitor was in a degraded state before recovering.
    /// </summary>
    public TimeSpan DegradedDuration { get; } = degradedDuration;
}
