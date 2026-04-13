namespace HealthMonitor.Abstractions;

/// <summary>
/// Abstraction over the system clock, enabling deterministic testing.
/// </summary>
public interface ISystemTimeProvider
{
    DateTimeOffset UtcNow { get; }
}
