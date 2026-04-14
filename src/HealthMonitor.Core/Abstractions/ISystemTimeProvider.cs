namespace HealthMonitor.Core.Abstractions;

/// <summary>
/// Abstraction over the system clock, enabling deterministic testing.
/// </summary>
public interface ISystemTimeProvider
{
    /// <summary>
    /// Gets the current UTC date and time.
    /// </summary>
    DateTimeOffset UtcNow
    {
        get;
    }
}