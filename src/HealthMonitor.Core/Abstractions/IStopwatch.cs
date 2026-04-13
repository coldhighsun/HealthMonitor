namespace HealthMonitor.Abstractions;

/// <summary>
/// Abstraction over a monotonic elapsed-time counter, enabling deterministic testing.
/// </summary>
internal interface IStopwatch
{
    /// <summary>Total time elapsed since the stopwatch was last started or restarted.</summary>
    TimeSpan Elapsed { get; }

    /// <summary>Stops the stopwatch, resets elapsed time to zero, and starts it again.</summary>
    void Restart();
}
