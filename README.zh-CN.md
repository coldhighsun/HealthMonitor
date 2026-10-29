# HealthMonitor（中文文档）

[English](README.md) | 简体中文

[![CI](https://github.com/coldhighsun/HealthMonitor/actions/workflows/ci.yml/badge.svg)](https://github.com/coldhighsun/HealthMonitor/actions/workflows/ci.yml)
[![NuGet 版本](https://img.shields.io/nuget/v/HealthMonitor.Core)](https://www.nuget.org/packages/HealthMonitor.Core)
[![NuGet 下载量](https://img.shields.io/nuget/dt/HealthMonitor.Core)](https://www.nuget.org/packages/HealthMonitor.Core)
[![License: MIT](https://img.shields.io/badge/License-MIT-blue.svg)](LICENSE)

轻量级 .NET 健康监控库，通过周期性心跳信号判断组件是否存活，在状态切换时触发 `Degraded` / `Recovered` 事件。支持依赖注入、多个命名监控器。

## 功能特性

- **多监控器** — 可注册任意数量的独立监控器，每个监控器拥有独立的降级阈值和检查间隔
- **两种事件** — 心跳超时触发 `Degraded`；降级期间收到信号立即触发 `Recovered`
- **DI 优先** — 集成 `IServiceCollection`，以 `BackgroundService` 形式运行
- **动态管理** — `DynamicHealthMonitorManager` 支持运行时动态添加/移除监控器，每个监控器有独立 `Timer` 和配置
- **按名称注入** — 通过 `IServiceProvider.GetRequiredKeyedService<IHealthMonitor>("name")` 按名称解析（.NET 8+）
- **易于测试** — `IStopwatch` 和 `ISystemTimeProvider` 抽象解耦了时间依赖

## 支持的目标框架

| 目标框架 | 键控 DI（`GetRequiredKeyedService`） |
|---|---|
| `netstandard2.0` | — |
| `net8.0` | ✓ |

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

**`netstandard2.0` — 不使用键控服务按名称解析：**

`netstandard2.0` 下不支持键控 DI，可通过 `IEnumerable<IHealthMonitor>` 按名称筛选：

```csharp
var monitors = provider.GetRequiredService<IEnumerable<IHealthMonitor>>();
var monitor  = monitors.First(m => m.Name == "quote-feed");

monitor.Degraded  += (_, e) => Console.WriteLine($"{e.MonitorName}: 降级");
monitor.Recovered += (_, e) => Console.WriteLine($"{e.MonitorName}: 恢复");
```

也可以使用 `DynamicHealthMonitorManager`，它完全不依赖 DI，在 `netstandard2.0` 上同样可用。

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

## 动态监控管理

`DynamicHealthMonitorManager` 支持在运行时动态注册和移除监控器，无需 DI 或托管服务。每个监控器拥有独立的 `System.Threading.Timer`，按自身的 `CheckInterval` 周期触发检查。

```csharp
using var manager = new DynamicHealthMonitorManager();

// 可在任意时机订阅 — 对所有已注册的监控器生效，无论添加顺序
manager.Degraded  += (_, e) => Console.WriteLine($"{e.MonitorName} 已降级");
manager.Recovered += (_, e) => Console.WriteLine($"{e.MonitorName} 已恢复");

// 随时添加监控器，每个监控器使用自己的 Timer 和 CheckInterval
var db = manager.Add("database", new HealthMonitorOptions
{
    DegradedThreshold = TimeSpan.FromSeconds(10),
    CheckInterval     = TimeSpan.FromSeconds(2),
});

// 在业务逻辑中发送心跳
db.Signal();

// 在运行时动态添加更多监控器
var cache = manager.Add("cache", new HealthMonitorOptions
{
    DegradedThreshold = TimeSpan.FromSeconds(5),
    CheckInterval     = TimeSpan.FromSeconds(1),
});

// 移除监控器 — 立即销毁其 Timer 并取消事件转发
manager.Remove("cache");

// 枚举所有活跃监控器
foreach (var m in manager.Monitors)
    Console.WriteLine($"{m.Name}: {(m.IsHealthy ? "健康" : "降级")}");
```

`DynamicHealthMonitorManager` 实现了 `IDisposable`，Dispose 时会停止所有计时器。

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
