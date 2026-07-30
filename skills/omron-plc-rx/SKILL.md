---
name: omron-plc-rx
description: Implement, review, or troubleshoot Omron FINS TCP, UDP, Host Link FINS, and Toolbus serial integrations with typed tags, logical tags, clock and memory operations, simulation, and generated bindings.
---

# OmronPlcRx

## Use this skill when

Use this skill for typed Omron FINS network access, Host Link FINS or Toolbus serial access, polling, logical tags, memory-area batches, PLC clock operations, simulators, or generated bindings.

Read the co-packaged `../../README.md`; in a repository checkout use `packagereadme/OmronPlcRx/README.md`. Inspect `src/OmronPlcRx` before describing undocumented members.

## Package choice

- Use `OmronPlcRx` and `IoT.DriverCore.OmronPlcRx` by default.
- Use `OmronPlcRx.Reactive` and its `.Reactive` namespace for System.Reactive applications.
- Runtime packages embed their matching generator. Install `OmronPlcRx.Generators` only to version it independently, and never load a duplicate analyzer.

## Network workflow

Create `OmronConnectionOptions(localNodeId, remoteNodeId, connectionMethod, remoteHost)` and set port, timeout, retries, and optional serial settings.

```csharp
using IoT.DriverCore.Core;
using IoT.DriverCore.OmronPlcRx;
using IoT.DriverCore.OmronPlcRx.Enums;
using IoT.DriverCore.OmronPlcRx.Tags;

var options = new OmronConnectionOptions(
    11, 1, ConnectionMethod.UDP, "192.168.250.1")
{
    Port = 9600,
    Timeout = 2000,
    Retries = 1,
};

using var plc = new OmronPlcRx(options, TimeSpan.FromMilliseconds(200));
using var errors = plc.Errors.Subscribe(Console.Error.WriteLine);

plc.AddUpdateTagItem(new PlcTag<bool>("MotorRun", "D100.0"));
plc.AddUpdateTagItem(new PlcTag<short>("Temperature", "D200"));

var motor = new LogicalTagKey<bool>("MotorRun");
await plc.WriteValueAsync(motor, true, CancellationToken.None);
var value = await plc.ReadValueAsync(
    new LogicalTagKey<short>("Temperature"),
    CancellationToken.None);
```

The `PlcTag<T>` name and `LogicalTagKey<T>` name/type must match. Register before read, write, cache access, or observation.

## Serial workflow

Use `OmronSerialOptions` for Host Link FINS or `OmronSerialOptions.CreateToolbus("COM3")` for Toolbus. Call `Validate()` when composing settings dynamically.

Host Link defaults to 9600/7E2; Toolbus uses its documented 115200/8N1/RTS settings. Confirm frame mode, node IDs, station, port, and PLC configuration. Classic C-mode Host Link is not the surface exposed by this driver.

## Tags, values, and errors

- Use `Observe(LogicalTagKey<T>)` for cached/polled changes.
- Prefer `ReadValueAsync` and `WriteValueAsync` for commands with explicit completion.
- Treat `SetValue` as queued background work, not proof that the PLC accepted a write.
- Use `GetValue` only after registering the tag and confirming its type.
- Subscribe to `Errors` before connection activity and include tag/address context in diagnostics.
- Dispose subscriptions and the client; pass cancellation tokens to async commands.

## Memory and clock operations

Use documented bulk word/bit memory-area APIs for contiguous work instead of loops. Verify Omron area, address, bit offset, element type, BCD rules, and count.

Clock read/write and cycle-time commands affect or expose controller state. Verify PLC mode and privileges, validate BCD conversion, and protect clock writes with explicit authorization.

## Logical tags and persistence

Use the driver logical-tag client and `IoT.DriverCore.Core` catalogues, typed keys, CSV, SQLite, and batch APIs. Keep FINS/serial syntax and codecs inside the Omron adapter.

## Simulation and generation

Use the Omron simulator and Host Link/Toolbus codec tests to exercise tag polling, reads/writes, word/bit batches, timeouts, retries, malformed frames, BCD conversion, and cancellation without hardware.

For generated bindings, declare documented partial types/attributes, build once, inspect diagnostics and generated members, then verify reads, writes, observation, error handling, and disposal against simulation.

## Safety checklist

- Verify local/remote node IDs, method, endpoint, serial settings, and frame mode.
- Verify memory area, address, bit, type, count, and BCD conversion.
- Treat queued setters as unconfirmed until a read or explicit result verifies state.
- Protect writes and clock commands with PLC/application interlocks.
- Keep poll rates conservative and dispose all resources.
- Commission with simulation and a safe PLC or test rig.
