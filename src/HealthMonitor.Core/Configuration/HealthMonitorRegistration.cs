namespace HealthMonitor.Configuration;

/// <summary>
/// Internal record that binds a set of options to a monitor instance during DI composition.
/// </summary>
internal sealed class HealthMonitorRegistration
{
    public HealthMonitorOptions Options { get; }

    public HealthMonitorRegistration(HealthMonitorOptions options)
    {
        Options = options;
    }
}
