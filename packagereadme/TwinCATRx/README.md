# TwinCATRx

## Overview

`CP.TwinCATRx` provides reactive Beckhoff TwinCAT ADS access: configuration-driven notifications, one-shot reads and writes, typed observation extensions, structured values, and an in-memory ADS client for deterministic tests.

## Safety

ADS writes change PLC state. Use a non-production target for development, separate engineering and production networks, restrict routes and credentials, validate every value and array length, and retain controller-side interlocks. Never use a reactive stream as the sole safety mechanism.

## Package matrix

| Package | Default namespace | Targets | Purpose |
|---|---|---|---|
| `CP.TwinCATRx` | `IoT.DriverCore.TwinCATRx` | core targets plus Windows targets | ADS client, observables, structures, generator |
| `CP.TwinCATRx.Reactive` | `IoT.DriverCore.TwinCATRx.Reactive` | matching package targets | System.Reactive-compatible surface where supplied |
| `CP.TwinCATRx.Core` | `IoT.DriverCore.TwinCATRx.Core` | net462, net472, net48, net481, net8.0-net11.0 | Shared settings, ADS abstractions, conversion, retry, and dynamic-code helpers |
| `CP.TwinCATRx.Core.Reactive` | `IoT.DriverCore.TwinCATRx.Core.Reactive` | matching core targets | System.Reactive-compatible core helper surface |
| `TwinCATRx.Generators` | source attributes are in `IoT.DriverCore.TwinCATRx` | analyzer targets supplied by the compiler | Standalone optional reactive-stream analyzer, useful when it is version-pinned separately from the runtime. |

The package references `HashTableRx`, `ReactiveUI.Primitives`, and `ReactiveUI.Primitives.Async`. The `TwinCATRx.Core` dependency supplies `Settings`, `ISettings`, ADS abstractions, and configuration extensions. Dynamic ADS type generation is annotated for trimming/AOT analysis; source-generated stream models avoid application-level runtime reflection.

## Install

```bash
dotnet add package CP.TwinCATRx
# Optional: CP.TwinCATRx already embeds this analyzer.
dotnet add package TwinCATRx.Generators
```

## Quick start

```csharp
using IoT.DriverCore.TwinCATRx;
using IoT.DriverCore.TwinCATRx.Core;

using var client = new RxTcAdsClient();
var settings = new Settings { AdsAddress = "5.35.59.10.1.1", Port = 851, SettingsId = "Default" };
settings.AddNotification(".AInt");
settings.AddWriteVariable(".AInt");

using var values = client.Observe<short>(".AInt", value => Convert.ToInt16(value))
    .Subscribe(value => Console.WriteLine($"AInt = {value}"));
using var failures = client.ErrorReceived.Subscribe(Console.Error.WriteLine);

client.Connect(settings);
client.Read(".AInt");
client.Write(".AInt", (short)42);
```

`InitializeComplete` is the readiness signal for a completed connection/setup. `DataReceived` emits `(Variable, Data, Id)` updates; use the `id` overloads of `Read`, `Write`, and `Observe` to correlate one-shot activity.

## Configuration

Set `Settings.AdsAddress`, `Port`, and `SettingsId`, then register every notification with `AddNotification` and every writable variable with `AddWriteVariable`. The configuration overloads accept a cycle time and an array/string size. Supply a positive explicit size for ADS strings and arrays when the protocol cannot infer it.

Use `Connect(ISettings)`, then `Disconnect()` before disposing. Dynamic structure materialization in `Connect` is marked `RequiresDynamicCode` and `RequiresUnreferencedCode`; publish trimmed/AOT applications only after validating their exact ADS structures.

## Detailed features

### Connection lifecycle, events, errors, and disposal

`IRxTcAdsClient` is the common contract implemented by `RxTcAdsClient` for ADS and `InMemoryAdsClient` for tests. Construct/configure the client, subscribe to its streams, call `Connect(ISettings)`, wait for `InitializeComplete`, then read/write or observe. `Disconnect()` releases ADS handles while allowing a later `Connect`; `Dispose()` is terminal. `Connected`, `Settings`, `IsPaused`, `IsPausedObservable`, `IsDisposed`, `ReadWriteHandleInfo`, and `WriteHandleInfo` expose session state. There is no cancellation token on the synchronous ADS operations: cancel work by disposing the subscription/client or by composing an async observable with its cancellation-aware subscription.

`ErrorReceived` is the fault stream: subscribe before `Connect`, report the exception with the variable/correlation context held by your application, and do not assume an exception has stopped other notifications. `Code` exposes generated/dynamic type code where applicable, `OnWrite` emits written variable names, and `DataReceived` emits `(Variable, Data, Id)` for notification and correlated read results. Each has an `IObservableAsync<T>` counterpart where declared (`...Async`), suitable for the ReactiveUI.Primitives async observer model.

```csharp
using IoT.DriverCore.TwinCATRx;
using IoT.DriverCore.TwinCATRx.Core;

using var client = new RxTcAdsClient();
using var errors = client.ErrorReceived.Subscribe(ex => AuditFailure(ex));
using var ready = client.InitializeComplete.Subscribe(_ => Console.WriteLine("ADS handles ready"));
using var written = client.OnWrite.Subscribe(name => Console.WriteLine($"Wrote {name}"));

var settings = new Settings { AdsAddress = "5.35.59.10.1.1", Port = 851, SettingsId = "LineA" };
settings.AddNotification(".Main.Temperature", cycleTime: 250);
settings.AddWriteVariable(".Main.Setpoint");
client.Connect(settings);
// Dispose subscriptions first, then Disconnect/Dispose when the application stops.
```

### Settings, ADS addressing, notifications, and handles

`Settings` implements `ISettings`: set `AdsAddress` to the route/net-id accepted by TwinCAT, set the ADS `Port` (commonly 851 for a PLC runtime but use the configured target), and give the configuration a stable `SettingsId`. Its `Notifications` are `INotification`/`Notification` records (`Variable`, `UpdateRate`, `ArraySize`); write registrations are `IWriteVariable`/`WriteVariable` (`Variable`, `ArraySize`). The `TwinCatRxExtensions.AddNotification` overloads accept a name alone, name/cycle, or name/cycle/array size. `AddWriteVariable` accepts a name alone or name/array size. Register all desired handles before `Connect`; adding to `Settings` afterwards does not retrofit an existing session.

Use the exact PLC symbol spelling, including the leading-dot convention used by your project. For fixed-length `STRING`, arrays, or an `ANYTYPE` shape the runtime cannot infer, use the explicit size. A non-positive or incorrect size produces a handle/read conversion failure or truncated data; it is not a network retry condition.

```csharp
var settings = new Settings
{
    AdsAddress = "5.35.59.10.1.1",
    Port = 851,
    SettingsId = "PackagingCell"
};
settings.AddNotification(".Main.State", 100);
settings.AddNotification(".Main.BatchName", 500, arraySize: 80);
settings.AddWriteVariable(".Main.RequestedSpeed");
settings.AddWriteVariable(".Main.Recipe", arraySize: 80);
```

### One-shot reads, correlated requests, writes, and pause windows

`Read(variable)`, `Read(variable, id)`, `Read(variable, arrayLength)`, and `Read(variable, arrayLength, id)` issue a one-shot read and publish its result through `DataReceived`. Use an `id` when several reads of the same variable can overlap; use `arrayLength` for an array/string request whose size is not registered. `Write(variable, value)` and `Write(variable, value, id)` issue a write and publish the variable through `OnWrite`; check `ErrorReceived` for asynchronous ADS failures. Calls are void because their useful completion signals are these streams.

`Pause(TimeSpan)` sets the pause state for paced write workflows; observe `IsPausedObservable` rather than guessing timing. It coordinates client work but does not create PLC-side transaction semantics. Use a PLC handshake/sequence value when multiple writes must be observed atomically by the control program.

```csharp
using var reply = client.DataReceived
    .Where(x => x.Variable == ".Main.State" && x.Id == "state-42")
    .Take(1)
    .Subscribe(x => Console.WriteLine(Convert.ToInt16(x.Data)));

client.Read(".Main.State", "state-42");
client.Pause(TimeSpan.FromMilliseconds(100));
client.Write(".Main.RequestedSpeed", (short)1200, "speed-42");
```

### Typed observable and asynchronous-observable APIs

`TwinCatRxExtensions.Observe<T>` is the usual typed adapter over `DataReceived`. Supply a variable and converter; its overload with an `id` lets one stream accept a particular correlated read/notification identity. It registers no magic conversion: the converter owns the cast/scale and should validate arrays/structures. `ObserveAsyncObservable<T>` provides the same value stream as `IObservableAsync<T>`; use it when downstream consumers implement `IObserverAsync<T>` or require cancellation-aware `SubscribeAsync` disposal.

```csharp
using ReactiveUI.Primitives;
using ReactiveUI.Primitives.Async;

using var temperature = client.Observe<float>(".Main.Temperature", value => Convert.ToSingle(value))
    .DistinctUntilChanged()
    .Subscribe(RenderTemperature);

var asynchronous = client.ObserveAsyncObservable<short>(
    ".Main.State", "state-42", value => Convert.ToInt16(value));
await using var subscription = await asynchronous.SubscribeAsync(
    new ConsoleAsyncObserver<short>(Console.WriteLine), CancellationToken.None);

sealed class ConsoleAsyncObserver<T>(Action<T> onNext) : IObserverAsync<T>
{
    public ValueTask DisposeAsync() => default;
    public ValueTask OnCompletedAsync(Result result) => default;
    public ValueTask OnErrorResumeAsync(Exception error, CancellationToken cancellationToken) =>
        ValueTask.FromException(error);
    public ValueTask OnNextAsync(T value, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        onNext(value);
        return default;
    }
}
```

`ObservableBridgeExtensions.ToAsyncObservable` adapts any ordinary `IObservable<T>` and `SubscribeTo` wires an `IObservableAsync<T>` into a standard observer. The concrete bridge adapters are implementation details; use the public extension methods and dispose the returned `IDisposable` or `IAsyncDisposable`, otherwise ADS notifications and UI handlers remain alive.

### Structured variables and coordinated member writes

`CreateStruct(variable)` creates a `TwinCatStructureTable` backed by `HashTableRx` for a structured ADS symbol. It subscribes to updates and exposes the mapped fields; `StructureReady()` signals when the first usable structure has been materialized. `CreateClone()` returns a snapshot suitable for editing off-stream. `WriteValues(table => ...)` applies an edit against a clone and writes it; `WriteValuesAsync(..., pace)` performs the same action coordinated with `Pause`. These helpers suit engineering/configuration screens; for trimming/AOT-sensitive or high-throughput workloads, prefer a source-generated typed stream model.

```csharp
var table = client.CreateStruct(".Main.Recipe");
if (table is null) throw new InvalidOperationException("Symbol is not a structure.");
using (table)
using (table.StructureReady().Take(1).Subscribe(_ => Console.WriteLine("Recipe available")))
{
    var editable = table.CreateClone();
    editable.Value("TargetWeight", 12.5f);
    if (!table.WriteValues(copy => copy.Value("TargetWeight", 12.5f)))
        Console.Error.WriteLine("Write was not accepted");

    bool completed = await table.WriteValuesAsync(
        copy => copy.Value("Enabled", true), TimeSpan.FromMilliseconds(150));
}
```

### Logical tags, catalogs, persistence, and bulk workflows

`TwinCatLogicalTagClient` is the common logical-tag facade over an `IRxTcAdsClient`. Construct it with the native client and optionally an `ILogicalTagCatalog`, `LogicalTagSqliteStore`, and `TimeProvider`. Its `Catalog` holds the mappings. `CreateTag`, `RegisterTag`, and `RemoveTag` manage the in-memory map; `ReadAsync`/`WriteAsync`, `ReadManyAsync`/`WriteManyAsync`, `Observe`/`ObserveMany`, and `ObserveAsync`/`ObserveManyAsync` operate by logical name. Results are `TagOperationResult<LogicalTagValue>`: inspect their success/error data rather than assuming every request was accepted.

The persistence members `InitializeStoreAsync`, `LoadTagsAsync`, `GetTagAsync`, `ListTagsAsync`, `UpsertTagAsync`, `EditTagAsync`, `DeleteTagAsync`, group CRUD, `ImportCsvAsync`, and `ExportCsvAsync` support commissioning data. All have cancellation-bearing overloads where I/O occurs. Import into a staged catalog/store, validate address/type information against the PLC, then replace the active set - avoid modifying a live safety mapping from an unreviewed CSV.

```csharp
using IoT.DriverCore.Core;

using var catalog = new LogicalTagCatalog();
var store = new LogicalTagSqliteStore("Data Source=logical-tags.db");
using var tags = new TwinCatLogicalTagClient(client, catalog, store);
await tags.InitializeStoreAsync(CancellationToken.None);
tags.RegisterTag(tags.CreateTag("Line.Speed", ".Main.ActualSpeed", "Int16"));

var read = await tags.ReadAsync("Line.Speed", CancellationToken.None);
if (read.Succeeded) Console.WriteLine(read.Value.Value);

var writes = await tags.WriteManyAsync(
    [new LogicalTagValue(
        "Line.Speed", (short)900, TimeProvider.System.GetUtcNow(), "Good")],
    CancellationToken.None);
using var changes = tags.Observe("Line.Speed").Subscribe(value => RenderSpeed((short)value.Value!));
```

### In-memory ADS client and deterministic tests

`InMemoryAdsClient` implements `IRxTcAdsClient` without a TwinCAT runtime. Register scalar or typed symbols through `RegisterSymbol`/`RegisterStructure`, configure/connect it exactly as the production client, and exercise `Read`, `Write`, `ReadMany`, `WriteMany`, `PublishNotifications`, `Pause`, `Reconnect`, `SetValue`, `TryGetValue`, and `RemoveSymbol`. `QueueFault(InMemoryAdsOperation, Exception)` injects an expected failure. `ConnectionState`/`ConnectionStates`, `Symbols`, and `OperationMetrics` (`InMemoryAdsOperationMetrics`) make tests assertable without timing against a real ADS route. `ResetOperationMetrics` clears counters between scenarios.

```csharp
using var fake = new InMemoryAdsClient();
fake.RegisterSymbol(".Main.Counter", 0);
fake.Connect(new Settings { SettingsId = "test", Port = 851 });
fake.QueueFault(InMemoryAdsOperation.Write, new InMemoryAdsException("planned"));
using var failed = fake.ErrorReceived.Take(1).Subscribe(_ => Console.WriteLine("fault observed"));
fake.Write(".Main.Counter", 1);            // exercises the failure route
fake.SetValue(".Main.Counter", 2);
fake.PublishNotifications();
Console.WriteLine(fake.OperationMetrics.WriteCount);
```

### Source-generated reactive stream and connection models

`TwinCatReactiveStreamGenerator` is embedded in `CP.TwinCATRx`; `TwinCATRx.Generators` is the standalone optional analyzer package for projects that intentionally pin it separately. Do not reference both analyzer versions in one project. Apply `TwinCatReactiveStreamAttribute(variable, dataType)` to a partial class for the legacy stream surface, or apply `TwinCatPlcConnectionAttribute(adsAddress, port)` to a partial connection class and decorate members with `DirectNotificationAttribute`, `StructuredNotificationAttribute`, and/or `WriteOnlyAttribute`.

`DirectNotificationAttribute` accepts `Address`, optional `CycleTime`, `ArraySize`, `Id`, `ObservableName`, `CanWrite`, and `WriteAddress`. `StructuredNotificationAttribute` supplies an address and optional member address plus the same notification/write options. `WriteOnlyAttribute` supplies `Address`, `ArraySize`, and `Id`. The generator emits strongly typed properties, observable members, read/write helpers, settings registrations and connection lifecycle wiring; inspect compiler diagnostics/`obj` generated source when a partial declaration does not produce the expected member.

```csharp
using IoT.DriverCore.TwinCATRx;

[TwinCatPlcConnection("5.35.59.10.1.1", 851, SettingsId = "LineA")]
public sealed partial class LineConnection
{
    [DirectNotification(".Main.Temperature", CycleTime = 250, CanWrite = false)]
    public float Temperature { get; private set; }

    [WriteOnly(".Main.Setpoint")]
    public float Setpoint { get; private set; }
}
// Generated members own typed observation/settings; the partial source remains the schema.
```

The legacy stream attribute is useful for a small, focused model rather than a full connection declaration. It generates a nullable typed property, classic/async observable properties, logical-tag read/write helpers, and `BindTwinCatRx(IRxTcAdsClient)`. The analyzer is supplied by `CP.TwinCATRx` or, when version-pinned deliberately, `TwinCATRx.Generators`; never both versions.

```csharp
using IoT.DriverCore.TwinCATRx;
using IoT.DriverCore.TwinCATRx.Core;
using ReactiveUI.Primitives;

[TwinCatReactiveStream(
    ".Main.Counter", typeof(short), PropertyName = "Counter", ObservableName = "CounterValues")]
public sealed partial class LegacyCounterStream
{
}

using var client = new InMemoryAdsClient();
client.RegisterSymbol(".Main.Counter", (short)41);
client.Connect(new Settings { SettingsId = "legacy-demo", Port = 851 });

var model = new LegacyCounterStream();
using var values = model.CounterValues.Subscribe(value => Console.WriteLine(value));
using var binding = model.BindTwinCatRx(client);
client.Read(".Main.Counter"); // updates Counter and CounterValues through the generated binding
```

### Core helpers, dynamic code generation, and service monitoring

`CP.TwinCATRx.Core` contains `CodeGenerator`/`ICodeGenerator`, `CSharpLanguage`/`ILanguageService`, `INodeEmulator`, `DirectoryInfoExtensions`, `Notification`, `WriteVariable`, and the `SimpleTypeException`/`UnsuportedTypeException` error types. `CodeGenerator` uses a symbol graph to produce/compile dynamic type support. These are advanced integration APIs and carry dynamic-code/trimming annotations: validate the actual published artifact, preserve required members or choose source generation for NativeAOT/trimmed applications.

`TwinCatRxExtensions.AdsStateChangedObserver` and `AdsStateObserver` expose ADS state changes. Its `OnErrorRetry` overload family retries an observable with optional exception type, retry count/delay, and error callback; use bounded retry with telemetry, not an infinite blind loop around a safety operation. `AssemblyLoad` and `GetType` support plug-in/type loading. `ObservableServiceController`, `IObservableServiceController`, and `ServiceStatus` are Windows service-monitoring APIs; use them only on supported Windows targets and dispose their polling subscription.

```csharp
using IoT.DriverCore.TwinCATRx.Core;
using ReactiveUI.Primitives;
using TwinCAT.Ads;

using var ads = new AdsClient();
using var adsState = TwinCatRxExtensions.OnErrorRetry<StateInfo, Exception>(
    TwinCatRxExtensions.AdsStateObserver(ads),
        error => Console.Error.WriteLine(error.Message),
        retryCount: 3,
        delay: TimeSpan.FromSeconds(1))
    .Subscribe(state => Console.WriteLine(state.AdsState));

using var service = new ObservableServiceController(
    new System.ServiceProcess.ServiceController("TcSysSrv"));
using var status = service.StatusObserver.Subscribe(s => Console.WriteLine(s));
```

The core APIs are independently usable, but dynamic-code methods are deliberately explicit about their deployment cost. The following source-verified recipe gives an ADS-state observer a bounded retry policy, emits a small support assembly, and emits source from a symbol tree discovered from the configured route. Run the dynamic part only in a non-trimmed, trusted engineering tool; it is not required for normal `RxTcAdsClient` use.

```csharp
using IoT.DriverCore.TwinCATRx.Core;
using TwinCAT.Ads;

static IDisposable WatchAdsState(AdsClient ads) =>
    TwinCatRxExtensions.OnErrorRetry<StateInfo, Exception>(
        TwinCatRxExtensions.AdsStateObserver(ads),
            error => Console.Error.WriteLine(error.Message),
            retryCount: 3,
            delay: TimeSpan.FromSeconds(1))
        .Subscribe(state => Console.WriteLine(state.AdsState));

using var generator = new CodeGenerator(Console.Error.WriteLine);
var roots = generator.LoadSymbols("5.35.59.10.1.1", 851);
var root = roots.FirstOrDefault();
if (root is not null)
{
    var source = generator.CreateCSharpCodeString(root, isTwinCat3: true, "LineA.Generated");
    bool emitted = CSharpLanguage.CreateAssembly(source, "LineA.Generated.dll");
    Console.WriteLine($"Generated: {emitted}");
}

var generatedFiles = new DirectoryInfo(".").GetFilesWhere("*.cs", file => file.Length > 0);
```

### Two combined production workflows

**Workflow 1 - typed live dashboard with a commanded setpoint.** Build `Settings` with notifications and write handles, subscribe to `ErrorReceived`, `InitializeComplete`, a typed `Observe<float>` stream and `OnWrite`, then `Connect`. Render only values from the stream; correlate a one-shot confirmation read with an id after the user requests a setpoint. Call `Pause` before the write if the UI must visibly rate-limit it; rely on a PLC acknowledgement tag, not the local pause, before declaring the change complete. Dispose streams then client on shutdown.

```csharp
using var live = new RxTcAdsClient();
var dashboard = new Settings { AdsAddress = "5.35.59.10.1.1", Port = 851, SettingsId = "Dashboard" };
dashboard.AddNotification(".Main.ActualSpeed", 200);
dashboard.AddWriteVariable(".Main.RequestedSpeed");
using var errors = live.ErrorReceived.Subscribe(ex => ReportAdsFailure(ex));
using var values = live.Observe<float>(".Main.ActualSpeed", Convert.ToSingle)
    .Subscribe(speed => RenderSpeed(speed));
using var confirm = live.DataReceived.Where(x => x.Id == "speed-confirm").Take(1)
    .Subscribe(x => ConfirmSpeed(Convert.ToSingle(x.Data)));
live.Connect(dashboard);

if (await OperatorInterlockAllowsAsync())
{
    live.Pause(TimeSpan.FromMilliseconds(100));
    live.Write(".Main.RequestedSpeed", 900f, "speed-command");
    live.Read(".Main.ActualSpeed", "speed-confirm");
}
live.Disconnect();
```

**Workflow 2 - commissioning and regression test.** Define logical tags in CSV, import them into a `TwinCatLogicalTagClient` over `InMemoryAdsClient`, register the corresponding fake symbols, run `ReadManyAsync`/`WriteManyAsync`, inject a queued `InMemoryAdsException` to test error handling, then point the unchanged logical-tag configuration at `RxTcAdsClient` in an isolated TwinCAT environment. For stable application schemas, replace runtime structure reflection with the generator attributes and test the generated connection class against `InMemoryAdsClient`.

```csharp
using var test = new InMemoryAdsClient();
test.RegisterSymbol(".Main.ActualSpeed", (short)500);
test.RegisterSymbol(".Main.RequestedSpeed", (short)0);
var configuration = new Settings { SettingsId = "commissioning", Port = 851 };
configuration.AddNotification(".Main.ActualSpeed");
configuration.AddWriteVariable(".Main.RequestedSpeed");
test.Connect(configuration);
using var observed = test.Observe<short>(".Main.ActualSpeed", Convert.ToInt16)
    .Subscribe(value => Console.WriteLine($"Observed {value}"));
test.ReadMany(new[] { ".Main.ActualSpeed", ".Main.RequestedSpeed" }, "baseline");
test.WriteMany(new[] { new KeyValuePair<string, object>(".Main.RequestedSpeed", (short)700) }, "setpoint");
test.QueueFault(InMemoryAdsOperation.Write, new InMemoryAdsException("Expected commissioning failure"));
using var fault = test.ErrorReceived.Take(1).Subscribe(ex => AssertExpectedFailure(ex));
test.Write(".Main.RequestedSpeed", (short)701, "expected-fault");
test.PublishNotifications();
test.Disconnect();
```

### Correlated reads and async streams

```csharp
using var client = new InMemoryAdsClient();
client.RegisterSymbol(".AInt", (short)5);
client.Connect(new Settings { Port = 851, SettingsId = "Test" });

using var result = client.Observe<short>(".AInt", "request-7", value => Convert.ToInt16(value))
    .Subscribe(value => Console.WriteLine(value));
client.Read(".AInt", "request-7");

var asyncData = client.DataReceivedAsync;
```

`IRxTcAdsClient` has async-native forms for initialization, data, errors, writes, and pause state. `TwinCatRxExtensions.ObserveAsyncObservable<T>` converts either typed observation overload to `IObservableAsync<T>`.

### Structures and paced writes

```csharp
var structure = client.CreateStruct(".Machine");
if (structure is not null)
{
    using var ready = structure.StructureReady().Subscribe(_ => Console.WriteLine("Structure ready"));
    bool written = structure.WriteValues(table => table.Value("Enabled", true));
    bool paced = await structure.WriteValuesAsync(
        table => table.Value("Enabled", false), TimeSpan.FromMilliseconds(200));
}
```

`CreateStruct` listens for the named variable and maps its received structure into `HashTableRx`. `WriteValues` and `WriteValuesAsync` clone before applying edits; the async variant coordinates the client's pause window. These helpers may use reflection for structures, so heed their trimming annotations.

### Test without an ADS runtime

```csharp
using var testClient = new InMemoryAdsClient();
testClient.RegisterSymbol(".Counter", 0);
testClient.Connect(new Settings { Port = 851, SettingsId = "Test" });
testClient.Write(".Counter", 1);
testClient.Read(".Counter");
```

`InMemoryAdsClient` also supports symbol removal/updates, publication, queued operation faults, operation metrics, reconnect, and bulk read/write helpers for deterministic tests.

## Complete public API reference

The following is the public surface by member family. Overloads with `id` add correlation; overloads with `arrayLength` provide a required ADS size; `...Async` observable properties use the ReactiveUI.Primitives async-observable contract, rather than converting an ADS call into `Task`.

| Area | Types and complete member families | Purpose, result, and lifetime |
| --- | --- | --- |
| ADS contract | `IRxTcAdsClient`: `Code`, `InitializeComplete`/`InitializeCompleteAsync`, `DataReceived`/`DataReceivedAsync`, `ErrorReceived`/`ErrorReceivedAsync`, `OnWrite`/`OnWriteAsync`, `ReadWriteHandleInfo`, `WriteHandleInfo`, `Settings`, `IsPaused`, `IsPausedObservable`/`IsPausedObservableAsync`, `IsDisposed`; `Connect`, `Disconnect`, `Pause`, `Read(variable)`, `Read(variable,id)`, `Read(variable,arrayLength)`, `Read(variable,arrayLength,id)`, `Write(variable,value)`, `Write(variable,value,id)`, `Dispose`. | This is the operational API. Stream subscriptions are individually disposable; client disposal releases handles and completes the owned infrastructure. |
| Production client | `RxTcAdsClient` constructors (default and `TimeProvider`) plus the complete `IRxTcAdsClient` contract. | `RxTcAdsClient` is the normal implementation. Platform/runtime adapters are intentionally internal composition details. |
| Standard and async observables | Main `TwinCatRxExtensions`: `Observe<T>` (variable/converter and variable/id/converter), `ObserveAsyncObservable<T>` matching both overloads, `CreateStruct`, `StructureReady`, `CreateClone`, `WriteValues`, `WriteValuesAsync`. `ObservableBridgeExtensions.ToAsyncObservable` and all public `SubscribeTo` overloads. | `Observe*` returns a stream that must be disposed. Structure helpers clone before modifying. Bridge methods adapt subscriptions; prefer their extensions to direct adapter construction. |
| In-memory test ADS | `InMemoryAdsClient` constructors and full `IRxTcAdsClient` surface, plus `ConnectionState`, `ConnectionStates`, `OperationMetrics`, `Symbols`, `Reconnect`, `PublishNotifications`, `QueueFault`, `ReadMany`, `WriteMany`, `RegisterSymbol` overloads, `RegisterStructure<T>` overloads, `RemoveSymbol`, `SetValue`, `TryGetValue<T>`, `ResetOperationMetrics`. `InMemoryAdsSymbol`, `InMemoryAdsOperation`, `InMemoryAdsOperationMetrics`, `InMemoryAdsException`, `InMemoryAdsConnectionState`. | Use only for deterministic tests/simulators. Calls are synchronous but publish the same events as the production contract; queue a fault before the operation whose failure path is being tested. |
| Logical tags | `TwinCatLogicalTagClient` constructors for native client, catalog/store and optional `TimeProvider`; `Catalog`, `CreateTag` overloads, `RegisterTag`, `RemoveTag`, `ReadAsync`, `ReadManyAsync`, `WriteAsync`, `WriteManyAsync`, `Observe`, `ObserveMany`, `ObserveAsync`, `ObserveManyAsync`, `Dispose`. Persistence: `ImportCsvAsync` overloads, `ExportCsvAsync` overloads, `InitializeStoreAsync`, `LoadTagsAsync` overloads, `GetTagAsync`, `ListTagsAsync`, `UpsertTagAsync`, `EditTagAsync`, `DeleteTagAsync`, `GetGroupAsync`, `ListGroupsAsync`, `UpsertGroupAsync`, `DeleteGroupAsync`. | Read/write methods return logical-tag operation results; observation returns normal or async streams. Store methods use cancellation-bearing I/O overloads and are not a substitute for validating PLC mappings. |
| Service monitoring | `IObservableServiceController`, `ObservableServiceController` constructors accepting `ServiceController` and interval, `IsDisposed`, `CanStop`, `DisplayName`, `ServiceName`, `Status`, `StatusObserver`, static `GetServices`, `Restart`, `Start`, `Stop`, `Dispose`, and `ServiceStatus`. | Windows-only operational integration. `StatusObserver` must be disposed with the controller; start/stop/restart can alter host services and require authorization. |
| Core configuration | `ISettings`/`Settings` (`AdsAddress`, `Port`, `Notifications`, `WriteVariables`, `SettingsId`); `INotification`/`Notification` (`Variable`, `UpdateRate`, `ArraySize`); `IWriteVariable`/`WriteVariable` (`Variable`, `ArraySize`). Core `TwinCatRxExtensions.AddNotification` three overloads and `AddWriteVariable` two overloads. | These values are consumed during `Connect`. Make a fresh settings instance for a different endpoint or shape rather than mutate a live connected configuration. |
| Core retry, state and loading | Core `TwinCatRxExtensions.AdsStateChangedObserver`, `AdsStateObserver`, all `OnErrorRetry` overloads (basic; typed error callback; callback plus delay; callback plus retry count; callback plus retry count/delay/sequencer), `AssemblyLoad`, `GetType`. | State observers are streams. Retry extension overloads return a new sequence; use a finite count/delay/callback for production recovery and observe terminal errors. |
| Dynamic code generation | `ICodeGenerator`, `CodeGenerator` constructors/properties, static `CodeGenerator.PLCToCSharpTypeConverter`, `CreateCSharpCode` overloads, `CreateCSharpCodeString` overloads, `CreateDll` overloads, `LoadSymbols` overloads, `ReadSymbol`, `SearchSymbols`, and `Dispose`; `ILanguageService`/`CSharpLanguage` (`CreateAssembly`, `ParseText`, `CreateLibraryCompilation`); `INodeEmulator`; `DirectoryInfoExtensions.GetFilesWhere` overloads; `SimpleTypeException`, `UnsuportedTypeException`. | Advanced reflection/dynamic-compilation integration. Generated DLL/source file operations return success flags and can throw IO/compiler errors; guard them and validate trim/AOT deployment. |
| Structures | `CreateStruct` returns the public `HashTableRx` abstraction from its extension API. | It materializes ADS structure values; dispose the returned table to release its source subscription. |
| Analyzer | `TwinCatReactiveStreamGenerator`. | It emits the compile-time schema attributes `TwinCatReactiveStreamAttribute`, `TwinCatPlcConnectionAttribute`, `DirectNotificationAttribute`, `StructuredNotificationAttribute`, and `WriteOnlyAttribute` into the consuming compilation; they are not public runtime package types. `CP.TwinCATRx` embeds the analyzer; `TwinCATRx.Generators` supplies the same analyzer as a standalone optional package. |

## Operational guidance

Register notifications before connecting, retain subscriptions for the whole operational session, and treat `ErrorReceived` and `OnWrite` as first-class telemetry. Use correlation identifiers for concurrent reads. Avoid high-rate notifications unless the PLC, ADS route, and consumer can keep up. Dispose structures and clients; do not silently recover from a safety-relevant write failure.

## Troubleshooting

- **Connect fails:** verify TwinCAT is in Run mode, the ADS route/address and port are correct, and the host can reach ADS.
- **No value arrives:** register a notification before `Connect`, wait for `InitializeComplete`, then check the exact variable spelling and route.
- **Strings/arrays are truncated or fail:** provide the required positive array/string length during registration or read.
- **Structure code fails under trimming/AOT:** preserve required members or use source-generated application stream wiring; validate the published artifact.
- **Write cadence is wrong:** use `Pause`/`WriteValuesAsync` deliberately and observe `IsPausedObservable` rather than adding uncoordinated delays.

## AI skill

Use the packaged [`twincat-rx` skill](../../skills/twincat-rx/SKILL.md) for concise source-grounded workflow guidance and use this README as the detailed reference.

MIT licensed. See the repository `LICENSE`.

<!-- BEGIN GENERATED PUBLIC API -->

## Exhaustive public API reference

This catalogue is generated from the packaged runtime assemblies and their XML documentation. It includes exported public types and their declared public members; inherited members and non-public implementation details are intentionally omitted.

### `TwinCATRx`

Exported public types: 14; declared public members: 220.

#### `T:IoT.DriverCore.TwinCATRx.IObservableServiceController`

```csharp
public interface IoT.DriverCore.TwinCATRx.IObservableServiceController
```
Interface for Observable Service Controller.

##### Declared public members

###### `M:IoT.DriverCore.TwinCATRx.IObservableServiceController.Restart`

```csharp
public void Restart()
```
Restarts this instance.

###### `M:IoT.DriverCore.TwinCATRx.IObservableServiceController.Start`

```csharp
public void Start()
```
Starts this instance.

###### `M:IoT.DriverCore.TwinCATRx.IObservableServiceController.Stop`

```csharp
public void Stop()
```
Stops this instance.

###### `P:IoT.DriverCore.TwinCATRx.IObservableServiceController.CanStop`

```csharp
public bool CanStop { get; }
```
Gets a value indicating whether this instance can stop.

- Value: true if this instance can stop; otherwise, false .

###### `P:IoT.DriverCore.TwinCATRx.IObservableServiceController.DisplayName`

```csharp
public string DisplayName { get; }
```
Gets the display name.

- Value: The display name.

###### `P:IoT.DriverCore.TwinCATRx.IObservableServiceController.ServiceName`

```csharp
public string ServiceName { get; }
```
Gets the name of the service.

- Value: The name of the service.

###### `P:IoT.DriverCore.TwinCATRx.IObservableServiceController.Status`

```csharp
public System.ServiceProcess.ServiceControllerStatus Status { get; }
```
Gets the status.

- Value: The status.

###### `P:IoT.DriverCore.TwinCATRx.IObservableServiceController.StatusObserver`

```csharp
public System.IObservable<System.ServiceProcess.ServiceControllerStatus> StatusObserver { get; }
```
Gets the status observer.

- Value: The status observer.

#### `T:IoT.DriverCore.TwinCATRx.IRxTcAdsClient`

```csharp
public interface IoT.DriverCore.TwinCATRx.IRxTcAdsClient
```
Interface for Rx Tc Ads Client.

##### Declared public members

###### `M:IoT.DriverCore.TwinCATRx.IRxTcAdsClient.Connect(IoT.DriverCore.TwinCATRx.Core.ISettings)`

```csharp
public void Connect(IoT.DriverCore.TwinCATRx.Core.ISettings settings)
```
Connects the specified settings.

- Parameter `settings`: The settings.

###### `M:IoT.DriverCore.TwinCATRx.IRxTcAdsClient.Disconnect`

```csharp
public void Disconnect()
```
Disconnects this instance.

###### `M:IoT.DriverCore.TwinCATRx.IRxTcAdsClient.Pause(System.TimeSpan)`

```csharp
public void Pause(System.TimeSpan time)
```
Pauses the specified time.

- Parameter `time`: The time.

###### `M:IoT.DriverCore.TwinCATRx.IRxTcAdsClient.Read(System.String)`

```csharp
public void Read(string variable)
```
Reads the specified data.

- Parameter `variable`: The data.

###### `M:IoT.DriverCore.TwinCATRx.IRxTcAdsClient.Read(System.String,System.Nullable`1{System.Int32})`

```csharp
public void Read(string variable, System.Nullable<int> arrayLength)
```
Executes the `Read` operation.

- Parameter `variable`: The `variable` value.
- Parameter `arrayLength`: The `arrayLength` value.

###### `M:IoT.DriverCore.TwinCATRx.IRxTcAdsClient.Read(System.String,System.Nullable`1{System.Int32},System.String)`

```csharp
public void Read(string variable, System.Nullable<int> arrayLength, string id)
```
Executes the `Read` operation.

- Parameter `variable`: The `variable` value.
- Parameter `arrayLength`: The `arrayLength` value.
- Parameter `id`: The `id` value.

###### `M:IoT.DriverCore.TwinCATRx.IRxTcAdsClient.Read(System.String,System.String)`

```csharp
public void Read(string variable, string id)
```
Reads the specified data with a correlation identifier.

- Parameter `variable`: The data.
- Parameter `id`: The identifier.

###### `M:IoT.DriverCore.TwinCATRx.IRxTcAdsClient.Write(System.String,System.Object)`

```csharp
public void Write(string variable, object value)
```
Writes the specified value.

- Parameter `variable`: The variable.
- Parameter `value`: The value.

###### `M:IoT.DriverCore.TwinCATRx.IRxTcAdsClient.Write(System.String,System.Object,System.String)`

```csharp
public void Write(string variable, object value, string id)
```
Writes the specified value with a correlation identifier.

- Parameter `variable`: The variable.
- Parameter `value`: The value.
- Parameter `id`: The identifier.

###### `P:IoT.DriverCore.TwinCATRx.IRxTcAdsClient.Code`

```csharp
public System.IObservable<string[]> Code { get; }
```
Gets the code.

- Value: The code.

###### `P:IoT.DriverCore.TwinCATRx.IRxTcAdsClient.DataReceived`

```csharp
public System.IObservable<System.ValueTuple<string, object, string>> DataReceived { get; }
```
Gets the data received.

- Value: The data received.

###### `P:IoT.DriverCore.TwinCATRx.IRxTcAdsClient.DataReceivedAsync`

```csharp
public ReactiveUI.Primitives.Async.IObservableAsync<System.ValueTuple<string, object, string>> DataReceivedAsync { get; }
```
Gets the async data received stream.

- Value: The `DataReceivedAsync` value.

###### `P:IoT.DriverCore.TwinCATRx.IRxTcAdsClient.ErrorReceived`

```csharp
public System.IObservable<System.Exception> ErrorReceived { get; }
```
Gets the error received.

- Value: The error received.

###### `P:IoT.DriverCore.TwinCATRx.IRxTcAdsClient.ErrorReceivedAsync`

```csharp
public ReactiveUI.Primitives.Async.IObservableAsync<System.Exception> ErrorReceivedAsync { get; }
```
Gets the async error received stream.

- Value: The `ErrorReceivedAsync` value.

###### `P:IoT.DriverCore.TwinCATRx.IRxTcAdsClient.InitializeComplete`

```csharp
public System.IObservable<ReactiveUI.Primitives.RxVoid> InitializeComplete { get; }
```
Gets the initialize complete. PLC is ready to read and write.

- Value: The initialize complete.

###### `P:IoT.DriverCore.TwinCATRx.IRxTcAdsClient.InitializeCompleteAsync`

```csharp
public ReactiveUI.Primitives.Async.IObservableAsync<ReactiveUI.Primitives.RxVoid> InitializeCompleteAsync { get; }
```
Gets the async initialize complete stream.

- Value: The `InitializeCompleteAsync` value.

###### `P:IoT.DriverCore.TwinCATRx.IRxTcAdsClient.IsDisposed`

```csharp
public bool IsDisposed { get; }
```
Gets a value indicating whether the instance is disposed.

- Value: The `IsDisposed` value.

###### `P:IoT.DriverCore.TwinCATRx.IRxTcAdsClient.IsPaused`

```csharp
public bool IsPaused { get; }
```
Gets a value indicating whether this instance is paused within WriteValuesAsync.

- Value: true if this instance is paused; otherwise, false .

###### `P:IoT.DriverCore.TwinCATRx.IRxTcAdsClient.IsPausedObservable`

```csharp
public System.IObservable<bool> IsPausedObservable { get; }
```
Gets the is paused observable.

- Value: The is paused observable.

###### `P:IoT.DriverCore.TwinCATRx.IRxTcAdsClient.IsPausedObservableAsync`

```csharp
public ReactiveUI.Primitives.Async.IObservableAsync<bool> IsPausedObservableAsync { get; }
```
Gets the async paused state stream.

- Value: The `IsPausedObservableAsync` value.

###### `P:IoT.DriverCore.TwinCATRx.IRxTcAdsClient.OnWrite`

```csharp
public System.IObservable<string> OnWrite { get; }
```
Gets the on write.

- Value: The on write.

###### `P:IoT.DriverCore.TwinCATRx.IRxTcAdsClient.OnWriteAsync`

```csharp
public ReactiveUI.Primitives.Async.IObservableAsync<string> OnWriteAsync { get; }
```
Gets the async write result stream.

- Value: The `OnWriteAsync` value.

###### `P:IoT.DriverCore.TwinCATRx.IRxTcAdsClient.ReadWriteHandleInfo`

```csharp
public System.Collections.Generic.IDictionary<string, System.Nullable<uint>> ReadWriteHandleInfo { get; }
```
Gets the read write handle information.

- Value: The read write handle information.

###### `P:IoT.DriverCore.TwinCATRx.IRxTcAdsClient.Settings`

```csharp
public IoT.DriverCore.TwinCATRx.Core.ISettings Settings { get; }
```
Gets the settings.

- Value: The settings.

###### `P:IoT.DriverCore.TwinCATRx.IRxTcAdsClient.WriteHandleInfo`

```csharp
public System.Collections.Generic.IDictionary<string, System.ValueTuple<System.Nullable<uint>, int>> WriteHandleInfo { get; }
```
Gets the write handle information.

- Value: The write handle information.

#### `T:IoT.DriverCore.TwinCATRx.InMemoryAdsClient`

```csharp
public class IoT.DriverCore.TwinCATRx.InMemoryAdsClient
```
Provides a deterministic, production-usable ADS simulator for applications that need to run without a TwinCAT runtime or physical PLC.

##### Declared public members

###### `M:IoT.DriverCore.TwinCATRx.InMemoryAdsClient.#ctor`

```csharp
public IoT.DriverCore.TwinCATRx.InMemoryAdsClient()
```
Initializes a new instance of the `T:IoT.DriverCore.TwinCATRx.InMemoryAdsClient` class.

###### `M:IoT.DriverCore.TwinCATRx.InMemoryAdsClient.Connect(IoT.DriverCore.TwinCATRx.Core.ISettings)`

```csharp
public void Connect(IoT.DriverCore.TwinCATRx.Core.ISettings settings)
```
Inherits XML documentation from its implemented or overridden member.

- Parameter `settings`: The `settings` value.

###### `M:IoT.DriverCore.TwinCATRx.InMemoryAdsClient.Disconnect`

```csharp
public void Disconnect()
```
Inherits XML documentation from its implemented or overridden member.

###### `M:IoT.DriverCore.TwinCATRx.InMemoryAdsClient.Dispose`

```csharp
public void Dispose()
```
Inherits XML documentation from its implemented or overridden member.

###### `M:IoT.DriverCore.TwinCATRx.InMemoryAdsClient.Pause(System.TimeSpan)`

```csharp
public void Pause(System.TimeSpan time)
```
Inherits XML documentation from its implemented or overridden member.

- Parameter `time`: The `time` value.

###### `M:IoT.DriverCore.TwinCATRx.InMemoryAdsClient.PublishNotifications`

```csharp
public void PublishNotifications()
```
Publishes every configured notification using the latest in-memory symbol values.

###### `M:IoT.DriverCore.TwinCATRx.InMemoryAdsClient.QueueFault(IoT.DriverCore.TwinCATRx.InMemoryAdsOperation,System.Exception)`

```csharp
public IoT.DriverCore.TwinCATRx.InMemoryAdsClient QueueFault(IoT.DriverCore.TwinCATRx.InMemoryAdsOperation operation, System.Exception error)
```
Queues a failure for the next matching operation.

- Parameter `operation`: The operation that will consume the failure.
- Parameter `error`: The error to publish.
- Returns: This simulator for fluent setup.

###### `M:IoT.DriverCore.TwinCATRx.InMemoryAdsClient.Read(System.String)`

```csharp
public void Read(string variable)
```
Inherits XML documentation from its implemented or overridden member.

- Parameter `variable`: The `variable` value.

###### `M:IoT.DriverCore.TwinCATRx.InMemoryAdsClient.Read(System.String,System.Nullable`1{System.Int32})`

```csharp
public void Read(string variable, System.Nullable<int> arrayLength)
```
Executes the `Read` operation.

- Parameter `variable`: The `variable` value.
- Parameter `arrayLength`: The `arrayLength` value.

###### `M:IoT.DriverCore.TwinCATRx.InMemoryAdsClient.Read(System.String,System.Nullable`1{System.Int32},System.String)`

```csharp
public void Read(string variable, System.Nullable<int> arrayLength, string id)
```
Executes the `Read` operation.

- Parameter `variable`: The `variable` value.
- Parameter `arrayLength`: The `arrayLength` value.
- Parameter `id`: The `id` value.

###### `M:IoT.DriverCore.TwinCATRx.InMemoryAdsClient.Read(System.String,System.String)`

```csharp
public void Read(string variable, string id)
```
Inherits XML documentation from its implemented or overridden member.

- Parameter `variable`: The `variable` value.
- Parameter `id`: The `id` value.

###### `M:IoT.DriverCore.TwinCATRx.InMemoryAdsClient.ReadMany(System.Collections.Generic.IEnumerable`1{System.String})`

```csharp
public void ReadMany(System.Collections.Generic.IEnumerable<string> variables)
```
Executes the `ReadMany` operation.

- Parameter `variables`: The `variables` value.

###### `M:IoT.DriverCore.TwinCATRx.InMemoryAdsClient.ReadMany(System.Collections.Generic.IEnumerable`1{System.String},System.String)`

```csharp
public void ReadMany(System.Collections.Generic.IEnumerable<string> variables, string correlationPrefix)
```
Executes the `ReadMany` operation.

- Parameter `variables`: The `variables` value.
- Parameter `correlationPrefix`: The `correlationPrefix` value.

###### `M:IoT.DriverCore.TwinCATRx.InMemoryAdsClient.Reconnect`

```csharp
public void Reconnect()
```
Reconnects with the latest settings while preserving registered symbols and queued faults.

###### `M:IoT.DriverCore.TwinCATRx.InMemoryAdsClient.RegisterStructure``1(System.String,``0)`

```csharp
public IoT.DriverCore.TwinCATRx.InMemoryAdsClient RegisterStructure<T>(string name, T value)
```
Registers or replaces an in-memory structure symbol.

- Parameter `name`: The root symbol name.
- Parameter `value`: The structure value.
- Returns: This simulator for fluent setup.

###### `M:IoT.DriverCore.TwinCATRx.InMemoryAdsClient.RegisterStructure``1(System.String,``0,System.Boolean,System.Boolean)`

```csharp
public IoT.DriverCore.TwinCATRx.InMemoryAdsClient RegisterStructure<T>(string name, T value, bool isReadable, bool isWritable)
```
Registers or replaces an in-memory structure symbol with full access metadata.

- Parameter `name`: The root symbol name.
- Parameter `value`: The structure value.
- Parameter `isReadable`: Whether reads are permitted.
- Parameter `isWritable`: Whether writes are permitted.
- Returns: This simulator for fluent setup.

###### `M:IoT.DriverCore.TwinCATRx.InMemoryAdsClient.RegisterSymbol(System.String,System.Object)`

```csharp
public IoT.DriverCore.TwinCATRx.InMemoryAdsClient RegisterSymbol(string name, object value)
```
Registers or replaces an in-memory ADS symbol.

- Parameter `name`: The symbol name.
- Parameter `value`: The initial value.
- Returns: This simulator for fluent setup.

###### `M:IoT.DriverCore.TwinCATRx.InMemoryAdsClient.RegisterSymbol(System.String,System.Object,System.Type)`

```csharp
public IoT.DriverCore.TwinCATRx.InMemoryAdsClient RegisterSymbol(string name, object value, System.Type dataType)
```
Registers or replaces an in-memory ADS symbol with an explicit declared type.

- Parameter `name`: The symbol name.
- Parameter `value`: The initial value.
- Parameter `dataType`: The declared type.
- Returns: This simulator for fluent setup.

###### `M:IoT.DriverCore.TwinCATRx.InMemoryAdsClient.RegisterSymbol(System.String,System.Object,System.Type,System.Int32,System.Boolean,System.Boolean)`

```csharp
public IoT.DriverCore.TwinCATRx.InMemoryAdsClient RegisterSymbol(string name, object value, System.Type dataType, int arrayLength, bool isReadable, bool isWritable)
```
Registers or replaces an in-memory ADS symbol with full access metadata.

- Parameter `name`: The symbol name.
- Parameter `value`: The initial value.
- Parameter `dataType`: The declared type.
- Parameter `arrayLength`: The array or string length, or -1 for a scalar.
- Parameter `isReadable`: Whether reads are permitted.
- Parameter `isWritable`: Whether writes are permitted.
- Returns: This simulator for fluent setup.

###### `M:IoT.DriverCore.TwinCATRx.InMemoryAdsClient.RemoveSymbol(System.String)`

```csharp
public bool RemoveSymbol(string name)
```
Removes a registered symbol and any configured handles for it.

- Parameter `name`: The symbol name.
- Returns: Whether a symbol was removed.

###### `M:IoT.DriverCore.TwinCATRx.InMemoryAdsClient.ResetOperationMetrics`

```csharp
public void ResetOperationMetrics()
```
Resets deterministic native ADS operation counts without changing simulator state.

###### `M:IoT.DriverCore.TwinCATRx.InMemoryAdsClient.SetValue(System.String,System.Object)`

```csharp
public void SetValue(string variable, object value)
```
Updates a symbol as if its value changed in the simulated PLC.

- Parameter `variable`: The variable to update.
- Parameter `value`: The new value.

###### `M:IoT.DriverCore.TwinCATRx.InMemoryAdsClient.TryGetValue``1(System.String,``0@)`

```csharp
public bool TryGetValue<T>(string variable, out T value)
```
Tries to retrieve and convert a registered symbol value.

- Parameter `variable`: The symbol name.
- Parameter `value`: The converted value when successful.
- Returns: Whether the symbol exists and its value is compatible.

###### `M:IoT.DriverCore.TwinCATRx.InMemoryAdsClient.Write(System.String,System.Object)`

```csharp
public void Write(string variable, object value)
```
Inherits XML documentation from its implemented or overridden member.

- Parameter `variable`: The `variable` value.
- Parameter `value`: The `value` value.

###### `M:IoT.DriverCore.TwinCATRx.InMemoryAdsClient.Write(System.String,System.Object,System.String)`

```csharp
public void Write(string variable, object value, string id)
```
Inherits XML documentation from its implemented or overridden member.

- Parameter `variable`: The `variable` value.
- Parameter `value`: The `value` value.
- Parameter `id`: The `id` value.

###### `M:IoT.DriverCore.TwinCATRx.InMemoryAdsClient.WriteMany(System.Collections.Generic.IEnumerable`1{System.Collections.Generic.KeyValuePair`2{System.String,System.Object}})`

```csharp
public void WriteMany(System.Collections.Generic.IEnumerable<System.Collections.Generic.KeyValuePair<string, object>> values)
```
Executes the `WriteMany` operation.

- Parameter `values`: The `values` value.

###### `M:IoT.DriverCore.TwinCATRx.InMemoryAdsClient.WriteMany(System.Collections.Generic.IEnumerable`1{System.Collections.Generic.KeyValuePair`2{System.String,System.Object}},System.String)`

```csharp
public void WriteMany(System.Collections.Generic.IEnumerable<System.Collections.Generic.KeyValuePair<string, object>> values, string correlationPrefix)
```
Executes the `WriteMany` operation.

- Parameter `values`: The `values` value.
- Parameter `correlationPrefix`: The `correlationPrefix` value.

###### `P:IoT.DriverCore.TwinCATRx.InMemoryAdsClient.Code`

```csharp
public System.IObservable<string[]> Code { get; }
```
Inherits XML documentation from its implemented or overridden member.

- Value: The `Code` value.

###### `P:IoT.DriverCore.TwinCATRx.InMemoryAdsClient.Connected`

```csharp
public bool Connected { get; }
```
Gets a value indicating whether the simulator is connected.

- Value: The `Connected` value.

###### `P:IoT.DriverCore.TwinCATRx.InMemoryAdsClient.ConnectionState`

```csharp
public IoT.DriverCore.TwinCATRx.InMemoryAdsConnectionState ConnectionState { get; }
```
Gets the current simulator connection state.

- Value: The `ConnectionState` value.

###### `P:IoT.DriverCore.TwinCATRx.InMemoryAdsClient.ConnectionStates`

```csharp
public System.IObservable<IoT.DriverCore.TwinCATRx.InMemoryAdsConnectionState> ConnectionStates { get; }
```
Gets the observable connection state stream.

- Value: The `ConnectionStates` value.

###### `P:IoT.DriverCore.TwinCATRx.InMemoryAdsClient.DataReceived`

```csharp
public System.IObservable<System.ValueTuple<string, object, string>> DataReceived { get; }
```
Inherits XML documentation from its implemented or overridden member.

- Value: The `DataReceived` value.

###### `P:IoT.DriverCore.TwinCATRx.InMemoryAdsClient.DataReceivedAsync`

```csharp
public ReactiveUI.Primitives.Async.IObservableAsync<System.ValueTuple<string, object, string>> DataReceivedAsync { get; }
```
Inherits XML documentation from its implemented or overridden member.

- Value: The `DataReceivedAsync` value.

###### `P:IoT.DriverCore.TwinCATRx.InMemoryAdsClient.ErrorReceived`

```csharp
public System.IObservable<System.Exception> ErrorReceived { get; }
```
Inherits XML documentation from its implemented or overridden member.

- Value: The `ErrorReceived` value.

###### `P:IoT.DriverCore.TwinCATRx.InMemoryAdsClient.ErrorReceivedAsync`

```csharp
public ReactiveUI.Primitives.Async.IObservableAsync<System.Exception> ErrorReceivedAsync { get; }
```
Inherits XML documentation from its implemented or overridden member.

- Value: The `ErrorReceivedAsync` value.

###### `P:IoT.DriverCore.TwinCATRx.InMemoryAdsClient.InitializeComplete`

```csharp
public System.IObservable<ReactiveUI.Primitives.RxVoid> InitializeComplete { get; }
```
Inherits XML documentation from its implemented or overridden member.

- Value: The `InitializeComplete` value.

###### `P:IoT.DriverCore.TwinCATRx.InMemoryAdsClient.InitializeCompleteAsync`

```csharp
public ReactiveUI.Primitives.Async.IObservableAsync<ReactiveUI.Primitives.RxVoid> InitializeCompleteAsync { get; }
```
Inherits XML documentation from its implemented or overridden member.

- Value: The `InitializeCompleteAsync` value.

###### `P:IoT.DriverCore.TwinCATRx.InMemoryAdsClient.IsDisposed`

```csharp
public bool IsDisposed { get; }
```
Inherits XML documentation from its implemented or overridden member.

- Value: The `IsDisposed` value.

###### `P:IoT.DriverCore.TwinCATRx.InMemoryAdsClient.IsPaused`

```csharp
public bool IsPaused { get; }
```
Inherits XML documentation from its implemented or overridden member.

- Value: The `IsPaused` value.

###### `P:IoT.DriverCore.TwinCATRx.InMemoryAdsClient.IsPausedObservable`

```csharp
public System.IObservable<bool> IsPausedObservable { get; }
```
Inherits XML documentation from its implemented or overridden member.

- Value: The `IsPausedObservable` value.

###### `P:IoT.DriverCore.TwinCATRx.InMemoryAdsClient.IsPausedObservableAsync`

```csharp
public ReactiveUI.Primitives.Async.IObservableAsync<bool> IsPausedObservableAsync { get; }
```
Inherits XML documentation from its implemented or overridden member.

- Value: The `IsPausedObservableAsync` value.

###### `P:IoT.DriverCore.TwinCATRx.InMemoryAdsClient.OnWrite`

```csharp
public System.IObservable<string> OnWrite { get; }
```
Inherits XML documentation from its implemented or overridden member.

- Value: The `OnWrite` value.

###### `P:IoT.DriverCore.TwinCATRx.InMemoryAdsClient.OnWriteAsync`

```csharp
public ReactiveUI.Primitives.Async.IObservableAsync<string> OnWriteAsync { get; }
```
Inherits XML documentation from its implemented or overridden member.

- Value: The `OnWriteAsync` value.

###### `P:IoT.DriverCore.TwinCATRx.InMemoryAdsClient.OperationMetrics`

```csharp
public IoT.DriverCore.TwinCATRx.InMemoryAdsOperationMetrics OperationMetrics { get; }
```
Gets a deterministic snapshot of native ADS operation counts.

- Value: The `OperationMetrics` value.

###### `P:IoT.DriverCore.TwinCATRx.InMemoryAdsClient.ReadWriteHandleInfo`

```csharp
public System.Collections.Generic.IDictionary<string, System.Nullable<uint>> ReadWriteHandleInfo { get; }
```
Inherits XML documentation from its implemented or overridden member.

- Value: The `ReadWriteHandleInfo` value.

###### `P:IoT.DriverCore.TwinCATRx.InMemoryAdsClient.Settings`

```csharp
public IoT.DriverCore.TwinCATRx.Core.ISettings Settings { get; }
```
Inherits XML documentation from its implemented or overridden member.

- Value: The `Settings` value.

###### `P:IoT.DriverCore.TwinCATRx.InMemoryAdsClient.Symbols`

```csharp
public System.Collections.Generic.IReadOnlyCollection<IoT.DriverCore.TwinCATRx.InMemoryAdsSymbol> Symbols { get; }
```
Gets a snapshot of all registered symbols.

- Value: The `Symbols` value.

###### `P:IoT.DriverCore.TwinCATRx.InMemoryAdsClient.WriteHandleInfo`

```csharp
public System.Collections.Generic.IDictionary<string, System.ValueTuple<System.Nullable<uint>, int>> WriteHandleInfo { get; }
```
Inherits XML documentation from its implemented or overridden member.

- Value: The `WriteHandleInfo` value.

#### `T:IoT.DriverCore.TwinCATRx.InMemoryAdsConnectionState`

```csharp
public enum IoT.DriverCore.TwinCATRx.InMemoryAdsConnectionState
```
Describes the lifecycle state of an `T:IoT.DriverCore.TwinCATRx.InMemoryAdsClient` .

##### Declared public members

###### `F:IoT.DriverCore.TwinCATRx.InMemoryAdsConnectionState.Connected`

```csharp
public static const IoT.DriverCore.TwinCATRx.InMemoryAdsConnectionState Connected
```
The simulator is ready to service reads, writes, and notifications.

###### `F:IoT.DriverCore.TwinCATRx.InMemoryAdsConnectionState.Connecting`

```csharp
public static const IoT.DriverCore.TwinCATRx.InMemoryAdsConnectionState Connecting
```
The simulator is validating settings and creating handles.

###### `F:IoT.DriverCore.TwinCATRx.InMemoryAdsConnectionState.Disconnected`

```csharp
public static const IoT.DriverCore.TwinCATRx.InMemoryAdsConnectionState Disconnected
```
The simulator is disconnected and can be connected.

###### `F:IoT.DriverCore.TwinCATRx.InMemoryAdsConnectionState.Disposed`

```csharp
public static const IoT.DriverCore.TwinCATRx.InMemoryAdsConnectionState Disposed
```
The simulator and its observable streams have been disposed.

###### `F:IoT.DriverCore.TwinCATRx.InMemoryAdsConnectionState.Faulted`

```csharp
public static const IoT.DriverCore.TwinCATRx.InMemoryAdsConnectionState Faulted
```
The latest connection attempt or simulated operation failed.

#### `T:IoT.DriverCore.TwinCATRx.InMemoryAdsException`

```csharp
public class IoT.DriverCore.TwinCATRx.InMemoryAdsException
```
Represents a deterministic in-memory ADS operation failure.

##### Declared public members

###### `M:IoT.DriverCore.TwinCATRx.InMemoryAdsException.#ctor`

```csharp
public IoT.DriverCore.TwinCATRx.InMemoryAdsException()
```
Initializes a new instance of the `T:IoT.DriverCore.TwinCATRx.InMemoryAdsException` class.

###### `M:IoT.DriverCore.TwinCATRx.InMemoryAdsException.#ctor(IoT.DriverCore.TwinCATRx.InMemoryAdsOperation,System.String)`

```csharp
public IoT.DriverCore.TwinCATRx.InMemoryAdsException(IoT.DriverCore.TwinCATRx.InMemoryAdsOperation operation, string message)
```
Initializes a new instance of the `T:IoT.DriverCore.TwinCATRx.InMemoryAdsException` class.

- Parameter `operation`: The failed operation.
- Parameter `message`: The failure message.

###### `M:IoT.DriverCore.TwinCATRx.InMemoryAdsException.#ctor(IoT.DriverCore.TwinCATRx.InMemoryAdsOperation,System.String,System.String)`

```csharp
public IoT.DriverCore.TwinCATRx.InMemoryAdsException(IoT.DriverCore.TwinCATRx.InMemoryAdsOperation operation, string message, string variable)
```
Initializes a new instance of the `T:IoT.DriverCore.TwinCATRx.InMemoryAdsException` class.

- Parameter `operation`: The failed operation.
- Parameter `message`: The failure message.
- Parameter `variable`: The optional ADS variable involved in the failure.

###### `M:IoT.DriverCore.TwinCATRx.InMemoryAdsException.#ctor(System.String)`

```csharp
public IoT.DriverCore.TwinCATRx.InMemoryAdsException(string message)
```
Initializes a new instance of the `T:IoT.DriverCore.TwinCATRx.InMemoryAdsException` class.

- Parameter `message`: The failure message.

###### `M:IoT.DriverCore.TwinCATRx.InMemoryAdsException.#ctor(System.String,System.Exception)`

```csharp
public IoT.DriverCore.TwinCATRx.InMemoryAdsException(string message, System.Exception innerException)
```
Initializes a new instance of the `T:IoT.DriverCore.TwinCATRx.InMemoryAdsException` class.

- Parameter `message`: The failure message.
- Parameter `innerException`: The failure that caused this exception.

###### `P:IoT.DriverCore.TwinCATRx.InMemoryAdsException.Operation`

```csharp
public IoT.DriverCore.TwinCATRx.InMemoryAdsOperation Operation { get; }
```
Gets the failed operation.

- Value: The `Operation` value.

###### `P:IoT.DriverCore.TwinCATRx.InMemoryAdsException.Variable`

```csharp
public string Variable { get; }
```
Gets the optional ADS variable involved in the failure.

- Value: The `Variable` value.

#### `T:IoT.DriverCore.TwinCATRx.InMemoryAdsOperation`

```csharp
public enum IoT.DriverCore.TwinCATRx.InMemoryAdsOperation
```
Identifies an operation that can receive a deterministic simulator fault.

##### Declared public members

###### `F:IoT.DriverCore.TwinCATRx.InMemoryAdsOperation.Connect`

```csharp
public static const IoT.DriverCore.TwinCATRx.InMemoryAdsOperation Connect
```
A connection or reconnection operation.

###### `F:IoT.DriverCore.TwinCATRx.InMemoryAdsOperation.Notification`

```csharp
public static const IoT.DriverCore.TwinCATRx.InMemoryAdsOperation Notification
```
A configured notification publication.

###### `F:IoT.DriverCore.TwinCATRx.InMemoryAdsOperation.Read`

```csharp
public static const IoT.DriverCore.TwinCATRx.InMemoryAdsOperation Read
```
A symbol read operation.

###### `F:IoT.DriverCore.TwinCATRx.InMemoryAdsOperation.Write`

```csharp
public static const IoT.DriverCore.TwinCATRx.InMemoryAdsOperation Write
```
A symbol write operation.

#### `T:IoT.DriverCore.TwinCATRx.InMemoryAdsOperationMetrics`

```csharp
public class IoT.DriverCore.TwinCATRx.InMemoryAdsOperationMetrics
```
Provides a deterministic snapshot of native ADS operations issued to an in-memory client.

##### Declared public members

###### `M:IoT.DriverCore.TwinCATRx.InMemoryAdsOperationMetrics.#ctor(System.Int64,System.Int64,System.Int64)`

```csharp
public IoT.DriverCore.TwinCATRx.InMemoryAdsOperationMetrics(long readOperations, long writeOperations, long notificationPublications)
```
Initializes a new instance of the `T:IoT.DriverCore.TwinCATRx.InMemoryAdsOperationMetrics` class.

- Parameter `readOperations`: The number of native read attempts.
- Parameter `writeOperations`: The number of native write attempts.
- Parameter `notificationPublications`: The number of notification publication attempts.

###### `P:IoT.DriverCore.TwinCATRx.InMemoryAdsOperationMetrics.NotificationPublications`

```csharp
public long NotificationPublications { get; }
```
Gets the number of notification publication attempts.

- Value: The `NotificationPublications` value.

###### `P:IoT.DriverCore.TwinCATRx.InMemoryAdsOperationMetrics.ReadOperations`

```csharp
public long ReadOperations { get; }
```
Gets the number of native read attempts.

- Value: The `ReadOperations` value.

###### `P:IoT.DriverCore.TwinCATRx.InMemoryAdsOperationMetrics.WriteOperations`

```csharp
public long WriteOperations { get; }
```
Gets the number of native write attempts.

- Value: The `WriteOperations` value.

#### `T:IoT.DriverCore.TwinCATRx.InMemoryAdsSymbol`

```csharp
public class IoT.DriverCore.TwinCATRx.InMemoryAdsSymbol
```
Describes one symbol hosted by an `T:IoT.DriverCore.TwinCATRx.InMemoryAdsClient` .

##### Declared public members

###### `M:IoT.DriverCore.TwinCATRx.InMemoryAdsSymbol.#ctor(System.String,System.Object,System.Type,System.Int32,System.Boolean,System.Boolean)`

```csharp
public IoT.DriverCore.TwinCATRx.InMemoryAdsSymbol(string name, object value, System.Type dataType, int arrayLength, bool isReadable, bool isWritable)
```
Initializes a new instance of the `T:IoT.DriverCore.TwinCATRx.InMemoryAdsSymbol` class.

- Parameter `name`: The case-insensitive ADS variable name.
- Parameter `value`: The initial symbol value.
- Parameter `dataType`: The declared value type.
- Parameter `arrayLength`: The declared array or string length, or -1 for a scalar.
- Parameter `isReadable`: Whether ADS reads are permitted.
- Parameter `isWritable`: Whether ADS writes are permitted.

###### `P:IoT.DriverCore.TwinCATRx.InMemoryAdsSymbol.ArrayLength`

```csharp
public int ArrayLength { get; }
```
Gets the declared array or string length, or -1 for a scalar.

- Value: The `ArrayLength` value.

###### `P:IoT.DriverCore.TwinCATRx.InMemoryAdsSymbol.DataType`

```csharp
public System.Type DataType { get; }
```
Gets the declared value type.

- Value: The `DataType` value.

###### `P:IoT.DriverCore.TwinCATRx.InMemoryAdsSymbol.IsReadable`

```csharp
public bool IsReadable { get; }
```
Gets a value indicating whether ADS reads are permitted.

- Value: The `IsReadable` value.

###### `P:IoT.DriverCore.TwinCATRx.InMemoryAdsSymbol.IsWritable`

```csharp
public bool IsWritable { get; }
```
Gets a value indicating whether ADS writes are permitted.

- Value: The `IsWritable` value.

###### `P:IoT.DriverCore.TwinCATRx.InMemoryAdsSymbol.Name`

```csharp
public string Name { get; }
```
Gets the case-insensitive ADS variable name.

- Value: The `Name` value.

###### `P:IoT.DriverCore.TwinCATRx.InMemoryAdsSymbol.Value`

```csharp
public object Value { get; }
```
Gets the current symbol value.

- Value: The `Value` value.

#### `T:IoT.DriverCore.TwinCATRx.ObservableBridgeExtensions`

```csharp
public class IoT.DriverCore.TwinCATRx.ObservableBridgeExtensions
```
Observable bridge helpers.

##### Declared public members

###### `M:IoT.DriverCore.TwinCATRx.ObservableBridgeExtensions.SubscribeTo``1(System.IObservable`1{``0})`

```csharp
public static System.IDisposable SubscribeTo<T>(System.IObservable<T> source)
```
Executes the `SubscribeTo` operation.

- Parameter `source`: The `source` value.
- Returns: A `System.IDisposable` result.

###### `M:IoT.DriverCore.TwinCATRx.ObservableBridgeExtensions.SubscribeTo``1(System.IObservable`1{``0},System.Action`1{``0})`

```csharp
public static System.IDisposable SubscribeTo<T>(System.IObservable<T> source, System.Action<T> onNext)
```
Executes the `SubscribeTo` operation.

- Parameter `source`: The `source` value.
- Parameter `onNext`: The `onNext` value.
- Returns: A `System.IDisposable` result.

###### `M:IoT.DriverCore.TwinCATRx.ObservableBridgeExtensions.SubscribeTo``1(System.IObservable`1{``0},System.Action`1{``0},System.Action`1{System.Exception},System.Action)`

```csharp
public static System.IDisposable SubscribeTo<T>(System.IObservable<T> source, System.Action<T> onNext, System.Action<System.Exception> onError, System.Action onCompleted)
```
Executes the `SubscribeTo` operation.

- Parameter `source`: The `source` value.
- Parameter `onNext`: The `onNext` value.
- Parameter `onError`: The `onError` value.
- Parameter `onCompleted`: The `onCompleted` value.
- Returns: A `System.IDisposable` result.

###### `M:IoT.DriverCore.TwinCATRx.ObservableBridgeExtensions.ToAsyncObservable``1(System.IObservable`1{``0})`

```csharp
public static ReactiveUI.Primitives.Async.IObservableAsync<T> ToAsyncObservable<T>(System.IObservable<T> source)
```
Executes the `ToAsyncObservable` operation.

- Parameter `source`: The `source` value.
- Returns: A `ReactiveUI.Primitives.Async.IObservableAsync<T>` result.

#### `T:IoT.DriverCore.TwinCATRx.ObservableServiceController`

```csharp
public class IoT.DriverCore.TwinCATRx.ObservableServiceController
```
Observable Service Controller.

##### Declared public members

###### `M:IoT.DriverCore.TwinCATRx.ObservableServiceController.#ctor(System.ServiceProcess.ServiceController)`

```csharp
public IoT.DriverCore.TwinCATRx.ObservableServiceController(System.ServiceProcess.ServiceController service)
```
Initializes a new instance of the `T:IoT.DriverCore.TwinCATRx.ObservableServiceController` class.

- Parameter `service`: The service.

###### `M:IoT.DriverCore.TwinCATRx.ObservableServiceController.#ctor(System.ServiceProcess.ServiceController,System.TimeSpan)`

```csharp
public IoT.DriverCore.TwinCATRx.ObservableServiceController(System.ServiceProcess.ServiceController service, System.TimeSpan interval)
```
Initializes a new instance of the `T:IoT.DriverCore.TwinCATRx.ObservableServiceController` class.

- Parameter `service`: The service.
- Parameter `interval`: The interval.

###### `M:IoT.DriverCore.TwinCATRx.ObservableServiceController.Dispose`

```csharp
public void Dispose()
```
Releases managed and unmanaged resources.

###### `M:IoT.DriverCore.TwinCATRx.ObservableServiceController.GetServices`

```csharp
public static System.IObservable<IoT.DriverCore.TwinCATRx.ObservableServiceController> GetServices()
```
Gets the services.

- Returns: A Value.

###### `M:IoT.DriverCore.TwinCATRx.ObservableServiceController.Restart`

```csharp
public void Restart()
```
Restarts this instance.

###### `M:IoT.DriverCore.TwinCATRx.ObservableServiceController.Start`

```csharp
public void Start()
```
Starts this instance.

###### `M:IoT.DriverCore.TwinCATRx.ObservableServiceController.Stop`

```csharp
public void Stop()
```
Stops this instance.

###### `P:IoT.DriverCore.TwinCATRx.ObservableServiceController.CanStop`

```csharp
public bool CanStop { get; }
```
Gets a value indicating whether this instance can stop.

- Value: true if this instance can stop; otherwise, false .

###### `P:IoT.DriverCore.TwinCATRx.ObservableServiceController.DisplayName`

```csharp
public string DisplayName { get; }
```
Gets the display name.

- Value: The display name.

###### `P:IoT.DriverCore.TwinCATRx.ObservableServiceController.IsDisposed`

```csharp
public bool IsDisposed { get; }
```
Gets a value indicating whether the is disposed.

- Value: The `IsDisposed` value.

###### `P:IoT.DriverCore.TwinCATRx.ObservableServiceController.ServiceName`

```csharp
public string ServiceName { get; }
```
Gets the name of the service.

- Value: The name of the service.

###### `P:IoT.DriverCore.TwinCATRx.ObservableServiceController.Status`

```csharp
public System.ServiceProcess.ServiceControllerStatus Status { get; }
```
Gets the status.

- Value: The status.

###### `P:IoT.DriverCore.TwinCATRx.ObservableServiceController.StatusObserver`

```csharp
public System.IObservable<System.ServiceProcess.ServiceControllerStatus> StatusObserver { get; }
```
Gets the status.

- Value: The status.

#### `T:IoT.DriverCore.TwinCATRx.RxTcAdsClient`

```csharp
public class IoT.DriverCore.TwinCATRx.RxTcAdsClient
```
Observable TwinCAT ADS Client.

##### Declared public members

###### `M:IoT.DriverCore.TwinCATRx.RxTcAdsClient.#ctor`

```csharp
public IoT.DriverCore.TwinCATRx.RxTcAdsClient()
```
Initializes a new instance of the `T:IoT.DriverCore.TwinCATRx.RxTcAdsClient` class.

###### `M:IoT.DriverCore.TwinCATRx.RxTcAdsClient.#ctor(System.TimeProvider)`

```csharp
public IoT.DriverCore.TwinCATRx.RxTcAdsClient(System.TimeProvider timeProvider)
```
Initializes a new instance of the `T:IoT.DriverCore.TwinCATRx.RxTcAdsClient` class.

- Parameter `timeProvider`: The time provider.

###### `M:IoT.DriverCore.TwinCATRx.RxTcAdsClient.Connect(IoT.DriverCore.TwinCATRx.Core.ISettings)`

```csharp
public void Connect(IoT.DriverCore.TwinCATRx.Core.ISettings settings)
```
Connects the specified settings.

- Parameter `settings`: The settings.

###### `M:IoT.DriverCore.TwinCATRx.RxTcAdsClient.Disconnect`

```csharp
public void Disconnect()
```
Disconnects this instance.

###### `M:IoT.DriverCore.TwinCATRx.RxTcAdsClient.Dispose`

```csharp
public void Dispose()
```
Releases unmanaged and - optionally - managed resources.

###### `M:IoT.DriverCore.TwinCATRx.RxTcAdsClient.Pause(System.TimeSpan)`

```csharp
public void Pause(System.TimeSpan time)
```
Pauses the specified time.

- Parameter `time`: The time.

###### `M:IoT.DriverCore.TwinCATRx.RxTcAdsClient.Read(System.String)`

```csharp
public void Read(string variable)
```
Reads the specified variable.

- Parameter `variable`: The data.

###### `M:IoT.DriverCore.TwinCATRx.RxTcAdsClient.Read(System.String,System.Nullable`1{System.Int32})`

```csharp
public void Read(string variable, System.Nullable<int> arrayLength)
```
Executes the `Read` operation.

- Parameter `variable`: The `variable` value.
- Parameter `arrayLength`: The `arrayLength` value.

###### `M:IoT.DriverCore.TwinCATRx.RxTcAdsClient.Read(System.String,System.Nullable`1{System.Int32},System.String)`

```csharp
public void Read(string variable, System.Nullable<int> arrayLength, string id)
```
Executes the `Read` operation.

- Parameter `variable`: The `variable` value.
- Parameter `arrayLength`: The `arrayLength` value.
- Parameter `id`: The `id` value.

###### `M:IoT.DriverCore.TwinCATRx.RxTcAdsClient.Read(System.String,System.String)`

```csharp
public void Read(string variable, string id)
```
Reads a variable with a correlation identifier.

- Parameter `variable`: The variable.
- Parameter `id`: The correlation identifier.

###### `M:IoT.DriverCore.TwinCATRx.RxTcAdsClient.Write(System.String,System.Object)`

```csharp
public void Write(string variable, object value)
```
Writes the specified variable.

- Parameter `variable`: The variable.
- Parameter `value`: The value.

###### `M:IoT.DriverCore.TwinCATRx.RxTcAdsClient.Write(System.String,System.Object,System.String)`

```csharp
public void Write(string variable, object value, string id)
```
Writes a variable with a correlation identifier.

- Parameter `variable`: The variable.
- Parameter `value`: The value.
- Parameter `id`: The correlation identifier.

###### `P:IoT.DriverCore.TwinCATRx.RxTcAdsClient.Code`

```csharp
public System.IObservable<string[]> Code { get; }
```
Gets codes this instance.

- Returns: A Value.
- Value: The `Code` value.

###### `P:IoT.DriverCore.TwinCATRx.RxTcAdsClient.Connected`

```csharp
public bool Connected { get; }
```
Gets a value indicating whether this `T:IoT.DriverCore.TwinCATRx.RxTcAdsClient` is connected.

- Value: true if connected; otherwise, false .

###### `P:IoT.DriverCore.TwinCATRx.RxTcAdsClient.DataReceived`

```csharp
public System.IObservable<System.ValueTuple<string, object, string>> DataReceived { get; }
```
Gets the data received.

- Value: The data received.

###### `P:IoT.DriverCore.TwinCATRx.RxTcAdsClient.DataReceivedAsync`

```csharp
public ReactiveUI.Primitives.Async.IObservableAsync<System.ValueTuple<string, object, string>> DataReceivedAsync { get; }
```
Inherits XML documentation from its implemented or overridden member.

- Value: The `DataReceivedAsync` value.

###### `P:IoT.DriverCore.TwinCATRx.RxTcAdsClient.ErrorReceived`

```csharp
public System.IObservable<System.Exception> ErrorReceived { get; }
```
Gets error received.

- Returns: A Value.
- Value: The `ErrorReceived` value.

###### `P:IoT.DriverCore.TwinCATRx.RxTcAdsClient.ErrorReceivedAsync`

```csharp
public ReactiveUI.Primitives.Async.IObservableAsync<System.Exception> ErrorReceivedAsync { get; }
```
Inherits XML documentation from its implemented or overridden member.

- Value: The `ErrorReceivedAsync` value.

###### `P:IoT.DriverCore.TwinCATRx.RxTcAdsClient.InitializeComplete`

```csharp
public System.IObservable<ReactiveUI.Primitives.RxVoid> InitializeComplete { get; }
```
Gets the initialize complete. PLC is ready to read and write.

- Value: The initialize complete.

###### `P:IoT.DriverCore.TwinCATRx.RxTcAdsClient.InitializeCompleteAsync`

```csharp
public ReactiveUI.Primitives.Async.IObservableAsync<ReactiveUI.Primitives.RxVoid> InitializeCompleteAsync { get; }
```
Inherits XML documentation from its implemented or overridden member.

- Value: The `InitializeCompleteAsync` value.

###### `P:IoT.DriverCore.TwinCATRx.RxTcAdsClient.IsDisposed`

```csharp
public bool IsDisposed { get; }
```
Gets a value indicating whether gets a value that indicates whether the object is disposed.

- Value: The `IsDisposed` value.

###### `P:IoT.DriverCore.TwinCATRx.RxTcAdsClient.IsPaused`

```csharp
public bool IsPaused { get; }
```
Gets a value indicating whether this instance is paused.

- Value: true if this instance is paused; otherwise, false .

###### `P:IoT.DriverCore.TwinCATRx.RxTcAdsClient.IsPausedObservable`

```csharp
public System.IObservable<bool> IsPausedObservable { get; }
```
Gets the is paused observable.

- Value: The is paused observable.

###### `P:IoT.DriverCore.TwinCATRx.RxTcAdsClient.IsPausedObservableAsync`

```csharp
public ReactiveUI.Primitives.Async.IObservableAsync<bool> IsPausedObservableAsync { get; }
```
Inherits XML documentation from its implemented or overridden member.

- Value: The `IsPausedObservableAsync` value.

###### `P:IoT.DriverCore.TwinCATRx.RxTcAdsClient.OnWrite`

```csharp
public System.IObservable<string> OnWrite { get; }
```
Gets the on write.

- Value: The on write.

###### `P:IoT.DriverCore.TwinCATRx.RxTcAdsClient.OnWriteAsync`

```csharp
public ReactiveUI.Primitives.Async.IObservableAsync<string> OnWriteAsync { get; }
```
Inherits XML documentation from its implemented or overridden member.

- Value: The `OnWriteAsync` value.

###### `P:IoT.DriverCore.TwinCATRx.RxTcAdsClient.ReadWriteHandleInfo`

```csharp
public System.Collections.Generic.IDictionary<string, System.Nullable<uint>> ReadWriteHandleInfo { get; }
```
Gets the read write handle information.

- Value: The read write handle information.

###### `P:IoT.DriverCore.TwinCATRx.RxTcAdsClient.Settings`

```csharp
public IoT.DriverCore.TwinCATRx.Core.ISettings Settings { get; }
```
Gets the settings.

- Value: The settings.

###### `P:IoT.DriverCore.TwinCATRx.RxTcAdsClient.WriteHandleInfo`

```csharp
public System.Collections.Generic.IDictionary<string, System.ValueTuple<System.Nullable<uint>, int>> WriteHandleInfo { get; }
```
Gets the write handle information.

- Value: The write handle information.

#### `T:IoT.DriverCore.TwinCATRx.ServiceStatus`

```csharp
public enum IoT.DriverCore.TwinCATRx.ServiceStatus
```
Service Status.

##### Declared public members

###### `F:IoT.DriverCore.TwinCATRx.ServiceStatus.Faulted`

```csharp
public static const IoT.DriverCore.TwinCATRx.ServiceStatus Faulted
```
The faulted.

###### `F:IoT.DriverCore.TwinCATRx.ServiceStatus.Paused`

```csharp
public static const IoT.DriverCore.TwinCATRx.ServiceStatus Paused
```
The paused.

###### `F:IoT.DriverCore.TwinCATRx.ServiceStatus.Running`

```csharp
public static const IoT.DriverCore.TwinCATRx.ServiceStatus Running
```
The running.

###### `F:IoT.DriverCore.TwinCATRx.ServiceStatus.Starting`

```csharp
public static const IoT.DriverCore.TwinCATRx.ServiceStatus Starting
```
The starting.

###### `F:IoT.DriverCore.TwinCATRx.ServiceStatus.StatusChanging`

```csharp
public static const IoT.DriverCore.TwinCATRx.ServiceStatus StatusChanging
```
The status changing.

###### `F:IoT.DriverCore.TwinCATRx.ServiceStatus.Stopped`

```csharp
public static const IoT.DriverCore.TwinCATRx.ServiceStatus Stopped
```
The stopped.

###### `F:IoT.DriverCore.TwinCATRx.ServiceStatus.Stopping`

```csharp
public static const IoT.DriverCore.TwinCATRx.ServiceStatus Stopping
```
The stopping.

###### `F:IoT.DriverCore.TwinCATRx.ServiceStatus.Unknown`

```csharp
public static const IoT.DriverCore.TwinCATRx.ServiceStatus Unknown
```
The unknown.

#### `T:IoT.DriverCore.TwinCATRx.TwinCatLogicalTagClient`

```csharp
public class IoT.DriverCore.TwinCATRx.TwinCatLogicalTagClient
```
Maps logical CP.IoT tags onto an event-driven TwinCAT ADS client.

##### Declared public members

###### `M:IoT.DriverCore.TwinCATRx.TwinCatLogicalTagClient.#ctor(IoT.DriverCore.TwinCATRx.IRxTcAdsClient)`

```csharp
public IoT.DriverCore.TwinCATRx.TwinCatLogicalTagClient(IoT.DriverCore.TwinCATRx.IRxTcAdsClient nativeClient)
```
Initializes a new instance of the `T:IoT.DriverCore.TwinCATRx.TwinCatLogicalTagClient` class.

- Parameter `nativeClient`: The composed ADS client.

###### `M:IoT.DriverCore.TwinCATRx.TwinCatLogicalTagClient.#ctor(IoT.DriverCore.TwinCATRx.IRxTcAdsClient,IoT.DriverCore.Core.ILogicalTagCatalog)`

```csharp
public IoT.DriverCore.TwinCATRx.TwinCatLogicalTagClient(IoT.DriverCore.TwinCATRx.IRxTcAdsClient nativeClient, IoT.DriverCore.Core.ILogicalTagCatalog catalog)
```
Initializes a new instance of the `T:IoT.DriverCore.TwinCATRx.TwinCatLogicalTagClient` class.

- Parameter `nativeClient`: The composed ADS client.
- Parameter `catalog`: The caller-owned catalog.

###### `M:IoT.DriverCore.TwinCATRx.TwinCatLogicalTagClient.#ctor(IoT.DriverCore.TwinCATRx.IRxTcAdsClient,IoT.DriverCore.Core.ILogicalTagCatalog,IoT.DriverCore.Core.LogicalTagSqliteStore)`

```csharp
public IoT.DriverCore.TwinCATRx.TwinCatLogicalTagClient(IoT.DriverCore.TwinCATRx.IRxTcAdsClient nativeClient, IoT.DriverCore.Core.ILogicalTagCatalog catalog, IoT.DriverCore.Core.LogicalTagSqliteStore store)
```
Initializes a new instance of the `T:IoT.DriverCore.TwinCATRx.TwinCatLogicalTagClient` class.

- Parameter `nativeClient`: The composed ADS client.
- Parameter `catalog`: The caller-owned catalog.
- Parameter `store`: The SQLite store.

###### `M:IoT.DriverCore.TwinCATRx.TwinCatLogicalTagClient.#ctor(IoT.DriverCore.TwinCATRx.IRxTcAdsClient,IoT.DriverCore.Core.ILogicalTagCatalog,IoT.DriverCore.Core.LogicalTagSqliteStore,System.TimeProvider)`

```csharp
public IoT.DriverCore.TwinCATRx.TwinCatLogicalTagClient(IoT.DriverCore.TwinCATRx.IRxTcAdsClient nativeClient, IoT.DriverCore.Core.ILogicalTagCatalog catalog, IoT.DriverCore.Core.LogicalTagSqliteStore store, System.TimeProvider timeProvider)
```
Initializes a new instance of the `T:IoT.DriverCore.TwinCATRx.TwinCatLogicalTagClient` class.

- Parameter `nativeClient`: The composed ADS client.
- Parameter `catalog`: The caller-owned catalog.
- Parameter `store`: The SQLite store.
- Parameter `timeProvider`: The time provider.

###### `M:IoT.DriverCore.TwinCATRx.TwinCatLogicalTagClient.#ctor(IoT.DriverCore.TwinCATRx.IRxTcAdsClient,IoT.DriverCore.Core.ILogicalTagCatalog,System.TimeProvider)`

```csharp
public IoT.DriverCore.TwinCATRx.TwinCatLogicalTagClient(IoT.DriverCore.TwinCATRx.IRxTcAdsClient nativeClient, IoT.DriverCore.Core.ILogicalTagCatalog catalog, System.TimeProvider timeProvider)
```
Initializes a new instance of the `T:IoT.DriverCore.TwinCATRx.TwinCatLogicalTagClient` class.

- Parameter `nativeClient`: The composed ADS client.
- Parameter `catalog`: The caller-owned catalog.
- Parameter `timeProvider`: The time provider.

###### `M:IoT.DriverCore.TwinCATRx.TwinCatLogicalTagClient.#ctor(IoT.DriverCore.TwinCATRx.IRxTcAdsClient,IoT.DriverCore.Core.LogicalTagSqliteStore)`

```csharp
public IoT.DriverCore.TwinCATRx.TwinCatLogicalTagClient(IoT.DriverCore.TwinCATRx.IRxTcAdsClient nativeClient, IoT.DriverCore.Core.LogicalTagSqliteStore store)
```
Initializes a new instance of the `T:IoT.DriverCore.TwinCATRx.TwinCatLogicalTagClient` class.

- Parameter `nativeClient`: The composed ADS client.
- Parameter `store`: The SQLite store.

###### `M:IoT.DriverCore.TwinCATRx.TwinCatLogicalTagClient.#ctor(IoT.DriverCore.TwinCATRx.IRxTcAdsClient,IoT.DriverCore.Core.LogicalTagSqliteStore,System.TimeProvider)`

```csharp
public IoT.DriverCore.TwinCATRx.TwinCatLogicalTagClient(IoT.DriverCore.TwinCATRx.IRxTcAdsClient nativeClient, IoT.DriverCore.Core.LogicalTagSqliteStore store, System.TimeProvider timeProvider)
```
Initializes a new instance of the `T:IoT.DriverCore.TwinCATRx.TwinCatLogicalTagClient` class.

- Parameter `nativeClient`: The composed ADS client.
- Parameter `store`: The SQLite store.
- Parameter `timeProvider`: The time provider.

###### `M:IoT.DriverCore.TwinCATRx.TwinCatLogicalTagClient.#ctor(IoT.DriverCore.TwinCATRx.IRxTcAdsClient,System.TimeProvider)`

```csharp
public IoT.DriverCore.TwinCATRx.TwinCatLogicalTagClient(IoT.DriverCore.TwinCATRx.IRxTcAdsClient nativeClient, System.TimeProvider timeProvider)
```
Initializes a new instance of the `T:IoT.DriverCore.TwinCATRx.TwinCatLogicalTagClient` class.

- Parameter `nativeClient`: The composed ADS client.
- Parameter `timeProvider`: The time provider.

###### `M:IoT.DriverCore.TwinCATRx.TwinCatLogicalTagClient.CreateTag(IoT.DriverCore.Core.LogicalTag)`

```csharp
public IoT.DriverCore.Core.LogicalTag CreateTag(IoT.DriverCore.Core.LogicalTag tag)
```
Creates a tag from a complete shared tag definition and registers it.

- Parameter `tag`: The complete shared tag definition.
- Returns: The registered tag.

###### `M:IoT.DriverCore.TwinCATRx.TwinCatLogicalTagClient.CreateTag(System.String,System.String,System.String)`

```csharp
public IoT.DriverCore.Core.LogicalTag CreateTag(string name, string address, string dataType)
```
Creates and registers a logical TwinCAT tag.

- Parameter `name`: The logical name.
- Parameter `address`: The ADS address.
- Parameter `dataType`: The logical data type.
- Returns: The registered tag.

###### `M:IoT.DriverCore.TwinCATRx.TwinCatLogicalTagClient.DeleteGroupAsync(System.String,System.Threading.CancellationToken)`

```csharp
public System.Threading.Tasks.Task<bool> DeleteGroupAsync(string name, System.Threading.CancellationToken cancellationToken)
```
Inherits XML documentation from its implemented or overridden member.

- Parameter `name`: The `name` value.
- Parameter `cancellationToken`: The `cancellationToken` value.
- Returns: A `System.Threading.Tasks.Task<bool>` result.

###### `M:IoT.DriverCore.TwinCATRx.TwinCatLogicalTagClient.DeleteTagAsync(System.String)`

```csharp
public System.Threading.Tasks.Task<bool> DeleteTagAsync(string name)
```
Deletes a tag from SQLite and the live registry.

- Parameter `name`: The logical tag name.
- Returns: Whether an existing tag was deleted.

###### `M:IoT.DriverCore.TwinCATRx.TwinCatLogicalTagClient.DeleteTagAsync(System.String,System.Threading.CancellationToken)`

```csharp
public System.Threading.Tasks.Task<bool> DeleteTagAsync(string name, System.Threading.CancellationToken cancellationToken)
```
Deletes a tag from SQLite and the live registry.

- Parameter `name`: The logical tag name.
- Parameter `cancellationToken`: The cancellation token.
- Returns: Whether an existing tag was deleted.

###### `M:IoT.DriverCore.TwinCATRx.TwinCatLogicalTagClient.Dispose`

```csharp
public void Dispose()
```
Releases registry-owned resources without disposing the composed ADS client.

###### `M:IoT.DriverCore.TwinCATRx.TwinCatLogicalTagClient.EditTagAsync(IoT.DriverCore.Core.LogicalTag)`

```csharp
public System.Threading.Tasks.Task<bool> EditTagAsync(IoT.DriverCore.Core.LogicalTag tag)
```
Edits an existing SQLite tag and refreshes the live registry.

- Parameter `tag`: The logical tag.
- Returns: Whether an existing tag was edited.

###### `M:IoT.DriverCore.TwinCATRx.TwinCatLogicalTagClient.EditTagAsync(IoT.DriverCore.Core.LogicalTag,System.Threading.CancellationToken)`

```csharp
public System.Threading.Tasks.Task<bool> EditTagAsync(IoT.DriverCore.Core.LogicalTag tag, System.Threading.CancellationToken cancellationToken)
```
Edits an existing SQLite tag and refreshes the live registry.

- Parameter `tag`: The logical tag.
- Parameter `cancellationToken`: The cancellation token.
- Returns: Whether an existing tag was edited.

###### `M:IoT.DriverCore.TwinCATRx.TwinCatLogicalTagClient.ExportCsvAsync(System.IO.TextWriter)`

```csharp
public System.Threading.Tasks.Task ExportCsvAsync(System.IO.TextWriter writer)
```
Exports the live registry as CSV.

- Parameter `writer`: The CSV writer.
- Returns: The export operation.

###### `M:IoT.DriverCore.TwinCATRx.TwinCatLogicalTagClient.ExportCsvAsync(System.IO.TextWriter,System.Char,System.Threading.CancellationToken)`

```csharp
public System.Threading.Tasks.Task ExportCsvAsync(System.IO.TextWriter writer, char delimiter, System.Threading.CancellationToken cancellationToken)
```
Inherits XML documentation from its implemented or overridden member.

- Parameter `writer`: The `writer` value.
- Parameter `delimiter`: The `delimiter` value.
- Parameter `cancellationToken`: The `cancellationToken` value.
- Returns: A `System.Threading.Tasks.Task` result.

###### `M:IoT.DriverCore.TwinCATRx.TwinCatLogicalTagClient.ExportCsvAsync(System.IO.TextWriter,System.Threading.CancellationToken)`

```csharp
public System.Threading.Tasks.Task ExportCsvAsync(System.IO.TextWriter writer, System.Threading.CancellationToken cancellationToken)
```
Exports the live registry as CSV.

- Parameter `writer`: The CSV writer.
- Parameter `cancellationToken`: The cancellation token.
- Returns: The export operation.

###### `M:IoT.DriverCore.TwinCATRx.TwinCatLogicalTagClient.GetGroupAsync(System.String,System.Threading.CancellationToken)`

```csharp
public System.Threading.Tasks.Task<IoT.DriverCore.Core.LogicalTagGroup> GetGroupAsync(string name, System.Threading.CancellationToken cancellationToken)
```
Inherits XML documentation from its implemented or overridden member.

- Parameter `name`: The `name` value.
- Parameter `cancellationToken`: The `cancellationToken` value.
- Returns: A `System.Threading.Tasks.Task<IoT.DriverCore.Core.LogicalTagGroup>` result.

###### `M:IoT.DriverCore.TwinCATRx.TwinCatLogicalTagClient.GetTagAsync(System.String)`

```csharp
public System.Threading.Tasks.Task<IoT.DriverCore.Core.LogicalTag> GetTagAsync(string name)
```
Gets a persisted tag.

- Parameter `name`: The logical tag name.
- Returns: The persisted tag, or null.

###### `M:IoT.DriverCore.TwinCATRx.TwinCatLogicalTagClient.GetTagAsync(System.String,System.Threading.CancellationToken)`

```csharp
public System.Threading.Tasks.Task<IoT.DriverCore.Core.LogicalTag> GetTagAsync(string name, System.Threading.CancellationToken cancellationToken)
```
Gets a persisted tag.

- Parameter `name`: The logical tag name.
- Parameter `cancellationToken`: The cancellation token.
- Returns: The persisted tag, or null.

###### `M:IoT.DriverCore.TwinCATRx.TwinCatLogicalTagClient.ImportCsvAsync(System.IO.TextReader)`

```csharp
public System.Threading.Tasks.Task<System.Collections.Generic.IReadOnlyList<IoT.DriverCore.Core.LogicalTag>> ImportCsvAsync(System.IO.TextReader reader)
```
Imports CSV definitions into the live registry.

- Parameter `reader`: The CSV reader.
- Returns: The imported tags.

###### `M:IoT.DriverCore.TwinCATRx.TwinCatLogicalTagClient.ImportCsvAsync(System.IO.TextReader,System.Boolean)`

```csharp
public System.Threading.Tasks.Task<System.Collections.Generic.IReadOnlyList<IoT.DriverCore.Core.LogicalTag>> ImportCsvAsync(System.IO.TextReader reader, bool replaceExisting)
```
Imports CSV definitions into the live registry.

- Parameter `reader`: The CSV reader.
- Parameter `replaceExisting`: Whether imported tags replace matching live tags.
- Returns: The imported tags.

###### `M:IoT.DriverCore.TwinCATRx.TwinCatLogicalTagClient.ImportCsvAsync(System.IO.TextReader,System.Boolean,System.Threading.CancellationToken)`

```csharp
public System.Threading.Tasks.Task<System.Collections.Generic.IReadOnlyList<IoT.DriverCore.Core.LogicalTag>> ImportCsvAsync(System.IO.TextReader reader, bool replaceExisting, System.Threading.CancellationToken cancellationToken)
```
Imports CSV definitions into the live registry.

- Parameter `reader`: The CSV reader.
- Parameter `replaceExisting`: Whether imported tags replace matching live tags.
- Parameter `cancellationToken`: The cancellation token.
- Returns: The imported tags.

###### `M:IoT.DriverCore.TwinCATRx.TwinCatLogicalTagClient.ImportCsvAsync(System.IO.TextReader,System.Char,System.Boolean,System.Threading.CancellationToken)`

```csharp
public System.Threading.Tasks.Task<System.Collections.Generic.IReadOnlyList<IoT.DriverCore.Core.LogicalTag>> ImportCsvAsync(System.IO.TextReader reader, char delimiter, bool replaceExisting, System.Threading.CancellationToken cancellationToken)
```
Imports CSV definitions into the live registry.

- Parameter `reader`: The CSV reader.
- Parameter `delimiter`: The CSV delimiter.
- Parameter `replaceExisting`: Whether imported tags replace matching live tags.
- Parameter `cancellationToken`: The cancellation token.
- Returns: The imported tags.

###### `M:IoT.DriverCore.TwinCATRx.TwinCatLogicalTagClient.ImportCsvAsync(System.IO.TextReader,System.Char,System.Threading.CancellationToken)`

```csharp
public System.Threading.Tasks.Task<System.Collections.Generic.IReadOnlyList<IoT.DriverCore.Core.LogicalTag>> ImportCsvAsync(System.IO.TextReader reader, char delimiter, System.Threading.CancellationToken cancellationToken)
```
Inherits XML documentation from its implemented or overridden member.

- Parameter `reader`: The `reader` value.
- Parameter `delimiter`: The `delimiter` value.
- Parameter `cancellationToken`: The `cancellationToken` value.
- Returns: A `System.Threading.Tasks.Task<System.Collections.Generic.IReadOnlyList<IoT.DriverCore.Core.LogicalTag>>` result.

###### `M:IoT.DriverCore.TwinCATRx.TwinCatLogicalTagClient.InitializeStoreAsync`

```csharp
public System.Threading.Tasks.Task InitializeStoreAsync()
```
Initializes the configured SQLite store.

- Returns: The initialization operation.

###### `M:IoT.DriverCore.TwinCATRx.TwinCatLogicalTagClient.InitializeStoreAsync(System.Threading.CancellationToken)`

```csharp
public System.Threading.Tasks.Task InitializeStoreAsync(System.Threading.CancellationToken cancellationToken)
```
Initializes the configured SQLite store.

- Parameter `cancellationToken`: The cancellation token.
- Returns: The initialization operation.

###### `M:IoT.DriverCore.TwinCATRx.TwinCatLogicalTagClient.ListGroupsAsync(System.Threading.CancellationToken)`

```csharp
public System.Threading.Tasks.Task<System.Collections.Generic.IReadOnlyList<IoT.DriverCore.Core.LogicalTagGroup>> ListGroupsAsync(System.Threading.CancellationToken cancellationToken)
```
Inherits XML documentation from its implemented or overridden member.

- Parameter `cancellationToken`: The `cancellationToken` value.
- Returns: A `System.Threading.Tasks.Task<System.Collections.Generic.IReadOnlyList<IoT.DriverCore.Core.LogicalTagGroup>>` result.

###### `M:IoT.DriverCore.TwinCATRx.TwinCatLogicalTagClient.ListTagsAsync(System.Threading.CancellationToken)`

```csharp
public System.Threading.Tasks.Task<System.Collections.Generic.IReadOnlyList<IoT.DriverCore.Core.LogicalTag>> ListTagsAsync(System.Threading.CancellationToken cancellationToken)
```
Inherits XML documentation from its implemented or overridden member.

- Parameter `cancellationToken`: The `cancellationToken` value.
- Returns: A `System.Threading.Tasks.Task<System.Collections.Generic.IReadOnlyList<IoT.DriverCore.Core.LogicalTag>>` result.

###### `M:IoT.DriverCore.TwinCATRx.TwinCatLogicalTagClient.LoadTagsAsync`

```csharp
public System.Threading.Tasks.Task<System.Collections.Generic.IReadOnlyList<IoT.DriverCore.Core.LogicalTag>> LoadTagsAsync()
```
Dynamically loads persisted tags into the live registry.

- Returns: The loaded tags.

###### `M:IoT.DriverCore.TwinCATRx.TwinCatLogicalTagClient.LoadTagsAsync(System.Boolean)`

```csharp
public System.Threading.Tasks.Task<System.Collections.Generic.IReadOnlyList<IoT.DriverCore.Core.LogicalTag>> LoadTagsAsync(bool replaceExisting)
```
Dynamically loads persisted tags into the live registry.

- Parameter `replaceExisting`: Whether persisted tags replace matching live tags.
- Returns: The loaded tags.

###### `M:IoT.DriverCore.TwinCATRx.TwinCatLogicalTagClient.LoadTagsAsync(System.Boolean,System.Threading.CancellationToken)`

```csharp
public System.Threading.Tasks.Task<System.Collections.Generic.IReadOnlyList<IoT.DriverCore.Core.LogicalTag>> LoadTagsAsync(bool replaceExisting, System.Threading.CancellationToken cancellationToken)
```
Dynamically loads persisted tags into the live registry.

- Parameter `replaceExisting`: Whether persisted tags replace matching live tags.
- Parameter `cancellationToken`: The cancellation token.
- Returns: The loaded tags.

###### `M:IoT.DriverCore.TwinCATRx.TwinCatLogicalTagClient.LoadTagsAsync(System.Threading.CancellationToken)`

```csharp
public System.Threading.Tasks.Task<System.Collections.Generic.IReadOnlyList<IoT.DriverCore.Core.LogicalTag>> LoadTagsAsync(System.Threading.CancellationToken cancellationToken)
```
Inherits XML documentation from its implemented or overridden member.

- Parameter `cancellationToken`: The `cancellationToken` value.
- Returns: A `System.Threading.Tasks.Task<System.Collections.Generic.IReadOnlyList<IoT.DriverCore.Core.LogicalTag>>` result.

###### `M:IoT.DriverCore.TwinCATRx.TwinCatLogicalTagClient.Observe(System.String)`

```csharp
public System.IObservable<IoT.DriverCore.Core.LogicalTagValue> Observe(string tagName)
```
Inherits XML documentation from its implemented or overridden member.

- Parameter `tagName`: The `tagName` value.
- Returns: A `System.IObservable<IoT.DriverCore.Core.LogicalTagValue>` result.

###### `M:IoT.DriverCore.TwinCATRx.TwinCatLogicalTagClient.ObserveAsync(System.String,System.Threading.CancellationToken)`

```csharp
public System.Collections.Generic.IAsyncEnumerable<IoT.DriverCore.Core.LogicalTagValue> ObserveAsync(string tagName, System.Threading.CancellationToken cancellationToken)
```
Inherits XML documentation from its implemented or overridden member.

- Parameter `tagName`: The `tagName` value.
- Parameter `cancellationToken`: The `cancellationToken` value.
- Returns: A `System.Collections.Generic.IAsyncEnumerable<IoT.DriverCore.Core.LogicalTagValue>` result.

###### `M:IoT.DriverCore.TwinCATRx.TwinCatLogicalTagClient.ObserveMany(System.Collections.Generic.IReadOnlyCollection`1{System.String})`

```csharp
public System.IObservable<IoT.DriverCore.Core.LogicalTagValue> ObserveMany(System.Collections.Generic.IReadOnlyCollection<string> tagNames)
```
Executes the `ObserveMany` operation.

- Parameter `tagNames`: The `tagNames` value.
- Returns: A `System.IObservable<IoT.DriverCore.Core.LogicalTagValue>` result.

###### `M:IoT.DriverCore.TwinCATRx.TwinCatLogicalTagClient.ObserveManyAsync(System.Collections.Generic.IReadOnlyCollection`1{System.String},System.Threading.CancellationToken)`

```csharp
public System.Collections.Generic.IAsyncEnumerable<IoT.DriverCore.Core.LogicalTagValue> ObserveManyAsync(System.Collections.Generic.IReadOnlyCollection<string> tagNames, System.Threading.CancellationToken cancellationToken)
```
Executes the `ObserveManyAsync` operation.

- Parameter `tagNames`: The `tagNames` value.
- Parameter `cancellationToken`: The `cancellationToken` value.
- Returns: A `System.Collections.Generic.IAsyncEnumerable<IoT.DriverCore.Core.LogicalTagValue>` result.

###### `M:IoT.DriverCore.TwinCATRx.TwinCatLogicalTagClient.ReadAsync(System.String,System.Threading.CancellationToken)`

```csharp
public System.Threading.Tasks.Task<IoT.DriverCore.Core.TagOperationResult<IoT.DriverCore.Core.LogicalTagValue>> ReadAsync(string tagName, System.Threading.CancellationToken cancellationToken)
```
Inherits XML documentation from its implemented or overridden member.

- Parameter `tagName`: The `tagName` value.
- Parameter `cancellationToken`: The `cancellationToken` value.
- Returns: A `System.Threading.Tasks.Task<IoT.DriverCore.Core.TagOperationResult<IoT.DriverCore.Core.LogicalTagValue>>` result.

###### `M:IoT.DriverCore.TwinCATRx.TwinCatLogicalTagClient.ReadManyAsync(System.Collections.Generic.IReadOnlyCollection`1{System.String},System.Threading.CancellationToken)`

```csharp
public System.Threading.Tasks.Task<System.Collections.Generic.IReadOnlyList<IoT.DriverCore.Core.TagOperationResult<IoT.DriverCore.Core.LogicalTagValue>>> ReadManyAsync(System.Collections.Generic.IReadOnlyCollection<string> tagNames, System.Threading.CancellationToken cancellationToken)
```
Executes the `ReadManyAsync` operation.

- Parameter `tagNames`: The `tagNames` value.
- Parameter `cancellationToken`: The `cancellationToken` value.
- Returns: A `System.Threading.Tasks.Task<System.Collections.Generic.IReadOnlyList<IoT.DriverCore.Core.TagOperationResult<IoT.DriverCore.Core.LogicalTagValue>>>` result.

###### `M:IoT.DriverCore.TwinCATRx.TwinCatLogicalTagClient.RegisterTag(IoT.DriverCore.Core.LogicalTag)`

```csharp
public void RegisterTag(IoT.DriverCore.Core.LogicalTag tag)
```
Adds or replaces a logical tag in the live registry.

- Parameter `tag`: The logical tag.

###### `M:IoT.DriverCore.TwinCATRx.TwinCatLogicalTagClient.RemoveTag(System.String)`

```csharp
public bool RemoveTag(string name)
```
Removes a logical tag from the live registry.

- Parameter `name`: The logical tag name.
- Returns: Whether the tag was removed.

###### `M:IoT.DriverCore.TwinCATRx.TwinCatLogicalTagClient.UpsertGroupAsync(IoT.DriverCore.Core.LogicalTagGroup,System.Threading.CancellationToken)`

```csharp
public System.Threading.Tasks.Task UpsertGroupAsync(IoT.DriverCore.Core.LogicalTagGroup group, System.Threading.CancellationToken cancellationToken)
```
Inherits XML documentation from its implemented or overridden member.

- Parameter `group`: The `group` value.
- Parameter `cancellationToken`: The `cancellationToken` value.
- Returns: A `System.Threading.Tasks.Task` result.

###### `M:IoT.DriverCore.TwinCATRx.TwinCatLogicalTagClient.UpsertTagAsync(IoT.DriverCore.Core.LogicalTag)`

```csharp
public System.Threading.Tasks.Task UpsertTagAsync(IoT.DriverCore.Core.LogicalTag tag)
```
Upserts a tag in SQLite and the live registry.

- Parameter `tag`: The logical tag.
- Returns: The upsert operation.

###### `M:IoT.DriverCore.TwinCATRx.TwinCatLogicalTagClient.UpsertTagAsync(IoT.DriverCore.Core.LogicalTag,System.Threading.CancellationToken)`

```csharp
public System.Threading.Tasks.Task UpsertTagAsync(IoT.DriverCore.Core.LogicalTag tag, System.Threading.CancellationToken cancellationToken)
```
Upserts a tag in SQLite and the live registry.

- Parameter `tag`: The logical tag.
- Parameter `cancellationToken`: The cancellation token.
- Returns: The upsert operation.

###### `M:IoT.DriverCore.TwinCATRx.TwinCatLogicalTagClient.WriteAsync(IoT.DriverCore.Core.LogicalTagValue,System.Threading.CancellationToken)`

```csharp
public System.Threading.Tasks.Task<IoT.DriverCore.Core.TagOperationResult<IoT.DriverCore.Core.LogicalTagValue>> WriteAsync(IoT.DriverCore.Core.LogicalTagValue value, System.Threading.CancellationToken cancellationToken)
```
Inherits XML documentation from its implemented or overridden member.

- Parameter `value`: The `value` value.
- Parameter `cancellationToken`: The `cancellationToken` value.
- Returns: A `System.Threading.Tasks.Task<IoT.DriverCore.Core.TagOperationResult<IoT.DriverCore.Core.LogicalTagValue>>` result.

###### `M:IoT.DriverCore.TwinCATRx.TwinCatLogicalTagClient.WriteManyAsync(System.Collections.Generic.IReadOnlyCollection`1{IoT.DriverCore.Core.LogicalTagValue},System.Threading.CancellationToken)`

```csharp
public System.Threading.Tasks.Task<System.Collections.Generic.IReadOnlyList<IoT.DriverCore.Core.TagOperationResult<IoT.DriverCore.Core.LogicalTagValue>>> WriteManyAsync(System.Collections.Generic.IReadOnlyCollection<IoT.DriverCore.Core.LogicalTagValue> values, System.Threading.CancellationToken cancellationToken)
```
Executes the `WriteManyAsync` operation.

- Parameter `values`: The `values` value.
- Parameter `cancellationToken`: The `cancellationToken` value.
- Returns: A `System.Threading.Tasks.Task<System.Collections.Generic.IReadOnlyList<IoT.DriverCore.Core.TagOperationResult<IoT.DriverCore.Core.LogicalTagValue>>>` result.

###### `P:IoT.DriverCore.TwinCATRx.TwinCatLogicalTagClient.Catalog`

```csharp
public IoT.DriverCore.Core.ILogicalTagCatalog Catalog { get; }
```
Gets the logical tag catalog.

- Value: The `Catalog` value.

#### `T:IoT.DriverCore.TwinCATRx.TwinCatRxExtensions`

```csharp
public class IoT.DriverCore.TwinCATRx.TwinCatRxExtensions
```
Observable TwinCAT extensions.

##### Declared public members

###### `M:IoT.DriverCore.TwinCATRx.TwinCatRxExtensions.CreateClone(CP.Collections.HashTableRx)`

```csharp
public static CP.Collections.HashTableRx CreateClone(CP.Collections.HashTableRx hashTable)
```
Clones the specified HashTableRx.

- Parameter `hashTable`: The HashTableRx instance.
- Returns: A HashTableRx.

###### `M:IoT.DriverCore.TwinCATRx.TwinCatRxExtensions.CreateStruct(IoT.DriverCore.TwinCATRx.IRxTcAdsClient,System.String)`

```csharp
public static CP.Collections.HashTableRx CreateStruct(IoT.DriverCore.TwinCATRx.IRxTcAdsClient client, string variable)
```
Creates the structure.

- Parameter `client`: The reactive TwinCAT client.
- Parameter `variable`: The variable.
- Returns: A HashTableRx with a link to the PLC.

###### `M:IoT.DriverCore.TwinCATRx.TwinCatRxExtensions.ObserveAsyncObservable``1(IoT.DriverCore.TwinCATRx.IRxTcAdsClient,System.String,System.Func`2{System.Object,``0})`

```csharp
public static ReactiveUI.Primitives.Async.IObservableAsync<T> ObserveAsyncObservable<T>(IoT.DriverCore.TwinCATRx.IRxTcAdsClient client, string variable, System.Func<object, T> converter)
```
Executes the `ObserveAsyncObservable` operation.

- Parameter `client`: The `client` value.
- Parameter `variable`: The `variable` value.
- Parameter `converter`: The `converter` value.
- Returns: A `ReactiveUI.Primitives.Async.IObservableAsync<T>` result.

###### `M:IoT.DriverCore.TwinCATRx.TwinCatRxExtensions.ObserveAsyncObservable``1(IoT.DriverCore.TwinCATRx.IRxTcAdsClient,System.String,System.String,System.Func`2{System.Object,``0})`

```csharp
public static ReactiveUI.Primitives.Async.IObservableAsync<T> ObserveAsyncObservable<T>(IoT.DriverCore.TwinCATRx.IRxTcAdsClient client, string variable, string id, System.Func<object, T> converter)
```
Executes the `ObserveAsyncObservable` operation.

- Parameter `client`: The `client` value.
- Parameter `variable`: The `variable` value.
- Parameter `id`: The `id` value.
- Parameter `converter`: The `converter` value.
- Returns: A `ReactiveUI.Primitives.Async.IObservableAsync<T>` result.

###### `M:IoT.DriverCore.TwinCATRx.TwinCatRxExtensions.Observe``1(IoT.DriverCore.TwinCATRx.IRxTcAdsClient,System.String,System.Func`2{System.Object,``0})`

```csharp
public static System.IObservable<T> Observe<T>(IoT.DriverCore.TwinCATRx.IRxTcAdsClient client, string variable, System.Func<object, T> converter)
```
Executes the `Observe` operation.

- Parameter `client`: The `client` value.
- Parameter `variable`: The `variable` value.
- Parameter `converter`: The `converter` value.
- Returns: A `System.IObservable<T>` result.

###### `M:IoT.DriverCore.TwinCATRx.TwinCatRxExtensions.Observe``1(IoT.DriverCore.TwinCATRx.IRxTcAdsClient,System.String,System.String,System.Func`2{System.Object,``0})`

```csharp
public static System.IObservable<T> Observe<T>(IoT.DriverCore.TwinCATRx.IRxTcAdsClient client, string variable, string id, System.Func<object, T> converter)
```
Executes the `Observe` operation.

- Parameter `client`: The `client` value.
- Parameter `variable`: The `variable` value.
- Parameter `id`: The `id` value.
- Parameter `converter`: The `converter` value.
- Returns: A `System.IObservable<T>` result.

###### `M:IoT.DriverCore.TwinCATRx.TwinCatRxExtensions.StructureReady(CP.Collections.HashTableRx)`

```csharp
public static System.IObservable<CP.Collections.HashTableRx> StructureReady(CP.Collections.HashTableRx hashTable)
```
Returns an observable that fires when the structure is ready.

- Parameter `hashTable`: The HashTableRx instance.
- Returns: An observable when values have been set.

###### `M:IoT.DriverCore.TwinCATRx.TwinCatRxExtensions.WriteValues(CP.Collections.HashTableRx,System.Action`1{CP.Collections.HashTableRx})`

```csharp
public static bool WriteValues(CP.Collections.HashTableRx hashTable, System.Action<CP.Collections.HashTableRx> setValues)
```
Executes the `WriteValues` operation.

- Parameter `hashTable`: The `hashTable` value.
- Parameter `setValues`: The `setValues` value.
- Returns: A `bool` result.

###### `M:IoT.DriverCore.TwinCATRx.TwinCatRxExtensions.WriteValuesAsync(CP.Collections.HashTableRx,System.Action`1{CP.Collections.HashTableRx},System.TimeSpan)`

```csharp
public static System.Threading.Tasks.Task<bool> WriteValuesAsync(CP.Collections.HashTableRx hashTable, System.Action<CP.Collections.HashTableRx> setValues, System.TimeSpan time)
```
Executes the `WriteValuesAsync` operation.

- Parameter `hashTable`: The `hashTable` value.
- Parameter `setValues`: The `setValues` value.
- Parameter `time`: The `time` value.
- Returns: A `System.Threading.Tasks.Task<bool>` result.

### `TwinCATRx.Core`

Exported public types: 13; declared public members: 97.

#### `T:IoT.DriverCore.TwinCATRx.Core.CSharpLanguage`

```csharp
public class IoT.DriverCore.TwinCATRx.Core.CSharpLanguage
```
C Sharp Language.

##### Declared public members

###### `M:IoT.DriverCore.TwinCATRx.Core.CSharpLanguage.#ctor`

```csharp
public IoT.DriverCore.TwinCATRx.Core.CSharpLanguage()
```
Initializes a new instance of `IoT.DriverCore.TwinCATRx.Core.CSharpLanguage`.

###### `M:IoT.DriverCore.TwinCATRx.Core.CSharpLanguage.CreateAssembly(System.String,System.String)`

```csharp
public static bool CreateAssembly(string code, string assemblyFileName)
```
Creates the assembly.

- Parameter `code`: The code.
- Parameter `assemblyFileName`: Name of the assembly file.
- Returns: A bool.

###### `M:IoT.DriverCore.TwinCATRx.Core.CSharpLanguage.CreateLibraryCompilation(System.String,System.Boolean)`

```csharp
public Microsoft.CodeAnalysis.Compilation CreateLibraryCompilation(string assemblyName, bool enableOptimisations)
```
Inherits XML documentation from its implemented or overridden member.

- Parameter `assemblyName`: The `assemblyName` value.
- Parameter `enableOptimisations`: The `enableOptimisations` value.
- Returns: A `Microsoft.CodeAnalysis.Compilation` result.

###### `M:IoT.DriverCore.TwinCATRx.Core.CSharpLanguage.ParseText(System.String,Microsoft.CodeAnalysis.SourceCodeKind)`

```csharp
public Microsoft.CodeAnalysis.SyntaxTree ParseText(string code, Microsoft.CodeAnalysis.SourceCodeKind kind)
```
Inherits XML documentation from its implemented or overridden member.

- Parameter `code`: The `code` value.
- Parameter `kind`: The `kind` value.
- Returns: A `Microsoft.CodeAnalysis.SyntaxTree` result.

#### `T:IoT.DriverCore.TwinCATRx.Core.CodeGenerator`

```csharp
public class IoT.DriverCore.TwinCATRx.Core.CodeGenerator
```
Code Generator.

##### Declared public members

###### `M:IoT.DriverCore.TwinCATRx.Core.CodeGenerator.#ctor`

```csharp
public IoT.DriverCore.TwinCATRx.Core.CodeGenerator()
```
Initializes a new instance of the `T:IoT.DriverCore.TwinCATRx.Core.CodeGenerator` class.

###### `M:IoT.DriverCore.TwinCATRx.Core.CodeGenerator.#ctor(System.Action`1{System.Exception})`

```csharp
public IoT.DriverCore.TwinCATRx.Core.CodeGenerator(System.Action<System.Exception> errorHandler)
```
Initializes a new instance of `IoT.DriverCore.TwinCATRx.Core.CodeGenerator`.

- Parameter `errorHandler`: The `errorHandler` value.

###### `M:IoT.DriverCore.TwinCATRx.Core.CodeGenerator.CreateCSharpCode(IoT.DriverCore.TwinCATRx.Core.INodeEmulator)`

```csharp
public bool CreateCSharpCode(IoT.DriverCore.TwinCATRx.Core.INodeEmulator selectedTN)
```
Creates a C# code file using the default TwinCAT version.

- Parameter `selectedTN`: The selected node.
- Returns: true when code was created.

###### `M:IoT.DriverCore.TwinCATRx.Core.CodeGenerator.CreateCSharpCode(IoT.DriverCore.TwinCATRx.Core.INodeEmulator,System.Boolean)`

```csharp
public bool CreateCSharpCode(IoT.DriverCore.TwinCATRx.Core.INodeEmulator selectedTN, bool isTwinCat3)
```
Creates a C# code file based on the selected node structure.

- Parameter `selectedTN`: The selected tn.
- Parameter `isTwinCat3`: Whether TwinCAT 3 packing should be used.
- Returns: Result as a Boolean.

###### `M:IoT.DriverCore.TwinCATRx.Core.CodeGenerator.CreateCSharpCode(IoT.DriverCore.TwinCATRx.Core.INodeEmulator,System.String)`

```csharp
public bool CreateCSharpCode(IoT.DriverCore.TwinCATRx.Core.INodeEmulator selectedTN, string fileName)
```
Creates a C# code file using default generation settings.

- Parameter `selectedTN`: The `selectedTN` value.
- Parameter `fileName`: The `fileName` value.
- Returns: A `bool` result.

###### `M:IoT.DriverCore.TwinCATRx.Core.CodeGenerator.CreateCSharpCode(IoT.DriverCore.TwinCATRx.Core.INodeEmulator,System.String,System.Boolean)`

```csharp
public bool CreateCSharpCode(IoT.DriverCore.TwinCATRx.Core.INodeEmulator selectedTN, string fileName, bool isTwinCat3)
```
Creates a C# code file using the default namespace.

- Parameter `selectedTN`: The `selectedTN` value.
- Parameter `fileName`: The `fileName` value.
- Parameter `isTwinCat3`: The `isTwinCat3` value.
- Returns: A `bool` result.

###### `M:IoT.DriverCore.TwinCATRx.Core.CodeGenerator.CreateCSharpCode(IoT.DriverCore.TwinCATRx.Core.INodeEmulator,System.String,System.Boolean,System.String)`

```csharp
public bool CreateCSharpCode(IoT.DriverCore.TwinCATRx.Core.INodeEmulator selectedTN, string fileName, bool isTwinCat3, string classNamespace)
```
Creates a C# code file based on the selected node structure.

- Parameter `selectedTN`: The selected tn.
- Parameter `fileName`: Name of the file.
- Parameter `isTwinCat3`: if set to true [is twin cat3].
- Parameter `classNamespace`: The class namespace.
- Returns: Result as a Boolean.

###### `M:IoT.DriverCore.TwinCATRx.Core.CodeGenerator.CreateCSharpCodeString(IoT.DriverCore.TwinCATRx.Core.INodeEmulator)`

```csharp
public string CreateCSharpCodeString(IoT.DriverCore.TwinCATRx.Core.INodeEmulator selectedTN)
```
Creates a C# code string using default generation settings.

- Parameter `selectedTN`: The selected node.
- Returns: The generated code.

###### `M:IoT.DriverCore.TwinCATRx.Core.CodeGenerator.CreateCSharpCodeString(IoT.DriverCore.TwinCATRx.Core.INodeEmulator,System.Boolean)`

```csharp
public string CreateCSharpCodeString(IoT.DriverCore.TwinCATRx.Core.INodeEmulator selectedTN, bool isTwinCat3)
```
Creates a C# code string using the default namespace.

- Parameter `selectedTN`: The `selectedTN` value.
- Parameter `isTwinCat3`: The `isTwinCat3` value.
- Returns: A `string` result.

###### `M:IoT.DriverCore.TwinCATRx.Core.CodeGenerator.CreateCSharpCodeString(IoT.DriverCore.TwinCATRx.Core.INodeEmulator,System.Boolean,System.String)`

```csharp
public string CreateCSharpCodeString(IoT.DriverCore.TwinCATRx.Core.INodeEmulator selectedTN, bool isTwinCat3, string classNamespace)
```
Creates the C# code string.

- Parameter `selectedTN`: The selected tn.
- Parameter `isTwinCat3`: if set to true [is twin cat3].
- Parameter `classNamespace`: The class namespace.
- Returns: A Value.

###### `M:IoT.DriverCore.TwinCATRx.Core.CodeGenerator.CreateDll(IoT.DriverCore.TwinCATRx.Core.INodeEmulator)`

```csharp
public bool CreateDll(IoT.DriverCore.TwinCATRx.Core.INodeEmulator selectedTN)
```
Creates a DLL based on the selected node structure.

- Parameter `selectedTN`: The selected tn.
- Returns: Result as a Boolean.

###### `M:IoT.DriverCore.TwinCATRx.Core.CodeGenerator.CreateDll(IoT.DriverCore.TwinCATRx.Core.INodeEmulator,System.Boolean)`

```csharp
public bool CreateDll(IoT.DriverCore.TwinCATRx.Core.INodeEmulator selectedTN, bool isTwinCat3)
```
Creates a DLL based on the selected node structure.

- Parameter `selectedTN`: The selected node.
- Parameter `isTwinCat3`: Whether TwinCAT 3 packing should be used.
- Returns: true when the DLL was created.

###### `M:IoT.DriverCore.TwinCATRx.Core.CodeGenerator.CreateDll(IoT.DriverCore.TwinCATRx.Core.INodeEmulator,System.String)`

```csharp
public bool CreateDll(IoT.DriverCore.TwinCATRx.Core.INodeEmulator selectedTN, string fileName)
```
Creates a DLL using default generation settings.

- Parameter `selectedTN`: The `selectedTN` value.
- Parameter `fileName`: The `fileName` value.
- Returns: A `bool` result.

###### `M:IoT.DriverCore.TwinCATRx.Core.CodeGenerator.CreateDll(IoT.DriverCore.TwinCATRx.Core.INodeEmulator,System.String,System.Boolean)`

```csharp
public bool CreateDll(IoT.DriverCore.TwinCATRx.Core.INodeEmulator selectedTN, string fileName, bool isTwinCat3)
```
Creates a DLL using the default namespace.

- Parameter `selectedTN`: The `selectedTN` value.
- Parameter `fileName`: The `fileName` value.
- Parameter `isTwinCat3`: The `isTwinCat3` value.
- Returns: A `bool` result.

###### `M:IoT.DriverCore.TwinCATRx.Core.CodeGenerator.CreateDll(IoT.DriverCore.TwinCATRx.Core.INodeEmulator,System.String,System.Boolean,System.String)`

```csharp
public bool CreateDll(IoT.DriverCore.TwinCATRx.Core.INodeEmulator selectedTN, string fileName, bool isTwinCat3, string classNamespace)
```
Creates a DLL based on the selected node structure.

- Parameter `selectedTN`: The selected tn.
- Parameter `fileName`: Name of the file.
- Parameter `isTwinCat3`: if set to true [is twincat3].
- Parameter `classNamespace`: The class namespace.
- Returns: Result as a Boolean.

###### `M:IoT.DriverCore.TwinCATRx.Core.CodeGenerator.CreateDll(System.String,System.String)`

```csharp
public bool CreateDll(string sourceCode, string fileName)
```
Creates the DLL from raw source.

- Parameter `sourceCode`: The C# source code.
- Parameter `fileName`: Name of the file.
- Returns: A Value.

###### `M:IoT.DriverCore.TwinCATRx.Core.CodeGenerator.Dispose`

```csharp
public void Dispose()
```
Performs application-defined tasks associated with freeing resources.

###### `M:IoT.DriverCore.TwinCATRx.Core.CodeGenerator.LoadSymbols(System.Int32)`

```csharp
public System.Collections.Generic.HashSet<IoT.DriverCore.TwinCATRx.Core.INodeEmulator> LoadSymbols(int port)
```
Loads symbols from the specified PLC ADS port.

- Parameter `port`: The port.
- Returns: A Value.

###### `M:IoT.DriverCore.TwinCATRx.Core.CodeGenerator.LoadSymbols(System.String)`

```csharp
public System.Collections.Generic.HashSet<IoT.DriverCore.TwinCATRx.Core.INodeEmulator> LoadSymbols(string adsAddress)
```
Loads symbols from the specified PLC ADS address.

- Parameter `adsAddress`: The ADS address.
- Returns: HashSet(Of NodeEmulator).

###### `M:IoT.DriverCore.TwinCATRx.Core.CodeGenerator.LoadSymbols(System.String,System.Int32)`

```csharp
public System.Collections.Generic.HashSet<IoT.DriverCore.TwinCATRx.Core.INodeEmulator> LoadSymbols(string adsAddress, int port)
```
Loads symbols from the specified PLC ADS address and port.

- Parameter `adsAddress`: The ADS address.
- Parameter `port`: The port.
- Returns: HashSet(Of NodeEmulator).

###### `M:IoT.DriverCore.TwinCATRx.Core.CodeGenerator.PLCToCSharpTypeConverter(System.String)`

```csharp
public static string PLCToCSharpTypeConverter(string plcType)
```
Converts a supported PLC scalar, string, or array type name to its CLR representation.

- Parameter `plcType`: Type of the PLC.
- Returns: A Value.

###### `M:IoT.DriverCore.TwinCATRx.Core.CodeGenerator.ReadSymbol(System.String,System.Int32,System.String,System.Type)`

```csharp
public object ReadSymbol(string adsAddress, int port, string variable, System.Type variableType)
```
Reads the symbol.

- Parameter `adsAddress`: The ADS address.
- Parameter `port`: The port.
- Parameter `variable`: The variable.
- Parameter `variableType`: Type of the variable.
- Returns: A Value.

###### `M:IoT.DriverCore.TwinCATRx.Core.CodeGenerator.SearchSymbols(System.String)`

```csharp
public IoT.DriverCore.TwinCATRx.Core.INodeEmulator SearchSymbols(string symbolName)
```
Searches for the nearest matching symbol list element.

- Parameter `symbolName`: Name of the symbol.
- Returns: NodeEmulator.

###### `P:IoT.DriverCore.TwinCATRx.Core.CodeGenerator.SymbolList`

```csharp
public System.Collections.Generic.HashSet<IoT.DriverCore.TwinCATRx.Core.INodeEmulator> SymbolList { get; }
```
Gets the symbol list.

- Value: The symbol list.

#### `T:IoT.DriverCore.TwinCATRx.Core.DirectoryInfoExtensions`

```csharp
public class IoT.DriverCore.TwinCATRx.Core.DirectoryInfoExtensions
```
Provides filtered file enumeration extensions for `T:System.IO.DirectoryInfo` .

##### Declared public members

###### `M:IoT.DriverCore.TwinCATRx.Core.DirectoryInfoExtensions.GetFilesWhere(System.IO.DirectoryInfo,System.Func`2{System.IO.FileInfo,System.Boolean})`

```csharp
public static System.IO.FileInfo[] GetFilesWhere(System.IO.DirectoryInfo directory, System.Func<System.IO.FileInfo, bool> predicate)
```
Executes the `GetFilesWhere` operation.

- Parameter `directory`: The `directory` value.
- Parameter `predicate`: The `predicate` value.
- Returns: A `System.IO.FileInfo[]` result.

###### `M:IoT.DriverCore.TwinCATRx.Core.DirectoryInfoExtensions.GetFilesWhere(System.IO.DirectoryInfo,System.String,System.Func`2{System.IO.FileInfo,System.Boolean})`

```csharp
public static System.IO.FileInfo[] GetFilesWhere(System.IO.DirectoryInfo directory, string searchPattern, System.Func<System.IO.FileInfo, bool> predicate)
```
Executes the `GetFilesWhere` operation.

- Parameter `directory`: The `directory` value.
- Parameter `searchPattern`: The `searchPattern` value.
- Parameter `predicate`: The `predicate` value.
- Returns: A `System.IO.FileInfo[]` result.

###### `M:IoT.DriverCore.TwinCATRx.Core.DirectoryInfoExtensions.GetFilesWhere(System.IO.DirectoryInfo,System.String,System.IO.SearchOption,System.Func`2{System.IO.FileInfo,System.Boolean})`

```csharp
public static System.IO.FileInfo[] GetFilesWhere(System.IO.DirectoryInfo directory, string searchPattern, System.IO.SearchOption searchOption, System.Func<System.IO.FileInfo, bool> predicate)
```
Executes the `GetFilesWhere` operation.

- Parameter `directory`: The `directory` value.
- Parameter `searchPattern`: The `searchPattern` value.
- Parameter `searchOption`: The `searchOption` value.
- Parameter `predicate`: The `predicate` value.
- Returns: A `System.IO.FileInfo[]` result.

###### `M:IoT.DriverCore.TwinCATRx.Core.DirectoryInfoExtensions.GetFilesWhere(System.IO.DirectoryInfo,System.String[],System.Func`2{System.IO.FileInfo,System.Boolean})`

```csharp
public static System.IO.FileInfo[] GetFilesWhere(System.IO.DirectoryInfo directory, string[] searchPatterns, System.Func<System.IO.FileInfo, bool> predicate)
```
Executes the `GetFilesWhere` operation.

- Parameter `directory`: The `directory` value.
- Parameter `searchPatterns`: The `searchPatterns` value.
- Parameter `predicate`: The `predicate` value.
- Returns: A `System.IO.FileInfo[]` result.

###### `M:IoT.DriverCore.TwinCATRx.Core.DirectoryInfoExtensions.GetFilesWhere(System.IO.DirectoryInfo,System.String[],System.IO.SearchOption,System.Func`2{System.IO.FileInfo,System.Boolean})`

```csharp
public static System.IO.FileInfo[] GetFilesWhere(System.IO.DirectoryInfo directory, string[] searchPatterns, System.IO.SearchOption searchOption, System.Func<System.IO.FileInfo, bool> predicate)
```
Executes the `GetFilesWhere` operation.

- Parameter `directory`: The `directory` value.
- Parameter `searchPatterns`: The `searchPatterns` value.
- Parameter `searchOption`: The `searchOption` value.
- Parameter `predicate`: The `predicate` value.
- Returns: A `System.IO.FileInfo[]` result.

#### `T:IoT.DriverCore.TwinCATRx.Core.ICodeGenerator`

```csharp
public interface IoT.DriverCore.TwinCATRx.Core.ICodeGenerator
```
Interface for Code Generator.

##### Declared public members

###### `M:IoT.DriverCore.TwinCATRx.Core.ICodeGenerator.CreateCSharpCode(IoT.DriverCore.TwinCATRx.Core.INodeEmulator)`

```csharp
public bool CreateCSharpCode(IoT.DriverCore.TwinCATRx.Core.INodeEmulator selectedTN)
```
Creates the c sharp code.

- Parameter `selectedTN`: The selected tn.
- Returns: A Value.

###### `M:IoT.DriverCore.TwinCATRx.Core.ICodeGenerator.CreateCSharpCode(IoT.DriverCore.TwinCATRx.Core.INodeEmulator,System.Boolean)`

```csharp
public bool CreateCSharpCode(IoT.DriverCore.TwinCATRx.Core.INodeEmulator selectedTN, bool isTwinCat3)
```
Creates the c sharp code.

- Parameter `selectedTN`: The selected tn.
- Parameter `isTwinCat3`: Whether TwinCAT 3 conventions are used.
- Returns: A Value.

###### `M:IoT.DriverCore.TwinCATRx.Core.ICodeGenerator.CreateCSharpCode(IoT.DriverCore.TwinCATRx.Core.INodeEmulator,System.String)`

```csharp
public bool CreateCSharpCode(IoT.DriverCore.TwinCATRx.Core.INodeEmulator selectedTN, string fileName)
```
Creates the c sharp code.

- Parameter `selectedTN`: The selected tn.
- Parameter `fileName`: Name of the file.
- Returns: A Value.

###### `M:IoT.DriverCore.TwinCATRx.Core.ICodeGenerator.CreateCSharpCode(IoT.DriverCore.TwinCATRx.Core.INodeEmulator,System.String,System.Boolean)`

```csharp
public bool CreateCSharpCode(IoT.DriverCore.TwinCATRx.Core.INodeEmulator selectedTN, string fileName, bool isTwinCat3)
```
Creates the c sharp code.

- Parameter `selectedTN`: The selected tn.
- Parameter `fileName`: Name of the file.
- Parameter `isTwinCat3`: if set to true [is twin cat3].
- Returns: A Value.

###### `M:IoT.DriverCore.TwinCATRx.Core.ICodeGenerator.CreateCSharpCode(IoT.DriverCore.TwinCATRx.Core.INodeEmulator,System.String,System.Boolean,System.String)`

```csharp
public bool CreateCSharpCode(IoT.DriverCore.TwinCATRx.Core.INodeEmulator selectedTN, string fileName, bool isTwinCat3, string classNamespace)
```
Creates the c sharp code.

- Parameter `selectedTN`: The selected tn.
- Parameter `fileName`: Name of the file.
- Parameter `isTwinCat3`: Whether TwinCAT 3 conventions are used.
- Parameter `classNamespace`: The namespace for generated types.
- Returns: A Value.

###### `M:IoT.DriverCore.TwinCATRx.Core.ICodeGenerator.CreateCSharpCodeString(IoT.DriverCore.TwinCATRx.Core.INodeEmulator)`

```csharp
public string CreateCSharpCodeString(IoT.DriverCore.TwinCATRx.Core.INodeEmulator selectedTN)
```
Creates the c sharp code string.

- Parameter `selectedTN`: The selected tn.
- Returns: A Value.

###### `M:IoT.DriverCore.TwinCATRx.Core.ICodeGenerator.CreateCSharpCodeString(IoT.DriverCore.TwinCATRx.Core.INodeEmulator,System.Boolean)`

```csharp
public string CreateCSharpCodeString(IoT.DriverCore.TwinCATRx.Core.INodeEmulator selectedTN, bool isTwinCat3)
```
Creates the c sharp code string.

- Parameter `selectedTN`: The selected tn.
- Parameter `isTwinCat3`: if set to true [is twin cat3].
- Returns: A Value.

###### `M:IoT.DriverCore.TwinCATRx.Core.ICodeGenerator.CreateCSharpCodeString(IoT.DriverCore.TwinCATRx.Core.INodeEmulator,System.Boolean,System.String)`

```csharp
public string CreateCSharpCodeString(IoT.DriverCore.TwinCATRx.Core.INodeEmulator selectedTN, bool isTwinCat3, string classNamespace)
```
Creates the c sharp code string.

- Parameter `selectedTN`: The selected tn.
- Parameter `isTwinCat3`: Whether TwinCAT 3 conventions are used.
- Parameter `classNamespace`: The namespace for generated types.
- Returns: A Value.

###### `M:IoT.DriverCore.TwinCATRx.Core.ICodeGenerator.CreateDll(IoT.DriverCore.TwinCATRx.Core.INodeEmulator)`

```csharp
public bool CreateDll(IoT.DriverCore.TwinCATRx.Core.INodeEmulator selectedTN)
```
Creates the DLL.

- Parameter `selectedTN`: The selected tn.
- Returns: A Value.

###### `M:IoT.DriverCore.TwinCATRx.Core.ICodeGenerator.CreateDll(IoT.DriverCore.TwinCATRx.Core.INodeEmulator,System.Boolean)`

```csharp
public bool CreateDll(IoT.DriverCore.TwinCATRx.Core.INodeEmulator selectedTN, bool isTwinCat3)
```
Creates the DLL.

- Parameter `selectedTN`: The selected tn.
- Parameter `isTwinCat3`: Whether TwinCAT 3 conventions are used.
- Returns: A Value.

###### `M:IoT.DriverCore.TwinCATRx.Core.ICodeGenerator.CreateDll(IoT.DriverCore.TwinCATRx.Core.INodeEmulator,System.String)`

```csharp
public bool CreateDll(IoT.DriverCore.TwinCATRx.Core.INodeEmulator selectedTN, string fileName)
```
Creates the DLL.

- Parameter `selectedTN`: The selected tn.
- Parameter `fileName`: Name of the file.
- Returns: A Value.

###### `M:IoT.DriverCore.TwinCATRx.Core.ICodeGenerator.CreateDll(IoT.DriverCore.TwinCATRx.Core.INodeEmulator,System.String,System.Boolean)`

```csharp
public bool CreateDll(IoT.DriverCore.TwinCATRx.Core.INodeEmulator selectedTN, string fileName, bool isTwinCat3)
```
Creates the DLL.

- Parameter `selectedTN`: The selected tn.
- Parameter `fileName`: Name of the file.
- Parameter `isTwinCat3`: if set to true [is twin cat3].
- Returns: A Value.

###### `M:IoT.DriverCore.TwinCATRx.Core.ICodeGenerator.CreateDll(IoT.DriverCore.TwinCATRx.Core.INodeEmulator,System.String,System.Boolean,System.String)`

```csharp
public bool CreateDll(IoT.DriverCore.TwinCATRx.Core.INodeEmulator selectedTN, string fileName, bool isTwinCat3, string classNamespace)
```
Creates the DLL.

- Parameter `selectedTN`: The selected tn.
- Parameter `fileName`: Name of the file.
- Parameter `isTwinCat3`: Whether TwinCAT 3 conventions are used.
- Parameter `classNamespace`: The namespace for generated types.
- Returns: A Value.

###### `M:IoT.DriverCore.TwinCATRx.Core.ICodeGenerator.CreateDll(System.String,System.String)`

```csharp
public bool CreateDll(string sourceCode, string fileName)
```
Creates the DLL.

- Parameter `sourceCode`: The C# source code.
- Parameter `fileName`: Name of the file.
- Returns: A Value.

###### `M:IoT.DriverCore.TwinCATRx.Core.ICodeGenerator.LoadSymbols(System.Int32)`

```csharp
public System.Collections.Generic.HashSet<IoT.DriverCore.TwinCATRx.Core.INodeEmulator> LoadSymbols(int port)
```
Loads the symbols.

- Parameter `port`: The port.
- Returns: A Value.

###### `M:IoT.DriverCore.TwinCATRx.Core.ICodeGenerator.LoadSymbols(System.String)`

```csharp
public System.Collections.Generic.HashSet<IoT.DriverCore.TwinCATRx.Core.INodeEmulator> LoadSymbols(string adsAddress)
```
Loads the symbols.

- Parameter `adsAddress`: The ADS address.
- Returns: A Value.

###### `M:IoT.DriverCore.TwinCATRx.Core.ICodeGenerator.LoadSymbols(System.String,System.Int32)`

```csharp
public System.Collections.Generic.HashSet<IoT.DriverCore.TwinCATRx.Core.INodeEmulator> LoadSymbols(string adsAddress, int port)
```
Loads the symbols.

- Parameter `adsAddress`: The ADS address.
- Parameter `port`: The port.
- Returns: A Value.

###### `M:IoT.DriverCore.TwinCATRx.Core.ICodeGenerator.ReadSymbol(System.String,System.Int32,System.String,System.Type)`

```csharp
public object ReadSymbol(string adsAddress, int port, string variable, System.Type variableType)
```
Reads the symbol.

- Parameter `adsAddress`: The ADS address.
- Parameter `port`: The port.
- Parameter `variable`: The variable.
- Parameter `variableType`: Type of the variable.
- Returns: A Value.

###### `M:IoT.DriverCore.TwinCATRx.Core.ICodeGenerator.SearchSymbols(System.String)`

```csharp
public IoT.DriverCore.TwinCATRx.Core.INodeEmulator SearchSymbols(string symbolName)
```
Searches the symbols.

- Parameter `symbolName`: Name of the symbol.
- Returns: A Value.

###### `P:IoT.DriverCore.TwinCATRx.Core.ICodeGenerator.SymbolList`

```csharp
public System.Collections.Generic.HashSet<IoT.DriverCore.TwinCATRx.Core.INodeEmulator> SymbolList { get; }
```
Gets the symbol list.

- Value: The symbol list.

#### `T:IoT.DriverCore.TwinCATRx.Core.ILanguageService`

```csharp
public interface IoT.DriverCore.TwinCATRx.Core.ILanguageService
```
I Language Service.

##### Declared public members

###### `M:IoT.DriverCore.TwinCATRx.Core.ILanguageService.CreateLibraryCompilation(System.String,System.Boolean)`

```csharp
public Microsoft.CodeAnalysis.Compilation CreateLibraryCompilation(string assemblyName, bool enableOptimisations)
```
Creates the library compilation.

- Parameter `assemblyName`: Name of the assembly.
- Parameter `enableOptimisations`: if set to true [enable optimisations].
- Returns: A Compilation.

###### `M:IoT.DriverCore.TwinCATRx.Core.ILanguageService.ParseText(System.String,Microsoft.CodeAnalysis.SourceCodeKind)`

```csharp
public Microsoft.CodeAnalysis.SyntaxTree ParseText(string code, Microsoft.CodeAnalysis.SourceCodeKind kind)
```
Parses the text.

- Parameter `code`: The code.
- Parameter `kind`: The kind.
- Returns: A SyntaxTree.

#### `T:IoT.DriverCore.TwinCATRx.Core.INodeEmulator`

```csharp
public interface IoT.DriverCore.TwinCATRx.Core.INodeEmulator
```
Interface for Node Emulator.

##### Declared public members

###### `P:IoT.DriverCore.TwinCATRx.Core.INodeEmulator.Nodes`

```csharp
public System.Collections.Generic.HashSet<IoT.DriverCore.TwinCATRx.Core.INodeEmulator> Nodes { get; }
```
Gets the nodes.

- Value: The nodes.

###### `P:IoT.DriverCore.TwinCATRx.Core.INodeEmulator.Tag`

```csharp
public object Tag { get; set; }
```
Gets or sets the tag.

- Value: The tag.

###### `P:IoT.DriverCore.TwinCATRx.Core.INodeEmulator.Text`

```csharp
public string Text { get; set; }
```
Gets or sets the text.

- Value: The text.

#### `T:IoT.DriverCore.TwinCATRx.Core.INotification`

```csharp
public interface IoT.DriverCore.TwinCATRx.Core.INotification
```
Interface for Notification.

##### Declared public members

###### `P:IoT.DriverCore.TwinCATRx.Core.INotification.ArraySize`

```csharp
public int ArraySize { get; }
```
Gets the size of the array.

- Value: The size of the array.

###### `P:IoT.DriverCore.TwinCATRx.Core.INotification.UpdateRate`

```csharp
public int UpdateRate { get; }
```
Gets the update rate.

- Value: The update rate.

###### `P:IoT.DriverCore.TwinCATRx.Core.INotification.Variable`

```csharp
public string Variable { get; }
```
Gets the variable.

- Value: The variable.

#### `T:IoT.DriverCore.TwinCATRx.Core.ISettings`

```csharp
public interface IoT.DriverCore.TwinCATRx.Core.ISettings
```
Interface for engine settings.

##### Declared public members

###### `M:IoT.DriverCore.TwinCATRx.Core.ISettings.Defaults``1(``0)`

```csharp
public T Defaults<T>(T defaultSettings)
```
Gets or sets Default settings.

- Parameter `defaultSettings`: The settings instance that establishes the requested type.
- Returns: Default values of type T.

###### `P:IoT.DriverCore.TwinCATRx.Core.ISettings.AdsAddress`

```csharp
public string AdsAddress { get; set; }
```
Gets or sets the ads address.

- Value: The ads address.

###### `P:IoT.DriverCore.TwinCATRx.Core.ISettings.Notifications`

```csharp
public System.Collections.Generic.IList<IoT.DriverCore.TwinCATRx.Core.INotification> Notifications { get; }
```
Gets or sets Notifications of this Engine.

- Value: The `Notifications` value.

###### `P:IoT.DriverCore.TwinCATRx.Core.ISettings.Port`

```csharp
public int Port { get; set; }
```
Gets or sets the port.

- Value: The port.

###### `P:IoT.DriverCore.TwinCATRx.Core.ISettings.SettingsId`

```csharp
public string SettingsId { get; set; }
```
Gets or sets System Identifier.

- Value: The `SettingsId` value.

###### `P:IoT.DriverCore.TwinCATRx.Core.ISettings.WriteVariables`

```csharp
public System.Collections.Generic.IList<IoT.DriverCore.TwinCATRx.Core.IWriteVariable> WriteVariables { get; }
```
Gets or sets Write variables to this Engine.

- Value: The `WriteVariables` value.

#### `T:IoT.DriverCore.TwinCATRx.Core.IWriteVariable`

```csharp
public interface IoT.DriverCore.TwinCATRx.Core.IWriteVariable
```
Interface for Write Variable.

##### Declared public members

###### `P:IoT.DriverCore.TwinCATRx.Core.IWriteVariable.ArraySize`

```csharp
public int ArraySize { get; }
```
Gets the size of the array.

- Value: The size of the array.

###### `P:IoT.DriverCore.TwinCATRx.Core.IWriteVariable.Variable`

```csharp
public string Variable { get; }
```
Gets the variable.

- Value: The variable.

#### `T:IoT.DriverCore.TwinCATRx.Core.Settings`

```csharp
public class IoT.DriverCore.TwinCATRx.Core.Settings
```
Base settings for Engine Settings file.

##### Declared public members

###### `M:IoT.DriverCore.TwinCATRx.Core.Settings.#ctor`

```csharp
public IoT.DriverCore.TwinCATRx.Core.Settings()
```
Initializes a new instance of `IoT.DriverCore.TwinCATRx.Core.Settings`.

###### `M:IoT.DriverCore.TwinCATRx.Core.Settings.Defaults``1(``0)`

```csharp
public T Defaults<T>(T defaultSettings)
```
Creates default settings when no persisted file exists.

- Parameter `defaultSettings`: The settings instance that establishes the requested type.
- Returns: The default settings.

###### `P:IoT.DriverCore.TwinCATRx.Core.Settings.AdsAddress`

```csharp
public string AdsAddress { get; set; }
```
Gets or sets the Ads Address.

- Value: The `AdsAddress` value.

###### `P:IoT.DriverCore.TwinCATRx.Core.Settings.Notifications`

```csharp
public System.Collections.Generic.List<IoT.DriverCore.TwinCATRx.Core.INotification> Notifications { get; set; }
```
Gets or sets Notifications of this Engine.

- Value: The `Notifications` value.

###### `P:IoT.DriverCore.TwinCATRx.Core.Settings.Port`

```csharp
public int Port { get; set; }
```
Gets or sets the Port of the PLC to connect to.

- Value: The `Port` value.

###### `P:IoT.DriverCore.TwinCATRx.Core.Settings.SettingsId`

```csharp
public string SettingsId { get; set; }
```
Gets or sets System Identifier.

- Value: The `SettingsId` value.

###### `P:IoT.DriverCore.TwinCATRx.Core.Settings.WriteVariables`

```csharp
public System.Collections.Generic.List<IoT.DriverCore.TwinCATRx.Core.IWriteVariable> WriteVariables { get; set; }
```
Gets or sets Write variables to this Engine.

- Value: The `WriteVariables` value.

#### `T:IoT.DriverCore.TwinCATRx.Core.SimpleTypeException`

```csharp
public class IoT.DriverCore.TwinCATRx.Core.SimpleTypeException
```
Exception thrown when a simple type is not supported.

##### Declared public members

###### `M:IoT.DriverCore.TwinCATRx.Core.SimpleTypeException.#ctor`

```csharp
public IoT.DriverCore.TwinCATRx.Core.SimpleTypeException()
```
Initializes a new instance of the `T:IoT.DriverCore.TwinCATRx.Core.SimpleTypeException` class.

###### `M:IoT.DriverCore.TwinCATRx.Core.SimpleTypeException.#ctor(System.String)`

```csharp
public IoT.DriverCore.TwinCATRx.Core.SimpleTypeException(string message)
```
Initializes a new instance of the `T:IoT.DriverCore.TwinCATRx.Core.SimpleTypeException` class.

- Parameter `message`: The message that describes the error.

###### `M:IoT.DriverCore.TwinCATRx.Core.SimpleTypeException.#ctor(System.String,System.Exception)`

```csharp
public IoT.DriverCore.TwinCATRx.Core.SimpleTypeException(string message, System.Exception innerException)
```
Initializes a new instance of the `T:IoT.DriverCore.TwinCATRx.Core.SimpleTypeException` class.

- Parameter `message`: The error message that explains the reason for the exception.
- Parameter `innerException`: The exception that caused this exception, or null when no inner exception is specified.

#### `T:IoT.DriverCore.TwinCATRx.Core.TwinCatRxExtensions`

```csharp
public class IoT.DriverCore.TwinCATRx.Core.TwinCatRxExtensions
```
Observable TwinCAT extensions.

##### Declared public members

###### `M:IoT.DriverCore.TwinCATRx.Core.TwinCatRxExtensions.AddNotification(IoT.DriverCore.TwinCATRx.Core.ISettings,System.String)`

```csharp
public static void AddNotification(IoT.DriverCore.TwinCATRx.Core.ISettings settings, string variableName)
```
Adds a notification variable to the settings.

- Parameter `settings`: The TwinCAT settings.
- Parameter `variableName`: The PLC variable name.

###### `M:IoT.DriverCore.TwinCATRx.Core.TwinCatRxExtensions.AddNotification(IoT.DriverCore.TwinCATRx.Core.ISettings,System.String,System.Int32)`

```csharp
public static void AddNotification(IoT.DriverCore.TwinCATRx.Core.ISettings settings, string variableName, int cycleTime)
```
Adds a notification variable to the settings.

- Parameter `settings`: The TwinCAT settings.
- Parameter `variableName`: The PLC variable name.
- Parameter `cycleTime`: The polling cycle time.

###### `M:IoT.DriverCore.TwinCATRx.Core.TwinCatRxExtensions.AddNotification(IoT.DriverCore.TwinCATRx.Core.ISettings,System.String,System.Int32,System.Int32)`

```csharp
public static void AddNotification(IoT.DriverCore.TwinCATRx.Core.ISettings settings, string variableName, int cycleTime, int arraySize)
```
Adds a notification variable to the settings.

- Parameter `settings`: The TwinCAT settings.
- Parameter `variableName`: The PLC variable name.
- Parameter `cycleTime`: The polling cycle time.
- Parameter `arraySize`: The array size.

###### `M:IoT.DriverCore.TwinCATRx.Core.TwinCatRxExtensions.AddWriteVariable(IoT.DriverCore.TwinCATRx.Core.ISettings,System.String)`

```csharp
public static void AddWriteVariable(IoT.DriverCore.TwinCATRx.Core.ISettings settings, string variableName)
```
Adds a write variable to the settings.

- Parameter `settings`: The TwinCAT settings.
- Parameter `variableName`: The PLC variable name.

###### `M:IoT.DriverCore.TwinCATRx.Core.TwinCatRxExtensions.AddWriteVariable(IoT.DriverCore.TwinCATRx.Core.ISettings,System.String,System.Int32)`

```csharp
public static void AddWriteVariable(IoT.DriverCore.TwinCATRx.Core.ISettings settings, string variableName, int arraySize)
```
Adds a write variable to the settings.

- Parameter `settings`: The TwinCAT settings.
- Parameter `variableName`: The PLC variable name.
- Parameter `arraySize`: The array size.

###### `M:IoT.DriverCore.TwinCATRx.Core.TwinCatRxExtensions.AdsStateChangedObserver(TwinCAT.Ads.AdsClient)`

```csharp
public static System.IObservable<TwinCAT.Ads.AdsStateChangedEventArgs> AdsStateChangedObserver(TwinCAT.Ads.AdsClient client)
```
Observes ADS state changed events.

- Parameter `client`: The ADS client.
- Returns: The ADS state changed observable sequence.

###### `M:IoT.DriverCore.TwinCATRx.Core.TwinCatRxExtensions.AdsStateObserver(TwinCAT.Ads.AdsClient)`

```csharp
public static System.IObservable<TwinCAT.Ads.StateInfo> AdsStateObserver(TwinCAT.Ads.AdsClient client)
```
Polls ADS state from the client.

- Parameter `client`: The ADS client.
- Returns: The ADS state observable sequence.

###### `M:IoT.DriverCore.TwinCATRx.Core.TwinCatRxExtensions.AssemblyLoad(System.String)`

```csharp
public static System.Reflection.Assembly AssemblyLoad(string dllFullName)
```
Loads an assembly from a DLL file path.

- Parameter `dllFullName`: The full DLL path.
- Returns: The loaded assembly.

###### `M:IoT.DriverCore.TwinCATRx.Core.TwinCatRxExtensions.GetType(System.String,System.String)`

```csharp
public static System.Type GetType(string dllFullName, string engineType)
```
Gets a type from an assembly file.

- Parameter `dllFullName`: The full DLL path.
- Parameter `engineType`: The type name.
- Returns: The resolved type.

###### `M:IoT.DriverCore.TwinCATRx.Core.TwinCatRxExtensions.OnErrorRetry``1(System.IObservable`1{``0})`

```csharp
public static System.IObservable<TSource> OnErrorRetry<TSource>(System.IObservable<TSource> source)
```
Executes the `OnErrorRetry` operation.

- Parameter `source`: The `source` value.
- Returns: A `System.IObservable<TSource>` result.

###### `M:IoT.DriverCore.TwinCATRx.Core.TwinCatRxExtensions.OnErrorRetry``2(System.IObservable`1{``0},System.Action`1{``1})`

```csharp
public static System.IObservable<TSource> OnErrorRetry<TSource, TException>(System.IObservable<TSource> source, System.Action<TException> onError)
```
Executes the `OnErrorRetry` operation.

- Parameter `source`: The `source` value.
- Parameter `onError`: The `onError` value.
- Returns: A `System.IObservable<TSource>` result.

###### `M:IoT.DriverCore.TwinCATRx.Core.TwinCatRxExtensions.OnErrorRetry``2(System.IObservable`1{``0},System.Action`1{``1},System.Int32)`

```csharp
public static System.IObservable<TSource> OnErrorRetry<TSource, TException>(System.IObservable<TSource> source, System.Action<TException> onError, int retryCount)
```
Executes the `OnErrorRetry` operation.

- Parameter `source`: The `source` value.
- Parameter `onError`: The `onError` value.
- Parameter `retryCount`: The `retryCount` value.
- Returns: A `System.IObservable<TSource>` result.

###### `M:IoT.DriverCore.TwinCATRx.Core.TwinCatRxExtensions.OnErrorRetry``2(System.IObservable`1{``0},System.Action`1{``1},System.Int32,System.TimeSpan)`

```csharp
public static System.IObservable<TSource> OnErrorRetry<TSource, TException>(System.IObservable<TSource> source, System.Action<TException> onError, int retryCount, System.TimeSpan delay)
```
Executes the `OnErrorRetry` operation.

- Parameter `source`: The `source` value.
- Parameter `onError`: The `onError` value.
- Parameter `retryCount`: The `retryCount` value.
- Parameter `delay`: The `delay` value.
- Returns: A `System.IObservable<TSource>` result.

###### `M:IoT.DriverCore.TwinCATRx.Core.TwinCatRxExtensions.OnErrorRetry``2(System.IObservable`1{``0},System.Action`1{``1},System.Int32,System.TimeSpan,ReactiveUI.Primitives.Concurrency.ISequencer)`

```csharp
public static System.IObservable<TSource> OnErrorRetry<TSource, TException>(System.IObservable<TSource> source, System.Action<TException> onError, int retryCount, System.TimeSpan delay, ReactiveUI.Primitives.Concurrency.ISequencer delaySequencer)
```
Executes the `OnErrorRetry` operation.

- Parameter `source`: The `source` value.
- Parameter `onError`: The `onError` value.
- Parameter `retryCount`: The `retryCount` value.
- Parameter `delay`: The `delay` value.
- Parameter `delaySequencer`: The `delaySequencer` value.
- Returns: A `System.IObservable<TSource>` result.

###### `M:IoT.DriverCore.TwinCATRx.Core.TwinCatRxExtensions.OnErrorRetry``2(System.IObservable`1{``0},System.Action`1{``1},System.TimeSpan)`

```csharp
public static System.IObservable<TSource> OnErrorRetry<TSource, TException>(System.IObservable<TSource> source, System.Action<TException> onError, System.TimeSpan delay)
```
Executes the `OnErrorRetry` operation.

- Parameter `source`: The `source` value.
- Parameter `onError`: The `onError` value.
- Parameter `delay`: The `delay` value.
- Returns: A `System.IObservable<TSource>` result.

#### `T:IoT.DriverCore.TwinCATRx.Core.UnsuportedTypeException`

```csharp
public class IoT.DriverCore.TwinCATRx.Core.UnsuportedTypeException
```
Exception thrown when a simple type is not supported.

##### Declared public members

###### `M:IoT.DriverCore.TwinCATRx.Core.UnsuportedTypeException.#ctor`

```csharp
public IoT.DriverCore.TwinCATRx.Core.UnsuportedTypeException()
```
Initializes a new instance of the `T:IoT.DriverCore.TwinCATRx.Core.UnsuportedTypeException` class.

###### `M:IoT.DriverCore.TwinCATRx.Core.UnsuportedTypeException.#ctor(System.String)`

```csharp
public IoT.DriverCore.TwinCATRx.Core.UnsuportedTypeException(string message)
```
Initializes a new instance of the `T:IoT.DriverCore.TwinCATRx.Core.UnsuportedTypeException` class.

- Parameter `message`: The message that describes the error.

###### `M:IoT.DriverCore.TwinCATRx.Core.UnsuportedTypeException.#ctor(System.String,System.Exception)`

```csharp
public IoT.DriverCore.TwinCATRx.Core.UnsuportedTypeException(string message, System.Exception innerException)
```
Initializes a new instance of the `T:IoT.DriverCore.TwinCATRx.Core.UnsuportedTypeException` class.

- Parameter `message`: The error message that explains the reason for the exception.
- Parameter `innerException`: The exception that caused this exception, or null when no inner exception is specified.

<!-- END GENERATED PUBLIC API -->
