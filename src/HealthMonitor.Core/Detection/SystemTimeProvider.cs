using HealthMonitor.Abstractions;

namespace HealthMonitor.Detection;

internal sealed class SystemTimeProvider : ISystemTimeProvider
{
    public DateTimeOffset UtcNow => DateTimeOffset.UtcNow;
}
