---
name: s7-plc-rx
description: Implement, review, or troubleshoot Siemens S7 integrations with S7PlcRx, including typed tags, addressing, polling, reads and writes, logical tags, diagnostics, batching, bindings, and generation.
---

# S7PlcRx

## Use this skill when

Use this skill for S7-200/300/400/1200/1500 communication over ISO-on-TCP, including tag registration, observables, manual and batched I/O, logical tags, production diagnostics, or generated bindings.

Read the co-packaged `../../README.md`; in a repository checkout use `packagereadme/S7PlcRx/README.md`. Inspect `src/S7PlcRx` before describing undocumented members.

## Package choice

- Use `S7PlcRx` with `IoT.DriverCore.S7PlcRx` by default.
- Use `S7PlcRx.Reactive` and its `.Reactive` namespace for System.Reactive applications.
- Runtime packages do not embed the analyzer. Install `S7PlcRx.Generators` alongside the selected runtime only when generated bindings are required.

## Connect and register

Create `IRxS7` through a CPU factory or `RxS7Options`, then dispose it. Confirm CPU family, IP address, rack, slot, and PLC protection.

```csharp
using IoT.DriverCore.Core;
using IoT.DriverCore.S7PlcRx;
using IoT.DriverCore.S7PlcRx.Enums;
using ReactiveUI.Primitives;

var options = new RxS7Options(
    new S7ConnectionOptions(
        CpuType.S71500,
        "192.168.1.100",
        rack: 0,
        slot: 1));

using var plc = new RxS7(options);

TagOperations.AddUpdateTagItem(
    plc, typeof(float), "Temperature", "DB1.DBD0");
TagOperations.AddUpdateTagItem(
    plc, typeof(bool), "Running", "DB1.DBX4.0");

using var errors = plc.LastError.Subscribe(Console.Error.WriteLine);
using var values = plc.Observe(new LogicalTagKey<float>("Temperature"))
    .Where(value => value.HasValue)
    .Subscribe(value => Console.WriteLine(value));
```

Factory equivalent: `using IRxS7 plc = S71500.Create(...)`.

## Addressing and types

Match address width to the CLR type:

- `DBX` bits use bit indexes 0–7 and `bool`;
- `DBB` is byte-oriented;
- `DBW` maps to 16-bit values;
- `DBD` maps to 32-bit values, while `double` consumes eight bytes;
- `I/E`, `Q/A`, and `M` address inputs, outputs, and markers;
- timers and counters use their documented forms.

Pass array length when registering byte arrays or other variable-sized values. Validate DB layout and optimized/non-optimized access settings on the PLC.

## Read, write, and observe

- Register the tag and correct type before calling `Value<T>`, `ReadAsync<T>`, `Observe<T>`, or writes.
- Use `LogicalTagKey<T>` to prevent name/type drift.
- Prefer batch, optimized, and logical-tag APIs for multiple adjacent values.
- Observe `IsConnected`, `LastError`, and `LastErrorCode`.
- There is no general invented cancellation surface: use only cancellation overloads actually exposed by the selected API.
- Treat `IsDisposed` as state; call `Dispose`/`using` to end the client.

Configure watchdogs only with a reviewed `DBW` address and safe interval. A watchdog is operational equipment interaction, not merely diagnostics.

## Bindings and generation

Use runtime binding APIs after a basic registered-tag workflow is proven. For generated binding, install `S7PlcRx.Generators`, mark binding types partial, use documented attributes, build once, and inspect diagnostics and generated members.

Test generated reads, queued writes, observations, byte-array grouping, conversion, and disposal. Do not assume generated code fixes an incorrect PLC address or CLR type.

## Logical tags and testing

Use driver logical-tag, CSV, SQLite, catalogue, and batch surfaces for protocol-neutral application names. Test conversion and application logic using provided abstractions/mocks and a safe S7 simulator or test PLC; cover disconnect, stale cache, malformed address, timeout, and write rejection.

## Safety checklist

- Verify CPU family, rack/slot, protection, DB layout, address, bit, type, and length.
- Require machine and application interlocks before writes.
- Monitor connection and error streams before operational I/O.
- Keep scan/watchdog rates within controller capacity.
- Dispose the client and every subscription.
- Validate on simulation and a safe rig before production.
