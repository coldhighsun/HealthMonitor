using System.Diagnostics;
using HealthMonitor.Core.Abstractions;

namespace HealthMonitor.Core.Detection;

/// <summary>
/// Production implementation of <see cref="IStopwatch"/> backed by <see cref="Stopwatch"/>.
/// Uses a monotonic, high-resolution timer — immune to system clock adjustments.
/// </summary>
internal sealed class RealStopwatch : IStopwatch
{
    /// <summary>
    /// Underlying stopwatch instance, started immediately on construction. The stopwatch is monotonic and high-resolution, making it ideal for measuring elapsed time intervals without being affected by system clock changes.
    /// </summary>
    private readonly Stopwatch _sw = Stopwatch.StartNew();

    /// <summary>
    /// Gets the total elapsed time measured by the timer.
    /// </summary>
    public TimeSpan Elapsed => _sw.Elapsed;

    /// <summary>
    /// Resets the elapsed time to zero and starts measuring elapsed time immediately.
    /// </summary>
    public void Restart() => _sw.Restart();
}