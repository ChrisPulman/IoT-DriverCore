<p align="center">
  <img src="https://github.com/ChrisPulman/IoT-DriverCore/blob/main/images/cp-iot-core.png" alt="CP.IoT.Core package logo" width="320" />
</p>

# IoT-Driver.Core

## Overview

`IoT-Driver.Core` (project and assembly `CP.IoT.Core`) is the protocol-neutral composition layer shared by the IoT-DriverCore PLC drivers. It gives applications stable logical names while each protocol adapter remains responsible for physical addresses, value conversion, batching, and transport failures.

Use it to define logical tags once, compose clients from different PLC families, persist definitions, plan contiguous transfers, or test application behavior without PLC hardware.

## Install

```bash
dotnet add package IoT-Driver.Core
```

The public namespace is `IoT.Driver.Core`.

## Core model

- `LogicalTag` is an immutable definition containing a logical name, protocol address, data type, access mode, optional scan interval, group, description, and metadata.
- `LogicalTagKey<T>` adds compile-time value typing to a logical name.
- `LogicalTagCatalog` is a thread-safe in-memory catalogue with add, update, remove, list, and change notification operations.
- `ILogicalTagClient` composes the read, write, observable, and async-observable contracts implemented by protocol adapters.
- `TagOperationResult<T>` reports expected operation failures through `Succeeded`, `Value`, and `Error`.
- `LogicalTagSqliteStore` persists tag and group definitions and can reconstruct a catalogue.
- `TagTransferPlanner` groups compatible adjacent or overlapping addresses within protocol limits.
- `SimulatorLogicalTagClient`, `SimulatorMemoryImage`, clocks, and scripts provide deterministic hardware-free execution.

## Define and catalogue tags

```csharp
using IoT.Driver.Core;

using var catalog = new LogicalTagCatalog();

var temperature = new LogicalTag(
    "Reactor.Temperature",
    "DB10.DBD0",
    "Single",
    new LogicalTagOptions
    {
        GroupName = "Reactor",
        Description = "Process temperature",
        AccessMode = LogicalTagAccessMode.Read,
        ScanInterval = TimeSpan.FromSeconds(1),
    });

catalog.Upsert(temperature);

var temperatureKey = new LogicalTagKey<float>(temperature);
```

The address string remains protocol-specific. Create it with the syntax expected by the selected driver; the core library deliberately does not reinterpret it.

## Read and write through a driver

Protocol packages expose or compose an `ILogicalTagClient`. Typed extension methods preserve the logical key type:

```csharp
using IoT.Driver.Core;

static async Task<float> ReadTemperatureAsync(
    ILogicalTagClient client,
    LogicalTagKey<float> key,
    CancellationToken cancellationToken)
{
    var result = await client.ReadAsync(key, cancellationToken);
    if (!result.Succeeded || result.Value is null)
    {
        throw new InvalidOperationException(result.Error);
    }

    return result.Value;
}
```

Inspect every result before using its value. Expected PLC, address, conversion, and transport failures are returned as unsuccessful results; argument and lifecycle errors may still throw.

## Observe changes

`ILogicalTagObserver` supports both `IObservable<LogicalTagValue>` and cancellation-aware `IAsyncEnumerable<LogicalTagValue>`:

```csharp
await foreach (var change in client.ObserveAsync(
    temperatureKey.Name,
    cancellationToken))
{
    Console.WriteLine($"{change.TagName}: {change.Value} at {change.TimestampUtc:O}");
}
```

Dispose observable subscriptions and cancel async enumeration when the owning component stops.

## Persist definitions

```csharp
var store = new LogicalTagSqliteStore("Data Source=logical-tags.db");
await store.UpsertTagAsync(temperature, cancellationToken);

using var restoredCatalog = await store.LoadCatalogAsync(cancellationToken);
```

`LogicalTagCsv` provides import and export when definitions must be reviewed or exchanged as text. Validate imported addresses against the target protocol before connecting to equipment.

## Batch planning

`TagTransferPlanner` consumes addresses already parsed by a protocol adapter. It preserves caller result positions while coalescing compatible ranges subject to `TagTransferCapabilities`.

```csharp
var planner = new TagTransferPlanner(
    new TagTransferCapabilities(maximumRangeLength: 120, maximumItemsPerRange: 32));
```

Adapters should use distinct transport partitions, memory areas, encodings, access directions, and routes so the planner never combines incompatible requests.

## Simulation and testing

Use `SimulatorLogicalTagClient` with a `SimulatorMemoryImage`, typed `SimulatorTagBinding` instances, a transfer planner, and optionally a `ManualSimulatorClock` or `SimulatorScript`. This exercises the same logical read, write, batch, and observation contracts without opening a network or serial connection.

Prefer the simulator for unit and integration tests. Before production use, validate the final tag catalogue, write permissions, byte ordering, ranges, and failure handling on a safe test rig.

## Lifetime and cancellation

- Pass a meaningful `CancellationToken` to I/O, persistence, and async observation operations.
- Dispose `LogicalTagCatalog` and every subscription owned by the application.
- Treat tag definitions as immutable; use `WithAddress`, `WithDataType`, or `WithOptions`, then update the catalogue.
- Keep protocol-specific address parsing and codecs in the protocol adapter rather than in application-domain models.

## Agent skill

The package includes the detailed `skills/cp-iot-core/SKILL.md` guide for coding agents. The complete repository documentation is available at [IoT-DriverCore](https://github.com/ChrisPulman/IoT-DriverCore).
