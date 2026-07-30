---
name: twincat-rx
description: Implement, review, or troubleshoot Beckhoff TwinCAT ADS access with CP.TwinCATRx, including routes, settings, notifications, correlated reads and writes, structures, logical tags, simulation, and generation.
---

# TwinCATRx

## Use this skill when

Use this skill for TwinCAT 2/3 ADS connections, notifications, one-shot reads/writes, correlated results, structured symbols, logical tags, service monitoring, in-memory tests, or generated models.

Read the co-packaged `../../README.md`; in a repository checkout use `packagereadme/TwinCATRx/README.md`. Inspect `src/TwinCATRx` and `src/TwinCATRx.Core` before describing undocumented members.

## Package choice

- `CP.TwinCATRx` / `CP.TwinCATRx.Reactive` provide Windows ADS integrations.
- `TwinCATRx.Core` / `TwinCATRx.Core.Reactive` provide core abstractions and testable composition.
- Import `IoT.DriverCore.TwinCATRx` and `IoT.DriverCore.TwinCATRx.Core`, or the corresponding `.Reactive` namespaces.
- Runtime ADS packages embed the generator. Install `TwinCATRx.Generators` only to version it independently; never load duplicate analyzers.

## Configure and connect

Set ADS address, port, and settings ID. Register every notification and writable variable before `Connect`.

```csharp
using IoT.DriverCore.TwinCATRx;
using IoT.DriverCore.TwinCATRx.Core;

using var client = new RxTcAdsClient();
var settings = new Settings
{
    AdsAddress = "5.35.59.10.1.1",
    Port = 851,
    SettingsId = "Default",
};

settings.AddNotification(".AInt");
settings.AddWriteVariable(".AInt");

using var errors = client.ErrorReceived.Subscribe(Console.Error.WriteLine);
using var ready = client.InitializeComplete
    .Subscribe(_ => Console.WriteLine("ADS handles ready"));
using var values = client.Observe<short>(
        ".AInt",
        value => Convert.ToInt16(value))
    .Subscribe(value => Console.WriteLine(value));

client.Connect(settings);
```

Wait for `InitializeComplete` before operational I/O. Validate the local/remote ADS route and runtime port; 851 commonly targets TwinCAT 3 runtime 1 but must not be assumed.

## Reads, writes, and correlation

Use `Read(variable[, id])`, `Write(variable, value[, id])`, and `Observe`/async-observable methods. Use correlation IDs when concurrent one-shot operations need distinct results. `DataReceived` contains variable, data, and ID; `OnWrite` reports written variable names.

Subscribe to `ErrorReceived` before `Connect`. Synchronous ADS operations do not expose a general cancellation token: cancel higher-level observation/composition by disposing subscriptions or the client.

Register a positive size for strings and arrays when ADS cannot infer it. Verify value type, string encoding/length, array length, structure layout, and symbol name before writes.

## Structures and dynamic code

Use `CreateStruct` and coordinated `WriteValues` only with validated layouts. Dynamic materialization carries trimming/AOT annotations; preserve/register required members and validate the published application rather than suppressing warnings.

## Logical tags and lifetime

Compose logical-tag clients from the Core package so application code uses `LogicalTagKey<T>` while ADS symbol names stay in the adapter. Use bulk/group APIs for compatible work.

`Disconnect()` releases ADS handles and permits reconnect. `Dispose()` is terminal. Observe `Connected`, `IsDisposed`, pause state, and handle information; dispose every subscription and client.

## In-memory testing and generation

Use `InMemoryAdsClient` to define symbols and test notification flow, correlated reads/writes, faults, reconnection, pause windows, metrics, and disposal without an ADS runtime.

For generation, choose `[TwinCatReactiveStream]` or `[TwinCatPlcConnection]` deliberately, declare documented direct/structured/write-only members on partial types, build once, inspect diagnostics and generated members, then test against `InMemoryAdsClient`.

## Safety checklist

- Verify ADS route, target runtime port, settings ID, symbol name, type, and size.
- Register notifications/writes before connection and wait for initialization.
- Require machine and application interlocks for writes.
- Preserve correlation context and observe errors.
- Address trimming/AOT diagnostics correctly.
- Validate with `InMemoryAdsClient` and safe TwinCAT test hardware.
