# OmronPlcRx

<div align="center">
  <img src="Images/OmronPLCRx-icon.png" style="width:25%;" />
</div>

OmronPlcRx is a reactive Omron PLC communications library for .NET Framework and modern .NET. It targets `net462`, `net472`, `net481`, `net8.0`, `net9.0`, `net10.0`, and `net11.0`.

The library provides high-level, strongly typed PLC tag access over Omron FINS using TCP, UDP, serial Host Link FINS, or serial Toolbus FINS. 

V2 is a breaking release: the core reactive implementation now uses `ReactiveUI.Primitives`, and Rx/System.Reactive-first applications should use the new `OmronPlcRx.Reactive` package surface.

## Contents

- [V2 Breaking Changes](#v2-breaking-changes)
- [Packages](#packages)
- [Installation](#installation)
- [Quick Start](#quick-start)
- [Rx Applications With OmronPlcRx.Reactive](#rx-applications-with-omronplcrxreactive)
- [R3 Bridge](#r3-bridge)
- [Addressing Guide](#addressing-guide)
- [Supported Types And Encoding](#supported-types-and-encoding)
- [Source Generators](#source-generators)
- [Full API Reference](#full-api-reference)
- [Error Handling](#error-handling)
- [Testing](#testing)
- [FAQ](#faq)
- [License](#license)

## V2 Breaking Changes

V2 changes the reactive foundation and package layout.

- `OmronPlcRx` now depends on `ReactiveUI.Primitives`, `ReactiveUI.Primitives.Async`, and `SerialPortRx`.
- `OmronPlcRx.Reactive` is a separate package for System.Reactive/Rx-based applications. It depends on `ReactiveUI.Primitives.Reactive`, `ReactiveUI.Primitives.Async.Reactive`, and `SerialPortRx.Reactive`.
- `OmronPlcRx.Reactive` uses `OmronPlcRx.Reactive.*` namespaces. For example, `OmronPlcRx.IOmronPlcRx` becomes `OmronPlcRx.Reactive.IOmronPlcRx`.
- Both package surfaces are built from shared source, but the reactive shim compiles with `REACTIVE_SHIM` and has its own namespaces, result types, enum types, tag types, and BCD wrapper types.
- Source-generated async observable members are generated only for `net8.0` and later.
- `Value<T>(tagName, value)` remains fire-and-forget. Write failures are delivered through `Errors`; they are not thrown synchronously by the setter call.

Use `OmronPlcRx` for new applications that are comfortable with `ReactiveUI.Primitives` and BCL `IObservable<T>`. Use `OmronPlcRx.Reactive` when an existing application already uses System.Reactive operators, schedulers, and package conventions.

## Packages

| Package | Primary namespaces | Use when |
| --- | --- | --- |
| `OmronPlcRx` | `OmronPlcRx`, `OmronPlcRx.Enums`, `OmronPlcRx.Tags`, `OmronPlcRx.Results`, `OmronPlcRx.Async` | New or lean applications using `ReactiveUI.Primitives`. |
| `OmronPlcRx.Reactive` | `OmronPlcRx.Reactive`, `OmronPlcRx.Reactive.Enums`, `OmronPlcRx.Reactive.Tags`, `OmronPlcRx.Reactive.Results`, `OmronPlcRx.Reactive.Async` | System.Reactive/Rx-first applications. |
| `OmronPlcRx.SourceGenerators` | `OmronPlcRx.SourceGenerators` | Analyzer implementation used by the runtime packages to generate `[PlcTag]` members. Most consumers get it through `OmronPlcRx` or `OmronPlcRx.Reactive`. |

## Installation

Base package:

```bash
dotnet add package OmronPlcRx
```

Rx/System.Reactive package:

```bash
dotnet add package OmronPlcRx.Reactive
```

Package Manager:

```powershell
Install-Package OmronPlcRx
Install-Package OmronPlcRx.Reactive
```

## Quick Start

```csharp
using OmronPlcRx;
using OmronPlcRx.Core.Types;
using OmronPlcRx.Enums;
using ReactiveUI.Primitives;

using var plc = new OmronPlcRx.OmronPlcRx(
    localNodeId: 11,
    remoteNodeId: 1,
    connectionMethod: ConnectionMethod.UDP,
    remoteHost: "192.168.250.1",
    port: 9600,
    timeout: 2000,
    retries: 1,
    pollInterval: TimeSpan.FromMilliseconds(200));

plc.AddUpdateTagItem<bool>("D100.0", "MotorRun");
plc.AddUpdateTagItem<short>("D200", "TemperatureRaw");
plc.AddUpdateTagItem<int>("D300", "BatchCounter");
plc.AddUpdateTagItem<float>("D400", "TankLevel");
plc.AddUpdateTagItem<double>("D500", "TotalizedFlow");
plc.AddUpdateTagItem<string>("D600[20]", "LineName");
plc.AddUpdateTagItem<Bcd16>("D700", "BcdTemperature");

using var motorSubscription = plc.Observe<bool>("MotorRun")
    .DistinctUntilChanged()
    .SubscribeSafe(
        value => Console.WriteLine($"MotorRun -> {value}"),
        error => Console.WriteLine(error.Message));

using var allTagsSubscription = plc.ObserveAll.SubscribeSafe(
    tag => Console.WriteLine($"{tag?.TagName} = {tag?.Value}"),
    error => Console.WriteLine(error.Message));

using var errorSubscription = plc.Errors.SubscribeSafe(
    error => Console.WriteLine($"PLC error: {error?.Message}"),
    error => Console.WriteLine(error.Message));

plc.Value("MotorRun", true);
plc.Value("LineName", "Filling Line 1");
plc.Value("BcdTemperature", new Bcd16(235));

short? cachedTemperature = plc.Value<short>("TemperatureRaw");
Console.WriteLine($"Cached temperature = {cachedTemperature}");

var clock = await plc.ReadClockAsync();
Console.WriteLine($"PLC clock: {clock.Clock:o}");

var cycle = await plc.ReadCycleTimeAsync();
Console.WriteLine($"Cycle time min={cycle.MinimumCycleTime} max={cycle.MaximumCycleTime} avg={cycle.AverageCycleTime}");
```

## Serial Quick Start

Host Link FINS:

```csharp
using System.IO.Ports;
using OmronPlcRx;
using OmronPlcRx.Enums;
using ReactiveUI.Primitives;

using var plc = new OmronPlcRx.OmronPlcRx(
    localNodeId: 11,
    remoteNodeId: 0,
    serialOptions: new OmronSerialOptions("COM3")
    {
        BaudRate = 9600,
        DataBits = 7,
        Parity = Parity.Even,
        StopBits = StopBits.Two,
        Handshake = Handshake.None,
        HostLinkUnitNumber = 0,
        ResponseWaitTime = 0,
        FrameMode = OmronHostLinkFinsFrameMode.Direct,
    },
    timeout: 2000,
    retries: 1,
    pollInterval: TimeSpan.FromMilliseconds(200));

plc.AddUpdateTagItem<short>("D100", "LegacyValue");

using var subscription = plc.Observe<short>("LegacyValue")
    .SubscribeSafe(
        value => Console.WriteLine($"D100 -> {value}"),
        error => Console.WriteLine(error.Message));
```

Toolbus:

```csharp
using OmronPlcRx;
using ReactiveUI.Primitives;

using var plc = new OmronPlcRx.OmronPlcRx(
    localNodeId: 11,
    remoteNodeId: 0,
    serialOptions: OmronSerialOptions.CreateToolbus("COM3"),
    timeout: 2000,
    retries: 1,
    pollInterval: TimeSpan.FromMilliseconds(200));

plc.AddUpdateTagItem<short>("D100", "ToolbusValue");

using var subscription = plc.Observe<short>("ToolbusValue")
    .SubscribeSafe(
        value => Console.WriteLine($"D100 -> {value}"),
        error => Console.WriteLine(error.Message));
```

Serial notes:

- `OmronSerialOptions` defaults to Host Link FINS: `9600`, `7E2`, no handshake, direct frame mode, maximum frame length `1004`.
- `OmronSerialOptions.CreateToolbus` applies common Toolbus settings: `115200`, `8N1`, no handshake, RTS enabled, maximum frame length `1004`.
- `OmronHostLinkFinsFrameMode.Direct` uses direct CPU Host Link FINS framing.
- `OmronHostLinkFinsFrameMode.Network` carries the complete FINS header for routed Host Link scenarios.
- Classic C-mode Host Link commands are not implemented.

## Rx Applications With OmronPlcRx.Reactive

Use `OmronPlcRx.Reactive` when the consuming application is already based on System.Reactive and wants the Rx-oriented ReactiveUI.Primitives package variants.

```csharp
using OmronPlcRx.Reactive;
using OmronPlcRx.Reactive.Enums;
using OmronPlcRx.Reactive.Tags;
using System.Reactive.Linq;

using var plc = new OmronPlcRx.Reactive.OmronPlcRx(
    localNodeId: 11,
    remoteNodeId: 1,
    connectionMethod: ConnectionMethod.TCP,
    remoteHost: "192.168.250.1");

plc.AddUpdateTagItem<bool>("D100.0", "MotorRun");
plc.AddUpdateTagItem<short>("D200", "Speed");

using var motorSubscription = plc.Observe<bool>("MotorRun")
    .DistinctUntilChanged()
    .Subscribe(
        value => Console.WriteLine($"MotorRun -> {value}"),
        error => Console.WriteLine(error.Message));

using var allTagsSubscription = plc.ObserveAll
    .Where(static tag => tag is not null)
    .Subscribe(static tag => Console.WriteLine($"{tag!.TagName} = {tag.Value}"));

IObservable<IPlcTag?> allChanges = plc.ObserveAll;
IObservable<OmronPLCException?> errors = plc.Errors;
```

Namespace migration example:

```csharp
// Base package
using OmronPlcRx;
using OmronPlcRx.Enums;
using OmronPlcRx.Tags;

// Rx package
using OmronPlcRx.Reactive;
using OmronPlcRx.Reactive.Enums;
using OmronPlcRx.Reactive.Tags;
```

The API shape is intentionally the same: `AddUpdateTagItem<T>`, `Observe<T>`, `ObserveAll`, `Errors`, `Value<T>(tagName)`, `Value<T>(tagName, value)`, `ReadClockAsync`, `WriteClockAsync`, `ReadCycleTimeAsync`, and `Dispose`.

## R3 Bridge

ReactiveUI.Primitives can generate R3 bridge extension methods in the consuming project when the required R3 symbols are visible. Use the bridge at application boundaries and keep the rest of a pipeline in one reactive model.

Install the packages your app needs:

```bash
dotnet add package OmronPlcRx
dotnet add package ReactiveUI.Primitives
dotnet add package ReactiveUI.Primitives.Async
dotnet add package R3
```

Convert PLC `IObservable<T>` streams to R3:

```csharp
using OmronPlcRx;
using OmronPlcRx.Enums;
using ReactiveUI.Primitives.R3Bridge;

using var plc = new OmronPlcRx.OmronPlcRx(11, 1, ConnectionMethod.UDP, "192.168.250.1");
plc.AddUpdateTagItem<short>("D200", "Speed");

R3.Observable<short?> speedR3 = plc.Observe<short>("Speed").AsR3Observable();

using var subscription = speedR3.Subscribe(value => Console.WriteLine($"Speed -> {value}"));
```

Convert R3 streams back to ReactiveUI.Primitives:

```csharp
using ReactiveUI.Primitives.R3Bridge;

R3.Observable<bool> command = GetMotorCommandStream();
IObservable<bool> primitivesSignal = command.AsPrimitivesSignal();
```

Bridge async PLC streams:

```csharp
using OmronPlcRx.Async;
using ReactiveUI.Primitives.Async;
using ReactiveUI.Primitives.R3Bridge;

IObservableAsync<short?> speedAsync = plc.ObserveAsAsyncObservable<short>("Speed");
R3.Observable<short?> speedR3 = speedAsync.AsR3Observable();
IObservableAsync<short?> backToAsync = speedR3.AsPrimitivesAsyncObservable<short?>();
```

When using `R3Async`, the async bridge generator emits `AsPrimitivesAsyncObservable<T>(this R3Async.AsyncObservable<T>)` and `AsR3AsyncObservable<T>(this IObservableAsync<T>)` when the R3Async symbols and `ReactiveUI.Primitives.Async.IObservableAsync<T>` are visible.

## Addressing Guide

Supported memory area prefixes:

| Prefix | Memory area |
| --- | --- |
| `D`, `DM` | Data Memory |
| `C`, `CIO` | Common I/O |
| `W` | Work |
| `H` | Holding |
| `A` | Auxiliary, restricted on some PLC models |

Bit address syntax is `<Area><Word>.<Bit>`, where bit is `0` through `15`.

```csharp
plc.AddUpdateTagItem<bool>("D100.0", "MotorRun");
plc.AddUpdateTagItem<bool>("W50.7", "WorkFlag");
plc.AddUpdateTagItem<bool>("CIO200.15", "InputSignal");
```

Word address syntax is `<Area><Word>`.

```csharp
plc.AddUpdateTagItem<short>("D200", "TemperatureRaw");
plc.AddUpdateTagItem<ushort>("H10", "HoldingWord");
plc.AddUpdateTagItem<int>("CIO500", "Counter");
```

String address syntax is `<Area><Word>[<Length>]`, where length is character count. If the length suffix is omitted, the runtime uses `16` characters.

```csharp
plc.AddUpdateTagItem<string>("D600[20]", "LineName");
plc.Value("LineName", "Filling Line 1");
```

Strings are ASCII and packed as two characters per PLC word, high byte first. Short values are null padded.

## Supported Types And Encoding

| C# type | PLC storage | Notes |
| --- | --- | --- |
| `bool` | 1 bit or 1 word | Bit address reads/writes one bit. Word address treats non-zero as `true`. |
| `byte` | 1 word | Uses the low byte. |
| `short` | 1 word | Signed 16-bit word. |
| `ushort` | 1 word | Unsigned 16-bit word. |
| `int` | 2 words | Low word first in PLC memory, high word second. |
| `uint` | 2 words | Low word first in PLC memory, high word second. |
| `float` | 2 words | IEEE 754 single. |
| `double` | 4 words | IEEE 754 double. |
| `string` | N words | ASCII, two characters per word, optional `[length]` suffix. |
| `Bcd16` | 1 word | Signed packed BCD wrapper. |
| `BcdU16` | 1 word | Unsigned packed BCD wrapper. |
| `Bcd32` | 2 words | Signed packed BCD wrapper. |
| `BcdU32` | 2 words | Unsigned packed BCD wrapper. |

Example:

```csharp
plc.AddUpdateTagItem<uint>("D720", "UnsignedCounter");
plc.AddUpdateTagItem<double>("D500", "TotalizedFlow");
plc.AddUpdateTagItem<Bcd32>("D710", "BatchNumber");

plc.Value("UnsignedCounter", 1234u);
plc.Value("BatchNumber", new Bcd32(12345678));
```

## Source Generators

The runtime packages include the OmronPlcRx source generator as an analyzer. Add `[PlcTag]` to fields in a partial class to generate strongly typed properties, observable streams, registration helpers, binding helpers, write helpers, and partial hooks.

```csharp
using OmronPlcRx;
using OmronPlcRx.Core.Types;

public sealed partial class MachineState
{
    [PlcTag("D100", Writable = true)]
    private short _tankLevel;

    [PlcTag("D100.0", TagName = "PumpRun")]
    private bool _pumpRunning;

    [PlcTag("D600[20]")]
    private string _lineName = string.Empty;

    [PlcTag("D700")]
    private Bcd16 _bcdTemperature;

    partial void OnTankLevelReceived(short value)
    {
        Console.WriteLine($"Tank level changed to {value}");
    }
}
```

Generated members for the sample above:

```csharp
public short TankLevel { get; private set; }
public bool PumpRunning { get; private set; }
public string LineName { get; private set; }
public Bcd16 BcdTemperature { get; private set; }

public IObservable<short> TankLevelObservable { get; }
public IObservable<bool> PumpRunningObservable { get; }
public IObservable<string> LineNameObservable { get; }
public IObservable<Bcd16> BcdTemperatureObservable { get; }

// net8.0 and later only
public IObservableAsync<short> TankLevelObservableAsync { get; }

public void RegisterPlcTags(IOmronPlcRx plc);
public IDisposable BindPlcTags(IOmronPlcRx plc);
public void WriteTankLevel(IOmronPlcRx plc, short value);

partial void OnTankLevelReceived(short value);
partial void OnPumpRunningReceived(bool value);
partial void OnLineNameReceived(string value);
partial void OnBcdTemperatureReceived(Bcd16 value);
```

Use the generated API:

```csharp
using ReactiveUI.Primitives;

var state = new MachineState();

using var binding = state.BindPlcTags(plc);
using var tankSubscription = state.TankLevelObservable.SubscribeSafe(
    value => Console.WriteLine($"TankLevel -> {value}"),
    error => Console.WriteLine(error.Message));

state.WriteTankLevel(plc, 456);
```

Use generated async observable members on `net8.0+`:

```csharp
using ReactiveUI.Primitives.Async;

IObservableAsync<short> tankLevelAsync = state.TankLevelObservableAsync;
```

Source generator attribute API:

| Member | Description | Example |
| --- | --- | --- |
| `PlcTagAttribute(string address)` | Required PLC address. | `[PlcTag("D100.0")]` |
| `TagName` | Optional logical PLC tag name. Defaults to generated property name. | `[PlcTag("D100.0", TagName = "PumpRun")]` |
| `Register` | When `true`, `RegisterPlcTags` calls `AddUpdateTagItem<T>`. Default is `true`. | `[PlcTag("D100", Register = false)]` |
| `Observe` | When `true`, `BindPlcTags` subscribes to `plc.Observe<T>`. Default is `true`. | `[PlcTag("D100", Observe = false)]` |
| `Writable` | When `true`, the generator emits `Write<Property>(IOmronPlcRx plc, T value)`. Default is `false`. | `[PlcTag("D100", Writable = true)]` |

Generator diagnostics:

| Diagnostic | Meaning |
| --- | --- |
| `OPRX001` | The containing type must be `partial`. |
| `OPRX002` | The field type is not a supported PLC tag type. |
| `OPRX003` | The address argument is missing or empty. |
| `OPRX004` | The generated property name collides with an existing member. |

Reactive package source generation:

```csharp
using OmronPlcRx.Reactive;
using ReactiveBcd16 = OmronPlcRx.Reactive.Core.Types.Bcd16;

namespace OmronPlcRx.Reactive.Example;

public sealed partial class ReactiveMachineState
{
    [PlcTag("D100", Writable = true)]
    private short _tankLevel;

    [PlcTag("D700")]
    private ReactiveBcd16 _bcdTemperature;
}
```

When the containing namespace is `OmronPlcRx.Reactive` or starts with `OmronPlcRx.Reactive.`, generated helper signatures use `global::OmronPlcRx.Reactive.IOmronPlcRx` and reactive async bridge helpers.

## Full API Reference

The public API is namespace-shifted between the base and reactive packages. Replace `OmronPlcRx` with `OmronPlcRx.Reactive` for the reactive package unless a section explicitly says otherwise.

### `OmronPlcRx.OmronPlcRx`

High-level facade for tag registration, polling, observation, cached values, writes, clock access, cycle time access, and disposal.

#### Constructors

```csharp
public OmronPlcRx(
    byte localNodeId,
    byte remoteNodeId,
    ConnectionMethod connectionMethod,
    string remoteHost,
    int port = 9600,
    int timeout = 2000,
    int retries = 1,
    TimeSpan? pollInterval = null);
```

Creates a TCP or UDP PLC facade. `localNodeId` and `remoteNodeId` are FINS node identifiers. `remoteHost` is the PLC hostname or IP address. `timeout` is in milliseconds. `retries` controls transient retry attempts. `pollInterval` defaults to `100` ms.

```csharp
using var plc = new OmronPlcRx.OmronPlcRx(
    11,
    1,
    ConnectionMethod.TCP,
    "192.168.250.1",
    port: 9600,
    timeout: 2000,
    retries: 1,
    pollInterval: TimeSpan.FromMilliseconds(250));
```

```csharp
public OmronPlcRx(
    byte localNodeId,
    byte remoteNodeId,
    OmronSerialOptions serialOptions,
    int timeout = 2000,
    int retries = 1,
    TimeSpan? pollInterval = null);
```

Creates a serial FINS PLC facade. The transport is selected by `serialOptions.Protocol`.

```csharp
using var plc = new OmronPlcRx.OmronPlcRx(
    11,
    0,
    OmronSerialOptions.CreateToolbus("COM3"),
    timeout: 2000,
    retries: 1);
```

#### Properties

| Property | Description | Example |
| --- | --- | --- |
| `IObservable<IPlcTag?> ObserveAll` | Emits each tag that changes during polling. | `using var sub = plc.ObserveAll.SubscribeSafe(tag => Console.WriteLine(tag?.TagName), ex => Console.WriteLine(ex.Message));` |
| `IObservable<OmronPLCException?> Errors` | Emits initialization, polling, and write failures. | `using var sub = plc.Errors.SubscribeSafe(ex => Console.WriteLine(ex?.Message), ex => Console.WriteLine(ex.Message));` |
| `bool IsDisposed` | Indicates whether the facade has been disposed. | `if (plc.IsDisposed) return;` |
| `PLCType PLCType` | Detected controller type after initialization. | `Console.WriteLine(plc.PLCType);` |
| `string? ControllerModel` | Controller model returned by CPU unit data. | `Console.WriteLine(plc.ControllerModel);` |
| `string? ControllerVersion` | Controller version returned by CPU unit data. | `Console.WriteLine(plc.ControllerVersion);` |

#### Methods

`AddUpdateTagItem<T>(string variable, string tagName)` registers or replaces a tag definition.

```csharp
plc.AddUpdateTagItem<bool>("D100.0", "MotorRun");
plc.AddUpdateTagItem<short>("D200", "TemperatureRaw");
```

`Observe<T>(string? tagName)` returns a typed observable for a registered tag. It throws `ArgumentNullException` when `tagName` is null.

```csharp
using var subscription = plc.Observe<short>("TemperatureRaw")
    .SubscribeSafe(value => Console.WriteLine(value), error => Console.WriteLine(error.Message));
```

`Value<T>(string? tagName)` returns the last cached value for a registered tag, or `default` when the tag has not been read or the requested type does not match.

```csharp
short? cached = plc.Value<short>("TemperatureRaw");
```

`Value<T>(string? tagName, T? value)` writes a value to the PLC asynchronously in the background. The tag must already be registered with the same `T`.

```csharp
plc.Value("MotorRun", true);
plc.Value("LineName", "Line 1");
plc.Value("BcdTemperature", new Bcd16(235));
```

`ReadClockAsync(CancellationToken cancellationToken = default)` reads the PLC real-time clock.

```csharp
ReadClockResult clock = await plc.ReadClockAsync(cancellationToken);
Console.WriteLine($"{clock.Clock:o}, day={clock.DayOfWeek}");
```

`WriteClockAsync(DateTime newDateTime, CancellationToken cancellationToken = default)` writes the PLC real-time clock and infers the day-of-week from `newDateTime`.

```csharp
WriteClockResult result = await plc.WriteClockAsync(DateTime.Now, cancellationToken);
Console.WriteLine($"Clock write took {result.Duration} ms");
```

`WriteClockAsync(DateTime newDateTime, int newDayOfWeek, CancellationToken cancellationToken = default)` writes the PLC real-time clock with an explicit day-of-week value from `0` through `6`.

```csharp
await plc.WriteClockAsync(DateTime.Now, newDayOfWeek: 3, cancellationToken);
```

`ReadCycleTimeAsync(CancellationToken cancellationToken = default)` reads scan cycle time statistics.

```csharp
ReadCycleTimeResult cycle = await plc.ReadCycleTimeAsync(cancellationToken);
Console.WriteLine($"{cycle.MinimumCycleTime}/{cycle.MaximumCycleTime}/{cycle.AverageCycleTime}");
```

`Dispose()` stops polling, completes observables, disposes signals, disposes the underlying PLC channel, and releases cancellation resources.

```csharp
plc.Dispose();
```

### `IOmronPlcRx`

`IOmronPlcRx` defines the facade contract and inherits `ReactiveUI.Primitives.Disposables.IsDisposed`.

```csharp
public sealed class MachineService(IOmronPlcRx plc)
{
    public IDisposable Start()
    {
        plc.AddUpdateTagItem<bool>("D100.0", "MotorRun");
        return plc.Observe<bool>("MotorRun")
            .SubscribeSafe(value => Console.WriteLine(value), error => Console.WriteLine(error.Message));
    }
}
```

Interface members match the high-level facade: `ObserveAll`, `Errors`, `PLCType`, `ControllerModel`, `ControllerVersion`, `AddUpdateTagItem<T>`, `Observe<T>`, `Value<T>(tagName)`, `Value<T>(tagName, value)`, `ReadClockAsync`, `WriteClockAsync`, and `ReadCycleTimeAsync`.

### Async Observable Extensions

Namespace: `OmronPlcRx.Async` or `OmronPlcRx.Reactive.Async`.

`ObserveAsAsyncObservable<T>(this IOmronPlcRx plc, string? tagName)` converts a typed tag stream to `IObservableAsync<T?>`.

```csharp
using OmronPlcRx.Async;
using ReactiveUI.Primitives.Async;

IObservableAsync<short?> speed = plc.ObserveAsAsyncObservable<short>("Speed");
```

`ObserveAllAsAsyncObservable(this IOmronPlcRx plc)` converts `ObserveAll` to `IObservableAsync<IPlcTag?>`.

```csharp
IObservableAsync<OmronPlcRx.Tags.IPlcTag?> all = plc.ObserveAllAsAsyncObservable();
```

`ErrorsAsAsyncObservable(this IOmronPlcRx plc)` converts `Errors` to `IObservableAsync<OmronPLCException?>`.

```csharp
IObservableAsync<OmronPLCException?> errors = plc.ErrorsAsAsyncObservable();
```

`ObserveValuesAsync<T>(this IOmronPlcRx plc, string? tagName, CancellationToken cancellationToken = default)` exposes a tag stream as `IAsyncEnumerable<T?>`.

```csharp
await foreach (short? value in plc.ObserveValuesAsync<short>("Speed", cancellationToken))
{
    Console.WriteLine(value);
}
```

### Tags

`IPlcTag` exposes tag metadata and the boxed latest value.

| Member | Description |
| --- | --- |
| `string Address` | PLC address such as `D100`, `D100.0`, or `D600[20]`. |
| `Type TagType` | CLR tag value type. |
| `string TagName` | Logical tag name used by `Observe<T>` and `Value<T>`. |
| `object? Value` | Latest cached value boxed as `object`. |

```csharp
using var sub = plc.ObserveAll.SubscribeSafe(tag =>
{
    Console.WriteLine($"{tag?.TagName} ({tag?.Address}) = {tag?.Value}");
}, error => Console.WriteLine(error.Message));
```

`PlcTag<T>(string tagName, string address)` is the typed metadata implementation.

```csharp
var tag = new OmronPlcRx.Tags.PlcTag<short>("TemperatureRaw", "D200");
Console.WriteLine($"{tag.TagName} -> {tag.Address} ({tag.TagType.Name})");
```

### Serial Options And Protocol Enums

`OmronSerialOptions(string portName)` creates serial configuration for Host Link FINS by default.

```csharp
var options = new OmronSerialOptions("COM3")
{
    BaudRate = 9600,
    DataBits = 7,
    Parity = System.IO.Ports.Parity.Even,
    StopBits = System.IO.Ports.StopBits.Two,
    Protocol = OmronSerialProtocol.HostLinkFins,
    HostLinkUnitNumber = 0,
    ResponseWaitTime = 0,
    FrameMode = OmronHostLinkFinsFrameMode.Direct,
    MaximumFrameLength = 1004,
};
```

`CreateToolbus(string portName)` creates Toolbus settings.

```csharp
OmronSerialOptions toolbus = OmronSerialOptions.CreateToolbus("COM3");
```

`Validate()` checks protocol, Host Link unit range, response wait range, baud rate, data bits, and frame length.

```csharp
options.Validate();
```

`OmronSerialOptions` properties:

| Property | Default | Description |
| --- | --- | --- |
| `PortName` | Constructor argument | Serial port name. |
| `BaudRate` | `9600` | Serial baud rate. |
| `DataBits` | `7` | Serial data bits. |
| `Parity` | `Even` | Serial parity. |
| `StopBits` | `Two` | Serial stop bits. |
| `Handshake` | `None` | Serial handshake. |
| `RtsEnable` | `false` | RTS line state. |
| `DtrEnable` | `false` | DTR line state. |
| `Protocol` | `HostLinkFins` | `HostLinkFins` or `Toolbus`. |
| `HostLinkUnitNumber` | `0` | Host Link unit number, `0` through `31`. |
| `ResponseWaitTime` | `0` | Host Link response wait value, `0` through `15`. |
| `FrameMode` | `Direct` | Host Link FINS frame layout. |
| `MaximumFrameLength` | `1004` | Maximum serial frame length. |

### Frame Codecs

`HostLinkFinsFrameCodec` encodes and decodes Host Link FINS ASCII frames.

```csharp
var options = new OmronSerialOptions("COM3")
{
    HostLinkUnitNumber = 0,
    FrameMode = OmronHostLinkFinsFrameMode.Direct,
};

var codec = new HostLinkFinsFrameCodec(options);
string fcs = HostLinkFinsFrameCodec.CalculateFcs("@00FA000");
string requestFrame = codec.EncodeRequest(finsRequestBytes);
Memory<byte> finsResponse = codec.DecodeResponse(responseFrameText);
```

| Function | Description |
| --- | --- |
| `HostLinkFinsFrameCodec(OmronSerialOptions options)` | Stores and validates Host Link options. |
| `CalculateFcs(string frameText)` | Calculates the two-character Host Link FCS over text from `@` through the final payload character, excluding FCS and terminator. |
| `EncodeRequest(ReadOnlyMemory<byte> finsMessage)` | Encodes a binary FINS request to an ASCII Host Link frame with FCS and `*\r` terminator. |
| `DecodeResponse(string frame)` | Validates FCS, terminator, unit, header, and end code, then returns the binary FINS response. |

`ToolbusFinsFrameCodec` encodes and decodes binary Toolbus frames.

```csharp
ReadOnlyMemory<byte> sync = ToolbusFinsFrameCodec.SynchronizationFrame;
Memory<byte> requestFrame = ToolbusFinsFrameCodec.EncodeRequest(finsRequestBytes);
Memory<byte> finsResponse = ToolbusFinsFrameCodec.DecodeResponse(toolbusResponseBytes);
ushort checksum = ToolbusFinsFrameCodec.CalculateChecksum(requestFrame.Span[..^2]);
```

| Function | Description |
| --- | --- |
| `SynchronizationFrame` | Returns the Toolbus synchronization frame `AC 01`. |
| `EncodeRequest(ReadOnlyMemory<byte> finsMessage)` | Wraps a binary FINS request in a Toolbus `0xAB` frame with length and checksum. |
| `DecodeResponse(ReadOnlyMemory<byte> frame)` | Validates Toolbus frame header, length, checksum, and FINS payload header, then returns the FINS payload. |
| `CalculateChecksum(ReadOnlySpan<byte> data)` | Calculates the 16-bit additive Toolbus checksum. |

### BCD Types And Converter

BCD wrapper types live in `OmronPlcRx.Core.Types` or `OmronPlcRx.Reactive.Core.Types`.

```csharp
var signed16 = new Bcd16(235);
var unsigned16 = new BcdU16(235);
var signed32 = new Bcd32(12345678);
var unsigned32 = new BcdU32(12345678);

Console.WriteLine(signed16.Value);
Console.WriteLine(signed32.ToString());
```

`BCDConverter` lives in `OmronPlcRx.Core.Converters` or `OmronPlcRx.Reactive.Core.Converters`.

| Function | Description | Example |
| --- | --- | --- |
| `ToByte(byte bcdByte)` | Converts one packed BCD byte to binary. | `byte value = BCDConverter.ToByte(0x42);` |
| `ToInt16(short bcdWord)` | Converts one BCD word to signed 16-bit integer. | `short value = BCDConverter.ToInt16(0x1234);` |
| `ToInt16(byte[] bcdBytes)` | Converts exactly two BCD bytes to signed 16-bit integer. | `short value = BCDConverter.ToInt16(new byte[] { 0x34, 0x12 });` |
| `ToUInt16(short bcdWord)` | Converts one BCD word to unsigned 16-bit integer. | `ushort value = BCDConverter.ToUInt16(0x1234);` |
| `ToUInt16(byte[] bcdBytes)` | Converts exactly two BCD bytes to unsigned 16-bit integer. | `ushort value = BCDConverter.ToUInt16(new byte[] { 0x34, 0x12 });` |
| `ToInt32(short bcdWord1, short bcdWord2)` | Converts two BCD words to signed 32-bit integer. | `int value = BCDConverter.ToInt32(0x5678, 0x1234);` |
| `ToInt32(byte[] bcdBytes)` | Converts exactly four BCD bytes to signed 32-bit integer. | `int value = BCDConverter.ToInt32(new byte[] { 0x78, 0x56, 0x34, 0x12 });` |
| `ToUInt32(short bcdWord1, short bcdWord2)` | Converts two BCD words to unsigned 32-bit integer. | `uint value = BCDConverter.ToUInt32(0x5678, 0x1234);` |
| `ToUInt32(byte[] bcdBytes)` | Converts exactly four BCD bytes to unsigned 32-bit integer. | `uint value = BCDConverter.ToUInt32(new byte[] { 0x78, 0x56, 0x34, 0x12 });` |
| `GetBCDByte(byte binaryValue)` | Encodes a byte as one packed BCD byte. | `byte bcd = BCDConverter.GetBCDByte(42);` |
| `GetBCDWord(short binaryValue)` | Encodes signed 16-bit integer as one BCD word. | `short bcd = BCDConverter.GetBCDWord((short)1234);` |
| `GetBCDWord(ushort binaryValue)` | Encodes unsigned 16-bit integer as one BCD word. | `short bcd = BCDConverter.GetBCDWord((ushort)1234);` |
| `GetBCDWords(int binaryValue)` | Encodes signed 32-bit integer as two BCD words. | `short[] words = BCDConverter.GetBCDWords(12345678);` |
| `GetBCDWords(uint binaryValue)` | Encodes unsigned 32-bit integer as two BCD words. | `short[] words = BCDConverter.GetBCDWords(12345678u);` |
| `GetBCDBytes(short binaryValue)` | Encodes signed 16-bit integer as two BCD bytes. | `byte[] bytes = BCDConverter.GetBCDBytes((short)1234);` |
| `GetBCDBytes(ushort binaryValue)` | Encodes unsigned 16-bit integer as two BCD bytes. | `byte[] bytes = BCDConverter.GetBCDBytes((ushort)1234);` |
| `GetBCDBytes(int binaryValue)` | Encodes signed 32-bit integer as four BCD bytes. | `byte[] bytes = BCDConverter.GetBCDBytes(12345678);` |
| `GetBCDBytes(uint binaryValue)` | Encodes unsigned 32-bit integer as four BCD bytes. | `byte[] bytes = BCDConverter.GetBCDBytes(12345678u);` |

### Result Types

All result records include transmission metrics:

| Property | Description |
| --- | --- |
| `BytesSent` | Number of bytes sent. |
| `PacketsSent` | Number of packets sent. |
| `BytesReceived` | Number of bytes received. |
| `PacketsReceived` | Number of packets received. |
| `Duration` | Request duration in milliseconds. |

Specific result payloads:

| Type | Payload properties | Used by |
| --- | --- | --- |
| `ReadBitsResult` | `bool[] Values` | Bit reads. |
| `ReadWordsResult` | `short[] Values` | Word reads. |
| `WriteBitsResult` | Metrics only. | Bit writes. |
| `WriteWordsResult` | Metrics only. | Word writes. |
| `ReadClockResult` | `DateTime Clock`, `int DayOfWeek` | `ReadClockAsync`. |
| `WriteClockResult` | Metrics only. | `WriteClockAsync`. |
| `ReadCycleTimeResult` | `MinimumCycleTime`, `MaximumCycleTime`, `AverageCycleTime` | `ReadCycleTimeAsync`. |

Example:

```csharp
ReadClockResult clock = await plc.ReadClockAsync();
Console.WriteLine($"{clock.Clock:o} received in {clock.Duration} ms");
```

### Enums

| Enum | Values |
| --- | --- |
| `ConnectionMethod` | `TCP`, `UDP`, `Serial` |
| `MemoryBitDataType` | `None`, `DataMemory`, `CommonIO`, `Work`, `Holding`, `Auxiliary` |
| `MemoryWordDataType` | `None`, `DataMemory`, `CommonIO`, `Work`, `Holding`, `Auxiliary` |
| `PLCType` | `NJ101`, `NJ301`, `NJ501`, `NX1P2`, `NX102`, `NX701`, `NY512`, `NY532`, `NJ_NX_NY_Series`, `CJ2`, `CP1`, `C_Series`, `Unknown` |
| `OmronSerialProtocol` | `HostLinkFins`, `Toolbus` |
| `OmronHostLinkFinsFrameMode` | `Direct`, `Network` |

Example:

```csharp
var method = ConnectionMethod.UDP;
var wordArea = MemoryWordDataType.DataMemory;
var bitArea = MemoryBitDataType.Work;
var protocol = OmronSerialProtocol.Toolbus;
```

### Exceptions

`OmronPLCException` represents communication, processing, validation, initialization, polling, and write failures.

```csharp
using var errors = plc.Errors.SubscribeSafe(
    error => Console.WriteLine(error?.Message),
    error => Console.WriteLine(error.Message));
```

`FINSException` represents FINS protocol errors or invalid FINS responses.

Both exception types provide standard constructors:

```csharp
throw new OmronPLCException();
throw new OmronPLCException("PLC communication failed");
throw new OmronPLCException("PLC communication failed", innerException);

throw new FINSException();
throw new FINSException("FINS response was invalid");
throw new FINSException("FINS response was invalid", innerException);
```

### `OmronPlcRx.SourceGenerators.PlcTagSourceGenerator`

The generator implements Roslyn `ISourceGenerator`. It is an analyzer component, not a runtime API normally called by application code. It responds to `[PlcTag]` fields and emits `<ContainingType>.PlcReactiveStreams.g.cs` files during compilation.

```csharp
// Application code uses the attribute, not the generator directly.
[PlcTag("D100", Writable = true)]
private short _tankLevel;
```

## Error Handling

Initialization, polling, unsupported type, address parsing, and write failures are published to `Errors`.

```csharp
using var errors = plc.Errors.SubscribeSafe(
    error => Console.WriteLine($"PLC error: {error?.Message}"),
    error => Console.WriteLine(error.Message));
```

Synchronous validation still throws for invalid arguments. For example, `Observe<T>(null)` throws `ArgumentNullException`, and writing an unregistered tag throws `KeyNotFoundException`. The actual PLC write runs in the background and reports failures through `Errors`.

## PLC Types And Limits

Controller type is inferred during initialization from CPU unit data. It affects supported memory areas and maximum read/write word counts.

- CP1 read limit: `499` words.
- CP1 write limit: `496` words.
- Other detected PLC types read limit: `999` words.
- Other detected PLC types write limit: `996` words.
- Auxiliary area is not supported on N-series PLCs.
- Data memory bits are not supported on CP1 by the low-level validation rules.
- `ReadCycleTimeAsync` is not supported on NX/NY series except NJ101/NJ301/NJ501.

## Testing

The test suite uses TUnit on Microsoft.Testing.Platform.

```bash
dotnet test tests/OmronPlcRx.Tests/OmronPlcRx.Tests.csproj --framework net10.0
```

## FAQ

**Which package should I use for V2?**

Use `OmronPlcRx` for new applications that do not need System.Reactive package conventions. Use `OmronPlcRx.Reactive` for Rx/System.Reactive applications.

**Why did my namespaces break after moving to `OmronPlcRx.Reactive`?**

The reactive package uses `OmronPlcRx.Reactive.*` namespaces. Update the root namespace and dependent namespaces such as `Enums`, `Tags`, `Results`, `Async`, `Core.Types`, and `Core.Converters`.

**Why does `Value<T>(tagName, value)` not throw when the PLC write fails?**

It starts the write in the background. Subscribe to `Errors` for write failures.

**Why are generated `ObservableAsync` properties missing?**

They are generated only for `net8.0` and later.

**Why do multi-word numeric values look swapped?**

The runtime stores 32-bit and 64-bit values in low-word-first PLC order and reconstructs them accordingly. Confirm the PLC program uses the same word order.

**Why does a string contain unexpected characters?**

Check the `[length]` suffix and confirm the PLC memory uses ASCII packed two characters per word.

**Can I change the polling interval after construction?**

No. Dispose the facade and create a new instance with a different `pollInterval`.

## Contributing

Pull requests are welcome. Useful areas include additional PLC data types, reconnect and health monitoring, structured logging hooks, and broader source-generator diagnostics coverage.

## License

MIT License. See `LICENSE`.

## Disclaimer

This project is not affiliated with Omron. Validate behavior in a safe test environment before using it with production equipment.
