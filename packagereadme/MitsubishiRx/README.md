<p align="center">
  <img src="https://raw.githubusercontent.com/ChrisPulman/IoT-DriverCore/main/images/mitsubishi-rx.png" alt="MitsubishiRx package logo" width="320" />
</p>

# MitsubishiRx

## Overview

`MitsubishiRx` is an async and observable Mitsubishi MC Protocol / SLMP client. It supports 1E, 3E and 4E Ethernet frames, 1C, 3C and 4C serial frames, TCP, UDP, and serial transports, direct device operations, tag databases, logical tags, polling, and test transports.

## Safety

PLC writes, remote run/stop/reset, password changes, and buffer-memory access can change a live process. Validate addresses and values against the PLC program, start with a simulator or isolated controller, restrict network access, and require an application-level interlock and audit trail before issuing control operations. A successful protocol response is not a safety guarantee.

## Package matrix

| Package | Namespace | Target frameworks | Use it when |
| --- | --- | --- | --- |
| `IoT-Driver.MitsubishiRx` | `IoT.Driver.MitsubishiRx` | net8.0, net9.0, net10.0, net11.0 | Using ReactiveUI.Primitives and SerialPortRx. |
| `IoT-Driver.MitsubishiRx.Reactive` | `IoT.Driver.MitsubishiRx.Reactive` | net8.0, net9.0, net10.0, net11.0 | Using the ReactiveUI.Primitives reactive bridge and SerialPortRx.Reactive. |
| `IoT-Driver.MitsubishiRx.Generators` | generated code targets `IoT.Driver.MitsubishiRx` | analyzer targets supplied by the compiler | Installing generated base-runtime tag clients. |

The runtime packages compile shared source under different namespaces. Do not reference both in one application unless the distinction is intentional. Neither runtime package contains the analyzer. The standalone generator emits clients for `IoT.Driver.MitsubishiRx` and requires the base `IoT-Driver.MitsubishiRx` runtime. Use the handwritten tag, polling, and write APIs in a reactive-runtime-only application.

## Install

```bash
dotnet add package IoT-Driver.MitsubishiRx
# or, for the reactive namespace
dotnet add package IoT-Driver.MitsubishiRx.Reactive
# Add separately only with the base runtime when using generated clients.
dotnet add package IoT-Driver.MitsubishiRx.Generators
```

## Quick start

The primary constructor takes options, an optional transport (useful for tests), and an optional scheduler. Calls return `Responce` / `Responce<T>`; check `IsSucceed` before using `Value`.

```csharp
using IoT.Driver.MitsubishiRx;

var options = new MitsubishiClientOptions(
    Host: "192.168.0.10", Port: 5000,
    FrameType: MitsubishiFrameType.ThreeE,
    DataCode: CommunicationDataCode.Binary,
    TransportKind: MitsubishiTransportKind.Tcp,
    Timeout: TimeSpan.FromSeconds(3));

await using var plc = new MitsubishiRx(options, transport: null, scheduler: null);
var opened = await plc.OpenAsync(CancellationToken.None);
if (!opened.IsSucceed) throw new InvalidOperationException(opened.Err);

var read = await plc.ReadWordsAsync("D100", 2, CancellationToken.None);
if (read.IsSucceed && read.Value is { } words)
    Console.WriteLine($"D100={words[0]}");

var written = await plc.WriteWordsAsync("D100", new ushort[] { 42 }, CancellationToken.None);
if (!written.IsSucceed) Console.Error.WriteLine(written.Err);
```

## Configuration

`MitsubishiClientOptions` records the endpoint and framing. Use 3E/binary/TCP as the normal starting point; select the actual PLC/module configuration rather than inferring it from a CPU family.

| Setting | Notes |
| --- | --- |
| `FrameType` | `OneE`, `ThreeE`, `FourE`, `OneC`, `ThreeC`, `FourC`. |
| `DataCode` | `Binary` or `Ascii`; 1C/3C are ASCII serial paths and format 5 is binary 4C. |
| `TransportKind` | `Tcp`, `Udp`, or `Serial`. |
| `Route`, `MonitoringTimer`, `LegacyPcNumber`, `SerialNumberProvider` | Ethernet/SLMP route and frame metadata. `ResolvedRoute` defaults to `MitsubishiRoute.Default`. |
| `Timeout`, `CpuType`, `XyNotation` | Client timeout, optional family hint, and X/Y octal/hexadecimal interpretation. |
| `Serial` | Required when `TransportKind.Serial`; configure `MitsubishiSerialOptions` with port parameters, message format, routing, station and buffer settings. |

```csharp
using System.IO.Ports;
using IoT.Driver.MitsubishiRx;

var serial = new MitsubishiClientOptions(
    "COM3", 0, MitsubishiFrameType.FourC, CommunicationDataCode.Binary,
    MitsubishiTransportKind.Serial,
    Serial: new MitsubishiSerialOptions(
        PortName: "COM3", BaudRate: 9600, DataBits: 7,
        Parity: Parity.Even, StopBits: StopBits.One, Handshake: Handshake.None,
        MessageFormat: MitsubishiSerialMessageFormat.Format5));
```

## Detailed features

### Response, errors, cancellation, and lifetime

Every command is asynchronous and returns `Responce` (the established public spelling) or `Responce<T>`. A protocol rejection, transport exception, timeout, or conversion error is represented by `IsSucceed == false`, `Err`, `ErrList`, and, where applicable, `Exception`; it is not normally thrown from a command. Argument validation and use after disposal can still throw. Pass a cancellation token when the operation needs to participate in an application cancellation scope, test the response before reading `Value`, and dispose the client after subscriptions have been disposed.

```csharp
static async Task<T> Require<T>(Task<Responce<T>> request)
{
    var result = await request.ConfigureAwait(false);
    if (!result.IsSucceed)
        throw new InvalidOperationException(result.Err, result.Exception);
    return result.Value!;
}

using var stop = new CancellationTokenSource(TimeSpan.FromSeconds(3));
await using var plc = new MitsubishiRx(options, transport: null, scheduler: null);
var opened = await plc.OpenAsync(stop.Token);
if (!opened.IsSucceed) return;             // report opened.Err / opened.ErrList
ushort[] words = await Require(plc.ReadWordsAsync("D100", 4, stop.Token));
await plc.CloseAsync(CancellationToken.None);
```

`ConnectionStates` publishes `Disconnected`, connecting/connected and fault states; `OperationLogs` contains the operation description, request/response bytes, success and exception. Subscribe before `OpenAsync` if startup telemetry matters, redact raw frames if they can contain sensitive payloads, and dispose the subscriptions before the client.

```csharp
using var state = plc.ConnectionStates.Subscribe(s => Console.WriteLine($"PLC: {s}"));
using var log = plc.OperationLogs.Subscribe(entry =>
{
    if (!entry.Success) Console.Error.WriteLine(entry.Description);
});
```

### Addressing, device values, and batch operations

`MitsubishiDeviceAddress.Parse(address, xyNotation)` is the parser used by the client. Device prefixes such as `D`, `M`, `X`, `Y`, `W`, `B`, `R`, `ZR`, and `TN` must match the PLC/program. `XyAddressNotation` chooses how X/Y suffixes are interpreted; retain the PLC's configured octal/hex convention. `ReadWordsAsync`/`WriteWordsAsync` read contiguous 16-bit device units, while `ReadBitsAsync`/`WriteBitsAsync` read contiguous bit units. The supplied count or list length is part of the protocol request, so never accidentally write a larger buffer than the designed tag region.

```csharp
var address = MitsubishiDeviceAddress.Parse("D200", XyAddressNotation.Octal);
var before = await plc.ReadWordsAsync("D200", points: 3, cancellationToken: CancellationToken.None);
if (before.IsSucceed)
{
    ushort[] next = [before.Value![0], 250, before.Value[2]];
    var write = await plc.WriteWordsAsync("D200", next, CancellationToken.None);
    if (!write.IsSucceed) Console.Error.WriteLine(write.Err);
}

var inputs = await plc.ReadBitsAsync("X10", 8, CancellationToken.None);
if (inputs.IsSucceed)
    await plc.WriteBitsAsync("M100", inputs.Value!, CancellationToken.None);
```

Use a contiguous batch read whenever addresses are nearby. Use random access only when grouping would read a large irrelevant gap:

```csharp
var sparse = await plc.RandomReadWordsAsync(["D100", "D220", "W10"], CancellationToken.None);
var commit = await plc.RandomWriteWordsAsync(
    new[] { new KeyValuePair<string, ushort>("D100", 12), new("W10", 9) },
    CancellationToken.None);
```

`RegisterMonitorAsync(addresses)` programs the PLC-side monitor list and `ExecuteMonitorAsync` obtains its raw response. Register once after connection/reconfiguration, then execute repeatedly; re-register after reconnect if the target controller does not preserve monitor state. `ReadBlocksAsync`/`WriteBlocksAsync` accept `MitsubishiBlockRequest`, which combines `MitsubishiWordBlock` and `MitsubishiBitBlock` for a single multi-block command. These APIs return raw bytes because the response layout follows the request blocks; decode only with the same block ordering used to construct the request.

```csharp
var registered = await plc.RegisterMonitorAsync(["D100", "M20", "D300"], CancellationToken.None);
if (registered.IsSucceed)
{
    Responce<byte[]> sample = await plc.ExecuteMonitorAsync(CancellationToken.None);
    if (sample.IsSucceed) Console.WriteLine(Convert.ToHexString(sample.Value!));
}

var blocks = new MitsubishiBlockRequest(
    [new MitsubishiWordBlock(MitsubishiDeviceAddress.Parse("D100", options.XyNotation), new ushort[] { 1, 2, 3, 4 })],
    [new MitsubishiBitBlock(MitsubishiDeviceAddress.Parse("M100", options.XyNotation), new bool[] { true, false })]);
var blockRead = await plc.ReadBlocksAsync(blocks, CancellationToken.None);
var blockWrite = await plc.WriteBlocksAsync(blocks, CancellationToken.None);
if (!blockWrite.IsSucceed) Console.Error.WriteLine(blockWrite.Err);
```

### Controller diagnostics, control, and raw protocol access

`ReadTypeNameAsync` returns `MitsubishiTypeName` (model name and model code). `ClearErrorAsync`, `LoopbackAsync`, and `ReadMemoryAsync`/`WriteMemoryAsync` support commissioning and diagnosis. Their result follows the same `Responce` model; `LoopbackAsync` returns the echoed bytes and memory reads return `ushort[]`. Memory calls require the documented MC-memory command as well as address and length/value payload.

```csharp
var identity = await plc.ReadTypeNameAsync(CancellationToken.None);
var echoed = await plc.LoopbackAsync([0x01, 0x02], CancellationToken.None);
var buffer = await plc.ReadMemoryAsync(command: 0x0613, address: 0x1000, length: 16,
    cancellationToken: CancellationToken.None);
if (buffer.IsSucceed)
    await plc.WriteMemoryAsync(command: 0x1613, address: 0x1000, values: buffer.Value!,
        cancellationToken: CancellationToken.None);
```

`RemoteRunAsync`, `RemoteStopAsync`, `RemotePauseAsync`, `RemoteLatchClearAsync`, `RemoteResetAsync`, `UnlockAsync`, and `LockAsync` change controller state or protection. Put an application-level authorization, interlock, confirmation record, and a read-back check around them. Do not expose these calls directly to an untrusted UI. `ExecuteRawAsync(MitsubishiRawCommandRequest, token)` is for a documented command not yet wrapped by the higher-level methods; it returns its wire reply and should be isolated behind a versioned, tested adapter. `SendPackageAsync`, `SendPackageSingleAsync`, and `SendPackageReliableAsync` are asynchronous raw-frame compatibility APIs: they transmit the supplied pre-encoded frame without re-encoding it. New code should prefer `ExecuteRawAsync`.

```csharp
if (operatorApproved && await IsPlantSafeAsync())
{
    var stopped = await plc.RemoteStopAsync(CancellationToken.None);
    if (!stopped.IsSucceed) throw new InvalidOperationException(stopped.Err);
    var clear = await plc.ClearErrorAsync(CancellationToken.None);
}

var request = new MitsubishiRawCommandRequest(Command: 0x0619, Subcommand: 0,
    Body: Array.Empty<byte>(), Description: "Site-approved diagnostic");
Responce<byte[]> raw = await plc.ExecuteRawAsync(request, CancellationToken.None);
```

### Polling, freshness, triggers, and write queues

`ObserveWords` and `ObserveBits` repeatedly invoke their direct counterparts at an interval. `ObserveWordsHeartbeat` wraps each poll in a `Heartbeat<T>`; `ObserveWordsStale` emits `Stale<T>` when no new value arrives within the configured age. `ObserveWordsLatest` accepts an observable trigger and keeps only the newest in-flight result, which is appropriate for bursty UI refresh events. Dispose each subscription to stop scheduling and retain no subscription after the client is disposed.

```csharp
using var cts = new CancellationTokenSource();
using var telemetry = plc.ObserveWordsHeartbeat("D100", 2, TimeSpan.FromMilliseconds(250),
    heartbeatAfter: TimeSpan.FromSeconds(1), minimumUpdateSpacing: null, pollTimeout: null)
    .Subscribe(beat => Console.WriteLine($"{beat.Value.Value?[0]} @ {beat.Timestamp:O}"));
using var health = plc.ObserveWordsStale("D100", 2, TimeSpan.FromMilliseconds(250),
    TimeSpan.FromSeconds(2), minimumUpdateSpacing: null)
    .Subscribe(stale => Console.WriteLine(stale.IsStale));
```

`ObserveReactiveWords`, `ObserveReactiveTag<T>`, and `ObserveReactiveTagGroup` add a `MitsubishiReactiveValue<T>` with `MitsubishiReactiveQuality` (`Good`, error/stale/heartbeat states) and timestamp/error context. Use it for view models and downstream decisions that must distinguish a valid zero from a failed or stale read. `CreateReactiveWordWritePipeline` and `CreateReactiveTagWritePipeline<T>` accept a source of desired values, serialize writes, and report `MitsubishiReactiveWriteResult`; choose `MitsubishiReactiveWriteMode` deliberately (for example, latest-only for a slider and every value for an auditable setpoint sequence).

```csharp
using var values = plc.ObserveReactiveWords("D100", 1, TimeSpan.FromSeconds(1))
    .Where(v => v.Quality == MitsubishiReactiveQuality.Good)
    .Subscribe(v => Render(v.Value![0]));

using var writer = plc.CreateReactiveWordWritePipeline("D200",
    MitsubishiReactiveWriteMode.LatestWins, TimeSpan.FromMilliseconds(100));
using var writeResults = writer.Results.Subscribe(r => Console.WriteLine(r.Success));
writer.Post(new ushort[] { 42 });
```

### Tag database, typed values, groups, and schema rollout

`MitsubishiTagDatabase` maps stable names to `MitsubishiTagDefinition` records. A tag can declare address, data type, point count, string length/encoding and engineering scale metadata; `MitsubishiTagValueConverter` performs the conversion. Build it with the constructor/Add methods, `FromCsv`, `FromJson`, `FromYaml`, or `Load`; persist using `ToCsv`, `ToJson`, `ToYaml`, and `Save`. Assign the database to `TagDatabase`, call `ValidateTagDatabase`, and only then use the by-tag APIs. Failure to assign or validate a database is an application configuration error, not a retryable PLC fault.

```csharp
var database = new MitsubishiTagDatabase([
    new MitsubishiTagDefinition("Temperature", "D100", "Float"),
    new MitsubishiTagDefinition("Enable", "M100", "Bit"),
    new MitsubishiTagDefinition("Recipe", "D200", "String", Length: 20),
    new MitsubishiTagDefinition("Flow", "D120", "UInt16", Scale: 0.1, Units: "L/min")
]);
database.AddGroup(new MitsubishiTagGroupDefinition("RecipeCommit", ["Temperature", "Enable", "Recipe"]));
plc.TagDatabase = database;
var valid = plc.ValidateTagDatabase();
if (!valid.IsSucceed) throw new InvalidOperationException(valid.Err);

var temperature = await plc.ReadFloatByTagAsync("Temperature", CancellationToken.None);
var enabled = await plc.ReadBitsByTagAsync("Enable", 1, CancellationToken.None);
var recipe = await plc.ReadStringByTagAsync("Recipe", CancellationToken.None);
```

The typed families are `Read`/`WriteWordsByTagAsync`, `Bits`, `Int16`, `UInt16`, `Int32`, `DWord`/`UInt32`, `Float`, `ScaledDouble`, and `String`; `ReadTagAsync`/`WriteTagAsync` dispatch by metadata. Use a typed method when the schema is known at compile time, and the generic method only for an editor/importer. String calls have overloads for explicit length where required. `RandomReadWordsByTagAsync`/`RandomWriteWordsByTagAsync` resolve names before the sparse protocol operation.

```csharp
var setTemperature = await plc.WriteFloatByTagAsync("Temperature", 21.5f, CancellationToken.None);
var setRecipe = await plc.WriteStringByTagAsync("Recipe", "Batch-07", CancellationToken.None);
var engineering = await plc.ReadScaledDoubleByTagAsync("Flow", CancellationToken.None);
var arbitrary = await plc.ReadTagAsync("Temperature", CancellationToken.None);
```

Groups provide an explicit, validated write boundary. `ReadTagGroupSnapshotAsync` returns `MitsubishiTagGroupSnapshot`; `ValidateTagGroupWrite` checks names/types before any remote write; `WriteTagGroupValuesAsync` writes the supplied members; and `WriteTagGroupSnapshotAsync` replays a snapshot. A group operation is sequential rather than a transactional PLC instruction, so use the PLC program's commit/handshake bit if atomic process visibility is required.

```csharp
var staged = new Dictionary<string, object?> { ["Temperature"] = 22.0f, ["Enable"] = true };
var check = plc.ValidateTagGroupWrite("RecipeCommit", staged);
if (check.IsSucceed)
    await plc.WriteTagGroupValuesAsync("RecipeCommit", staged, CancellationToken.None);

var snapshot = await plc.ReadTagGroupSnapshotAsync("RecipeCommit", CancellationToken.None);
if (snapshot.IsSucceed)
    await plc.WriteTagGroupSnapshotAsync(snapshot.Value!, CancellationToken.None);
```

For controlled configuration change, use `LoadAndValidateTagDatabase`, `PreviewTagDatabaseDiff`, `ObserveTagDatabaseDiff`, and `ObserveTagDatabaseReload`. `MitsubishiTagDatabaseDiff`, `MitsubishiTagChange`, `MitsubishiTagGroupChange`, and `MitsubishiSchemaChangeKind` tell which entries change. Apply a `MitsubishiTagRolloutPolicy` only after a preview has been reviewed; do not hot-reload a safety-critical mapping without an operations change process.

```csharp
var preview = plc.PreviewTagDatabaseDiff("tags.yaml", MitsubishiTagRolloutPolicy.AllowAll);
if (preview.IsSucceed && preview.Value!.IsEmpty)
    Console.WriteLine("No mapping change");
using var reload = plc.ObserveTagDatabaseReload("tags.yaml", TimeSpan.FromSeconds(5), emitInitial: true)
    .Subscribe(result => Console.WriteLine(result.IsSucceed ? "Reloaded" : result.Err));
```

### Logical tags, bulk operations, and source generation

`CreateLogicalTagClient` composes the common `ILogicalTagCatalog` with the Mitsubishi transport and optional `LogicalTagSqliteStore`. `MitsubishiLogicalTagClient` supports registration, read/write one or many tags, observable and async-enumerable observation, persistence and operation metrics. It is the appropriate boundary when several protocols share a logical-tag catalog. Dispose it before disposing the native Mitsubishi client.

```csharp
using IoT.Driver.Core;

using var catalog = new LogicalTagCatalog();
var store = new LogicalTagSqliteStore("Data Source=logical-tags.db");
using var logical = plc.CreateLogicalTagClient(catalog, TimeSpan.FromSeconds(1), store);
var result = await logical.ReadAsync("Line1.Temperature", CancellationToken.None);
await foreach (var update in logical.ObserveAsync("Line1.Temperature", CancellationToken.None))
    Console.WriteLine(update.Value);
```

`IoT-Driver.MitsubishiRx.Generators` supplies `MitsubishiTagClientGenerator`, which recognizes `MitsubishiTagClientAttribute`, `MitsubishiTagAttribute`, and `MitsubishiTagClientSchemaAttribute`. It generates a typed client, `GeneratedMitsubishiTagClient` entrypoint, extension/`GroupsClient` surfaces and per-group `TagsClient` accessors. The generator validates supported scalar/tag metadata at compile time, so use attributes for a stable, source-controlled schema and the runtime database for imported/operational schemas.

```csharp
using IoT.Driver.MitsubishiRx;

[MitsubishiTagClient(nameof(LogicalTags))]
public sealed partial class FurnaceTags
{
    public MitsubishiLogicalTagClient LogicalTags { get; init; } = null!;

    [MitsubishiTag("Furnace.Temperature")]
    public float Temperature { get; set; }

    [MitsubishiTag("Furnace.Enabled")]
    public bool Enabled { get; set; }
}
// The generated client binds the declared members to MitsubishiRx; inspect obj/Generated
// or the generator diagnostics when an attribute is malformed.
```

### Test transports and two combined workflows

The client selects its built-in TCP/UDP or serial transport from `MitsubishiClientOptions`; those transport implementations are intentionally internal. `IMitsubishiTransport` is the public contract for a custom transport. `MitsubishiSimulatorMemory` offers direct word/bit state, while `MitsubishiSimulatorTransport` records requests, queues responses/connect/exchange faults, or uses stateful memory. Use the simulator in unit tests rather than a production PLC.

```csharp
var memory = new MitsubishiSimulatorMemory();
memory.WriteWords("D100", [10, 20]);
await using var simulated = new MitsubishiRx(
    options, new MitsubishiSimulatorTransport(memory), scheduler: null);
await simulated.OpenAsync(CancellationToken.None);
var result = await simulated.ReadWordsAsync("D100", 2, CancellationToken.None);
```

**Workflow 1 - production telemetry with freshness and safe setpoints.** Validate/load the tag database, open the client, observe a tag group with `ObserveReactiveTagGroup` or `ObserveTagGroupStale`, inhibit UI changes when quality is stale/error, validate a group write, write the staged values through a latest-only pipeline, and record `OperationLogs`. This combines configuration validation, polling, quality, grouped writes and audit telemetry without assuming a successful network response is a process-safe outcome.

```csharp
await using var production = new MitsubishiRx(options, transport: null, scheduler: null);
production.TagDatabase = MitsubishiTagDatabase.Load("line-a-tags.yaml");
var schema = production.ValidateTagDatabase();
if (!schema.IsSucceed) throw new InvalidOperationException(schema.Err);

using var logs = production.OperationLogs.Subscribe(log => Audit(log.Description, log.Success));
using var fresh = production.ObserveTagGroupStale("Setpoints", TimeSpan.FromMilliseconds(250),
    TimeSpan.FromSeconds(2), minimumUpdateSpacing: null)
    .Subscribe(sample => SetControlsEnabled(!sample.IsStale && sample.Value.IsSucceed));

var open = await production.OpenAsync(CancellationToken.None);
if (!open.IsSucceed) throw new InvalidOperationException(open.Err);
var staged = new Dictionary<string, object?> { ["TargetTemperature"] = 70.0f, ["Enabled"] = true };
var accepted = production.ValidateTagGroupWrite("Setpoints", staged);
if (accepted.IsSucceed && await OperatorInterlockAllowsAsync())
{
    var written = await production.WriteTagGroupValuesAsync("Setpoints", staged, CancellationToken.None);
    if (!written.IsSucceed) ReportWriteFailure(written.Err);
}
await production.CloseAsync(CancellationToken.None);
```

**Workflow 2 - commissioning with sparse diagnostics.** Open a simulator first, use `ReadTypeNameAsync`, batch-read contiguous regions, random-read sparse registers, register/execute a monitor for repeated inspection, then move the same options to a live isolated PLC. Keep remote-control and raw commands behind an explicit operator authorization, capture operation logs, and close/dispose when the session ends.

```csharp
var memory = new MitsubishiSimulatorMemory();
memory.WriteWords("D100", new ushort[] { 100, 101, 102, 103 });
memory.WriteWords("D200", new ushort[] { 200 });
await using var bench = new MitsubishiRx(
    options, new MitsubishiSimulatorTransport(memory), scheduler: null);
using var diagnostics = bench.OperationLogs.Subscribe(log => Console.WriteLine(log.Description));
if (!(await bench.OpenAsync(CancellationToken.None)).IsSucceed) return;

var identity = await bench.ReadTypeNameAsync(CancellationToken.None);
var contiguous = await bench.ReadWordsAsync("D100", 4, CancellationToken.None);
var sparse = await bench.RandomReadWordsAsync(new[] { "D100", "D200" }, CancellationToken.None);
var registered = await bench.RegisterMonitorAsync(new[] { "D100", "D200" }, CancellationToken.None);
Responce<byte[]> monitor = registered.IsSucceed
    ? await bench.ExecuteMonitorAsync(CancellationToken.None)
    : new Responce<byte[]>(registered);
if (!identity.IsSucceed || !contiguous.IsSucceed || !sparse.IsSucceed || !monitor.IsSucceed)
    throw new InvalidOperationException("Commissioning diagnostic did not succeed.");
await bench.CloseAsync(CancellationToken.None);
```

### Device operations and monitoring

`ReadWordsAsync`, `WriteWordsAsync`, `ReadBitsAsync`, and `WriteBitsAsync` operate on direct addresses. `RandomReadWordsAsync` / `RandomWriteWordsAsync` issue random-device requests; `RegisterMonitorAsync` then `ExecuteMonitorAsync` use PLC monitor registration; `ReadBlocksAsync` / `WriteBlocksAsync` use `MitsubishiWordBlock` and `MitsubishiBitBlock` requests. Use a cancellation token on every asynchronous call.

```csharp
var bits = await plc.ReadBitsAsync("M100", 8, CancellationToken.None);
var random = await plc.RandomReadWordsAsync(new[] { "D100", "D200" }, CancellationToken.None);
var monitor = await plc.RegisterMonitorAsync(new[] { "D100", "D101" }, CancellationToken.None);
var data = monitor.IsSucceed
    ? await plc.ExecuteMonitorAsync(CancellationToken.None)
    : new Responce<byte[]>(monitor);
```

The protocol/control surface is `ReadTypeNameAsync`, `RemoteRunAsync`, `RemoteStopAsync`, `RemotePauseAsync`, `RemoteLatchClearAsync`, `RemoteResetAsync`, `UnlockAsync`, `LockAsync`, `ClearErrorAsync`, `LoopbackAsync`, `ReadMemoryAsync`, `WriteMemoryAsync`, and `ExecuteRawAsync(MitsubishiRawCommandRequest, ...)`. Guard the control methods more strictly than normal reads.

```csharp
// This belongs behind a site authorization and an independently verified safe state.
if (operatorApproved && await IsPlantSafeAsync())
{
    var unlocked = await plc.UnlockAsync(passwordFromSecureStore, CancellationToken.None);
    if (!unlocked.IsSucceed) throw new InvalidOperationException(unlocked.Err);
    var running = await plc.RemoteRunAsync(force: false, clearMode: false,
        cancellationToken: CancellationToken.None);
    if (!running.IsSucceed) ReportWriteFailure(running.Err);
    var locked = await plc.LockAsync(passwordFromSecureStore, CancellationToken.None);
}
```

### Polling, health, and queued writes

`ObserveWords`, `ObserveBits`, `ObserveWordsHeartbeat`, `ObserveWordsStale`, and `ObserveWordsLatest` turn a scan interval or trigger into streams. Tag groups have equivalent `ObserveTagGroup*` methods. `ConnectionStates`, `OperationLogs`, `SampleDiagnostics`, and `ObserveConnectionHealth` expose lifecycle and operational observations. `ObserveReactiveWords` / `ObserveReactiveTagGroup` add `MitsubishiReactiveValue<T>` quality metadata. `CreateReactiveWordWritePipeline` serializes/coalesces word writes and returns `MitsubishiReactiveWritePipeline<IReadOnlyList<ushort>>`.

```csharp
using var subscription = plc.ObserveWords("D100", 2, TimeSpan.FromMilliseconds(250),
    minimumUpdateSpacing: null, pollTimeout: null)
    .Subscribe(reply =>
    {
        if (reply.IsSucceed) Console.WriteLine(reply.Value![0]);
    });
```

For externally triggered refreshes, use a caller-owned `Signal<Unit>` and the latest-only overload. The same scope can observe bit changes; dispose all three subscriptions/signals before disposing the client.

```csharp
using ReactiveUI.Primitives;
using ReactiveUI.Primitives.Signals;

using var refresh = new Signal<Unit>();
using var bits = plc.ObserveBits("M100", 8, TimeSpan.FromMilliseconds(250), minimumUpdateSpacing: null)
    .Subscribe(reply =>
    {
        if (reply.IsSucceed) Console.WriteLine(string.Join(',', reply.Value!));
    });
using var latest = plc.ObserveWordsLatest("D100", 2, refresh)
    .Subscribe(reply =>
    {
        if (reply.IsSucceed) Console.WriteLine(string.Join(',', reply.Value!));
    });
refresh.OnNext(Unit.Default); // a UI refresh or a configuration-change trigger
```

`ObserveReactiveTag<T>` and `CreateReactiveTagWritePipeline<T>` use the validated `TagDatabase` metadata. A typed key prevents an accidental value-shape mismatch, while the write pipeline serializes/coalesces desired values.

```csharp
using IoT.Driver.Core;

var temperatureKey = new LogicalTagKey<float>("Temperature");
using var quality = plc.ObserveReactiveTag(temperatureKey, TimeSpan.FromMilliseconds(250), null)
    .Subscribe(sample =>
    {
        if (sample.Quality == MitsubishiReactiveQuality.Good && sample.Value is float value)
            Console.WriteLine($"Temperature = {value}");
    });
using var setpointWriter = plc.CreateReactiveTagWritePipeline(
    temperatureKey, MitsubishiReactiveWriteMode.LatestWins, TimeSpan.FromMilliseconds(100));
using var writes = setpointWriter.Results.Subscribe(result => Console.WriteLine($"{result.Target}: {result.Success}"));
setpointWriter.Post(22.5f);
```

Group streams provide the equivalent heartbeat and latest-only forms. A group must exist in the assigned tag database before subscribing.

```csharp
using ReactiveUI.Primitives;
using ReactiveUI.Primitives.Signals;

using var groupHeartbeats = plc.ObserveTagGroupHeartbeat(
    "RecipeCommit", TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(3), minimumUpdateSpacing: null)
    .Subscribe(sample => Console.WriteLine($"Heartbeat={sample.IsHeartbeat}, success={sample.Value.IsSucceed}"));
using var groupRefresh = new Signal<Unit>();
using var latestGroup = plc.ObserveTagGroupLatest("RecipeCommit", groupRefresh)
    .Subscribe(reply =>
    {
        if (reply.IsSucceed) Console.WriteLine($"Recipe values: {reply.Value!.Values.Count}");
    });
groupRefresh.OnNext(Unit.Default);
```

### Tags and logical tags

`MitsubishiTagDatabase.Load`, `FromCsv`, `FromJson`, and `FromYaml` create a database. Assign it to `TagDatabase`, then use `ReadWordsByTagAsync`, `ReadBitsByTagAsync`, `WriteWordsByTagAsync`, `WriteBitsByTagAsync`, random tag methods, and typed tag methods for `Int16`, `UInt16`, `Int32`, `DWord`, `Float`, scaled `Double`, and strings. `ValidateTagDatabase`, `LoadAndValidateTagDatabase`, `PreviewTagDatabaseDiff`, and `ObserveTagDatabaseDiff` / `ObserveTagDatabaseReload` support controlled schema rollout. `ReadTagGroupSnapshotAsync`, `ValidateTagGroupWrite`, and `WriteTagGroupValuesAsync` / `WriteTagGroupSnapshotAsync` operate on defined groups.

```csharp
var tags = MitsubishiTagDatabase.FromCsv("Name,Address,DataType\nTemperature,D100,UInt16");
plc.TagDatabase = tags;
var temperature = await plc.ReadUInt16ByTagAsync("Temperature", CancellationToken.None);
if (temperature.IsSucceed) Console.WriteLine(temperature.Value);
```

`CreateLogicalTagClient` composes the shared `ILogicalTagCatalog` and optional SQLite store with the Mitsubishi transport. Use it where tag definitions, persistence, batch reads/writes, and `IObservable` / `IAsyncEnumerable` observation are required.

### Transports and test doubles

Implement `IMitsubishiTransport` only for a custom endpoint. The normal built-in TCP/UDP and serial transports are selected automatically from the configured `TransportKind`. `MitsubishiSimulatorMemory` and `MitsubishiSimulatorTransport` provide deterministic in-process test behavior, including queued responses/faults and stateful memory.

## Complete public API inventory

This is the public type inventory from `src/MitsubishiRx`; the reactive package publishes the same types in its `.Reactive` namespace.

| Area | Public types / members |
| --- | --- |
| Client construction and state | `MitsubishiRx(CpuType, ip, port, timeout)`, `MitsubishiRx(MitsubishiClientOptions, IMitsubishiTransport?, scheduler?)`, `Options`, `TagDatabase`, `Connected`, `ConnectionStates`, `OperationLogs`, `OpenAsync`, `CloseAsync`, `Dispose`/`DisposeAsync`. `OpenAsync` creates the configured transport session; `CloseAsync` disconnects it without discarding the options. |
| Raw and direct protocol | `SendPackageAsync`, `SendPackageSingleAsync`, `SendPackageReliableAsync` send pre-encoded frames; `ExecuteRawAsync(MitsubishiRawCommandRequest, token)`; `ReadWordsAsync`, `WriteWordsAsync`, `ReadBitsAsync`, `WriteBitsAsync`; all direct operation overloads take a device address plus point count/value list and cancellation token. `Read*` returns `Responce<T>`; writes return `Responce`. |
| Sparse, monitor, and block protocol | `RandomReadWordsAsync(IEnumerable<string>, token)`, `RandomWriteWordsAsync(IEnumerable<KeyValuePair<string, ushort>>, token)`, `RegisterMonitorAsync`, `ExecuteMonitorAsync`, `ReadBlocksAsync(MitsubishiBlockRequest, token)`, `WriteBlocksAsync(MitsubishiBlockRequest, token)`. Random and monitor requests return raw/word results in address order; block responses are raw and must be interpreted in request order. |
| Diagnostics and controller state | `ReadTypeNameAsync`, `RemoteRunAsync(force, clearMode, token)`, `RemoteStopAsync`, `RemotePauseAsync`, `RemoteLatchClearAsync`, `RemoteResetAsync`, `UnlockAsync(password, token)`, `LockAsync(password, token)`, `ClearErrorAsync`, `LoopbackAsync`, `ReadMemoryAsync(command,address,length,token)`, `WriteMemoryAsync(command,address,values,token)`. The remote and protection family must be protected by an application interlock. |
| Polling and health | `ObserveWords`, `ObserveBits`, `ObserveWordsHeartbeat`, `ObserveWordsStale`, `ObserveWordsLatest`, `ObserveTagGroup`, `ObserveTagGroupHeartbeat`, `ObserveTagGroupStale`, `ObserveTagGroupLatest`, `SampleDiagnostics`, `ObserveConnectionHealth`. Interval overloads schedule reads; trigger/latest overloads coalesce trigger bursts. Dispose the returned subscription. |
| Reactive quality and writes | `ObserveReactiveWords`, `ObserveReactiveTag<T>`, `ObserveReactiveTagGroup`, `CreateReactiveWordWritePipeline`, `CreateReactiveTagWritePipeline<T>`. `MitsubishiReactiveValue<T>` carries `Value`, `Quality`, timestamp and response/error context; `MitsubishiReactiveWritePipeline<TPayload>` exposes the write-result stream and is disposable. |
| Tag read/write methods | `ReadWordsByTagAsync`/`WriteWordsByTagAsync`, `ReadBitsByTagAsync`/`WriteBitsByTagAsync`, random by-tag methods, `Read`/`WriteInt16ByTagAsync`, `UInt16`, `Int32`, `DWord`, `Float`, `ScaledDouble`, `String`, plus metadata-dispatched `ReadTagAsync`/`WriteTagAsync`. Every member resolves the name through `TagDatabase`; typed pairs convert wire words using definition metadata. |
| Tag database and group methods | `ValidateTagDatabase`, `LoadAndValidateTagDatabase`, `PreviewTagDatabaseDiff`, `ObserveTagDatabaseDiff`, `ObserveTagDatabaseReload`, `ReadTagGroupSnapshotAsync`, `ValidateTagGroupWrite`, `WriteTagGroupValuesAsync`, `WriteTagGroupSnapshotAsync`, `CreateLogicalTagClient`. The validation/preview methods are synchronous configuration checks; observation methods return disposables. |
| Options, routes, and protocol values | `MitsubishiClientOptions`, `MitsubishiSerialOptions`, `MitsubishiRoute`, `MitsubishiSerialRoute`, `MitsubishiTransportRequest`, `MitsubishiRawCommandRequest`, `MitsubishiBlockRequest`, `MitsubishiCommands`; enums `CpuType`, `MitsubishiFrameType`, `CommunicationDataCode`, `MitsubishiTransportKind`, `MitsubishiSerialMessageFormat`, `XyAddressNotation`, `DeviceNumberFormat`, `DeviceValueKind`, `MitsubishiConnectionState`. They are value/configuration types; set a route/format explicitly rather than relying on a CPU guess. |
| Address, response, blocks, and logging | `Responce`, `Responce<T>`, `MitsubishiDeviceAddress`, `MitsubishiDeviceMetadata`, `MitsubishiDeviceValue`, `MitsubishiTypeName`, `MitsubishiWordBlock`, `MitsubishiBitBlock`, `MitsubishiOperationLog`. `Responce` owns success/error/timing data; the block/device records describe request payloads and parsed addresses. |
| Tag schema values | `MitsubishiTagDatabase`, `MitsubishiTagDefinition`, `MitsubishiTagGroupDefinition`, `MitsubishiTagDatabaseDiff`, `MitsubishiTagChange`, `MitsubishiTagGroupChange`, `MitsubishiTagGroupSnapshot`, `MitsubishiTagRolloutPolicy`, `MitsubishiSchemaChangeKind`, `MitsubishiTagValueConverter`. `Load`/`From*`/`To*`/`Save`, `Add`, `TryGet`, `GetRequired`, group equivalents, and `CompareWith` are the database member families. Serialization documents and schema-format selectors are internal implementation details. |
| Logical-tag and bulk metrics | `MitsubishiLogicalTagRegistration`, `MitsubishiLogicalTagClient`, `MitsubishiLogicalTagBulkReadRequest`, `MitsubishiLogicalTagBulkWriteRequest`, `MitsubishiLogicalTagBulkDirectionMetrics`, `MitsubishiLogicalTagBulkOperationMetrics`. The client follows the shared logical-tag contracts for catalog/store CRUD, single/bulk operations and observation. |
| Transport and simulation | `IMitsubishiTransport` (`ConnectAsync`, `DisconnectAsync`, `ExchangeAsync`, disposal), `MitsubishiSimulatorTransport`, `MitsubishiSimulatorMemory`, `MitsubishiSimulatorDeviceValue`. The built-in TCP/UDP and serial transports are selected automatically from `MitsubishiClientOptions`; simulator members include request/connect counts, response/fault queues, `Snapshot`, word/bit read/write and `Clear`; use them for deterministic tests. |
| Generation | The standalone `IoT-Driver.MitsubishiRx.Generators` package supplies `MitsubishiTagClientAttribute`, `MitsubishiTagAttribute`, `MitsubishiTagClientSchemaAttribute`, and `MitsubishiTagClientGenerator`. The generated model exposes `GeneratedMitsubishiTagClient`, `GeneratedMitsubishiTagClientExtensions`, `GroupsClient`, and per-group `TagsClient` accessors; attributes are compile-time schema, not runtime PLC discovery. |

## Operational guidance

Keep one client per endpoint and dispose it at shutdown. Prefer bounded, contiguous reads; select a scan interval that the PLC, network, and downstream consumer can sustain. Serialize application-level writes and make retries idempotent. Log `OperationLogs` with endpoint, command, result, and correlation information, but never log passwords. Treat tag database changes as versioned configuration: validate and preview the diff before applying it.

## Troubleshooting

| Symptom | Check |
| --- | --- |
| Open or request timeout | Host/port, firewall, selected TCP/UDP path, `Timeout`, and whether the PLC is configured for the selected frame. |
| Protocol error | 1E/3E/4E or 1C/3C/4C, binary/ASCII, route, monitoring timer, and `XyNotation`. |
| Serial failures | Port ownership, baud/data/parity/stop/handshake, station/routing values, and serial message format. |
| Tag lookup/type failure | Validate the database, address, point count, declared type, and string/scaling metadata before reading. |
| Polling overload | Increase scan interval, group contiguous values, unsubscribe unused streams, and avoid overlapping writes. |

## AI skill

For source-grounded implementation assistance, use [skills/mitsubishi-rx/SKILL.md](../../skills/mitsubishi-rx/SKILL.md). It directs an agent to inspect this README and the current source before proposing protocol, tag, or safety-sensitive code.

<!-- BEGIN GENERATED PUBLIC API -->

## Exhaustive public API reference

This catalogue is generated from the packaged runtime assemblies and their XML documentation. It includes exported public types and their declared public members; inherited members and non-public implementation details are intentionally omitted.

### `MitsubishiRx`

Exported public types: 54; declared public members: 670.

#### `T:IoT.Driver.MitsubishiRx.CommunicationDataCode`

```csharp
public enum IoT.Driver.MitsubishiRx.CommunicationDataCode
```
Defines the CommunicationDataCode values.

##### Declared public members

###### `F:IoT.Driver.MitsubishiRx.CommunicationDataCode.Ascii`

```csharp
public static const IoT.Driver.MitsubishiRx.CommunicationDataCode Ascii
```
Represents the Ascii option.

###### `F:IoT.Driver.MitsubishiRx.CommunicationDataCode.Binary`

```csharp
public static const IoT.Driver.MitsubishiRx.CommunicationDataCode Binary
```
Represents the Binary option.

#### `T:IoT.Driver.MitsubishiRx.CpuType`

```csharp
public enum IoT.Driver.MitsubishiRx.CpuType
```
Defines the CpuType values.

##### Declared public members

###### `F:IoT.Driver.MitsubishiRx.CpuType.ASeries`

```csharp
public static const IoT.Driver.MitsubishiRx.CpuType ASeries
```
Represents the ASeries option.

###### `F:IoT.Driver.MitsubishiRx.CpuType.Fx3`

```csharp
public static const IoT.Driver.MitsubishiRx.CpuType Fx3
```
Represents the Fx3 option.

###### `F:IoT.Driver.MitsubishiRx.CpuType.Fx5`

```csharp
public static const IoT.Driver.MitsubishiRx.CpuType Fx5
```
Represents the Fx5 option.

###### `F:IoT.Driver.MitsubishiRx.CpuType.IQR`

```csharp
public static const IoT.Driver.MitsubishiRx.CpuType IQR
```
Represents the IQR option.

###### `F:IoT.Driver.MitsubishiRx.CpuType.LSeries`

```csharp
public static const IoT.Driver.MitsubishiRx.CpuType LSeries
```
Represents the LSeries option.

###### `F:IoT.Driver.MitsubishiRx.CpuType.None`

```csharp
public static const IoT.Driver.MitsubishiRx.CpuType None
```
Represents the None option.

###### `F:IoT.Driver.MitsubishiRx.CpuType.QSeries`

```csharp
public static const IoT.Driver.MitsubishiRx.CpuType QSeries
```
Represents the QSeries option.

###### `F:IoT.Driver.MitsubishiRx.CpuType.QnaSeries`

```csharp
public static const IoT.Driver.MitsubishiRx.CpuType QnaSeries
```
Represents the QnaSeries option.

#### `T:IoT.Driver.MitsubishiRx.DeviceNumberFormat`

```csharp
public enum IoT.Driver.MitsubishiRx.DeviceNumberFormat
```
Defines the DeviceNumberFormat values.

##### Declared public members

###### `F:IoT.Driver.MitsubishiRx.DeviceNumberFormat.Decimal`

```csharp
public static const IoT.Driver.MitsubishiRx.DeviceNumberFormat Decimal
```
Represents the Decimal option.

###### `F:IoT.Driver.MitsubishiRx.DeviceNumberFormat.Hexadecimal`

```csharp
public static const IoT.Driver.MitsubishiRx.DeviceNumberFormat Hexadecimal
```
Represents the Hexadecimal option.

###### `F:IoT.Driver.MitsubishiRx.DeviceNumberFormat.Octal`

```csharp
public static const IoT.Driver.MitsubishiRx.DeviceNumberFormat Octal
```
Represents the Octal option.

###### `F:IoT.Driver.MitsubishiRx.DeviceNumberFormat.XyVariable`

```csharp
public static const IoT.Driver.MitsubishiRx.DeviceNumberFormat XyVariable
```
Represents the XyVariable option.

#### `T:IoT.Driver.MitsubishiRx.DeviceValueKind`

```csharp
public enum IoT.Driver.MitsubishiRx.DeviceValueKind
```
Defines the DeviceValueKind values.

##### Declared public members

###### `F:IoT.Driver.MitsubishiRx.DeviceValueKind.Bit`

```csharp
public static const IoT.Driver.MitsubishiRx.DeviceValueKind Bit
```
Represents the Bit option.

###### `F:IoT.Driver.MitsubishiRx.DeviceValueKind.Word`

```csharp
public static const IoT.Driver.MitsubishiRx.DeviceValueKind Word
```
Represents the Word option.

#### `T:IoT.Driver.MitsubishiRx.IMitsubishiTransport`

```csharp
public interface IoT.Driver.MitsubishiRx.IMitsubishiTransport
```
Provides the IMitsubishiTransport contract.

##### Declared public members

###### `M:IoT.Driver.MitsubishiRx.IMitsubishiTransport.ConnectAsync(IoT.Driver.MitsubishiRx.MitsubishiClientOptions,System.Threading.CancellationToken)`

```csharp
public System.Threading.Tasks.ValueTask ConnectAsync(IoT.Driver.MitsubishiRx.MitsubishiClientOptions options, System.Threading.CancellationToken cancellationToken)
```
Executes the ConnectAsync operation.

- Parameter `options`: The options parameter.
- Parameter `cancellationToken`: The cancellationToken parameter.
- Returns: The ConnectAsync operation result.

###### `M:IoT.Driver.MitsubishiRx.IMitsubishiTransport.DisconnectAsync(System.Threading.CancellationToken)`

```csharp
public System.Threading.Tasks.ValueTask DisconnectAsync(System.Threading.CancellationToken cancellationToken)
```
Executes the DisconnectAsync operation.

- Parameter `cancellationToken`: The cancellationToken parameter.
- Returns: The DisconnectAsync operation result.

###### `M:IoT.Driver.MitsubishiRx.IMitsubishiTransport.ExchangeAsync(IoT.Driver.MitsubishiRx.MitsubishiTransportRequest,System.Threading.CancellationToken)`

```csharp
public System.Threading.Tasks.ValueTask<byte[]> ExchangeAsync(IoT.Driver.MitsubishiRx.MitsubishiTransportRequest request, System.Threading.CancellationToken cancellationToken)
```
Executes the ExchangeAsync operation.

- Parameter `request`: The request parameter.
- Parameter `cancellationToken`: The cancellationToken parameter.
- Returns: The ExchangeAsync operation result.

###### `P:IoT.Driver.MitsubishiRx.IMitsubishiTransport.IsConnected`

```csharp
public bool IsConnected { get; }
```
Gets or sets the IsConnected property.

- Value: The `IsConnected` value.

#### `T:IoT.Driver.MitsubishiRx.MitsubishiBitBlock`

```csharp
public class IoT.Driver.MitsubishiRx.MitsubishiBitBlock
```
Provides the MitsubishiBitBlock record.

##### Declared public members

###### `M:IoT.Driver.MitsubishiRx.MitsubishiBitBlock.#ctor(IoT.Driver.MitsubishiRx.MitsubishiDeviceAddress,System.ReadOnlyMemory`1{System.Boolean})`

```csharp
public IoT.Driver.MitsubishiRx.MitsubishiBitBlock(IoT.Driver.MitsubishiRx.MitsubishiDeviceAddress Address, System.ReadOnlyMemory<bool> Values)
```
Initializes a new instance of `IoT.Driver.MitsubishiRx.MitsubishiBitBlock`.

- Parameter `Address`: The `Address` value.
- Parameter `Values`: The `Values` value.

###### `M:IoT.Driver.MitsubishiRx.MitsubishiBitBlock.Deconstruct(IoT.Driver.MitsubishiRx.MitsubishiDeviceAddress@,System.ReadOnlyMemory`1{System.Boolean}@)`

```csharp
public void Deconstruct(out IoT.Driver.MitsubishiRx.MitsubishiDeviceAddress Address, out System.ReadOnlyMemory<bool> Values)
```
Deconstructs the value into its component values.

- Parameter `Address`: The `Address` value.
- Parameter `Values`: The `Values` value.

###### `M:IoT.Driver.MitsubishiRx.MitsubishiBitBlock.Equals(IoT.Driver.MitsubishiRx.MitsubishiBitBlock)`

```csharp
public bool Equals(IoT.Driver.MitsubishiRx.MitsubishiBitBlock other)
```
Determines whether the supplied value is equal to the current value.

- Parameter `other`: The `other` value.
- Returns: A `bool` result.

###### `M:IoT.Driver.MitsubishiRx.MitsubishiBitBlock.Equals(System.Object)`

```csharp
public bool Equals(object obj)
```
Determines whether the supplied value is equal to the current value.

- Parameter `obj`: The `obj` value.
- Returns: A `bool` result.

###### `M:IoT.Driver.MitsubishiRx.MitsubishiBitBlock.GetHashCode`

```csharp
public int GetHashCode()
```
Returns the hash code for the current value.

- Returns: A `int` result.

###### `M:IoT.Driver.MitsubishiRx.MitsubishiBitBlock.ToString`

```csharp
public string ToString()
```
Returns a string representation of the current value.

- Returns: A `string` result.

###### `M:IoT.Driver.MitsubishiRx.MitsubishiBitBlock.op_Equality(IoT.Driver.MitsubishiRx.MitsubishiBitBlock,IoT.Driver.MitsubishiRx.MitsubishiBitBlock)`

```csharp
public static bool op_Equality(IoT.Driver.MitsubishiRx.MitsubishiBitBlock left, IoT.Driver.MitsubishiRx.MitsubishiBitBlock right)
```
Determines whether the two supplied values are equal.

- Parameter `left`: The `left` value.
- Parameter `right`: The `right` value.
- Returns: A `bool` result.

###### `M:IoT.Driver.MitsubishiRx.MitsubishiBitBlock.op_Inequality(IoT.Driver.MitsubishiRx.MitsubishiBitBlock,IoT.Driver.MitsubishiRx.MitsubishiBitBlock)`

```csharp
public static bool op_Inequality(IoT.Driver.MitsubishiRx.MitsubishiBitBlock left, IoT.Driver.MitsubishiRx.MitsubishiBitBlock right)
```
Determines whether the two supplied values are not equal.

- Parameter `left`: The `left` value.
- Parameter `right`: The `right` value.
- Returns: A `bool` result.

###### `P:IoT.Driver.MitsubishiRx.MitsubishiBitBlock.Address`

```csharp
public IoT.Driver.MitsubishiRx.MitsubishiDeviceAddress Address { get; set; }
```
The Address parameter.

- Value: The `Address` value.

###### `P:IoT.Driver.MitsubishiRx.MitsubishiBitBlock.Values`

```csharp
public System.ReadOnlyMemory<bool> Values { get; set; }
```
The Values parameter.

- Value: The `Values` value.

#### `T:IoT.Driver.MitsubishiRx.MitsubishiBlockRequest`

```csharp
public class IoT.Driver.MitsubishiRx.MitsubishiBlockRequest
```
Provides the MitsubishiBlockRequest record.

##### Declared public members

###### `M:IoT.Driver.MitsubishiRx.MitsubishiBlockRequest.#ctor(System.Collections.Generic.IReadOnlyList`1{IoT.Driver.MitsubishiRx.MitsubishiWordBlock},System.Collections.Generic.IReadOnlyList`1{IoT.Driver.MitsubishiRx.MitsubishiBitBlock})`

```csharp
public IoT.Driver.MitsubishiRx.MitsubishiBlockRequest(System.Collections.Generic.IReadOnlyList<IoT.Driver.MitsubishiRx.MitsubishiWordBlock> WordBlocks, System.Collections.Generic.IReadOnlyList<IoT.Driver.MitsubishiRx.MitsubishiBitBlock> BitBlocks)
```
Initializes a new instance of `IoT.Driver.MitsubishiRx.MitsubishiBlockRequest`.

- Parameter `WordBlocks`: The `WordBlocks` value.
- Parameter `BitBlocks`: The `BitBlocks` value.

###### `M:IoT.Driver.MitsubishiRx.MitsubishiBlockRequest.Deconstruct(System.Collections.Generic.IReadOnlyList`1{IoT.Driver.MitsubishiRx.MitsubishiWordBlock}@,System.Collections.Generic.IReadOnlyList`1{IoT.Driver.MitsubishiRx.MitsubishiBitBlock}@)`

```csharp
public void Deconstruct(out System.Collections.Generic.IReadOnlyList<IoT.Driver.MitsubishiRx.MitsubishiWordBlock> WordBlocks, out System.Collections.Generic.IReadOnlyList<IoT.Driver.MitsubishiRx.MitsubishiBitBlock> BitBlocks)
```
Deconstructs the value into its component values.

- Parameter `WordBlocks`: The `WordBlocks` value.
- Parameter `BitBlocks`: The `BitBlocks` value.

###### `M:IoT.Driver.MitsubishiRx.MitsubishiBlockRequest.Equals(IoT.Driver.MitsubishiRx.MitsubishiBlockRequest)`

```csharp
public bool Equals(IoT.Driver.MitsubishiRx.MitsubishiBlockRequest other)
```
Determines whether the supplied value is equal to the current value.

- Parameter `other`: The `other` value.
- Returns: A `bool` result.

###### `M:IoT.Driver.MitsubishiRx.MitsubishiBlockRequest.Equals(System.Object)`

```csharp
public bool Equals(object obj)
```
Determines whether the supplied value is equal to the current value.

- Parameter `obj`: The `obj` value.
- Returns: A `bool` result.

###### `M:IoT.Driver.MitsubishiRx.MitsubishiBlockRequest.GetHashCode`

```csharp
public int GetHashCode()
```
Returns the hash code for the current value.

- Returns: A `int` result.

###### `M:IoT.Driver.MitsubishiRx.MitsubishiBlockRequest.ToString`

```csharp
public string ToString()
```
Returns a string representation of the current value.

- Returns: A `string` result.

###### `M:IoT.Driver.MitsubishiRx.MitsubishiBlockRequest.op_Equality(IoT.Driver.MitsubishiRx.MitsubishiBlockRequest,IoT.Driver.MitsubishiRx.MitsubishiBlockRequest)`

```csharp
public static bool op_Equality(IoT.Driver.MitsubishiRx.MitsubishiBlockRequest left, IoT.Driver.MitsubishiRx.MitsubishiBlockRequest right)
```
Determines whether the two supplied values are equal.

- Parameter `left`: The `left` value.
- Parameter `right`: The `right` value.
- Returns: A `bool` result.

###### `M:IoT.Driver.MitsubishiRx.MitsubishiBlockRequest.op_Inequality(IoT.Driver.MitsubishiRx.MitsubishiBlockRequest,IoT.Driver.MitsubishiRx.MitsubishiBlockRequest)`

```csharp
public static bool op_Inequality(IoT.Driver.MitsubishiRx.MitsubishiBlockRequest left, IoT.Driver.MitsubishiRx.MitsubishiBlockRequest right)
```
Determines whether the two supplied values are not equal.

- Parameter `left`: The `left` value.
- Parameter `right`: The `right` value.
- Returns: A `bool` result.

###### `P:IoT.Driver.MitsubishiRx.MitsubishiBlockRequest.BitBlocks`

```csharp
public System.Collections.Generic.IReadOnlyList<IoT.Driver.MitsubishiRx.MitsubishiBitBlock> BitBlocks { get; set; }
```
The BitBlocks parameter.

- Value: The `BitBlocks` value.

###### `P:IoT.Driver.MitsubishiRx.MitsubishiBlockRequest.ResolvedBitBlocks`

```csharp
public System.Collections.Generic.IReadOnlyList<IoT.Driver.MitsubishiRx.MitsubishiBitBlock> ResolvedBitBlocks { get; }
```
Gets or sets the ResolvedBitBlocks property.

- Value: The `ResolvedBitBlocks` value.

###### `P:IoT.Driver.MitsubishiRx.MitsubishiBlockRequest.ResolvedWordBlocks`

```csharp
public System.Collections.Generic.IReadOnlyList<IoT.Driver.MitsubishiRx.MitsubishiWordBlock> ResolvedWordBlocks { get; }
```
Gets or sets the ResolvedWordBlocks property.

- Value: The `ResolvedWordBlocks` value.

###### `P:IoT.Driver.MitsubishiRx.MitsubishiBlockRequest.WordBlocks`

```csharp
public System.Collections.Generic.IReadOnlyList<IoT.Driver.MitsubishiRx.MitsubishiWordBlock> WordBlocks { get; set; }
```
The WordBlocks parameter.

- Value: The `WordBlocks` value.

#### `T:IoT.Driver.MitsubishiRx.MitsubishiClientOptions`

```csharp
public class IoT.Driver.MitsubishiRx.MitsubishiClientOptions
```
Provides the MitsubishiClientOptions record.

##### Declared public members

###### `M:IoT.Driver.MitsubishiRx.MitsubishiClientOptions.#ctor(System.String,System.Int32,IoT.Driver.MitsubishiRx.MitsubishiFrameType,IoT.Driver.MitsubishiRx.CommunicationDataCode,IoT.Driver.MitsubishiRx.MitsubishiTransportKind,IoT.Driver.MitsubishiRx.MitsubishiRoute,System.UInt16,System.Nullable`1{System.TimeSpan},IoT.Driver.MitsubishiRx.CpuType,IoT.Driver.MitsubishiRx.XyAddressNotation,System.Byte,System.Func`1{System.UInt16},IoT.Driver.MitsubishiRx.MitsubishiSerialOptions)`

```csharp
public IoT.Driver.MitsubishiRx.MitsubishiClientOptions(string Host, int Port, IoT.Driver.MitsubishiRx.MitsubishiFrameType FrameType, IoT.Driver.MitsubishiRx.CommunicationDataCode DataCode, IoT.Driver.MitsubishiRx.MitsubishiTransportKind TransportKind, IoT.Driver.MitsubishiRx.MitsubishiRoute Route, ushort MonitoringTimer, System.Nullable<System.TimeSpan> Timeout, IoT.Driver.MitsubishiRx.CpuType CpuType, IoT.Driver.MitsubishiRx.XyAddressNotation XyNotation, byte LegacyPcNumber, System.Func<ushort> SerialNumberProvider, IoT.Driver.MitsubishiRx.MitsubishiSerialOptions Serial)
```
Initializes a new instance of `IoT.Driver.MitsubishiRx.MitsubishiClientOptions`.

- Parameter `Host`: The `Host` value.
- Parameter `Port`: The `Port` value.
- Parameter `FrameType`: The `FrameType` value.
- Parameter `DataCode`: The `DataCode` value.
- Parameter `TransportKind`: The `TransportKind` value.
- Parameter `Route`: The `Route` value.
- Parameter `MonitoringTimer`: The `MonitoringTimer` value.
- Parameter `Timeout`: The `Timeout` value.
- Parameter `CpuType`: The `CpuType` value.
- Parameter `XyNotation`: The `XyNotation` value.
- Parameter `LegacyPcNumber`: The `LegacyPcNumber` value.
- Parameter `SerialNumberProvider`: The `SerialNumberProvider` value.
- Parameter `Serial`: The `Serial` value.

###### `M:IoT.Driver.MitsubishiRx.MitsubishiClientOptions.Deconstruct(System.String@,System.Int32@,IoT.Driver.MitsubishiRx.MitsubishiFrameType@,IoT.Driver.MitsubishiRx.CommunicationDataCode@,IoT.Driver.MitsubishiRx.MitsubishiTransportKind@,IoT.Driver.MitsubishiRx.MitsubishiRoute@,System.UInt16@,System.Nullable`1{System.TimeSpan}@,IoT.Driver.MitsubishiRx.CpuType@,IoT.Driver.MitsubishiRx.XyAddressNotation@,System.Byte@,System.Func`1{System.UInt16}@,IoT.Driver.MitsubishiRx.MitsubishiSerialOptions@)`

```csharp
public void Deconstruct(out string Host, out int Port, out IoT.Driver.MitsubishiRx.MitsubishiFrameType FrameType, out IoT.Driver.MitsubishiRx.CommunicationDataCode DataCode, out IoT.Driver.MitsubishiRx.MitsubishiTransportKind TransportKind, out IoT.Driver.MitsubishiRx.MitsubishiRoute Route, out ushort MonitoringTimer, out System.Nullable<System.TimeSpan> Timeout, out IoT.Driver.MitsubishiRx.CpuType CpuType, out IoT.Driver.MitsubishiRx.XyAddressNotation XyNotation, out byte LegacyPcNumber, out System.Func<ushort> SerialNumberProvider, out IoT.Driver.MitsubishiRx.MitsubishiSerialOptions Serial)
```
Deconstructs the value into its component values.

- Parameter `Host`: The `Host` value.
- Parameter `Port`: The `Port` value.
- Parameter `FrameType`: The `FrameType` value.
- Parameter `DataCode`: The `DataCode` value.
- Parameter `TransportKind`: The `TransportKind` value.
- Parameter `Route`: The `Route` value.
- Parameter `MonitoringTimer`: The `MonitoringTimer` value.
- Parameter `Timeout`: The `Timeout` value.
- Parameter `CpuType`: The `CpuType` value.
- Parameter `XyNotation`: The `XyNotation` value.
- Parameter `LegacyPcNumber`: The `LegacyPcNumber` value.
- Parameter `SerialNumberProvider`: The `SerialNumberProvider` value.
- Parameter `Serial`: The `Serial` value.

###### `M:IoT.Driver.MitsubishiRx.MitsubishiClientOptions.Equals(IoT.Driver.MitsubishiRx.MitsubishiClientOptions)`

```csharp
public bool Equals(IoT.Driver.MitsubishiRx.MitsubishiClientOptions other)
```
Determines whether the supplied value is equal to the current value.

- Parameter `other`: The `other` value.
- Returns: A `bool` result.

###### `M:IoT.Driver.MitsubishiRx.MitsubishiClientOptions.Equals(System.Object)`

```csharp
public bool Equals(object obj)
```
Determines whether the supplied value is equal to the current value.

- Parameter `obj`: The `obj` value.
- Returns: A `bool` result.

###### `M:IoT.Driver.MitsubishiRx.MitsubishiClientOptions.GetHashCode`

```csharp
public int GetHashCode()
```
Returns the hash code for the current value.

- Returns: A `int` result.

###### `M:IoT.Driver.MitsubishiRx.MitsubishiClientOptions.GetNextSerialNumber`

```csharp
public ushort GetNextSerialNumber()
```
Executes the GetNextSerialNumber operation.

- Returns: The GetNextSerialNumber operation result.

###### `M:IoT.Driver.MitsubishiRx.MitsubishiClientOptions.ToString`

```csharp
public string ToString()
```
Returns a string representation of the current value.

- Returns: A `string` result.

###### `M:IoT.Driver.MitsubishiRx.MitsubishiClientOptions.op_Equality(IoT.Driver.MitsubishiRx.MitsubishiClientOptions,IoT.Driver.MitsubishiRx.MitsubishiClientOptions)`

```csharp
public static bool op_Equality(IoT.Driver.MitsubishiRx.MitsubishiClientOptions left, IoT.Driver.MitsubishiRx.MitsubishiClientOptions right)
```
Determines whether the two supplied values are equal.

- Parameter `left`: The `left` value.
- Parameter `right`: The `right` value.
- Returns: A `bool` result.

###### `M:IoT.Driver.MitsubishiRx.MitsubishiClientOptions.op_Inequality(IoT.Driver.MitsubishiRx.MitsubishiClientOptions,IoT.Driver.MitsubishiRx.MitsubishiClientOptions)`

```csharp
public static bool op_Inequality(IoT.Driver.MitsubishiRx.MitsubishiClientOptions left, IoT.Driver.MitsubishiRx.MitsubishiClientOptions right)
```
Determines whether the two supplied values are not equal.

- Parameter `left`: The `left` value.
- Parameter `right`: The `right` value.
- Returns: A `bool` result.

###### `P:IoT.Driver.MitsubishiRx.MitsubishiClientOptions.CpuType`

```csharp
public IoT.Driver.MitsubishiRx.CpuType CpuType { get; set; }
```
The CpuType parameter.

- Value: The `CpuType` value.

###### `P:IoT.Driver.MitsubishiRx.MitsubishiClientOptions.DataCode`

```csharp
public IoT.Driver.MitsubishiRx.CommunicationDataCode DataCode { get; set; }
```
The DataCode parameter.

- Value: The `DataCode` value.

###### `P:IoT.Driver.MitsubishiRx.MitsubishiClientOptions.FrameType`

```csharp
public IoT.Driver.MitsubishiRx.MitsubishiFrameType FrameType { get; set; }
```
The FrameType parameter.

- Value: The `FrameType` value.

###### `P:IoT.Driver.MitsubishiRx.MitsubishiClientOptions.Host`

```csharp
public string Host { get; set; }
```
The Host parameter.

- Value: The `Host` value.

###### `P:IoT.Driver.MitsubishiRx.MitsubishiClientOptions.LegacyPcNumber`

```csharp
public byte LegacyPcNumber { get; set; }
```
The LegacyPcNumber parameter.

- Value: The `LegacyPcNumber` value.

###### `P:IoT.Driver.MitsubishiRx.MitsubishiClientOptions.MonitoringTimer`

```csharp
public ushort MonitoringTimer { get; set; }
```
The MonitoringTimer parameter.

- Value: The `MonitoringTimer` value.

###### `P:IoT.Driver.MitsubishiRx.MitsubishiClientOptions.Port`

```csharp
public int Port { get; set; }
```
The Port parameter.

- Value: The `Port` value.

###### `P:IoT.Driver.MitsubishiRx.MitsubishiClientOptions.ResolvedRoute`

```csharp
public IoT.Driver.MitsubishiRx.MitsubishiRoute ResolvedRoute { get; }
```
Gets or sets the ResolvedRoute property.

- Value: The `ResolvedRoute` value.

###### `P:IoT.Driver.MitsubishiRx.MitsubishiClientOptions.ResolvedSerial`

```csharp
public IoT.Driver.MitsubishiRx.MitsubishiSerialOptions ResolvedSerial { get; }
```
Gets or sets the ResolvedSerial property.

- Value: The `ResolvedSerial` value.

###### `P:IoT.Driver.MitsubishiRx.MitsubishiClientOptions.ResolvedTimeout`

```csharp
public System.TimeSpan ResolvedTimeout { get; }
```
Gets or sets the ResolvedTimeout property.

- Value: The `ResolvedTimeout` value.

###### `P:IoT.Driver.MitsubishiRx.MitsubishiClientOptions.Route`

```csharp
public IoT.Driver.MitsubishiRx.MitsubishiRoute Route { get; set; }
```
The Route parameter.

- Value: The `Route` value.

###### `P:IoT.Driver.MitsubishiRx.MitsubishiClientOptions.Serial`

```csharp
public IoT.Driver.MitsubishiRx.MitsubishiSerialOptions Serial { get; set; }
```
The Serial parameter.

- Value: The `Serial` value.

###### `P:IoT.Driver.MitsubishiRx.MitsubishiClientOptions.SerialNumberProvider`

```csharp
public System.Func<ushort> SerialNumberProvider { get; set; }
```
The SerialNumberProvider parameter.

- Value: The `SerialNumberProvider` value.

###### `P:IoT.Driver.MitsubishiRx.MitsubishiClientOptions.Timeout`

```csharp
public System.Nullable<System.TimeSpan> Timeout { get; set; }
```
The Timeout parameter.

- Value: The `Timeout` value.

###### `P:IoT.Driver.MitsubishiRx.MitsubishiClientOptions.TransportKind`

```csharp
public IoT.Driver.MitsubishiRx.MitsubishiTransportKind TransportKind { get; set; }
```
The TransportKind parameter.

- Value: The `TransportKind` value.

###### `P:IoT.Driver.MitsubishiRx.MitsubishiClientOptions.XyNotation`

```csharp
public IoT.Driver.MitsubishiRx.XyAddressNotation XyNotation { get; set; }
```
The XYNotation parameter.

- Value: The `XyNotation` value.

#### `T:IoT.Driver.MitsubishiRx.MitsubishiCommands`

```csharp
public class IoT.Driver.MitsubishiRx.MitsubishiCommands
```
Provides the MitsubishiCommands type.

##### Declared public members

###### `F:IoT.Driver.MitsubishiRx.MitsubishiCommands.BlockRead`

```csharp
public static ushort BlockRead
```
Stores the BlockRead field.

###### `F:IoT.Driver.MitsubishiRx.MitsubishiCommands.BlockWrite`

```csharp
public static ushort BlockWrite
```
Stores the BlockWrite field.

###### `F:IoT.Driver.MitsubishiRx.MitsubishiCommands.ClearError`

```csharp
public static ushort ClearError
```
Stores the ClearError field.

###### `F:IoT.Driver.MitsubishiRx.MitsubishiCommands.DeviceRead`

```csharp
public static ushort DeviceRead
```
Stores the DeviceRead field.

###### `F:IoT.Driver.MitsubishiRx.MitsubishiCommands.DeviceWrite`

```csharp
public static ushort DeviceWrite
```
Stores the DeviceWrite field.

###### `F:IoT.Driver.MitsubishiRx.MitsubishiCommands.EntryMonitorDevice`

```csharp
public static ushort EntryMonitorDevice
```
Stores the EntryMonitorDevice field.

###### `F:IoT.Driver.MitsubishiRx.MitsubishiCommands.ExecuteMonitor`

```csharp
public static ushort ExecuteMonitor
```
Stores the ExecuteMonitor field.

###### `F:IoT.Driver.MitsubishiRx.MitsubishiCommands.ExtendUnitRead`

```csharp
public static ushort ExtendUnitRead
```
Stores the ExtendUnitRead field.

###### `F:IoT.Driver.MitsubishiRx.MitsubishiCommands.ExtendUnitWrite`

```csharp
public static ushort ExtendUnitWrite
```
Stores the ExtendUnitWrite field.

###### `F:IoT.Driver.MitsubishiRx.MitsubishiCommands.Lock`

```csharp
public static ushort Lock
```
Stores the Lock field.

###### `F:IoT.Driver.MitsubishiRx.MitsubishiCommands.LoopbackTest`

```csharp
public static ushort LoopbackTest
```
Stores the LoopbackTest field.

###### `F:IoT.Driver.MitsubishiRx.MitsubishiCommands.MemoryRead`

```csharp
public static ushort MemoryRead
```
Stores the MemoryRead field.

###### `F:IoT.Driver.MitsubishiRx.MitsubishiCommands.MemoryWrite`

```csharp
public static ushort MemoryWrite
```
Stores the MemoryWrite field.

###### `F:IoT.Driver.MitsubishiRx.MitsubishiCommands.RandomRead`

```csharp
public static ushort RandomRead
```
Stores the RandomRead field.

###### `F:IoT.Driver.MitsubishiRx.MitsubishiCommands.RandomWrite`

```csharp
public static ushort RandomWrite
```
Stores the RandomWrite field.

###### `F:IoT.Driver.MitsubishiRx.MitsubishiCommands.ReadTypeName`

```csharp
public static ushort ReadTypeName
```
Stores the ReadTypeName field.

###### `F:IoT.Driver.MitsubishiRx.MitsubishiCommands.RemoteLatchClear`

```csharp
public static ushort RemoteLatchClear
```
Stores the RemoteLatchClear field.

###### `F:IoT.Driver.MitsubishiRx.MitsubishiCommands.RemotePause`

```csharp
public static ushort RemotePause
```
Stores the RemotePause field.

###### `F:IoT.Driver.MitsubishiRx.MitsubishiCommands.RemoteReset`

```csharp
public static ushort RemoteReset
```
Stores the RemoteReset field.

###### `F:IoT.Driver.MitsubishiRx.MitsubishiCommands.RemoteRun`

```csharp
public static ushort RemoteRun
```
Stores the RemoteRun field.

###### `F:IoT.Driver.MitsubishiRx.MitsubishiCommands.RemoteStop`

```csharp
public static ushort RemoteStop
```
Stores the RemoteStop field.

###### `F:IoT.Driver.MitsubishiRx.MitsubishiCommands.Unlock`

```csharp
public static ushort Unlock
```
Stores the Unlock field.

#### `T:IoT.Driver.MitsubishiRx.MitsubishiConnectionState`

```csharp
public enum IoT.Driver.MitsubishiRx.MitsubishiConnectionState
```
Defines the MitsubishiConnectionState values.

##### Declared public members

###### `F:IoT.Driver.MitsubishiRx.MitsubishiConnectionState.Connected`

```csharp
public static const IoT.Driver.MitsubishiRx.MitsubishiConnectionState Connected
```
Represents the Connected option.

###### `F:IoT.Driver.MitsubishiRx.MitsubishiConnectionState.Connecting`

```csharp
public static const IoT.Driver.MitsubishiRx.MitsubishiConnectionState Connecting
```
Represents the Connecting option.

###### `F:IoT.Driver.MitsubishiRx.MitsubishiConnectionState.Disconnected`

```csharp
public static const IoT.Driver.MitsubishiRx.MitsubishiConnectionState Disconnected
```
Represents the Disconnected option.

###### `F:IoT.Driver.MitsubishiRx.MitsubishiConnectionState.Faulted`

```csharp
public static const IoT.Driver.MitsubishiRx.MitsubishiConnectionState Faulted
```
Represents the Faulted option.

###### `F:IoT.Driver.MitsubishiRx.MitsubishiConnectionState.Reconnecting`

```csharp
public static const IoT.Driver.MitsubishiRx.MitsubishiConnectionState Reconnecting
```
Represents the Reconnecting option.

#### `T:IoT.Driver.MitsubishiRx.MitsubishiDeviceAddress`

```csharp
public class IoT.Driver.MitsubishiRx.MitsubishiDeviceAddress
```
Provides the MitsubishiDeviceAddress record.

##### Declared public members

###### `M:IoT.Driver.MitsubishiRx.MitsubishiDeviceAddress.#ctor(System.String,System.Int32,IoT.Driver.MitsubishiRx.XyAddressNotation,System.String)`

```csharp
public IoT.Driver.MitsubishiRx.MitsubishiDeviceAddress(string Symbol, int Number, IoT.Driver.MitsubishiRx.XyAddressNotation Notation, string Original)
```
Provides the MitsubishiDeviceAddress record.

- Parameter `Symbol`: The Symbol parameter.
- Parameter `Number`: The Number parameter.
- Parameter `Notation`: The Notation parameter.
- Parameter `Original`: The Original parameter.

###### `M:IoT.Driver.MitsubishiRx.MitsubishiDeviceAddress.Deconstruct(System.String@,System.Int32@,IoT.Driver.MitsubishiRx.XyAddressNotation@,System.String@)`

```csharp
public void Deconstruct(out string Symbol, out int Number, out IoT.Driver.MitsubishiRx.XyAddressNotation Notation, out string Original)
```
Deconstructs the value into its component values.

- Parameter `Symbol`: The `Symbol` value.
- Parameter `Number`: The `Number` value.
- Parameter `Notation`: The `Notation` value.
- Parameter `Original`: The `Original` value.

###### `M:IoT.Driver.MitsubishiRx.MitsubishiDeviceAddress.Equals(IoT.Driver.MitsubishiRx.MitsubishiDeviceAddress)`

```csharp
public bool Equals(IoT.Driver.MitsubishiRx.MitsubishiDeviceAddress other)
```
Determines whether the supplied value is equal to the current value.

- Parameter `other`: The `other` value.
- Returns: A `bool` result.

###### `M:IoT.Driver.MitsubishiRx.MitsubishiDeviceAddress.Equals(System.Object)`

```csharp
public bool Equals(object obj)
```
Determines whether the supplied value is equal to the current value.

- Parameter `obj`: The `obj` value.
- Returns: A `bool` result.

###### `M:IoT.Driver.MitsubishiRx.MitsubishiDeviceAddress.GetHashCode`

```csharp
public int GetHashCode()
```
Returns the hash code for the current value.

- Returns: A `int` result.

###### `M:IoT.Driver.MitsubishiRx.MitsubishiDeviceAddress.Parse(System.String,IoT.Driver.MitsubishiRx.XyAddressNotation)`

```csharp
public static IoT.Driver.MitsubishiRx.MitsubishiDeviceAddress Parse(string value, IoT.Driver.MitsubishiRx.XyAddressNotation addressNotation)
```
Executes the Parse operation.

- Parameter `value`: The value parameter.
- Parameter `addressNotation`: The addressNotation parameter.
- Returns: The Parse operation result.

###### `M:IoT.Driver.MitsubishiRx.MitsubishiDeviceAddress.ToString`

```csharp
public string ToString()
```
Returns a string representation of the current value.

- Returns: A `string` result.

###### `M:IoT.Driver.MitsubishiRx.MitsubishiDeviceAddress.op_Equality(IoT.Driver.MitsubishiRx.MitsubishiDeviceAddress,IoT.Driver.MitsubishiRx.MitsubishiDeviceAddress)`

```csharp
public static bool op_Equality(IoT.Driver.MitsubishiRx.MitsubishiDeviceAddress left, IoT.Driver.MitsubishiRx.MitsubishiDeviceAddress right)
```
Determines whether the two supplied values are equal.

- Parameter `left`: The `left` value.
- Parameter `right`: The `right` value.
- Returns: A `bool` result.

###### `M:IoT.Driver.MitsubishiRx.MitsubishiDeviceAddress.op_Inequality(IoT.Driver.MitsubishiRx.MitsubishiDeviceAddress,IoT.Driver.MitsubishiRx.MitsubishiDeviceAddress)`

```csharp
public static bool op_Inequality(IoT.Driver.MitsubishiRx.MitsubishiDeviceAddress left, IoT.Driver.MitsubishiRx.MitsubishiDeviceAddress right)
```
Determines whether the two supplied values are not equal.

- Parameter `left`: The `left` value.
- Parameter `right`: The `right` value.
- Returns: A `bool` result.

###### `P:IoT.Driver.MitsubishiRx.MitsubishiDeviceAddress.Descriptor`

```csharp
public IoT.Driver.MitsubishiRx.MitsubishiDeviceMetadata Descriptor { get; }
```
Gets or sets the Descriptor property.

- Value: The `Descriptor` value.

###### `P:IoT.Driver.MitsubishiRx.MitsubishiDeviceAddress.Metadata`

```csharp
public System.Collections.Generic.IReadOnlyDictionary<string, IoT.Driver.MitsubishiRx.MitsubishiDeviceMetadata> Metadata { get; }
```
Gets or sets the Metadata property.

- Value: The `Metadata` value.

###### `P:IoT.Driver.MitsubishiRx.MitsubishiDeviceAddress.Notation`

```csharp
public IoT.Driver.MitsubishiRx.XyAddressNotation Notation { get; set; }
```
The Notation parameter.

- Value: The `Notation` value.

###### `P:IoT.Driver.MitsubishiRx.MitsubishiDeviceAddress.Number`

```csharp
public int Number { get; set; }
```
The Number parameter.

- Value: The `Number` value.

###### `P:IoT.Driver.MitsubishiRx.MitsubishiDeviceAddress.Original`

```csharp
public string Original { get; set; }
```
The Original parameter.

- Value: The `Original` value.

###### `P:IoT.Driver.MitsubishiRx.MitsubishiDeviceAddress.Symbol`

```csharp
public string Symbol { get; set; }
```
The Symbol parameter.

- Value: The `Symbol` value.

#### `T:IoT.Driver.MitsubishiRx.MitsubishiDeviceMetadata`

```csharp
public class IoT.Driver.MitsubishiRx.MitsubishiDeviceMetadata
```
Provides the MitsubishiDeviceMetadata record.

##### Declared public members

###### `M:IoT.Driver.MitsubishiRx.MitsubishiDeviceMetadata.#ctor(System.String,System.UInt16,System.UInt16,IoT.Driver.MitsubishiRx.DeviceValueKind,IoT.Driver.MitsubishiRx.DeviceNumberFormat)`

```csharp
public IoT.Driver.MitsubishiRx.MitsubishiDeviceMetadata(string Symbol, ushort BinaryCode, ushort AsciiCode, IoT.Driver.MitsubishiRx.DeviceValueKind Kind, IoT.Driver.MitsubishiRx.DeviceNumberFormat NumberFormat)
```
Provides the MitsubishiDeviceMetadata record.

- Parameter `Symbol`: The Symbol parameter.
- Parameter `BinaryCode`: The BinaryCode parameter.
- Parameter `AsciiCode`: The AsciiCode parameter.
- Parameter `Kind`: The Kind parameter.
- Parameter `NumberFormat`: The NumberFormat parameter.

###### `M:IoT.Driver.MitsubishiRx.MitsubishiDeviceMetadata.Deconstruct(System.String@,System.UInt16@,System.UInt16@,IoT.Driver.MitsubishiRx.DeviceValueKind@,IoT.Driver.MitsubishiRx.DeviceNumberFormat@)`

```csharp
public void Deconstruct(out string Symbol, out ushort BinaryCode, out ushort AsciiCode, out IoT.Driver.MitsubishiRx.DeviceValueKind Kind, out IoT.Driver.MitsubishiRx.DeviceNumberFormat NumberFormat)
```
Deconstructs the value into its component values.

- Parameter `Symbol`: The `Symbol` value.
- Parameter `BinaryCode`: The `BinaryCode` value.
- Parameter `AsciiCode`: The `AsciiCode` value.
- Parameter `Kind`: The `Kind` value.
- Parameter `NumberFormat`: The `NumberFormat` value.

###### `M:IoT.Driver.MitsubishiRx.MitsubishiDeviceMetadata.Equals(IoT.Driver.MitsubishiRx.MitsubishiDeviceMetadata)`

```csharp
public bool Equals(IoT.Driver.MitsubishiRx.MitsubishiDeviceMetadata other)
```
Determines whether the supplied value is equal to the current value.

- Parameter `other`: The `other` value.
- Returns: A `bool` result.

###### `M:IoT.Driver.MitsubishiRx.MitsubishiDeviceMetadata.Equals(System.Object)`

```csharp
public bool Equals(object obj)
```
Determines whether the supplied value is equal to the current value.

- Parameter `obj`: The `obj` value.
- Returns: A `bool` result.

###### `M:IoT.Driver.MitsubishiRx.MitsubishiDeviceMetadata.GetHashCode`

```csharp
public int GetHashCode()
```
Returns the hash code for the current value.

- Returns: A `int` result.

###### `M:IoT.Driver.MitsubishiRx.MitsubishiDeviceMetadata.GetRadix(IoT.Driver.MitsubishiRx.XyAddressNotation)`

```csharp
public int GetRadix(IoT.Driver.MitsubishiRx.XyAddressNotation addressNotation)
```
Executes the GetRadix operation.

- Parameter `addressNotation`: The addressNotation parameter.
- Returns: The GetRadix operation result.

###### `M:IoT.Driver.MitsubishiRx.MitsubishiDeviceMetadata.ToString`

```csharp
public string ToString()
```
Returns a string representation of the current value.

- Returns: A `string` result.

###### `M:IoT.Driver.MitsubishiRx.MitsubishiDeviceMetadata.op_Equality(IoT.Driver.MitsubishiRx.MitsubishiDeviceMetadata,IoT.Driver.MitsubishiRx.MitsubishiDeviceMetadata)`

```csharp
public static bool op_Equality(IoT.Driver.MitsubishiRx.MitsubishiDeviceMetadata left, IoT.Driver.MitsubishiRx.MitsubishiDeviceMetadata right)
```
Determines whether the two supplied values are equal.

- Parameter `left`: The `left` value.
- Parameter `right`: The `right` value.
- Returns: A `bool` result.

###### `M:IoT.Driver.MitsubishiRx.MitsubishiDeviceMetadata.op_Inequality(IoT.Driver.MitsubishiRx.MitsubishiDeviceMetadata,IoT.Driver.MitsubishiRx.MitsubishiDeviceMetadata)`

```csharp
public static bool op_Inequality(IoT.Driver.MitsubishiRx.MitsubishiDeviceMetadata left, IoT.Driver.MitsubishiRx.MitsubishiDeviceMetadata right)
```
Determines whether the two supplied values are not equal.

- Parameter `left`: The `left` value.
- Parameter `right`: The `right` value.
- Returns: A `bool` result.

###### `P:IoT.Driver.MitsubishiRx.MitsubishiDeviceMetadata.AsciiCode`

```csharp
public ushort AsciiCode { get; set; }
```
The AsciiCode parameter.

- Value: The `AsciiCode` value.

###### `P:IoT.Driver.MitsubishiRx.MitsubishiDeviceMetadata.BinaryCode`

```csharp
public ushort BinaryCode { get; set; }
```
The BinaryCode parameter.

- Value: The `BinaryCode` value.

###### `P:IoT.Driver.MitsubishiRx.MitsubishiDeviceMetadata.Kind`

```csharp
public IoT.Driver.MitsubishiRx.DeviceValueKind Kind { get; set; }
```
The Kind parameter.

- Value: The `Kind` value.

###### `P:IoT.Driver.MitsubishiRx.MitsubishiDeviceMetadata.NumberFormat`

```csharp
public IoT.Driver.MitsubishiRx.DeviceNumberFormat NumberFormat { get; set; }
```
The NumberFormat parameter.

- Value: The `NumberFormat` value.

###### `P:IoT.Driver.MitsubishiRx.MitsubishiDeviceMetadata.Symbol`

```csharp
public string Symbol { get; set; }
```
The Symbol parameter.

- Value: The `Symbol` value.

#### `T:IoT.Driver.MitsubishiRx.MitsubishiDeviceValue`

```csharp
public class IoT.Driver.MitsubishiRx.MitsubishiDeviceValue
```
Provides the MitsubishiDeviceValue record.

##### Declared public members

###### `M:IoT.Driver.MitsubishiRx.MitsubishiDeviceValue.#ctor(IoT.Driver.MitsubishiRx.MitsubishiDeviceAddress,System.UInt16)`

```csharp
public IoT.Driver.MitsubishiRx.MitsubishiDeviceValue(IoT.Driver.MitsubishiRx.MitsubishiDeviceAddress Address, ushort Value)
```
Provides the MitsubishiDeviceValue record.

- Parameter `Address`: The Address parameter.
- Parameter `Value`: The Value parameter.

###### `M:IoT.Driver.MitsubishiRx.MitsubishiDeviceValue.Deconstruct(IoT.Driver.MitsubishiRx.MitsubishiDeviceAddress@,System.UInt16@)`

```csharp
public void Deconstruct(out IoT.Driver.MitsubishiRx.MitsubishiDeviceAddress Address, out ushort Value)
```
Deconstructs the value into its component values.

- Parameter `Address`: The `Address` value.
- Parameter `Value`: The `Value` value.

###### `M:IoT.Driver.MitsubishiRx.MitsubishiDeviceValue.Equals(IoT.Driver.MitsubishiRx.MitsubishiDeviceValue)`

```csharp
public bool Equals(IoT.Driver.MitsubishiRx.MitsubishiDeviceValue other)
```
Determines whether the supplied value is equal to the current value.

- Parameter `other`: The `other` value.
- Returns: A `bool` result.

###### `M:IoT.Driver.MitsubishiRx.MitsubishiDeviceValue.Equals(System.Object)`

```csharp
public bool Equals(object obj)
```
Determines whether the supplied value is equal to the current value.

- Parameter `obj`: The `obj` value.
- Returns: A `bool` result.

###### `M:IoT.Driver.MitsubishiRx.MitsubishiDeviceValue.GetHashCode`

```csharp
public int GetHashCode()
```
Returns the hash code for the current value.

- Returns: A `int` result.

###### `M:IoT.Driver.MitsubishiRx.MitsubishiDeviceValue.ToString`

```csharp
public string ToString()
```
Returns a string representation of the current value.

- Returns: A `string` result.

###### `M:IoT.Driver.MitsubishiRx.MitsubishiDeviceValue.op_Equality(IoT.Driver.MitsubishiRx.MitsubishiDeviceValue,IoT.Driver.MitsubishiRx.MitsubishiDeviceValue)`

```csharp
public static bool op_Equality(IoT.Driver.MitsubishiRx.MitsubishiDeviceValue left, IoT.Driver.MitsubishiRx.MitsubishiDeviceValue right)
```
Determines whether the two supplied values are equal.

- Parameter `left`: The `left` value.
- Parameter `right`: The `right` value.
- Returns: A `bool` result.

###### `M:IoT.Driver.MitsubishiRx.MitsubishiDeviceValue.op_Inequality(IoT.Driver.MitsubishiRx.MitsubishiDeviceValue,IoT.Driver.MitsubishiRx.MitsubishiDeviceValue)`

```csharp
public static bool op_Inequality(IoT.Driver.MitsubishiRx.MitsubishiDeviceValue left, IoT.Driver.MitsubishiRx.MitsubishiDeviceValue right)
```
Determines whether the two supplied values are not equal.

- Parameter `left`: The `left` value.
- Parameter `right`: The `right` value.
- Returns: A `bool` result.

###### `P:IoT.Driver.MitsubishiRx.MitsubishiDeviceValue.Address`

```csharp
public IoT.Driver.MitsubishiRx.MitsubishiDeviceAddress Address { get; set; }
```
The Address parameter.

- Value: The `Address` value.

###### `P:IoT.Driver.MitsubishiRx.MitsubishiDeviceValue.Value`

```csharp
public ushort Value { get; set; }
```
The Value parameter.

- Value: The `Value` value.

#### `T:IoT.Driver.MitsubishiRx.MitsubishiFrameType`

```csharp
public enum IoT.Driver.MitsubishiRx.MitsubishiFrameType
```
Defines the MitsubishiFrameType values.

##### Declared public members

###### `F:IoT.Driver.MitsubishiRx.MitsubishiFrameType.FourC`

```csharp
public static const IoT.Driver.MitsubishiRx.MitsubishiFrameType FourC
```
Represents the FourC option.

###### `F:IoT.Driver.MitsubishiRx.MitsubishiFrameType.FourE`

```csharp
public static const IoT.Driver.MitsubishiRx.MitsubishiFrameType FourE
```
Represents the FourE option.

###### `F:IoT.Driver.MitsubishiRx.MitsubishiFrameType.OneC`

```csharp
public static const IoT.Driver.MitsubishiRx.MitsubishiFrameType OneC
```
Represents the OneC option.

###### `F:IoT.Driver.MitsubishiRx.MitsubishiFrameType.OneE`

```csharp
public static const IoT.Driver.MitsubishiRx.MitsubishiFrameType OneE
```
Represents the OneE option.

###### `F:IoT.Driver.MitsubishiRx.MitsubishiFrameType.ThreeC`

```csharp
public static const IoT.Driver.MitsubishiRx.MitsubishiFrameType ThreeC
```
Represents the ThreeC option.

###### `F:IoT.Driver.MitsubishiRx.MitsubishiFrameType.ThreeE`

```csharp
public static const IoT.Driver.MitsubishiRx.MitsubishiFrameType ThreeE
```
Represents the ThreeE option.

#### `T:IoT.Driver.MitsubishiRx.MitsubishiLogicalTagBulkDirectionMetrics`

```csharp
public class IoT.Driver.MitsubishiRx.MitsubishiLogicalTagBulkDirectionMetrics
```
Provides an immutable deterministic snapshot for one bulk transfer direction.

##### Declared public members

###### `M:IoT.Driver.MitsubishiRx.MitsubishiLogicalTagBulkDirectionMetrics.#ctor(System.Int64,System.Int64,System.Int64,System.Int64)`

```csharp
public IoT.Driver.MitsubishiRx.MitsubishiLogicalTagBulkDirectionMetrics(long planCount, long itemCount, long rangeCount, long protocolCallCount)
```
Initializes a new instance of the `T:IoT.Driver.MitsubishiRx.MitsubishiLogicalTagBulkDirectionMetrics` class.

- Parameter `planCount`: The number of eligible plans created.
- Parameter `itemCount`: The number of eligible word operations planned.
- Parameter `rangeCount`: The number of contiguous ranges produced by the planner.
- Parameter `protocolCallCount`: The number of grouped protocol calls issued.

###### `P:IoT.Driver.MitsubishiRx.MitsubishiLogicalTagBulkDirectionMetrics.ItemCount`

```csharp
public long ItemCount { get; }
```
Gets the number of eligible word operations planned.

- Value: The `ItemCount` value.

###### `P:IoT.Driver.MitsubishiRx.MitsubishiLogicalTagBulkDirectionMetrics.PlanCount`

```csharp
public long PlanCount { get; }
```
Gets the number of eligible plans created.

- Value: The `PlanCount` value.

###### `P:IoT.Driver.MitsubishiRx.MitsubishiLogicalTagBulkDirectionMetrics.ProtocolCallCount`

```csharp
public long ProtocolCallCount { get; }
```
Gets the number of grouped protocol calls issued.

- Value: The `ProtocolCallCount` value.

###### `P:IoT.Driver.MitsubishiRx.MitsubishiLogicalTagBulkDirectionMetrics.RangeCount`

```csharp
public long RangeCount { get; }
```
Gets the number of contiguous ranges produced by the planner.

- Value: The `RangeCount` value.

#### `T:IoT.Driver.MitsubishiRx.MitsubishiLogicalTagBulkOperationMetrics`

```csharp
public class IoT.Driver.MitsubishiRx.MitsubishiLogicalTagBulkOperationMetrics
```
Provides immutable deterministic snapshots of logical-tag bulk planning and protocol dispatch activity.

##### Declared public members

###### `M:IoT.Driver.MitsubishiRx.MitsubishiLogicalTagBulkOperationMetrics.#ctor(IoT.Driver.MitsubishiRx.MitsubishiLogicalTagBulkDirectionMetrics,IoT.Driver.MitsubishiRx.MitsubishiLogicalTagBulkDirectionMetrics)`

```csharp
public IoT.Driver.MitsubishiRx.MitsubishiLogicalTagBulkOperationMetrics(IoT.Driver.MitsubishiRx.MitsubishiLogicalTagBulkDirectionMetrics read, IoT.Driver.MitsubishiRx.MitsubishiLogicalTagBulkDirectionMetrics write)
```
Initializes a new instance of the `T:IoT.Driver.MitsubishiRx.MitsubishiLogicalTagBulkOperationMetrics` class.

- Parameter `read`: The read planning and dispatch snapshot.
- Parameter `write`: The write planning and dispatch snapshot.

###### `P:IoT.Driver.MitsubishiRx.MitsubishiLogicalTagBulkOperationMetrics.Read`

```csharp
public IoT.Driver.MitsubishiRx.MitsubishiLogicalTagBulkDirectionMetrics Read { get; }
```
Gets the read planning and dispatch snapshot.

- Value: The `Read` value.

###### `P:IoT.Driver.MitsubishiRx.MitsubishiLogicalTagBulkOperationMetrics.Write`

```csharp
public IoT.Driver.MitsubishiRx.MitsubishiLogicalTagBulkDirectionMetrics Write { get; }
```
Gets the write planning and dispatch snapshot.

- Value: The `Write` value.

#### `T:IoT.Driver.MitsubishiRx.MitsubishiLogicalTagClient`

```csharp
public class IoT.Driver.MitsubishiRx.MitsubishiLogicalTagClient
```
Composes common logical-tag operations with Mitsubishi protocol transports.

##### Declared public members

###### `M:IoT.Driver.MitsubishiRx.MitsubishiLogicalTagClient.#ctor(IoT.Driver.MitsubishiRx.MitsubishiRx,IoT.Driver.Core.ILogicalTagCatalog,System.Nullable`1{System.TimeSpan},IoT.Driver.Core.LogicalTagSqliteStore)`

```csharp
public IoT.Driver.MitsubishiRx.MitsubishiLogicalTagClient(IoT.Driver.MitsubishiRx.MitsubishiRx owner, IoT.Driver.Core.ILogicalTagCatalog catalog, System.Nullable<System.TimeSpan> defaultScanInterval, IoT.Driver.Core.LogicalTagSqliteStore store)
```
Initializes a new instance of `IoT.Driver.MitsubishiRx.MitsubishiLogicalTagClient`.

- Parameter `owner`: The `owner` value.
- Parameter `catalog`: The `catalog` value.
- Parameter `defaultScanInterval`: The `defaultScanInterval` value.
- Parameter `store`: The `store` value.

###### `M:IoT.Driver.MitsubishiRx.MitsubishiLogicalTagClient.#ctor(IoT.Driver.MitsubishiRx.MitsubishiRx,IoT.Driver.Core.ILogicalTagCatalog,System.Nullable`1{System.TimeSpan},IoT.Driver.Core.LogicalTagSqliteStore,System.TimeProvider)`

```csharp
public IoT.Driver.MitsubishiRx.MitsubishiLogicalTagClient(IoT.Driver.MitsubishiRx.MitsubishiRx owner, IoT.Driver.Core.ILogicalTagCatalog catalog, System.Nullable<System.TimeSpan> defaultScanInterval, IoT.Driver.Core.LogicalTagSqliteStore store, System.TimeProvider timeProvider)
```
Initializes a new instance of `IoT.Driver.MitsubishiRx.MitsubishiLogicalTagClient`.

- Parameter `owner`: The `owner` value.
- Parameter `catalog`: The `catalog` value.
- Parameter `defaultScanInterval`: The `defaultScanInterval` value.
- Parameter `store`: The `store` value.
- Parameter `timeProvider`: The `timeProvider` value.

###### `M:IoT.Driver.MitsubishiRx.MitsubishiLogicalTagClient.CreateTag(IoT.Driver.MitsubishiRx.MitsubishiLogicalTagRegistration)`

```csharp
public IoT.Driver.Core.LogicalTag CreateTag(IoT.Driver.MitsubishiRx.MitsubishiLogicalTagRegistration registration)
```
Creates and registers a logical Mitsubishi tag.

- Parameter `registration`: The logical tag registration.
- Returns: The registered immutable tag.

###### `M:IoT.Driver.MitsubishiRx.MitsubishiLogicalTagClient.DeleteGroupAsync(System.String,System.Threading.CancellationToken)`

```csharp
public System.Threading.Tasks.Task<bool> DeleteGroupAsync(string name, System.Threading.CancellationToken cancellationToken)
```
Inherits XML documentation from its implemented or overridden member.

- Parameter `name`: The `name` value.
- Parameter `cancellationToken`: The `cancellationToken` value.
- Returns: A `System.Threading.Tasks.Task<bool>` result.

###### `M:IoT.Driver.MitsubishiRx.MitsubishiLogicalTagClient.DeleteTagAsync(System.String,System.Threading.CancellationToken)`

```csharp
public System.Threading.Tasks.Task<bool> DeleteTagAsync(string name, System.Threading.CancellationToken cancellationToken)
```
Deletes a persisted tag from the configured SQLite store and catalog.

- Parameter `name`: The logical name.
- Parameter `cancellationToken`: The cancellation token.
- Returns: when the persisted tag existed.

###### `M:IoT.Driver.MitsubishiRx.MitsubishiLogicalTagClient.Dispose`

```csharp
public void Dispose()
```
Inherits XML documentation from its implemented or overridden member.

###### `M:IoT.Driver.MitsubishiRx.MitsubishiLogicalTagClient.EditTagAsync(IoT.Driver.Core.LogicalTag,System.Threading.CancellationToken)`

```csharp
public System.Threading.Tasks.Task<bool> EditTagAsync(IoT.Driver.Core.LogicalTag tag, System.Threading.CancellationToken cancellationToken)
```
Edits a persisted tag in the configured SQLite store.

- Parameter `tag`: The replacement tag.
- Parameter `cancellationToken`: The cancellation token.
- Returns: when the persisted tag existed.

###### `M:IoT.Driver.MitsubishiRx.MitsubishiLogicalTagClient.ExportCsvAsync(System.IO.TextWriter,System.Char,System.Threading.CancellationToken)`

```csharp
public System.Threading.Tasks.Task ExportCsvAsync(System.IO.TextWriter writer, char delimiter, System.Threading.CancellationToken cancellationToken)
```
Exports the current catalog using the shared RFC 4180 CSV format.

- Parameter `writer`: The CSV writer.
- Parameter `delimiter`: The field delimiter.
- Parameter `cancellationToken`: The cancellation token.
- Returns: A task that completes when the catalog is written.

###### `M:IoT.Driver.MitsubishiRx.MitsubishiLogicalTagClient.GetGroupAsync(System.String,System.Threading.CancellationToken)`

```csharp
public System.Threading.Tasks.Task<IoT.Driver.Core.LogicalTagGroup> GetGroupAsync(string name, System.Threading.CancellationToken cancellationToken)
```
Inherits XML documentation from its implemented or overridden member.

- Parameter `name`: The `name` value.
- Parameter `cancellationToken`: The `cancellationToken` value.
- Returns: A `System.Threading.Tasks.Task<IoT.Driver.Core.LogicalTagGroup>` result.

###### `M:IoT.Driver.MitsubishiRx.MitsubishiLogicalTagClient.GetTagAsync(System.String,System.Threading.CancellationToken)`

```csharp
public System.Threading.Tasks.Task<IoT.Driver.Core.LogicalTag> GetTagAsync(string name, System.Threading.CancellationToken cancellationToken)
```
Gets a persisted tag from the configured SQLite store.

- Parameter `name`: The logical name.
- Parameter `cancellationToken`: The cancellation token.
- Returns: The persisted tag, if found.

###### `M:IoT.Driver.MitsubishiRx.MitsubishiLogicalTagClient.ImportCsvAsync(System.IO.TextReader,System.Char,System.Threading.CancellationToken)`

```csharp
public System.Threading.Tasks.Task<System.Collections.Generic.IReadOnlyList<IoT.Driver.Core.LogicalTag>> ImportCsvAsync(System.IO.TextReader reader, char delimiter, System.Threading.CancellationToken cancellationToken)
```
Imports the shared RFC 4180 CSV format and registers every tag.

- Parameter `reader`: The CSV reader.
- Parameter `delimiter`: The field delimiter.
- Parameter `cancellationToken`: The cancellation token.
- Returns: The imported tags.

###### `M:IoT.Driver.MitsubishiRx.MitsubishiLogicalTagClient.InitializeStoreAsync(System.Threading.CancellationToken)`

```csharp
public System.Threading.Tasks.Task InitializeStoreAsync(System.Threading.CancellationToken cancellationToken)
```
Initializes the configured common SQLite store.

- Parameter `cancellationToken`: The cancellation token.
- Returns: A task that completes when the schema exists.

###### `M:IoT.Driver.MitsubishiRx.MitsubishiLogicalTagClient.ListGroupsAsync(System.Threading.CancellationToken)`

```csharp
public System.Threading.Tasks.Task<System.Collections.Generic.IReadOnlyList<IoT.Driver.Core.LogicalTagGroup>> ListGroupsAsync(System.Threading.CancellationToken cancellationToken)
```
Inherits XML documentation from its implemented or overridden member.

- Parameter `cancellationToken`: The `cancellationToken` value.
- Returns: A `System.Threading.Tasks.Task<System.Collections.Generic.IReadOnlyList<IoT.Driver.Core.LogicalTagGroup>>` result.

###### `M:IoT.Driver.MitsubishiRx.MitsubishiLogicalTagClient.ListTagsAsync(System.Threading.CancellationToken)`

```csharp
public System.Threading.Tasks.Task<System.Collections.Generic.IReadOnlyList<IoT.Driver.Core.LogicalTag>> ListTagsAsync(System.Threading.CancellationToken cancellationToken)
```
Inherits XML documentation from its implemented or overridden member.

- Parameter `cancellationToken`: The `cancellationToken` value.
- Returns: A `System.Threading.Tasks.Task<System.Collections.Generic.IReadOnlyList<IoT.Driver.Core.LogicalTag>>` result.

###### `M:IoT.Driver.MitsubishiRx.MitsubishiLogicalTagClient.LoadFromSqliteAsync(IoT.Driver.Core.LogicalTagSqliteStore,System.Threading.CancellationToken)`

```csharp
public System.Threading.Tasks.Task<System.Collections.Generic.IReadOnlyList<IoT.Driver.Core.LogicalTag>> LoadFromSqliteAsync(IoT.Driver.Core.LogicalTagSqliteStore store, System.Threading.CancellationToken cancellationToken)
```
Loads and registers all tags from the common SQLite store.

- Parameter `store`: The initialized or uninitialized store.
- Parameter `cancellationToken`: The cancellation token.
- Returns: The loaded tags.

###### `M:IoT.Driver.MitsubishiRx.MitsubishiLogicalTagClient.LoadTagsAsync(System.Threading.CancellationToken)`

```csharp
public System.Threading.Tasks.Task<System.Collections.Generic.IReadOnlyList<IoT.Driver.Core.LogicalTag>> LoadTagsAsync(System.Threading.CancellationToken cancellationToken)
```
Loads the configured common SQLite store into this client.

- Parameter `cancellationToken`: The cancellation token.
- Returns: The loaded tags.

###### `M:IoT.Driver.MitsubishiRx.MitsubishiLogicalTagClient.Observe(System.String)`

```csharp
public System.IObservable<IoT.Driver.Core.LogicalTagValue> Observe(string tagName)
```
Inherits XML documentation from its implemented or overridden member.

- Parameter `tagName`: The `tagName` value.
- Returns: A `System.IObservable<IoT.Driver.Core.LogicalTagValue>` result.

###### `M:IoT.Driver.MitsubishiRx.MitsubishiLogicalTagClient.ObserveAsync(System.String,System.Threading.CancellationToken)`

```csharp
public System.Collections.Generic.IAsyncEnumerable<IoT.Driver.Core.LogicalTagValue> ObserveAsync(string tagName, System.Threading.CancellationToken cancellationToken)
```
Inherits XML documentation from its implemented or overridden member.

- Parameter `tagName`: The `tagName` value.
- Parameter `cancellationToken`: The `cancellationToken` value.
- Returns: A `System.Collections.Generic.IAsyncEnumerable<IoT.Driver.Core.LogicalTagValue>` result.

###### `M:IoT.Driver.MitsubishiRx.MitsubishiLogicalTagClient.ObserveAsync``1(IoT.Driver.Core.LogicalTagKey`1{``0},System.Threading.CancellationToken)`

```csharp
public System.Collections.Generic.IAsyncEnumerable<T> ObserveAsync<T>(IoT.Driver.Core.LogicalTagKey<T> tag, System.Threading.CancellationToken cancellationToken)
```
Executes the `ObserveAsync` operation.

- Parameter `tag`: The `tag` value.
- Parameter `cancellationToken`: The `cancellationToken` value.
- Returns: A `System.Collections.Generic.IAsyncEnumerable<T>` result.

###### `M:IoT.Driver.MitsubishiRx.MitsubishiLogicalTagClient.ObserveMany(System.Collections.Generic.IReadOnlyCollection`1{System.String})`

```csharp
public System.IObservable<IoT.Driver.Core.LogicalTagValue> ObserveMany(System.Collections.Generic.IReadOnlyCollection<string> tagNames)
```
Executes the `ObserveMany` operation.

- Parameter `tagNames`: The `tagNames` value.
- Returns: A `System.IObservable<IoT.Driver.Core.LogicalTagValue>` result.

###### `M:IoT.Driver.MitsubishiRx.MitsubishiLogicalTagClient.ObserveManyAsync(System.Collections.Generic.IReadOnlyCollection`1{System.String},System.Threading.CancellationToken)`

```csharp
public System.Collections.Generic.IAsyncEnumerable<IoT.Driver.Core.LogicalTagValue> ObserveManyAsync(System.Collections.Generic.IReadOnlyCollection<string> tagNames, System.Threading.CancellationToken cancellationToken)
```
Executes the `ObserveManyAsync` operation.

- Parameter `tagNames`: The `tagNames` value.
- Parameter `cancellationToken`: The `cancellationToken` value.
- Returns: A `System.Collections.Generic.IAsyncEnumerable<IoT.Driver.Core.LogicalTagValue>` result.

###### `M:IoT.Driver.MitsubishiRx.MitsubishiLogicalTagClient.Observe``1(IoT.Driver.Core.LogicalTagKey`1{``0})`

```csharp
public System.IObservable<T> Observe<T>(IoT.Driver.Core.LogicalTagKey<T> tag)
```
Executes the `Observe` operation.

- Parameter `tag`: The `tag` value.
- Returns: A `System.IObservable<T>` result.

###### `M:IoT.Driver.MitsubishiRx.MitsubishiLogicalTagClient.ReadAsync(System.String,System.Threading.CancellationToken)`

```csharp
public System.Threading.Tasks.Task<IoT.Driver.Core.TagOperationResult<IoT.Driver.Core.LogicalTagValue>> ReadAsync(string tagName, System.Threading.CancellationToken cancellationToken)
```
Inherits XML documentation from its implemented or overridden member.

- Parameter `tagName`: The `tagName` value.
- Parameter `cancellationToken`: The `cancellationToken` value.
- Returns: A `System.Threading.Tasks.Task<IoT.Driver.Core.TagOperationResult<IoT.Driver.Core.LogicalTagValue>>` result.

###### `M:IoT.Driver.MitsubishiRx.MitsubishiLogicalTagClient.ReadAsync``1(IoT.Driver.Core.LogicalTagKey`1{``0},System.Threading.CancellationToken)`

```csharp
public System.Threading.Tasks.Task<IoT.Driver.Core.TagOperationResult<T>> ReadAsync<T>(IoT.Driver.Core.LogicalTagKey<T> tag, System.Threading.CancellationToken cancellationToken)
```
Executes the `ReadAsync` operation.

- Parameter `tag`: The `tag` value.
- Parameter `cancellationToken`: The `cancellationToken` value.
- Returns: A `System.Threading.Tasks.Task<IoT.Driver.Core.TagOperationResult<T>>` result.

###### `M:IoT.Driver.MitsubishiRx.MitsubishiLogicalTagClient.ReadManyAsync(System.Collections.Generic.IReadOnlyCollection`1{System.String},System.Threading.CancellationToken)`

```csharp
public System.Threading.Tasks.Task<System.Collections.Generic.IReadOnlyList<IoT.Driver.Core.TagOperationResult<IoT.Driver.Core.LogicalTagValue>>> ReadManyAsync(System.Collections.Generic.IReadOnlyCollection<string> tagNames, System.Threading.CancellationToken cancellationToken)
```
Executes the `ReadManyAsync` operation.

- Parameter `tagNames`: The `tagNames` value.
- Parameter `cancellationToken`: The `cancellationToken` value.
- Returns: A `System.Threading.Tasks.Task<System.Collections.Generic.IReadOnlyList<IoT.Driver.Core.TagOperationResult<IoT.Driver.Core.LogicalTagValue>>>` result.

###### `M:IoT.Driver.MitsubishiRx.MitsubishiLogicalTagClient.Register(IoT.Driver.Core.LogicalTag)`

```csharp
public void Register(IoT.Driver.Core.LogicalTag tag)
```
Compatibility alias for `M:IoT.Driver.MitsubishiRx.MitsubishiLogicalTagClient.RegisterTag(IoT.Driver.Core.LogicalTag)` .

- Parameter `tag`: The tag to register.

###### `M:IoT.Driver.MitsubishiRx.MitsubishiLogicalTagClient.RegisterRange(System.Collections.Generic.IEnumerable`1{IoT.Driver.Core.LogicalTag})`

```csharp
public void RegisterRange(System.Collections.Generic.IEnumerable<IoT.Driver.Core.LogicalTag> tags)
```
Executes the `RegisterRange` operation.

- Parameter `tags`: The `tags` value.

###### `M:IoT.Driver.MitsubishiRx.MitsubishiLogicalTagClient.RegisterTag(IoT.Driver.Core.LogicalTag)`

```csharp
public void RegisterTag(IoT.Driver.Core.LogicalTag tag)
```
Adds or replaces a logical tag and makes it available to Mitsubishi typed APIs.

- Parameter `tag`: The tag to register.

###### `M:IoT.Driver.MitsubishiRx.MitsubishiLogicalTagClient.RemoveTag(System.String)`

```csharp
public bool RemoveTag(string name)
```
Removes a tag from the common catalog.

- Parameter `name`: The logical name.
- Returns: when the tag existed.

###### `M:IoT.Driver.MitsubishiRx.MitsubishiLogicalTagClient.UpsertGroupAsync(IoT.Driver.Core.LogicalTagGroup,System.Threading.CancellationToken)`

```csharp
public System.Threading.Tasks.Task UpsertGroupAsync(IoT.Driver.Core.LogicalTagGroup group, System.Threading.CancellationToken cancellationToken)
```
Inherits XML documentation from its implemented or overridden member.

- Parameter `group`: The `group` value.
- Parameter `cancellationToken`: The `cancellationToken` value.
- Returns: A `System.Threading.Tasks.Task` result.

###### `M:IoT.Driver.MitsubishiRx.MitsubishiLogicalTagClient.UpsertTagAsync(IoT.Driver.Core.LogicalTag,System.Threading.CancellationToken)`

```csharp
public System.Threading.Tasks.Task UpsertTagAsync(IoT.Driver.Core.LogicalTag tag, System.Threading.CancellationToken cancellationToken)
```
Inserts or replaces a persisted tag in the configured SQLite store.

- Parameter `tag`: The tag to persist.
- Parameter `cancellationToken`: The cancellation token.
- Returns: A task that completes when the tag is persisted.

###### `M:IoT.Driver.MitsubishiRx.MitsubishiLogicalTagClient.WriteAsync(IoT.Driver.Core.LogicalTagValue,System.Threading.CancellationToken)`

```csharp
public System.Threading.Tasks.Task<IoT.Driver.Core.TagOperationResult<IoT.Driver.Core.LogicalTagValue>> WriteAsync(IoT.Driver.Core.LogicalTagValue value, System.Threading.CancellationToken cancellationToken)
```
Inherits XML documentation from its implemented or overridden member.

- Parameter `value`: The `value` value.
- Parameter `cancellationToken`: The `cancellationToken` value.
- Returns: A `System.Threading.Tasks.Task<IoT.Driver.Core.TagOperationResult<IoT.Driver.Core.LogicalTagValue>>` result.

###### `M:IoT.Driver.MitsubishiRx.MitsubishiLogicalTagClient.WriteAsync``1(System.String,``0,System.Threading.CancellationToken)`

```csharp
public System.Threading.Tasks.Task<IoT.Driver.Core.TagOperationResult<T>> WriteAsync<T>(string tagName, T value, System.Threading.CancellationToken cancellationToken)
```
Writes one typed logical tag.

- Parameter `tagName`: The logical name.
- Parameter `value`: The value to write.
- Parameter `cancellationToken`: The cancellation token.
- Returns: The typed operation result.

###### `M:IoT.Driver.MitsubishiRx.MitsubishiLogicalTagClient.WriteManyAsync(System.Collections.Generic.IReadOnlyCollection`1{IoT.Driver.Core.LogicalTagValue},System.Threading.CancellationToken)`

```csharp
public System.Threading.Tasks.Task<System.Collections.Generic.IReadOnlyList<IoT.Driver.Core.TagOperationResult<IoT.Driver.Core.LogicalTagValue>>> WriteManyAsync(System.Collections.Generic.IReadOnlyCollection<IoT.Driver.Core.LogicalTagValue> values, System.Threading.CancellationToken cancellationToken)
```
Executes the `WriteManyAsync` operation.

- Parameter `values`: The `values` value.
- Parameter `cancellationToken`: The `cancellationToken` value.
- Returns: A `System.Threading.Tasks.Task<System.Collections.Generic.IReadOnlyList<IoT.Driver.Core.TagOperationResult<IoT.Driver.Core.LogicalTagValue>>>` result.

###### `P:IoT.Driver.MitsubishiRx.MitsubishiLogicalTagClient.BulkOperationMetrics`

```csharp
public IoT.Driver.MitsubishiRx.MitsubishiLogicalTagBulkOperationMetrics BulkOperationMetrics { get; }
```
Gets an immutable snapshot of deterministic grouped bulk operation counts.

- Value: The `BulkOperationMetrics` value.

###### `P:IoT.Driver.MitsubishiRx.MitsubishiLogicalTagClient.Catalog`

```csharp
public IoT.Driver.Core.ILogicalTagCatalog Catalog { get; }
```
Gets the shared catalog used for registrations and persistence.

- Value: The `Catalog` value.

#### `T:IoT.Driver.MitsubishiRx.MitsubishiLogicalTagRegistration`

```csharp
public class IoT.Driver.MitsubishiRx.MitsubishiLogicalTagRegistration
```
Describes a logical Mitsubishi tag to create and register.

##### Declared public members

###### `M:IoT.Driver.MitsubishiRx.MitsubishiLogicalTagRegistration.#ctor(System.String,System.String,System.String,System.String,System.String,System.Collections.Generic.IReadOnlyDictionary`2{System.String,System.String},IoT.Driver.Core.LogicalTagAccessMode,System.Nullable`1{System.TimeSpan})`

```csharp
public IoT.Driver.MitsubishiRx.MitsubishiLogicalTagRegistration(string Name, string Address, string DataType, string GroupName, string Description, System.Collections.Generic.IReadOnlyDictionary<string, string> Metadata, IoT.Driver.Core.LogicalTagAccessMode AccessMode, System.Nullable<System.TimeSpan> ScanInterval)
```
Initializes a new instance of `IoT.Driver.MitsubishiRx.MitsubishiLogicalTagRegistration`.

- Parameter `Name`: The `Name` value.
- Parameter `Address`: The `Address` value.
- Parameter `DataType`: The `DataType` value.
- Parameter `GroupName`: The `GroupName` value.
- Parameter `Description`: The `Description` value.
- Parameter `Metadata`: The `Metadata` value.
- Parameter `AccessMode`: The `AccessMode` value.
- Parameter `ScanInterval`: The `ScanInterval` value.

###### `M:IoT.Driver.MitsubishiRx.MitsubishiLogicalTagRegistration.Deconstruct(System.String@,System.String@,System.String@,System.String@,System.String@,System.Collections.Generic.IReadOnlyDictionary`2{System.String,System.String}@,IoT.Driver.Core.LogicalTagAccessMode@,System.Nullable`1{System.TimeSpan}@)`

```csharp
public void Deconstruct(out string Name, out string Address, out string DataType, out string GroupName, out string Description, out System.Collections.Generic.IReadOnlyDictionary<string, string> Metadata, out IoT.Driver.Core.LogicalTagAccessMode AccessMode, out System.Nullable<System.TimeSpan> ScanInterval)
```
Deconstructs the value into its component values.

- Parameter `Name`: The `Name` value.
- Parameter `Address`: The `Address` value.
- Parameter `DataType`: The `DataType` value.
- Parameter `GroupName`: The `GroupName` value.
- Parameter `Description`: The `Description` value.
- Parameter `Metadata`: The `Metadata` value.
- Parameter `AccessMode`: The `AccessMode` value.
- Parameter `ScanInterval`: The `ScanInterval` value.

###### `M:IoT.Driver.MitsubishiRx.MitsubishiLogicalTagRegistration.Equals(IoT.Driver.MitsubishiRx.MitsubishiLogicalTagRegistration)`

```csharp
public bool Equals(IoT.Driver.MitsubishiRx.MitsubishiLogicalTagRegistration other)
```
Determines whether the supplied value is equal to the current value.

- Parameter `other`: The `other` value.
- Returns: A `bool` result.

###### `M:IoT.Driver.MitsubishiRx.MitsubishiLogicalTagRegistration.Equals(System.Object)`

```csharp
public bool Equals(object obj)
```
Determines whether the supplied value is equal to the current value.

- Parameter `obj`: The `obj` value.
- Returns: A `bool` result.

###### `M:IoT.Driver.MitsubishiRx.MitsubishiLogicalTagRegistration.GetHashCode`

```csharp
public int GetHashCode()
```
Returns the hash code for the current value.

- Returns: A `int` result.

###### `M:IoT.Driver.MitsubishiRx.MitsubishiLogicalTagRegistration.ToLogicalTag`

```csharp
public IoT.Driver.Core.LogicalTag ToLogicalTag()
```
Creates the common immutable tag model.

- Returns: The common logical tag.

###### `M:IoT.Driver.MitsubishiRx.MitsubishiLogicalTagRegistration.ToString`

```csharp
public string ToString()
```
Returns a string representation of the current value.

- Returns: A `string` result.

###### `M:IoT.Driver.MitsubishiRx.MitsubishiLogicalTagRegistration.op_Equality(IoT.Driver.MitsubishiRx.MitsubishiLogicalTagRegistration,IoT.Driver.MitsubishiRx.MitsubishiLogicalTagRegistration)`

```csharp
public static bool op_Equality(IoT.Driver.MitsubishiRx.MitsubishiLogicalTagRegistration left, IoT.Driver.MitsubishiRx.MitsubishiLogicalTagRegistration right)
```
Determines whether the two supplied values are equal.

- Parameter `left`: The `left` value.
- Parameter `right`: The `right` value.
- Returns: A `bool` result.

###### `M:IoT.Driver.MitsubishiRx.MitsubishiLogicalTagRegistration.op_Inequality(IoT.Driver.MitsubishiRx.MitsubishiLogicalTagRegistration,IoT.Driver.MitsubishiRx.MitsubishiLogicalTagRegistration)`

```csharp
public static bool op_Inequality(IoT.Driver.MitsubishiRx.MitsubishiLogicalTagRegistration left, IoT.Driver.MitsubishiRx.MitsubishiLogicalTagRegistration right)
```
Determines whether the two supplied values are not equal.

- Parameter `left`: The `left` value.
- Parameter `right`: The `right` value.
- Returns: A `bool` result.

###### `P:IoT.Driver.MitsubishiRx.MitsubishiLogicalTagRegistration.AccessMode`

```csharp
public IoT.Driver.Core.LogicalTagAccessMode AccessMode { get; set; }
```
The tag access mode.

- Value: The `AccessMode` value.

###### `P:IoT.Driver.MitsubishiRx.MitsubishiLogicalTagRegistration.Address`

```csharp
public string Address { get; set; }
```
The Mitsubishi device address.

- Value: The `Address` value.

###### `P:IoT.Driver.MitsubishiRx.MitsubishiLogicalTagRegistration.DataType`

```csharp
public string DataType { get; set; }
```
The declared data type.

- Value: The `DataType` value.

###### `P:IoT.Driver.MitsubishiRx.MitsubishiLogicalTagRegistration.Description`

```csharp
public string Description { get; set; }
```
The optional description.

- Value: The `Description` value.

###### `P:IoT.Driver.MitsubishiRx.MitsubishiLogicalTagRegistration.GroupName`

```csharp
public string GroupName { get; set; }
```
The optional primary group.

- Value: The `GroupName` value.

###### `P:IoT.Driver.MitsubishiRx.MitsubishiLogicalTagRegistration.Metadata`

```csharp
public System.Collections.Generic.IReadOnlyDictionary<string, string> Metadata { get; set; }
```
The driver-specific metadata.

- Value: The `Metadata` value.

###### `P:IoT.Driver.MitsubishiRx.MitsubishiLogicalTagRegistration.Name`

```csharp
public string Name { get; set; }
```
The logical tag name.

- Value: The `Name` value.

###### `P:IoT.Driver.MitsubishiRx.MitsubishiLogicalTagRegistration.ScanInterval`

```csharp
public System.Nullable<System.TimeSpan> ScanInterval { get; set; }
```
The optional scan interval.

- Value: The `ScanInterval` value.

#### `T:IoT.Driver.MitsubishiRx.MitsubishiOperationLog`

```csharp
public class IoT.Driver.MitsubishiRx.MitsubishiOperationLog
```
Provides the MitsubishiOperationLog record.

##### Declared public members

###### `M:IoT.Driver.MitsubishiRx.MitsubishiOperationLog.#ctor(System.DateTimeOffset,IoT.Driver.MitsubishiRx.MitsubishiConnectionState,System.String,System.Boolean,System.ReadOnlyMemory`1{System.Byte},System.ReadOnlyMemory`1{System.Byte},System.Exception)`

```csharp
public IoT.Driver.MitsubishiRx.MitsubishiOperationLog(System.DateTimeOffset TimestampUtc, IoT.Driver.MitsubishiRx.MitsubishiConnectionState State, string Description, bool Success, System.ReadOnlyMemory<byte> RequestPayload, System.ReadOnlyMemory<byte> ResponsePayload, System.Exception Exception)
```
Initializes a new instance of `IoT.Driver.MitsubishiRx.MitsubishiOperationLog`.

- Parameter `TimestampUtc`: The `TimestampUtc` value.
- Parameter `State`: The `State` value.
- Parameter `Description`: The `Description` value.
- Parameter `Success`: The `Success` value.
- Parameter `RequestPayload`: The `RequestPayload` value.
- Parameter `ResponsePayload`: The `ResponsePayload` value.
- Parameter `Exception`: The `Exception` value.

###### `M:IoT.Driver.MitsubishiRx.MitsubishiOperationLog.Deconstruct(System.DateTimeOffset@,IoT.Driver.MitsubishiRx.MitsubishiConnectionState@,System.String@,System.Boolean@,System.ReadOnlyMemory`1{System.Byte}@,System.ReadOnlyMemory`1{System.Byte}@,System.Exception@)`

```csharp
public void Deconstruct(out System.DateTimeOffset TimestampUtc, out IoT.Driver.MitsubishiRx.MitsubishiConnectionState State, out string Description, out bool Success, out System.ReadOnlyMemory<byte> RequestPayload, out System.ReadOnlyMemory<byte> ResponsePayload, out System.Exception Exception)
```
Deconstructs the value into its component values.

- Parameter `TimestampUtc`: The `TimestampUtc` value.
- Parameter `State`: The `State` value.
- Parameter `Description`: The `Description` value.
- Parameter `Success`: The `Success` value.
- Parameter `RequestPayload`: The `RequestPayload` value.
- Parameter `ResponsePayload`: The `ResponsePayload` value.
- Parameter `Exception`: The `Exception` value.

###### `M:IoT.Driver.MitsubishiRx.MitsubishiOperationLog.Equals(IoT.Driver.MitsubishiRx.MitsubishiOperationLog)`

```csharp
public bool Equals(IoT.Driver.MitsubishiRx.MitsubishiOperationLog other)
```
Determines whether the supplied value is equal to the current value.

- Parameter `other`: The `other` value.
- Returns: A `bool` result.

###### `M:IoT.Driver.MitsubishiRx.MitsubishiOperationLog.Equals(System.Object)`

```csharp
public bool Equals(object obj)
```
Determines whether the supplied value is equal to the current value.

- Parameter `obj`: The `obj` value.
- Returns: A `bool` result.

###### `M:IoT.Driver.MitsubishiRx.MitsubishiOperationLog.GetHashCode`

```csharp
public int GetHashCode()
```
Returns the hash code for the current value.

- Returns: A `int` result.

###### `M:IoT.Driver.MitsubishiRx.MitsubishiOperationLog.ToString`

```csharp
public string ToString()
```
Returns a string representation of the current value.

- Returns: A `string` result.

###### `M:IoT.Driver.MitsubishiRx.MitsubishiOperationLog.op_Equality(IoT.Driver.MitsubishiRx.MitsubishiOperationLog,IoT.Driver.MitsubishiRx.MitsubishiOperationLog)`

```csharp
public static bool op_Equality(IoT.Driver.MitsubishiRx.MitsubishiOperationLog left, IoT.Driver.MitsubishiRx.MitsubishiOperationLog right)
```
Determines whether the two supplied values are equal.

- Parameter `left`: The `left` value.
- Parameter `right`: The `right` value.
- Returns: A `bool` result.

###### `M:IoT.Driver.MitsubishiRx.MitsubishiOperationLog.op_Inequality(IoT.Driver.MitsubishiRx.MitsubishiOperationLog,IoT.Driver.MitsubishiRx.MitsubishiOperationLog)`

```csharp
public static bool op_Inequality(IoT.Driver.MitsubishiRx.MitsubishiOperationLog left, IoT.Driver.MitsubishiRx.MitsubishiOperationLog right)
```
Determines whether the two supplied values are not equal.

- Parameter `left`: The `left` value.
- Parameter `right`: The `right` value.
- Returns: A `bool` result.

###### `P:IoT.Driver.MitsubishiRx.MitsubishiOperationLog.Description`

```csharp
public string Description { get; set; }
```
The Description parameter.

- Value: The `Description` value.

###### `P:IoT.Driver.MitsubishiRx.MitsubishiOperationLog.Exception`

```csharp
public System.Exception Exception { get; set; }
```
The Exception parameter.

- Value: The `Exception` value.

###### `P:IoT.Driver.MitsubishiRx.MitsubishiOperationLog.RequestPayload`

```csharp
public System.ReadOnlyMemory<byte> RequestPayload { get; set; }
```
The RequestPayload parameter.

- Value: The `RequestPayload` value.

###### `P:IoT.Driver.MitsubishiRx.MitsubishiOperationLog.ResponsePayload`

```csharp
public System.ReadOnlyMemory<byte> ResponsePayload { get; set; }
```
The ResponsePayload parameter.

- Value: The `ResponsePayload` value.

###### `P:IoT.Driver.MitsubishiRx.MitsubishiOperationLog.State`

```csharp
public IoT.Driver.MitsubishiRx.MitsubishiConnectionState State { get; set; }
```
The State parameter.

- Value: The `State` value.

###### `P:IoT.Driver.MitsubishiRx.MitsubishiOperationLog.Success`

```csharp
public bool Success { get; set; }
```
The Success parameter.

- Value: The `Success` value.

###### `P:IoT.Driver.MitsubishiRx.MitsubishiOperationLog.TimestampUtc`

```csharp
public System.DateTimeOffset TimestampUtc { get; set; }
```
The TimestampUtc parameter.

- Value: The `TimestampUtc` value.

#### `T:IoT.Driver.MitsubishiRx.MitsubishiRawCommandRequest`

```csharp
public class IoT.Driver.MitsubishiRx.MitsubishiRawCommandRequest
```
Provides the MitsubishiRawCommandRequest record.

##### Declared public members

###### `M:IoT.Driver.MitsubishiRx.MitsubishiRawCommandRequest.#ctor(System.UInt16,System.UInt16,System.Collections.Generic.IReadOnlyList`1{System.Byte},System.String)`

```csharp
public IoT.Driver.MitsubishiRx.MitsubishiRawCommandRequest(ushort Command, ushort Subcommand, System.Collections.Generic.IReadOnlyList<byte> Body, string Description)
```
Initializes a new instance of `IoT.Driver.MitsubishiRx.MitsubishiRawCommandRequest`.

- Parameter `Command`: The `Command` value.
- Parameter `Subcommand`: The `Subcommand` value.
- Parameter `Body`: The `Body` value.
- Parameter `Description`: The `Description` value.

###### `M:IoT.Driver.MitsubishiRx.MitsubishiRawCommandRequest.Deconstruct(System.UInt16@,System.UInt16@,System.Collections.Generic.IReadOnlyList`1{System.Byte}@,System.String@)`

```csharp
public void Deconstruct(out ushort Command, out ushort Subcommand, out System.Collections.Generic.IReadOnlyList<byte> Body, out string Description)
```
Deconstructs the value into its component values.

- Parameter `Command`: The `Command` value.
- Parameter `Subcommand`: The `Subcommand` value.
- Parameter `Body`: The `Body` value.
- Parameter `Description`: The `Description` value.

###### `M:IoT.Driver.MitsubishiRx.MitsubishiRawCommandRequest.Equals(IoT.Driver.MitsubishiRx.MitsubishiRawCommandRequest)`

```csharp
public bool Equals(IoT.Driver.MitsubishiRx.MitsubishiRawCommandRequest other)
```
Determines whether the supplied value is equal to the current value.

- Parameter `other`: The `other` value.
- Returns: A `bool` result.

###### `M:IoT.Driver.MitsubishiRx.MitsubishiRawCommandRequest.Equals(System.Object)`

```csharp
public bool Equals(object obj)
```
Determines whether the supplied value is equal to the current value.

- Parameter `obj`: The `obj` value.
- Returns: A `bool` result.

###### `M:IoT.Driver.MitsubishiRx.MitsubishiRawCommandRequest.GetHashCode`

```csharp
public int GetHashCode()
```
Returns the hash code for the current value.

- Returns: A `int` result.

###### `M:IoT.Driver.MitsubishiRx.MitsubishiRawCommandRequest.ToString`

```csharp
public string ToString()
```
Returns a string representation of the current value.

- Returns: A `string` result.

###### `M:IoT.Driver.MitsubishiRx.MitsubishiRawCommandRequest.op_Equality(IoT.Driver.MitsubishiRx.MitsubishiRawCommandRequest,IoT.Driver.MitsubishiRx.MitsubishiRawCommandRequest)`

```csharp
public static bool op_Equality(IoT.Driver.MitsubishiRx.MitsubishiRawCommandRequest left, IoT.Driver.MitsubishiRx.MitsubishiRawCommandRequest right)
```
Determines whether the two supplied values are equal.

- Parameter `left`: The `left` value.
- Parameter `right`: The `right` value.
- Returns: A `bool` result.

###### `M:IoT.Driver.MitsubishiRx.MitsubishiRawCommandRequest.op_Inequality(IoT.Driver.MitsubishiRx.MitsubishiRawCommandRequest,IoT.Driver.MitsubishiRx.MitsubishiRawCommandRequest)`

```csharp
public static bool op_Inequality(IoT.Driver.MitsubishiRx.MitsubishiRawCommandRequest left, IoT.Driver.MitsubishiRx.MitsubishiRawCommandRequest right)
```
Determines whether the two supplied values are not equal.

- Parameter `left`: The `left` value.
- Parameter `right`: The `right` value.
- Returns: A `bool` result.

###### `P:IoT.Driver.MitsubishiRx.MitsubishiRawCommandRequest.Body`

```csharp
public System.Collections.Generic.IReadOnlyList<byte> Body { get; set; }
```
The Body parameter.

- Value: The `Body` value.

###### `P:IoT.Driver.MitsubishiRx.MitsubishiRawCommandRequest.Command`

```csharp
public ushort Command { get; set; }
```
The Command parameter.

- Value: The `Command` value.

###### `P:IoT.Driver.MitsubishiRx.MitsubishiRawCommandRequest.Description`

```csharp
public string Description { get; set; }
```
The Description parameter.

- Value: The `Description` value.

###### `P:IoT.Driver.MitsubishiRx.MitsubishiRawCommandRequest.ResolvedBody`

```csharp
public System.Collections.Generic.IReadOnlyList<byte> ResolvedBody { get; }
```
Gets or sets the ResolvedBody property.

- Value: The `ResolvedBody` value.

###### `P:IoT.Driver.MitsubishiRx.MitsubishiRawCommandRequest.Subcommand`

```csharp
public ushort Subcommand { get; set; }
```
The Subcommand parameter.

- Value: The `Subcommand` value.

#### `T:IoT.Driver.MitsubishiRx.MitsubishiReactiveQuality`

```csharp
public enum IoT.Driver.MitsubishiRx.MitsubishiReactiveQuality
```
Defines the MitsubishiReactiveQuality values.

##### Declared public members

###### `F:IoT.Driver.MitsubishiRx.MitsubishiReactiveQuality.Bad`

```csharp
public static const IoT.Driver.MitsubishiRx.MitsubishiReactiveQuality Bad
```
Represents the Bad option.

###### `F:IoT.Driver.MitsubishiRx.MitsubishiReactiveQuality.Error`

```csharp
public static const IoT.Driver.MitsubishiRx.MitsubishiReactiveQuality Error
```
Represents the Error option.

###### `F:IoT.Driver.MitsubishiRx.MitsubishiReactiveQuality.Good`

```csharp
public static const IoT.Driver.MitsubishiRx.MitsubishiReactiveQuality Good
```
Represents the Good option.

###### `F:IoT.Driver.MitsubishiRx.MitsubishiReactiveQuality.Heartbeat`

```csharp
public static const IoT.Driver.MitsubishiRx.MitsubishiReactiveQuality Heartbeat
```
Represents the Heartbeat option.

###### `F:IoT.Driver.MitsubishiRx.MitsubishiReactiveQuality.Stale`

```csharp
public static const IoT.Driver.MitsubishiRx.MitsubishiReactiveQuality Stale
```
Represents the Stale option.

#### `T:IoT.Driver.MitsubishiRx.MitsubishiReactiveValue`

```csharp
public class IoT.Driver.MitsubishiRx.MitsubishiReactiveValue
```
Provides the MitsubishiReactiveValue type.

##### Declared public members

###### `M:IoT.Driver.MitsubishiRx.MitsubishiReactiveValue.FromResponse``1(IoT.Driver.MitsubishiRx.Responce`1{``0},System.DateTimeOffset,System.String)`

```csharp
public static IoT.Driver.MitsubishiRx.MitsubishiReactiveValue<T> FromResponse<T>(IoT.Driver.MitsubishiRx.Responce<T> response, System.DateTimeOffset timestampUtc, string source)
```
Executes the `FromResponse` operation.

- Parameter `response`: The `response` value.
- Parameter `timestampUtc`: The `timestampUtc` value.
- Parameter `source`: The `source` value.
- Returns: A `IoT.Driver.MitsubishiRx.MitsubishiReactiveValue<T>` result.

###### `M:IoT.Driver.MitsubishiRx.MitsubishiReactiveValue.Heartbeat``1(IoT.Driver.MitsubishiRx.MitsubishiReactiveValue`1{``0},System.DateTimeOffset)`

```csharp
public static IoT.Driver.MitsubishiRx.MitsubishiReactiveValue<T> Heartbeat<T>(IoT.Driver.MitsubishiRx.MitsubishiReactiveValue<T> value, System.DateTimeOffset timestampUtc)
```
Executes the `Heartbeat` operation.

- Parameter `value`: The `value` value.
- Parameter `timestampUtc`: The `timestampUtc` value.
- Returns: A `IoT.Driver.MitsubishiRx.MitsubishiReactiveValue<T>` result.

###### `M:IoT.Driver.MitsubishiRx.MitsubishiReactiveValue.Stale``1(IoT.Driver.MitsubishiRx.MitsubishiReactiveValue`1{``0},System.DateTimeOffset)`

```csharp
public static IoT.Driver.MitsubishiRx.MitsubishiReactiveValue<T> Stale<T>(IoT.Driver.MitsubishiRx.MitsubishiReactiveValue<T> value, System.DateTimeOffset timestampUtc)
```
Executes the `Stale` operation.

- Parameter `value`: The `value` value.
- Parameter `timestampUtc`: The `timestampUtc` value.
- Returns: A `IoT.Driver.MitsubishiRx.MitsubishiReactiveValue<T>` result.

#### `T:IoT.Driver.MitsubishiRx.MitsubishiReactiveValue`1`

```csharp
public class IoT.Driver.MitsubishiRx.MitsubishiReactiveValue`1
```
Provides the MitsubishiReactiveValue record.

##### Declared public members

###### `M:IoT.Driver.MitsubishiRx.MitsubishiReactiveValue`1.#ctor(`0,System.DateTimeOffset,IoT.Driver.MitsubishiRx.MitsubishiReactiveQuality,System.Boolean,System.Boolean,System.String,System.String,System.Int32,System.Exception)`

```csharp
public IoT.Driver.MitsubishiRx.MitsubishiReactiveValue<T>(T Value, System.DateTimeOffset TimestampUtc, IoT.Driver.MitsubishiRx.MitsubishiReactiveQuality Quality, bool IsHeartbeat, bool IsStale, string Source, string Error, int ErrorCode, System.Exception Exception)
```
Provides the MitsubishiReactiveValue record.

- Parameter `Value`: The Value parameter.
- Parameter `TimestampUtc`: The TimestampUtc parameter.
- Parameter `Quality`: The Quality parameter.
- Parameter `IsHeartbeat`: The IsHeartbeat parameter.
- Parameter `IsStale`: The IsStale parameter.
- Parameter `Source`: The Source parameter.
- Parameter `Error`: The Error parameter.
- Parameter `ErrorCode`: The ErrorCode parameter.
- Parameter `Exception`: The Exception parameter.

###### `M:IoT.Driver.MitsubishiRx.MitsubishiReactiveValue`1.Deconstruct(`0@,System.DateTimeOffset@,IoT.Driver.MitsubishiRx.MitsubishiReactiveQuality@,System.Boolean@,System.Boolean@,System.String@,System.String@,System.Int32@,System.Exception@)`

```csharp
public void Deconstruct(out T Value, out System.DateTimeOffset TimestampUtc, out IoT.Driver.MitsubishiRx.MitsubishiReactiveQuality Quality, out bool IsHeartbeat, out bool IsStale, out string Source, out string Error, out int ErrorCode, out System.Exception Exception)
```
Deconstructs the value into its component values.

- Parameter `Value`: The `Value` value.
- Parameter `TimestampUtc`: The `TimestampUtc` value.
- Parameter `Quality`: The `Quality` value.
- Parameter `IsHeartbeat`: The `IsHeartbeat` value.
- Parameter `IsStale`: The `IsStale` value.
- Parameter `Source`: The `Source` value.
- Parameter `Error`: The `Error` value.
- Parameter `ErrorCode`: The `ErrorCode` value.
- Parameter `Exception`: The `Exception` value.

###### `M:IoT.Driver.MitsubishiRx.MitsubishiReactiveValue`1.Equals(IoT.Driver.MitsubishiRx.MitsubishiReactiveValue`1{`0})`

```csharp
public bool Equals(IoT.Driver.MitsubishiRx.MitsubishiReactiveValue<T> other)
```
Determines whether the supplied value is equal to the current value.

- Parameter `other`: The `other` value.
- Returns: A `bool` result.

###### `M:IoT.Driver.MitsubishiRx.MitsubishiReactiveValue`1.Equals(System.Object)`

```csharp
public bool Equals(object obj)
```
Determines whether the supplied value is equal to the current value.

- Parameter `obj`: The `obj` value.
- Returns: A `bool` result.

###### `M:IoT.Driver.MitsubishiRx.MitsubishiReactiveValue`1.GetHashCode`

```csharp
public int GetHashCode()
```
Returns the hash code for the current value.

- Returns: A `int` result.

###### `M:IoT.Driver.MitsubishiRx.MitsubishiReactiveValue`1.ToString`

```csharp
public string ToString()
```
Returns a string representation of the current value.

- Returns: A `string` result.

###### `M:IoT.Driver.MitsubishiRx.MitsubishiReactiveValue`1.op_Equality(IoT.Driver.MitsubishiRx.MitsubishiReactiveValue`1{`0},IoT.Driver.MitsubishiRx.MitsubishiReactiveValue`1{`0})`

```csharp
public static bool op_Equality(IoT.Driver.MitsubishiRx.MitsubishiReactiveValue<T> left, IoT.Driver.MitsubishiRx.MitsubishiReactiveValue<T> right)
```
Determines whether the two supplied values are equal.

- Parameter `left`: The `left` value.
- Parameter `right`: The `right` value.
- Returns: A `bool` result.

###### `M:IoT.Driver.MitsubishiRx.MitsubishiReactiveValue`1.op_Inequality(IoT.Driver.MitsubishiRx.MitsubishiReactiveValue`1{`0},IoT.Driver.MitsubishiRx.MitsubishiReactiveValue`1{`0})`

```csharp
public static bool op_Inequality(IoT.Driver.MitsubishiRx.MitsubishiReactiveValue<T> left, IoT.Driver.MitsubishiRx.MitsubishiReactiveValue<T> right)
```
Determines whether the two supplied values are not equal.

- Parameter `left`: The `left` value.
- Parameter `right`: The `right` value.
- Returns: A `bool` result.

###### `P:IoT.Driver.MitsubishiRx.MitsubishiReactiveValue`1.Error`

```csharp
public string Error { get; set; }
```
The Error parameter.

- Value: The `Error` value.

###### `P:IoT.Driver.MitsubishiRx.MitsubishiReactiveValue`1.ErrorCode`

```csharp
public int ErrorCode { get; set; }
```
The ErrorCode parameter.

- Value: The `ErrorCode` value.

###### `P:IoT.Driver.MitsubishiRx.MitsubishiReactiveValue`1.Exception`

```csharp
public System.Exception Exception { get; set; }
```
The Exception parameter.

- Value: The `Exception` value.

###### `P:IoT.Driver.MitsubishiRx.MitsubishiReactiveValue`1.IsHeartbeat`

```csharp
public bool IsHeartbeat { get; set; }
```
The IsHeartbeat parameter.

- Value: The `IsHeartbeat` value.

###### `P:IoT.Driver.MitsubishiRx.MitsubishiReactiveValue`1.IsStale`

```csharp
public bool IsStale { get; set; }
```
The IsStale parameter.

- Value: The `IsStale` value.

###### `P:IoT.Driver.MitsubishiRx.MitsubishiReactiveValue`1.Quality`

```csharp
public IoT.Driver.MitsubishiRx.MitsubishiReactiveQuality Quality { get; set; }
```
The Quality parameter.

- Value: The `Quality` value.

###### `P:IoT.Driver.MitsubishiRx.MitsubishiReactiveValue`1.Source`

```csharp
public string Source { get; set; }
```
The Source parameter.

- Value: The `Source` value.

###### `P:IoT.Driver.MitsubishiRx.MitsubishiReactiveValue`1.TimestampUtc`

```csharp
public System.DateTimeOffset TimestampUtc { get; set; }
```
The TimestampUtc parameter.

- Value: The `TimestampUtc` value.

###### `P:IoT.Driver.MitsubishiRx.MitsubishiReactiveValue`1.Value`

```csharp
public T Value { get; set; }
```
The Value parameter.

- Value: The `Value` value.

#### `T:IoT.Driver.MitsubishiRx.MitsubishiReactiveWriteMode`

```csharp
public enum IoT.Driver.MitsubishiRx.MitsubishiReactiveWriteMode
```
Defines the MitsubishiReactiveWriteMode values.

##### Declared public members

###### `F:IoT.Driver.MitsubishiRx.MitsubishiReactiveWriteMode.Coalescing`

```csharp
public static const IoT.Driver.MitsubishiRx.MitsubishiReactiveWriteMode Coalescing
```
Represents the Coalescing option.

###### `F:IoT.Driver.MitsubishiRx.MitsubishiReactiveWriteMode.LatestWins`

```csharp
public static const IoT.Driver.MitsubishiRx.MitsubishiReactiveWriteMode LatestWins
```
Represents the LatestWins option.

###### `F:IoT.Driver.MitsubishiRx.MitsubishiReactiveWriteMode.Queued`

```csharp
public static const IoT.Driver.MitsubishiRx.MitsubishiReactiveWriteMode Queued
```
Represents the Queued option.

#### `T:IoT.Driver.MitsubishiRx.MitsubishiReactiveWritePipeline`1`

```csharp
public class IoT.Driver.MitsubishiRx.MitsubishiReactiveWritePipeline`1
```
Provides the MitsubishiReactiveWritePipeline type.

##### Declared public members

###### `M:IoT.Driver.MitsubishiRx.MitsubishiReactiveWritePipeline`1.Dispose`

```csharp
public void Dispose()
```
Executes the Dispose operation.

###### `M:IoT.Driver.MitsubishiRx.MitsubishiReactiveWritePipeline`1.Post(`0)`

```csharp
public void Post(TPayload payload)
```
Executes the Post operation.

- Parameter `payload`: The payload parameter.

###### `P:IoT.Driver.MitsubishiRx.MitsubishiReactiveWritePipeline`1.Mode`

```csharp
public IoT.Driver.MitsubishiRx.MitsubishiReactiveWriteMode Mode { get; }
```
Gets or sets the Mode property.

- Value: The `Mode` value.

###### `P:IoT.Driver.MitsubishiRx.MitsubishiReactiveWritePipeline`1.Results`

```csharp
public System.IObservable<IoT.Driver.MitsubishiRx.MitsubishiReactiveWriteResult> Results { get; }
```
Gets or sets the Results property.

- Value: The `Results` value.

#### `T:IoT.Driver.MitsubishiRx.MitsubishiReactiveWriteResult`

```csharp
public class IoT.Driver.MitsubishiRx.MitsubishiReactiveWriteResult
```
Provides the MitsubishiReactiveWriteResult record.

##### Declared public members

###### `M:IoT.Driver.MitsubishiRx.MitsubishiReactiveWriteResult.#ctor(System.String,System.DateTimeOffset,IoT.Driver.MitsubishiRx.MitsubishiReactiveWriteMode,System.Boolean,System.String,System.Int32,System.Exception)`

```csharp
public IoT.Driver.MitsubishiRx.MitsubishiReactiveWriteResult(string Target, System.DateTimeOffset TimestampUtc, IoT.Driver.MitsubishiRx.MitsubishiReactiveWriteMode Mode, bool Success, string Error, int ErrorCode, System.Exception Exception)
```
Provides the MitsubishiReactiveWriteResult record.

- Parameter `Target`: The Target parameter.
- Parameter `TimestampUtc`: The TimestampUtc parameter.
- Parameter `Mode`: The Mode parameter.
- Parameter `Success`: The Success parameter.
- Parameter `Error`: The Error parameter.
- Parameter `ErrorCode`: The ErrorCode parameter.
- Parameter `Exception`: The Exception parameter.

###### `M:IoT.Driver.MitsubishiRx.MitsubishiReactiveWriteResult.Deconstruct(System.String@,System.DateTimeOffset@,IoT.Driver.MitsubishiRx.MitsubishiReactiveWriteMode@,System.Boolean@,System.String@,System.Int32@,System.Exception@)`

```csharp
public void Deconstruct(out string Target, out System.DateTimeOffset TimestampUtc, out IoT.Driver.MitsubishiRx.MitsubishiReactiveWriteMode Mode, out bool Success, out string Error, out int ErrorCode, out System.Exception Exception)
```
Deconstructs the value into its component values.

- Parameter `Target`: The `Target` value.
- Parameter `TimestampUtc`: The `TimestampUtc` value.
- Parameter `Mode`: The `Mode` value.
- Parameter `Success`: The `Success` value.
- Parameter `Error`: The `Error` value.
- Parameter `ErrorCode`: The `ErrorCode` value.
- Parameter `Exception`: The `Exception` value.

###### `M:IoT.Driver.MitsubishiRx.MitsubishiReactiveWriteResult.Equals(IoT.Driver.MitsubishiRx.MitsubishiReactiveWriteResult)`

```csharp
public bool Equals(IoT.Driver.MitsubishiRx.MitsubishiReactiveWriteResult other)
```
Determines whether the supplied value is equal to the current value.

- Parameter `other`: The `other` value.
- Returns: A `bool` result.

###### `M:IoT.Driver.MitsubishiRx.MitsubishiReactiveWriteResult.Equals(System.Object)`

```csharp
public bool Equals(object obj)
```
Determines whether the supplied value is equal to the current value.

- Parameter `obj`: The `obj` value.
- Returns: A `bool` result.

###### `M:IoT.Driver.MitsubishiRx.MitsubishiReactiveWriteResult.GetHashCode`

```csharp
public int GetHashCode()
```
Returns the hash code for the current value.

- Returns: A `int` result.

###### `M:IoT.Driver.MitsubishiRx.MitsubishiReactiveWriteResult.ToString`

```csharp
public string ToString()
```
Returns a string representation of the current value.

- Returns: A `string` result.

###### `M:IoT.Driver.MitsubishiRx.MitsubishiReactiveWriteResult.op_Equality(IoT.Driver.MitsubishiRx.MitsubishiReactiveWriteResult,IoT.Driver.MitsubishiRx.MitsubishiReactiveWriteResult)`

```csharp
public static bool op_Equality(IoT.Driver.MitsubishiRx.MitsubishiReactiveWriteResult left, IoT.Driver.MitsubishiRx.MitsubishiReactiveWriteResult right)
```
Determines whether the two supplied values are equal.

- Parameter `left`: The `left` value.
- Parameter `right`: The `right` value.
- Returns: A `bool` result.

###### `M:IoT.Driver.MitsubishiRx.MitsubishiReactiveWriteResult.op_Inequality(IoT.Driver.MitsubishiRx.MitsubishiReactiveWriteResult,IoT.Driver.MitsubishiRx.MitsubishiReactiveWriteResult)`

```csharp
public static bool op_Inequality(IoT.Driver.MitsubishiRx.MitsubishiReactiveWriteResult left, IoT.Driver.MitsubishiRx.MitsubishiReactiveWriteResult right)
```
Determines whether the two supplied values are not equal.

- Parameter `left`: The `left` value.
- Parameter `right`: The `right` value.
- Returns: A `bool` result.

###### `P:IoT.Driver.MitsubishiRx.MitsubishiReactiveWriteResult.Error`

```csharp
public string Error { get; set; }
```
The Error parameter.

- Value: The `Error` value.

###### `P:IoT.Driver.MitsubishiRx.MitsubishiReactiveWriteResult.ErrorCode`

```csharp
public int ErrorCode { get; set; }
```
The ErrorCode parameter.

- Value: The `ErrorCode` value.

###### `P:IoT.Driver.MitsubishiRx.MitsubishiReactiveWriteResult.Exception`

```csharp
public System.Exception Exception { get; set; }
```
The Exception parameter.

- Value: The `Exception` value.

###### `P:IoT.Driver.MitsubishiRx.MitsubishiReactiveWriteResult.Mode`

```csharp
public IoT.Driver.MitsubishiRx.MitsubishiReactiveWriteMode Mode { get; set; }
```
The Mode parameter.

- Value: The `Mode` value.

###### `P:IoT.Driver.MitsubishiRx.MitsubishiReactiveWriteResult.Success`

```csharp
public bool Success { get; set; }
```
The Success parameter.

- Value: The `Success` value.

###### `P:IoT.Driver.MitsubishiRx.MitsubishiReactiveWriteResult.Target`

```csharp
public string Target { get; set; }
```
The Target parameter.

- Value: The `Target` value.

###### `P:IoT.Driver.MitsubishiRx.MitsubishiReactiveWriteResult.TimestampUtc`

```csharp
public System.DateTimeOffset TimestampUtc { get; set; }
```
The TimestampUtc parameter.

- Value: The `TimestampUtc` value.

#### `T:IoT.Driver.MitsubishiRx.MitsubishiRoute`

```csharp
public class IoT.Driver.MitsubishiRx.MitsubishiRoute
```
Provides the MitsubishiRoute record.

##### Declared public members

###### `M:IoT.Driver.MitsubishiRx.MitsubishiRoute.#ctor(System.Byte,System.Byte,System.UInt16,System.Byte,System.Nullable`1{System.UInt16})`

```csharp
public IoT.Driver.MitsubishiRx.MitsubishiRoute(byte NetworkNumber, byte StationNumber, ushort ModuleIoNumber, byte MultidropStationNumber, System.Nullable<ushort> ExtensionStationNumber)
```
Initializes a new instance of `IoT.Driver.MitsubishiRx.MitsubishiRoute`.

- Parameter `NetworkNumber`: The `NetworkNumber` value.
- Parameter `StationNumber`: The `StationNumber` value.
- Parameter `ModuleIoNumber`: The `ModuleIoNumber` value.
- Parameter `MultidropStationNumber`: The `MultidropStationNumber` value.
- Parameter `ExtensionStationNumber`: The `ExtensionStationNumber` value.

###### `M:IoT.Driver.MitsubishiRx.MitsubishiRoute.Deconstruct(System.Byte@,System.Byte@,System.UInt16@,System.Byte@,System.Nullable`1{System.UInt16}@)`

```csharp
public void Deconstruct(out byte NetworkNumber, out byte StationNumber, out ushort ModuleIoNumber, out byte MultidropStationNumber, out System.Nullable<ushort> ExtensionStationNumber)
```
Deconstructs the value into its component values.

- Parameter `NetworkNumber`: The `NetworkNumber` value.
- Parameter `StationNumber`: The `StationNumber` value.
- Parameter `ModuleIoNumber`: The `ModuleIoNumber` value.
- Parameter `MultidropStationNumber`: The `MultidropStationNumber` value.
- Parameter `ExtensionStationNumber`: The `ExtensionStationNumber` value.

###### `M:IoT.Driver.MitsubishiRx.MitsubishiRoute.Equals(IoT.Driver.MitsubishiRx.MitsubishiRoute)`

```csharp
public bool Equals(IoT.Driver.MitsubishiRx.MitsubishiRoute other)
```
Determines whether the supplied value is equal to the current value.

- Parameter `other`: The `other` value.
- Returns: A `bool` result.

###### `M:IoT.Driver.MitsubishiRx.MitsubishiRoute.Equals(System.Object)`

```csharp
public bool Equals(object obj)
```
Determines whether the supplied value is equal to the current value.

- Parameter `obj`: The `obj` value.
- Returns: A `bool` result.

###### `M:IoT.Driver.MitsubishiRx.MitsubishiRoute.GetHashCode`

```csharp
public int GetHashCode()
```
Returns the hash code for the current value.

- Returns: A `int` result.

###### `M:IoT.Driver.MitsubishiRx.MitsubishiRoute.ToString`

```csharp
public string ToString()
```
Returns a string representation of the current value.

- Returns: A `string` result.

###### `M:IoT.Driver.MitsubishiRx.MitsubishiRoute.op_Equality(IoT.Driver.MitsubishiRx.MitsubishiRoute,IoT.Driver.MitsubishiRx.MitsubishiRoute)`

```csharp
public static bool op_Equality(IoT.Driver.MitsubishiRx.MitsubishiRoute left, IoT.Driver.MitsubishiRx.MitsubishiRoute right)
```
Determines whether the two supplied values are equal.

- Parameter `left`: The `left` value.
- Parameter `right`: The `right` value.
- Returns: A `bool` result.

###### `M:IoT.Driver.MitsubishiRx.MitsubishiRoute.op_Inequality(IoT.Driver.MitsubishiRx.MitsubishiRoute,IoT.Driver.MitsubishiRx.MitsubishiRoute)`

```csharp
public static bool op_Inequality(IoT.Driver.MitsubishiRx.MitsubishiRoute left, IoT.Driver.MitsubishiRx.MitsubishiRoute right)
```
Determines whether the two supplied values are not equal.

- Parameter `left`: The `left` value.
- Parameter `right`: The `right` value.
- Returns: A `bool` result.

###### `P:IoT.Driver.MitsubishiRx.MitsubishiRoute.Default`

```csharp
public IoT.Driver.MitsubishiRx.MitsubishiRoute Default { get; }
```
Gets or sets the Default property.

- Value: The `Default` value.

###### `P:IoT.Driver.MitsubishiRx.MitsubishiRoute.ExtensionStationNumber`

```csharp
public System.Nullable<ushort> ExtensionStationNumber { get; set; }
```
The ExtensionStationNumber parameter.

- Value: The `ExtensionStationNumber` value.

###### `P:IoT.Driver.MitsubishiRx.MitsubishiRoute.ModuleIoNumber`

```csharp
public ushort ModuleIoNumber { get; set; }
```
The ModuleIoNumber parameter.

- Value: The `ModuleIoNumber` value.

###### `P:IoT.Driver.MitsubishiRx.MitsubishiRoute.MultidropStationNumber`

```csharp
public byte MultidropStationNumber { get; set; }
```
The MultidropStationNumber parameter.

- Value: The `MultidropStationNumber` value.

###### `P:IoT.Driver.MitsubishiRx.MitsubishiRoute.NetworkNumber`

```csharp
public byte NetworkNumber { get; set; }
```
The NetworkNumber parameter.

- Value: The `NetworkNumber` value.

###### `P:IoT.Driver.MitsubishiRx.MitsubishiRoute.StationNumber`

```csharp
public byte StationNumber { get; set; }
```
The StationNumber parameter.

- Value: The `StationNumber` value.

#### `T:IoT.Driver.MitsubishiRx.MitsubishiRx`

```csharp
public class IoT.Driver.MitsubishiRx.MitsubishiRx
```
Provides the MitsubishiRx type.

##### Declared public members

###### `M:IoT.Driver.MitsubishiRx.MitsubishiRx.#ctor(IoT.Driver.MitsubishiRx.CpuType,System.String,System.Int32,System.Int32)`

```csharp
public IoT.Driver.MitsubishiRx.MitsubishiRx(IoT.Driver.MitsubishiRx.CpuType cpuType, string ip, int port, int timeout)
```
Initializes a new instance of the MitsubishiRx class.

- Parameter `cpuType`: The cpuType parameter.
- Parameter `ip`: The ip parameter.
- Parameter `port`: The port parameter.
- Parameter `timeout`: The timeout parameter.

###### `M:IoT.Driver.MitsubishiRx.MitsubishiRx.#ctor(IoT.Driver.MitsubishiRx.MitsubishiClientOptions,IoT.Driver.MitsubishiRx.IMitsubishiTransport,ReactiveUI.Primitives.Concurrency.ISequencer)`

```csharp
public IoT.Driver.MitsubishiRx.MitsubishiRx(IoT.Driver.MitsubishiRx.MitsubishiClientOptions options, IoT.Driver.MitsubishiRx.IMitsubishiTransport transport, ReactiveUI.Primitives.Concurrency.ISequencer scheduler)
```
Initializes a new instance of the MitsubishiRx class.

- Parameter `options`: The options parameter.
- Parameter `transport`: The transport parameter.
- Parameter `scheduler`: The scheduler parameter.

###### `M:IoT.Driver.MitsubishiRx.MitsubishiRx.#ctor(IoT.Driver.MitsubishiRx.MitsubishiClientOptions,IoT.Driver.MitsubishiRx.IMitsubishiTransport,ReactiveUI.Primitives.Concurrency.ISequencer,System.TimeProvider)`

```csharp
public IoT.Driver.MitsubishiRx.MitsubishiRx(IoT.Driver.MitsubishiRx.MitsubishiClientOptions options, IoT.Driver.MitsubishiRx.IMitsubishiTransport transport, ReactiveUI.Primitives.Concurrency.ISequencer scheduler, System.TimeProvider timeProvider)
```
Initializes a new instance of the MitsubishiRx class.

- Parameter `options`: The options parameter.
- Parameter `transport`: The transport parameter.
- Parameter `scheduler`: The scheduler parameter.
- Parameter `timeProvider`: The time provider.

###### `M:IoT.Driver.MitsubishiRx.MitsubishiRx.ClearErrorAsync(System.Threading.CancellationToken)`

```csharp
public System.Threading.Tasks.Task<IoT.Driver.MitsubishiRx.Responce> ClearErrorAsync(System.Threading.CancellationToken cancellationToken)
```
Executes the ClearErrorAsync operation.

- Parameter `cancellationToken`: The cancellationToken parameter.
- Returns: The ClearErrorAsync operation result.

###### `M:IoT.Driver.MitsubishiRx.MitsubishiRx.CloseAsync(System.Threading.CancellationToken)`

```csharp
public System.Threading.Tasks.Task<IoT.Driver.MitsubishiRx.Responce> CloseAsync(System.Threading.CancellationToken cancellationToken)
```
Executes the CloseAsync operation.

- Parameter `cancellationToken`: The cancellationToken parameter.
- Returns: The CloseAsync operation result.

###### `M:IoT.Driver.MitsubishiRx.MitsubishiRx.CreateLogicalTagClient(IoT.Driver.Core.ILogicalTagCatalog,System.Nullable`1{System.TimeSpan},IoT.Driver.Core.LogicalTagSqliteStore)`

```csharp
public IoT.Driver.MitsubishiRx.MitsubishiLogicalTagClient CreateLogicalTagClient(IoT.Driver.Core.ILogicalTagCatalog catalog, System.Nullable<System.TimeSpan> defaultScanInterval, IoT.Driver.Core.LogicalTagSqliteStore store)
```
Executes the `CreateLogicalTagClient` operation.

- Parameter `catalog`: The `catalog` value.
- Parameter `defaultScanInterval`: The `defaultScanInterval` value.
- Parameter `store`: The `store` value.
- Returns: A `IoT.Driver.MitsubishiRx.MitsubishiLogicalTagClient` result.

###### `M:IoT.Driver.MitsubishiRx.MitsubishiRx.CreateReactiveTagWritePipeline``1(IoT.Driver.Core.LogicalTagKey`1{``0},IoT.Driver.MitsubishiRx.MitsubishiReactiveWriteMode,System.Nullable`1{System.TimeSpan})`

```csharp
public IoT.Driver.MitsubishiRx.MitsubishiReactiveWritePipeline<T> CreateReactiveTagWritePipeline<T>(IoT.Driver.Core.LogicalTagKey<T> tag, IoT.Driver.MitsubishiRx.MitsubishiReactiveWriteMode mode, System.Nullable<System.TimeSpan> coalescingWindow)
```
Executes the `CreateReactiveTagWritePipeline` operation.

- Parameter `tag`: The `tag` value.
- Parameter `mode`: The `mode` value.
- Parameter `coalescingWindow`: The `coalescingWindow` value.
- Returns: A `IoT.Driver.MitsubishiRx.MitsubishiReactiveWritePipeline<T>` result.

###### `M:IoT.Driver.MitsubishiRx.MitsubishiRx.CreateReactiveWordWritePipeline(System.String,IoT.Driver.MitsubishiRx.MitsubishiReactiveWriteMode,System.Nullable`1{System.TimeSpan})`

```csharp
public IoT.Driver.MitsubishiRx.MitsubishiReactiveWritePipeline<System.Collections.Generic.IReadOnlyList<ushort>> CreateReactiveWordWritePipeline(string address, IoT.Driver.MitsubishiRx.MitsubishiReactiveWriteMode mode, System.Nullable<System.TimeSpan> coalescingWindow)
```
Executes the `CreateReactiveWordWritePipeline` operation.

- Parameter `address`: The `address` value.
- Parameter `mode`: The `mode` value.
- Parameter `coalescingWindow`: The `coalescingWindow` value.
- Returns: A `IoT.Driver.MitsubishiRx.MitsubishiReactiveWritePipeline<System.Collections.Generic.IReadOnlyList<ushort>>` result.

###### `M:IoT.Driver.MitsubishiRx.MitsubishiRx.Dispose`

```csharp
public void Dispose()
```
Executes the Dispose operation.

###### `M:IoT.Driver.MitsubishiRx.MitsubishiRx.DisposeAsync`

```csharp
public System.Threading.Tasks.ValueTask DisposeAsync()
```
Executes the DisposeAsync operation.

- Returns: The DisposeAsync operation result.

###### `M:IoT.Driver.MitsubishiRx.MitsubishiRx.ExecuteMonitorAsync(System.Threading.CancellationToken)`

```csharp
public System.Threading.Tasks.Task<IoT.Driver.MitsubishiRx.Responce<byte[]>> ExecuteMonitorAsync(System.Threading.CancellationToken cancellationToken)
```
Executes the ExecuteMonitorAsync operation.

- Parameter `cancellationToken`: The cancellationToken parameter.
- Returns: The ExecuteMonitorAsync operation result.

###### `M:IoT.Driver.MitsubishiRx.MitsubishiRx.ExecuteRawAsync(IoT.Driver.MitsubishiRx.MitsubishiRawCommandRequest,System.Threading.CancellationToken)`

```csharp
public System.Threading.Tasks.Task<IoT.Driver.MitsubishiRx.Responce<byte[]>> ExecuteRawAsync(IoT.Driver.MitsubishiRx.MitsubishiRawCommandRequest request, System.Threading.CancellationToken cancellationToken)
```
Executes the ExecuteRawAsync operation.

- Parameter `request`: The request parameter.
- Parameter `cancellationToken`: The cancellationToken parameter.
- Returns: The ExecuteRawAsync operation result.

###### `M:IoT.Driver.MitsubishiRx.MitsubishiRx.LoadAndValidateTagDatabase(System.String)`

```csharp
public IoT.Driver.MitsubishiRx.Responce<IoT.Driver.MitsubishiRx.MitsubishiTagDatabase> LoadAndValidateTagDatabase(string path)
```
Executes the LoadAndValidateTagDatabase operation.

- Parameter `path`: The path parameter.
- Returns: The LoadAndValidateTagDatabase operation result.

###### `M:IoT.Driver.MitsubishiRx.MitsubishiRx.LoadAndValidateTagDatabase(System.String,IoT.Driver.MitsubishiRx.MitsubishiTagRolloutPolicy)`

```csharp
public IoT.Driver.MitsubishiRx.Responce<IoT.Driver.MitsubishiRx.MitsubishiTagDatabase> LoadAndValidateTagDatabase(string path, IoT.Driver.MitsubishiRx.MitsubishiTagRolloutPolicy policy)
```
Executes the LoadAndValidateTagDatabase operation.

- Parameter `path`: The path parameter.
- Parameter `policy`: The policy parameter.
- Returns: The LoadAndValidateTagDatabase operation result.

###### `M:IoT.Driver.MitsubishiRx.MitsubishiRx.LockAsync(System.String,System.Threading.CancellationToken)`

```csharp
public System.Threading.Tasks.Task<IoT.Driver.MitsubishiRx.Responce> LockAsync(string password, System.Threading.CancellationToken cancellationToken)
```
Executes the LockAsync operation.

- Parameter `password`: The password parameter.
- Parameter `cancellationToken`: The cancellationToken parameter.
- Returns: The LockAsync operation result.

###### `M:IoT.Driver.MitsubishiRx.MitsubishiRx.LoopbackAsync(System.Byte[],System.Threading.CancellationToken)`

```csharp
public System.Threading.Tasks.Task<IoT.Driver.MitsubishiRx.Responce<byte[]>> LoopbackAsync(byte[] data, System.Threading.CancellationToken cancellationToken)
```
Executes the LoopbackAsync operation.

- Parameter `data`: The data parameter.
- Parameter `cancellationToken`: The cancellationToken parameter.
- Returns: The LoopbackAsync operation result.

###### `M:IoT.Driver.MitsubishiRx.MitsubishiRx.ObserveBits(System.String,System.Int32,System.TimeSpan,System.Nullable`1{System.TimeSpan})`

```csharp
public System.IObservable<IoT.Driver.MitsubishiRx.Responce<bool[]>> ObserveBits(string address, int points, System.TimeSpan pollInterval, System.Nullable<System.TimeSpan> minimumUpdateSpacing)
```
Executes the `ObserveBits` operation.

- Parameter `address`: The `address` value.
- Parameter `points`: The `points` value.
- Parameter `pollInterval`: The `pollInterval` value.
- Parameter `minimumUpdateSpacing`: The `minimumUpdateSpacing` value.
- Returns: A `System.IObservable<IoT.Driver.MitsubishiRx.Responce<bool[]>>` result.

###### `M:IoT.Driver.MitsubishiRx.MitsubishiRx.ObserveConnectionHealth(System.TimeSpan)`

```csharp
public System.IObservable<ReactiveUI.Primitives.Extensions.Stale<IoT.Driver.MitsubishiRx.MitsubishiConnectionState>> ObserveConnectionHealth(System.TimeSpan staleAfter)
```
Executes the ObserveConnectionHealth operation.

- Parameter `staleAfter`: The staleAfter parameter.
- Returns: The ObserveConnectionHealth operation result.

###### `M:IoT.Driver.MitsubishiRx.MitsubishiRx.ObserveReactiveTagGroup(System.String,System.TimeSpan,System.Nullable`1{System.TimeSpan})`

```csharp
public System.IObservable<IoT.Driver.MitsubishiRx.MitsubishiReactiveValue<IoT.Driver.MitsubishiRx.MitsubishiTagGroupSnapshot>> ObserveReactiveTagGroup(string groupName, System.TimeSpan pollInterval, System.Nullable<System.TimeSpan> minimumUpdateSpacing)
```
Executes the `ObserveReactiveTagGroup` operation.

- Parameter `groupName`: The `groupName` value.
- Parameter `pollInterval`: The `pollInterval` value.
- Parameter `minimumUpdateSpacing`: The `minimumUpdateSpacing` value.
- Returns: A `System.IObservable<IoT.Driver.MitsubishiRx.MitsubishiReactiveValue<IoT.Driver.MitsubishiRx.MitsubishiTagGroupSnapshot>>` result.

###### `M:IoT.Driver.MitsubishiRx.MitsubishiRx.ObserveReactiveTag``1(IoT.Driver.Core.LogicalTagKey`1{``0},System.TimeSpan,System.Nullable`1{System.TimeSpan})`

```csharp
public System.IObservable<IoT.Driver.MitsubishiRx.MitsubishiReactiveValue<T>> ObserveReactiveTag<T>(IoT.Driver.Core.LogicalTagKey<T> tagKey, System.TimeSpan pollInterval, System.Nullable<System.TimeSpan> minimumUpdateSpacing)
```
Executes the `ObserveReactiveTag` operation.

- Parameter `tagKey`: The `tagKey` value.
- Parameter `pollInterval`: The `pollInterval` value.
- Parameter `minimumUpdateSpacing`: The `minimumUpdateSpacing` value.
- Returns: A `System.IObservable<IoT.Driver.MitsubishiRx.MitsubishiReactiveValue<T>>` result.

###### `M:IoT.Driver.MitsubishiRx.MitsubishiRx.ObserveReactiveWords(System.String,System.Int32,System.TimeSpan,System.Nullable`1{System.TimeSpan})`

```csharp
public System.IObservable<IoT.Driver.MitsubishiRx.MitsubishiReactiveValue<ushort[]>> ObserveReactiveWords(string address, int points, System.TimeSpan pollInterval, System.Nullable<System.TimeSpan> minimumUpdateSpacing)
```
Executes the `ObserveReactiveWords` operation.

- Parameter `address`: The `address` value.
- Parameter `points`: The `points` value.
- Parameter `pollInterval`: The `pollInterval` value.
- Parameter `minimumUpdateSpacing`: The `minimumUpdateSpacing` value.
- Returns: A `System.IObservable<IoT.Driver.MitsubishiRx.MitsubishiReactiveValue<ushort[]>>` result.

###### `M:IoT.Driver.MitsubishiRx.MitsubishiRx.ObserveTagDatabaseDiff(System.String,System.TimeSpan,System.Boolean)`

```csharp
public System.IObservable<IoT.Driver.MitsubishiRx.Responce<IoT.Driver.MitsubishiRx.MitsubishiTagDatabaseDiff>> ObserveTagDatabaseDiff(string path, System.TimeSpan pollInterval, bool emitInitial)
```
Executes the ObserveTagDatabaseDiff operation.

- Parameter `path`: The path parameter.
- Parameter `pollInterval`: The pollInterval parameter.
- Parameter `emitInitial`: The emitInitial parameter.
- Returns: The ObserveTagDatabaseDiff operation result.

###### `M:IoT.Driver.MitsubishiRx.MitsubishiRx.ObserveTagDatabaseDiff(System.String,System.TimeSpan,System.Boolean,IoT.Driver.MitsubishiRx.MitsubishiTagRolloutPolicy)`

```csharp
public System.IObservable<IoT.Driver.MitsubishiRx.Responce<IoT.Driver.MitsubishiRx.MitsubishiTagDatabaseDiff>> ObserveTagDatabaseDiff(string path, System.TimeSpan pollInterval, bool emitInitial, IoT.Driver.MitsubishiRx.MitsubishiTagRolloutPolicy policy)
```
Executes the ObserveTagDatabaseDiff operation.

- Parameter `path`: The path parameter.
- Parameter `pollInterval`: The pollInterval parameter.
- Parameter `emitInitial`: The emitInitial parameter.
- Parameter `policy`: The policy parameter.
- Returns: The ObserveTagDatabaseDiff operation result.

###### `M:IoT.Driver.MitsubishiRx.MitsubishiRx.ObserveTagDatabaseReload(System.String,System.TimeSpan,System.Boolean)`

```csharp
public System.IObservable<IoT.Driver.MitsubishiRx.Responce<IoT.Driver.MitsubishiRx.MitsubishiTagDatabase>> ObserveTagDatabaseReload(string path, System.TimeSpan pollInterval, bool emitInitial)
```
Executes the ObserveTagDatabaseReload operation.

- Parameter `path`: The path parameter.
- Parameter `pollInterval`: The pollInterval parameter.
- Parameter `emitInitial`: The emitInitial parameter.
- Returns: The ObserveTagDatabaseReload operation result.

###### `M:IoT.Driver.MitsubishiRx.MitsubishiRx.ObserveTagDatabaseReload(System.String,System.TimeSpan,System.Boolean,IoT.Driver.MitsubishiRx.MitsubishiTagRolloutPolicy)`

```csharp
public System.IObservable<IoT.Driver.MitsubishiRx.Responce<IoT.Driver.MitsubishiRx.MitsubishiTagDatabase>> ObserveTagDatabaseReload(string path, System.TimeSpan pollInterval, bool emitInitial, IoT.Driver.MitsubishiRx.MitsubishiTagRolloutPolicy policy)
```
Executes the ObserveTagDatabaseReload operation.

- Parameter `path`: The path parameter.
- Parameter `pollInterval`: The pollInterval parameter.
- Parameter `emitInitial`: The emitInitial parameter.
- Parameter `policy`: The policy parameter.
- Returns: The ObserveTagDatabaseReload operation result.

###### `M:IoT.Driver.MitsubishiRx.MitsubishiRx.ObserveTagGroup(System.String,System.TimeSpan,System.Nullable`1{System.TimeSpan})`

```csharp
public System.IObservable<IoT.Driver.MitsubishiRx.Responce<IoT.Driver.MitsubishiRx.MitsubishiTagGroupSnapshot>> ObserveTagGroup(string groupName, System.TimeSpan pollInterval, System.Nullable<System.TimeSpan> minimumUpdateSpacing)
```
Executes the `ObserveTagGroup` operation.

- Parameter `groupName`: The `groupName` value.
- Parameter `pollInterval`: The `pollInterval` value.
- Parameter `minimumUpdateSpacing`: The `minimumUpdateSpacing` value.
- Returns: A `System.IObservable<IoT.Driver.MitsubishiRx.Responce<IoT.Driver.MitsubishiRx.MitsubishiTagGroupSnapshot>>` result.

###### `M:IoT.Driver.MitsubishiRx.MitsubishiRx.ObserveTagGroupHeartbeat(System.String,System.TimeSpan,System.TimeSpan,System.Nullable`1{System.TimeSpan})`

```csharp
public System.IObservable<ReactiveUI.Primitives.Extensions.Heartbeat<IoT.Driver.MitsubishiRx.Responce<IoT.Driver.MitsubishiRx.MitsubishiTagGroupSnapshot>>> ObserveTagGroupHeartbeat(string groupName, System.TimeSpan pollInterval, System.TimeSpan heartbeatAfter, System.Nullable<System.TimeSpan> minimumUpdateSpacing)
```
Executes the `ObserveTagGroupHeartbeat` operation.

- Parameter `groupName`: The `groupName` value.
- Parameter `pollInterval`: The `pollInterval` value.
- Parameter `heartbeatAfter`: The `heartbeatAfter` value.
- Parameter `minimumUpdateSpacing`: The `minimumUpdateSpacing` value.
- Returns: A `System.IObservable<ReactiveUI.Primitives.Extensions.Heartbeat<IoT.Driver.MitsubishiRx.Responce<IoT.Driver.MitsubishiRx.MitsubishiTagGroupSnapshot>>>` result.

###### `M:IoT.Driver.MitsubishiRx.MitsubishiRx.ObserveTagGroupLatest(System.String,System.IObservable`1{ReactiveUI.Primitives.RxVoid})`

```csharp
public System.IObservable<IoT.Driver.MitsubishiRx.Responce<IoT.Driver.MitsubishiRx.MitsubishiTagGroupSnapshot>> ObserveTagGroupLatest(string groupName, System.IObservable<ReactiveUI.Primitives.RxVoid> trigger)
```
Executes the `ObserveTagGroupLatest` operation.

- Parameter `groupName`: The `groupName` value.
- Parameter `trigger`: The `trigger` value.
- Returns: A `System.IObservable<IoT.Driver.MitsubishiRx.Responce<IoT.Driver.MitsubishiRx.MitsubishiTagGroupSnapshot>>` result.

###### `M:IoT.Driver.MitsubishiRx.MitsubishiRx.ObserveTagGroupStale(System.String,System.TimeSpan,System.TimeSpan,System.Nullable`1{System.TimeSpan})`

```csharp
public System.IObservable<ReactiveUI.Primitives.Extensions.Stale<IoT.Driver.MitsubishiRx.Responce<IoT.Driver.MitsubishiRx.MitsubishiTagGroupSnapshot>>> ObserveTagGroupStale(string groupName, System.TimeSpan pollInterval, System.TimeSpan staleAfter, System.Nullable<System.TimeSpan> minimumUpdateSpacing)
```
Executes the `ObserveTagGroupStale` operation.

- Parameter `groupName`: The `groupName` value.
- Parameter `pollInterval`: The `pollInterval` value.
- Parameter `staleAfter`: The `staleAfter` value.
- Parameter `minimumUpdateSpacing`: The `minimumUpdateSpacing` value.
- Returns: A `System.IObservable<ReactiveUI.Primitives.Extensions.Stale<IoT.Driver.MitsubishiRx.Responce<IoT.Driver.MitsubishiRx.MitsubishiTagGroupSnapshot>>>` result.

###### `M:IoT.Driver.MitsubishiRx.MitsubishiRx.ObserveWords(System.String,System.Int32,System.TimeSpan,System.Nullable`1{System.TimeSpan},System.Nullable`1{System.TimeSpan})`

```csharp
public System.IObservable<IoT.Driver.MitsubishiRx.Responce<ushort[]>> ObserveWords(string address, int points, System.TimeSpan pollInterval, System.Nullable<System.TimeSpan> minimumUpdateSpacing, System.Nullable<System.TimeSpan> pollTimeout)
```
Executes the `ObserveWords` operation.

- Parameter `address`: The `address` value.
- Parameter `points`: The `points` value.
- Parameter `pollInterval`: The `pollInterval` value.
- Parameter `minimumUpdateSpacing`: The `minimumUpdateSpacing` value.
- Parameter `pollTimeout`: The `pollTimeout` value.
- Returns: A `System.IObservable<IoT.Driver.MitsubishiRx.Responce<ushort[]>>` result.

###### `M:IoT.Driver.MitsubishiRx.MitsubishiRx.ObserveWordsHeartbeat(System.String,System.Int32,System.TimeSpan,System.TimeSpan,System.Nullable`1{System.TimeSpan},System.Nullable`1{System.TimeSpan})`

```csharp
public System.IObservable<ReactiveUI.Primitives.Extensions.Heartbeat<IoT.Driver.MitsubishiRx.Responce<ushort[]>>> ObserveWordsHeartbeat(string address, int points, System.TimeSpan pollInterval, System.TimeSpan heartbeatAfter, System.Nullable<System.TimeSpan> minimumUpdateSpacing, System.Nullable<System.TimeSpan> pollTimeout)
```
Executes the `ObserveWordsHeartbeat` operation.

- Parameter `address`: The `address` value.
- Parameter `points`: The `points` value.
- Parameter `pollInterval`: The `pollInterval` value.
- Parameter `heartbeatAfter`: The `heartbeatAfter` value.
- Parameter `minimumUpdateSpacing`: The `minimumUpdateSpacing` value.
- Parameter `pollTimeout`: The `pollTimeout` value.
- Returns: A `System.IObservable<ReactiveUI.Primitives.Extensions.Heartbeat<IoT.Driver.MitsubishiRx.Responce<ushort[]>>>` result.

###### `M:IoT.Driver.MitsubishiRx.MitsubishiRx.ObserveWordsLatest(System.String,System.Int32,System.IObservable`1{ReactiveUI.Primitives.RxVoid})`

```csharp
public System.IObservable<IoT.Driver.MitsubishiRx.Responce<ushort[]>> ObserveWordsLatest(string address, int points, System.IObservable<ReactiveUI.Primitives.RxVoid> trigger)
```
Executes the `ObserveWordsLatest` operation.

- Parameter `address`: The `address` value.
- Parameter `points`: The `points` value.
- Parameter `trigger`: The `trigger` value.
- Returns: A `System.IObservable<IoT.Driver.MitsubishiRx.Responce<ushort[]>>` result.

###### `M:IoT.Driver.MitsubishiRx.MitsubishiRx.ObserveWordsStale(System.String,System.Int32,System.TimeSpan,System.TimeSpan,System.Nullable`1{System.TimeSpan})`

```csharp
public System.IObservable<ReactiveUI.Primitives.Extensions.Stale<IoT.Driver.MitsubishiRx.Responce<ushort[]>>> ObserveWordsStale(string address, int points, System.TimeSpan pollInterval, System.TimeSpan staleAfter, System.Nullable<System.TimeSpan> minimumUpdateSpacing)
```
Executes the `ObserveWordsStale` operation.

- Parameter `address`: The `address` value.
- Parameter `points`: The `points` value.
- Parameter `pollInterval`: The `pollInterval` value.
- Parameter `staleAfter`: The `staleAfter` value.
- Parameter `minimumUpdateSpacing`: The `minimumUpdateSpacing` value.
- Returns: A `System.IObservable<ReactiveUI.Primitives.Extensions.Stale<IoT.Driver.MitsubishiRx.Responce<ushort[]>>>` result.

###### `M:IoT.Driver.MitsubishiRx.MitsubishiRx.OpenAsync(System.Threading.CancellationToken)`

```csharp
public System.Threading.Tasks.Task<IoT.Driver.MitsubishiRx.Responce> OpenAsync(System.Threading.CancellationToken cancellationToken)
```
Executes the OpenAsync operation.

- Parameter `cancellationToken`: The cancellationToken parameter.
- Returns: The OpenAsync operation result.

###### `M:IoT.Driver.MitsubishiRx.MitsubishiRx.PreviewTagDatabaseDiff(System.String)`

```csharp
public IoT.Driver.MitsubishiRx.Responce<IoT.Driver.MitsubishiRx.MitsubishiTagDatabaseDiff> PreviewTagDatabaseDiff(string path)
```
Executes the PreviewTagDatabaseDiff operation.

- Parameter `path`: The path parameter.
- Returns: The PreviewTagDatabaseDiff operation result.

###### `M:IoT.Driver.MitsubishiRx.MitsubishiRx.PreviewTagDatabaseDiff(System.String,IoT.Driver.MitsubishiRx.MitsubishiTagRolloutPolicy)`

```csharp
public IoT.Driver.MitsubishiRx.Responce<IoT.Driver.MitsubishiRx.MitsubishiTagDatabaseDiff> PreviewTagDatabaseDiff(string path, IoT.Driver.MitsubishiRx.MitsubishiTagRolloutPolicy policy)
```
Executes the PreviewTagDatabaseDiff operation.

- Parameter `path`: The path parameter.
- Parameter `policy`: The policy parameter.
- Returns: The PreviewTagDatabaseDiff operation result.

###### `M:IoT.Driver.MitsubishiRx.MitsubishiRx.RandomReadWordsAsync(System.Collections.Generic.IEnumerable`1{System.String},System.Threading.CancellationToken)`

```csharp
public System.Threading.Tasks.Task<IoT.Driver.MitsubishiRx.Responce<ushort[]>> RandomReadWordsAsync(System.Collections.Generic.IEnumerable<string> addresses, System.Threading.CancellationToken cancellationToken)
```
Executes the `RandomReadWordsAsync` operation.

- Parameter `addresses`: The `addresses` value.
- Parameter `cancellationToken`: The `cancellationToken` value.
- Returns: A `System.Threading.Tasks.Task<IoT.Driver.MitsubishiRx.Responce<ushort[]>>` result.

###### `M:IoT.Driver.MitsubishiRx.MitsubishiRx.RandomReadWordsByTagAsync(System.Collections.Generic.IEnumerable`1{System.String},System.Threading.CancellationToken)`

```csharp
public System.Threading.Tasks.Task<IoT.Driver.MitsubishiRx.Responce<ushort[]>> RandomReadWordsByTagAsync(System.Collections.Generic.IEnumerable<string> tagNames, System.Threading.CancellationToken cancellationToken)
```
Executes the `RandomReadWordsByTagAsync` operation.

- Parameter `tagNames`: The `tagNames` value.
- Parameter `cancellationToken`: The `cancellationToken` value.
- Returns: A `System.Threading.Tasks.Task<IoT.Driver.MitsubishiRx.Responce<ushort[]>>` result.

###### `M:IoT.Driver.MitsubishiRx.MitsubishiRx.RandomWriteWordsAsync(System.Collections.Generic.IEnumerable`1{System.Collections.Generic.KeyValuePair`2{System.String,System.UInt16}},System.Threading.CancellationToken)`

```csharp
public System.Threading.Tasks.Task<IoT.Driver.MitsubishiRx.Responce> RandomWriteWordsAsync(System.Collections.Generic.IEnumerable<System.Collections.Generic.KeyValuePair<string, ushort>> values, System.Threading.CancellationToken cancellationToken)
```
Executes the `RandomWriteWordsAsync` operation.

- Parameter `values`: The `values` value.
- Parameter `cancellationToken`: The `cancellationToken` value.
- Returns: A `System.Threading.Tasks.Task<IoT.Driver.MitsubishiRx.Responce>` result.

###### `M:IoT.Driver.MitsubishiRx.MitsubishiRx.RandomWriteWordsByTagAsync(System.Collections.Generic.IEnumerable`1{System.Collections.Generic.KeyValuePair`2{System.String,System.UInt16}},System.Threading.CancellationToken)`

```csharp
public System.Threading.Tasks.Task<IoT.Driver.MitsubishiRx.Responce> RandomWriteWordsByTagAsync(System.Collections.Generic.IEnumerable<System.Collections.Generic.KeyValuePair<string, ushort>> values, System.Threading.CancellationToken cancellationToken)
```
Executes the `RandomWriteWordsByTagAsync` operation.

- Parameter `values`: The `values` value.
- Parameter `cancellationToken`: The `cancellationToken` value.
- Returns: A `System.Threading.Tasks.Task<IoT.Driver.MitsubishiRx.Responce>` result.

###### `M:IoT.Driver.MitsubishiRx.MitsubishiRx.ReadBitsAsync(System.String,System.Int32,System.Threading.CancellationToken)`

```csharp
public System.Threading.Tasks.Task<IoT.Driver.MitsubishiRx.Responce<bool[]>> ReadBitsAsync(string address, int points, System.Threading.CancellationToken cancellationToken)
```
Executes the ReadBitsAsync operation.

- Parameter `address`: The address parameter.
- Parameter `points`: The points parameter.
- Parameter `cancellationToken`: The cancellationToken parameter.
- Returns: The ReadBitsAsync operation result.

###### `M:IoT.Driver.MitsubishiRx.MitsubishiRx.ReadBitsByTagAsync(System.String,System.Int32,System.Threading.CancellationToken)`

```csharp
public System.Threading.Tasks.Task<IoT.Driver.MitsubishiRx.Responce<bool[]>> ReadBitsByTagAsync(string tagName, int points, System.Threading.CancellationToken cancellationToken)
```
Executes the ReadBitsByTagAsync operation.

- Parameter `tagName`: The tagName parameter.
- Parameter `points`: The points parameter.
- Parameter `cancellationToken`: The cancellationToken parameter.
- Returns: The ReadBitsByTagAsync operation result.

###### `M:IoT.Driver.MitsubishiRx.MitsubishiRx.ReadBlocksAsync(IoT.Driver.MitsubishiRx.MitsubishiBlockRequest,System.Threading.CancellationToken)`

```csharp
public System.Threading.Tasks.Task<IoT.Driver.MitsubishiRx.Responce<byte[]>> ReadBlocksAsync(IoT.Driver.MitsubishiRx.MitsubishiBlockRequest request, System.Threading.CancellationToken cancellationToken)
```
Executes the ReadBlocksAsync operation.

- Parameter `request`: The request parameter.
- Parameter `cancellationToken`: The cancellationToken parameter.
- Returns: The ReadBlocksAsync operation result.

###### `M:IoT.Driver.MitsubishiRx.MitsubishiRx.ReadDWordByTagAsync(System.String,System.Threading.CancellationToken)`

```csharp
public System.Threading.Tasks.Task<IoT.Driver.MitsubishiRx.Responce<uint>> ReadDWordByTagAsync(string tagName, System.Threading.CancellationToken cancellationToken)
```
Executes the ReadDWordByTagAsync operation.

- Parameter `tagName`: The tagName parameter.
- Parameter `cancellationToken`: The cancellationToken parameter.
- Returns: The ReadDWordByTagAsync operation result.

###### `M:IoT.Driver.MitsubishiRx.MitsubishiRx.ReadFloatByTagAsync(System.String,System.Threading.CancellationToken)`

```csharp
public System.Threading.Tasks.Task<IoT.Driver.MitsubishiRx.Responce<float>> ReadFloatByTagAsync(string tagName, System.Threading.CancellationToken cancellationToken)
```
Executes the ReadFloatByTagAsync operation.

- Parameter `tagName`: The tagName parameter.
- Parameter `cancellationToken`: The cancellationToken parameter.
- Returns: The ReadFloatByTagAsync operation result.

###### `M:IoT.Driver.MitsubishiRx.MitsubishiRx.ReadGeneratedBitTagAsync(System.String,System.Threading.CancellationToken)`

```csharp
public System.Threading.Tasks.Task<IoT.Driver.MitsubishiRx.Responce<bool>> ReadGeneratedBitTagAsync(string tagName, System.Threading.CancellationToken cancellationToken)
```
Executes the ReadGeneratedBitTagAsync operation.

- Parameter `tagName`: The tagName parameter.
- Parameter `cancellationToken`: The cancellationToken parameter.
- Returns: The ReadGeneratedBitTagAsync operation result.

###### `M:IoT.Driver.MitsubishiRx.MitsubishiRx.ReadInt16ByTagAsync(System.String,System.Threading.CancellationToken)`

```csharp
public System.Threading.Tasks.Task<IoT.Driver.MitsubishiRx.Responce<short>> ReadInt16ByTagAsync(string tagName, System.Threading.CancellationToken cancellationToken)
```
Executes the ReadInt16ByTagAsync operation.

- Parameter `tagName`: The tagName parameter.
- Parameter `cancellationToken`: The cancellationToken parameter.
- Returns: The ReadInt16ByTagAsync operation result.

###### `M:IoT.Driver.MitsubishiRx.MitsubishiRx.ReadInt32ByTagAsync(System.String,System.Threading.CancellationToken)`

```csharp
public System.Threading.Tasks.Task<IoT.Driver.MitsubishiRx.Responce<int>> ReadInt32ByTagAsync(string tagName, System.Threading.CancellationToken cancellationToken)
```
Executes the ReadInt32ByTagAsync operation.

- Parameter `tagName`: The tagName parameter.
- Parameter `cancellationToken`: The cancellationToken parameter.
- Returns: The ReadInt32ByTagAsync operation result.

###### `M:IoT.Driver.MitsubishiRx.MitsubishiRx.ReadMemoryAsync(System.UInt16,System.UInt16,System.Int32,System.Threading.CancellationToken)`

```csharp
public System.Threading.Tasks.Task<IoT.Driver.MitsubishiRx.Responce<ushort[]>> ReadMemoryAsync(ushort command, ushort address, int length, System.Threading.CancellationToken cancellationToken)
```
Executes the ReadMemoryAsync operation.

- Parameter `command`: The command parameter.
- Parameter `address`: The address parameter.
- Parameter `length`: The length parameter.
- Parameter `cancellationToken`: The cancellationToken parameter.
- Returns: The ReadMemoryAsync operation result.

###### `M:IoT.Driver.MitsubishiRx.MitsubishiRx.ReadScaledDoubleByTagAsync(System.String,System.Threading.CancellationToken)`

```csharp
public System.Threading.Tasks.Task<IoT.Driver.MitsubishiRx.Responce<double>> ReadScaledDoubleByTagAsync(string tagName, System.Threading.CancellationToken cancellationToken)
```
Executes the ReadScaledDoubleByTagAsync operation.

- Parameter `tagName`: The tagName parameter.
- Parameter `cancellationToken`: The cancellationToken parameter.
- Returns: The ReadScaledDoubleByTagAsync operation result.

###### `M:IoT.Driver.MitsubishiRx.MitsubishiRx.ReadStringByTagAsync(System.String,System.Int32,System.Threading.CancellationToken)`

```csharp
public System.Threading.Tasks.Task<IoT.Driver.MitsubishiRx.Responce<string>> ReadStringByTagAsync(string tagName, int wordLength, System.Threading.CancellationToken cancellationToken)
```
Executes the ReadStringByTagAsync operation.

- Parameter `tagName`: The tagName parameter.
- Parameter `wordLength`: The wordLength parameter.
- Parameter `cancellationToken`: The cancellationToken parameter.
- Returns: The ReadStringByTagAsync operation result.

###### `M:IoT.Driver.MitsubishiRx.MitsubishiRx.ReadStringByTagAsync(System.String,System.Threading.CancellationToken)`

```csharp
public System.Threading.Tasks.Task<IoT.Driver.MitsubishiRx.Responce<string>> ReadStringByTagAsync(string tagName, System.Threading.CancellationToken cancellationToken)
```
Executes the ReadStringByTagAsync operation.

- Parameter `tagName`: The tagName parameter.
- Parameter `cancellationToken`: The cancellationToken parameter.
- Returns: The ReadStringByTagAsync operation result.

###### `M:IoT.Driver.MitsubishiRx.MitsubishiRx.ReadTagAsync(System.String,System.Threading.CancellationToken)`

```csharp
public System.Threading.Tasks.Task<IoT.Driver.MitsubishiRx.Responce<object>> ReadTagAsync(string tagName, System.Threading.CancellationToken cancellationToken)
```
Reads a tag using the data type declared in `P:IoT.Driver.MitsubishiRx.MitsubishiRx.TagDatabase` .

- Parameter `tagName`: The logical tag name.
- Parameter `cancellationToken`: The cancellation token.
- Returns: The untyped tag value response.

###### `M:IoT.Driver.MitsubishiRx.MitsubishiRx.ReadTagGroupSnapshotAsync(System.String,System.Threading.CancellationToken)`

```csharp
public System.Threading.Tasks.Task<IoT.Driver.MitsubishiRx.Responce<IoT.Driver.MitsubishiRx.MitsubishiTagGroupSnapshot>> ReadTagGroupSnapshotAsync(string groupName, System.Threading.CancellationToken cancellationToken)
```
Executes the ReadTagGroupSnapshotAsync operation.

- Parameter `groupName`: The groupName parameter.
- Parameter `cancellationToken`: The cancellationToken parameter.
- Returns: The ReadTagGroupSnapshotAsync operation result.

###### `M:IoT.Driver.MitsubishiRx.MitsubishiRx.ReadTypeNameAsync(System.Threading.CancellationToken)`

```csharp
public System.Threading.Tasks.Task<IoT.Driver.MitsubishiRx.Responce<IoT.Driver.MitsubishiRx.MitsubishiTypeName>> ReadTypeNameAsync(System.Threading.CancellationToken cancellationToken)
```
Executes the ReadTypeNameAsync operation.

- Parameter `cancellationToken`: The cancellationToken parameter.
- Returns: The ReadTypeNameAsync operation result.

###### `M:IoT.Driver.MitsubishiRx.MitsubishiRx.ReadUInt16ByTagAsync(System.String,System.Threading.CancellationToken)`

```csharp
public System.Threading.Tasks.Task<IoT.Driver.MitsubishiRx.Responce<ushort>> ReadUInt16ByTagAsync(string tagName, System.Threading.CancellationToken cancellationToken)
```
Executes the ReadUInt16ByTagAsync operation.

- Parameter `tagName`: The tagName parameter.
- Parameter `cancellationToken`: The cancellationToken parameter.
- Returns: The ReadUInt16ByTagAsync operation result.

###### `M:IoT.Driver.MitsubishiRx.MitsubishiRx.ReadWordsAsync(System.String,System.Int32,System.Threading.CancellationToken)`

```csharp
public System.Threading.Tasks.Task<IoT.Driver.MitsubishiRx.Responce<ushort[]>> ReadWordsAsync(string address, int points, System.Threading.CancellationToken cancellationToken)
```
Executes the ReadWordsAsync operation.

- Parameter `address`: The address parameter.
- Parameter `points`: The points parameter.
- Parameter `cancellationToken`: The cancellationToken parameter.
- Returns: The ReadWordsAsync operation result.

###### `M:IoT.Driver.MitsubishiRx.MitsubishiRx.ReadWordsByTagAsync(System.String,System.Int32,System.Threading.CancellationToken)`

```csharp
public System.Threading.Tasks.Task<IoT.Driver.MitsubishiRx.Responce<ushort[]>> ReadWordsByTagAsync(string tagName, int points, System.Threading.CancellationToken cancellationToken)
```
Executes the ReadWordsByTagAsync operation.

- Parameter `tagName`: The tagName parameter.
- Parameter `points`: The points parameter.
- Parameter `cancellationToken`: The cancellationToken parameter.
- Returns: The ReadWordsByTagAsync operation result.

###### `M:IoT.Driver.MitsubishiRx.MitsubishiRx.RegisterMonitorAsync(System.Collections.Generic.IEnumerable`1{System.String},System.Threading.CancellationToken)`

```csharp
public System.Threading.Tasks.Task<IoT.Driver.MitsubishiRx.Responce> RegisterMonitorAsync(System.Collections.Generic.IEnumerable<string> addresses, System.Threading.CancellationToken cancellationToken)
```
Executes the `RegisterMonitorAsync` operation.

- Parameter `addresses`: The `addresses` value.
- Parameter `cancellationToken`: The `cancellationToken` value.
- Returns: A `System.Threading.Tasks.Task<IoT.Driver.MitsubishiRx.Responce>` result.

###### `M:IoT.Driver.MitsubishiRx.MitsubishiRx.RemoteLatchClearAsync(System.Threading.CancellationToken)`

```csharp
public System.Threading.Tasks.Task<IoT.Driver.MitsubishiRx.Responce> RemoteLatchClearAsync(System.Threading.CancellationToken cancellationToken)
```
Executes the RemoteLatchClearAsync operation.

- Parameter `cancellationToken`: The cancellationToken parameter.
- Returns: The RemoteLatchClearAsync operation result.

###### `M:IoT.Driver.MitsubishiRx.MitsubishiRx.RemotePauseAsync(System.Threading.CancellationToken)`

```csharp
public System.Threading.Tasks.Task<IoT.Driver.MitsubishiRx.Responce> RemotePauseAsync(System.Threading.CancellationToken cancellationToken)
```
Executes the RemotePauseAsync operation.

- Parameter `cancellationToken`: The cancellationToken parameter.
- Returns: The RemotePauseAsync operation result.

###### `M:IoT.Driver.MitsubishiRx.MitsubishiRx.RemoteResetAsync(System.Threading.CancellationToken)`

```csharp
public System.Threading.Tasks.Task<IoT.Driver.MitsubishiRx.Responce> RemoteResetAsync(System.Threading.CancellationToken cancellationToken)
```
Executes the RemoteResetAsync operation.

- Parameter `cancellationToken`: The cancellationToken parameter.
- Returns: The RemoteResetAsync operation result.

###### `M:IoT.Driver.MitsubishiRx.MitsubishiRx.RemoteRunAsync(System.Boolean,System.Boolean,System.Threading.CancellationToken)`

```csharp
public System.Threading.Tasks.Task<IoT.Driver.MitsubishiRx.Responce> RemoteRunAsync(bool force, bool clearMode, System.Threading.CancellationToken cancellationToken)
```
Executes the RemoteRunAsync operation.

- Parameter `force`: The force parameter.
- Parameter `clearMode`: The clearMode parameter.
- Parameter `cancellationToken`: The cancellationToken parameter.
- Returns: The RemoteRunAsync operation result.

###### `M:IoT.Driver.MitsubishiRx.MitsubishiRx.RemoteStopAsync(System.Threading.CancellationToken)`

```csharp
public System.Threading.Tasks.Task<IoT.Driver.MitsubishiRx.Responce> RemoteStopAsync(System.Threading.CancellationToken cancellationToken)
```
Executes the RemoteStopAsync operation.

- Parameter `cancellationToken`: The cancellationToken parameter.
- Returns: The RemoteStopAsync operation result.

###### `M:IoT.Driver.MitsubishiRx.MitsubishiRx.SampleDiagnostics(System.IObservable`1{System.Object})`

```csharp
public System.IObservable<IoT.Driver.MitsubishiRx.MitsubishiOperationLog> SampleDiagnostics(System.IObservable<object> trigger)
```
Executes the `SampleDiagnostics` operation.

- Parameter `trigger`: The `trigger` value.
- Returns: A `System.IObservable<IoT.Driver.MitsubishiRx.MitsubishiOperationLog>` result.

###### `M:IoT.Driver.MitsubishiRx.MitsubishiRx.SendPackageAsync(System.Byte[],System.Int32,System.Threading.CancellationToken)`

```csharp
public System.Threading.Tasks.Task<IoT.Driver.MitsubishiRx.Responce<byte[]>> SendPackageAsync(byte[] command, int receiveCount, System.Threading.CancellationToken cancellationToken = default)
```
Asynchronously sends a pre-encoded package with a fixed response length.

- Parameter `command`: The command parameter.
- Parameter `receiveCount`: The receiveCount parameter.
- Parameter `cancellationToken`: The cancellation token.
- Returns: The SendPackageAsync operation result.

###### `M:IoT.Driver.MitsubishiRx.MitsubishiRx.SendPackageReliableAsync(System.Byte[],System.Threading.CancellationToken)`

```csharp
public System.Threading.Tasks.Task<IoT.Driver.MitsubishiRx.Responce<byte[]>> SendPackageReliableAsync(byte[] command, System.Threading.CancellationToken cancellationToken = default)
```
Asynchronously sends a pre-encoded package using the reliable package route.

- Parameter `command`: The command parameter.
- Parameter `cancellationToken`: The cancellation token.
- Returns: The SendPackageReliableAsync operation result.

###### `M:IoT.Driver.MitsubishiRx.MitsubishiRx.SendPackageSingleAsync(System.Byte[],System.Threading.CancellationToken)`

```csharp
public System.Threading.Tasks.Task<IoT.Driver.MitsubishiRx.Responce<byte[]>> SendPackageSingleAsync(byte[] command, System.Threading.CancellationToken cancellationToken = default)
```
Asynchronously sends a pre-encoded package with a variable-length response.

- Parameter `command`: The command parameter.
- Parameter `cancellationToken`: The cancellation token.
- Returns: The SendPackageSingleAsync operation result.

###### `M:IoT.Driver.MitsubishiRx.MitsubishiRx.UnlockAsync(System.String,System.Threading.CancellationToken)`

```csharp
public System.Threading.Tasks.Task<IoT.Driver.MitsubishiRx.Responce> UnlockAsync(string password, System.Threading.CancellationToken cancellationToken)
```
Executes the UnlockAsync operation.

- Parameter `password`: The password parameter.
- Parameter `cancellationToken`: The cancellationToken parameter.
- Returns: The UnlockAsync operation result.

###### `M:IoT.Driver.MitsubishiRx.MitsubishiRx.ValidateTagDatabase`

```csharp
public IoT.Driver.MitsubishiRx.Responce ValidateTagDatabase()
```
Executes the ValidateTagDatabase operation.

- Returns: The ValidateTagDatabase operation result.

###### `M:IoT.Driver.MitsubishiRx.MitsubishiRx.ValidateTagGroupWrite(System.String,System.Collections.Generic.IReadOnlyDictionary`2{System.String,System.Object})`

```csharp
public IoT.Driver.MitsubishiRx.Responce ValidateTagGroupWrite(string groupName, System.Collections.Generic.IReadOnlyDictionary<string, object> values)
```
Executes the `ValidateTagGroupWrite` operation.

- Parameter `groupName`: The `groupName` value.
- Parameter `values`: The `values` value.
- Returns: A `IoT.Driver.MitsubishiRx.Responce` result.

###### `M:IoT.Driver.MitsubishiRx.MitsubishiRx.WriteBitsAsync(System.String,System.Collections.Generic.IReadOnlyList`1{System.Boolean},System.Threading.CancellationToken)`

```csharp
public System.Threading.Tasks.Task<IoT.Driver.MitsubishiRx.Responce> WriteBitsAsync(string address, System.Collections.Generic.IReadOnlyList<bool> values, System.Threading.CancellationToken cancellationToken)
```
Executes the `WriteBitsAsync` operation.

- Parameter `address`: The `address` value.
- Parameter `values`: The `values` value.
- Parameter `cancellationToken`: The `cancellationToken` value.
- Returns: A `System.Threading.Tasks.Task<IoT.Driver.MitsubishiRx.Responce>` result.

###### `M:IoT.Driver.MitsubishiRx.MitsubishiRx.WriteBitsByTagAsync(System.String,System.Collections.Generic.IReadOnlyList`1{System.Boolean},System.Threading.CancellationToken)`

```csharp
public System.Threading.Tasks.Task<IoT.Driver.MitsubishiRx.Responce> WriteBitsByTagAsync(string tagName, System.Collections.Generic.IReadOnlyList<bool> values, System.Threading.CancellationToken cancellationToken)
```
Executes the `WriteBitsByTagAsync` operation.

- Parameter `tagName`: The `tagName` value.
- Parameter `values`: The `values` value.
- Parameter `cancellationToken`: The `cancellationToken` value.
- Returns: A `System.Threading.Tasks.Task<IoT.Driver.MitsubishiRx.Responce>` result.

###### `M:IoT.Driver.MitsubishiRx.MitsubishiRx.WriteBlocksAsync(IoT.Driver.MitsubishiRx.MitsubishiBlockRequest,System.Threading.CancellationToken)`

```csharp
public System.Threading.Tasks.Task<IoT.Driver.MitsubishiRx.Responce> WriteBlocksAsync(IoT.Driver.MitsubishiRx.MitsubishiBlockRequest request, System.Threading.CancellationToken cancellationToken)
```
Executes the WriteBlocksAsync operation.

- Parameter `request`: The request parameter.
- Parameter `cancellationToken`: The cancellationToken parameter.
- Returns: The WriteBlocksAsync operation result.

###### `M:IoT.Driver.MitsubishiRx.MitsubishiRx.WriteDWordByTagAsync(System.String,System.UInt32,System.Threading.CancellationToken)`

```csharp
public System.Threading.Tasks.Task<IoT.Driver.MitsubishiRx.Responce> WriteDWordByTagAsync(string tagName, uint value, System.Threading.CancellationToken cancellationToken)
```
Executes the WriteDWordByTagAsync operation.

- Parameter `tagName`: The tagName parameter.
- Parameter `value`: The value parameter.
- Parameter `cancellationToken`: The cancellationToken parameter.
- Returns: The WriteDWordByTagAsync operation result.

###### `M:IoT.Driver.MitsubishiRx.MitsubishiRx.WriteFloatByTagAsync(System.String,System.Single,System.Threading.CancellationToken)`

```csharp
public System.Threading.Tasks.Task<IoT.Driver.MitsubishiRx.Responce> WriteFloatByTagAsync(string tagName, float value, System.Threading.CancellationToken cancellationToken)
```
Executes the WriteFloatByTagAsync operation.

- Parameter `tagName`: The tagName parameter.
- Parameter `value`: The value parameter.
- Parameter `cancellationToken`: The cancellationToken parameter.
- Returns: The WriteFloatByTagAsync operation result.

###### `M:IoT.Driver.MitsubishiRx.MitsubishiRx.WriteGeneratedBitTagAsync(System.String,System.Boolean,System.Threading.CancellationToken)`

```csharp
public System.Threading.Tasks.Task<IoT.Driver.MitsubishiRx.Responce> WriteGeneratedBitTagAsync(string tagName, bool value, System.Threading.CancellationToken cancellationToken)
```
Executes the WriteGeneratedBitTagAsync operation.

- Parameter `tagName`: The tagName parameter.
- Parameter `value`: The value parameter.
- Parameter `cancellationToken`: The cancellationToken parameter.
- Returns: The WriteGeneratedBitTagAsync operation result.

###### `M:IoT.Driver.MitsubishiRx.MitsubishiRx.WriteInt16ByTagAsync(System.String,System.Int16,System.Threading.CancellationToken)`

```csharp
public System.Threading.Tasks.Task<IoT.Driver.MitsubishiRx.Responce> WriteInt16ByTagAsync(string tagName, short value, System.Threading.CancellationToken cancellationToken)
```
Executes the WriteInt16ByTagAsync operation.

- Parameter `tagName`: The tagName parameter.
- Parameter `value`: The value parameter.
- Parameter `cancellationToken`: The cancellationToken parameter.
- Returns: The WriteInt16ByTagAsync operation result.

###### `M:IoT.Driver.MitsubishiRx.MitsubishiRx.WriteInt32ByTagAsync(System.String,System.Int32,System.Threading.CancellationToken)`

```csharp
public System.Threading.Tasks.Task<IoT.Driver.MitsubishiRx.Responce> WriteInt32ByTagAsync(string tagName, int value, System.Threading.CancellationToken cancellationToken)
```
Executes the WriteInt32ByTagAsync operation.

- Parameter `tagName`: The tagName parameter.
- Parameter `value`: The value parameter.
- Parameter `cancellationToken`: The cancellationToken parameter.
- Returns: The WriteInt32ByTagAsync operation result.

###### `M:IoT.Driver.MitsubishiRx.MitsubishiRx.WriteMemoryAsync(System.UInt16,System.UInt16,System.Collections.Generic.IReadOnlyList`1{System.UInt16},System.Threading.CancellationToken)`

```csharp
public System.Threading.Tasks.Task<IoT.Driver.MitsubishiRx.Responce> WriteMemoryAsync(ushort command, ushort address, System.Collections.Generic.IReadOnlyList<ushort> values, System.Threading.CancellationToken cancellationToken)
```
Executes the `WriteMemoryAsync` operation.

- Parameter `command`: The `command` value.
- Parameter `address`: The `address` value.
- Parameter `values`: The `values` value.
- Parameter `cancellationToken`: The `cancellationToken` value.
- Returns: A `System.Threading.Tasks.Task<IoT.Driver.MitsubishiRx.Responce>` result.

###### `M:IoT.Driver.MitsubishiRx.MitsubishiRx.WriteScaledDoubleByTagAsync(System.String,System.Double,System.Threading.CancellationToken)`

```csharp
public System.Threading.Tasks.Task<IoT.Driver.MitsubishiRx.Responce> WriteScaledDoubleByTagAsync(string tagName, double value, System.Threading.CancellationToken cancellationToken)
```
Executes the WriteScaledDoubleByTagAsync operation.

- Parameter `tagName`: The tagName parameter.
- Parameter `value`: The value parameter.
- Parameter `cancellationToken`: The cancellationToken parameter.
- Returns: The WriteScaledDoubleByTagAsync operation result.

###### `M:IoT.Driver.MitsubishiRx.MitsubishiRx.WriteStringByTagAsync(System.String,System.String,System.Int32,System.Threading.CancellationToken)`

```csharp
public System.Threading.Tasks.Task<IoT.Driver.MitsubishiRx.Responce> WriteStringByTagAsync(string tagName, string value, int wordLength, System.Threading.CancellationToken cancellationToken)
```
Executes the WriteStringByTagAsync operation.

- Parameter `tagName`: The tagName parameter.
- Parameter `value`: The value parameter.
- Parameter `wordLength`: The wordLength parameter.
- Parameter `cancellationToken`: The cancellationToken parameter.
- Returns: The WriteStringByTagAsync operation result.

###### `M:IoT.Driver.MitsubishiRx.MitsubishiRx.WriteStringByTagAsync(System.String,System.String,System.Threading.CancellationToken)`

```csharp
public System.Threading.Tasks.Task<IoT.Driver.MitsubishiRx.Responce> WriteStringByTagAsync(string tagName, string value, System.Threading.CancellationToken cancellationToken)
```
Executes the WriteStringByTagAsync operation.

- Parameter `tagName`: The tagName parameter.
- Parameter `value`: The value parameter.
- Parameter `cancellationToken`: The cancellationToken parameter.
- Returns: The WriteStringByTagAsync operation result.

###### `M:IoT.Driver.MitsubishiRx.MitsubishiRx.WriteTagAsync(System.String,System.Object,System.Threading.CancellationToken)`

```csharp
public System.Threading.Tasks.Task<IoT.Driver.MitsubishiRx.Responce> WriteTagAsync(string tagName, object value, System.Threading.CancellationToken cancellationToken)
```
Writes a tag using the data type declared in `P:IoT.Driver.MitsubishiRx.MitsubishiRx.TagDatabase` .

- Parameter `tagName`: The logical tag name.
- Parameter `value`: The value to write.
- Parameter `cancellationToken`: The cancellation token.
- Returns: The write response.

###### `M:IoT.Driver.MitsubishiRx.MitsubishiRx.WriteTagGroupSnapshotAsync(IoT.Driver.MitsubishiRx.MitsubishiTagGroupSnapshot,System.Threading.CancellationToken)`

```csharp
public System.Threading.Tasks.Task<IoT.Driver.MitsubishiRx.Responce> WriteTagGroupSnapshotAsync(IoT.Driver.MitsubishiRx.MitsubishiTagGroupSnapshot snapshot, System.Threading.CancellationToken cancellationToken)
```
Executes the WriteTagGroupSnapshotAsync operation.

- Parameter `snapshot`: The snapshot parameter.
- Parameter `cancellationToken`: The cancellationToken parameter.
- Returns: The WriteTagGroupSnapshotAsync operation result.

###### `M:IoT.Driver.MitsubishiRx.MitsubishiRx.WriteTagGroupValuesAsync(System.String,System.Collections.Generic.IReadOnlyDictionary`2{System.String,System.Object},System.Threading.CancellationToken)`

```csharp
public System.Threading.Tasks.Task<IoT.Driver.MitsubishiRx.Responce> WriteTagGroupValuesAsync(string groupName, System.Collections.Generic.IReadOnlyDictionary<string, object> values, System.Threading.CancellationToken cancellationToken)
```
Executes the `WriteTagGroupValuesAsync` operation.

- Parameter `groupName`: The `groupName` value.
- Parameter `values`: The `values` value.
- Parameter `cancellationToken`: The `cancellationToken` value.
- Returns: A `System.Threading.Tasks.Task<IoT.Driver.MitsubishiRx.Responce>` result.

###### `M:IoT.Driver.MitsubishiRx.MitsubishiRx.WriteUInt16ByTagAsync(System.String,System.UInt16,System.Threading.CancellationToken)`

```csharp
public System.Threading.Tasks.Task<IoT.Driver.MitsubishiRx.Responce> WriteUInt16ByTagAsync(string tagName, ushort value, System.Threading.CancellationToken cancellationToken)
```
Executes the WriteUInt16ByTagAsync operation.

- Parameter `tagName`: The tagName parameter.
- Parameter `value`: The value parameter.
- Parameter `cancellationToken`: The cancellationToken parameter.
- Returns: The WriteUInt16ByTagAsync operation result.

###### `M:IoT.Driver.MitsubishiRx.MitsubishiRx.WriteWordsAsync(System.String,System.Collections.Generic.IReadOnlyList`1{System.UInt16},System.Threading.CancellationToken)`

```csharp
public System.Threading.Tasks.Task<IoT.Driver.MitsubishiRx.Responce> WriteWordsAsync(string address, System.Collections.Generic.IReadOnlyList<ushort> values, System.Threading.CancellationToken cancellationToken)
```
Executes the `WriteWordsAsync` operation.

- Parameter `address`: The `address` value.
- Parameter `values`: The `values` value.
- Parameter `cancellationToken`: The `cancellationToken` value.
- Returns: A `System.Threading.Tasks.Task<IoT.Driver.MitsubishiRx.Responce>` result.

###### `M:IoT.Driver.MitsubishiRx.MitsubishiRx.WriteWordsByTagAsync(System.String,System.Collections.Generic.IReadOnlyList`1{System.UInt16},System.Threading.CancellationToken)`

```csharp
public System.Threading.Tasks.Task<IoT.Driver.MitsubishiRx.Responce> WriteWordsByTagAsync(string tagName, System.Collections.Generic.IReadOnlyList<ushort> values, System.Threading.CancellationToken cancellationToken)
```
Executes the `WriteWordsByTagAsync` operation.

- Parameter `tagName`: The `tagName` value.
- Parameter `values`: The `values` value.
- Parameter `cancellationToken`: The `cancellationToken` value.
- Returns: A `System.Threading.Tasks.Task<IoT.Driver.MitsubishiRx.Responce>` result.

###### `P:IoT.Driver.MitsubishiRx.MitsubishiRx.Connected`

```csharp
public bool Connected { get; }
```
Gets or sets the Connected property.

- Value: The `Connected` value.

###### `P:IoT.Driver.MitsubishiRx.MitsubishiRx.ConnectionStates`

```csharp
public System.IObservable<IoT.Driver.MitsubishiRx.MitsubishiConnectionState> ConnectionStates { get; }
```
Gets or sets the ConnectionStates property.

- Value: The `ConnectionStates` value.

###### `P:IoT.Driver.MitsubishiRx.MitsubishiRx.OperationLogs`

```csharp
public System.IObservable<IoT.Driver.MitsubishiRx.MitsubishiOperationLog> OperationLogs { get; }
```
Gets or sets the OperationLogs property.

- Value: The `OperationLogs` value.

###### `P:IoT.Driver.MitsubishiRx.MitsubishiRx.Options`

```csharp
public IoT.Driver.MitsubishiRx.MitsubishiClientOptions Options { get; }
```
Gets or sets the Options property.

- Value: The `Options` value.

###### `P:IoT.Driver.MitsubishiRx.MitsubishiRx.TagDatabase`

```csharp
public IoT.Driver.MitsubishiRx.MitsubishiTagDatabase TagDatabase { get; set; }
```
Gets or sets the TagDatabase property.

- Value: The `TagDatabase` value.

#### `T:IoT.Driver.MitsubishiRx.MitsubishiSchemaChangeKind`

```csharp
public enum IoT.Driver.MitsubishiRx.MitsubishiSchemaChangeKind
```
Defines the MitsubishiSchemaChangeKind values.

##### Declared public members

###### `F:IoT.Driver.MitsubishiRx.MitsubishiSchemaChangeKind.AddressChange`

```csharp
public static const IoT.Driver.MitsubishiRx.MitsubishiSchemaChangeKind AddressChange
```
Represents the AddressChange option.

###### `F:IoT.Driver.MitsubishiRx.MitsubishiSchemaChangeKind.DataTypeChange`

```csharp
public static const IoT.Driver.MitsubishiRx.MitsubishiSchemaChangeKind DataTypeChange
```
Represents the DataTypeChange option.

###### `F:IoT.Driver.MitsubishiRx.MitsubishiSchemaChangeKind.GroupMembershipChange`

```csharp
public static const IoT.Driver.MitsubishiRx.MitsubishiSchemaChangeKind GroupMembershipChange
```
Represents the GroupMembershipChange option.

###### `F:IoT.Driver.MitsubishiRx.MitsubishiSchemaChangeKind.MetadataOnly`

```csharp
public static const IoT.Driver.MitsubishiRx.MitsubishiSchemaChangeKind MetadataOnly
```
Represents the MetadataOnly option.

###### `F:IoT.Driver.MitsubishiRx.MitsubishiSchemaChangeKind.None`

```csharp
public static const IoT.Driver.MitsubishiRx.MitsubishiSchemaChangeKind None
```
Represents the None option.

###### `F:IoT.Driver.MitsubishiRx.MitsubishiSchemaChangeKind.StructureChange`

```csharp
public static const IoT.Driver.MitsubishiRx.MitsubishiSchemaChangeKind StructureChange
```
Represents the StructureChange option.

#### `T:IoT.Driver.MitsubishiRx.MitsubishiSerialMessageFormat`

```csharp
public enum IoT.Driver.MitsubishiRx.MitsubishiSerialMessageFormat
```
Defines the MitsubishiSerialMessageFormat values.

##### Declared public members

###### `F:IoT.Driver.MitsubishiRx.MitsubishiSerialMessageFormat.Format1`

```csharp
public static const IoT.Driver.MitsubishiRx.MitsubishiSerialMessageFormat Format1
```
Represents the Format1 option.

###### `F:IoT.Driver.MitsubishiRx.MitsubishiSerialMessageFormat.Format4`

```csharp
public static const IoT.Driver.MitsubishiRx.MitsubishiSerialMessageFormat Format4
```
Represents the Format4 option.

###### `F:IoT.Driver.MitsubishiRx.MitsubishiSerialMessageFormat.Format5`

```csharp
public static const IoT.Driver.MitsubishiRx.MitsubishiSerialMessageFormat Format5
```
Represents the Format5 option.

#### `T:IoT.Driver.MitsubishiRx.MitsubishiSerialOptions`

```csharp
public class IoT.Driver.MitsubishiRx.MitsubishiSerialOptions
```
Provides the MitsubishiSerialOptions record.

##### Declared public members

###### `M:IoT.Driver.MitsubishiRx.MitsubishiSerialOptions.#ctor(System.String,System.Int32,System.Int32,System.IO.Ports.Parity,System.IO.Ports.StopBits,System.IO.Ports.Handshake,IoT.Driver.MitsubishiRx.MitsubishiSerialMessageFormat,System.Byte,System.Byte,System.Byte,System.UInt16,System.Byte,System.Byte,System.Byte,System.Int32,System.Int32,System.String)`

```csharp
public IoT.Driver.MitsubishiRx.MitsubishiSerialOptions(string PortName, int BaudRate, int DataBits, System.IO.Ports.Parity Parity, System.IO.Ports.StopBits StopBits, System.IO.Ports.Handshake Handshake, IoT.Driver.MitsubishiRx.MitsubishiSerialMessageFormat MessageFormat, byte StationNumber, byte NetworkNumber, byte PcNumber, ushort RequestDestinationModuleIoNumber, byte RequestDestinationModuleStationNumber, byte SelfStationNumber, byte MessageWait, int ReadBufferSize, int WriteBufferSize, string NewLine)
```
Provides the MitsubishiSerialOptions record.

- Parameter `PortName`: The PortName parameter.
- Parameter `BaudRate`: The BaudRate parameter.
- Parameter `DataBits`: The DataBits parameter.
- Parameter `Parity`: The Parity parameter.
- Parameter `StopBits`: The StopBits parameter.
- Parameter `Handshake`: The Handshake parameter.
- Parameter `MessageFormat`: The MessageFormat parameter.
- Parameter `StationNumber`: The StationNumber parameter.
- Parameter `NetworkNumber`: The NetworkNumber parameter.
- Parameter `PcNumber`: The PcNumber parameter.
- Parameter `RequestDestinationModuleIoNumber`: The RequestDestinationModuleIoNumber parameter.
- Parameter `RequestDestinationModuleStationNumber`: The RequestDestinationModuleStationNumber parameter.
- Parameter `SelfStationNumber`: The SelfStationNumber parameter.
- Parameter `MessageWait`: The MessageWait parameter.
- Parameter `ReadBufferSize`: The ReadBufferSize parameter.
- Parameter `WriteBufferSize`: The WriteBufferSize parameter.
- Parameter `NewLine`: The NewLine parameter.

###### `M:IoT.Driver.MitsubishiRx.MitsubishiSerialOptions.Deconstruct(System.String@,System.Int32@,System.Int32@,System.IO.Ports.Parity@,System.IO.Ports.StopBits@,System.IO.Ports.Handshake@,IoT.Driver.MitsubishiRx.MitsubishiSerialMessageFormat@,System.Byte@,System.Byte@,System.Byte@,System.UInt16@,System.Byte@,System.Byte@,System.Byte@,System.Int32@,System.Int32@,System.String@)`

```csharp
public void Deconstruct(out string PortName, out int BaudRate, out int DataBits, out System.IO.Ports.Parity Parity, out System.IO.Ports.StopBits StopBits, out System.IO.Ports.Handshake Handshake, out IoT.Driver.MitsubishiRx.MitsubishiSerialMessageFormat MessageFormat, out byte StationNumber, out byte NetworkNumber, out byte PcNumber, out ushort RequestDestinationModuleIoNumber, out byte RequestDestinationModuleStationNumber, out byte SelfStationNumber, out byte MessageWait, out int ReadBufferSize, out int WriteBufferSize, out string NewLine)
```
Deconstructs the value into its component values.

- Parameter `PortName`: The `PortName` value.
- Parameter `BaudRate`: The `BaudRate` value.
- Parameter `DataBits`: The `DataBits` value.
- Parameter `Parity`: The `Parity` value.
- Parameter `StopBits`: The `StopBits` value.
- Parameter `Handshake`: The `Handshake` value.
- Parameter `MessageFormat`: The `MessageFormat` value.
- Parameter `StationNumber`: The `StationNumber` value.
- Parameter `NetworkNumber`: The `NetworkNumber` value.
- Parameter `PcNumber`: The `PcNumber` value.
- Parameter `RequestDestinationModuleIoNumber`: The `RequestDestinationModuleIoNumber` value.
- Parameter `RequestDestinationModuleStationNumber`: The `RequestDestinationModuleStationNumber` value.
- Parameter `SelfStationNumber`: The `SelfStationNumber` value.
- Parameter `MessageWait`: The `MessageWait` value.
- Parameter `ReadBufferSize`: The `ReadBufferSize` value.
- Parameter `WriteBufferSize`: The `WriteBufferSize` value.
- Parameter `NewLine`: The `NewLine` value.

###### `M:IoT.Driver.MitsubishiRx.MitsubishiSerialOptions.Equals(IoT.Driver.MitsubishiRx.MitsubishiSerialOptions)`

```csharp
public bool Equals(IoT.Driver.MitsubishiRx.MitsubishiSerialOptions other)
```
Determines whether the supplied value is equal to the current value.

- Parameter `other`: The `other` value.
- Returns: A `bool` result.

###### `M:IoT.Driver.MitsubishiRx.MitsubishiSerialOptions.Equals(System.Object)`

```csharp
public bool Equals(object obj)
```
Determines whether the supplied value is equal to the current value.

- Parameter `obj`: The `obj` value.
- Returns: A `bool` result.

###### `M:IoT.Driver.MitsubishiRx.MitsubishiSerialOptions.GetHashCode`

```csharp
public int GetHashCode()
```
Returns the hash code for the current value.

- Returns: A `int` result.

###### `M:IoT.Driver.MitsubishiRx.MitsubishiSerialOptions.ToString`

```csharp
public string ToString()
```
Returns a string representation of the current value.

- Returns: A `string` result.

###### `M:IoT.Driver.MitsubishiRx.MitsubishiSerialOptions.op_Equality(IoT.Driver.MitsubishiRx.MitsubishiSerialOptions,IoT.Driver.MitsubishiRx.MitsubishiSerialOptions)`

```csharp
public static bool op_Equality(IoT.Driver.MitsubishiRx.MitsubishiSerialOptions left, IoT.Driver.MitsubishiRx.MitsubishiSerialOptions right)
```
Determines whether the two supplied values are equal.

- Parameter `left`: The `left` value.
- Parameter `right`: The `right` value.
- Returns: A `bool` result.

###### `M:IoT.Driver.MitsubishiRx.MitsubishiSerialOptions.op_Inequality(IoT.Driver.MitsubishiRx.MitsubishiSerialOptions,IoT.Driver.MitsubishiRx.MitsubishiSerialOptions)`

```csharp
public static bool op_Inequality(IoT.Driver.MitsubishiRx.MitsubishiSerialOptions left, IoT.Driver.MitsubishiRx.MitsubishiSerialOptions right)
```
Determines whether the two supplied values are not equal.

- Parameter `left`: The `left` value.
- Parameter `right`: The `right` value.
- Returns: A `bool` result.

###### `P:IoT.Driver.MitsubishiRx.MitsubishiSerialOptions.BaudRate`

```csharp
public int BaudRate { get; set; }
```
The BaudRate parameter.

- Value: The `BaudRate` value.

###### `P:IoT.Driver.MitsubishiRx.MitsubishiSerialOptions.DataBits`

```csharp
public int DataBits { get; set; }
```
The DataBits parameter.

- Value: The `DataBits` value.

###### `P:IoT.Driver.MitsubishiRx.MitsubishiSerialOptions.Handshake`

```csharp
public System.IO.Ports.Handshake Handshake { get; set; }
```
The Handshake parameter.

- Value: The `Handshake` value.

###### `P:IoT.Driver.MitsubishiRx.MitsubishiSerialOptions.MessageFormat`

```csharp
public IoT.Driver.MitsubishiRx.MitsubishiSerialMessageFormat MessageFormat { get; set; }
```
The MessageFormat parameter.

- Value: The `MessageFormat` value.

###### `P:IoT.Driver.MitsubishiRx.MitsubishiSerialOptions.MessageWait`

```csharp
public byte MessageWait { get; set; }
```
The MessageWait parameter.

- Value: The `MessageWait` value.

###### `P:IoT.Driver.MitsubishiRx.MitsubishiSerialOptions.NetworkNumber`

```csharp
public byte NetworkNumber { get; set; }
```
The NetworkNumber parameter.

- Value: The `NetworkNumber` value.

###### `P:IoT.Driver.MitsubishiRx.MitsubishiSerialOptions.NewLine`

```csharp
public string NewLine { get; set; }
```
The NewLine parameter.

- Value: The `NewLine` value.

###### `P:IoT.Driver.MitsubishiRx.MitsubishiSerialOptions.Parity`

```csharp
public System.IO.Ports.Parity Parity { get; set; }
```
The Parity parameter.

- Value: The `Parity` value.

###### `P:IoT.Driver.MitsubishiRx.MitsubishiSerialOptions.PcNumber`

```csharp
public byte PcNumber { get; set; }
```
The PcNumber parameter.

- Value: The `PcNumber` value.

###### `P:IoT.Driver.MitsubishiRx.MitsubishiSerialOptions.PortName`

```csharp
public string PortName { get; set; }
```
The PortName parameter.

- Value: The `PortName` value.

###### `P:IoT.Driver.MitsubishiRx.MitsubishiSerialOptions.ReadBufferSize`

```csharp
public int ReadBufferSize { get; set; }
```
The ReadBufferSize parameter.

- Value: The `ReadBufferSize` value.

###### `P:IoT.Driver.MitsubishiRx.MitsubishiSerialOptions.RequestDestinationModuleIoNumber`

```csharp
public ushort RequestDestinationModuleIoNumber { get; set; }
```
The RequestDestinationModuleIoNumber parameter.

- Value: The `RequestDestinationModuleIoNumber` value.

###### `P:IoT.Driver.MitsubishiRx.MitsubishiSerialOptions.RequestDestinationModuleStationNumber`

```csharp
public byte RequestDestinationModuleStationNumber { get; set; }
```
The RequestDestinationModuleStationNumber parameter.

- Value: The `RequestDestinationModuleStationNumber` value.

###### `P:IoT.Driver.MitsubishiRx.MitsubishiSerialOptions.Route`

```csharp
public IoT.Driver.MitsubishiRx.MitsubishiSerialRoute Route { get; }
```
Gets or sets the Route property.

- Value: The `Route` value.

###### `P:IoT.Driver.MitsubishiRx.MitsubishiSerialOptions.SelfStationNumber`

```csharp
public byte SelfStationNumber { get; set; }
```
The SelfStationNumber parameter.

- Value: The `SelfStationNumber` value.

###### `P:IoT.Driver.MitsubishiRx.MitsubishiSerialOptions.StationNumber`

```csharp
public byte StationNumber { get; set; }
```
The StationNumber parameter.

- Value: The `StationNumber` value.

###### `P:IoT.Driver.MitsubishiRx.MitsubishiSerialOptions.StopBits`

```csharp
public System.IO.Ports.StopBits StopBits { get; set; }
```
The StopBits parameter.

- Value: The `StopBits` value.

###### `P:IoT.Driver.MitsubishiRx.MitsubishiSerialOptions.WriteBufferSize`

```csharp
public int WriteBufferSize { get; set; }
```
The WriteBufferSize parameter.

- Value: The `WriteBufferSize` value.

#### `T:IoT.Driver.MitsubishiRx.MitsubishiSerialRoute`

```csharp
public class IoT.Driver.MitsubishiRx.MitsubishiSerialRoute
```
Provides the MitsubishiSerialRoute record.

##### Declared public members

###### `M:IoT.Driver.MitsubishiRx.MitsubishiSerialRoute.#ctor(System.Byte,System.Byte,System.Byte,System.UInt16,System.Byte,System.Byte)`

```csharp
public IoT.Driver.MitsubishiRx.MitsubishiSerialRoute(byte StationNumber, byte NetworkNumber, byte PcNumber, ushort RequestDestinationModuleIoNumber, byte RequestDestinationModuleStationNumber, byte SelfStationNumber)
```
Provides the MitsubishiSerialRoute record.

- Parameter `StationNumber`: The StationNumber parameter.
- Parameter `NetworkNumber`: The NetworkNumber parameter.
- Parameter `PcNumber`: The PcNumber parameter.
- Parameter `RequestDestinationModuleIoNumber`: The RequestDestinationModuleIoNumber parameter.
- Parameter `RequestDestinationModuleStationNumber`: The RequestDestinationModuleStationNumber parameter.
- Parameter `SelfStationNumber`: The SelfStationNumber parameter.

###### `M:IoT.Driver.MitsubishiRx.MitsubishiSerialRoute.Deconstruct(System.Byte@,System.Byte@,System.Byte@,System.UInt16@,System.Byte@,System.Byte@)`

```csharp
public void Deconstruct(out byte StationNumber, out byte NetworkNumber, out byte PcNumber, out ushort RequestDestinationModuleIoNumber, out byte RequestDestinationModuleStationNumber, out byte SelfStationNumber)
```
Deconstructs the value into its component values.

- Parameter `StationNumber`: The `StationNumber` value.
- Parameter `NetworkNumber`: The `NetworkNumber` value.
- Parameter `PcNumber`: The `PcNumber` value.
- Parameter `RequestDestinationModuleIoNumber`: The `RequestDestinationModuleIoNumber` value.
- Parameter `RequestDestinationModuleStationNumber`: The `RequestDestinationModuleStationNumber` value.
- Parameter `SelfStationNumber`: The `SelfStationNumber` value.

###### `M:IoT.Driver.MitsubishiRx.MitsubishiSerialRoute.Equals(IoT.Driver.MitsubishiRx.MitsubishiSerialRoute)`

```csharp
public bool Equals(IoT.Driver.MitsubishiRx.MitsubishiSerialRoute other)
```
Determines whether the supplied value is equal to the current value.

- Parameter `other`: The `other` value.
- Returns: A `bool` result.

###### `M:IoT.Driver.MitsubishiRx.MitsubishiSerialRoute.Equals(System.Object)`

```csharp
public bool Equals(object obj)
```
Determines whether the supplied value is equal to the current value.

- Parameter `obj`: The `obj` value.
- Returns: A `bool` result.

###### `M:IoT.Driver.MitsubishiRx.MitsubishiSerialRoute.GetHashCode`

```csharp
public int GetHashCode()
```
Returns the hash code for the current value.

- Returns: A `int` result.

###### `M:IoT.Driver.MitsubishiRx.MitsubishiSerialRoute.ToString`

```csharp
public string ToString()
```
Returns a string representation of the current value.

- Returns: A `string` result.

###### `M:IoT.Driver.MitsubishiRx.MitsubishiSerialRoute.op_Equality(IoT.Driver.MitsubishiRx.MitsubishiSerialRoute,IoT.Driver.MitsubishiRx.MitsubishiSerialRoute)`

```csharp
public static bool op_Equality(IoT.Driver.MitsubishiRx.MitsubishiSerialRoute left, IoT.Driver.MitsubishiRx.MitsubishiSerialRoute right)
```
Determines whether the two supplied values are equal.

- Parameter `left`: The `left` value.
- Parameter `right`: The `right` value.
- Returns: A `bool` result.

###### `M:IoT.Driver.MitsubishiRx.MitsubishiSerialRoute.op_Inequality(IoT.Driver.MitsubishiRx.MitsubishiSerialRoute,IoT.Driver.MitsubishiRx.MitsubishiSerialRoute)`

```csharp
public static bool op_Inequality(IoT.Driver.MitsubishiRx.MitsubishiSerialRoute left, IoT.Driver.MitsubishiRx.MitsubishiSerialRoute right)
```
Determines whether the two supplied values are not equal.

- Parameter `left`: The `left` value.
- Parameter `right`: The `right` value.
- Returns: A `bool` result.

###### `P:IoT.Driver.MitsubishiRx.MitsubishiSerialRoute.NetworkNumber`

```csharp
public byte NetworkNumber { get; set; }
```
The NetworkNumber parameter.

- Value: The `NetworkNumber` value.

###### `P:IoT.Driver.MitsubishiRx.MitsubishiSerialRoute.PcNumber`

```csharp
public byte PcNumber { get; set; }
```
The PcNumber parameter.

- Value: The `PcNumber` value.

###### `P:IoT.Driver.MitsubishiRx.MitsubishiSerialRoute.RequestDestinationModuleIoNumber`

```csharp
public ushort RequestDestinationModuleIoNumber { get; set; }
```
The RequestDestinationModuleIoNumber parameter.

- Value: The `RequestDestinationModuleIoNumber` value.

###### `P:IoT.Driver.MitsubishiRx.MitsubishiSerialRoute.RequestDestinationModuleStationNumber`

```csharp
public byte RequestDestinationModuleStationNumber { get; set; }
```
The RequestDestinationModuleStationNumber parameter.

- Value: The `RequestDestinationModuleStationNumber` value.

###### `P:IoT.Driver.MitsubishiRx.MitsubishiSerialRoute.SelfStationNumber`

```csharp
public byte SelfStationNumber { get; set; }
```
The SelfStationNumber parameter.

- Value: The `SelfStationNumber` value.

###### `P:IoT.Driver.MitsubishiRx.MitsubishiSerialRoute.StationNumber`

```csharp
public byte StationNumber { get; set; }
```
The StationNumber parameter.

- Value: The `StationNumber` value.

#### `T:IoT.Driver.MitsubishiRx.MitsubishiSimulatorDeviceValue`

```csharp
public class IoT.Driver.MitsubishiRx.MitsubishiSimulatorDeviceValue
```
Represents one populated value in a Mitsubishi simulator memory snapshot.

##### Declared public members

###### `M:IoT.Driver.MitsubishiRx.MitsubishiSimulatorDeviceValue.#ctor(System.String,System.Int32,IoT.Driver.MitsubishiRx.DeviceValueKind,System.UInt16)`

```csharp
public IoT.Driver.MitsubishiRx.MitsubishiSimulatorDeviceValue(string Symbol, int Number, IoT.Driver.MitsubishiRx.DeviceValueKind Kind, ushort Value)
```
Represents one populated value in a Mitsubishi simulator memory snapshot.

- Parameter `Symbol`: The device symbol.
- Parameter `Number`: The numeric device address.
- Parameter `Kind`: The device value kind.
- Parameter `Value`: The stored raw value.

###### `M:IoT.Driver.MitsubishiRx.MitsubishiSimulatorDeviceValue.Deconstruct(System.String@,System.Int32@,IoT.Driver.MitsubishiRx.DeviceValueKind@,System.UInt16@)`

```csharp
public void Deconstruct(out string Symbol, out int Number, out IoT.Driver.MitsubishiRx.DeviceValueKind Kind, out ushort Value)
```
Deconstructs the value into its component values.

- Parameter `Symbol`: The `Symbol` value.
- Parameter `Number`: The `Number` value.
- Parameter `Kind`: The `Kind` value.
- Parameter `Value`: The `Value` value.

###### `M:IoT.Driver.MitsubishiRx.MitsubishiSimulatorDeviceValue.Equals(IoT.Driver.MitsubishiRx.MitsubishiSimulatorDeviceValue)`

```csharp
public bool Equals(IoT.Driver.MitsubishiRx.MitsubishiSimulatorDeviceValue other)
```
Determines whether the supplied value is equal to the current value.

- Parameter `other`: The `other` value.
- Returns: A `bool` result.

###### `M:IoT.Driver.MitsubishiRx.MitsubishiSimulatorDeviceValue.Equals(System.Object)`

```csharp
public bool Equals(object obj)
```
Determines whether the supplied value is equal to the current value.

- Parameter `obj`: The `obj` value.
- Returns: A `bool` result.

###### `M:IoT.Driver.MitsubishiRx.MitsubishiSimulatorDeviceValue.GetHashCode`

```csharp
public int GetHashCode()
```
Returns the hash code for the current value.

- Returns: A `int` result.

###### `M:IoT.Driver.MitsubishiRx.MitsubishiSimulatorDeviceValue.ToString`

```csharp
public string ToString()
```
Returns a string representation of the current value.

- Returns: A `string` result.

###### `M:IoT.Driver.MitsubishiRx.MitsubishiSimulatorDeviceValue.op_Equality(IoT.Driver.MitsubishiRx.MitsubishiSimulatorDeviceValue,IoT.Driver.MitsubishiRx.MitsubishiSimulatorDeviceValue)`

```csharp
public static bool op_Equality(IoT.Driver.MitsubishiRx.MitsubishiSimulatorDeviceValue left, IoT.Driver.MitsubishiRx.MitsubishiSimulatorDeviceValue right)
```
Determines whether the two supplied values are equal.

- Parameter `left`: The `left` value.
- Parameter `right`: The `right` value.
- Returns: A `bool` result.

###### `M:IoT.Driver.MitsubishiRx.MitsubishiSimulatorDeviceValue.op_Inequality(IoT.Driver.MitsubishiRx.MitsubishiSimulatorDeviceValue,IoT.Driver.MitsubishiRx.MitsubishiSimulatorDeviceValue)`

```csharp
public static bool op_Inequality(IoT.Driver.MitsubishiRx.MitsubishiSimulatorDeviceValue left, IoT.Driver.MitsubishiRx.MitsubishiSimulatorDeviceValue right)
```
Determines whether the two supplied values are not equal.

- Parameter `left`: The `left` value.
- Parameter `right`: The `right` value.
- Returns: A `bool` result.

###### `P:IoT.Driver.MitsubishiRx.MitsubishiSimulatorDeviceValue.Kind`

```csharp
public IoT.Driver.MitsubishiRx.DeviceValueKind Kind { get; set; }
```
The device value kind.

- Value: The `Kind` value.

###### `P:IoT.Driver.MitsubishiRx.MitsubishiSimulatorDeviceValue.Number`

```csharp
public int Number { get; set; }
```
The numeric device address.

- Value: The `Number` value.

###### `P:IoT.Driver.MitsubishiRx.MitsubishiSimulatorDeviceValue.Symbol`

```csharp
public string Symbol { get; set; }
```
The device symbol.

- Value: The `Symbol` value.

###### `P:IoT.Driver.MitsubishiRx.MitsubishiSimulatorDeviceValue.Value`

```csharp
public ushort Value { get; set; }
```
The stored raw value.

- Value: The `Value` value.

#### `T:IoT.Driver.MitsubishiRx.MitsubishiSimulatorMemory`

```csharp
public class IoT.Driver.MitsubishiRx.MitsubishiSimulatorMemory
```
Provides a thread-safe, deterministic Mitsubishi device-memory image.

##### Declared public members

###### `M:IoT.Driver.MitsubishiRx.MitsubishiSimulatorMemory.#ctor`

```csharp
public IoT.Driver.MitsubishiRx.MitsubishiSimulatorMemory()
```
Initializes a new instance of `IoT.Driver.MitsubishiRx.MitsubishiSimulatorMemory`.

###### `M:IoT.Driver.MitsubishiRx.MitsubishiSimulatorMemory.Clear`

```csharp
public void Clear()
```
Clears all populated devices.

###### `M:IoT.Driver.MitsubishiRx.MitsubishiSimulatorMemory.ReadBit(System.String)`

```csharp
public bool ReadBit(string address)
```
Reads one bit device.

- Parameter `address`: The bit-device address.
- Returns: The current value, or when the device has not been written.

###### `M:IoT.Driver.MitsubishiRx.MitsubishiSimulatorMemory.ReadBit(System.String,IoT.Driver.MitsubishiRx.XyAddressNotation)`

```csharp
public bool ReadBit(string address, IoT.Driver.MitsubishiRx.XyAddressNotation addressNotation)
```
Reads one bit device.

- Parameter `address`: The bit-device address.
- Parameter `addressNotation`: The X/Y address notation.
- Returns: The current value, or when the device has not been written.

###### `M:IoT.Driver.MitsubishiRx.MitsubishiSimulatorMemory.ReadBits(IoT.Driver.MitsubishiRx.MitsubishiDeviceAddress,System.Int32)`

```csharp
public bool[] ReadBits(IoT.Driver.MitsubishiRx.MitsubishiDeviceAddress address, int points)
```
Reads consecutive bit devices.

- Parameter `address`: The first bit-device address.
- Parameter `points`: The number of bits to read.
- Returns: A detached value snapshot.

###### `M:IoT.Driver.MitsubishiRx.MitsubishiSimulatorMemory.ReadBits(System.String,System.Int32)`

```csharp
public bool[] ReadBits(string address, int points)
```
Reads consecutive bit devices.

- Parameter `address`: The first bit-device address.
- Parameter `points`: The number of bits to read.
- Returns: A detached value snapshot.

###### `M:IoT.Driver.MitsubishiRx.MitsubishiSimulatorMemory.ReadBits(System.String,System.Int32,IoT.Driver.MitsubishiRx.XyAddressNotation)`

```csharp
public bool[] ReadBits(string address, int points, IoT.Driver.MitsubishiRx.XyAddressNotation addressNotation)
```
Reads consecutive bit devices.

- Parameter `address`: The first bit-device address.
- Parameter `points`: The number of bits to read.
- Parameter `addressNotation`: The X/Y address notation.
- Returns: A detached value snapshot.

###### `M:IoT.Driver.MitsubishiRx.MitsubishiSimulatorMemory.ReadWord(System.String)`

```csharp
public ushort ReadWord(string address)
```
Reads one word device.

- Parameter `address`: The word-device address.
- Returns: The current value, or zero when the device has not been written.

###### `M:IoT.Driver.MitsubishiRx.MitsubishiSimulatorMemory.ReadWord(System.String,IoT.Driver.MitsubishiRx.XyAddressNotation)`

```csharp
public ushort ReadWord(string address, IoT.Driver.MitsubishiRx.XyAddressNotation addressNotation)
```
Reads one word device.

- Parameter `address`: The word-device address.
- Parameter `addressNotation`: The X/Y address notation.
- Returns: The current value, or zero when the device has not been written.

###### `M:IoT.Driver.MitsubishiRx.MitsubishiSimulatorMemory.ReadWords(IoT.Driver.MitsubishiRx.MitsubishiDeviceAddress,System.Int32)`

```csharp
public ushort[] ReadWords(IoT.Driver.MitsubishiRx.MitsubishiDeviceAddress address, int points)
```
Reads consecutive word devices.

- Parameter `address`: The first word-device address.
- Parameter `points`: The number of words to read.
- Returns: A detached value snapshot.

###### `M:IoT.Driver.MitsubishiRx.MitsubishiSimulatorMemory.ReadWords(System.String,System.Int32)`

```csharp
public ushort[] ReadWords(string address, int points)
```
Reads consecutive word devices.

- Parameter `address`: The first word-device address.
- Parameter `points`: The number of words to read.
- Returns: A detached value snapshot.

###### `M:IoT.Driver.MitsubishiRx.MitsubishiSimulatorMemory.ReadWords(System.String,System.Int32,IoT.Driver.MitsubishiRx.XyAddressNotation)`

```csharp
public ushort[] ReadWords(string address, int points, IoT.Driver.MitsubishiRx.XyAddressNotation addressNotation)
```
Reads consecutive word devices.

- Parameter `address`: The first word-device address.
- Parameter `points`: The number of words to read.
- Parameter `addressNotation`: The X/Y address notation.
- Returns: A detached value snapshot.

###### `M:IoT.Driver.MitsubishiRx.MitsubishiSimulatorMemory.Snapshot`

```csharp
public System.Collections.Generic.IReadOnlyList<IoT.Driver.MitsubishiRx.MitsubishiSimulatorDeviceValue> Snapshot()
```
Gets a deterministic detached snapshot of the populated memory image.

- Returns: Values ordered by device symbol and numeric address.

###### `M:IoT.Driver.MitsubishiRx.MitsubishiSimulatorMemory.WriteBit(System.String,System.Boolean)`

```csharp
public void WriteBit(string address, bool value)
```
Writes one bit device.

- Parameter `address`: The bit-device address.
- Parameter `value`: The value to write.

###### `M:IoT.Driver.MitsubishiRx.MitsubishiSimulatorMemory.WriteBit(System.String,System.Boolean,IoT.Driver.MitsubishiRx.XyAddressNotation)`

```csharp
public void WriteBit(string address, bool value, IoT.Driver.MitsubishiRx.XyAddressNotation addressNotation)
```
Writes one bit device.

- Parameter `address`: The bit-device address.
- Parameter `value`: The value to write.
- Parameter `addressNotation`: The X/Y address notation.

###### `M:IoT.Driver.MitsubishiRx.MitsubishiSimulatorMemory.WriteBits(IoT.Driver.MitsubishiRx.MitsubishiDeviceAddress,System.Collections.Generic.IReadOnlyList`1{System.Boolean})`

```csharp
public void WriteBits(IoT.Driver.MitsubishiRx.MitsubishiDeviceAddress address, System.Collections.Generic.IReadOnlyList<bool> values)
```
Executes the `WriteBits` operation.

- Parameter `address`: The `address` value.
- Parameter `values`: The `values` value.

###### `M:IoT.Driver.MitsubishiRx.MitsubishiSimulatorMemory.WriteBits(System.String,System.Collections.Generic.IReadOnlyList`1{System.Boolean})`

```csharp
public void WriteBits(string address, System.Collections.Generic.IReadOnlyList<bool> values)
```
Executes the `WriteBits` operation.

- Parameter `address`: The `address` value.
- Parameter `values`: The `values` value.

###### `M:IoT.Driver.MitsubishiRx.MitsubishiSimulatorMemory.WriteBits(System.String,System.Collections.Generic.IReadOnlyList`1{System.Boolean},IoT.Driver.MitsubishiRx.XyAddressNotation)`

```csharp
public void WriteBits(string address, System.Collections.Generic.IReadOnlyList<bool> values, IoT.Driver.MitsubishiRx.XyAddressNotation addressNotation)
```
Executes the `WriteBits` operation.

- Parameter `address`: The `address` value.
- Parameter `values`: The `values` value.
- Parameter `addressNotation`: The `addressNotation` value.

###### `M:IoT.Driver.MitsubishiRx.MitsubishiSimulatorMemory.WriteWord(System.String,System.UInt16)`

```csharp
public void WriteWord(string address, ushort value)
```
Writes one word device.

- Parameter `address`: The word-device address.
- Parameter `value`: The value to write.

###### `M:IoT.Driver.MitsubishiRx.MitsubishiSimulatorMemory.WriteWord(System.String,System.UInt16,IoT.Driver.MitsubishiRx.XyAddressNotation)`

```csharp
public void WriteWord(string address, ushort value, IoT.Driver.MitsubishiRx.XyAddressNotation addressNotation)
```
Writes one word device.

- Parameter `address`: The word-device address.
- Parameter `value`: The value to write.
- Parameter `addressNotation`: The X/Y address notation.

###### `M:IoT.Driver.MitsubishiRx.MitsubishiSimulatorMemory.WriteWords(IoT.Driver.MitsubishiRx.MitsubishiDeviceAddress,System.Collections.Generic.IReadOnlyList`1{System.UInt16})`

```csharp
public void WriteWords(IoT.Driver.MitsubishiRx.MitsubishiDeviceAddress address, System.Collections.Generic.IReadOnlyList<ushort> values)
```
Executes the `WriteWords` operation.

- Parameter `address`: The `address` value.
- Parameter `values`: The `values` value.

###### `M:IoT.Driver.MitsubishiRx.MitsubishiSimulatorMemory.WriteWords(System.String,System.Collections.Generic.IReadOnlyList`1{System.UInt16})`

```csharp
public void WriteWords(string address, System.Collections.Generic.IReadOnlyList<ushort> values)
```
Executes the `WriteWords` operation.

- Parameter `address`: The `address` value.
- Parameter `values`: The `values` value.

###### `M:IoT.Driver.MitsubishiRx.MitsubishiSimulatorMemory.WriteWords(System.String,System.Collections.Generic.IReadOnlyList`1{System.UInt16},IoT.Driver.MitsubishiRx.XyAddressNotation)`

```csharp
public void WriteWords(string address, System.Collections.Generic.IReadOnlyList<ushort> values, IoT.Driver.MitsubishiRx.XyAddressNotation addressNotation)
```
Executes the `WriteWords` operation.

- Parameter `address`: The `address` value.
- Parameter `values`: The `values` value.
- Parameter `addressNotation`: The `addressNotation` value.

###### `P:IoT.Driver.MitsubishiRx.MitsubishiSimulatorMemory.Version`

```csharp
public long Version { get; }
```
Gets the monotonically increasing memory version.

- Value: The `Version` value.

#### `T:IoT.Driver.MitsubishiRx.MitsubishiSimulatorTransport`

```csharp
public class IoT.Driver.MitsubishiRx.MitsubishiSimulatorTransport
```
Provides a deterministic, in-memory Mitsubishi transport for simulations, examples, integration tests, and applications that do not have a physical PLC.

##### Declared public members

###### `M:IoT.Driver.MitsubishiRx.MitsubishiSimulatorTransport.#ctor`

```csharp
public IoT.Driver.MitsubishiRx.MitsubishiSimulatorTransport()
```
Initializes a new instance of the `T:IoT.Driver.MitsubishiRx.MitsubishiSimulatorTransport` class.

###### `M:IoT.Driver.MitsubishiRx.MitsubishiSimulatorTransport.#ctor(IoT.Driver.MitsubishiRx.MitsubishiSimulatorMemory)`

```csharp
public IoT.Driver.MitsubishiRx.MitsubishiSimulatorTransport(IoT.Driver.MitsubishiRx.MitsubishiSimulatorMemory memory)
```
Initializes a new instance of the `T:IoT.Driver.MitsubishiRx.MitsubishiSimulatorTransport` class.

- Parameter `memory`: The stateful device-memory image.

###### `M:IoT.Driver.MitsubishiRx.MitsubishiSimulatorTransport.#ctor(System.Collections.Generic.IEnumerable`1{System.Byte[]})`

```csharp
public IoT.Driver.MitsubishiRx.MitsubishiSimulatorTransport(System.Collections.Generic.IEnumerable<byte[]> responses)
```
Initializes a new instance of `IoT.Driver.MitsubishiRx.MitsubishiSimulatorTransport`.

- Parameter `responses`: The `responses` value.

###### `M:IoT.Driver.MitsubishiRx.MitsubishiSimulatorTransport.#ctor(System.Func`2{IoT.Driver.MitsubishiRx.MitsubishiTransportRequest,System.Byte[]})`

```csharp
public IoT.Driver.MitsubishiRx.MitsubishiSimulatorTransport(System.Func<IoT.Driver.MitsubishiRx.MitsubishiTransportRequest, byte[]> responseFactory)
```
Initializes a new instance of `IoT.Driver.MitsubishiRx.MitsubishiSimulatorTransport`.

- Parameter `responseFactory`: The `responseFactory` value.

###### `M:IoT.Driver.MitsubishiRx.MitsubishiSimulatorTransport.ClearRequests`

```csharp
public void ClearRequests()
```
Clears the captured request history.

###### `M:IoT.Driver.MitsubishiRx.MitsubishiSimulatorTransport.ConnectAsync(IoT.Driver.MitsubishiRx.MitsubishiClientOptions,System.Threading.CancellationToken)`

```csharp
public System.Threading.Tasks.ValueTask ConnectAsync(IoT.Driver.MitsubishiRx.MitsubishiClientOptions options, System.Threading.CancellationToken cancellationToken)
```
Inherits XML documentation from its implemented or overridden member.

- Parameter `options`: The `options` value.
- Parameter `cancellationToken`: The `cancellationToken` value.
- Returns: A `System.Threading.Tasks.ValueTask` result.

###### `M:IoT.Driver.MitsubishiRx.MitsubishiSimulatorTransport.CreateErrorResponse(IoT.Driver.MitsubishiRx.MitsubishiClientOptions,System.UInt16)`

```csharp
public static byte[] CreateErrorResponse(IoT.Driver.MitsubishiRx.MitsubishiClientOptions options, ushort endCode)
```
Creates a complete protocol response containing a PLC end code.

- Parameter `options`: The client options defining the response framing.
- Parameter `endCode`: The PLC end code.
- Returns: The complete wire response.

###### `M:IoT.Driver.MitsubishiRx.MitsubishiSimulatorTransport.CreateSuccessResponse(IoT.Driver.MitsubishiRx.MitsubishiClientOptions,System.ReadOnlySpan`1{System.Byte})`

```csharp
public static byte[] CreateSuccessResponse(IoT.Driver.MitsubishiRx.MitsubishiClientOptions options, System.ReadOnlySpan<byte> payload)
```
Executes the `CreateSuccessResponse` operation.

- Parameter `options`: The `options` value.
- Parameter `payload`: The `payload` value.
- Returns: A `byte[]` result.

###### `M:IoT.Driver.MitsubishiRx.MitsubishiSimulatorTransport.DisconnectAsync(System.Threading.CancellationToken)`

```csharp
public System.Threading.Tasks.ValueTask DisconnectAsync(System.Threading.CancellationToken cancellationToken)
```
Inherits XML documentation from its implemented or overridden member.

- Parameter `cancellationToken`: The `cancellationToken` value.
- Returns: A `System.Threading.Tasks.ValueTask` result.

###### `M:IoT.Driver.MitsubishiRx.MitsubishiSimulatorTransport.Dispose`

```csharp
public void Dispose()
```
Inherits XML documentation from its implemented or overridden member.

###### `M:IoT.Driver.MitsubishiRx.MitsubishiSimulatorTransport.DisposeAsync`

```csharp
public System.Threading.Tasks.ValueTask DisposeAsync()
```
Inherits XML documentation from its implemented or overridden member.

- Returns: A `System.Threading.Tasks.ValueTask` result.

###### `M:IoT.Driver.MitsubishiRx.MitsubishiSimulatorTransport.EnqueueConnectFault(System.Exception)`

```csharp
public void EnqueueConnectFault(System.Exception exception)
```
Queues a fault to be thrown by the next connection attempt.

- Parameter `exception`: The fault to throw.

###### `M:IoT.Driver.MitsubishiRx.MitsubishiSimulatorTransport.EnqueueFault(System.Exception)`

```csharp
public void EnqueueFault(System.Exception exception)
```
Queues a fault to be thrown by the next exchange.

- Parameter `exception`: The fault to throw.

###### `M:IoT.Driver.MitsubishiRx.MitsubishiSimulatorTransport.EnqueueResponse(System.ReadOnlySpan`1{System.Byte})`

```csharp
public void EnqueueResponse(System.ReadOnlySpan<byte> response)
```
Executes the `EnqueueResponse` operation.

- Parameter `response`: The `response` value.

###### `M:IoT.Driver.MitsubishiRx.MitsubishiSimulatorTransport.ExchangeAsync(IoT.Driver.MitsubishiRx.MitsubishiTransportRequest,System.Threading.CancellationToken)`

```csharp
public System.Threading.Tasks.ValueTask<byte[]> ExchangeAsync(IoT.Driver.MitsubishiRx.MitsubishiTransportRequest request, System.Threading.CancellationToken cancellationToken)
```
Inherits XML documentation from its implemented or overridden member.

- Parameter `request`: The `request` value.
- Parameter `cancellationToken`: The `cancellationToken` value.
- Returns: A `System.Threading.Tasks.ValueTask<byte[]>` result.

###### `M:IoT.Driver.MitsubishiRx.MitsubishiSimulatorTransport.ReadBufferMemory(System.UInt16,System.Int32)`

```csharp
public ushort[] ReadBufferMemory(ushort address, int length)
```
Reads consecutive simulated buffer-memory words.

- Parameter `address`: The first buffer-memory address.
- Parameter `length`: The number of words to read.
- Returns: A detached word snapshot.

###### `M:IoT.Driver.MitsubishiRx.MitsubishiSimulatorTransport.SetControllerError(System.UInt16)`

```csharp
public void SetControllerError(ushort errorCode)
```
Sets the current simulated controller error code.

- Parameter `errorCode`: The deterministic controller error code.

###### `M:IoT.Driver.MitsubishiRx.MitsubishiSimulatorTransport.WriteBufferMemory(System.UInt16,System.Collections.Generic.IReadOnlyList`1{System.UInt16})`

```csharp
public void WriteBufferMemory(ushort address, System.Collections.Generic.IReadOnlyList<ushort> values)
```
Executes the `WriteBufferMemory` operation.

- Parameter `address`: The `address` value.
- Parameter `values`: The `values` value.

###### `P:IoT.Driver.MitsubishiRx.MitsubishiSimulatorTransport.ConnectCount`

```csharp
public int ConnectCount { get; }
```
Gets the number of successful connection attempts.

- Value: The `ConnectCount` value.

###### `P:IoT.Driver.MitsubishiRx.MitsubishiSimulatorTransport.ConnectedOptions`

```csharp
public IoT.Driver.MitsubishiRx.MitsubishiClientOptions ConnectedOptions { get; }
```
Gets the options supplied by the most recent successful connection.

- Value: The `ConnectedOptions` value.

###### `P:IoT.Driver.MitsubishiRx.MitsubishiSimulatorTransport.ControllerError`

```csharp
public ushort ControllerError { get; }
```
Gets the current simulated controller error code.

- Value: The `ControllerError` value.

###### `P:IoT.Driver.MitsubishiRx.MitsubishiSimulatorTransport.DisconnectCount`

```csharp
public int DisconnectCount { get; }
```
Gets the number of disconnect operations.

- Value: The `DisconnectCount` value.

###### `P:IoT.Driver.MitsubishiRx.MitsubishiSimulatorTransport.IsConnected`

```csharp
public bool IsConnected { get; }
```
Inherits XML documentation from its implemented or overridden member.

- Value: The `IsConnected` value.

###### `P:IoT.Driver.MitsubishiRx.MitsubishiSimulatorTransport.IsCpuRunning`

```csharp
public bool IsCpuRunning { get; }
```
Gets whether the simulated controller is in the run state.

- Value: The `IsCpuRunning` value.

###### `P:IoT.Driver.MitsubishiRx.MitsubishiSimulatorTransport.Memory`

```csharp
public IoT.Driver.MitsubishiRx.MitsubishiSimulatorMemory Memory { get; }
```
Gets the stateful device-memory image used by automatic responses.

- Value: The `Memory` value.

###### `P:IoT.Driver.MitsubishiRx.MitsubishiSimulatorTransport.ModelCode`

```csharp
public ushort ModelCode { get; set; }
```
Gets or sets the simulated controller model code.

- Value: The `ModelCode` value.

###### `P:IoT.Driver.MitsubishiRx.MitsubishiSimulatorTransport.ModelName`

```csharp
public string ModelName { get; set; }
```
Gets or sets the simulated controller model name.

- Value: The `ModelName` value.

###### `P:IoT.Driver.MitsubishiRx.MitsubishiSimulatorTransport.Requests`

```csharp
public System.Collections.Generic.IReadOnlyList<IoT.Driver.MitsubishiRx.MitsubishiTransportRequest> Requests { get; }
```
Gets immutable snapshots of requests in their exchange order.

- Value: The `Requests` value.

#### `T:IoT.Driver.MitsubishiRx.MitsubishiTagAttribute`

```csharp
public class IoT.Driver.MitsubishiRx.MitsubishiTagAttribute
```
Binds a generated property to a logical Mitsubishi tag name.

##### Declared public members

###### `M:IoT.Driver.MitsubishiRx.MitsubishiTagAttribute.#ctor(System.String)`

```csharp
public IoT.Driver.MitsubishiRx.MitsubishiTagAttribute(string tagName)
```
Initializes a tag binding attribute.

- Parameter `tagName`: The logical tag name.

###### `P:IoT.Driver.MitsubishiRx.MitsubishiTagAttribute.TagName`

```csharp
public string TagName { get; }
```
Gets the logical tag name.

- Value: The `TagName` value.

#### `T:IoT.Driver.MitsubishiRx.MitsubishiTagChange`

```csharp
public class IoT.Driver.MitsubishiRx.MitsubishiTagChange
```
Provides the MitsubishiTagChange record.

##### Declared public members

###### `M:IoT.Driver.MitsubishiRx.MitsubishiTagChange.#ctor(System.String,IoT.Driver.MitsubishiRx.MitsubishiTagDefinition,IoT.Driver.MitsubishiRx.MitsubishiTagDefinition)`

```csharp
public IoT.Driver.MitsubishiRx.MitsubishiTagChange(string Name, IoT.Driver.MitsubishiRx.MitsubishiTagDefinition Previous, IoT.Driver.MitsubishiRx.MitsubishiTagDefinition Current)
```
Provides the MitsubishiTagChange record.

- Parameter `Name`: The Name parameter.
- Parameter `Previous`: The Previous parameter.
- Parameter `Current`: The Current parameter.

###### `M:IoT.Driver.MitsubishiRx.MitsubishiTagChange.Deconstruct(System.String@,IoT.Driver.MitsubishiRx.MitsubishiTagDefinition@,IoT.Driver.MitsubishiRx.MitsubishiTagDefinition@)`

```csharp
public void Deconstruct(out string Name, out IoT.Driver.MitsubishiRx.MitsubishiTagDefinition Previous, out IoT.Driver.MitsubishiRx.MitsubishiTagDefinition Current)
```
Deconstructs the value into its component values.

- Parameter `Name`: The `Name` value.
- Parameter `Previous`: The `Previous` value.
- Parameter `Current`: The `Current` value.

###### `M:IoT.Driver.MitsubishiRx.MitsubishiTagChange.Equals(IoT.Driver.MitsubishiRx.MitsubishiTagChange)`

```csharp
public bool Equals(IoT.Driver.MitsubishiRx.MitsubishiTagChange other)
```
Determines whether the supplied value is equal to the current value.

- Parameter `other`: The `other` value.
- Returns: A `bool` result.

###### `M:IoT.Driver.MitsubishiRx.MitsubishiTagChange.Equals(System.Object)`

```csharp
public bool Equals(object obj)
```
Determines whether the supplied value is equal to the current value.

- Parameter `obj`: The `obj` value.
- Returns: A `bool` result.

###### `M:IoT.Driver.MitsubishiRx.MitsubishiTagChange.GetHashCode`

```csharp
public int GetHashCode()
```
Returns the hash code for the current value.

- Returns: A `int` result.

###### `M:IoT.Driver.MitsubishiRx.MitsubishiTagChange.ToString`

```csharp
public string ToString()
```
Returns a string representation of the current value.

- Returns: A `string` result.

###### `M:IoT.Driver.MitsubishiRx.MitsubishiTagChange.op_Equality(IoT.Driver.MitsubishiRx.MitsubishiTagChange,IoT.Driver.MitsubishiRx.MitsubishiTagChange)`

```csharp
public static bool op_Equality(IoT.Driver.MitsubishiRx.MitsubishiTagChange left, IoT.Driver.MitsubishiRx.MitsubishiTagChange right)
```
Determines whether the two supplied values are equal.

- Parameter `left`: The `left` value.
- Parameter `right`: The `right` value.
- Returns: A `bool` result.

###### `M:IoT.Driver.MitsubishiRx.MitsubishiTagChange.op_Inequality(IoT.Driver.MitsubishiRx.MitsubishiTagChange,IoT.Driver.MitsubishiRx.MitsubishiTagChange)`

```csharp
public static bool op_Inequality(IoT.Driver.MitsubishiRx.MitsubishiTagChange left, IoT.Driver.MitsubishiRx.MitsubishiTagChange right)
```
Determines whether the two supplied values are not equal.

- Parameter `left`: The `left` value.
- Parameter `right`: The `right` value.
- Returns: A `bool` result.

###### `P:IoT.Driver.MitsubishiRx.MitsubishiTagChange.ChangeKinds`

```csharp
public IoT.Driver.MitsubishiRx.MitsubishiSchemaChangeKind ChangeKinds { get; }
```
Gets or sets the ChangeKinds property.

- Value: The `ChangeKinds` value.

###### `P:IoT.Driver.MitsubishiRx.MitsubishiTagChange.Current`

```csharp
public IoT.Driver.MitsubishiRx.MitsubishiTagDefinition Current { get; set; }
```
The Current parameter.

- Value: The `Current` value.

###### `P:IoT.Driver.MitsubishiRx.MitsubishiTagChange.Name`

```csharp
public string Name { get; set; }
```
The Name parameter.

- Value: The `Name` value.

###### `P:IoT.Driver.MitsubishiRx.MitsubishiTagChange.Previous`

```csharp
public IoT.Driver.MitsubishiRx.MitsubishiTagDefinition Previous { get; set; }
```
The Previous parameter.

- Value: The `Previous` value.

#### `T:IoT.Driver.MitsubishiRx.MitsubishiTagClientAttribute`

```csharp
public class IoT.Driver.MitsubishiRx.MitsubishiTagClientAttribute
```
Binds generated tag members to a Mitsubishi client member.

##### Declared public members

###### `M:IoT.Driver.MitsubishiRx.MitsubishiTagClientAttribute.#ctor(System.String)`

```csharp
public IoT.Driver.MitsubishiRx.MitsubishiTagClientAttribute(string clientMemberName)
```
Initializes a client binding attribute.

- Parameter `clientMemberName`: The client field or property name.

###### `P:IoT.Driver.MitsubishiRx.MitsubishiTagClientAttribute.ClientMemberName`

```csharp
public string ClientMemberName { get; }
```
Gets the client field or property name.

- Value: The `ClientMemberName` value.

#### `T:IoT.Driver.MitsubishiRx.MitsubishiTagClientSchemaAttribute`

```csharp
public class IoT.Driver.MitsubishiRx.MitsubishiTagClientSchemaAttribute
```
Declares an inline Mitsubishi tag schema.

##### Declared public members

###### `M:IoT.Driver.MitsubishiRx.MitsubishiTagClientSchemaAttribute.#ctor(System.String)`

```csharp
public IoT.Driver.MitsubishiRx.MitsubishiTagClientSchemaAttribute(string schemaJson)
```
Initializes a schema attribute.

- Parameter `schemaJson`: The JSON tag schema.

###### `P:IoT.Driver.MitsubishiRx.MitsubishiTagClientSchemaAttribute.SchemaJson`

```csharp
public string SchemaJson { get; }
```
Gets the JSON tag schema.

- Value: The `SchemaJson` value.

#### `T:IoT.Driver.MitsubishiRx.MitsubishiTagDatabase`

```csharp
public class IoT.Driver.MitsubishiRx.MitsubishiTagDatabase
```
Provides the MitsubishiTagDatabase type.

##### Declared public members

###### `M:IoT.Driver.MitsubishiRx.MitsubishiTagDatabase.#ctor(System.Collections.Generic.IEnumerable`1{IoT.Driver.MitsubishiRx.MitsubishiTagDefinition})`

```csharp
public IoT.Driver.MitsubishiRx.MitsubishiTagDatabase(System.Collections.Generic.IEnumerable<IoT.Driver.MitsubishiRx.MitsubishiTagDefinition> tags)
```
Initializes a new instance of `IoT.Driver.MitsubishiRx.MitsubishiTagDatabase`.

- Parameter `tags`: The `tags` value.

###### `M:IoT.Driver.MitsubishiRx.MitsubishiTagDatabase.Add(IoT.Driver.MitsubishiRx.MitsubishiTagDefinition)`

```csharp
public void Add(IoT.Driver.MitsubishiRx.MitsubishiTagDefinition tag)
```
Executes the Add operation.

- Parameter `tag`: The tag parameter.

###### `M:IoT.Driver.MitsubishiRx.MitsubishiTagDatabase.AddGroup(IoT.Driver.MitsubishiRx.MitsubishiTagGroupDefinition)`

```csharp
public void AddGroup(IoT.Driver.MitsubishiRx.MitsubishiTagGroupDefinition group)
```
Executes the AddGroup operation.

- Parameter `group`: The group parameter.

###### `M:IoT.Driver.MitsubishiRx.MitsubishiTagDatabase.CompareWith(IoT.Driver.MitsubishiRx.MitsubishiTagDatabase)`

```csharp
public IoT.Driver.MitsubishiRx.MitsubishiTagDatabaseDiff CompareWith(IoT.Driver.MitsubishiRx.MitsubishiTagDatabase other)
```
Executes the CompareWith operation.

- Parameter `other`: The other parameter.
- Returns: The CompareWith operation result.

###### `M:IoT.Driver.MitsubishiRx.MitsubishiTagDatabase.FromCsv(System.String)`

```csharp
public static IoT.Driver.MitsubishiRx.MitsubishiTagDatabase FromCsv(string csvContent)
```
Executes the FromCsv operation.

- Parameter `csvContent`: The csvContent parameter.
- Returns: The FromCsv operation result.

###### `M:IoT.Driver.MitsubishiRx.MitsubishiTagDatabase.FromJson(System.String)`

```csharp
public static IoT.Driver.MitsubishiRx.MitsubishiTagDatabase FromJson(string json)
```
Executes the FromJson operation.

- Parameter `json`: The json parameter.
- Returns: The FromJson operation result.

###### `M:IoT.Driver.MitsubishiRx.MitsubishiTagDatabase.FromYaml(System.String)`

```csharp
public static IoT.Driver.MitsubishiRx.MitsubishiTagDatabase FromYaml(string yaml)
```
Executes the FromYaml operation.

- Parameter `yaml`: The yaml parameter.
- Returns: The FromYaml operation result.

###### `M:IoT.Driver.MitsubishiRx.MitsubishiTagDatabase.GetRequired(System.String)`

```csharp
public IoT.Driver.MitsubishiRx.MitsubishiTagDefinition GetRequired(string name)
```
Executes the GetRequired operation.

- Parameter `name`: The name parameter.
- Returns: The GetRequired operation result.

###### `M:IoT.Driver.MitsubishiRx.MitsubishiTagDatabase.GetRequiredGroup(System.String)`

```csharp
public IoT.Driver.MitsubishiRx.MitsubishiTagGroupDefinition GetRequiredGroup(string name)
```
Executes the GetRequiredGroup operation.

- Parameter `name`: The name parameter.
- Returns: The GetRequiredGroup operation result.

###### `M:IoT.Driver.MitsubishiRx.MitsubishiTagDatabase.Load(System.String)`

```csharp
public static IoT.Driver.MitsubishiRx.MitsubishiTagDatabase Load(string path)
```
Executes the Load operation.

- Parameter `path`: The path parameter.
- Returns: The Load operation result.

###### `M:IoT.Driver.MitsubishiRx.MitsubishiTagDatabase.Save(System.String)`

```csharp
public void Save(string path)
```
Executes the Save operation.

- Parameter `path`: The path parameter.

###### `M:IoT.Driver.MitsubishiRx.MitsubishiTagDatabase.ToCsv`

```csharp
public string ToCsv()
```
Serializes tags and group membership to CSV.

- Returns: The CSV document.

###### `M:IoT.Driver.MitsubishiRx.MitsubishiTagDatabase.ToJson`

```csharp
public string ToJson()
```
Executes the ToJson operation.

- Returns: The ToJson operation result.

###### `M:IoT.Driver.MitsubishiRx.MitsubishiTagDatabase.ToYaml`

```csharp
public string ToYaml()
```
Executes the ToYaml operation.

- Returns: The ToYaml operation result.

###### `M:IoT.Driver.MitsubishiRx.MitsubishiTagDatabase.TryGet(System.String,IoT.Driver.MitsubishiRx.MitsubishiTagDefinition@)`

```csharp
public bool TryGet(string name, out IoT.Driver.MitsubishiRx.MitsubishiTagDefinition tag)
```
Executes the TryGet operation.

- Parameter `name`: The name parameter.
- Parameter `tag`: The tag parameter.
- Returns: The TryGet operation result.

###### `M:IoT.Driver.MitsubishiRx.MitsubishiTagDatabase.TryGetGroup(System.String,IoT.Driver.MitsubishiRx.MitsubishiTagGroupDefinition@)`

```csharp
public bool TryGetGroup(string name, out IoT.Driver.MitsubishiRx.MitsubishiTagGroupDefinition group)
```
Executes the TryGetGroup operation.

- Parameter `name`: The name parameter.
- Parameter `group`: The group parameter.
- Returns: The TryGetGroup operation result.

###### `P:IoT.Driver.MitsubishiRx.MitsubishiTagDatabase.Count`

```csharp
public int Count { get; }
```
Gets or sets the Count property.

- Value: The `Count` value.

###### `P:IoT.Driver.MitsubishiRx.MitsubishiTagDatabase.GroupCount`

```csharp
public int GroupCount { get; }
```
Gets or sets the GroupCount property.

- Value: The `GroupCount` value.

###### `P:IoT.Driver.MitsubishiRx.MitsubishiTagDatabase.Groups`

```csharp
public System.Collections.Generic.IReadOnlyCollection<IoT.Driver.MitsubishiRx.MitsubishiTagGroupDefinition> Groups { get; }
```
Gets or sets the Groups property.

- Value: The `Groups` value.

###### `P:IoT.Driver.MitsubishiRx.MitsubishiTagDatabase.Tags`

```csharp
public System.Collections.Generic.IReadOnlyCollection<IoT.Driver.MitsubishiRx.MitsubishiTagDefinition> Tags { get; }
```
Gets or sets the Tags property.

- Value: The `Tags` value.

#### `T:IoT.Driver.MitsubishiRx.MitsubishiTagDatabaseDiff`

```csharp
public class IoT.Driver.MitsubishiRx.MitsubishiTagDatabaseDiff
```
Provides the MitsubishiTagDatabaseDiff record.

##### Declared public members

###### `M:IoT.Driver.MitsubishiRx.MitsubishiTagDatabaseDiff.#ctor(System.Collections.Generic.IReadOnlyList`1{IoT.Driver.MitsubishiRx.MitsubishiTagDefinition},System.Collections.Generic.IReadOnlyList`1{IoT.Driver.MitsubishiRx.MitsubishiTagDefinition},System.Collections.Generic.IReadOnlyList`1{IoT.Driver.MitsubishiRx.MitsubishiTagChange},System.Collections.Generic.IReadOnlyList`1{IoT.Driver.MitsubishiRx.MitsubishiTagGroupDefinition},System.Collections.Generic.IReadOnlyList`1{IoT.Driver.MitsubishiRx.MitsubishiTagGroupDefinition},System.Collections.Generic.IReadOnlyList`1{IoT.Driver.MitsubishiRx.MitsubishiTagGroupChange})`

```csharp
public IoT.Driver.MitsubishiRx.MitsubishiTagDatabaseDiff(System.Collections.Generic.IReadOnlyList<IoT.Driver.MitsubishiRx.MitsubishiTagDefinition> AddedTags, System.Collections.Generic.IReadOnlyList<IoT.Driver.MitsubishiRx.MitsubishiTagDefinition> RemovedTags, System.Collections.Generic.IReadOnlyList<IoT.Driver.MitsubishiRx.MitsubishiTagChange> ChangedTags, System.Collections.Generic.IReadOnlyList<IoT.Driver.MitsubishiRx.MitsubishiTagGroupDefinition> AddedGroups, System.Collections.Generic.IReadOnlyList<IoT.Driver.MitsubishiRx.MitsubishiTagGroupDefinition> RemovedGroups, System.Collections.Generic.IReadOnlyList<IoT.Driver.MitsubishiRx.MitsubishiTagGroupChange> ChangedGroups)
```
Initializes a new instance of `IoT.Driver.MitsubishiRx.MitsubishiTagDatabaseDiff`.

- Parameter `AddedTags`: The `AddedTags` value.
- Parameter `RemovedTags`: The `RemovedTags` value.
- Parameter `ChangedTags`: The `ChangedTags` value.
- Parameter `AddedGroups`: The `AddedGroups` value.
- Parameter `RemovedGroups`: The `RemovedGroups` value.
- Parameter `ChangedGroups`: The `ChangedGroups` value.

###### `M:IoT.Driver.MitsubishiRx.MitsubishiTagDatabaseDiff.Deconstruct(System.Collections.Generic.IReadOnlyList`1{IoT.Driver.MitsubishiRx.MitsubishiTagDefinition}@,System.Collections.Generic.IReadOnlyList`1{IoT.Driver.MitsubishiRx.MitsubishiTagDefinition}@,System.Collections.Generic.IReadOnlyList`1{IoT.Driver.MitsubishiRx.MitsubishiTagChange}@,System.Collections.Generic.IReadOnlyList`1{IoT.Driver.MitsubishiRx.MitsubishiTagGroupDefinition}@,System.Collections.Generic.IReadOnlyList`1{IoT.Driver.MitsubishiRx.MitsubishiTagGroupDefinition}@,System.Collections.Generic.IReadOnlyList`1{IoT.Driver.MitsubishiRx.MitsubishiTagGroupChange}@)`

```csharp
public void Deconstruct(out System.Collections.Generic.IReadOnlyList<IoT.Driver.MitsubishiRx.MitsubishiTagDefinition> AddedTags, out System.Collections.Generic.IReadOnlyList<IoT.Driver.MitsubishiRx.MitsubishiTagDefinition> RemovedTags, out System.Collections.Generic.IReadOnlyList<IoT.Driver.MitsubishiRx.MitsubishiTagChange> ChangedTags, out System.Collections.Generic.IReadOnlyList<IoT.Driver.MitsubishiRx.MitsubishiTagGroupDefinition> AddedGroups, out System.Collections.Generic.IReadOnlyList<IoT.Driver.MitsubishiRx.MitsubishiTagGroupDefinition> RemovedGroups, out System.Collections.Generic.IReadOnlyList<IoT.Driver.MitsubishiRx.MitsubishiTagGroupChange> ChangedGroups)
```
Deconstructs the value into its component values.

- Parameter `AddedTags`: The `AddedTags` value.
- Parameter `RemovedTags`: The `RemovedTags` value.
- Parameter `ChangedTags`: The `ChangedTags` value.
- Parameter `AddedGroups`: The `AddedGroups` value.
- Parameter `RemovedGroups`: The `RemovedGroups` value.
- Parameter `ChangedGroups`: The `ChangedGroups` value.

###### `M:IoT.Driver.MitsubishiRx.MitsubishiTagDatabaseDiff.Equals(IoT.Driver.MitsubishiRx.MitsubishiTagDatabaseDiff)`

```csharp
public bool Equals(IoT.Driver.MitsubishiRx.MitsubishiTagDatabaseDiff other)
```
Determines whether the supplied value is equal to the current value.

- Parameter `other`: The `other` value.
- Returns: A `bool` result.

###### `M:IoT.Driver.MitsubishiRx.MitsubishiTagDatabaseDiff.Equals(System.Object)`

```csharp
public bool Equals(object obj)
```
Determines whether the supplied value is equal to the current value.

- Parameter `obj`: The `obj` value.
- Returns: A `bool` result.

###### `M:IoT.Driver.MitsubishiRx.MitsubishiTagDatabaseDiff.GetHashCode`

```csharp
public int GetHashCode()
```
Returns the hash code for the current value.

- Returns: A `int` result.

###### `M:IoT.Driver.MitsubishiRx.MitsubishiTagDatabaseDiff.ToString`

```csharp
public string ToString()
```
Returns a string representation of the current value.

- Returns: A `string` result.

###### `M:IoT.Driver.MitsubishiRx.MitsubishiTagDatabaseDiff.op_Equality(IoT.Driver.MitsubishiRx.MitsubishiTagDatabaseDiff,IoT.Driver.MitsubishiRx.MitsubishiTagDatabaseDiff)`

```csharp
public static bool op_Equality(IoT.Driver.MitsubishiRx.MitsubishiTagDatabaseDiff left, IoT.Driver.MitsubishiRx.MitsubishiTagDatabaseDiff right)
```
Determines whether the two supplied values are equal.

- Parameter `left`: The `left` value.
- Parameter `right`: The `right` value.
- Returns: A `bool` result.

###### `M:IoT.Driver.MitsubishiRx.MitsubishiTagDatabaseDiff.op_Inequality(IoT.Driver.MitsubishiRx.MitsubishiTagDatabaseDiff,IoT.Driver.MitsubishiRx.MitsubishiTagDatabaseDiff)`

```csharp
public static bool op_Inequality(IoT.Driver.MitsubishiRx.MitsubishiTagDatabaseDiff left, IoT.Driver.MitsubishiRx.MitsubishiTagDatabaseDiff right)
```
Determines whether the two supplied values are not equal.

- Parameter `left`: The `left` value.
- Parameter `right`: The `right` value.
- Returns: A `bool` result.

###### `P:IoT.Driver.MitsubishiRx.MitsubishiTagDatabaseDiff.AddedGroups`

```csharp
public System.Collections.Generic.IReadOnlyList<IoT.Driver.MitsubishiRx.MitsubishiTagGroupDefinition> AddedGroups { get; set; }
```
The AddedGroups parameter.

- Value: The `AddedGroups` value.

###### `P:IoT.Driver.MitsubishiRx.MitsubishiTagDatabaseDiff.AddedTags`

```csharp
public System.Collections.Generic.IReadOnlyList<IoT.Driver.MitsubishiRx.MitsubishiTagDefinition> AddedTags { get; set; }
```
The AddedTags parameter.

- Value: The `AddedTags` value.

###### `P:IoT.Driver.MitsubishiRx.MitsubishiTagDatabaseDiff.ChangeCount`

```csharp
public int ChangeCount { get; }
```
Gets or sets the ChangeCount property.

- Value: The `ChangeCount` value.

###### `P:IoT.Driver.MitsubishiRx.MitsubishiTagDatabaseDiff.ChangeKinds`

```csharp
public IoT.Driver.MitsubishiRx.MitsubishiSchemaChangeKind ChangeKinds { get; }
```
Gets or sets the ChangeKinds property.

- Value: The `ChangeKinds` value.

###### `P:IoT.Driver.MitsubishiRx.MitsubishiTagDatabaseDiff.ChangedGroups`

```csharp
public System.Collections.Generic.IReadOnlyList<IoT.Driver.MitsubishiRx.MitsubishiTagGroupChange> ChangedGroups { get; set; }
```
The ChangedGroups parameter.

- Value: The `ChangedGroups` value.

###### `P:IoT.Driver.MitsubishiRx.MitsubishiTagDatabaseDiff.ChangedTags`

```csharp
public System.Collections.Generic.IReadOnlyList<IoT.Driver.MitsubishiRx.MitsubishiTagChange> ChangedTags { get; set; }
```
The ChangedTags parameter.

- Value: The `ChangedTags` value.

###### `P:IoT.Driver.MitsubishiRx.MitsubishiTagDatabaseDiff.Empty`

```csharp
public IoT.Driver.MitsubishiRx.MitsubishiTagDatabaseDiff Empty { get; }
```
Gets or sets the Empty property.

- Value: The `Empty` value.

###### `P:IoT.Driver.MitsubishiRx.MitsubishiTagDatabaseDiff.HasChanges`

```csharp
public bool HasChanges { get; }
```
Gets or sets the HasChanges property.

- Value: The `HasChanges` value.

###### `P:IoT.Driver.MitsubishiRx.MitsubishiTagDatabaseDiff.RemovedGroups`

```csharp
public System.Collections.Generic.IReadOnlyList<IoT.Driver.MitsubishiRx.MitsubishiTagGroupDefinition> RemovedGroups { get; set; }
```
The RemovedGroups parameter.

- Value: The `RemovedGroups` value.

###### `P:IoT.Driver.MitsubishiRx.MitsubishiTagDatabaseDiff.RemovedTags`

```csharp
public System.Collections.Generic.IReadOnlyList<IoT.Driver.MitsubishiRx.MitsubishiTagDefinition> RemovedTags { get; set; }
```
The RemovedTags parameter.

- Value: The `RemovedTags` value.

#### `T:IoT.Driver.MitsubishiRx.MitsubishiTagDefinition`

```csharp
public class IoT.Driver.MitsubishiRx.MitsubishiTagDefinition
```
Provides the MitsubishiTagDefinition record.

##### Declared public members

###### `M:IoT.Driver.MitsubishiRx.MitsubishiTagDefinition.#ctor(System.String,System.String,System.String,System.String,System.Double,System.Double,System.Nullable`1{System.Int32},System.String,System.String,System.Boolean,System.String,System.String)`

```csharp
public IoT.Driver.MitsubishiRx.MitsubishiTagDefinition(string Name, string Address, string DataType, string Description, double Scale, double Offset, System.Nullable<int> Length, string Encoding, string Units, bool Signed, string ByteOrder, string Notes)
```
Initializes a new instance of `IoT.Driver.MitsubishiRx.MitsubishiTagDefinition`.

- Parameter `Name`: The `Name` value.
- Parameter `Address`: The `Address` value.
- Parameter `DataType`: The `DataType` value.
- Parameter `Description`: The `Description` value.
- Parameter `Scale`: The `Scale` value.
- Parameter `Offset`: The `Offset` value.
- Parameter `Length`: The `Length` value.
- Parameter `Encoding`: The `Encoding` value.
- Parameter `Units`: The `Units` value.
- Parameter `Signed`: The `Signed` value.
- Parameter `ByteOrder`: The `ByteOrder` value.
- Parameter `Notes`: The `Notes` value.

###### `M:IoT.Driver.MitsubishiRx.MitsubishiTagDefinition.Deconstruct(System.String@,System.String@,System.String@,System.String@,System.Double@,System.Double@,System.Nullable`1{System.Int32}@,System.String@,System.String@,System.Boolean@,System.String@,System.String@)`

```csharp
public void Deconstruct(out string Name, out string Address, out string DataType, out string Description, out double Scale, out double Offset, out System.Nullable<int> Length, out string Encoding, out string Units, out bool Signed, out string ByteOrder, out string Notes)
```
Deconstructs the value into its component values.

- Parameter `Name`: The `Name` value.
- Parameter `Address`: The `Address` value.
- Parameter `DataType`: The `DataType` value.
- Parameter `Description`: The `Description` value.
- Parameter `Scale`: The `Scale` value.
- Parameter `Offset`: The `Offset` value.
- Parameter `Length`: The `Length` value.
- Parameter `Encoding`: The `Encoding` value.
- Parameter `Units`: The `Units` value.
- Parameter `Signed`: The `Signed` value.
- Parameter `ByteOrder`: The `ByteOrder` value.
- Parameter `Notes`: The `Notes` value.

###### `M:IoT.Driver.MitsubishiRx.MitsubishiTagDefinition.Equals(IoT.Driver.MitsubishiRx.MitsubishiTagDefinition)`

```csharp
public bool Equals(IoT.Driver.MitsubishiRx.MitsubishiTagDefinition other)
```
Determines whether the supplied value is equal to the current value.

- Parameter `other`: The `other` value.
- Returns: A `bool` result.

###### `M:IoT.Driver.MitsubishiRx.MitsubishiTagDefinition.Equals(System.Object)`

```csharp
public bool Equals(object obj)
```
Determines whether the supplied value is equal to the current value.

- Parameter `obj`: The `obj` value.
- Returns: A `bool` result.

###### `M:IoT.Driver.MitsubishiRx.MitsubishiTagDefinition.GetHashCode`

```csharp
public int GetHashCode()
```
Returns the hash code for the current value.

- Returns: A `int` result.

###### `M:IoT.Driver.MitsubishiRx.MitsubishiTagDefinition.ToString`

```csharp
public string ToString()
```
Returns a string representation of the current value.

- Returns: A `string` result.

###### `M:IoT.Driver.MitsubishiRx.MitsubishiTagDefinition.op_Equality(IoT.Driver.MitsubishiRx.MitsubishiTagDefinition,IoT.Driver.MitsubishiRx.MitsubishiTagDefinition)`

```csharp
public static bool op_Equality(IoT.Driver.MitsubishiRx.MitsubishiTagDefinition left, IoT.Driver.MitsubishiRx.MitsubishiTagDefinition right)
```
Determines whether the two supplied values are equal.

- Parameter `left`: The `left` value.
- Parameter `right`: The `right` value.
- Returns: A `bool` result.

###### `M:IoT.Driver.MitsubishiRx.MitsubishiTagDefinition.op_Inequality(IoT.Driver.MitsubishiRx.MitsubishiTagDefinition,IoT.Driver.MitsubishiRx.MitsubishiTagDefinition)`

```csharp
public static bool op_Inequality(IoT.Driver.MitsubishiRx.MitsubishiTagDefinition left, IoT.Driver.MitsubishiRx.MitsubishiTagDefinition right)
```
Determines whether the two supplied values are not equal.

- Parameter `left`: The `left` value.
- Parameter `right`: The `right` value.
- Returns: A `bool` result.

###### `P:IoT.Driver.MitsubishiRx.MitsubishiTagDefinition.Address`

```csharp
public string Address { get; set; }
```
The Address parameter.

- Value: The `Address` value.

###### `P:IoT.Driver.MitsubishiRx.MitsubishiTagDefinition.ByteOrder`

```csharp
public string ByteOrder { get; set; }
```
The ByteOrder parameter.

- Value: The `ByteOrder` value.

###### `P:IoT.Driver.MitsubishiRx.MitsubishiTagDefinition.DataType`

```csharp
public string DataType { get; set; }
```
The DataType parameter.

- Value: The `DataType` value.

###### `P:IoT.Driver.MitsubishiRx.MitsubishiTagDefinition.Description`

```csharp
public string Description { get; set; }
```
The Description parameter.

- Value: The `Description` value.

###### `P:IoT.Driver.MitsubishiRx.MitsubishiTagDefinition.Encoding`

```csharp
public string Encoding { get; set; }
```
The Encoding parameter.

- Value: The `Encoding` value.

###### `P:IoT.Driver.MitsubishiRx.MitsubishiTagDefinition.Length`

```csharp
public System.Nullable<int> Length { get; set; }
```
The Length parameter.

- Value: The `Length` value.

###### `P:IoT.Driver.MitsubishiRx.MitsubishiTagDefinition.Name`

```csharp
public string Name { get; set; }
```
The Name parameter.

- Value: The `Name` value.

###### `P:IoT.Driver.MitsubishiRx.MitsubishiTagDefinition.Notes`

```csharp
public string Notes { get; set; }
```
The Notes parameter.

- Value: The `Notes` value.

###### `P:IoT.Driver.MitsubishiRx.MitsubishiTagDefinition.Offset`

```csharp
public double Offset { get; set; }
```
The Offset parameter.

- Value: The `Offset` value.

###### `P:IoT.Driver.MitsubishiRx.MitsubishiTagDefinition.Scale`

```csharp
public double Scale { get; set; }
```
The Scale parameter.

- Value: The `Scale` value.

###### `P:IoT.Driver.MitsubishiRx.MitsubishiTagDefinition.Signed`

```csharp
public bool Signed { get; set; }
```
The Signed parameter.

- Value: The `Signed` value.

###### `P:IoT.Driver.MitsubishiRx.MitsubishiTagDefinition.Units`

```csharp
public string Units { get; set; }
```
The Units parameter.

- Value: The `Units` value.

#### `T:IoT.Driver.MitsubishiRx.MitsubishiTagGroupChange`

```csharp
public class IoT.Driver.MitsubishiRx.MitsubishiTagGroupChange
```
Provides the MitsubishiTagGroupChange record.

##### Declared public members

###### `M:IoT.Driver.MitsubishiRx.MitsubishiTagGroupChange.#ctor(System.String,IoT.Driver.MitsubishiRx.MitsubishiTagGroupDefinition,IoT.Driver.MitsubishiRx.MitsubishiTagGroupDefinition)`

```csharp
public IoT.Driver.MitsubishiRx.MitsubishiTagGroupChange(string Name, IoT.Driver.MitsubishiRx.MitsubishiTagGroupDefinition Previous, IoT.Driver.MitsubishiRx.MitsubishiTagGroupDefinition Current)
```
Provides the MitsubishiTagGroupChange record.

- Parameter `Name`: The Name parameter.
- Parameter `Previous`: The Previous parameter.
- Parameter `Current`: The Current parameter.

###### `M:IoT.Driver.MitsubishiRx.MitsubishiTagGroupChange.Deconstruct(System.String@,IoT.Driver.MitsubishiRx.MitsubishiTagGroupDefinition@,IoT.Driver.MitsubishiRx.MitsubishiTagGroupDefinition@)`

```csharp
public void Deconstruct(out string Name, out IoT.Driver.MitsubishiRx.MitsubishiTagGroupDefinition Previous, out IoT.Driver.MitsubishiRx.MitsubishiTagGroupDefinition Current)
```
Deconstructs the value into its component values.

- Parameter `Name`: The `Name` value.
- Parameter `Previous`: The `Previous` value.
- Parameter `Current`: The `Current` value.

###### `M:IoT.Driver.MitsubishiRx.MitsubishiTagGroupChange.Equals(IoT.Driver.MitsubishiRx.MitsubishiTagGroupChange)`

```csharp
public bool Equals(IoT.Driver.MitsubishiRx.MitsubishiTagGroupChange other)
```
Determines whether the supplied value is equal to the current value.

- Parameter `other`: The `other` value.
- Returns: A `bool` result.

###### `M:IoT.Driver.MitsubishiRx.MitsubishiTagGroupChange.Equals(System.Object)`

```csharp
public bool Equals(object obj)
```
Determines whether the supplied value is equal to the current value.

- Parameter `obj`: The `obj` value.
- Returns: A `bool` result.

###### `M:IoT.Driver.MitsubishiRx.MitsubishiTagGroupChange.GetHashCode`

```csharp
public int GetHashCode()
```
Returns the hash code for the current value.

- Returns: A `int` result.

###### `M:IoT.Driver.MitsubishiRx.MitsubishiTagGroupChange.ToString`

```csharp
public string ToString()
```
Returns a string representation of the current value.

- Returns: A `string` result.

###### `M:IoT.Driver.MitsubishiRx.MitsubishiTagGroupChange.op_Equality(IoT.Driver.MitsubishiRx.MitsubishiTagGroupChange,IoT.Driver.MitsubishiRx.MitsubishiTagGroupChange)`

```csharp
public static bool op_Equality(IoT.Driver.MitsubishiRx.MitsubishiTagGroupChange left, IoT.Driver.MitsubishiRx.MitsubishiTagGroupChange right)
```
Determines whether the two supplied values are equal.

- Parameter `left`: The `left` value.
- Parameter `right`: The `right` value.
- Returns: A `bool` result.

###### `M:IoT.Driver.MitsubishiRx.MitsubishiTagGroupChange.op_Inequality(IoT.Driver.MitsubishiRx.MitsubishiTagGroupChange,IoT.Driver.MitsubishiRx.MitsubishiTagGroupChange)`

```csharp
public static bool op_Inequality(IoT.Driver.MitsubishiRx.MitsubishiTagGroupChange left, IoT.Driver.MitsubishiRx.MitsubishiTagGroupChange right)
```
Determines whether the two supplied values are not equal.

- Parameter `left`: The `left` value.
- Parameter `right`: The `right` value.
- Returns: A `bool` result.

###### `P:IoT.Driver.MitsubishiRx.MitsubishiTagGroupChange.ChangeKinds`

```csharp
public IoT.Driver.MitsubishiRx.MitsubishiSchemaChangeKind ChangeKinds { get; }
```
Gets or sets the ChangeKinds property.

- Value: The `ChangeKinds` value.

###### `P:IoT.Driver.MitsubishiRx.MitsubishiTagGroupChange.Current`

```csharp
public IoT.Driver.MitsubishiRx.MitsubishiTagGroupDefinition Current { get; set; }
```
The Current parameter.

- Value: The `Current` value.

###### `P:IoT.Driver.MitsubishiRx.MitsubishiTagGroupChange.Name`

```csharp
public string Name { get; set; }
```
The Name parameter.

- Value: The `Name` value.

###### `P:IoT.Driver.MitsubishiRx.MitsubishiTagGroupChange.Previous`

```csharp
public IoT.Driver.MitsubishiRx.MitsubishiTagGroupDefinition Previous { get; set; }
```
The Previous parameter.

- Value: The `Previous` value.

#### `T:IoT.Driver.MitsubishiRx.MitsubishiTagGroupDefinition`

```csharp
public class IoT.Driver.MitsubishiRx.MitsubishiTagGroupDefinition
```
Provides the MitsubishiTagGroupDefinition record.

##### Declared public members

###### `M:IoT.Driver.MitsubishiRx.MitsubishiTagGroupDefinition.#ctor(System.String,System.Collections.Generic.IReadOnlyList`1{System.String})`

```csharp
public IoT.Driver.MitsubishiRx.MitsubishiTagGroupDefinition(string Name, System.Collections.Generic.IReadOnlyList<string> TagNames)
```
Initializes a new instance of `IoT.Driver.MitsubishiRx.MitsubishiTagGroupDefinition`.

- Parameter `Name`: The `Name` value.
- Parameter `TagNames`: The `TagNames` value.

###### `M:IoT.Driver.MitsubishiRx.MitsubishiTagGroupDefinition.Deconstruct(System.String@,System.Collections.Generic.IReadOnlyList`1{System.String}@)`

```csharp
public void Deconstruct(out string Name, out System.Collections.Generic.IReadOnlyList<string> TagNames)
```
Deconstructs the value into its component values.

- Parameter `Name`: The `Name` value.
- Parameter `TagNames`: The `TagNames` value.

###### `M:IoT.Driver.MitsubishiRx.MitsubishiTagGroupDefinition.Equals(IoT.Driver.MitsubishiRx.MitsubishiTagGroupDefinition)`

```csharp
public bool Equals(IoT.Driver.MitsubishiRx.MitsubishiTagGroupDefinition other)
```
Determines whether the supplied value is equal to the current value.

- Parameter `other`: The `other` value.
- Returns: A `bool` result.

###### `M:IoT.Driver.MitsubishiRx.MitsubishiTagGroupDefinition.Equals(System.Object)`

```csharp
public bool Equals(object obj)
```
Determines whether the supplied value is equal to the current value.

- Parameter `obj`: The `obj` value.
- Returns: A `bool` result.

###### `M:IoT.Driver.MitsubishiRx.MitsubishiTagGroupDefinition.GetHashCode`

```csharp
public int GetHashCode()
```
Returns the hash code for the current value.

- Returns: A `int` result.

###### `M:IoT.Driver.MitsubishiRx.MitsubishiTagGroupDefinition.ToString`

```csharp
public string ToString()
```
Returns a string representation of the current value.

- Returns: A `string` result.

###### `M:IoT.Driver.MitsubishiRx.MitsubishiTagGroupDefinition.op_Equality(IoT.Driver.MitsubishiRx.MitsubishiTagGroupDefinition,IoT.Driver.MitsubishiRx.MitsubishiTagGroupDefinition)`

```csharp
public static bool op_Equality(IoT.Driver.MitsubishiRx.MitsubishiTagGroupDefinition left, IoT.Driver.MitsubishiRx.MitsubishiTagGroupDefinition right)
```
Determines whether the two supplied values are equal.

- Parameter `left`: The `left` value.
- Parameter `right`: The `right` value.
- Returns: A `bool` result.

###### `M:IoT.Driver.MitsubishiRx.MitsubishiTagGroupDefinition.op_Inequality(IoT.Driver.MitsubishiRx.MitsubishiTagGroupDefinition,IoT.Driver.MitsubishiRx.MitsubishiTagGroupDefinition)`

```csharp
public static bool op_Inequality(IoT.Driver.MitsubishiRx.MitsubishiTagGroupDefinition left, IoT.Driver.MitsubishiRx.MitsubishiTagGroupDefinition right)
```
Determines whether the two supplied values are not equal.

- Parameter `left`: The `left` value.
- Parameter `right`: The `right` value.
- Returns: A `bool` result.

###### `P:IoT.Driver.MitsubishiRx.MitsubishiTagGroupDefinition.Name`

```csharp
public string Name { get; set; }
```
The Name parameter.

- Value: The `Name` value.

###### `P:IoT.Driver.MitsubishiRx.MitsubishiTagGroupDefinition.ResolvedTagNames`

```csharp
public System.Collections.Generic.IReadOnlyList<string> ResolvedTagNames { get; }
```
Gets or sets the ResolvedTagNames property.

- Value: The `ResolvedTagNames` value.

###### `P:IoT.Driver.MitsubishiRx.MitsubishiTagGroupDefinition.TagNames`

```csharp
public System.Collections.Generic.IReadOnlyList<string> TagNames { get; set; }
```
The TagNames parameter.

- Value: The `TagNames` value.

#### `T:IoT.Driver.MitsubishiRx.MitsubishiTagGroupSnapshot`

```csharp
public class IoT.Driver.MitsubishiRx.MitsubishiTagGroupSnapshot
```
Provides the MitsubishiTagGroupSnapshot record.

##### Declared public members

###### `M:IoT.Driver.MitsubishiRx.MitsubishiTagGroupSnapshot.#ctor(System.String,System.Collections.Generic.IReadOnlyDictionary`2{System.String,System.Object})`

```csharp
public IoT.Driver.MitsubishiRx.MitsubishiTagGroupSnapshot(string GroupName, System.Collections.Generic.IReadOnlyDictionary<string, object> Values)
```
Initializes a new instance of `IoT.Driver.MitsubishiRx.MitsubishiTagGroupSnapshot`.

- Parameter `GroupName`: The `GroupName` value.
- Parameter `Values`: The `Values` value.

###### `M:IoT.Driver.MitsubishiRx.MitsubishiTagGroupSnapshot.Deconstruct(System.String@,System.Collections.Generic.IReadOnlyDictionary`2{System.String,System.Object}@)`

```csharp
public void Deconstruct(out string GroupName, out System.Collections.Generic.IReadOnlyDictionary<string, object> Values)
```
Deconstructs the value into its component values.

- Parameter `GroupName`: The `GroupName` value.
- Parameter `Values`: The `Values` value.

###### `M:IoT.Driver.MitsubishiRx.MitsubishiTagGroupSnapshot.Equals(IoT.Driver.MitsubishiRx.MitsubishiTagGroupSnapshot)`

```csharp
public bool Equals(IoT.Driver.MitsubishiRx.MitsubishiTagGroupSnapshot other)
```
Determines whether the supplied value is equal to the current value.

- Parameter `other`: The `other` value.
- Returns: A `bool` result.

###### `M:IoT.Driver.MitsubishiRx.MitsubishiTagGroupSnapshot.Equals(System.Object)`

```csharp
public bool Equals(object obj)
```
Determines whether the supplied value is equal to the current value.

- Parameter `obj`: The `obj` value.
- Returns: A `bool` result.

###### `M:IoT.Driver.MitsubishiRx.MitsubishiTagGroupSnapshot.GetHashCode`

```csharp
public int GetHashCode()
```
Returns the hash code for the current value.

- Returns: A `int` result.

###### `M:IoT.Driver.MitsubishiRx.MitsubishiTagGroupSnapshot.GetOptional``1(IoT.Driver.Core.LogicalTagKey`1{``0})`

```csharp
public T GetOptional<T>(IoT.Driver.Core.LogicalTagKey<T> tag)
```
Executes the `GetOptional` operation.

- Parameter `tag`: The `tag` value.
- Returns: A `T` result.

###### `M:IoT.Driver.MitsubishiRx.MitsubishiTagGroupSnapshot.GetRequired``1(IoT.Driver.Core.LogicalTagKey`1{``0})`

```csharp
public T GetRequired<T>(IoT.Driver.Core.LogicalTagKey<T> tag)
```
Executes the `GetRequired` operation.

- Parameter `tag`: The `tag` value.
- Returns: A `T` result.

###### `M:IoT.Driver.MitsubishiRx.MitsubishiTagGroupSnapshot.ToString`

```csharp
public string ToString()
```
Returns a string representation of the current value.

- Returns: A `string` result.

###### `M:IoT.Driver.MitsubishiRx.MitsubishiTagGroupSnapshot.op_Equality(IoT.Driver.MitsubishiRx.MitsubishiTagGroupSnapshot,IoT.Driver.MitsubishiRx.MitsubishiTagGroupSnapshot)`

```csharp
public static bool op_Equality(IoT.Driver.MitsubishiRx.MitsubishiTagGroupSnapshot left, IoT.Driver.MitsubishiRx.MitsubishiTagGroupSnapshot right)
```
Determines whether the two supplied values are equal.

- Parameter `left`: The `left` value.
- Parameter `right`: The `right` value.
- Returns: A `bool` result.

###### `M:IoT.Driver.MitsubishiRx.MitsubishiTagGroupSnapshot.op_Inequality(IoT.Driver.MitsubishiRx.MitsubishiTagGroupSnapshot,IoT.Driver.MitsubishiRx.MitsubishiTagGroupSnapshot)`

```csharp
public static bool op_Inequality(IoT.Driver.MitsubishiRx.MitsubishiTagGroupSnapshot left, IoT.Driver.MitsubishiRx.MitsubishiTagGroupSnapshot right)
```
Determines whether the two supplied values are not equal.

- Parameter `left`: The `left` value.
- Parameter `right`: The `right` value.
- Returns: A `bool` result.

###### `P:IoT.Driver.MitsubishiRx.MitsubishiTagGroupSnapshot.GroupName`

```csharp
public string GroupName { get; set; }
```
The GroupName parameter.

- Value: The `GroupName` value.

###### `P:IoT.Driver.MitsubishiRx.MitsubishiTagGroupSnapshot.TagNames`

```csharp
public System.Collections.Generic.IReadOnlyList<string> TagNames { get; }
```
Gets or sets the TagNames property.

- Value: The `TagNames` value.

###### `P:IoT.Driver.MitsubishiRx.MitsubishiTagGroupSnapshot.Values`

```csharp
public System.Collections.Generic.IReadOnlyDictionary<string, object> Values { get; set; }
```
The Values parameter.

- Value: The `Values` value.

#### `T:IoT.Driver.MitsubishiRx.MitsubishiTagRolloutPolicy`

```csharp
public enum IoT.Driver.MitsubishiRx.MitsubishiTagRolloutPolicy
```
Defines the MitsubishiTagRolloutPolicy values.

##### Declared public members

###### `F:IoT.Driver.MitsubishiRx.MitsubishiTagRolloutPolicy.AllowAll`

```csharp
public static const IoT.Driver.MitsubishiRx.MitsubishiTagRolloutPolicy AllowAll
```
Represents the AllowAll option.

###### `F:IoT.Driver.MitsubishiRx.MitsubishiTagRolloutPolicy.SafeMetadataAndGroups`

```csharp
public static const IoT.Driver.MitsubishiRx.MitsubishiTagRolloutPolicy SafeMetadataAndGroups
```
Represents the SafeMetadataAndGroups option.

#### `T:IoT.Driver.MitsubishiRx.MitsubishiTagValueConverter`

```csharp
public class IoT.Driver.MitsubishiRx.MitsubishiTagValueConverter
```
Converts dynamically read tag values to declared tag types.

##### Declared public members

###### `M:IoT.Driver.MitsubishiRx.MitsubishiTagValueConverter.Require``1(System.Object,IoT.Driver.Core.LogicalTagKey`1{``0})`

```csharp
public static T Require<T>(object value, IoT.Driver.Core.LogicalTagKey<T> tag)
```
Executes the `Require` operation.

- Parameter `value`: The `value` value.
- Parameter `tag`: The `tag` value.
- Returns: A `T` result.

#### `T:IoT.Driver.MitsubishiRx.MitsubishiTransportKind`

```csharp
public enum IoT.Driver.MitsubishiRx.MitsubishiTransportKind
```
Defines the MitsubishiTransportKind values.

##### Declared public members

###### `F:IoT.Driver.MitsubishiRx.MitsubishiTransportKind.Serial`

```csharp
public static const IoT.Driver.MitsubishiRx.MitsubishiTransportKind Serial
```
Represents the Serial option.

###### `F:IoT.Driver.MitsubishiRx.MitsubishiTransportKind.Tcp`

```csharp
public static const IoT.Driver.MitsubishiRx.MitsubishiTransportKind Tcp
```
Represents the Tcp option.

###### `F:IoT.Driver.MitsubishiRx.MitsubishiTransportKind.Udp`

```csharp
public static const IoT.Driver.MitsubishiRx.MitsubishiTransportKind Udp
```
Represents the Udp option.

#### `T:IoT.Driver.MitsubishiRx.MitsubishiTransportRequest`

```csharp
public class IoT.Driver.MitsubishiRx.MitsubishiTransportRequest
```
Provides the MitsubishiTransportRequest record.

##### Declared public members

###### `M:IoT.Driver.MitsubishiRx.MitsubishiTransportRequest.#ctor(System.Byte[],System.Nullable`1{System.Int32},System.String)`

```csharp
public IoT.Driver.MitsubishiRx.MitsubishiTransportRequest(byte[] Payload, System.Nullable<int> ExpectedResponseLength, string Description)
```
Initializes a new instance of `IoT.Driver.MitsubishiRx.MitsubishiTransportRequest`.

- Parameter `Payload`: The `Payload` value.
- Parameter `ExpectedResponseLength`: The `ExpectedResponseLength` value.
- Parameter `Description`: The `Description` value.

###### `M:IoT.Driver.MitsubishiRx.MitsubishiTransportRequest.Deconstruct(System.Byte[]@,System.Nullable`1{System.Int32}@,System.String@)`

```csharp
public void Deconstruct(out byte[] Payload, out System.Nullable<int> ExpectedResponseLength, out string Description)
```
Deconstructs the value into its component values.

- Parameter `Payload`: The `Payload` value.
- Parameter `ExpectedResponseLength`: The `ExpectedResponseLength` value.
- Parameter `Description`: The `Description` value.

###### `M:IoT.Driver.MitsubishiRx.MitsubishiTransportRequest.Equals(IoT.Driver.MitsubishiRx.MitsubishiTransportRequest)`

```csharp
public bool Equals(IoT.Driver.MitsubishiRx.MitsubishiTransportRequest other)
```
Determines whether the supplied value is equal to the current value.

- Parameter `other`: The `other` value.
- Returns: A `bool` result.

###### `M:IoT.Driver.MitsubishiRx.MitsubishiTransportRequest.Equals(System.Object)`

```csharp
public bool Equals(object obj)
```
Determines whether the supplied value is equal to the current value.

- Parameter `obj`: The `obj` value.
- Returns: A `bool` result.

###### `M:IoT.Driver.MitsubishiRx.MitsubishiTransportRequest.GetHashCode`

```csharp
public int GetHashCode()
```
Returns the hash code for the current value.

- Returns: A `int` result.

###### `M:IoT.Driver.MitsubishiRx.MitsubishiTransportRequest.ToString`

```csharp
public string ToString()
```
Returns a string representation of the current value.

- Returns: A `string` result.

###### `M:IoT.Driver.MitsubishiRx.MitsubishiTransportRequest.op_Equality(IoT.Driver.MitsubishiRx.MitsubishiTransportRequest,IoT.Driver.MitsubishiRx.MitsubishiTransportRequest)`

```csharp
public static bool op_Equality(IoT.Driver.MitsubishiRx.MitsubishiTransportRequest left, IoT.Driver.MitsubishiRx.MitsubishiTransportRequest right)
```
Determines whether the two supplied values are equal.

- Parameter `left`: The `left` value.
- Parameter `right`: The `right` value.
- Returns: A `bool` result.

###### `M:IoT.Driver.MitsubishiRx.MitsubishiTransportRequest.op_Inequality(IoT.Driver.MitsubishiRx.MitsubishiTransportRequest,IoT.Driver.MitsubishiRx.MitsubishiTransportRequest)`

```csharp
public static bool op_Inequality(IoT.Driver.MitsubishiRx.MitsubishiTransportRequest left, IoT.Driver.MitsubishiRx.MitsubishiTransportRequest right)
```
Determines whether the two supplied values are not equal.

- Parameter `left`: The `left` value.
- Parameter `right`: The `right` value.
- Returns: A `bool` result.

###### `P:IoT.Driver.MitsubishiRx.MitsubishiTransportRequest.Description`

```csharp
public string Description { get; set; }
```
The Description parameter.

- Value: The `Description` value.

###### `P:IoT.Driver.MitsubishiRx.MitsubishiTransportRequest.ExpectedResponseLength`

```csharp
public System.Nullable<int> ExpectedResponseLength { get; set; }
```
The ExpectedResponseLength parameter.

- Value: The `ExpectedResponseLength` value.

###### `P:IoT.Driver.MitsubishiRx.MitsubishiTransportRequest.Payload`

```csharp
public byte[] Payload { get; set; }
```
The Payload parameter.

- Value: The `Payload` value.

#### `T:IoT.Driver.MitsubishiRx.MitsubishiTypeName`

```csharp
public class IoT.Driver.MitsubishiRx.MitsubishiTypeName
```
Provides the MitsubishiTypeName record.

##### Declared public members

###### `M:IoT.Driver.MitsubishiRx.MitsubishiTypeName.#ctor(System.String,System.UInt16)`

```csharp
public IoT.Driver.MitsubishiRx.MitsubishiTypeName(string ModelName, ushort ModelCode)
```
Provides the MitsubishiTypeName record.

- Parameter `ModelName`: The ModelName parameter.
- Parameter `ModelCode`: The ModelCode parameter.

###### `M:IoT.Driver.MitsubishiRx.MitsubishiTypeName.Deconstruct(System.String@,System.UInt16@)`

```csharp
public void Deconstruct(out string ModelName, out ushort ModelCode)
```
Deconstructs the value into its component values.

- Parameter `ModelName`: The `ModelName` value.
- Parameter `ModelCode`: The `ModelCode` value.

###### `M:IoT.Driver.MitsubishiRx.MitsubishiTypeName.Equals(IoT.Driver.MitsubishiRx.MitsubishiTypeName)`

```csharp
public bool Equals(IoT.Driver.MitsubishiRx.MitsubishiTypeName other)
```
Determines whether the supplied value is equal to the current value.

- Parameter `other`: The `other` value.
- Returns: A `bool` result.

###### `M:IoT.Driver.MitsubishiRx.MitsubishiTypeName.Equals(System.Object)`

```csharp
public bool Equals(object obj)
```
Determines whether the supplied value is equal to the current value.

- Parameter `obj`: The `obj` value.
- Returns: A `bool` result.

###### `M:IoT.Driver.MitsubishiRx.MitsubishiTypeName.GetHashCode`

```csharp
public int GetHashCode()
```
Returns the hash code for the current value.

- Returns: A `int` result.

###### `M:IoT.Driver.MitsubishiRx.MitsubishiTypeName.ToString`

```csharp
public string ToString()
```
Returns a string representation of the current value.

- Returns: A `string` result.

###### `M:IoT.Driver.MitsubishiRx.MitsubishiTypeName.op_Equality(IoT.Driver.MitsubishiRx.MitsubishiTypeName,IoT.Driver.MitsubishiRx.MitsubishiTypeName)`

```csharp
public static bool op_Equality(IoT.Driver.MitsubishiRx.MitsubishiTypeName left, IoT.Driver.MitsubishiRx.MitsubishiTypeName right)
```
Determines whether the two supplied values are equal.

- Parameter `left`: The `left` value.
- Parameter `right`: The `right` value.
- Returns: A `bool` result.

###### `M:IoT.Driver.MitsubishiRx.MitsubishiTypeName.op_Inequality(IoT.Driver.MitsubishiRx.MitsubishiTypeName,IoT.Driver.MitsubishiRx.MitsubishiTypeName)`

```csharp
public static bool op_Inequality(IoT.Driver.MitsubishiRx.MitsubishiTypeName left, IoT.Driver.MitsubishiRx.MitsubishiTypeName right)
```
Determines whether the two supplied values are not equal.

- Parameter `left`: The `left` value.
- Parameter `right`: The `right` value.
- Returns: A `bool` result.

###### `P:IoT.Driver.MitsubishiRx.MitsubishiTypeName.ModelCode`

```csharp
public ushort ModelCode { get; set; }
```
The ModelCode parameter.

- Value: The `ModelCode` value.

###### `P:IoT.Driver.MitsubishiRx.MitsubishiTypeName.ModelName`

```csharp
public string ModelName { get; set; }
```
The ModelName parameter.

- Value: The `ModelName` value.

#### `T:IoT.Driver.MitsubishiRx.MitsubishiWordBlock`

```csharp
public class IoT.Driver.MitsubishiRx.MitsubishiWordBlock
```
Provides the MitsubishiWordBlock record.

##### Declared public members

###### `M:IoT.Driver.MitsubishiRx.MitsubishiWordBlock.#ctor(IoT.Driver.MitsubishiRx.MitsubishiDeviceAddress,System.ReadOnlyMemory`1{System.UInt16})`

```csharp
public IoT.Driver.MitsubishiRx.MitsubishiWordBlock(IoT.Driver.MitsubishiRx.MitsubishiDeviceAddress Address, System.ReadOnlyMemory<ushort> Values)
```
Initializes a new instance of `IoT.Driver.MitsubishiRx.MitsubishiWordBlock`.

- Parameter `Address`: The `Address` value.
- Parameter `Values`: The `Values` value.

###### `M:IoT.Driver.MitsubishiRx.MitsubishiWordBlock.Deconstruct(IoT.Driver.MitsubishiRx.MitsubishiDeviceAddress@,System.ReadOnlyMemory`1{System.UInt16}@)`

```csharp
public void Deconstruct(out IoT.Driver.MitsubishiRx.MitsubishiDeviceAddress Address, out System.ReadOnlyMemory<ushort> Values)
```
Deconstructs the value into its component values.

- Parameter `Address`: The `Address` value.
- Parameter `Values`: The `Values` value.

###### `M:IoT.Driver.MitsubishiRx.MitsubishiWordBlock.Equals(IoT.Driver.MitsubishiRx.MitsubishiWordBlock)`

```csharp
public bool Equals(IoT.Driver.MitsubishiRx.MitsubishiWordBlock other)
```
Determines whether the supplied value is equal to the current value.

- Parameter `other`: The `other` value.
- Returns: A `bool` result.

###### `M:IoT.Driver.MitsubishiRx.MitsubishiWordBlock.Equals(System.Object)`

```csharp
public bool Equals(object obj)
```
Determines whether the supplied value is equal to the current value.

- Parameter `obj`: The `obj` value.
- Returns: A `bool` result.

###### `M:IoT.Driver.MitsubishiRx.MitsubishiWordBlock.GetHashCode`

```csharp
public int GetHashCode()
```
Returns the hash code for the current value.

- Returns: A `int` result.

###### `M:IoT.Driver.MitsubishiRx.MitsubishiWordBlock.ToString`

```csharp
public string ToString()
```
Returns a string representation of the current value.

- Returns: A `string` result.

###### `M:IoT.Driver.MitsubishiRx.MitsubishiWordBlock.op_Equality(IoT.Driver.MitsubishiRx.MitsubishiWordBlock,IoT.Driver.MitsubishiRx.MitsubishiWordBlock)`

```csharp
public static bool op_Equality(IoT.Driver.MitsubishiRx.MitsubishiWordBlock left, IoT.Driver.MitsubishiRx.MitsubishiWordBlock right)
```
Determines whether the two supplied values are equal.

- Parameter `left`: The `left` value.
- Parameter `right`: The `right` value.
- Returns: A `bool` result.

###### `M:IoT.Driver.MitsubishiRx.MitsubishiWordBlock.op_Inequality(IoT.Driver.MitsubishiRx.MitsubishiWordBlock,IoT.Driver.MitsubishiRx.MitsubishiWordBlock)`

```csharp
public static bool op_Inequality(IoT.Driver.MitsubishiRx.MitsubishiWordBlock left, IoT.Driver.MitsubishiRx.MitsubishiWordBlock right)
```
Determines whether the two supplied values are not equal.

- Parameter `left`: The `left` value.
- Parameter `right`: The `right` value.
- Returns: A `bool` result.

###### `P:IoT.Driver.MitsubishiRx.MitsubishiWordBlock.Address`

```csharp
public IoT.Driver.MitsubishiRx.MitsubishiDeviceAddress Address { get; set; }
```
The Address parameter.

- Value: The `Address` value.

###### `P:IoT.Driver.MitsubishiRx.MitsubishiWordBlock.Values`

```csharp
public System.ReadOnlyMemory<ushort> Values { get; set; }
```
The Values parameter.

- Value: The `Values` value.

#### `T:IoT.Driver.MitsubishiRx.Responce`

```csharp
public class IoT.Driver.MitsubishiRx.Responce
```
Provides the Responce type.

##### Declared public members

###### `M:IoT.Driver.MitsubishiRx.Responce.#ctor`

```csharp
public IoT.Driver.MitsubishiRx.Responce()
```
Initializes a new instance of the `T:IoT.Driver.MitsubishiRx.Responce` class using `P:System.TimeProvider.System` .

###### `M:IoT.Driver.MitsubishiRx.Responce.#ctor(System.TimeProvider)`

```csharp
public IoT.Driver.MitsubishiRx.Responce(System.TimeProvider timeProvider)
```
Initializes a new instance of the `T:IoT.Driver.MitsubishiRx.Responce` class.

- Parameter `timeProvider`: The time provider used to stamp `P:IoT.Driver.MitsubishiRx.Responce.InitialTime` .

###### `M:IoT.Driver.MitsubishiRx.Responce.AddErr2List`

```csharp
public void AddErr2List()
```
Executes the AddErr2List operation.

###### `M:IoT.Driver.MitsubishiRx.Responce.SetErrInfo(IoT.Driver.MitsubishiRx.Responce)`

```csharp
public IoT.Driver.MitsubishiRx.Responce SetErrInfo(IoT.Driver.MitsubishiRx.Responce result)
```
Executes the SetErrInfo operation.

- Parameter `result`: The result parameter.
- Returns: The SetErrInfo operation result.

###### `P:IoT.Driver.MitsubishiRx.Responce.Err`

```csharp
public string Err { get; set; }
```
Gets or sets the Err property.

- Value: The `Err` value.

###### `P:IoT.Driver.MitsubishiRx.Responce.ErrCode`

```csharp
public int ErrCode { get; set; }
```
Gets or sets the ErrCode property.

- Value: The `ErrCode` value.

###### `P:IoT.Driver.MitsubishiRx.Responce.ErrList`

```csharp
public System.Collections.Generic.List<string> ErrList { get; }
```
Gets or sets the ErrList property.

- Value: The `ErrList` value.

###### `P:IoT.Driver.MitsubishiRx.Responce.Exception`

```csharp
public System.Exception Exception { get; set; }
```
Gets or sets the Exception property.

- Value: The `Exception` value.

###### `P:IoT.Driver.MitsubishiRx.Responce.InitialTime`

```csharp
public System.DateTimeOffset InitialTime { get; }
```
Gets the InitialTime property.

- Value: The `InitialTime` value.

###### `P:IoT.Driver.MitsubishiRx.Responce.IsSucceed`

```csharp
public bool IsSucceed { get; set; }
```
Gets or sets the IsSucceed property.

- Value: The `IsSucceed` value.

###### `P:IoT.Driver.MitsubishiRx.Responce.Request`

```csharp
public string Request { get; set; }
```
Gets or sets the Request property.

- Value: The `Request` value.

###### `P:IoT.Driver.MitsubishiRx.Responce.Request2`

```csharp
public string Request2 { get; set; }
```
Gets or sets the Request2 property.

- Value: The `Request2` value.

###### `P:IoT.Driver.MitsubishiRx.Responce.Response`

```csharp
public string Response { get; set; }
```
Gets or sets the Response property.

- Value: The `Response` value.

###### `P:IoT.Driver.MitsubishiRx.Responce.Response2`

```csharp
public string Response2 { get; set; }
```
Gets or sets the Response2 property.

- Value: The `Response2` value.

###### `P:IoT.Driver.MitsubishiRx.Responce.TimeConsuming`

```csharp
public System.Nullable<double> TimeConsuming { get; }
```
Gets the TimeConsuming property.

- Value: The `TimeConsuming` value.

#### `T:IoT.Driver.MitsubishiRx.Responce`1`

```csharp
public class IoT.Driver.MitsubishiRx.Responce`1
```
Provides the Responce type.

##### Declared public members

###### `M:IoT.Driver.MitsubishiRx.Responce`1.#ctor`

```csharp
public IoT.Driver.MitsubishiRx.Responce<T>()
```
Initializes a new instance of the Responce class.

###### `M:IoT.Driver.MitsubishiRx.Responce`1.#ctor(IoT.Driver.MitsubishiRx.Responce)`

```csharp
public IoT.Driver.MitsubishiRx.Responce<T>(IoT.Driver.MitsubishiRx.Responce result)
```
Initializes a new instance of the Responce class.

- Parameter `result`: The result parameter.

###### `M:IoT.Driver.MitsubishiRx.Responce`1.#ctor(IoT.Driver.MitsubishiRx.Responce,`0)`

```csharp
public IoT.Driver.MitsubishiRx.Responce<T>(IoT.Driver.MitsubishiRx.Responce result, T data)
```
Initializes a new instance of the Responce class.

- Parameter `result`: The result parameter.
- Parameter `data`: The data parameter.

###### `M:IoT.Driver.MitsubishiRx.Responce`1.#ctor(`0)`

```csharp
public IoT.Driver.MitsubishiRx.Responce<T>(T data)
```
Initializes a new instance of the Responce class.

- Parameter `data`: The data parameter.

###### `M:IoT.Driver.MitsubishiRx.Responce`1.SetErrInfo(IoT.Driver.MitsubishiRx.Responce)`

```csharp
public IoT.Driver.MitsubishiRx.Responce<T> SetErrInfo(IoT.Driver.MitsubishiRx.Responce result)
```
Executes the SetErrInfo operation.

- Parameter `result`: The result parameter.
- Returns: The SetErrInfo operation result.

###### `P:IoT.Driver.MitsubishiRx.Responce`1.Value`

```csharp
public T Value { get; set; }
```
Gets or sets the Value property.

- Value: The `Value` value.

#### `T:IoT.Driver.MitsubishiRx.XyAddressNotation`

```csharp
public enum IoT.Driver.MitsubishiRx.XyAddressNotation
```
Defines the XyAddressNotation values.

##### Declared public members

###### `F:IoT.Driver.MitsubishiRx.XyAddressNotation.Hexadecimal`

```csharp
public static const IoT.Driver.MitsubishiRx.XyAddressNotation Hexadecimal
```
Represents the Hexadecimal option.

###### `F:IoT.Driver.MitsubishiRx.XyAddressNotation.Octal`

```csharp
public static const IoT.Driver.MitsubishiRx.XyAddressNotation Octal
```
Represents the Octal option.

<!-- END GENERATED PUBLIC API -->
