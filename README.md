# updog_unity_client

Unity client for [Updog](https://wuzupdog.com) error reporting and custom metrics.

This package is intentionally opt-in. It does not auto-start from a player
client. Call `Updog.Initialize(...)` only from the Unity process that should
report errors, such as a dedicated server, world server, zone server, or editor
tool.

## Installation

Add the package to `Packages/manifest.json`:

```json
{
  "dependencies": {
    "com.wuzupdog.updog-unity-client": "https://github.com/wuzupdog/updog_unity_client.git#v0.3.3"
  }
}
```

## Configuration

```csharp
using System;
using System.Collections.Generic;
using Updog.Unity;

Updog.Initialize(new UpdogConfig
{
    ApiKey = Environment.GetEnvironmentVariable("UPDOG_API_KEY"),
    Endpoint = "https://wuzupdog.com",
    Environment = "production",
    Service = "world-server",
    Release = "1.2.3",
    Hostname = "world-01",
    StatsdEndpoint = "127.0.0.1:8125",
    ContextProvider = () => new Dictionary<string, object>
    {
        ["server_short_name"] = "live",
        ["zone_hid"] = "evergrove"
    }
});
```

| Option | Default | Description |
|--------|---------|-------------|
| `ApiKey` | `UPDOG_API_KEY` | Your Updog project API key |
| `Endpoint` | `https://wuzupdog.com` | Updog server URL |
| `Environment` | `UPDOG_ENVIRONMENT` or `production` | Environment name |
| `Service` | `UPDOG_SERVICE` or `Application.productName` | Service/process name |
| `Release` | `UPDOG_RELEASE` or `Application.version` | Release/build version |
| `Hostname` | `UPDOG_HOSTNAME` or system hostname | Hostname attached to error and metric telemetry |
| `StatsdEndpoint` | `UPDOG_STATSD_ENDPOINT` or disabled | Local Updog host-agent StatsD endpoint |
| `CaptureUnityLogs` | `true` | Capture `LogType.Error`, `Exception`, and `Assert` |
| `MaxQueueSize` | `2048` | Maximum pending notices kept in memory |
| `MaxQueueBytes` | `8 MiB` | Maximum encoded queue memory |
| `MaxRecordBytes` | `64 KiB` | Maximum encoded notice size |
| `MaxBatchSize` | `512` | Maximum notices per request |
| `MaxBatchBytes` | `512 KiB` | Maximum encoded notice bytes per request |
| `FlushIntervalSeconds` | `5` | Periodic partial-batch flush interval |
| `TimeoutSeconds` | `5` | Per-request HTTP timeout |
| `MaxRetries` | `3` | Retries after the initial request |
| `ContextProvider` | `null` | Callback for dynamic context fields |

The following environment variables are also recognized:

- `UPDOG_API_KEY`
- `UPDOG_ENDPOINT`
- `UPDOG_ENVIRONMENT`
- `UPDOG_SERVICE`
- `UPDOG_RELEASE`
- `UPDOG_HOSTNAME`
- `UPDOG_STATSD_ENDPOINT`
- `UPDOG_ENABLED=false`
- `UPDOG_DISABLED=true`

## Manual reporting

```csharp
try
{
    DoWork();
}
catch (Exception ex)
{
    Updog.Notify(ex, new Dictionary<string, object>
    {
        ["operation"] = "DoWork"
    });

    throw;
}
```

Capture is fire-and-forget: `Notify` only serializes and adds the notice to a bounded in-memory queue on the Unity main thread. A single coroutine bulk-submits notices, so there is at most one in-flight error request. Full queues and oversized notices are dropped rather than blocking or growing memory without limit; no disk spool is enabled by default.

## Custom metrics through the host agent

Run [`updog-agent`](https://github.com/wuzupdog/updog_agent) on the server and set `UPDOG_STATSD_ENDPOINT=127.0.0.1:8125`. The Unity process sends local UDP metrics to that agent, while the project ingestion key stays only in the agent's root-readable systemd environment file.

```csharp
Updog.ReportMetric(
    "zone.players",
    350,
    tags: new Dictionary<string, string> { ["zone"] = "night-harbor" });

Updog.ReportMetric("zone.tick", 43, type: "timer", unit: "ms");
```

Metrics share the bounded in-memory queue limits with errors but have lower priority. They are emitted as tagged StatsD gauges, counters, or timers to the configured local agent; Unity does not perform metric HTTP requests or need an ingestion key when only metrics are enabled.

Network failures, `408`, `429`, and `5xx` are retried three times with full-jitter exponential backoff. The client honors `Retry-After`, preserves the request ID across retries, splits a batch after `413`, and does not retry permanent client errors.

Use a bounded coroutine flush before ending a short-lived session:

```csharp
yield return Updog.Flush(5f);
var stats = Updog.DeliveryStats();
Updog.Shutdown(5f);
```

`Shutdown` stops capture immediately and gives queued telemetry up to the timeout to finish before destroying the persistent reporter object.

## Server-only Unity projects

For a combined client/server Unity project, put `Updog.Initialize(...)` in the
server entry points only. Do not initialize it from player-client scenes or
client-only assemblies. Do not ship an Updog API key or configure a StatsD
endpoint in end-user client builds.
