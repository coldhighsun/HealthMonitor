# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Commands

```bash
dotnet build          # Build all projects
dotnet test           # Run all tests (multi-targets: net8.0, net9.0, net10.0)
dotnet test --filter "FullyQualifiedName~SomeTest"  # Run a single test
```

Build artifacts go to `/artifacts/bin/` (configured in `Directory.Build.props`).

## Architecture

**HealthMonitor** is a .NET library for monitoring application health via periodic heartbeats. Consumers call `Signal()` on a named monitor; if no signal arrives within `DegradedThreshold`, the monitor fires a `Degraded` event. It recovers immediately when a signal is received while degraded.

### Key components (`src/HealthMonitor.Core/`)

| Layer | Purpose |
|---|---|
| `Abstractions/` | `IHealthMonitor`, `IStopwatch`, `ISystemTimeProvider` |
| `Monitors/` | `HealthMonitorBase` (state machine), `NamedHealthMonitor`, `HealthMonitorCoordinator` |
| `Services/` | `HealthMonitorHostedService` — background loop, ticks coordinator at `MinCheckInterval` |
| `Configuration/` | `HealthMonitorOptions`, `HealthMonitorRegistration` |
| `Extensions/` | `AddHealthMonitor()` DI extension |
| `Events/` | `HealthDegradedEventArgs`, `HealthRecoveredEventArgs` |

### State machine

- **Healthy → Degraded**: no `Signal()` received within `DegradedThreshold` (default 30 s)
- **Degraded → Healthy**: `Signal()` received while degraded; fires `Recovered` event immediately

### DI registration

`AddHealthMonitor()` registers monitors as keyed services (by name, .NET 8+) and via `IEnumerable<IHealthMonitor>` on all targets. The hosted service resolves all monitors through the coordinator.

### Testability

`IStopwatch` and `ISystemTimeProvider` abstractions allow time-controlled unit tests. The `tests/HealthMonitor.Tests/Fakes/` directory provides `FakeStopwatch` and related fakes.

### Multi-targeting

`HealthMonitor.Core` targets `netstandard2.0`, `net8.0`, `net9.0`, and `net10.0`. Keyed DI services are only available on `net8.0`+. Use `#if NET8_0_OR_GREATER` guards when adding version-specific APIs.
