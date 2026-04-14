using HealthMonitor.Core.Abstractions;

namespace HealthMonitor.Core.Detection;

/// <summary>
/// Production implementation of <see cref="ISystemTimeProvider"/> that returns the current UTC time using <see cref="DateTimeOffset.UtcNow"/>.
/// </summary>
internal sealed class SystemTimeProvider : ISystemTimeProvider
{
    /// <summary>
    /// Gets the current date and time in Coordinated Universal Time (UTC).
    /// </summary>
    public DateTimeOffset UtcNow => DateTimeOffset.UtcNow;
}