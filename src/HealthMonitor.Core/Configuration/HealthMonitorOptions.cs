namespace HealthMonitor.Core.Configuration;

/// <summary>
/// Configuration options for a single <see cref="Abstractions.IHealthMonitor"/> instance.
/// </summary>
public sealed class HealthMonitorOptions
{
    /// <summary>
    /// How frequently the background service checks whether the degraded threshold has been exceeded.
    /// Should be significantly shorter than <see cref="DegradedThreshold"/> for timely detection.
    /// Default: 5 seconds.
    /// </summary>
    public TimeSpan CheckInterval { get; set; } = TimeSpan.FromSeconds(5);

    /// <summary>
    /// Maximum time allowed between consecutive <see cref="Abstractions.IHealthMonitor.Signal"/> calls
    /// before the monitor is considered degraded.
    /// Default: 30 seconds.
    /// </summary>
    public TimeSpan DegradedThreshold { get; set; } = TimeSpan.FromSeconds(30);

    /// <summary>
    /// Unique logical name for this monitor. Set automatically by <c>AddHealthMonitor</c>.
    /// </summary>
    public string Name { get; set; } = string.Empty;
}