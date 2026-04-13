using HealthMonitor.Abstractions;

namespace HealthMonitor.Tests.Fakes;

/// <summary>
/// Test double for <see cref="IStopwatch"/> with manually advanceable elapsed time.
/// </summary>
internal sealed class FakeStopwatch : IStopwatch
{
    private TimeSpan _elapsed = TimeSpan.Zero;

    public TimeSpan Elapsed => _elapsed;

    public void Restart() => _elapsed = TimeSpan.Zero;

    public void Advance(TimeSpan duration) => _elapsed += duration;
}
