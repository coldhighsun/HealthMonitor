using HealthMonitor.Core.Abstractions;
using HealthMonitor.Core.Configuration;
using HealthMonitor.Core.Detection;
using HealthMonitor.Core.Events;

namespace HealthMonitor.Core.Monitors;

/// <summary>
/// Manages a dynamic set of health monitors, each driven by its own <see cref="Timer"/>
/// firing at the monitor's configured <see cref="HealthMonitorOptions.CheckInterval"/>.
/// Monitors can be added or removed at any time while the manager is running.
/// <para>
/// Subscribing to <see cref="Degraded"/> / <see cref="Recovered"/> on the manager
/// receives forwarded events from all monitors, including those added after the subscription.
/// </para>
/// </summary>
public sealed class DynamicHealthMonitorManager : IDisposable
{
    /// <summary>
    /// The system clock provider, used by monitors to timestamp events. Injected for testability.
    /// </summary>
    private readonly ISystemTimeProvider _clock;

    /// <summary>
    /// Stores entries indexed by key, using a case-insensitive string comparer.
    /// </summary>
    private readonly Dictionary<string, Entry> _entries = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Lock to protect the _entries dictionary and the _disposed flag, ensuring thread safety for all public methods.
    /// </summary>
    private readonly object _lock = new();

    /// <summary>
    /// Set to <c>true</c> when the manager has been disposed, preventing further operations and ensuring timers are cleaned up.
    /// </summary>
    private bool _disposed;

    /// <summary>
    /// Initializes a new instance using the real system clock.
    /// </summary>
    public DynamicHealthMonitorManager() : this(new SystemTimeProvider()) { }

    /// <summary>
    /// Initializes a new instance with an injected clock (enables unit-test control).
    /// </summary>
    public DynamicHealthMonitorManager(ISystemTimeProvider clock)
    {
        _clock = clock;
    }

    /// <summary>
    /// Raised when any registered monitor transitions to the degraded state.
    /// The sender is the individual <see cref="IHealthMonitor"/> that degraded.
    /// </summary>
    public event EventHandler<HealthDegradedEventArgs>? Degraded;

    /// <summary>
    /// Raised when any registered monitor recovers from a degraded state.
    /// The sender is the individual <see cref="IHealthMonitor"/> that recovered.
    /// </summary>
    public event EventHandler<HealthRecoveredEventArgs>? Recovered;

    /// <summary>
    /// Snapshot of all currently registered monitors, in addition order.
    /// </summary>
    public IReadOnlyList<IHealthMonitor> Monitors
    {
        get
        {
            lock (_lock)
                return _entries.Values.Select(e => (IHealthMonitor)e.Monitor).ToList();
        }
    }

    /// <summary>
    /// Adds a new monitor with the given name and options and starts its timer immediately.
    /// Throws <see cref="ArgumentException"/> if a monitor with the same name already exists.
    /// </summary>
    public IHealthMonitor Add(string name, HealthMonitorOptions? options = null)
    {
        if (_disposed)
            throw new ObjectDisposedException(nameof(DynamicHealthMonitorManager));

        var opts = options ?? new HealthMonitorOptions();
        opts.Name = name;

        lock (_lock)
        {
            if (_entries.ContainsKey(name))
                throw new ArgumentException($"A monitor named '{name}' is already registered.", nameof(name));

            var monitor = new NamedHealthMonitor(opts, _clock);

            EventHandler<HealthDegradedEventArgs> onDegraded = (s, e) => Degraded?.Invoke(s, e);
            EventHandler<HealthRecoveredEventArgs> onRecovered = (s, e) => Recovered?.Invoke(s, e);
            monitor.Degraded += onDegraded;
            monitor.Recovered += onRecovered;

            var timer = new Timer(
                _ => monitor.Tick(),
                null,
                opts.CheckInterval,
                opts.CheckInterval);

            _entries[name] = new(monitor, timer, onDegraded, onRecovered);
            return monitor;
        }
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        lock (_lock)
        {
            if (_disposed)
                return;
            _disposed = true;

            foreach (var entry in _entries.Values)
            {
                entry.Unsubscribe();
                entry.Timer.Dispose();
            }

            _entries.Clear();
        }
    }

    /// <summary>
    /// Removes and disposes the timer for the monitor with the given name.
    /// Returns <c>true</c> if a monitor was found and removed; <c>false</c> if the name was not registered.
    /// </summary>
    public bool Remove(string name)
    {
        lock (_lock)
        {
            if (!_entries.TryGetValue(name, out var entry))
                return false;

            _entries.Remove(name);
            entry.Unsubscribe();
            entry.Timer.Dispose();
            return true;
        }
    }

    /// <summary>
    /// Retrieves a registered monitor by name, or <c>null</c> if not found.
    /// </summary>
    public IHealthMonitor? TryGet(string name)
    {
        lock (_lock)
            return _entries.TryGetValue(name, out var e) ? e.Monitor : null;
    }

    /// <summary>
    /// Internal class to hold a monitor and its associated timer together in the dictionary.
    /// </summary>
    /// <param name="monitor">The health monitor instance.</param>
    /// <param name="timer">The timer associated with the monitor.</param>
    private sealed class Entry(
        NamedHealthMonitor monitor,
        Timer timer,
        EventHandler<HealthDegradedEventArgs> onDegraded,
        EventHandler<HealthRecoveredEventArgs> onRecovered)
    {
        /// <summary>
        /// Gets the health monitor associated with this instance.
        /// </summary>
        public NamedHealthMonitor Monitor { get; } = monitor;

        /// <summary>
        /// Gets the timer instance associated with this object.
        /// </summary>
        public Timer Timer { get; } = timer;

        /// <summary>
        /// Unsubscribes the manager's event handlers from the monitor's events. Called when removing a monitor or disposing the manager.
        /// </summary>
        public void Unsubscribe()
        {
            Monitor.Degraded -= onDegraded;
            Monitor.Recovered -= onRecovered;
        }
    }
}
