<p align="center">
  <img src="https://github.com/ChrisPulman/IoT-DriverCore/blob/main/images/TwinCATRx.png" alt="TwinCATRx package logo" width="320" />
</p>

# TwinCATRx

TwinCATRx connects .NET applications to Beckhoff TwinCAT ADS through observable streams. It includes polling and native ADS notifications, correlated reads and writes, structured values, logical tags with CSV/SQLite persistence, generated typed connection models, Windows service monitoring, and an in-memory ADS simulator.

## Choose a package

| Package | Namespace | Purpose |
| --- | --- | --- |
| `IoT-Driver.TwinCATRx` | `IoT.Driver.TwinCATRx` | ADS runtime using ReactiveUI.Primitives operators |
| `IoT-Driver.TwinCATRx.Core` | `IoT.Driver.TwinCATRx.Core` | Settings, notifications, ADS state observers, retry and dynamic code generation; referenced by the runtime |
| `IoT-Driver.TwinCATRx.Reactive` | `IoT.Driver.TwinCATRx.Reactive` | Runtime variant for System.Reactive applications |
| `IoT-Driver.TwinCATRx.Core.Reactive` | `IoT.Driver.TwinCATRx.Core.Reactive` | Corresponding System.Reactive core variant |
| `IoT-Driver.TwinCATRx.Generators` | `IoT.Driver.TwinCATRx.SourceGenerators` | Optional standalone Roslyn analyzer for typed streams and connections |

Core target frameworks are .NET Framework 4.6.2, 4.7.2, 4.8 and 4.8.1, and .NET 8 through .NET 11. Runtime packages also supply Windows-specific modern targets. Windows service APIs require a Windows target. The examples below use modern C# and the default runtime; some features such as async enumeration need the corresponding framework/runtime support.

```sh
dotnet add package IoT-Driver.TwinCATRx
# Add when using the source-generation examples:
dotnet add package IoT-Driver.TwinCATRx.Generators
```

For System.Reactive applications install `IoT-Driver.TwinCATRx.Reactive` instead. Use its runtime and core namespaces together and `System.Reactive.Linq` for operators. Both variants can coexist through explicit namespace aliases, but their client/settings contracts are distinct; do not pass a default-variant client to a Reactive-variant helper. Async observable adapters still use `ReactiveUI.Primitives.Async`.

**API calling convention:** despite their names, most `TwinCatRxExtensions` methods and the `ObservableBridgeExtensions` methods are static helpers, not C# extension methods. `AddAdsNotification` is an extension-block member called on settings. The examples use aliases to distinguish the runtime and core helper classes:

```csharp
using AdsApi = IoT.Driver.TwinCATRx.TwinCatRxExtensions;
using AdsSettings = IoT.Driver.TwinCATRx.Core.TwinCatRxExtensions;
using Bridge = IoT.Driver.TwinCATRx.ObservableBridgeExtensions;
```

A System.Reactive simulator example uses the corresponding namespace pair:

```csharp
using System;
using System.Reactive.Linq;
using IoT.Driver.TwinCATRx.Reactive;
using IoT.Driver.TwinCATRx.Core.Reactive;
using RxAdsApi = IoT.Driver.TwinCATRx.Reactive.TwinCatRxExtensions;
using RxAdsSettings = IoT.Driver.TwinCATRx.Core.Reactive.TwinCatRxExtensions;

using var simulator = new InMemoryAdsClient();
simulator.RegisterSymbol(".Main.Counter", 1);
var settings = new Settings { Port = 851, SettingsId = "rx-demo" };
RxAdsSettings.AddNotification(settings, ".Main.Counter");
using var values = RxAdsApi.Observe(simulator, ".Main.Counter", Convert.ToInt32)
    .DistinctUntilChanged().Subscribe(Console.WriteLine);
simulator.Connect(settings);
simulator.Read(".Main.Counter");
```

## Quick start with a PLC

Configure the ADS route and replace the example AMS Net ID and PLC symbols with your target's values. Port 851 is a TwinCAT 3 PLC example; `Settings.Port` defaults to 801. Register handles before connecting. The production initialization code can request `AdsState.Run` when it finds a connected PLC stopped, so connecting can change controller state. ADS writes change PLC state, so run commissioning examples against a development target with controller-side interlocks.

This complete console example subscribes before connecting, waits for initialization with a timeout, performs a correlated read, and remains alive until Enter is pressed. It logs write results without issuing a write automatically.

```csharp
using System;
using System.Threading.Tasks;
using IoT.Driver.TwinCATRx;
using IoT.Driver.TwinCATRx.Core;
using AdsApi = IoT.Driver.TwinCATRx.TwinCatRxExtensions;
using AdsSettings = IoT.Driver.TwinCATRx.Core.TwinCatRxExtensions;
using Bridge = IoT.Driver.TwinCATRx.ObservableBridgeExtensions;

using var client = new RxTcAdsClient();
var settings = new Settings
{
    AdsAddress = "5.35.59.10.1.1", Port = 851, SettingsId = "LineA"
};
AdsSettings.AddNotification(settings, ".Main.Counter", cycleTime: 250);
AdsSettings.AddWriteVariable(settings, ".Main.Counter");

var initialized = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
using var ready = Bridge.SubscribeTo(client.InitializeComplete, _ => initialized.TrySetResult(true));
using var errors = Bridge.SubscribeTo(client.ErrorReceived, ex => Console.Error.WriteLine(ex));
using var writes = Bridge.SubscribeTo(client.OnWrite, result => Console.WriteLine($"Write: {result}"));
using var values = Bridge.SubscribeTo(
    AdsApi.Observe(client, ".Main.Counter", value => Convert.ToInt16(value)),
    value => Console.WriteLine($"Counter = {value}"));
using var reply = Bridge.SubscribeTo(
    AdsApi.Observe(client, ".Main.Counter", "counter-request", value => Convert.ToInt16(value)),
    value => Console.WriteLine($"Requested counter = {value}"));

try
{
    client.Connect(settings);
    await initialized.Task.WaitAsync(TimeSpan.FromSeconds(30));
    client.Read(".Main.Counter", "counter-request");
    // When an approved application action requires a write:
    // client.Write(".Main.Counter", (short)42, "counter-command");
    Console.ReadLine();
}
finally
{
    client.Disconnect();
}
```

`Connect` initiates setup; returning from it does not establish readiness. On a fresh client, `InitializeComplete` signals that initialization has completed and handles are ready. A failure can arrive on `ErrorReceived` without initialization completing, hence the timeout. `Disconnect` releases session resources; dispose the client and every subscription when finished. Reconnect by calling `Connect(settings)` again after disconnecting. Only the simulator exposes a separate `Reconnect()` convenience method.

Both clients retain the most recent `InitializeComplete` event for the lifetime of the client. A new subscription after a successful connection can receive that earlier event even after disconnecting or while a new connection is still initializing; it does not establish readiness of the new session. The event has no connection identifier. For a distinct readiness wait on every attempt, create a fresh client, subscribe before calling `Connect`, and dispose the prior client and its subscriptions. If reusing a client, manage session boundaries and event timing in the application; do not treat a replayed event or an already-completed task as confirmation that reconnection finished. The same limitation applies to `InitializeCompleteAsync`.

On the production client, disconnect or disposal retires the current connection without waiting for a synchronous ADS operation already in progress. Native resources remain alive until that operation exits, and its late result or error is not published to the retired connection or a replacement connection. A notification already being delivered to an observer may finish after teardown returns. Handles belong to a connection; retrieve the current handle dictionaries again after reconnecting instead of retaining an earlier reference.

## Settings and notification modes

`ISettings`/`Settings` hold `AdsAddress`, `Port`, `SettingsId`, `Notifications` and `WriteVariables`. Polling registrations are `INotification`/`Notification` (`Variable`, `UpdateRate`, `ArraySize`); native registrations use `IAdsNotification`/`AdsNotification` with ADS transmission mode, cycle time and maximum delay. Write registrations use `IWriteVariable`/`WriteVariable` (`Variable`, `ArraySize`). `Settings.Defaults<T>(defaultSettings)` creates a new default configuration (SettingsId `Defaults`, notification `.UIStructure`, write variable `.FailSafeCounter`); it does not clone the supplied settings. Registration helpers append to the settings; they do not update an already initialized session.

The following snippets build on the aliases and client from the quick start unless they declare a separate client. Use the exact symbol name exported by your PLC; leading dots are illustrative, not a requirement for every project.

```csharp
using TwinCAT.Ads;

// Poll reads every 100 ms (the default).
AdsSettings.AddNotification(settings, ".Main.State");
AdsSettings.AddNotification(settings, ".Main.Temperature", cycleTime: 250);
// Explicit lengths for strings/arrays when required by the symbol's shape.
AdsSettings.AddNotification(settings, ".Main.BatchName", cycleTime: 500, arraySize: 80);
AdsSettings.AddWriteVariable(settings, ".Main.BatchName", arraySize: 80);

// Subscribe to Beckhoff symbol ValueChanged events instead of polling.
settings.AddAdsNotification(".Main.Pressure");
settings.AddAdsNotification(".Main.Flow",
    adsTransMode: AdsTransMode.Cyclic, cycleTime: 250, maxDelay: 0);
```

`AddNotification` has name-only, name/cycle and name/cycle/size overloads. `AddWriteVariable` has name-only and name/size overloads. `AddAdsNotification` defaults to `AdsTransMode.OnChange`, 100 ms cycle time and zero maximum delay. Native registrations create symbol event subscriptions on the production client; callbacks feed `DataReceived`. The connection owns the shared loader and removes event handlers on disconnect/disposal. Reconnecting creates a new loader. Delivery is scheduled away from the ADS callback; marshal to your UI dispatcher when updating UI state.

## Reads, writes, results and pause windows

The common `IRxTcAdsClient` contract provides:

| Operation | Result |
| --- | --- |
| `Read(variable)` / `Read(variable, id)` | Emits `(Variable, Data, Id)` on `DataReceived` |
| `Read(variable, arrayLength)` / `Read(variable, arrayLength, id)` | Same, with an explicit size |
| `Write(variable, value)` / `Write(variable, value, id)` | Emits a textual write result on `OnWrite`; failures also reach `ErrorReceived` |
| `Pause(time)` | Updates `IsPaused` and `IsPausedObservable`; coordinates paced structure/generated writes |
| `Disconnect()` / `Dispose()` | Ends a session / releases the client's lifetime |

`OnWrite` emits `Success` or `Success,<id>` on success. The production client emits `Exception.ToString()` on a native write failure, without preserving the ID in that string; the simulator emits `Error:<message>` or `Error:<message>,<id>`. Both also report failures through `ErrorReceived`. It does **not** emit a variable name. Retain your own operation ID-to-variable mapping and do not depend on correlation being present in native failure text. These are ADS operation results; a successful write does not imply the PLC program has accepted a command or reached the requested state.

```csharp
using var requested = Bridge.SubscribeTo(
    AdsApi.Observe(client, ".Main.State", "state-42", Convert.ToInt16),
    value => Console.WriteLine($"Request state-42: {value}"));
client.Read(".Main.State", "state-42");
client.Read(".Main.BatchName", arrayLength: 80, id: "batch-42");
// Only after application validation and controller permission:
client.Write(".Main.Setpoint", 12.5f, "setpoint-42");
```

Reads/writes return `void`, not a completion task. Disposing a data subscription stops observation; it does not cancel an ADS operation already submitted. No cancellation token is exposed on these synchronous request methods. `Pause` is coordination state, not a queued write delay, lock, PLC transaction or acknowledgement. Use a PLC handshake for related commands.

`ReadWriteHandleInfo` and `WriteHandleInfo` expose configured handles for diagnostics; `Settings`, `IsPaused`, `IsPausedObservable`, `IsDisposed` and `Code` expose session/configuration information. `Code` publishes generated structure source. Both concrete clients expose `Connected`; the simulator additionally exposes `ConnectionState` and `ConnectionStates`.

## Typed and asynchronous observation

`AdsApi.Observe<T>(client, variable, converter)` filters variable names case-insensitively and skips null data. Its `(client, variable, id, converter)` overload additionally matches IDs ordinally. Supply the conversion for the PLC type; a converter exception is an observation failure, not an ADS retry. Observation alone does not register a notification or request a read.

```csharp
using ReactiveUI.Primitives;
using ReactiveUI.Primitives.Extensions;

using var temperatures = Bridge.SubscribeTo(
    AdsApi.Observe(client, ".Main.Temperature", Convert.ToSingle).DistinctUntilChanged(),
    temperature => Console.WriteLine($"Temperature: {temperature}"));
```

`InitializeCompleteAsync`, `DataReceivedAsync`, `ErrorReceivedAsync`, `OnWriteAsync` and `IsPausedObservableAsync` expose `IObservableAsync<T>`. `AdsApi.ObserveAsyncObservable` has the same two variable/converter and variable/ID/converter forms as `Observe`. These adapt event streams; they do not turn ADS requests into asynchronous methods.

```csharp
using System.Threading;
using ReactiveUI.Primitives;
using ReactiveUI.Primitives.Async;

var source = AdsApi.ObserveAsyncObservable(client, ".Main.State", Convert.ToInt16);
await using var subscription = await source.SubscribeAsync(
    new PrintingObserver<short>(), CancellationToken.None);
// Keep this scope alive while consuming values.

sealed class PrintingObserver<T> : IObserverAsync<T>
{
    public ValueTask OnNextAsync(T value, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        Console.WriteLine(value);
        return default;
    }
    public ValueTask OnErrorResumeAsync(Exception error, CancellationToken cancellationToken)
    {
        Console.Error.WriteLine(error);
        return default;
    }
    public ValueTask OnCompletedAsync(Result result) => default;
    public ValueTask DisposeAsync() => default;
}
```

`Bridge.ToAsyncObservable(source)` adapts any ordinary `IObservable<T>`. `Bridge.SubscribeTo(source)`, `SubscribeTo(source, onNext)` and `SubscribeTo(source, onNext, onError, onCompleted)` subscribe to ordinary observables and return `IDisposable`. Retain/dispose them. A value-only subscription rethrows an observable terminal error; provide all handlers when you need explicit terminal-error handling.

## Structured values

`AdsApi.CreateStruct(client, variable)` returns a disposable `HashTableRx` abstraction linked to the variable's updates. It creates the table before data arrives, does not validate symbol existence, and does not request an initial read. Configure a notification or issue a read after subscribing. `StructureReady(table)` waits for the first nonempty table update, then delays two seconds; it is not an immediate handle-readiness event.

```csharp
using var recipe = AdsApi.CreateStruct(client, ".Main.Recipe")
    ?? throw new InvalidOperationException("A client is required.");
var populated = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
using var recipeReady = Bridge.SubscribeTo(AdsApi.StructureReady(recipe),
    _ => populated.TrySetResult(true));
client.Read(".Main.Recipe");
await populated.Task.WaitAsync(TimeSpan.FromSeconds(10));

using var snapshot = AdsApi.CreateClone(recipe);
snapshot["TargetWeight"] = 12.5f;
bool submitted = AdsApi.WriteValues(recipe, copy => copy["TargetWeight"] = 12.5f);
bool pacedSubmission = await AdsApi.WriteValuesAsync(recipe,
    copy => copy["Enabled"] = true, TimeSpan.FromMilliseconds(150));
Console.WriteLine($"Submitted: {submitted}, {pacedSubmission}");
```

Register `.Main.Recipe` as writable before connecting for the write example. `CreateClone` creates an independently disposable table snapshot. `WriteValues` clones, edits and submits the structure. The `bool` means local preparation/submission succeeded; inspect `OnWrite`/`ErrorReceived` for the ADS outcome. `WriteValuesAsync(table, edit, time)` waits if the client is already paused; otherwise it starts a pause window and submits immediately. Structure mapping can use reflection and carries trimming warnings.

## Logical tags, shared reads and writes

`TwinCatLogicalTagClient` composes any `IRxTcAdsClient` with an `ILogicalTagCatalog`, optional `LogicalTagSqliteStore`, and optional `TimeProvider`. It does not connect or own the supplied ADS client. A catalog you supply remains caller-owned; a catalog created internally is disposed with the facade. `CreateTag` registers its result; use `RegisterTag` for an existing definition and `RemoveTag` to remove a live mapping.

This complete in-memory example exercises the same facade used with a PLC:

```csharp
using System;
using System.Threading;
using IoT.Driver.Core;
using IoT.Driver.TwinCATRx;
using IoT.Driver.TwinCATRx.Core;
using Bridge = IoT.Driver.TwinCATRx.ObservableBridgeExtensions;

using var ads = new InMemoryAdsClient();
ads.RegisterSymbol(".Main.Speed", (short)500);
ads.Connect(new Settings { Port = 851, SettingsId = "logical-demo" });
using var tags = new TwinCatLogicalTagClient(ads);
tags.CreateTag("Line.Speed", ".Main.Speed", "Int16");
using var changes = Bridge.SubscribeTo(tags.Observe("Line.Speed"),
    value => Console.WriteLine($"{value.TagName}: {value.Value}"));

using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
var read = await tags.ReadAsync("Line.Speed", timeout.Token);
if (read.Succeeded) Console.WriteLine(read.Value!.Value);
else Console.Error.WriteLine(read.Error);

var results = await tags.WriteManyAsync(new[]
{
    new LogicalTagValue("Line.Speed", (short)900, TimeProvider.System.GetUtcNow(), "Good")
}, timeout.Token);
foreach (var result in results)
    Console.WriteLine(result.Succeeded ? "Written" : result.Error);
```

`ReadManyAsync`/`WriteManyAsync` preserve per-tag result reporting while grouping shared native addresses/structure roots, reducing repeated ADS operations. `Observe`/`ObserveMany` return `IObservable<LogicalTagValue>`. Their `ObserveAsync`/`ObserveManyAsync` counterparts return **`IAsyncEnumerable<LogicalTagValue>`**, consumed with `await foreach`, not `IObservableAsync<T>`. Use cancellation for live async enumerations and request timeouts.

For a live async consumer, pass a token and keep a producer connected while enumerating:

```csharp
using var stopObserving = new CancellationTokenSource(TimeSpan.FromSeconds(30));
try
{
    await foreach (var value in tags.ObserveManyAsync(new[] { "Line.Speed" }, stopObserving.Token))
        Console.WriteLine($"{value.TagName}: {value.Value}");
}
catch (OperationCanceledException) when (stopObserving.IsCancellationRequested)
{
    Console.WriteLine("Observation stopped");
}
```

Logical tags support access modes, group/description/scan interval, and immutable metadata through `LogicalTagOptions`. Structure routing uses `StructureRoot` and `MemberAddress`; an alternate `WriteAddress` can route commands separately from observed values. Grouped writes are not a PLC transaction. Observe or read a controller acknowledgement when application completion depends on it.

## CSV, SQLite and groups

CSV import/export work with the live catalog even without a store. `ImportCsvAsync` accepts a `TextReader`, optional delimiter, `replaceExisting`, and cancellation; replacement concerns matching tags. `ExportCsvAsync` accepts a `TextWriter`, optional delimiter, and cancellation. Export a known catalog to obtain the supported column format rather than constructing a guessed schema.

```csharp
using var output = new StringWriter();
await tags.ExportCsvAsync(output, CancellationToken.None);
string csv = output.ToString();
using var input = new StringReader(csv);
await tags.ImportCsvAsync(input, replaceExisting: true, CancellationToken.None);
```

For persistence construct the facade with a store, initialize it, and load definitions explicitly:

```csharp
var store = new LogicalTagSqliteStore("Data Source=logical-tags.db");
using var persisted = new TwinCatLogicalTagClient(ads, store);
await persisted.InitializeStoreAsync(CancellationToken.None);
await persisted.UpsertTagAsync(new LogicalTag("Line.Speed", ".Main.Speed", "Int16"),
    CancellationToken.None);
await persisted.LoadTagsAsync(replaceExisting: true, CancellationToken.None);
var definitions = await persisted.ListTagsAsync(CancellationToken.None);
await persisted.UpsertGroupAsync(new LogicalTagGroup("Line", "Line A tags"), CancellationToken.None);
var groups = await persisted.ListGroupsAsync(CancellationToken.None);
```

`GetTagAsync`, `ListTagsAsync`, `UpsertTagAsync`, `EditTagAsync` and `DeleteTagAsync` manage stored definitions; upsert/edit/delete also update the live catalog when successful. `GetGroupAsync`, `ListGroupsAsync`, `UpsertGroupAsync` and `DeleteGroupAsync` manage persisted groups. Store operations require a configured store. Importing CSV updates the live registry; persist imported definitions with `UpsertTagAsync` when desired. Register the required native notification/write handles before connecting; adding a logical mapping does not retrofit native session settings.

## In-memory ADS simulator

`InMemoryAdsClient` implements the ADS contract without a route or TwinCAT installation. It is useful for commissioning UI, deterministic tests and fault scenarios. Register symbols before connecting, including explicit types, array sizes and read/write permissions through the full `RegisterSymbol` overload. `RegisterStructure<T>` registers class structures. `SetValue` changes a symbol; `TryGetValue<T>` inspects it; `RemoveSymbol` removes it and associated handles.

```csharp
using var simulator = new InMemoryAdsClient();
simulator.RegisterSymbol(".Main.Counter", 0);
var simulatedSettings = new Settings { Port = 851, SettingsId = "simulation" };
AdsSettings.AddNotification(simulatedSettings, ".Main.Counter");
AdsSettings.AddWriteVariable(simulatedSettings, ".Main.Counter");
using var faults = Bridge.SubscribeTo(simulator.ErrorReceived, Console.Error.WriteLine);
using var data = Bridge.SubscribeTo(simulator.DataReceived,
    value => Console.WriteLine($"{value.Variable}: {value.Data}, ID={value.Id}"));
simulator.Connect(simulatedSettings);
simulator.SetValue(".Main.Counter", 2);
simulator.PublishNotifications();
simulator.ReadMany(new[] { ".Main.Counter" }, "baseline");
simulator.WriteMany(new[] { new KeyValuePair<string, object>(".Main.Counter", 3) }, "commands");
simulator.QueueFault(InMemoryAdsOperation.Write, new InMemoryAdsException("planned failure"));
simulator.Write(".Main.Counter", 4, "fault-case");
Console.WriteLine(simulator.GetOperationMetrics().WriteOperations);
simulator.ResetOperationMetrics();
simulator.Reconnect();
```

`QueueFault` queues a failure for the specified operation. `OperationMetrics` and `GetOperationMetrics()` return snapshots with `ReadOperations`, `WriteOperations` and `NotificationPublications`; counts include attempts, including failed operations. `ResetOperationMetrics` resets counters. `ConnectionState`/`ConnectionStates` and `Symbols` expose simulator state. `ReadMany`/`WriteMany` are simulator conveniences, not methods on `IRxTcAdsClient` or the production client. Bulk simulator requests execute in enumeration order. Notification publication is explicit and synchronous; this does not simulate PLC scan timing or real network scheduling.

## Generate typed connections

Install the standalone generator alongside the runtime. The runtime package supplies the public attribute classes in its own namespace; the analyzer generates members for attributed partial classes. Use attributes from the same runtime variant as your client. The example is a complete console program with declarations after its top-level statements:

```csharp
using System;
using IoT.Driver.TwinCATRx;
using Bridge = IoT.Driver.TwinCATRx.ObservableBridgeExtensions;

using var simulator = new InMemoryAdsClient();
simulator.RegisterSymbol(".Main.Temperature", 20f);
simulator.RegisterSymbol(".Main.Setpoint", 0f);
var model = new LineConnection();
using var temperatures = Bridge.SubscribeTo(model.TemperatureObservable, Console.WriteLine);
using var binding = model.BindTwinCatRx(simulator);
simulator.Connect(model.CreateTwinCatRxSettings());
model.ReadTemperature();
model.WriteSetpoint(22f);
Console.WriteLine(model.Temperature);

[TwinCatPlcConnection("5.35.59.10.1.1", 851, SettingsId = "LineA")]
public sealed partial class LineConnection
{
    [DirectNotification(".Main.Temperature", CycleTime = 250, CanWrite = false)]
    public float Temperature { get; private set; }

    [WriteOnly(".Main.Setpoint")]
    public float Setpoint { get; private set; }
}
```

For a live connection use `using var connection = model.ConnectTwinCatRx()` in place of the simulator/binding setup. It creates and owns a production client, binds it, builds settings and starts connecting. Keep the returned lifetime alive; generated helpers still require initialized handles before live operations.

| Attribute | Configuration and generated behavior |
| --- | --- |
| `TwinCatPlcConnection(adsAddress, port)` | `SettingsId`; generates `CreateTwinCatRxSettings`, `BindTwinCatRx`, `ConnectTwinCatRx` and operation helpers |
| `DirectNotification(address)` | `CycleTime`, `ArraySize`, `Id`, `ObservableName`, `CanWrite`, `WriteAddress`; generates value observation, direct read and optional write helpers |
| `StructuredNotification(rootAddress, memberAddress)` | Same notification/write options; projects a member from a shared structure root and optionally writes it |
| `WriteOnly(address)` | `ArraySize`, `Id`; generates write helpers without observation/read |
| `TwinCatReactiveStream(variable, dataType)` | Focused stream model with configurable `PropertyName`, `ObservableName` and other attribute options; generates binding and typed stream members |

Generated connection settings use polling registrations. `DirectNotification` is a generator attribute name; it does not mean native `AddAdsNotification`. Default observable names append `Observable` to the property name. Only direct notification properties receive `Read<Property>()`; writable properties receive `Write<Property>(value)`. Generated `WriteTwinCatRx(params (string Tag, object? Value)[] values)` coordinates named writes, including shared structures. Typed `Read<Property>Async`/`Write<Property>Async` helpers return `Task<TagOperationResult<T>>` and accept cancellation; these use the composed logical-tag facade. The model also exposes `<Property>Tag`, `<Property>ObservableAsync`, `TwinCatRxClient` and `TwinCatRxLogicalTags`. Inspect generated output/compiler diagnostics to resolve unsupported member declarations.

For a focused stream, declare this class in a separate source file (or after all top-level program statements):

```csharp
using IoT.Driver.TwinCATRx;

[TwinCatReactiveStream(".Main.Counter", typeof(short),
    PropertyName = "Counter", ObservableName = "CounterValues")]
public sealed partial class CounterStream { }
```

Instantiate `CounterStream`, subscribe to `CounterValues`, retain `BindTwinCatRx(client)`, then request a read or configure notifications on the supplied client. Binding does not connect it. Source generation removes the need to write application stream wiring by hand; it does not make the production client's runtime ADS structure generation Native AOT-safe.

## ADS state, retry and dynamic type generation

The core helpers can be used separately from the runtime. `AdsStateChangedObserver(AdsClient)` observes state-change events. `AdsStateObserver(AdsClient)` polls once per second and emits `AdsState.Invalid` while disconnected. `OnErrorRetry` has unbounded, typed callback, retry-count, delay, and sequencer/scheduler overloads. Retrying an observable resubscribes to that observable; it does not automatically rebuild an ADS route or replay a failed command safely.

```csharp
using TwinCAT.Ads;

using var native = new AdsClient();
native.Connect("5.35.59.10.1.1", 851);
using var state = Bridge.SubscribeTo(
    AdsSettings.OnErrorRetry<StateInfo, Exception>(AdsSettings.AdsStateObserver(native),
        ex => Console.Error.WriteLine(ex.Message), retryCount: 3, delay: TimeSpan.FromSeconds(1)),
    value => Console.WriteLine(value.AdsState),
    ex => Console.Error.WriteLine($"State observer ended: {ex.Message}"),
    () => Console.WriteLine("State observer completed"));
// Retain this scope for the monitoring session.
```

`CodeGenerator`/`ICodeGenerator` load/search/read symbol trees and emit C# source or DLLs through `CreateCSharpCode`, `CreateCSharpCodeString` and `CreateDll`. `PLCToCSharpTypeConverter` maps PLC type names. `CSharpLanguage`/`ILanguageService` expose parsing, library compilation and assembly creation. `INodeEmulator`, `DirectoryInfoExtensions.GetFilesWhere`, `AssemblyLoad` and `GetType` support symbol/code-loading workflows. `SimpleTypeException` and the API's historical spelling `UnsuportedTypeException` identify type-conversion failures.

```csharp
using var generator = new CodeGenerator(Console.Error.WriteLine);
var roots = generator.LoadSymbols("5.35.59.10.1.1", 851);
foreach (var root in roots)
{
    string source = generator.CreateCSharpCodeString(root, true, "LineA.Generated");
    Console.WriteLine(source);
    break;
}
```

Use dynamic compilation/loading in a trusted engineering application with a writable output location. `Connect` and dynamic code APIs carry `RequiresDynamicCode`/`RequiresUnreferencedCode`; structure helpers also carry reflection warnings. Validate the exact published artifact when trimming. Choosing generated typed models does not remove these ADS runtime restrictions.

## Windows service monitoring

On Windows targets, `ObservableServiceController` wraps a `System.ServiceProcess.ServiceController`, optionally with a refresh interval. `Status`, `StatusObserver`, `CanStop`, `DisplayName`, `ServiceName` and `IsDisposed` expose its state. Static `GetServices()` returns observable controller wrappers; dispose each wrapper you retain. `Start`, `Stop` and `Restart` require appropriate Windows service permissions and alter host services.

```csharp
using System.ServiceProcess;

using var nativeService = new ServiceController("TcSysSrv");
using var service = new ObservableServiceController(nativeService, TimeSpan.FromSeconds(2));
using var status = Bridge.SubscribeTo(service.StatusObserver, value => Console.WriteLine(value));
Console.WriteLine(service.Status);
Console.ReadLine();
```

## Additional API examples

### Register, read, write, and observe TwinCAT logical tags

These methods take an existing connected `IRxTcAdsClient`. Replace the example ADS symbol paths with symbols registered and configured on that native client. The logical client composes the native client; disposing it does not dispose the native client. Use `IoT.Driver.TwinCATRx.Reactive` in place of `IoT.Driver.TwinCATRx` for the Reactive package.

```csharp
using System;
using System.Threading;
using System.Threading.Tasks;
using IoT.Driver.Core;
using IoT.Driver.TwinCATRx;

static async Task UseLogicalTagsAsync(IRxTcAdsClient native, CancellationToken cancellationToken)
{
    using var client = new TwinCatLogicalTagClient(native);
    var speed = new LogicalTag("Motor.Speed", "MAIN.MotorSpeed", "REAL");
    client.RegisterTag(speed); // Adds or replaces a live catalog definition.
    var enabled = client.CreateTag(new LogicalTag("Motor.Enabled", "MAIN.MotorEnabled", "BOOL"));
    var counter = client.CreateTag("Motor.Counter", "MAIN.MotorCounter", "DINT");

    var single = await client.ReadAsync(speed.Name, cancellationToken);
    var results = await client.ReadManyAsync(new[] { speed.Name, enabled.Name }, cancellationToken);
    foreach (var result in results)
    {
        Console.WriteLine(result.Succeeded ? result.Value?.Value : result.Error);
    }

    var written = await client.WriteAsync(
        new LogicalTagValue(enabled.Name, true, DateTimeOffset.UtcNow), cancellationToken);
    Console.WriteLine(written.Succeeded ? "Write completed" : written.Error);

    // A concrete observer works with both package variants without Rx extension imports.
    using (client.ObserveMany(new[] { speed.Name, enabled.Name }).Subscribe(new TagObserver()))
    {
        await Task.Delay(TimeSpan.FromSeconds(5), cancellationToken);
    }

    // Break after one update; the caller's cancellation token also bounds the wait.
    await foreach (var value in client.ObserveAsync(speed.Name, cancellationToken))
    {
        Console.WriteLine($"{value.TagName}: {value.Value}");
        break;
    }
    await foreach (var value in client.ObserveManyAsync(new[] { speed.Name, enabled.Name }, cancellationToken))
    {
        Console.WriteLine($"{value.TagName}: {value.Value}");
        break;
    }

    Console.WriteLine(client.RemoveTag(counter.Name)); // Removes only the live definition.
}

sealed class TagObserver : IObserver<LogicalTagValue>
{
    public void OnNext(LogicalTagValue value) => Console.WriteLine($"{value.TagName}: {value.Value}");
    public void OnError(Exception error) => Console.WriteLine(error.Message);
    public void OnCompleted() => Console.WriteLine("Observation completed");
}
```

### Manage the in-memory catalog

Catalog names use ordinal, case-sensitive comparison. `List()` returns a name-sorted snapshot. Tags are immutable; copy methods return new definitions.

```csharp
using System;
using IoT.Driver.Core;

using var catalog = new LogicalTagCatalog();
catalog.Changed += (_, change) => Console.WriteLine(change);
var tag = new LogicalTag("Motor.Speed", "MAIN.MotorSpeed", "REAL", new LogicalTagOptions
{
    GroupName = "Motor",
    Description = "Motor speed",
    AccessMode = LogicalTagAccessMode.ReadWrite,
    ScanInterval = TimeSpan.FromMilliseconds(100),
});
Console.WriteLine(catalog.TryAdd(tag));
catalog.Upsert(tag.WithAddress("MAIN.ActualSpeed"));
if (catalog.TryGet(tag.Name, out var found) && found is not null)
{
    var options = found.CurrentOptions();
    catalog.Upsert(found.WithDataType("LREAL").WithOptions(options));
}
foreach (var registered in catalog.List())
{
    Console.WriteLine($"{registered.Name}: {registered.Address}");
}
Console.WriteLine(catalog.TryRemove(tag.Name, out var removed));
```

### SQLite tag and group CRUD

This independent example creates a database file in the current directory. `GetTagAsync` and `GetGroupAsync` return null for missing rows; edit and delete operations return false when no matching row exists. Deleting a group does not delete its tags. Direct store operations do not update an already loaded live catalog.

```csharp
using System;
using System.Threading;
using IoT.Driver.Core;

var cancellationToken = CancellationToken.None;
var store = new LogicalTagSqliteStore("Data Source=logical-tags-demo.db");
await store.InitializeAsync(cancellationToken);
await store.UpsertGroupAsync(new LogicalTagGroup("Motor", "Motor control tags"), cancellationToken);
var tag = new LogicalTag("Motor.Speed", "MAIN.MotorSpeed", "REAL",
    new LogicalTagOptions { GroupName = "Motor" });
await store.UpsertTagAsync(tag, cancellationToken);
var persisted = await store.GetTagAsync(tag.Name, cancellationToken);
if (persisted is not null)
{
    Console.WriteLine(await store.EditTagAsync(persisted.WithAddress("MAIN.ActualSpeed"), cancellationToken));
}
foreach (var item in await store.ListTagsAsync(cancellationToken))
{
    Console.WriteLine(item.Name);
}
var group = await store.GetGroupAsync("Motor", cancellationToken);
foreach (var item in await store.ListGroupsAsync(cancellationToken))
{
    Console.WriteLine(item.Name);
}
using (var loaded = await store.LoadCatalogAsync(cancellationToken))
{
    Console.WriteLine(loaded.List().Count);
}
Console.WriteLine(await store.DeleteTagAsync(tag.Name, cancellationToken));
Console.WriteLine(await store.DeleteGroupAsync("Motor", cancellationToken));
```

The store also supplies parameterless-token overloads, for example `await store.GetTagAsync("Motor.Speed")` and `await store.DeleteGroupAsync("Motor")`.

### Keep TwinCAT persistence and the live catalog together

Pass the store at construction. Client `UpsertTagAsync`, successful `EditTagAsync`, and successful `DeleteTagAsync` update both SQLite and its live catalog. `LoadTagsAsync` imports persisted definitions; it does not clear live tags absent from the database. Group operations require an explicit cancellation token.

```csharp
static async Task ManagePersistedTagsAsync(IRxTcAdsClient native, CancellationToken cancellationToken)
{
    var store = new LogicalTagSqliteStore("Data Source=logical-tags-demo.db");
    using var client = new TwinCatLogicalTagClient(native, store);
    await client.InitializeStoreAsync(cancellationToken);
    await client.UpsertGroupAsync(new LogicalTagGroup("Motor", "Motor control tags"), cancellationToken);
    var tag = new LogicalTag("Motor.Speed", "MAIN.MotorSpeed", "REAL",
        new LogicalTagOptions { GroupName = "Motor" });
    await client.UpsertTagAsync(tag, cancellationToken);
    var saved = await client.GetTagAsync(tag.Name, cancellationToken);
    if (saved is not null)
    {
        Console.WriteLine(await client.EditTagAsync(saved.WithAddress("MAIN.ActualSpeed"), cancellationToken));
    }
    var persistedTags = await client.ListTagsAsync(cancellationToken);
    var loadedTags = await client.LoadTagsAsync(replaceExisting: false, cancellationToken);
    var group = await client.GetGroupAsync("Motor", cancellationToken);
    var groups = await client.ListGroupsAsync(cancellationToken);
    using var csv = new System.IO.StringWriter();
    await client.ExportCsvAsync(csv, ';', cancellationToken);
    using var input = new System.IO.StringReader(csv.ToString());
    await client.ImportCsvAsync(input, ';', replaceExisting: true, cancellationToken);
    Console.WriteLine(await client.DeleteTagAsync(tag.Name, cancellationToken));
    Console.WriteLine(await client.DeleteGroupAsync("Motor", cancellationToken));
}
```

### Simulator, pause, and bridge operations

```csharp
using System;
using IoT.Driver.TwinCATRx;

using var simulator = new InMemoryAdsClient();
simulator.RegisterSymbol(".Main.Counter", 1); // inferred type
simulator.RegisterSymbol(".Main.OptionalName", null, typeof(string)); // declared type
simulator.RegisterSymbol(".Main.Samples", new[] { 1, 2, 3 }, typeof(int[]),
    arrayLength: 3, isReadable: true, isWritable: false);
simulator.RegisterStructure(".Main.Recipe", new Recipe { Target = 12.5f });
simulator.RegisterStructure(".Main.Status", new Recipe { Target = 8f },
    isReadable: true, isWritable: false);

simulator.SetValue(".Main.Counter", 2); // simulate a PLC-side change
if (simulator.TryGetValue<int>(".Main.Counter", out var counter))
    Console.WriteLine(counter);
foreach (var symbol in simulator.Symbols)
    Console.WriteLine($"{symbol.Name}: {symbol.DataType.Name}, writable={symbol.IsWritable}");
bool removed = simulator.RemoveSymbol(".Main.Samples");
Console.WriteLine($"Removed: {removed}");

public sealed class Recipe
{
    public float Target { get; set; }
}
```

`RegisterSymbol` replaces a matching name; the full overload specifies declared type, array/string length (`-1` for a scalar) and read/write permissions. `RegisterStructure<T>` requires a non-null class instance. Names are case-insensitive. `TryGetValue<T>` succeeds only when the stored value is compatible with `T`; it does not perform numeric conversion. `SetValue` models a PLC-side update and can update a read-only symbol; `RemoveSymbol` also removes configured handles.

```csharp
using System;
using System.Threading.Tasks;
using IoT.Driver.TwinCATRx;
using Bridge = IoT.Driver.TwinCATRx.ObservableBridgeExtensions;

using var simulator = new InMemoryAdsClient();
var resumed = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
bool sawPause = false;
using var state = Bridge.SubscribeTo(simulator.IsPausedObservable, isPaused =>
{
    Console.WriteLine($"Paused: {isPaused}");
    if (isPaused) sawPause = true;
    else if (sawPause) resumed.TrySetResult(true);
});
simulator.Pause(TimeSpan.FromMilliseconds(100));
await resumed.Task.WaitAsync(TimeSpan.FromSeconds(5));
```

The simulator publishes its pause state and automatically clears it after a positive duration; a nonpositive duration has no effect. On the production client, call `Pause` after initialization: the pause is scheduled on the active connection.

```csharp
using System;
using System.Threading;
using IoT.Driver.TwinCATRx;
using IoT.Driver.TwinCATRx.Core;
using Bridge = IoT.Driver.TwinCATRx.ObservableBridgeExtensions;

using var simulator = new InMemoryAdsClient();
simulator.RegisterSymbol(".Main.Counter", 1);
simulator.Connect(new Settings { Port = 851, SettingsId = "bridge-demo" });

using var ignored = Bridge.SubscribeTo(simulator.DataReceived);
using var values = Bridge.SubscribeTo(simulator.DataReceived,
    item => Console.WriteLine($"{item.Variable}: {item.Data}"));
using var handled = Bridge.SubscribeTo(simulator.DataReceived,
    item => Console.WriteLine(item.Data),
    error => Console.Error.WriteLine(error),
    () => Console.WriteLine("Completed"));

var asyncSource = Bridge.ToAsyncObservable(simulator.DataReceived);
await using var asyncValues = await asyncSource.SubscribeAsync(
    new PrintingObserver<(string Variable, object? Data, string? Id)>(), CancellationToken.None);
simulator.Read(".Main.Counter");
```

### Default settings and ADS state events

`Defaults` creates a new settings instance; it does not copy values from its argument. Its built-in defaults contain `.UIStructure` and `.FailSafeCounter`. Replace those names to match your PLC before connecting.

```csharp
using IoT.Driver.TwinCATRx;
using IoT.Driver.TwinCATRx.Core;
using TwinCAT.Ads;

var settings = new Settings().Defaults(new Settings());
settings.AdsAddress = "1.2.3.4.5.6"; // Replace with your configured AMS Net ID.
settings.Port = 851;                 // TwinCAT 3 PLC runtime 1.

using var ads = new AdsClient();
using var stateChanges = ObservableBridgeExtensions.SubscribeTo(
    IoT.Driver.TwinCATRx.Core.TwinCatRxExtensions.AdsStateChangedObserver(ads),
    change => Console.WriteLine(change),
    error => Console.Error.WriteLine(error),
    () => Console.WriteLine("State observation ended."));
ads.Connect(settings.AdsAddress, settings.Port);
// Keep ads and stateChanges alive while your application needs events.
```

This observes the client's ADS state changed events; it does not poll state or emit an initial snapshot. ADS operations require an installed/configured ADS runtime/router and a reachable PLC with a matching route.

### Read symbols and generate PLC structure types

Use the exact published PLC symbol names. Generation needs a structure node with children; a missing search returns an empty node. The read type must match the PLC value layout. The example assumes a `MAIN.UiStructure` structure and a `MAIN.Counter` DINT exist.

```csharp
using System.IO;
using IoT.Driver.TwinCATRx.Core;

const string amsNetId = "1.2.3.4.5.6";
const int adsPort = 851;
using var generator = new CodeGenerator(error => Console.Error.WriteLine(error));
generator.LoadSymbols(amsNetId, adsPort);
var structure = generator.SearchSymbols("MAIN.UiStructure");
var counter = (int)generator.ReadSymbol(amsNetId, adsPort, "MAIN.Counter", typeof(int));
Console.WriteLine(counter);

var output = Directory.CreateDirectory(Path.Combine(Path.GetTempPath(), "TwinCATRxGenerated"));
if (structure.Nodes.Count > 0)
{
    bool sourceWritten = generator.CreateCSharpCode(
        structure, Path.Combine(output.FullName, "PlcTypes.cs"),
        isTwinCat3: true, classNamespace: "Sample.Plc");
    bool dllWritten = generator.CreateDll(
        structure, Path.Combine(output.FullName, "PlcTypes.dll"),
        isTwinCat3: true, classNamespace: "Sample.Plc");
    Console.WriteLine($"Source: {sourceWritten}; DLL: {dllWritten}");
}

Console.WriteLine(CodeGenerator.PLCToCSharpTypeConverter("DINT"));       // System.Int32
Console.WriteLine(CodeGenerator.PLCToCSharpTypeConverter("STRING(42)")); // System.String,42
```

The converter returns a representation string, which can include comma-separated size metadata, and throws for unsupported PLC types. DLL generation uses runtime compilation and requires a JIT deployment with available reference assemblies; review the dynamic-code/trimming annotations before using it in trimmed or Native AOT applications.

### Compile C# source, inspect files, and resolve generated types

These helpers can be used independently of a PLC. Only compile and load trusted source and assemblies. Use a writable output directory and check the returned success values; file access failures can throw.

```csharp
using System.IO;
using IoT.Driver.TwinCATRx.Core;
using Microsoft.CodeAnalysis;

var output = Directory.CreateDirectory(Path.Combine(Path.GetTempPath(), "TwinCATRxCompilation"));
const string source = "namespace Sample { public sealed class Payload { public int Count; } }";
var language = new CSharpLanguage();
var syntax = language.ParseText(source, SourceCodeKind.Regular);
var compilation = language.CreateLibraryCompilation("PayloadPreview", enableOptimisations: true)
    .AddSyntaxTrees(syntax);
Console.WriteLine(compilation.AssemblyName);

string directDll = Path.Combine(output.FullName, "DirectPayload.dll");
bool directCreated = CSharpLanguage.CreateAssembly(source, directDll);
using var generator = new CodeGenerator(error => Console.Error.WriteLine(error));
bool generatorCreated = generator.CreateDll(
    "namespace OtherSample { public sealed class Payload { public int Count; } }",
    Path.Combine(output.FullName, "OtherPayload.dll"));
Console.WriteLine($"Direct: {directCreated}; generator: {generatorCreated}");

var dlls = DirectoryInfoExtensions.GetFilesWhere(
    output, "*.dll", SearchOption.TopDirectoryOnly, file => file.Length > 0);
var selectedFiles = DirectoryInfoExtensions.GetFilesWhere(
    output, new[] { "*.dll", "*.cs" }, SearchOption.AllDirectories, file => file.Length > 0);
Console.WriteLine($"DLLs: {dlls.Length}; selected files: {selectedFiles.Length}");

if (directCreated)
{
    var assembly = TwinCatRxExtensions.AssemblyLoad(directDll);
    var payloadType = TwinCatRxExtensions.GetType(directDll, "Sample.Payload");
    Console.WriteLine($"{assembly?.FullName}: {payloadType?.FullName}");
}
```

`AssemblyLoad` returns `null` for a missing file; `GetType` also returns `null` for an absent type. Invalid DLLs and dependency/loading errors can throw. Both use runtime reflection/loading and carry dynamic-code and trimming annotations. `GetFilesWhere` also has predicate-only, single-pattern, and multiple-pattern overloads that default to the top directory.

### Windows service discovery and control

`ObservableServiceController` requires Windows service APIs and sufficient permissions. Use a service you own for control operations. Start, stop, and restart wait synchronously for the requested status and can block; run them away from a UI thread.

```csharp
using System.ServiceProcess;
using IoT.Driver.TwinCATRx;

// Keep this subscription alive: it owns the emitted wrappers.
using var services = ObservableBridgeExtensions.SubscribeTo(
    ObservableServiceController.GetServices(),
    service => Console.WriteLine($"{service.ServiceName}: {service.Status}"),
    error => Console.Error.WriteLine(error),
    () => Console.WriteLine("Service discovery completed."));

// Explicit, opt-in control of a service owned by this application.
static void ControlOwnedService(string serviceName, string operation)
{
    using var service = new ObservableServiceController(new ServiceController(serviceName));
    switch (operation)
    {
        case "start":
            service.Start();
            break;
        case "stop" when service.CanStop:
            service.Stop();
            break;
        case "restart":
            service.Restart();
            break;
    }
}
// Invoke ControlOwnedService with an appropriate service name only when needed.
```

### Retry observable reads with a bounded policy

The static retry helpers work with `IObservable<T?>`. They resubscribe to their source, so use a source that performs a fresh read on each subscription. Callback overloads handle the specified exception type; other exception types propagate. `retryCount` counts handled failures up to the terminal failure (a count of three permits at most three failing subscriptions).

```csharp
using System.IO;
using IoT.Driver.TwinCATRx.Core;
using ReactiveUI.Primitives.Concurrency;

static void RetryExamples(IObservable<string?> reads)
{
    Action<IOException> log = error => Console.Error.WriteLine(error.Message);
    var retryAnyError = TwinCatRxExtensions.OnErrorRetry(reads);
    var retryIoErrors = TwinCatRxExtensions.OnErrorRetry<string, IOException>(reads, log);
    var delayedIoRetry = TwinCatRxExtensions.OnErrorRetry<string, IOException>(
        reads, log, TimeSpan.FromSeconds(1));
    var boundedIoRetry = TwinCatRxExtensions.OnErrorRetry<string, IOException>(reads, log, 3);
    var boundedDelayedIoRetry = TwinCatRxExtensions.OnErrorRetry<string, IOException>(
        reads, log, 3, TimeSpan.FromSeconds(1));
    var explicitlyScheduledRetry = TwinCatRxExtensions.OnErrorRetry<string, IOException>(
        reads, log, 3, TimeSpan.FromSeconds(1), TaskPoolSequencer.Default);
    // Subscribe to the chosen policy using ObservableBridgeExtensions.SubscribeTo,
    // and retain/dispose its subscription with the owning application component.
}
```

Overloads without a retry count use `int.MaxValue`. Use a bounded, delayed policy for persistently failing sources to avoid a tight immediate retry loop. The explicit sequencer is `ReactiveUI.Primitives.Concurrency.ISequencer`; it controls the delay scheduling.

### Generated application connection

Reference `IoT-Driver.TwinCATRx` and `IoT-Driver.TwinCATRx.Generators` (the generator is an analyzer). These snippets use the `IoT.Driver.TwinCATRx` namespace and the default ReactiveUI.Primitives observable surface. Use a normal JIT application: ADS structure materialization uses runtime code generation and reflection. Replace the AMS Net ID and PLC symbols with those from your controller, configure an ADS route, and match C# types to PLC types (`float` for `REAL`, `int` for `DINT`). Keep the class `partial` and give attributed properties setters accessible from the generated partial class.

```csharp
using System;
using System.Diagnostics.CodeAnalysis;
using System.Threading;
using System.Threading.Tasks;
using IoT.Driver.TwinCATRx;

[TwinCatPlcConnection("1.2.3.4.5.6", 851, SettingsId = "Application")]
internal sealed partial class MachinePlc
{
    [DirectNotification("MAIN.Counter", CycleTime = 100, CanWrite = true)]
    public int Counter { get; private set; }

    // The notification observes MAIN.Rig; the member path is relative to that root.
    [StructuredNotification("MAIN.Rig", "Pressure.PV", CanWrite = false)]
    public float Pressure { get; private set; }

    [WriteOnly("MAIN.Command")]
    public int Command { get; private set; }
}

internal static class GeneratedConnectionExample
{
    [RequiresDynamicCode("RxTcAdsClient generates PLC structure types at runtime.")]
    [RequiresUnreferencedCode("PLC structure materialization uses reflection.")]
    public static async Task RunAsync(CancellationToken cancellationToken)
    {
        var plc = new MachinePlc();
        using var pressure = ObservableBridgeExtensions.SubscribeTo(
            plc.PressureObservable,
            value => Console.WriteLine($"Pressure: {value}"));
        using var client = new RxTcAdsClient();
        using var connection = plc.BindTwinCatRx(client);
        var initialized = new TaskCompletionSource<bool>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        using var ready = ObservableBridgeExtensions.SubscribeTo(
            client.InitializeComplete, _ => initialized.TrySetResult(true));
        client.Connect(plc.CreateTwinCatRxSettings());
        await initialized.Task.WaitAsync(cancellationToken);

        // Generated typed methods return TagOperationResult<T>.
        var read = await plc.ReadCounterAsync(cancellationToken);
        if (!read.Succeeded)
        {
            throw new InvalidOperationException(read.Error);
        }

        Console.WriteLine($"Counter read: {read.Value}");
        var write = await plc.WriteCounterAsync(42, cancellationToken);
        if (!write.Succeeded)
        {
            throw new InvalidOperationException(write.Error);
        }

        // Name-based application helper queues writes; it returns no result.
        plc.WriteTwinCatRx((nameof(MachinePlc.Command), 1));
        await Task.Delay(Timeout.Infinite, cancellationToken);
    }
}
```

`ConnectTwinCatRx()` creates, binds, and connects an owned client; disposing its handle releases both the binding and client. `BindTwinCatRx(client)` instead binds an existing client and leaves client ownership with the caller. Generated connection members include `<Property>Observable`, `<Property>ObservableAsync`, `<Property>Tag`, typed `Read<Property>Async` for readable tags, and typed `Write<Property>Async` for writable tags. Direct notifications additionally get synchronous `Read<Property>()`. Structured members have typed asynchronous reads, but no synchronous `Read<Property>()`. Write-only tags have no read helper. Observe operation errors through the returned typed result and native client error stream as appropriate; queuing a write does not establish PLC application acknowledgement.

### Focused generated stream

Use `[TwinCatReactiveStream]` for a small projection over an existing client. This form generates properties and observables, `BindTwinCatRx`, logical tags, and typed async operations. It does not generate settings or `ConnectTwinCatRx`; register its symbols in the client settings yourself.

```csharp
using System;
using System.Diagnostics.CodeAnalysis;
using System.Threading;
using System.Threading.Tasks;
using IoT.Driver.TwinCATRx;

[TwinCatReactiveStream(
    "MAIN.Counter", typeof(int),
    PropertyName = "Counter", ObservableName = "CounterChanges")]
internal sealed partial class CounterStreams
{
}

internal static class FocusedStreamExample
{
    [RequiresDynamicCode("RxTcAdsClient generates PLC structure types at runtime.")]
    [RequiresUnreferencedCode("PLC structure materialization uses reflection.")]
    public static async Task RunAsync(CancellationToken cancellationToken)
    {
        var settings = new IoT.Driver.TwinCATRx.Core.Settings
        {
            AdsAddress = "1.2.3.4.5.6",
            Port = 851,
            SettingsId = "FocusedCounter"
        };
        IoT.Driver.TwinCATRx.Core.TwinCatRxExtensions.AddNotification(settings, "MAIN.Counter", cycleTime: 100);
        using var client = new RxTcAdsClient();
        var streams = new CounterStreams();
        using var binding = streams.BindTwinCatRx(client);
        using var subscription = ObservableBridgeExtensions.SubscribeTo(
            streams.CounterChanges,
            value => Console.WriteLine($"Counter notification: {value}"));
        var initialized = new TaskCompletionSource<bool>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        using var ready = ObservableBridgeExtensions.SubscribeTo(
            client.InitializeComplete, _ => initialized.TrySetResult(true));
        client.Connect(settings);
        await initialized.Task.WaitAsync(cancellationToken);

        var result = await streams.ReadCounterAsync(cancellationToken);
        if (!result.Succeeded)
        {
            throw new InvalidOperationException(result.Error);
        }

        await Task.Delay(Timeout.Infinite, cancellationToken);
    }
}
```

Generated observables are behavior signals and initially expose the default value (the focused `int` stream starts with `null`); treat a real notification or successful explicit read as evidence of PLC data. Retain the binding and subscription for the full observation lifetime. In applications, handle cancellation around these demonstration infinite waits.

## Troubleshooting and further reference

- **No initialization:** verify the ADS route, AMS Net ID, port, PLC state and `ErrorReceived`; keep the process alive and use an initialization timeout.
- **No data:** configure notifications before connection, or subscribe and issue a one-shot read after readiness. Observation itself does not read a tag.
- **Size/type errors:** match the PLC scalar type and supply the correct string/array size. Retry does not fix a bad mapping.
- **Write reported success but the machine did not change:** inspect the PLC handshake and interlocks; ADS completion is not application acknowledgement.
- **Empty structure:** subscribe/create the table before reading, then wait for a populated update. `StructureReady` adds a two-second delay.
- **Generator missing members:** install the generator separately, use a partial class and check compiler diagnostics and generated source. Match the runtime variant's attribute namespace.

The packaged [`twincat-rx` skill](../../skills/twincat-rx/SKILL.md) gives concise workflow guidance. The generated reference below lists public signatures from the runtime and core assemblies; private composition adapters and generated application members are not public runtime API.

<!-- BEGIN GENERATED PUBLIC API -->

## Exhaustive public API reference

This catalogue is generated from the packaged runtime assemblies and their XML documentation. It includes exported public types and their declared public members; inherited members and non-public implementation details are intentionally omitted.

### `TwinCATRx`

Exported public types: 19; declared public members: 253.

#### `T:IoT.Driver.TwinCATRx.DirectNotificationAttribute`

```csharp
public class IoT.Driver.TwinCATRx.DirectNotificationAttribute
```
Marks a directly observed TwinCAT property.

##### Declared public members

###### `M:IoT.Driver.TwinCATRx.DirectNotificationAttribute.#ctor(System.String)`

```csharp
public IoT.Driver.TwinCATRx.DirectNotificationAttribute(string address)
```
Initializes a new instance of the `T:IoT.Driver.TwinCATRx.DirectNotificationAttribute` class.

- Parameter `address`: The TwinCAT variable address.

###### `P:IoT.Driver.TwinCATRx.DirectNotificationAttribute.Address`

```csharp
public string Address { get; }
```
Gets the TwinCAT variable address.

- Value: The `Address` value.

###### `P:IoT.Driver.TwinCATRx.DirectNotificationAttribute.ArraySize`

```csharp
public int ArraySize { get; set; }
```
Gets or sets the array size.

- Value: The `ArraySize` value.

###### `P:IoT.Driver.TwinCATRx.DirectNotificationAttribute.CanWrite`

```csharp
public bool CanWrite { get; set; }
```
Gets or sets a value indicating whether writes are generated.

- Value: The `CanWrite` value.

###### `P:IoT.Driver.TwinCATRx.DirectNotificationAttribute.CycleTime`

```csharp
public int CycleTime { get; set; }
```
Gets or sets the notification cycle time.

- Value: The `CycleTime` value.

###### `P:IoT.Driver.TwinCATRx.DirectNotificationAttribute.Id`

```csharp
public string Id { get; set; }
```
Gets or sets the optional correlation identifier.

- Value: The `Id` value.

###### `P:IoT.Driver.TwinCATRx.DirectNotificationAttribute.ObservableName`

```csharp
public string ObservableName { get; set; }
```
Gets or sets the generated observable name.

- Value: The `ObservableName` value.

###### `P:IoT.Driver.TwinCATRx.DirectNotificationAttribute.WriteAddress`

```csharp
public string WriteAddress { get; set; }
```
Gets or sets an optional alternate write address.

- Value: The `WriteAddress` value.

#### `T:IoT.Driver.TwinCATRx.IObservableServiceController`

```csharp
public interface IoT.Driver.TwinCATRx.IObservableServiceController
```
Interface for Observable Service Controller.

##### Declared public members

###### `M:IoT.Driver.TwinCATRx.IObservableServiceController.Restart`

```csharp
public void Restart()
```
Restarts this instance.

###### `M:IoT.Driver.TwinCATRx.IObservableServiceController.Start`

```csharp
public void Start()
```
Starts this instance.

###### `M:IoT.Driver.TwinCATRx.IObservableServiceController.Stop`

```csharp
public void Stop()
```
Stops this instance.

###### `P:IoT.Driver.TwinCATRx.IObservableServiceController.CanStop`

```csharp
public bool CanStop { get; }
```
Gets a value indicating whether this instance can stop.

- Value: true if this instance can stop; otherwise, false .

###### `P:IoT.Driver.TwinCATRx.IObservableServiceController.DisplayName`

```csharp
public string DisplayName { get; }
```
Gets the display name.

- Value: The display name.

###### `P:IoT.Driver.TwinCATRx.IObservableServiceController.ServiceName`

```csharp
public string ServiceName { get; }
```
Gets the name of the service.

- Value: The name of the service.

###### `P:IoT.Driver.TwinCATRx.IObservableServiceController.Status`

```csharp
public System.ServiceProcess.ServiceControllerStatus Status { get; }
```
Gets the status.

- Value: The status.

###### `P:IoT.Driver.TwinCATRx.IObservableServiceController.StatusObserver`

```csharp
public System.IObservable<System.ServiceProcess.ServiceControllerStatus> StatusObserver { get; }
```
Gets the status observer.

- Value: The status observer.

#### `T:IoT.Driver.TwinCATRx.IRxTcAdsClient`

```csharp
public interface IoT.Driver.TwinCATRx.IRxTcAdsClient
```
Interface for Rx Tc Ads Client.

##### Declared public members

###### `M:IoT.Driver.TwinCATRx.IRxTcAdsClient.Connect(IoT.Driver.TwinCATRx.Core.ISettings)`

```csharp
public void Connect(IoT.Driver.TwinCATRx.Core.ISettings settings)
```
Connects the specified settings.

- Parameter `settings`: The settings.

###### `M:IoT.Driver.TwinCATRx.IRxTcAdsClient.Disconnect`

```csharp
public void Disconnect()
```
Disconnects this instance.

###### `M:IoT.Driver.TwinCATRx.IRxTcAdsClient.Pause(System.TimeSpan)`

```csharp
public void Pause(System.TimeSpan time)
```
Pauses the specified time.

- Parameter `time`: The time.

###### `M:IoT.Driver.TwinCATRx.IRxTcAdsClient.Read(System.String)`

```csharp
public void Read(string variable)
```
Reads the specified data.

- Parameter `variable`: The data.

###### `M:IoT.Driver.TwinCATRx.IRxTcAdsClient.Read(System.String,System.Nullable`1{System.Int32})`

```csharp
public void Read(string variable, System.Nullable<int> arrayLength)
```
Executes the `Read` operation.

- Parameter `variable`: The `variable` value.
- Parameter `arrayLength`: The `arrayLength` value.

###### `M:IoT.Driver.TwinCATRx.IRxTcAdsClient.Read(System.String,System.Nullable`1{System.Int32},System.String)`

```csharp
public void Read(string variable, System.Nullable<int> arrayLength, string id)
```
Executes the `Read` operation.

- Parameter `variable`: The `variable` value.
- Parameter `arrayLength`: The `arrayLength` value.
- Parameter `id`: The `id` value.

###### `M:IoT.Driver.TwinCATRx.IRxTcAdsClient.Read(System.String,System.String)`

```csharp
public void Read(string variable, string id)
```
Reads the specified data with a correlation identifier.

- Parameter `variable`: The data.
- Parameter `id`: The identifier.

###### `M:IoT.Driver.TwinCATRx.IRxTcAdsClient.Write(System.String,System.Object)`

```csharp
public void Write(string variable, object value)
```
Writes the specified value.

- Parameter `variable`: The variable.
- Parameter `value`: The value.

###### `M:IoT.Driver.TwinCATRx.IRxTcAdsClient.Write(System.String,System.Object,System.String)`

```csharp
public void Write(string variable, object value, string id)
```
Writes the specified value with a correlation identifier.

- Parameter `variable`: The variable.
- Parameter `value`: The value.
- Parameter `id`: The identifier.

###### `P:IoT.Driver.TwinCATRx.IRxTcAdsClient.Code`

```csharp
public System.IObservable<string[]> Code { get; }
```
Gets the code.

- Value: The code.

###### `P:IoT.Driver.TwinCATRx.IRxTcAdsClient.DataReceived`

```csharp
public System.IObservable<System.ValueTuple<string, object, string>> DataReceived { get; }
```
Gets the data received.

- Value: The data received.

###### `P:IoT.Driver.TwinCATRx.IRxTcAdsClient.DataReceivedAsync`

```csharp
public ReactiveUI.Primitives.Async.IObservableAsync<System.ValueTuple<string, object, string>> DataReceivedAsync { get; }
```
Gets the async data received stream.

- Value: The `DataReceivedAsync` value.

###### `P:IoT.Driver.TwinCATRx.IRxTcAdsClient.ErrorReceived`

```csharp
public System.IObservable<System.Exception> ErrorReceived { get; }
```
Gets the error received.

- Value: The error received.

###### `P:IoT.Driver.TwinCATRx.IRxTcAdsClient.ErrorReceivedAsync`

```csharp
public ReactiveUI.Primitives.Async.IObservableAsync<System.Exception> ErrorReceivedAsync { get; }
```
Gets the async error received stream.

- Value: The `ErrorReceivedAsync` value.

###### `P:IoT.Driver.TwinCATRx.IRxTcAdsClient.InitializeComplete`

```csharp
public System.IObservable<ReactiveUI.Primitives.RxVoid> InitializeComplete { get; }
```
Gets initialization completion events. The latest event can be replayed from an earlier connection; see the reconnect guidance above.

- Value: The initialize complete.

###### `P:IoT.Driver.TwinCATRx.IRxTcAdsClient.InitializeCompleteAsync`

```csharp
public ReactiveUI.Primitives.Async.IObservableAsync<ReactiveUI.Primitives.RxVoid> InitializeCompleteAsync { get; }
```
Gets the async initialize complete stream.

- Value: The `InitializeCompleteAsync` value.

###### `P:IoT.Driver.TwinCATRx.IRxTcAdsClient.IsDisposed`

```csharp
public bool IsDisposed { get; }
```
Gets a value indicating whether the instance is disposed.

- Value: The `IsDisposed` value.

###### `P:IoT.Driver.TwinCATRx.IRxTcAdsClient.IsPaused`

```csharp
public bool IsPaused { get; }
```
Gets a value indicating whether this instance is paused within WriteValuesAsync.

- Value: true if this instance is paused; otherwise, false .

###### `P:IoT.Driver.TwinCATRx.IRxTcAdsClient.IsPausedObservable`

```csharp
public System.IObservable<bool> IsPausedObservable { get; }
```
Gets the is paused observable.

- Value: The is paused observable.

###### `P:IoT.Driver.TwinCATRx.IRxTcAdsClient.IsPausedObservableAsync`

```csharp
public ReactiveUI.Primitives.Async.IObservableAsync<bool> IsPausedObservableAsync { get; }
```
Gets the async paused state stream.

- Value: The `IsPausedObservableAsync` value.

###### `P:IoT.Driver.TwinCATRx.IRxTcAdsClient.OnWrite`

```csharp
public System.IObservable<string> OnWrite { get; }
```
Gets the on write.

- Value: The on write.

###### `P:IoT.Driver.TwinCATRx.IRxTcAdsClient.OnWriteAsync`

```csharp
public ReactiveUI.Primitives.Async.IObservableAsync<string> OnWriteAsync { get; }
```
Gets the async write result stream.

- Value: The `OnWriteAsync` value.

###### `P:IoT.Driver.TwinCATRx.IRxTcAdsClient.ReadWriteHandleInfo`

```csharp
public System.Collections.Generic.IDictionary<string, System.Nullable<uint>> ReadWriteHandleInfo { get; }
```
Gets the read write handle information.

- Value: The read write handle information.

###### `P:IoT.Driver.TwinCATRx.IRxTcAdsClient.Settings`

```csharp
public IoT.Driver.TwinCATRx.Core.ISettings Settings { get; }
```
Gets the settings.

- Value: The settings.

###### `P:IoT.Driver.TwinCATRx.IRxTcAdsClient.WriteHandleInfo`

```csharp
public System.Collections.Generic.IDictionary<string, System.ValueTuple<System.Nullable<uint>, int>> WriteHandleInfo { get; }
```
Gets the write handle information.

- Value: The write handle information.

#### `T:IoT.Driver.TwinCATRx.InMemoryAdsClient`

```csharp
public class IoT.Driver.TwinCATRx.InMemoryAdsClient
```
Provides a deterministic, production-usable ADS simulator for applications that need to run without a TwinCAT runtime or physical PLC.

##### Declared public members

###### `M:IoT.Driver.TwinCATRx.InMemoryAdsClient.#ctor`

```csharp
public IoT.Driver.TwinCATRx.InMemoryAdsClient()
```
Initializes a new instance of the `T:IoT.Driver.TwinCATRx.InMemoryAdsClient` class.

###### `M:IoT.Driver.TwinCATRx.InMemoryAdsClient.Connect(IoT.Driver.TwinCATRx.Core.ISettings)`

```csharp
public void Connect(IoT.Driver.TwinCATRx.Core.ISettings settings)
```
Inherits XML documentation from its implemented or overridden member.

- Parameter `settings`: The `settings` value.

###### `M:IoT.Driver.TwinCATRx.InMemoryAdsClient.Disconnect`

```csharp
public void Disconnect()
```
Inherits XML documentation from its implemented or overridden member.

###### `M:IoT.Driver.TwinCATRx.InMemoryAdsClient.Dispose`

```csharp
public void Dispose()
```
Inherits XML documentation from its implemented or overridden member.

###### `M:IoT.Driver.TwinCATRx.InMemoryAdsClient.GetOperationMetrics`

```csharp
public IoT.Driver.TwinCATRx.InMemoryAdsOperationMetrics GetOperationMetrics()
```
Creates a deterministic snapshot of native ADS operation counts.

- Returns: A new snapshot whose counts are unaffected by subsequent operations or resets.

###### `M:IoT.Driver.TwinCATRx.InMemoryAdsClient.Pause(System.TimeSpan)`

```csharp
public void Pause(System.TimeSpan time)
```
Inherits XML documentation from its implemented or overridden member.

- Parameter `time`: The `time` value.

###### `M:IoT.Driver.TwinCATRx.InMemoryAdsClient.PublishNotifications`

```csharp
public void PublishNotifications()
```
Publishes every configured notification using the latest in-memory symbol values.

###### `M:IoT.Driver.TwinCATRx.InMemoryAdsClient.QueueFault(IoT.Driver.TwinCATRx.InMemoryAdsOperation,System.Exception)`

```csharp
public IoT.Driver.TwinCATRx.InMemoryAdsClient QueueFault(IoT.Driver.TwinCATRx.InMemoryAdsOperation operation, System.Exception error)
```
Queues a failure for the next matching operation.

- Parameter `operation`: The operation that will consume the failure.
- Parameter `error`: The error to publish.
- Returns: This simulator for fluent setup.

###### `M:IoT.Driver.TwinCATRx.InMemoryAdsClient.Read(System.String)`

```csharp
public void Read(string variable)
```
Inherits XML documentation from its implemented or overridden member.

- Parameter `variable`: The `variable` value.

###### `M:IoT.Driver.TwinCATRx.InMemoryAdsClient.Read(System.String,System.Nullable`1{System.Int32})`

```csharp
public void Read(string variable, System.Nullable<int> arrayLength)
```
Executes the `Read` operation.

- Parameter `variable`: The `variable` value.
- Parameter `arrayLength`: The `arrayLength` value.

###### `M:IoT.Driver.TwinCATRx.InMemoryAdsClient.Read(System.String,System.Nullable`1{System.Int32},System.String)`

```csharp
public void Read(string variable, System.Nullable<int> arrayLength, string id)
```
Executes the `Read` operation.

- Parameter `variable`: The `variable` value.
- Parameter `arrayLength`: The `arrayLength` value.
- Parameter `id`: The `id` value.

###### `M:IoT.Driver.TwinCATRx.InMemoryAdsClient.Read(System.String,System.String)`

```csharp
public void Read(string variable, string id)
```
Inherits XML documentation from its implemented or overridden member.

- Parameter `variable`: The `variable` value.
- Parameter `id`: The `id` value.

###### `M:IoT.Driver.TwinCATRx.InMemoryAdsClient.ReadMany(System.Collections.Generic.IEnumerable`1{System.String})`

```csharp
public void ReadMany(System.Collections.Generic.IEnumerable<string> variables)
```
Executes the `ReadMany` operation.

- Parameter `variables`: The `variables` value.

###### `M:IoT.Driver.TwinCATRx.InMemoryAdsClient.ReadMany(System.Collections.Generic.IEnumerable`1{System.String},System.String)`

```csharp
public void ReadMany(System.Collections.Generic.IEnumerable<string> variables, string correlationPrefix)
```
Executes the `ReadMany` operation.

- Parameter `variables`: The `variables` value.
- Parameter `correlationPrefix`: The `correlationPrefix` value.

###### `M:IoT.Driver.TwinCATRx.InMemoryAdsClient.Reconnect`

```csharp
public void Reconnect()
```
Reconnects with the latest settings while preserving registered symbols and queued faults.

###### `M:IoT.Driver.TwinCATRx.InMemoryAdsClient.RegisterStructure``1(System.String,``0)`

```csharp
public IoT.Driver.TwinCATRx.InMemoryAdsClient RegisterStructure<T>(string name, T value)
```
Registers or replaces an in-memory structure symbol.

- Parameter `name`: The root symbol name.
- Parameter `value`: The structure value.
- Returns: This simulator for fluent setup.

###### `M:IoT.Driver.TwinCATRx.InMemoryAdsClient.RegisterStructure``1(System.String,``0,System.Boolean,System.Boolean)`

```csharp
public IoT.Driver.TwinCATRx.InMemoryAdsClient RegisterStructure<T>(string name, T value, bool isReadable, bool isWritable)
```
Registers or replaces an in-memory structure symbol with full access metadata.

- Parameter `name`: The root symbol name.
- Parameter `value`: The structure value.
- Parameter `isReadable`: Whether reads are permitted.
- Parameter `isWritable`: Whether writes are permitted.
- Returns: This simulator for fluent setup.

###### `M:IoT.Driver.TwinCATRx.InMemoryAdsClient.RegisterSymbol(System.String,System.Object)`

```csharp
public IoT.Driver.TwinCATRx.InMemoryAdsClient RegisterSymbol(string name, object value)
```
Registers or replaces an in-memory ADS symbol.

- Parameter `name`: The symbol name.
- Parameter `value`: The initial value.
- Returns: This simulator for fluent setup.

###### `M:IoT.Driver.TwinCATRx.InMemoryAdsClient.RegisterSymbol(System.String,System.Object,System.Type)`

```csharp
public IoT.Driver.TwinCATRx.InMemoryAdsClient RegisterSymbol(string name, object value, System.Type dataType)
```
Registers or replaces an in-memory ADS symbol with an explicit declared type.

- Parameter `name`: The symbol name.
- Parameter `value`: The initial value.
- Parameter `dataType`: The declared type.
- Returns: This simulator for fluent setup.

###### `M:IoT.Driver.TwinCATRx.InMemoryAdsClient.RegisterSymbol(System.String,System.Object,System.Type,System.Int32,System.Boolean,System.Boolean)`

```csharp
public IoT.Driver.TwinCATRx.InMemoryAdsClient RegisterSymbol(string name, object value, System.Type dataType, int arrayLength, bool isReadable, bool isWritable)
```
Registers or replaces an in-memory ADS symbol with full access metadata.

- Parameter `name`: The symbol name.
- Parameter `value`: The initial value.
- Parameter `dataType`: The declared type.
- Parameter `arrayLength`: The array or string length, or -1 for a scalar.
- Parameter `isReadable`: Whether reads are permitted.
- Parameter `isWritable`: Whether writes are permitted.
- Returns: This simulator for fluent setup.

###### `M:IoT.Driver.TwinCATRx.InMemoryAdsClient.RemoveSymbol(System.String)`

```csharp
public bool RemoveSymbol(string name)
```
Removes a registered symbol and any configured handles for it.

- Parameter `name`: The symbol name.
- Returns: Whether a symbol was removed.

###### `M:IoT.Driver.TwinCATRx.InMemoryAdsClient.ResetOperationMetrics`

```csharp
public void ResetOperationMetrics()
```
Resets deterministic native ADS operation counts without changing simulator state.

###### `M:IoT.Driver.TwinCATRx.InMemoryAdsClient.SetValue(System.String,System.Object)`

```csharp
public void SetValue(string variable, object value)
```
Updates a symbol as if its value changed in the simulated PLC.

- Parameter `variable`: The variable to update.
- Parameter `value`: The new value.

###### `M:IoT.Driver.TwinCATRx.InMemoryAdsClient.TryGetValue``1(System.String,``0@)`

```csharp
public bool TryGetValue<T>(string variable, out T value)
```
Tries to retrieve and convert a registered symbol value.

- Parameter `variable`: The symbol name.
- Parameter `value`: The converted value when successful.
- Returns: Whether the symbol exists and its value is compatible.

###### `M:IoT.Driver.TwinCATRx.InMemoryAdsClient.Write(System.String,System.Object)`

```csharp
public void Write(string variable, object value)
```
Inherits XML documentation from its implemented or overridden member.

- Parameter `variable`: The `variable` value.
- Parameter `value`: The `value` value.

###### `M:IoT.Driver.TwinCATRx.InMemoryAdsClient.Write(System.String,System.Object,System.String)`

```csharp
public void Write(string variable, object value, string id)
```
Inherits XML documentation from its implemented or overridden member.

- Parameter `variable`: The `variable` value.
- Parameter `value`: The `value` value.
- Parameter `id`: The `id` value.

###### `M:IoT.Driver.TwinCATRx.InMemoryAdsClient.WriteMany(System.Collections.Generic.IEnumerable`1{System.Collections.Generic.KeyValuePair`2{System.String,System.Object}})`

```csharp
public void WriteMany(System.Collections.Generic.IEnumerable<System.Collections.Generic.KeyValuePair<string, object>> values)
```
Executes the `WriteMany` operation.

- Parameter `values`: The `values` value.

###### `M:IoT.Driver.TwinCATRx.InMemoryAdsClient.WriteMany(System.Collections.Generic.IEnumerable`1{System.Collections.Generic.KeyValuePair`2{System.String,System.Object}},System.String)`

```csharp
public void WriteMany(System.Collections.Generic.IEnumerable<System.Collections.Generic.KeyValuePair<string, object>> values, string correlationPrefix)
```
Executes the `WriteMany` operation.

- Parameter `values`: The `values` value.
- Parameter `correlationPrefix`: The `correlationPrefix` value.

###### `P:IoT.Driver.TwinCATRx.InMemoryAdsClient.Code`

```csharp
public System.IObservable<string[]> Code { get; }
```
Inherits XML documentation from its implemented or overridden member.

- Value: The `Code` value.

###### `P:IoT.Driver.TwinCATRx.InMemoryAdsClient.Connected`

```csharp
public bool Connected { get; }
```
Gets a value indicating whether the simulator is connected.

- Value: The `Connected` value.

###### `P:IoT.Driver.TwinCATRx.InMemoryAdsClient.ConnectionState`

```csharp
public IoT.Driver.TwinCATRx.InMemoryAdsConnectionState ConnectionState { get; }
```
Gets the current simulator connection state.

- Value: The `ConnectionState` value.

###### `P:IoT.Driver.TwinCATRx.InMemoryAdsClient.ConnectionStates`

```csharp
public System.IObservable<IoT.Driver.TwinCATRx.InMemoryAdsConnectionState> ConnectionStates { get; }
```
Gets the observable connection state stream.

- Value: The `ConnectionStates` value.

###### `P:IoT.Driver.TwinCATRx.InMemoryAdsClient.DataReceived`

```csharp
public System.IObservable<System.ValueTuple<string, object, string>> DataReceived { get; }
```
Inherits XML documentation from its implemented or overridden member.

- Value: The `DataReceived` value.

###### `P:IoT.Driver.TwinCATRx.InMemoryAdsClient.DataReceivedAsync`

```csharp
public ReactiveUI.Primitives.Async.IObservableAsync<System.ValueTuple<string, object, string>> DataReceivedAsync { get; }
```
Inherits XML documentation from its implemented or overridden member.

- Value: The `DataReceivedAsync` value.

###### `P:IoT.Driver.TwinCATRx.InMemoryAdsClient.ErrorReceived`

```csharp
public System.IObservable<System.Exception> ErrorReceived { get; }
```
Inherits XML documentation from its implemented or overridden member.

- Value: The `ErrorReceived` value.

###### `P:IoT.Driver.TwinCATRx.InMemoryAdsClient.ErrorReceivedAsync`

```csharp
public ReactiveUI.Primitives.Async.IObservableAsync<System.Exception> ErrorReceivedAsync { get; }
```
Inherits XML documentation from its implemented or overridden member.

- Value: The `ErrorReceivedAsync` value.

###### `P:IoT.Driver.TwinCATRx.InMemoryAdsClient.InitializeComplete`

```csharp
public System.IObservable<ReactiveUI.Primitives.RxVoid> InitializeComplete { get; }
```
Inherits XML documentation from its implemented or overridden member.

- Value: The `InitializeComplete` value.

###### `P:IoT.Driver.TwinCATRx.InMemoryAdsClient.InitializeCompleteAsync`

```csharp
public ReactiveUI.Primitives.Async.IObservableAsync<ReactiveUI.Primitives.RxVoid> InitializeCompleteAsync { get; }
```
Inherits XML documentation from its implemented or overridden member.

- Value: The `InitializeCompleteAsync` value.

###### `P:IoT.Driver.TwinCATRx.InMemoryAdsClient.IsDisposed`

```csharp
public bool IsDisposed { get; }
```
Inherits XML documentation from its implemented or overridden member.

- Value: The `IsDisposed` value.

###### `P:IoT.Driver.TwinCATRx.InMemoryAdsClient.IsPaused`

```csharp
public bool IsPaused { get; }
```
Inherits XML documentation from its implemented or overridden member.

- Value: The `IsPaused` value.

###### `P:IoT.Driver.TwinCATRx.InMemoryAdsClient.IsPausedObservable`

```csharp
public System.IObservable<bool> IsPausedObservable { get; }
```
Inherits XML documentation from its implemented or overridden member.

- Value: The `IsPausedObservable` value.

###### `P:IoT.Driver.TwinCATRx.InMemoryAdsClient.IsPausedObservableAsync`

```csharp
public ReactiveUI.Primitives.Async.IObservableAsync<bool> IsPausedObservableAsync { get; }
```
Inherits XML documentation from its implemented or overridden member.

- Value: The `IsPausedObservableAsync` value.

###### `P:IoT.Driver.TwinCATRx.InMemoryAdsClient.OnWrite`

```csharp
public System.IObservable<string> OnWrite { get; }
```
Inherits XML documentation from its implemented or overridden member.

- Value: The `OnWrite` value.

###### `P:IoT.Driver.TwinCATRx.InMemoryAdsClient.OnWriteAsync`

```csharp
public ReactiveUI.Primitives.Async.IObservableAsync<string> OnWriteAsync { get; }
```
Inherits XML documentation from its implemented or overridden member.

- Value: The `OnWriteAsync` value.

###### `P:IoT.Driver.TwinCATRx.InMemoryAdsClient.OperationMetrics`

```csharp
public IoT.Driver.TwinCATRx.InMemoryAdsOperationMetrics OperationMetrics { get; }
```
Gets a deterministic snapshot of native ADS operation counts.

- Value: The `OperationMetrics` value.

###### `P:IoT.Driver.TwinCATRx.InMemoryAdsClient.ReadWriteHandleInfo`

```csharp
public System.Collections.Generic.IDictionary<string, System.Nullable<uint>> ReadWriteHandleInfo { get; }
```
Inherits XML documentation from its implemented or overridden member.

- Value: The `ReadWriteHandleInfo` value.

###### `P:IoT.Driver.TwinCATRx.InMemoryAdsClient.Settings`

```csharp
public IoT.Driver.TwinCATRx.Core.ISettings Settings { get; }
```
Inherits XML documentation from its implemented or overridden member.

- Value: The `Settings` value.

###### `P:IoT.Driver.TwinCATRx.InMemoryAdsClient.Symbols`

```csharp
public System.Collections.Generic.IReadOnlyCollection<IoT.Driver.TwinCATRx.InMemoryAdsSymbol> Symbols { get; }
```
Gets the read-only view of all registered symbols.

- Value: The `Symbols` value.

###### `P:IoT.Driver.TwinCATRx.InMemoryAdsClient.WriteHandleInfo`

```csharp
public System.Collections.Generic.IDictionary<string, System.ValueTuple<System.Nullable<uint>, int>> WriteHandleInfo { get; }
```
Inherits XML documentation from its implemented or overridden member.

- Value: The `WriteHandleInfo` value.

#### `T:IoT.Driver.TwinCATRx.InMemoryAdsConnectionState`

```csharp
public enum IoT.Driver.TwinCATRx.InMemoryAdsConnectionState
```
Describes the lifecycle state of an `T:IoT.Driver.TwinCATRx.InMemoryAdsClient` .

##### Declared public members

###### `F:IoT.Driver.TwinCATRx.InMemoryAdsConnectionState.Connected`

```csharp
public static const IoT.Driver.TwinCATRx.InMemoryAdsConnectionState Connected
```
The simulator is ready to service reads, writes, and notifications.

###### `F:IoT.Driver.TwinCATRx.InMemoryAdsConnectionState.Connecting`

```csharp
public static const IoT.Driver.TwinCATRx.InMemoryAdsConnectionState Connecting
```
The simulator is validating settings and creating handles.

###### `F:IoT.Driver.TwinCATRx.InMemoryAdsConnectionState.Disconnected`

```csharp
public static const IoT.Driver.TwinCATRx.InMemoryAdsConnectionState Disconnected
```
The simulator is disconnected and can be connected.

###### `F:IoT.Driver.TwinCATRx.InMemoryAdsConnectionState.Disposed`

```csharp
public static const IoT.Driver.TwinCATRx.InMemoryAdsConnectionState Disposed
```
The simulator and its observable streams have been disposed.

###### `F:IoT.Driver.TwinCATRx.InMemoryAdsConnectionState.Faulted`

```csharp
public static const IoT.Driver.TwinCATRx.InMemoryAdsConnectionState Faulted
```
The latest connection attempt or simulated operation failed.

#### `T:IoT.Driver.TwinCATRx.InMemoryAdsException`

```csharp
public class IoT.Driver.TwinCATRx.InMemoryAdsException
```
Represents a deterministic in-memory ADS operation failure.

##### Declared public members

###### `M:IoT.Driver.TwinCATRx.InMemoryAdsException.#ctor`

```csharp
public IoT.Driver.TwinCATRx.InMemoryAdsException()
```
Initializes a new instance of the `T:IoT.Driver.TwinCATRx.InMemoryAdsException` class.

###### `M:IoT.Driver.TwinCATRx.InMemoryAdsException.#ctor(IoT.Driver.TwinCATRx.InMemoryAdsOperation,System.String)`

```csharp
public IoT.Driver.TwinCATRx.InMemoryAdsException(IoT.Driver.TwinCATRx.InMemoryAdsOperation operation, string message)
```
Initializes a new instance of the `T:IoT.Driver.TwinCATRx.InMemoryAdsException` class.

- Parameter `operation`: The failed operation.
- Parameter `message`: The failure message.

###### `M:IoT.Driver.TwinCATRx.InMemoryAdsException.#ctor(IoT.Driver.TwinCATRx.InMemoryAdsOperation,System.String,System.String)`

```csharp
public IoT.Driver.TwinCATRx.InMemoryAdsException(IoT.Driver.TwinCATRx.InMemoryAdsOperation operation, string message, string variable)
```
Initializes a new instance of the `T:IoT.Driver.TwinCATRx.InMemoryAdsException` class.

- Parameter `operation`: The failed operation.
- Parameter `message`: The failure message.
- Parameter `variable`: The optional ADS variable involved in the failure.

###### `M:IoT.Driver.TwinCATRx.InMemoryAdsException.#ctor(System.String)`

```csharp
public IoT.Driver.TwinCATRx.InMemoryAdsException(string message)
```
Initializes a new instance of the `T:IoT.Driver.TwinCATRx.InMemoryAdsException` class.

- Parameter `message`: The failure message.

###### `M:IoT.Driver.TwinCATRx.InMemoryAdsException.#ctor(System.String,System.Exception)`

```csharp
public IoT.Driver.TwinCATRx.InMemoryAdsException(string message, System.Exception innerException)
```
Initializes a new instance of the `T:IoT.Driver.TwinCATRx.InMemoryAdsException` class.

- Parameter `message`: The failure message.
- Parameter `innerException`: The failure that caused this exception.

###### `P:IoT.Driver.TwinCATRx.InMemoryAdsException.Operation`

```csharp
public IoT.Driver.TwinCATRx.InMemoryAdsOperation Operation { get; }
```
Gets the failed operation.

- Value: The `Operation` value.

###### `P:IoT.Driver.TwinCATRx.InMemoryAdsException.Variable`

```csharp
public string Variable { get; }
```
Gets the optional ADS variable involved in the failure.

- Value: The `Variable` value.

#### `T:IoT.Driver.TwinCATRx.InMemoryAdsOperation`

```csharp
public enum IoT.Driver.TwinCATRx.InMemoryAdsOperation
```
Identifies an operation that can receive a deterministic simulator fault.

##### Declared public members

###### `F:IoT.Driver.TwinCATRx.InMemoryAdsOperation.Connect`

```csharp
public static const IoT.Driver.TwinCATRx.InMemoryAdsOperation Connect
```
A connection or reconnection operation.

###### `F:IoT.Driver.TwinCATRx.InMemoryAdsOperation.Notification`

```csharp
public static const IoT.Driver.TwinCATRx.InMemoryAdsOperation Notification
```
A configured notification publication.

###### `F:IoT.Driver.TwinCATRx.InMemoryAdsOperation.Read`

```csharp
public static const IoT.Driver.TwinCATRx.InMemoryAdsOperation Read
```
A symbol read operation.

###### `F:IoT.Driver.TwinCATRx.InMemoryAdsOperation.Write`

```csharp
public static const IoT.Driver.TwinCATRx.InMemoryAdsOperation Write
```
A symbol write operation.

#### `T:IoT.Driver.TwinCATRx.InMemoryAdsOperationMetrics`

```csharp
public class IoT.Driver.TwinCATRx.InMemoryAdsOperationMetrics
```
Provides a deterministic snapshot of native ADS operations issued to an in-memory client.

##### Declared public members

###### `M:IoT.Driver.TwinCATRx.InMemoryAdsOperationMetrics.#ctor(System.Int64,System.Int64,System.Int64)`

```csharp
public IoT.Driver.TwinCATRx.InMemoryAdsOperationMetrics(long readOperations, long writeOperations, long notificationPublications)
```
Initializes a new instance of the `T:IoT.Driver.TwinCATRx.InMemoryAdsOperationMetrics` class.

- Parameter `readOperations`: The number of native read attempts.
- Parameter `writeOperations`: The number of native write attempts.
- Parameter `notificationPublications`: The number of notification publication attempts.

###### `P:IoT.Driver.TwinCATRx.InMemoryAdsOperationMetrics.NotificationPublications`

```csharp
public long NotificationPublications { get; }
```
Gets the number of notification publication attempts.

- Value: The `NotificationPublications` value.

###### `P:IoT.Driver.TwinCATRx.InMemoryAdsOperationMetrics.ReadOperations`

```csharp
public long ReadOperations { get; }
```
Gets the number of native read attempts.

- Value: The `ReadOperations` value.

###### `P:IoT.Driver.TwinCATRx.InMemoryAdsOperationMetrics.WriteOperations`

```csharp
public long WriteOperations { get; }
```
Gets the number of native write attempts.

- Value: The `WriteOperations` value.

#### `T:IoT.Driver.TwinCATRx.InMemoryAdsSymbol`

```csharp
public class IoT.Driver.TwinCATRx.InMemoryAdsSymbol
```
Describes one symbol hosted by an `T:IoT.Driver.TwinCATRx.InMemoryAdsClient` .

##### Declared public members

###### `M:IoT.Driver.TwinCATRx.InMemoryAdsSymbol.#ctor(System.String,System.Object,System.Type,System.Int32,System.Boolean,System.Boolean)`

```csharp
public IoT.Driver.TwinCATRx.InMemoryAdsSymbol(string name, object value, System.Type dataType, int arrayLength, bool isReadable, bool isWritable)
```
Initializes a new instance of the `T:IoT.Driver.TwinCATRx.InMemoryAdsSymbol` class.

- Parameter `name`: The case-insensitive ADS variable name.
- Parameter `value`: The initial symbol value.
- Parameter `dataType`: The declared value type.
- Parameter `arrayLength`: The declared array or string length, or -1 for a scalar.
- Parameter `isReadable`: Whether ADS reads are permitted.
- Parameter `isWritable`: Whether ADS writes are permitted.

###### `P:IoT.Driver.TwinCATRx.InMemoryAdsSymbol.ArrayLength`

```csharp
public int ArrayLength { get; }
```
Gets the declared array or string length, or -1 for a scalar.

- Value: The `ArrayLength` value.

###### `P:IoT.Driver.TwinCATRx.InMemoryAdsSymbol.DataType`

```csharp
public System.Type DataType { get; }
```
Gets the declared value type.

- Value: The `DataType` value.

###### `P:IoT.Driver.TwinCATRx.InMemoryAdsSymbol.IsReadable`

```csharp
public bool IsReadable { get; }
```
Gets a value indicating whether ADS reads are permitted.

- Value: The `IsReadable` value.

###### `P:IoT.Driver.TwinCATRx.InMemoryAdsSymbol.IsWritable`

```csharp
public bool IsWritable { get; }
```
Gets a value indicating whether ADS writes are permitted.

- Value: The `IsWritable` value.

###### `P:IoT.Driver.TwinCATRx.InMemoryAdsSymbol.Name`

```csharp
public string Name { get; }
```
Gets the case-insensitive ADS variable name.

- Value: The `Name` value.

###### `P:IoT.Driver.TwinCATRx.InMemoryAdsSymbol.Value`

```csharp
public object Value { get; }
```
Gets the current symbol value.

- Value: The `Value` value.

#### `T:IoT.Driver.TwinCATRx.ObservableBridgeExtensions`

```csharp
public class IoT.Driver.TwinCATRx.ObservableBridgeExtensions
```
Observable bridge helpers.

##### Declared public members

###### `M:IoT.Driver.TwinCATRx.ObservableBridgeExtensions.SubscribeTo``1(System.IObservable`1{``0})`

```csharp
public static System.IDisposable SubscribeTo<T>(System.IObservable<T> source)
```
Executes the `SubscribeTo` operation.

- Parameter `source`: The `source` value.
- Returns: A `System.IDisposable` result.

###### `M:IoT.Driver.TwinCATRx.ObservableBridgeExtensions.SubscribeTo``1(System.IObservable`1{``0},System.Action`1{``0})`

```csharp
public static System.IDisposable SubscribeTo<T>(System.IObservable<T> source, System.Action<T> onNext)
```
Executes the `SubscribeTo` operation.

- Parameter `source`: The `source` value.
- Parameter `onNext`: The `onNext` value.
- Returns: A `System.IDisposable` result.

###### `M:IoT.Driver.TwinCATRx.ObservableBridgeExtensions.SubscribeTo``1(System.IObservable`1{``0},System.Action`1{``0},System.Action`1{System.Exception},System.Action)`

```csharp
public static System.IDisposable SubscribeTo<T>(System.IObservable<T> source, System.Action<T> onNext, System.Action<System.Exception> onError, System.Action onCompleted)
```
Executes the `SubscribeTo` operation.

- Parameter `source`: The `source` value.
- Parameter `onNext`: The `onNext` value.
- Parameter `onError`: The `onError` value.
- Parameter `onCompleted`: The `onCompleted` value.
- Returns: A `System.IDisposable` result.

###### `M:IoT.Driver.TwinCATRx.ObservableBridgeExtensions.ToAsyncObservable``1(System.IObservable`1{``0})`

```csharp
public static ReactiveUI.Primitives.Async.IObservableAsync<T> ToAsyncObservable<T>(System.IObservable<T> source)
```
Executes the `ToAsyncObservable` operation.

- Parameter `source`: The `source` value.
- Returns: A `ReactiveUI.Primitives.Async.IObservableAsync<T>` result.

#### `T:IoT.Driver.TwinCATRx.ObservableServiceController`

```csharp
public class IoT.Driver.TwinCATRx.ObservableServiceController
```
Observable Service Controller.

##### Declared public members

###### `M:IoT.Driver.TwinCATRx.ObservableServiceController.#ctor(System.ServiceProcess.ServiceController)`

```csharp
public IoT.Driver.TwinCATRx.ObservableServiceController(System.ServiceProcess.ServiceController service)
```
Initializes a new instance of the `T:IoT.Driver.TwinCATRx.ObservableServiceController` class.

- Parameter `service`: The service.

###### `M:IoT.Driver.TwinCATRx.ObservableServiceController.#ctor(System.ServiceProcess.ServiceController,System.TimeSpan)`

```csharp
public IoT.Driver.TwinCATRx.ObservableServiceController(System.ServiceProcess.ServiceController service, System.TimeSpan interval)
```
Initializes a new instance of the `T:IoT.Driver.TwinCATRx.ObservableServiceController` class.

- Parameter `service`: The service.
- Parameter `interval`: The interval.

###### `M:IoT.Driver.TwinCATRx.ObservableServiceController.Dispose`

```csharp
public void Dispose()
```
Releases managed and unmanaged resources.

###### `M:IoT.Driver.TwinCATRx.ObservableServiceController.GetServices`

```csharp
public static System.IObservable<IoT.Driver.TwinCATRx.ObservableServiceController> GetServices()
```
Gets the services.

- Returns: A Value.

###### `M:IoT.Driver.TwinCATRx.ObservableServiceController.Restart`

```csharp
public void Restart()
```
Restarts this instance.

###### `M:IoT.Driver.TwinCATRx.ObservableServiceController.Start`

```csharp
public void Start()
```
Starts this instance.

###### `M:IoT.Driver.TwinCATRx.ObservableServiceController.Stop`

```csharp
public void Stop()
```
Stops this instance.

###### `P:IoT.Driver.TwinCATRx.ObservableServiceController.CanStop`

```csharp
public bool CanStop { get; }
```
Gets a value indicating whether this instance can stop.

- Value: true if this instance can stop; otherwise, false .

###### `P:IoT.Driver.TwinCATRx.ObservableServiceController.DisplayName`

```csharp
public string DisplayName { get; }
```
Gets the display name.

- Value: The display name.

###### `P:IoT.Driver.TwinCATRx.ObservableServiceController.IsDisposed`

```csharp
public bool IsDisposed { get; }
```
Gets a value indicating whether the is disposed.

- Value: The `IsDisposed` value.

###### `P:IoT.Driver.TwinCATRx.ObservableServiceController.ServiceName`

```csharp
public string ServiceName { get; }
```
Gets the name of the service.

- Value: The name of the service.

###### `P:IoT.Driver.TwinCATRx.ObservableServiceController.Status`

```csharp
public System.ServiceProcess.ServiceControllerStatus Status { get; }
```
Gets the status.

- Value: The status.

###### `P:IoT.Driver.TwinCATRx.ObservableServiceController.StatusObserver`

```csharp
public System.IObservable<System.ServiceProcess.ServiceControllerStatus> StatusObserver { get; }
```
Gets the status.

- Value: The status.

#### `T:IoT.Driver.TwinCATRx.RxTcAdsClient`

```csharp
public class IoT.Driver.TwinCATRx.RxTcAdsClient
```
Observable TwinCAT ADS Client.

##### Declared public members

###### `M:IoT.Driver.TwinCATRx.RxTcAdsClient.#ctor`

```csharp
public IoT.Driver.TwinCATRx.RxTcAdsClient()
```
Initializes a new instance of the `T:IoT.Driver.TwinCATRx.RxTcAdsClient` class.

###### `M:IoT.Driver.TwinCATRx.RxTcAdsClient.#ctor(System.TimeProvider)`

```csharp
public IoT.Driver.TwinCATRx.RxTcAdsClient(System.TimeProvider timeProvider)
```
Initializes a new instance of the `T:IoT.Driver.TwinCATRx.RxTcAdsClient` class.

- Parameter `timeProvider`: The time provider.

###### `M:IoT.Driver.TwinCATRx.RxTcAdsClient.Connect(IoT.Driver.TwinCATRx.Core.ISettings)`

```csharp
public void Connect(IoT.Driver.TwinCATRx.Core.ISettings settings)
```
Connects the specified settings.

- Parameter `settings`: The settings.

###### `M:IoT.Driver.TwinCATRx.RxTcAdsClient.Disconnect`

```csharp
public void Disconnect()
```
Disconnects this instance.

###### `M:IoT.Driver.TwinCATRx.RxTcAdsClient.Dispose`

```csharp
public void Dispose()
```
Releases unmanaged and - optionally - managed resources.

###### `M:IoT.Driver.TwinCATRx.RxTcAdsClient.Pause(System.TimeSpan)`

```csharp
public void Pause(System.TimeSpan time)
```
Pauses the specified time.

- Parameter `time`: The time.

###### `M:IoT.Driver.TwinCATRx.RxTcAdsClient.Read(System.String)`

```csharp
public void Read(string variable)
```
Reads the specified variable.

- Parameter `variable`: The data.

###### `M:IoT.Driver.TwinCATRx.RxTcAdsClient.Read(System.String,System.Nullable`1{System.Int32})`

```csharp
public void Read(string variable, System.Nullable<int> arrayLength)
```
Executes the `Read` operation.

- Parameter `variable`: The `variable` value.
- Parameter `arrayLength`: The `arrayLength` value.

###### `M:IoT.Driver.TwinCATRx.RxTcAdsClient.Read(System.String,System.Nullable`1{System.Int32},System.String)`

```csharp
public void Read(string variable, System.Nullable<int> arrayLength, string id)
```
Executes the `Read` operation.

- Parameter `variable`: The `variable` value.
- Parameter `arrayLength`: The `arrayLength` value.
- Parameter `id`: The `id` value.

###### `M:IoT.Driver.TwinCATRx.RxTcAdsClient.Read(System.String,System.String)`

```csharp
public void Read(string variable, string id)
```
Reads a variable with a correlation identifier.

- Parameter `variable`: The variable.
- Parameter `id`: The correlation identifier.

###### `M:IoT.Driver.TwinCATRx.RxTcAdsClient.Write(System.String,System.Object)`

```csharp
public void Write(string variable, object value)
```
Writes the specified variable.

- Parameter `variable`: The variable.
- Parameter `value`: The value.

###### `M:IoT.Driver.TwinCATRx.RxTcAdsClient.Write(System.String,System.Object,System.String)`

```csharp
public void Write(string variable, object value, string id)
```
Writes a variable with a correlation identifier.

- Parameter `variable`: The variable.
- Parameter `value`: The value.
- Parameter `id`: The correlation identifier.

###### `P:IoT.Driver.TwinCATRx.RxTcAdsClient.Code`

```csharp
public System.IObservable<string[]> Code { get; }
```
Gets codes this instance.

- Returns: A Value.
- Value: The `Code` value.

###### `P:IoT.Driver.TwinCATRx.RxTcAdsClient.Connected`

```csharp
public bool Connected { get; }
```
Gets a value indicating whether this `T:IoT.Driver.TwinCATRx.RxTcAdsClient` is connected.

- Value: true if connected; otherwise, false .

###### `P:IoT.Driver.TwinCATRx.RxTcAdsClient.DataReceived`

```csharp
public System.IObservable<System.ValueTuple<string, object, string>> DataReceived { get; }
```
Gets the data received.

- Value: The data received.

###### `P:IoT.Driver.TwinCATRx.RxTcAdsClient.DataReceivedAsync`

```csharp
public ReactiveUI.Primitives.Async.IObservableAsync<System.ValueTuple<string, object, string>> DataReceivedAsync { get; }
```
Inherits XML documentation from its implemented or overridden member.

- Value: The `DataReceivedAsync` value.

###### `P:IoT.Driver.TwinCATRx.RxTcAdsClient.ErrorReceived`

```csharp
public System.IObservable<System.Exception> ErrorReceived { get; }
```
Gets error received.

- Returns: A Value.
- Value: The `ErrorReceived` value.

###### `P:IoT.Driver.TwinCATRx.RxTcAdsClient.ErrorReceivedAsync`

```csharp
public ReactiveUI.Primitives.Async.IObservableAsync<System.Exception> ErrorReceivedAsync { get; }
```
Inherits XML documentation from its implemented or overridden member.

- Value: The `ErrorReceivedAsync` value.

###### `P:IoT.Driver.TwinCATRx.RxTcAdsClient.InitializeComplete`

```csharp
public System.IObservable<ReactiveUI.Primitives.RxVoid> InitializeComplete { get; }
```
Gets initialization completion events. The latest event can be replayed from an earlier connection; see the reconnect guidance above.

- Value: The initialize complete.

###### `P:IoT.Driver.TwinCATRx.RxTcAdsClient.InitializeCompleteAsync`

```csharp
public ReactiveUI.Primitives.Async.IObservableAsync<ReactiveUI.Primitives.RxVoid> InitializeCompleteAsync { get; }
```
Inherits XML documentation from its implemented or overridden member.

- Value: The `InitializeCompleteAsync` value.

###### `P:IoT.Driver.TwinCATRx.RxTcAdsClient.IsDisposed`

```csharp
public bool IsDisposed { get; }
```
Gets a value indicating whether gets a value that indicates whether the object is disposed.

- Value: The `IsDisposed` value.

###### `P:IoT.Driver.TwinCATRx.RxTcAdsClient.IsPaused`

```csharp
public bool IsPaused { get; }
```
Gets a value indicating whether this instance is paused.

- Value: true if this instance is paused; otherwise, false .

###### `P:IoT.Driver.TwinCATRx.RxTcAdsClient.IsPausedObservable`

```csharp
public System.IObservable<bool> IsPausedObservable { get; }
```
Gets the is paused observable.

- Value: The is paused observable.

###### `P:IoT.Driver.TwinCATRx.RxTcAdsClient.IsPausedObservableAsync`

```csharp
public ReactiveUI.Primitives.Async.IObservableAsync<bool> IsPausedObservableAsync { get; }
```
Inherits XML documentation from its implemented or overridden member.

- Value: The `IsPausedObservableAsync` value.

###### `P:IoT.Driver.TwinCATRx.RxTcAdsClient.OnWrite`

```csharp
public System.IObservable<string> OnWrite { get; }
```
Gets the on write.

- Value: The on write.

###### `P:IoT.Driver.TwinCATRx.RxTcAdsClient.OnWriteAsync`

```csharp
public ReactiveUI.Primitives.Async.IObservableAsync<string> OnWriteAsync { get; }
```
Inherits XML documentation from its implemented or overridden member.

- Value: The `OnWriteAsync` value.

###### `P:IoT.Driver.TwinCATRx.RxTcAdsClient.ReadWriteHandleInfo`

```csharp
public System.Collections.Generic.IDictionary<string, System.Nullable<uint>> ReadWriteHandleInfo { get; }
```
Gets the read write handle information.

- Value: The read write handle information.

###### `P:IoT.Driver.TwinCATRx.RxTcAdsClient.Settings`

```csharp
public IoT.Driver.TwinCATRx.Core.ISettings Settings { get; }
```
Gets the settings.

- Value: The settings.

###### `P:IoT.Driver.TwinCATRx.RxTcAdsClient.WriteHandleInfo`

```csharp
public System.Collections.Generic.IDictionary<string, System.ValueTuple<System.Nullable<uint>, int>> WriteHandleInfo { get; }
```
Gets the write handle information.

- Value: The write handle information.

#### `T:IoT.Driver.TwinCATRx.ServiceStatus`

```csharp
public enum IoT.Driver.TwinCATRx.ServiceStatus
```
Service Status.

##### Declared public members

###### `F:IoT.Driver.TwinCATRx.ServiceStatus.Faulted`

```csharp
public static const IoT.Driver.TwinCATRx.ServiceStatus Faulted
```
The faulted.

###### `F:IoT.Driver.TwinCATRx.ServiceStatus.Paused`

```csharp
public static const IoT.Driver.TwinCATRx.ServiceStatus Paused
```
The paused.

###### `F:IoT.Driver.TwinCATRx.ServiceStatus.Running`

```csharp
public static const IoT.Driver.TwinCATRx.ServiceStatus Running
```
The running.

###### `F:IoT.Driver.TwinCATRx.ServiceStatus.Starting`

```csharp
public static const IoT.Driver.TwinCATRx.ServiceStatus Starting
```
The starting.

###### `F:IoT.Driver.TwinCATRx.ServiceStatus.StatusChanging`

```csharp
public static const IoT.Driver.TwinCATRx.ServiceStatus StatusChanging
```
The status changing.

###### `F:IoT.Driver.TwinCATRx.ServiceStatus.Stopped`

```csharp
public static const IoT.Driver.TwinCATRx.ServiceStatus Stopped
```
The stopped.

###### `F:IoT.Driver.TwinCATRx.ServiceStatus.Stopping`

```csharp
public static const IoT.Driver.TwinCATRx.ServiceStatus Stopping
```
The stopping.

###### `F:IoT.Driver.TwinCATRx.ServiceStatus.Unknown`

```csharp
public static const IoT.Driver.TwinCATRx.ServiceStatus Unknown
```
The unknown.

#### `T:IoT.Driver.TwinCATRx.StructuredNotificationAttribute`

```csharp
public class IoT.Driver.TwinCATRx.StructuredNotificationAttribute
```
Marks a property observed within a TwinCAT structure.

##### Declared public members

###### `M:IoT.Driver.TwinCATRx.StructuredNotificationAttribute.#ctor(System.String)`

```csharp
public IoT.Driver.TwinCATRx.StructuredNotificationAttribute(string address)
```
Initializes a new instance of the `T:IoT.Driver.TwinCATRx.StructuredNotificationAttribute` class.

- Parameter `address`: The structure address.

###### `M:IoT.Driver.TwinCATRx.StructuredNotificationAttribute.#ctor(System.String,System.String)`

```csharp
public IoT.Driver.TwinCATRx.StructuredNotificationAttribute(string address, string memberAddress)
```
Initializes a new instance of the `T:IoT.Driver.TwinCATRx.StructuredNotificationAttribute` class.

- Parameter `address`: The structure address.
- Parameter `memberAddress`: The member address.

###### `P:IoT.Driver.TwinCATRx.StructuredNotificationAttribute.Address`

```csharp
public string Address { get; }
```
Gets the structure address.

- Value: The `Address` value.

###### `P:IoT.Driver.TwinCATRx.StructuredNotificationAttribute.ArraySize`

```csharp
public int ArraySize { get; set; }
```
Gets or sets the array size.

- Value: The `ArraySize` value.

###### `P:IoT.Driver.TwinCATRx.StructuredNotificationAttribute.CanWrite`

```csharp
public bool CanWrite { get; set; }
```
Gets or sets a value indicating whether writes are generated.

- Value: The `CanWrite` value.

###### `P:IoT.Driver.TwinCATRx.StructuredNotificationAttribute.CycleTime`

```csharp
public int CycleTime { get; set; }
```
Gets or sets the notification cycle time.

- Value: The `CycleTime` value.

###### `P:IoT.Driver.TwinCATRx.StructuredNotificationAttribute.Id`

```csharp
public string Id { get; set; }
```
Gets or sets the optional correlation identifier.

- Value: The `Id` value.

###### `P:IoT.Driver.TwinCATRx.StructuredNotificationAttribute.MemberAddress`

```csharp
public string MemberAddress { get; set; }
```
Gets or sets the member address.

- Value: The `MemberAddress` value.

###### `P:IoT.Driver.TwinCATRx.StructuredNotificationAttribute.ObservableName`

```csharp
public string ObservableName { get; set; }
```
Gets or sets the generated observable name.

- Value: The `ObservableName` value.

###### `P:IoT.Driver.TwinCATRx.StructuredNotificationAttribute.WriteAddress`

```csharp
public string WriteAddress { get; set; }
```
Gets or sets an optional alternate write address.

- Value: The `WriteAddress` value.

#### `T:IoT.Driver.TwinCATRx.TwinCatLogicalTagClient`

```csharp
public class IoT.Driver.TwinCATRx.TwinCatLogicalTagClient
```
Maps logical CP.IoT tags onto an event-driven TwinCAT ADS client.

##### Declared public members

###### `M:IoT.Driver.TwinCATRx.TwinCatLogicalTagClient.#ctor(IoT.Driver.TwinCATRx.IRxTcAdsClient)`

```csharp
public IoT.Driver.TwinCATRx.TwinCatLogicalTagClient(IoT.Driver.TwinCATRx.IRxTcAdsClient nativeClient)
```
Initializes a new instance of the `T:IoT.Driver.TwinCATRx.TwinCatLogicalTagClient` class.

- Parameter `nativeClient`: The composed ADS client.

###### `M:IoT.Driver.TwinCATRx.TwinCatLogicalTagClient.#ctor(IoT.Driver.TwinCATRx.IRxTcAdsClient,IoT.Driver.Core.ILogicalTagCatalog)`

```csharp
public IoT.Driver.TwinCATRx.TwinCatLogicalTagClient(IoT.Driver.TwinCATRx.IRxTcAdsClient nativeClient, IoT.Driver.Core.ILogicalTagCatalog catalog)
```
Initializes a new instance of the `T:IoT.Driver.TwinCATRx.TwinCatLogicalTagClient` class.

- Parameter `nativeClient`: The composed ADS client.
- Parameter `catalog`: The caller-owned catalog.

###### `M:IoT.Driver.TwinCATRx.TwinCatLogicalTagClient.#ctor(IoT.Driver.TwinCATRx.IRxTcAdsClient,IoT.Driver.Core.ILogicalTagCatalog,IoT.Driver.Core.LogicalTagSqliteStore)`

```csharp
public IoT.Driver.TwinCATRx.TwinCatLogicalTagClient(IoT.Driver.TwinCATRx.IRxTcAdsClient nativeClient, IoT.Driver.Core.ILogicalTagCatalog catalog, IoT.Driver.Core.LogicalTagSqliteStore store)
```
Initializes a new instance of the `T:IoT.Driver.TwinCATRx.TwinCatLogicalTagClient` class.

- Parameter `nativeClient`: The composed ADS client.
- Parameter `catalog`: The caller-owned catalog.
- Parameter `store`: The SQLite store.

###### `M:IoT.Driver.TwinCATRx.TwinCatLogicalTagClient.#ctor(IoT.Driver.TwinCATRx.IRxTcAdsClient,IoT.Driver.Core.ILogicalTagCatalog,IoT.Driver.Core.LogicalTagSqliteStore,System.TimeProvider)`

```csharp
public IoT.Driver.TwinCATRx.TwinCatLogicalTagClient(IoT.Driver.TwinCATRx.IRxTcAdsClient nativeClient, IoT.Driver.Core.ILogicalTagCatalog catalog, IoT.Driver.Core.LogicalTagSqliteStore store, System.TimeProvider timeProvider)
```
Initializes a new instance of the `T:IoT.Driver.TwinCATRx.TwinCatLogicalTagClient` class.

- Parameter `nativeClient`: The composed ADS client.
- Parameter `catalog`: The caller-owned catalog.
- Parameter `store`: The SQLite store.
- Parameter `timeProvider`: The time provider.

###### `M:IoT.Driver.TwinCATRx.TwinCatLogicalTagClient.#ctor(IoT.Driver.TwinCATRx.IRxTcAdsClient,IoT.Driver.Core.ILogicalTagCatalog,System.TimeProvider)`

```csharp
public IoT.Driver.TwinCATRx.TwinCatLogicalTagClient(IoT.Driver.TwinCATRx.IRxTcAdsClient nativeClient, IoT.Driver.Core.ILogicalTagCatalog catalog, System.TimeProvider timeProvider)
```
Initializes a new instance of the `T:IoT.Driver.TwinCATRx.TwinCatLogicalTagClient` class.

- Parameter `nativeClient`: The composed ADS client.
- Parameter `catalog`: The caller-owned catalog.
- Parameter `timeProvider`: The time provider.

###### `M:IoT.Driver.TwinCATRx.TwinCatLogicalTagClient.#ctor(IoT.Driver.TwinCATRx.IRxTcAdsClient,IoT.Driver.Core.LogicalTagSqliteStore)`

```csharp
public IoT.Driver.TwinCATRx.TwinCatLogicalTagClient(IoT.Driver.TwinCATRx.IRxTcAdsClient nativeClient, IoT.Driver.Core.LogicalTagSqliteStore store)
```
Initializes a new instance of the `T:IoT.Driver.TwinCATRx.TwinCatLogicalTagClient` class.

- Parameter `nativeClient`: The composed ADS client.
- Parameter `store`: The SQLite store.

###### `M:IoT.Driver.TwinCATRx.TwinCatLogicalTagClient.#ctor(IoT.Driver.TwinCATRx.IRxTcAdsClient,IoT.Driver.Core.LogicalTagSqliteStore,System.TimeProvider)`

```csharp
public IoT.Driver.TwinCATRx.TwinCatLogicalTagClient(IoT.Driver.TwinCATRx.IRxTcAdsClient nativeClient, IoT.Driver.Core.LogicalTagSqliteStore store, System.TimeProvider timeProvider)
```
Initializes a new instance of the `T:IoT.Driver.TwinCATRx.TwinCatLogicalTagClient` class.

- Parameter `nativeClient`: The composed ADS client.
- Parameter `store`: The SQLite store.
- Parameter `timeProvider`: The time provider.

###### `M:IoT.Driver.TwinCATRx.TwinCatLogicalTagClient.#ctor(IoT.Driver.TwinCATRx.IRxTcAdsClient,System.TimeProvider)`

```csharp
public IoT.Driver.TwinCATRx.TwinCatLogicalTagClient(IoT.Driver.TwinCATRx.IRxTcAdsClient nativeClient, System.TimeProvider timeProvider)
```
Initializes a new instance of the `T:IoT.Driver.TwinCATRx.TwinCatLogicalTagClient` class.

- Parameter `nativeClient`: The composed ADS client.
- Parameter `timeProvider`: The time provider.

###### `M:IoT.Driver.TwinCATRx.TwinCatLogicalTagClient.CreateTag(IoT.Driver.Core.LogicalTag)`

```csharp
public IoT.Driver.Core.LogicalTag CreateTag(IoT.Driver.Core.LogicalTag tag)
```
Creates a tag from a complete shared tag definition and registers it.

- Parameter `tag`: The complete shared tag definition.
- Returns: The registered tag.

###### `M:IoT.Driver.TwinCATRx.TwinCatLogicalTagClient.CreateTag(System.String,System.String,System.String)`

```csharp
public IoT.Driver.Core.LogicalTag CreateTag(string name, string address, string dataType)
```
Creates and registers a logical TwinCAT tag.

- Parameter `name`: The logical name.
- Parameter `address`: The ADS address.
- Parameter `dataType`: The logical data type.
- Returns: The registered tag.

###### `M:IoT.Driver.TwinCATRx.TwinCatLogicalTagClient.DeleteGroupAsync(System.String,System.Threading.CancellationToken)`

```csharp
public System.Threading.Tasks.Task<bool> DeleteGroupAsync(string name, System.Threading.CancellationToken cancellationToken)
```
Inherits XML documentation from its implemented or overridden member.

- Parameter `name`: The `name` value.
- Parameter `cancellationToken`: The `cancellationToken` value.
- Returns: A `System.Threading.Tasks.Task<bool>` result.

###### `M:IoT.Driver.TwinCATRx.TwinCatLogicalTagClient.DeleteTagAsync(System.String)`

```csharp
public System.Threading.Tasks.Task<bool> DeleteTagAsync(string name)
```
Deletes a tag from SQLite and the live registry.

- Parameter `name`: The logical tag name.
- Returns: Whether an existing tag was deleted.

###### `M:IoT.Driver.TwinCATRx.TwinCatLogicalTagClient.DeleteTagAsync(System.String,System.Threading.CancellationToken)`

```csharp
public System.Threading.Tasks.Task<bool> DeleteTagAsync(string name, System.Threading.CancellationToken cancellationToken)
```
Deletes a tag from SQLite and the live registry.

- Parameter `name`: The logical tag name.
- Parameter `cancellationToken`: The cancellation token.
- Returns: Whether an existing tag was deleted.

###### `M:IoT.Driver.TwinCATRx.TwinCatLogicalTagClient.Dispose`

```csharp
public void Dispose()
```
Releases registry-owned resources without disposing the composed ADS client.

###### `M:IoT.Driver.TwinCATRx.TwinCatLogicalTagClient.EditTagAsync(IoT.Driver.Core.LogicalTag)`

```csharp
public System.Threading.Tasks.Task<bool> EditTagAsync(IoT.Driver.Core.LogicalTag tag)
```
Edits an existing SQLite tag and refreshes the live registry.

- Parameter `tag`: The logical tag.
- Returns: Whether an existing tag was edited.

###### `M:IoT.Driver.TwinCATRx.TwinCatLogicalTagClient.EditTagAsync(IoT.Driver.Core.LogicalTag,System.Threading.CancellationToken)`

```csharp
public System.Threading.Tasks.Task<bool> EditTagAsync(IoT.Driver.Core.LogicalTag tag, System.Threading.CancellationToken cancellationToken)
```
Edits an existing SQLite tag and refreshes the live registry.

- Parameter `tag`: The logical tag.
- Parameter `cancellationToken`: The cancellation token.
- Returns: Whether an existing tag was edited.

###### `M:IoT.Driver.TwinCATRx.TwinCatLogicalTagClient.ExportCsvAsync(System.IO.TextWriter)`

```csharp
public System.Threading.Tasks.Task ExportCsvAsync(System.IO.TextWriter writer)
```
Exports the live registry as CSV.

- Parameter `writer`: The CSV writer.
- Returns: The export operation.

###### `M:IoT.Driver.TwinCATRx.TwinCatLogicalTagClient.ExportCsvAsync(System.IO.TextWriter,System.Char,System.Threading.CancellationToken)`

```csharp
public System.Threading.Tasks.Task ExportCsvAsync(System.IO.TextWriter writer, char delimiter, System.Threading.CancellationToken cancellationToken)
```
Inherits XML documentation from its implemented or overridden member.

- Parameter `writer`: The `writer` value.
- Parameter `delimiter`: The `delimiter` value.
- Parameter `cancellationToken`: The `cancellationToken` value.
- Returns: A `System.Threading.Tasks.Task` result.

###### `M:IoT.Driver.TwinCATRx.TwinCatLogicalTagClient.ExportCsvAsync(System.IO.TextWriter,System.Threading.CancellationToken)`

```csharp
public System.Threading.Tasks.Task ExportCsvAsync(System.IO.TextWriter writer, System.Threading.CancellationToken cancellationToken)
```
Exports the live registry as CSV.

- Parameter `writer`: The CSV writer.
- Parameter `cancellationToken`: The cancellation token.
- Returns: The export operation.

###### `M:IoT.Driver.TwinCATRx.TwinCatLogicalTagClient.GetGroupAsync(System.String,System.Threading.CancellationToken)`

```csharp
public System.Threading.Tasks.Task<IoT.Driver.Core.LogicalTagGroup> GetGroupAsync(string name, System.Threading.CancellationToken cancellationToken)
```
Inherits XML documentation from its implemented or overridden member.

- Parameter `name`: The `name` value.
- Parameter `cancellationToken`: The `cancellationToken` value.
- Returns: A `System.Threading.Tasks.Task<IoT.Driver.Core.LogicalTagGroup>` result.

###### `M:IoT.Driver.TwinCATRx.TwinCatLogicalTagClient.GetTagAsync(System.String)`

```csharp
public System.Threading.Tasks.Task<IoT.Driver.Core.LogicalTag> GetTagAsync(string name)
```
Gets a persisted tag.

- Parameter `name`: The logical tag name.
- Returns: The persisted tag, or null.

###### `M:IoT.Driver.TwinCATRx.TwinCatLogicalTagClient.GetTagAsync(System.String,System.Threading.CancellationToken)`

```csharp
public System.Threading.Tasks.Task<IoT.Driver.Core.LogicalTag> GetTagAsync(string name, System.Threading.CancellationToken cancellationToken)
```
Gets a persisted tag.

- Parameter `name`: The logical tag name.
- Parameter `cancellationToken`: The cancellation token.
- Returns: The persisted tag, or null.

###### `M:IoT.Driver.TwinCATRx.TwinCatLogicalTagClient.ImportCsvAsync(System.IO.TextReader)`

```csharp
public System.Threading.Tasks.Task<System.Collections.Generic.IReadOnlyList<IoT.Driver.Core.LogicalTag>> ImportCsvAsync(System.IO.TextReader reader)
```
Imports CSV definitions into the live registry.

- Parameter `reader`: The CSV reader.
- Returns: The imported tags.

###### `M:IoT.Driver.TwinCATRx.TwinCatLogicalTagClient.ImportCsvAsync(System.IO.TextReader,System.Boolean)`

```csharp
public System.Threading.Tasks.Task<System.Collections.Generic.IReadOnlyList<IoT.Driver.Core.LogicalTag>> ImportCsvAsync(System.IO.TextReader reader, bool replaceExisting)
```
Imports CSV definitions into the live registry.

- Parameter `reader`: The CSV reader.
- Parameter `replaceExisting`: Whether imported tags replace matching live tags.
- Returns: The imported tags.

###### `M:IoT.Driver.TwinCATRx.TwinCatLogicalTagClient.ImportCsvAsync(System.IO.TextReader,System.Boolean,System.Threading.CancellationToken)`

```csharp
public System.Threading.Tasks.Task<System.Collections.Generic.IReadOnlyList<IoT.Driver.Core.LogicalTag>> ImportCsvAsync(System.IO.TextReader reader, bool replaceExisting, System.Threading.CancellationToken cancellationToken)
```
Imports CSV definitions into the live registry.

- Parameter `reader`: The CSV reader.
- Parameter `replaceExisting`: Whether imported tags replace matching live tags.
- Parameter `cancellationToken`: The cancellation token.
- Returns: The imported tags.

###### `M:IoT.Driver.TwinCATRx.TwinCatLogicalTagClient.ImportCsvAsync(System.IO.TextReader,System.Char,System.Boolean,System.Threading.CancellationToken)`

```csharp
public System.Threading.Tasks.Task<System.Collections.Generic.IReadOnlyList<IoT.Driver.Core.LogicalTag>> ImportCsvAsync(System.IO.TextReader reader, char delimiter, bool replaceExisting, System.Threading.CancellationToken cancellationToken)
```
Imports CSV definitions into the live registry.

- Parameter `reader`: The CSV reader.
- Parameter `delimiter`: The CSV delimiter.
- Parameter `replaceExisting`: Whether imported tags replace matching live tags.
- Parameter `cancellationToken`: The cancellation token.
- Returns: The imported tags.

###### `M:IoT.Driver.TwinCATRx.TwinCatLogicalTagClient.ImportCsvAsync(System.IO.TextReader,System.Char,System.Threading.CancellationToken)`

```csharp
public System.Threading.Tasks.Task<System.Collections.Generic.IReadOnlyList<IoT.Driver.Core.LogicalTag>> ImportCsvAsync(System.IO.TextReader reader, char delimiter, System.Threading.CancellationToken cancellationToken)
```
Inherits XML documentation from its implemented or overridden member.

- Parameter `reader`: The `reader` value.
- Parameter `delimiter`: The `delimiter` value.
- Parameter `cancellationToken`: The `cancellationToken` value.
- Returns: A `System.Threading.Tasks.Task<System.Collections.Generic.IReadOnlyList<IoT.Driver.Core.LogicalTag>>` result.

###### `M:IoT.Driver.TwinCATRx.TwinCatLogicalTagClient.InitializeStoreAsync`

```csharp
public System.Threading.Tasks.Task InitializeStoreAsync()
```
Initializes the configured SQLite store.

- Returns: The initialization operation.

###### `M:IoT.Driver.TwinCATRx.TwinCatLogicalTagClient.InitializeStoreAsync(System.Threading.CancellationToken)`

```csharp
public System.Threading.Tasks.Task InitializeStoreAsync(System.Threading.CancellationToken cancellationToken)
```
Initializes the configured SQLite store.

- Parameter `cancellationToken`: The cancellation token.
- Returns: The initialization operation.

###### `M:IoT.Driver.TwinCATRx.TwinCatLogicalTagClient.ListGroupsAsync(System.Threading.CancellationToken)`

```csharp
public System.Threading.Tasks.Task<System.Collections.Generic.IReadOnlyList<IoT.Driver.Core.LogicalTagGroup>> ListGroupsAsync(System.Threading.CancellationToken cancellationToken)
```
Inherits XML documentation from its implemented or overridden member.

- Parameter `cancellationToken`: The `cancellationToken` value.
- Returns: A `System.Threading.Tasks.Task<System.Collections.Generic.IReadOnlyList<IoT.Driver.Core.LogicalTagGroup>>` result.

###### `M:IoT.Driver.TwinCATRx.TwinCatLogicalTagClient.ListTagsAsync(System.Threading.CancellationToken)`

```csharp
public System.Threading.Tasks.Task<System.Collections.Generic.IReadOnlyList<IoT.Driver.Core.LogicalTag>> ListTagsAsync(System.Threading.CancellationToken cancellationToken)
```
Inherits XML documentation from its implemented or overridden member.

- Parameter `cancellationToken`: The `cancellationToken` value.
- Returns: A `System.Threading.Tasks.Task<System.Collections.Generic.IReadOnlyList<IoT.Driver.Core.LogicalTag>>` result.

###### `M:IoT.Driver.TwinCATRx.TwinCatLogicalTagClient.LoadTagsAsync`

```csharp
public System.Threading.Tasks.Task<System.Collections.Generic.IReadOnlyList<IoT.Driver.Core.LogicalTag>> LoadTagsAsync()
```
Dynamically loads persisted tags into the live registry.

- Returns: The loaded tags.

###### `M:IoT.Driver.TwinCATRx.TwinCatLogicalTagClient.LoadTagsAsync(System.Boolean)`

```csharp
public System.Threading.Tasks.Task<System.Collections.Generic.IReadOnlyList<IoT.Driver.Core.LogicalTag>> LoadTagsAsync(bool replaceExisting)
```
Dynamically loads persisted tags into the live registry.

- Parameter `replaceExisting`: Whether persisted tags replace matching live tags.
- Returns: The loaded tags.

###### `M:IoT.Driver.TwinCATRx.TwinCatLogicalTagClient.LoadTagsAsync(System.Boolean,System.Threading.CancellationToken)`

```csharp
public System.Threading.Tasks.Task<System.Collections.Generic.IReadOnlyList<IoT.Driver.Core.LogicalTag>> LoadTagsAsync(bool replaceExisting, System.Threading.CancellationToken cancellationToken)
```
Dynamically loads persisted tags into the live registry.

- Parameter `replaceExisting`: Whether persisted tags replace matching live tags.
- Parameter `cancellationToken`: The cancellation token.
- Returns: The loaded tags.

###### `M:IoT.Driver.TwinCATRx.TwinCatLogicalTagClient.LoadTagsAsync(System.Threading.CancellationToken)`

```csharp
public System.Threading.Tasks.Task<System.Collections.Generic.IReadOnlyList<IoT.Driver.Core.LogicalTag>> LoadTagsAsync(System.Threading.CancellationToken cancellationToken)
```
Inherits XML documentation from its implemented or overridden member.

- Parameter `cancellationToken`: The `cancellationToken` value.
- Returns: A `System.Threading.Tasks.Task<System.Collections.Generic.IReadOnlyList<IoT.Driver.Core.LogicalTag>>` result.

###### `M:IoT.Driver.TwinCATRx.TwinCatLogicalTagClient.Observe(System.String)`

```csharp
public System.IObservable<IoT.Driver.Core.LogicalTagValue> Observe(string tagName)
```
Inherits XML documentation from its implemented or overridden member.

- Parameter `tagName`: The `tagName` value.
- Returns: A `System.IObservable<IoT.Driver.Core.LogicalTagValue>` result.

###### `M:IoT.Driver.TwinCATRx.TwinCatLogicalTagClient.ObserveAsync(System.String,System.Threading.CancellationToken)`

```csharp
public System.Collections.Generic.IAsyncEnumerable<IoT.Driver.Core.LogicalTagValue> ObserveAsync(string tagName, System.Threading.CancellationToken cancellationToken)
```
Inherits XML documentation from its implemented or overridden member.

- Parameter `tagName`: The `tagName` value.
- Parameter `cancellationToken`: The `cancellationToken` value.
- Returns: A `System.Collections.Generic.IAsyncEnumerable<IoT.Driver.Core.LogicalTagValue>` result.

###### `M:IoT.Driver.TwinCATRx.TwinCatLogicalTagClient.ObserveMany(System.Collections.Generic.IReadOnlyCollection`1{System.String})`

```csharp
public System.IObservable<IoT.Driver.Core.LogicalTagValue> ObserveMany(System.Collections.Generic.IReadOnlyCollection<string> tagNames)
```
Executes the `ObserveMany` operation.

- Parameter `tagNames`: The `tagNames` value.
- Returns: A `System.IObservable<IoT.Driver.Core.LogicalTagValue>` result.

###### `M:IoT.Driver.TwinCATRx.TwinCatLogicalTagClient.ObserveManyAsync(System.Collections.Generic.IReadOnlyCollection`1{System.String},System.Threading.CancellationToken)`

```csharp
public System.Collections.Generic.IAsyncEnumerable<IoT.Driver.Core.LogicalTagValue> ObserveManyAsync(System.Collections.Generic.IReadOnlyCollection<string> tagNames, System.Threading.CancellationToken cancellationToken)
```
Executes the `ObserveManyAsync` operation.

- Parameter `tagNames`: The `tagNames` value.
- Parameter `cancellationToken`: The `cancellationToken` value.
- Returns: A `System.Collections.Generic.IAsyncEnumerable<IoT.Driver.Core.LogicalTagValue>` result.

###### `M:IoT.Driver.TwinCATRx.TwinCatLogicalTagClient.ReadAsync(System.String,System.Threading.CancellationToken)`

```csharp
public System.Threading.Tasks.Task<IoT.Driver.Core.TagOperationResult<IoT.Driver.Core.LogicalTagValue>> ReadAsync(string tagName, System.Threading.CancellationToken cancellationToken)
```
Inherits XML documentation from its implemented or overridden member.

- Parameter `tagName`: The `tagName` value.
- Parameter `cancellationToken`: The `cancellationToken` value.
- Returns: A `System.Threading.Tasks.Task<IoT.Driver.Core.TagOperationResult<IoT.Driver.Core.LogicalTagValue>>` result.

###### `M:IoT.Driver.TwinCATRx.TwinCatLogicalTagClient.ReadManyAsync(System.Collections.Generic.IReadOnlyCollection`1{System.String},System.Threading.CancellationToken)`

```csharp
public System.Threading.Tasks.Task<System.Collections.Generic.IReadOnlyList<IoT.Driver.Core.TagOperationResult<IoT.Driver.Core.LogicalTagValue>>> ReadManyAsync(System.Collections.Generic.IReadOnlyCollection<string> tagNames, System.Threading.CancellationToken cancellationToken)
```
Executes the `ReadManyAsync` operation.

- Parameter `tagNames`: The `tagNames` value.
- Parameter `cancellationToken`: The `cancellationToken` value.
- Returns: A `System.Threading.Tasks.Task<System.Collections.Generic.IReadOnlyList<IoT.Driver.Core.TagOperationResult<IoT.Driver.Core.LogicalTagValue>>>` result.

###### `M:IoT.Driver.TwinCATRx.TwinCatLogicalTagClient.RegisterTag(IoT.Driver.Core.LogicalTag)`

```csharp
public void RegisterTag(IoT.Driver.Core.LogicalTag tag)
```
Adds or replaces a logical tag in the live registry.

- Parameter `tag`: The logical tag.

###### `M:IoT.Driver.TwinCATRx.TwinCatLogicalTagClient.RemoveTag(System.String)`

```csharp
public bool RemoveTag(string name)
```
Removes a logical tag from the live registry.

- Parameter `name`: The logical tag name.
- Returns: Whether the tag was removed.

###### `M:IoT.Driver.TwinCATRx.TwinCatLogicalTagClient.UpsertGroupAsync(IoT.Driver.Core.LogicalTagGroup,System.Threading.CancellationToken)`

```csharp
public System.Threading.Tasks.Task UpsertGroupAsync(IoT.Driver.Core.LogicalTagGroup group, System.Threading.CancellationToken cancellationToken)
```
Inherits XML documentation from its implemented or overridden member.

- Parameter `group`: The `group` value.
- Parameter `cancellationToken`: The `cancellationToken` value.
- Returns: A `System.Threading.Tasks.Task` result.

###### `M:IoT.Driver.TwinCATRx.TwinCatLogicalTagClient.UpsertTagAsync(IoT.Driver.Core.LogicalTag)`

```csharp
public System.Threading.Tasks.Task UpsertTagAsync(IoT.Driver.Core.LogicalTag tag)
```
Upserts a tag in SQLite and the live registry.

- Parameter `tag`: The logical tag.
- Returns: The upsert operation.

###### `M:IoT.Driver.TwinCATRx.TwinCatLogicalTagClient.UpsertTagAsync(IoT.Driver.Core.LogicalTag,System.Threading.CancellationToken)`

```csharp
public System.Threading.Tasks.Task UpsertTagAsync(IoT.Driver.Core.LogicalTag tag, System.Threading.CancellationToken cancellationToken)
```
Upserts a tag in SQLite and the live registry.

- Parameter `tag`: The logical tag.
- Parameter `cancellationToken`: The cancellation token.
- Returns: The upsert operation.

###### `M:IoT.Driver.TwinCATRx.TwinCatLogicalTagClient.WriteAsync(IoT.Driver.Core.LogicalTagValue,System.Threading.CancellationToken)`

```csharp
public System.Threading.Tasks.Task<IoT.Driver.Core.TagOperationResult<IoT.Driver.Core.LogicalTagValue>> WriteAsync(IoT.Driver.Core.LogicalTagValue value, System.Threading.CancellationToken cancellationToken)
```
Inherits XML documentation from its implemented or overridden member.

- Parameter `value`: The `value` value.
- Parameter `cancellationToken`: The `cancellationToken` value.
- Returns: A `System.Threading.Tasks.Task<IoT.Driver.Core.TagOperationResult<IoT.Driver.Core.LogicalTagValue>>` result.

###### `M:IoT.Driver.TwinCATRx.TwinCatLogicalTagClient.WriteManyAsync(System.Collections.Generic.IReadOnlyCollection`1{IoT.Driver.Core.LogicalTagValue},System.Threading.CancellationToken)`

```csharp
public System.Threading.Tasks.Task<System.Collections.Generic.IReadOnlyList<IoT.Driver.Core.TagOperationResult<IoT.Driver.Core.LogicalTagValue>>> WriteManyAsync(System.Collections.Generic.IReadOnlyCollection<IoT.Driver.Core.LogicalTagValue> values, System.Threading.CancellationToken cancellationToken)
```
Executes the `WriteManyAsync` operation.

- Parameter `values`: The `values` value.
- Parameter `cancellationToken`: The `cancellationToken` value.
- Returns: A `System.Threading.Tasks.Task<System.Collections.Generic.IReadOnlyList<IoT.Driver.Core.TagOperationResult<IoT.Driver.Core.LogicalTagValue>>>` result.

###### `P:IoT.Driver.TwinCATRx.TwinCatLogicalTagClient.Catalog`

```csharp
public IoT.Driver.Core.ILogicalTagCatalog Catalog { get; }
```
Gets the logical tag catalog.

- Value: The `Catalog` value.

#### `T:IoT.Driver.TwinCATRx.TwinCatPlcConnectionAttribute`

```csharp
public class IoT.Driver.TwinCATRx.TwinCatPlcConnectionAttribute
```
Generates a TwinCAT PLC connection binding.

##### Declared public members

###### `M:IoT.Driver.TwinCATRx.TwinCatPlcConnectionAttribute.#ctor(System.String,System.Int32)`

```csharp
public IoT.Driver.TwinCATRx.TwinCatPlcConnectionAttribute(string adsAddress, int port)
```
Initializes a new instance of the `T:IoT.Driver.TwinCATRx.TwinCatPlcConnectionAttribute` class.

- Parameter `adsAddress`: The ADS address.
- Parameter `port`: The ADS port.

###### `P:IoT.Driver.TwinCATRx.TwinCatPlcConnectionAttribute.AdsAddress`

```csharp
public string AdsAddress { get; }
```
Gets the ADS address.

- Value: The `AdsAddress` value.

###### `P:IoT.Driver.TwinCATRx.TwinCatPlcConnectionAttribute.Port`

```csharp
public int Port { get; }
```
Gets the ADS port.

- Value: The `Port` value.

###### `P:IoT.Driver.TwinCATRx.TwinCatPlcConnectionAttribute.SettingsId`

```csharp
public string SettingsId { get; set; }
```
Gets or sets the optional settings identifier.

- Value: The `SettingsId` value.

#### `T:IoT.Driver.TwinCATRx.TwinCatReactiveStreamAttribute`

```csharp
public class IoT.Driver.TwinCATRx.TwinCatReactiveStreamAttribute
```
Generates observable members for one TwinCAT variable.

##### Declared public members

###### `M:IoT.Driver.TwinCATRx.TwinCatReactiveStreamAttribute.#ctor(System.String,System.Type)`

```csharp
public IoT.Driver.TwinCATRx.TwinCatReactiveStreamAttribute(string variable, System.Type dataType)
```
Initializes a new instance of the `T:IoT.Driver.TwinCATRx.TwinCatReactiveStreamAttribute` class.

- Parameter `variable`: The TwinCAT variable address.
- Parameter `dataType`: The variable data type.

###### `P:IoT.Driver.TwinCATRx.TwinCatReactiveStreamAttribute.DataType`

```csharp
public System.Type DataType { get; }
```
Gets the variable data type.

- Value: The `DataType` value.

###### `P:IoT.Driver.TwinCATRx.TwinCatReactiveStreamAttribute.Id`

```csharp
public string Id { get; set; }
```
Gets or sets the optional correlation identifier.

- Value: The `Id` value.

###### `P:IoT.Driver.TwinCATRx.TwinCatReactiveStreamAttribute.ObservableName`

```csharp
public string ObservableName { get; set; }
```
Gets or sets the generated observable name.

- Value: The `ObservableName` value.

###### `P:IoT.Driver.TwinCATRx.TwinCatReactiveStreamAttribute.PropertyName`

```csharp
public string PropertyName { get; set; }
```
Gets or sets the generated property name.

- Value: The `PropertyName` value.

###### `P:IoT.Driver.TwinCATRx.TwinCatReactiveStreamAttribute.Variable`

```csharp
public string Variable { get; }
```
Gets the TwinCAT variable address.

- Value: The `Variable` value.

#### `T:IoT.Driver.TwinCATRx.TwinCatRxExtensions`

```csharp
public class IoT.Driver.TwinCATRx.TwinCatRxExtensions
```
Observable TwinCAT extensions.

##### Declared public members

###### `M:IoT.Driver.TwinCATRx.TwinCatRxExtensions.CreateClone(CP.Collections.HashTableRx)`

```csharp
public static CP.Collections.HashTableRx CreateClone(CP.Collections.HashTableRx hashTable)
```
Clones the specified HashTableRx.

- Parameter `hashTable`: The HashTableRx instance.
- Returns: A HashTableRx.

###### `M:IoT.Driver.TwinCATRx.TwinCatRxExtensions.CreateStruct(IoT.Driver.TwinCATRx.IRxTcAdsClient,System.String)`

```csharp
public static CP.Collections.HashTableRx CreateStruct(IoT.Driver.TwinCATRx.IRxTcAdsClient client, string variable)
```
Creates the structure.

- Parameter `client`: The reactive TwinCAT client.
- Parameter `variable`: The variable.
- Returns: A HashTableRx with a link to the PLC.

###### `M:IoT.Driver.TwinCATRx.TwinCatRxExtensions.ObserveAsyncObservable``1(IoT.Driver.TwinCATRx.IRxTcAdsClient,System.String,System.Func`2{System.Object,``0})`

```csharp
public static ReactiveUI.Primitives.Async.IObservableAsync<T> ObserveAsyncObservable<T>(IoT.Driver.TwinCATRx.IRxTcAdsClient client, string variable, System.Func<object, T> converter)
```
Executes the `ObserveAsyncObservable` operation.

- Parameter `client`: The `client` value.
- Parameter `variable`: The `variable` value.
- Parameter `converter`: The `converter` value.
- Returns: A `ReactiveUI.Primitives.Async.IObservableAsync<T>` result.

###### `M:IoT.Driver.TwinCATRx.TwinCatRxExtensions.ObserveAsyncObservable``1(IoT.Driver.TwinCATRx.IRxTcAdsClient,System.String,System.String,System.Func`2{System.Object,``0})`

```csharp
public static ReactiveUI.Primitives.Async.IObservableAsync<T> ObserveAsyncObservable<T>(IoT.Driver.TwinCATRx.IRxTcAdsClient client, string variable, string id, System.Func<object, T> converter)
```
Executes the `ObserveAsyncObservable` operation.

- Parameter `client`: The `client` value.
- Parameter `variable`: The `variable` value.
- Parameter `id`: The `id` value.
- Parameter `converter`: The `converter` value.
- Returns: A `ReactiveUI.Primitives.Async.IObservableAsync<T>` result.

###### `M:IoT.Driver.TwinCATRx.TwinCatRxExtensions.Observe``1(IoT.Driver.TwinCATRx.IRxTcAdsClient,System.String,System.Func`2{System.Object,``0})`

```csharp
public static System.IObservable<T> Observe<T>(IoT.Driver.TwinCATRx.IRxTcAdsClient client, string variable, System.Func<object, T> converter)
```
Executes the `Observe` operation.

- Parameter `client`: The `client` value.
- Parameter `variable`: The `variable` value.
- Parameter `converter`: The `converter` value.
- Returns: A `System.IObservable<T>` result.

###### `M:IoT.Driver.TwinCATRx.TwinCatRxExtensions.Observe``1(IoT.Driver.TwinCATRx.IRxTcAdsClient,System.String,System.String,System.Func`2{System.Object,``0})`

```csharp
public static System.IObservable<T> Observe<T>(IoT.Driver.TwinCATRx.IRxTcAdsClient client, string variable, string id, System.Func<object, T> converter)
```
Executes the `Observe` operation.

- Parameter `client`: The `client` value.
- Parameter `variable`: The `variable` value.
- Parameter `id`: The `id` value.
- Parameter `converter`: The `converter` value.
- Returns: A `System.IObservable<T>` result.

###### `M:IoT.Driver.TwinCATRx.TwinCatRxExtensions.StructureReady(CP.Collections.HashTableRx)`

```csharp
public static System.IObservable<CP.Collections.HashTableRx> StructureReady(CP.Collections.HashTableRx hashTable)
```
Returns an observable that fires when the structure is ready.

- Parameter `hashTable`: The HashTableRx instance.
- Returns: An observable when values have been set.

###### `M:IoT.Driver.TwinCATRx.TwinCatRxExtensions.WriteValues(CP.Collections.HashTableRx,System.Action`1{CP.Collections.HashTableRx})`

```csharp
public static bool WriteValues(CP.Collections.HashTableRx hashTable, System.Action<CP.Collections.HashTableRx> setValues)
```
Executes the `WriteValues` operation.

- Parameter `hashTable`: The `hashTable` value.
- Parameter `setValues`: The `setValues` value.
- Returns: A `bool` result.

###### `M:IoT.Driver.TwinCATRx.TwinCatRxExtensions.WriteValuesAsync(CP.Collections.HashTableRx,System.Action`1{CP.Collections.HashTableRx},System.TimeSpan)`

```csharp
public static System.Threading.Tasks.Task<bool> WriteValuesAsync(CP.Collections.HashTableRx hashTable, System.Action<CP.Collections.HashTableRx> setValues, System.TimeSpan time)
```
Executes the `WriteValuesAsync` operation.

- Parameter `hashTable`: The `hashTable` value.
- Parameter `setValues`: The `setValues` value.
- Parameter `time`: The `time` value.
- Returns: A `System.Threading.Tasks.Task<bool>` result.

#### `T:IoT.Driver.TwinCATRx.WriteOnlyAttribute`

```csharp
public class IoT.Driver.TwinCATRx.WriteOnlyAttribute
```
Marks a write-only TwinCAT property.

##### Declared public members

###### `M:IoT.Driver.TwinCATRx.WriteOnlyAttribute.#ctor(System.String)`

```csharp
public IoT.Driver.TwinCATRx.WriteOnlyAttribute(string address)
```
Initializes a new instance of the `T:IoT.Driver.TwinCATRx.WriteOnlyAttribute` class.

- Parameter `address`: The TwinCAT variable address.

###### `P:IoT.Driver.TwinCATRx.WriteOnlyAttribute.Address`

```csharp
public string Address { get; }
```
Gets the TwinCAT variable address.

- Value: The `Address` value.

###### `P:IoT.Driver.TwinCATRx.WriteOnlyAttribute.ArraySize`

```csharp
public int ArraySize { get; set; }
```
Gets or sets the array size.

- Value: The `ArraySize` value.

###### `P:IoT.Driver.TwinCATRx.WriteOnlyAttribute.Id`

```csharp
public string Id { get; set; }
```
Gets or sets the optional correlation identifier.

- Value: The `Id` value.

### `TwinCATRx.Core`

Exported public types: 16; declared public members: 112.

#### `T:IoT.Driver.TwinCATRx.Core.CSharpLanguage`

```csharp
public class IoT.Driver.TwinCATRx.Core.CSharpLanguage
```
C Sharp Language.

##### Declared public members

###### `M:IoT.Driver.TwinCATRx.Core.CSharpLanguage.#ctor`

```csharp
public IoT.Driver.TwinCATRx.Core.CSharpLanguage()
```
Initializes a new instance of `IoT.Driver.TwinCATRx.Core.CSharpLanguage`.

###### `M:IoT.Driver.TwinCATRx.Core.CSharpLanguage.CreateAssembly(System.String,System.String)`

```csharp
public static bool CreateAssembly(string code, string assemblyFileName)
```
Creates the assembly.

- Parameter `code`: The code.
- Parameter `assemblyFileName`: Name of the assembly file.
- Returns: A bool.

###### `M:IoT.Driver.TwinCATRx.Core.CSharpLanguage.CreateLibraryCompilation(System.String,System.Boolean)`

```csharp
public Microsoft.CodeAnalysis.Compilation CreateLibraryCompilation(string assemblyName, bool enableOptimisations)
```
Inherits XML documentation from its implemented or overridden member.

- Parameter `assemblyName`: The `assemblyName` value.
- Parameter `enableOptimisations`: The `enableOptimisations` value.
- Returns: A `Microsoft.CodeAnalysis.Compilation` result.

###### `M:IoT.Driver.TwinCATRx.Core.CSharpLanguage.ParseText(System.String,Microsoft.CodeAnalysis.SourceCodeKind)`

```csharp
public Microsoft.CodeAnalysis.SyntaxTree ParseText(string code, Microsoft.CodeAnalysis.SourceCodeKind kind)
```
Inherits XML documentation from its implemented or overridden member.

- Parameter `code`: The `code` value.
- Parameter `kind`: The `kind` value.
- Returns: A `Microsoft.CodeAnalysis.SyntaxTree` result.

#### `T:IoT.Driver.TwinCATRx.Core.CodeGenerator`

```csharp
public class IoT.Driver.TwinCATRx.Core.CodeGenerator
```
Code Generator.

##### Declared public members

###### `M:IoT.Driver.TwinCATRx.Core.CodeGenerator.#ctor`

```csharp
public IoT.Driver.TwinCATRx.Core.CodeGenerator()
```
Initializes a new instance of the `T:IoT.Driver.TwinCATRx.Core.CodeGenerator` class.

###### `M:IoT.Driver.TwinCATRx.Core.CodeGenerator.#ctor(System.Action`1{System.Exception})`

```csharp
public IoT.Driver.TwinCATRx.Core.CodeGenerator(System.Action<System.Exception> errorHandler)
```
Initializes a new instance of `IoT.Driver.TwinCATRx.Core.CodeGenerator`.

- Parameter `errorHandler`: The `errorHandler` value.

###### `M:IoT.Driver.TwinCATRx.Core.CodeGenerator.CreateCSharpCode(IoT.Driver.TwinCATRx.Core.INodeEmulator)`

```csharp
public bool CreateCSharpCode(IoT.Driver.TwinCATRx.Core.INodeEmulator selectedTN)
```
Creates a C# code file using the default TwinCAT version.

- Parameter `selectedTN`: The selected node.
- Returns: true when code was created.

###### `M:IoT.Driver.TwinCATRx.Core.CodeGenerator.CreateCSharpCode(IoT.Driver.TwinCATRx.Core.INodeEmulator,System.Boolean)`

```csharp
public bool CreateCSharpCode(IoT.Driver.TwinCATRx.Core.INodeEmulator selectedTN, bool isTwinCat3)
```
Creates a C# code file based on the selected node structure.

- Parameter `selectedTN`: The selected tn.
- Parameter `isTwinCat3`: Whether TwinCAT 3 packing should be used.
- Returns: Result as a Boolean.

###### `M:IoT.Driver.TwinCATRx.Core.CodeGenerator.CreateCSharpCode(IoT.Driver.TwinCATRx.Core.INodeEmulator,System.String)`

```csharp
public bool CreateCSharpCode(IoT.Driver.TwinCATRx.Core.INodeEmulator selectedTN, string fileName)
```
Creates a C# code file using default generation settings.

- Parameter `selectedTN`: The `selectedTN` value.
- Parameter `fileName`: The `fileName` value.
- Returns: A `bool` result.

###### `M:IoT.Driver.TwinCATRx.Core.CodeGenerator.CreateCSharpCode(IoT.Driver.TwinCATRx.Core.INodeEmulator,System.String,System.Boolean)`

```csharp
public bool CreateCSharpCode(IoT.Driver.TwinCATRx.Core.INodeEmulator selectedTN, string fileName, bool isTwinCat3)
```
Creates a C# code file using the default namespace.

- Parameter `selectedTN`: The `selectedTN` value.
- Parameter `fileName`: The `fileName` value.
- Parameter `isTwinCat3`: The `isTwinCat3` value.
- Returns: A `bool` result.

###### `M:IoT.Driver.TwinCATRx.Core.CodeGenerator.CreateCSharpCode(IoT.Driver.TwinCATRx.Core.INodeEmulator,System.String,System.Boolean,System.String)`

```csharp
public bool CreateCSharpCode(IoT.Driver.TwinCATRx.Core.INodeEmulator selectedTN, string fileName, bool isTwinCat3, string classNamespace)
```
Creates a C# code file based on the selected node structure.

- Parameter `selectedTN`: The selected tn.
- Parameter `fileName`: Name of the file.
- Parameter `isTwinCat3`: if set to true [is twin cat3].
- Parameter `classNamespace`: The class namespace.
- Returns: Result as a Boolean.

###### `M:IoT.Driver.TwinCATRx.Core.CodeGenerator.CreateCSharpCodeString(IoT.Driver.TwinCATRx.Core.INodeEmulator)`

```csharp
public string CreateCSharpCodeString(IoT.Driver.TwinCATRx.Core.INodeEmulator selectedTN)
```
Creates a C# code string using default generation settings.

- Parameter `selectedTN`: The selected node.
- Returns: The generated code.

###### `M:IoT.Driver.TwinCATRx.Core.CodeGenerator.CreateCSharpCodeString(IoT.Driver.TwinCATRx.Core.INodeEmulator,System.Boolean)`

```csharp
public string CreateCSharpCodeString(IoT.Driver.TwinCATRx.Core.INodeEmulator selectedTN, bool isTwinCat3)
```
Creates a C# code string using the default namespace.

- Parameter `selectedTN`: The `selectedTN` value.
- Parameter `isTwinCat3`: The `isTwinCat3` value.
- Returns: A `string` result.

###### `M:IoT.Driver.TwinCATRx.Core.CodeGenerator.CreateCSharpCodeString(IoT.Driver.TwinCATRx.Core.INodeEmulator,System.Boolean,System.String)`

```csharp
public string CreateCSharpCodeString(IoT.Driver.TwinCATRx.Core.INodeEmulator selectedTN, bool isTwinCat3, string classNamespace)
```
Creates the C# code string.

- Parameter `selectedTN`: The selected tn.
- Parameter `isTwinCat3`: if set to true [is twin cat3].
- Parameter `classNamespace`: The class namespace.
- Returns: A Value.

###### `M:IoT.Driver.TwinCATRx.Core.CodeGenerator.CreateDll(IoT.Driver.TwinCATRx.Core.INodeEmulator)`

```csharp
public bool CreateDll(IoT.Driver.TwinCATRx.Core.INodeEmulator selectedTN)
```
Creates a DLL based on the selected node structure.

- Parameter `selectedTN`: The selected tn.
- Returns: Result as a Boolean.

###### `M:IoT.Driver.TwinCATRx.Core.CodeGenerator.CreateDll(IoT.Driver.TwinCATRx.Core.INodeEmulator,System.Boolean)`

```csharp
public bool CreateDll(IoT.Driver.TwinCATRx.Core.INodeEmulator selectedTN, bool isTwinCat3)
```
Creates a DLL based on the selected node structure.

- Parameter `selectedTN`: The selected node.
- Parameter `isTwinCat3`: Whether TwinCAT 3 packing should be used.
- Returns: true when the DLL was created.

###### `M:IoT.Driver.TwinCATRx.Core.CodeGenerator.CreateDll(IoT.Driver.TwinCATRx.Core.INodeEmulator,System.String)`

```csharp
public bool CreateDll(IoT.Driver.TwinCATRx.Core.INodeEmulator selectedTN, string fileName)
```
Creates a DLL using default generation settings.

- Parameter `selectedTN`: The `selectedTN` value.
- Parameter `fileName`: The `fileName` value.
- Returns: A `bool` result.

###### `M:IoT.Driver.TwinCATRx.Core.CodeGenerator.CreateDll(IoT.Driver.TwinCATRx.Core.INodeEmulator,System.String,System.Boolean)`

```csharp
public bool CreateDll(IoT.Driver.TwinCATRx.Core.INodeEmulator selectedTN, string fileName, bool isTwinCat3)
```
Creates a DLL using the default namespace.

- Parameter `selectedTN`: The `selectedTN` value.
- Parameter `fileName`: The `fileName` value.
- Parameter `isTwinCat3`: The `isTwinCat3` value.
- Returns: A `bool` result.

###### `M:IoT.Driver.TwinCATRx.Core.CodeGenerator.CreateDll(IoT.Driver.TwinCATRx.Core.INodeEmulator,System.String,System.Boolean,System.String)`

```csharp
public bool CreateDll(IoT.Driver.TwinCATRx.Core.INodeEmulator selectedTN, string fileName, bool isTwinCat3, string classNamespace)
```
Creates a DLL based on the selected node structure.

- Parameter `selectedTN`: The selected tn.
- Parameter `fileName`: Name of the file.
- Parameter `isTwinCat3`: if set to true [is twincat3].
- Parameter `classNamespace`: The class namespace.
- Returns: Result as a Boolean.

###### `M:IoT.Driver.TwinCATRx.Core.CodeGenerator.CreateDll(System.String,System.String)`

```csharp
public bool CreateDll(string sourceCode, string fileName)
```
Creates the DLL from raw source.

- Parameter `sourceCode`: The C# source code.
- Parameter `fileName`: Name of the file.
- Returns: A Value.

###### `M:IoT.Driver.TwinCATRx.Core.CodeGenerator.Dispose`

```csharp
public void Dispose()
```
Performs application-defined tasks associated with freeing resources.

###### `M:IoT.Driver.TwinCATRx.Core.CodeGenerator.LoadSymbols(System.Int32)`

```csharp
public System.Collections.Generic.HashSet<IoT.Driver.TwinCATRx.Core.INodeEmulator> LoadSymbols(int port)
```
Loads symbols from the specified PLC ADS port.

- Parameter `port`: The port.
- Returns: A Value.

###### `M:IoT.Driver.TwinCATRx.Core.CodeGenerator.LoadSymbols(System.String)`

```csharp
public System.Collections.Generic.HashSet<IoT.Driver.TwinCATRx.Core.INodeEmulator> LoadSymbols(string adsAddress)
```
Loads symbols from the specified PLC ADS address.

- Parameter `adsAddress`: The ADS address.
- Returns: HashSet(Of NodeEmulator).

###### `M:IoT.Driver.TwinCATRx.Core.CodeGenerator.LoadSymbols(System.String,System.Int32)`

```csharp
public System.Collections.Generic.HashSet<IoT.Driver.TwinCATRx.Core.INodeEmulator> LoadSymbols(string adsAddress, int port)
```
Loads symbols from the specified PLC ADS address and port.

- Parameter `adsAddress`: The ADS address.
- Parameter `port`: The port.
- Returns: HashSet(Of NodeEmulator).

###### `M:IoT.Driver.TwinCATRx.Core.CodeGenerator.PLCToCSharpTypeConverter(System.String)`

```csharp
public static string PLCToCSharpTypeConverter(string plcType)
```
Converts a supported PLC scalar, string, or array type name to its CLR representation.

- Parameter `plcType`: Type of the PLC.
- Returns: A Value.

###### `M:IoT.Driver.TwinCATRx.Core.CodeGenerator.ReadSymbol(System.String,System.Int32,System.String,System.Type)`

```csharp
public object ReadSymbol(string adsAddress, int port, string variable, System.Type variableType)
```
Reads the symbol.

- Parameter `adsAddress`: The ADS address.
- Parameter `port`: The port.
- Parameter `variable`: The variable.
- Parameter `variableType`: Type of the variable.
- Returns: A Value.

###### `M:IoT.Driver.TwinCATRx.Core.CodeGenerator.SearchSymbols(System.String)`

```csharp
public IoT.Driver.TwinCATRx.Core.INodeEmulator SearchSymbols(string symbolName)
```
Searches for the nearest matching symbol list element.

- Parameter `symbolName`: Name of the symbol.
- Returns: NodeEmulator.

###### `P:IoT.Driver.TwinCATRx.Core.CodeGenerator.SymbolList`

```csharp
public System.Collections.Generic.HashSet<IoT.Driver.TwinCATRx.Core.INodeEmulator> SymbolList { get; }
```
Gets the symbol list.

- Value: The symbol list.

#### `T:IoT.Driver.TwinCATRx.Core.DirectoryInfoExtensions`

```csharp
public class IoT.Driver.TwinCATRx.Core.DirectoryInfoExtensions
```
Provides filtered file enumeration extensions for `T:System.IO.DirectoryInfo` .

##### Declared public members

###### `M:IoT.Driver.TwinCATRx.Core.DirectoryInfoExtensions.GetFilesWhere(System.IO.DirectoryInfo,System.Func`2{System.IO.FileInfo,System.Boolean})`

```csharp
public static System.IO.FileInfo[] GetFilesWhere(System.IO.DirectoryInfo directory, System.Func<System.IO.FileInfo, bool> predicate)
```
Executes the `GetFilesWhere` operation.

- Parameter `directory`: The `directory` value.
- Parameter `predicate`: The `predicate` value.
- Returns: A `System.IO.FileInfo[]` result.

###### `M:IoT.Driver.TwinCATRx.Core.DirectoryInfoExtensions.GetFilesWhere(System.IO.DirectoryInfo,System.String,System.Func`2{System.IO.FileInfo,System.Boolean})`

```csharp
public static System.IO.FileInfo[] GetFilesWhere(System.IO.DirectoryInfo directory, string searchPattern, System.Func<System.IO.FileInfo, bool> predicate)
```
Executes the `GetFilesWhere` operation.

- Parameter `directory`: The `directory` value.
- Parameter `searchPattern`: The `searchPattern` value.
- Parameter `predicate`: The `predicate` value.
- Returns: A `System.IO.FileInfo[]` result.

###### `M:IoT.Driver.TwinCATRx.Core.DirectoryInfoExtensions.GetFilesWhere(System.IO.DirectoryInfo,System.String,System.IO.SearchOption,System.Func`2{System.IO.FileInfo,System.Boolean})`

```csharp
public static System.IO.FileInfo[] GetFilesWhere(System.IO.DirectoryInfo directory, string searchPattern, System.IO.SearchOption searchOption, System.Func<System.IO.FileInfo, bool> predicate)
```
Executes the `GetFilesWhere` operation.

- Parameter `directory`: The `directory` value.
- Parameter `searchPattern`: The `searchPattern` value.
- Parameter `searchOption`: The `searchOption` value.
- Parameter `predicate`: The `predicate` value.
- Returns: A `System.IO.FileInfo[]` result.

###### `M:IoT.Driver.TwinCATRx.Core.DirectoryInfoExtensions.GetFilesWhere(System.IO.DirectoryInfo,System.String[],System.Func`2{System.IO.FileInfo,System.Boolean})`

```csharp
public static System.IO.FileInfo[] GetFilesWhere(System.IO.DirectoryInfo directory, string[] searchPatterns, System.Func<System.IO.FileInfo, bool> predicate)
```
Executes the `GetFilesWhere` operation.

- Parameter `directory`: The `directory` value.
- Parameter `searchPatterns`: The `searchPatterns` value.
- Parameter `predicate`: The `predicate` value.
- Returns: A `System.IO.FileInfo[]` result.

###### `M:IoT.Driver.TwinCATRx.Core.DirectoryInfoExtensions.GetFilesWhere(System.IO.DirectoryInfo,System.String[],System.IO.SearchOption,System.Func`2{System.IO.FileInfo,System.Boolean})`

```csharp
public static System.IO.FileInfo[] GetFilesWhere(System.IO.DirectoryInfo directory, string[] searchPatterns, System.IO.SearchOption searchOption, System.Func<System.IO.FileInfo, bool> predicate)
```
Executes the `GetFilesWhere` operation.

- Parameter `directory`: The `directory` value.
- Parameter `searchPatterns`: The `searchPatterns` value.
- Parameter `searchOption`: The `searchOption` value.
- Parameter `predicate`: The `predicate` value.
- Returns: A `System.IO.FileInfo[]` result.

#### `T:IoT.Driver.TwinCATRx.Core.IAdsNotification`

```csharp
public interface IoT.Driver.TwinCATRx.Core.IAdsNotification
```
Configures a PLC notification delivered by ADS.

##### Declared public members

###### `P:IoT.Driver.TwinCATRx.Core.IAdsNotification.AdsTransMode`

```csharp
public TwinCAT.Ads.AdsTransMode AdsTransMode { get; }
```
Gets the ADS transmission mode.

- Value: The `AdsTransMode` value.

###### `P:IoT.Driver.TwinCATRx.Core.IAdsNotification.CycleTime`

```csharp
public int CycleTime { get; }
```
Gets the notification cycle time in milliseconds.

- Value: The `CycleTime` value.

###### `P:IoT.Driver.TwinCATRx.Core.IAdsNotification.MaxDelay`

```csharp
public int MaxDelay { get; }
```
Gets the maximum delivery delay in milliseconds.

- Value: The `MaxDelay` value.

#### `T:IoT.Driver.TwinCATRx.Core.ICodeGenerator`

```csharp
public interface IoT.Driver.TwinCATRx.Core.ICodeGenerator
```
Interface for Code Generator.

##### Declared public members

###### `M:IoT.Driver.TwinCATRx.Core.ICodeGenerator.CreateCSharpCode(IoT.Driver.TwinCATRx.Core.INodeEmulator)`

```csharp
public bool CreateCSharpCode(IoT.Driver.TwinCATRx.Core.INodeEmulator selectedTN)
```
Creates the c sharp code.

- Parameter `selectedTN`: The selected tn.
- Returns: A Value.

###### `M:IoT.Driver.TwinCATRx.Core.ICodeGenerator.CreateCSharpCode(IoT.Driver.TwinCATRx.Core.INodeEmulator,System.Boolean)`

```csharp
public bool CreateCSharpCode(IoT.Driver.TwinCATRx.Core.INodeEmulator selectedTN, bool isTwinCat3)
```
Creates the c sharp code.

- Parameter `selectedTN`: The selected tn.
- Parameter `isTwinCat3`: Whether TwinCAT 3 conventions are used.
- Returns: A Value.

###### `M:IoT.Driver.TwinCATRx.Core.ICodeGenerator.CreateCSharpCode(IoT.Driver.TwinCATRx.Core.INodeEmulator,System.String)`

```csharp
public bool CreateCSharpCode(IoT.Driver.TwinCATRx.Core.INodeEmulator selectedTN, string fileName)
```
Creates the c sharp code.

- Parameter `selectedTN`: The selected tn.
- Parameter `fileName`: Name of the file.
- Returns: A Value.

###### `M:IoT.Driver.TwinCATRx.Core.ICodeGenerator.CreateCSharpCode(IoT.Driver.TwinCATRx.Core.INodeEmulator,System.String,System.Boolean)`

```csharp
public bool CreateCSharpCode(IoT.Driver.TwinCATRx.Core.INodeEmulator selectedTN, string fileName, bool isTwinCat3)
```
Creates the c sharp code.

- Parameter `selectedTN`: The selected tn.
- Parameter `fileName`: Name of the file.
- Parameter `isTwinCat3`: if set to true [is twin cat3].
- Returns: A Value.

###### `M:IoT.Driver.TwinCATRx.Core.ICodeGenerator.CreateCSharpCode(IoT.Driver.TwinCATRx.Core.INodeEmulator,System.String,System.Boolean,System.String)`

```csharp
public bool CreateCSharpCode(IoT.Driver.TwinCATRx.Core.INodeEmulator selectedTN, string fileName, bool isTwinCat3, string classNamespace)
```
Creates the c sharp code.

- Parameter `selectedTN`: The selected tn.
- Parameter `fileName`: Name of the file.
- Parameter `isTwinCat3`: Whether TwinCAT 3 conventions are used.
- Parameter `classNamespace`: The namespace for generated types.
- Returns: A Value.

###### `M:IoT.Driver.TwinCATRx.Core.ICodeGenerator.CreateCSharpCodeString(IoT.Driver.TwinCATRx.Core.INodeEmulator)`

```csharp
public string CreateCSharpCodeString(IoT.Driver.TwinCATRx.Core.INodeEmulator selectedTN)
```
Creates the c sharp code string.

- Parameter `selectedTN`: The selected tn.
- Returns: A Value.

###### `M:IoT.Driver.TwinCATRx.Core.ICodeGenerator.CreateCSharpCodeString(IoT.Driver.TwinCATRx.Core.INodeEmulator,System.Boolean)`

```csharp
public string CreateCSharpCodeString(IoT.Driver.TwinCATRx.Core.INodeEmulator selectedTN, bool isTwinCat3)
```
Creates the c sharp code string.

- Parameter `selectedTN`: The selected tn.
- Parameter `isTwinCat3`: if set to true [is twin cat3].
- Returns: A Value.

###### `M:IoT.Driver.TwinCATRx.Core.ICodeGenerator.CreateCSharpCodeString(IoT.Driver.TwinCATRx.Core.INodeEmulator,System.Boolean,System.String)`

```csharp
public string CreateCSharpCodeString(IoT.Driver.TwinCATRx.Core.INodeEmulator selectedTN, bool isTwinCat3, string classNamespace)
```
Creates the c sharp code string.

- Parameter `selectedTN`: The selected tn.
- Parameter `isTwinCat3`: Whether TwinCAT 3 conventions are used.
- Parameter `classNamespace`: The namespace for generated types.
- Returns: A Value.

###### `M:IoT.Driver.TwinCATRx.Core.ICodeGenerator.CreateDll(IoT.Driver.TwinCATRx.Core.INodeEmulator)`

```csharp
public bool CreateDll(IoT.Driver.TwinCATRx.Core.INodeEmulator selectedTN)
```
Creates the DLL.

- Parameter `selectedTN`: The selected tn.
- Returns: A Value.

###### `M:IoT.Driver.TwinCATRx.Core.ICodeGenerator.CreateDll(IoT.Driver.TwinCATRx.Core.INodeEmulator,System.Boolean)`

```csharp
public bool CreateDll(IoT.Driver.TwinCATRx.Core.INodeEmulator selectedTN, bool isTwinCat3)
```
Creates the DLL.

- Parameter `selectedTN`: The selected tn.
- Parameter `isTwinCat3`: Whether TwinCAT 3 conventions are used.
- Returns: A Value.

###### `M:IoT.Driver.TwinCATRx.Core.ICodeGenerator.CreateDll(IoT.Driver.TwinCATRx.Core.INodeEmulator,System.String)`

```csharp
public bool CreateDll(IoT.Driver.TwinCATRx.Core.INodeEmulator selectedTN, string fileName)
```
Creates the DLL.

- Parameter `selectedTN`: The selected tn.
- Parameter `fileName`: Name of the file.
- Returns: A Value.

###### `M:IoT.Driver.TwinCATRx.Core.ICodeGenerator.CreateDll(IoT.Driver.TwinCATRx.Core.INodeEmulator,System.String,System.Boolean)`

```csharp
public bool CreateDll(IoT.Driver.TwinCATRx.Core.INodeEmulator selectedTN, string fileName, bool isTwinCat3)
```
Creates the DLL.

- Parameter `selectedTN`: The selected tn.
- Parameter `fileName`: Name of the file.
- Parameter `isTwinCat3`: if set to true [is twin cat3].
- Returns: A Value.

###### `M:IoT.Driver.TwinCATRx.Core.ICodeGenerator.CreateDll(IoT.Driver.TwinCATRx.Core.INodeEmulator,System.String,System.Boolean,System.String)`

```csharp
public bool CreateDll(IoT.Driver.TwinCATRx.Core.INodeEmulator selectedTN, string fileName, bool isTwinCat3, string classNamespace)
```
Creates the DLL.

- Parameter `selectedTN`: The selected tn.
- Parameter `fileName`: Name of the file.
- Parameter `isTwinCat3`: Whether TwinCAT 3 conventions are used.
- Parameter `classNamespace`: The namespace for generated types.
- Returns: A Value.

###### `M:IoT.Driver.TwinCATRx.Core.ICodeGenerator.CreateDll(System.String,System.String)`

```csharp
public bool CreateDll(string sourceCode, string fileName)
```
Creates the DLL.

- Parameter `sourceCode`: The C# source code.
- Parameter `fileName`: Name of the file.
- Returns: A Value.

###### `M:IoT.Driver.TwinCATRx.Core.ICodeGenerator.LoadSymbols(System.Int32)`

```csharp
public System.Collections.Generic.HashSet<IoT.Driver.TwinCATRx.Core.INodeEmulator> LoadSymbols(int port)
```
Loads the symbols.

- Parameter `port`: The port.
- Returns: A Value.

###### `M:IoT.Driver.TwinCATRx.Core.ICodeGenerator.LoadSymbols(System.String)`

```csharp
public System.Collections.Generic.HashSet<IoT.Driver.TwinCATRx.Core.INodeEmulator> LoadSymbols(string adsAddress)
```
Loads the symbols.

- Parameter `adsAddress`: The ADS address.
- Returns: A Value.

###### `M:IoT.Driver.TwinCATRx.Core.ICodeGenerator.LoadSymbols(System.String,System.Int32)`

```csharp
public System.Collections.Generic.HashSet<IoT.Driver.TwinCATRx.Core.INodeEmulator> LoadSymbols(string adsAddress, int port)
```
Loads the symbols.

- Parameter `adsAddress`: The ADS address.
- Parameter `port`: The port.
- Returns: A Value.

###### `M:IoT.Driver.TwinCATRx.Core.ICodeGenerator.ReadSymbol(System.String,System.Int32,System.String,System.Type)`

```csharp
public object ReadSymbol(string adsAddress, int port, string variable, System.Type variableType)
```
Reads the symbol.

- Parameter `adsAddress`: The ADS address.
- Parameter `port`: The port.
- Parameter `variable`: The variable.
- Parameter `variableType`: Type of the variable.
- Returns: A Value.

###### `M:IoT.Driver.TwinCATRx.Core.ICodeGenerator.SearchSymbols(System.String)`

```csharp
public IoT.Driver.TwinCATRx.Core.INodeEmulator SearchSymbols(string symbolName)
```
Searches the symbols.

- Parameter `symbolName`: Name of the symbol.
- Returns: A Value.

###### `P:IoT.Driver.TwinCATRx.Core.ICodeGenerator.SymbolList`

```csharp
public System.Collections.Generic.HashSet<IoT.Driver.TwinCATRx.Core.INodeEmulator> SymbolList { get; }
```
Gets the symbol list.

- Value: The symbol list.

#### `T:IoT.Driver.TwinCATRx.Core.ILanguageService`

```csharp
public interface IoT.Driver.TwinCATRx.Core.ILanguageService
```
I Language Service.

##### Declared public members

###### `M:IoT.Driver.TwinCATRx.Core.ILanguageService.CreateLibraryCompilation(System.String,System.Boolean)`

```csharp
public Microsoft.CodeAnalysis.Compilation CreateLibraryCompilation(string assemblyName, bool enableOptimisations)
```
Creates the library compilation.

- Parameter `assemblyName`: Name of the assembly.
- Parameter `enableOptimisations`: if set to true [enable optimisations].
- Returns: A Compilation.

###### `M:IoT.Driver.TwinCATRx.Core.ILanguageService.ParseText(System.String,Microsoft.CodeAnalysis.SourceCodeKind)`

```csharp
public Microsoft.CodeAnalysis.SyntaxTree ParseText(string code, Microsoft.CodeAnalysis.SourceCodeKind kind)
```
Parses the text.

- Parameter `code`: The code.
- Parameter `kind`: The kind.
- Returns: A SyntaxTree.

#### `T:IoT.Driver.TwinCATRx.Core.INodeEmulator`

```csharp
public interface IoT.Driver.TwinCATRx.Core.INodeEmulator
```
Interface for Node Emulator.

##### Declared public members

###### `P:IoT.Driver.TwinCATRx.Core.INodeEmulator.Nodes`

```csharp
public System.Collections.Generic.HashSet<IoT.Driver.TwinCATRx.Core.INodeEmulator> Nodes { get; }
```
Gets the nodes.

- Value: The nodes.

###### `P:IoT.Driver.TwinCATRx.Core.INodeEmulator.Tag`

```csharp
public object Tag { get; set; }
```
Gets or sets the tag.

- Value: The tag.

###### `P:IoT.Driver.TwinCATRx.Core.INodeEmulator.Text`

```csharp
public string Text { get; set; }
```
Gets or sets the text.

- Value: The text.

#### `T:IoT.Driver.TwinCATRx.Core.INotification`

```csharp
public interface IoT.Driver.TwinCATRx.Core.INotification
```
Interface for Notification.

##### Declared public members

###### `P:IoT.Driver.TwinCATRx.Core.INotification.ArraySize`

```csharp
public int ArraySize { get; }
```
Gets the size of the array.

- Value: The size of the array.

###### `P:IoT.Driver.TwinCATRx.Core.INotification.UpdateRate`

```csharp
public int UpdateRate { get; }
```
Gets the update rate.

- Value: The update rate.

###### `P:IoT.Driver.TwinCATRx.Core.INotification.Variable`

```csharp
public string Variable { get; }
```
Gets the variable.

- Value: The variable.

#### `T:IoT.Driver.TwinCATRx.Core.ISettings`

```csharp
public interface IoT.Driver.TwinCATRx.Core.ISettings
```
Interface for engine settings.

##### Declared public members

###### `M:IoT.Driver.TwinCATRx.Core.ISettings.Defaults``1(``0)`

```csharp
public T Defaults<T>(T defaultSettings)
```
Gets or sets Default settings.

- Parameter `defaultSettings`: The settings instance that establishes the requested type.
- Returns: Default values of type T.

###### `P:IoT.Driver.TwinCATRx.Core.ISettings.AdsAddress`

```csharp
public string AdsAddress { get; set; }
```
Gets or sets the ads address.

- Value: The ads address.

###### `P:IoT.Driver.TwinCATRx.Core.ISettings.Notifications`

```csharp
public System.Collections.Generic.IList<IoT.Driver.TwinCATRx.Core.INotification> Notifications { get; }
```
Gets or sets Notifications of this Engine.

- Value: The `Notifications` value.

###### `P:IoT.Driver.TwinCATRx.Core.ISettings.Port`

```csharp
public int Port { get; set; }
```
Gets or sets the port.

- Value: The port.

###### `P:IoT.Driver.TwinCATRx.Core.ISettings.SettingsId`

```csharp
public string SettingsId { get; set; }
```
Gets or sets System Identifier.

- Value: The `SettingsId` value.

###### `P:IoT.Driver.TwinCATRx.Core.ISettings.WriteVariables`

```csharp
public System.Collections.Generic.IList<IoT.Driver.TwinCATRx.Core.IWriteVariable> WriteVariables { get; }
```
Gets or sets Write variables to this Engine.

- Value: The `WriteVariables` value.

#### `T:IoT.Driver.TwinCATRx.Core.IWriteVariable`

```csharp
public interface IoT.Driver.TwinCATRx.Core.IWriteVariable
```
Interface for Write Variable.

##### Declared public members

###### `P:IoT.Driver.TwinCATRx.Core.IWriteVariable.ArraySize`

```csharp
public int ArraySize { get; }
```
Gets the size of the array.

- Value: The size of the array.

###### `P:IoT.Driver.TwinCATRx.Core.IWriteVariable.Variable`

```csharp
public string Variable { get; }
```
Gets the variable.

- Value: The variable.

#### `T:IoT.Driver.TwinCATRx.Core.Settings`

```csharp
public class IoT.Driver.TwinCATRx.Core.Settings
```
Base settings for Engine Settings file.

##### Declared public members

###### `M:IoT.Driver.TwinCATRx.Core.Settings.#ctor`

```csharp
public IoT.Driver.TwinCATRx.Core.Settings()
```
Initializes a new instance of `IoT.Driver.TwinCATRx.Core.Settings`.

###### `M:IoT.Driver.TwinCATRx.Core.Settings.Defaults``1(``0)`

```csharp
public T Defaults<T>(T defaultSettings)
```
Creates default settings when no persisted file exists.

- Parameter `defaultSettings`: The settings instance that establishes the requested type.
- Returns: The default settings.

###### `P:IoT.Driver.TwinCATRx.Core.Settings.AdsAddress`

```csharp
public string AdsAddress { get; set; }
```
Gets or sets the Ads Address.

- Value: The `AdsAddress` value.

###### `P:IoT.Driver.TwinCATRx.Core.Settings.Notifications`

```csharp
public System.Collections.Generic.List<IoT.Driver.TwinCATRx.Core.INotification> Notifications { get; set; }
```
Gets or sets Notifications of this Engine.

- Value: The `Notifications` value.

###### `P:IoT.Driver.TwinCATRx.Core.Settings.Port`

```csharp
public int Port { get; set; }
```
Gets or sets the Port of the PLC to connect to.

- Value: The `Port` value.

###### `P:IoT.Driver.TwinCATRx.Core.Settings.SettingsId`

```csharp
public string SettingsId { get; set; }
```
Gets or sets System Identifier.

- Value: The `SettingsId` value.

###### `P:IoT.Driver.TwinCATRx.Core.Settings.WriteVariables`

```csharp
public System.Collections.Generic.List<IoT.Driver.TwinCATRx.Core.IWriteVariable> WriteVariables { get; set; }
```
Gets or sets Write variables to this Engine.

- Value: The `WriteVariables` value.

#### `T:IoT.Driver.TwinCATRx.Core.SimpleTypeException`

```csharp
public class IoT.Driver.TwinCATRx.Core.SimpleTypeException
```
Exception thrown when a simple type is not supported.

##### Declared public members

###### `M:IoT.Driver.TwinCATRx.Core.SimpleTypeException.#ctor`

```csharp
public IoT.Driver.TwinCATRx.Core.SimpleTypeException()
```
Initializes a new instance of the `T:IoT.Driver.TwinCATRx.Core.SimpleTypeException` class.

###### `M:IoT.Driver.TwinCATRx.Core.SimpleTypeException.#ctor(System.String)`

```csharp
public IoT.Driver.TwinCATRx.Core.SimpleTypeException(string message)
```
Initializes a new instance of the `T:IoT.Driver.TwinCATRx.Core.SimpleTypeException` class.

- Parameter `message`: The message that describes the error.

###### `M:IoT.Driver.TwinCATRx.Core.SimpleTypeException.#ctor(System.String,System.Exception)`

```csharp
public IoT.Driver.TwinCATRx.Core.SimpleTypeException(string message, System.Exception innerException)
```
Initializes a new instance of the `T:IoT.Driver.TwinCATRx.Core.SimpleTypeException` class.

- Parameter `message`: The error message that explains the reason for the exception.
- Parameter `innerException`: The exception that caused this exception, or null when no inner exception is specified.

#### `T:IoT.Driver.TwinCATRx.Core.TwinCatRxExtensions`

```csharp
public class IoT.Driver.TwinCATRx.Core.TwinCatRxExtensions
```
Observable TwinCAT extensions.

##### Declared public members

###### `M:IoT.Driver.TwinCATRx.Core.TwinCatRxExtensions.AddAdsNotification(IoT.Driver.TwinCATRx.Core.ISettings,System.String)`

```csharp
public static void AddAdsNotification(IoT.Driver.TwinCATRx.Core.ISettings settings, string variableName)
```
Inherits XML documentation from its implemented or overridden member.

- Parameter `settings`: The `settings` value.
- Parameter `variableName`: The `variableName` value.

###### `M:IoT.Driver.TwinCATRx.Core.TwinCatRxExtensions.AddAdsNotification(IoT.Driver.TwinCATRx.Core.ISettings,System.String,System.Int32)`

```csharp
public static void AddAdsNotification(IoT.Driver.TwinCATRx.Core.ISettings settings, string variableName, int cycleTime)
```
Inherits XML documentation from its implemented or overridden member.

- Parameter `settings`: The `settings` value.
- Parameter `variableName`: The `variableName` value.
- Parameter `cycleTime`: The `cycleTime` value.

###### `M:IoT.Driver.TwinCATRx.Core.TwinCatRxExtensions.AddAdsNotification(IoT.Driver.TwinCATRx.Core.ISettings,System.String,System.Int32,System.Int32)`

```csharp
public static void AddAdsNotification(IoT.Driver.TwinCATRx.Core.ISettings settings, string variableName, int cycleTime, int maxDelay)
```
Inherits XML documentation from its implemented or overridden member.

- Parameter `settings`: The `settings` value.
- Parameter `variableName`: The `variableName` value.
- Parameter `cycleTime`: The `cycleTime` value.
- Parameter `maxDelay`: The `maxDelay` value.

###### `M:IoT.Driver.TwinCATRx.Core.TwinCatRxExtensions.AddAdsNotification(IoT.Driver.TwinCATRx.Core.ISettings,System.String,TwinCAT.Ads.AdsTransMode)`

```csharp
public static void AddAdsNotification(IoT.Driver.TwinCATRx.Core.ISettings settings, string variableName, TwinCAT.Ads.AdsTransMode adsTransMode)
```
Inherits XML documentation from its implemented or overridden member.

- Parameter `settings`: The `settings` value.
- Parameter `variableName`: The `variableName` value.
- Parameter `adsTransMode`: The `adsTransMode` value.

###### `M:IoT.Driver.TwinCATRx.Core.TwinCatRxExtensions.AddAdsNotification(IoT.Driver.TwinCATRx.Core.ISettings,System.String,TwinCAT.Ads.AdsTransMode,System.Int32)`

```csharp
public static void AddAdsNotification(IoT.Driver.TwinCATRx.Core.ISettings settings, string variableName, TwinCAT.Ads.AdsTransMode adsTransMode, int cycleTime)
```
Inherits XML documentation from its implemented or overridden member.

- Parameter `settings`: The `settings` value.
- Parameter `variableName`: The `variableName` value.
- Parameter `adsTransMode`: The `adsTransMode` value.
- Parameter `cycleTime`: The `cycleTime` value.

###### `M:IoT.Driver.TwinCATRx.Core.TwinCatRxExtensions.AddAdsNotification(IoT.Driver.TwinCATRx.Core.ISettings,System.String,TwinCAT.Ads.AdsTransMode,System.Int32,System.Int32)`

```csharp
public static void AddAdsNotification(IoT.Driver.TwinCATRx.Core.ISettings settings, string variableName, TwinCAT.Ads.AdsTransMode adsTransMode, int cycleTime, int maxDelay)
```
Inherits XML documentation from its implemented or overridden member.

- Parameter `settings`: The `settings` value.
- Parameter `variableName`: The `variableName` value.
- Parameter `adsTransMode`: The `adsTransMode` value.
- Parameter `cycleTime`: The `cycleTime` value.
- Parameter `maxDelay`: The `maxDelay` value.

###### `M:IoT.Driver.TwinCATRx.Core.TwinCatRxExtensions.AddNotification(IoT.Driver.TwinCATRx.Core.ISettings,System.String)`

```csharp
public static void AddNotification(IoT.Driver.TwinCATRx.Core.ISettings settings, string variableName)
```
Adds a notification variable to the settings.

- Parameter `settings`: The TwinCAT settings.
- Parameter `variableName`: The PLC variable name.

###### `M:IoT.Driver.TwinCATRx.Core.TwinCatRxExtensions.AddNotification(IoT.Driver.TwinCATRx.Core.ISettings,System.String,System.Int32)`

```csharp
public static void AddNotification(IoT.Driver.TwinCATRx.Core.ISettings settings, string variableName, int cycleTime)
```
Adds a notification variable to the settings.

- Parameter `settings`: The TwinCAT settings.
- Parameter `variableName`: The PLC variable name.
- Parameter `cycleTime`: The polling cycle time.

###### `M:IoT.Driver.TwinCATRx.Core.TwinCatRxExtensions.AddNotification(IoT.Driver.TwinCATRx.Core.ISettings,System.String,System.Int32,System.Int32)`

```csharp
public static void AddNotification(IoT.Driver.TwinCATRx.Core.ISettings settings, string variableName, int cycleTime, int arraySize)
```
Adds a notification variable to the settings.

- Parameter `settings`: The TwinCAT settings.
- Parameter `variableName`: The PLC variable name.
- Parameter `cycleTime`: The polling cycle time.
- Parameter `arraySize`: The array size.

###### `M:IoT.Driver.TwinCATRx.Core.TwinCatRxExtensions.AddWriteVariable(IoT.Driver.TwinCATRx.Core.ISettings,System.String)`

```csharp
public static void AddWriteVariable(IoT.Driver.TwinCATRx.Core.ISettings settings, string variableName)
```
Adds a write variable to the settings.

- Parameter `settings`: The TwinCAT settings.
- Parameter `variableName`: The PLC variable name.

###### `M:IoT.Driver.TwinCATRx.Core.TwinCatRxExtensions.AddWriteVariable(IoT.Driver.TwinCATRx.Core.ISettings,System.String,System.Int32)`

```csharp
public static void AddWriteVariable(IoT.Driver.TwinCATRx.Core.ISettings settings, string variableName, int arraySize)
```
Adds a write variable to the settings.

- Parameter `settings`: The TwinCAT settings.
- Parameter `variableName`: The PLC variable name.
- Parameter `arraySize`: The array size.

###### `M:IoT.Driver.TwinCATRx.Core.TwinCatRxExtensions.AdsStateChangedObserver(TwinCAT.Ads.AdsClient)`

```csharp
public static System.IObservable<TwinCAT.Ads.AdsStateChangedEventArgs> AdsStateChangedObserver(TwinCAT.Ads.AdsClient client)
```
Observes ADS state changed events.

- Parameter `client`: The ADS client.
- Returns: The ADS state changed observable sequence.

###### `M:IoT.Driver.TwinCATRx.Core.TwinCatRxExtensions.AdsStateObserver(TwinCAT.Ads.AdsClient)`

```csharp
public static System.IObservable<TwinCAT.Ads.StateInfo> AdsStateObserver(TwinCAT.Ads.AdsClient client)
```
Polls ADS state from the client.

- Parameter `client`: The ADS client.
- Returns: The ADS state observable sequence.

###### `M:IoT.Driver.TwinCATRx.Core.TwinCatRxExtensions.AssemblyLoad(System.String)`

```csharp
public static System.Reflection.Assembly AssemblyLoad(string dllFullName)
```
Loads an assembly from a DLL file path.

- Parameter `dllFullName`: The full DLL path.
- Returns: The loaded assembly.

###### `M:IoT.Driver.TwinCATRx.Core.TwinCatRxExtensions.GetType(System.String,System.String)`

```csharp
public static System.Type GetType(string dllFullName, string engineType)
```
Gets a type from an assembly file.

- Parameter `dllFullName`: The full DLL path.
- Parameter `engineType`: The type name.
- Returns: The resolved type.

###### `M:IoT.Driver.TwinCATRx.Core.TwinCatRxExtensions.OnErrorRetry``1(System.IObservable`1{``0})`

```csharp
public static System.IObservable<TSource> OnErrorRetry<TSource>(System.IObservable<TSource> source)
```
Executes the `OnErrorRetry` operation.

- Parameter `source`: The `source` value.
- Returns: A `System.IObservable<TSource>` result.

###### `M:IoT.Driver.TwinCATRx.Core.TwinCatRxExtensions.OnErrorRetry``2(System.IObservable`1{``0},System.Action`1{``1})`

```csharp
public static System.IObservable<TSource> OnErrorRetry<TSource, TException>(System.IObservable<TSource> source, System.Action<TException> onError)
```
Executes the `OnErrorRetry` operation.

- Parameter `source`: The `source` value.
- Parameter `onError`: The `onError` value.
- Returns: A `System.IObservable<TSource>` result.

###### `M:IoT.Driver.TwinCATRx.Core.TwinCatRxExtensions.OnErrorRetry``2(System.IObservable`1{``0},System.Action`1{``1},System.Int32)`

```csharp
public static System.IObservable<TSource> OnErrorRetry<TSource, TException>(System.IObservable<TSource> source, System.Action<TException> onError, int retryCount)
```
Executes the `OnErrorRetry` operation.

- Parameter `source`: The `source` value.
- Parameter `onError`: The `onError` value.
- Parameter `retryCount`: The `retryCount` value.
- Returns: A `System.IObservable<TSource>` result.

###### `M:IoT.Driver.TwinCATRx.Core.TwinCatRxExtensions.OnErrorRetry``2(System.IObservable`1{``0},System.Action`1{``1},System.Int32,System.TimeSpan)`

```csharp
public static System.IObservable<TSource> OnErrorRetry<TSource, TException>(System.IObservable<TSource> source, System.Action<TException> onError, int retryCount, System.TimeSpan delay)
```
Executes the `OnErrorRetry` operation.

- Parameter `source`: The `source` value.
- Parameter `onError`: The `onError` value.
- Parameter `retryCount`: The `retryCount` value.
- Parameter `delay`: The `delay` value.
- Returns: A `System.IObservable<TSource>` result.

###### `M:IoT.Driver.TwinCATRx.Core.TwinCatRxExtensions.OnErrorRetry``2(System.IObservable`1{``0},System.Action`1{``1},System.Int32,System.TimeSpan,ReactiveUI.Primitives.Concurrency.ISequencer)`

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

###### `M:IoT.Driver.TwinCATRx.Core.TwinCatRxExtensions.OnErrorRetry``2(System.IObservable`1{``0},System.Action`1{``1},System.TimeSpan)`

```csharp
public static System.IObservable<TSource> OnErrorRetry<TSource, TException>(System.IObservable<TSource> source, System.Action<TException> onError, System.TimeSpan delay)
```
Executes the `OnErrorRetry` operation.

- Parameter `source`: The `source` value.
- Parameter `onError`: The `onError` value.
- Parameter `delay`: The `delay` value.
- Returns: A `System.IObservable<TSource>` result.

#### `T:IoT.Driver.TwinCATRx.Core.TwinCatRxExtensions.<G>$ABBAA2A5A4FDFED3520D7A320DCB29E8`

```csharp
public class IoT.Driver.TwinCATRx.Core.TwinCatRxExtensions.<G>$ABBAA2A5A4FDFED3520D7A320DCB29E8
```
Public class `IoT.Driver.TwinCATRx.Core.TwinCatRxExtensions.<G>$ABBAA2A5A4FDFED3520D7A320DCB29E8`.

##### Declared public members

###### `M:IoT.Driver.TwinCATRx.Core.TwinCatRxExtensions.<G>$ABBAA2A5A4FDFED3520D7A320DCB29E8.AddAdsNotification(System.String)`

```csharp
public void AddAdsNotification(string variableName)
```
Adds an ADS notification with OnChange, a 100 ms cycle, and no delay.

- Parameter `variableName`: The PLC variable name.

###### `M:IoT.Driver.TwinCATRx.Core.TwinCatRxExtensions.<G>$ABBAA2A5A4FDFED3520D7A320DCB29E8.AddAdsNotification(System.String,System.Int32)`

```csharp
public void AddAdsNotification(string variableName, int cycleTime)
```
Adds an OnChange ADS notification with no delay.

- Parameter `variableName`: The PLC variable name.
- Parameter `cycleTime`: The cycle time in milliseconds.

###### `M:IoT.Driver.TwinCATRx.Core.TwinCatRxExtensions.<G>$ABBAA2A5A4FDFED3520D7A320DCB29E8.AddAdsNotification(System.String,System.Int32,System.Int32)`

```csharp
public void AddAdsNotification(string variableName, int cycleTime, int maxDelay)
```
Adds an OnChange ADS notification.

- Parameter `variableName`: The PLC variable name.
- Parameter `cycleTime`: The cycle time in milliseconds.
- Parameter `maxDelay`: The maximum delivery delay in milliseconds.

###### `M:IoT.Driver.TwinCATRx.Core.TwinCatRxExtensions.<G>$ABBAA2A5A4FDFED3520D7A320DCB29E8.AddAdsNotification(System.String,TwinCAT.Ads.AdsTransMode)`

```csharp
public void AddAdsNotification(string variableName, TwinCAT.Ads.AdsTransMode adsTransMode)
```
Adds an ADS notification with a 100 ms cycle and no delay.

- Parameter `variableName`: The PLC variable name.
- Parameter `adsTransMode`: The ADS transmission mode.

###### `M:IoT.Driver.TwinCATRx.Core.TwinCatRxExtensions.<G>$ABBAA2A5A4FDFED3520D7A320DCB29E8.AddAdsNotification(System.String,TwinCAT.Ads.AdsTransMode,System.Int32)`

```csharp
public void AddAdsNotification(string variableName, TwinCAT.Ads.AdsTransMode adsTransMode, int cycleTime)
```
Adds an ADS notification with no delay.

- Parameter `variableName`: The PLC variable name.
- Parameter `adsTransMode`: The ADS transmission mode.
- Parameter `cycleTime`: The cycle time in milliseconds.

###### `M:IoT.Driver.TwinCATRx.Core.TwinCatRxExtensions.<G>$ABBAA2A5A4FDFED3520D7A320DCB29E8.AddAdsNotification(System.String,TwinCAT.Ads.AdsTransMode,System.Int32,System.Int32)`

```csharp
public void AddAdsNotification(string variableName, TwinCAT.Ads.AdsTransMode adsTransMode, int cycleTime, int maxDelay)
```
Adds a variable whose values are delivered by ADS notifications.

- Parameter `variableName`: The PLC variable name.
- Parameter `adsTransMode`: The ADS transmission mode.
- Parameter `cycleTime`: The notification cycle time in milliseconds.
- Parameter `maxDelay`: The maximum delivery delay in milliseconds.

#### `T:IoT.Driver.TwinCATRx.Core.TwinCatRxExtensions.<G>$ABBAA2A5A4FDFED3520D7A320DCB29E8.<M>$C67FD8DCD80868A1236C5183CED0D70E`

```csharp
public class IoT.Driver.TwinCATRx.Core.TwinCatRxExtensions.<G>$ABBAA2A5A4FDFED3520D7A320DCB29E8.<M>$C67FD8DCD80868A1236C5183CED0D70E
```
Configures notifications delivered by ADS.

#### `T:IoT.Driver.TwinCATRx.Core.UnsuportedTypeException`

```csharp
public class IoT.Driver.TwinCATRx.Core.UnsuportedTypeException
```
Exception thrown when a simple type is not supported.

##### Declared public members

###### `M:IoT.Driver.TwinCATRx.Core.UnsuportedTypeException.#ctor`

```csharp
public IoT.Driver.TwinCATRx.Core.UnsuportedTypeException()
```
Initializes a new instance of the `T:IoT.Driver.TwinCATRx.Core.UnsuportedTypeException` class.

###### `M:IoT.Driver.TwinCATRx.Core.UnsuportedTypeException.#ctor(System.String)`

```csharp
public IoT.Driver.TwinCATRx.Core.UnsuportedTypeException(string message)
```
Initializes a new instance of the `T:IoT.Driver.TwinCATRx.Core.UnsuportedTypeException` class.

- Parameter `message`: The message that describes the error.

###### `M:IoT.Driver.TwinCATRx.Core.UnsuportedTypeException.#ctor(System.String,System.Exception)`

```csharp
public IoT.Driver.TwinCATRx.Core.UnsuportedTypeException(string message, System.Exception innerException)
```
Initializes a new instance of the `T:IoT.Driver.TwinCATRx.Core.UnsuportedTypeException` class.

- Parameter `message`: The error message that explains the reason for the exception.
- Parameter `innerException`: The exception that caused this exception, or null when no inner exception is specified.

<!-- END GENERATED PUBLIC API -->
