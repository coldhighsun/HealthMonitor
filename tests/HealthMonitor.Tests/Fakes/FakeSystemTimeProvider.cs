using HealthMonitor.Abstractions;

namespace HealthMonitor.Tests.Fakes;

/// <summary>
/// Test double for <see cref="ISystemTimeProvider"/> with manually advanceable clock.
/// </summary>
internal sealed class FakeSystemTimeProvider : ISystemTimeProvider
{
    private DateTimeOffset _utcNow;

    public FakeSystemTimeProvider(DateTimeOffset? start = null)
        => _utcNow = start ?? new DateTimeOffset(2024, 1, 1, 0, 0, 0, TimeSpan.Zero);

    public DateTimeOffset UtcNow => _utcNow;

    public void Advance(TimeSpan duration) => _utcNow += duration;
}
