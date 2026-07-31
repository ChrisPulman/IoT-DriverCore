---
name: ab-plc-rx
description: Implement, review, or troubleshoot Allen-Bradley PLC access with IoT-Driver.ABPlcRx, including libplctag registration, scanning, typed reads and writes, observables, logical tags, simulation, and generated models.
---

# ABPlcRx

## Use this skill when

Use this skill for ControlLogix, CompactLogix, MicroLogix, SLC, or PLC-5 access through libplctag.

Read the co-packaged `../../README.md` first. In a repository checkout use `packagereadme/ABPlcRx/README.md`; inspect `src/ABPlcRx` before describing undocumented members.

## Package choice

- Use `IoT-Driver.ABPlcRx` and `IoT.Driver.ABPlcRx` for the ReactiveUI.Primitives surface.
- Use `IoT-Driver.ABPlcRx.Reactive` and its `.Reactive` namespace only for the System.Reactive-oriented surface.
- Runtime packages never embed the generator. Install `IoT-Driver.ABPlcRx.Generators` separately only when generated models are required.

## Connection and registration

Construct `ABPlcRx` with `PlcType`, IP address, scan interval, and optional timeout/path. Choose `LGX`, `SLC`, or `PLC5` deliberately and verify the Logix route such as `"1,0"` where required.

```csharp
using IoT.Driver.ABPlcRx;

using var plc = new ABPlcRx(
    PlcType.LGX,
    "192.168.1.60",
    TimeSpan.FromMilliseconds(200));

plc.AddUpdateTagItem("Counter", "MyDINT", "Default", 0);

using var errors = plc.ObserveErrors.Subscribe(Console.Error.WriteLine);
using var changes = plc.Observe("Counter", 0, -1)
    .Subscribe(value => Console.WriteLine($"Counter = {value}"));
```

Register each physical tag under a unique logical variable and group before use. The final argument is the generic type witness. For an SLC/PLC-5 word bit, register a `short` and use bit index 0–15; do not use word-bit indexing for a native Logix boolean.

## Read and write workflow

- Prefer cancellation-aware `ReadValueAsync`/`WriteValueAsync` and typed methods for application operations.
- Inspect `PlcTagResult.StatusCode`; use `PlcTagStatus.IsError` and `PlcTagStatus.DecodeError`.
- Use `ReadManyAsync`, `WriteManyAsync`, or grouped scans for multiple variables.
- `Value` updates the cache. With `AutoWriteValue = true` it also writes.
- For deliberate staging, set `AutoWriteValue = false`, set values, then call `Write(variable)` or `Write()`.

```csharp
plc.AutoWriteValue = false;
plc.Value("Counter", 42, -1);
var result = plc.Write("Counter");
if (result is not null && PlcTagStatus.IsError(result.StatusCode))
    throw new InvalidOperationException(PlcTagStatus.DecodeError(result.StatusCode));
```

Treat a staged value as uncommitted until the write result confirms success. Do not retry a non-idempotent command blindly.

## Observation and lifetime

Use `Observe`, `ObserveMany`, grouped streams, sampling, or `IObservableAsync<T>` according to workload. Subscribe to `ObserveErrors` and health/ping streams before operational writes. Dispose every subscription and the client, and pass meaningful cancellation tokens to async I/O.

Use `ScanEnabled` to control registered scans. Start with a conservative interval, group variables by cadence, and avoid high-frequency polling that overloads the controller or route.

## Logical tags

Use `LogicalTagKey<T>` from `IoT.Driver.Core` to keep application names typed. The AB adapter owns libplctag addresses and conversions. Use its catalogue, access policy, CSV, SQLite, and bulk logical-tag surfaces instead of duplicating protocol logic.

## Simulator and generated models

Use `ABPlcSimulator` to exercise registration, reads, writes, scans, batches, disconnect/reconnect behavior, and injected faults without hardware. Assert error delivery and recovery as well as successful values.

For generation, declare the documented partial model and attributes, build once, inspect generator diagnostics and generated members, then test observations, writes, and disposal against the simulator.

## Safety checklist

- Verify PLC type, route, physical address, CLR type, array/string length, and bit index.
- Require controller-side and application-side write interlocks.
- Check every result and subscribe to the error stream.
- Make cancellation, subscription, and client ownership explicit.
- Test timeout, fault, reconnect, and cancellation paths.
- Commission on a safe controller or test rig before production equipment.
