<p align="center">
  <img src="https://raw.githubusercontent.com/ChrisPulman/IoT-DriverCore/main/images/ab-plc-rx.png" alt="ABPlcRx package logo" width="320" />
</p>

# ABPlcRx

## Overview

`ABPlcRx` is a reactive Allen-Bradley PLC client over libplctag. It registers physical PLC tags under application-owned logical names, scans them, and exposes value changes through `IObservable<T>` and `IObservableAsync<T>`.

## Safety

PLC writes can move equipment. Validate tags against a non-production controller first, apply least-privilege network access, make writes deliberate, and ensure independent interlocks and emergency-stop logic remain effective. A successful library call is not proof that a machine state is safe.

## Package matrix

| Package | Default namespace | Targets | Purpose |
|---|---|---|---|
| `ABPlcRx` | `IoT.DriverCore.ABPlcRx` | net472, net48, net481, net8.0–net11.0 | libplctag-backed reactive AB client and source generator |
| `ABPlcRx.Reactive` | `IoT.DriverCore.ABPlcRx.Reactive` | matching package targets | System.Reactive-compatible surface where supplied |
| `ABPlcRx.Generators` | `IoT.DriverCore.ABPlcRx.SourceGenerators` | netstandard2.0 analyzer | Standalone analyzer package for generated PLC models; the runtime packages also embed their matching analyzer |

`ABPlcRx` references `ReactiveUI.Primitives`, `ReactiveUI.Primitives.Async`, and `libplctag.NativeImport`. Its source-generator analyzer is packed with the package.

## Install

```bash
dotnet add package ABPlcRx
```

Use `IoT.DriverCore.ABPlcRx`; add `ReactiveUI.Primitives.Extensions` only when you want its subscription helpers.

## Quick start

The generic APIs deliberately take a type witness. This makes the PLC representation explicit and is valid C# for current packages.

```csharp
using IoT.DriverCore.ABPlcRx;

using var plc = new ABPlcRx(PlcType.LGX, "192.168.1.60", TimeSpan.FromMilliseconds(200));
plc.AddUpdateTagItem("Counter", "MyDINT", "Default", 0);

using var changes = plc.Observe("Counter", 0, -1)
    .Subscribe(value => Console.WriteLine($"Counter = {value}"));

plc.Value("Counter", 42, -1); // AutoWriteValue is true by default.
var result = plc.Read("Counter");
if (result is not null && PlcTagStatus.IsError(result.StatusCode))
    Console.Error.WriteLine(PlcTagStatus.DecodeError(result.StatusCode));
```

For an SLC/PLC-5 word bit, register the word as `short` and give a bit index from 0 through 15:

```csharp
using var plc = new ABPlcRx(PlcType.SLC, "192.168.1.50", TimeSpan.FromMilliseconds(500));
plc.AddUpdateTagItem("LightWord", "B3:3", "Outputs", (short)0);
plc.Value("LightWord", true, 0);
bool? lightOn = plc.GetValue("LightWord", false, 0);
```

## Configuration

Construct with `(PlcType, ip, scanInterval)` or `(PlcType, ip, scanInterval, timeout, path)`. `PlcType` is `LGX`, `SLC`, or `PLC5`; Logix routing uses `path` such as `"1,0"` when required by the controller topology.

Set `ScanEnabled` to enable or disable scans for registered groups. Set `AutoWriteValue = false` to stage values with `Value` and commit with `Write(variable)` or `Write()`. Use logical variables consistently; they are the keys used by read, write, and observation methods.

## Detailed features

### Batching, health, and async reads

```csharp
var reads = await plc.ReadManyAsync(["Counter"], CancellationToken.None);
var writes = await plc.WriteManyAsync(
    new Dictionary<string, object?> { ["Counter"] = 43 }, CancellationToken.None);

using var health = plc.ObservePing(TimeSpan.FromSeconds(2), false, null)
    .Subscribe(connected => Console.WriteLine($"PLC reachable: {connected}"));

var typed = await plc.ReadValueAsync("Counter", 0, -1, CancellationToken.None);
```

`ObserveMany` publishes a latest-value dictionary; `ObserveGroup` publishes changed `IPlcTag` instances in one group; `ObserveSampled` bounds a consumer's update rate. Each has an `IObservableAsync` counterpart named `...AsyncObservable`.

### Deterministic simulation

Use `ABPlcSimulator` for tests and integration harnesses without hardware. It shares the production facade and records native-style operations.

```csharp
using var simulator = new ABPlcSimulator(PlcType.LGX);
simulator.AddUpdateTagItem("Counter", "MyDINT", "Default", 0);
simulator.SetTagValue("MyDINT", 7);
simulator.Read("Counter");
Console.WriteLine(simulator.GetValue("Counter", 0, -1));
```

### Generated stream models

The packaged analyzer recognizes the AB PLC model/tag attributes. Keep the model partial and use the generated binding method; inspect generated output when changing attribute names or types. The analyzer is a compile-time convenience, not a substitute for validating physical tag names.

## Exhaustive feature guide and worked workflows

The following sections supersede examples written for the pre-migration namespace. All examples use the current `IoT.DriverCore.ABPlcRx` surface and deliberately include ownership, cancellation, and result handling.

### Registering tags, addressing bits, and choosing a value type

`AddUpdateTagItem<T>` has three overload families. `(tagName, typeWitness)` makes the logical variable equal to the controller tag and uses the `Default` group. `(variable, tagName, typeWitness)` separates the application name from the physical address. `(variable, tagName, group, typeWitness)` also assigns a group for observation and bulk I/O. `typeWitness` is a value solely used to select `T`; use `0`, `false`, `0f`, or `string.Empty` rather than relying on inference.

For native Logix `BOOL`, register `bool` and pass bit `-1`. For a bit in an SLC/PLC-5 integral word, register the *word* as `short`, `ushort`, `int`, `uint`, `long`, or `ulong`, then use an in-range bit index. A bit index with `bool` is invalid because the physical tag is already one Boolean. Read/write conversion errors and native operation failures appear in `PlcTagResult` / `TagOperationResult<T>` and on `ObserveErrors`.

```csharp
using IoT.DriverCore.ABPlcRx;

using var plc = new ABPlcRx(PlcType.LGX, "192.168.1.60", TimeSpan.FromMilliseconds(250),
    TimeSpan.FromSeconds(2), "1,0");

plc.AddUpdateTagItem("RunCommand", "Program:Line.RunCmd", "Commands", false);
plc.AddUpdateTagItem("SpeedSetpoint", "Program:Line.Speed", "Commands", 0f);
plc.AddUpdateTagItem("AlarmWord", "N7:10", "Alarms", (short)0);

// Native BOOL: bit -1. Integral SLC word: read/write bit 4.
bool? run = plc.GetValue("RunCommand", false, -1);
bool? alarm4 = plc.GetValue("AlarmWord", false, 4);
if (alarm4 is true)
    plc.Value("AlarmWord", false, 4);
```

### Cached values, synchronous I/O, and write staging

`GetValue<T>` returns the most recently cached value; it does not force a transaction. `Value<T>(variable, value, bit)` changes the cached value. With `AutoWriteValue = true` (the default), it then writes immediately; otherwise it stages the change for `Write(variable)` or `Write()`. `Read(variable)` and `Write(variable)` return a nullable result when the variable does not exist. `Read()` and `Write()` return one result per registered tag. Check `StatusCode` with `PlcTagStatus.IsError` and `DecodeError`; do not infer success from a non-null result.

```csharp
plc.AutoWriteValue = false;
plc.Value("SpeedSetpoint", 750f, -1); // cache/stage only

PlcTagResult? committed = plc.Write("SpeedSetpoint");
if (committed is null || PlcTagStatus.IsError(committed.StatusCode))
    throw new InvalidOperationException(committed is null
        ? "SpeedSetpoint is not registered."
        : PlcTagStatus.DecodeError(committed.StatusCode));

PlcTagResult? refreshed = plc.Read("SpeedSetpoint");
Console.WriteLine($"PLC speed = {plc.GetValue("SpeedSetpoint", 0f, -1)}");
```

### Cancelable, typed and batched I/O

Use `ReadValueAsync<T>` and `WriteValueAsync<T>` when a command must have a completion boundary. They return `TagOperationResult<T>` rather than throwing expected PLC status failures; inspect its success/status/value members before acting. `ReadManyAsync` accepts selected logical variables and returns a result for each. `WriteManyAsync` accepts logical-name/value pairs and writes them with the same cancellation boundary. Cancellation stops awaiting the operation; always dispose the facade once its subscriptions and commands are finished.

```csharp
using var shutdown = new CancellationTokenSource(TimeSpan.FromSeconds(3));
var ct = shutdown.Token;

var read = await plc.ReadValueAsync("SpeedSetpoint", 0f, -1, ct);
if (!read.Succeeded)
    Console.Error.WriteLine($"Read failed: {read.Error}");
else if (read.Value is float speed && speed < 1200)
{
    var write = await plc.WriteValueAsync("SpeedSetpoint", speed + 25, -1, ct);
    if (!write.Succeeded) Console.Error.WriteLine(write.Error);
}

var results = await plc.WriteManyAsync(new Dictionary<string, object?>
{
    ["RunCommand"] = true,
    ["SpeedSetpoint"] = 800f,
}, ct);
foreach (var result in results.Where(r => PlcTagStatus.IsError(r.StatusCode)))
    Console.Error.WriteLine(PlcTagStatus.DecodeError(result.StatusCode));
```

### Reactive, async-reactive, group, many, and sampled observation

`Observe<T>` emits the registered variable's current/change values while its subscription lives. `ObserveAll` emits each changed `IPlcTag`; `ObserveGroup` limits that to a registration group. `ObserveMany` emits a latest-value dictionary for the requested names. Their `...AsyncObservable` counterparts expose `IObservableAsync<T>` for `ReactiveUI.Primitives.Async` pipelines. `ObserveSampled<T>` and `ObserveSampledAsyncObservable<T>` keep the latest value at the supplied cadence, which is appropriate for UI/telemetry sinks but not safety interlocks. All sequences are cold from the caller's perspective: retain and dispose every returned subscription.

```csharp
using ReactiveUI.Primitives.Disposables;

using var subscriptions = new CompositeDisposable();
subscriptions.Add(plc.Observe("RunCommand", false, -1).Subscribe(
    value => Console.WriteLine($"Run={value}"),
    error => Console.Error.WriteLine(error)));
subscriptions.Add(plc.ObserveGroup("Alarms").Subscribe(tag =>
    Console.WriteLine($"{tag.Variable}: {tag.Value}")));
subscriptions.Add(plc.ObserveMany("RunCommand", "SpeedSetpoint").Subscribe(values =>
    Console.WriteLine($"Snapshot: {values["RunCommand"]}, {values["SpeedSetpoint"]}")));
subscriptions.Add(plc.ObserveSampled("SpeedSetpoint", TimeSpan.FromSeconds(1), 0f, -1, null)
    .Subscribe(speed => PublishTelemetry(speed)));
subscriptions.Add(plc.ObserveErrors().Subscribe(result =>
    Console.Error.WriteLine(PlcTagStatus.DecodeError(result.StatusCode))));

static void PublishTelemetry(float? speed) => Console.WriteLine($"telemetry={speed}");
```

`CreateWriter<T>(variable, typeWitness, bit)` is an `IObserver<T>` adapter: each `OnNext` calls the same value/write path, therefore write failures should also be observed through `ObserveErrors`. It is useful at the boundary of a validated command stream, but never connect an unbounded sensor stream directly to a PLC write.

```csharp
IObserver<bool> commandWriter = plc.CreateWriter("RunCommand", false, -1);
using var commandErrors = plc.ObserveErrors().Subscribe(e =>
    Console.Error.WriteLine($"Command failed: {PlcTagStatus.DecodeError(e.StatusCode)}"));
commandWriter.OnNext(true);
```

### Consuming `IObservableAsync<T>` streams

Every `Observe*AsyncObservable` member is an `IObservableAsync<T>`, not an `IAsyncEnumerable<T>`. Subscribe with an `IObserverAsync<T>` and dispose the returned `IAsyncDisposable`; the cancellation token is checked when the subscription is created and is also supplied to value/error callbacks. The following observer is deliberately small but complete, and can be reused for values, errors, groups, snapshots, and ping state.

```csharp
using IoT.DriverCore.ABPlcRx;
using ReactiveUI.Primitives;
using ReactiveUI.Primitives.Async;

using var cancellation = new CancellationTokenSource(TimeSpan.FromMinutes(1));
var observer = new ConsoleAsyncObserver<float?>();
await using var subscription = await plc
    .ObserveSampledAsyncObservable("SpeedSetpoint", TimeSpan.FromSeconds(1), 0f, -1, null)
    .SubscribeAsync(observer, cancellation.Token);

// Keep the subscription alive for the required application lifetime. On shutdown,
// cancel the owner and let await using dispose the subscription.

sealed class ConsoleAsyncObserver<T> : IObserverAsync<T>
{
    public ValueTask OnNextAsync(T value, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        Console.WriteLine(value);
        return default;
    }

    public ValueTask OnErrorResumeAsync(Exception error, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        Console.Error.WriteLine(error);
        return default;
    }

    public ValueTask OnCompletedAsync(Result result)
    {
        Console.WriteLine($"Completed: {result}");
        return default;
    }

    public ValueTask DisposeAsync() => default;
}
```

Use the same ownership pattern for all async-native members. For example, a group subscription has `IPlcTag` values, and a snapshot subscription has `IReadOnlyDictionary<string, object?>` values:

```csharp
var groupObserver = new ConsoleAsyncObserver<IPlcTag>();
var snapshotObserver = new ConsoleAsyncObserver<IReadOnlyDictionary<string, object?>>();
await using var groupSubscription = await plc
    .ObserveGroupAsyncObservable("Alarms")
    .SubscribeAsync(groupObserver, CancellationToken.None);
await using var snapshotSubscription = await plc
    .ObserveManyAsyncObservable("RunCommand", "SpeedSetpoint")
    .SubscribeAsync(snapshotObserver, CancellationToken.None);
```

Do not use `await foreach` directly on these members. `ABLogicalTagClient.ObserveAsync` and `ObserveManyAsync` are the distinct APIs that intentionally expose `IAsyncEnumerable<LogicalTagValue>`.

### Logical-tag catalog, access policy, CSV, and SQLite persistence

`ABLogicalTagClient` composes an `IABPlcRx`; it does not own or dispose that controller. It maps a stable application tag name to an AB address and a supported data type (`bool`, integral types, `float`, `double`, or `string`), then validates the tag's `LogicalTagAccessMode` before I/O. A Boolean logical tag can describe a bit in an integral physical word by placing the zero-based bit index in `LogicalTagOptions.Metadata` under the key `"Bit"`.

```csharp
using IoT.DriverCore.ABPlcRx;
using IoT.DriverCore.Core;

using var simulator = new ABPlcSimulator(PlcType.LGX);
using var tags = simulator.CreateLogicalTagClient();

tags.CreateTag(new LogicalTag(
    "PumpSpeed",
    "Program:Pump.Speed",
    "REAL",
    new LogicalTagOptions
    {
        GroupName = "Pumps",
        Description = "Pump speed feedback",
        AccessMode = LogicalTagAccessMode.Read,
        ScanInterval = TimeSpan.FromMilliseconds(500),
    }));
tags.CreateTag(new LogicalTag(
    "PumpRunning",
    "N7:10",
    "bool",
    new LogicalTagOptions
    {
        GroupName = "Pumps",
        Metadata = new Dictionary<string, string> { ["Bit"] = "3" },
    }));

simulator.SetTagValue("Program:Pump.Speed", 123.5f);
simulator.SetTagValue("N7:10", (short)8);

var speed = await tags.ReadAsync("PumpSpeed", CancellationToken.None);
var running = await tags.ReadAsync("PumpRunning", CancellationToken.None);
if (speed.Succeeded && running.Succeeded)
    Console.WriteLine($"speed={speed.Value!.Value}, running={running.Value!.Value}");
```

The logical client produces application-oriented `TagOperationResult<LogicalTagValue>` outcomes. It rejects absent tags, denied access, unsupported type declarations, duplicate bulk names, and a non-Boolean value supplied to a bit projection. Check `Succeeded` before using `Value`; bulk results retain the input order.

```csharp
var writes = await tags.WriteManyAsync(
[
    new LogicalTagValue("PumpRunning", false, TimeProvider.System.GetUtcNow()),
],
CancellationToken.None);

foreach (var write in writes)
{
    if (!write.Succeeded)
        Console.Error.WriteLine(write.Error);
}

using var logicalObservation = tags.Observe("PumpRunning").Subscribe(value =>
    Console.WriteLine($"{value.TagName}={value.Value}; quality={value.Quality}"));

using var streamCancellation = new CancellationTokenSource(TimeSpan.FromSeconds(5));
await using var enumerator = tags
    .ObserveManyAsync(["PumpSpeed", "PumpRunning"], streamCancellation.Token)
    .GetAsyncEnumerator();
simulator.Read("PumpSpeed");
simulator.Read("PumpRunning");
if (await enumerator.MoveNextAsync())
    Console.WriteLine($"Changed: {enumerator.Current.TagName}");
```

For persistent definitions, construct `LogicalTagSqliteStore`, initialise it through the client, then use the client CRUD methods. `UpsertTagAsync`, `EditTagAsync`, and `DeleteTagAsync` synchronise the running catalog; `UpsertGroupAsync` and `DeleteGroupAsync` persist group metadata. `LoadTagsAsync` replaces live registrations with the definitions in the store, so coordinate it with active I/O.

```csharp
using IoT.DriverCore.Core;

var database = Path.Combine(AppContext.BaseDirectory, "ab-logical-tags.db");
var store = new LogicalTagSqliteStore($"Data Source={database}");
using var persistentTags = new ABLogicalTagClient(simulator, new LogicalTagCatalog(), store);

await persistentTags.InitializeStoreAsync(CancellationToken.None);
await persistentTags.UpsertGroupAsync(
    new LogicalTagGroup("Pumps", "Pump controls"), CancellationToken.None);
await persistentTags.UpsertTagAsync(
    new LogicalTag("PumpCommand", "Program:Pump.Command", "bool",
        new LogicalTagOptions { GroupName = "Pumps" }),
    CancellationToken.None);

LogicalTag? stored = await persistentTags.GetTagAsync("PumpCommand", CancellationToken.None);
bool changed = await persistentTags.EditTagAsync(
    stored!.WithAddress("Program:Pump.CommandNew"), CancellationToken.None);
bool deleted = await persistentTags.DeleteTagAsync("PumpCommand", CancellationToken.None);
Console.WriteLine($"edited={changed}, deleted={deleted}");
```

CSV import/export affects the in-memory catalog. Import validates definitions and registers them; export writes the current catalog. Use a `TextReader`/`TextWriter` you own and keep its lifetime outside the asynchronous call.

```csharp
using var input = new StringReader(
    "Name,Address,DataType,GroupName,Description,Metadata,AccessMode,ScanIntervalMilliseconds\n" +
    "TankLevel,Program:Tank.Level,REAL,Tanks,Tank level,,ReadWrite,500\n");
IReadOnlyList<LogicalTag> imported = await tags.ImportCsvAsync(input, CancellationToken.None);

using var output = new StringWriter();
await tags.ExportCsvAsync(output, CancellationToken.None);
Console.WriteLine(output.ToString());
```

### Simulator fault scripts, disconnection, and recovery

`ABPlcSimulator` uses the production facade over deterministic in-memory native storage. `QueueFault` affects future matching native operations, optionally only for one physical tag; `ClearFaults` removes unconsumed scripted results. `Disconnect` makes subsequent I/O report the supplied non-success status until `Reconnect`, without losing memory or registered tags. `ConnectionChanged`, `OperationLog`, `OperationMetrics`, `TagStatuses`, and `ActiveHandleCount` make behavior observable in a test or local integration harness.

```csharp
using IoT.DriverCore.ABPlcRx;

using var simulator = new ABPlcSimulator(PlcType.LGX);
simulator.AddUpdateTagItem("Counter", "Program:Counter", "Test", 0);
simulator.SetTagValue("Program:Counter", 7);

using var connectivity = simulator.ConnectionChanged.Subscribe(online =>
    Console.WriteLine(online ? "connected" : "disconnected"));

simulator.QueueFault(
    ABPlcSimulatorOperation.Read,
    PlcTagStatus.ErrRead,
    repeatCount: 1,
    tagName: "Program:Counter");
PlcTagResult? scriptedFailure = simulator.Read("Counter");
Console.WriteLine(PlcTagStatus.DecodeError(scriptedFailure!.StatusCode));

simulator.Disconnect();
PlcTagResult? disconnected = simulator.Read("Counter");
if (PlcTagStatus.IsError(disconnected!.StatusCode))
    Console.WriteLine("I/O is deliberately unavailable while disconnected.");

simulator.Reconnect();
PlcTagResult? recovered = simulator.Read("Counter");
Console.WriteLine($"Recovered: {!PlcTagStatus.IsError(recovered!.StatusCode)}");

Console.WriteLine($"reads={simulator.OperationMetrics.ReadOperations}; " +
                  $"failures={simulator.OperationMetrics.FailedOperations}");
simulator.ClearFaults();
simulator.ClearOperationLog();
```

Seed scalars with `SetTagValue<T>` or, for a structured fixture, raw bytes with the simulator-only `SetTagBytes`; inspect them with the corresponding getters. `CreateLogicalTagClient` is particularly useful for running the catalog examples above against the same deterministic memory. The simulator throws `ObjectDisposedException` for operations after disposal, so dispose logical clients/subscriptions first, then the simulator.

```csharp
using var simulator = new ABPlcSimulator(PlcType.LGX);
simulator.SetTagBytes("Program:Recipe", new byte[] { 0x34, 0x12, 0x00, 0x00 });
byte[] fixture = simulator.GetTagBytes("Program:Recipe");
if (!fixture.AsSpan().SequenceEqual(new byte[] { 0x34, 0x12, 0x00, 0x00 }))
    throw new InvalidOperationException("Simulator fixture was not retained.");
```

### Low-level `IPlcTag`, typed tags, result reduction, and value helpers

Use `IABPlcRx` for ordinary code. The lower-level surface is for a tested adapter that needs a tag's native-style handle, raw value layout, or explicit `Lock`/`Unlock` boundary. `ObserveAll` is the public route to an `IPlcTag`; do not retain a tag after its containing controller is disposed. `IPlcTag.ValueManager` is a `PlcTagWrapper` with typed getters/setters, bit conversion, and fixed-format string support. It does not automatically write after a `Set*` call: call `IPlcTag.Write()` and check its result.

```csharp
IPlcTag? latest = null;
using var all = plc.ObserveAll.Subscribe(tag => latest = tag);
_ = plc.Read("AlarmWord");

if (latest is not null)
{
    int lockStatus = latest.Lock();
    try
    {
        if (!PlcTagStatus.IsError(lockStatus))
        {
            short word = latest.ValueManager.GetInt16(0);
            latest.ValueManager.SetInt16(TagMixins.SetBit(word, 4, true), 0);
            PlcTagResult write = latest.Write();
            if (PlcTagStatus.IsError(write.StatusCode))
                Console.Error.WriteLine(PlcTagStatus.DecodeError(write.StatusCode));
        }
    }
    finally
    {
        if (!PlcTagStatus.IsError(lockStatus))
            _ = latest.Unlock();
    }
}
```

`IPlcTag<T>` adds a typed `Value`; `Changed` emits `PlcTagResult` for that individual tag. `GetStatus`, `GetSize`, and `Abort` return libplctag-compatible status/data. `PlcTagResult.Reduce` combines a non-empty set into its earliest timestamp, summed execution time, and worst status; it is useful for reporting a known batch, not for making a multi-write transactional. Tags are created and released by the controller facade rather than by application code.

```csharp
var resultSet = plc.Read().ToArray();
if (resultSet.Length != 0)
{
    PlcTagResult summary = PlcTagResult.Reduce(resultSet);
    Console.WriteLine($"status={summary.StatusCode}; elapsed={summary.ExecutionTime}ms");
}

// A typed tag can be obtained from the public all-tag stream after registration/read.
plc.AddUpdateTagItem("AlarmWord", "Program:AlarmWord", (short)0);
IPlcTag<short>? alarmWord = null;
using var tagChanges = plc.ObserveAll.Subscribe(tag =>
{
    if (tag.Variable == "AlarmWord" && tag is IPlcTag<short> typed)
        alarmWord = typed;
});
_ = plc.Read("AlarmWord");
if (alarmWord is not null)
{
    using var changed = alarmWord.Changed.Subscribe(result =>
        Console.WriteLine($"AlarmWord status={result.StatusCode}"));
    TagMixins.SetBit(alarmWord, bit: 4, value: true); // writes the typed tag
}

// These pure helpers are suitable for validation and diagnostics before I/O.
var bits = TagHelper.NumberToBits(0b_0001_0010);
int roundTrip = TagHelper.BitsToNumber(bits);
Console.WriteLine($"bit 4 = {TagMixins.GetBit((short)roundTrip, 4)}");

if (latest?.Value is double)
{
    // The tag's cached value must be a numeric value within this raw range.
    double engineeringUnits = TagHelper.ScaleLinear(latest, 0, 32_767, 0, 100);
    double squareRootUnits = TagMixins.ScaleSquareRoot(latest, 0, 32_767, 0, 100);
    Console.WriteLine($"linear={engineeringUnits:F1}, sqrt={squareRootUnits:F1}");
}
```

For engineering units, `TagHelper.ScaleLinear` / `ScaleSquareRoot` and the corresponding `TagMixins` extensions operate on an `IPlcTag` whose `Value` is numerically convertible to `double`. Confirm raw limits and scaling values on a non-production controller before treating a scaled result as a command or safety input.

### Health monitoring and lifecycle

`Ping(echo)` returns whether the controller is reachable. `PingAsync(echo, token)` is the cancelable variant. `ObservePing(interval, echo, scheduler)` emits deduplicated state changes; its async counterpart is `ObservePingAsyncObservable`. `ScanEnabled` gates background scanning, so turn it off only for controlled maintenance. `Dispose` disposes the native controller and does not dispose subscriptions that you own; dispose those first. `IsDisposed` is a diagnostic state only.

```csharp
using var online = plc.ObservePing(TimeSpan.FromSeconds(2), false, null)
    .Subscribe(isOnline => Console.WriteLine(isOnline ? "PLC online" : "PLC offline"));

if (!await plc.PingAsync(false, CancellationToken.None))
    Console.Error.WriteLine("Do not issue production commands while the PLC is unreachable.");
```

### Combined workflow 1: guarded command with health, sampled feedback, and explicit completion

This composition waits for the health gate, limits feedback publication, writes only with an awaited command, and releases all subscriptions before the client.

```csharp
using var lifetime = new CancellationTokenSource(TimeSpan.FromSeconds(10));
var ct = lifetime.Token;
using var health = plc.ObservePing(TimeSpan.FromSeconds(1), false, null)
    .Subscribe(online => Console.WriteLine($"online={online}"));
using var feedback = plc.ObserveSampled("SpeedSetpoint", TimeSpan.FromMilliseconds(500), 0f, -1, null)
    .Subscribe(value => Console.WriteLine($"feedback={value}"));

if (await plc.PingAsync(false, ct))
{
    var result = await plc.WriteValueAsync("RunCommand", true, -1, ct);
    if (!result.Succeeded) throw new InvalidOperationException(result.Error);
}
```

### Combined workflow 2: staged multi-tag recipe with group visibility and rollback decision

Staging gives the application an opportunity to validate all command values before any commit. It is not transactional at the PLC: record successful results and implement a device-specific compensation/stop policy for partial failure.

```csharp
plc.AutoWriteValue = false;
using var commandAudit = plc.ObserveGroup("Commands").Subscribe(tag =>
    Console.WriteLine($"Changed {tag.Variable} to {tag.Value}"));

plc.Value("SpeedSetpoint", 650f, -1);
plc.Value("RunCommand", true, -1);
var recipeResults = plc.Write().ToArray();
if (recipeResults.Any(r => PlcTagStatus.IsError(r.StatusCode)))
{
    plc.Value("RunCommand", false, -1);
    plc.Write("RunCommand");
    foreach (var failure in recipeResults.Where(r => PlcTagStatus.IsError(r.StatusCode)))
        Console.Error.WriteLine(PlcTagStatus.DecodeError(failure.StatusCode));
}
```

### Generated model workflow

The analyzer is included in the runtime package and also available as `ABPlcRx.Generators`. Apply `PlcModelAttribute` and one or more `PlcTagAttribute`s to a partial class. Each attribute describes the generated property name, controller tag/address, optional group, optional bit, and CLR type. `AttachPlcStreams(IABPlcRx)` registers and subscribes; `DetachPlcStreams()` releases generated subscriptions. The generator produces a current-value property, `<Property>Observable`, and the async observable member where supported by the target. Use an integral registration for a Boolean `Bit` attribute.

```csharp
using IoT.DriverCore.ABPlcRx;
using IoT.DriverCore.ABPlcRx.SourceGeneration;

[PlcModel]
[PlcTag(typeof(float), "Speed", "Program:Line.Speed", Group = "Motion")]
[PlcTag(typeof(bool), "Alarm", "N7:10", Group = "Alarms", Bit = 4)]
public partial class LineTags;

var model = new LineTags();
using var generatedBinding = model.AttachPlcStreams(plc);
using var speed = model.SpeedObservable.Subscribe(Console.WriteLine);
using var alarm = model.AlarmObservable.Subscribe(value =>
{
    if (value) Console.Error.WriteLine("Alarm bit is set");
});
// At shutdown: dispose subscriptions/binding, then dispose plc.
```

The reactive package has the same contracts under `IoT.DriverCore.ABPlcRx.Reactive`; choose that package only when the application uses its System.Reactive-compatible dependency stack. Never reference both runtime surfaces in the same project.

## Complete public API reference

The primary contract is `IABPlcRx`, implemented by `ABPlcRx` and `ABPlcSimulator`:

- Lifecycle/state: `Dispose`, `IsDisposed`, `ScanEnabled`, `AutoWriteValue`, `ObserveAll`, `ObserveAllAsyncObservable`.
- Registration: `AddUpdateTagItem<T>(tagName, typeWitness)`, `(variable, tagName, typeWitness)`, and `(variable, tagName, tagGroup, typeWitness)`; `RemoveTagItem`.
- Values and streams: `GetValue<T>`, `Value<T>`, `Observe<T>`, `ObserveAsyncObservable<T>`, `ObserveMany`, `ObserveManyAsyncObservable`, `ObserveGroup`, `ObserveGroupAsyncObservable`, `ObserveSampled<T>`, `ObserveSampledAsyncObservable<T>`, and `CreateWriter<T>`.
- I/O: `Read`, `Read(variable)`, `Write`, `Write(variable)`, `ReadManyAsync`, `WriteManyAsync`, `ReadValueAsync<T>`, and `WriteValueAsync<T>`.
- Diagnostics: `ObserveErrors`, `ObserveErrorsAsyncObservable`, `Ping`, `PingAsync`, `ObservePing`, and `ObservePingAsyncObservable`.
- Core types: `PlcType`, `IPlcTag`, `IPlcTag<T>`, `PlcTagResult`, `PlcTagException`, `PlcTagStatus`, `PlcTagWrapper`, `TagHelper`, and `TagMixins`. `PlcTagStatus.IsError` and `DecodeError` translate libplctag statuses.
- Simulation: `ABPlcSimulator`, `ABPlcSimulatorOperation`, `ABPlcSimulatorOperationMetrics`, and `ABPlcSimulatorLogEntry`; simulator-only members include connection control, raw/scalar tag seeding, fault queueing, operation log/metrics, and `CreateLogicalTagClient`.
- Logical tags: `ABLogicalTagClient` supports catalog registration, read/write one or many, observable and async enumeration, CSV import/export, and persistent tag/group CRUD.
- Generation and bridges: `PlcModelAttribute`, `PlcTagAttribute`, and the `PlcModelGenerator` analyzer define generated model bindings; `ObservableAsyncBridgeExtensions` supplies the public observable-to-async-observable bridge used by generated and handwritten code.

### Member-by-member contract

| Member family | Inputs and return | Failure, state, and ownership |
| --- | --- | --- |
| Constructors | `PlcType`, host, scan interval; optionally timeout and LGX path. | Construction does not make a successful safety/connection guarantee. Dispose the created facade. |
| Registration | `AddUpdateTagItem<T>` overloads and `RemoveTagItem`. | Duplicate variable replaces/updates the registration; unknown remove returns `false`. A variable/type mismatch is a caller error. |
| Value access | `GetValue<T>` reads cache; `Value<T>` stages or writes; `Read`/`Write` return `PlcTagResult`. | Cache may be stale until read/scan. Check every returned status; `AutoWriteValue` decides whether `Value` performs I/O. |
| Async I/O | `ReadValueAsync<T>`, `WriteValueAsync<T>`, `ReadManyAsync`, `WriteManyAsync`. | Pass a token for deadline/cancellation; inspect `TagOperationResult<T>` or each `PlcTagResult`, then dispose the client. |
| Streams | `Observe`, `ObserveAll`, `ObserveMany`, `ObserveGroup`, `ObserveSampled`, writer factory, plus async-observable mirrors. | Each subscription is independently owned. Dispose it; stream errors/results do not make a PLC command safe. |
| Diagnostics | `ObserveErrors`, `Ping`, `PingAsync`, `ObservePing`, plus async mirrors. | Health is reachability only. Decode native statuses and retain operational logs/audit records. |
| Logical/simulation helpers | `ABLogicalTagClient`, `ABPlcSimulator`, operation records/metrics, `TagHelper`, `TagMixins`, tags/results/status. | Use simulators for deterministic tests; logical tags provide catalog/batch convenience but not PLC transactions. |

### Supporting public member index

These helpers are public because they support diagnostics, catalog administration, simulator test setup, and low-level libplctag mappings. They are normally composed through `IABPlcRx`, `ABLogicalTagClient`, or `ABPlcSimulator`; direct low-level use should remain behind a tested adapter.

| Feature | Members and use |
| --- | --- |
| Logical-tag catalog and persistence | `CreateTag`, `RegisterTag`, `RemoveTag`, `ReadAsync`, `ReadManyAsync`, `WriteAsync`, `WriteManyAsync`, `ObserveAsync`, `ObserveManyAsync`, `InitializeStoreAsync`, `LoadTagsAsync`, `GetTagAsync`, `ListTagsAsync`, `EditTagAsync`, `DeleteTagAsync`, `UpsertTagAsync`, `GetGroupAsync`, `ListGroupsAsync`, `UpsertGroupAsync`, `DeleteGroupAsync`, `ImportCsvAsync`, `ExportCsvAsync`. All async members take a cancellation token; inspect `TagOperationResult<T>` before using its payload. |
| Simulator connection/fault/audit | `Disconnect`, `Reconnect`, `QueueFault`, `ClearFaults`, `ClearOperationLog`, `CreateLogicalTagClient`, and `Operations`/metrics properties. Use a queued failure to test status/error paths, then clear/reset before the next scenario. |
| Public tag and wrapper access | `IPlcTag.Abort`, `Lock`, `Unlock`, `GetStatus`, `GetSize`, `Read`, and `Write`, plus `PlcTagWrapper` `Get*`/`Set*` scalar/string members and `GetType`/`SetType` for object layouts. Tags are supplied by `ObserveAll`; `GetType`/`SetType` belong to `PlcTagWrapper`, not `IPlcTag`. Raw `GetTagBytes`/`SetTagBytes` are simulator-only fixture helpers. |
| Bit/scaling utilities | `GetBit`, `SetBit`, `GetBits`, `SetBits`, `GetBitsArray`, `GetBitsString`, `NumberToBits`, `BitsToNumber`, `ScaleLinear`, `ScaleSquareRoot`, `Reduce`, and `ToString`. These are pure conversion/diagnostic helpers; validate range, signedness, endianness, and engineering-unit assumptions with known values before command use. |

## Operational guidance

Dispose subscriptions and clients. Prefer a scan interval that is realistic for controller and network capacity. Batch selected reads/writes instead of issuing many independent round trips. Treat `PlcTagResult.StatusCode` as the operation outcome; subscribe to `ObserveErrors` for stream-level visibility. Use sampled streams for UI and telemetry sinks.

## Troubleshooting

- **Cannot connect to Logix:** confirm the EtherNet/IP path and controller slot; use the five-argument constructor when routing is required.
- **Bit value is wrong:** use an integral tag, not a native `bool`, for word-bit addressing and keep the index in range.
- **A staged value does not reach the PLC:** check `AutoWriteValue`; call `Write(variable)` or `Write()` when it is false.
- **No updates:** ensure the tag is registered, scanning is enabled, and the subscription remains alive.
- **Native error:** inspect `StatusCode` with `PlcTagStatus.DecodeError`; verify gateway, firewall, and controller access before retrying.

## AI skill

For source-grounded implementation guidance, use the packaged [`ab-plc-rx` skill](../../skills/ab-plc-rx/SKILL.md). It directs an agent to retain explicit type witnesses, validate PLC safety, and consult this README for the detailed contract.

MIT licensed. See the repository `LICENSE`.

<!-- BEGIN GENERATED PUBLIC API -->

## Exhaustive public API reference

This catalogue is generated from the packaged runtime assemblies and their XML documentation. It includes exported public types and their declared public members; inherited members and non-public implementation details are intentionally omitted.

### `ABPlcRx`

Exported public types: 19; declared public members: 315.

#### `T:IoT.DriverCore.ABPlcRx.ABLogicalTagClient`

```csharp
public class IoT.DriverCore.ABPlcRx.ABLogicalTagClient
```
Adapts existing Allen-Bradley setup members to the common logical-tag setup contracts.

##### Declared public members

###### `M:IoT.DriverCore.ABPlcRx.ABLogicalTagClient.#ctor(IoT.DriverCore.ABPlcRx.IABPlcRx)`

```csharp
public IoT.DriverCore.ABPlcRx.ABLogicalTagClient(IoT.DriverCore.ABPlcRx.IABPlcRx controller)
```
Initializes a new instance of the `T:IoT.DriverCore.ABPlcRx.ABLogicalTagClient` class.

- Parameter `controller`: The composed Allen-Bradley controller.

###### `M:IoT.DriverCore.ABPlcRx.ABLogicalTagClient.#ctor(IoT.DriverCore.ABPlcRx.IABPlcRx,IoT.DriverCore.Core.ILogicalTagCatalog)`

```csharp
public IoT.DriverCore.ABPlcRx.ABLogicalTagClient(IoT.DriverCore.ABPlcRx.IABPlcRx controller, IoT.DriverCore.Core.ILogicalTagCatalog catalog)
```
Initializes a new instance of the `T:IoT.DriverCore.ABPlcRx.ABLogicalTagClient` class.

- Parameter `controller`: The composed Allen-Bradley controller.
- Parameter `catalog`: The logical-tag catalog.

###### `M:IoT.DriverCore.ABPlcRx.ABLogicalTagClient.#ctor(IoT.DriverCore.ABPlcRx.IABPlcRx,IoT.DriverCore.Core.ILogicalTagCatalog,IoT.DriverCore.Core.LogicalTagSqliteStore)`

```csharp
public IoT.DriverCore.ABPlcRx.ABLogicalTagClient(IoT.DriverCore.ABPlcRx.IABPlcRx controller, IoT.DriverCore.Core.ILogicalTagCatalog catalog, IoT.DriverCore.Core.LogicalTagSqliteStore store)
```
Initializes a new instance of the `T:IoT.DriverCore.ABPlcRx.ABLogicalTagClient` class.

- Parameter `controller`: The composed Allen-Bradley controller.
- Parameter `catalog`: The logical-tag catalog.
- Parameter `store`: The SQLite tag store.

###### `M:IoT.DriverCore.ABPlcRx.ABLogicalTagClient.#ctor(IoT.DriverCore.ABPlcRx.IABPlcRx,IoT.DriverCore.Core.ILogicalTagCatalog,IoT.DriverCore.Core.LogicalTagSqliteStore,System.TimeProvider)`

```csharp
public IoT.DriverCore.ABPlcRx.ABLogicalTagClient(IoT.DriverCore.ABPlcRx.IABPlcRx controller, IoT.DriverCore.Core.ILogicalTagCatalog catalog, IoT.DriverCore.Core.LogicalTagSqliteStore store, System.TimeProvider timeProvider)
```
Initializes a new instance of the `T:IoT.DriverCore.ABPlcRx.ABLogicalTagClient` class.

- Parameter `controller`: The composed Allen-Bradley controller.
- Parameter `catalog`: The logical-tag catalog.
- Parameter `store`: The SQLite tag store.
- Parameter `timeProvider`: The time provider used to stamp logical tag values.

###### `M:IoT.DriverCore.ABPlcRx.ABLogicalTagClient.#ctor(IoT.DriverCore.ABPlcRx.IABPlcRx,IoT.DriverCore.Core.ILogicalTagCatalog,System.TimeProvider)`

```csharp
public IoT.DriverCore.ABPlcRx.ABLogicalTagClient(IoT.DriverCore.ABPlcRx.IABPlcRx controller, IoT.DriverCore.Core.ILogicalTagCatalog catalog, System.TimeProvider timeProvider)
```
Initializes a new instance of the `T:IoT.DriverCore.ABPlcRx.ABLogicalTagClient` class.

- Parameter `controller`: The composed Allen-Bradley controller.
- Parameter `catalog`: The logical-tag catalog.
- Parameter `timeProvider`: The time provider used to stamp logical tag values.

###### `M:IoT.DriverCore.ABPlcRx.ABLogicalTagClient.#ctor(IoT.DriverCore.ABPlcRx.IABPlcRx,System.TimeProvider)`

```csharp
public IoT.DriverCore.ABPlcRx.ABLogicalTagClient(IoT.DriverCore.ABPlcRx.IABPlcRx controller, System.TimeProvider timeProvider)
```
Initializes a new instance of the `T:IoT.DriverCore.ABPlcRx.ABLogicalTagClient` class.

- Parameter `controller`: The composed Allen-Bradley controller.
- Parameter `timeProvider`: The time provider used to stamp logical tag values.

###### `M:IoT.DriverCore.ABPlcRx.ABLogicalTagClient.CreateTag(IoT.DriverCore.Core.LogicalTag)`

```csharp
public IoT.DriverCore.Core.LogicalTag CreateTag(IoT.DriverCore.Core.LogicalTag tag)
```
Creates and registers an existing logical tag definition.

- Parameter `tag`: The logical tag definition.
- Returns: The registered definition.

###### `M:IoT.DriverCore.ABPlcRx.ABLogicalTagClient.CreateTag(System.String,System.String,System.String)`

```csharp
public IoT.DriverCore.Core.LogicalTag CreateTag(string name, string address, string dataType)
```
Creates and registers a logical tag.

- Parameter `name`: The logical name.
- Parameter `address`: The Allen-Bradley address.
- Parameter `dataType`: The CLR or PLC data type name.
- Returns: The registered definition.

###### `M:IoT.DriverCore.ABPlcRx.ABLogicalTagClient.DeleteGroupAsync(System.String,System.Threading.CancellationToken)`

```csharp
public System.Threading.Tasks.Task<bool> DeleteGroupAsync(string name, System.Threading.CancellationToken cancellationToken)
```
Inherits XML documentation from its implemented or overridden member.

- Parameter `name`: The `name` value.
- Parameter `cancellationToken`: The `cancellationToken` value.
- Returns: A `System.Threading.Tasks.Task<bool>` result.

###### `M:IoT.DriverCore.ABPlcRx.ABLogicalTagClient.DeleteTagAsync(System.String,System.Threading.CancellationToken)`

```csharp
public System.Threading.Tasks.Task<bool> DeleteTagAsync(string tagName, System.Threading.CancellationToken cancellationToken)
```
Deletes a SQLite tag and removes it from the live catalog when found.

- Parameter `tagName`: The logical tag name.
- Parameter `cancellationToken`: A token to cancel the operation.
- Returns: True when the persisted tag existed.

###### `M:IoT.DriverCore.ABPlcRx.ABLogicalTagClient.Dispose`

```csharp
public void Dispose()
```
Inherits XML documentation from its implemented or overridden member.

###### `M:IoT.DriverCore.ABPlcRx.ABLogicalTagClient.EditTagAsync(IoT.DriverCore.Core.LogicalTag,System.Threading.CancellationToken)`

```csharp
public System.Threading.Tasks.Task<bool> EditTagAsync(IoT.DriverCore.Core.LogicalTag tag, System.Threading.CancellationToken cancellationToken)
```
Edits a SQLite tag and synchronizes the live catalog when found.

- Parameter `tag`: The replacement definition.
- Parameter `cancellationToken`: A token to cancel the operation.
- Returns: True when the persisted tag existed.

###### `M:IoT.DriverCore.ABPlcRx.ABLogicalTagClient.ExportCsvAsync(System.IO.TextWriter,System.Char,System.Threading.CancellationToken)`

```csharp
public System.Threading.Tasks.Task ExportCsvAsync(System.IO.TextWriter writer, char delimiter, System.Threading.CancellationToken cancellationToken)
```
Inherits XML documentation from its implemented or overridden member.

- Parameter `writer`: The `writer` value.
- Parameter `delimiter`: The `delimiter` value.
- Parameter `cancellationToken`: The `cancellationToken` value.
- Returns: A `System.Threading.Tasks.Task` result.

###### `M:IoT.DriverCore.ABPlcRx.ABLogicalTagClient.ExportCsvAsync(System.IO.TextWriter,System.Threading.CancellationToken)`

```csharp
public System.Threading.Tasks.Task ExportCsvAsync(System.IO.TextWriter writer, System.Threading.CancellationToken cancellationToken)
```
Exports the current catalog through `T:IoT.DriverCore.Core.LogicalTagCsv` .

- Parameter `writer`: The CSV writer.
- Parameter `cancellationToken`: A token to cancel the operation.
- Returns: A task that completes after export.

###### `M:IoT.DriverCore.ABPlcRx.ABLogicalTagClient.GetGroupAsync(System.String,System.Threading.CancellationToken)`

```csharp
public System.Threading.Tasks.Task<IoT.DriverCore.Core.LogicalTagGroup> GetGroupAsync(string name, System.Threading.CancellationToken cancellationToken)
```
Inherits XML documentation from its implemented or overridden member.

- Parameter `name`: The `name` value.
- Parameter `cancellationToken`: The `cancellationToken` value.
- Returns: A `System.Threading.Tasks.Task<IoT.DriverCore.Core.LogicalTagGroup>` result.

###### `M:IoT.DriverCore.ABPlcRx.ABLogicalTagClient.GetTagAsync(System.String,System.Threading.CancellationToken)`

```csharp
public System.Threading.Tasks.Task<IoT.DriverCore.Core.LogicalTag> GetTagAsync(string tagName, System.Threading.CancellationToken cancellationToken)
```
Gets a tag from the configured SQLite store.

- Parameter `tagName`: The logical tag name.
- Parameter `cancellationToken`: A token to cancel the operation.
- Returns: The stored tag, or null.

###### `M:IoT.DriverCore.ABPlcRx.ABLogicalTagClient.ImportCsvAsync(System.IO.TextReader,System.Char,System.Threading.CancellationToken)`

```csharp
public System.Threading.Tasks.Task<System.Collections.Generic.IReadOnlyList<IoT.DriverCore.Core.LogicalTag>> ImportCsvAsync(System.IO.TextReader reader, char delimiter, System.Threading.CancellationToken cancellationToken)
```
Inherits XML documentation from its implemented or overridden member.

- Parameter `reader`: The `reader` value.
- Parameter `delimiter`: The `delimiter` value.
- Parameter `cancellationToken`: The `cancellationToken` value.
- Returns: A `System.Threading.Tasks.Task<System.Collections.Generic.IReadOnlyList<IoT.DriverCore.Core.LogicalTag>>` result.

###### `M:IoT.DriverCore.ABPlcRx.ABLogicalTagClient.ImportCsvAsync(System.IO.TextReader,System.Threading.CancellationToken)`

```csharp
public System.Threading.Tasks.Task<System.Collections.Generic.IReadOnlyList<IoT.DriverCore.Core.LogicalTag>> ImportCsvAsync(System.IO.TextReader reader, System.Threading.CancellationToken cancellationToken)
```
Imports CSV definitions through `T:IoT.DriverCore.Core.LogicalTagCsv` and registers them.

- Parameter `reader`: The CSV reader.
- Parameter `cancellationToken`: A token to cancel the operation.
- Returns: The imported definitions.

###### `M:IoT.DriverCore.ABPlcRx.ABLogicalTagClient.InitializeStoreAsync(IoT.DriverCore.Core.LogicalTagSqliteStore,System.Threading.CancellationToken)`

```csharp
public System.Threading.Tasks.Task InitializeStoreAsync(IoT.DriverCore.Core.LogicalTagSqliteStore store, System.Threading.CancellationToken cancellationToken)
```
Initializes and retains a SQLite store for CRUD operations.

- Parameter `store`: The SQLite store.
- Parameter `cancellationToken`: A token to cancel the operation.
- Returns: A task that completes after schema initialization.

###### `M:IoT.DriverCore.ABPlcRx.ABLogicalTagClient.InitializeStoreAsync(System.Threading.CancellationToken)`

```csharp
public System.Threading.Tasks.Task InitializeStoreAsync(System.Threading.CancellationToken cancellationToken)
```
Inherits XML documentation from its implemented or overridden member.

- Parameter `cancellationToken`: The `cancellationToken` value.
- Returns: A `System.Threading.Tasks.Task` result.

###### `M:IoT.DriverCore.ABPlcRx.ABLogicalTagClient.ListGroupsAsync(System.Threading.CancellationToken)`

```csharp
public System.Threading.Tasks.Task<System.Collections.Generic.IReadOnlyList<IoT.DriverCore.Core.LogicalTagGroup>> ListGroupsAsync(System.Threading.CancellationToken cancellationToken)
```
Inherits XML documentation from its implemented or overridden member.

- Parameter `cancellationToken`: The `cancellationToken` value.
- Returns: A `System.Threading.Tasks.Task<System.Collections.Generic.IReadOnlyList<IoT.DriverCore.Core.LogicalTagGroup>>` result.

###### `M:IoT.DriverCore.ABPlcRx.ABLogicalTagClient.ListTagsAsync(System.Threading.CancellationToken)`

```csharp
public System.Threading.Tasks.Task<System.Collections.Generic.IReadOnlyList<IoT.DriverCore.Core.LogicalTag>> ListTagsAsync(System.Threading.CancellationToken cancellationToken)
```
Inherits XML documentation from its implemented or overridden member.

- Parameter `cancellationToken`: The `cancellationToken` value.
- Returns: A `System.Threading.Tasks.Task<System.Collections.Generic.IReadOnlyList<IoT.DriverCore.Core.LogicalTag>>` result.

###### `M:IoT.DriverCore.ABPlcRx.ABLogicalTagClient.LoadTagsAsync(System.Threading.CancellationToken)`

```csharp
public System.Threading.Tasks.Task<System.Collections.Generic.IReadOnlyList<IoT.DriverCore.Core.LogicalTag>> LoadTagsAsync(System.Threading.CancellationToken cancellationToken)
```
Loads the configured SQLite catalog and dynamically registers every definition.

- Parameter `cancellationToken`: A token to cancel the operation.
- Returns: The loaded definitions.

###### `M:IoT.DriverCore.ABPlcRx.ABLogicalTagClient.Observe(System.String)`

```csharp
public System.IObservable<IoT.DriverCore.Core.LogicalTagValue> Observe(string tagName)
```
Inherits XML documentation from its implemented or overridden member.

- Parameter `tagName`: The `tagName` value.
- Returns: A `System.IObservable<IoT.DriverCore.Core.LogicalTagValue>` result.

###### `M:IoT.DriverCore.ABPlcRx.ABLogicalTagClient.ObserveAsync(System.String,System.Threading.CancellationToken)`

```csharp
public System.Collections.Generic.IAsyncEnumerable<IoT.DriverCore.Core.LogicalTagValue> ObserveAsync(string tagName, System.Threading.CancellationToken cancellationToken)
```
Inherits XML documentation from its implemented or overridden member.

- Parameter `tagName`: The `tagName` value.
- Parameter `cancellationToken`: The `cancellationToken` value.
- Returns: A `System.Collections.Generic.IAsyncEnumerable<IoT.DriverCore.Core.LogicalTagValue>` result.

###### `M:IoT.DriverCore.ABPlcRx.ABLogicalTagClient.ObserveMany(System.Collections.Generic.IReadOnlyCollection`1{System.String})`

```csharp
public System.IObservable<IoT.DriverCore.Core.LogicalTagValue> ObserveMany(System.Collections.Generic.IReadOnlyCollection<string> tagNames)
```
Executes the `ObserveMany` operation.

- Parameter `tagNames`: The `tagNames` value.
- Returns: A `System.IObservable<IoT.DriverCore.Core.LogicalTagValue>` result.

###### `M:IoT.DriverCore.ABPlcRx.ABLogicalTagClient.ObserveManyAsync(System.Collections.Generic.IReadOnlyCollection`1{System.String},System.Threading.CancellationToken)`

```csharp
public System.Collections.Generic.IAsyncEnumerable<IoT.DriverCore.Core.LogicalTagValue> ObserveManyAsync(System.Collections.Generic.IReadOnlyCollection<string> tagNames, System.Threading.CancellationToken cancellationToken)
```
Executes the `ObserveManyAsync` operation.

- Parameter `tagNames`: The `tagNames` value.
- Parameter `cancellationToken`: The `cancellationToken` value.
- Returns: A `System.Collections.Generic.IAsyncEnumerable<IoT.DriverCore.Core.LogicalTagValue>` result.

###### `M:IoT.DriverCore.ABPlcRx.ABLogicalTagClient.ReadAsync(System.String,System.Threading.CancellationToken)`

```csharp
public System.Threading.Tasks.Task<IoT.DriverCore.Core.TagOperationResult<IoT.DriverCore.Core.LogicalTagValue>> ReadAsync(string tagName, System.Threading.CancellationToken cancellationToken)
```
Inherits XML documentation from its implemented or overridden member.

- Parameter `tagName`: The `tagName` value.
- Parameter `cancellationToken`: The `cancellationToken` value.
- Returns: A `System.Threading.Tasks.Task<IoT.DriverCore.Core.TagOperationResult<IoT.DriverCore.Core.LogicalTagValue>>` result.

###### `M:IoT.DriverCore.ABPlcRx.ABLogicalTagClient.ReadManyAsync(System.Collections.Generic.IReadOnlyCollection`1{System.String},System.Threading.CancellationToken)`

```csharp
public System.Threading.Tasks.Task<System.Collections.Generic.IReadOnlyList<IoT.DriverCore.Core.TagOperationResult<IoT.DriverCore.Core.LogicalTagValue>>> ReadManyAsync(System.Collections.Generic.IReadOnlyCollection<string> tagNames, System.Threading.CancellationToken cancellationToken)
```
Executes the `ReadManyAsync` operation.

- Parameter `tagNames`: The `tagNames` value.
- Parameter `cancellationToken`: The `cancellationToken` value.
- Returns: A `System.Threading.Tasks.Task<System.Collections.Generic.IReadOnlyList<IoT.DriverCore.Core.TagOperationResult<IoT.DriverCore.Core.LogicalTagValue>>>` result.

###### `M:IoT.DriverCore.ABPlcRx.ABLogicalTagClient.RegisterTag(IoT.DriverCore.Core.LogicalTag)`

```csharp
public void RegisterTag(IoT.DriverCore.Core.LogicalTag tag)
```
Registers or replaces a logical tag in the controller and catalog.

- Parameter `tag`: The logical tag definition.

###### `M:IoT.DriverCore.ABPlcRx.ABLogicalTagClient.RemoveTag(System.String)`

```csharp
public bool RemoveTag(string tagName)
```
Removes a logical tag from the controller and catalog.

- Parameter `tagName`: The logical tag name.
- Returns: True when either layer contained the tag.

###### `M:IoT.DriverCore.ABPlcRx.ABLogicalTagClient.UpsertGroupAsync(IoT.DriverCore.Core.LogicalTagGroup,System.Threading.CancellationToken)`

```csharp
public System.Threading.Tasks.Task UpsertGroupAsync(IoT.DriverCore.Core.LogicalTagGroup group, System.Threading.CancellationToken cancellationToken)
```
Inherits XML documentation from its implemented or overridden member.

- Parameter `group`: The `group` value.
- Parameter `cancellationToken`: The `cancellationToken` value.
- Returns: A `System.Threading.Tasks.Task` result.

###### `M:IoT.DriverCore.ABPlcRx.ABLogicalTagClient.UpsertTagAsync(IoT.DriverCore.Core.LogicalTag,System.Threading.CancellationToken)`

```csharp
public System.Threading.Tasks.Task UpsertTagAsync(IoT.DriverCore.Core.LogicalTag tag, System.Threading.CancellationToken cancellationToken)
```
Upserts a SQLite tag and synchronizes the live catalog.

- Parameter `tag`: The tag definition.
- Parameter `cancellationToken`: A token to cancel the operation.
- Returns: A task that completes after synchronization.

###### `M:IoT.DriverCore.ABPlcRx.ABLogicalTagClient.WriteAsync(IoT.DriverCore.Core.LogicalTagValue,System.Threading.CancellationToken)`

```csharp
public System.Threading.Tasks.Task<IoT.DriverCore.Core.TagOperationResult<IoT.DriverCore.Core.LogicalTagValue>> WriteAsync(IoT.DriverCore.Core.LogicalTagValue value, System.Threading.CancellationToken cancellationToken)
```
Inherits XML documentation from its implemented or overridden member.

- Parameter `value`: The `value` value.
- Parameter `cancellationToken`: The `cancellationToken` value.
- Returns: A `System.Threading.Tasks.Task<IoT.DriverCore.Core.TagOperationResult<IoT.DriverCore.Core.LogicalTagValue>>` result.

###### `M:IoT.DriverCore.ABPlcRx.ABLogicalTagClient.WriteManyAsync(System.Collections.Generic.IReadOnlyCollection`1{IoT.DriverCore.Core.LogicalTagValue},System.Threading.CancellationToken)`

```csharp
public System.Threading.Tasks.Task<System.Collections.Generic.IReadOnlyList<IoT.DriverCore.Core.TagOperationResult<IoT.DriverCore.Core.LogicalTagValue>>> WriteManyAsync(System.Collections.Generic.IReadOnlyCollection<IoT.DriverCore.Core.LogicalTagValue> values, System.Threading.CancellationToken cancellationToken)
```
Executes the `WriteManyAsync` operation.

- Parameter `values`: The `values` value.
- Parameter `cancellationToken`: The `cancellationToken` value.
- Returns: A `System.Threading.Tasks.Task<System.Collections.Generic.IReadOnlyList<IoT.DriverCore.Core.TagOperationResult<IoT.DriverCore.Core.LogicalTagValue>>>` result.

###### `P:IoT.DriverCore.ABPlcRx.ABLogicalTagClient.Catalog`

```csharp
public IoT.DriverCore.Core.ILogicalTagCatalog Catalog { get; }
```
Gets the logical-tag catalog used by this adapter.

- Value: The `Catalog` value.

#### `T:IoT.DriverCore.ABPlcRx.ABPlcRx`

```csharp
public class IoT.DriverCore.ABPlcRx.ABPlcRx
```
Reactive Allen Bradley PLC facade.

##### Declared public members

###### `M:IoT.DriverCore.ABPlcRx.ABPlcRx.#ctor(IoT.DriverCore.ABPlcRx.PlcType,System.String,System.TimeSpan)`

```csharp
public IoT.DriverCore.ABPlcRx.ABPlcRx(IoT.DriverCore.ABPlcRx.PlcType plcType, string ip, System.TimeSpan scanInterval)
```
Initializes a new instance of the `T:IoT.DriverCore.ABPlcRx.ABPlcRx` class.

- Parameter `plcType`: Type of the PLC.
- Parameter `ip`: The ip.
- Parameter `scanInterval`: The scan interval.

###### `M:IoT.DriverCore.ABPlcRx.ABPlcRx.#ctor(IoT.DriverCore.ABPlcRx.PlcType,System.String,System.TimeSpan,System.TimeSpan,System.String)`

```csharp
public IoT.DriverCore.ABPlcRx.ABPlcRx(IoT.DriverCore.ABPlcRx.PlcType plcType, string ip, System.TimeSpan scanInterval, System.TimeSpan timeOut, string path)
```
Initializes a new instance of the `T:IoT.DriverCore.ABPlcRx.ABPlcRx` class.

- Parameter `plcType`: Type of the PLC.
- Parameter `ip`: The ip.
- Parameter `scanInterval`: The scan interval.
- Parameter `timeOut`: The time out.
- Parameter `path`: The path.

###### `M:IoT.DriverCore.ABPlcRx.ABPlcRx.AddUpdateTagItem``1(System.String,System.String,System.String,``0)`

```csharp
public void AddUpdateTagItem<T>(string variable, string tagName, string tagGroup, T typeWitness)
```
Adds the update tag item.

- Parameter `variable`: The variable, this can be any non null name you wish to use.
- Parameter `tagName`: Name of the tag.
- Parameter `tagGroup`: The tag group.
- Parameter `typeWitness`: Optional type witness for callers that infer from a value.

###### `M:IoT.DriverCore.ABPlcRx.ABPlcRx.AddUpdateTagItem``1(System.String,System.String,``0)`

```csharp
public void AddUpdateTagItem<T>(string variable, string tagName, T typeWitness)
```
Adds the update tag item.

- Parameter `variable`: The variable, this can be any non null name you wish to use.
- Parameter `tagName`: Name of the tag.
- Parameter `typeWitness`: Optional type witness for callers that infer from a value.

###### `M:IoT.DriverCore.ABPlcRx.ABPlcRx.AddUpdateTagItem``1(System.String,``0)`

```csharp
public void AddUpdateTagItem<T>(string tagName, T typeWitness)
```
Adds the update tag item.

- Parameter `tagName`: Name of the tag.
- Parameter `typeWitness`: Optional type witness for callers that infer from a value.

###### `M:IoT.DriverCore.ABPlcRx.ABPlcRx.CreateWriter``1(System.String,``0,System.Int32)`

```csharp
public System.IObserver<T> CreateWriter<T>(string variable, T typeWitness, int bit)
```
Creates an observer that writes values to a PLC variable when OnNext is called.

- Parameter `variable`: The variable to write to.
- Parameter `typeWitness`: Type witness for the writer value type.
- Parameter `bit`: The bit [ONLY use for bool tags].
- Returns: An observer that will write and commit values to the PLC.

###### `M:IoT.DriverCore.ABPlcRx.ABPlcRx.Dispose`

```csharp
public void Dispose()
```
Releases the PLC facade's managed and unmanaged resources.

###### `M:IoT.DriverCore.ABPlcRx.ABPlcRx.GetValue``1(System.String,``0,System.Int32)`

```csharp
public T GetValue<T>(string variable, T typeWitness, int bit)
```
Values the specified variable.

- Parameter `variable`: The variable.
- Parameter `typeWitness`: Type witness for the requested PLC value type.
- Parameter `bit`: The bit [ONLY use for bool tags].
- Returns: A value of T.

###### `M:IoT.DriverCore.ABPlcRx.ABPlcRx.ObserveAsyncObservable``1(System.String,``0,System.Int32)`

```csharp
public ReactiveUI.Primitives.Async.IObservableAsync<T> ObserveAsyncObservable<T>(string variable, T typeWitness, int bit)
```
Observes the specified variable as an async-native observable.

- Parameter `variable`: The variable.
- Parameter `typeWitness`: Type witness for the requested PLC value type.
- Parameter `bit`: The bit.
- Returns: An async observable of T.

###### `M:IoT.DriverCore.ABPlcRx.ABPlcRx.ObserveErrors`

```csharp
public System.IObservable<IoT.DriverCore.ABPlcRx.PlcTagResult> ObserveErrors()
```
Streams only error results across all tags.

- Returns: Observable sequence of error results.

###### `M:IoT.DriverCore.ABPlcRx.ABPlcRx.ObserveErrorsAsyncObservable`

```csharp
public ReactiveUI.Primitives.Async.IObservableAsync<IoT.DriverCore.ABPlcRx.PlcTagResult> ObserveErrorsAsyncObservable()
```
Streams only error results across all tags as an async-native observable.

- Returns: Async observable sequence of error results.

###### `M:IoT.DriverCore.ABPlcRx.ABPlcRx.ObserveGroup(System.String)`

```csharp
public System.IObservable<IoT.DriverCore.ABPlcRx.IPlcTag> ObserveGroup(string groupName)
```
Observe a PLC tag group, emitting the tag whose value changed.

- Parameter `groupName`: The group name to observe.
- Returns: Observable sequence of tags in the group that have changed.

###### `M:IoT.DriverCore.ABPlcRx.ABPlcRx.ObserveGroupAsyncObservable(System.String)`

```csharp
public ReactiveUI.Primitives.Async.IObservableAsync<IoT.DriverCore.ABPlcRx.IPlcTag> ObserveGroupAsyncObservable(string groupName)
```
Observe a PLC tag group as an async-native observable.

- Parameter `groupName`: The group name to observe.
- Returns: Async observable sequence of tags in the group that have changed.

###### `M:IoT.DriverCore.ABPlcRx.ABPlcRx.ObserveMany(System.String[])`

```csharp
public System.IObservable<System.Collections.Generic.IReadOnlyDictionary<string, object>> ObserveMany(string[] variables)
```
Observe values for many variables and emit a latest-value dictionary.

- Parameter `variables`: One or more variable names to observe.
- Returns: Observable sequence of dictionary containing the latest values for each variable.

###### `M:IoT.DriverCore.ABPlcRx.ABPlcRx.ObserveManyAsyncObservable(System.String[])`

```csharp
public ReactiveUI.Primitives.Async.IObservableAsync<System.Collections.Generic.IReadOnlyDictionary<string, object>> ObserveManyAsyncObservable(string[] variables)
```
Observes many variables as an async-native latest-value dictionary.

- Parameter `variables`: One or more variable names to observe.
- Returns: Async observable sequence of dictionary containing the latest values for each variable.

###### `M:IoT.DriverCore.ABPlcRx.ABPlcRx.ObservePing(System.TimeSpan,System.Boolean,ReactiveUI.Primitives.Concurrency.ISequencer)`

```csharp
public System.IObservable<bool> ObservePing(System.TimeSpan interval, bool echo, ReactiveUI.Primitives.Concurrency.ISequencer scheduler)
```
Observe ping results on a schedule.

- Parameter `interval`: The interval between pings.
- Parameter `echo`: True echo result to standard output.
- Parameter `scheduler`: Optional scheduler for the ping cadence.
- Returns: Observable sequence of ping result states, deduplicated.

###### `M:IoT.DriverCore.ABPlcRx.ABPlcRx.ObservePingAsyncObservable(System.TimeSpan,System.Boolean,ReactiveUI.Primitives.Concurrency.ISequencer)`

```csharp
public ReactiveUI.Primitives.Async.IObservableAsync<bool> ObservePingAsyncObservable(System.TimeSpan interval, bool echo, ReactiveUI.Primitives.Concurrency.ISequencer scheduler)
```
Observe ping results on a schedule as an async-native observable.

- Parameter `interval`: The interval between pings.
- Parameter `echo`: True echo result to standard output.
- Parameter `scheduler`: Optional scheduler for the ping cadence.
- Returns: Async observable sequence of ping result states, deduplicated.

###### `M:IoT.DriverCore.ABPlcRx.ABPlcRx.ObserveSampledAsyncObservable``1(System.String,System.TimeSpan,``0,System.Int32,ReactiveUI.Primitives.Concurrency.ISequencer)`

```csharp
public ReactiveUI.Primitives.Async.IObservableAsync<T> ObserveSampledAsyncObservable<T>(string variable, System.TimeSpan sampleInterval, T typeWitness, int bit, ReactiveUI.Primitives.Concurrency.ISequencer scheduler)
```
Observe a variable with sampling as an async-native observable.

- Parameter `variable`: The variable to observe.
- Parameter `sampleInterval`: The sampling interval.
- Parameter `typeWitness`: Type witness for the requested PLC value type.
- Parameter `bit`: The bit [ONLY use for bool tags].
- Parameter `scheduler`: Optional scheduler for sampling.
- Returns: Async observable sequence of sampled values.

###### `M:IoT.DriverCore.ABPlcRx.ABPlcRx.ObserveSampled``1(System.String,System.TimeSpan,``0,System.Int32,ReactiveUI.Primitives.Concurrency.ISequencer)`

```csharp
public System.IObservable<T> ObserveSampled<T>(string variable, System.TimeSpan sampleInterval, T typeWitness, int bit, ReactiveUI.Primitives.Concurrency.ISequencer scheduler)
```
Observe a variable with sampling, reducing event rate while preserving latest value.

- Parameter `variable`: The variable to observe.
- Parameter `sampleInterval`: The sampling interval.
- Parameter `typeWitness`: Type witness for the requested PLC value type.
- Parameter `bit`: The bit [ONLY use for bool tags].
- Parameter `scheduler`: Optional scheduler for sampling.
- Returns: Observable sequence of sampled values.

###### `M:IoT.DriverCore.ABPlcRx.ABPlcRx.Observe``1(System.String,``0,System.Int32)`

```csharp
public System.IObservable<T> Observe<T>(string variable, T typeWitness, int bit)
```
Observes the specified variable.

- Parameter `variable`: The variable.
- Parameter `typeWitness`: Type witness for the requested PLC value type.
- Parameter `bit`: The bit.
- Returns: An Observable of T.

###### `M:IoT.DriverCore.ABPlcRx.ABPlcRx.Ping(System.Boolean)`

```csharp
public bool Ping(bool echo)
```
Ping the PLC.

- Parameter `echo`: True echo result to standard output.
- Returns: True when ping succeeds; otherwise false.

###### `M:IoT.DriverCore.ABPlcRx.ABPlcRx.PingAsync(System.Boolean,System.Threading.CancellationToken)`

```csharp
public System.Threading.Tasks.Task<bool> PingAsync(bool echo, System.Threading.CancellationToken cancellationToken)
```
Ping the PLC asynchronously.

- Parameter `echo`: True echo result to standard output.
- Parameter `cancellationToken`: A token to cancel the ping operation.
- Returns: A task producing true when ping succeeds; otherwise false.

###### `M:IoT.DriverCore.ABPlcRx.ABPlcRx.Read`

```csharp
public System.Collections.Generic.IEnumerable<IoT.DriverCore.ABPlcRx.PlcTagResult> Read()
```
Reads all the Tags in this instance.

- Returns: A PlcTagResult.

###### `M:IoT.DriverCore.ABPlcRx.ABPlcRx.Read(System.String)`

```csharp
public IoT.DriverCore.ABPlcRx.PlcTagResult Read(string variable)
```
Reads the specified variable.

- Parameter `variable`: The variable.
- Returns: A PlcTagResult.

###### `M:IoT.DriverCore.ABPlcRx.ABPlcRx.ReadManyAsync(System.Collections.Generic.IReadOnlyCollection`1{System.String},System.Threading.CancellationToken)`

```csharp
public System.Threading.Tasks.Task<System.Collections.Generic.IReadOnlyList<IoT.DriverCore.ABPlcRx.PlcTagResult>> ReadManyAsync(System.Collections.Generic.IReadOnlyCollection<string> variables, System.Threading.CancellationToken cancellationToken)
```
Executes the `ReadManyAsync` operation.

- Parameter `variables`: The `variables` value.
- Parameter `cancellationToken`: The `cancellationToken` value.
- Returns: A `System.Threading.Tasks.Task<System.Collections.Generic.IReadOnlyList<IoT.DriverCore.ABPlcRx.PlcTagResult>>` result.

###### `M:IoT.DriverCore.ABPlcRx.ABPlcRx.ReadValueAsync``1(System.String,``0,System.Int32,System.Threading.CancellationToken)`

```csharp
public System.Threading.Tasks.Task<IoT.DriverCore.Core.TagOperationResult<T>> ReadValueAsync<T>(string variable, T typeWitness, int bit, System.Threading.CancellationToken cancellationToken)
```
Reads and converts one logical variable asynchronously.

- Parameter `variable`: The logical variable name.
- Parameter `typeWitness`: Type witness for the requested PLC value type.
- Parameter `bit`: The optional integral bit index.
- Parameter `cancellationToken`: A token to cancel the operation.
- Returns: The typed operation result.

###### `M:IoT.DriverCore.ABPlcRx.ABPlcRx.RemoveTagItem(System.String)`

```csharp
public bool RemoveTagItem(string variable)
```
Removes a registered tag by logical variable name.

- Parameter `variable`: The logical variable name.
- Returns: True when a tag was removed.

###### `M:IoT.DriverCore.ABPlcRx.ABPlcRx.Value``1(System.String,``0,System.Int32)`

```csharp
public void Value<T>(string variable, T value, int bit)
```
Values the specified variable.

- Parameter `variable`: The variable.
- Parameter `value`: The value.
- Parameter `bit`: The bit [ONLY use for bool tags].

###### `M:IoT.DriverCore.ABPlcRx.ABPlcRx.Write`

```csharp
public System.Collections.Generic.IEnumerable<IoT.DriverCore.ABPlcRx.PlcTagResult> Write()
```
Writes all the tags in this instance.

- Returns: A PlcTagResult.

###### `M:IoT.DriverCore.ABPlcRx.ABPlcRx.Write(System.String)`

```csharp
public IoT.DriverCore.ABPlcRx.PlcTagResult Write(string variable)
```
Writes the specified variable.

- Parameter `variable`: The variable.
- Returns: A PlcTagResult.

###### `M:IoT.DriverCore.ABPlcRx.ABPlcRx.WriteManyAsync(System.Collections.Generic.IReadOnlyDictionary`2{System.String,System.Object},System.Threading.CancellationToken)`

```csharp
public System.Threading.Tasks.Task<System.Collections.Generic.IReadOnlyList<IoT.DriverCore.ABPlcRx.PlcTagResult>> WriteManyAsync(System.Collections.Generic.IReadOnlyDictionary<string, object> values, System.Threading.CancellationToken cancellationToken)
```
Executes the `WriteManyAsync` operation.

- Parameter `values`: The `values` value.
- Parameter `cancellationToken`: The `cancellationToken` value.
- Returns: A `System.Threading.Tasks.Task<System.Collections.Generic.IReadOnlyList<IoT.DriverCore.ABPlcRx.PlcTagResult>>` result.

###### `M:IoT.DriverCore.ABPlcRx.ABPlcRx.WriteValueAsync``1(System.String,``0,System.Int32,System.Threading.CancellationToken)`

```csharp
public System.Threading.Tasks.Task<IoT.DriverCore.Core.TagOperationResult<T>> WriteValueAsync<T>(string variable, T value, int bit, System.Threading.CancellationToken cancellationToken)
```
Writes one logical variable asynchronously.

- Parameter `variable`: The logical variable name.
- Parameter `value`: The value to write.
- Parameter `bit`: The optional integral bit index.
- Parameter `cancellationToken`: A token to cancel the operation.
- Returns: The typed operation result.

###### `P:IoT.DriverCore.ABPlcRx.ABPlcRx.AutoWriteValue`

```csharp
public bool AutoWriteValue { get; set; }
```
Gets or sets a value indicating whether [automatic write value].

- Value: true if [automatic write value]; otherwise, false .

###### `P:IoT.DriverCore.ABPlcRx.ABPlcRx.IsDisposed`

```csharp
public bool IsDisposed { get; }
```
Gets a value indicating whether gets a value that indicates whether the object is disposed.

- Value: The `IsDisposed` value.

###### `P:IoT.DriverCore.ABPlcRx.ABPlcRx.ObserveAll`

```csharp
public System.IObservable<IoT.DriverCore.ABPlcRx.IPlcTag> ObserveAll { get; }
```
Gets the data read.

- Value: The data read.

###### `P:IoT.DriverCore.ABPlcRx.ABPlcRx.ObserveAllAsyncObservable`

```csharp
public ReactiveUI.Primitives.Async.IObservableAsync<IoT.DriverCore.ABPlcRx.IPlcTag> ObserveAllAsyncObservable { get; }
```
Gets the data read as an async-native observable.

- Value: The async data read stream.

###### `P:IoT.DriverCore.ABPlcRx.ABPlcRx.ScanEnabled`

```csharp
public bool ScanEnabled { get; set; }
```
Gets or sets a value indicating whether [scan enabled].

- Value: true if [scan enabled]; otherwise, false .

#### `T:IoT.DriverCore.ABPlcRx.ABPlcSimulator`

```csharp
public class IoT.DriverCore.ABPlcRx.ABPlcSimulator
```
Deterministic, in-memory Allen-Bradley controller simulator.

##### Declared public members

###### `M:IoT.DriverCore.ABPlcRx.ABPlcSimulator.#ctor`

```csharp
public IoT.DriverCore.ABPlcRx.ABPlcSimulator()
```
Initializes a new instance of the `T:IoT.DriverCore.ABPlcRx.ABPlcSimulator` class.

###### `M:IoT.DriverCore.ABPlcRx.ABPlcSimulator.#ctor(IoT.DriverCore.ABPlcRx.PlcType)`

```csharp
public IoT.DriverCore.ABPlcRx.ABPlcSimulator(IoT.DriverCore.ABPlcRx.PlcType plcType)
```
Initializes a new instance of the `T:IoT.DriverCore.ABPlcRx.ABPlcSimulator` class.

- Parameter `plcType`: The processor family to emulate.

###### `M:IoT.DriverCore.ABPlcRx.ABPlcSimulator.#ctor(IoT.DriverCore.ABPlcRx.PlcType,System.TimeSpan,System.TimeSpan,System.String,System.TimeProvider)`

```csharp
public IoT.DriverCore.ABPlcRx.ABPlcSimulator(IoT.DriverCore.ABPlcRx.PlcType plcType, System.TimeSpan scanInterval, System.TimeSpan timeout, string path, System.TimeProvider timeProvider)
```
Initializes a new instance of the `T:IoT.DriverCore.ABPlcRx.ABPlcSimulator` class.

- Parameter `plcType`: The processor family to emulate.
- Parameter `scanInterval`: The tag scan interval.
- Parameter `timeout`: The operation timeout.
- Parameter `path`: The optional route path.
- Parameter `timeProvider`: The time provider used for results and operation logs.

###### `M:IoT.DriverCore.ABPlcRx.ABPlcSimulator.AddUpdateTagItem``1(System.String,System.String,System.String,``0)`

```csharp
public void AddUpdateTagItem<T>(string variable, string tagName, string tagGroup, T typeWitness)
```
Inherits XML documentation from its implemented or overridden member.

- Parameter `variable`: The `variable` value.
- Parameter `tagName`: The `tagName` value.
- Parameter `tagGroup`: The `tagGroup` value.
- Parameter `typeWitness`: The `typeWitness` value.

###### `M:IoT.DriverCore.ABPlcRx.ABPlcSimulator.AddUpdateTagItem``1(System.String,System.String,``0)`

```csharp
public void AddUpdateTagItem<T>(string variable, string tagName, T typeWitness)
```
Inherits XML documentation from its implemented or overridden member.

- Parameter `variable`: The `variable` value.
- Parameter `tagName`: The `tagName` value.
- Parameter `typeWitness`: The `typeWitness` value.

###### `M:IoT.DriverCore.ABPlcRx.ABPlcSimulator.AddUpdateTagItem``1(System.String,``0)`

```csharp
public void AddUpdateTagItem<T>(string tagName, T typeWitness)
```
Inherits XML documentation from its implemented or overridden member.

- Parameter `tagName`: The `tagName` value.
- Parameter `typeWitness`: The `typeWitness` value.

###### `M:IoT.DriverCore.ABPlcRx.ABPlcSimulator.ClearFaults`

```csharp
public void ClearFaults()
```
Clears all unconsumed scripted operation results.

###### `M:IoT.DriverCore.ABPlcRx.ABPlcSimulator.ClearOperationLog`

```csharp
public void ClearOperationLog()
```
Clears the operation log and restarts its sequence at one.

###### `M:IoT.DriverCore.ABPlcRx.ABPlcSimulator.CreateLogicalTagClient`

```csharp
public IoT.DriverCore.ABPlcRx.ABLogicalTagClient CreateLogicalTagClient()
```
Creates a logical-tag client over this simulator.

- Returns: A logical-tag client that does not require physical hardware.

###### `M:IoT.DriverCore.ABPlcRx.ABPlcSimulator.CreateWriter``1(System.String,``0,System.Int32)`

```csharp
public System.IObserver<T> CreateWriter<T>(string variable, T typeWitness, int bit)
```
Inherits XML documentation from its implemented or overridden member.

- Parameter `variable`: The `variable` value.
- Parameter `typeWitness`: The `typeWitness` value.
- Parameter `bit`: The `bit` value.
- Returns: A `System.IObserver<T>` result.

###### `M:IoT.DriverCore.ABPlcRx.ABPlcSimulator.Disconnect`

```csharp
public void Disconnect()
```
Disconnects simulated communications with `F:IoT.DriverCore.ABPlcRx.PlcTagStatus.ErrBadConnection` .

###### `M:IoT.DriverCore.ABPlcRx.ABPlcSimulator.Disconnect(System.Int32)`

```csharp
public void Disconnect(int statusCode)
```
Disconnects simulated communications.

- Parameter `statusCode`: The status returned by IO while disconnected.

###### `M:IoT.DriverCore.ABPlcRx.ABPlcSimulator.Dispose`

```csharp
public void Dispose()
```
Inherits XML documentation from its implemented or overridden member.

###### `M:IoT.DriverCore.ABPlcRx.ABPlcSimulator.GetTagBytes(System.String)`

```csharp
public byte[] GetTagBytes(string tagName)
```
Gets a copy of raw device memory for a physical PLC tag.

- Parameter `tagName`: The physical PLC tag name.
- Returns: A copy of the raw tag bytes.

###### `M:IoT.DriverCore.ABPlcRx.ABPlcSimulator.GetTagValue``1(System.String,``0)`

```csharp
public T GetTagValue<T>(string tagName, T typeWitness)
```
Reads a supported scalar value directly from device memory.

- Parameter `tagName`: The physical PLC tag name.
- Parameter `typeWitness`: A type witness for the scalar value.
- Returns: The decoded value.

###### `M:IoT.DriverCore.ABPlcRx.ABPlcSimulator.GetValue``1(System.String,``0,System.Int32)`

```csharp
public T GetValue<T>(string variable, T typeWitness, int bit)
```
Inherits XML documentation from its implemented or overridden member.

- Parameter `variable`: The `variable` value.
- Parameter `typeWitness`: The `typeWitness` value.
- Parameter `bit`: The `bit` value.
- Returns: A `T` result.

###### `M:IoT.DriverCore.ABPlcRx.ABPlcSimulator.ObserveAsyncObservable``1(System.String,``0,System.Int32)`

```csharp
public ReactiveUI.Primitives.Async.IObservableAsync<T> ObserveAsyncObservable<T>(string variable, T typeWitness, int bit)
```
Inherits XML documentation from its implemented or overridden member.

- Parameter `variable`: The `variable` value.
- Parameter `typeWitness`: The `typeWitness` value.
- Parameter `bit`: The `bit` value.
- Returns: A `ReactiveUI.Primitives.Async.IObservableAsync<T>` result.

###### `M:IoT.DriverCore.ABPlcRx.ABPlcSimulator.ObserveErrors`

```csharp
public System.IObservable<IoT.DriverCore.ABPlcRx.PlcTagResult> ObserveErrors()
```
Inherits XML documentation from its implemented or overridden member.

- Returns: A `System.IObservable<IoT.DriverCore.ABPlcRx.PlcTagResult>` result.

###### `M:IoT.DriverCore.ABPlcRx.ABPlcSimulator.ObserveErrorsAsyncObservable`

```csharp
public ReactiveUI.Primitives.Async.IObservableAsync<IoT.DriverCore.ABPlcRx.PlcTagResult> ObserveErrorsAsyncObservable()
```
Inherits XML documentation from its implemented or overridden member.

- Returns: A `ReactiveUI.Primitives.Async.IObservableAsync<IoT.DriverCore.ABPlcRx.PlcTagResult>` result.

###### `M:IoT.DriverCore.ABPlcRx.ABPlcSimulator.ObserveGroup(System.String)`

```csharp
public System.IObservable<IoT.DriverCore.ABPlcRx.IPlcTag> ObserveGroup(string groupName)
```
Inherits XML documentation from its implemented or overridden member.

- Parameter `groupName`: The `groupName` value.
- Returns: A `System.IObservable<IoT.DriverCore.ABPlcRx.IPlcTag>` result.

###### `M:IoT.DriverCore.ABPlcRx.ABPlcSimulator.ObserveGroupAsyncObservable(System.String)`

```csharp
public ReactiveUI.Primitives.Async.IObservableAsync<IoT.DriverCore.ABPlcRx.IPlcTag> ObserveGroupAsyncObservable(string groupName)
```
Inherits XML documentation from its implemented or overridden member.

- Parameter `groupName`: The `groupName` value.
- Returns: A `ReactiveUI.Primitives.Async.IObservableAsync<IoT.DriverCore.ABPlcRx.IPlcTag>` result.

###### `M:IoT.DriverCore.ABPlcRx.ABPlcSimulator.ObserveMany(System.String[])`

```csharp
public System.IObservable<System.Collections.Generic.IReadOnlyDictionary<string, object>> ObserveMany(string[] variables)
```
Inherits XML documentation from its implemented or overridden member.

- Parameter `variables`: The `variables` value.
- Returns: A `System.IObservable<System.Collections.Generic.IReadOnlyDictionary<string, object>>` result.

###### `M:IoT.DriverCore.ABPlcRx.ABPlcSimulator.ObserveManyAsyncObservable(System.String[])`

```csharp
public ReactiveUI.Primitives.Async.IObservableAsync<System.Collections.Generic.IReadOnlyDictionary<string, object>> ObserveManyAsyncObservable(string[] variables)
```
Inherits XML documentation from its implemented or overridden member.

- Parameter `variables`: The `variables` value.
- Returns: A `ReactiveUI.Primitives.Async.IObservableAsync<System.Collections.Generic.IReadOnlyDictionary<string, object>>` result.

###### `M:IoT.DriverCore.ABPlcRx.ABPlcSimulator.ObservePing(System.TimeSpan,System.Boolean,ReactiveUI.Primitives.Concurrency.ISequencer)`

```csharp
public System.IObservable<bool> ObservePing(System.TimeSpan interval, bool echo, ReactiveUI.Primitives.Concurrency.ISequencer scheduler)
```
Inherits XML documentation from its implemented or overridden member.

- Parameter `interval`: The `interval` value.
- Parameter `echo`: The `echo` value.
- Parameter `scheduler`: The `scheduler` value.
- Returns: A `System.IObservable<bool>` result.

###### `M:IoT.DriverCore.ABPlcRx.ABPlcSimulator.ObservePingAsyncObservable(System.TimeSpan,System.Boolean,ReactiveUI.Primitives.Concurrency.ISequencer)`

```csharp
public ReactiveUI.Primitives.Async.IObservableAsync<bool> ObservePingAsyncObservable(System.TimeSpan interval, bool echo, ReactiveUI.Primitives.Concurrency.ISequencer scheduler)
```
Inherits XML documentation from its implemented or overridden member.

- Parameter `interval`: The `interval` value.
- Parameter `echo`: The `echo` value.
- Parameter `scheduler`: The `scheduler` value.
- Returns: A `ReactiveUI.Primitives.Async.IObservableAsync<bool>` result.

###### `M:IoT.DriverCore.ABPlcRx.ABPlcSimulator.ObserveSampledAsyncObservable``1(System.String,System.TimeSpan,``0,System.Int32,ReactiveUI.Primitives.Concurrency.ISequencer)`

```csharp
public ReactiveUI.Primitives.Async.IObservableAsync<T> ObserveSampledAsyncObservable<T>(string variable, System.TimeSpan sampleInterval, T typeWitness, int bit, ReactiveUI.Primitives.Concurrency.ISequencer scheduler)
```
Inherits XML documentation from its implemented or overridden member.

- Parameter `variable`: The `variable` value.
- Parameter `sampleInterval`: The `sampleInterval` value.
- Parameter `typeWitness`: The `typeWitness` value.
- Parameter `bit`: The `bit` value.
- Parameter `scheduler`: The `scheduler` value.
- Returns: A `ReactiveUI.Primitives.Async.IObservableAsync<T>` result.

###### `M:IoT.DriverCore.ABPlcRx.ABPlcSimulator.ObserveSampled``1(System.String,System.TimeSpan,``0,System.Int32,ReactiveUI.Primitives.Concurrency.ISequencer)`

```csharp
public System.IObservable<T> ObserveSampled<T>(string variable, System.TimeSpan sampleInterval, T typeWitness, int bit, ReactiveUI.Primitives.Concurrency.ISequencer scheduler)
```
Inherits XML documentation from its implemented or overridden member.

- Parameter `variable`: The `variable` value.
- Parameter `sampleInterval`: The `sampleInterval` value.
- Parameter `typeWitness`: The `typeWitness` value.
- Parameter `bit`: The `bit` value.
- Parameter `scheduler`: The `scheduler` value.
- Returns: A `System.IObservable<T>` result.

###### `M:IoT.DriverCore.ABPlcRx.ABPlcSimulator.Observe``1(System.String,``0,System.Int32)`

```csharp
public System.IObservable<T> Observe<T>(string variable, T typeWitness, int bit)
```
Inherits XML documentation from its implemented or overridden member.

- Parameter `variable`: The `variable` value.
- Parameter `typeWitness`: The `typeWitness` value.
- Parameter `bit`: The `bit` value.
- Returns: A `System.IObservable<T>` result.

###### `M:IoT.DriverCore.ABPlcRx.ABPlcSimulator.Ping(System.Boolean)`

```csharp
public bool Ping(bool echo)
```
Inherits XML documentation from its implemented or overridden member.

- Parameter `echo`: The `echo` value.
- Returns: A `bool` result.

###### `M:IoT.DriverCore.ABPlcRx.ABPlcSimulator.PingAsync(System.Boolean,System.Threading.CancellationToken)`

```csharp
public System.Threading.Tasks.Task<bool> PingAsync(bool echo, System.Threading.CancellationToken cancellationToken)
```
Inherits XML documentation from its implemented or overridden member.

- Parameter `echo`: The `echo` value.
- Parameter `cancellationToken`: The `cancellationToken` value.
- Returns: A `System.Threading.Tasks.Task<bool>` result.

###### `M:IoT.DriverCore.ABPlcRx.ABPlcSimulator.QueueFault(IoT.DriverCore.ABPlcRx.ABPlcSimulatorOperation,System.Int32)`

```csharp
public void QueueFault(IoT.DriverCore.ABPlcRx.ABPlcSimulatorOperation operation, int statusCode)
```
Queues a libplctag-compatible result for a future matching operation.

- Parameter `operation`: The operation to fault.
- Parameter `statusCode`: The status to return.

###### `M:IoT.DriverCore.ABPlcRx.ABPlcSimulator.QueueFault(IoT.DriverCore.ABPlcRx.ABPlcSimulatorOperation,System.Int32,System.Int32)`

```csharp
public void QueueFault(IoT.DriverCore.ABPlcRx.ABPlcSimulatorOperation operation, int statusCode, int repeatCount)
```
Queues a repeated libplctag-compatible result for future matching operations.

- Parameter `operation`: The operation to fault.
- Parameter `statusCode`: The status to return.
- Parameter `repeatCount`: The number of matching operations affected.

###### `M:IoT.DriverCore.ABPlcRx.ABPlcSimulator.QueueFault(IoT.DriverCore.ABPlcRx.ABPlcSimulatorOperation,System.Int32,System.Int32,System.String)`

```csharp
public void QueueFault(IoT.DriverCore.ABPlcRx.ABPlcSimulatorOperation operation, int statusCode, int repeatCount, string tagName)
```
Queues a libplctag-compatible result for a future matching operation.

- Parameter `operation`: The operation to fault.
- Parameter `statusCode`: The status to return.
- Parameter `repeatCount`: The number of matching operations affected.
- Parameter `tagName`: Optional physical tag-name filter.

###### `M:IoT.DriverCore.ABPlcRx.ABPlcSimulator.Read`

```csharp
public System.Collections.Generic.IEnumerable<IoT.DriverCore.ABPlcRx.PlcTagResult> Read()
```
Inherits XML documentation from its implemented or overridden member.

- Returns: A `System.Collections.Generic.IEnumerable<IoT.DriverCore.ABPlcRx.PlcTagResult>` result.

###### `M:IoT.DriverCore.ABPlcRx.ABPlcSimulator.Read(System.String)`

```csharp
public IoT.DriverCore.ABPlcRx.PlcTagResult Read(string variable)
```
Inherits XML documentation from its implemented or overridden member.

- Parameter `variable`: The `variable` value.
- Returns: A `IoT.DriverCore.ABPlcRx.PlcTagResult` result.

###### `M:IoT.DriverCore.ABPlcRx.ABPlcSimulator.ReadManyAsync(System.Collections.Generic.IReadOnlyCollection`1{System.String},System.Threading.CancellationToken)`

```csharp
public System.Threading.Tasks.Task<System.Collections.Generic.IReadOnlyList<IoT.DriverCore.ABPlcRx.PlcTagResult>> ReadManyAsync(System.Collections.Generic.IReadOnlyCollection<string> variables, System.Threading.CancellationToken cancellationToken)
```
Executes the `ReadManyAsync` operation.

- Parameter `variables`: The `variables` value.
- Parameter `cancellationToken`: The `cancellationToken` value.
- Returns: A `System.Threading.Tasks.Task<System.Collections.Generic.IReadOnlyList<IoT.DriverCore.ABPlcRx.PlcTagResult>>` result.

###### `M:IoT.DriverCore.ABPlcRx.ABPlcSimulator.ReadValueAsync``1(System.String,``0,System.Int32,System.Threading.CancellationToken)`

```csharp
public System.Threading.Tasks.Task<IoT.DriverCore.Core.TagOperationResult<T>> ReadValueAsync<T>(string variable, T typeWitness, int bit, System.Threading.CancellationToken cancellationToken)
```
Inherits XML documentation from its implemented or overridden member.

- Parameter `variable`: The `variable` value.
- Parameter `typeWitness`: The `typeWitness` value.
- Parameter `bit`: The `bit` value.
- Parameter `cancellationToken`: The `cancellationToken` value.
- Returns: A `System.Threading.Tasks.Task<IoT.DriverCore.Core.TagOperationResult<T>>` result.

###### `M:IoT.DriverCore.ABPlcRx.ABPlcSimulator.Reconnect`

```csharp
public void Reconnect()
```
Reconnects simulated communications without losing device memory or registrations.

###### `M:IoT.DriverCore.ABPlcRx.ABPlcSimulator.RemoveTagItem(System.String)`

```csharp
public bool RemoveTagItem(string variable)
```
Inherits XML documentation from its implemented or overridden member.

- Parameter `variable`: The `variable` value.
- Returns: A `bool` result.

###### `M:IoT.DriverCore.ABPlcRx.ABPlcSimulator.SetTagBytes(System.String,System.Collections.Generic.IReadOnlyCollection`1{System.Byte})`

```csharp
public void SetTagBytes(string tagName, System.Collections.Generic.IReadOnlyCollection<byte> value)
```
Executes the `SetTagBytes` operation.

- Parameter `tagName`: The `tagName` value.
- Parameter `value`: The `value` value.

###### `M:IoT.DriverCore.ABPlcRx.ABPlcSimulator.SetTagValue``1(System.String,``0)`

```csharp
public void SetTagValue<T>(string tagName, T value)
```
Seeds or updates a supported scalar value in device memory.

- Parameter `tagName`: The physical PLC tag name.
- Parameter `value`: The value to encode.

###### `M:IoT.DriverCore.ABPlcRx.ABPlcSimulator.Value``1(System.String,``0,System.Int32)`

```csharp
public void Value<T>(string variable, T value, int bit)
```
Inherits XML documentation from its implemented or overridden member.

- Parameter `variable`: The `variable` value.
- Parameter `value`: The `value` value.
- Parameter `bit`: The `bit` value.

###### `M:IoT.DriverCore.ABPlcRx.ABPlcSimulator.Write`

```csharp
public System.Collections.Generic.IEnumerable<IoT.DriverCore.ABPlcRx.PlcTagResult> Write()
```
Inherits XML documentation from its implemented or overridden member.

- Returns: A `System.Collections.Generic.IEnumerable<IoT.DriverCore.ABPlcRx.PlcTagResult>` result.

###### `M:IoT.DriverCore.ABPlcRx.ABPlcSimulator.Write(System.String)`

```csharp
public IoT.DriverCore.ABPlcRx.PlcTagResult Write(string variable)
```
Inherits XML documentation from its implemented or overridden member.

- Parameter `variable`: The `variable` value.
- Returns: A `IoT.DriverCore.ABPlcRx.PlcTagResult` result.

###### `M:IoT.DriverCore.ABPlcRx.ABPlcSimulator.WriteManyAsync(System.Collections.Generic.IReadOnlyDictionary`2{System.String,System.Object},System.Threading.CancellationToken)`

```csharp
public System.Threading.Tasks.Task<System.Collections.Generic.IReadOnlyList<IoT.DriverCore.ABPlcRx.PlcTagResult>> WriteManyAsync(System.Collections.Generic.IReadOnlyDictionary<string, object> values, System.Threading.CancellationToken cancellationToken)
```
Executes the `WriteManyAsync` operation.

- Parameter `values`: The `values` value.
- Parameter `cancellationToken`: The `cancellationToken` value.
- Returns: A `System.Threading.Tasks.Task<System.Collections.Generic.IReadOnlyList<IoT.DriverCore.ABPlcRx.PlcTagResult>>` result.

###### `M:IoT.DriverCore.ABPlcRx.ABPlcSimulator.WriteValueAsync``1(System.String,``0,System.Int32,System.Threading.CancellationToken)`

```csharp
public System.Threading.Tasks.Task<IoT.DriverCore.Core.TagOperationResult<T>> WriteValueAsync<T>(string variable, T value, int bit, System.Threading.CancellationToken cancellationToken)
```
Inherits XML documentation from its implemented or overridden member.

- Parameter `variable`: The `variable` value.
- Parameter `value`: The `value` value.
- Parameter `bit`: The `bit` value.
- Parameter `cancellationToken`: The `cancellationToken` value.
- Returns: A `System.Threading.Tasks.Task<IoT.DriverCore.Core.TagOperationResult<T>>` result.

###### `P:IoT.DriverCore.ABPlcRx.ABPlcSimulator.ActiveHandleCount`

```csharp
public int ActiveHandleCount { get; }
```
Gets the number of live tag handles.

- Value: The `ActiveHandleCount` value.

###### `P:IoT.DriverCore.ABPlcRx.ABPlcSimulator.AutoWriteValue`

```csharp
public bool AutoWriteValue { get; set; }
```
Inherits XML documentation from its implemented or overridden member.

- Value: The `AutoWriteValue` value.

###### `P:IoT.DriverCore.ABPlcRx.ABPlcSimulator.ConnectionChanged`

```csharp
public System.IObservable<bool> ConnectionChanged { get; }
```
Gets connection-state changes. The current state is emitted on subscription.

- Value: The `ConnectionChanged` value.

###### `P:IoT.DriverCore.ABPlcRx.ABPlcSimulator.IsConnected`

```csharp
public bool IsConnected { get; }
```
Gets a value indicating whether simulated communications are connected.

- Value: The `IsConnected` value.

###### `P:IoT.DriverCore.ABPlcRx.ABPlcSimulator.IsDisposed`

```csharp
public bool IsDisposed { get; }
```
Inherits XML documentation from its implemented or overridden member.

- Value: The `IsDisposed` value.

###### `P:IoT.DriverCore.ABPlcRx.ABPlcSimulator.ObserveAll`

```csharp
public System.IObservable<IoT.DriverCore.ABPlcRx.IPlcTag> ObserveAll { get; }
```
Inherits XML documentation from its implemented or overridden member.

- Value: The `ObserveAll` value.

###### `P:IoT.DriverCore.ABPlcRx.ABPlcSimulator.ObserveAllAsyncObservable`

```csharp
public ReactiveUI.Primitives.Async.IObservableAsync<IoT.DriverCore.ABPlcRx.IPlcTag> ObserveAllAsyncObservable { get; }
```
Inherits XML documentation from its implemented or overridden member.

- Value: The `ObserveAllAsyncObservable` value.

###### `P:IoT.DriverCore.ABPlcRx.ABPlcSimulator.OperationLog`

```csharp
public System.Collections.Generic.IReadOnlyList<IoT.DriverCore.ABPlcRx.ABPlcSimulatorLogEntry> OperationLog { get; }
```
Gets a stable snapshot of recorded simulator operations.

- Value: The `OperationLog` value.

###### `P:IoT.DriverCore.ABPlcRx.ABPlcSimulator.OperationMetrics`

```csharp
public IoT.DriverCore.ABPlcRx.ABPlcSimulatorOperationMetrics OperationMetrics { get; }
```
Gets exact native-operation counts without relying on wall-clock timings.

- Value: The `OperationMetrics` value.

###### `P:IoT.DriverCore.ABPlcRx.ABPlcSimulator.ScanEnabled`

```csharp
public bool ScanEnabled { get; set; }
```
Inherits XML documentation from its implemented or overridden member.

- Value: The `ScanEnabled` value.

###### `P:IoT.DriverCore.ABPlcRx.ABPlcSimulator.TagStatuses`

```csharp
public System.Collections.Generic.IReadOnlyDictionary<string, int> TagStatuses { get; }
```
Gets the latest operation status for every physical PLC tag.

- Value: The `TagStatuses` value.

#### `T:IoT.DriverCore.ABPlcRx.ABPlcSimulatorLogEntry`

```csharp
public class IoT.DriverCore.ABPlcRx.ABPlcSimulatorLogEntry
```
One deterministic simulator operation record.

##### Declared public members

###### `M:IoT.DriverCore.ABPlcRx.ABPlcSimulatorLogEntry.ToString`

```csharp
public string ToString()
```
Inherits XML documentation from its implemented or overridden member.

- Returns: A `string` result.

###### `P:IoT.DriverCore.ABPlcRx.ABPlcSimulatorLogEntry.Handle`

```csharp
public int Handle { get; }
```
Gets the native-style handle.

- Value: The `Handle` value.

###### `P:IoT.DriverCore.ABPlcRx.ABPlcSimulatorLogEntry.Operation`

```csharp
public IoT.DriverCore.ABPlcRx.ABPlcSimulatorOperation Operation { get; }
```
Gets the operation.

- Value: The `Operation` value.

###### `P:IoT.DriverCore.ABPlcRx.ABPlcSimulatorLogEntry.Sequence`

```csharp
public long Sequence { get; }
```
Gets the monotonic operation sequence.

- Value: The `Sequence` value.

###### `P:IoT.DriverCore.ABPlcRx.ABPlcSimulatorLogEntry.StatusCode`

```csharp
public int StatusCode { get; }
```
Gets the resulting libplctag-compatible status.

- Value: The `StatusCode` value.

###### `P:IoT.DriverCore.ABPlcRx.ABPlcSimulatorLogEntry.TagName`

```csharp
public string TagName { get; }
```
Gets the physical PLC tag name, when known.

- Value: The `TagName` value.

###### `P:IoT.DriverCore.ABPlcRx.ABPlcSimulatorLogEntry.Timestamp`

```csharp
public System.DateTimeOffset Timestamp { get; }
```
Gets the operation timestamp.

- Value: The `Timestamp` value.

#### `T:IoT.DriverCore.ABPlcRx.ABPlcSimulatorOperation`

```csharp
public enum IoT.DriverCore.ABPlcRx.ABPlcSimulatorOperation
```
Operations that can be recorded or faulted by `T:IoT.DriverCore.ABPlcRx.ABPlcSimulator` .

##### Declared public members

###### `F:IoT.DriverCore.ABPlcRx.ABPlcSimulatorOperation.Abort`

```csharp
public static const IoT.DriverCore.ABPlcRx.ABPlcSimulatorOperation Abort
```
Abort outstanding tag IO.

###### `F:IoT.DriverCore.ABPlcRx.ABPlcSimulatorOperation.Create`

```csharp
public static const IoT.DriverCore.ABPlcRx.ABPlcSimulatorOperation Create
```
Create a tag handle.

###### `F:IoT.DriverCore.ABPlcRx.ABPlcSimulatorOperation.Destroy`

```csharp
public static const IoT.DriverCore.ABPlcRx.ABPlcSimulatorOperation Destroy
```
Destroy a tag handle.

###### `F:IoT.DriverCore.ABPlcRx.ABPlcSimulatorOperation.GetStatus`

```csharp
public static const IoT.DriverCore.ABPlcRx.ABPlcSimulatorOperation GetStatus
```
Query tag status.

###### `F:IoT.DriverCore.ABPlcRx.ABPlcSimulatorOperation.Lock`

```csharp
public static const IoT.DriverCore.ABPlcRx.ABPlcSimulatorOperation Lock
```
Lock a tag handle.

###### `F:IoT.DriverCore.ABPlcRx.ABPlcSimulatorOperation.Read`

```csharp
public static const IoT.DriverCore.ABPlcRx.ABPlcSimulatorOperation Read
```
Read device memory into a tag handle.

###### `F:IoT.DriverCore.ABPlcRx.ABPlcSimulatorOperation.Unlock`

```csharp
public static const IoT.DriverCore.ABPlcRx.ABPlcSimulatorOperation Unlock
```
Unlock a tag handle.

###### `F:IoT.DriverCore.ABPlcRx.ABPlcSimulatorOperation.Write`

```csharp
public static const IoT.DriverCore.ABPlcRx.ABPlcSimulatorOperation Write
```
Write a tag handle into device memory.

#### `T:IoT.DriverCore.ABPlcRx.ABPlcSimulatorOperationMetrics`

```csharp
public class IoT.DriverCore.ABPlcRx.ABPlcSimulatorOperationMetrics
```
Immutable, deterministic native-operation counts captured by an `T:IoT.DriverCore.ABPlcRx.ABPlcSimulator` .

##### Declared public members

###### `P:IoT.DriverCore.ABPlcRx.ABPlcSimulatorOperationMetrics.CreateOperations`

```csharp
public long CreateOperations { get; }
```
Gets the number of create operations.

- Value: The `CreateOperations` value.

###### `P:IoT.DriverCore.ABPlcRx.ABPlcSimulatorOperationMetrics.DestroyOperations`

```csharp
public long DestroyOperations { get; }
```
Gets the number of destroy operations.

- Value: The `DestroyOperations` value.

###### `P:IoT.DriverCore.ABPlcRx.ABPlcSimulatorOperationMetrics.FailedOperations`

```csharp
public long FailedOperations { get; }
```
Gets the number of non-success native operations.

- Value: The `FailedOperations` value.

###### `P:IoT.DriverCore.ABPlcRx.ABPlcSimulatorOperationMetrics.ReadOperations`

```csharp
public long ReadOperations { get; }
```
Gets the number of native reads.

- Value: The `ReadOperations` value.

###### `P:IoT.DriverCore.ABPlcRx.ABPlcSimulatorOperationMetrics.TotalOperations`

```csharp
public long TotalOperations { get; }
```
Gets the number of native operations recorded.

- Value: The `TotalOperations` value.

###### `P:IoT.DriverCore.ABPlcRx.ABPlcSimulatorOperationMetrics.WriteOperations`

```csharp
public long WriteOperations { get; }
```
Gets the number of native writes.

- Value: The `WriteOperations` value.

#### `T:IoT.DriverCore.ABPlcRx.IABPlcRx`

```csharp
public interface IoT.DriverCore.ABPlcRx.IABPlcRx
```
Reactive Allen Bradley PLC facade contract.

##### Declared public members

###### `M:IoT.DriverCore.ABPlcRx.IABPlcRx.AddUpdateTagItem``1(System.String,System.String,System.String,``0)`

```csharp
public void AddUpdateTagItem<T>(string variable, string tagName, string tagGroup, T typeWitness)
```
Adds the update tag item.

- Parameter `variable`: The variable, this can be any non null name you wish to use.
- Parameter `tagName`: Name of the plc tag.
- Parameter `tagGroup`: The tag group.
- Parameter `typeWitness`: Optional type witness for callers that infer from a value.

###### `M:IoT.DriverCore.ABPlcRx.IABPlcRx.AddUpdateTagItem``1(System.String,System.String,``0)`

```csharp
public void AddUpdateTagItem<T>(string variable, string tagName, T typeWitness)
```
Adds the update tag item.

- Parameter `variable`: The variable, this can be any non null name you wish to use.
- Parameter `tagName`: Name of the plc tag.
- Parameter `typeWitness`: Optional type witness for callers that infer from a value.

###### `M:IoT.DriverCore.ABPlcRx.IABPlcRx.AddUpdateTagItem``1(System.String,``0)`

```csharp
public void AddUpdateTagItem<T>(string tagName, T typeWitness)
```
Adds the update tag item.

- Parameter `tagName`: Name of the PLC tag.
- Parameter `typeWitness`: Optional type witness for callers that infer from a value.

###### `M:IoT.DriverCore.ABPlcRx.IABPlcRx.CreateWriter``1(System.String,``0,System.Int32)`

```csharp
public System.IObserver<T> CreateWriter<T>(string variable, T typeWitness, int bit)
```
Creates an observer that writes values to a PLC variable when OnNext is called.

- Parameter `variable`: The variable to write to.
- Parameter `typeWitness`: Type witness for the writer value type.
- Parameter `bit`: The bit [ONLY use for bool tags].
- Returns: An observer that will write and commit values to the PLC.

###### `M:IoT.DriverCore.ABPlcRx.IABPlcRx.GetValue``1(System.String,``0,System.Int32)`

```csharp
public T GetValue<T>(string variable, T typeWitness, int bit)
```
Values the specified variable.

- Parameter `variable`: The variable.
- Parameter `typeWitness`: Type witness for the requested PLC value type.
- Parameter `bit`: The bit.
- Returns: A value of T.

###### `M:IoT.DriverCore.ABPlcRx.IABPlcRx.ObserveAsyncObservable``1(System.String,``0,System.Int32)`

```csharp
public ReactiveUI.Primitives.Async.IObservableAsync<T> ObserveAsyncObservable<T>(string variable, T typeWitness, int bit)
```
Observes the specified variable using an async-native observable.

- Parameter `variable`: The variable.
- Parameter `typeWitness`: Type witness for the requested PLC value type.
- Parameter `bit`: The bit.
- Returns: An async observable sequence of values of type T.

###### `M:IoT.DriverCore.ABPlcRx.IABPlcRx.ObserveErrors`

```csharp
public System.IObservable<IoT.DriverCore.ABPlcRx.PlcTagResult> ObserveErrors()
```
Streams only error results across all tags.

- Returns: Observable sequence of error results.

###### `M:IoT.DriverCore.ABPlcRx.IABPlcRx.ObserveErrorsAsyncObservable`

```csharp
public ReactiveUI.Primitives.Async.IObservableAsync<IoT.DriverCore.ABPlcRx.PlcTagResult> ObserveErrorsAsyncObservable()
```
Streams only error results across all tags using an async-native observable.

- Returns: Async observable sequence of error results.

###### `M:IoT.DriverCore.ABPlcRx.IABPlcRx.ObserveGroup(System.String)`

```csharp
public System.IObservable<IoT.DriverCore.ABPlcRx.IPlcTag> ObserveGroup(string groupName)
```
Observe a PLC tag group, emitting the tag whose value changed.

- Parameter `groupName`: The group name to observe.
- Returns: Observable sequence of tags in the group that have changed.

###### `M:IoT.DriverCore.ABPlcRx.IABPlcRx.ObserveGroupAsyncObservable(System.String)`

```csharp
public ReactiveUI.Primitives.Async.IObservableAsync<IoT.DriverCore.ABPlcRx.IPlcTag> ObserveGroupAsyncObservable(string groupName)
```
Observe a PLC tag group using an async-native observable.

- Parameter `groupName`: The group name to observe.
- Returns: Async observable sequence of tags in the group that have changed.

###### `M:IoT.DriverCore.ABPlcRx.IABPlcRx.ObserveMany(System.String[])`

```csharp
public System.IObservable<System.Collections.Generic.IReadOnlyDictionary<string, object>> ObserveMany(string[] variables)
```
Observe values for many variables and emit a latest-value dictionary.

- Parameter `variables`: One or more variable names to observe.
- Returns: Observable sequence of dictionary containing the latest values for each variable.

###### `M:IoT.DriverCore.ABPlcRx.IABPlcRx.ObserveManyAsyncObservable(System.String[])`

```csharp
public ReactiveUI.Primitives.Async.IObservableAsync<System.Collections.Generic.IReadOnlyDictionary<string, object>> ObserveManyAsyncObservable(string[] variables)
```
Observe values for many variables using an async-native observable.

- Parameter `variables`: One or more variable names to observe.
- Returns: Async observable sequence of dictionary containing the latest values for each variable.

###### `M:IoT.DriverCore.ABPlcRx.IABPlcRx.ObservePing(System.TimeSpan,System.Boolean,ReactiveUI.Primitives.Concurrency.ISequencer)`

```csharp
public System.IObservable<bool> ObservePing(System.TimeSpan interval, bool echo, ReactiveUI.Primitives.Concurrency.ISequencer scheduler)
```
Observe ping results on a schedule.

- Parameter `interval`: The interval between pings.
- Parameter `echo`: True echo result to standard output.
- Parameter `scheduler`: Optional scheduler for the ping cadence.
- Returns: Observable sequence of ping result states, deduplicated.

###### `M:IoT.DriverCore.ABPlcRx.IABPlcRx.ObservePingAsyncObservable(System.TimeSpan,System.Boolean,ReactiveUI.Primitives.Concurrency.ISequencer)`

```csharp
public ReactiveUI.Primitives.Async.IObservableAsync<bool> ObservePingAsyncObservable(System.TimeSpan interval, bool echo, ReactiveUI.Primitives.Concurrency.ISequencer scheduler)
```
Observe ping results on a schedule using an async-native observable.

- Parameter `interval`: The interval between pings.
- Parameter `echo`: True echo result to standard output.
- Parameter `scheduler`: Optional scheduler for the ping cadence.
- Returns: Async observable sequence of ping result states, deduplicated.

###### `M:IoT.DriverCore.ABPlcRx.IABPlcRx.ObserveSampledAsyncObservable``1(System.String,System.TimeSpan,``0,System.Int32,ReactiveUI.Primitives.Concurrency.ISequencer)`

```csharp
public ReactiveUI.Primitives.Async.IObservableAsync<T> ObserveSampledAsyncObservable<T>(string variable, System.TimeSpan sampleInterval, T typeWitness, int bit, ReactiveUI.Primitives.Concurrency.ISequencer scheduler)
```
Observe a variable with sampling using an async-native observable.

- Parameter `variable`: The variable to observe.
- Parameter `sampleInterval`: The sampling interval.
- Parameter `typeWitness`: Type witness for the requested PLC value type.
- Parameter `bit`: The bit [ONLY use for bool tags].
- Parameter `scheduler`: Optional scheduler for sampling.
- Returns: Async observable sequence of sampled values.

###### `M:IoT.DriverCore.ABPlcRx.IABPlcRx.ObserveSampled``1(System.String,System.TimeSpan,``0,System.Int32,ReactiveUI.Primitives.Concurrency.ISequencer)`

```csharp
public System.IObservable<T> ObserveSampled<T>(string variable, System.TimeSpan sampleInterval, T typeWitness, int bit, ReactiveUI.Primitives.Concurrency.ISequencer scheduler)
```
Observe a variable with sampling, reducing event rate while preserving latest value.

- Parameter `variable`: The variable to observe.
- Parameter `sampleInterval`: The sampling interval.
- Parameter `typeWitness`: Type witness for the requested PLC value type.
- Parameter `bit`: The bit [ONLY use for bool tags].
- Parameter `scheduler`: Optional scheduler for sampling.
- Returns: Observable sequence of sampled values.

###### `M:IoT.DriverCore.ABPlcRx.IABPlcRx.Observe``1(System.String,``0,System.Int32)`

```csharp
public System.IObservable<T> Observe<T>(string variable, T typeWitness, int bit)
```
Observes the specified variable.

- Parameter `variable`: The variable.
- Parameter `typeWitness`: Type witness for the requested PLC value type.
- Parameter `bit`: The bit.
- Returns: An observable sequence of values of type T.

###### `M:IoT.DriverCore.ABPlcRx.IABPlcRx.Ping(System.Boolean)`

```csharp
public bool Ping(bool echo)
```
Ping the PLC.

- Parameter `echo`: True echo result to standard output.
- Returns: True when ping succeeds; otherwise, false.

###### `M:IoT.DriverCore.ABPlcRx.IABPlcRx.PingAsync(System.Boolean,System.Threading.CancellationToken)`

```csharp
public System.Threading.Tasks.Task<bool> PingAsync(bool echo, System.Threading.CancellationToken cancellationToken)
```
Ping the PLC asynchronously.

- Parameter `echo`: True echo result to standard output.
- Parameter `cancellationToken`: A token to cancel the ping operation.
- Returns: A task producing true when ping succeeds; otherwise, false.

###### `M:IoT.DriverCore.ABPlcRx.IABPlcRx.Read`

```csharp
public System.Collections.Generic.IEnumerable<IoT.DriverCore.ABPlcRx.PlcTagResult> Read()
```
Reads all tags in this instance.

- Returns: A sequence of PlcTagResult.

###### `M:IoT.DriverCore.ABPlcRx.IABPlcRx.Read(System.String)`

```csharp
public IoT.DriverCore.ABPlcRx.PlcTagResult Read(string variable)
```
Reads the specified variable.

- Parameter `variable`: The variable.
- Returns: A PlcTagResult.

###### `M:IoT.DriverCore.ABPlcRx.IABPlcRx.ReadManyAsync(System.Collections.Generic.IReadOnlyCollection`1{System.String},System.Threading.CancellationToken)`

```csharp
public System.Threading.Tasks.Task<System.Collections.Generic.IReadOnlyList<IoT.DriverCore.ABPlcRx.PlcTagResult>> ReadManyAsync(System.Collections.Generic.IReadOnlyCollection<string> variables, System.Threading.CancellationToken cancellationToken)
```
Executes the `ReadManyAsync` operation.

- Parameter `variables`: The `variables` value.
- Parameter `cancellationToken`: The `cancellationToken` value.
- Returns: A `System.Threading.Tasks.Task<System.Collections.Generic.IReadOnlyList<IoT.DriverCore.ABPlcRx.PlcTagResult>>` result.

###### `M:IoT.DriverCore.ABPlcRx.IABPlcRx.ReadValueAsync``1(System.String,``0,System.Int32,System.Threading.CancellationToken)`

```csharp
public System.Threading.Tasks.Task<IoT.DriverCore.Core.TagOperationResult<T>> ReadValueAsync<T>(string variable, T typeWitness, int bit, System.Threading.CancellationToken cancellationToken)
```
Reads and converts one logical variable asynchronously.

- Parameter `variable`: The logical variable name.
- Parameter `typeWitness`: Type witness for the requested PLC value type.
- Parameter `bit`: The optional integral bit index.
- Parameter `cancellationToken`: A token to cancel the operation.
- Returns: The typed operation result.

###### `M:IoT.DriverCore.ABPlcRx.IABPlcRx.RemoveTagItem(System.String)`

```csharp
public bool RemoveTagItem(string variable)
```
Removes a registered tag by logical variable name.

- Parameter `variable`: The logical variable name.
- Returns: True when a tag was removed.

###### `M:IoT.DriverCore.ABPlcRx.IABPlcRx.Value``1(System.String,``0,System.Int32)`

```csharp
public void Value<T>(string variable, T value, int bit)
```
Values the specified variable.

- Parameter `variable`: The variable.
- Parameter `value`: The value.
- Parameter `bit`: The bit.

###### `M:IoT.DriverCore.ABPlcRx.IABPlcRx.Write`

```csharp
public System.Collections.Generic.IEnumerable<IoT.DriverCore.ABPlcRx.PlcTagResult> Write()
```
Writes all tags in this instance.

- Returns: A sequence of PlcTagResult.

###### `M:IoT.DriverCore.ABPlcRx.IABPlcRx.Write(System.String)`

```csharp
public IoT.DriverCore.ABPlcRx.PlcTagResult Write(string variable)
```
Writes the specified variable.

- Parameter `variable`: The variable.
- Returns: A PlcTagResult.

###### `M:IoT.DriverCore.ABPlcRx.IABPlcRx.WriteManyAsync(System.Collections.Generic.IReadOnlyDictionary`2{System.String,System.Object},System.Threading.CancellationToken)`

```csharp
public System.Threading.Tasks.Task<System.Collections.Generic.IReadOnlyList<IoT.DriverCore.ABPlcRx.PlcTagResult>> WriteManyAsync(System.Collections.Generic.IReadOnlyDictionary<string, object> values, System.Threading.CancellationToken cancellationToken)
```
Executes the `WriteManyAsync` operation.

- Parameter `values`: The `values` value.
- Parameter `cancellationToken`: The `cancellationToken` value.
- Returns: A `System.Threading.Tasks.Task<System.Collections.Generic.IReadOnlyList<IoT.DriverCore.ABPlcRx.PlcTagResult>>` result.

###### `M:IoT.DriverCore.ABPlcRx.IABPlcRx.WriteValueAsync``1(System.String,``0,System.Int32,System.Threading.CancellationToken)`

```csharp
public System.Threading.Tasks.Task<IoT.DriverCore.Core.TagOperationResult<T>> WriteValueAsync<T>(string variable, T value, int bit, System.Threading.CancellationToken cancellationToken)
```
Writes one logical variable asynchronously.

- Parameter `variable`: The logical variable name.
- Parameter `value`: The value to write.
- Parameter `bit`: The optional integral bit index.
- Parameter `cancellationToken`: A token to cancel the operation.
- Returns: The typed operation result.

###### `P:IoT.DriverCore.ABPlcRx.IABPlcRx.AutoWriteValue`

```csharp
public bool AutoWriteValue { get; set; }
```
Gets or sets a value indicating whether [automatic write value].

- Value: true if [automatic write value]; otherwise, false .

###### `P:IoT.DriverCore.ABPlcRx.IABPlcRx.IsDisposed`

```csharp
public bool IsDisposed { get; }
```
Gets a value indicating whether the object is disposed.

- Value: The `IsDisposed` value.

###### `P:IoT.DriverCore.ABPlcRx.IABPlcRx.ObserveAll`

```csharp
public System.IObservable<IoT.DriverCore.ABPlcRx.IPlcTag> ObserveAll { get; }
```
Gets the observe all.

- Value: The observe all.

###### `P:IoT.DriverCore.ABPlcRx.IABPlcRx.ObserveAllAsyncObservable`

```csharp
public ReactiveUI.Primitives.Async.IObservableAsync<IoT.DriverCore.ABPlcRx.IPlcTag> ObserveAllAsyncObservable { get; }
```
Gets the asynchronous observe all stream.

- Value: The asynchronous observe all stream.

###### `P:IoT.DriverCore.ABPlcRx.IABPlcRx.ScanEnabled`

```csharp
public bool ScanEnabled { get; set; }
```
Gets or sets a value indicating whether [scan enabled].

- Value: true if [scan enabled]; otherwise, false .

#### `T:IoT.DriverCore.ABPlcRx.IPlcTag`

```csharp
public interface IoT.DriverCore.ABPlcRx.IPlcTag
```
Interface Tag.

##### Declared public members

###### `M:IoT.DriverCore.ABPlcRx.IPlcTag.Abort`

```csharp
public int Abort()
```
Abort any outstanding IO to the PLC. `T:IoT.DriverCore.ABPlcRx.PlcTagStatus` .

- Returns: A Value.

###### `M:IoT.DriverCore.ABPlcRx.IPlcTag.GetSize`

```csharp
public int GetSize()
```
Get size tag.

- Returns: A Value.

###### `M:IoT.DriverCore.ABPlcRx.IPlcTag.GetStatus`

```csharp
public int GetStatus()
```
Get status operation. `T:IoT.DriverCore.ABPlcRx.PlcTagStatus` .

- Returns: A Value.

###### `M:IoT.DriverCore.ABPlcRx.IPlcTag.Lock`

```csharp
public int Lock()
```
Lock for multitrading. `T:IoT.DriverCore.ABPlcRx.PlcTagStatus` .

- Returns: A Value.

###### `M:IoT.DriverCore.ABPlcRx.IPlcTag.Read`

```csharp
public IoT.DriverCore.ABPlcRx.PlcTagResult Read()
```
Performs read of Tag.

- Returns: A Value.

###### `M:IoT.DriverCore.ABPlcRx.IPlcTag.Unlock`

```csharp
public int Unlock()
```
Unlock for multitrading `T:IoT.DriverCore.ABPlcRx.PlcTagStatus` .

- Returns: A Value.

###### `M:IoT.DriverCore.ABPlcRx.IPlcTag.Write`

```csharp
public IoT.DriverCore.ABPlcRx.PlcTagResult Write()
```
Perform write of Tag.

- Returns: A Value.

###### `P:IoT.DriverCore.ABPlcRx.IPlcTag.Changed`

```csharp
public System.IObservable<IoT.DriverCore.ABPlcRx.PlcTagResult> Changed { get; }
```
Gets the changed.

- Value: The changed.

###### `P:IoT.DriverCore.ABPlcRx.IPlcTag.Handle`

```csharp
public int Handle { get; }
```
Gets handle creation Tag.

- Value: The `Handle` value.

###### `P:IoT.DriverCore.ABPlcRx.IPlcTag.IsRead`

```csharp
public bool IsRead { get; }
```
Gets a value indicating whether indicates whether or not a value must be read from the PLC.

- Value: The `IsRead` value.

###### `P:IoT.DriverCore.ABPlcRx.IPlcTag.IsWrite`

```csharp
public bool IsWrite { get; }
```
Gets a value indicating whether indicates whether or not a value must be write to the PLC.

- Value: The `IsWrite` value.

###### `P:IoT.DriverCore.ABPlcRx.IPlcTag.Length`

```csharp
public int Length { get; }
```
Gets elements length: 1- single, n-array.

- Value: The `Length` value.

###### `P:IoT.DriverCore.ABPlcRx.IPlcTag.ReadOnly`

```csharp
public bool ReadOnly { get; set; }
```
Gets or sets whether the tag is read-only.

- Value: The `ReadOnly` value.

###### `P:IoT.DriverCore.ABPlcRx.IPlcTag.Size`

```csharp
public int Size { get; }
```
Gets the size of an element in bytes. The tag is assumed to be composed of elements of the same size. For structure tags, use the total size of the structure.

- Value: The `Size` value.

###### `P:IoT.DriverCore.ABPlcRx.IPlcTag.TagName`

```csharp
public string TagName { get; }
```
Gets the textual name of the tag to access. The name is anything allowed by the protocol. E.g. myDataStruct.rotationTimer.ACC, myDINTArray[42] etc.

- Value: The `TagName` value.

###### `P:IoT.DriverCore.ABPlcRx.IPlcTag.TypeValue`

```csharp
public System.Type TypeValue { get; }
```
Gets type value.

- Value: The `TypeValue` value.

###### `P:IoT.DriverCore.ABPlcRx.IPlcTag.Value`

```csharp
public object Value { get; set; }
```
Gets or sets value tag.

- Value: The `Value` value.

###### `P:IoT.DriverCore.ABPlcRx.IPlcTag.ValueManager`

```csharp
public IoT.DriverCore.ABPlcRx.PlcTagWrapper ValueManager { get; }
```
Gets value manager.

- Value: The `ValueManager` value.

###### `P:IoT.DriverCore.ABPlcRx.IPlcTag.Variable`

```csharp
public string Variable { get; }
```
Gets the key.

- Value: The key.

#### `T:IoT.DriverCore.ABPlcRx.IPlcTag`1`

```csharp
public interface IoT.DriverCore.ABPlcRx.IPlcTag`1
```
Interface Tag.

##### Declared public members

###### `P:IoT.DriverCore.ABPlcRx.IPlcTag`1.Value`

```csharp
public TType Value { get; set; }
```
Gets or sets the value.

- Value: The value.

#### `T:IoT.DriverCore.ABPlcRx.ObservableAsyncBridgeExtensions`

```csharp
public class IoT.DriverCore.ABPlcRx.ObservableAsyncBridgeExtensions
```
Bridges synchronous observable streams to ReactiveUI.Primitives async observables.

##### Declared public members

###### `M:IoT.DriverCore.ABPlcRx.ObservableAsyncBridgeExtensions.ToAsyncObservable``1(System.IObservable`1{``0})`

```csharp
public static ReactiveUI.Primitives.Async.IObservableAsync<T> ToAsyncObservable<T>(System.IObservable<T> source)
```
Executes the `ToAsyncObservable` operation.

- Parameter `source`: The `source` value.
- Returns: A `ReactiveUI.Primitives.Async.IObservableAsync<T>` result.

#### `T:IoT.DriverCore.ABPlcRx.PlcTagException`

```csharp
public class IoT.DriverCore.ABPlcRx.PlcTagException
```
Plc Tag Exception.

##### Declared public members

###### `M:IoT.DriverCore.ABPlcRx.PlcTagException.#ctor`

```csharp
public IoT.DriverCore.ABPlcRx.PlcTagException()
```
Initializes a new instance of the `T:IoT.DriverCore.ABPlcRx.PlcTagException` class.

###### `M:IoT.DriverCore.ABPlcRx.PlcTagException.#ctor(IoT.DriverCore.ABPlcRx.PlcTagResult)`

```csharp
public IoT.DriverCore.ABPlcRx.PlcTagException(IoT.DriverCore.ABPlcRx.PlcTagResult result)
```
Initializes a new instance of the `T:IoT.DriverCore.ABPlcRx.PlcTagException` class.

- Parameter `result`: The PLC tag result that caused the exception.

###### `M:IoT.DriverCore.ABPlcRx.PlcTagException.#ctor(System.String)`

```csharp
public IoT.DriverCore.ABPlcRx.PlcTagException(string message)
```
Initializes a new instance of the `T:IoT.DriverCore.ABPlcRx.PlcTagException` class.

- Parameter `message`: The exception message.

###### `M:IoT.DriverCore.ABPlcRx.PlcTagException.#ctor(System.String,System.Exception)`

```csharp
public IoT.DriverCore.ABPlcRx.PlcTagException(string message, System.Exception innerException)
```
Initializes a new instance of the `T:IoT.DriverCore.ABPlcRx.PlcTagException` class.

- Parameter `message`: The exception message.
- Parameter `innerException`: The inner exception.

###### `P:IoT.DriverCore.ABPlcRx.PlcTagException.Result`

```csharp
public IoT.DriverCore.ABPlcRx.PlcTagResult Result { get; }
```
Gets result operation.

- Value: ResultOperation.

#### `T:IoT.DriverCore.ABPlcRx.PlcTagResult`

```csharp
public class IoT.DriverCore.ABPlcRx.PlcTagResult
```
Result returned by PLC tag operations.

##### Declared public members

###### `M:IoT.DriverCore.ABPlcRx.PlcTagResult.Reduce(System.Collections.Generic.IEnumerable`1{IoT.DriverCore.ABPlcRx.PlcTagResult})`

```csharp
public static IoT.DriverCore.ABPlcRx.PlcTagResult Reduce(System.Collections.Generic.IEnumerable<IoT.DriverCore.ABPlcRx.PlcTagResult> results)
```
Executes the `Reduce` operation.

- Parameter `results`: The `results` value.
- Returns: A `IoT.DriverCore.ABPlcRx.PlcTagResult` result.

###### `M:IoT.DriverCore.ABPlcRx.PlcTagResult.Reduce(System.Collections.Generic.IEnumerable`1{IoT.DriverCore.ABPlcRx.PlcTagResult},System.TimeProvider)`

```csharp
public static IoT.DriverCore.ABPlcRx.PlcTagResult Reduce(System.Collections.Generic.IEnumerable<IoT.DriverCore.ABPlcRx.PlcTagResult> results, System.TimeProvider timeProvider)
```
Executes the `Reduce` operation.

- Parameter `results`: The `results` value.
- Parameter `timeProvider`: The `timeProvider` value.
- Returns: A `IoT.DriverCore.ABPlcRx.PlcTagResult` result.

###### `M:IoT.DriverCore.ABPlcRx.PlcTagResult.ToString`

```csharp
public string ToString()
```
Information result.

- Returns: A Value.

###### `P:IoT.DriverCore.ABPlcRx.PlcTagResult.ExecutionTime`

```csharp
public long ExecutionTime { get; }
```
Gets millisecond execution operatorion.

- Value: The execution time.

###### `P:IoT.DriverCore.ABPlcRx.PlcTagResult.StatusCode`

```csharp
public int StatusCode { get; }
```
Gets the `T:IoT.DriverCore.ABPlcRx.PlcTagStatus` code; STATUS_OK indicates success.

- Value: The status code.

###### `P:IoT.DriverCore.ABPlcRx.PlcTagResult.Tag`

```csharp
public IoT.DriverCore.ABPlcRx.IPlcTag Tag { get; }
```
Gets tag.

- Value: The tag.

###### `P:IoT.DriverCore.ABPlcRx.PlcTagResult.Timestamp`

```csharp
public System.DateTimeOffset Timestamp { get; }
```
Gets timestamp last operation.

- Value: The timestamp.

#### `T:IoT.DriverCore.ABPlcRx.PlcTagStatus`

```csharp
public class IoT.DriverCore.ABPlcRx.PlcTagStatus
```
Status code operation.

##### Declared public members

###### `F:IoT.DriverCore.ABPlcRx.PlcTagStatus.ErrBadConfig`

```csharp
public static int ErrBadConfig
```
The operation failed due to incorrect remote-system configuration.

###### `F:IoT.DriverCore.ABPlcRx.PlcTagStatus.ErrBadConnection`

```csharp
public static int ErrBadConnection
```
The connection failed, for example because the remote PLC was power cycled.

###### `F:IoT.DriverCore.ABPlcRx.PlcTagStatus.ErrBadData`

```csharp
public static int ErrBadData
```
The data received from the remote PLC was undecipherable or otherwise not able to be processed. Can also be returned from a remote system that cannot process the data sent to it.

###### `F:IoT.DriverCore.ABPlcRx.PlcTagStatus.ErrBadDevice`

```csharp
public static int ErrBadDevice
```
Usually returned from a remote system when something addressed does not exist.

###### `F:IoT.DriverCore.ABPlcRx.PlcTagStatus.ErrBadGateway`

```csharp
public static int ErrBadGateway
```
Usually returned when the library is unable to connect to a remote system.

###### `F:IoT.DriverCore.ABPlcRx.PlcTagStatus.ErrBadParam`

```csharp
public static int ErrBadParam
```
A common error return when something is not correct with the tag creation attribute string.

###### `F:IoT.DriverCore.ABPlcRx.PlcTagStatus.ErrBadReply`

```csharp
public static int ErrBadReply
```
Usually returned when the remote system returned an unexpected response.

###### `F:IoT.DriverCore.ABPlcRx.PlcTagStatus.ErrBadStatus`

```csharp
public static int ErrBadStatus
```
Usually returned by a remote system when something is not in a good state.

###### `F:IoT.DriverCore.ABPlcRx.PlcTagStatus.ErrClose`

```csharp
public static int ErrClose
```
An error occurred trying to close some resource.

###### `F:IoT.DriverCore.ABPlcRx.PlcTagStatus.ErrCreate`

```csharp
public static int ErrCreate
```
An error occurred trying to create some internal resource.

###### `F:IoT.DriverCore.ABPlcRx.PlcTagStatus.ErrDuplicate`

```csharp
public static int ErrDuplicate
```
A remote-system error caused by a duplicate value, such as a connection ID.

###### `F:IoT.DriverCore.ABPlcRx.PlcTagStatus.ErrEncode`

```csharp
public static int ErrEncode
```
An error was returned when trying to encode some data such as a tag name.

###### `F:IoT.DriverCore.ABPlcRx.PlcTagStatus.ErrErrAbort`

```csharp
public static int ErrErrAbort
```
The operation was aborted.

###### `F:IoT.DriverCore.ABPlcRx.PlcTagStatus.ErrMutexDestroy`

```csharp
public static int ErrMutexDestroy
```
An internal library error that should be very unusual to see.

###### `F:IoT.DriverCore.ABPlcRx.PlcTagStatus.ErrMutexInit`

```csharp
public static int ErrMutexInit
```
An internal library error that should be very unusual to see.

###### `F:IoT.DriverCore.ABPlcRx.PlcTagStatus.ErrMutexLock`

```csharp
public static int ErrMutexLock
```
An internal library error that should be very unusual to see.

###### `F:IoT.DriverCore.ABPlcRx.PlcTagStatus.ErrMutexUnlock`

```csharp
public static int ErrMutexUnlock
```
An internal library error that should be very unusual to see.

###### `F:IoT.DriverCore.ABPlcRx.PlcTagStatus.ErrNoData`

```csharp
public static int ErrNoData
```
Returned when expected data is not present.

###### `F:IoT.DriverCore.ABPlcRx.PlcTagStatus.ErrNoMatch`

```csharp
public static int ErrNoMatch
```
Similar to NOT_FOUND.

###### `F:IoT.DriverCore.ABPlcRx.PlcTagStatus.ErrNoMem`

```csharp
public static int ErrNoMem
```
Returned by the library when memory allocation fails.

###### `F:IoT.DriverCore.ABPlcRx.PlcTagStatus.ErrNoResources`

```csharp
public static int ErrNoResources
```
Returned by the remote system when some resource allocation fails.

###### `F:IoT.DriverCore.ABPlcRx.PlcTagStatus.ErrNotAllowed`

```csharp
public static int ErrNotAllowed
```
Often returned from the remote system when an operation is not permitted.

###### `F:IoT.DriverCore.ABPlcRx.PlcTagStatus.ErrNotFound`

```csharp
public static int ErrNotFound
```
Often returned from the remote system when something is not found.

###### `F:IoT.DriverCore.ABPlcRx.PlcTagStatus.ErrNotImplemented`

```csharp
public static int ErrNotImplemented
```
Returned when a valid operation is not implemented.

###### `F:IoT.DriverCore.ABPlcRx.PlcTagStatus.ErrNullPtr`

```csharp
public static int ErrNullPtr
```
An internal error that can also indicate an invalid API handle.

###### `F:IoT.DriverCore.ABPlcRx.PlcTagStatus.ErrOpen`

```csharp
public static int ErrOpen
```
Returned when an error occurs opening a resource such as a socket.

###### `F:IoT.DriverCore.ABPlcRx.PlcTagStatus.ErrOutOfBounds`

```csharp
public static int ErrOutOfBounds
```
Usually returned when trying to write a value into a tag outside of the tag data bounds.

###### `F:IoT.DriverCore.ABPlcRx.PlcTagStatus.ErrRead`

```csharp
public static int ErrRead
```
Returned when an error occurs during a read operation, usually related to socket problems.

###### `F:IoT.DriverCore.ABPlcRx.PlcTagStatus.ErrRemoteErr`

```csharp
public static int ErrRemoteErr
```
An unspecified or untranslatable remote error causes this.

###### `F:IoT.DriverCore.ABPlcRx.PlcTagStatus.ErrThreadCreate`

```csharp
public static int ErrThreadCreate
```
An internal library error. If you see this, it is likely that everything is about to crash.

###### `F:IoT.DriverCore.ABPlcRx.PlcTagStatus.ErrThreadJoin`

```csharp
public static int ErrThreadJoin
```
Another internal library error that should be very unlikely to see.

###### `F:IoT.DriverCore.ABPlcRx.PlcTagStatus.ErrTimeout`

```csharp
public static int ErrTimeout
```
An operation took too long and timed out.

###### `F:IoT.DriverCore.ABPlcRx.PlcTagStatus.ErrTooLarge`

```csharp
public static int ErrTooLarge
```
More data was returned than was expected.

###### `F:IoT.DriverCore.ABPlcRx.PlcTagStatus.ErrTooSmall`

```csharp
public static int ErrTooSmall
```
Insufficient data was returned from the remote system.

###### `F:IoT.DriverCore.ABPlcRx.PlcTagStatus.ErrUnsupported`

```csharp
public static int ErrUnsupported
```
The operation is not supported on the remote system.

###### `F:IoT.DriverCore.ABPlcRx.PlcTagStatus.ErrWinsock`

```csharp
public static int ErrWinsock
```
A Winsock-specific error occurred (only on Windows).

###### `F:IoT.DriverCore.ABPlcRx.PlcTagStatus.ErrWrite`

```csharp
public static int ErrWrite
```
An error occurred trying to write, usually to a socket.

###### `F:IoT.DriverCore.ABPlcRx.PlcTagStatus.StatusOK`

```csharp
public static int StatusOK
```
No error.

###### `F:IoT.DriverCore.ABPlcRx.PlcTagStatus.StatusPending`

```csharp
public static int StatusPending
```
Operation in progress. Not an error.

###### `M:IoT.DriverCore.ABPlcRx.PlcTagStatus.DecodeError(System.Int32)`

```csharp
public static string DecodeError(int code)
```
Decode error.

- Parameter `code`: Error code.
- Returns: A Value.

###### `M:IoT.DriverCore.ABPlcRx.PlcTagStatus.IsError(System.Int32)`

```csharp
public static bool IsError(int code)
```
Check code in error.

- Parameter `code`: The code.
- Returns: A Value.

#### `T:IoT.DriverCore.ABPlcRx.PlcTagWrapper`

```csharp
public class IoT.DriverCore.ABPlcRx.PlcTagWrapper
```
Plc Tag Wrapper.

##### Declared public members

###### `M:IoT.DriverCore.ABPlcRx.PlcTagWrapper.GetBit(System.Int32)`

```csharp
public bool GetBit(int index)
```
Get bit from index.

- Parameter `index`: The index.
- Returns: A Value.

###### `M:IoT.DriverCore.ABPlcRx.PlcTagWrapper.GetBits`

```csharp
public System.Collections.BitArray GetBits()
```
Get bit array from value.

- Returns: A Value.

###### `M:IoT.DriverCore.ABPlcRx.PlcTagWrapper.GetBitsArray`

```csharp
public bool[] GetBitsArray()
```
Get bit array from value.

- Returns: A Value.

###### `M:IoT.DriverCore.ABPlcRx.PlcTagWrapper.GetBitsString`

```csharp
public string GetBitsString()
```
Get bit string format.

- Returns: A Value.

###### `M:IoT.DriverCore.ABPlcRx.PlcTagWrapper.GetBool(System.Int32)`

```csharp
public bool GetBool(int offset)
```
Get local value Bool.

- Parameter `offset`: The offset.
- Returns: A Value.

###### `M:IoT.DriverCore.ABPlcRx.PlcTagWrapper.GetFloat32(System.Int32)`

```csharp
public float GetFloat32(int offset)
```
Get local value Float32.

- Parameter `offset`: The offset.
- Returns: A Value.

###### `M:IoT.DriverCore.ABPlcRx.PlcTagWrapper.GetFloat64(System.Int32)`

```csharp
public double GetFloat64(int offset)
```
Get local value Float.

- Parameter `offset`: The offset.
- Returns: A Value.

###### `M:IoT.DriverCore.ABPlcRx.PlcTagWrapper.GetInt16(System.Int32)`

```csharp
public short GetInt16(int offset)
```
Get local value Int16.

- Parameter `offset`: The offset.
- Returns: A Value.

###### `M:IoT.DriverCore.ABPlcRx.PlcTagWrapper.GetInt32(System.Int32)`

```csharp
public int GetInt32(int offset)
```
Get local value Int32.

- Parameter `offset`: The offset.
- Returns: A Value.

###### `M:IoT.DriverCore.ABPlcRx.PlcTagWrapper.GetInt64(System.Int32)`

```csharp
public long GetInt64(int offset)
```
Get local value Int64.

- Parameter `offset`: The offset.
- Returns: A Value.

###### `M:IoT.DriverCore.ABPlcRx.PlcTagWrapper.GetInt8(System.Int32)`

```csharp
public sbyte GetInt8(int offset)
```
Get local value Int8.

- Parameter `offset`: The offset.
- Returns: A Value.

###### `M:IoT.DriverCore.ABPlcRx.PlcTagWrapper.GetString(System.Int32)`

```csharp
public string GetString(int offset)
```
Get local value String.

- Parameter `offset`: The offset.
- Returns: A Value.

###### `M:IoT.DriverCore.ABPlcRx.PlcTagWrapper.GetType(System.Object,System.Int32)`

```csharp
public object GetType(object obj, int offset)
```
Get local value form type.

- Parameter `obj`: The object.
- Parameter `offset`: The offset.
- Returns: A Value.

###### `M:IoT.DriverCore.ABPlcRx.PlcTagWrapper.GetUInt16(System.Int32)`

```csharp
public ushort GetUInt16(int offset)
```
Get local value UInt16.

- Parameter `offset`: The offset.
- Returns: A Value.

###### `M:IoT.DriverCore.ABPlcRx.PlcTagWrapper.GetUInt32(System.Int32)`

```csharp
public uint GetUInt32(int offset)
```
Get local value UInt32.

- Parameter `offset`: The offset.
- Returns: A Value.

###### `M:IoT.DriverCore.ABPlcRx.PlcTagWrapper.GetUInt64(System.Int32)`

```csharp
public ulong GetUInt64(int offset)
```
Get local value UInt64.

- Parameter `offset`: The offset.
- Returns: A Value.

###### `M:IoT.DriverCore.ABPlcRx.PlcTagWrapper.GetUInt8(System.Int32)`

```csharp
public byte GetUInt8(int offset)
```
Get local value UInt8.

- Parameter `offset`: The offset.
- Returns: A Value.

###### `M:IoT.DriverCore.ABPlcRx.PlcTagWrapper.SetBit(System.Int32,System.Boolean)`

```csharp
public void SetBit(int index, bool value)
```
Set bit from index and value.

- Parameter `index`: The index.
- Parameter `value`: if set to true [value].

###### `M:IoT.DriverCore.ABPlcRx.PlcTagWrapper.SetBits(System.Collections.BitArray)`

```csharp
public void SetBits(System.Collections.BitArray bits)
```
Set bits from BitArray.

- Parameter `bits`: The bits.

###### `M:IoT.DriverCore.ABPlcRx.PlcTagWrapper.SetBool(System.Boolean,System.Int32)`

```csharp
public void SetBool(bool value, int offset)
```
Set local value Bool.

- Parameter `value`: if set to true [value].
- Parameter `offset`: The offset.

###### `M:IoT.DriverCore.ABPlcRx.PlcTagWrapper.SetFloat32(System.Single,System.Int32)`

```csharp
public void SetFloat32(float value, int offset)
```
Set local value Float32.

- Parameter `value`: The value.
- Parameter `offset`: The offset.

###### `M:IoT.DriverCore.ABPlcRx.PlcTagWrapper.SetFloat64(System.Double,System.Int32)`

```csharp
public void SetFloat64(double value, int offset)
```
Set local value Float.

- Parameter `value`: The value.
- Parameter `offset`: The offset.

###### `M:IoT.DriverCore.ABPlcRx.PlcTagWrapper.SetInt16(System.Int16,System.Int32)`

```csharp
public void SetInt16(short value, int offset)
```
Set local value Int16.

- Parameter `value`: The value.
- Parameter `offset`: The offset.

###### `M:IoT.DriverCore.ABPlcRx.PlcTagWrapper.SetInt32(System.Int32,System.Int32)`

```csharp
public void SetInt32(int value, int offset)
```
Set local value Int32.

- Parameter `value`: The value.
- Parameter `offset`: The offset.

###### `M:IoT.DriverCore.ABPlcRx.PlcTagWrapper.SetInt64(System.Int64,System.Int32)`

```csharp
public void SetInt64(long value, int offset)
```
Set local value Int64.

- Parameter `value`: The value.
- Parameter `offset`: The offset.

###### `M:IoT.DriverCore.ABPlcRx.PlcTagWrapper.SetInt8(System.SByte,System.Int32)`

```csharp
public void SetInt8(sbyte value, int offset)
```
Set local value Int8.

- Parameter `value`: The value.
- Parameter `offset`: The offset.

###### `M:IoT.DriverCore.ABPlcRx.PlcTagWrapper.SetString(System.String,System.Int32)`

```csharp
public void SetString(string value, int offset)
```
Set local value String.

- Parameter `value`: The value.
- Parameter `offset`: The offset.

###### `M:IoT.DriverCore.ABPlcRx.PlcTagWrapper.SetType(System.Object,System.Int32)`

```csharp
public void SetType(object obj, int offset)
```
Set local valute from type.

- Parameter `obj`: The object.
- Parameter `offset`: The offset.

###### `M:IoT.DriverCore.ABPlcRx.PlcTagWrapper.SetUInt16(System.UInt16,System.Int32)`

```csharp
public void SetUInt16(ushort value, int offset)
```
Set local value UInt16.

- Parameter `value`: The value.
- Parameter `offset`: The offset.

###### `M:IoT.DriverCore.ABPlcRx.PlcTagWrapper.SetUInt32(System.UInt32,System.Int32)`

```csharp
public void SetUInt32(uint value, int offset)
```
Set local value UInt32.

- Parameter `value`: The value.
- Parameter `offset`: The offset.

###### `M:IoT.DriverCore.ABPlcRx.PlcTagWrapper.SetUInt64(System.UInt64,System.Int32)`

```csharp
public void SetUInt64(ulong value, int offset)
```
Set local value UInt64.

- Parameter `value`: The value.
- Parameter `offset`: The offset.

###### `M:IoT.DriverCore.ABPlcRx.PlcTagWrapper.SetUInt8(System.Byte,System.Int32)`

```csharp
public void SetUInt8(byte value, int offset)
```
Set local value UInt8.

- Parameter `value`: The value.
- Parameter `offset`: The offset.

#### `T:IoT.DriverCore.ABPlcRx.PlcType`

```csharp
public enum IoT.DriverCore.ABPlcRx.PlcType
```
Allen Bradley PLC processor family.

##### Declared public members

###### `F:IoT.DriverCore.ABPlcRx.PlcType.LGX`

```csharp
public static const IoT.DriverCore.ABPlcRx.PlcType LGX
```
ControlLogix / CompactLogix Control Systems.

###### `F:IoT.DriverCore.ABPlcRx.PlcType.PLC5`

```csharp
public static const IoT.DriverCore.ABPlcRx.PlcType PLC5
```
PLC-5 Controllers.

###### `F:IoT.DriverCore.ABPlcRx.PlcType.SLC`

```csharp
public static const IoT.DriverCore.ABPlcRx.PlcType SLC
```
SLC / MicroLogix Controller.

#### `T:IoT.DriverCore.ABPlcRx.SourceGeneration.PlcModelAttribute`

```csharp
public class IoT.DriverCore.ABPlcRx.SourceGeneration.PlcModelAttribute
```
Marks a partial type as a PLC reactive stream model.

##### Declared public members

###### `M:IoT.DriverCore.ABPlcRx.SourceGeneration.PlcModelAttribute.#ctor`

```csharp
public IoT.DriverCore.ABPlcRx.SourceGeneration.PlcModelAttribute()
```
Initializes a new instance of `IoT.DriverCore.ABPlcRx.SourceGeneration.PlcModelAttribute`.

#### `T:IoT.DriverCore.ABPlcRx.SourceGeneration.PlcTagAttribute`

```csharp
public class IoT.DriverCore.ABPlcRx.SourceGeneration.PlcTagAttribute
```
Describes a PLC tag stream that should be generated for a partial model.

##### Declared public members

###### `M:IoT.DriverCore.ABPlcRx.SourceGeneration.PlcTagAttribute.#ctor(System.String)`

```csharp
public IoT.DriverCore.ABPlcRx.SourceGeneration.PlcTagAttribute(string tagName)
```
Initializes a new instance of the `T:IoT.DriverCore.ABPlcRx.SourceGeneration.PlcTagAttribute` class.

- Parameter `tagName`: The PLC tag name.

###### `M:IoT.DriverCore.ABPlcRx.SourceGeneration.PlcTagAttribute.#ctor(System.Type,System.String,System.String)`

```csharp
public IoT.DriverCore.ABPlcRx.SourceGeneration.PlcTagAttribute(System.Type valueType, string propertyName, string tagName)
```
Initializes a new instance of the `T:IoT.DriverCore.ABPlcRx.SourceGeneration.PlcTagAttribute` class.

- Parameter `valueType`: The PLC value type.
- Parameter `propertyName`: The generated property name.
- Parameter `tagName`: The PLC tag name.

###### `P:IoT.DriverCore.ABPlcRx.SourceGeneration.PlcTagAttribute.Bit`

```csharp
public int Bit { get; set; }
```
Gets or sets the bit index for boolean bit access.

- Value: The `Bit` value.

###### `P:IoT.DriverCore.ABPlcRx.SourceGeneration.PlcTagAttribute.Group`

```csharp
public string Group { get; set; }
```
Gets or sets the tag group.

- Value: The `Group` value.

###### `P:IoT.DriverCore.ABPlcRx.SourceGeneration.PlcTagAttribute.PropertyName`

```csharp
public string PropertyName { get; }
```
Gets the generated property name when the attribute is applied to a class.

- Value: The `PropertyName` value.

###### `P:IoT.DriverCore.ABPlcRx.SourceGeneration.PlcTagAttribute.RegisterTag`

```csharp
public bool RegisterTag { get; set; }
```
Gets or sets a value indicating whether generated attach logic should register the tag.

- Value: The `RegisterTag` value.

###### `P:IoT.DriverCore.ABPlcRx.SourceGeneration.PlcTagAttribute.TagName`

```csharp
public string TagName { get; }
```
Gets the PLC tag name.

- Value: The `TagName` value.

###### `P:IoT.DriverCore.ABPlcRx.SourceGeneration.PlcTagAttribute.ValueType`

```csharp
public System.Type ValueType { get; }
```
Gets the generated property value type when the attribute is applied to a class.

- Value: The `ValueType` value.

###### `P:IoT.DriverCore.ABPlcRx.SourceGeneration.PlcTagAttribute.Variable`

```csharp
public string Variable { get; set; }
```
Gets or sets the application variable key. Defaults to the property name.

- Value: The `Variable` value.

#### `T:IoT.DriverCore.ABPlcRx.TagHelper`

```csharp
public class IoT.DriverCore.ABPlcRx.TagHelper
```
Helper Tag.

##### Declared public members

###### `M:IoT.DriverCore.ABPlcRx.TagHelper.BitsToNumber(System.Collections.BitArray)`

```csharp
public static int BitsToNumber(System.Collections.BitArray bits)
```
Bite array to number.

- Parameter `bits`: The bits.
- Returns: A Value.

###### `M:IoT.DriverCore.ABPlcRx.TagHelper.CreateObject``1(``0,System.Int32)`

```csharp
public static TType CreateObject<TType>(TType typeWitness, int length)
```
Create object from Type.

- Parameter `typeWitness`: Type witness for the requested value.
- Parameter `length`: The length.
- Returns: A Value.

###### `M:IoT.DriverCore.ABPlcRx.TagHelper.NumberToBits(System.Int32)`

```csharp
public static System.Collections.BitArray NumberToBits(int value)
```
Number to bit array.

- Parameter `value`: The value.
- Returns: A Value.

###### `M:IoT.DriverCore.ABPlcRx.TagHelper.ScaleLinear(IoT.DriverCore.ABPlcRx.IPlcTag,System.Double,System.Double,System.Double,System.Double)`

```csharp
public static double ScaleLinear(IoT.DriverCore.ABPlcRx.IPlcTag tag, double minRaw, double maxRaw, double minScale, double maxScale)
```
Performs Linear scaling conversion.

- Parameter `tag`: The tag.
- Parameter `minRaw`: The minimum raw.
- Parameter `maxRaw`: The maximum raw.
- Parameter `minScale`: The minimum scale.
- Parameter `maxScale`: The maximum scale.
- Returns: A Value.

###### `M:IoT.DriverCore.ABPlcRx.TagHelper.ScaleSquareRoot(IoT.DriverCore.ABPlcRx.IPlcTag,System.Double,System.Double,System.Double,System.Double)`

```csharp
public static double ScaleSquareRoot(IoT.DriverCore.ABPlcRx.IPlcTag tag, double minRaw, double maxRaw, double minScale, double maxScale)
```
Performs SquareRoot conversion.

- Parameter `tag`: The tag.
- Parameter `minRaw`: The minimum raw.
- Parameter `maxRaw`: The maximum raw.
- Parameter `minScale`: The minimum scale.
- Parameter `maxScale`: The maximum scale.
- Returns: A Value.

#### `T:IoT.DriverCore.ABPlcRx.TagMixins`

```csharp
public class IoT.DriverCore.ABPlcRx.TagMixins
```
PLC tag bit helper extensions.

##### Declared public members

###### `M:IoT.DriverCore.ABPlcRx.TagMixins.GetBit(IoT.DriverCore.ABPlcRx.IPlcTag`1{System.Int16},System.Int32)`

```csharp
public static bool GetBit(IoT.DriverCore.ABPlcRx.IPlcTag<short> source, int bit)
```
Executes the `GetBit` operation.

- Parameter `source`: The `source` value.
- Parameter `bit`: The `bit` value.
- Returns: A `bool` result.

###### `M:IoT.DriverCore.ABPlcRx.TagMixins.GetBit(System.Int16,System.Int32)`

```csharp
public static bool GetBit(short source, int bit)
```
Gets the bit.

- Parameter `source`: The signed 16-bit source value.
- Parameter `bit`: The bit.
- Returns: A bool from the source at bit x.

###### `M:IoT.DriverCore.ABPlcRx.TagMixins.ScaleLinear(IoT.DriverCore.ABPlcRx.IPlcTag,System.Double,System.Double,System.Double,System.Double)`

```csharp
public static double ScaleLinear(IoT.DriverCore.ABPlcRx.IPlcTag tag, double minRaw, double maxRaw, double minScale, double maxScale)
```
Performs Linear scaling conversion.

- Parameter `tag`: The PLC tag.
- Parameter `minRaw`: The minimum raw.
- Parameter `maxRaw`: The maximum raw.
- Parameter `minScale`: The minimum scale.
- Parameter `maxScale`: The maximum scale.
- Returns: A Value.

###### `M:IoT.DriverCore.ABPlcRx.TagMixins.ScaleSquareRoot(IoT.DriverCore.ABPlcRx.IPlcTag,System.Double,System.Double,System.Double,System.Double)`

```csharp
public static double ScaleSquareRoot(IoT.DriverCore.ABPlcRx.IPlcTag tag, double minRaw, double maxRaw, double minScale, double maxScale)
```
Performs SquareRoot conversion.

- Parameter `tag`: The PLC tag.
- Parameter `minRaw`: The minimum raw.
- Parameter `maxRaw`: The maximum raw.
- Parameter `minScale`: The minimum scale.
- Parameter `maxScale`: The maximum scale.
- Returns: A Value.

###### `M:IoT.DriverCore.ABPlcRx.TagMixins.SetBit(IoT.DriverCore.ABPlcRx.IPlcTag`1{System.Int16},System.Int32,System.Boolean)`

```csharp
public static void SetBit(IoT.DriverCore.ABPlcRx.IPlcTag<short> source, int bit, bool value)
```
Executes the `SetBit` operation.

- Parameter `source`: The `source` value.
- Parameter `bit`: The `bit` value.
- Parameter `value`: The `value` value.

###### `M:IoT.DriverCore.ABPlcRx.TagMixins.SetBit(System.Int16,System.Int32,System.Boolean)`

```csharp
public static short SetBit(short source, int bit, bool value)
```
Sets the bit.

- Parameter `source`: The signed 16-bit source value.
- Parameter `bit`: The bit.
- Parameter `value`: if set to true [value].
- Returns: A short.

<!-- END GENERATED PUBLIC API -->
