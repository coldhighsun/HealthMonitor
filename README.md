# HealthMonitor

A lightweight .NET library for monitoring application health via periodic heartbeats. Supports dependency injection, multiple named monitors, and fires `Degraded` / `Recovered` events on state transitions.

## Features

- **Multiple monitors** — register any number of independent monitors, each with its own degraded threshold and check interval
- **Two events** — `Degraded` fires when no heartbeat is received within the threshold; `Recovered` fires immediately when a signal arrives while degraded
- **DI-first** — integrates with `IServiceCollection` and runs as a `BackgroundService`
- **Keyed injection** — resolve a specific monitor by name via `IServiceProvider.GetRequiredKeyedService<IHealthMonitor>("name")` (.NET 8+)
- **Testable** — `IStopwatch` and `ISystemTimeProvider` abstractions decouple timing from wall-clock time

## Getting Started

### Registration

```csharp
services.AddHealthMonitor("quote-feed", opt =>
{
    opt.DegradedThreshold = TimeSpan.FromSeconds(30);
    opt.CheckInterval     = TimeSpan.FromSeconds(5);
});
```

`AddHealthMonitor` is idempotent for the shared infrastructure (hosted service, coordinator) — call it once per monitor.

### Subscribing to Events

**By name (keyed service, .NET 8+):**

```csharp
var monitor = provider.GetRequiredKeyedService<IHealthMonitor>("quote-feed");

monitor.Degraded  += (_, e) =>
    Console.WriteLine($"[{e.MonitorName}] Degraded after {e.HealthyDuration} healthy");

monitor.Recovered += (_, e) =>
    Console.WriteLine($"[{e.MonitorName}] Recovered after {e.DegradedDuration} degraded");
```

**All monitors:**

```csharp
foreach (var monitor in provider.GetRequiredService<IEnumerable<IHealthMonitor>>())
{
    monitor.Degraded  += (_, e) => Console.WriteLine($"{e.MonitorName}: degraded");
    monitor.Recovered += (_, e) => Console.WriteLine($"{e.MonitorName}: recovered");
}
```

### Sending Heartbeats

```csharp
// Call Signal() whenever the monitored component is alive.
// If no signal arrives within DegradedThreshold, the Degraded event fires.
monitor.Signal();
```

## Configuration

| Property | Default | Description |
|---|---|---|
| `DegradedThreshold` | 30 seconds | Max time between signals before `Degraded` fires |
| `CheckInterval` | 5 seconds | How often the background service checks for threshold breaches |

## Event Arguments

### `HealthDegradedEventArgs`
| Property | Type | Description |
|---|---|---|
| `MonitorName` | `string` | Name of the monitor |
| `Timestamp` | `DateTimeOffset` | UTC time of the transition |
| `HealthyDuration` | `TimeSpan` | How long the monitor was healthy before degrading |

### `HealthRecoveredEventArgs`
| Property | Type | Description |
|---|---|---|
| `MonitorName` | `string` | Name of the monitor |
| `Timestamp` | `DateTimeOffset` | UTC time of the transition |
| `DegradedDuration` | `TimeSpan` | How long the monitor was degraded before recovering |

## State Machine

```
            no signal >= DegradedThreshold
  Healthy ──────────────────────────────► Degraded
          ◄──────────────────────────────
                  Signal() called
```

Each monitor independently tracks its own state. The `BackgroundService` runs a single loop at the minimum `CheckInterval` across all registered monitors.

## Building

```bash
dotnet build
dotnet test
```

Build outputs land in `./artifacts/bin/`.
