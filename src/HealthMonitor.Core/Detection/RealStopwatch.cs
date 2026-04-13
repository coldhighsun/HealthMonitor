using System.Diagnostics;
using HealthMonitor.Abstractions;

namespace HealthMonitor.Detection;

/// <summary>
/// Production implementation of <see cref="IStopwatch"/> backed by <see cref="Stopwatch"/>.
/// Uses a monotonic, high-resolution timer — immune to system clock adjustments.
/// </summary>
internal sealed class RealStopwatch : IStopwatch
{
    private readonly Stopwatch _sw = Stopwatch.StartNew();

    public TimeSpan Elapsed => _sw.Elapsed;

    public void Restart() => _sw.Restart();
}
