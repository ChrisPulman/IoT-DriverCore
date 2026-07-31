---
name: cp-iot-core
description: Design, implement, review, or test protocol-neutral logical-tag workflows with IoT-Driver.Core, including catalogues, typed keys, operation results, observation, persistence, batching, and simulation.
---

# IoT-Driver.Core

## Use this skill when

Use this skill when application code must work with logical PLC names independently of a specific wire protocol, share tag definitions across several drivers, persist a catalogue, plan adjacent transfers, or run deterministic tests without hardware.

Read the co-packaged `../../README.md` for examples. In a repository checkout, use `packagereadme/CP.IoT.Core/README.md` and inspect `src/CP.IoT.Core` before describing members not covered there.

## Package and namespace

- Install `IoT-Driver.Core`.
- Import `IoT.Driver.Core`.
- Protocol runtime packages reference these contracts and expose or compose `ILogicalTagClient`.
- Keep protocol address parsing, byte order, conversion, and transport retries in the protocol adapter.

## Model a tag

Create an immutable `LogicalTag(name, address, dataType[, options])`. The name is application-owned; the address and data-type strings must match the selected protocol adapter.

Use `LogicalTagOptions` for `GroupName`, `Description`, `Metadata`, `AccessMode`, and `ScanInterval`. Required strings are validated, access modes must be defined, and scan intervals must be positive.

Use `LogicalTagKey<T>` whenever the application knows the expected CLR type:

```csharp
using IoT.Driver.Core;

var definition = new LogicalTag(
    "Line.Speed",
    "DB20.DBD0",
    "Single",
    new LogicalTagOptions
    {
        AccessMode = LogicalTagAccessMode.ReadWrite,
        ScanInterval = TimeSpan.FromMilliseconds(250),
    });

var speed = new LogicalTagKey<float>(definition);
```

Treat definitions as immutable. Use `WithAddress`, `WithDataType`, or `WithOptions`, then update the catalogue.

## Catalogue and setup

`LogicalTagCatalog` is a thread-safe `ILogicalTagCatalog`. Use `TryAdd`, `Upsert`, `TryGet`, `TryRemove`, and `List`; subscribe to `Changed` when application state must follow catalogue edits. Dispose the catalogue when its owner stops.

Drivers that implement `IManagedLogicalTagClient` also implement `ILogicalTagSetup`: register tags through their setup surface instead of maintaining an unrelated second catalogue.

## Read, write, and observe

`ILogicalTagClient` combines `ILogicalTagReader`, `ILogicalTagWriter`, and `ILogicalTagObserver`.

- Call typed `ReadAsync<T>(LogicalTagKey<T>, CancellationToken)` and `WriteAsync<T>(LogicalTagKey<T>, T, CancellationToken)` when possible.
- Use `ReadManyAsync`, `WriteManyAsync`, `ReadAllAsync`, and `WriteAllAsync` for batches supported by the adapter.
- Check `TagOperationResult<T>.Succeeded` before using `Value`; log or surface `Error` on failure.
- Use `Observe`/`ObserveMany` for `IObservable<LogicalTagValue>`.
- Use `ObserveAsync`/`ObserveManyAsync` for cancellation-aware `IAsyncEnumerable<LogicalTagValue>`.
- Dispose subscriptions and cancel async enumeration with the owning component.

Expected PLC, conversion, access, and transport failures may be returned as failed results. Invalid arguments and disposed-object usage may still throw.

## Persistence and definition exchange

Use `LogicalTagSqliteStore` to get, list, upsert, edit, delete, and load tag and group definitions. Pass cancellation tokens to persistence work. Use `LoadCatalogAsync` to reconstruct an in-memory catalogue.

Use `LogicalTagCsv.ExportAsync` and `ImportAsync` for reviewable definition exchange. Treat imported files as untrusted configuration: validate names, addresses, types, access permissions, and scan rates before deployment.

## Transfer planning

`TagTransferPlanner` does not parse PLC syntax. Give it `TagTransferRequest` values containing adapter-produced `TagTransportAddress` instances.

Set `TagTransferCapabilities.MaximumRangeLength` and `MaximumItemsPerRange` to actual protocol limits. Partition by transport, memory area, encoding, access direction, and route so incompatible operations cannot be coalesced. Preserve the plan's input indexes when mapping results back to callers.

## Simulation workflow

Compose a `SimulatorMemoryImage`, typed `SimulatorTagBinding.Create<T>` entries, a `TagTransferPlanner`, a `SimulatorLogicalTagClient`, and optionally a `ManualSimulatorClock` and `SimulatorScript`.

Test single and batch reads/writes, access-mode rejection, observations, latency, injected errors, and caller cancellation. Use a safe PLC or test rig only after the simulator workflow is green.

## Safety checklist

- Never assume that identical logical names imply identical protocol addresses.
- Verify write access, machine interlocks, scaling, endianness, string lengths, and array bounds.
- Keep cancellation and disposal ownership explicit.
- Batch only compatible addresses within adapter limits.
- Persist definitions outside active I/O transactions and review changes before rollout.
- Prefer composition through `ILogicalTagClient` over inheritance between concrete protocol clients.
