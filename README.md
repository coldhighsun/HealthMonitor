# HealthMonitor

English | [简体中文](README.zh-CN.md)

[![CI](https://github.com/coldhighsun/HealthMonitor/actions/workflows/ci.yml/badge.svg)](https://github.com/coldhighsun/HealthMonitor/actions/workflows/ci.yml)
[![NuGet Version](https://img.shields.io/nuget/v/HealthMonitor.Core)](https://www.nuget.org/packages/HealthMonitor.Core)
[![NuGet Downloads](https://img.shields.io/nuget/dt/HealthMonitor.Core)](https://www.nuget.org/packages/HealthMonitor.Core)
[![License: MIT](https://img.shields.io/badge/License-MIT-blue.svg)](LICENSE)

A lightweight .NET library for monitoring application health via periodic heartbeats. Supports dependency injection, multiple named monitors, and fires `Degraded` / `Recovered` events on state transitions.

## Features

- **Multiple monitors** — register any number of independent monitors, each with its own degraded threshold and check interval
- **Two events** — `Degraded` fires when no heartbeat is received within the threshold; `Recovered` fires immediately when a signal arrives while degraded
- **DI-first** — integrates with `IServiceCollection` and runs as a `BackgroundService`
- **Dynamic management** — `DynamicHealthMonitorManager` adds and removes monitors at runtime; each runs its own `Timer` with per-monitor options
- **Keyed injection** — resolve a specific monitor by name via `IServiceProvider.GetRequiredKeyedService<IHealthMonitor>("name")` (.NET 8+)
- **Testable** — `IStopwatch` and `ISystemTimeProvider` abstractions decouple timing from wall-clock time

## Supported Frameworks

| Target | Keyed DI (`GetRequiredKeyedService`) |
|---|---|
| `netstandard2.0` | — |
| `net8.0` | ✓ |

## Installation

```bash
dotnet add package HealthMonitor.Core
```

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

**`netstandard2.0` — resolve by name without keyed services:**

Keyed DI is not available on `netstandard2.0`. Filter the `IEnumerable<IHealthMonitor>` by name instead:

```csharp
var monitors = provider.GetRequiredService<IEnumerable<IHealthMonitor>>();
var monitor  = monitors.First(m => m.Name == "quote-feed");

monitor.Degraded  += (_, e) => Console.WriteLine($"{e.MonitorName}: degraded");
monitor.Recovered += (_, e) => Console.WriteLine($"{e.MonitorName}: recovered");
```

Alternatively, use `DynamicHealthMonitorManager` which requires no DI at all and is fully supported on `netstandard2.0`.

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

## Dynamic Monitor Management

`DynamicHealthMonitorManager` lets you register and remove monitors at runtime — no DI or hosted service required. Each monitor owns its own `System.Threading.Timer` that fires at its configured `CheckInterval`.

```csharp
using var manager = new DynamicHealthMonitorManager();

// Subscribe at any time — fires for all registered monitors, regardless of when they were added
manager.Degraded  += (_, e) => Console.WriteLine($"{e.MonitorName} degraded");
manager.Recovered += (_, e) => Console.WriteLine($"{e.MonitorName} recovered");

// Add monitors at any time; each runs its own Timer at its own CheckInterval
var db = manager.Add("database", new HealthMonitorOptions
{
    DegradedThreshold = TimeSpan.FromSeconds(10),
    CheckInterval     = TimeSpan.FromSeconds(2),
});

// Heartbeat from your business logic
db.Signal();

// Add more monitors dynamically while the app runs
var cache = manager.Add("cache", new HealthMonitorOptions
{
    DegradedThreshold = TimeSpan.FromSeconds(5),
    CheckInterval     = TimeSpan.FromSeconds(1),
});

// Remove a monitor — disposes its timer and unsubscribes event forwarding
manager.Remove("cache");

// Enumerate all active monitors
foreach (var m in manager.Monitors)
    Console.WriteLine($"{m.Name}: {(m.IsHealthy ? "healthy" : "degraded")}");
```

`DynamicHealthMonitorManager` implements `IDisposable`; disposing it stops all timers.

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
