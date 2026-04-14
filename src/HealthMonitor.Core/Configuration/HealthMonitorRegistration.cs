namespace HealthMonitor.Core.Configuration;

/// <summary>
/// Internal record that binds a set of options to a monitor instance during DI composition.
/// </summary>
/// <param name="options">Options for the monitor instance being registered.</param>
internal sealed class HealthMonitorRegistration(HealthMonitorOptions options)
{
    /// <summary>
    /// Gets the configuration options for the health monitor.
    /// </summary>
    public HealthMonitorOptions Options
    {
        get;
    } = options;
}