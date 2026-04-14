# HealthMonitor

[![CI](https://github.com/coldhighsun/HealthMonitor/actions/workflows/ci.yml/badge.svg)](https://github.com/coldhighsun/HealthMonitor/actions/workflows/ci.yml)
[![NuGet Version](https://img.shields.io/nuget/v/HealthMonitor.Core)](https://www.nuget.org/packages/HealthMonitor.Core)
[![NuGet Downloads](https://img.shields.io/nuget/dt/HealthMonitor.Core)](https://www.nuget.org/packages/HealthMonitor.Core)

A lightweight .NET library for monitoring application health via periodic heartbeats. Supports dependency injection, multiple named monitors, and fires `Degraded` / `Recovered` events on state transitions.

## Features

- **Multiple monitors** — register any number of independent monitors, each with its own degraded threshold and check interval
- **Two events** — `Degraded` fires when no heartbeat is received within the threshold; `Recovered` fires immediately when a signal arrives while degraded
- **DI-first** — integrates with `IServiceCollection` and runs as a `BackgroundService`
- **Keyed injection** — resolve a specific monitor by name via `IServiceProvider.GetRequiredKeyedService<IHealthMonitor>("name")` (.NET 8+)
- **Testable** — `IStopwatch` and `ISystemTimeProvider` abstractions decouple timing from wall-clock time

## Supported Frameworks

| Target | Keyed DI (`GetRequiredKeyedService`) |
|---|---|
| `netstandard2.0` | — |
| `net8.0` | ✓ |
| `net9.0` | ✓ |
| `net10.0` | ✓ |

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

---

# HealthMonitor（中文文档）

[![CI](https://github.com/coldhighsun/HealthMonitor/actions/workflows/ci.yml/badge.svg)](https://github.com/coldhighsun/HealthMonitor/actions/workflows/ci.yml)
[![NuGet 版本](https://img.shields.io/nuget/v/HealthMonitor.Core)](https://www.nuget.org/packages/HealthMonitor.Core)
[![NuGet 下载量](https://img.shields.io/nuget/dt/HealthMonitor.Core)](https://www.nuget.org/packages/HealthMonitor.Core)

轻量级 .NET 健康监控库，通过周期性心跳信号判断组件是否存活，在状态切换时触发 `Degraded` / `Recovered` 事件。支持依赖注入、多个命名监控器。

## 功能特性

- **多监控器** — 可注册任意数量的独立监控器，每个监控器拥有独立的降级阈值和检查间隔
- **两种事件** — 心跳超时触发 `Degraded`；降级期间收到信号立即触发 `Recovered`
- **DI 优先** — 集成 `IServiceCollection`，以 `BackgroundService` 形式运行
- **按名称注入** — 通过 `IServiceProvider.GetRequiredKeyedService<IHealthMonitor>("name")` 按名称解析（.NET 8+）
- **易于测试** — `IStopwatch` 和 `ISystemTimeProvider` 抽象解耦了时间依赖

## 支持的目标框架

| 目标框架 | 键控 DI（`GetRequiredKeyedService`） |
|---|---|
| `netstandard2.0` | — |
| `net8.0` | ✓ |
| `net9.0` | ✓ |
| `net10.0` | ✓ |

## 安装

```bash
dotnet add package HealthMonitor.Core
```

## 快速开始

### 注册

```csharp
services.AddHealthMonitor("quote-feed", opt =>
{
    opt.DegradedThreshold = TimeSpan.FromSeconds(30);
    opt.CheckInterval     = TimeSpan.FromSeconds(5);
});
```

`AddHealthMonitor` 对共享基础设施（托管服务、协调器）是幂等的——每个监控器调用一次即可。

### 订阅事件

**按名称（键控服务，.NET 8+）：**

```csharp
var monitor = provider.GetRequiredKeyedService<IHealthMonitor>("quote-feed");

monitor.Degraded  += (_, e) =>
    Console.WriteLine($"[{e.MonitorName}] 已降级，此前健康持续 {e.HealthyDuration}");

monitor.Recovered += (_, e) =>
    Console.WriteLine($"[{e.MonitorName}] 已恢复，降级持续 {e.DegradedDuration}");
```

**所有监控器：**

```csharp
foreach (var monitor in provider.GetRequiredService<IEnumerable<IHealthMonitor>>())
{
    monitor.Degraded  += (_, e) => Console.WriteLine($"{e.MonitorName}: 降级");
    monitor.Recovered += (_, e) => Console.WriteLine($"{e.MonitorName}: 恢复");
}
```

### 发送心跳

```csharp
// 在被监控组件存活时调用 Signal()。
// 若在 DegradedThreshold 内未收到信号，则触发 Degraded 事件。
monitor.Signal();
```

## 配置项

| 属性 | 默认值 | 说明 |
|---|---|---|
| `DegradedThreshold` | 30 秒 | 两次信号之间的最大间隔，超过后触发 `Degraded` |
| `CheckInterval` | 5 秒 | 后台服务检测阈值是否超出的频率 |

## 事件参数

### `HealthDegradedEventArgs`
| 属性 | 类型 | 说明 |
|---|---|---|
| `MonitorName` | `string` | 监控器名称 |
| `Timestamp` | `DateTimeOffset` | 状态切换的 UTC 时间 |
| `HealthyDuration` | `TimeSpan` | 降级前的健康持续时长 |

### `HealthRecoveredEventArgs`
| 属性 | 类型 | 说明 |
|---|---|---|
| `MonitorName` | `string` | 监控器名称 |
| `Timestamp` | `DateTimeOffset` | 状态切换的 UTC 时间 |
| `DegradedDuration` | `TimeSpan` | 恢复前的降级持续时长 |

## 状态机

```
            超过 DegradedThreshold 未收到信号
  健康 ─────────────────────────────────► 降级
       ◄─────────────────────────────────
                  调用 Signal()
```

每个监控器独立追踪自身状态。`BackgroundService` 以所有已注册监控器中最小的 `CheckInterval` 运行单一循环。

## 构建

```bash
dotnet build
dotnet test
```

构建产物位于 `./artifacts/bin/`。
