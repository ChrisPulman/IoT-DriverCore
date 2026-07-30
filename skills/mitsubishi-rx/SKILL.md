---
name: mitsubishi-rx
description: Implement, review, or troubleshoot Mitsubishi MC Protocol and SLMP integrations with MitsubishiRx, including Ethernet and serial frames, device and tag operations, observations, logical tags, simulation, and generation.
---

# MitsubishiRx

## Use this skill when

Use this skill for MELSEC MC Protocol or SLMP using 1E/3E/4E Ethernet frames or 1C/3C/4C serial frames over TCP, UDP, or serial transports.

Read the co-packaged `../../README.md`; in a repository checkout use `packagereadme/MitsubishiRx/README.md`. Inspect `src/MitsubishiRx` before describing an API because the reactive build changes namespaces through shared source.

## Package choice

- Use `MitsubishiRx` with `IoT.DriverCore.MitsubishiRx` by default.
- Use `MitsubishiRx.Reactive` and its `.Reactive` namespace for System.Reactive applications.
- Both runtime packages carry an analyzer, but the current generator emits the base namespace and requires the base runtime.
- Install `MitsubishiRx.Generators` only with the base runtime when independently versioning the analyzer. Never load duplicate analyzers.

## Configure and open

Match `FrameType`, `DataCode`, `TransportKind`, route, monitoring timer, CPU hint, and X/Y notation to the PLC or communication module. For serial, provide validated `MitsubishiSerialOptions`.

```csharp
using IoT.DriverCore.MitsubishiRx;

var options = new MitsubishiClientOptions(
    Host: "192.168.0.10",
    Port: 5000,
    FrameType: MitsubishiFrameType.ThreeE,
    DataCode: CommunicationDataCode.Binary,
    TransportKind: MitsubishiTransportKind.Tcp,
    Timeout: TimeSpan.FromSeconds(3));

await using var plc = new MitsubishiRx(options, transport: null, scheduler: null);
var opened = await plc.OpenAsync(CancellationToken.None);
if (!opened.IsSucceed)
    throw new InvalidOperationException(opened.Err);
```

Do not infer framing from the CPU family alone. Confirm network/serial module settings and routing.

## Responses and I/O

Operations return `Responce` or `Responce<T>`. Always check `IsSucceed` before reading `Value`, and report `Err` with operation/address context.

```csharp
var read = await plc.ReadWordsAsync("D100", 2, CancellationToken.None);
if (!read.IsSucceed || read.Value is null)
    throw new InvalidOperationException(read.Err);

var write = await plc.WriteWordsAsync(
    "D100",
    new ushort[] { 42 },
    CancellationToken.None);
if (!write.IsSucceed)
    throw new InvalidOperationException(write.Err);
```

Use word/bit APIs for direct devices and typed tag APIs when the tag database owns conversion. Batch contiguous addresses, keep X/Y notation explicit, and serialize or coalesce competing writes.

## Tags, observation, and logical names

Create and validate tag definitions before polling. Use `ConnectionStates` and `OperationLogs` for operational visibility. Dispose observation subscriptions.

Use `CreateLogicalTagClient` to compose protocol-neutral `LogicalTagKey<T>` workflows. Keep MC/SLMP device syntax in the adapter and use catalogue access modes to reject unintended writes.

## Diagnostics and sensitive commands

Treat PLC run/stop/reset control, password operations, memory commands, raw commands, and writes as safety-sensitive. Subscribe to logs/state before invoking them, require explicit authorization and machine interlocks, and preserve an audit trail.

Pass cancellation tokens to open and I/O operations. `await using` the client so the transport and polling work are released.

## Simulation and generation

Use `MitsubishiSimulatorTransport` and `MitsubishiSimulatorMemory` for deterministic tests of reads, writes, tag conversion, batches, logging, cancellation, timeout, and injected failures.

For generated clients, define only supported Mitsubishi attributes, build once to inspect diagnostics and the generated base-namespace surface, then test generated reads, writes, observations, and disposal against the simulator.

## Safety checklist

- Verify frame, binary/ASCII data code, transport, route, station, and serial format.
- Validate device syntax, X/Y notation, value type, word order, and range.
- Check `IsSucceed` before every `Value`.
- Batch only compatible contiguous addresses.
- Protect every control/write operation with interlocks and authorization.
- Commission with simulator and safe test hardware before production.
