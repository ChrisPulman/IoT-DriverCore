<p align="center">
  <img src="https://raw.githubusercontent.com/ChrisPulman/IoT-DriverCore/main/images/omron-plc-rx.png" alt="OmronPlcRx package logo" width="320" />
</p>

# OmronPlcRx

## Overview

`OmronPlcRx` is a typed, reactive .NET driver for Omron FINS over TCP, UDP, Host Link FINS serial, and Toolbus serial. Register a `PlcTag<T>`, address it through a `LogicalTagKey<T>`, then read, write, or observe it. The base package uses `ReactiveUI.Primitives`; `OmronPlcRx.Reactive` is the equivalent System.Reactive-oriented surface. Both are shared-source builds.

The public namespaces are `IoT.DriverCore.OmronPlcRx` and its `.Core`, `.Core.Types`, `.Enums`, `.Results`, `.Tags`, and `.Async` children. The reactive package replaces the root with `IoT.DriverCore.OmronPlcRx.Reactive`.

## Safety

PLC writes can change machinery state. Validate addresses, types, interlocks, ownership, and the target controller in a non-production environment first. Subscribe to `Errors`, use cancellation tokens and bounded timeouts, and prefer `WriteValueAsync` when a command must be observed. `SetValue` queues a background write; failures are reported through `Errors` rather than returned to the caller.

## Package matrix

| Package | Use |
| --- | --- |
| `OmronPlcRx` | Base API using ReactiveUI.Primitives and BCL `IObservable<T>`. |
| `OmronPlcRx.Reactive` | Same driver shape for System.Reactive consumers; namespaces end in `.Reactive`. |
| `OmronPlcRx.Generators` | Roslyn generator package; the runtime packages reference it as an analyzer. |

All runtime packages target `net462`, `net472`, `net481`, `net8.0`, `net9.0`, `net10.0`, and `net11.0`.

## Install

```bash
dotnet add package OmronPlcRx
# Or, for System.Reactive applications:
dotnet add package OmronPlcRx.Reactive
```

## Quick start

The public network constructor takes `OmronConnectionOptions` and an explicit nullable poll interval.

```csharp
using IoT.DriverCore.Core;
using IoT.DriverCore.OmronPlcRx;
using IoT.DriverCore.OmronPlcRx.Enums;
using IoT.DriverCore.OmronPlcRx.Tags;

var motorRun = new PlcTag<bool>("MotorRun", "D100.0");
var temperature = new PlcTag<short>("Temperature", "D200");
var options = new OmronConnectionOptions(11, 1, ConnectionMethod.UDP, "192.168.250.1")
{
    Port = 9600,
    Timeout = 2000,
    Retries = 1,
};

using var plc = new OmronPlcRx(options, TimeSpan.FromMilliseconds(200));
plc.AddUpdateTagItem(motorRun);
plc.AddUpdateTagItem(temperature);

var motor = new LogicalTagKey<bool>("MotorRun");
using var subscription = plc.Observe(motor).Subscribe(value => Console.WriteLine(value));

await plc.WriteValueAsync(motor, true, CancellationToken.None);
short? value = await plc.ReadValueAsync(new LogicalTagKey<short>("Temperature"), CancellationToken.None);
```

## Configuration

Create `OmronConnectionOptions(localNodeId, remoteNodeId, connectionMethod, remoteHost)`. Its init-only settings are `Port` (9600), `Timeout` milliseconds (2000), `Retries` (1), and optional `SerialOptions`.

For serial, use the dedicated constructor and supply every parameter:

```csharp
using IoT.DriverCore.OmronPlcRx;

var serial = OmronSerialOptions.CreateToolbus("COM3");
using var plc = new OmronPlcRx(11, 0, serial, 2000, 1, TimeSpan.FromMilliseconds(250));
```

`OmronSerialOptions` defaults to Host Link FINS (`9600`, `7E2`, no handshake). `CreateToolbus` sets Toolbus, `115200`, `8N1`, and RTS. Call `Validate()` before connecting when options are composed dynamically. Host Link supports `Direct` and `Network` `OmronHostLinkFinsFrameMode` values; classic C-mode Host Link is not the API exposed by this driver.

FINS addresses supported by the typed tag codec include bit forms such as `D100.0` and word forms such as `D200`; strings use `D600[20]`. Use a tag type matching PLC storage: `bool`, integer types, `float`, `double`, `string`, and `Bcd16`, `BcdU16`, `Bcd32`, or `BcdU32`.

## Detailed features

### Typed tags, polling, and fault handling

`ObserveAll` emits changed `IPlcTag` instances and `Errors` emits `OmronPLCException`. Use a logical key whose type exactly matches the registered tag.

```csharp
using IoT.DriverCore.Core;
using IoT.DriverCore.OmronPlcRx.Tags;

var level = new PlcTag<float>("TankLevel", "D400");
plc.AddUpdateTagItem(level);

using var changes = plc.ObserveAll.Subscribe(tag =>
    Console.WriteLine($"{tag?.TagName}: {tag?.Value}"));
using var errors = plc.Errors.Subscribe(error => Console.Error.WriteLine(error?.Message));

var levelKey = new LogicalTagKey<float>("TankLevel");
plc.SetValue(levelKey, 12.5f);                 // fire-and-forget
await plc.WriteValueAsync(levelKey, 12.5f, CancellationToken.None); // awaited command
float? cached = plc.GetValue(levelKey);
bool removed = plc.RemoveTagItem("TankLevel");
```

### Clock and scan-time operations

```csharp
var clock = await plc.ReadClockAsync(CancellationToken.None);
Console.WriteLine(clock.Clock);

await plc.WriteClockAsync(DateTimeOffset.UtcNow, CancellationToken.None);
var cycle = await plc.ReadCycleTimeAsync(CancellationToken.None);
Console.WriteLine($"min={cycle.MinimumCycleTime}, max={cycle.MaximumCycleTime}");
```

`ReadClockResult`, `WriteClockResult`, `ReadCycleTimeResult`, `ReadBitsResult`, `ReadWordsResult`, `WriteBitsResult`, and `WriteWordsResult` expose transport counters, duration, and their operation-specific payload.

### Async observation and simulation

`OmronPlcRxAsyncObservableExtensions` provides `ObserveAsAsyncObservable`, `ObserveAllAsAsyncObservable`, `ErrorsAsAsyncObservable`, and `ObserveValuesAsync`. Use `OmronPlcSimulator` for deterministic application tests: construct it, `Seed(tag, value)`, optionally `QueueFault`, then exercise the same `IOmronPlcRx` API. It records `OmronSimulatorOperationRecord` values in `Operations`.

### Source-generated bindings

Apply `PlcTagAttribute` to a field in a partial class. The included analyzer generates registration, observation, and optional write helpers. Its configurable members are `TagName`, `Register`, `Observe`, and `Writable`; `PlcTagBindingAttribute` marks a binding target. Treat generated member names as analyzer output and keep the containing type partial.

## Exhaustive feature guide and worked workflows

The driver uses a *definition* (`PlcTag<T>`) and a *lookup key* (`LogicalTagKey<T>`). A definition has a logical `TagName` and FINS `Address`; a key contains only the logical name and must use the same `T`. This makes wrong address/type combinations visible at registration rather than allowing untyped strings to flow through every command. The reactive package has the same shape beneath `IoT.DriverCore.OmronPlcRx.Reactive`; do not mix base and reactive tag/key types.

### Network constructors, serial constructors, and options validation

`OmronConnectionOptions` is the network constructor input: local node ID, remote node ID, `ConnectionMethod.TCP` or `UDP`, remote host, optional port, timeout, retry count, and optional serial options. Its values are immutable after construction except through the initializer, so build and validate it before creating a long-lived client. The serial constructor takes `(localNodeId, remoteNodeId, OmronSerialOptions, timeout, retries, pollInterval)`. `pollInterval` is nullable: provide a positive interval for automatic tag polling; use `null` only when the application performs direct reads deliberately.

```csharp
using IoT.DriverCore.OmronPlcRx;
using IoT.DriverCore.OmronPlcRx.Enums;

var options = new OmronConnectionOptions(11, 1, ConnectionMethod.TCP, "192.168.250.1")
{
    Port = 9600,
    Timeout = 2_000,
    Retries = 2,
};

using var plc = new OmronPlcRx(options, TimeSpan.FromMilliseconds(250));

var serialOptions = OmronSerialOptions.CreateToolbus("COM3");
serialOptions.Validate();
using var serialPlc = new OmronPlcRx(11, 0, serialOptions, 2_000, 1,
    TimeSpan.FromMilliseconds(250));
```

`OmronSerialOptions` models COM port, baud rate, data bits, parity, stop bits, handshake, Host Link unit number, response wait, maximum frame length, protocol, and Host Link FINS frame mode. The default is Host Link FINS at `9600 7E2`. `CreateToolbus` changes the protocol and standard Toolbus framing (`115200 8N1` with RTS). `HostLinkFinsFrameMode.Direct` is direct CPU framing; `Network` carries the full routed FINS header. `Validate()` throws for invalid combinations before a connection is attempted; communication/PLC errors arrive as `OmronPLCException` through `Errors` or from awaited calls.

### Addressing, supported values, BCD, and codec behaviour

Use FINS addresses with the data area prefix: `D100.0` is one data-memory bit, `D200` one word, and `D600[20]` a string region. Match the tag generic type to the physical storage. Supported codecs include `bool`, signed/unsigned integral types, `float`, `double`, `string`, and `Bcd16`, `BcdU16`, `Bcd32`, `BcdU32`. BCD wrappers state that a value is decimal packed BCD rather than a normal signed binary integer; construct them from the logical numeric value. `BCDConverter` is the explicit conversion utility when an application must inspect raw BCD words. `MemoryBitDataType` and `MemoryWordDataType` describe areas used by lower-level FINS operations, while `PlcType` is detected controller metadata.

```csharp
using IoT.DriverCore.Core;
using IoT.DriverCore.OmronPlcRx.Core.Types;
using IoT.DriverCore.OmronPlcRx.Tags;

plc.AddUpdateTagItem(new PlcTag<bool>("PumpEnabled", "D100.0"));
plc.AddUpdateTagItem(new PlcTag<int>("BatchCount", "D300"));
plc.AddUpdateTagItem(new PlcTag<string>("RecipeName", "D600[20]"));
plc.AddUpdateTagItem(new PlcTag<Bcd16>("TemperatureBcd", "D700"));

var bcdKey = new LogicalTagKey<Bcd16>("TemperatureBcd");
await plc.WriteValueAsync(bcdKey, new Bcd16(235), CancellationToken.None);
Bcd16? displayed = await plc.ReadValueAsync(bcdKey, CancellationToken.None);
Console.WriteLine(displayed?.Value);
```

### Registration, cache, observations, and direct commands

`AddUpdateTagItem(PlcTag<T>)` registers or updates the logical definition. `RemoveTagItem(name)` returns `true` only if a definition existed. `GetValue(key)` is cached and therefore never a substitute for a command read. `Observe(key)` emits changed values for one registered definition, while `ObserveAll` emits every changed `IPlcTag`. `SetValue(key, value)` starts a background write to preserve compatibility with reactive UIs; it has no completion result. Use `WriteValueAsync` for a command that requires acknowledgement, cancellation, or a catchable failure. `ReadValueAsync` directly reads and updates the cache. Keep `Errors` subscribed for polling/queued-write failures; error streams and subscriptions must be disposed independently of the facade.

```csharp
using IoT.DriverCore.Core;
using IoT.DriverCore.OmronPlcRx.Tags;

var pump = new LogicalTagKey<bool>("PumpEnabled");
var batch = new LogicalTagKey<int>("BatchCount");
using var changes = plc.Observe(pump).Subscribe(value => Console.WriteLine($"pump={value}"));
using var all = plc.ObserveAll.Subscribe(tag => Console.WriteLine($"{tag?.TagName}={tag?.Value}"));
using var errors = plc.Errors.Subscribe(error => Console.Error.WriteLine(error?.Message));

plc.SetValue(pump, true); // queue-only: observe Errors for failure
using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(2));
try
{
    int? count = await plc.ReadValueAsync(batch, deadline.Token);
    await plc.WriteValueAsync(pump, count is not null, deadline.Token);
}
catch (OperationCanceledException) { Console.Error.WriteLine("PLC command timed out."); }
catch (OmronPLCException ex) { Console.Error.WriteLine(ex.Message); }
```

### PLC clock and cycle-time commands

`ReadClockAsync(token)` returns `ReadClockResult`, whose clock value and operation metadata describe the reply. `WriteClockAsync(DateTimeOffset, token)` infers the day of week. `WriteClockAsync(DateTimeOffset, int dayOfWeek, token)` is the explicit overload; day of week must be 0–6. Both return `WriteClockResult`. `ReadCycleTimeAsync(token)` returns `ReadCycleTimeResult` with the controller's min/max/average cycle information. These calls operate directly on the controller and should be used sparingly during production; treat cancellation as an incomplete operation rather than proof it was not received.

```csharp
using var commandDeadline = new CancellationTokenSource(TimeSpan.FromSeconds(5));
try
{
    var before = await plc.ReadClockAsync(commandDeadline.Token);
    Console.WriteLine($"PLC time: {before.Clock:O}");

    var clockWrite = await plc.WriteClockAsync(DateTimeOffset.UtcNow, commandDeadline.Token);
    var cycle = await plc.ReadCycleTimeAsync(commandDeadline.Token);
    Console.WriteLine($"cycle min={cycle.MinimumCycleTime}, max={cycle.MaximumCycleTime}");
}
catch (OperationCanceledException) { Console.Error.WriteLine("Clock/cycle request cancelled."); }
catch (FINSException ex) { Console.Error.WriteLine($"FINS rejected request: {ex.Message}"); }
```

### Async enumeration and logical tags

`OmronPlcRxAsyncObservableExtensions` bridges `Observe`, `ObserveAll`, and `Errors` into `IObservableAsync<T>` through `ObserveAsAsyncObservable`, `ObserveAllAsAsyncObservable`, and `ErrorsAsAsyncObservable`; `ObserveValuesAsync` supplies asynchronous value enumeration. Each wrapper still owns/creates a subscription, so pass a cancellation token to consumers and dispose the client at application shutdown.

`OmronLogicalTagClient` is a higher-level catalog that creates/registers tags, reads/writes one or many logical values, exposes observable and async enumeration, and supports store/import/export/group CRUD where configured. It adds planning/batching and persistence convenience; it does not change the atomicity guarantees of the physical PLC. Use the core facade when a physical address/type must be explicit at the call site, and use logical tags when a reviewed catalog owns that mapping.

```csharp
// A direct stream can be consumed as async enumeration with cancellation.
using var readWindow = new CancellationTokenSource(TimeSpan.FromSeconds(10));
await foreach (var value in plc.ObserveValuesAsync(new LogicalTagKey<int>("BatchCount"), readWindow.Token))
{
    Console.WriteLine($"batch={value}");
    if (value is > 1000) break;
}
```

When an application is already built on `IObservableAsync<T>`, convert individual values, all-tag changes, or driver errors with `OmronPlcRxAsyncObservableExtensions`. Convert back with `ObservableAsyncBridgeExtensions.ToObservable` only at the boundary where a classic `IObservable<T>` subscriber is required. The async stream does not turn a queued `SetValue` into an acknowledged command; use `WriteValueAsync` for that.

```csharp
using IoT.DriverCore.Core;
using IoT.DriverCore.OmronPlcRx.Async;
using IoT.DriverCore.Serial;

var levelKey = new LogicalTagKey<float>("TankLevel");
var asyncLevels = plc.ObserveAsAsyncObservable(levelKey);
using var levels = ObservableAsyncBridgeExtensions.ToObservable(asyncLevels)
    .Subscribe(level => Console.WriteLine($"tank level={level}"));
using var errors = ObservableAsyncBridgeExtensions.ToObservable(plc.ErrorsAsAsyncObservable())
    .Subscribe(error => Console.Error.WriteLine(error?.Message));
```

Use `ObserveValuesAsync` when a sequential consumer is clearer than a callback. Cancellation ends the enumerable and the client still owns the polling/connection lifetime.

```csharp
using var stop = new CancellationTokenSource(TimeSpan.FromSeconds(30));
await foreach (float? level in plc.ObserveValuesAsync(levelKey, stop.Token))
{
    if (level is > 90f)
        Console.WriteLine("High-level alarm policy should run here.");
}
```

### Host Link and Toolbus codec testing

`HostLinkFinsFrameCodec` and `ToolbusFinsFrameCodec` are framing utilities for serial adapters and protocol tests. Normal application traffic should use `OmronPlcRx` with `OmronSerialOptions`; do not send codec output through an arbitrary serial port without the expected serial protocol. Host Link validates FCS/unit/header/end-code on decode; Toolbus validates start byte, declared length, checksum, and the FINS response header.

```csharp
using IoT.DriverCore.OmronPlcRx;

var hostLinkOptions = new OmronSerialOptions("COM3")
{
    FrameMode = OmronHostLinkFinsFrameMode.Network,
};
var hostLink = new HostLinkFinsFrameCodec(hostLinkOptions);

// A FINS request always has a 10-byte header followed by a 2-byte command code.
ReadOnlyMemory<byte> finsRequest = new byte[12] { 0x80, 0, 2, 0, 0, 1, 0, 0, 11, 1, 1, 1 };
string hostLinkRequest = hostLink.EncodeRequest(finsRequest);
Memory<byte> toolbusRequest = ToolbusFinsFrameCodec.EncodeRequest(finsRequest);
Console.WriteLine(hostLinkRequest); // ends with FCS + "*\\r"
Console.WriteLine(ToolbusFinsFrameCodec.CalculateChecksum(toolbusRequest.Span[..^2]));

// Build minimal complete response frames for a codec unit test.
var finsResponse = new byte[14];
finsResponse[0] = 0xC0;
string hostLinkBody = "@00FA00" + BitConverter.ToString(finsResponse).Replace("-", string.Empty);
string hostLinkResponse = hostLinkBody + HostLinkFinsFrameCodec.CalculateFcs(hostLinkBody) + "*\r";
Memory<byte> finsReply = hostLink.DecodeResponse(hostLinkResponse);

var toolbusResponse = new byte[3 + finsResponse.Length + 2];
toolbusResponse[0] = 0xAB;
toolbusResponse[1] = 0;
toolbusResponse[2] = (byte)(finsResponse.Length + 2);
finsResponse.CopyTo(toolbusResponse, 3);
ushort checksum = ToolbusFinsFrameCodec.CalculateChecksum(toolbusResponse.AsSpan(0, toolbusResponse.Length - 2));
toolbusResponse[^2] = (byte)(checksum >> 8);
toolbusResponse[^1] = (byte)checksum;
Memory<byte> toolbusReply = ToolbusFinsFrameCodec.DecodeResponse(toolbusResponse);
```

### Explicit packed-BCD conversion

`Bcd16`, `BcdU16`, `Bcd32`, and `BcdU32` are strongly typed PLC tag values. `BCDConverter` is for an explicit boundary conversion when a command or capture supplies raw BCD bytes/words. The value `0x0235` represents decimal `235`, not binary `565`; reject malformed BCD instead of treating it as a normal integer.

```csharp
using IoT.DriverCore.OmronPlcRx.Core.Converters;

short encoded = BCDConverter.GetBCDWord(235);
short decoded = BCDConverter.ToInt16(encoded);
if (decoded != 235) throw new InvalidOperationException("Unexpected packed BCD conversion.");

short[] encoded32 = BCDConverter.GetBCDWords(12_345_678);
int decoded32 = BCDConverter.ToInt32(encoded32[0], encoded32[1]);
Console.WriteLine(decoded32);
```

### Logical-tag persistence, CRUD, and combined batch command

Construct `OmronLogicalTagClient` with a SQLite connection string when the reviewed tag map must persist. Initialize the store before CRUD, and remember that `UpsertTagAsync` also registers the tag with the PLC facade. `ReadManyAsync` / `WriteManyAsync` preserve caller order and use grouped FINS operations where the facade supports them; they are not a PLC transaction.

```csharp
using IoT.DriverCore.Core;

var database = $"Data Source={Path.Combine(AppContext.BaseDirectory, "omron-tags.db")}";
using var tags = new OmronLogicalTagClient(plc, database);
await tags.InitializeStoreAsync(CancellationToken.None);

var process = new LogicalTagGroup("Process");
await tags.UpsertGroupAsync(process, CancellationToken.None);
var temperature = tags.CreateTag(new PlcTag<short>("Temperature", "D200"),
    groupName: "Process", description: "Product temperature", metadata: null,
    accessMode: LogicalTagAccessMode.ReadWrite, scanInterval: TimeSpan.FromMilliseconds(250));
await tags.UpsertTagAsync(temperature, CancellationToken.None);

using var import = new StringReader(
    "Name,Address,DataType,GroupName,Description,Metadata,AccessMode,ScanIntervalMilliseconds\n" +
    "HighTemperature,D201,Int16,Process,High limit,,ReadWrite,250\n");
IReadOnlyList<LogicalTag> imported = await tags.ImportCsvAsync(import, ',', CancellationToken.None);
using var export = new StringWriter();
await tags.ExportCsvAsync(export, ',', CancellationToken.None);
Console.WriteLine($"Imported {imported.Count} tags; CSV length = {export.GetStringBuilder().Length}.");

LogicalTag? stored = await tags.GetTagAsync("Temperature", CancellationToken.None);
if (stored is not null)
{
    var options = stored.CurrentOptions();
    options.Description = "Validated process temperature";
    await tags.EditTagAsync(stored.WithOptions(options), CancellationToken.None);
}

var results = await tags.WriteManyAsync(
    [new LogicalTagValue("Temperature", (short)185, TimeProvider.System.GetUtcNow())],
    CancellationToken.None);
if (!results.All(static result => result.Succeeded))
    throw new InvalidOperationException("Logical write failed.");

await tags.DeleteTagAsync("Temperature", CancellationToken.None);
await tags.DeleteTagAsync("HighTemperature", CancellationToken.None);
await tags.DeleteGroupAsync("Process", CancellationToken.None);
```

### Deterministic simulator workflow

`OmronPlcSimulator` implements `IOmronPlcRx` so application code can be exercised without a network endpoint. Register the same `PlcTag<T>`, seed a value, optionally queue a fault, then read/write/observe through the normal interface. `Operations` returns `OmronSimulatorOperationRecord` entries, while `OmronSimulatorOperation` describes the recorded kind. Use this for timeout/error/UI tests; it is not a protocol emulator or safety certification.

```csharp
using var simulator = new OmronPlcSimulator();
await simulator.ConnectAsync(CancellationToken.None); // useful after construction with initiallyConnected: false
var simulatedPump = new PlcTag<bool>("PumpEnabled", "D100.0");
simulator.AddUpdateTagItem(simulatedPump);
simulator.Seed(simulatedPump, false);
using var observed = simulator.Observe(new LogicalTagKey<bool>("PumpEnabled"))
    .Subscribe(Console.WriteLine);
await simulator.WriteValueAsync(new LogicalTagKey<bool>("PumpEnabled"), true, CancellationToken.None);
Console.WriteLine(simulator.Operations.Count);
simulator.Disconnect(); // retains registrations and seeded memory
```

Faults can deliberately disconnect the simulator. Handle the expected exception, call `ReconnectAsync`, and then repeat an awaited command; `ReconnectCount` and `Operations` make this recovery path assertable in application tests.

```csharp
using IoT.DriverCore.Core;
using IoT.DriverCore.OmronPlcRx.Tags;

using var simulator = new OmronPlcSimulator(initiallyConnected: true);
var speed = new PlcTag<short>("Speed", "D100");
simulator.AddUpdateTagItem(speed);
simulator.Seed(speed, (short)25);
var speedKey = new LogicalTagKey<short>("Speed");
simulator.QueueFault(OmronSimulatorOperation.Read, new TimeoutException("Simulated cable loss"));
try
{
    await simulator.ReadValueAsync(speedKey, CancellationToken.None);
}
catch (OmronPLCException)
{
    await simulator.ReconnectAsync(CancellationToken.None);
}
short? recovered = await simulator.ReadValueAsync(speedKey, CancellationToken.None);
Console.WriteLine($"value={recovered}, reconnects={simulator.ReconnectCount}");
```

### Combined workflow 1: network polling with explicit command verification

This keeps telemetry reactive, executes a command with an acknowledgement, and records failures without allowing a change stream to issue writes.

```csharp
var motor = new LogicalTagKey<bool>("PumpEnabled");
var level = new LogicalTagKey<float>("TankLevel");
using var telemetry = plc.Observe(level).Subscribe(v => Console.WriteLine($"level={v}"));
using var faults = plc.Errors.Subscribe(e => Console.Error.WriteLine(e?.Message));
using var commandCt = new CancellationTokenSource(TimeSpan.FromSeconds(3));

float? currentLevel = await plc.ReadValueAsync(level, commandCt.Token);
if (currentLevel is < 85f)
    await plc.WriteValueAsync(motor, true, commandCt.Token);
```

### Combined workflow 2: serial commissioning with clock check and BCD recipe

```csharp
var recipe = new LogicalTagKey<Bcd16>("TemperatureBcd");
using var faults = serialPlc.Errors.Subscribe(e => Console.Error.WriteLine(e?.Message));
using var commissioning = new CancellationTokenSource(TimeSpan.FromSeconds(5));

var cycle = await serialPlc.ReadCycleTimeAsync(commissioning.Token);
if (cycle.MaximumCycleTime > 1_000d) // cycle-time values are milliseconds
    throw new InvalidOperationException("Controller cycle time is outside commissioning limit.");

await serialPlc.WriteValueAsync(recipe, new Bcd16(180), commissioning.Token);
var verify = await serialPlc.ReadValueAsync(recipe, commissioning.Token);
if (verify?.Value != 180) throw new InvalidOperationException("Recipe verification failed.");
```

### Complete generator workflow

Install `OmronPlcRx.Generators` explicitly when a project needs analyzer pinning; runtime packages also carry the matching analyzer. Decorate a partial model with binding metadata and fields annotated by `PlcTagAttribute`. `TagName` selects the logical name; `Register` controls registration, `Observe` controls generated observation, and `Writable` requests write helpers. For each tag it generates the current value property, `<Property>Observable`, and—on .NET 8+—`<Property>ObservableAsync`; it also generates `Read<Property>Async`, `Write<Property>`, and `Write<Property>Async` for writable tags. Dispose the binding returned by `BindPlcTags`.

```csharp
using IoT.DriverCore.OmronPlcRx;

[PlcTagBinding]
public partial class MixerTags
{
    [PlcTag("D100.0", TagName = "MixerRun", Writable = true)]
    private bool _mixerRun;

    [PlcTag("D200", TagName = "MixerSpeed", Observe = true)]
    private short _mixerSpeed;
}

var model = new MixerTags();
model.RegisterPlcTags(plc);
using var binding = model.BindPlcTags(plc);
model.WriteMixerRun(plc, true);
var written = await model.WriteMixerRunAsync(true, CancellationToken.None);
if (!written.Succeeded) throw new InvalidOperationException(written.Error);
using var speed = model.MixerSpeedObservable.Subscribe(Console.WriteLine);
#if NET8_0_OR_GREATER
using var asyncSpeed = ObservableAsyncBridgeExtensions.ToObservable(model.MixerSpeedObservableAsync)
    .Subscribe(Console.WriteLine);
#endif
```

## Complete public API reference

The reactive package has the same inventory under `IoT.DriverCore.OmronPlcRx.Reactive`.

| Area | Public types and primary members |
| --- | --- |
| Root | `IOmronPlcRx` and `OmronPlcRx`: `ObserveAll`, `Errors`, controller metadata, `AddUpdateTagItem`, `RemoveTagItem`, `Observe`, `GetValue`, `SetValue`, `ReadValueAsync`, `WriteValueAsync`, clock/cycle methods, and `Dispose`. `OmronConnectionOptions`, `OmronSerialOptions`, `OmronPlcSimulator`, `OmronSimulatorOperation`, `OmronSimulatorOperationRecord`, `OmronHostLinkFinsFrameMode`, and `OmronSerialProtocol`. |
| Tags | `IPlcTag` and `PlcTag<T>` (`TagName`, `Address`, `Value`, `TagType`). |
| Results | `ReadBitsResult`, `ReadWordsResult`, `ReadClockResult`, `ReadCycleTimeResult`, `WriteBitsResult`, `WriteWordsResult`, and `WriteClockResult`. |
| Enums and exceptions | `ConnectionMethod`, `MemoryBitDataType`, `MemoryWordDataType`, `PlcType`, `FINSException`, and `OmronPLCException`. |
| Core | `Bcd16`, `BcdU16`, `Bcd32`, `BcdU32`, `BCDConverter`, `HostLinkFinsFrameCodec`, and `ToolbusFinsFrameCodec`. |
| Logical tags | `OmronLogicalTagClient`; use it for catalog-backed, batch logical-tag work. Its public catalog and persistence operations are documented below. |
| Async and generation | `OmronPlcRxAsyncObservableExtensions`, `PlcTagAttribute`, `PlcTagBindingAttribute`, and the `OmronPlcRx.SourceGenerators.PlcTagSourceGenerator` analyzer. |

### Member-by-member contract

| Member family | Inputs and return | Failure, lifecycle, and use |
| --- | --- | --- |
| Construction/options | Network options or serial parameters; optional polling interval. | Validate serial settings first, use one client per endpoint, and dispose it after subscriptions. |
| Tags/keys | `PlcTag<T>(tagName, address)`, `LogicalTagKey<T>(tagName)`, `AddUpdateTagItem`, `RemoveTagItem`. | The type/name must agree across all calls. Registration changes the polling set. |
| Cache/streams | `GetValue`, `Observe`, `ObserveAll`, `Errors`. | Cache is not a direct read. Dispose subscriptions; errors from poll/background writes appear on `Errors`. |
| Commands | `SetValue`, `ReadValueAsync`, `WriteValueAsync`. | `SetValue` is fire-and-forget. Await explicit methods with a cancellation token and catch FINS/Omron errors. |
| Clock/cycle | `ReadClockAsync`, both `WriteClockAsync` overloads, `ReadCycleTimeAsync`. | Direct PLC commands return result records; day is inferred or must be 0–6. |
| Options/protocol | `OmronConnectionOptions`, `OmronSerialOptions`, protocol/frame enums and codecs. | Select transport/framing to match the PLC; only FINS Host Link/Toolbus are exposed. |
| Simulation/logical/generation | `OmronPlcSimulator`, operation records, `OmronLogicalTagClient`, async extensions, BCD values/codecs, generator attributes. | Simulator and catalog make tests/configuration repeatable; they do not create PLC transaction semantics. |

### Supporting public member index

| Feature | Members and use |
| --- | --- |
| Logical catalog and persistence | `CreateTag`, `RegisterTag`, `RemoveTag`, `ReadAsync`, `ReadManyAsync`, `WriteAsync`, `WriteManyAsync`, `ObserveAsync`, `ObserveMany`, `ObserveManyAsync`, `InitializeStoreAsync`, `LoadTagsAsync`, `GetTagAsync`, `ListTagsAsync`, `EditTagAsync`, `DeleteTagAsync`, `UpsertTagAsync`, `GetGroupAsync`, `ListGroupsAsync`, `UpsertGroupAsync`, `DeleteGroupAsync`, `ImportCsvAsync`, `ExportCsvAsync`. These are cancellation-aware catalog operations; check result success/error and remember that batch planning is not a PLC transaction. |
| Simulator lifecycle and faults | `ConnectAsync`, `Disconnect`, `ReconnectAsync`, `QueueFault`, `Seed`, and operation records. A simulator fault may disconnect the simulated link; reconnect explicitly before the next command. |
| FINS serial framing | `OpenAsync`, `Close`, `DiscardInBuffer`, `EncodeRequest`, `DecodeResponse`, `CalculateChecksum`, and `CalculateFcs`. Use the serial options/connection facade for normal traffic; these codec/frame methods are for adapter/protocol tests and require exact framing. |
| BCD conversion | `GetBCDByte`, `GetBCDBytes`, `GetBCDWord`, `GetBCDWords`, `ToByte`, `ToInt16`, `ToUInt16`, `ToInt32`, and `ToUInt32`. They translate packed BCD, not normal binary; invalid BCD digits or an unsuitable width are caller errors. |
| Value semantics | `Equals`, `GetHashCode`, `ToString`, and `Index` are value/compatibility members on records/wrappers. Use typed tags/results for PLC work rather than comparing formatted text. |

## Operational guidance

Use one long-lived driver per endpoint; register all tags before relying on polling; keep subscriptions and the driver disposed with the application lifetime. Select a poll interval that the PLC, network, and number of tags can sustain. Keep command writes out of a high-frequency value stream, and monitor `Errors` plus controller metadata during commissioning. Do not expose the PLC network directly to untrusted networks.

## Troubleshooting

| Symptom | Check |
| --- | --- |
| No values arrive | Confirm FINS node IDs, transport/port, host, address syntax, tag registration, and subscription lifetime. |
| `KeyNotFoundException` or default value | Use the same tag name and generic type in registration and logical key. |
| Serial connection fails | Call `Validate()`, then verify protocol, framing, COM port, baud/parity/data/stop bits, and Host Link unit/wait values. |
| Write did not take effect | Use `WriteValueAsync` to observe the operation; inspect `Errors`; verify PLC permissions and program interlocks. |
| Generator diagnostics | Make the target type `partial`, use a supported field type/address, and remove generated-name collisions. |

## AI skill

For implementation guidance, load [`skills/omron-plc-rx/SKILL.md`](../../skills/omron-plc-rx/SKILL.md). It directs an agent to this README for the detailed source-grounded reference.

<!-- BEGIN GENERATED PUBLIC API -->

## Exhaustive public API reference

This catalogue is generated from the packaged runtime assemblies and their XML documentation. It includes exported public types and their declared public members; inherited members and non-public implementation details are intentionally omitted.

### `OmronPlcRx`

Exported public types: 35; declared public members: 352.

#### `T:IoT.DriverCore.OmronPlcRx.Async.OmronPlcRxAsyncObservableExtensions`

```csharp
public class IoT.DriverCore.OmronPlcRx.Async.OmronPlcRxAsyncObservableExtensions
```
Bridges Omron PLC classic Rx streams into ReactiveUI.Primitives.Async observables.

##### Declared public members

###### `M:IoT.DriverCore.OmronPlcRx.Async.OmronPlcRxAsyncObservableExtensions.ErrorsAsAsyncObservable(IoT.DriverCore.OmronPlcRx.IOmronPlcRx)`

```csharp
public static ReactiveUI.Primitives.Async.IObservableAsync<IoT.DriverCore.OmronPlcRx.OmronPLCException> ErrorsAsAsyncObservable(IoT.DriverCore.OmronPlcRx.IOmronPlcRx plc)
```
Observes PLC operational errors as an async observable.

- Parameter `plc`: The PLC reactive facade.
- Returns: An async observable of PLC errors.

###### `M:IoT.DriverCore.OmronPlcRx.Async.OmronPlcRxAsyncObservableExtensions.ObserveAllAsAsyncObservable(IoT.DriverCore.OmronPlcRx.IOmronPlcRx)`

```csharp
public static ReactiveUI.Primitives.Async.IObservableAsync<IoT.DriverCore.OmronPlcRx.Tags.IPlcTag> ObserveAllAsAsyncObservable(IoT.DriverCore.OmronPlcRx.IOmronPlcRx plc)
```
Observes every changed PLC tag as an async observable.

- Parameter `plc`: The PLC reactive facade.
- Returns: An async observable of all changed tags.

###### `M:IoT.DriverCore.OmronPlcRx.Async.OmronPlcRxAsyncObservableExtensions.ObserveAsAsyncObservable``1(IoT.DriverCore.OmronPlcRx.IOmronPlcRx,IoT.DriverCore.Core.LogicalTagKey`1{``0})`

```csharp
public static ReactiveUI.Primitives.Async.IObservableAsync<T> ObserveAsAsyncObservable<T>(IoT.DriverCore.OmronPlcRx.IOmronPlcRx plc, IoT.DriverCore.Core.LogicalTagKey<T> tag)
```
Executes the `ObserveAsAsyncObservable` operation.

- Parameter `plc`: The `plc` value.
- Parameter `tag`: The `tag` value.
- Returns: A `ReactiveUI.Primitives.Async.IObservableAsync<T>` result.

###### `M:IoT.DriverCore.OmronPlcRx.Async.OmronPlcRxAsyncObservableExtensions.ObserveValuesAsync``1(IoT.DriverCore.OmronPlcRx.IOmronPlcRx,IoT.DriverCore.Core.LogicalTagKey`1{``0},System.Threading.CancellationToken)`

```csharp
public static System.Collections.Generic.IAsyncEnumerable<T> ObserveValuesAsync<T>(IoT.DriverCore.OmronPlcRx.IOmronPlcRx plc, IoT.DriverCore.Core.LogicalTagKey<T> tag, System.Threading.CancellationToken cancellationToken)
```
Executes the `ObserveValuesAsync` operation.

- Parameter `plc`: The `plc` value.
- Parameter `tag`: The `tag` value.
- Parameter `cancellationToken`: The `cancellationToken` value.
- Returns: A `System.Collections.Generic.IAsyncEnumerable<T>` result.

#### `T:IoT.DriverCore.OmronPlcRx.Core.Converters.BCDConverter`

```csharp
public class IoT.DriverCore.OmronPlcRx.Core.Converters.BCDConverter
```
Converts between BCD encoded values and numeric values.

##### Declared public members

###### `M:IoT.DriverCore.OmronPlcRx.Core.Converters.BCDConverter.GetBCDByte(System.Byte)`

```csharp
public static byte GetBCDByte(byte binaryValue)
```
Gets the BCD byte.

- Parameter `binaryValue`: The binary value.
- Returns: A BCD-encoded byte.

###### `M:IoT.DriverCore.OmronPlcRx.Core.Converters.BCDConverter.GetBCDBytes(System.Int16)`

```csharp
public static byte[] GetBCDBytes(short binaryValue)
```
Gets the BCD bytes.

- Parameter `binaryValue`: The binary value.
- Returns: A BCD-encoded byte array.

###### `M:IoT.DriverCore.OmronPlcRx.Core.Converters.BCDConverter.GetBCDBytes(System.Int32)`

```csharp
public static byte[] GetBCDBytes(int binaryValue)
```
Gets the BCD bytes.

- Parameter `binaryValue`: The binary value.
- Returns: A BCD-encoded byte array.

###### `M:IoT.DriverCore.OmronPlcRx.Core.Converters.BCDConverter.GetBCDBytes(System.UInt16)`

```csharp
public static byte[] GetBCDBytes(ushort binaryValue)
```
Gets the BCD bytes.

- Parameter `binaryValue`: The binary value.
- Returns: A BCD-encoded byte array.

###### `M:IoT.DriverCore.OmronPlcRx.Core.Converters.BCDConverter.GetBCDBytes(System.UInt32)`

```csharp
public static byte[] GetBCDBytes(uint binaryValue)
```
Gets the BCD bytes.

- Parameter `binaryValue`: The binary value.
- Returns: A BCD-encoded byte array.

###### `M:IoT.DriverCore.OmronPlcRx.Core.Converters.BCDConverter.GetBCDWord(System.Int16)`

```csharp
public static short GetBCDWord(short binaryValue)
```
Gets the BCD word.

- Parameter `binaryValue`: The binary value.
- Returns: A BCD-encoded word.

###### `M:IoT.DriverCore.OmronPlcRx.Core.Converters.BCDConverter.GetBCDWord(System.UInt16)`

```csharp
public static short GetBCDWord(ushort binaryValue)
```
Gets the BCD word.

- Parameter `binaryValue`: The binary value.
- Returns: A BCD-encoded word.

###### `M:IoT.DriverCore.OmronPlcRx.Core.Converters.BCDConverter.GetBCDWords(System.Int32)`

```csharp
public static short[] GetBCDWords(int binaryValue)
```
Gets the BCD words.

- Parameter `binaryValue`: The binary value.
- Returns: An array of two BCD-encoded words.

###### `M:IoT.DriverCore.OmronPlcRx.Core.Converters.BCDConverter.GetBCDWords(System.UInt32)`

```csharp
public static short[] GetBCDWords(uint binaryValue)
```
Gets the BCD words.

- Parameter `binaryValue`: The binary value.
- Returns: An array of two BCD-encoded words.

###### `M:IoT.DriverCore.OmronPlcRx.Core.Converters.BCDConverter.ToByte(System.Byte)`

```csharp
public static byte ToByte(byte bcdByte)
```
Converts to byte.

- Parameter `bcdByte`: The BCD byte.
- Returns: A byte representing the converted value.

###### `M:IoT.DriverCore.OmronPlcRx.Core.Converters.BCDConverter.ToInt16(System.Byte[])`

```csharp
public static short ToInt16(byte[] bcdBytes)
```
Converts to int16.

- Parameter `bcdBytes`: The BCD bytes.
- Returns: A short.

###### `M:IoT.DriverCore.OmronPlcRx.Core.Converters.BCDConverter.ToInt16(System.Int16)`

```csharp
public static short ToInt16(short bcdWord)
```
Converts to int16.

- Parameter `bcdWord`: The BCD word.
- Returns: A short.

###### `M:IoT.DriverCore.OmronPlcRx.Core.Converters.BCDConverter.ToInt32(System.Byte[])`

```csharp
public static int ToInt32(byte[] bcdBytes)
```
Converts to int32.

- Parameter `bcdBytes`: The BCD bytes.
- Returns: An int.

###### `M:IoT.DriverCore.OmronPlcRx.Core.Converters.BCDConverter.ToInt32(System.Int16,System.Int16)`

```csharp
public static int ToInt32(short bcdWord1, short bcdWord2)
```
Converts to int32.

- Parameter `bcdWord1`: The BCD word1.
- Parameter `bcdWord2`: The BCD word2.
- Returns: An int.

###### `M:IoT.DriverCore.OmronPlcRx.Core.Converters.BCDConverter.ToUInt16(System.Byte[])`

```csharp
public static ushort ToUInt16(byte[] bcdBytes)
```
Converts to uint16.

- Parameter `bcdBytes`: The BCD bytes.
- Returns: A ushort.

###### `M:IoT.DriverCore.OmronPlcRx.Core.Converters.BCDConverter.ToUInt16(System.Int16)`

```csharp
public static ushort ToUInt16(short bcdWord)
```
Converts to uint16.

- Parameter `bcdWord`: The BCD word.
- Returns: A ushort.

###### `M:IoT.DriverCore.OmronPlcRx.Core.Converters.BCDConverter.ToUInt32(System.Byte[])`

```csharp
public static uint ToUInt32(byte[] bcdBytes)
```
Converts to uint32.

- Parameter `bcdBytes`: The BCD bytes.
- Returns: A uint.

###### `M:IoT.DriverCore.OmronPlcRx.Core.Converters.BCDConverter.ToUInt32(System.Int16,System.Int16)`

```csharp
public static uint ToUInt32(short bcdWord1, short bcdWord2)
```
Converts to uint32.

- Parameter `bcdWord1`: The BCD word1.
- Parameter `bcdWord2`: The BCD word2.
- Returns: A uint.

#### `T:IoT.DriverCore.OmronPlcRx.Core.Types.Bcd16`

```csharp
public struct IoT.DriverCore.OmronPlcRx.Core.Types.Bcd16
```
Signed 16-bit BCD numeric wrapper.

##### Declared public members

###### `M:IoT.DriverCore.OmronPlcRx.Core.Types.Bcd16.#ctor(System.Int16)`

```csharp
public IoT.DriverCore.OmronPlcRx.Core.Types.Bcd16(short value)
```
Initializes a new instance of the `T:IoT.DriverCore.OmronPlcRx.Core.Types.Bcd16` struct.

- Parameter `value`: The signed value.

###### `M:IoT.DriverCore.OmronPlcRx.Core.Types.Bcd16.Equals(IoT.DriverCore.OmronPlcRx.Core.Types.Bcd16)`

```csharp
public bool Equals(IoT.DriverCore.OmronPlcRx.Core.Types.Bcd16 other)
```
Determines whether the supplied value is equal to the current value.

- Parameter `other`: The `other` value.
- Returns: A `bool` result.

###### `M:IoT.DriverCore.OmronPlcRx.Core.Types.Bcd16.Equals(System.Object)`

```csharp
public bool Equals(object obj)
```
Determines whether the supplied value is equal to the current value.

- Parameter `obj`: The `obj` value.
- Returns: A `bool` result.

###### `M:IoT.DriverCore.OmronPlcRx.Core.Types.Bcd16.GetHashCode`

```csharp
public int GetHashCode()
```
Inherits XML documentation from its implemented or overridden member.

- Returns: A `int` result.

###### `M:IoT.DriverCore.OmronPlcRx.Core.Types.Bcd16.ToString`

```csharp
public string ToString()
```
Inherits XML documentation from its implemented or overridden member.

- Returns: A `string` result.

###### `M:IoT.DriverCore.OmronPlcRx.Core.Types.Bcd16.op_Equality(IoT.DriverCore.OmronPlcRx.Core.Types.Bcd16,IoT.DriverCore.OmronPlcRx.Core.Types.Bcd16)`

```csharp
public static bool op_Equality(IoT.DriverCore.OmronPlcRx.Core.Types.Bcd16 left, IoT.DriverCore.OmronPlcRx.Core.Types.Bcd16 right)
```
Determines whether the two supplied values are equal.

- Parameter `left`: The `left` value.
- Parameter `right`: The `right` value.
- Returns: A `bool` result.

###### `M:IoT.DriverCore.OmronPlcRx.Core.Types.Bcd16.op_Inequality(IoT.DriverCore.OmronPlcRx.Core.Types.Bcd16,IoT.DriverCore.OmronPlcRx.Core.Types.Bcd16)`

```csharp
public static bool op_Inequality(IoT.DriverCore.OmronPlcRx.Core.Types.Bcd16 left, IoT.DriverCore.OmronPlcRx.Core.Types.Bcd16 right)
```
Determines whether the two supplied values are not equal.

- Parameter `left`: The `left` value.
- Parameter `right`: The `right` value.
- Returns: A `bool` result.

###### `P:IoT.DriverCore.OmronPlcRx.Core.Types.Bcd16.Value`

```csharp
public short Value { get; }
```
Gets the numeric value.

- Value: The `Value` value.

#### `T:IoT.DriverCore.OmronPlcRx.Core.Types.Bcd32`

```csharp
public struct IoT.DriverCore.OmronPlcRx.Core.Types.Bcd32
```
Signed 32-bit BCD numeric wrapper.

##### Declared public members

###### `M:IoT.DriverCore.OmronPlcRx.Core.Types.Bcd32.#ctor(System.Int32)`

```csharp
public IoT.DriverCore.OmronPlcRx.Core.Types.Bcd32(int value)
```
Initializes a new instance of the `T:IoT.DriverCore.OmronPlcRx.Core.Types.Bcd32` struct.

- Parameter `value`: The signed value.

###### `M:IoT.DriverCore.OmronPlcRx.Core.Types.Bcd32.Equals(IoT.DriverCore.OmronPlcRx.Core.Types.Bcd32)`

```csharp
public bool Equals(IoT.DriverCore.OmronPlcRx.Core.Types.Bcd32 other)
```
Determines whether the supplied value is equal to the current value.

- Parameter `other`: The `other` value.
- Returns: A `bool` result.

###### `M:IoT.DriverCore.OmronPlcRx.Core.Types.Bcd32.Equals(System.Object)`

```csharp
public bool Equals(object obj)
```
Determines whether the supplied value is equal to the current value.

- Parameter `obj`: The `obj` value.
- Returns: A `bool` result.

###### `M:IoT.DriverCore.OmronPlcRx.Core.Types.Bcd32.GetHashCode`

```csharp
public int GetHashCode()
```
Inherits XML documentation from its implemented or overridden member.

- Returns: A `int` result.

###### `M:IoT.DriverCore.OmronPlcRx.Core.Types.Bcd32.ToString`

```csharp
public string ToString()
```
Inherits XML documentation from its implemented or overridden member.

- Returns: A `string` result.

###### `M:IoT.DriverCore.OmronPlcRx.Core.Types.Bcd32.op_Equality(IoT.DriverCore.OmronPlcRx.Core.Types.Bcd32,IoT.DriverCore.OmronPlcRx.Core.Types.Bcd32)`

```csharp
public static bool op_Equality(IoT.DriverCore.OmronPlcRx.Core.Types.Bcd32 left, IoT.DriverCore.OmronPlcRx.Core.Types.Bcd32 right)
```
Determines whether the two supplied values are equal.

- Parameter `left`: The `left` value.
- Parameter `right`: The `right` value.
- Returns: A `bool` result.

###### `M:IoT.DriverCore.OmronPlcRx.Core.Types.Bcd32.op_Inequality(IoT.DriverCore.OmronPlcRx.Core.Types.Bcd32,IoT.DriverCore.OmronPlcRx.Core.Types.Bcd32)`

```csharp
public static bool op_Inequality(IoT.DriverCore.OmronPlcRx.Core.Types.Bcd32 left, IoT.DriverCore.OmronPlcRx.Core.Types.Bcd32 right)
```
Determines whether the two supplied values are not equal.

- Parameter `left`: The `left` value.
- Parameter `right`: The `right` value.
- Returns: A `bool` result.

###### `P:IoT.DriverCore.OmronPlcRx.Core.Types.Bcd32.Value`

```csharp
public int Value { get; }
```
Gets the numeric value.

- Value: The `Value` value.

#### `T:IoT.DriverCore.OmronPlcRx.Core.Types.BcdU16`

```csharp
public struct IoT.DriverCore.OmronPlcRx.Core.Types.BcdU16
```
Unsigned 16-bit BCD numeric wrapper.

##### Declared public members

###### `M:IoT.DriverCore.OmronPlcRx.Core.Types.BcdU16.#ctor(System.UInt16)`

```csharp
public IoT.DriverCore.OmronPlcRx.Core.Types.BcdU16(ushort value)
```
Initializes a new instance of the `T:IoT.DriverCore.OmronPlcRx.Core.Types.BcdU16` struct.

- Parameter `value`: The unsigned value.

###### `M:IoT.DriverCore.OmronPlcRx.Core.Types.BcdU16.Equals(IoT.DriverCore.OmronPlcRx.Core.Types.BcdU16)`

```csharp
public bool Equals(IoT.DriverCore.OmronPlcRx.Core.Types.BcdU16 other)
```
Determines whether the supplied value is equal to the current value.

- Parameter `other`: The `other` value.
- Returns: A `bool` result.

###### `M:IoT.DriverCore.OmronPlcRx.Core.Types.BcdU16.Equals(System.Object)`

```csharp
public bool Equals(object obj)
```
Determines whether the supplied value is equal to the current value.

- Parameter `obj`: The `obj` value.
- Returns: A `bool` result.

###### `M:IoT.DriverCore.OmronPlcRx.Core.Types.BcdU16.GetHashCode`

```csharp
public int GetHashCode()
```
Inherits XML documentation from its implemented or overridden member.

- Returns: A `int` result.

###### `M:IoT.DriverCore.OmronPlcRx.Core.Types.BcdU16.ToString`

```csharp
public string ToString()
```
Inherits XML documentation from its implemented or overridden member.

- Returns: A `string` result.

###### `M:IoT.DriverCore.OmronPlcRx.Core.Types.BcdU16.op_Equality(IoT.DriverCore.OmronPlcRx.Core.Types.BcdU16,IoT.DriverCore.OmronPlcRx.Core.Types.BcdU16)`

```csharp
public static bool op_Equality(IoT.DriverCore.OmronPlcRx.Core.Types.BcdU16 left, IoT.DriverCore.OmronPlcRx.Core.Types.BcdU16 right)
```
Determines whether the two supplied values are equal.

- Parameter `left`: The `left` value.
- Parameter `right`: The `right` value.
- Returns: A `bool` result.

###### `M:IoT.DriverCore.OmronPlcRx.Core.Types.BcdU16.op_Inequality(IoT.DriverCore.OmronPlcRx.Core.Types.BcdU16,IoT.DriverCore.OmronPlcRx.Core.Types.BcdU16)`

```csharp
public static bool op_Inequality(IoT.DriverCore.OmronPlcRx.Core.Types.BcdU16 left, IoT.DriverCore.OmronPlcRx.Core.Types.BcdU16 right)
```
Determines whether the two supplied values are not equal.

- Parameter `left`: The `left` value.
- Parameter `right`: The `right` value.
- Returns: A `bool` result.

###### `P:IoT.DriverCore.OmronPlcRx.Core.Types.BcdU16.Value`

```csharp
public ushort Value { get; }
```
Gets the numeric value.

- Value: The `Value` value.

#### `T:IoT.DriverCore.OmronPlcRx.Core.Types.BcdU32`

```csharp
public struct IoT.DriverCore.OmronPlcRx.Core.Types.BcdU32
```
Unsigned 32-bit BCD numeric wrapper.

##### Declared public members

###### `M:IoT.DriverCore.OmronPlcRx.Core.Types.BcdU32.#ctor(System.UInt32)`

```csharp
public IoT.DriverCore.OmronPlcRx.Core.Types.BcdU32(uint value)
```
Initializes a new instance of the `T:IoT.DriverCore.OmronPlcRx.Core.Types.BcdU32` struct.

- Parameter `value`: The unsigned value.

###### `M:IoT.DriverCore.OmronPlcRx.Core.Types.BcdU32.Equals(IoT.DriverCore.OmronPlcRx.Core.Types.BcdU32)`

```csharp
public bool Equals(IoT.DriverCore.OmronPlcRx.Core.Types.BcdU32 other)
```
Determines whether the supplied value is equal to the current value.

- Parameter `other`: The `other` value.
- Returns: A `bool` result.

###### `M:IoT.DriverCore.OmronPlcRx.Core.Types.BcdU32.Equals(System.Object)`

```csharp
public bool Equals(object obj)
```
Determines whether the supplied value is equal to the current value.

- Parameter `obj`: The `obj` value.
- Returns: A `bool` result.

###### `M:IoT.DriverCore.OmronPlcRx.Core.Types.BcdU32.GetHashCode`

```csharp
public int GetHashCode()
```
Inherits XML documentation from its implemented or overridden member.

- Returns: A `int` result.

###### `M:IoT.DriverCore.OmronPlcRx.Core.Types.BcdU32.ToString`

```csharp
public string ToString()
```
Inherits XML documentation from its implemented or overridden member.

- Returns: A `string` result.

###### `M:IoT.DriverCore.OmronPlcRx.Core.Types.BcdU32.op_Equality(IoT.DriverCore.OmronPlcRx.Core.Types.BcdU32,IoT.DriverCore.OmronPlcRx.Core.Types.BcdU32)`

```csharp
public static bool op_Equality(IoT.DriverCore.OmronPlcRx.Core.Types.BcdU32 left, IoT.DriverCore.OmronPlcRx.Core.Types.BcdU32 right)
```
Determines whether the two supplied values are equal.

- Parameter `left`: The `left` value.
- Parameter `right`: The `right` value.
- Returns: A `bool` result.

###### `M:IoT.DriverCore.OmronPlcRx.Core.Types.BcdU32.op_Inequality(IoT.DriverCore.OmronPlcRx.Core.Types.BcdU32,IoT.DriverCore.OmronPlcRx.Core.Types.BcdU32)`

```csharp
public static bool op_Inequality(IoT.DriverCore.OmronPlcRx.Core.Types.BcdU32 left, IoT.DriverCore.OmronPlcRx.Core.Types.BcdU32 right)
```
Determines whether the two supplied values are not equal.

- Parameter `left`: The `left` value.
- Parameter `right`: The `right` value.
- Returns: A `bool` result.

###### `P:IoT.DriverCore.OmronPlcRx.Core.Types.BcdU32.Value`

```csharp
public uint Value { get; }
```
Gets the numeric value.

- Value: The `Value` value.

#### `T:IoT.DriverCore.OmronPlcRx.Enums.ConnectionMethod`

```csharp
public enum IoT.DriverCore.OmronPlcRx.Enums.ConnectionMethod
```
Transport protocol used for communication with the PLC.

##### Declared public members

###### `F:IoT.DriverCore.OmronPlcRx.Enums.ConnectionMethod.Serial`

```csharp
public static const IoT.DriverCore.OmronPlcRx.Enums.ConnectionMethod Serial
```
Serial FINS protocol using Host Link FINS or Toolbus framing.

###### `F:IoT.DriverCore.OmronPlcRx.Enums.ConnectionMethod.TCP`

```csharp
public static const IoT.DriverCore.OmronPlcRx.Enums.ConnectionMethod TCP
```
Transmission Control Protocol.

###### `F:IoT.DriverCore.OmronPlcRx.Enums.ConnectionMethod.UDP`

```csharp
public static const IoT.DriverCore.OmronPlcRx.Enums.ConnectionMethod UDP
```
User Datagram Protocol.

#### `T:IoT.DriverCore.OmronPlcRx.Enums.MemoryBitDataType`

```csharp
public enum IoT.DriverCore.OmronPlcRx.Enums.MemoryBitDataType
```
Bit-addressable PLC memory areas.

##### Declared public members

###### `F:IoT.DriverCore.OmronPlcRx.Enums.MemoryBitDataType.Auxiliary`

```csharp
public static const IoT.DriverCore.OmronPlcRx.Enums.MemoryBitDataType Auxiliary
```
Auxiliary area (A).

###### `F:IoT.DriverCore.OmronPlcRx.Enums.MemoryBitDataType.CommonIO`

```csharp
public static const IoT.DriverCore.OmronPlcRx.Enums.MemoryBitDataType CommonIO
```
Common I/O area (CIO).

###### `F:IoT.DriverCore.OmronPlcRx.Enums.MemoryBitDataType.DataMemory`

```csharp
public static const IoT.DriverCore.OmronPlcRx.Enums.MemoryBitDataType DataMemory
```
Data memory area (DM).

###### `F:IoT.DriverCore.OmronPlcRx.Enums.MemoryBitDataType.Holding`

```csharp
public static const IoT.DriverCore.OmronPlcRx.Enums.MemoryBitDataType Holding
```
Holding area (H).

###### `F:IoT.DriverCore.OmronPlcRx.Enums.MemoryBitDataType.None`

```csharp
public static const IoT.DriverCore.OmronPlcRx.Enums.MemoryBitDataType None
```
No bit-addressable memory area.

###### `F:IoT.DriverCore.OmronPlcRx.Enums.MemoryBitDataType.Work`

```csharp
public static const IoT.DriverCore.OmronPlcRx.Enums.MemoryBitDataType Work
```
Work area (W).

#### `T:IoT.DriverCore.OmronPlcRx.Enums.MemoryWordDataType`

```csharp
public enum IoT.DriverCore.OmronPlcRx.Enums.MemoryWordDataType
```
Word-addressable PLC memory areas.

##### Declared public members

###### `F:IoT.DriverCore.OmronPlcRx.Enums.MemoryWordDataType.Auxiliary`

```csharp
public static const IoT.DriverCore.OmronPlcRx.Enums.MemoryWordDataType Auxiliary
```
Auxiliary area (A).

###### `F:IoT.DriverCore.OmronPlcRx.Enums.MemoryWordDataType.CommonIO`

```csharp
public static const IoT.DriverCore.OmronPlcRx.Enums.MemoryWordDataType CommonIO
```
Common I/O area (CIO).

###### `F:IoT.DriverCore.OmronPlcRx.Enums.MemoryWordDataType.DataMemory`

```csharp
public static const IoT.DriverCore.OmronPlcRx.Enums.MemoryWordDataType DataMemory
```
Data memory area (DM).

###### `F:IoT.DriverCore.OmronPlcRx.Enums.MemoryWordDataType.Holding`

```csharp
public static const IoT.DriverCore.OmronPlcRx.Enums.MemoryWordDataType Holding
```
Holding area (H).

###### `F:IoT.DriverCore.OmronPlcRx.Enums.MemoryWordDataType.None`

```csharp
public static const IoT.DriverCore.OmronPlcRx.Enums.MemoryWordDataType None
```
No word-addressable memory area.

###### `F:IoT.DriverCore.OmronPlcRx.Enums.MemoryWordDataType.Work`

```csharp
public static const IoT.DriverCore.OmronPlcRx.Enums.MemoryWordDataType Work
```
Work area (W).

#### `T:IoT.DriverCore.OmronPlcRx.Enums.PlcType`

```csharp
public enum IoT.DriverCore.OmronPlcRx.Enums.PlcType
```
Supported Omron PLC types used to adjust message capabilities and limits.

##### Declared public members

###### `F:IoT.DriverCore.OmronPlcRx.Enums.PlcType.CJ2`

```csharp
public static const IoT.DriverCore.OmronPlcRx.Enums.PlcType CJ2
```
Omron CJ2 series.

###### `F:IoT.DriverCore.OmronPlcRx.Enums.PlcType.CP1`

```csharp
public static const IoT.DriverCore.OmronPlcRx.Enums.PlcType CP1
```
Omron CP1 series.

###### `F:IoT.DriverCore.OmronPlcRx.Enums.PlcType.C_Series`

```csharp
public static const IoT.DriverCore.OmronPlcRx.Enums.PlcType C_Series
```
Omron C-series (legacy).

###### `F:IoT.DriverCore.OmronPlcRx.Enums.PlcType.NJ101`

```csharp
public static const IoT.DriverCore.OmronPlcRx.Enums.PlcType NJ101
```
Omron NJ101 series.

###### `F:IoT.DriverCore.OmronPlcRx.Enums.PlcType.NJ301`

```csharp
public static const IoT.DriverCore.OmronPlcRx.Enums.PlcType NJ301
```
Omron NJ301 series.

###### `F:IoT.DriverCore.OmronPlcRx.Enums.PlcType.NJ501`

```csharp
public static const IoT.DriverCore.OmronPlcRx.Enums.PlcType NJ501
```
Omron NJ501 series.

###### `F:IoT.DriverCore.OmronPlcRx.Enums.PlcType.NJ_NX_NY_Series`

```csharp
public static const IoT.DriverCore.OmronPlcRx.Enums.PlcType NJ_NX_NY_Series
```
Generic NJ/NX/NY series.

###### `F:IoT.DriverCore.OmronPlcRx.Enums.PlcType.NX102`

```csharp
public static const IoT.DriverCore.OmronPlcRx.Enums.PlcType NX102
```
Omron NX102 series.

###### `F:IoT.DriverCore.OmronPlcRx.Enums.PlcType.NX1P2`

```csharp
public static const IoT.DriverCore.OmronPlcRx.Enums.PlcType NX1P2
```
Omron NX1P2 series.

###### `F:IoT.DriverCore.OmronPlcRx.Enums.PlcType.NX701`

```csharp
public static const IoT.DriverCore.OmronPlcRx.Enums.PlcType NX701
```
Omron NX701 series.

###### `F:IoT.DriverCore.OmronPlcRx.Enums.PlcType.NY512`

```csharp
public static const IoT.DriverCore.OmronPlcRx.Enums.PlcType NY512
```
Omron NY512 series.

###### `F:IoT.DriverCore.OmronPlcRx.Enums.PlcType.NY532`

```csharp
public static const IoT.DriverCore.OmronPlcRx.Enums.PlcType NY532
```
Omron NY532 series.

###### `F:IoT.DriverCore.OmronPlcRx.Enums.PlcType.Unknown`

```csharp
public static const IoT.DriverCore.OmronPlcRx.Enums.PlcType Unknown
```
Unknown or not yet identified.

#### `T:IoT.DriverCore.OmronPlcRx.FINSException`

```csharp
public class IoT.DriverCore.OmronPlcRx.FINSException
```
An exception that represents a FINS protocol error or invalid response.

##### Declared public members

###### `M:IoT.DriverCore.OmronPlcRx.FINSException.#ctor`

```csharp
public IoT.DriverCore.OmronPlcRx.FINSException()
```
Initializes a new instance of the `T:IoT.DriverCore.OmronPlcRx.FINSException` class.

###### `M:IoT.DriverCore.OmronPlcRx.FINSException.#ctor(System.String)`

```csharp
public IoT.DriverCore.OmronPlcRx.FINSException(string message)
```
Initializes a new instance of the `T:IoT.DriverCore.OmronPlcRx.FINSException` class with a message.

- Parameter `message`: The message that describes the error.

###### `M:IoT.DriverCore.OmronPlcRx.FINSException.#ctor(System.String,System.Exception)`

```csharp
public IoT.DriverCore.OmronPlcRx.FINSException(string message, System.Exception innerException)
```
Initializes a new instance of the `T:IoT.DriverCore.OmronPlcRx.FINSException` class.

- Parameter `message`: The error message that explains the reason for the exception.
- Parameter `innerException`: The exception that caused the current exception.

#### `T:IoT.DriverCore.OmronPlcRx.HostLinkFinsFrameCodec`

```csharp
public class IoT.DriverCore.OmronPlcRx.HostLinkFinsFrameCodec
```
Encodes and decodes Omron FINS frames carried in Host Link serial frames.

##### Declared public members

###### `M:IoT.DriverCore.OmronPlcRx.HostLinkFinsFrameCodec.#ctor(IoT.DriverCore.OmronPlcRx.OmronSerialOptions)`

```csharp
public IoT.DriverCore.OmronPlcRx.HostLinkFinsFrameCodec(IoT.DriverCore.OmronPlcRx.OmronSerialOptions options)
```
Initializes a new instance of the `T:IoT.DriverCore.OmronPlcRx.HostLinkFinsFrameCodec` class.

- Parameter `options`: Serial Host Link options.

###### `M:IoT.DriverCore.OmronPlcRx.HostLinkFinsFrameCodec.CalculateFcs(System.String)`

```csharp
public static string CalculateFcs(string frameText)
```
Calculates the Host Link frame-check sequence.

- Parameter `frameText`: Frame text from @ through the final text character, excluding FCS and terminator.
- Returns: Two-character uppercase hexadecimal FCS.

###### `M:IoT.DriverCore.OmronPlcRx.HostLinkFinsFrameCodec.DecodeResponse(System.String)`

```csharp
public System.Memory<byte> DecodeResponse(string frame)
```
Decodes an ASCII Host Link FINS response into a binary FINS response message.

- Parameter `frame`: ASCII Host Link FINS response frame including FCS and terminator.
- Returns: Binary FINS response message.

###### `M:IoT.DriverCore.OmronPlcRx.HostLinkFinsFrameCodec.EncodeRequest(System.ReadOnlyMemory`1{System.Byte})`

```csharp
public string EncodeRequest(System.ReadOnlyMemory<byte> finsMessage)
```
Executes the `EncodeRequest` operation.

- Parameter `finsMessage`: The `finsMessage` value.
- Returns: A `string` result.

#### `T:IoT.DriverCore.OmronPlcRx.IOmronPlcRx`

```csharp
public interface IoT.DriverCore.OmronPlcRx.IOmronPlcRx
```
Defines high-level Omron PLC operations and tag access.

##### Declared public members

###### `M:IoT.DriverCore.OmronPlcRx.IOmronPlcRx.AddUpdateTagItem``1(IoT.DriverCore.OmronPlcRx.Tags.PlcTag`1{``0})`

```csharp
public void AddUpdateTagItem<T>(IoT.DriverCore.OmronPlcRx.Tags.PlcTag<T> tag)
```
Executes the `AddUpdateTagItem` operation.

- Parameter `tag`: The `tag` value.

###### `M:IoT.DriverCore.OmronPlcRx.IOmronPlcRx.GetValue``1(IoT.DriverCore.Core.LogicalTagKey`1{``0})`

```csharp
public T GetValue<T>(IoT.DriverCore.Core.LogicalTagKey<T> tag)
```
Executes the `GetValue` operation.

- Parameter `tag`: The `tag` value.
- Returns: A `T` result.

###### `M:IoT.DriverCore.OmronPlcRx.IOmronPlcRx.Observe``1(IoT.DriverCore.Core.LogicalTagKey`1{``0})`

```csharp
public System.IObservable<T> Observe<T>(IoT.DriverCore.Core.LogicalTagKey<T> tag)
```
Executes the `Observe` operation.

- Parameter `tag`: The `tag` value.
- Returns: A `System.IObservable<T>` result.

###### `M:IoT.DriverCore.OmronPlcRx.IOmronPlcRx.ReadClockAsync(System.Threading.CancellationToken)`

```csharp
public System.Threading.Tasks.Task<IoT.DriverCore.OmronPlcRx.Results.ReadClockResult> ReadClockAsync(System.Threading.CancellationToken cancellationToken)
```
Reads the PLC real-time clock.

- Parameter `cancellationToken`: Cancellation token.
- Returns: Clock read result.

###### `M:IoT.DriverCore.OmronPlcRx.IOmronPlcRx.ReadCycleTimeAsync(System.Threading.CancellationToken)`

```csharp
public System.Threading.Tasks.Task<IoT.DriverCore.OmronPlcRx.Results.ReadCycleTimeResult> ReadCycleTimeAsync(System.Threading.CancellationToken cancellationToken)
```
Reads PLC scan cycle time statistics.

- Parameter `cancellationToken`: Cancellation token.
- Returns: Cycle time statistics.

###### `M:IoT.DriverCore.OmronPlcRx.IOmronPlcRx.ReadValueAsync``1(IoT.DriverCore.Core.LogicalTagKey`1{``0},System.Threading.CancellationToken)`

```csharp
public System.Threading.Tasks.Task<T> ReadValueAsync<T>(IoT.DriverCore.Core.LogicalTagKey<T> tag, System.Threading.CancellationToken cancellationToken)
```
Executes the `ReadValueAsync` operation.

- Parameter `tag`: The `tag` value.
- Parameter `cancellationToken`: The `cancellationToken` value.
- Returns: A `System.Threading.Tasks.Task<T>` result.

###### `M:IoT.DriverCore.OmronPlcRx.IOmronPlcRx.RemoveTagItem(System.String)`

```csharp
public bool RemoveTagItem(string tagName)
```
Removes a registered tag definition.

- Parameter `tagName`: Logical tag name.
- Returns: when a tag was removed.

###### `M:IoT.DriverCore.OmronPlcRx.IOmronPlcRx.SetValue``1(IoT.DriverCore.Core.LogicalTagKey`1{``0},``0)`

```csharp
public void SetValue<T>(IoT.DriverCore.Core.LogicalTagKey<T> tag, T value)
```
Executes the `SetValue` operation.

- Parameter `tag`: The `tag` value.
- Parameter `value`: The `value` value.

###### `M:IoT.DriverCore.OmronPlcRx.IOmronPlcRx.WriteClockAsync(System.DateTimeOffset,System.Int32,System.Threading.CancellationToken)`

```csharp
public System.Threading.Tasks.Task<IoT.DriverCore.OmronPlcRx.Results.WriteClockResult> WriteClockAsync(System.DateTimeOffset newDateTime, int newDayOfWeek, System.Threading.CancellationToken cancellationToken)
```
Writes the PLC real-time clock with explicit day-of-week.

- Parameter `newDateTime`: New date/time.
- Parameter `newDayOfWeek`: Day of week (0-6).
- Parameter `cancellationToken`: Cancellation token.
- Returns: Clock write result.

###### `M:IoT.DriverCore.OmronPlcRx.IOmronPlcRx.WriteClockAsync(System.DateTimeOffset,System.Threading.CancellationToken)`

```csharp
public System.Threading.Tasks.Task<IoT.DriverCore.OmronPlcRx.Results.WriteClockResult> WriteClockAsync(System.DateTimeOffset newDateTime, System.Threading.CancellationToken cancellationToken)
```
Writes the PLC real-time clock (day-of-week inferred from date).

- Parameter `newDateTime`: New date/time.
- Parameter `cancellationToken`: Cancellation token.
- Returns: Clock write result.

###### `M:IoT.DriverCore.OmronPlcRx.IOmronPlcRx.WriteValueAsync``1(IoT.DriverCore.Core.LogicalTagKey`1{``0},``0,System.Threading.CancellationToken)`

```csharp
public System.Threading.Tasks.Task WriteValueAsync<T>(IoT.DriverCore.Core.LogicalTagKey<T> tag, T value, System.Threading.CancellationToken cancellationToken)
```
Executes the `WriteValueAsync` operation.

- Parameter `tag`: The `tag` value.
- Parameter `value`: The `value` value.
- Parameter `cancellationToken`: The `cancellationToken` value.
- Returns: A `System.Threading.Tasks.Task` result.

###### `P:IoT.DriverCore.OmronPlcRx.IOmronPlcRx.ControllerModel`

```csharp
public string ControllerModel { get; }
```
Gets the PLC controller model string.

- Value: The `ControllerModel` value.

###### `P:IoT.DriverCore.OmronPlcRx.IOmronPlcRx.ControllerVersion`

```csharp
public string ControllerVersion { get; }
```
Gets the PLC controller version string.

- Value: The `ControllerVersion` value.

###### `P:IoT.DriverCore.OmronPlcRx.IOmronPlcRx.Errors`

```csharp
public System.IObservable<IoT.DriverCore.OmronPlcRx.OmronPLCException> Errors { get; }
```
Gets an observable of operational errors.

- Value: The `Errors` value.

###### `P:IoT.DriverCore.OmronPlcRx.IOmronPlcRx.ObserveAll`

```csharp
public System.IObservable<IoT.DriverCore.OmronPlcRx.Tags.IPlcTag> ObserveAll { get; }
```
Gets an observable of all tag change events.

- Value: The `ObserveAll` value.

###### `P:IoT.DriverCore.OmronPlcRx.IOmronPlcRx.PlcType`

```csharp
public IoT.DriverCore.OmronPlcRx.Enums.PlcType PlcType { get; }
```
Gets the detected PLC type.

- Value: The `PlcType` value.

#### `T:IoT.DriverCore.OmronPlcRx.OmronConnectionOptions`

```csharp
public class IoT.DriverCore.OmronPlcRx.OmronConnectionOptions
```
Configures an Omron PLC transport connection.

##### Declared public members

###### `M:IoT.DriverCore.OmronPlcRx.OmronConnectionOptions.#ctor(System.Byte,System.Byte,IoT.DriverCore.OmronPlcRx.Enums.ConnectionMethod,System.String)`

```csharp
public IoT.DriverCore.OmronPlcRx.OmronConnectionOptions(byte localNodeId, byte remoteNodeId, IoT.DriverCore.OmronPlcRx.Enums.ConnectionMethod connectionMethod, string remoteHost)
```
Configures an Omron PLC transport connection.

- Parameter `localNodeId`: Local FINS node identifier.
- Parameter `remoteNodeId`: Remote PLC FINS node identifier.
- Parameter `connectionMethod`: Transport to use.
- Parameter `remoteHost`: PLC hostname, IP address, or serial port name.

###### `P:IoT.DriverCore.OmronPlcRx.OmronConnectionOptions.ConnectionMethod`

```csharp
public IoT.DriverCore.OmronPlcRx.Enums.ConnectionMethod ConnectionMethod { get; }
```
Gets the transport to use.

- Value: The `ConnectionMethod` value.

###### `P:IoT.DriverCore.OmronPlcRx.OmronConnectionOptions.LocalNodeId`

```csharp
public byte LocalNodeId { get; }
```
Gets the local FINS node identifier.

- Value: The `LocalNodeId` value.

###### `P:IoT.DriverCore.OmronPlcRx.OmronConnectionOptions.Port`

```csharp
public int Port { get; set; }
```
Gets or initializes the network service port.

- Value: The `Port` value.

###### `P:IoT.DriverCore.OmronPlcRx.OmronConnectionOptions.RemoteHost`

```csharp
public string RemoteHost { get; }
```
Gets the PLC hostname, IP address, or serial port name.

- Value: The `RemoteHost` value.

###### `P:IoT.DriverCore.OmronPlcRx.OmronConnectionOptions.RemoteNodeId`

```csharp
public byte RemoteNodeId { get; }
```
Gets the remote PLC FINS node identifier.

- Value: The `RemoteNodeId` value.

###### `P:IoT.DriverCore.OmronPlcRx.OmronConnectionOptions.Retries`

```csharp
public int Retries { get; set; }
```
Gets or initializes the transient retry count.

- Value: The `Retries` value.

###### `P:IoT.DriverCore.OmronPlcRx.OmronConnectionOptions.SerialOptions`

```csharp
public IoT.DriverCore.OmronPlcRx.OmronSerialOptions SerialOptions { get; set; }
```
Gets or initializes serial transport settings.

- Value: The `SerialOptions` value.

###### `P:IoT.DriverCore.OmronPlcRx.OmronConnectionOptions.Timeout`

```csharp
public int Timeout { get; set; }
```
Gets or initializes the request timeout in milliseconds.

- Value: The `Timeout` value.

#### `T:IoT.DriverCore.OmronPlcRx.OmronHostLinkFinsFrameMode`

```csharp
public enum IoT.DriverCore.OmronPlcRx.OmronHostLinkFinsFrameMode
```
Specifies the Host Link FINS frame layout used over serial communications.

##### Declared public members

###### `F:IoT.DriverCore.OmronPlcRx.OmronHostLinkFinsFrameMode.Direct`

```csharp
public static const IoT.DriverCore.OmronPlcRx.OmronHostLinkFinsFrameMode Direct
```
Directly connected host-computer-to-CPU format using ICF/DA2/SA2/SID fields.

###### `F:IoT.DriverCore.OmronPlcRx.OmronHostLinkFinsFrameMode.Network`

```csharp
public static const IoT.DriverCore.OmronPlcRx.OmronHostLinkFinsFrameMode Network
```
Network-capable format using the complete FINS header.

#### `T:IoT.DriverCore.OmronPlcRx.OmronLogicalTagClient`

```csharp
public class IoT.DriverCore.OmronPlcRx.OmronLogicalTagClient
```
Contains grouped FINS operations for the logical-tag client.

##### Declared public members

###### `M:IoT.DriverCore.OmronPlcRx.OmronLogicalTagClient.#ctor(IoT.DriverCore.OmronPlcRx.IOmronPlcRx)`

```csharp
public IoT.DriverCore.OmronPlcRx.OmronLogicalTagClient(IoT.DriverCore.OmronPlcRx.IOmronPlcRx plc)
```
Initializes a new instance of the `T:IoT.DriverCore.OmronPlcRx.OmronLogicalTagClient` class.

- Parameter `plc`: Omron PLC facade used for protocol operations.

###### `M:IoT.DriverCore.OmronPlcRx.OmronLogicalTagClient.#ctor(IoT.DriverCore.OmronPlcRx.IOmronPlcRx,IoT.DriverCore.Core.ILogicalTagCatalog)`

```csharp
public IoT.DriverCore.OmronPlcRx.OmronLogicalTagClient(IoT.DriverCore.OmronPlcRx.IOmronPlcRx plc, IoT.DriverCore.Core.ILogicalTagCatalog catalog)
```
Initializes a new instance of the `T:IoT.DriverCore.OmronPlcRx.OmronLogicalTagClient` class.

- Parameter `plc`: Omron PLC facade used for protocol operations.
- Parameter `catalog`: Logical-tag catalog.

###### `M:IoT.DriverCore.OmronPlcRx.OmronLogicalTagClient.#ctor(IoT.DriverCore.OmronPlcRx.IOmronPlcRx,IoT.DriverCore.Core.ILogicalTagCatalog,IoT.DriverCore.Core.LogicalTagSqliteStore)`

```csharp
public IoT.DriverCore.OmronPlcRx.OmronLogicalTagClient(IoT.DriverCore.OmronPlcRx.IOmronPlcRx plc, IoT.DriverCore.Core.ILogicalTagCatalog catalog, IoT.DriverCore.Core.LogicalTagSqliteStore store)
```
Initializes a new instance of the `T:IoT.DriverCore.OmronPlcRx.OmronLogicalTagClient` class.

- Parameter `plc`: Omron PLC facade used for protocol operations.
- Parameter `catalog`: Logical-tag catalog.
- Parameter `store`: Optional SQLite store.

###### `M:IoT.DriverCore.OmronPlcRx.OmronLogicalTagClient.#ctor(IoT.DriverCore.OmronPlcRx.IOmronPlcRx,System.String)`

```csharp
public IoT.DriverCore.OmronPlcRx.OmronLogicalTagClient(IoT.DriverCore.OmronPlcRx.IOmronPlcRx plc, string sqliteConnectionString)
```
Initializes a new instance of the `T:IoT.DriverCore.OmronPlcRx.OmronLogicalTagClient` class.

- Parameter `plc`: Omron PLC facade used for protocol operations.
- Parameter `sqliteConnectionString`: SQLite connection string.

###### `M:IoT.DriverCore.OmronPlcRx.OmronLogicalTagClient.CreateTag``1(IoT.DriverCore.OmronPlcRx.Tags.PlcTag`1{``0})`

```csharp
public IoT.DriverCore.Core.LogicalTag CreateTag<T>(IoT.DriverCore.OmronPlcRx.Tags.PlcTag<T> tag)
```
Executes the `CreateTag` operation.

- Parameter `tag`: The `tag` value.
- Returns: A `IoT.DriverCore.Core.LogicalTag` result.

###### `M:IoT.DriverCore.OmronPlcRx.OmronLogicalTagClient.CreateTag``1(IoT.DriverCore.OmronPlcRx.Tags.PlcTag`1{``0},System.String,System.String,System.Collections.Generic.IReadOnlyDictionary`2{System.String,System.String},IoT.DriverCore.Core.LogicalTagAccessMode,System.Nullable`1{System.TimeSpan})`

```csharp
public IoT.DriverCore.Core.LogicalTag CreateTag<T>(IoT.DriverCore.OmronPlcRx.Tags.PlcTag<T> tag, string groupName, string description, System.Collections.Generic.IReadOnlyDictionary<string, string> metadata, IoT.DriverCore.Core.LogicalTagAccessMode accessMode, System.Nullable<System.TimeSpan> scanInterval)
```
Executes the `CreateTag` operation.

- Parameter `tag`: The `tag` value.
- Parameter `groupName`: The `groupName` value.
- Parameter `description`: The `description` value.
- Parameter `metadata`: The `metadata` value.
- Parameter `accessMode`: The `accessMode` value.
- Parameter `scanInterval`: The `scanInterval` value.
- Returns: A `IoT.DriverCore.Core.LogicalTag` result.

###### `M:IoT.DriverCore.OmronPlcRx.OmronLogicalTagClient.DeleteGroupAsync(System.String,System.Threading.CancellationToken)`

```csharp
public System.Threading.Tasks.Task<bool> DeleteGroupAsync(string name, System.Threading.CancellationToken cancellationToken)
```
Deletes a persisted tag group.

- Parameter `name`: Logical tag group name.
- Parameter `cancellationToken`: Token used to cancel the operation.
- Returns: True when the persisted group existed; otherwise false.

###### `M:IoT.DriverCore.OmronPlcRx.OmronLogicalTagClient.DeleteTagAsync(System.String,System.Threading.CancellationToken)`

```csharp
public System.Threading.Tasks.Task<bool> DeleteTagAsync(string name, System.Threading.CancellationToken cancellationToken)
```
Deletes a persisted and registered tag.

- Parameter `name`: Logical tag name.
- Parameter `cancellationToken`: Token used to cancel the operation.
- Returns: True when the persisted tag existed; otherwise false.

###### `M:IoT.DriverCore.OmronPlcRx.OmronLogicalTagClient.Dispose`

```csharp
public void Dispose()
```
Inherits XML documentation from its implemented or overridden member.

###### `M:IoT.DriverCore.OmronPlcRx.OmronLogicalTagClient.EditTagAsync(IoT.DriverCore.Core.LogicalTag,System.Threading.CancellationToken)`

```csharp
public System.Threading.Tasks.Task<bool> EditTagAsync(IoT.DriverCore.Core.LogicalTag tag, System.Threading.CancellationToken cancellationToken)
```
Edits an existing persisted tag and refreshes its registration.

- Parameter `tag`: Logical tag definition.
- Parameter `cancellationToken`: Token used to cancel the operation.
- Returns: True when the persisted tag existed; otherwise false.

###### `M:IoT.DriverCore.OmronPlcRx.OmronLogicalTagClient.ExportCsvAsync(System.IO.TextWriter,System.Char,System.Threading.CancellationToken)`

```csharp
public System.Threading.Tasks.Task ExportCsvAsync(System.IO.TextWriter writer, char delimiter, System.Threading.CancellationToken cancellationToken)
```
Exports the current catalog as RFC 4180 CSV.

- Parameter `writer`: CSV destination writer.
- Parameter `delimiter`: CSV delimiter.
- Parameter `cancellationToken`: Token used to cancel the export.
- Returns: A task representing the export.

###### `M:IoT.DriverCore.OmronPlcRx.OmronLogicalTagClient.GetGroupAsync(System.String,System.Threading.CancellationToken)`

```csharp
public System.Threading.Tasks.Task<IoT.DriverCore.Core.LogicalTagGroup> GetGroupAsync(string name, System.Threading.CancellationToken cancellationToken)
```
Gets a persisted tag group.

- Parameter `name`: Logical tag group name.
- Parameter `cancellationToken`: Token used to cancel the query.
- Returns: The matching group when present; otherwise null.

###### `M:IoT.DriverCore.OmronPlcRx.OmronLogicalTagClient.GetTagAsync(System.String,System.Threading.CancellationToken)`

```csharp
public System.Threading.Tasks.Task<IoT.DriverCore.Core.LogicalTag> GetTagAsync(string name, System.Threading.CancellationToken cancellationToken)
```
Gets a persisted tag by name.

- Parameter `name`: Logical tag name.
- Parameter `cancellationToken`: Token used to cancel the query.
- Returns: The matching tag when present; otherwise null.

###### `M:IoT.DriverCore.OmronPlcRx.OmronLogicalTagClient.ImportCsvAsync(System.IO.TextReader,System.Char,System.Threading.CancellationToken)`

```csharp
public System.Threading.Tasks.Task<System.Collections.Generic.IReadOnlyList<IoT.DriverCore.Core.LogicalTag>> ImportCsvAsync(System.IO.TextReader reader, char delimiter, System.Threading.CancellationToken cancellationToken)
```
Imports RFC 4180 CSV definitions and registers them dynamically.

- Parameter `reader`: CSV source reader.
- Parameter `delimiter`: CSV delimiter.
- Parameter `cancellationToken`: Token used to cancel the import.
- Returns: The imported logical tags.

###### `M:IoT.DriverCore.OmronPlcRx.OmronLogicalTagClient.InitializeStoreAsync(System.Threading.CancellationToken)`

```csharp
public System.Threading.Tasks.Task InitializeStoreAsync(System.Threading.CancellationToken cancellationToken)
```
Initializes the configured SQLite store.

- Parameter `cancellationToken`: Token used to cancel initialization.
- Returns: A task representing initialization.

###### `M:IoT.DriverCore.OmronPlcRx.OmronLogicalTagClient.ListGroupsAsync(System.Threading.CancellationToken)`

```csharp
public System.Threading.Tasks.Task<System.Collections.Generic.IReadOnlyList<IoT.DriverCore.Core.LogicalTagGroup>> ListGroupsAsync(System.Threading.CancellationToken cancellationToken)
```
Lists persisted tag groups.

- Parameter `cancellationToken`: Token used to cancel the query.
- Returns: The persisted groups.

###### `M:IoT.DriverCore.OmronPlcRx.OmronLogicalTagClient.ListTagsAsync(System.Threading.CancellationToken)`

```csharp
public System.Threading.Tasks.Task<System.Collections.Generic.IReadOnlyList<IoT.DriverCore.Core.LogicalTag>> ListTagsAsync(System.Threading.CancellationToken cancellationToken)
```
Lists persisted tags.

- Parameter `cancellationToken`: Token used to cancel the query.
- Returns: The persisted logical tags.

###### `M:IoT.DriverCore.OmronPlcRx.OmronLogicalTagClient.LoadTagsAsync(System.Threading.CancellationToken)`

```csharp
public System.Threading.Tasks.Task<System.Collections.Generic.IReadOnlyList<IoT.DriverCore.Core.LogicalTag>> LoadTagsAsync(System.Threading.CancellationToken cancellationToken)
```
Loads and dynamically registers all tags from the configured SQLite store.

- Parameter `cancellationToken`: Token used to cancel the load.
- Returns: The loaded logical tags.

###### `M:IoT.DriverCore.OmronPlcRx.OmronLogicalTagClient.Observe(System.String)`

```csharp
public System.IObservable<IoT.DriverCore.Core.LogicalTagValue> Observe(string tagName)
```
Inherits XML documentation from its implemented or overridden member.

- Parameter `tagName`: The `tagName` value.
- Returns: A `System.IObservable<IoT.DriverCore.Core.LogicalTagValue>` result.

###### `M:IoT.DriverCore.OmronPlcRx.OmronLogicalTagClient.ObserveAsync(System.String,System.Threading.CancellationToken)`

```csharp
public System.Collections.Generic.IAsyncEnumerable<IoT.DriverCore.Core.LogicalTagValue> ObserveAsync(string tagName, System.Threading.CancellationToken cancellationToken)
```
Inherits XML documentation from its implemented or overridden member.

- Parameter `tagName`: The `tagName` value.
- Parameter `cancellationToken`: The `cancellationToken` value.
- Returns: A `System.Collections.Generic.IAsyncEnumerable<IoT.DriverCore.Core.LogicalTagValue>` result.

###### `M:IoT.DriverCore.OmronPlcRx.OmronLogicalTagClient.ObserveMany(System.Collections.Generic.IReadOnlyCollection`1{System.String})`

```csharp
public System.IObservable<IoT.DriverCore.Core.LogicalTagValue> ObserveMany(System.Collections.Generic.IReadOnlyCollection<string> tagNames)
```
Executes the `ObserveMany` operation.

- Parameter `tagNames`: The `tagNames` value.
- Returns: A `System.IObservable<IoT.DriverCore.Core.LogicalTagValue>` result.

###### `M:IoT.DriverCore.OmronPlcRx.OmronLogicalTagClient.ObserveManyAsync(System.Collections.Generic.IReadOnlyCollection`1{System.String},System.Threading.CancellationToken)`

```csharp
public System.Collections.Generic.IAsyncEnumerable<IoT.DriverCore.Core.LogicalTagValue> ObserveManyAsync(System.Collections.Generic.IReadOnlyCollection<string> tagNames, System.Threading.CancellationToken cancellationToken)
```
Executes the `ObserveManyAsync` operation.

- Parameter `tagNames`: The `tagNames` value.
- Parameter `cancellationToken`: The `cancellationToken` value.
- Returns: A `System.Collections.Generic.IAsyncEnumerable<IoT.DriverCore.Core.LogicalTagValue>` result.

###### `M:IoT.DriverCore.OmronPlcRx.OmronLogicalTagClient.ReadAsync(System.String,System.Threading.CancellationToken)`

```csharp
public System.Threading.Tasks.Task<IoT.DriverCore.Core.TagOperationResult<IoT.DriverCore.Core.LogicalTagValue>> ReadAsync(string tagName, System.Threading.CancellationToken cancellationToken)
```
Inherits XML documentation from its implemented or overridden member.

- Parameter `tagName`: The `tagName` value.
- Parameter `cancellationToken`: The `cancellationToken` value.
- Returns: A `System.Threading.Tasks.Task<IoT.DriverCore.Core.TagOperationResult<IoT.DriverCore.Core.LogicalTagValue>>` result.

###### `M:IoT.DriverCore.OmronPlcRx.OmronLogicalTagClient.ReadManyAsync(System.Collections.Generic.IReadOnlyCollection`1{System.String},System.Threading.CancellationToken)`

```csharp
public System.Threading.Tasks.Task<System.Collections.Generic.IReadOnlyList<IoT.DriverCore.Core.TagOperationResult<IoT.DriverCore.Core.LogicalTagValue>>> ReadManyAsync(System.Collections.Generic.IReadOnlyCollection<string> tagNames, System.Threading.CancellationToken cancellationToken)
```
Executes the `ReadManyAsync` operation.

- Parameter `tagNames`: The `tagNames` value.
- Parameter `cancellationToken`: The `cancellationToken` value.
- Returns: A `System.Threading.Tasks.Task<System.Collections.Generic.IReadOnlyList<IoT.DriverCore.Core.TagOperationResult<IoT.DriverCore.Core.LogicalTagValue>>>` result.

###### `M:IoT.DriverCore.OmronPlcRx.OmronLogicalTagClient.RegisterTag(IoT.DriverCore.Core.LogicalTag)`

```csharp
public void RegisterTag(IoT.DriverCore.Core.LogicalTag tag)
```
Registers or replaces a logical tag in both the Omron facade and catalog.

- Parameter `tag`: Logical tag to register.

###### `M:IoT.DriverCore.OmronPlcRx.OmronLogicalTagClient.RemoveTag(System.String)`

```csharp
public bool RemoveTag(string name)
```
Removes a logical tag from the Omron facade and catalog.

- Parameter `name`: Logical tag name.
- Returns: True when either registration was removed; otherwise false.

###### `M:IoT.DriverCore.OmronPlcRx.OmronLogicalTagClient.UpsertGroupAsync(IoT.DriverCore.Core.LogicalTagGroup,System.Threading.CancellationToken)`

```csharp
public System.Threading.Tasks.Task UpsertGroupAsync(IoT.DriverCore.Core.LogicalTagGroup group, System.Threading.CancellationToken cancellationToken)
```
Upserts a persisted tag group.

- Parameter `group`: Logical tag group.
- Parameter `cancellationToken`: Token used to cancel the operation.
- Returns: A task representing the operation.

###### `M:IoT.DriverCore.OmronPlcRx.OmronLogicalTagClient.UpsertTagAsync(IoT.DriverCore.Core.LogicalTag,System.Threading.CancellationToken)`

```csharp
public System.Threading.Tasks.Task UpsertTagAsync(IoT.DriverCore.Core.LogicalTag tag, System.Threading.CancellationToken cancellationToken)
```
Upserts a persisted tag and registers the resulting definition.

- Parameter `tag`: Logical tag to upsert.
- Parameter `cancellationToken`: Token used to cancel the operation.
- Returns: A task representing the operation.

###### `M:IoT.DriverCore.OmronPlcRx.OmronLogicalTagClient.WriteAsync(IoT.DriverCore.Core.LogicalTagValue,System.Threading.CancellationToken)`

```csharp
public System.Threading.Tasks.Task<IoT.DriverCore.Core.TagOperationResult<IoT.DriverCore.Core.LogicalTagValue>> WriteAsync(IoT.DriverCore.Core.LogicalTagValue value, System.Threading.CancellationToken cancellationToken)
```
Inherits XML documentation from its implemented or overridden member.

- Parameter `value`: The `value` value.
- Parameter `cancellationToken`: The `cancellationToken` value.
- Returns: A `System.Threading.Tasks.Task<IoT.DriverCore.Core.TagOperationResult<IoT.DriverCore.Core.LogicalTagValue>>` result.

###### `M:IoT.DriverCore.OmronPlcRx.OmronLogicalTagClient.WriteManyAsync(System.Collections.Generic.IReadOnlyCollection`1{IoT.DriverCore.Core.LogicalTagValue},System.Threading.CancellationToken)`

```csharp
public System.Threading.Tasks.Task<System.Collections.Generic.IReadOnlyList<IoT.DriverCore.Core.TagOperationResult<IoT.DriverCore.Core.LogicalTagValue>>> WriteManyAsync(System.Collections.Generic.IReadOnlyCollection<IoT.DriverCore.Core.LogicalTagValue> values, System.Threading.CancellationToken cancellationToken)
```
Executes the `WriteManyAsync` operation.

- Parameter `values`: The `values` value.
- Parameter `cancellationToken`: The `cancellationToken` value.
- Returns: A `System.Threading.Tasks.Task<System.Collections.Generic.IReadOnlyList<IoT.DriverCore.Core.TagOperationResult<IoT.DriverCore.Core.LogicalTagValue>>>` result.

###### `P:IoT.DriverCore.OmronPlcRx.OmronLogicalTagClient.Catalog`

```csharp
public IoT.DriverCore.Core.ILogicalTagCatalog Catalog { get; }
```
Gets the logical-tag catalog composed by this client.

- Value: The `Catalog` value.

#### `T:IoT.DriverCore.OmronPlcRx.OmronPLCException`

```csharp
public class IoT.DriverCore.OmronPlcRx.OmronPLCException
```
Represents errors that occur during Omron PLC communication or processing.

##### Declared public members

###### `M:IoT.DriverCore.OmronPlcRx.OmronPLCException.#ctor`

```csharp
public IoT.DriverCore.OmronPlcRx.OmronPLCException()
```
Initializes a new instance of the `T:IoT.DriverCore.OmronPlcRx.OmronPLCException` class.

###### `M:IoT.DriverCore.OmronPlcRx.OmronPLCException.#ctor(System.String)`

```csharp
public IoT.DriverCore.OmronPlcRx.OmronPLCException(string message)
```
Initializes a new instance of the `T:IoT.DriverCore.OmronPlcRx.OmronPLCException` class.

- Parameter `message`: The message that describes the error.

###### `M:IoT.DriverCore.OmronPlcRx.OmronPLCException.#ctor(System.String,System.Exception)`

```csharp
public IoT.DriverCore.OmronPlcRx.OmronPLCException(string message, System.Exception innerException)
```
Initializes a new instance of the `T:IoT.DriverCore.OmronPlcRx.OmronPLCException` class.

- Parameter `message`: The error message that explains the reason for the exception.
- Parameter `innerException`: The exception that is the cause of the current exception.

#### `T:IoT.DriverCore.OmronPlcRx.OmronPlcRx`

```csharp
public class IoT.DriverCore.OmronPlcRx.OmronPlcRx
```
Contains PLC tag address parsing helpers.

##### Declared public members

###### `M:IoT.DriverCore.OmronPlcRx.OmronPlcRx.#ctor(IoT.DriverCore.OmronPlcRx.OmronConnectionOptions,System.Nullable`1{System.TimeSpan})`

```csharp
public IoT.DriverCore.OmronPlcRx.OmronPlcRx(IoT.DriverCore.OmronPlcRx.OmronConnectionOptions options, System.Nullable<System.TimeSpan> pollInterval)
```
Initializes a new instance of `IoT.DriverCore.OmronPlcRx.OmronPlcRx`.

- Parameter `options`: The `options` value.
- Parameter `pollInterval`: The `pollInterval` value.

###### `M:IoT.DriverCore.OmronPlcRx.OmronPlcRx.#ctor(System.Byte,System.Byte,IoT.DriverCore.OmronPlcRx.OmronSerialOptions,System.Int32,System.Int32,System.Nullable`1{System.TimeSpan})`

```csharp
public IoT.DriverCore.OmronPlcRx.OmronPlcRx(byte localNodeId, byte remoteNodeId, IoT.DriverCore.OmronPlcRx.OmronSerialOptions serialOptions, int timeout, int retries, System.Nullable<System.TimeSpan> pollInterval)
```
Initializes a new instance of `IoT.DriverCore.OmronPlcRx.OmronPlcRx`.

- Parameter `localNodeId`: The `localNodeId` value.
- Parameter `remoteNodeId`: The `remoteNodeId` value.
- Parameter `serialOptions`: The `serialOptions` value.
- Parameter `timeout`: The `timeout` value.
- Parameter `retries`: The `retries` value.
- Parameter `pollInterval`: The `pollInterval` value.

###### `M:IoT.DriverCore.OmronPlcRx.OmronPlcRx.AddUpdateTagItem``1(IoT.DriverCore.OmronPlcRx.Tags.PlcTag`1{``0})`

```csharp
public void AddUpdateTagItem<T>(IoT.DriverCore.OmronPlcRx.Tags.PlcTag<T> tag)
```
Executes the `AddUpdateTagItem` operation.

- Parameter `tag`: The `tag` value.

###### `M:IoT.DriverCore.OmronPlcRx.OmronPlcRx.Dispose`

```csharp
public void Dispose()
```
Dispose pattern.

###### `M:IoT.DriverCore.OmronPlcRx.OmronPlcRx.GetValue``1(IoT.DriverCore.Core.LogicalTagKey`1{``0})`

```csharp
public T GetValue<T>(IoT.DriverCore.Core.LogicalTagKey<T> tag)
```
Executes the `GetValue` operation.

- Parameter `tag`: The `tag` value.
- Returns: A `T` result.

###### `M:IoT.DriverCore.OmronPlcRx.OmronPlcRx.Observe``1(IoT.DriverCore.Core.LogicalTagKey`1{``0})`

```csharp
public System.IObservable<T> Observe<T>(IoT.DriverCore.Core.LogicalTagKey<T> tag)
```
Executes the `Observe` operation.

- Parameter `tag`: The `tag` value.
- Returns: A `System.IObservable<T>` result.

###### `M:IoT.DriverCore.OmronPlcRx.OmronPlcRx.ReadClockAsync(System.Threading.CancellationToken)`

```csharp
public System.Threading.Tasks.Task<IoT.DriverCore.OmronPlcRx.Results.ReadClockResult> ReadClockAsync(System.Threading.CancellationToken cancellationToken)
```
Reads the PLC real-time clock via the underlying connection.

- Parameter `cancellationToken`: Cancellation token.
- Returns: Clock read result.

###### `M:IoT.DriverCore.OmronPlcRx.OmronPlcRx.ReadCycleTimeAsync(System.Threading.CancellationToken)`

```csharp
public System.Threading.Tasks.Task<IoT.DriverCore.OmronPlcRx.Results.ReadCycleTimeResult> ReadCycleTimeAsync(System.Threading.CancellationToken cancellationToken)
```
Reads PLC scan cycle time statistics via the underlying connection.

- Parameter `cancellationToken`: Cancellation token.
- Returns: Cycle time statistics.

###### `M:IoT.DriverCore.OmronPlcRx.OmronPlcRx.ReadValueAsync``1(IoT.DriverCore.Core.LogicalTagKey`1{``0},System.Threading.CancellationToken)`

```csharp
public System.Threading.Tasks.Task<T> ReadValueAsync<T>(IoT.DriverCore.Core.LogicalTagKey<T> tag, System.Threading.CancellationToken cancellationToken)
```
Executes the `ReadValueAsync` operation.

- Parameter `tag`: The `tag` value.
- Parameter `cancellationToken`: The `cancellationToken` value.
- Returns: A `System.Threading.Tasks.Task<T>` result.

###### `M:IoT.DriverCore.OmronPlcRx.OmronPlcRx.RemoveTagItem(System.String)`

```csharp
public bool RemoveTagItem(string tagName)
```
Inherits XML documentation from its implemented or overridden member.

- Parameter `tagName`: The `tagName` value.
- Returns: A `bool` result.

###### `M:IoT.DriverCore.OmronPlcRx.OmronPlcRx.SetValue``1(IoT.DriverCore.Core.LogicalTagKey`1{``0},``0)`

```csharp
public void SetValue<T>(IoT.DriverCore.Core.LogicalTagKey<T> tag, T value)
```
Executes the `SetValue` operation.

- Parameter `tag`: The `tag` value.
- Parameter `value`: The `value` value.

###### `M:IoT.DriverCore.OmronPlcRx.OmronPlcRx.WriteClockAsync(System.DateTimeOffset,System.Int32,System.Threading.CancellationToken)`

```csharp
public System.Threading.Tasks.Task<IoT.DriverCore.OmronPlcRx.Results.WriteClockResult> WriteClockAsync(System.DateTimeOffset newDateTime, int newDayOfWeek, System.Threading.CancellationToken cancellationToken)
```
Writes the PLC real-time clock with explicit day-of-week via the underlying connection.

- Parameter `newDateTime`: New date/time.
- Parameter `newDayOfWeek`: Day of week (0-6).
- Parameter `cancellationToken`: Cancellation token.
- Returns: Clock write result.

###### `M:IoT.DriverCore.OmronPlcRx.OmronPlcRx.WriteClockAsync(System.DateTimeOffset,System.Threading.CancellationToken)`

```csharp
public System.Threading.Tasks.Task<IoT.DriverCore.OmronPlcRx.Results.WriteClockResult> WriteClockAsync(System.DateTimeOffset newDateTime, System.Threading.CancellationToken cancellationToken)
```
Writes the PLC real-time clock (day-of-week inferred) via the underlying connection.

- Parameter `newDateTime`: New date/time.
- Parameter `cancellationToken`: Cancellation token.
- Returns: Clock write result.

###### `M:IoT.DriverCore.OmronPlcRx.OmronPlcRx.WriteValueAsync``1(IoT.DriverCore.Core.LogicalTagKey`1{``0},``0,System.Threading.CancellationToken)`

```csharp
public System.Threading.Tasks.Task WriteValueAsync<T>(IoT.DriverCore.Core.LogicalTagKey<T> tag, T value, System.Threading.CancellationToken cancellationToken)
```
Executes the `WriteValueAsync` operation.

- Parameter `tag`: The `tag` value.
- Parameter `value`: The `value` value.
- Parameter `cancellationToken`: The `cancellationToken` value.
- Returns: A `System.Threading.Tasks.Task` result.

###### `P:IoT.DriverCore.OmronPlcRx.OmronPlcRx.ControllerModel`

```csharp
public string ControllerModel { get; }
```
Gets the controller model value.

- Value: The `ControllerModel` value.

###### `P:IoT.DriverCore.OmronPlcRx.OmronPlcRx.ControllerVersion`

```csharp
public string ControllerVersion { get; }
```
Gets the controller version value.

- Value: The `ControllerVersion` value.

###### `P:IoT.DriverCore.OmronPlcRx.OmronPlcRx.Errors`

```csharp
public System.IObservable<IoT.DriverCore.OmronPlcRx.OmronPLCException> Errors { get; }
```
Inherits XML documentation from its implemented or overridden member.

- Value: The `Errors` value.

###### `P:IoT.DriverCore.OmronPlcRx.OmronPlcRx.IsDisposed`

```csharp
public bool IsDisposed { get; }
```
Inherits XML documentation from its implemented or overridden member.

- Value: The `IsDisposed` value.

###### `P:IoT.DriverCore.OmronPlcRx.OmronPlcRx.ObserveAll`

```csharp
public System.IObservable<IoT.DriverCore.OmronPlcRx.Tags.IPlcTag> ObserveAll { get; }
```
Inherits XML documentation from its implemented or overridden member.

- Value: The `ObserveAll` value.

###### `P:IoT.DriverCore.OmronPlcRx.OmronPlcRx.PlcType`

```csharp
public IoT.DriverCore.OmronPlcRx.Enums.PlcType PlcType { get; }
```
Gets the plc type value.

- Value: The type of the PLC.

#### `T:IoT.DriverCore.OmronPlcRx.OmronPlcSimulator`

```csharp
public class IoT.DriverCore.OmronPlcRx.OmronPlcSimulator
```
Provides a deterministic, in-memory Omron PLC through `T:IoT.DriverCore.OmronPlcRx.IOmronPlcRx` .

##### Declared public members

###### `M:IoT.DriverCore.OmronPlcRx.OmronPlcSimulator.#ctor`

```csharp
public IoT.DriverCore.OmronPlcRx.OmronPlcSimulator()
```
Initializes a new instance of the `T:IoT.DriverCore.OmronPlcRx.OmronPlcSimulator` class.

###### `M:IoT.DriverCore.OmronPlcRx.OmronPlcSimulator.#ctor(IoT.DriverCore.OmronPlcRx.Enums.PlcType,System.String,System.String,System.Boolean,System.DateTimeOffset)`

```csharp
public IoT.DriverCore.OmronPlcRx.OmronPlcSimulator(IoT.DriverCore.OmronPlcRx.Enums.PlcType plcType, string controllerModel, string controllerVersion, bool initiallyConnected, System.DateTimeOffset initialClock)
```
Initializes a new instance of the `T:IoT.DriverCore.OmronPlcRx.OmronPlcSimulator` class.

- Parameter `plcType`: PLC model family reported to callers.
- Parameter `controllerModel`: Controller model reported to callers.
- Parameter `controllerVersion`: Controller version reported to callers.
- Parameter `initiallyConnected`: Whether the simulated transport starts connected.
- Parameter `initialClock`: Optional deterministic initial clock.

###### `M:IoT.DriverCore.OmronPlcRx.OmronPlcSimulator.#ctor(System.Boolean)`

```csharp
public IoT.DriverCore.OmronPlcRx.OmronPlcSimulator(bool initiallyConnected)
```
Initializes a new instance of the `T:IoT.DriverCore.OmronPlcRx.OmronPlcSimulator` class.

- Parameter `initiallyConnected`: Whether the simulated transport starts connected.

###### `M:IoT.DriverCore.OmronPlcRx.OmronPlcSimulator.AddUpdateTagItem``1(IoT.DriverCore.OmronPlcRx.Tags.PlcTag`1{``0})`

```csharp
public void AddUpdateTagItem<T>(IoT.DriverCore.OmronPlcRx.Tags.PlcTag<T> tag)
```
Executes the `AddUpdateTagItem` operation.

- Parameter `tag`: The `tag` value.

###### `M:IoT.DriverCore.OmronPlcRx.OmronPlcSimulator.ConnectAsync`

```csharp
public System.Threading.Tasks.Task ConnectAsync()
```
Connects the simulated transport.

- Returns: A task that represents the operation.

###### `M:IoT.DriverCore.OmronPlcRx.OmronPlcSimulator.ConnectAsync(System.Threading.CancellationToken)`

```csharp
public System.Threading.Tasks.Task ConnectAsync(System.Threading.CancellationToken cancellationToken)
```
Connects the simulated transport.

- Parameter `cancellationToken`: Cancellation token.
- Returns: A task that represents the operation.

###### `M:IoT.DriverCore.OmronPlcRx.OmronPlcSimulator.Disconnect`

```csharp
public void Disconnect()
```
Disconnects the simulated transport while retaining memory and registrations.

###### `M:IoT.DriverCore.OmronPlcRx.OmronPlcSimulator.Dispose`

```csharp
public void Dispose()
```
Inherits XML documentation from its implemented or overridden member.

###### `M:IoT.DriverCore.OmronPlcRx.OmronPlcSimulator.GetValue``1(IoT.DriverCore.Core.LogicalTagKey`1{``0})`

```csharp
public T GetValue<T>(IoT.DriverCore.Core.LogicalTagKey<T> tag)
```
Executes the `GetValue` operation.

- Parameter `tag`: The `tag` value.
- Returns: A `T` result.

###### `M:IoT.DriverCore.OmronPlcRx.OmronPlcSimulator.Observe``1(IoT.DriverCore.Core.LogicalTagKey`1{``0})`

```csharp
public System.IObservable<T> Observe<T>(IoT.DriverCore.Core.LogicalTagKey<T> tag)
```
Executes the `Observe` operation.

- Parameter `tag`: The `tag` value.
- Returns: A `System.IObservable<T>` result.

###### `M:IoT.DriverCore.OmronPlcRx.OmronPlcSimulator.QueueFault(IoT.DriverCore.OmronPlcRx.OmronSimulatorOperation,System.Exception)`

```csharp
public void QueueFault(IoT.DriverCore.OmronPlcRx.OmronSimulatorOperation operation, System.Exception exception)
```
Queues one disconnecting failure for the selected operation.

- Parameter `operation`: Operation to fail.
- Parameter `exception`: Failure to wrap and publish.

###### `M:IoT.DriverCore.OmronPlcRx.OmronPlcSimulator.QueueFault(IoT.DriverCore.OmronPlcRx.OmronSimulatorOperation,System.Exception,System.Int32,System.Boolean)`

```csharp
public void QueueFault(IoT.DriverCore.OmronPlcRx.OmronSimulatorOperation operation, System.Exception exception, int occurrences, bool disconnect)
```
Queues deterministic failures for the selected operation.

- Parameter `operation`: Operation to fail.
- Parameter `exception`: Failure to wrap and publish.
- Parameter `occurrences`: Number of consecutive failures to queue.
- Parameter `disconnect`: Whether each failure disconnects the simulated transport.

###### `M:IoT.DriverCore.OmronPlcRx.OmronPlcSimulator.ReadClockAsync(System.Threading.CancellationToken)`

```csharp
public System.Threading.Tasks.Task<IoT.DriverCore.OmronPlcRx.Results.ReadClockResult> ReadClockAsync(System.Threading.CancellationToken cancellationToken)
```
Inherits XML documentation from its implemented or overridden member.

- Parameter `cancellationToken`: The `cancellationToken` value.
- Returns: A `System.Threading.Tasks.Task<IoT.DriverCore.OmronPlcRx.Results.ReadClockResult>` result.

###### `M:IoT.DriverCore.OmronPlcRx.OmronPlcSimulator.ReadCycleTimeAsync(System.Threading.CancellationToken)`

```csharp
public System.Threading.Tasks.Task<IoT.DriverCore.OmronPlcRx.Results.ReadCycleTimeResult> ReadCycleTimeAsync(System.Threading.CancellationToken cancellationToken)
```
Inherits XML documentation from its implemented or overridden member.

- Parameter `cancellationToken`: The `cancellationToken` value.
- Returns: A `System.Threading.Tasks.Task<IoT.DriverCore.OmronPlcRx.Results.ReadCycleTimeResult>` result.

###### `M:IoT.DriverCore.OmronPlcRx.OmronPlcSimulator.ReadValueAsync``1(IoT.DriverCore.Core.LogicalTagKey`1{``0},System.Threading.CancellationToken)`

```csharp
public System.Threading.Tasks.Task<T> ReadValueAsync<T>(IoT.DriverCore.Core.LogicalTagKey<T> tag, System.Threading.CancellationToken cancellationToken)
```
Executes the `ReadValueAsync` operation.

- Parameter `tag`: The `tag` value.
- Parameter `cancellationToken`: The `cancellationToken` value.
- Returns: A `System.Threading.Tasks.Task<T>` result.

###### `M:IoT.DriverCore.OmronPlcRx.OmronPlcSimulator.ReconnectAsync`

```csharp
public System.Threading.Tasks.Task ReconnectAsync()
```
Reconnects the simulated transport while retaining memory and registrations.

- Returns: A task that represents the operation.

###### `M:IoT.DriverCore.OmronPlcRx.OmronPlcSimulator.ReconnectAsync(System.Threading.CancellationToken)`

```csharp
public System.Threading.Tasks.Task ReconnectAsync(System.Threading.CancellationToken cancellationToken)
```
Reconnects the simulated transport while retaining memory and registrations.

- Parameter `cancellationToken`: Cancellation token.
- Returns: A task that represents the operation.

###### `M:IoT.DriverCore.OmronPlcRx.OmronPlcSimulator.RemoveTagItem(System.String)`

```csharp
public bool RemoveTagItem(string tagName)
```
Inherits XML documentation from its implemented or overridden member.

- Parameter `tagName`: The `tagName` value.
- Returns: A `bool` result.

###### `M:IoT.DriverCore.OmronPlcRx.OmronPlcSimulator.Seed``1(IoT.DriverCore.OmronPlcRx.Tags.PlcTag`1{``0},``0)`

```csharp
public void Seed<T>(IoT.DriverCore.OmronPlcRx.Tags.PlcTag<T> tag, T value)
```
Executes the `Seed` operation.

- Parameter `tag`: The `tag` value.
- Parameter `value`: The `value` value.

###### `M:IoT.DriverCore.OmronPlcRx.OmronPlcSimulator.SetValue``1(IoT.DriverCore.Core.LogicalTagKey`1{``0},``0)`

```csharp
public void SetValue<T>(IoT.DriverCore.Core.LogicalTagKey<T> tag, T value)
```
Executes the `SetValue` operation.

- Parameter `tag`: The `tag` value.
- Parameter `value`: The `value` value.

###### `M:IoT.DriverCore.OmronPlcRx.OmronPlcSimulator.WriteClockAsync(System.DateTimeOffset,System.Int32,System.Threading.CancellationToken)`

```csharp
public System.Threading.Tasks.Task<IoT.DriverCore.OmronPlcRx.Results.WriteClockResult> WriteClockAsync(System.DateTimeOffset newDateTime, int newDayOfWeek, System.Threading.CancellationToken cancellationToken)
```
Inherits XML documentation from its implemented or overridden member.

- Parameter `newDateTime`: The `newDateTime` value.
- Parameter `newDayOfWeek`: The `newDayOfWeek` value.
- Parameter `cancellationToken`: The `cancellationToken` value.
- Returns: A `System.Threading.Tasks.Task<IoT.DriverCore.OmronPlcRx.Results.WriteClockResult>` result.

###### `M:IoT.DriverCore.OmronPlcRx.OmronPlcSimulator.WriteClockAsync(System.DateTimeOffset,System.Threading.CancellationToken)`

```csharp
public System.Threading.Tasks.Task<IoT.DriverCore.OmronPlcRx.Results.WriteClockResult> WriteClockAsync(System.DateTimeOffset newDateTime, System.Threading.CancellationToken cancellationToken)
```
Inherits XML documentation from its implemented or overridden member.

- Parameter `newDateTime`: The `newDateTime` value.
- Parameter `cancellationToken`: The `cancellationToken` value.
- Returns: A `System.Threading.Tasks.Task<IoT.DriverCore.OmronPlcRx.Results.WriteClockResult>` result.

###### `M:IoT.DriverCore.OmronPlcRx.OmronPlcSimulator.WriteValueAsync``1(IoT.DriverCore.Core.LogicalTagKey`1{``0},``0,System.Threading.CancellationToken)`

```csharp
public System.Threading.Tasks.Task WriteValueAsync<T>(IoT.DriverCore.Core.LogicalTagKey<T> tag, T value, System.Threading.CancellationToken cancellationToken)
```
Executes the `WriteValueAsync` operation.

- Parameter `tag`: The `tag` value.
- Parameter `value`: The `value` value.
- Parameter `cancellationToken`: The `cancellationToken` value.
- Returns: A `System.Threading.Tasks.Task` result.

###### `P:IoT.DriverCore.OmronPlcRx.OmronPlcSimulator.AverageCycleTime`

```csharp
public double AverageCycleTime { get; set; }
```
Gets or sets simulated average cycle time in milliseconds.

- Value: The `AverageCycleTime` value.

###### `P:IoT.DriverCore.OmronPlcRx.OmronPlcSimulator.ControllerModel`

```csharp
public string ControllerModel { get; }
```
Inherits XML documentation from its implemented or overridden member.

- Value: The `ControllerModel` value.

###### `P:IoT.DriverCore.OmronPlcRx.OmronPlcSimulator.ControllerVersion`

```csharp
public string ControllerVersion { get; }
```
Inherits XML documentation from its implemented or overridden member.

- Value: The `ControllerVersion` value.

###### `P:IoT.DriverCore.OmronPlcRx.OmronPlcSimulator.Errors`

```csharp
public System.IObservable<IoT.DriverCore.OmronPlcRx.OmronPLCException> Errors { get; }
```
Inherits XML documentation from its implemented or overridden member.

- Value: The `Errors` value.

###### `P:IoT.DriverCore.OmronPlcRx.OmronPlcSimulator.IsConnected`

```csharp
public bool IsConnected { get; }
```
Gets a value indicating whether the simulated transport is connected.

- Value: The `IsConnected` value.

###### `P:IoT.DriverCore.OmronPlcRx.OmronPlcSimulator.IsDisposed`

```csharp
public bool IsDisposed { get; }
```
Inherits XML documentation from its implemented or overridden member.

- Value: The `IsDisposed` value.

###### `P:IoT.DriverCore.OmronPlcRx.OmronPlcSimulator.MaximumCycleTime`

```csharp
public double MaximumCycleTime { get; set; }
```
Gets or sets simulated maximum cycle time in milliseconds.

- Value: The `MaximumCycleTime` value.

###### `P:IoT.DriverCore.OmronPlcRx.OmronPlcSimulator.MinimumCycleTime`

```csharp
public double MinimumCycleTime { get; set; }
```
Gets or sets simulated minimum cycle time in milliseconds.

- Value: The `MinimumCycleTime` value.

###### `P:IoT.DriverCore.OmronPlcRx.OmronPlcSimulator.ObserveAll`

```csharp
public System.IObservable<IoT.DriverCore.OmronPlcRx.Tags.IPlcTag> ObserveAll { get; }
```
Inherits XML documentation from its implemented or overridden member.

- Value: The `ObserveAll` value.

###### `P:IoT.DriverCore.OmronPlcRx.OmronPlcSimulator.Operations`

```csharp
public System.Collections.Generic.IReadOnlyList<IoT.DriverCore.OmronPlcRx.OmronSimulatorOperationRecord> Operations { get; }
```
Gets a snapshot of completed simulator operations.

- Value: The `Operations` value.

###### `P:IoT.DriverCore.OmronPlcRx.OmronPlcSimulator.PlcType`

```csharp
public IoT.DriverCore.OmronPlcRx.Enums.PlcType PlcType { get; }
```
Inherits XML documentation from its implemented or overridden member.

- Value: The `PlcType` value.

###### `P:IoT.DriverCore.OmronPlcRx.OmronPlcSimulator.ReconnectCount`

```csharp
public int ReconnectCount { get; }
```
Gets the number of successful reconnections.

- Value: The `ReconnectCount` value.

#### `T:IoT.DriverCore.OmronPlcRx.OmronSerialOptions`

```csharp
public class IoT.DriverCore.OmronPlcRx.OmronSerialOptions
```
Gets or sets the omron serial options value.

##### Declared public members

###### `M:IoT.DriverCore.OmronPlcRx.OmronSerialOptions.#ctor(System.String)`

```csharp
public IoT.DriverCore.OmronPlcRx.OmronSerialOptions(string portName)
```
Initializes a new instance of the `T:IoT.DriverCore.OmronPlcRx.OmronSerialOptions` class.

- Parameter `portName`: Serial port name, e.g. COM1 or /dev/ttyUSB0.

###### `M:IoT.DriverCore.OmronPlcRx.OmronSerialOptions.CreateToolbus(System.String)`

```csharp
public static IoT.DriverCore.OmronPlcRx.OmronSerialOptions CreateToolbus(string portName)
```
Creates Toolbus serial options using common Omron Toolbus port settings.

- Parameter `portName`: Serial port name, e.g. COM1 or /dev/ttyUSB0.
- Returns: Serial options configured for Toolbus FINS framing.

###### `M:IoT.DriverCore.OmronPlcRx.OmronSerialOptions.Equals(IoT.DriverCore.OmronPlcRx.OmronSerialOptions)`

```csharp
public bool Equals(IoT.DriverCore.OmronPlcRx.OmronSerialOptions other)
```
Determines whether the supplied value is equal to the current value.

- Parameter `other`: The `other` value.
- Returns: A `bool` result.

###### `M:IoT.DriverCore.OmronPlcRx.OmronSerialOptions.Equals(System.Object)`

```csharp
public bool Equals(object obj)
```
Determines whether the supplied value is equal to the current value.

- Parameter `obj`: The `obj` value.
- Returns: A `bool` result.

###### `M:IoT.DriverCore.OmronPlcRx.OmronSerialOptions.GetHashCode`

```csharp
public int GetHashCode()
```
Returns the hash code for the current value.

- Returns: A `int` result.

###### `M:IoT.DriverCore.OmronPlcRx.OmronSerialOptions.ToString`

```csharp
public string ToString()
```
Returns a string representation of the current value.

- Returns: A `string` result.

###### `M:IoT.DriverCore.OmronPlcRx.OmronSerialOptions.Validate`

```csharp
public void Validate()
```
Validates this options instance.

###### `M:IoT.DriverCore.OmronPlcRx.OmronSerialOptions.op_Equality(IoT.DriverCore.OmronPlcRx.OmronSerialOptions,IoT.DriverCore.OmronPlcRx.OmronSerialOptions)`

```csharp
public static bool op_Equality(IoT.DriverCore.OmronPlcRx.OmronSerialOptions left, IoT.DriverCore.OmronPlcRx.OmronSerialOptions right)
```
Determines whether the two supplied values are equal.

- Parameter `left`: The `left` value.
- Parameter `right`: The `right` value.
- Returns: A `bool` result.

###### `M:IoT.DriverCore.OmronPlcRx.OmronSerialOptions.op_Inequality(IoT.DriverCore.OmronPlcRx.OmronSerialOptions,IoT.DriverCore.OmronPlcRx.OmronSerialOptions)`

```csharp
public static bool op_Inequality(IoT.DriverCore.OmronPlcRx.OmronSerialOptions left, IoT.DriverCore.OmronPlcRx.OmronSerialOptions right)
```
Determines whether the two supplied values are not equal.

- Parameter `left`: The `left` value.
- Parameter `right`: The `right` value.
- Returns: A `bool` result.

###### `P:IoT.DriverCore.OmronPlcRx.OmronSerialOptions.BaudRate`

```csharp
public int BaudRate { get; set; }
```
Gets or sets the baud rate value.

- Value: The `BaudRate` value.

###### `P:IoT.DriverCore.OmronPlcRx.OmronSerialOptions.DataBits`

```csharp
public int DataBits { get; set; }
```
Gets or sets the data bits value.

- Value: The `DataBits` value.

###### `P:IoT.DriverCore.OmronPlcRx.OmronSerialOptions.DtrEnable`

```csharp
public bool DtrEnable { get; set; }
```
Gets or sets the dtr enable value.

- Value: The `DtrEnable` value.

###### `P:IoT.DriverCore.OmronPlcRx.OmronSerialOptions.FrameMode`

```csharp
public IoT.DriverCore.OmronPlcRx.OmronHostLinkFinsFrameMode FrameMode { get; set; }
```
Gets or sets the frame mode value.

- Value: The `FrameMode` value.

###### `P:IoT.DriverCore.OmronPlcRx.OmronSerialOptions.Handshake`

```csharp
public System.IO.Ports.Handshake Handshake { get; set; }
```
Gets or sets the handshake value.

- Value: The `Handshake` value.

###### `P:IoT.DriverCore.OmronPlcRx.OmronSerialOptions.HostLinkUnitNumber`

```csharp
public byte HostLinkUnitNumber { get; set; }
```
Gets or sets the host link unit number value.

- Value: The `HostLinkUnitNumber` value.

###### `P:IoT.DriverCore.OmronPlcRx.OmronSerialOptions.MaximumFrameLength`

```csharp
public int MaximumFrameLength { get; set; }
```
Gets or sets the maximum frame length value.

- Value: The `MaximumFrameLength` value.

###### `P:IoT.DriverCore.OmronPlcRx.OmronSerialOptions.Parity`

```csharp
public System.IO.Ports.Parity Parity { get; set; }
```
Gets or sets the parity value.

- Value: The `Parity` value.

###### `P:IoT.DriverCore.OmronPlcRx.OmronSerialOptions.PortName`

```csharp
public string PortName { get; set; }
```
Gets or sets the port name value.

- Value: The `PortName` value.

###### `P:IoT.DriverCore.OmronPlcRx.OmronSerialOptions.Protocol`

```csharp
public IoT.DriverCore.OmronPlcRx.OmronSerialProtocol Protocol { get; set; }
```
Gets or sets the protocol value.

- Value: The `Protocol` value.

###### `P:IoT.DriverCore.OmronPlcRx.OmronSerialOptions.ResponseWaitTime`

```csharp
public byte ResponseWaitTime { get; set; }
```
Gets or sets the response wait time value.

- Value: The `ResponseWaitTime` value.

###### `P:IoT.DriverCore.OmronPlcRx.OmronSerialOptions.RtsEnable`

```csharp
public bool RtsEnable { get; set; }
```
Gets or sets the rts enable value.

- Value: The `RtsEnable` value.

###### `P:IoT.DriverCore.OmronPlcRx.OmronSerialOptions.StopBits`

```csharp
public System.IO.Ports.StopBits StopBits { get; set; }
```
Gets or sets the stop bits value.

- Value: The `StopBits` value.

#### `T:IoT.DriverCore.OmronPlcRx.OmronSerialProtocol`

```csharp
public enum IoT.DriverCore.OmronPlcRx.OmronSerialProtocol
```
Specifies the serial protocol used to carry FINS messages.

##### Declared public members

###### `F:IoT.DriverCore.OmronPlcRx.OmronSerialProtocol.HostLinkFins`

```csharp
public static const IoT.DriverCore.OmronPlcRx.OmronSerialProtocol HostLinkFins
```
Host Link FINS using ASCII FA frames.

###### `F:IoT.DriverCore.OmronPlcRx.OmronSerialProtocol.Toolbus`

```csharp
public static const IoT.DriverCore.OmronPlcRx.OmronSerialProtocol Toolbus
```
Omron Toolbus using binary 0xAB frames carrying binary FINS messages.

#### `T:IoT.DriverCore.OmronPlcRx.OmronSimulatorOperation`

```csharp
public enum IoT.DriverCore.OmronPlcRx.OmronSimulatorOperation
```
Identifies an operation that can be faulted by `T:IoT.DriverCore.OmronPlcRx.OmronPlcSimulator` .

##### Declared public members

###### `F:IoT.DriverCore.OmronPlcRx.OmronSimulatorOperation.Connect`

```csharp
public static const IoT.DriverCore.OmronPlcRx.OmronSimulatorOperation Connect
```
Opening or reopening the simulated connection.

###### `F:IoT.DriverCore.OmronPlcRx.OmronSimulatorOperation.Read`

```csharp
public static const IoT.DriverCore.OmronPlcRx.OmronSimulatorOperation Read
```
Reading a registered tag.

###### `F:IoT.DriverCore.OmronPlcRx.OmronSimulatorOperation.ReadClock`

```csharp
public static const IoT.DriverCore.OmronPlcRx.OmronSimulatorOperation ReadClock
```
Reading the simulated real-time clock.

###### `F:IoT.DriverCore.OmronPlcRx.OmronSimulatorOperation.ReadCycleTime`

```csharp
public static const IoT.DriverCore.OmronPlcRx.OmronSimulatorOperation ReadCycleTime
```
Reading simulated cycle-time statistics.

###### `F:IoT.DriverCore.OmronPlcRx.OmronSimulatorOperation.Write`

```csharp
public static const IoT.DriverCore.OmronPlcRx.OmronSimulatorOperation Write
```
Writing a registered tag.

###### `F:IoT.DriverCore.OmronPlcRx.OmronSimulatorOperation.WriteClock`

```csharp
public static const IoT.DriverCore.OmronPlcRx.OmronSimulatorOperation WriteClock
```
Writing the simulated real-time clock.

#### `T:IoT.DriverCore.OmronPlcRx.OmronSimulatorOperationRecord`

```csharp
public class IoT.DriverCore.OmronPlcRx.OmronSimulatorOperationRecord
```
Describes one completed simulator operation.

##### Declared public members

###### `M:IoT.DriverCore.OmronPlcRx.OmronSimulatorOperationRecord.#ctor(System.Int64,IoT.DriverCore.OmronPlcRx.OmronSimulatorOperation,System.String,System.Object,System.Boolean)`

```csharp
public IoT.DriverCore.OmronPlcRx.OmronSimulatorOperationRecord(long Sequence, IoT.DriverCore.OmronPlcRx.OmronSimulatorOperation Operation, string TagName, object Value, bool Succeeded)
```
Describes one completed simulator operation.

- Parameter `Sequence`: Monotonic operation sequence number.
- Parameter `Operation`: Operation kind.
- Parameter `TagName`: Optional logical tag name.
- Parameter `Value`: Optional operation value.
- Parameter `Succeeded`: Whether the operation succeeded.

###### `M:IoT.DriverCore.OmronPlcRx.OmronSimulatorOperationRecord.Deconstruct(System.Int64@,IoT.DriverCore.OmronPlcRx.OmronSimulatorOperation@,System.String@,System.Object@,System.Boolean@)`

```csharp
public void Deconstruct(out long Sequence, out IoT.DriverCore.OmronPlcRx.OmronSimulatorOperation Operation, out string TagName, out object Value, out bool Succeeded)
```
Deconstructs the value into its component values.

- Parameter `Sequence`: The `Sequence` value.
- Parameter `Operation`: The `Operation` value.
- Parameter `TagName`: The `TagName` value.
- Parameter `Value`: The `Value` value.
- Parameter `Succeeded`: The `Succeeded` value.

###### `M:IoT.DriverCore.OmronPlcRx.OmronSimulatorOperationRecord.Equals(IoT.DriverCore.OmronPlcRx.OmronSimulatorOperationRecord)`

```csharp
public bool Equals(IoT.DriverCore.OmronPlcRx.OmronSimulatorOperationRecord other)
```
Determines whether the supplied value is equal to the current value.

- Parameter `other`: The `other` value.
- Returns: A `bool` result.

###### `M:IoT.DriverCore.OmronPlcRx.OmronSimulatorOperationRecord.Equals(System.Object)`

```csharp
public bool Equals(object obj)
```
Determines whether the supplied value is equal to the current value.

- Parameter `obj`: The `obj` value.
- Returns: A `bool` result.

###### `M:IoT.DriverCore.OmronPlcRx.OmronSimulatorOperationRecord.GetHashCode`

```csharp
public int GetHashCode()
```
Returns the hash code for the current value.

- Returns: A `int` result.

###### `M:IoT.DriverCore.OmronPlcRx.OmronSimulatorOperationRecord.ToString`

```csharp
public string ToString()
```
Returns a string representation of the current value.

- Returns: A `string` result.

###### `M:IoT.DriverCore.OmronPlcRx.OmronSimulatorOperationRecord.op_Equality(IoT.DriverCore.OmronPlcRx.OmronSimulatorOperationRecord,IoT.DriverCore.OmronPlcRx.OmronSimulatorOperationRecord)`

```csharp
public static bool op_Equality(IoT.DriverCore.OmronPlcRx.OmronSimulatorOperationRecord left, IoT.DriverCore.OmronPlcRx.OmronSimulatorOperationRecord right)
```
Determines whether the two supplied values are equal.

- Parameter `left`: The `left` value.
- Parameter `right`: The `right` value.
- Returns: A `bool` result.

###### `M:IoT.DriverCore.OmronPlcRx.OmronSimulatorOperationRecord.op_Inequality(IoT.DriverCore.OmronPlcRx.OmronSimulatorOperationRecord,IoT.DriverCore.OmronPlcRx.OmronSimulatorOperationRecord)`

```csharp
public static bool op_Inequality(IoT.DriverCore.OmronPlcRx.OmronSimulatorOperationRecord left, IoT.DriverCore.OmronPlcRx.OmronSimulatorOperationRecord right)
```
Determines whether the two supplied values are not equal.

- Parameter `left`: The `left` value.
- Parameter `right`: The `right` value.
- Returns: A `bool` result.

###### `P:IoT.DriverCore.OmronPlcRx.OmronSimulatorOperationRecord.Operation`

```csharp
public IoT.DriverCore.OmronPlcRx.OmronSimulatorOperation Operation { get; set; }
```
Operation kind.

- Value: The `Operation` value.

###### `P:IoT.DriverCore.OmronPlcRx.OmronSimulatorOperationRecord.Sequence`

```csharp
public long Sequence { get; set; }
```
Monotonic operation sequence number.

- Value: The `Sequence` value.

###### `P:IoT.DriverCore.OmronPlcRx.OmronSimulatorOperationRecord.Succeeded`

```csharp
public bool Succeeded { get; set; }
```
Whether the operation succeeded.

- Value: The `Succeeded` value.

###### `P:IoT.DriverCore.OmronPlcRx.OmronSimulatorOperationRecord.TagName`

```csharp
public string TagName { get; set; }
```
Optional logical tag name.

- Value: The `TagName` value.

###### `P:IoT.DriverCore.OmronPlcRx.OmronSimulatorOperationRecord.Value`

```csharp
public object Value { get; set; }
```
Optional operation value.

- Value: The `Value` value.

#### `T:IoT.DriverCore.OmronPlcRx.PlcTagAttribute`

```csharp
public class IoT.DriverCore.OmronPlcRx.PlcTagAttribute
```
Marks a field or property for PLC reactive stream source generation.

##### Declared public members

###### `M:IoT.DriverCore.OmronPlcRx.PlcTagAttribute.#ctor(System.String)`

```csharp
public IoT.DriverCore.OmronPlcRx.PlcTagAttribute(string address)
```
Marks a field or property for PLC reactive stream source generation.

- Parameter `address`: The a dd re ss value.

###### `P:IoT.DriverCore.OmronPlcRx.PlcTagAttribute.Address`

```csharp
public string Address { get; }
```
Gets the address value.

- Value: The `Address` value.

###### `P:IoT.DriverCore.OmronPlcRx.PlcTagAttribute.Observe`

```csharp
public bool Observe { get; set; }
```
Gets or sets the observe value.

- Value: The `Observe` value.

###### `P:IoT.DriverCore.OmronPlcRx.PlcTagAttribute.Register`

```csharp
public bool Register { get; set; }
```
Gets or sets the register value.

- Value: The `Register` value.

###### `P:IoT.DriverCore.OmronPlcRx.PlcTagAttribute.TagName`

```csharp
public string TagName { get; set; }
```
Gets or sets the tag name value.

- Value: The `TagName` value.

###### `P:IoT.DriverCore.OmronPlcRx.PlcTagAttribute.Writable`

```csharp
public bool Writable { get; set; }
```
Gets or sets the writable value.

- Value: The `Writable` value.

#### `T:IoT.DriverCore.OmronPlcRx.PlcTagBindingAttribute`

```csharp
public class IoT.DriverCore.OmronPlcRx.PlcTagBindingAttribute
```
Marks a partial class as a generated PLC tag binding container.

##### Declared public members

###### `M:IoT.DriverCore.OmronPlcRx.PlcTagBindingAttribute.#ctor`

```csharp
public IoT.DriverCore.OmronPlcRx.PlcTagBindingAttribute()
```
Initializes a new instance of `IoT.DriverCore.OmronPlcRx.PlcTagBindingAttribute`.

#### `T:IoT.DriverCore.OmronPlcRx.Results.ReadBitsResult`

```csharp
public struct IoT.DriverCore.OmronPlcRx.Results.ReadBitsResult
```
Result of a Read Bits operation.

##### Declared public members

###### `M:IoT.DriverCore.OmronPlcRx.Results.ReadBitsResult.Equals(IoT.DriverCore.OmronPlcRx.Results.ReadBitsResult)`

```csharp
public bool Equals(IoT.DriverCore.OmronPlcRx.Results.ReadBitsResult other)
```
Determines whether the supplied value is equal to the current value.

- Parameter `other`: The `other` value.
- Returns: A `bool` result.

###### `M:IoT.DriverCore.OmronPlcRx.Results.ReadBitsResult.Equals(System.Object)`

```csharp
public bool Equals(object obj)
```
Determines whether the supplied value is equal to the current value.

- Parameter `obj`: The `obj` value.
- Returns: A `bool` result.

###### `M:IoT.DriverCore.OmronPlcRx.Results.ReadBitsResult.GetHashCode`

```csharp
public int GetHashCode()
```
Returns the hash code for the current value.

- Returns: A `int` result.

###### `M:IoT.DriverCore.OmronPlcRx.Results.ReadBitsResult.ToString`

```csharp
public string ToString()
```
Returns a string representation of the current value.

- Returns: A `string` result.

###### `M:IoT.DriverCore.OmronPlcRx.Results.ReadBitsResult.op_Equality(IoT.DriverCore.OmronPlcRx.Results.ReadBitsResult,IoT.DriverCore.OmronPlcRx.Results.ReadBitsResult)`

```csharp
public static bool op_Equality(IoT.DriverCore.OmronPlcRx.Results.ReadBitsResult left, IoT.DriverCore.OmronPlcRx.Results.ReadBitsResult right)
```
Determines whether the two supplied values are equal.

- Parameter `left`: The `left` value.
- Parameter `right`: The `right` value.
- Returns: A `bool` result.

###### `M:IoT.DriverCore.OmronPlcRx.Results.ReadBitsResult.op_Inequality(IoT.DriverCore.OmronPlcRx.Results.ReadBitsResult,IoT.DriverCore.OmronPlcRx.Results.ReadBitsResult)`

```csharp
public static bool op_Inequality(IoT.DriverCore.OmronPlcRx.Results.ReadBitsResult left, IoT.DriverCore.OmronPlcRx.Results.ReadBitsResult right)
```
Determines whether the two supplied values are not equal.

- Parameter `left`: The `left` value.
- Parameter `right`: The `right` value.
- Returns: A `bool` result.

###### `P:IoT.DriverCore.OmronPlcRx.Results.ReadBitsResult.BytesReceived`

```csharp
public int BytesReceived { get; set; }
```
Gets or sets the bytes received value.

- Value: The `BytesReceived` value.

###### `P:IoT.DriverCore.OmronPlcRx.Results.ReadBitsResult.BytesSent`

```csharp
public int BytesSent { get; set; }
```
Gets or sets the bytes sent value.

- Value: The `BytesSent` value.

###### `P:IoT.DriverCore.OmronPlcRx.Results.ReadBitsResult.Duration`

```csharp
public double Duration { get; set; }
```
Gets or sets the duration value.

- Value: The `Duration` value.

###### `P:IoT.DriverCore.OmronPlcRx.Results.ReadBitsResult.PacketsReceived`

```csharp
public int PacketsReceived { get; set; }
```
Gets or sets the packets received value.

- Value: The `PacketsReceived` value.

###### `P:IoT.DriverCore.OmronPlcRx.Results.ReadBitsResult.PacketsSent`

```csharp
public int PacketsSent { get; set; }
```
Gets or sets the packets sent value.

- Value: The `PacketsSent` value.

###### `P:IoT.DriverCore.OmronPlcRx.Results.ReadBitsResult.Values`

```csharp
public bool[] Values { get; set; }
```
Gets or sets the values value.

- Value: The `Values` value.

#### `T:IoT.DriverCore.OmronPlcRx.Results.ReadClockResult`

```csharp
public struct IoT.DriverCore.OmronPlcRx.Results.ReadClockResult
```
Result of a Read Clock operation.

##### Declared public members

###### `M:IoT.DriverCore.OmronPlcRx.Results.ReadClockResult.Equals(IoT.DriverCore.OmronPlcRx.Results.ReadClockResult)`

```csharp
public bool Equals(IoT.DriverCore.OmronPlcRx.Results.ReadClockResult other)
```
Determines whether the supplied value is equal to the current value.

- Parameter `other`: The `other` value.
- Returns: A `bool` result.

###### `M:IoT.DriverCore.OmronPlcRx.Results.ReadClockResult.Equals(System.Object)`

```csharp
public bool Equals(object obj)
```
Determines whether the supplied value is equal to the current value.

- Parameter `obj`: The `obj` value.
- Returns: A `bool` result.

###### `M:IoT.DriverCore.OmronPlcRx.Results.ReadClockResult.GetHashCode`

```csharp
public int GetHashCode()
```
Returns the hash code for the current value.

- Returns: A `int` result.

###### `M:IoT.DriverCore.OmronPlcRx.Results.ReadClockResult.ToString`

```csharp
public string ToString()
```
Returns a string representation of the current value.

- Returns: A `string` result.

###### `M:IoT.DriverCore.OmronPlcRx.Results.ReadClockResult.op_Equality(IoT.DriverCore.OmronPlcRx.Results.ReadClockResult,IoT.DriverCore.OmronPlcRx.Results.ReadClockResult)`

```csharp
public static bool op_Equality(IoT.DriverCore.OmronPlcRx.Results.ReadClockResult left, IoT.DriverCore.OmronPlcRx.Results.ReadClockResult right)
```
Determines whether the two supplied values are equal.

- Parameter `left`: The `left` value.
- Parameter `right`: The `right` value.
- Returns: A `bool` result.

###### `M:IoT.DriverCore.OmronPlcRx.Results.ReadClockResult.op_Inequality(IoT.DriverCore.OmronPlcRx.Results.ReadClockResult,IoT.DriverCore.OmronPlcRx.Results.ReadClockResult)`

```csharp
public static bool op_Inequality(IoT.DriverCore.OmronPlcRx.Results.ReadClockResult left, IoT.DriverCore.OmronPlcRx.Results.ReadClockResult right)
```
Determines whether the two supplied values are not equal.

- Parameter `left`: The `left` value.
- Parameter `right`: The `right` value.
- Returns: A `bool` result.

###### `P:IoT.DriverCore.OmronPlcRx.Results.ReadClockResult.BytesReceived`

```csharp
public int BytesReceived { get; set; }
```
Gets or sets the bytes received value.

- Value: The `BytesReceived` value.

###### `P:IoT.DriverCore.OmronPlcRx.Results.ReadClockResult.BytesSent`

```csharp
public int BytesSent { get; set; }
```
Gets or sets the bytes sent value.

- Value: The `BytesSent` value.

###### `P:IoT.DriverCore.OmronPlcRx.Results.ReadClockResult.Clock`

```csharp
public System.DateTimeOffset Clock { get; set; }
```
Gets or sets the clock value.

- Value: The `Clock` value.

###### `P:IoT.DriverCore.OmronPlcRx.Results.ReadClockResult.DayOfWeek`

```csharp
public int DayOfWeek { get; set; }
```
Gets or sets the day of week value.

- Value: The `DayOfWeek` value.

###### `P:IoT.DriverCore.OmronPlcRx.Results.ReadClockResult.Duration`

```csharp
public double Duration { get; set; }
```
Gets or sets the duration value.

- Value: The `Duration` value.

###### `P:IoT.DriverCore.OmronPlcRx.Results.ReadClockResult.PacketsReceived`

```csharp
public int PacketsReceived { get; set; }
```
Gets or sets the packets received value.

- Value: The `PacketsReceived` value.

###### `P:IoT.DriverCore.OmronPlcRx.Results.ReadClockResult.PacketsSent`

```csharp
public int PacketsSent { get; set; }
```
Gets or sets the packets sent value.

- Value: The `PacketsSent` value.

#### `T:IoT.DriverCore.OmronPlcRx.Results.ReadCycleTimeResult`

```csharp
public struct IoT.DriverCore.OmronPlcRx.Results.ReadCycleTimeResult
```
Result of a Read Cycle Time operation.

##### Declared public members

###### `M:IoT.DriverCore.OmronPlcRx.Results.ReadCycleTimeResult.Equals(IoT.DriverCore.OmronPlcRx.Results.ReadCycleTimeResult)`

```csharp
public bool Equals(IoT.DriverCore.OmronPlcRx.Results.ReadCycleTimeResult other)
```
Determines whether the supplied value is equal to the current value.

- Parameter `other`: The `other` value.
- Returns: A `bool` result.

###### `M:IoT.DriverCore.OmronPlcRx.Results.ReadCycleTimeResult.Equals(System.Object)`

```csharp
public bool Equals(object obj)
```
Determines whether the supplied value is equal to the current value.

- Parameter `obj`: The `obj` value.
- Returns: A `bool` result.

###### `M:IoT.DriverCore.OmronPlcRx.Results.ReadCycleTimeResult.GetHashCode`

```csharp
public int GetHashCode()
```
Returns the hash code for the current value.

- Returns: A `int` result.

###### `M:IoT.DriverCore.OmronPlcRx.Results.ReadCycleTimeResult.ToString`

```csharp
public string ToString()
```
Returns a string representation of the current value.

- Returns: A `string` result.

###### `M:IoT.DriverCore.OmronPlcRx.Results.ReadCycleTimeResult.op_Equality(IoT.DriverCore.OmronPlcRx.Results.ReadCycleTimeResult,IoT.DriverCore.OmronPlcRx.Results.ReadCycleTimeResult)`

```csharp
public static bool op_Equality(IoT.DriverCore.OmronPlcRx.Results.ReadCycleTimeResult left, IoT.DriverCore.OmronPlcRx.Results.ReadCycleTimeResult right)
```
Determines whether the two supplied values are equal.

- Parameter `left`: The `left` value.
- Parameter `right`: The `right` value.
- Returns: A `bool` result.

###### `M:IoT.DriverCore.OmronPlcRx.Results.ReadCycleTimeResult.op_Inequality(IoT.DriverCore.OmronPlcRx.Results.ReadCycleTimeResult,IoT.DriverCore.OmronPlcRx.Results.ReadCycleTimeResult)`

```csharp
public static bool op_Inequality(IoT.DriverCore.OmronPlcRx.Results.ReadCycleTimeResult left, IoT.DriverCore.OmronPlcRx.Results.ReadCycleTimeResult right)
```
Determines whether the two supplied values are not equal.

- Parameter `left`: The `left` value.
- Parameter `right`: The `right` value.
- Returns: A `bool` result.

###### `P:IoT.DriverCore.OmronPlcRx.Results.ReadCycleTimeResult.AverageCycleTime`

```csharp
public double AverageCycleTime { get; set; }
```
Gets or sets the average cycle time value.

- Value: The `AverageCycleTime` value.

###### `P:IoT.DriverCore.OmronPlcRx.Results.ReadCycleTimeResult.BytesReceived`

```csharp
public int BytesReceived { get; set; }
```
Gets or sets the bytes received value.

- Value: The `BytesReceived` value.

###### `P:IoT.DriverCore.OmronPlcRx.Results.ReadCycleTimeResult.BytesSent`

```csharp
public int BytesSent { get; set; }
```
Gets or sets the bytes sent value.

- Value: The `BytesSent` value.

###### `P:IoT.DriverCore.OmronPlcRx.Results.ReadCycleTimeResult.Duration`

```csharp
public double Duration { get; set; }
```
Gets or sets the duration value.

- Value: The `Duration` value.

###### `P:IoT.DriverCore.OmronPlcRx.Results.ReadCycleTimeResult.MaximumCycleTime`

```csharp
public double MaximumCycleTime { get; set; }
```
Gets or sets the maximum cycle time value.

- Value: The `MaximumCycleTime` value.

###### `P:IoT.DriverCore.OmronPlcRx.Results.ReadCycleTimeResult.MinimumCycleTime`

```csharp
public double MinimumCycleTime { get; set; }
```
Gets or sets the minimum cycle time value.

- Value: The `MinimumCycleTime` value.

###### `P:IoT.DriverCore.OmronPlcRx.Results.ReadCycleTimeResult.PacketsReceived`

```csharp
public int PacketsReceived { get; set; }
```
Gets or sets the packets received value.

- Value: The `PacketsReceived` value.

###### `P:IoT.DriverCore.OmronPlcRx.Results.ReadCycleTimeResult.PacketsSent`

```csharp
public int PacketsSent { get; set; }
```
Gets or sets the packets sent value.

- Value: The `PacketsSent` value.

#### `T:IoT.DriverCore.OmronPlcRx.Results.ReadWordsResult`

```csharp
public struct IoT.DriverCore.OmronPlcRx.Results.ReadWordsResult
```
Result of a Read Words operation.

##### Declared public members

###### `M:IoT.DriverCore.OmronPlcRx.Results.ReadWordsResult.Equals(IoT.DriverCore.OmronPlcRx.Results.ReadWordsResult)`

```csharp
public bool Equals(IoT.DriverCore.OmronPlcRx.Results.ReadWordsResult other)
```
Determines whether the supplied value is equal to the current value.

- Parameter `other`: The `other` value.
- Returns: A `bool` result.

###### `M:IoT.DriverCore.OmronPlcRx.Results.ReadWordsResult.Equals(System.Object)`

```csharp
public bool Equals(object obj)
```
Determines whether the supplied value is equal to the current value.

- Parameter `obj`: The `obj` value.
- Returns: A `bool` result.

###### `M:IoT.DriverCore.OmronPlcRx.Results.ReadWordsResult.GetHashCode`

```csharp
public int GetHashCode()
```
Returns the hash code for the current value.

- Returns: A `int` result.

###### `M:IoT.DriverCore.OmronPlcRx.Results.ReadWordsResult.ToString`

```csharp
public string ToString()
```
Returns a string representation of the current value.

- Returns: A `string` result.

###### `M:IoT.DriverCore.OmronPlcRx.Results.ReadWordsResult.op_Equality(IoT.DriverCore.OmronPlcRx.Results.ReadWordsResult,IoT.DriverCore.OmronPlcRx.Results.ReadWordsResult)`

```csharp
public static bool op_Equality(IoT.DriverCore.OmronPlcRx.Results.ReadWordsResult left, IoT.DriverCore.OmronPlcRx.Results.ReadWordsResult right)
```
Determines whether the two supplied values are equal.

- Parameter `left`: The `left` value.
- Parameter `right`: The `right` value.
- Returns: A `bool` result.

###### `M:IoT.DriverCore.OmronPlcRx.Results.ReadWordsResult.op_Inequality(IoT.DriverCore.OmronPlcRx.Results.ReadWordsResult,IoT.DriverCore.OmronPlcRx.Results.ReadWordsResult)`

```csharp
public static bool op_Inequality(IoT.DriverCore.OmronPlcRx.Results.ReadWordsResult left, IoT.DriverCore.OmronPlcRx.Results.ReadWordsResult right)
```
Determines whether the two supplied values are not equal.

- Parameter `left`: The `left` value.
- Parameter `right`: The `right` value.
- Returns: A `bool` result.

###### `P:IoT.DriverCore.OmronPlcRx.Results.ReadWordsResult.BytesReceived`

```csharp
public int BytesReceived { get; set; }
```
Gets or sets the bytes received value.

- Value: The `BytesReceived` value.

###### `P:IoT.DriverCore.OmronPlcRx.Results.ReadWordsResult.BytesSent`

```csharp
public int BytesSent { get; set; }
```
Gets or sets the bytes sent value.

- Value: The `BytesSent` value.

###### `P:IoT.DriverCore.OmronPlcRx.Results.ReadWordsResult.Duration`

```csharp
public double Duration { get; set; }
```
Gets or sets the duration value.

- Value: The `Duration` value.

###### `P:IoT.DriverCore.OmronPlcRx.Results.ReadWordsResult.PacketsReceived`

```csharp
public int PacketsReceived { get; set; }
```
Gets or sets the packets received value.

- Value: The `PacketsReceived` value.

###### `P:IoT.DriverCore.OmronPlcRx.Results.ReadWordsResult.PacketsSent`

```csharp
public int PacketsSent { get; set; }
```
Gets or sets the packets sent value.

- Value: The `PacketsSent` value.

###### `P:IoT.DriverCore.OmronPlcRx.Results.ReadWordsResult.Values`

```csharp
public short[] Values { get; set; }
```
Gets or sets the values value.

- Value: The `Values` value.

#### `T:IoT.DriverCore.OmronPlcRx.Results.WriteBitsResult`

```csharp
public struct IoT.DriverCore.OmronPlcRx.Results.WriteBitsResult
```
Result of a Write Bits operation.

##### Declared public members

###### `M:IoT.DriverCore.OmronPlcRx.Results.WriteBitsResult.Equals(IoT.DriverCore.OmronPlcRx.Results.WriteBitsResult)`

```csharp
public bool Equals(IoT.DriverCore.OmronPlcRx.Results.WriteBitsResult other)
```
Determines whether the supplied value is equal to the current value.

- Parameter `other`: The `other` value.
- Returns: A `bool` result.

###### `M:IoT.DriverCore.OmronPlcRx.Results.WriteBitsResult.Equals(System.Object)`

```csharp
public bool Equals(object obj)
```
Determines whether the supplied value is equal to the current value.

- Parameter `obj`: The `obj` value.
- Returns: A `bool` result.

###### `M:IoT.DriverCore.OmronPlcRx.Results.WriteBitsResult.GetHashCode`

```csharp
public int GetHashCode()
```
Returns the hash code for the current value.

- Returns: A `int` result.

###### `M:IoT.DriverCore.OmronPlcRx.Results.WriteBitsResult.ToString`

```csharp
public string ToString()
```
Returns a string representation of the current value.

- Returns: A `string` result.

###### `M:IoT.DriverCore.OmronPlcRx.Results.WriteBitsResult.op_Equality(IoT.DriverCore.OmronPlcRx.Results.WriteBitsResult,IoT.DriverCore.OmronPlcRx.Results.WriteBitsResult)`

```csharp
public static bool op_Equality(IoT.DriverCore.OmronPlcRx.Results.WriteBitsResult left, IoT.DriverCore.OmronPlcRx.Results.WriteBitsResult right)
```
Determines whether the two supplied values are equal.

- Parameter `left`: The `left` value.
- Parameter `right`: The `right` value.
- Returns: A `bool` result.

###### `M:IoT.DriverCore.OmronPlcRx.Results.WriteBitsResult.op_Inequality(IoT.DriverCore.OmronPlcRx.Results.WriteBitsResult,IoT.DriverCore.OmronPlcRx.Results.WriteBitsResult)`

```csharp
public static bool op_Inequality(IoT.DriverCore.OmronPlcRx.Results.WriteBitsResult left, IoT.DriverCore.OmronPlcRx.Results.WriteBitsResult right)
```
Determines whether the two supplied values are not equal.

- Parameter `left`: The `left` value.
- Parameter `right`: The `right` value.
- Returns: A `bool` result.

###### `P:IoT.DriverCore.OmronPlcRx.Results.WriteBitsResult.BytesReceived`

```csharp
public int BytesReceived { get; set; }
```
Gets or sets the bytes received value.

- Value: The `BytesReceived` value.

###### `P:IoT.DriverCore.OmronPlcRx.Results.WriteBitsResult.BytesSent`

```csharp
public int BytesSent { get; set; }
```
Gets or sets the bytes sent value.

- Value: The `BytesSent` value.

###### `P:IoT.DriverCore.OmronPlcRx.Results.WriteBitsResult.Duration`

```csharp
public double Duration { get; set; }
```
Gets or sets the duration value.

- Value: The `Duration` value.

###### `P:IoT.DriverCore.OmronPlcRx.Results.WriteBitsResult.PacketsReceived`

```csharp
public int PacketsReceived { get; set; }
```
Gets or sets the packets received value.

- Value: The `PacketsReceived` value.

###### `P:IoT.DriverCore.OmronPlcRx.Results.WriteBitsResult.PacketsSent`

```csharp
public int PacketsSent { get; set; }
```
Gets or sets the packets sent value.

- Value: The `PacketsSent` value.

#### `T:IoT.DriverCore.OmronPlcRx.Results.WriteClockResult`

```csharp
public struct IoT.DriverCore.OmronPlcRx.Results.WriteClockResult
```
Result of a Write Clock operation.

##### Declared public members

###### `M:IoT.DriverCore.OmronPlcRx.Results.WriteClockResult.Equals(IoT.DriverCore.OmronPlcRx.Results.WriteClockResult)`

```csharp
public bool Equals(IoT.DriverCore.OmronPlcRx.Results.WriteClockResult other)
```
Determines whether the supplied value is equal to the current value.

- Parameter `other`: The `other` value.
- Returns: A `bool` result.

###### `M:IoT.DriverCore.OmronPlcRx.Results.WriteClockResult.Equals(System.Object)`

```csharp
public bool Equals(object obj)
```
Determines whether the supplied value is equal to the current value.

- Parameter `obj`: The `obj` value.
- Returns: A `bool` result.

###### `M:IoT.DriverCore.OmronPlcRx.Results.WriteClockResult.GetHashCode`

```csharp
public int GetHashCode()
```
Returns the hash code for the current value.

- Returns: A `int` result.

###### `M:IoT.DriverCore.OmronPlcRx.Results.WriteClockResult.ToString`

```csharp
public string ToString()
```
Returns a string representation of the current value.

- Returns: A `string` result.

###### `M:IoT.DriverCore.OmronPlcRx.Results.WriteClockResult.op_Equality(IoT.DriverCore.OmronPlcRx.Results.WriteClockResult,IoT.DriverCore.OmronPlcRx.Results.WriteClockResult)`

```csharp
public static bool op_Equality(IoT.DriverCore.OmronPlcRx.Results.WriteClockResult left, IoT.DriverCore.OmronPlcRx.Results.WriteClockResult right)
```
Determines whether the two supplied values are equal.

- Parameter `left`: The `left` value.
- Parameter `right`: The `right` value.
- Returns: A `bool` result.

###### `M:IoT.DriverCore.OmronPlcRx.Results.WriteClockResult.op_Inequality(IoT.DriverCore.OmronPlcRx.Results.WriteClockResult,IoT.DriverCore.OmronPlcRx.Results.WriteClockResult)`

```csharp
public static bool op_Inequality(IoT.DriverCore.OmronPlcRx.Results.WriteClockResult left, IoT.DriverCore.OmronPlcRx.Results.WriteClockResult right)
```
Determines whether the two supplied values are not equal.

- Parameter `left`: The `left` value.
- Parameter `right`: The `right` value.
- Returns: A `bool` result.

###### `P:IoT.DriverCore.OmronPlcRx.Results.WriteClockResult.BytesReceived`

```csharp
public int BytesReceived { get; set; }
```
Gets or sets the bytes received value.

- Value: The `BytesReceived` value.

###### `P:IoT.DriverCore.OmronPlcRx.Results.WriteClockResult.BytesSent`

```csharp
public int BytesSent { get; set; }
```
Gets or sets the bytes sent value.

- Value: The `BytesSent` value.

###### `P:IoT.DriverCore.OmronPlcRx.Results.WriteClockResult.Duration`

```csharp
public double Duration { get; set; }
```
Gets or sets the duration value.

- Value: The `Duration` value.

###### `P:IoT.DriverCore.OmronPlcRx.Results.WriteClockResult.PacketsReceived`

```csharp
public int PacketsReceived { get; set; }
```
Gets or sets the packets received value.

- Value: The `PacketsReceived` value.

###### `P:IoT.DriverCore.OmronPlcRx.Results.WriteClockResult.PacketsSent`

```csharp
public int PacketsSent { get; set; }
```
Gets or sets the packets sent value.

- Value: The `PacketsSent` value.

#### `T:IoT.DriverCore.OmronPlcRx.Results.WriteWordsResult`

```csharp
public struct IoT.DriverCore.OmronPlcRx.Results.WriteWordsResult
```
Result of a Write Words operation.

##### Declared public members

###### `M:IoT.DriverCore.OmronPlcRx.Results.WriteWordsResult.Equals(IoT.DriverCore.OmronPlcRx.Results.WriteWordsResult)`

```csharp
public bool Equals(IoT.DriverCore.OmronPlcRx.Results.WriteWordsResult other)
```
Determines whether the supplied value is equal to the current value.

- Parameter `other`: The `other` value.
- Returns: A `bool` result.

###### `M:IoT.DriverCore.OmronPlcRx.Results.WriteWordsResult.Equals(System.Object)`

```csharp
public bool Equals(object obj)
```
Determines whether the supplied value is equal to the current value.

- Parameter `obj`: The `obj` value.
- Returns: A `bool` result.

###### `M:IoT.DriverCore.OmronPlcRx.Results.WriteWordsResult.GetHashCode`

```csharp
public int GetHashCode()
```
Returns the hash code for the current value.

- Returns: A `int` result.

###### `M:IoT.DriverCore.OmronPlcRx.Results.WriteWordsResult.ToString`

```csharp
public string ToString()
```
Returns a string representation of the current value.

- Returns: A `string` result.

###### `M:IoT.DriverCore.OmronPlcRx.Results.WriteWordsResult.op_Equality(IoT.DriverCore.OmronPlcRx.Results.WriteWordsResult,IoT.DriverCore.OmronPlcRx.Results.WriteWordsResult)`

```csharp
public static bool op_Equality(IoT.DriverCore.OmronPlcRx.Results.WriteWordsResult left, IoT.DriverCore.OmronPlcRx.Results.WriteWordsResult right)
```
Determines whether the two supplied values are equal.

- Parameter `left`: The `left` value.
- Parameter `right`: The `right` value.
- Returns: A `bool` result.

###### `M:IoT.DriverCore.OmronPlcRx.Results.WriteWordsResult.op_Inequality(IoT.DriverCore.OmronPlcRx.Results.WriteWordsResult,IoT.DriverCore.OmronPlcRx.Results.WriteWordsResult)`

```csharp
public static bool op_Inequality(IoT.DriverCore.OmronPlcRx.Results.WriteWordsResult left, IoT.DriverCore.OmronPlcRx.Results.WriteWordsResult right)
```
Determines whether the two supplied values are not equal.

- Parameter `left`: The `left` value.
- Parameter `right`: The `right` value.
- Returns: A `bool` result.

###### `P:IoT.DriverCore.OmronPlcRx.Results.WriteWordsResult.BytesReceived`

```csharp
public int BytesReceived { get; set; }
```
Gets or sets the bytes received value.

- Value: The `BytesReceived` value.

###### `P:IoT.DriverCore.OmronPlcRx.Results.WriteWordsResult.BytesSent`

```csharp
public int BytesSent { get; set; }
```
Gets or sets the bytes sent value.

- Value: The `BytesSent` value.

###### `P:IoT.DriverCore.OmronPlcRx.Results.WriteWordsResult.Duration`

```csharp
public double Duration { get; set; }
```
Gets or sets the duration value.

- Value: The `Duration` value.

###### `P:IoT.DriverCore.OmronPlcRx.Results.WriteWordsResult.PacketsReceived`

```csharp
public int PacketsReceived { get; set; }
```
Gets or sets the packets received value.

- Value: The `PacketsReceived` value.

###### `P:IoT.DriverCore.OmronPlcRx.Results.WriteWordsResult.PacketsSent`

```csharp
public int PacketsSent { get; set; }
```
Gets or sets the packets sent value.

- Value: The `PacketsSent` value.

#### `T:IoT.DriverCore.OmronPlcRx.Tags.IPlcTag`

```csharp
public interface IoT.DriverCore.OmronPlcRx.Tags.IPlcTag
```
Defines metadata and value access for a PLC tag.

##### Declared public members

###### `P:IoT.DriverCore.OmronPlcRx.Tags.IPlcTag.Address`

```csharp
public string Address { get; }
```
Gets the address.

- Value: The address.

###### `P:IoT.DriverCore.OmronPlcRx.Tags.IPlcTag.TagName`

```csharp
public string TagName { get; }
```
Gets the name of the tag.

- Value: The name of the tag.

###### `P:IoT.DriverCore.OmronPlcRx.Tags.IPlcTag.TagType`

```csharp
public System.Type TagType { get; }
```
Gets a value indicating whether this instance is bit address.

- Value: true if this instance is bit address; otherwise, false .

###### `P:IoT.DriverCore.OmronPlcRx.Tags.IPlcTag.Value`

```csharp
public object Value { get; }
```
Gets the value.

- Value: The value.

#### `T:IoT.DriverCore.OmronPlcRx.Tags.PlcTag`1`

```csharp
public class IoT.DriverCore.OmronPlcRx.Tags.PlcTag`1
```
Represents a typed PLC tag binding.

##### Declared public members

###### `M:IoT.DriverCore.OmronPlcRx.Tags.PlcTag`1.#ctor(System.String,System.String)`

```csharp
public IoT.DriverCore.OmronPlcRx.Tags.PlcTag<T>(string tagName, string address)
```
Represents a typed PLC tag binding.

- Parameter `tagName`: The tag Name.
- Parameter `address`: The address.

###### `P:IoT.DriverCore.OmronPlcRx.Tags.PlcTag`1.Address`

```csharp
public string Address { get; }
```
Gets the address.

- Value: The address.

###### `P:IoT.DriverCore.OmronPlcRx.Tags.PlcTag`1.TagName`

```csharp
public string TagName { get; }
```
Gets the Tag Name.

- Value: The name.

###### `P:IoT.DriverCore.OmronPlcRx.Tags.PlcTag`1.TagType`

```csharp
public System.Type TagType { get; }
```
Gets a value indicating whether this instance is bit address.

- Value: true if this instance is bit address; otherwise, false .

###### `P:IoT.DriverCore.OmronPlcRx.Tags.PlcTag`1.Value`

```csharp
public T Value { get; }
```
Gets the tag value.

- Value: The value.

#### `T:IoT.DriverCore.OmronPlcRx.ToolbusFinsFrameCodec`

```csharp
public class IoT.DriverCore.OmronPlcRx.ToolbusFinsFrameCodec
```
Encodes and decodes Omron Toolbus serial frames carrying binary FINS messages.

##### Declared public members

###### `M:IoT.DriverCore.OmronPlcRx.ToolbusFinsFrameCodec.CalculateChecksum(System.ReadOnlySpan`1{System.Byte})`

```csharp
public static ushort CalculateChecksum(System.ReadOnlySpan<byte> data)
```
Executes the `CalculateChecksum` operation.

- Parameter `data`: The `data` value.
- Returns: A `ushort` result.

###### `M:IoT.DriverCore.OmronPlcRx.ToolbusFinsFrameCodec.DecodeResponse(System.ReadOnlyMemory`1{System.Byte})`

```csharp
public static System.Memory<byte> DecodeResponse(System.ReadOnlyMemory<byte> frame)
```
Executes the `DecodeResponse` operation.

- Parameter `frame`: The `frame` value.
- Returns: A `System.Memory<byte>` result.

###### `M:IoT.DriverCore.OmronPlcRx.ToolbusFinsFrameCodec.EncodeRequest(System.ReadOnlyMemory`1{System.Byte})`

```csharp
public static System.Memory<byte> EncodeRequest(System.ReadOnlyMemory<byte> finsMessage)
```
Executes the `EncodeRequest` operation.

- Parameter `finsMessage`: The `finsMessage` value.
- Returns: A `System.Memory<byte>` result.

###### `P:IoT.DriverCore.OmronPlcRx.ToolbusFinsFrameCodec.SynchronizationFrame`

```csharp
public System.ReadOnlyMemory<byte> SynchronizationFrame { get; }
```
Gets the synchronization frame value.

- Value: The `SynchronizationFrame` value.

<!-- END GENERATED PUBLIC API -->
