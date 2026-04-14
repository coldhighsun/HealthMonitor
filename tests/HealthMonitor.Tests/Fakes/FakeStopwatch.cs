using HealthMonitor.Core.Abstractions;

namespace HealthMonitor.Tests.Fakes;

/// <summary>
/// Test double for <see cref="IStopwatch"/> with manually advanceable elapsed time.
/// </summary>
internal sealed class FakeStopwatch : IStopwatch
{
    /// <summary>
    /// The total elapsed time tracked by this instance. Initially zero, and can be advanced manually via <see cref="Advance"/>.
    /// </summary>
    private TimeSpan _elapsed = TimeSpan.Zero;

    /// <summary>
    /// Gets the total elapsed time tracked by this instance.
    /// </summary>
    public TimeSpan Elapsed => _elapsed;

    /// <summary>
    /// Advances the elapsed time by the specified <paramref name="duration"/>.
    /// </summary>
    /// <param name="duration">The amount of time to add to <see cref="Elapsed"/>.</param>
    public void Advance(TimeSpan duration) => _elapsed += duration;

    /// <summary>
    /// Resets the elapsed time to <see cref="TimeSpan.Zero"/>.
    /// </summary>
    public void Restart() => _elapsed = TimeSpan.Zero;
}