<p align="center">
  <img src="https://raw.githubusercontent.com/ChrisPulman/IoT-DriverCore/main/images/modbus-rx.png" alt="ModbusRx package logo" width="320" />
</p>

# ModbusRx

## Overview

`ModbusRx` is an asynchronous Modbus master, slave/server, simulation, logical-tag, and observable toolkit. It supports Modbus TCP, UDP, RTU, and ASCII through SerialPortRx adapters and exposes core function codes 1, 2, 3, 4, 5, 6, 8, 15, 16, and 23.

## Safety

Modbus has no intrinsic authorization and write functions can alter a live process. Segment the control network, allow-list endpoints/unit IDs, validate zero-based addresses against the device map, use least privilege, and require an application interlock/audit record for writes. Test against `DataStore`, a simulator, or an isolated device first.

## Package matrix

| Package | Namespace | Target frameworks | Use it when |
| --- | --- | --- | --- |
| `ModbusRx` | `IoT.DriverCore.ModbusRx` | net48, net8.0, net9.0, net10.0, net11.0 | Using ReactiveUI.Primitives.Async and SerialPortRx. |
| `ModbusRx.Reactive` | `IoT.DriverCore.ModbusRx.Reactive` | net48, net8.0, net9.0, net10.0, net11.0 | Using the reactive bridge and SerialPortRx.Reactive. |
| `ModbusRx.Generators` | `IoT.DriverCore.ModbusRx.Generators` | netstandard2.0 analyzer | Generating typed reactive device maps; install it alongside one runtime package. |

Both packages compile the same source with namespace aliases. Choose one; do not mix both surfaces accidentally.

## Install

```bash
dotnet add package ModbusRx
# or
dotnet add package ModbusRx.Reactive
# optional typed reactive device-map generator
dotnet add package ModbusRx.Generators
```

## Quick start

Use `ModbusIpMaster.CreateIp` with a connected `TcpClientRx`, `UdpClientRx`, `SerialPortRx`, or an `IStreamResource`. Addresses are zero-based API offsets, not display addresses such as 40001.

```csharp
using IoT.DriverCore.ModbusRx.Device;
using IoT.DriverCore.Serial;

using var tcp = new TcpClientRx("192.168.0.20", 502);
using var master = ModbusIpMaster.CreateIp(tcp);

var registers = await master.ReadHoldingRegistersAsync(
    slaveAddress: 1, startAddress: 0, numberOfPoints: 2);
await master.WriteSingleRegisterAsync(slaveAddress: 1, registerAddress: 0, value: 42);
Console.WriteLine(registers[0]);
```

`ModbusIpMaster` also has no-unit-id overloads that use the IP default unit ID. For serial devices, use `ModbusSerialMaster.CreateRtu` or `CreateAscii`.

## Configuration

Configure the transport first, then set the transport's timeout/retry properties if needed. Match TCP port, UDP endpoint, or serial baud/data bits/parity/stop bits/handshake exactly to the device. Preserve unit IDs for serial and gateway deployments. Requests validate protocol limits: reads allow up to 2,000 bits or 125 registers; writes allow 1,968 coils, 123 registers, or 121 combined-write registers.

```csharp
using System.IO.Ports;
using IoT.DriverCore.ModbusRx.Device;
using IoT.DriverCore.Serial;

using var port = new SerialPortRx("COM3")
{
    BaudRate = 9600, DataBits = 8, Parity = Parity.Even,
    StopBits = StopBits.One, Handshake = Handshake.None,
};
using var master = ModbusSerialMaster.CreateRtu(port);
var coils = await master.ReadCoilsAsync(1, 0, 8);
```

## Detailed features

### Master requests

`IModbusMaster` / `ModbusMaster` expose `ReadCoilsAsync`, `ReadInputsAsync`, `ReadHoldingRegistersAsync`, `ReadInputRegistersAsync`, `WriteSingleCoilAsync`, `WriteSingleRegisterAsync`, `WriteMultipleCoilsAsync`, `WriteMultipleRegistersAsync`, and `ReadWriteMultipleRegistersAsync`. Each base overload accepts `byte slaveAddress`; `ModbusIpMaster` adds convenience overloads. `ExecuteCustomMessage<TResponse>` supports a custom request/response pair; catch `SlaveException`, `InvalidModbusRequestException`, and `ModbusCommunicationException` explicitly.

```csharp
var current = await master.ReadWriteMultipleRegistersAsync(
    slaveAddress: 1, startReadAddress: 10, numberOfPointsToRead: 2,
    startWriteAddress: 20, writeData: new ushort[] { 100, 101 });
```

### Slaves, servers, and test data

`DataStoreFactory.CreateDefaultDataStore` creates the four data areas. `ModbusTcpSlave.CreateTcp`, `ModbusUdpSlave.CreateUdp`, and `ModbusSerialSlave.CreateRtu` / `CreateAscii` create protocol slaves. Call `ListenAsync` for a slave; dispose it to stop it. `ModbusServer` aggregates TCP/UDP slaves and remote clients, exposes `IsRunning`, `DataStore`, and `SimulationMode`, and owns server lifecycle/simulation. Use `DataStoreWrittenTo` / `DataStoreReadFrom` plus `GetOperationMetrics` for observability.

```csharp
using System.Net;
using System.Net.Sockets;
using IoT.DriverCore.ModbusRx.Data;
using IoT.DriverCore.ModbusRx.Device;

using var slave = ModbusTcpSlave.CreateTcp(1, new TcpListener(IPAddress.Loopback, 1502));
slave.DataStore = DataStoreFactory.CreateDefaultDataStore();
slave.DataStore.HoldingRegisters[0] = 25;
var listening = slave.ListenAsync(); // keep task alive until the slave is disposed
```

`ModbusSimulator`, `SimulationDataProvider`, `TestPattern`, and `SimulationType` support deterministic test/scenario data. `ModbusTcpLoopbackEndpoint` supports local end-to-end tests without a physical device.

### Observable operations and connection factories

`Create.TcpIpMaster`, `UdpIpMaster`, `SerialRtuMaster`, and `SerialAsciiMaster` create connection-state observables. Read extensions such as `ReadCoils`, `ReadInputs`, `ReadHoldingRegisters`, and `ReadInputRegisters` poll those streams and emit `(Data, Error)` tuples. `ModbusAsyncObservableExtensions` provides async-observable bridges. `Create.PingInterval` and `CheckConnectionInterval` control factory monitoring; dispose subscriptions to release their masters.

```csharp
using IoT.DriverCore.ModbusRx;

using var poll = Create.TcpIpMaster("192.168.0.20", 502)
    .ReadHoldingRegisters(1, 0, 2, interval: 500)
    .Subscribe(result =>
    {
        if (result.Error is null) Console.WriteLine(result.Data![0]);
    });
```

### Logical tags, conversions, and Enron helpers

`ModbusLogicalTagClient` composes an `IModbusMaster` with a `ModbusTagCatalog`, optional SQLite persistence, grouped reads/writes, and observable/async observation. Build a `ModbusTagConfiguration`, set byte order/access/scan interval as needed, and call `CreateTag`; use `ReadAsync` / `WriteAsync` or batch variants. `ModbusDataExtensions` and `ModbusUtility` convert registers, byte order, CRC/LRC, floats, doubles, ASCII, and network values. `EnronModbusExtensions` adds 32-bit register read/write helpers.

```csharp
using IoT.DriverCore.ModbusRx.LogicalTags;

using var tags = new ModbusLogicalTagClient(master, catalog: null, defaultScanInterval: TimeSpan.FromSeconds(1));
tags.CreateTag(new ModbusTagConfiguration(
    "Temperature", 1, ModbusDataArea.HoldingRegister, 0, 1, typeof(ushort)));
var value = await tags.ReadAsync("Temperature");
if (value.Succeeded) Console.WriteLine(value.Value!.Value);
```

### Generated reactive device maps

Add `ModbusRx.Generators` when a partial class should expose generated latest-value, observable, async-observable, binding, and optional logical-tag read/write members. The connection member must expose the master stream produced by the `Create` factory.

```csharp
using IoT.DriverCore.ModbusRx;
using IoT.DriverCore.ModbusRx.Device;
using IoT.DriverCore.ModbusRx.Generators;

var map = new BoilerMap
{
    MasterStream = Create.TcpIpMaster("192.168.0.20", 502),
};
using var binding = map.BindGeneratedModbusStreams();
using var changes = map.TemperatureObservable.Subscribe(Console.WriteLine);

[ModbusReactiveDevice(ConnectionMember = "MasterStream")]
public partial class BoilerMap
{
    public IObservable<(bool connected, Exception? error, ModbusIpMaster? master)>
        MasterStream { get; set; } = default!;

    [HoldingRegister(0)]
    public partial ushort? Temperature { get; private set; }

    [Coil(3)]
    public partial bool? Enabled { get; private set; }
}
```

Supported point attributes are `HoldingRegister`, `InputRegister`, `Coil`, and `DiscreteInput`. Configure `Count`, `DataType`, `SwapWords`, or `TagName` on an attribute when defaults are insufficient. Set `TagClientMember` on `ModbusReactiveDevice` to generate logical-tag `Read<Property>Async` and `Write<Property>Async` helpers.

## Exhaustive feature guide and worked workflows

All Modbus addresses passed to this library are **zero-based protocol offsets**. Device manuals often display `40001`, `30001`, `00001`, or `10001`; remove the display-area prefix and one-based offset before calling the master (for example holding register `40001` is `0`). Read limits are 2,000 bits or 125 registers; write limits are 1,968 coils, 123 registers, and 121 registers for function 23. The master validates ranges before writing to the transport; PLC/device exceptions are still remote outcomes and must be handled.

### Master construction and all function-code families

`ModbusIpMaster.CreateIp` has `TcpClientRx`, `UdpClientRx`, `SerialPortRx`, and `IStreamResource` overloads. `ModbusSerialMaster.CreateRtu` and `CreateAscii` have matching serial/stream overloads. The base `IModbusMaster`/`ModbusMaster` methods always take a `byte slaveAddress`: reads are coils (FC01), inputs (FC02), holding registers (FC03), and input registers (FC04); writes are single coil/register (FC05/06), multiple coils/registers (FC15/16), and read/write registers (FC23). `ModbusIpMaster` adds no-unit-ID overloads for the IP default. `ModbusSerialMaster.ReturnQueryData` exposes the diagnostic return-query-data operation (FC08).

```csharp
using IoT.DriverCore.ModbusRx.Device;
using IoT.DriverCore.Serial;

using var tcp = new TcpClientRx("192.168.0.20", 502);
using var master = ModbusIpMaster.CreateIp(tcp);
try
{
    bool[] coils = await master.ReadCoilsAsync(1, 0, 8);
    bool[] inputs = await master.ReadInputsAsync(1, 0, 8);
    ushort[] holding = await master.ReadHoldingRegistersAsync(1, 0, 4);
    ushort[] input = await master.ReadInputRegistersAsync(1, 0, 4);

    await master.WriteSingleCoilAsync(1, 3, true);
    await master.WriteSingleRegisterAsync(1, 0, 42);
    await master.WriteMultipleCoilsAsync(1, 8, [true, false, true]);
    await master.WriteMultipleRegistersAsync(1, 10, [100, 200]);
    ushort[] response = await master.ReadWriteMultipleRegistersAsync(1, 0, 2, 20, [300, 400]);
    Console.WriteLine($"Read/write response: {string.Join(',', response)}");
}
catch (SlaveException ex) { Console.Error.WriteLine($"Device exception: {ex.Message}"); }
catch (InvalidModbusRequestException ex) { Console.Error.WriteLine($"Invalid request: {ex.Message}"); }
catch (ModbusCommunicationException ex) { Console.Error.WriteLine($"Transport failure: {ex.Message}"); }
```

`ModbusSerialMaster.ReturnQueryData` is the synchronous FC08 **Return Query Data** diagnostic: it returns `true` only when the device echoes the supplied 16-bit value. Run it only against a serial-line device that supports the diagnostic; it is a link check, not a proof that the process program is healthy.

```csharp
using IoT.DriverCore.ModbusRx.Device;
using IoT.DriverCore.Serial;

using var port = new SerialPortRx("COM3");
using var serial = ModbusSerialMaster.CreateRtu(port);
bool echoed = serial.ReturnQueryData(slaveAddress: 1, data: 0x5AA5);
if (!echoed) throw new InvalidOperationException("The serial slave did not echo FC08 data.");
```

Use `ExecuteCustomMessage<TResponse>(IModbusMessage, Func<TResponse>)` only when a standard public message class does not express the operation, or for a proprietary PDU whose complete request/response contract you own. The direct FC08 diagnostics message is not part of the public surface, so the source-verified example below uses the standard FC06 request/response class through the same transport path. The standard `WriteSingleRegisterAsync` method is usually clearer for this operation.

```csharp
using IoT.DriverCore.ModbusRx.Message;

var request = new WriteSingleRegisterRequestResponse(
    slaveAddress: 1, startAddress: 10, registerValue: 0x1234);
WriteSingleRegisterRequestResponse response = master.ExecuteCustomMessage(
    request,
    static () => new WriteSingleRegisterRequestResponse());

if (response.Data[0] != 0x1234)
    throw new InvalidOperationException("FC06 response did not echo the expected value.");
```

`IModbusMessage` exposes function code, slave address, transaction ID, frame/PDU, and `Initialize`; `IModbusRequest` also validates a response. In this release, the public message base constructors are not consumable outside the package, so vendor PDU types cannot safely be derived by an application. Use the public standard message classes/factories or a dedicated protocol adapter package for a proprietary PDU. Validate function code, unit ID, byte count, and payload before using any reply; do not use a raw/custom path to bypass point-count or process-safety validation.

### TCP, UDP, RTU, ASCII and transport lifetime

TCP and UDP use port 502 by convention but the library accepts any transport endpoint. RTU and ASCII require the exact COM port framing used by the device. Own the `SerialPortRx`/client and master together with `using`; a master disposes its transport resources. Configure timeout, retries, and retry delay on `master.Transport` where applicable before issuing commands. Retries can repeat a write after a lost response, so write only idempotent values or add application-level command/audit protection.

```csharp
using System.IO.Ports;
using IoT.DriverCore.ModbusRx.Device;
using IoT.DriverCore.Serial;

using var port = new SerialPortRx("COM3")
{
    BaudRate = 19_200,
    DataBits = 8,
    Parity = Parity.Even,
    StopBits = StopBits.One,
    Handshake = Handshake.None,
};
using var rtu = ModbusSerialMaster.CreateRtu(port);
rtu.Transport.ReadTimeout = 2_000;
rtu.Transport.Retries = 1;
ushort[] setpoint = await rtu.ReadHoldingRegistersAsync(1, 10, 1);
await rtu.WriteSingleRegisterAsync(1, 10, checked((ushort)(setpoint[0] + 1)));
```

For Modbus UDP, create a **connected** `UdpClientRx` before passing it to `ModbusIpMaster.CreateIp`. Modbus ASCII can likewise run over a `SerialPortRx`, TCP client, UDP client, or an `IStreamResource`; the framing mode is selected by `CreateAscii`, not by the endpoint type.

```csharp
using IoT.DriverCore.ModbusRx.Device;
using IoT.DriverCore.Serial;

using var udp = new UdpClientRx("192.168.0.20", 502);
using var udpMaster = ModbusIpMaster.CreateIp(udp);
ushort[] udpValue = await udpMaster.ReadHoldingRegistersAsync(1, 0, 1);

using var asciiPort = new SerialPortRx("COM4");
using var ascii = ModbusSerialMaster.CreateAscii(asciiPort);
await ascii.WriteSingleRegisterAsync(1, 10, udpValue[0]);
```

### Data conversion, byte order, Enron helpers and collections

`RegisterCollection` and `DiscreteCollection` are PDU data containers with byte-count semantics; `DataStore` holds four server areas and exposes `SyncRoot`/`Lock` for safe direct mutation. `ModbusDataExtensions` packs/unpacks booleans and converts 16/32/64-bit values. `CreateExtensions` offers `ToFloat`, `FromFloat`, `ToDouble`, and `FromDouble` helpers for reactive use. `ModbusUtility` owns CRC/LRC and general network conversion helpers. `ModbusByteOrder`, `SwapWords`, and a tag's data type determine word ordering: verify this against the device manual with known test values. `EnronModbusExtensions` provides 32-bit Enron read/write helpers; it does not make a conventional Modbus register map Enron-compatible.

```csharp
using IoT.DriverCore.ModbusRx.Data;
using IoT.DriverCore.ModbusRx.Reactive;

ushort[] registers = await master.ReadHoldingRegistersAsync(1, 100, 2);
float? engineeringValue = CreateExtensions.ToFloat(registers, 0, swapWords: true);
if (engineeringValue is float value)
{
    var encoded = new ushort[2];
    CreateExtensions.FromFloat(value + 1.5f, encoded, 0, swapWords: true);
    await master.WriteMultipleRegistersAsync(1, 100, encoded);
}
```

Use `EnronModbusExtensions` only with an Enron-compatible 32-bit map. Each `uint` consumes two conventional Modbus registers; reads allow 1-62 values and writes allow 1-61 values. The helper's word ordering is defined by its implementation, so validate it with a known device value before deploying.

```csharp
using IoT.DriverCore.ModbusRx.Extensions.Enron;

uint[] totals = await master.ReadHoldingRegisters32Async(
    slaveAddress: 1, startAddress: 200, numberOfPoints: 2);
await master.WriteSingleRegister32Async(1, registerAddress: 204, totals[0] + 1);
await master.WriteMultipleRegisters32Async(1, startAddress: 206, data: totals);
```

### Slaves, server aggregation, request observability and simulation

For one endpoint, create a slave with `ModbusTcpSlave.CreateTcp`, `ModbusUdpSlave.CreateUdp`, or `ModbusSerialSlave.CreateRtu/CreateAscii`, assign a `DataStore`, then await `ListenAsync` for the lifetime of that endpoint. `ModbusSlave` exposes request/data-store events; use `CreateExtensions.ObserveRequest`, `ObserveWriteComplete`, `ObserveDataStoreReadFrom`, and `ObserveDataStoreWrittenTo` or their async-observable bridges to observe them.

`ModbusServer` aggregates TCP/UDP server endpoints and remote clients. `StartTcpServer`, `StartUdpServer`, `AddTcpClient`, and `AddUdpClient` return `IDisposable` registrations; retain them. `Start` begins the configured server, `Stop` halts it, `IsRunning` is an observable state stream, `LoadSimulationData` populates all areas, `GetCurrentData` snapshots areas, and `SimulationMode` controls simulator behaviour. `EnhancedModbusServerExtensions` offers event-driven, optimized, and buffered `ModbusServerDataSnapshot` streams. These monitoring streams are snapshots, not durable audit storage.

```csharp
using System.Net;
using System.Net.Sockets;
using IoT.DriverCore.ModbusRx.Data;
using IoT.DriverCore.ModbusRx.Device;
using IoT.DriverCore.ModbusRx.Reactive;

using var slave = ModbusTcpSlave.CreateTcp(1, new TcpListener(IPAddress.Loopback, 1502));
slave.DataStore = DataStoreFactory.CreateDefaultDataStore();
using var writes = CreateExtensions.ObserveDataStoreWrittenTo(slave)
    .Subscribe(e => Console.WriteLine($"Area={e.ModbusDataType}, start={e.StartAddress}"));
var listening = slave.ListenAsync(); // await/host this task until disposal

using var server = new ModbusServer { SimulationMode = true };
using var tcpEndpoint = server.StartTcpServer(1503, 1);
server.LoadSimulationData([1, 2], [3, 4], [true, false], [false, true]);
server.Start();
using var snapshots = server.ObserveDataChangesEventDriven().Subscribe(snapshot =>
    Console.WriteLine(snapshot.HoldingRegisters.Length));
// server.Stop() is called explicitly before the using scope ends when required.
```

For server-owned memory, use the optimized `DataStoreExtensions` methods only while holding the store's lock, then inspect operation metrics as logical operation counters. They are useful for deterministic simulation setup and bridge code; normal Modbus clients should use master requests instead. `ObserveDataChangesBuffered` batches snapshots by count (the current implementation does not apply its time argument), so choose a bounded buffer and dispose the subscription.

```csharp
using IoT.DriverCore.ModbusRx.Data;
using IoT.DriverCore.ModbusRx.Reactive;

var source = DataStoreFactory.CreateDefaultDataStore();
var replica = DataStoreFactory.CreateDefaultDataStore();
lock (source.SyncRoot)
{
    source.WriteHoldingRegistersOptimized(1, [10, 20, 30]);
    source.BulkCopyHoldingRegisters(replica, 1, 3);
    bool identical = source.CompareHoldingRegisters(replica, 1, 3);
    Console.WriteLine($"In sync: {identical}");
}
DataStoreOperationMetrics metrics = source.GetOperationMetrics();
Console.WriteLine($"Reads={metrics.ReadOperations}, writes={metrics.WriteOperations}");
using var batches = server.ObserveDataChangesBuffered(bufferSize: 8, bufferTimeMilliseconds: 250)
    .Subscribe(batch => Console.WriteLine($"Received {batch.Length} snapshots."));
```

`ModbusSimulator` owns a local `DataStore`, creates an in-memory master, starts a TCP loopback endpoint, queues `ModbusSimulatorFaultKind` failures, clears faults, and exposes `RequestCount`. `ModbusTcpLoopbackEndpoint` is the lower-level public deterministic endpoint. `SimulationDataProvider` can generate sine/square/sawtooth/random/boolean patterns, load `TestPattern`, expose `IsRunning`, and start/stop changes to a `DataStore`. Use them for integration tests, fault paths, dashboards, and model validation before a live device.

```csharp
using var simulator = new ModbusSimulator(unitId: 1);
using var simulatedMaster = simulator.CreateMaster();
simulator.DataStore.HoldingRegisters[0] = 77;
ushort[] initial = await simulatedMaster.ReadHoldingRegistersAsync(1, 0, 1);
simulator.QueueFault(ModbusSimulatorFaultKind.Timeout);
try { await simulatedMaster.ReadHoldingRegistersAsync(1, 0, 1); }
catch (ModbusCommunicationException) { Console.WriteLine("Expected simulated timeout."); }
simulator.ClearFaults();
```

### Reactive connection factories, polling, async observables, and server writers

`Create.TcpIpMaster`, `UdpIpMaster`, `SerialIpMaster`, `SerialRtuMaster`, and `SerialAsciiMaster` emit connection tuples `(Connected, Error, Master)`. `Create.PingInterval` controls reachability probes and `CheckConnectionInterval` controls connection checks globally for those factories. `CreateExtensions` and `Create` read overloads project a master stream to `(Data, Error)` observations for coils, inputs, holding registers, and input registers; overloads select default/unit ID and polling interval. An error tuple is a value, so inspect `Error` and never dereference `Data` until it is null-checked. `ModbusAsyncObservableExtensions` supplies equivalent `IObservableAsync<T>` read, request, datastore, and point-observation bridges. Dispose the final subscription; it owns the connection/master lifecycle in the factory pipeline.

```csharp
using IoT.DriverCore.ModbusRx.Reactive;

Create.PingInterval = TimeSpan.FromSeconds(2);
using var poll = Create.TcpIpMaster("192.168.0.20", 502)
    .ReadHoldingRegisters(slaveAddress: 1, startAddress: 0, numberOfPoints: 2, interval: 500)
    .Subscribe(result =>
    {
        if (result.Error is not null)
            Console.Error.WriteLine(result.Error.Message);
        else if (result.Data is { Length: > 0 } registers)
            Console.WriteLine($"value={registers[0]}");
    });
```

`ToModbusObservable` converts a factory stream to `IObservableAsync<T>`; use the async extension read overloads when the rest of the pipeline uses ReactiveUI.Primitives.Async. The returned value still carries `Error`, so check it before using `Data`.

```csharp
using IoT.DriverCore.ModbusRx;
using IoT.DriverCore.ModbusRx.Reactive;

var asyncConnections = Create.TcpIpMaster("192.168.0.20", 502).ToModbusObservable();
var asyncRegisters = asyncConnections.ReadHoldingRegisters(
    startAddress: 0, numberOfPoints: 2, interval: 500);
using var subscription = asyncRegisters.ToObservable().Subscribe(result =>
{
    if (result.Error is null && result.Data is { Length: > 0 } values)
        Console.WriteLine(values[0]);
});
```

The async slave writer methods combine a slave stream and a value stream, then write to the slave `DataStore`. They return the original slave stream: retain a subscription to keep the combined pipeline alive. The same four writer names (`WriteHoldingRegisters`, `WriteInputRegisters`, `WriteCoilDiscretes`, `WriteInputDiscretes`) are available for TCP, UDP, and serial slaves.

```csharp
using IoT.DriverCore.ModbusRx;
using IoT.DriverCore.ModbusRx.Device;
using IoT.DriverCore.ModbusRx.Reactive;
using ReactiveUI.Primitives.Async;

// Build both streams from public reactive factories/operators.
IObservableAsync<ModbusTcpSlave> slaveStream = Create.TcpIpSlave("127.0.0.1", 1502, 1)
    .ToAsyncObservable();
IObservableAsync<ushort[]> setpoints = SignalAsync.Return(new ushort[] { 100, 200 });
IObservableAsync<ModbusTcpSlave> updated = slaveStream.WriteHoldingRegisters(10, setpoints);
using var writer = updated.ToObservable().Subscribe(static _ => { });
```

`ModbusBufferManager` is a per-owner pool facade. Rent the buffer, use only the required slice, and always return it in `finally`; `clearArray: true` is appropriate for sensitive payloads. `GetMetrics` is a deterministic counter snapshot, not a throughput measurement.

```csharp
using IoT.DriverCore.ModbusRx.IO;

using var buffers = new ModbusBufferManager();
byte[] frame = buffers.RentByteBuffer(260);
try
{
    frame[0] = 1; // populate only the actual PDU length before sending it
}
finally
{
    buffers.ReturnByteBuffer(frame, clearArray: true);
}
ModbusBufferMetrics bufferMetrics = buffers.GetMetrics();
Console.WriteLine($"rents={bufferMetrics.RentOperations}, returns={bufferMetrics.ReturnOperations}");
```

### Logical-tag catalog, batch planning, CSV and SQLite persistence

`ModbusTagConfiguration` declares logical name, unit, `ModbusDataArea`, zero-based address, count, CLR value type, optional byte order/access/scan options. `ModbusLogicalTagClient.CreateTag` constructs and registers one; `RegisterTag`/`RemoveTag` manage an existing map. `ReadAsync`/`WriteAsync` return `TagOperationResult<LogicalTagValue>`; `ReadManyAsync`/`WriteManyAsync` batch requests by compatible ranges. `Observe`, `ObserveMany`, `ObserveAsync`, and `ObserveManyAsync` repeat reads at the configured scan interval. `ModbusTagCatalog` provides in-memory list/create/upsert/import/export; `ModbusTagSqliteStore` and the client's store methods initialize/load/get/list/upsert/update/delete persisted tags. Persistence contains configuration, not a distributed lock or a write transaction.

```csharp
using IoT.DriverCore.Core;
using IoT.DriverCore.ModbusRx.LogicalTags;

using var tags = new ModbusLogicalTagClient(master, catalog: null,
    defaultScanInterval: TimeSpan.FromMilliseconds(500));
tags.CreateTag(new ModbusTagConfiguration("TankTemperature", 1,
    ModbusDataArea.HoldingRegister, 100, 2, typeof(float))
{
    ByteOrder = ModbusByteOrder.BigEndian,
    SwapWords = true,
});

using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(2));
var reading = await tags.ReadAsync("TankTemperature", deadline.Token);
if (!reading.Succeeded) Console.Error.WriteLine(reading.Error);
else Console.WriteLine(reading.Value?.Value);

await tags.InitializeStoreAsync("Data Source=tags.db", deadline.Token);
await tags.ExportCsvAsync(Console.Out, deadline.Token);
```

### Combined workflow 1: safely derived command from a reactive read

Keep data acquisition and command policy separate: the subscription publishes measurements, while an awaited function makes an explicit, bounded write after range validation.

```csharp
using var commandDeadline = new CancellationTokenSource(TimeSpan.FromSeconds(2));
using var display = Create.TcpIpMaster("192.168.0.20", 502)
    .ReadHoldingRegisters(1, 0, 1, 1_000)
    .Subscribe(r => { if (r.Error is null) Console.WriteLine(r.Data?[0]); });

ushort[] level = await master.ReadHoldingRegistersAsync(1, 0, 1);
if (level[0] < 900)
    await master.WriteSingleCoilAsync(1, 10, true); // validate policy before command
```

### Combined workflow 2: simulated logical map, fault injection, and server observation

```csharp
using var simulator = new ModbusSimulator(1);
using var simulatedMaster = simulator.CreateMaster();
using var map = new ModbusLogicalTagClient(simulatedMaster, null, TimeSpan.FromMilliseconds(100));
map.CreateTag(new ModbusTagConfiguration("Pressure", 1, ModbusDataArea.HoldingRegister, 0, 1, typeof(ushort)));
simulator.DataStore.HoldingRegisters[0] = 250;
using var observed = map.Observe("Pressure").Subscribe(value => Console.WriteLine(value.Value));
simulator.QueueFault(ModbusSimulatorFaultKind.Timeout);
var failed = await map.ReadAsync("Pressure", CancellationToken.None);
Console.WriteLine($"Succeeded={failed.Succeeded}");
```

### Complete generator workflow

`ModbusRx.Generators` is a Roslyn analyzer package. Annotate a partial device class with `ModbusReactiveDevice` and properties with `HoldingRegister`, `InputRegister`, `Coil`, or `DiscreteInput`. The `ConnectionMember` must expose the supported `Create` master stream; optional `MasterKind`/`TagClientMember` select a master factory or logical-tag helpers. Point attributes use zero-based address and may set count, data type, word swap, and generated tag name. The analyzer validates unsupported point/property combinations and a non-partial class through diagnostics. Generated code supplies a binder, current values, observables, async observables where available, and logical read/write helpers when a tag client is configured. Dispose the `BindGeneratedModbusStreams()` result.

```csharp
using IoT.DriverCore.ModbusRx;
using IoT.DriverCore.ModbusRx.Generators;
using IoT.DriverCore.ModbusRx.LogicalTags;
using IoT.DriverCore.ModbusRx.Reactive;

[ModbusReactiveDevice(ConnectionMember = "MasterStream", TagClientMember = "Tags")]
public partial class PumpMap
{
    public IObservable<(bool Connected, Exception? Error, ModbusIpMaster? Master)> MasterStream { get; set; } = default!;
    public ModbusLogicalTagClient? Tags { get; set; }

    [HoldingRegister(0, Count = 2, DataType = ModbusReactiveDataType.Float32, SwapWords = true)]
    public partial float? Pressure { get; private set; }

    [Coil(5, TagName = "PumpEnabled")]
    public partial bool? Enabled { get; private set; }
}

var map = new PumpMap { MasterStream = Create.TcpIpMaster("192.168.0.20", 502) };
using var mapBinding = map.BindGeneratedModbusStreams();
using var pressure = map.PressureObservable.Subscribe(Console.WriteLine);
```

## Complete public API reference

This list is the public type inventory from `src/ModbusRx`; `ModbusRx.Reactive` mirrors it under `IoT.DriverCore.ModbusRx.Reactive`.

| Area | Public types / API |
| --- | --- |
| Master/device | `ICancelable`, `IModbusMaster`, `IModbusSerialMaster`, `ModbusDevice`, `ModbusMaster`, `ModbusIpMaster`, `ModbusSerialMaster`; standard reads/writes, combined read/write, custom messages, and disposal. |
| Slave/server | `ModbusSlave`, `ModbusTcpSlave`, `ModbusUdpSlave`, `ModbusSerialSlave`, `ModbusServer`, `ModbusTcpLoopbackEndpoint`, `ModbusSlaveRequestEventArgs`, `ModbusServerExtensions`, `EnhancedModbusServerExtensions`, `ModbusTcpSlaveExtensions`, `ModbusUdpSlaveExtensions`, and `ModbusSerialSlaveExtensions`. |
| Data/simulation | `DataStore`, `DataStoreFactory`, `DataStoreExtensions`, `DataStoreEventArgs`, `DataStoreOperationMetrics`, `ModbusDataCollection<T>`, `RegisterCollection`, `DiscreteCollection`, `IDataCollection`, `ModbusDataType`, `BooleanPattern`, `SimulationDataProvider`, `SimulationType`, `TestPattern`, `ModbusSimulator`, `ModbusSimulatorRequestEventArgs`, and `ModbusSimulatorFaultKind`. |
| Logical tags | `ModbusLogicalTagClient`, `ModbusTagCatalog`, `ModbusTagSqliteStore`, `ModbusTagConfiguration`, `ModbusLogicalTag`, `ModbusDataArea`, and `ModbusByteOrder`; create/register/import/export/store/CRUD/read/write/batch/observe methods. |
| Transport | `IStreamResource`, `SerialPortAdapter`, `ModbusTransport`, `ModbusSerialTransport`, `EmptyTransport`, `ModbusBufferManager`, and `ModbusBufferMetrics`. |
| Reactive | `Create`, `CreateExtensions`, `ModbusAsyncObservableExtensions`, `ModbusObservationMetrics`, `ModbusServerDataSnapshot`, and `ModbusCommunicationException`. |
| Generation | `ModbusRx.Generators` injects `ModbusReactiveDeviceAttribute`, `ModbusReactiveMasterKind`, `ModbusReactiveDataType`, `HoldingRegisterAttribute`, `InputRegisterAttribute`, `CoilAttribute`, and `DiscreteInputAttribute`; `ModbusReactiveStreamGenerator` then generates binding and typed stream members for valid partial device maps. |
| Messages/utilities | `IModbusMessage`, `IModbusRequest`, `AbstractModbusMessage`, `AbstractModbusMessageWithData<T>`, `ModbusMessageFactory`, `OptimizedModbusMessageFactory`, `ReadCoilsInputsRequest`, `ReadCoilsInputsResponse`, `ReadHoldingInputRegistersRequest`, `ReadHoldingInputRegistersResponse`, `ReadWriteMultipleRegistersRequest`, `WriteMultipleCoilsRequest`, `WriteMultipleCoilsResponse`, `WriteMultipleRegistersRequest`, `WriteMultipleRegistersResponse`, `WriteSingleCoilRequestResponse`, `WriteSingleRegisterRequestResponse`, the remaining supported function-code messages, `SlaveException`, `SlaveExceptionResponse`, `InvalidModbusRequestException`, `ModbusUtility`, `EnronModbusExtensions`, `DiscriminatedUnion<TA,TB>`, and `DiscriminatedUnionOption`. |

### Member-family contract

| Area | Members, returns, errors, and ownership |
| --- | --- |
| Master | Factory overloads create a disposable master; all standard FC01/02/03/04 reads return arrays, FC05/06/15/16 writes return `Task`, and FC23 returns registers. Use unit-ID overloads unless the IP default is intended; handle request, slave, and transport failures. |
| Transport | `IStreamResource`, adapters, transports, buffers and metrics own framing. Set timeout/retry before operations; disposal cancels/reclaims transport resources but cannot undo a remote write. |
| Slave/server | `Create*`, `ListenAsync`, data store/request events, `ModbusServer` start/stop/client/endpoint/snapshot methods. Host listening tasks and dispose endpoint registrations, server and subscriptions. |
| Data/conversion | Stores/collections/factory/extensions, simulation patterns, utility/Enron conversions. Lock direct store mutation and validate endian/count/type using known device values. |
| Reactive | `Create` connection factory/read/slave methods, `CreateExtensions`, async-observable bridges and server extensions. Tuples carry errors as values; dispose final subscriptions. |
| Logical tags | Configuration/catalog/store/client/codec/planner values. Catalog/persistence validates mapping but does not make physical multi-write operations atomic. Pass cancellation to every database/PLC call. |
| Generator/messages | `ModbusReactiveStreamGenerator` is the public analyzer type; its attributes are injected into the consuming compilation rather than exported as runtime package API. PDU contracts/factories/exception types require partial models, compiler-diagnostic review, and a documented PDU contract for every custom message. |

### Supporting public member index

The following complete member groups round out the API reference. Their low-level nature is intentional: use master/slave/logical APIs first, and confine direct helper use to tested protocol adapters.

| Feature | Members and use |
| --- | --- |
| Data-store maintenance | `ReadHoldingRegistersOptimized`, `ReadInputRegistersOptimized`, `ReadCoilsOptimized`, `ReadInputsOptimized`, `WriteHoldingRegistersOptimized`, `WriteCoilsOptimized`, `BulkCopyHoldingRegisters`, `BulkCopyCoils`, `ClearHoldingRegisters`, `ClearCoils`, `CompareHoldingRegisters`, and `GetMetrics`. Lock the store around direct mutation and use protocol operations for normal client/server traffic. |
| Encoding and utility | `PackBooleans`, `UnpackBooleans`, `FastEquals`, `ToRegisters`, `ToInt32`, `ToUInt32`, `GetUInt32`, `ToInt64`, `GetSingle`, `GetDouble`, `ReadSingle`, `ReadDouble`, `WriteSingle`, `WriteDouble`, `CalculateCrc`, `CalculateLrc`, `GetAsciiBytes`, `HexToBytes`, `NetworkBytesToHostUInt16`, `ValidateMessageCrc`, and `ValidateResponse`. These helpers have explicit byte-order/length requirements; validate using known device values. |
| 32-bit and Enron helpers | `ReadHoldingRegisters32Async`, `ReadInputRegisters32Async`, `WriteSingleRegister32Async`, `WriteMultipleRegisters32Async`, and Enron read/write helpers. They issue multiple-register operations and do not remove normal unit/address/word-order requirements. |
| Factories/messages | `CreateModbusRequest`, `CreateReadCoilsRequest`, `CreateReadHoldingRegistersRequest`, `CreateWriteSingleCoilRequest`, `CreateWriteSingleRegisterRequest`, `CreateWriteMultipleCoilsRequest`, `CreateWriteMultipleRegistersRequest`, `ParseReadCoilsResponse`, and `ParseReadHoldingRegistersResponse`. They create/parse PDUs; validate the response function/unit/CRC before accepting custom traffic. |
| Collection/catalog/store | `CreateA`, `CreateB`, `FromLogicalTag`, `ToLogicalTag`, `TryAdd`, `TryGet`, `TryRemove`, `ImportCsvAsync`, `InitializeAsync`, `GetAsync`, `ListAsync`, `UpsertAsync`, `UpdateAsync`, `DeleteAsync`, `LoadCatalogAsync`, `LoadFromSqliteAsync`, `LoadTagsAsync`, `GetStoredTagAsync`, `ListStoredTagsAsync`, `UpsertStoredTagAsync`, `UpdateStoredTagAsync`, and `DeleteStoredTagAsync`. These methods separate in-memory mapping from SQLite persistence; pass cancellation and dispose stores/catalogs. |
| Simulation and loopback | `GenerateSineWave`, `GenerateSquareWave`, `GenerateSawtoothWave`, `GenerateRandomData`, `GenerateBooleanPattern`, `LoadTestPattern`, `StartTcpLoopback`, and simulator request events. Generated data modifies a supplied store on a schedule; stop/dispose the provider and loopback endpoint. |
| Observable server/read helpers | `ObserveCoils`, `ObserveCoilsObservable`, `ObserveHoldingRegisters`, `ObserveHoldingRegistersObservable`, `ObserveInputRegisters`, `ObserveInputRegistersObservable`, `ObserveDiscreteInputs`, `ObserveDiscreteInputsObservable`, `ObserveDataChangesOptimized`, `ObserveDataChangesBuffered`, `ObserveHoldingRegistersOptimized`, `ObserveCoilsOptimized`, `ObserveRequestObservable`, `ObserveWriteCompleteObservable`, `ObserveDataStoreReadFromObservable`, and `ObserveDataStoreWrittenToObservable`. All expose subscriptions that must be disposed and all errors/data tuples must be checked. |
| Slave writers and resource pools | `WriteHoldingRegisters`, `WriteInputRegisters`, `WriteCoilDiscretes`, `WriteInputDiscretes`, `RentByteBuffer`, `ReturnByteBuffer`, `RentUshortBuffer`, `ReturnUshortBuffer`, `RentBoolBuffer`, `ReturnBoolBuffer`, `DisposeSharedResources`, and `DiscardInBuffer`. These are infrastructure helpers; pair each rent with the matching return and do not dispose shared pools while active transport work remains. |
| Value/object members | `Equals`, `GetHashCode`, `ToString`, plus `ToLogicalTag`/`FromLogicalTag` conversions preserve configuration values for comparison/persistence rather than physical device state. |

## Operational guidance

Use one master per serialized physical link unless the device explicitly supports concurrent transactions. Batch contiguous ranges and keep within the protocol limits. Keep writes idempotent where retries are possible, record unit ID/address/value/result, and dispose masters, slaves, servers, and subscriptions. Synchronize direct `DataStore` collection access with `SyncRoot` or `Lock`; normal protocol operations already protect the store.

## Troubleshooting

| Symptom | Check |
| --- | --- |
| `SlaveException` | Function, unit ID, zero-based address, point count, and device permissions. |
| Timeout/communication failure | IP/port/firewall, gateway route, transport timeout/retry values, and whether the endpoint is TCP or UDP. |
| Serial failure | Port ownership and exact serial settings; use RTU or ASCII to match the device. |
| Wrong decoded number | Register count, signedness, and `ModbusByteOrder`; use `ModbusDataExtensions`/`ModbusUtility` rather than ad-hoc casts. |
| Polling churn | Increase interval, consolidate ranges/tags, and dispose obsolete connection/read subscriptions. |

## AI skill

For source-grounded ModbusRx work, use [skills/modbus-rx/SKILL.md](../../skills/modbus-rx/SKILL.md). It directs an agent to validate namespaces, transport semantics, limits, and current public APIs against source.

<!-- BEGIN GENERATED PUBLIC API -->

## Exhaustive public API reference

This catalogue is generated from the packaged runtime assemblies and their XML documentation. It includes exported public types and their declared public members; inherited members and non-public implementation details are intentionally omitted.

### `ModbusRx`

Exported public types: 81; declared public members: 618.

#### `T:IoT.DriverCore.ModbusRx.Create`

```csharp
public class IoT.DriverCore.ModbusRx.Create
```
Provides ModbusRx functionality.

##### Declared public members

###### `M:IoT.DriverCore.ModbusRx.Create.ReadCoils(System.IObservable`1{System.ValueTuple`3{System.Boolean,System.Exception,IoT.DriverCore.ModbusRx.Device.IModbusSerialMaster}},System.Byte,System.UInt16,System.UInt16,System.Double)`

```csharp
public static System.IObservable<System.ValueTuple<bool[], System.Exception>> ReadCoils(System.IObservable<System.ValueTuple<bool, System.Exception, IoT.DriverCore.ModbusRx.Device.IModbusSerialMaster>> source, byte slaveAddress, ushort startAddress, ushort numberOfPoints, double interval)
```
Executes the `ReadCoils` operation.

- Parameter `source`: The `source` value.
- Parameter `slaveAddress`: The `slaveAddress` value.
- Parameter `startAddress`: The `startAddress` value.
- Parameter `numberOfPoints`: The `numberOfPoints` value.
- Parameter `interval`: The `interval` value.
- Returns: A `System.IObservable<System.ValueTuple<bool[], System.Exception>>` result.

###### `M:IoT.DriverCore.ModbusRx.Create.ReadCoils(System.IObservable`1{System.ValueTuple`3{System.Boolean,System.Exception,IoT.DriverCore.ModbusRx.Device.IModbusSerialMaster}},System.UInt16,System.UInt16,System.Double)`

```csharp
public static System.IObservable<System.ValueTuple<bool[], System.Exception>> ReadCoils(System.IObservable<System.ValueTuple<bool, System.Exception, IoT.DriverCore.ModbusRx.Device.IModbusSerialMaster>> source, ushort startAddress, ushort numberOfPoints, double interval)
```
Executes the `ReadCoils` operation.

- Parameter `source`: The `source` value.
- Parameter `startAddress`: The `startAddress` value.
- Parameter `numberOfPoints`: The `numberOfPoints` value.
- Parameter `interval`: The `interval` value.
- Returns: A `System.IObservable<System.ValueTuple<bool[], System.Exception>>` result.

###### `M:IoT.DriverCore.ModbusRx.Create.ReadCoils(System.IObservable`1{System.ValueTuple`3{System.Boolean,System.Exception,IoT.DriverCore.ModbusRx.Device.ModbusIpMaster}},System.UInt16,System.UInt16,System.Double)`

```csharp
public static System.IObservable<System.ValueTuple<bool[], System.Exception>> ReadCoils(System.IObservable<System.ValueTuple<bool, System.Exception, IoT.DriverCore.ModbusRx.Device.ModbusIpMaster>> source, ushort startAddress, ushort numberOfPoints, double interval)
```
Executes the `ReadCoils` operation.

- Parameter `source`: The `source` value.
- Parameter `startAddress`: The `startAddress` value.
- Parameter `numberOfPoints`: The `numberOfPoints` value.
- Parameter `interval`: The `interval` value.
- Returns: A `System.IObservable<System.ValueTuple<bool[], System.Exception>>` result.

###### `M:IoT.DriverCore.ModbusRx.Create.ReadHoldingRegisters(System.IObservable`1{System.ValueTuple`3{System.Boolean,System.Exception,IoT.DriverCore.ModbusRx.Device.IModbusSerialMaster}},System.Byte,System.UInt16,System.UInt16,System.Double)`

```csharp
public static System.IObservable<System.ValueTuple<ushort[], System.Exception>> ReadHoldingRegisters(System.IObservable<System.ValueTuple<bool, System.Exception, IoT.DriverCore.ModbusRx.Device.IModbusSerialMaster>> source, byte slaveAddress, ushort startAddress, ushort numberOfPoints, double interval)
```
Executes the `ReadHoldingRegisters` operation.

- Parameter `source`: The `source` value.
- Parameter `slaveAddress`: The `slaveAddress` value.
- Parameter `startAddress`: The `startAddress` value.
- Parameter `numberOfPoints`: The `numberOfPoints` value.
- Parameter `interval`: The `interval` value.
- Returns: A `System.IObservable<System.ValueTuple<ushort[], System.Exception>>` result.

###### `M:IoT.DriverCore.ModbusRx.Create.ReadHoldingRegisters(System.IObservable`1{System.ValueTuple`3{System.Boolean,System.Exception,IoT.DriverCore.ModbusRx.Device.IModbusSerialMaster}},System.UInt16,System.UInt16,System.Double)`

```csharp
public static System.IObservable<System.ValueTuple<ushort[], System.Exception>> ReadHoldingRegisters(System.IObservable<System.ValueTuple<bool, System.Exception, IoT.DriverCore.ModbusRx.Device.IModbusSerialMaster>> source, ushort startAddress, ushort numberOfPoints, double interval)
```
Executes the `ReadHoldingRegisters` operation.

- Parameter `source`: The `source` value.
- Parameter `startAddress`: The `startAddress` value.
- Parameter `numberOfPoints`: The `numberOfPoints` value.
- Parameter `interval`: The `interval` value.
- Returns: A `System.IObservable<System.ValueTuple<ushort[], System.Exception>>` result.

###### `M:IoT.DriverCore.ModbusRx.Create.ReadHoldingRegisters(System.IObservable`1{System.ValueTuple`3{System.Boolean,System.Exception,IoT.DriverCore.ModbusRx.Device.ModbusIpMaster}},System.UInt16,System.UInt16,System.Double)`

```csharp
public static System.IObservable<System.ValueTuple<ushort[], System.Exception>> ReadHoldingRegisters(System.IObservable<System.ValueTuple<bool, System.Exception, IoT.DriverCore.ModbusRx.Device.ModbusIpMaster>> source, ushort startAddress, ushort numberOfPoints, double interval)
```
Executes the `ReadHoldingRegisters` operation.

- Parameter `source`: The `source` value.
- Parameter `startAddress`: The `startAddress` value.
- Parameter `numberOfPoints`: The `numberOfPoints` value.
- Parameter `interval`: The `interval` value.
- Returns: A `System.IObservable<System.ValueTuple<ushort[], System.Exception>>` result.

###### `M:IoT.DriverCore.ModbusRx.Create.ReadInputRegisters(System.IObservable`1{System.ValueTuple`3{System.Boolean,System.Exception,IoT.DriverCore.ModbusRx.Device.IModbusSerialMaster}},System.Byte,System.UInt16,System.UInt16,System.Double)`

```csharp
public static System.IObservable<System.ValueTuple<ushort[], System.Exception>> ReadInputRegisters(System.IObservable<System.ValueTuple<bool, System.Exception, IoT.DriverCore.ModbusRx.Device.IModbusSerialMaster>> source, byte slaveAddress, ushort startAddress, ushort numberOfPoints, double interval)
```
Executes the `ReadInputRegisters` operation.

- Parameter `source`: The `source` value.
- Parameter `slaveAddress`: The `slaveAddress` value.
- Parameter `startAddress`: The `startAddress` value.
- Parameter `numberOfPoints`: The `numberOfPoints` value.
- Parameter `interval`: The `interval` value.
- Returns: A `System.IObservable<System.ValueTuple<ushort[], System.Exception>>` result.

###### `M:IoT.DriverCore.ModbusRx.Create.ReadInputRegisters(System.IObservable`1{System.ValueTuple`3{System.Boolean,System.Exception,IoT.DriverCore.ModbusRx.Device.IModbusSerialMaster}},System.UInt16,System.UInt16,System.Double)`

```csharp
public static System.IObservable<System.ValueTuple<ushort[], System.Exception>> ReadInputRegisters(System.IObservable<System.ValueTuple<bool, System.Exception, IoT.DriverCore.ModbusRx.Device.IModbusSerialMaster>> source, ushort startAddress, ushort numberOfPoints, double interval)
```
Executes the `ReadInputRegisters` operation.

- Parameter `source`: The `source` value.
- Parameter `startAddress`: The `startAddress` value.
- Parameter `numberOfPoints`: The `numberOfPoints` value.
- Parameter `interval`: The `interval` value.
- Returns: A `System.IObservable<System.ValueTuple<ushort[], System.Exception>>` result.

###### `M:IoT.DriverCore.ModbusRx.Create.ReadInputRegisters(System.IObservable`1{System.ValueTuple`3{System.Boolean,System.Exception,IoT.DriverCore.ModbusRx.Device.ModbusIpMaster}},System.UInt16,System.UInt16,System.Double)`

```csharp
public static System.IObservable<System.ValueTuple<ushort[], System.Exception>> ReadInputRegisters(System.IObservable<System.ValueTuple<bool, System.Exception, IoT.DriverCore.ModbusRx.Device.ModbusIpMaster>> source, ushort startAddress, ushort numberOfPoints, double interval)
```
Executes the `ReadInputRegisters` operation.

- Parameter `source`: The `source` value.
- Parameter `startAddress`: The `startAddress` value.
- Parameter `numberOfPoints`: The `numberOfPoints` value.
- Parameter `interval`: The `interval` value.
- Returns: A `System.IObservable<System.ValueTuple<ushort[], System.Exception>>` result.

###### `M:IoT.DriverCore.ModbusRx.Create.ReadInputs(System.IObservable`1{System.ValueTuple`3{System.Boolean,System.Exception,IoT.DriverCore.ModbusRx.Device.IModbusSerialMaster}},System.Byte,System.UInt16,System.UInt16,System.Double)`

```csharp
public static System.IObservable<System.ValueTuple<bool[], System.Exception>> ReadInputs(System.IObservable<System.ValueTuple<bool, System.Exception, IoT.DriverCore.ModbusRx.Device.IModbusSerialMaster>> source, byte slaveAddress, ushort startAddress, ushort numberOfPoints, double interval)
```
Executes the `ReadInputs` operation.

- Parameter `source`: The `source` value.
- Parameter `slaveAddress`: The `slaveAddress` value.
- Parameter `startAddress`: The `startAddress` value.
- Parameter `numberOfPoints`: The `numberOfPoints` value.
- Parameter `interval`: The `interval` value.
- Returns: A `System.IObservable<System.ValueTuple<bool[], System.Exception>>` result.

###### `M:IoT.DriverCore.ModbusRx.Create.ReadInputs(System.IObservable`1{System.ValueTuple`3{System.Boolean,System.Exception,IoT.DriverCore.ModbusRx.Device.IModbusSerialMaster}},System.UInt16,System.UInt16,System.Double)`

```csharp
public static System.IObservable<System.ValueTuple<bool[], System.Exception>> ReadInputs(System.IObservable<System.ValueTuple<bool, System.Exception, IoT.DriverCore.ModbusRx.Device.IModbusSerialMaster>> source, ushort startAddress, ushort numberOfPoints, double interval)
```
Executes the `ReadInputs` operation.

- Parameter `source`: The `source` value.
- Parameter `startAddress`: The `startAddress` value.
- Parameter `numberOfPoints`: The `numberOfPoints` value.
- Parameter `interval`: The `interval` value.
- Returns: A `System.IObservable<System.ValueTuple<bool[], System.Exception>>` result.

###### `M:IoT.DriverCore.ModbusRx.Create.ReadInputs(System.IObservable`1{System.ValueTuple`3{System.Boolean,System.Exception,IoT.DriverCore.ModbusRx.Device.ModbusIpMaster}},System.UInt16,System.UInt16,System.Double)`

```csharp
public static System.IObservable<System.ValueTuple<bool[], System.Exception>> ReadInputs(System.IObservable<System.ValueTuple<bool, System.Exception, IoT.DriverCore.ModbusRx.Device.ModbusIpMaster>> source, ushort startAddress, ushort numberOfPoints, double interval)
```
Executes the `ReadInputs` operation.

- Parameter `source`: The `source` value.
- Parameter `startAddress`: The `startAddress` value.
- Parameter `numberOfPoints`: The `numberOfPoints` value.
- Parameter `interval`: The `interval` value.
- Returns: A `System.IObservable<System.ValueTuple<bool[], System.Exception>>` result.

###### `M:IoT.DriverCore.ModbusRx.Create.SerialAsciiMaster(System.String,System.Int32,System.Int32,System.IO.Ports.Parity,System.IO.Ports.StopBits,System.IO.Ports.Handshake)`

```csharp
public static System.IObservable<System.ValueTuple<bool, System.Exception, IoT.DriverCore.ModbusRx.Device.IModbusSerialMaster>> SerialAsciiMaster(string port, int baudRate, int dataBits, System.IO.Ports.Parity parity, System.IO.Ports.StopBits stopBits, System.IO.Ports.Handshake handshake)
```
Create a reactive Modbus Serial ASCII master that automatically manages connection state.

- Parameter `port`: The COM port (e.g., "COM1").
- Parameter `baudRate`: The baud rate.
- Parameter `dataBits`: The data bits.
- Parameter `parity`: The parity.
- Parameter `stopBits`: The stop bits.
- Parameter `handshake`: The handshake.
- Returns: An observable stream of connection status and the ASCII master.

###### `M:IoT.DriverCore.ModbusRx.Create.SerialAsciiSlave(System.String,System.Byte,System.Int32,System.Int32,System.IO.Ports.Parity,System.IO.Ports.StopBits,System.IO.Ports.Handshake)`

```csharp
public static System.IObservable<IoT.DriverCore.ModbusRx.Device.ModbusSerialSlave> SerialAsciiSlave(string port, byte unitId, int baudRate, int dataBits, System.IO.Ports.Parity parity, System.IO.Ports.StopBits stopBits, System.IO.Ports.Handshake handshake)
```
Creates an Serial Ascii Slave.

- Parameter `port`: The port.
- Parameter `unitId`: The unit identifier.
- Parameter `baudRate`: The baud rate.
- Parameter `dataBits`: The data bits.
- Parameter `parity`: The parity.
- Parameter `stopBits`: The stop bits.
- Parameter `handshake`: The handshake.
- Returns: An observable of serial ASCII slave instances.

###### `M:IoT.DriverCore.ModbusRx.Create.SerialIpMaster(System.String,System.Int32)`

```csharp
public static System.IObservable<System.ValueTuple<bool, System.Exception, IoT.DriverCore.ModbusRx.Device.ModbusIpMaster>> SerialIpMaster(string port, int baudRate)
```
Create a SerialIpMaster with the specified ip address.

- Parameter `port`: The COM Port.
- Parameter `baudRate`: The baud rate.
- Returns: The master and connection status.

###### `M:IoT.DriverCore.ModbusRx.Create.SerialRtuMaster(System.String,System.Int32,System.Int32,System.IO.Ports.Parity,System.IO.Ports.StopBits,System.IO.Ports.Handshake)`

```csharp
public static System.IObservable<System.ValueTuple<bool, System.Exception, IoT.DriverCore.ModbusRx.Device.IModbusSerialMaster>> SerialRtuMaster(string port, int baudRate, int dataBits, System.IO.Ports.Parity parity, System.IO.Ports.StopBits stopBits, System.IO.Ports.Handshake handshake)
```
Create a reactive Modbus Serial RTU master that automatically manages connection state.

- Parameter `port`: The COM port (e.g., "COM1").
- Parameter `baudRate`: The baud rate.
- Parameter `dataBits`: The data bits.
- Parameter `parity`: The parity.
- Parameter `stopBits`: The stop bits.
- Parameter `handshake`: The handshake.
- Returns: An observable stream of connection status and the RTU master.

###### `M:IoT.DriverCore.ModbusRx.Create.SerialRtuSlave(System.String,System.Byte,System.Int32,System.Int32,System.IO.Ports.Parity,System.IO.Ports.StopBits,System.IO.Ports.Handshake)`

```csharp
public static System.IObservable<IoT.DriverCore.ModbusRx.Device.ModbusSerialSlave> SerialRtuSlave(string port, byte unitId, int baudRate, int dataBits, System.IO.Ports.Parity parity, System.IO.Ports.StopBits stopBits, System.IO.Ports.Handshake handshake)
```
Creates an Serial Rtu Slave.

- Parameter `port`: The port.
- Parameter `unitId`: The unit identifier.
- Parameter `baudRate`: The baud rate.
- Parameter `dataBits`: The data bits.
- Parameter `parity`: The parity.
- Parameter `stopBits`: The stop bits.
- Parameter `handshake`: The handshake.
- Returns: An observable of serial RTU slave instances.

###### `M:IoT.DriverCore.ModbusRx.Create.TcpIpMaster(System.String,System.Int32)`

```csharp
public static System.IObservable<System.ValueTuple<bool, System.Exception, IoT.DriverCore.ModbusRx.Device.ModbusIpMaster>> TcpIpMaster(string hostAddress, int port)
```
Create a TcpIpMaster with the specified host address.

- Parameter `hostAddress`: The host address.
- Parameter `port`: The port.
- Returns: The master and connection status.

###### `M:IoT.DriverCore.ModbusRx.Create.TcpIpSlave(System.String,System.Int32,System.Byte)`

```csharp
public static System.IObservable<IoT.DriverCore.ModbusRx.Device.ModbusTcpSlave> TcpIpSlave(string hostAddress, int port, byte unitId)
```
TCPs the ip slave.

- Parameter `hostAddress`: The host address.
- Parameter `port`: The port.
- Parameter `unitId`: The unit identifier.
- Returns: An Observable of.

###### `M:IoT.DriverCore.ModbusRx.Create.UdpIpMaster(System.String,System.Int32)`

```csharp
public static System.IObservable<System.ValueTuple<bool, System.Exception, IoT.DriverCore.ModbusRx.Device.ModbusIpMaster>> UdpIpMaster(string hostAddress, int port)
```
Create a UdpIpMaster with the specified host address.

- Parameter `hostAddress`: The host address.
- Parameter `port`: The port.
- Returns: The master and connection status.

###### `M:IoT.DriverCore.ModbusRx.Create.UdpIpSlave(System.String,System.Int32,System.Byte)`

```csharp
public static System.IObservable<IoT.DriverCore.ModbusRx.Device.ModbusUdpSlave> UdpIpSlave(string hostAddress, int port, byte unitId)
```
Creates an UdpIp slave.

- Parameter `hostAddress`: The host address.
- Parameter `port`: The port.
- Parameter `unitId`: The unit identifier.
- Returns: An Observable of.

###### `P:IoT.DriverCore.ModbusRx.Create.CheckConnectionInterval`

```csharp
public System.TimeSpan CheckConnectionInterval { get; set; }
```
Gets or sets the check connection interval.

- Value: The check connection interval.

###### `P:IoT.DriverCore.ModbusRx.Create.PingInterval`

```csharp
public System.TimeSpan PingInterval { get; set; }
```
Gets or sets the ping interval.

- Value: The ping interval.

#### `T:IoT.DriverCore.ModbusRx.CreateExtensions`

```csharp
public class IoT.DriverCore.ModbusRx.CreateExtensions
```
Extension methods for Modbus reactive creation helpers.

##### Declared public members

###### `M:IoT.DriverCore.ModbusRx.CreateExtensions.FromDouble(System.Double,System.Span`1{System.UInt16},System.Int32,System.Boolean)`

```csharp
public static void FromDouble(double input, System.Span<ushort> output, int start, bool swapWords)
```
Executes the `FromDouble` operation.

- Parameter `input`: The `input` value.
- Parameter `output`: The `output` value.
- Parameter `start`: The `start` value.
- Parameter `swapWords`: The `swapWords` value.

###### `M:IoT.DriverCore.ModbusRx.CreateExtensions.FromDouble(System.Double,System.UInt16[],System.Int32,System.Boolean)`

```csharp
public static void FromDouble(double input, ushort[] output, int start, bool swapWords)
```
Writes the double value to a register array.

- Parameter `input`: The extension receiver.
- Parameter `output`: The output array.
- Parameter `start`: The start index.
- Parameter `swapWords`: Whether to swap words.

###### `M:IoT.DriverCore.ModbusRx.CreateExtensions.FromFloat(System.Single,System.Span`1{System.UInt16},System.Int32,System.Boolean)`

```csharp
public static void FromFloat(float input, System.Span<ushort> output, int start, bool swapWords)
```
Executes the `FromFloat` operation.

- Parameter `input`: The `input` value.
- Parameter `output`: The `output` value.
- Parameter `start`: The `start` value.
- Parameter `swapWords`: The `swapWords` value.

###### `M:IoT.DriverCore.ModbusRx.CreateExtensions.FromFloat(System.Single,System.UInt16[],System.Int32,System.Boolean)`

```csharp
public static void FromFloat(float input, ushort[] output, int start, bool swapWords)
```
Writes the float value to a register array.

- Parameter `input`: The extension receiver.
- Parameter `output`: The output array.
- Parameter `start`: The start index.
- Parameter `swapWords`: Whether to swap words.

###### `M:IoT.DriverCore.ModbusRx.CreateExtensions.ObserveDataStoreReadFrom(IoT.DriverCore.ModbusRx.Device.ModbusSlave)`

```csharp
public static System.IObservable<IoT.DriverCore.ModbusRx.Data.DataStoreEventArgs> ObserveDataStoreReadFrom(IoT.DriverCore.ModbusRx.Device.ModbusSlave slave)
```
Observes reads from the data store.

- Parameter `slave`: The extension receiver.
- Returns: An observable of data-store events.

###### `M:IoT.DriverCore.ModbusRx.CreateExtensions.ObserveDataStoreWrittenTo(IoT.DriverCore.ModbusRx.Device.ModbusSlave)`

```csharp
public static System.IObservable<IoT.DriverCore.ModbusRx.Data.DataStoreEventArgs> ObserveDataStoreWrittenTo(IoT.DriverCore.ModbusRx.Device.ModbusSlave slave)
```
Observes writes to the data store.

- Parameter `slave`: The extension receiver.
- Returns: An observable of data-store events.

###### `M:IoT.DriverCore.ModbusRx.CreateExtensions.ObserveRequest(IoT.DriverCore.ModbusRx.Device.ModbusSlave)`

```csharp
public static System.IObservable<IoT.DriverCore.ModbusRx.Device.ModbusSlaveRequestEventArgs> ObserveRequest(IoT.DriverCore.ModbusRx.Device.ModbusSlave slave)
```
Observes received slave requests.

- Parameter `slave`: The extension receiver.
- Returns: An observable of request events.

###### `M:IoT.DriverCore.ModbusRx.CreateExtensions.ObserveWriteComplete(IoT.DriverCore.ModbusRx.Device.ModbusSlave)`

```csharp
public static System.IObservable<IoT.DriverCore.ModbusRx.Device.ModbusSlaveRequestEventArgs> ObserveWriteComplete(IoT.DriverCore.ModbusRx.Device.ModbusSlave slave)
```
Observes completed writes.

- Parameter `slave`: The extension receiver.
- Returns: An observable of request events.

###### `M:IoT.DriverCore.ModbusRx.CreateExtensions.ReadCoils(System.IObservable`1{System.ValueTuple`3{System.Boolean,System.Exception,IoT.DriverCore.ModbusRx.Device.IModbusSerialMaster}},System.Byte,System.UInt16,System.UInt16,System.Double)`

```csharp
public static System.IObservable<System.ValueTuple<bool[], System.Exception>> ReadCoils(System.IObservable<System.ValueTuple<bool, System.Exception, IoT.DriverCore.ModbusRx.Device.IModbusSerialMaster>> source, byte slaveAddress, ushort startAddress, ushort numberOfPoints, double interval)
```
Executes the `ReadCoils` operation.

- Parameter `source`: The `source` value.
- Parameter `slaveAddress`: The `slaveAddress` value.
- Parameter `startAddress`: The `startAddress` value.
- Parameter `numberOfPoints`: The `numberOfPoints` value.
- Parameter `interval`: The `interval` value.
- Returns: A `System.IObservable<System.ValueTuple<bool[], System.Exception>>` result.

###### `M:IoT.DriverCore.ModbusRx.CreateExtensions.ReadCoils(System.IObservable`1{System.ValueTuple`3{System.Boolean,System.Exception,IoT.DriverCore.ModbusRx.Device.IModbusSerialMaster}},System.UInt16,System.UInt16,System.Double)`

```csharp
public static System.IObservable<System.ValueTuple<bool[], System.Exception>> ReadCoils(System.IObservable<System.ValueTuple<bool, System.Exception, IoT.DriverCore.ModbusRx.Device.IModbusSerialMaster>> source, ushort startAddress, ushort numberOfPoints, double interval)
```
Executes the `ReadCoils` operation.

- Parameter `source`: The `source` value.
- Parameter `startAddress`: The `startAddress` value.
- Parameter `numberOfPoints`: The `numberOfPoints` value.
- Parameter `interval`: The `interval` value.
- Returns: A `System.IObservable<System.ValueTuple<bool[], System.Exception>>` result.

###### `M:IoT.DriverCore.ModbusRx.CreateExtensions.ReadCoils(System.IObservable`1{System.ValueTuple`3{System.Boolean,System.Exception,IoT.DriverCore.ModbusRx.Device.ModbusIpMaster}},System.UInt16,System.UInt16,System.Double)`

```csharp
public static System.IObservable<System.ValueTuple<bool[], System.Exception>> ReadCoils(System.IObservable<System.ValueTuple<bool, System.Exception, IoT.DriverCore.ModbusRx.Device.ModbusIpMaster>> source, ushort startAddress, ushort numberOfPoints, double interval)
```
Executes the `ReadCoils` operation.

- Parameter `source`: The `source` value.
- Parameter `startAddress`: The `startAddress` value.
- Parameter `numberOfPoints`: The `numberOfPoints` value.
- Parameter `interval`: The `interval` value.
- Returns: A `System.IObservable<System.ValueTuple<bool[], System.Exception>>` result.

###### `M:IoT.DriverCore.ModbusRx.CreateExtensions.ReadHoldingRegisters(System.IObservable`1{System.ValueTuple`3{System.Boolean,System.Exception,IoT.DriverCore.ModbusRx.Device.IModbusSerialMaster}},System.Byte,System.UInt16,System.UInt16,System.Double)`

```csharp
public static System.IObservable<System.ValueTuple<ushort[], System.Exception>> ReadHoldingRegisters(System.IObservable<System.ValueTuple<bool, System.Exception, IoT.DriverCore.ModbusRx.Device.IModbusSerialMaster>> source, byte slaveAddress, ushort startAddress, ushort numberOfPoints, double interval)
```
Executes the `ReadHoldingRegisters` operation.

- Parameter `source`: The `source` value.
- Parameter `slaveAddress`: The `slaveAddress` value.
- Parameter `startAddress`: The `startAddress` value.
- Parameter `numberOfPoints`: The `numberOfPoints` value.
- Parameter `interval`: The `interval` value.
- Returns: A `System.IObservable<System.ValueTuple<ushort[], System.Exception>>` result.

###### `M:IoT.DriverCore.ModbusRx.CreateExtensions.ReadHoldingRegisters(System.IObservable`1{System.ValueTuple`3{System.Boolean,System.Exception,IoT.DriverCore.ModbusRx.Device.IModbusSerialMaster}},System.UInt16,System.UInt16,System.Double)`

```csharp
public static System.IObservable<System.ValueTuple<ushort[], System.Exception>> ReadHoldingRegisters(System.IObservable<System.ValueTuple<bool, System.Exception, IoT.DriverCore.ModbusRx.Device.IModbusSerialMaster>> source, ushort startAddress, ushort numberOfPoints, double interval)
```
Executes the `ReadHoldingRegisters` operation.

- Parameter `source`: The `source` value.
- Parameter `startAddress`: The `startAddress` value.
- Parameter `numberOfPoints`: The `numberOfPoints` value.
- Parameter `interval`: The `interval` value.
- Returns: A `System.IObservable<System.ValueTuple<ushort[], System.Exception>>` result.

###### `M:IoT.DriverCore.ModbusRx.CreateExtensions.ReadHoldingRegisters(System.IObservable`1{System.ValueTuple`3{System.Boolean,System.Exception,IoT.DriverCore.ModbusRx.Device.ModbusIpMaster}},System.UInt16,System.UInt16,System.Double)`

```csharp
public static System.IObservable<System.ValueTuple<ushort[], System.Exception>> ReadHoldingRegisters(System.IObservable<System.ValueTuple<bool, System.Exception, IoT.DriverCore.ModbusRx.Device.ModbusIpMaster>> source, ushort startAddress, ushort numberOfPoints, double interval)
```
Executes the `ReadHoldingRegisters` operation.

- Parameter `source`: The `source` value.
- Parameter `startAddress`: The `startAddress` value.
- Parameter `numberOfPoints`: The `numberOfPoints` value.
- Parameter `interval`: The `interval` value.
- Returns: A `System.IObservable<System.ValueTuple<ushort[], System.Exception>>` result.

###### `M:IoT.DriverCore.ModbusRx.CreateExtensions.ReadInputRegisters(System.IObservable`1{System.ValueTuple`3{System.Boolean,System.Exception,IoT.DriverCore.ModbusRx.Device.IModbusSerialMaster}},System.Byte,System.UInt16,System.UInt16,System.Double)`

```csharp
public static System.IObservable<System.ValueTuple<ushort[], System.Exception>> ReadInputRegisters(System.IObservable<System.ValueTuple<bool, System.Exception, IoT.DriverCore.ModbusRx.Device.IModbusSerialMaster>> source, byte slaveAddress, ushort startAddress, ushort numberOfPoints, double interval)
```
Executes the `ReadInputRegisters` operation.

- Parameter `source`: The `source` value.
- Parameter `slaveAddress`: The `slaveAddress` value.
- Parameter `startAddress`: The `startAddress` value.
- Parameter `numberOfPoints`: The `numberOfPoints` value.
- Parameter `interval`: The `interval` value.
- Returns: A `System.IObservable<System.ValueTuple<ushort[], System.Exception>>` result.

###### `M:IoT.DriverCore.ModbusRx.CreateExtensions.ReadInputRegisters(System.IObservable`1{System.ValueTuple`3{System.Boolean,System.Exception,IoT.DriverCore.ModbusRx.Device.IModbusSerialMaster}},System.UInt16,System.UInt16,System.Double)`

```csharp
public static System.IObservable<System.ValueTuple<ushort[], System.Exception>> ReadInputRegisters(System.IObservable<System.ValueTuple<bool, System.Exception, IoT.DriverCore.ModbusRx.Device.IModbusSerialMaster>> source, ushort startAddress, ushort numberOfPoints, double interval)
```
Executes the `ReadInputRegisters` operation.

- Parameter `source`: The `source` value.
- Parameter `startAddress`: The `startAddress` value.
- Parameter `numberOfPoints`: The `numberOfPoints` value.
- Parameter `interval`: The `interval` value.
- Returns: A `System.IObservable<System.ValueTuple<ushort[], System.Exception>>` result.

###### `M:IoT.DriverCore.ModbusRx.CreateExtensions.ReadInputRegisters(System.IObservable`1{System.ValueTuple`3{System.Boolean,System.Exception,IoT.DriverCore.ModbusRx.Device.ModbusIpMaster}},System.UInt16,System.UInt16,System.Double)`

```csharp
public static System.IObservable<System.ValueTuple<ushort[], System.Exception>> ReadInputRegisters(System.IObservable<System.ValueTuple<bool, System.Exception, IoT.DriverCore.ModbusRx.Device.ModbusIpMaster>> source, ushort startAddress, ushort numberOfPoints, double interval)
```
Executes the `ReadInputRegisters` operation.

- Parameter `source`: The `source` value.
- Parameter `startAddress`: The `startAddress` value.
- Parameter `numberOfPoints`: The `numberOfPoints` value.
- Parameter `interval`: The `interval` value.
- Returns: A `System.IObservable<System.ValueTuple<ushort[], System.Exception>>` result.

###### `M:IoT.DriverCore.ModbusRx.CreateExtensions.ReadInputs(System.IObservable`1{System.ValueTuple`3{System.Boolean,System.Exception,IoT.DriverCore.ModbusRx.Device.IModbusSerialMaster}},System.Byte,System.UInt16,System.UInt16,System.Double)`

```csharp
public static System.IObservable<System.ValueTuple<bool[], System.Exception>> ReadInputs(System.IObservable<System.ValueTuple<bool, System.Exception, IoT.DriverCore.ModbusRx.Device.IModbusSerialMaster>> source, byte slaveAddress, ushort startAddress, ushort numberOfPoints, double interval)
```
Executes the `ReadInputs` operation.

- Parameter `source`: The `source` value.
- Parameter `slaveAddress`: The `slaveAddress` value.
- Parameter `startAddress`: The `startAddress` value.
- Parameter `numberOfPoints`: The `numberOfPoints` value.
- Parameter `interval`: The `interval` value.
- Returns: A `System.IObservable<System.ValueTuple<bool[], System.Exception>>` result.

###### `M:IoT.DriverCore.ModbusRx.CreateExtensions.ReadInputs(System.IObservable`1{System.ValueTuple`3{System.Boolean,System.Exception,IoT.DriverCore.ModbusRx.Device.IModbusSerialMaster}},System.UInt16,System.UInt16,System.Double)`

```csharp
public static System.IObservable<System.ValueTuple<bool[], System.Exception>> ReadInputs(System.IObservable<System.ValueTuple<bool, System.Exception, IoT.DriverCore.ModbusRx.Device.IModbusSerialMaster>> source, ushort startAddress, ushort numberOfPoints, double interval)
```
Executes the `ReadInputs` operation.

- Parameter `source`: The `source` value.
- Parameter `startAddress`: The `startAddress` value.
- Parameter `numberOfPoints`: The `numberOfPoints` value.
- Parameter `interval`: The `interval` value.
- Returns: A `System.IObservable<System.ValueTuple<bool[], System.Exception>>` result.

###### `M:IoT.DriverCore.ModbusRx.CreateExtensions.ReadInputs(System.IObservable`1{System.ValueTuple`3{System.Boolean,System.Exception,IoT.DriverCore.ModbusRx.Device.ModbusIpMaster}},System.UInt16,System.UInt16,System.Double)`

```csharp
public static System.IObservable<System.ValueTuple<bool[], System.Exception>> ReadInputs(System.IObservable<System.ValueTuple<bool, System.Exception, IoT.DriverCore.ModbusRx.Device.ModbusIpMaster>> source, ushort startAddress, ushort numberOfPoints, double interval)
```
Executes the `ReadInputs` operation.

- Parameter `source`: The `source` value.
- Parameter `startAddress`: The `startAddress` value.
- Parameter `numberOfPoints`: The `numberOfPoints` value.
- Parameter `interval`: The `interval` value.
- Returns: A `System.IObservable<System.ValueTuple<bool[], System.Exception>>` result.

###### `M:IoT.DriverCore.ModbusRx.CreateExtensions.ToDouble(System.ReadOnlySpan`1{System.UInt16},System.Int32,System.Boolean)`

```csharp
public static System.Nullable<double> ToDouble(System.ReadOnlySpan<ushort> inputs, int start, bool swapWords)
```
Executes the `ToDouble` operation.

- Parameter `inputs`: The `inputs` value.
- Parameter `start`: The `start` value.
- Parameter `swapWords`: The `swapWords` value.
- Returns: A `System.Nullable<double>` result.

###### `M:IoT.DriverCore.ModbusRx.CreateExtensions.ToDouble(System.UInt16[],System.Int32,System.Boolean)`

```csharp
public static System.Nullable<double> ToDouble(ushort[] inputs, int start, bool swapWords)
```
Converts register data to a double.

- Parameter `inputs`: The extension receiver.
- Parameter `start`: The start index.
- Parameter `swapWords`: Whether to swap words.
- Returns: A double value or null if insufficient data is available.

###### `M:IoT.DriverCore.ModbusRx.CreateExtensions.ToFloat(System.ReadOnlySpan`1{System.UInt16},System.Int32,System.Boolean)`

```csharp
public static System.Nullable<float> ToFloat(System.ReadOnlySpan<ushort> inputs, int start, bool swapWords)
```
Executes the `ToFloat` operation.

- Parameter `inputs`: The `inputs` value.
- Parameter `start`: The `start` value.
- Parameter `swapWords`: The `swapWords` value.
- Returns: A `System.Nullable<float>` result.

###### `M:IoT.DriverCore.ModbusRx.CreateExtensions.ToFloat(System.UInt16[],System.Int32,System.Boolean)`

```csharp
public static System.Nullable<float> ToFloat(ushort[] inputs, int start, bool swapWords)
```
Converts register data to a float.

- Parameter `inputs`: The extension receiver.
- Parameter `start`: The start index.
- Parameter `swapWords`: Whether to swap words.
- Returns: A float value or null if insufficient data is available.

#### `T:IoT.DriverCore.ModbusRx.Data.BooleanPattern`

```csharp
public enum IoT.DriverCore.ModbusRx.Data.BooleanPattern
```
Boolean pattern types.

##### Declared public members

###### `F:IoT.DriverCore.ModbusRx.Data.BooleanPattern.AllFalse`

```csharp
public static const IoT.DriverCore.ModbusRx.Data.BooleanPattern AllFalse
```
All false values.

###### `F:IoT.DriverCore.ModbusRx.Data.BooleanPattern.AllTrue`

```csharp
public static const IoT.DriverCore.ModbusRx.Data.BooleanPattern AllTrue
```
All true values.

###### `F:IoT.DriverCore.ModbusRx.Data.BooleanPattern.Alternating`

```csharp
public static const IoT.DriverCore.ModbusRx.Data.BooleanPattern Alternating
```
Alternating true/false pattern.

###### `F:IoT.DriverCore.ModbusRx.Data.BooleanPattern.Random`

```csharp
public static const IoT.DriverCore.ModbusRx.Data.BooleanPattern Random
```
Random true/false values.

#### `T:IoT.DriverCore.ModbusRx.Data.DataStore`

```csharp
public class IoT.DriverCore.ModbusRx.Data.DataStore
```
Object simulation of device memory map. The underlying collections are thread safe when using the ModbusMaster API to read/write values. You can use the SyncRoot property to synchronize direct access to the DataStore collections.

##### Declared public members

###### `E:IoT.DriverCore.ModbusRx.Data.DataStore.DataStoreReadFrom`

```csharp
public event System.EventHandler<IoT.DriverCore.ModbusRx.Data.DataStoreEventArgs> DataStoreReadFrom
```
Occurs when the DataStore is read from via a Modbus command.

###### `E:IoT.DriverCore.ModbusRx.Data.DataStore.DataStoreWrittenTo`

```csharp
public event System.EventHandler<IoT.DriverCore.ModbusRx.Data.DataStoreEventArgs> DataStoreWrittenTo
```
Occurs when the DataStore is written to via a Modbus command.

###### `M:IoT.DriverCore.ModbusRx.Data.DataStore.#ctor`

```csharp
public IoT.DriverCore.ModbusRx.Data.DataStore()
```
Initializes a new instance of the `T:IoT.DriverCore.ModbusRx.Data.DataStore` class.

###### `M:IoT.DriverCore.ModbusRx.Data.DataStore.Dispose`

```csharp
public void Dispose()
```
Disposes the DataStore and releases resources.

###### `M:IoT.DriverCore.ModbusRx.Data.DataStore.GetOperationMetrics`

```csharp
public IoT.DriverCore.ModbusRx.Data.DataStoreOperationMetrics GetOperationMetrics()
```
Gets a deterministic snapshot of data-store range-operation work.

- Returns: The current range-operation counters.

###### `M:IoT.DriverCore.ModbusRx.Data.DataStore.ReadDataOptimized``2(IoT.DriverCore.ModbusRx.Data.ModbusDataCollection`1{``1},System.Func`1{``0},System.UInt16,System.UInt16)`

```csharp
public T ReadDataOptimized<T, TU>(IoT.DriverCore.ModbusRx.Data.ModbusDataCollection<TU> dataSource, System.Func<T> resultFactory, ushort startAddress, ushort count)
```
Executes the `ReadDataOptimized` operation.

- Parameter `dataSource`: The `dataSource` value.
- Parameter `resultFactory`: The `resultFactory` value.
- Parameter `startAddress`: The `startAddress` value.
- Parameter `count`: The `count` value.
- Returns: A `T` result.

###### `M:IoT.DriverCore.ModbusRx.Data.DataStore.WriteDataOptimized``1(System.Collections.Generic.IEnumerable`1{``0},IoT.DriverCore.ModbusRx.Data.ModbusDataCollection`1{``0},System.UInt16)`

```csharp
public void WriteDataOptimized<TData>(System.Collections.Generic.IEnumerable<TData> items, IoT.DriverCore.ModbusRx.Data.ModbusDataCollection<TData> destination, ushort startAddress)
```
Executes the `WriteDataOptimized` operation.

- Parameter `items`: The `items` value.
- Parameter `destination`: The `destination` value.
- Parameter `startAddress`: The `startAddress` value.

###### `P:IoT.DriverCore.ModbusRx.Data.DataStore.CoilDiscretes`

```csharp
public IoT.DriverCore.ModbusRx.Data.ModbusDataCollection<bool> CoilDiscretes { get; }
```
Gets the discrete coils.

- Value: The `CoilDiscretes` value.

###### `P:IoT.DriverCore.ModbusRx.Data.DataStore.HoldingRegisters`

```csharp
public IoT.DriverCore.ModbusRx.Data.ModbusDataCollection<ushort> HoldingRegisters { get; }
```
Gets the holding registers.

- Value: The `HoldingRegisters` value.

###### `P:IoT.DriverCore.ModbusRx.Data.DataStore.InputDiscretes`

```csharp
public IoT.DriverCore.ModbusRx.Data.ModbusDataCollection<bool> InputDiscretes { get; }
```
Gets the discrete inputs.

- Value: The `InputDiscretes` value.

###### `P:IoT.DriverCore.ModbusRx.Data.DataStore.InputRegisters`

```csharp
public IoT.DriverCore.ModbusRx.Data.ModbusDataCollection<ushort> InputRegisters { get; }
```
Gets the input registers.

- Value: The `InputRegisters` value.

###### `P:IoT.DriverCore.ModbusRx.Data.DataStore.Lock`

```csharp
public System.Threading.ReaderWriterLockSlim Lock { get; }
```
Gets the reader-writer lock for more granular access control.

- Value: The `Lock` value.

###### `P:IoT.DriverCore.ModbusRx.Data.DataStore.SyncRoot`

```csharp
public object SyncRoot { get; }
```
Gets an object that can be used to synchronize direct access to the DataStore collections.

- Value: The `SyncRoot` value.

#### `T:IoT.DriverCore.ModbusRx.Data.DataStoreEventArgs`

```csharp
public class IoT.DriverCore.ModbusRx.Data.DataStoreEventArgs
```
Event args for read write actions performed on the DataStore.

##### Declared public members

###### `P:IoT.DriverCore.ModbusRx.Data.DataStoreEventArgs.Data`

```csharp
public IoT.DriverCore.ModbusRx.Utility.DiscriminatedUnion<System.Collections.ObjectModel.ReadOnlyCollection<bool>, System.Collections.ObjectModel.ReadOnlyCollection<ushort>> Data { get; }
```
Gets data that was read or written.

- Value: The `Data` value.

###### `P:IoT.DriverCore.ModbusRx.Data.DataStoreEventArgs.ModbusDataType`

```csharp
public IoT.DriverCore.ModbusRx.Data.ModbusDataType ModbusDataType { get; }
```
Gets type of Modbus data (e.g. Holding register).

- Value: The `ModbusDataType` value.

###### `P:IoT.DriverCore.ModbusRx.Data.DataStoreEventArgs.StartAddress`

```csharp
public ushort StartAddress { get; }
```
Gets start address of data.

- Value: The `StartAddress` value.

#### `T:IoT.DriverCore.ModbusRx.Data.DataStoreExtensions`

```csharp
public class IoT.DriverCore.ModbusRx.Data.DataStoreExtensions
```
High-performance extensions for DataStore operations using optimized techniques.

##### Declared public members

###### `M:IoT.DriverCore.ModbusRx.Data.DataStoreExtensions.BulkCopyCoils(IoT.DriverCore.ModbusRx.Data.DataStore,IoT.DriverCore.ModbusRx.Data.DataStore,System.UInt16,System.UInt16)`

```csharp
public static void BulkCopyCoils(IoT.DriverCore.ModbusRx.Data.DataStore dataStore, IoT.DriverCore.ModbusRx.Data.DataStore destinationStore, ushort startAddress, ushort count)
```
Performs a bulk copy operation for coils between data stores with high performance.

- Parameter `dataStore`: The extension receiver.
- Parameter `destinationStore`: The destination data store.
- Parameter `startAddress`: The start address.
- Parameter `count`: The number of elements to copy.

###### `M:IoT.DriverCore.ModbusRx.Data.DataStoreExtensions.BulkCopyHoldingRegisters(IoT.DriverCore.ModbusRx.Data.DataStore,IoT.DriverCore.ModbusRx.Data.DataStore,System.UInt16,System.UInt16)`

```csharp
public static void BulkCopyHoldingRegisters(IoT.DriverCore.ModbusRx.Data.DataStore dataStore, IoT.DriverCore.ModbusRx.Data.DataStore destinationStore, ushort startAddress, ushort count)
```
Performs a bulk copy operation between data stores with high performance.

- Parameter `dataStore`: The extension receiver.
- Parameter `destinationStore`: The destination data store.
- Parameter `startAddress`: The start address.
- Parameter `count`: The number of elements to copy.

###### `M:IoT.DriverCore.ModbusRx.Data.DataStoreExtensions.ClearCoils(IoT.DriverCore.ModbusRx.Data.DataStore,System.UInt16,System.UInt16)`

```csharp
public static void ClearCoils(IoT.DriverCore.ModbusRx.Data.DataStore dataStore, ushort startAddress, ushort count)
```
Clears a range of coils with high performance.

- Parameter `dataStore`: The extension receiver.
- Parameter `startAddress`: The start address.
- Parameter `count`: The number of coils to clear.

###### `M:IoT.DriverCore.ModbusRx.Data.DataStoreExtensions.ClearHoldingRegisters(IoT.DriverCore.ModbusRx.Data.DataStore,System.UInt16,System.UInt16)`

```csharp
public static void ClearHoldingRegisters(IoT.DriverCore.ModbusRx.Data.DataStore dataStore, ushort startAddress, ushort count)
```
Clears a range of holding registers with high performance.

- Parameter `dataStore`: The extension receiver.
- Parameter `startAddress`: The start address.
- Parameter `count`: The number of registers to clear.

###### `M:IoT.DriverCore.ModbusRx.Data.DataStoreExtensions.CompareHoldingRegisters(IoT.DriverCore.ModbusRx.Data.DataStore,IoT.DriverCore.ModbusRx.Data.DataStore,System.UInt16,System.UInt16)`

```csharp
public static bool CompareHoldingRegisters(IoT.DriverCore.ModbusRx.Data.DataStore dataStore, IoT.DriverCore.ModbusRx.Data.DataStore store2, ushort startAddress, ushort count)
```
Performs a memory-efficient comparison between two data stores.

- Parameter `dataStore`: The extension receiver.
- Parameter `store2`: The second data store.
- Parameter `startAddress`: The start address.
- Parameter `count`: The number of elements to compare.
- Returns: True if the data ranges are identical.

###### `M:IoT.DriverCore.ModbusRx.Data.DataStoreExtensions.ReadCoilsOptimized(IoT.DriverCore.ModbusRx.Data.DataStore,System.UInt16,System.UInt16)`

```csharp
public static bool[] ReadCoilsOptimized(IoT.DriverCore.ModbusRx.Data.DataStore dataStore, ushort startAddress, ushort count)
```
Reads coils with optimized performance.

- Parameter `dataStore`: The extension receiver.
- Parameter `startAddress`: The start address.
- Parameter `count`: The count.
- Returns: Array of coil values.

###### `M:IoT.DriverCore.ModbusRx.Data.DataStoreExtensions.ReadHoldingRegistersOptimized(IoT.DriverCore.ModbusRx.Data.DataStore,System.UInt16,System.UInt16)`

```csharp
public static ushort[] ReadHoldingRegistersOptimized(IoT.DriverCore.ModbusRx.Data.DataStore dataStore, ushort startAddress, ushort count)
```
Reads holding registers with optimized performance.

- Parameter `dataStore`: The extension receiver.
- Parameter `startAddress`: The start address.
- Parameter `count`: The count.
- Returns: Array of register values.

###### `M:IoT.DriverCore.ModbusRx.Data.DataStoreExtensions.ReadInputRegistersOptimized(IoT.DriverCore.ModbusRx.Data.DataStore,System.UInt16,System.UInt16)`

```csharp
public static ushort[] ReadInputRegistersOptimized(IoT.DriverCore.ModbusRx.Data.DataStore dataStore, ushort startAddress, ushort count)
```
Reads input registers with optimized performance.

- Parameter `dataStore`: The extension receiver.
- Parameter `startAddress`: The start address.
- Parameter `count`: The count.
- Returns: Array of register values.

###### `M:IoT.DriverCore.ModbusRx.Data.DataStoreExtensions.ReadInputsOptimized(IoT.DriverCore.ModbusRx.Data.DataStore,System.UInt16,System.UInt16)`

```csharp
public static bool[] ReadInputsOptimized(IoT.DriverCore.ModbusRx.Data.DataStore dataStore, ushort startAddress, ushort count)
```
Reads discrete inputs with optimized performance.

- Parameter `dataStore`: The extension receiver.
- Parameter `startAddress`: The start address.
- Parameter `count`: The count.
- Returns: Array of input values.

###### `M:IoT.DriverCore.ModbusRx.Data.DataStoreExtensions.WriteCoilsOptimized(IoT.DriverCore.ModbusRx.Data.DataStore,System.UInt16,System.Boolean[])`

```csharp
public static void WriteCoilsOptimized(IoT.DriverCore.ModbusRx.Data.DataStore dataStore, ushort startAddress, bool[] values)
```
Writes coils with optimized performance.

- Parameter `dataStore`: The extension receiver.
- Parameter `startAddress`: The start address.
- Parameter `values`: The values to write.

###### `M:IoT.DriverCore.ModbusRx.Data.DataStoreExtensions.WriteHoldingRegistersOptimized(IoT.DriverCore.ModbusRx.Data.DataStore,System.UInt16,System.UInt16[])`

```csharp
public static void WriteHoldingRegistersOptimized(IoT.DriverCore.ModbusRx.Data.DataStore dataStore, ushort startAddress, ushort[] values)
```
Writes holding registers with optimized performance.

- Parameter `dataStore`: The extension receiver.
- Parameter `startAddress`: The start address.
- Parameter `values`: The values to write.

#### `T:IoT.DriverCore.ModbusRx.Data.DataStoreFactory`

```csharp
public class IoT.DriverCore.ModbusRx.Data.DataStoreFactory
```
Data story factory.

##### Declared public members

###### `M:IoT.DriverCore.ModbusRx.Data.DataStoreFactory.CreateDefaultDataStore`

```csharp
public static IoT.DriverCore.ModbusRx.Data.DataStore CreateDefaultDataStore()
```
Creates a default data store with zeroed registers and false discrete values.

- Returns: A DataStore.

###### `M:IoT.DriverCore.ModbusRx.Data.DataStoreFactory.CreateDefaultDataStore(System.UInt16,System.UInt16,System.UInt16,System.UInt16)`

```csharp
public static IoT.DriverCore.ModbusRx.Data.DataStore CreateDefaultDataStore(ushort coilsCount, ushort inputsCount, ushort holdingRegistersCount, ushort inputRegistersCount)
```
Creates a default data store with zeroed registers and false discrete values.

- Parameter `coilsCount`: Number of discrete coils.
- Parameter `inputsCount`: Number of discrete inputs.
- Parameter `holdingRegistersCount`: Number of holding registers.
- Parameter `inputRegistersCount`: Number of input registers.
- Returns: New instance of Data store with defined inputs/outputs.

#### `T:IoT.DriverCore.ModbusRx.Data.DataStoreOperationMetrics`

```csharp
public class IoT.DriverCore.ModbusRx.Data.DataStoreOperationMetrics
```
Provides deterministic operation counters for data-store range operations.

##### Declared public members

###### `M:IoT.DriverCore.ModbusRx.Data.DataStoreOperationMetrics.#ctor(System.Int64,System.Int64,System.Int64,System.Int64,System.Int64)`

```csharp
public IoT.DriverCore.ModbusRx.Data.DataStoreOperationMetrics(long readOperations, long writeOperations, long elementCopies, long resultCollectionAllocations, long inputMaterializations)
```
Initializes a new instance of the `T:IoT.DriverCore.ModbusRx.Data.DataStoreOperationMetrics` class.

- Parameter `readOperations`: The number of completed range reads.
- Parameter `writeOperations`: The number of completed range writes.
- Parameter `elementCopies`: The number of elements copied between data-store ranges and results.
- Parameter `resultCollectionAllocations`: The number of result collections created for reads.
- Parameter `inputMaterializations`: The number of non-indexable write inputs materialized once.

###### `P:IoT.DriverCore.ModbusRx.Data.DataStoreOperationMetrics.ElementCopies`

```csharp
public long ElementCopies { get; }
```
Gets the number of elements copied by range operations.

- Value: The `ElementCopies` value.

###### `P:IoT.DriverCore.ModbusRx.Data.DataStoreOperationMetrics.InputMaterializations`

```csharp
public long InputMaterializations { get; }
```
Gets the number of non-indexable write inputs materialized exactly once.

- Value: The `InputMaterializations` value.

###### `P:IoT.DriverCore.ModbusRx.Data.DataStoreOperationMetrics.ReadOperations`

```csharp
public long ReadOperations { get; }
```
Gets the number of completed range reads.

- Value: The `ReadOperations` value.

###### `P:IoT.DriverCore.ModbusRx.Data.DataStoreOperationMetrics.ResultCollectionAllocations`

```csharp
public long ResultCollectionAllocations { get; }
```
Gets the number of result collections created by reads.

- Value: The `ResultCollectionAllocations` value.

###### `P:IoT.DriverCore.ModbusRx.Data.DataStoreOperationMetrics.WriteOperations`

```csharp
public long WriteOperations { get; }
```
Gets the number of completed range writes.

- Value: The `WriteOperations` value.

#### `T:IoT.DriverCore.ModbusRx.Data.DiscreteCollection`

```csharp
public class IoT.DriverCore.ModbusRx.Data.DiscreteCollection
```
Collection of discrete values.

##### Declared public members

###### `M:IoT.DriverCore.ModbusRx.Data.DiscreteCollection.#ctor`

```csharp
public IoT.DriverCore.ModbusRx.Data.DiscreteCollection()
```
Initializes a new instance of the `T:IoT.DriverCore.ModbusRx.Data.DiscreteCollection` class.

###### `M:IoT.DriverCore.ModbusRx.Data.DiscreteCollection.#ctor(System.Boolean[])`

```csharp
public IoT.DriverCore.ModbusRx.Data.DiscreteCollection(bool[] bits)
```
Initializes a new instance of the `T:IoT.DriverCore.ModbusRx.Data.DiscreteCollection` class.

- Parameter `bits`: Array for discrete collection.

###### `M:IoT.DriverCore.ModbusRx.Data.DiscreteCollection.#ctor(System.Byte[])`

```csharp
public IoT.DriverCore.ModbusRx.Data.DiscreteCollection(byte[] bytes)
```
Initializes a new instance of the `T:IoT.DriverCore.ModbusRx.Data.DiscreteCollection` class.

- Parameter `bytes`: Array for discrete collection.

###### `M:IoT.DriverCore.ModbusRx.Data.DiscreteCollection.#ctor(System.Collections.Generic.IList`1{System.Boolean})`

```csharp
public IoT.DriverCore.ModbusRx.Data.DiscreteCollection(System.Collections.Generic.IList<bool> bits)
```
Initializes a new instance of `IoT.DriverCore.ModbusRx.Data.DiscreteCollection`.

- Parameter `bits`: The `bits` value.

###### `M:IoT.DriverCore.ModbusRx.Data.DiscreteCollection.ToString`

```csharp
public string ToString()
```
Returns a string that represents the current object.

- Returns: A `T:System.String` that represents the current `T:System.Object` .

###### `P:IoT.DriverCore.ModbusRx.Data.DiscreteCollection.ByteCount`

```csharp
public byte ByteCount { get; }
```
Gets the byte count.

- Value: The `ByteCount` value.

###### `P:IoT.DriverCore.ModbusRx.Data.DiscreteCollection.NetworkBytes`

```csharp
public byte[] NetworkBytes { get; }
```
Gets the network bytes.

- Value: The `NetworkBytes` value.

#### `T:IoT.DriverCore.ModbusRx.Data.IDataCollection`

```csharp
public interface IoT.DriverCore.ModbusRx.Data.IDataCollection
```
Modbus message containing data.

##### Declared public members

###### `P:IoT.DriverCore.ModbusRx.Data.IDataCollection.ByteCount`

```csharp
public byte ByteCount { get; }
```
Gets the byte count.

- Value: The `ByteCount` value.

###### `P:IoT.DriverCore.ModbusRx.Data.IDataCollection.NetworkBytes`

```csharp
public byte[] NetworkBytes { get; }
```
Gets the network bytes.

- Value: The `NetworkBytes` value.

#### `T:IoT.DriverCore.ModbusRx.Data.ModbusDataCollection`1`

```csharp
public class IoT.DriverCore.ModbusRx.Data.ModbusDataCollection`1
```
A 1 origin collection represetative of the Modbus Data Model.

##### Declared public members

###### `M:IoT.DriverCore.ModbusRx.Data.ModbusDataCollection`1.#ctor`

```csharp
public IoT.DriverCore.ModbusRx.Data.ModbusDataCollection<TData>()
```
Initializes a new instance of the `T:IoT.DriverCore.ModbusRx.Data.ModbusDataCollection`1` class.

###### `M:IoT.DriverCore.ModbusRx.Data.ModbusDataCollection`1.#ctor(System.Collections.Generic.IList`1{`0})`

```csharp
public IoT.DriverCore.ModbusRx.Data.ModbusDataCollection<TData>(System.Collections.Generic.IList<TData> data)
```
Initializes a new instance of `IoT.DriverCore.ModbusRx.Data.ModbusDataCollection`1`.

- Parameter `data`: The `data` value.

###### `M:IoT.DriverCore.ModbusRx.Data.ModbusDataCollection`1.#ctor(`0[])`

```csharp
public IoT.DriverCore.ModbusRx.Data.ModbusDataCollection<TData>(TData[] data)
```
Initializes a new instance of the `T:IoT.DriverCore.ModbusRx.Data.ModbusDataCollection`1` class.

- Parameter `data`: The data.

#### `T:IoT.DriverCore.ModbusRx.Data.ModbusDataExtensions`

```csharp
public class IoT.DriverCore.ModbusRx.Data.ModbusDataExtensions
```
High-performance data conversion extensions optimized for different target frameworks.

##### Declared public members

###### `M:IoT.DriverCore.ModbusRx.Data.ModbusDataExtensions.FastEquals(System.Byte[],System.Byte[])`

```csharp
public static bool FastEquals(byte[] bytes, byte[] array2)
```
Performs a fast memory comparison between two byte arrays.

- Parameter `bytes`: The extension receiver.
- Parameter `array2`: The second array.
- Returns: True if arrays are equal.

###### `M:IoT.DriverCore.ModbusRx.Data.ModbusDataExtensions.PackBooleans(System.Boolean[])`

```csharp
public static byte[] PackBooleans(bool[] values)
```
Packs boolean values into bytes with optimized performance.

- Parameter `values`: The extension receiver.
- Returns: Array of bytes containing packed boolean values.

###### `M:IoT.DriverCore.ModbusRx.Data.ModbusDataExtensions.ToInt32(System.UInt16[],System.Int32,System.Boolean)`

```csharp
public static int ToInt32(ushort[] registers, int startIndex, bool swapWords)
```
Converts two 16-bit registers to a 32-bit integer with optimized performance.

- Parameter `registers`: The extension receiver.
- Parameter `startIndex`: The start index.
- Parameter `swapWords`: Whether words are swapped.
- Returns: The 32-bit integer value.

###### `M:IoT.DriverCore.ModbusRx.Data.ModbusDataExtensions.ToInt64(System.UInt16[],System.Int32,System.Boolean)`

```csharp
public static long ToInt64(ushort[] registers, int startIndex, bool swapWords)
```
Converts four 16-bit registers to a 64-bit long with optimized performance.

- Parameter `registers`: The extension receiver.
- Parameter `startIndex`: The start index.
- Parameter `swapWords`: Whether words are swapped.
- Returns: The 64-bit long value.

###### `M:IoT.DriverCore.ModbusRx.Data.ModbusDataExtensions.ToRegisters(System.Int32,System.Boolean)`

```csharp
public static ushort[] ToRegisters(int value, bool swapWords)
```
Converts a 32-bit integer to two 16-bit registers with optimized performance.

- Parameter `value`: The extension receiver.
- Parameter `swapWords`: Whether to swap word order.
- Returns: Array containing two 16-bit register values.

###### `M:IoT.DriverCore.ModbusRx.Data.ModbusDataExtensions.ToRegisters(System.Int64,System.Boolean)`

```csharp
public static ushort[] ToRegisters(long value, bool swapWords)
```
Converts a 64-bit long to four 16-bit registers with optimized performance.

- Parameter `value`: The extension receiver.
- Parameter `swapWords`: Whether to swap word order.
- Returns: Array containing four 16-bit register values.

###### `M:IoT.DriverCore.ModbusRx.Data.ModbusDataExtensions.ToRegisters(System.UInt32,System.Boolean)`

```csharp
public static ushort[] ToRegisters(uint value, bool swapWords)
```
Converts a 32-bit unsigned integer to two 16-bit registers with optimized performance.

- Parameter `value`: The extension receiver.
- Parameter `swapWords`: Whether to swap word order.
- Returns: Array containing two 16-bit register values.

###### `M:IoT.DriverCore.ModbusRx.Data.ModbusDataExtensions.ToUInt32(System.UInt16[],System.Int32,System.Boolean)`

```csharp
public static uint ToUInt32(ushort[] registers, int startIndex, bool swapWords)
```
Converts two 16-bit registers to a 32-bit unsigned integer with optimized performance.

- Parameter `registers`: The extension receiver.
- Parameter `startIndex`: The start index.
- Parameter `swapWords`: Whether words are swapped.
- Returns: The 32-bit unsigned integer value.

###### `M:IoT.DriverCore.ModbusRx.Data.ModbusDataExtensions.UnpackBooleans(System.Byte[],System.Int32)`

```csharp
public static bool[] UnpackBooleans(byte[] bytes, int numberOfBooleans)
```
Unpacks bytes into boolean values with optimized performance.

- Parameter `bytes`: The extension receiver.
- Parameter `numberOfBooleans`: The number of boolean values to extract.
- Returns: Array of boolean values.

#### `T:IoT.DriverCore.ModbusRx.Data.ModbusDataType`

```csharp
public enum IoT.DriverCore.ModbusRx.Data.ModbusDataType
```
Types of data supported by the Modbus protocol.

##### Declared public members

###### `F:IoT.DriverCore.ModbusRx.Data.ModbusDataType.Coil`

```csharp
public static const IoT.DriverCore.ModbusRx.Data.ModbusDataType Coil
```
Read/write discrete.

###### `F:IoT.DriverCore.ModbusRx.Data.ModbusDataType.HoldingRegister`

```csharp
public static const IoT.DriverCore.ModbusRx.Data.ModbusDataType HoldingRegister
```
Read/write register.

###### `F:IoT.DriverCore.ModbusRx.Data.ModbusDataType.Input`

```csharp
public static const IoT.DriverCore.ModbusRx.Data.ModbusDataType Input
```
Readonly discrete.

###### `F:IoT.DriverCore.ModbusRx.Data.ModbusDataType.InputRegister`

```csharp
public static const IoT.DriverCore.ModbusRx.Data.ModbusDataType InputRegister
```
Readonly register.

#### `T:IoT.DriverCore.ModbusRx.Data.RegisterCollection`

```csharp
public class IoT.DriverCore.ModbusRx.Data.RegisterCollection
```
Collection of 16 bit registers.

##### Declared public members

###### `M:IoT.DriverCore.ModbusRx.Data.RegisterCollection.#ctor`

```csharp
public IoT.DriverCore.ModbusRx.Data.RegisterCollection()
```
Initializes a new instance of the `T:IoT.DriverCore.ModbusRx.Data.RegisterCollection` class.

###### `M:IoT.DriverCore.ModbusRx.Data.RegisterCollection.#ctor(System.Byte[])`

```csharp
public IoT.DriverCore.ModbusRx.Data.RegisterCollection(byte[] bytes)
```
Initializes a new instance of the `T:IoT.DriverCore.ModbusRx.Data.RegisterCollection` class.

- Parameter `bytes`: Array for register collection.

###### `M:IoT.DriverCore.ModbusRx.Data.RegisterCollection.#ctor(System.Collections.Generic.IList`1{System.UInt16})`

```csharp
public IoT.DriverCore.ModbusRx.Data.RegisterCollection(System.Collections.Generic.IList<ushort> registers)
```
Initializes a new instance of `IoT.DriverCore.ModbusRx.Data.RegisterCollection`.

- Parameter `registers`: The `registers` value.

###### `M:IoT.DriverCore.ModbusRx.Data.RegisterCollection.#ctor(System.UInt16[])`

```csharp
public IoT.DriverCore.ModbusRx.Data.RegisterCollection(ushort[] registers)
```
Initializes a new instance of the `T:IoT.DriverCore.ModbusRx.Data.RegisterCollection` class.

- Parameter `registers`: Array for register collection.

###### `M:IoT.DriverCore.ModbusRx.Data.RegisterCollection.ToString`

```csharp
public string ToString()
```
Returns a string that represents the current object.

- Returns: A `T:System.String` that represents the current `T:System.Object` .

###### `P:IoT.DriverCore.ModbusRx.Data.RegisterCollection.ByteCount`

```csharp
public byte ByteCount { get; }
```
Gets the byte count.

- Value: The `ByteCount` value.

###### `P:IoT.DriverCore.ModbusRx.Data.RegisterCollection.NetworkBytes`

```csharp
public byte[] NetworkBytes { get; }
```
Gets the network bytes.

- Value: The `NetworkBytes` value.

#### `T:IoT.DriverCore.ModbusRx.Data.SimulationDataProvider`

```csharp
public class IoT.DriverCore.ModbusRx.Data.SimulationDataProvider
```
Provides simulation data for Modbus testing and development.

##### Declared public members

###### `M:IoT.DriverCore.ModbusRx.Data.SimulationDataProvider.#ctor`

```csharp
public IoT.DriverCore.ModbusRx.Data.SimulationDataProvider()
```
Initializes a new instance of the `T:IoT.DriverCore.ModbusRx.Data.SimulationDataProvider` class.

###### `M:IoT.DriverCore.ModbusRx.Data.SimulationDataProvider.#ctor(System.TimeProvider)`

```csharp
public IoT.DriverCore.ModbusRx.Data.SimulationDataProvider(System.TimeProvider timeProvider)
```
Initializes a new instance of the `T:IoT.DriverCore.ModbusRx.Data.SimulationDataProvider` class.

- Parameter `timeProvider`: The time provider used for simulation timing.

###### `M:IoT.DriverCore.ModbusRx.Data.SimulationDataProvider.Dispose`

```csharp
public void Dispose()
```
Disposes the simulation data provider.

###### `M:IoT.DriverCore.ModbusRx.Data.SimulationDataProvider.GenerateBooleanPattern(System.Int32,IoT.DriverCore.ModbusRx.Data.BooleanPattern)`

```csharp
public bool[] GenerateBooleanPattern(int length, IoT.DriverCore.ModbusRx.Data.BooleanPattern pattern)
```
Generates boolean pattern for discrete values.

- Parameter `length`: The number of data points.
- Parameter `pattern`: The pattern type.
- Returns: An array of boolean values.

###### `M:IoT.DriverCore.ModbusRx.Data.SimulationDataProvider.GenerateRandomData(System.Int32,System.UInt16,System.UInt16)`

```csharp
public ushort[] GenerateRandomData(int length, ushort minValue, ushort maxValue)
```
Generates random data within specified bounds.

- Parameter `length`: The number of data points.
- Parameter `minValue`: The minimum value.
- Parameter `maxValue`: The maximum value.
- Returns: An array of random values.

###### `M:IoT.DriverCore.ModbusRx.Data.SimulationDataProvider.GenerateSawtoothWave(System.Int32,System.UInt16,System.UInt16)`

```csharp
public static ushort[] GenerateSawtoothWave(int length, ushort maxValue, ushort minValue)
```
Generates sawtooth wave pattern data.

- Parameter `length`: The number of data points.
- Parameter `maxValue`: The maximum value.
- Parameter `minValue`: The minimum value.
- Returns: An array of sawtooth wave values.

###### `M:IoT.DriverCore.ModbusRx.Data.SimulationDataProvider.GenerateSineWave(System.Int32,System.Double,System.Double,System.Double)`

```csharp
public static ushort[] GenerateSineWave(int length, double amplitude, double frequency, double phase)
```
Generates sine wave pattern data.

- Parameter `length`: The number of data points.
- Parameter `amplitude`: The amplitude of the sine wave.
- Parameter `frequency`: The frequency of the sine wave.
- Parameter `phase`: The phase offset.
- Returns: An array of sine wave values.

###### `M:IoT.DriverCore.ModbusRx.Data.SimulationDataProvider.GenerateSquareWave(System.Int32,System.UInt16,System.UInt16,System.Double)`

```csharp
public static ushort[] GenerateSquareWave(int length, ushort highValue, ushort lowValue, double dutyCycle)
```
Generates square wave pattern data.

- Parameter `length`: The number of data points.
- Parameter `highValue`: The high value of the square wave.
- Parameter `lowValue`: The low value of the square wave.
- Parameter `dutyCycle`: The duty cycle (0.0 to 1.0).
- Returns: An array of square wave values.

###### `M:IoT.DriverCore.ModbusRx.Data.SimulationDataProvider.LoadTestPattern(IoT.DriverCore.ModbusRx.Data.DataStore,IoT.DriverCore.ModbusRx.Data.TestPattern)`

```csharp
public void LoadTestPattern(IoT.DriverCore.ModbusRx.Data.DataStore dataStore, IoT.DriverCore.ModbusRx.Data.TestPattern pattern)
```
Loads predefined test patterns into a data store.

- Parameter `dataStore`: The data store to populate.
- Parameter `pattern`: The pattern type to load.

###### `M:IoT.DriverCore.ModbusRx.Data.SimulationDataProvider.Start(IoT.DriverCore.ModbusRx.Data.DataStore,System.TimeSpan,IoT.DriverCore.ModbusRx.Data.SimulationType)`

```csharp
public void Start(IoT.DriverCore.ModbusRx.Data.DataStore dataStore, System.TimeSpan interval, IoT.DriverCore.ModbusRx.Data.SimulationType simulationType)
```
Starts the simulation with the specified interval.

- Parameter `dataStore`: The data store to update.
- Parameter `interval`: The update interval.
- Parameter `simulationType`: The type of simulation to run.

###### `M:IoT.DriverCore.ModbusRx.Data.SimulationDataProvider.Stop`

```csharp
public void Stop()
```
Stops the simulation.

###### `P:IoT.DriverCore.ModbusRx.Data.SimulationDataProvider.IsRunning`

```csharp
public System.IObservable<bool> IsRunning { get; }
```
Gets an observable indicating if simulation is running.

- Value: The `IsRunning` value.

#### `T:IoT.DriverCore.ModbusRx.Data.SimulationType`

```csharp
public enum IoT.DriverCore.ModbusRx.Data.SimulationType
```
Types of simulation patterns available.

##### Declared public members

###### `F:IoT.DriverCore.ModbusRx.Data.SimulationType.CountingDown`

```csharp
public static const IoT.DriverCore.ModbusRx.Data.SimulationType CountingDown
```
Counting down pattern.

###### `F:IoT.DriverCore.ModbusRx.Data.SimulationType.CountingUp`

```csharp
public static const IoT.DriverCore.ModbusRx.Data.SimulationType CountingUp
```
Counting up pattern.

###### `F:IoT.DriverCore.ModbusRx.Data.SimulationType.Random`

```csharp
public static const IoT.DriverCore.ModbusRx.Data.SimulationType Random
```
Random values.

###### `F:IoT.DriverCore.ModbusRx.Data.SimulationType.SineWave`

```csharp
public static const IoT.DriverCore.ModbusRx.Data.SimulationType SineWave
```
Sine wave pattern.

###### `F:IoT.DriverCore.ModbusRx.Data.SimulationType.SquareWave`

```csharp
public static const IoT.DriverCore.ModbusRx.Data.SimulationType SquareWave
```
Square wave pattern.

#### `T:IoT.DriverCore.ModbusRx.Data.TestPattern`

```csharp
public enum IoT.DriverCore.ModbusRx.Data.TestPattern
```
Test pattern types.

##### Declared public members

###### `F:IoT.DriverCore.ModbusRx.Data.TestPattern.AllOnes`

```csharp
public static const IoT.DriverCore.ModbusRx.Data.TestPattern AllOnes
```
All ones (max values).

###### `F:IoT.DriverCore.ModbusRx.Data.TestPattern.AllZeros`

```csharp
public static const IoT.DriverCore.ModbusRx.Data.TestPattern AllZeros
```
All zeros.

###### `F:IoT.DriverCore.ModbusRx.Data.TestPattern.CountingDown`

```csharp
public static const IoT.DriverCore.ModbusRx.Data.TestPattern CountingDown
```
Counting down to 0.

###### `F:IoT.DriverCore.ModbusRx.Data.TestPattern.CountingUp`

```csharp
public static const IoT.DriverCore.ModbusRx.Data.TestPattern CountingUp
```
Counting up from 0.

###### `F:IoT.DriverCore.ModbusRx.Data.TestPattern.Random`

```csharp
public static const IoT.DriverCore.ModbusRx.Data.TestPattern Random
```
Random values.

###### `F:IoT.DriverCore.ModbusRx.Data.TestPattern.SineWave`

```csharp
public static const IoT.DriverCore.ModbusRx.Data.TestPattern SineWave
```
Sine wave pattern.

###### `F:IoT.DriverCore.ModbusRx.Data.TestPattern.SquareWave`

```csharp
public static const IoT.DriverCore.ModbusRx.Data.TestPattern SquareWave
```
Square wave pattern.

#### `T:IoT.DriverCore.ModbusRx.Device.ICancelable`

```csharp
public interface IoT.DriverCore.ModbusRx.Device.ICancelable
```
Represents a disposable resource whose disposed state can be inspected.

##### Declared public members

###### `P:IoT.DriverCore.ModbusRx.Device.ICancelable.IsDisposed`

```csharp
public bool IsDisposed { get; }
```
Gets a value indicating whether the resource has been disposed.

- Value: The `IsDisposed` value.

#### `T:IoT.DriverCore.ModbusRx.Device.IModbusMaster`

```csharp
public interface IoT.DriverCore.ModbusRx.Device.IModbusMaster
```
Modbus master device.

##### Declared public members

###### `M:IoT.DriverCore.ModbusRx.Device.IModbusMaster.ReadCoilsAsync(System.Byte,System.UInt16,System.UInt16)`

```csharp
public System.Threading.Tasks.Task<bool[]> ReadCoilsAsync(byte slaveAddress, ushort startAddress, ushort numberOfPoints)
```
Asynchronously reads from 1 to 2000 contiguous coils status.

- Parameter `slaveAddress`: Address of device to read values from.
- Parameter `startAddress`: Address to begin reading.
- Parameter `numberOfPoints`: Number of coils to read.
- Returns: A task that represents the asynchronous read operation.

###### `M:IoT.DriverCore.ModbusRx.Device.IModbusMaster.ReadHoldingRegistersAsync(System.Byte,System.UInt16,System.UInt16)`

```csharp
public System.Threading.Tasks.Task<ushort[]> ReadHoldingRegistersAsync(byte slaveAddress, ushort startAddress, ushort numberOfPoints)
```
Asynchronously reads contiguous block of holding registers.

- Parameter `slaveAddress`: Address of device to read values from.
- Parameter `startAddress`: Address to begin reading.
- Parameter `numberOfPoints`: Number of holding registers to read.
- Returns: A task that represents the asynchronous read operation.

###### `M:IoT.DriverCore.ModbusRx.Device.IModbusMaster.ReadInputRegistersAsync(System.Byte,System.UInt16,System.UInt16)`

```csharp
public System.Threading.Tasks.Task<ushort[]> ReadInputRegistersAsync(byte slaveAddress, ushort startAddress, ushort numberOfPoints)
```
Asynchronously reads contiguous block of input registers.

- Parameter `slaveAddress`: Address of device to read values from.
- Parameter `startAddress`: Address to begin reading.
- Parameter `numberOfPoints`: Number of holding registers to read.
- Returns: A task that represents the asynchronous read operation.

###### `M:IoT.DriverCore.ModbusRx.Device.IModbusMaster.ReadInputsAsync(System.Byte,System.UInt16,System.UInt16)`

```csharp
public System.Threading.Tasks.Task<bool[]> ReadInputsAsync(byte slaveAddress, ushort startAddress, ushort numberOfPoints)
```
Asynchronously reads from 1 to 2000 contiguous discrete input status.

- Parameter `slaveAddress`: Address of device to read values from.
- Parameter `startAddress`: Address to begin reading.
- Parameter `numberOfPoints`: Number of discrete inputs to read.
- Returns: A task that represents the asynchronous read operation.

###### `M:IoT.DriverCore.ModbusRx.Device.IModbusMaster.ReadWriteMultipleRegistersAsync(System.Byte,System.UInt16,System.UInt16,System.UInt16,System.UInt16[])`

```csharp
public System.Threading.Tasks.Task<ushort[]> ReadWriteMultipleRegistersAsync(byte slaveAddress, ushort startReadAddress, ushort numberOfPointsToRead, ushort startWriteAddress, ushort[] writeData)
```
Asynchronously performs a combined write and read holding-register transaction. The write operation is performed before the read.

- Parameter `slaveAddress`: Address of device to read values from.
- Parameter `startReadAddress`: Address to begin reading (Holding registers are addressed starting at 0).
- Parameter `numberOfPointsToRead`: Number of registers to read.
- Parameter `startWriteAddress`: The zero-based holding-register address at which to begin writing.
- Parameter `writeData`: Register values to write.
- Returns: A task that represents the asynchronous operation.

###### `M:IoT.DriverCore.ModbusRx.Device.IModbusMaster.WriteMultipleCoilsAsync(System.Byte,System.UInt16,System.Boolean[])`

```csharp
public System.Threading.Tasks.Task WriteMultipleCoilsAsync(byte slaveAddress, ushort startAddress, bool[] data)
```
Asynchronously writes a sequence of coils.

- Parameter `slaveAddress`: Address of the device to write to.
- Parameter `startAddress`: Address to begin writing values.
- Parameter `data`: Values to write.
- Returns: A task that represents the asynchronous write operation.

###### `M:IoT.DriverCore.ModbusRx.Device.IModbusMaster.WriteMultipleRegistersAsync(System.Byte,System.UInt16,System.UInt16[])`

```csharp
public System.Threading.Tasks.Task WriteMultipleRegistersAsync(byte slaveAddress, ushort startAddress, ushort[] data)
```
Asynchronously writes a block of 1 to 123 contiguous registers.

- Parameter `slaveAddress`: Address of the device to write to.
- Parameter `startAddress`: Address to begin writing values.
- Parameter `data`: Values to write.
- Returns: A task that represents the asynchronous write operation.

###### `M:IoT.DriverCore.ModbusRx.Device.IModbusMaster.WriteSingleCoilAsync(System.Byte,System.UInt16,System.Boolean)`

```csharp
public System.Threading.Tasks.Task WriteSingleCoilAsync(byte slaveAddress, ushort coilAddress, bool value)
```
Asynchronously writes a single coil value.

- Parameter `slaveAddress`: Address of the device to write to.
- Parameter `coilAddress`: Address to write value to.
- Parameter `value`: Value to write.
- Returns: A task that represents the asynchronous write operation.

###### `M:IoT.DriverCore.ModbusRx.Device.IModbusMaster.WriteSingleRegisterAsync(System.Byte,System.UInt16,System.UInt16)`

```csharp
public System.Threading.Tasks.Task WriteSingleRegisterAsync(byte slaveAddress, ushort registerAddress, ushort value)
```
Asynchronously writes a single holding register.

- Parameter `slaveAddress`: Address of the device to write to.
- Parameter `registerAddress`: Address to write.
- Parameter `value`: Value to write.
- Returns: A task that represents the asynchronous write operation.

###### `P:IoT.DriverCore.ModbusRx.Device.IModbusMaster.Transport`

```csharp
public IoT.DriverCore.ModbusRx.IO.ModbusTransport Transport { get; }
```
Gets transport used by this master.

- Value: The `Transport` value.

#### `T:IoT.DriverCore.ModbusRx.Device.IModbusSerialMaster`

```csharp
public interface IoT.DriverCore.ModbusRx.Device.IModbusSerialMaster
```
Modbus Serial Master device.

##### Declared public members

###### `M:IoT.DriverCore.ModbusRx.Device.IModbusSerialMaster.ReturnQueryData(System.Byte,System.UInt16)`

```csharp
public bool ReturnQueryData(byte slaveAddress, ushort data)
```
Performs the serial-line return query diagnostic and verifies the echoed data.

- Parameter `slaveAddress`: Address of device to test.
- Parameter `data`: Data to return.
- Returns: Return true if slave device echoed data.

###### `P:IoT.DriverCore.ModbusRx.Device.IModbusSerialMaster.Transport`

```csharp
public IoT.DriverCore.ModbusRx.IO.ModbusSerialTransport Transport { get; }
```
Gets transport for used by this master.

- Value: The `Transport` value.

#### `T:IoT.DriverCore.ModbusRx.Device.ModbusDevice`

```csharp
public class IoT.DriverCore.ModbusRx.Device.ModbusDevice
```
Modbus device.

##### Declared public members

###### `M:IoT.DriverCore.ModbusRx.Device.ModbusDevice.Dispose`

```csharp
public void Dispose()
```
Releases unmanaged and - optionally - managed resources.

###### `P:IoT.DriverCore.ModbusRx.Device.ModbusDevice.IsDisposed`

```csharp
public bool IsDisposed { get; }
```
Gets a value indicating whether gets a value that indicates whether the object is disposed.

- Value: The `IsDisposed` value.

###### `P:IoT.DriverCore.ModbusRx.Device.ModbusDevice.Transport`

```csharp
public IoT.DriverCore.ModbusRx.IO.ModbusTransport Transport { get; }
```
Gets the Modbus Transport.

- Value: The `Transport` value.

#### `T:IoT.DriverCore.ModbusRx.Device.ModbusIpMaster`

```csharp
public class IoT.DriverCore.ModbusRx.Device.ModbusIpMaster
```
Modbus IP master device.

##### Declared public members

###### `M:IoT.DriverCore.ModbusRx.Device.ModbusIpMaster.CreateIp(IoT.DriverCore.ModbusRx.IO.IStreamResource)`

```csharp
public static IoT.DriverCore.ModbusRx.Device.ModbusIpMaster CreateIp(IoT.DriverCore.ModbusRx.IO.IStreamResource streamResource)
```
Modbus IP master factory method.

- Parameter `streamResource`: The stream resource.
- Returns: streamResource. New instance of Modbus IP master device using provided stream resource.

###### `M:IoT.DriverCore.ModbusRx.Device.ModbusIpMaster.CreateIp(IoT.DriverCore.Serial.SerialPortRx)`

```csharp
public static IoT.DriverCore.ModbusRx.Device.ModbusIpMaster CreateIp(IoT.DriverCore.Serial.SerialPortRx serialPort)
```
Modbus IP master factory method.

- Parameter `serialPort`: The serial port.
- Returns: serialPort. New instance of Modbus IP master device using provided serial port.

###### `M:IoT.DriverCore.ModbusRx.Device.ModbusIpMaster.CreateIp(IoT.DriverCore.Serial.TcpClientRx)`

```csharp
public static IoT.DriverCore.ModbusRx.Device.ModbusIpMaster CreateIp(IoT.DriverCore.Serial.TcpClientRx tcpClient)
```
Modbus IP master factory method.

- Parameter `tcpClient`: The TCP client.
- Returns: tcpClient. New instance of Modbus IP master device using provided TCP client.

###### `M:IoT.DriverCore.ModbusRx.Device.ModbusIpMaster.CreateIp(IoT.DriverCore.Serial.UdpClientRx)`

```csharp
public static IoT.DriverCore.ModbusRx.Device.ModbusIpMaster CreateIp(IoT.DriverCore.Serial.UdpClientRx udpClient)
```
Modbus IP master factory method.

- Parameter `udpClient`: The UDP client.
- Returns: udpClient. New instance of Modbus IP master device using provided UDP client.

###### `M:IoT.DriverCore.ModbusRx.Device.ModbusIpMaster.ReadCoilsAsync(System.UInt16,System.UInt16)`

```csharp
public System.Threading.Tasks.Task<bool[]> ReadCoilsAsync(ushort startAddress, ushort numberOfPoints)
```
Asynchronously reads from 1 to 2000 contiguous coils status.

- Parameter `startAddress`: Address to begin reading.
- Parameter `numberOfPoints`: Number of coils to read.
- Returns: A task that represents the asynchronous read operation.

###### `M:IoT.DriverCore.ModbusRx.Device.ModbusIpMaster.ReadHoldingRegistersAsync(System.UInt16,System.UInt16)`

```csharp
public System.Threading.Tasks.Task<ushort[]> ReadHoldingRegistersAsync(ushort startAddress, ushort numberOfPoints)
```
Asynchronously reads contiguous block of holding registers.

- Parameter `startAddress`: Address to begin reading.
- Parameter `numberOfPoints`: Number of holding registers to read.
- Returns: A task that represents the asynchronous read operation.

###### `M:IoT.DriverCore.ModbusRx.Device.ModbusIpMaster.ReadInputRegistersAsync(System.UInt16,System.UInt16)`

```csharp
public System.Threading.Tasks.Task<ushort[]> ReadInputRegistersAsync(ushort startAddress, ushort numberOfPoints)
```
Asynchronously reads contiguous block of input registers.

- Parameter `startAddress`: Address to begin reading.
- Parameter `numberOfPoints`: Number of holding registers to read.
- Returns: A task that represents the asynchronous read operation.

###### `M:IoT.DriverCore.ModbusRx.Device.ModbusIpMaster.ReadInputsAsync(System.UInt16,System.UInt16)`

```csharp
public System.Threading.Tasks.Task<bool[]> ReadInputsAsync(ushort startAddress, ushort numberOfPoints)
```
Asynchronously reads from 1 to 2000 contiguous discrete input status.

- Parameter `startAddress`: Address to begin reading.
- Parameter `numberOfPoints`: Number of discrete inputs to read.
- Returns: A task that represents the asynchronous read operation.

###### `M:IoT.DriverCore.ModbusRx.Device.ModbusIpMaster.ReadWriteMultipleRegistersAsync(System.UInt16,System.UInt16,System.UInt16,System.UInt16[])`

```csharp
public System.Threading.Tasks.Task<ushort[]> ReadWriteMultipleRegistersAsync(ushort startReadAddress, ushort numberOfPointsToRead, ushort startWriteAddress, ushort[] writeData)
```
Asynchronously performs a combined write and read holding-register transaction. The write operation is performed before the read.

- Parameter `startReadAddress`: Address to begin reading (Holding registers are addressed starting at 0).
- Parameter `numberOfPointsToRead`: Number of registers to read.
- Parameter `startWriteAddress`: The zero-based holding-register address at which to begin writing.
- Parameter `writeData`: Register values to write.
- Returns: A task that represents the asynchronous operation.

###### `M:IoT.DriverCore.ModbusRx.Device.ModbusIpMaster.WriteMultipleCoilsAsync(System.UInt16,System.Boolean[])`

```csharp
public System.Threading.Tasks.Task WriteMultipleCoilsAsync(ushort startAddress, bool[] data)
```
Asynchronously writes a sequence of coils.

- Parameter `startAddress`: Address to begin writing values.
- Parameter `data`: Values to write.
- Returns: A task that represents the asynchronous write operation.

###### `M:IoT.DriverCore.ModbusRx.Device.ModbusIpMaster.WriteMultipleRegistersAsync(System.UInt16,System.UInt16[])`

```csharp
public System.Threading.Tasks.Task WriteMultipleRegistersAsync(ushort startAddress, ushort[] data)
```
Asynchronously writes a block of 1 to 123 contiguous registers.

- Parameter `startAddress`: Address to begin writing values.
- Parameter `data`: Values to write.
- Returns: A task that represents the asynchronous write operation.

###### `M:IoT.DriverCore.ModbusRx.Device.ModbusIpMaster.WriteSingleCoilAsync(System.UInt16,System.Boolean)`

```csharp
public System.Threading.Tasks.Task WriteSingleCoilAsync(ushort coilAddress, bool value)
```
Asynchronously writes a single coil value.

- Parameter `coilAddress`: Address to write value to.
- Parameter `value`: Value to write.
- Returns: A task that represents the asynchronous write operation.

###### `M:IoT.DriverCore.ModbusRx.Device.ModbusIpMaster.WriteSingleRegisterAsync(System.UInt16,System.UInt16)`

```csharp
public System.Threading.Tasks.Task WriteSingleRegisterAsync(ushort registerAddress, ushort value)
```
Asynchronously writes a single holding register.

- Parameter `registerAddress`: Address to write.
- Parameter `value`: Value to write.
- Returns: A task that represents the asynchronous write operation.

#### `T:IoT.DriverCore.ModbusRx.Device.ModbusMaster`

```csharp
public class IoT.DriverCore.ModbusRx.Device.ModbusMaster
```
Modbus master device.

##### Declared public members

###### `M:IoT.DriverCore.ModbusRx.Device.ModbusMaster.ExecuteCustomMessage``1(IoT.DriverCore.ModbusRx.Message.IModbusMessage,System.Func`1{``0})`

```csharp
public TResponse ExecuteCustomMessage<TResponse>(IoT.DriverCore.ModbusRx.Message.IModbusMessage request, System.Func<TResponse> responseFactory)
```
Executes the `ExecuteCustomMessage` operation.

- Parameter `request`: The `request` value.
- Parameter `responseFactory`: The `responseFactory` value.
- Returns: A `TResponse` result.

###### `M:IoT.DriverCore.ModbusRx.Device.ModbusMaster.ReadCoilsAsync(System.Byte,System.UInt16,System.UInt16)`

```csharp
public System.Threading.Tasks.Task<bool[]> ReadCoilsAsync(byte slaveAddress, ushort startAddress, ushort numberOfPoints)
```
Asynchronously reads from 1 to 2000 contiguous coils status.

- Parameter `slaveAddress`: Address of device to read values from.
- Parameter `startAddress`: Address to begin reading.
- Parameter `numberOfPoints`: Number of coils to read.
- Returns: A task that represents the asynchronous read operation.

###### `M:IoT.DriverCore.ModbusRx.Device.ModbusMaster.ReadHoldingRegistersAsync(System.Byte,System.UInt16,System.UInt16)`

```csharp
public System.Threading.Tasks.Task<ushort[]> ReadHoldingRegistersAsync(byte slaveAddress, ushort startAddress, ushort numberOfPoints)
```
Asynchronously reads contiguous block of holding registers.

- Parameter `slaveAddress`: Address of device to read values from.
- Parameter `startAddress`: Address to begin reading.
- Parameter `numberOfPoints`: Number of holding registers to read.
- Returns: A task that represents the asynchronous read operation.

###### `M:IoT.DriverCore.ModbusRx.Device.ModbusMaster.ReadInputRegistersAsync(System.Byte,System.UInt16,System.UInt16)`

```csharp
public System.Threading.Tasks.Task<ushort[]> ReadInputRegistersAsync(byte slaveAddress, ushort startAddress, ushort numberOfPoints)
```
Asynchronously reads contiguous block of input registers.

- Parameter `slaveAddress`: Address of device to read values from.
- Parameter `startAddress`: Address to begin reading.
- Parameter `numberOfPoints`: Number of holding registers to read.
- Returns: A task that represents the asynchronous read operation.

###### `M:IoT.DriverCore.ModbusRx.Device.ModbusMaster.ReadInputsAsync(System.Byte,System.UInt16,System.UInt16)`

```csharp
public System.Threading.Tasks.Task<bool[]> ReadInputsAsync(byte slaveAddress, ushort startAddress, ushort numberOfPoints)
```
Asynchronously reads from 1 to 2000 contiguous discrete input status.

- Parameter `slaveAddress`: Address of device to read values from.
- Parameter `startAddress`: Address to begin reading.
- Parameter `numberOfPoints`: Number of discrete inputs to read.
- Returns: A task that represents the asynchronous read operation.

###### `M:IoT.DriverCore.ModbusRx.Device.ModbusMaster.ReadWriteMultipleRegistersAsync(System.Byte,System.UInt16,System.UInt16,System.UInt16,System.UInt16[])`

```csharp
public System.Threading.Tasks.Task<ushort[]> ReadWriteMultipleRegistersAsync(byte slaveAddress, ushort startReadAddress, ushort numberOfPointsToRead, ushort startWriteAddress, ushort[] writeData)
```
Asynchronously performs a combined write and read holding-register transaction. The write operation is performed before the read.

- Parameter `slaveAddress`: Address of device to read values from.
- Parameter `startReadAddress`: Address to begin reading (Holding registers are addressed starting at 0).
- Parameter `numberOfPointsToRead`: Number of registers to read.
- Parameter `startWriteAddress`: The zero-based holding-register address at which to begin writing.
- Parameter `writeData`: Register values to write.
- Returns: A task that represents the asynchronous operation.

###### `M:IoT.DriverCore.ModbusRx.Device.ModbusMaster.WriteMultipleCoilsAsync(System.Byte,System.UInt16,System.Boolean[])`

```csharp
public System.Threading.Tasks.Task WriteMultipleCoilsAsync(byte slaveAddress, ushort startAddress, bool[] data)
```
Asynchronously writes a sequence of coils.

- Parameter `slaveAddress`: Address of the device to write to.
- Parameter `startAddress`: Address to begin writing values.
- Parameter `data`: Values to write.
- Returns: A task that represents the asynchronous write operation.

###### `M:IoT.DriverCore.ModbusRx.Device.ModbusMaster.WriteMultipleRegistersAsync(System.Byte,System.UInt16,System.UInt16[])`

```csharp
public System.Threading.Tasks.Task WriteMultipleRegistersAsync(byte slaveAddress, ushort startAddress, ushort[] data)
```
Asynchronously writes a block of 1 to 123 contiguous registers.

- Parameter `slaveAddress`: Address of the device to write to.
- Parameter `startAddress`: Address to begin writing values.
- Parameter `data`: Values to write.
- Returns: A task that represents the asynchronous write operation.

###### `M:IoT.DriverCore.ModbusRx.Device.ModbusMaster.WriteSingleCoilAsync(System.Byte,System.UInt16,System.Boolean)`

```csharp
public System.Threading.Tasks.Task WriteSingleCoilAsync(byte slaveAddress, ushort coilAddress, bool value)
```
Asynchronously writes a single coil value.

- Parameter `slaveAddress`: Address of the device to write to.
- Parameter `coilAddress`: Address to write value to.
- Parameter `value`: Value to write.
- Returns: A task that represents the asynchronous write operation.

###### `M:IoT.DriverCore.ModbusRx.Device.ModbusMaster.WriteSingleRegisterAsync(System.Byte,System.UInt16,System.UInt16)`

```csharp
public System.Threading.Tasks.Task WriteSingleRegisterAsync(byte slaveAddress, ushort registerAddress, ushort value)
```
Asynchronously writes a single holding register.

- Parameter `slaveAddress`: Address of the device to write to.
- Parameter `registerAddress`: Address to write.
- Parameter `value`: Value to write.
- Returns: A task that represents the asynchronous write operation.

#### `T:IoT.DriverCore.ModbusRx.Device.ModbusSerialMaster`

```csharp
public class IoT.DriverCore.ModbusRx.Device.ModbusSerialMaster
```
Modbus serial master device.

##### Declared public members

###### `M:IoT.DriverCore.ModbusRx.Device.ModbusSerialMaster.CreateAscii(IoT.DriverCore.ModbusRx.IO.IStreamResource)`

```csharp
public static IoT.DriverCore.ModbusRx.Device.ModbusSerialMaster CreateAscii(IoT.DriverCore.ModbusRx.IO.IStreamResource streamResource)
```
Modbus ASCII master factory method.

- Parameter `streamResource`: The stream resource.
- Returns: A ModbusSerialMaster.

###### `M:IoT.DriverCore.ModbusRx.Device.ModbusSerialMaster.CreateAscii(IoT.DriverCore.Serial.SerialPortRx)`

```csharp
public static IoT.DriverCore.ModbusRx.Device.ModbusSerialMaster CreateAscii(IoT.DriverCore.Serial.SerialPortRx serialPort)
```
Modbus ASCII master factory method.

- Parameter `serialPort`: The serial port.
- Returns: A ModbusSerialMaster.

###### `M:IoT.DriverCore.ModbusRx.Device.ModbusSerialMaster.CreateAscii(IoT.DriverCore.Serial.TcpClientRx)`

```csharp
public static IoT.DriverCore.ModbusRx.Device.ModbusSerialMaster CreateAscii(IoT.DriverCore.Serial.TcpClientRx tcpClient)
```
Modbus ASCII master factory method.

- Parameter `tcpClient`: The TCP client.
- Returns: A ModbusSerialMaster.

###### `M:IoT.DriverCore.ModbusRx.Device.ModbusSerialMaster.CreateAscii(IoT.DriverCore.Serial.UdpClientRx)`

```csharp
public static IoT.DriverCore.ModbusRx.Device.ModbusSerialMaster CreateAscii(IoT.DriverCore.Serial.UdpClientRx udpClient)
```
Modbus ASCII master factory method.

- Parameter `udpClient`: The UDP client.
- Returns: A ModbusSerialMaster.

###### `M:IoT.DriverCore.ModbusRx.Device.ModbusSerialMaster.CreateRtu(IoT.DriverCore.ModbusRx.IO.IStreamResource)`

```csharp
public static IoT.DriverCore.ModbusRx.Device.ModbusSerialMaster CreateRtu(IoT.DriverCore.ModbusRx.IO.IStreamResource streamResource)
```
Modbus RTU master factory method.

- Parameter `streamResource`: The stream resource.
- Returns: A ModbusSerialMaster.

###### `M:IoT.DriverCore.ModbusRx.Device.ModbusSerialMaster.CreateRtu(IoT.DriverCore.Serial.SerialPortRx)`

```csharp
public static IoT.DriverCore.ModbusRx.Device.ModbusSerialMaster CreateRtu(IoT.DriverCore.Serial.SerialPortRx serialPort)
```
Modbus RTU master factory method.

- Parameter `serialPort`: The serial port.
- Returns: A ModbusSerialMaster.

###### `M:IoT.DriverCore.ModbusRx.Device.ModbusSerialMaster.CreateRtu(IoT.DriverCore.Serial.TcpClientRx)`

```csharp
public static IoT.DriverCore.ModbusRx.Device.ModbusSerialMaster CreateRtu(IoT.DriverCore.Serial.TcpClientRx tcpClient)
```
Modbus RTU master factory method.

- Parameter `tcpClient`: The TCP client.
- Returns: A ModbusSerialMaster.

###### `M:IoT.DriverCore.ModbusRx.Device.ModbusSerialMaster.CreateRtu(IoT.DriverCore.Serial.UdpClientRx)`

```csharp
public static IoT.DriverCore.ModbusRx.Device.ModbusSerialMaster CreateRtu(IoT.DriverCore.Serial.UdpClientRx udpClient)
```
Modbus RTU master factory method.

- Parameter `udpClient`: The UDP client.
- Returns: A ModbusSerialMaster.

###### `M:IoT.DriverCore.ModbusRx.Device.ModbusSerialMaster.ReturnQueryData(System.Byte,System.UInt16)`

```csharp
public bool ReturnQueryData(byte slaveAddress, ushort data)
```
Performs the serial-line return query diagnostic and verifies the echoed data.

- Parameter `slaveAddress`: Address of device to test.
- Parameter `data`: Data to return.
- Returns: Return true if slave device echoed data.

#### `T:IoT.DriverCore.ModbusRx.Device.ModbusSerialSlave`

```csharp
public class IoT.DriverCore.ModbusRx.Device.ModbusSerialSlave
```
Modbus serial slave device.

##### Declared public members

###### `M:IoT.DriverCore.ModbusRx.Device.ModbusSerialSlave.CreateAscii(System.Byte,IoT.DriverCore.ModbusRx.IO.IStreamResource)`

```csharp
public static IoT.DriverCore.ModbusRx.Device.ModbusSerialSlave CreateAscii(byte unitId, IoT.DriverCore.ModbusRx.IO.IStreamResource streamResource)
```
Modbus ASCII slave factory method.

- Parameter `unitId`: The unit identifier.
- Parameter `streamResource`: The stream resource.
- Returns: A ModbusSerialSlave.

###### `M:IoT.DriverCore.ModbusRx.Device.ModbusSerialSlave.CreateAscii(System.Byte,IoT.DriverCore.Serial.SerialPortRx)`

```csharp
public static IoT.DriverCore.ModbusRx.Device.ModbusSerialSlave CreateAscii(byte unitId, IoT.DriverCore.Serial.SerialPortRx serialPort)
```
Modbus ASCII slave factory method.

- Parameter `unitId`: The unit identifier.
- Parameter `serialPort`: The serial port.
- Returns: A ModbusSerialSlave.

###### `M:IoT.DriverCore.ModbusRx.Device.ModbusSerialSlave.CreateRtu(System.Byte,IoT.DriverCore.ModbusRx.IO.IStreamResource)`

```csharp
public static IoT.DriverCore.ModbusRx.Device.ModbusSerialSlave CreateRtu(byte unitId, IoT.DriverCore.ModbusRx.IO.IStreamResource streamResource)
```
Modbus RTU slave factory method.

- Parameter `unitId`: The unit identifier.
- Parameter `streamResource`: The stream resource.
- Returns: A ModbusSerialSlave.

###### `M:IoT.DriverCore.ModbusRx.Device.ModbusSerialSlave.CreateRtu(System.Byte,IoT.DriverCore.Serial.SerialPortRx)`

```csharp
public static IoT.DriverCore.ModbusRx.Device.ModbusSerialSlave CreateRtu(byte unitId, IoT.DriverCore.Serial.SerialPortRx serialPort)
```
Modbus RTU slave factory method.

- Parameter `unitId`: The unit identifier.
- Parameter `serialPort`: The serial port.
- Returns: A ModbusSerialSlave.

###### `M:IoT.DriverCore.ModbusRx.Device.ModbusSerialSlave.ListenAsync`

```csharp
public System.Threading.Tasks.Task ListenAsync()
```
Start slave listening for requests.

- Returns: A `T:System.Threading.Tasks.Task` representing the asynchronous operation.

#### `T:IoT.DriverCore.ModbusRx.Device.ModbusServer`

```csharp
public class IoT.DriverCore.ModbusRx.Device.ModbusServer
```
A reactive Modbus server that can serve multiple clients via TCP/UDP. Supports unified client aggregation and simulation modes.

##### Declared public members

###### `M:IoT.DriverCore.ModbusRx.Device.ModbusServer.#ctor`

```csharp
public IoT.DriverCore.ModbusRx.Device.ModbusServer()
```
Initializes a new instance of the `T:IoT.DriverCore.ModbusRx.Device.ModbusServer` class.

###### `M:IoT.DriverCore.ModbusRx.Device.ModbusServer.AddTcpClient(System.String,System.String,System.Int32,System.Byte)`

```csharp
public System.IDisposable AddTcpClient(string name, string hostAddress, int port, byte slaveAddress)
```
Adds a Modbus TCP/IP client to serve data from.

- Parameter `name`: The name identifier for the client.
- Parameter `hostAddress`: The host address of the client.
- Parameter `port`: The port number.
- Parameter `slaveAddress`: The slave address.
- Returns: A disposable subscription.

###### `M:IoT.DriverCore.ModbusRx.Device.ModbusServer.AddUdpClient(System.String,System.String,System.Int32,System.Byte)`

```csharp
public System.IDisposable AddUdpClient(string name, string hostAddress, int port, byte slaveAddress)
```
Adds a Modbus UDP client to serve data from.

- Parameter `name`: The name identifier for the client.
- Parameter `hostAddress`: The host address of the client.
- Parameter `port`: The port number.
- Parameter `slaveAddress`: The slave address.
- Returns: A disposable subscription.

###### `M:IoT.DriverCore.ModbusRx.Device.ModbusServer.Dispose`

```csharp
public void Dispose()
```
Disposes the server and all resources.

###### `M:IoT.DriverCore.ModbusRx.Device.ModbusServer.GetCurrentData`

```csharp
public System.ValueTuple<ushort[], ushort[], bool[], bool[]> GetCurrentData()
```
Gets the current data from the server's data store.

- Returns: A snapshot of the current data.

###### `M:IoT.DriverCore.ModbusRx.Device.ModbusServer.LoadSimulationData(System.UInt16[],System.UInt16[],System.Boolean[],System.Boolean[])`

```csharp
public void LoadSimulationData(ushort[] holdingRegisters, ushort[] inputRegisters, bool[] coils, bool[] inputs)
```
Loads simulation data from specified values for testing.

- Parameter `holdingRegisters`: Holding register values.
- Parameter `inputRegisters`: Input register values.
- Parameter `coils`: Coil values.
- Parameter `inputs`: Input values.

###### `M:IoT.DriverCore.ModbusRx.Device.ModbusServer.Start`

```csharp
public void Start()
```
Starts the server with all configured endpoints.

###### `M:IoT.DriverCore.ModbusRx.Device.ModbusServer.StartTcpServer(System.Int32,System.Byte)`

```csharp
public System.IDisposable StartTcpServer(int port, byte unitId)
```
Starts a TCP server on the specified port.

- Parameter `port`: The port to listen on.
- Parameter `unitId`: The unit ID for the slave.
- Returns: A disposable subscription.

###### `M:IoT.DriverCore.ModbusRx.Device.ModbusServer.StartUdpServer(System.Int32,System.Byte)`

```csharp
public System.IDisposable StartUdpServer(int port, byte unitId)
```
Starts a UDP server on the specified port.

- Parameter `port`: The port to listen on.
- Parameter `unitId`: The unit ID for the slave.
- Returns: A disposable subscription.

###### `M:IoT.DriverCore.ModbusRx.Device.ModbusServer.Stop`

```csharp
public void Stop()
```
Stops the server and all endpoints.

###### `P:IoT.DriverCore.ModbusRx.Device.ModbusServer.DataStore`

```csharp
public IoT.DriverCore.ModbusRx.Data.DataStore DataStore { get; set; }
```
Gets or sets the data store for the server.

- Value: The `DataStore` value.

###### `P:IoT.DriverCore.ModbusRx.Device.ModbusServer.IsRunning`

```csharp
public System.IObservable<bool> IsRunning { get; }
```
Gets an observable that indicates if the server is running.

- Value: The `IsRunning` value.

###### `P:IoT.DriverCore.ModbusRx.Device.ModbusServer.SimulationMode`

```csharp
public bool SimulationMode { get; set; }
```
Gets or sets a value indicating whether simulation mode is enabled.

- Value: The `SimulationMode` value.

#### `T:IoT.DriverCore.ModbusRx.Device.ModbusSimulator`

```csharp
public class IoT.DriverCore.ModbusRx.Device.ModbusSimulator
```
Provides a deterministic, stateful Modbus device for development, testing, and offline operation.

##### Declared public members

###### `E:IoT.DriverCore.ModbusRx.Device.ModbusSimulator.RequestProcessed`

```csharp
public event System.EventHandler<IoT.DriverCore.ModbusRx.Device.ModbusSimulatorRequestEventArgs> RequestProcessed
```
Occurs after a complete request frame has been accepted by the simulator.

###### `M:IoT.DriverCore.ModbusRx.Device.ModbusSimulator.#ctor`

```csharp
public IoT.DriverCore.ModbusRx.Device.ModbusSimulator()
```
Initializes a new instance of the `T:IoT.DriverCore.ModbusRx.Device.ModbusSimulator` class.

###### `M:IoT.DriverCore.ModbusRx.Device.ModbusSimulator.#ctor(System.Byte)`

```csharp
public IoT.DriverCore.ModbusRx.Device.ModbusSimulator(byte unitId)
```
Initializes a new instance of the `T:IoT.DriverCore.ModbusRx.Device.ModbusSimulator` class.

- Parameter `unitId`: The Modbus unit identifier.

###### `M:IoT.DriverCore.ModbusRx.Device.ModbusSimulator.#ctor(System.Byte,IoT.DriverCore.ModbusRx.Data.DataStore)`

```csharp
public IoT.DriverCore.ModbusRx.Device.ModbusSimulator(byte unitId, IoT.DriverCore.ModbusRx.Data.DataStore dataStore)
```
Initializes a new instance of the `T:IoT.DriverCore.ModbusRx.Device.ModbusSimulator` class.

- Parameter `unitId`: The Modbus unit identifier.
- Parameter `dataStore`: The persistent device memory used by the simulator.

###### `M:IoT.DriverCore.ModbusRx.Device.ModbusSimulator.#ctor(System.Byte,IoT.DriverCore.ModbusRx.Data.DataStore,System.TimeProvider)`

```csharp
public IoT.DriverCore.ModbusRx.Device.ModbusSimulator(byte unitId, IoT.DriverCore.ModbusRx.Data.DataStore dataStore, System.TimeProvider timeProvider)
```
Initializes a new instance of the `T:IoT.DriverCore.ModbusRx.Device.ModbusSimulator` class.

- Parameter `unitId`: The Modbus unit identifier.
- Parameter `dataStore`: The persistent device memory used by the simulator.
- Parameter `timeProvider`: The time provider used for request-event timestamps.

###### `M:IoT.DriverCore.ModbusRx.Device.ModbusSimulator.ClearFaults`

```csharp
public void ClearFaults()
```
Removes every scripted fault that has not yet been applied.

###### `M:IoT.DriverCore.ModbusRx.Device.ModbusSimulator.CreateMaster`

```csharp
public IoT.DriverCore.ModbusRx.Device.ModbusIpMaster CreateMaster()
```
Creates a Modbus IP master connected through a complete in-memory MBAP transport.

- Returns: A master that communicates with this simulator.

###### `M:IoT.DriverCore.ModbusRx.Device.ModbusSimulator.Dispose`

```csharp
public void Dispose()
```
Disposes simulator endpoints and owned resources.

###### `M:IoT.DriverCore.ModbusRx.Device.ModbusSimulator.QueueFault(IoT.DriverCore.ModbusRx.Device.ModbusSimulatorFaultKind)`

```csharp
public void QueueFault(IoT.DriverCore.ModbusRx.Device.ModbusSimulatorFaultKind fault)
```
Queues a deterministic fault for the next request.

- Parameter `fault`: The fault to apply.

###### `M:IoT.DriverCore.ModbusRx.Device.ModbusSimulator.StartTcpLoopback`

```csharp
public IoT.DriverCore.ModbusRx.Device.ModbusTcpLoopbackEndpoint StartTcpLoopback()
```
Starts an IPv4 loopback TCP endpoint on an operating-system assigned port.

- Returns: An endpoint that creates masters connected through the real socket stack.

###### `P:IoT.DriverCore.ModbusRx.Device.ModbusSimulator.DataStore`

```csharp
public IoT.DriverCore.ModbusRx.Data.DataStore DataStore { get; }
```
Gets the persistent Modbus device memory.

- Value: The `DataStore` value.

###### `P:IoT.DriverCore.ModbusRx.Device.ModbusSimulator.RequestCount`

```csharp
public long RequestCount { get; }
```
Gets the number of requests accepted by the simulator.

- Value: The `RequestCount` value.

###### `P:IoT.DriverCore.ModbusRx.Device.ModbusSimulator.ResponseDelay`

```csharp
public System.TimeSpan ResponseDelay { get; set; }
```
Gets or sets a delay applied before each in-memory response is made available.

- Value: The `ResponseDelay` value.

###### `P:IoT.DriverCore.ModbusRx.Device.ModbusSimulator.UnitId`

```csharp
public byte UnitId { get; }
```
Gets the Modbus unit identifier served by this simulator.

- Value: The `UnitId` value.

#### `T:IoT.DriverCore.ModbusRx.Device.ModbusSimulatorFaultKind`

```csharp
public enum IoT.DriverCore.ModbusRx.Device.ModbusSimulatorFaultKind
```
Identifies a deterministic fault applied to the next simulator request.

##### Declared public members

###### `F:IoT.DriverCore.ModbusRx.Device.ModbusSimulatorFaultKind.CorruptTransactionId`

```csharp
public static const IoT.DriverCore.ModbusRx.Device.ModbusSimulatorFaultKind CorruptTransactionId
```
The device returns a response with a different transaction identifier.

###### `F:IoT.DriverCore.ModbusRx.Device.ModbusSimulatorFaultKind.IOException`

```csharp
public static const IoT.DriverCore.ModbusRx.Device.ModbusSimulatorFaultKind IOException
```
The request fails while it is written to the in-memory transport.

###### `F:IoT.DriverCore.ModbusRx.Device.ModbusSimulatorFaultKind.None`

```csharp
public static const IoT.DriverCore.ModbusRx.Device.ModbusSimulatorFaultKind None
```
No fault is applied.

###### `F:IoT.DriverCore.ModbusRx.Device.ModbusSimulatorFaultKind.SlaveDeviceBusy`

```csharp
public static const IoT.DriverCore.ModbusRx.Device.ModbusSimulatorFaultKind SlaveDeviceBusy
```
The device returns the Modbus slave-device-busy exception.

###### `F:IoT.DriverCore.ModbusRx.Device.ModbusSimulatorFaultKind.Timeout`

```csharp
public static const IoT.DriverCore.ModbusRx.Device.ModbusSimulatorFaultKind Timeout
```
The response read fails with a timeout.

#### `T:IoT.DriverCore.ModbusRx.Device.ModbusSimulatorRequestEventArgs`

```csharp
public class IoT.DriverCore.ModbusRx.Device.ModbusSimulatorRequestEventArgs
```
Provides details about a request processed by a `T:IoT.DriverCore.ModbusRx.Device.ModbusSimulator` .

##### Declared public members

###### `P:IoT.DriverCore.ModbusRx.Device.ModbusSimulatorRequestEventArgs.Fault`

```csharp
public System.Nullable<IoT.DriverCore.ModbusRx.Device.ModbusSimulatorFaultKind> Fault { get; }
```
Gets the scripted fault applied to the request, if any.

- Value: The `Fault` value.

###### `P:IoT.DriverCore.ModbusRx.Device.ModbusSimulatorRequestEventArgs.Request`

```csharp
public IoT.DriverCore.ModbusRx.Message.IModbusMessage Request { get; }
```
Gets the request received by the simulator.

- Value: The `Request` value.

###### `P:IoT.DriverCore.ModbusRx.Device.ModbusSimulatorRequestEventArgs.Response`

```csharp
public IoT.DriverCore.ModbusRx.Message.IModbusMessage Response { get; }
```
Gets the response produced by the simulator, if any.

- Value: The `Response` value.

###### `P:IoT.DriverCore.ModbusRx.Device.ModbusSimulatorRequestEventArgs.Timestamp`

```csharp
public System.DateTimeOffset Timestamp { get; }
```
Gets the time at which the request was processed.

- Value: The `Timestamp` value.

#### `T:IoT.DriverCore.ModbusRx.Device.ModbusSlave`

```csharp
public class IoT.DriverCore.ModbusRx.Device.ModbusSlave
```
Modbus slave device.

##### Declared public members

###### `E:IoT.DriverCore.ModbusRx.Device.ModbusSlave.ModbusSlaveRequestReceived`

```csharp
public event System.EventHandler<IoT.DriverCore.ModbusRx.Device.ModbusSlaveRequestEventArgs> ModbusSlaveRequestReceived
```
Raised when a Modbus slave receives a request, before processing request function.

###### `E:IoT.DriverCore.ModbusRx.Device.ModbusSlave.WriteComplete`

```csharp
public event System.EventHandler<IoT.DriverCore.ModbusRx.Device.ModbusSlaveRequestEventArgs> WriteComplete
```
Raised after a Modbus slave processes the write portion of a request.

###### `M:IoT.DriverCore.ModbusRx.Device.ModbusSlave.ListenAsync`

```csharp
public System.Threading.Tasks.Task ListenAsync()
```
Start slave listening for requests.

- Returns: A Task.

###### `P:IoT.DriverCore.ModbusRx.Device.ModbusSlave.DataStore`

```csharp
public IoT.DriverCore.ModbusRx.Data.DataStore DataStore { get; set; }
```
Gets or sets the data store.

- Value: The `DataStore` value.

###### `P:IoT.DriverCore.ModbusRx.Device.ModbusSlave.UnitId`

```csharp
public byte UnitId { get; set; }
```
Gets or sets the unit ID.

- Value: The `UnitId` value.

#### `T:IoT.DriverCore.ModbusRx.Device.ModbusSlaveRequestEventArgs`

```csharp
public class IoT.DriverCore.ModbusRx.Device.ModbusSlaveRequestEventArgs
```
Modbus Slave request event args containing information on the message.

##### Declared public members

###### `P:IoT.DriverCore.ModbusRx.Device.ModbusSlaveRequestEventArgs.Message`

```csharp
public IoT.DriverCore.ModbusRx.Message.IModbusMessage Message { get; }
```
Gets the message.

- Value: The `Message` value.

#### `T:IoT.DriverCore.ModbusRx.Device.ModbusTcpLoopbackEndpoint`

```csharp
public class IoT.DriverCore.ModbusRx.Device.ModbusTcpLoopbackEndpoint
```
Represents a running IPv4 Modbus TCP loopback endpoint.

##### Declared public members

###### `M:IoT.DriverCore.ModbusRx.Device.ModbusTcpLoopbackEndpoint.CreateMaster`

```csharp
public IoT.DriverCore.ModbusRx.Device.ModbusIpMaster CreateMaster()
```
Creates a Modbus IP master connected through the operating-system TCP stack.

- Returns: A connected master.

###### `M:IoT.DriverCore.ModbusRx.Device.ModbusTcpLoopbackEndpoint.Dispose`

```csharp
public void Dispose()
```
Stops the listener and disconnects its masters.

###### `P:IoT.DriverCore.ModbusRx.Device.ModbusTcpLoopbackEndpoint.Completion`

```csharp
public System.Threading.Tasks.Task Completion { get; }
```
Gets the listener completion task.

- Value: The `Completion` value.

###### `P:IoT.DriverCore.ModbusRx.Device.ModbusTcpLoopbackEndpoint.EndPoint`

```csharp
public System.Net.IPEndPoint EndPoint { get; }
```
Gets the bound IPv4 loopback endpoint.

- Value: The `EndPoint` value.

###### `P:IoT.DriverCore.ModbusRx.Device.ModbusTcpLoopbackEndpoint.Port`

```csharp
public int Port { get; }
```
Gets the operating-system assigned TCP port.

- Value: The `Port` value.

#### `T:IoT.DriverCore.ModbusRx.Device.ModbusTcpSlave`

```csharp
public class IoT.DriverCore.ModbusRx.Device.ModbusTcpSlave
```
Modbus TCP slave device.

##### Declared public members

###### `M:IoT.DriverCore.ModbusRx.Device.ModbusTcpSlave.CreateTcp(System.Byte,System.Net.Sockets.TcpListener)`

```csharp
public static IoT.DriverCore.ModbusRx.Device.ModbusTcpSlave CreateTcp(byte unitId, System.Net.Sockets.TcpListener tcpListener)
```
Modbus TCP slave factory method.

- Parameter `unitId`: The unit identifier.
- Parameter `tcpListener`: The TCP listener.
- Returns: A ModbusTcpSlave.

###### `M:IoT.DriverCore.ModbusRx.Device.ModbusTcpSlave.ListenAsync`

```csharp
public System.Threading.Tasks.Task ListenAsync()
```
Start slave listening for requests.

- Returns: A `T:System.Threading.Tasks.Task` representing the asynchronous operation.

###### `P:IoT.DriverCore.ModbusRx.Device.ModbusTcpSlave.IsListening`

```csharp
public bool IsListening { get; }
```
Gets a value indicating whether this slave currently owns an active accept loop.

- Value: The `IsListening` value.

###### `P:IoT.DriverCore.ModbusRx.Device.ModbusTcpSlave.Masters`

```csharp
public System.Collections.ObjectModel.ReadOnlyCollection<IoT.DriverCore.Serial.TcpClientRx> Masters { get; }
```
Gets the Modbus TCP Masters connected to this Modbus TCP Slave.

- Value: The `Masters` value.

#### `T:IoT.DriverCore.ModbusRx.Device.ModbusUdpSlave`

```csharp
public class IoT.DriverCore.ModbusRx.Device.ModbusUdpSlave
```
Modbus UDP slave device.

##### Declared public members

###### `M:IoT.DriverCore.ModbusRx.Device.ModbusUdpSlave.CreateUdp(IoT.DriverCore.Serial.UdpClientRx)`

```csharp
public static IoT.DriverCore.ModbusRx.Device.ModbusUdpSlave CreateUdp(IoT.DriverCore.Serial.UdpClientRx client)
```
Modbus UDP slave factory method. Creates NModbus UDP slave with default.

- Parameter `client`: The client.
- Returns: A ModbusUdpSlave.

###### `M:IoT.DriverCore.ModbusRx.Device.ModbusUdpSlave.CreateUdp(System.Byte,IoT.DriverCore.Serial.UdpClientRx)`

```csharp
public static IoT.DriverCore.ModbusRx.Device.ModbusUdpSlave CreateUdp(byte unitId, IoT.DriverCore.Serial.UdpClientRx client)
```
Modbus UDP slave factory method.

- Parameter `unitId`: The unit identifier.
- Parameter `client`: The client.
- Returns: A ModbusUdpSlave.

###### `M:IoT.DriverCore.ModbusRx.Device.ModbusUdpSlave.ListenAsync`

```csharp
public System.Threading.Tasks.Task ListenAsync()
```
Start slave listening for requests.

- Returns: A `T:System.Threading.Tasks.Task` representing the asynchronous operation.

#### `T:IoT.DriverCore.ModbusRx.EnhancedModbusServerExtensions`

```csharp
public class IoT.DriverCore.ModbusRx.EnhancedModbusServerExtensions
```
Enhanced reactive extensions for ModbusServer with performance optimizations.

##### Declared public members

###### `M:IoT.DriverCore.ModbusRx.EnhancedModbusServerExtensions.ObserveCoilsOptimized(IoT.DriverCore.ModbusRx.Device.ModbusServer,System.UInt16,System.UInt16,System.Int32)`

```csharp
public static System.IObservable<bool[]> ObserveCoilsOptimized(IoT.DriverCore.ModbusRx.Device.ModbusServer server, ushort startAddress, ushort count, int interval)
```
Observes coil changes with range filtering.

- Parameter `server`: The extension receiver.
- Parameter `startAddress`: The start address to observe.
- Parameter `count`: The number of coils to observe.
- Parameter `interval`: The observation interval in milliseconds.
- Returns: An observable of coil values.

###### `M:IoT.DriverCore.ModbusRx.EnhancedModbusServerExtensions.ObserveDataChangesBuffered(IoT.DriverCore.ModbusRx.Device.ModbusServer,System.Int32,System.Int32)`

```csharp
public static System.IObservable<IoT.DriverCore.ModbusRx.ModbusServerDataSnapshot[]> ObserveDataChangesBuffered(IoT.DriverCore.ModbusRx.Device.ModbusServer server, int bufferSize, int bufferTimeMilliseconds)
```
Creates a buffered observable with change detection and batching.

- Parameter `server`: The extension receiver.
- Parameter `bufferSize`: The buffer size for batching changes.
- Parameter `bufferTimeMilliseconds`: The buffer time window in milliseconds.
- Returns: An observable of batched data changes.

###### `M:IoT.DriverCore.ModbusRx.EnhancedModbusServerExtensions.ObserveDataChangesBuffered(IoT.DriverCore.ModbusRx.Device.ModbusServer,System.Int32,System.Int32,System.TimeProvider)`

```csharp
public static System.IObservable<IoT.DriverCore.ModbusRx.ModbusServerDataSnapshot[]> ObserveDataChangesBuffered(IoT.DriverCore.ModbusRx.Device.ModbusServer server, int bufferSize, int bufferTimeMilliseconds, System.TimeProvider timeProvider)
```
Creates a buffered observable with change detection and batching.

- Parameter `server`: The extension receiver.
- Parameter `bufferSize`: The buffer size for batching changes.
- Parameter `bufferTimeMilliseconds`: The buffer time window in milliseconds.
- Parameter `timeProvider`: The time provider used for snapshot timestamps.
- Returns: An observable of batched data changes.

###### `M:IoT.DriverCore.ModbusRx.EnhancedModbusServerExtensions.ObserveDataChangesEventDriven(IoT.DriverCore.ModbusRx.Device.ModbusServer)`

```csharp
public static System.IObservable<IoT.DriverCore.ModbusRx.ModbusServerDataSnapshot> ObserveDataChangesEventDriven(IoT.DriverCore.ModbusRx.Device.ModbusServer server)
```
Observes data-store writes without polling or elapsed-time dependencies.

- Parameter `server`: The extension receiver.
- Returns: An observable that emits one snapshot for each completed data-store write.

###### `M:IoT.DriverCore.ModbusRx.EnhancedModbusServerExtensions.ObserveDataChangesEventDriven(IoT.DriverCore.ModbusRx.Device.ModbusServer,System.TimeProvider,IoT.DriverCore.ModbusRx.ModbusObservationMetrics)`

```csharp
public static System.IObservable<IoT.DriverCore.ModbusRx.ModbusServerDataSnapshot> ObserveDataChangesEventDriven(IoT.DriverCore.ModbusRx.Device.ModbusServer server, System.TimeProvider timeProvider, IoT.DriverCore.ModbusRx.ModbusObservationMetrics metrics)
```
Observes data-store writes without polling or elapsed-time dependencies.

- Parameter `server`: The extension receiver.
- Parameter `timeProvider`: The time provider used for snapshot timestamps.
- Parameter `metrics`: Optional deterministic observation counters.
- Returns: An observable that emits one snapshot for each completed data-store write.

###### `M:IoT.DriverCore.ModbusRx.EnhancedModbusServerExtensions.ObserveDataChangesOptimized(IoT.DriverCore.ModbusRx.Device.ModbusServer,System.Int32)`

```csharp
public static System.IObservable<IoT.DriverCore.ModbusRx.ModbusServerDataSnapshot> ObserveDataChangesOptimized(IoT.DriverCore.ModbusRx.Device.ModbusServer server, int interval)
```
Observes data changes in the server with high-performance optimizations.

- Parameter `server`: The extension receiver.
- Parameter `interval`: The observation interval in milliseconds.
- Returns: An observable of data changes.

###### `M:IoT.DriverCore.ModbusRx.EnhancedModbusServerExtensions.ObserveDataChangesOptimized(IoT.DriverCore.ModbusRx.Device.ModbusServer,System.Int32,System.TimeProvider)`

```csharp
public static System.IObservable<IoT.DriverCore.ModbusRx.ModbusServerDataSnapshot> ObserveDataChangesOptimized(IoT.DriverCore.ModbusRx.Device.ModbusServer server, int interval, System.TimeProvider timeProvider)
```
Observes data changes in the server with high-performance optimizations.

- Parameter `server`: The extension receiver.
- Parameter `interval`: The observation interval in milliseconds.
- Parameter `timeProvider`: The time provider used for snapshot timestamps.
- Returns: An observable of data changes.

###### `M:IoT.DriverCore.ModbusRx.EnhancedModbusServerExtensions.ObserveHoldingRegistersOptimized(IoT.DriverCore.ModbusRx.Device.ModbusServer,System.UInt16,System.UInt16,System.Int32)`

```csharp
public static System.IObservable<ushort[]> ObserveHoldingRegistersOptimized(IoT.DriverCore.ModbusRx.Device.ModbusServer server, ushort startAddress, ushort count, int interval)
```
Observes holding register changes with range filtering.

- Parameter `server`: The extension receiver.
- Parameter `startAddress`: The start address to observe.
- Parameter `count`: The number of registers to observe.
- Parameter `interval`: The observation interval in milliseconds.
- Returns: An observable of register values.

#### `T:IoT.DriverCore.ModbusRx.Extensions.Enron.EnronModbusExtensions`

```csharp
public class IoT.DriverCore.ModbusRx.Extensions.Enron.EnronModbusExtensions
```
Utility extensions for the Enron Modbus dialect.

##### Declared public members

###### `M:IoT.DriverCore.ModbusRx.Extensions.Enron.EnronModbusExtensions.ReadHoldingRegisters32Async(IoT.DriverCore.ModbusRx.Device.ModbusMaster,System.Byte,System.UInt16,System.UInt16)`

```csharp
public static System.Threading.Tasks.Task<uint[]> ReadHoldingRegisters32Async(IoT.DriverCore.ModbusRx.Device.ModbusMaster master, byte slaveAddress, ushort startAddress, ushort numberOfPoints)
```
Read contiguous block of 32 bit holding registers.

- Parameter `master`: The extension receiver.
- Parameter `slaveAddress`: Address of device to read values from.
- Parameter `startAddress`: Address to begin reading.
- Parameter `numberOfPoints`: Number of holding registers to read.
- Returns: Holding registers status.

###### `M:IoT.DriverCore.ModbusRx.Extensions.Enron.EnronModbusExtensions.ReadInputRegisters32Async(IoT.DriverCore.ModbusRx.Device.ModbusMaster,System.Byte,System.UInt16,System.UInt16)`

```csharp
public static System.Threading.Tasks.Task<uint[]> ReadInputRegisters32Async(IoT.DriverCore.ModbusRx.Device.ModbusMaster master, byte slaveAddress, ushort startAddress, ushort numberOfPoints)
```
Read contiguous block of 32 bit input registers.

- Parameter `master`: The extension receiver.
- Parameter `slaveAddress`: Address of device to read values from.
- Parameter `startAddress`: Address to begin reading.
- Parameter `numberOfPoints`: Number of holding registers to read.
- Returns: Input registers status.

###### `M:IoT.DriverCore.ModbusRx.Extensions.Enron.EnronModbusExtensions.WriteMultipleRegisters32Async(IoT.DriverCore.ModbusRx.Device.ModbusMaster,System.Byte,System.UInt16,System.UInt32[])`

```csharp
public static System.Threading.Tasks.Task WriteMultipleRegisters32Async(IoT.DriverCore.ModbusRx.Device.ModbusMaster master, byte slaveAddress, ushort startAddress, uint[] data)
```
Write a block of contiguous 32 bit holding registers.

- Parameter `master`: The extension receiver.
- Parameter `slaveAddress`: Address of the device to write to.
- Parameter `startAddress`: Address to begin writing values.
- Parameter `data`: Values to write.
- Returns: A `T:System.Threading.Tasks.Task` representing the asynchronous operation.

###### `M:IoT.DriverCore.ModbusRx.Extensions.Enron.EnronModbusExtensions.WriteSingleRegister32Async(IoT.DriverCore.ModbusRx.Device.ModbusMaster,System.Byte,System.UInt16,System.UInt32)`

```csharp
public static System.Threading.Tasks.Task WriteSingleRegister32Async(IoT.DriverCore.ModbusRx.Device.ModbusMaster master, byte slaveAddress, ushort registerAddress, uint value)
```
Write a single 16 bit holding register.

- Parameter `master`: The extension receiver.
- Parameter `slaveAddress`: Address of the device to write to.
- Parameter `registerAddress`: Address to write.
- Parameter `value`: Value to write.
- Returns: A task representing the asynchronous operation.

#### `T:IoT.DriverCore.ModbusRx.IO.EmptyTransport`

```csharp
public class IoT.DriverCore.ModbusRx.IO.EmptyTransport
```
Provides EmptyTransport functionality.

##### Declared public members

###### `M:IoT.DriverCore.ModbusRx.IO.EmptyTransport.#ctor`

```csharp
public IoT.DriverCore.ModbusRx.IO.EmptyTransport()
```
Initializes a new instance of `IoT.DriverCore.ModbusRx.IO.EmptyTransport`.

#### `T:IoT.DriverCore.ModbusRx.IO.IStreamResource`

```csharp
public interface IoT.DriverCore.ModbusRx.IO.IStreamResource
```
Represents a serial resource. Implementor - http://en.wikipedia.org/wiki/Bridge_Pattern.

##### Declared public members

###### `M:IoT.DriverCore.ModbusRx.IO.IStreamResource.DiscardInBuffer`

```csharp
public void DiscardInBuffer()
```
Purges the receive buffer.

###### `M:IoT.DriverCore.ModbusRx.IO.IStreamResource.ReadAsync(System.Byte[],System.Int32,System.Int32)`

```csharp
public System.Threading.Tasks.Task<int> ReadAsync(byte[] buffer, int offset, int count)
```
Reads bytes into a byte array at the specified offset.

- Parameter `buffer`: The byte array to write the input to.
- Parameter `offset`: The offset in the buffer array to begin writing.
- Parameter `count`: The number of bytes to read.
- Returns: The number of bytes read.

###### `M:IoT.DriverCore.ModbusRx.IO.IStreamResource.Write(System.Byte[],System.Int32,System.Int32)`

```csharp
public void Write(byte[] buffer, int offset, int count)
```
Writes bytes from an output buffer, starting at the specified offset.

- Parameter `buffer`: The byte array that contains the data to write to the port.
- Parameter `offset`: The offset in the buffer array to begin writing.
- Parameter `count`: The number of bytes to write.

###### `P:IoT.DriverCore.ModbusRx.IO.IStreamResource.InfiniteTimeout`

```csharp
public int InfiniteTimeout { get; }
```
Gets indicates that no timeout should occur.

- Value: The `InfiniteTimeout` value.

###### `P:IoT.DriverCore.ModbusRx.IO.IStreamResource.ReadTimeout`

```csharp
public int ReadTimeout { get; set; }
```
Gets or sets the read-operation timeout in milliseconds.

- Value: The `ReadTimeout` value.

###### `P:IoT.DriverCore.ModbusRx.IO.IStreamResource.WriteTimeout`

```csharp
public int WriteTimeout { get; set; }
```
Gets or sets the write-operation timeout in milliseconds.

- Value: The `WriteTimeout` value.

#### `T:IoT.DriverCore.ModbusRx.IO.ModbusBufferManager`

```csharp
public class IoT.DriverCore.ModbusRx.IO.ModbusBufferManager
```
High-performance buffer manager for Modbus message processing with cross-platform compatibility.

##### Declared public members

###### `M:IoT.DriverCore.ModbusRx.IO.ModbusBufferManager.#ctor`

```csharp
public IoT.DriverCore.ModbusRx.IO.ModbusBufferManager()
```
Initializes a new instance of the `T:IoT.DriverCore.ModbusRx.IO.ModbusBufferManager` class.

###### `M:IoT.DriverCore.ModbusRx.IO.ModbusBufferManager.ClearArray``1(``0[])`

```csharp
public static void ClearArray<T>(T[] array)
```
Clears an array with high performance.

- Parameter `array`: The array to clear.

###### `M:IoT.DriverCore.ModbusRx.IO.ModbusBufferManager.CompareArrays``1(``0[],``0[])`

```csharp
public static bool CompareArrays<T>(T[] array1, T[] array2)
```
Performs a high-performance comparison between two arrays.

- Parameter `array1`: The first array.
- Parameter `array2`: The second array.
- Returns: True if the arrays are equal in content.

###### `M:IoT.DriverCore.ModbusRx.IO.ModbusBufferManager.CopyDataAndTrack``1(``0[],System.Int32,``0[],System.Int32,System.Int32)`

```csharp
public int CopyDataAndTrack<T>(T[] source, int sourceIndex, T[] destination, int destinationIndex, int length)
```
Copies data and records deterministic operation and element-copy counts.

- Parameter `source`: The source array.
- Parameter `sourceIndex`: The source index.
- Parameter `destination`: The destination array.
- Parameter `destinationIndex`: The destination index.
- Parameter `length`: The requested element count.
- Returns: The number of copied elements.

###### `M:IoT.DriverCore.ModbusRx.IO.ModbusBufferManager.CopyData``1(``0[],System.Int32,``0[],System.Int32,System.Int32)`

```csharp
public static int CopyData<T>(T[] source, int sourceIndex, T[] destination, int destinationIndex, int length)
```
Copies data efficiently between arrays.

- Parameter `source`: The source array.
- Parameter `sourceIndex`: The source index.
- Parameter `destination`: The destination array.
- Parameter `destinationIndex`: The destination index.
- Parameter `length`: The length to copy.
- Returns: The number of elements copied.

###### `M:IoT.DriverCore.ModbusRx.IO.ModbusBufferManager.Dispose`

```csharp
public void Dispose()
```
Disposes the buffer manager and releases all resources.

###### `M:IoT.DriverCore.ModbusRx.IO.ModbusBufferManager.GetMetrics`

```csharp
public IoT.DriverCore.ModbusRx.IO.ModbusBufferMetrics GetMetrics()
```
Gets a deterministic snapshot of buffer-manager work.

- Returns: The current operation counters.

###### `M:IoT.DriverCore.ModbusRx.IO.ModbusBufferManager.RentBoolBuffer(System.Int32)`

```csharp
public bool[] RentBoolBuffer(int minimumLength)
```
Rents a bool buffer from the pool or creates a new one.

- Parameter `minimumLength`: The minimum length required.
- Returns: A rented buffer that should be returned when finished.

###### `M:IoT.DriverCore.ModbusRx.IO.ModbusBufferManager.RentByteBuffer(System.Int32)`

```csharp
public byte[] RentByteBuffer(int minimumLength)
```
Rents a byte buffer from the pool or creates a new one.

- Parameter `minimumLength`: The minimum length required.
- Returns: A rented buffer that should be returned when finished.

###### `M:IoT.DriverCore.ModbusRx.IO.ModbusBufferManager.RentUshortBuffer(System.Int32)`

```csharp
public ushort[] RentUshortBuffer(int minimumLength)
```
Rents a ushort buffer from the pool or creates a new one.

- Parameter `minimumLength`: The minimum length required.
- Returns: A rented buffer that should be returned when finished.

###### `M:IoT.DriverCore.ModbusRx.IO.ModbusBufferManager.ReturnBoolBuffer(System.Boolean[],System.Boolean)`

```csharp
public void ReturnBoolBuffer(bool[] buffer, bool clearArray)
```
Returns a bool buffer to the pool.

- Parameter `buffer`: The buffer to return.
- Parameter `clearArray`: Whether to clear the array.

###### `M:IoT.DriverCore.ModbusRx.IO.ModbusBufferManager.ReturnByteBuffer(System.Byte[],System.Boolean)`

```csharp
public void ReturnByteBuffer(byte[] buffer, bool clearArray)
```
Returns a byte buffer to the pool.

- Parameter `buffer`: The buffer to return.
- Parameter `clearArray`: Whether to clear the array.

###### `M:IoT.DriverCore.ModbusRx.IO.ModbusBufferManager.ReturnUshortBuffer(System.UInt16[],System.Boolean)`

```csharp
public void ReturnUshortBuffer(ushort[] buffer, bool clearArray)
```
Returns a ushort buffer to the pool.

- Parameter `buffer`: The buffer to return.
- Parameter `clearArray`: Whether to clear the array.

#### `T:IoT.DriverCore.ModbusRx.IO.ModbusBufferMetrics`

```csharp
public class IoT.DriverCore.ModbusRx.IO.ModbusBufferMetrics
```
Provides deterministic operation counters for a `T:IoT.DriverCore.ModbusRx.IO.ModbusBufferManager` .

##### Declared public members

###### `M:IoT.DriverCore.ModbusRx.IO.ModbusBufferMetrics.#ctor(System.Int64,System.Int64,System.Int64,System.Int64,System.Int64)`

```csharp
public IoT.DriverCore.ModbusRx.IO.ModbusBufferMetrics(long rentOperations, long returnOperations, long dedicatedAllocations, long copyOperations, long copiedElements)
```
Initializes a new instance of the `T:IoT.DriverCore.ModbusRx.IO.ModbusBufferMetrics` class.

- Parameter `rentOperations`: The number of successful rents.
- Parameter `returnOperations`: The number of successful returns.
- Parameter `dedicatedAllocations`: The number of arrays allocated instead of rented.
- Parameter `copyOperations`: The number of tracked copy operations.
- Parameter `copiedElements`: The number of elements copied by tracked operations.

###### `P:IoT.DriverCore.ModbusRx.IO.ModbusBufferMetrics.CopiedElements`

```csharp
public long CopiedElements { get; }
```
Gets the number of elements copied by tracked copy operations.

- Value: The `CopiedElements` value.

###### `P:IoT.DriverCore.ModbusRx.IO.ModbusBufferMetrics.CopyOperations`

```csharp
public long CopyOperations { get; }
```
Gets the number of tracked copy operations.

- Value: The `CopyOperations` value.

###### `P:IoT.DriverCore.ModbusRx.IO.ModbusBufferMetrics.DedicatedAllocations`

```csharp
public long DedicatedAllocations { get; }
```
Gets the number of arrays allocated instead of rented.

- Value: The `DedicatedAllocations` value.

###### `P:IoT.DriverCore.ModbusRx.IO.ModbusBufferMetrics.RentOperations`

```csharp
public long RentOperations { get; }
```
Gets the number of successful rents.

- Value: The `RentOperations` value.

###### `P:IoT.DriverCore.ModbusRx.IO.ModbusBufferMetrics.ReturnOperations`

```csharp
public long ReturnOperations { get; }
```
Gets the number of successful returns.

- Value: The `ReturnOperations` value.

#### `T:IoT.DriverCore.ModbusRx.IO.ModbusSerialTransport`

```csharp
public class IoT.DriverCore.ModbusRx.IO.ModbusSerialTransport
```
Transport for serial protocols.

##### Declared public members

###### `P:IoT.DriverCore.ModbusRx.IO.ModbusSerialTransport.CheckFrame`

```csharp
public bool CheckFrame { get; set; }
```
Gets or sets a value indicating whether LRC/CRC frame checking is performed on messages.

- Value: The `CheckFrame` value.

#### `T:IoT.DriverCore.ModbusRx.IO.ModbusTransport`

```csharp
public class IoT.DriverCore.ModbusRx.IO.ModbusTransport
```
Modbus transport. Abstraction - http://en.wikipedia.org/wiki/Bridge_Pattern.

##### Declared public members

###### `M:IoT.DriverCore.ModbusRx.IO.ModbusTransport.Dispose`

```csharp
public void Dispose()
```
Frees, releases, or resets unmanaged resources.

###### `P:IoT.DriverCore.ModbusRx.IO.ModbusTransport.ReadTimeout`

```csharp
public int ReadTimeout { get; set; }
```
Gets or sets the read-operation timeout in milliseconds.

- Value: The `ReadTimeout` value.

###### `P:IoT.DriverCore.ModbusRx.IO.ModbusTransport.Retries`

```csharp
public int Retries { get; set; }
```
Gets or sets number of times to retry sending message after encountering a failure such as an IOException, TimeoutException, or a corrupt message.

- Value: The `Retries` value.

###### `P:IoT.DriverCore.ModbusRx.IO.ModbusTransport.RetryOnOldResponseThreshold`

```csharp
public uint RetryOnOldResponseThreshold { get; set; }
```
Gets or sets whether a second reply is read when the first is behind the sequence number. request by less than this number. For example, set this to 3, and if when sending request 5, response 3 is read, we will attempt to re-read responses.

- Value: The `RetryOnOldResponseThreshold` value.

###### `P:IoT.DriverCore.ModbusRx.IO.ModbusTransport.SlaveBusyUsesRetryCount`

```csharp
public bool SlaveBusyUsesRetryCount { get; set; }
```
Gets or sets whether a slave-busy exception consumes the retry count.

- Value: The `SlaveBusyUsesRetryCount` value.

###### `P:IoT.DriverCore.ModbusRx.IO.ModbusTransport.WaitToRetryMilliseconds`

```csharp
public int WaitToRetryMilliseconds { get; set; }
```
Gets or sets the number of milliseconds the tranport will wait before retrying a message after receiving an ACKNOWLEGE or SLAVE DEVICE BUSY slave exception response.

- Value: The `WaitToRetryMilliseconds` value.

###### `P:IoT.DriverCore.ModbusRx.IO.ModbusTransport.WriteTimeout`

```csharp
public int WriteTimeout { get; set; }
```
Gets or sets the write-operation timeout in milliseconds.

- Value: The `WriteTimeout` value.

#### `T:IoT.DriverCore.ModbusRx.IO.OptimizedModbusMessageFactory`

```csharp
public class IoT.DriverCore.ModbusRx.IO.OptimizedModbusMessageFactory
```
High-performance Modbus message factory with cross-platform optimizations.

##### Declared public members

###### `M:IoT.DriverCore.ModbusRx.IO.OptimizedModbusMessageFactory.CreateReadCoilsRequest(System.Byte,System.UInt16,System.UInt16)`

```csharp
public static byte[] CreateReadCoilsRequest(byte slaveAddress, ushort startAddress, ushort numberOfPoints)
```
Creates a read coils request with high performance.

- Parameter `slaveAddress`: The slave address.
- Parameter `startAddress`: The start address.
- Parameter `numberOfPoints`: The number of points.
- Returns: The serialized message bytes.

###### `M:IoT.DriverCore.ModbusRx.IO.OptimizedModbusMessageFactory.CreateReadHoldingRegistersRequest(System.Byte,System.UInt16,System.UInt16)`

```csharp
public static byte[] CreateReadHoldingRegistersRequest(byte slaveAddress, ushort startAddress, ushort numberOfPoints)
```
Creates a read holding registers request with high performance.

- Parameter `slaveAddress`: The slave address.
- Parameter `startAddress`: The start address.
- Parameter `numberOfPoints`: The number of points.
- Returns: The serialized message bytes.

###### `M:IoT.DriverCore.ModbusRx.IO.OptimizedModbusMessageFactory.CreateWriteMultipleCoilsRequest(System.Byte,System.UInt16,System.Boolean[])`

```csharp
public static byte[] CreateWriteMultipleCoilsRequest(byte slaveAddress, ushort startAddress, bool[] values)
```
Creates a write multiple coils request with high performance.

- Parameter `slaveAddress`: The slave address.
- Parameter `startAddress`: The start address.
- Parameter `values`: The values to write.
- Returns: The serialized message bytes.

###### `M:IoT.DriverCore.ModbusRx.IO.OptimizedModbusMessageFactory.CreateWriteMultipleRegistersRequest(System.Byte,System.UInt16,System.UInt16[])`

```csharp
public static byte[] CreateWriteMultipleRegistersRequest(byte slaveAddress, ushort startAddress, ushort[] values)
```
Creates a write multiple registers request with high performance.

- Parameter `slaveAddress`: The slave address.
- Parameter `startAddress`: The start address.
- Parameter `values`: The values to write.
- Returns: The serialized message bytes.

###### `M:IoT.DriverCore.ModbusRx.IO.OptimizedModbusMessageFactory.CreateWriteSingleCoilRequest(System.Byte,System.UInt16,System.Boolean)`

```csharp
public static byte[] CreateWriteSingleCoilRequest(byte slaveAddress, ushort coilAddress, bool value)
```
Creates a write single coil request with high performance.

- Parameter `slaveAddress`: The slave address.
- Parameter `coilAddress`: The coil address.
- Parameter `value`: The value to write.
- Returns: The serialized message bytes.

###### `M:IoT.DriverCore.ModbusRx.IO.OptimizedModbusMessageFactory.CreateWriteSingleRegisterRequest(System.Byte,System.UInt16,System.UInt16)`

```csharp
public static byte[] CreateWriteSingleRegisterRequest(byte slaveAddress, ushort registerAddress, ushort value)
```
Creates a write single register request with high performance.

- Parameter `slaveAddress`: The slave address.
- Parameter `registerAddress`: The register address.
- Parameter `value`: The value to write.
- Returns: The serialized message bytes.

###### `M:IoT.DriverCore.ModbusRx.IO.OptimizedModbusMessageFactory.DisposeSharedResources`

```csharp
public static void DisposeSharedResources()
```
Releases the shared buffer manager reference and replaces it for subsequent factory operations.

###### `M:IoT.DriverCore.ModbusRx.IO.OptimizedModbusMessageFactory.ParseReadCoilsResponse(System.Byte[],System.Int32)`

```csharp
public static bool[] ParseReadCoilsResponse(byte[] responseData, int numberOfCoils)
```
Parses a read coils response with high performance.

- Parameter `responseData`: The response data.
- Parameter `numberOfCoils`: The number of coils requested.
- Returns: The parsed coil values.

###### `M:IoT.DriverCore.ModbusRx.IO.OptimizedModbusMessageFactory.ParseReadHoldingRegistersResponse(System.Byte[])`

```csharp
public static ushort[] ParseReadHoldingRegistersResponse(byte[] responseData)
```
Parses a read holding registers response with high performance.

- Parameter `responseData`: The response data.
- Returns: The parsed register values.

###### `M:IoT.DriverCore.ModbusRx.IO.OptimizedModbusMessageFactory.ValidateMessageCrc(System.Byte[])`

```csharp
public static bool ValidateMessageCrc(byte[] messageData)
```
Validates a Modbus message CRC with high performance.

- Parameter `messageData`: The complete message data including CRC.
- Returns: True if CRC is valid.

#### `T:IoT.DriverCore.ModbusRx.IO.SerialPortAdapter`

```csharp
public class IoT.DriverCore.ModbusRx.IO.SerialPortAdapter
```
Concrete Implementor - http://en.wikipedia.org/wiki/Bridge_Pattern.

##### Declared public members

###### `M:IoT.DriverCore.ModbusRx.IO.SerialPortAdapter.#ctor(IoT.DriverCore.Serial.SerialPortRx)`

```csharp
public IoT.DriverCore.ModbusRx.IO.SerialPortAdapter(IoT.DriverCore.Serial.SerialPortRx serialPort)
```
Initializes a new instance of the `T:IoT.DriverCore.ModbusRx.IO.SerialPortAdapter` class.

- Parameter `serialPort`: The serial port.

###### `M:IoT.DriverCore.ModbusRx.IO.SerialPortAdapter.DiscardInBuffer`

```csharp
public void DiscardInBuffer()
```
Purges the receive buffer.

###### `M:IoT.DriverCore.ModbusRx.IO.SerialPortAdapter.Dispose`

```csharp
public void Dispose()
```
Frees, releases, or resets unmanaged resources.

###### `M:IoT.DriverCore.ModbusRx.IO.SerialPortAdapter.ReadAsync(System.Byte[],System.Int32,System.Int32)`

```csharp
public System.Threading.Tasks.Task<int> ReadAsync(byte[] buffer, int offset, int count)
```
Reads bytes into a byte array at the specified offset.

- Parameter `buffer`: The byte array to write the input to.
- Parameter `offset`: The offset in the buffer array to begin writing.
- Parameter `count`: The number of bytes to read.
- Returns: The number of bytes read.

###### `M:IoT.DriverCore.ModbusRx.IO.SerialPortAdapter.Write(System.Byte[],System.Int32,System.Int32)`

```csharp
public void Write(byte[] buffer, int offset, int count)
```
Writes bytes from an output buffer, starting at the specified offset.

- Parameter `buffer`: The byte array that contains the data to write to the port.
- Parameter `offset`: The offset in the buffer array to begin writing.
- Parameter `count`: The number of bytes to write.

###### `P:IoT.DriverCore.ModbusRx.IO.SerialPortAdapter.InfiniteTimeout`

```csharp
public int InfiniteTimeout { get; }
```
Gets indicates that no timeout should occur.

- Value: The `InfiniteTimeout` value.

###### `P:IoT.DriverCore.ModbusRx.IO.SerialPortAdapter.ReadTimeout`

```csharp
public int ReadTimeout { get; set; }
```
Gets or sets the read-operation timeout in milliseconds.

- Value: The `ReadTimeout` value.

###### `P:IoT.DriverCore.ModbusRx.IO.SerialPortAdapter.WriteTimeout`

```csharp
public int WriteTimeout { get; set; }
```
Gets or sets the write-operation timeout in milliseconds.

- Value: The `WriteTimeout` value.

#### `T:IoT.DriverCore.ModbusRx.InvalidModbusRequestException`

```csharp
public class IoT.DriverCore.ModbusRx.InvalidModbusRequestException
```
An exception that provides the exception code sent in response to an invalid Modbus request.

##### Declared public members

###### `M:IoT.DriverCore.ModbusRx.InvalidModbusRequestException.#ctor`

```csharp
public IoT.DriverCore.ModbusRx.InvalidModbusRequestException()
```
Initializes a new instance of the `T:IoT.DriverCore.ModbusRx.InvalidModbusRequestException` class.

###### `M:IoT.DriverCore.ModbusRx.InvalidModbusRequestException.#ctor(System.Byte)`

```csharp
public IoT.DriverCore.ModbusRx.InvalidModbusRequestException(byte exceptionCode)
```
Initializes a new instance of the `T:IoT.DriverCore.ModbusRx.InvalidModbusRequestException` class.

- Parameter `exceptionCode`: The Modbus exception code to provide to the slave.

###### `M:IoT.DriverCore.ModbusRx.InvalidModbusRequestException.#ctor(System.Byte,System.Exception)`

```csharp
public IoT.DriverCore.ModbusRx.InvalidModbusRequestException(byte exceptionCode, System.Exception innerException)
```
Initializes a new instance of the `T:IoT.DriverCore.ModbusRx.InvalidModbusRequestException` class.

- Parameter `exceptionCode`: The Modbus exception code to provide to the slave.
- Parameter `innerException`: The exception that caused the current exception. If `innerException` is not null, a null reference, the current exception is raised in a catch block that handles the inner exception.

###### `M:IoT.DriverCore.ModbusRx.InvalidModbusRequestException.#ctor(System.String)`

```csharp
public IoT.DriverCore.ModbusRx.InvalidModbusRequestException(string message)
```
Initializes a new instance of the `T:IoT.DriverCore.ModbusRx.InvalidModbusRequestException` class.

- Parameter `message`: The error message that explains the reason for the exception.

###### `M:IoT.DriverCore.ModbusRx.InvalidModbusRequestException.#ctor(System.String,System.Byte)`

```csharp
public IoT.DriverCore.ModbusRx.InvalidModbusRequestException(string message, byte exceptionCode)
```
Initializes a new instance of the `T:IoT.DriverCore.ModbusRx.InvalidModbusRequestException` class.

- Parameter `message`: The error message that explains the reason for the exception.
- Parameter `exceptionCode`: The Modbus exception code to provide to the slave.

###### `M:IoT.DriverCore.ModbusRx.InvalidModbusRequestException.#ctor(System.String,System.Byte,System.Exception)`

```csharp
public IoT.DriverCore.ModbusRx.InvalidModbusRequestException(string message, byte exceptionCode, System.Exception innerException)
```
Initializes a new instance of the `T:IoT.DriverCore.ModbusRx.InvalidModbusRequestException` class.

- Parameter `message`: The error message that explains the reason for the exception.
- Parameter `exceptionCode`: The Modbus exception code to provide to the slave.
- Parameter `innerException`: The exception that caused the current exception. If `innerException` is not null, a null reference, the current exception is raised in a catch block that handles the inner exception.

###### `M:IoT.DriverCore.ModbusRx.InvalidModbusRequestException.#ctor(System.String,System.Exception)`

```csharp
public IoT.DriverCore.ModbusRx.InvalidModbusRequestException(string message, System.Exception innerException)
```
Initializes a new instance of the `T:IoT.DriverCore.ModbusRx.InvalidModbusRequestException` class.

- Parameter `message`: The error message that explains the reason for the exception.
- Parameter `innerException`: The exception that is the cause of the current exception.

###### `P:IoT.DriverCore.ModbusRx.InvalidModbusRequestException.ExceptionCode`

```csharp
public byte ExceptionCode { get; }
```
Gets the Modbus exception code to provide to the slave.

- Value: The `ExceptionCode` value.

#### `T:IoT.DriverCore.ModbusRx.LogicalTags.ModbusByteOrder`

```csharp
public enum IoT.DriverCore.ModbusRx.LogicalTags.ModbusByteOrder
```
Describes byte and word ordering for register values.

##### Declared public members

###### `F:IoT.DriverCore.ModbusRx.LogicalTags.ModbusByteOrder.BigEndian`

```csharp
public static const IoT.DriverCore.ModbusRx.LogicalTags.ModbusByteOrder BigEndian
```
Bytes and words are stored most-significant first (ABCD).

###### `F:IoT.DriverCore.ModbusRx.LogicalTags.ModbusByteOrder.BigEndianWordSwap`

```csharp
public static const IoT.DriverCore.ModbusRx.LogicalTags.ModbusByteOrder BigEndianWordSwap
```
Big-endian bytes with least-significant word first (CDAB).

###### `F:IoT.DriverCore.ModbusRx.LogicalTags.ModbusByteOrder.LittleEndian`

```csharp
public static const IoT.DriverCore.ModbusRx.LogicalTags.ModbusByteOrder LittleEndian
```
Bytes and words are stored least-significant first (DCBA).

###### `F:IoT.DriverCore.ModbusRx.LogicalTags.ModbusByteOrder.LittleEndianWordSwap`

```csharp
public static const IoT.DriverCore.ModbusRx.LogicalTags.ModbusByteOrder LittleEndianWordSwap
```
Little-endian bytes with most-significant word first (BADC).

#### `T:IoT.DriverCore.ModbusRx.LogicalTags.ModbusDataArea`

```csharp
public enum IoT.DriverCore.ModbusRx.LogicalTags.ModbusDataArea
```
Identifies a Modbus data area.

##### Declared public members

###### `F:IoT.DriverCore.ModbusRx.LogicalTags.ModbusDataArea.Coil`

```csharp
public static const IoT.DriverCore.ModbusRx.LogicalTags.ModbusDataArea Coil
```
Read/write coil bits.

###### `F:IoT.DriverCore.ModbusRx.LogicalTags.ModbusDataArea.DiscreteInput`

```csharp
public static const IoT.DriverCore.ModbusRx.LogicalTags.ModbusDataArea DiscreteInput
```
Read-only discrete input bits.

###### `F:IoT.DriverCore.ModbusRx.LogicalTags.ModbusDataArea.HoldingRegister`

```csharp
public static const IoT.DriverCore.ModbusRx.LogicalTags.ModbusDataArea HoldingRegister
```
Read/write holding registers.

###### `F:IoT.DriverCore.ModbusRx.LogicalTags.ModbusDataArea.InputRegister`

```csharp
public static const IoT.DriverCore.ModbusRx.LogicalTags.ModbusDataArea InputRegister
```
Read-only input registers.

#### `T:IoT.DriverCore.ModbusRx.LogicalTags.ModbusLogicalTag`

```csharp
public class IoT.DriverCore.ModbusRx.LogicalTags.ModbusLogicalTag
```
Maps a logical name to a strongly typed Modbus address.

##### Declared public members

###### `M:IoT.DriverCore.ModbusRx.LogicalTags.ModbusLogicalTag.#ctor(IoT.DriverCore.ModbusRx.LogicalTags.ModbusTagConfiguration)`

```csharp
public IoT.DriverCore.ModbusRx.LogicalTags.ModbusLogicalTag(IoT.DriverCore.ModbusRx.LogicalTags.ModbusTagConfiguration configuration)
```
Initializes a new instance of the `T:IoT.DriverCore.ModbusRx.LogicalTags.ModbusLogicalTag` class.

- Parameter `configuration`: The address and behavior configuration.

###### `M:IoT.DriverCore.ModbusRx.LogicalTags.ModbusLogicalTag.FromLogicalTag(IoT.DriverCore.Core.LogicalTag)`

```csharp
public static IoT.DriverCore.ModbusRx.LogicalTags.ModbusLogicalTag FromLogicalTag(IoT.DriverCore.Core.LogicalTag tag)
```
Converts a common logical tag to a validated Modbus definition.

- Parameter `tag`: The common logical-tag definition.
- Returns: The validated Modbus definition.

###### `M:IoT.DriverCore.ModbusRx.LogicalTags.ModbusLogicalTag.ToLogicalTag`

```csharp
public IoT.DriverCore.Core.LogicalTag ToLogicalTag()
```
Converts this definition to the common logical-tag representation.

- Returns: The common definition.

###### `P:IoT.DriverCore.ModbusRx.LogicalTags.ModbusLogicalTag.AccessMode`

```csharp
public IoT.DriverCore.Core.LogicalTagAccessMode AccessMode { get; }
```
Gets the permitted access mode.

- Value: The `AccessMode` value.

###### `P:IoT.DriverCore.ModbusRx.LogicalTags.ModbusLogicalTag.Address`

```csharp
public ushort Address { get; }
```
Gets the zero-based Modbus address.

- Value: The `Address` value.

###### `P:IoT.DriverCore.ModbusRx.LogicalTags.ModbusLogicalTag.ByteOrder`

```csharp
public IoT.DriverCore.ModbusRx.LogicalTags.ModbusByteOrder ByteOrder { get; }
```
Gets the register byte and word order.

- Value: The `ByteOrder` value.

###### `P:IoT.DriverCore.ModbusRx.LogicalTags.ModbusLogicalTag.ClrDataType`

```csharp
public System.Type ClrDataType { get; }
```
Gets the CLR value type exposed by the tag.

- Value: The `ClrDataType` value.

###### `P:IoT.DriverCore.ModbusRx.LogicalTags.ModbusLogicalTag.Count`

```csharp
public ushort Count { get; }
```
Gets the number of coils, inputs, or registers.

- Value: The `Count` value.

###### `P:IoT.DriverCore.ModbusRx.LogicalTags.ModbusLogicalTag.DataArea`

```csharp
public IoT.DriverCore.ModbusRx.LogicalTags.ModbusDataArea DataArea { get; }
```
Gets the Modbus data area.

- Value: The `DataArea` value.

###### `P:IoT.DriverCore.ModbusRx.LogicalTags.ModbusLogicalTag.Description`

```csharp
public string Description { get; }
```
Gets the optional description.

- Value: The `Description` value.

###### `P:IoT.DriverCore.ModbusRx.LogicalTags.ModbusLogicalTag.GroupName`

```csharp
public string GroupName { get; }
```
Gets the optional group name.

- Value: The `GroupName` value.

###### `P:IoT.DriverCore.ModbusRx.LogicalTags.ModbusLogicalTag.Metadata`

```csharp
public System.Collections.Generic.IReadOnlyDictionary<string, string> Metadata { get; }
```
Gets caller-defined metadata.

- Value: The `Metadata` value.

###### `P:IoT.DriverCore.ModbusRx.LogicalTags.ModbusLogicalTag.Name`

```csharp
public string Name { get; }
```
Gets the unique logical name.

- Value: The `Name` value.

###### `P:IoT.DriverCore.ModbusRx.LogicalTags.ModbusLogicalTag.ScanInterval`

```csharp
public System.Nullable<System.TimeSpan> ScanInterval { get; }
```
Gets the preferred observation interval.

- Value: The `ScanInterval` value.

###### `P:IoT.DriverCore.ModbusRx.LogicalTags.ModbusLogicalTag.UnitId`

```csharp
public byte UnitId { get; }
```
Gets the Modbus unit identifier.

- Value: The `UnitId` value.

#### `T:IoT.DriverCore.ModbusRx.LogicalTags.ModbusLogicalTagClient`

```csharp
public class IoT.DriverCore.ModbusRx.LogicalTags.ModbusLogicalTagClient
```
Adapts the Modbus catalog and configured store to the common logical-tag setup contracts.

##### Declared public members

###### `M:IoT.DriverCore.ModbusRx.LogicalTags.ModbusLogicalTagClient.#ctor(IoT.DriverCore.ModbusRx.Device.IModbusMaster,IoT.DriverCore.ModbusRx.LogicalTags.ModbusTagCatalog,System.Nullable`1{System.TimeSpan})`

```csharp
public IoT.DriverCore.ModbusRx.LogicalTags.ModbusLogicalTagClient(IoT.DriverCore.ModbusRx.Device.IModbusMaster master, IoT.DriverCore.ModbusRx.LogicalTags.ModbusTagCatalog catalog, System.Nullable<System.TimeSpan> defaultScanInterval)
```
Initializes a new instance of `IoT.DriverCore.ModbusRx.LogicalTags.ModbusLogicalTagClient`.

- Parameter `master`: The `master` value.
- Parameter `catalog`: The `catalog` value.
- Parameter `defaultScanInterval`: The `defaultScanInterval` value.

###### `M:IoT.DriverCore.ModbusRx.LogicalTags.ModbusLogicalTagClient.#ctor(IoT.DriverCore.ModbusRx.Device.IModbusMaster,IoT.DriverCore.ModbusRx.LogicalTags.ModbusTagCatalog,System.Nullable`1{System.TimeSpan},System.TimeProvider)`

```csharp
public IoT.DriverCore.ModbusRx.LogicalTags.ModbusLogicalTagClient(IoT.DriverCore.ModbusRx.Device.IModbusMaster master, IoT.DriverCore.ModbusRx.LogicalTags.ModbusTagCatalog catalog, System.Nullable<System.TimeSpan> defaultScanInterval, System.TimeProvider timeProvider)
```
Initializes a new instance of `IoT.DriverCore.ModbusRx.LogicalTags.ModbusLogicalTagClient`.

- Parameter `master`: The `master` value.
- Parameter `catalog`: The `catalog` value.
- Parameter `defaultScanInterval`: The `defaultScanInterval` value.
- Parameter `timeProvider`: The `timeProvider` value.

###### `M:IoT.DriverCore.ModbusRx.LogicalTags.ModbusLogicalTagClient.CreateTag(IoT.DriverCore.ModbusRx.LogicalTags.ModbusTagConfiguration)`

```csharp
public IoT.DriverCore.ModbusRx.LogicalTags.ModbusLogicalTag CreateTag(IoT.DriverCore.ModbusRx.LogicalTags.ModbusTagConfiguration configuration)
```
Creates and registers a validated logical tag.

- Parameter `configuration`: The address and behavior configuration.
- Returns: The registered definition.

###### `M:IoT.DriverCore.ModbusRx.LogicalTags.ModbusLogicalTagClient.DeleteStoredTagAsync(System.String,System.Threading.CancellationToken)`

```csharp
public System.Threading.Tasks.Task<bool> DeleteStoredTagAsync(string name, System.Threading.CancellationToken cancellationToken)
```
Deletes a persisted tag and removes it from the live catalog.

- Parameter `name`: The logical name.
- Parameter `cancellationToken`: The cancellation token.
- Returns: True when the definition existed.

###### `M:IoT.DriverCore.ModbusRx.LogicalTags.ModbusLogicalTagClient.Dispose`

```csharp
public void Dispose()
```
Inherits XML documentation from its implemented or overridden member.

###### `M:IoT.DriverCore.ModbusRx.LogicalTags.ModbusLogicalTagClient.ExportCsvAsync(System.IO.TextWriter,System.Threading.CancellationToken)`

```csharp
public System.Threading.Tasks.Task ExportCsvAsync(System.IO.TextWriter writer, System.Threading.CancellationToken cancellationToken)
```
Exports registered definitions as common RFC 4180 CSV.

- Parameter `writer`: The CSV writer.
- Parameter `cancellationToken`: The cancellation token.
- Returns: A task representing the operation.

###### `M:IoT.DriverCore.ModbusRx.LogicalTags.ModbusLogicalTagClient.GetStoredTagAsync(System.String,System.Threading.CancellationToken)`

```csharp
public System.Threading.Tasks.Task<IoT.DriverCore.ModbusRx.LogicalTags.ModbusLogicalTag> GetStoredTagAsync(string name, System.Threading.CancellationToken cancellationToken)
```
Gets a persisted tag by logical name.

- Parameter `name`: The logical name.
- Parameter `cancellationToken`: The cancellation token.
- Returns: The stored definition, or null.

###### `M:IoT.DriverCore.ModbusRx.LogicalTags.ModbusLogicalTagClient.ImportCsvAsync(System.IO.TextReader,System.Threading.CancellationToken)`

```csharp
public System.Threading.Tasks.Task<int> ImportCsvAsync(System.IO.TextReader reader, System.Threading.CancellationToken cancellationToken)
```
Imports and registers common RFC 4180 CSV definitions.

- Parameter `reader`: The CSV reader.
- Parameter `cancellationToken`: The cancellation token.
- Returns: The number of imported definitions.

###### `M:IoT.DriverCore.ModbusRx.LogicalTags.ModbusLogicalTagClient.InitializeStoreAsync(System.String,System.Threading.CancellationToken)`

```csharp
public System.Threading.Tasks.Task InitializeStoreAsync(string connectionString, System.Threading.CancellationToken cancellationToken)
```
Initializes the SQLite store used by CRUD forwarding methods.

- Parameter `connectionString`: The SQLite connection string.
- Parameter `cancellationToken`: The cancellation token.
- Returns: A task representing the operation.

###### `M:IoT.DriverCore.ModbusRx.LogicalTags.ModbusLogicalTagClient.ListStoredTagsAsync(System.Threading.CancellationToken)`

```csharp
public System.Threading.Tasks.Task<System.Collections.Generic.IReadOnlyList<IoT.DriverCore.ModbusRx.LogicalTags.ModbusLogicalTag>> ListStoredTagsAsync(System.Threading.CancellationToken cancellationToken)
```
Lists persisted tags.

- Parameter `cancellationToken`: The cancellation token.
- Returns: The stored definitions.

###### `M:IoT.DriverCore.ModbusRx.LogicalTags.ModbusLogicalTagClient.LoadTagsAsync(System.Threading.CancellationToken)`

```csharp
public System.Threading.Tasks.Task<int> LoadTagsAsync(System.Threading.CancellationToken cancellationToken)
```
Replaces registered tags with the current SQLite snapshot.

- Parameter `cancellationToken`: The cancellation token.
- Returns: The number of loaded definitions.

###### `M:IoT.DriverCore.ModbusRx.LogicalTags.ModbusLogicalTagClient.Observe(System.String)`

```csharp
public System.IObservable<IoT.DriverCore.Core.LogicalTagValue> Observe(string tagName)
```
Inherits XML documentation from its implemented or overridden member.

- Parameter `tagName`: The `tagName` value.
- Returns: A `System.IObservable<IoT.DriverCore.Core.LogicalTagValue>` result.

###### `M:IoT.DriverCore.ModbusRx.LogicalTags.ModbusLogicalTagClient.ObserveAsync(System.String,System.Threading.CancellationToken)`

```csharp
public System.Collections.Generic.IAsyncEnumerable<IoT.DriverCore.Core.LogicalTagValue> ObserveAsync(string tagName, System.Threading.CancellationToken cancellationToken)
```
Inherits XML documentation from its implemented or overridden member.

- Parameter `tagName`: The `tagName` value.
- Parameter `cancellationToken`: The `cancellationToken` value.
- Returns: A `System.Collections.Generic.IAsyncEnumerable<IoT.DriverCore.Core.LogicalTagValue>` result.

###### `M:IoT.DriverCore.ModbusRx.LogicalTags.ModbusLogicalTagClient.ObserveMany(System.Collections.Generic.IReadOnlyCollection`1{System.String})`

```csharp
public System.IObservable<IoT.DriverCore.Core.LogicalTagValue> ObserveMany(System.Collections.Generic.IReadOnlyCollection<string> tagNames)
```
Executes the `ObserveMany` operation.

- Parameter `tagNames`: The `tagNames` value.
- Returns: A `System.IObservable<IoT.DriverCore.Core.LogicalTagValue>` result.

###### `M:IoT.DriverCore.ModbusRx.LogicalTags.ModbusLogicalTagClient.ObserveManyAsync(System.Collections.Generic.IReadOnlyCollection`1{System.String},System.Threading.CancellationToken)`

```csharp
public System.Collections.Generic.IAsyncEnumerable<IoT.DriverCore.Core.LogicalTagValue> ObserveManyAsync(System.Collections.Generic.IReadOnlyCollection<string> tagNames, System.Threading.CancellationToken cancellationToken)
```
Executes the `ObserveManyAsync` operation.

- Parameter `tagNames`: The `tagNames` value.
- Parameter `cancellationToken`: The `cancellationToken` value.
- Returns: A `System.Collections.Generic.IAsyncEnumerable<IoT.DriverCore.Core.LogicalTagValue>` result.

###### `M:IoT.DriverCore.ModbusRx.LogicalTags.ModbusLogicalTagClient.ReadAsync(System.String,System.Threading.CancellationToken)`

```csharp
public System.Threading.Tasks.Task<IoT.DriverCore.Core.TagOperationResult<IoT.DriverCore.Core.LogicalTagValue>> ReadAsync(string tagName, System.Threading.CancellationToken cancellationToken)
```
Inherits XML documentation from its implemented or overridden member.

- Parameter `tagName`: The `tagName` value.
- Parameter `cancellationToken`: The `cancellationToken` value.
- Returns: A `System.Threading.Tasks.Task<IoT.DriverCore.Core.TagOperationResult<IoT.DriverCore.Core.LogicalTagValue>>` result.

###### `M:IoT.DriverCore.ModbusRx.LogicalTags.ModbusLogicalTagClient.ReadManyAsync(System.Collections.Generic.IReadOnlyCollection`1{System.String},System.Threading.CancellationToken)`

```csharp
public System.Threading.Tasks.Task<System.Collections.Generic.IReadOnlyList<IoT.DriverCore.Core.TagOperationResult<IoT.DriverCore.Core.LogicalTagValue>>> ReadManyAsync(System.Collections.Generic.IReadOnlyCollection<string> tagNames, System.Threading.CancellationToken cancellationToken)
```
Executes the `ReadManyAsync` operation.

- Parameter `tagNames`: The `tagNames` value.
- Parameter `cancellationToken`: The `cancellationToken` value.
- Returns: A `System.Threading.Tasks.Task<System.Collections.Generic.IReadOnlyList<IoT.DriverCore.Core.TagOperationResult<IoT.DriverCore.Core.LogicalTagValue>>>` result.

###### `M:IoT.DriverCore.ModbusRx.LogicalTags.ModbusLogicalTagClient.RegisterTag(IoT.DriverCore.ModbusRx.LogicalTags.ModbusLogicalTag)`

```csharp
public void RegisterTag(IoT.DriverCore.ModbusRx.LogicalTags.ModbusLogicalTag tag)
```
Adds or replaces a logical tag definition.

- Parameter `tag`: The definition to register.

###### `M:IoT.DriverCore.ModbusRx.LogicalTags.ModbusLogicalTagClient.RemoveTag(System.String)`

```csharp
public bool RemoveTag(string name)
```
Removes a logical tag definition.

- Parameter `name`: The logical name.
- Returns: True when the definition existed.

###### `M:IoT.DriverCore.ModbusRx.LogicalTags.ModbusLogicalTagClient.UpdateStoredTagAsync(IoT.DriverCore.ModbusRx.LogicalTags.ModbusLogicalTag,System.Threading.CancellationToken)`

```csharp
public System.Threading.Tasks.Task<bool> UpdateStoredTagAsync(IoT.DriverCore.ModbusRx.LogicalTags.ModbusLogicalTag tag, System.Threading.CancellationToken cancellationToken)
```
Updates a persisted tag and the live catalog when it exists.

- Parameter `tag`: The definition to update.
- Parameter `cancellationToken`: The cancellation token.
- Returns: True when the definition existed.

###### `M:IoT.DriverCore.ModbusRx.LogicalTags.ModbusLogicalTagClient.UpsertStoredTagAsync(IoT.DriverCore.ModbusRx.LogicalTags.ModbusLogicalTag,System.Threading.CancellationToken)`

```csharp
public System.Threading.Tasks.Task UpsertStoredTagAsync(IoT.DriverCore.ModbusRx.LogicalTags.ModbusLogicalTag tag, System.Threading.CancellationToken cancellationToken)
```
Creates or replaces a persisted tag and updates the live catalog.

- Parameter `tag`: The definition to persist.
- Parameter `cancellationToken`: The cancellation token.
- Returns: A task representing the operation.

###### `M:IoT.DriverCore.ModbusRx.LogicalTags.ModbusLogicalTagClient.WriteAsync(IoT.DriverCore.Core.LogicalTagValue,System.Threading.CancellationToken)`

```csharp
public System.Threading.Tasks.Task<IoT.DriverCore.Core.TagOperationResult<IoT.DriverCore.Core.LogicalTagValue>> WriteAsync(IoT.DriverCore.Core.LogicalTagValue value, System.Threading.CancellationToken cancellationToken)
```
Inherits XML documentation from its implemented or overridden member.

- Parameter `value`: The `value` value.
- Parameter `cancellationToken`: The `cancellationToken` value.
- Returns: A `System.Threading.Tasks.Task<IoT.DriverCore.Core.TagOperationResult<IoT.DriverCore.Core.LogicalTagValue>>` result.

###### `M:IoT.DriverCore.ModbusRx.LogicalTags.ModbusLogicalTagClient.WriteManyAsync(System.Collections.Generic.IReadOnlyCollection`1{IoT.DriverCore.Core.LogicalTagValue},System.Threading.CancellationToken)`

```csharp
public System.Threading.Tasks.Task<System.Collections.Generic.IReadOnlyList<IoT.DriverCore.Core.TagOperationResult<IoT.DriverCore.Core.LogicalTagValue>>> WriteManyAsync(System.Collections.Generic.IReadOnlyCollection<IoT.DriverCore.Core.LogicalTagValue> values, System.Threading.CancellationToken cancellationToken)
```
Executes the `WriteManyAsync` operation.

- Parameter `values`: The `values` value.
- Parameter `cancellationToken`: The `cancellationToken` value.
- Returns: A `System.Threading.Tasks.Task<System.Collections.Generic.IReadOnlyList<IoT.DriverCore.Core.TagOperationResult<IoT.DriverCore.Core.LogicalTagValue>>>` result.

###### `P:IoT.DriverCore.ModbusRx.LogicalTags.ModbusLogicalTagClient.Catalog`

```csharp
public IoT.DriverCore.ModbusRx.LogicalTags.ModbusTagCatalog Catalog { get; }
```
Gets the composed Modbus tag catalog.

- Value: The `Catalog` value.

###### `P:IoT.DriverCore.ModbusRx.LogicalTags.ModbusLogicalTagClient.Master`

```csharp
public IoT.DriverCore.ModbusRx.Device.IModbusMaster Master { get; }
```
Gets the unchanged raw Modbus master.

- Value: The `Master` value.

#### `T:IoT.DriverCore.ModbusRx.LogicalTags.ModbusTagCatalog`

```csharp
public class IoT.DriverCore.ModbusRx.LogicalTags.ModbusTagCatalog
```
Provides strongly typed Modbus access over a common logical-tag catalog.

##### Declared public members

###### `M:IoT.DriverCore.ModbusRx.LogicalTags.ModbusTagCatalog.#ctor`

```csharp
public IoT.DriverCore.ModbusRx.LogicalTags.ModbusTagCatalog()
```
Initializes a new instance of the `T:IoT.DriverCore.ModbusRx.LogicalTags.ModbusTagCatalog` class.

###### `M:IoT.DriverCore.ModbusRx.LogicalTags.ModbusTagCatalog.#ctor(IoT.DriverCore.Core.ILogicalTagCatalog)`

```csharp
public IoT.DriverCore.ModbusRx.LogicalTags.ModbusTagCatalog(IoT.DriverCore.Core.ILogicalTagCatalog catalog)
```
Initializes a new instance of the `T:IoT.DriverCore.ModbusRx.LogicalTags.ModbusTagCatalog` class.

- Parameter `catalog`: The common catalog to wrap.

###### `M:IoT.DriverCore.ModbusRx.LogicalTags.ModbusTagCatalog.Create(IoT.DriverCore.ModbusRx.LogicalTags.ModbusTagConfiguration)`

```csharp
public static IoT.DriverCore.ModbusRx.LogicalTags.ModbusLogicalTag Create(IoT.DriverCore.ModbusRx.LogicalTags.ModbusTagConfiguration configuration)
```
Creates a validated tag definition without registering it.

- Parameter `configuration`: The address and behavior configuration.
- Returns: The validated definition.

###### `M:IoT.DriverCore.ModbusRx.LogicalTags.ModbusTagCatalog.Dispose`

```csharp
public void Dispose()
```
Inherits XML documentation from its implemented or overridden member.

###### `M:IoT.DriverCore.ModbusRx.LogicalTags.ModbusTagCatalog.ExportCsvAsync(System.IO.TextWriter,System.Threading.CancellationToken)`

```csharp
public System.Threading.Tasks.Task ExportCsvAsync(System.IO.TextWriter writer, System.Threading.CancellationToken cancellationToken)
```
Exports this catalog using the common RFC 4180 CSV representation.

- Parameter `writer`: The CSV writer.
- Parameter `cancellationToken`: The cancellation token.
- Returns: A task representing the operation.

###### `M:IoT.DriverCore.ModbusRx.LogicalTags.ModbusTagCatalog.ImportCsvAsync(System.IO.TextReader,System.Threading.CancellationToken)`

```csharp
public System.Threading.Tasks.Task<int> ImportCsvAsync(System.IO.TextReader reader, System.Threading.CancellationToken cancellationToken)
```
Imports common RFC 4180 CSV definitions into this catalog.

- Parameter `reader`: The CSV reader.
- Parameter `cancellationToken`: The cancellation token.
- Returns: The number of imported definitions.

###### `M:IoT.DriverCore.ModbusRx.LogicalTags.ModbusTagCatalog.List`

```csharp
public System.Collections.Generic.IReadOnlyList<IoT.DriverCore.ModbusRx.LogicalTags.ModbusLogicalTag> List()
```
Returns a stable logical-name-ordered snapshot.

- Returns: The current definitions.

###### `M:IoT.DriverCore.ModbusRx.LogicalTags.ModbusTagCatalog.LoadFromSqliteAsync(IoT.DriverCore.Core.LogicalTagSqliteStore,System.Threading.CancellationToken)`

```csharp
public System.Threading.Tasks.Task<int> LoadFromSqliteAsync(IoT.DriverCore.Core.LogicalTagSqliteStore store, System.Threading.CancellationToken cancellationToken)
```
Replaces the in-memory snapshot with tags currently stored in SQLite.

- Parameter `store`: The common SQLite store.
- Parameter `cancellationToken`: The cancellation token.
- Returns: The number of loaded definitions.

###### `M:IoT.DriverCore.ModbusRx.LogicalTags.ModbusTagCatalog.TryAdd(IoT.DriverCore.ModbusRx.LogicalTags.ModbusLogicalTag)`

```csharp
public bool TryAdd(IoT.DriverCore.ModbusRx.LogicalTags.ModbusLogicalTag tag)
```
Adds a definition when its logical name is unused.

- Parameter `tag`: The definition to add.
- Returns: True when the definition was added.

###### `M:IoT.DriverCore.ModbusRx.LogicalTags.ModbusTagCatalog.TryGet(System.String,IoT.DriverCore.ModbusRx.LogicalTags.ModbusLogicalTag@)`

```csharp
public bool TryGet(string name, out IoT.DriverCore.ModbusRx.LogicalTags.ModbusLogicalTag tag)
```
Gets a definition by logical name.

- Parameter `name`: The logical name.
- Parameter `tag`: The resolved definition.
- Returns: True when the definition exists.

###### `M:IoT.DriverCore.ModbusRx.LogicalTags.ModbusTagCatalog.TryRemove(System.String,IoT.DriverCore.ModbusRx.LogicalTags.ModbusLogicalTag@)`

```csharp
public bool TryRemove(string name, out IoT.DriverCore.ModbusRx.LogicalTags.ModbusLogicalTag tag)
```
Removes a definition by logical name.

- Parameter `name`: The logical name.
- Parameter `tag`: The removed definition.
- Returns: True when the definition was removed.

###### `M:IoT.DriverCore.ModbusRx.LogicalTags.ModbusTagCatalog.Upsert(IoT.DriverCore.ModbusRx.LogicalTags.ModbusLogicalTag)`

```csharp
public void Upsert(IoT.DriverCore.ModbusRx.LogicalTags.ModbusLogicalTag tag)
```
Adds or replaces a definition.

- Parameter `tag`: The definition to add or replace.

###### `P:IoT.DriverCore.ModbusRx.LogicalTags.ModbusTagCatalog.CoreCatalog`

```csharp
public IoT.DriverCore.Core.ILogicalTagCatalog CoreCatalog { get; }
```
Gets the composed common catalog.

- Value: The `CoreCatalog` value.

#### `T:IoT.DriverCore.ModbusRx.LogicalTags.ModbusTagConfiguration`

```csharp
public class IoT.DriverCore.ModbusRx.LogicalTags.ModbusTagConfiguration
```
Collects the required address and optional behavior of a Modbus logical tag.

##### Declared public members

###### `M:IoT.DriverCore.ModbusRx.LogicalTags.ModbusTagConfiguration.#ctor(System.String,System.Byte,IoT.DriverCore.ModbusRx.LogicalTags.ModbusDataArea,System.UInt16,System.UInt16,System.Type)`

```csharp
public IoT.DriverCore.ModbusRx.LogicalTags.ModbusTagConfiguration(string name, byte unitId, IoT.DriverCore.ModbusRx.LogicalTags.ModbusDataArea dataArea, ushort address, ushort count, System.Type clrDataType)
```
Initializes a new instance of the `T:IoT.DriverCore.ModbusRx.LogicalTags.ModbusTagConfiguration` class.

- Parameter `name`: The unique logical name.
- Parameter `unitId`: The Modbus unit identifier.
- Parameter `dataArea`: The Modbus data area.
- Parameter `address`: The zero-based Modbus address.
- Parameter `count`: The number of Modbus points.
- Parameter `clrDataType`: The exposed CLR data type.

###### `P:IoT.DriverCore.ModbusRx.LogicalTags.ModbusTagConfiguration.AccessMode`

```csharp
public IoT.DriverCore.Core.LogicalTagAccessMode AccessMode { get; set; }
```
Gets or sets the permitted access mode.

- Value: The `AccessMode` value.

###### `P:IoT.DriverCore.ModbusRx.LogicalTags.ModbusTagConfiguration.Address`

```csharp
public ushort Address { get; }
```
Gets the zero-based Modbus address.

- Value: The `Address` value.

###### `P:IoT.DriverCore.ModbusRx.LogicalTags.ModbusTagConfiguration.ByteOrder`

```csharp
public IoT.DriverCore.ModbusRx.LogicalTags.ModbusByteOrder ByteOrder { get; set; }
```
Gets or sets the register byte and word order.

- Value: The `ByteOrder` value.

###### `P:IoT.DriverCore.ModbusRx.LogicalTags.ModbusTagConfiguration.ClrDataType`

```csharp
public System.Type ClrDataType { get; }
```
Gets the exposed CLR data type.

- Value: The `ClrDataType` value.

###### `P:IoT.DriverCore.ModbusRx.LogicalTags.ModbusTagConfiguration.Count`

```csharp
public ushort Count { get; }
```
Gets the number of Modbus points.

- Value: The `Count` value.

###### `P:IoT.DriverCore.ModbusRx.LogicalTags.ModbusTagConfiguration.DataArea`

```csharp
public IoT.DriverCore.ModbusRx.LogicalTags.ModbusDataArea DataArea { get; }
```
Gets the Modbus data area.

- Value: The `DataArea` value.

###### `P:IoT.DriverCore.ModbusRx.LogicalTags.ModbusTagConfiguration.Description`

```csharp
public string Description { get; set; }
```
Gets or sets the optional description.

- Value: The `Description` value.

###### `P:IoT.DriverCore.ModbusRx.LogicalTags.ModbusTagConfiguration.GroupName`

```csharp
public string GroupName { get; set; }
```
Gets or sets the optional group name.

- Value: The `GroupName` value.

###### `P:IoT.DriverCore.ModbusRx.LogicalTags.ModbusTagConfiguration.Metadata`

```csharp
public System.Collections.Generic.IReadOnlyDictionary<string, string> Metadata { get; set; }
```
Gets or sets caller-defined metadata.

- Value: The `Metadata` value.

###### `P:IoT.DriverCore.ModbusRx.LogicalTags.ModbusTagConfiguration.Name`

```csharp
public string Name { get; }
```
Gets the unique logical name.

- Value: The `Name` value.

###### `P:IoT.DriverCore.ModbusRx.LogicalTags.ModbusTagConfiguration.ScanInterval`

```csharp
public System.Nullable<System.TimeSpan> ScanInterval { get; set; }
```
Gets or sets the preferred observation interval.

- Value: The `ScanInterval` value.

###### `P:IoT.DriverCore.ModbusRx.LogicalTags.ModbusTagConfiguration.UnitId`

```csharp
public byte UnitId { get; }
```
Gets the Modbus unit identifier.

- Value: The `UnitId` value.

#### `T:IoT.DriverCore.ModbusRx.LogicalTags.ModbusTagSqliteStore`

```csharp
public class IoT.DriverCore.ModbusRx.LogicalTags.ModbusTagSqliteStore
```
Provides Modbus-specific CRUD over the common SQLite logical-tag store.

##### Declared public members

###### `M:IoT.DriverCore.ModbusRx.LogicalTags.ModbusTagSqliteStore.#ctor(System.String)`

```csharp
public IoT.DriverCore.ModbusRx.LogicalTags.ModbusTagSqliteStore(string connectionString)
```
Initializes a new instance of the `T:IoT.DriverCore.ModbusRx.LogicalTags.ModbusTagSqliteStore` class.

- Parameter `connectionString`: The SQLite connection string.

###### `M:IoT.DriverCore.ModbusRx.LogicalTags.ModbusTagSqliteStore.DeleteAsync(System.String,System.Threading.CancellationToken)`

```csharp
public System.Threading.Tasks.Task<bool> DeleteAsync(string name, System.Threading.CancellationToken cancellationToken)
```
Deletes a stored Modbus tag.

- Parameter `name`: The logical name.
- Parameter `cancellationToken`: The cancellation token.
- Returns: True when the definition existed.

###### `M:IoT.DriverCore.ModbusRx.LogicalTags.ModbusTagSqliteStore.GetAsync(System.String,System.Threading.CancellationToken)`

```csharp
public System.Threading.Tasks.Task<IoT.DriverCore.ModbusRx.LogicalTags.ModbusLogicalTag> GetAsync(string name, System.Threading.CancellationToken cancellationToken)
```
Gets a Modbus tag by logical name.

- Parameter `name`: The logical name.
- Parameter `cancellationToken`: The cancellation token.
- Returns: The stored definition, or null.

###### `M:IoT.DriverCore.ModbusRx.LogicalTags.ModbusTagSqliteStore.InitializeAsync(System.Threading.CancellationToken)`

```csharp
public System.Threading.Tasks.Task InitializeAsync(System.Threading.CancellationToken cancellationToken)
```
Creates or upgrades the common schema.

- Parameter `cancellationToken`: The cancellation token.
- Returns: A task representing the operation.

###### `M:IoT.DriverCore.ModbusRx.LogicalTags.ModbusTagSqliteStore.ListAsync(System.Threading.CancellationToken)`

```csharp
public System.Threading.Tasks.Task<System.Collections.Generic.IReadOnlyList<IoT.DriverCore.ModbusRx.LogicalTags.ModbusLogicalTag>> ListAsync(System.Threading.CancellationToken cancellationToken)
```
Lists all stored Modbus tags.

- Parameter `cancellationToken`: The cancellation token.
- Returns: The stored definitions.

###### `M:IoT.DriverCore.ModbusRx.LogicalTags.ModbusTagSqliteStore.LoadCatalogAsync(System.Threading.CancellationToken)`

```csharp
public System.Threading.Tasks.Task<IoT.DriverCore.ModbusRx.LogicalTags.ModbusTagCatalog> LoadCatalogAsync(System.Threading.CancellationToken cancellationToken)
```
Loads a new in-memory Modbus catalog from SQLite.

- Parameter `cancellationToken`: The cancellation token.
- Returns: The loaded catalog.

###### `M:IoT.DriverCore.ModbusRx.LogicalTags.ModbusTagSqliteStore.UpdateAsync(IoT.DriverCore.ModbusRx.LogicalTags.ModbusLogicalTag,System.Threading.CancellationToken)`

```csharp
public System.Threading.Tasks.Task<bool> UpdateAsync(IoT.DriverCore.ModbusRx.LogicalTags.ModbusLogicalTag tag, System.Threading.CancellationToken cancellationToken)
```
Updates an existing stored Modbus tag.

- Parameter `tag`: The definition to update.
- Parameter `cancellationToken`: The cancellation token.
- Returns: True when the definition existed.

###### `M:IoT.DriverCore.ModbusRx.LogicalTags.ModbusTagSqliteStore.UpsertAsync(IoT.DriverCore.ModbusRx.LogicalTags.ModbusLogicalTag,System.Threading.CancellationToken)`

```csharp
public System.Threading.Tasks.Task UpsertAsync(IoT.DriverCore.ModbusRx.LogicalTags.ModbusLogicalTag tag, System.Threading.CancellationToken cancellationToken)
```
Creates or replaces a stored Modbus tag.

- Parameter `tag`: The definition to persist.
- Parameter `cancellationToken`: The cancellation token.
- Returns: A task representing the operation.

###### `P:IoT.DriverCore.ModbusRx.LogicalTags.ModbusTagSqliteStore.CoreStore`

```csharp
public IoT.DriverCore.Core.LogicalTagSqliteStore CoreStore { get; }
```
Gets the composed common store.

- Value: The `CoreStore` value.

#### `T:IoT.DriverCore.ModbusRx.Message.AbstractModbusMessage`

```csharp
public class IoT.DriverCore.ModbusRx.Message.AbstractModbusMessage
```
Abstract Modbus message.

##### Declared public members

###### `M:IoT.DriverCore.ModbusRx.Message.AbstractModbusMessage.Initialize(System.Byte[])`

```csharp
public void Initialize(byte[] frame)
```
Initializes the specified frame.

- Parameter `frame`: The frame.

###### `P:IoT.DriverCore.ModbusRx.Message.AbstractModbusMessage.FunctionCode`

```csharp
public byte FunctionCode { get; set; }
```
Gets or sets the function code.

- Value: The function code.

###### `P:IoT.DriverCore.ModbusRx.Message.AbstractModbusMessage.MessageFrame`

```csharp
public byte[] MessageFrame { get; }
```
Gets the message frame.

- Value: The message frame.

###### `P:IoT.DriverCore.ModbusRx.Message.AbstractModbusMessage.MinimumFrameSize`

```csharp
public int MinimumFrameSize { get; }
```
Gets the minimum size of the frame.

- Value: The minimum size of the frame.

###### `P:IoT.DriverCore.ModbusRx.Message.AbstractModbusMessage.ProtocolDataUnit`

```csharp
public byte[] ProtocolDataUnit { get; }
```
Gets the protocol data unit.

- Value: The protocol data unit.

###### `P:IoT.DriverCore.ModbusRx.Message.AbstractModbusMessage.SlaveAddress`

```csharp
public byte SlaveAddress { get; set; }
```
Gets or sets the slave address.

- Value: The slave address.

###### `P:IoT.DriverCore.ModbusRx.Message.AbstractModbusMessage.TransactionId`

```csharp
public ushort TransactionId { get; set; }
```
Gets or sets the transaction identifier.

- Value: The transaction identifier.

#### `T:IoT.DriverCore.ModbusRx.Message.AbstractModbusMessageWithData`1`

```csharp
public class IoT.DriverCore.ModbusRx.Message.AbstractModbusMessageWithData`1
```
Provides AbstractModbusMessageWithData functionality.

##### Declared public members

###### `P:IoT.DriverCore.ModbusRx.Message.AbstractModbusMessageWithData`1.Data`

```csharp
public TData Data { get; set; }
```
Gets or sets the data.

- Value: The data.

#### `T:IoT.DriverCore.ModbusRx.Message.IModbusMessage`

```csharp
public interface IoT.DriverCore.ModbusRx.Message.IModbusMessage
```
A message built by the master (client) that initiates a Modbus transaction.

##### Declared public members

###### `M:IoT.DriverCore.ModbusRx.Message.IModbusMessage.Initialize(System.Byte[])`

```csharp
public void Initialize(byte[] frame)
```
Initializes a modbus message from the specified message frame.

- Parameter `frame`: Bytes of Modbus frame.

###### `P:IoT.DriverCore.ModbusRx.Message.IModbusMessage.FunctionCode`

```csharp
public byte FunctionCode { get; set; }
```
Gets or sets the function code tells the server what kind of action to perform.

- Value: The `FunctionCode` value.

###### `P:IoT.DriverCore.ModbusRx.Message.IModbusMessage.MessageFrame`

```csharp
public byte[] MessageFrame { get; }
```
Gets composition of the slave address and protocol data unit.

- Value: The `MessageFrame` value.

###### `P:IoT.DriverCore.ModbusRx.Message.IModbusMessage.ProtocolDataUnit`

```csharp
public byte[] ProtocolDataUnit { get; }
```
Gets composition of the function code and message data.

- Value: The `ProtocolDataUnit` value.

###### `P:IoT.DriverCore.ModbusRx.Message.IModbusMessage.SlaveAddress`

```csharp
public byte SlaveAddress { get; set; }
```
Gets or sets address of the slave (server).

- Value: The `SlaveAddress` value.

###### `P:IoT.DriverCore.ModbusRx.Message.IModbusMessage.TransactionId`

```csharp
public ushort TransactionId { get; set; }
```
Gets or sets a unique identifier assigned to a message when using the IP protocol.

- Value: The `TransactionId` value.

#### `T:IoT.DriverCore.ModbusRx.Message.IModbusRequest`

```csharp
public interface IoT.DriverCore.ModbusRx.Message.IModbusRequest
```
Methods specific to a modbus request message.

##### Declared public members

###### `M:IoT.DriverCore.ModbusRx.Message.IModbusRequest.ValidateResponse(IoT.DriverCore.ModbusRx.Message.IModbusMessage)`

```csharp
public void ValidateResponse(IoT.DriverCore.ModbusRx.Message.IModbusMessage response)
```
Validate the specified response against the current request.

- Parameter `response`: The response.

#### `T:IoT.DriverCore.ModbusRx.Message.ModbusMessageFactory`

```csharp
public class IoT.DriverCore.ModbusRx.Message.ModbusMessageFactory
```
Modbus message factory.

##### Declared public members

###### `M:IoT.DriverCore.ModbusRx.Message.ModbusMessageFactory.CreateModbusMessage``1(``0,System.Byte[])`

```csharp
public static T CreateModbusMessage<T>(T message, byte[] frame)
```
Create a Modbus message.

- Parameter `message`: The message instance to initialize.
- Parameter `frame`: Bytes of Modbus frame.
- Returns: New Modbus message based on type and frame bytes.

###### `M:IoT.DriverCore.ModbusRx.Message.ModbusMessageFactory.CreateModbusRequest(System.Byte[])`

```csharp
public static IoT.DriverCore.ModbusRx.Message.IModbusMessage CreateModbusRequest(byte[] frame)
```
Create a Modbus request.

- Parameter `frame`: Bytes of Modbus frame.
- Returns: Modbus request.

#### `T:IoT.DriverCore.ModbusRx.Message.ReadCoilsInputsRequest`

```csharp
public class IoT.DriverCore.ModbusRx.Message.ReadCoilsInputsRequest
```
Provides ReadCoilsInputsRequest functionality.

##### Declared public members

###### `M:IoT.DriverCore.ModbusRx.Message.ReadCoilsInputsRequest.#ctor`

```csharp
public IoT.DriverCore.ModbusRx.Message.ReadCoilsInputsRequest()
```
Initializes a new instance of the `T:IoT.DriverCore.ModbusRx.Message.ReadCoilsInputsRequest` class.

###### `M:IoT.DriverCore.ModbusRx.Message.ReadCoilsInputsRequest.#ctor(System.Byte,System.Byte,System.UInt16,System.UInt16)`

```csharp
public IoT.DriverCore.ModbusRx.Message.ReadCoilsInputsRequest(byte functionCode, byte slaveAddress, ushort startAddress, ushort numberOfPoints)
```
Initializes a new instance of the `T:IoT.DriverCore.ModbusRx.Message.ReadCoilsInputsRequest` class.

- Parameter `functionCode`: The function code.
- Parameter `slaveAddress`: The slave address.
- Parameter `startAddress`: The start address.
- Parameter `numberOfPoints`: The number of points.

###### `M:IoT.DriverCore.ModbusRx.Message.ReadCoilsInputsRequest.ToString`

```csharp
public string ToString()
```
Inherits XML documentation from its implemented or overridden member.

- Returns: A `string` result.

###### `M:IoT.DriverCore.ModbusRx.Message.ReadCoilsInputsRequest.ValidateResponse(IoT.DriverCore.ModbusRx.Message.IModbusMessage)`

```csharp
public void ValidateResponse(IoT.DriverCore.ModbusRx.Message.IModbusMessage response)
```
Inherits XML documentation from its implemented or overridden member.

- Parameter `response`: The `response` value.

###### `P:IoT.DriverCore.ModbusRx.Message.ReadCoilsInputsRequest.MinimumFrameSize`

```csharp
public int MinimumFrameSize { get; }
```
Inherits XML documentation from its implemented or overridden member.

- Value: The `MinimumFrameSize` value.

###### `P:IoT.DriverCore.ModbusRx.Message.ReadCoilsInputsRequest.NumberOfPoints`

```csharp
public ushort NumberOfPoints { get; set; }
```
Gets or sets the number of points.

- Value: The `NumberOfPoints` value.

###### `P:IoT.DriverCore.ModbusRx.Message.ReadCoilsInputsRequest.StartAddress`

```csharp
public ushort StartAddress { get; set; }
```
Gets or sets the start address.

- Value: The start address.

#### `T:IoT.DriverCore.ModbusRx.Message.ReadCoilsInputsResponse`

```csharp
public class IoT.DriverCore.ModbusRx.Message.ReadCoilsInputsResponse
```
Provides ReadCoilsInputsResponse functionality.

##### Declared public members

###### `M:IoT.DriverCore.ModbusRx.Message.ReadCoilsInputsResponse.#ctor`

```csharp
public IoT.DriverCore.ModbusRx.Message.ReadCoilsInputsResponse()
```
Initializes a new instance of the `T:IoT.DriverCore.ModbusRx.Message.ReadCoilsInputsResponse` class.

###### `M:IoT.DriverCore.ModbusRx.Message.ReadCoilsInputsResponse.#ctor(System.Byte,System.Byte,System.Byte,IoT.DriverCore.ModbusRx.Data.DiscreteCollection)`

```csharp
public IoT.DriverCore.ModbusRx.Message.ReadCoilsInputsResponse(byte functionCode, byte slaveAddress, byte byteCount, IoT.DriverCore.ModbusRx.Data.DiscreteCollection data)
```
Initializes a new instance of the `T:IoT.DriverCore.ModbusRx.Message.ReadCoilsInputsResponse` class.

- Parameter `functionCode`: The function code.
- Parameter `slaveAddress`: The slave address.
- Parameter `byteCount`: The byte count.
- Parameter `data`: The data.

###### `M:IoT.DriverCore.ModbusRx.Message.ReadCoilsInputsResponse.ToString`

```csharp
public string ToString()
```
Inherits XML documentation from its implemented or overridden member.

- Returns: A `string` result.

###### `P:IoT.DriverCore.ModbusRx.Message.ReadCoilsInputsResponse.ByteCount`

```csharp
public byte ByteCount { get; set; }
```
Gets or sets the byte count.

- Value: The byte count.

###### `P:IoT.DriverCore.ModbusRx.Message.ReadCoilsInputsResponse.MinimumFrameSize`

```csharp
public int MinimumFrameSize { get; }
```
Inherits XML documentation from its implemented or overridden member.

- Value: The `MinimumFrameSize` value.

#### `T:IoT.DriverCore.ModbusRx.Message.ReadHoldingInputRegistersRequest`

```csharp
public class IoT.DriverCore.ModbusRx.Message.ReadHoldingInputRegistersRequest
```
Provides ReadHoldingInputRegistersRequest functionality.

##### Declared public members

###### `M:IoT.DriverCore.ModbusRx.Message.ReadHoldingInputRegistersRequest.#ctor`

```csharp
public IoT.DriverCore.ModbusRx.Message.ReadHoldingInputRegistersRequest()
```
Initializes a new instance of the `T:IoT.DriverCore.ModbusRx.Message.ReadHoldingInputRegistersRequest` class.

###### `M:IoT.DriverCore.ModbusRx.Message.ReadHoldingInputRegistersRequest.#ctor(System.Byte,System.Byte,System.UInt16,System.UInt16)`

```csharp
public IoT.DriverCore.ModbusRx.Message.ReadHoldingInputRegistersRequest(byte functionCode, byte slaveAddress, ushort startAddress, ushort numberOfPoints)
```
Initializes a new instance of the `T:IoT.DriverCore.ModbusRx.Message.ReadHoldingInputRegistersRequest` class.

- Parameter `functionCode`: The function code.
- Parameter `slaveAddress`: The slave address.
- Parameter `startAddress`: The start address.
- Parameter `numberOfPoints`: The number of points.

###### `M:IoT.DriverCore.ModbusRx.Message.ReadHoldingInputRegistersRequest.ToString`

```csharp
public string ToString()
```
Inherits XML documentation from its implemented or overridden member.

- Returns: A `string` result.

###### `M:IoT.DriverCore.ModbusRx.Message.ReadHoldingInputRegistersRequest.ValidateResponse(IoT.DriverCore.ModbusRx.Message.IModbusMessage)`

```csharp
public void ValidateResponse(IoT.DriverCore.ModbusRx.Message.IModbusMessage response)
```
Inherits XML documentation from its implemented or overridden member.

- Parameter `response`: The `response` value.

###### `P:IoT.DriverCore.ModbusRx.Message.ReadHoldingInputRegistersRequest.MinimumFrameSize`

```csharp
public int MinimumFrameSize { get; }
```
Inherits XML documentation from its implemented or overridden member.

- Value: The `MinimumFrameSize` value.

###### `P:IoT.DriverCore.ModbusRx.Message.ReadHoldingInputRegistersRequest.NumberOfPoints`

```csharp
public ushort NumberOfPoints { get; set; }
```
Gets or sets the number of points.

- Value: The `NumberOfPoints` value.

###### `P:IoT.DriverCore.ModbusRx.Message.ReadHoldingInputRegistersRequest.StartAddress`

```csharp
public ushort StartAddress { get; set; }
```
Gets or sets the start address.

- Value: The start address.

#### `T:IoT.DriverCore.ModbusRx.Message.ReadHoldingInputRegistersResponse`

```csharp
public class IoT.DriverCore.ModbusRx.Message.ReadHoldingInputRegistersResponse
```
Provides ReadHoldingInputRegistersResponse functionality.

##### Declared public members

###### `M:IoT.DriverCore.ModbusRx.Message.ReadHoldingInputRegistersResponse.#ctor`

```csharp
public IoT.DriverCore.ModbusRx.Message.ReadHoldingInputRegistersResponse()
```
Initializes a new instance of the `T:IoT.DriverCore.ModbusRx.Message.ReadHoldingInputRegistersResponse` class.

###### `M:IoT.DriverCore.ModbusRx.Message.ReadHoldingInputRegistersResponse.#ctor(System.Byte,System.Byte,IoT.DriverCore.ModbusRx.Data.RegisterCollection)`

```csharp
public IoT.DriverCore.ModbusRx.Message.ReadHoldingInputRegistersResponse(byte functionCode, byte slaveAddress, IoT.DriverCore.ModbusRx.Data.RegisterCollection data)
```
Initializes a new instance of the `T:IoT.DriverCore.ModbusRx.Message.ReadHoldingInputRegistersResponse` class.

- Parameter `functionCode`: The function code.
- Parameter `slaveAddress`: The slave address.
- Parameter `data`: The data.

###### `M:IoT.DriverCore.ModbusRx.Message.ReadHoldingInputRegistersResponse.ToString`

```csharp
public string ToString()
```
Inherits XML documentation from its implemented or overridden member.

- Returns: A `string` result.

###### `P:IoT.DriverCore.ModbusRx.Message.ReadHoldingInputRegistersResponse.ByteCount`

```csharp
public byte ByteCount { get; set; }
```
Gets or sets the byte count.

- Value: The byte count.

###### `P:IoT.DriverCore.ModbusRx.Message.ReadHoldingInputRegistersResponse.MinimumFrameSize`

```csharp
public int MinimumFrameSize { get; }
```
Inherits XML documentation from its implemented or overridden member.

- Value: The `MinimumFrameSize` value.

#### `T:IoT.DriverCore.ModbusRx.Message.ReadWriteMultipleRegistersRequest`

```csharp
public class IoT.DriverCore.ModbusRx.Message.ReadWriteMultipleRegistersRequest
```
Provides ReadWriteMultipleRegistersRequest functionality.

##### Declared public members

###### `M:IoT.DriverCore.ModbusRx.Message.ReadWriteMultipleRegistersRequest.#ctor`

```csharp
public IoT.DriverCore.ModbusRx.Message.ReadWriteMultipleRegistersRequest()
```
Initializes a new instance of the `T:IoT.DriverCore.ModbusRx.Message.ReadWriteMultipleRegistersRequest` class.

###### `M:IoT.DriverCore.ModbusRx.Message.ReadWriteMultipleRegistersRequest.#ctor(System.Byte,System.UInt16,System.UInt16,System.UInt16,IoT.DriverCore.ModbusRx.Data.RegisterCollection)`

```csharp
public IoT.DriverCore.ModbusRx.Message.ReadWriteMultipleRegistersRequest(byte slaveAddress, ushort startReadAddress, ushort numberOfPointsToRead, ushort startWriteAddress, IoT.DriverCore.ModbusRx.Data.RegisterCollection writeData)
```
Initializes a new instance of the `T:IoT.DriverCore.ModbusRx.Message.ReadWriteMultipleRegistersRequest` class.

- Parameter `slaveAddress`: The slave address.
- Parameter `startReadAddress`: The start read address.
- Parameter `numberOfPointsToRead`: The number of points to read.
- Parameter `startWriteAddress`: The start write address.
- Parameter `writeData`: The write data.

###### `M:IoT.DriverCore.ModbusRx.Message.ReadWriteMultipleRegistersRequest.ToString`

```csharp
public string ToString()
```
Inherits XML documentation from its implemented or overridden member.

- Returns: A `string` result.

###### `M:IoT.DriverCore.ModbusRx.Message.ReadWriteMultipleRegistersRequest.ValidateResponse(IoT.DriverCore.ModbusRx.Message.IModbusMessage)`

```csharp
public void ValidateResponse(IoT.DriverCore.ModbusRx.Message.IModbusMessage response)
```
Inherits XML documentation from its implemented or overridden member.

- Parameter `response`: The `response` value.

###### `P:IoT.DriverCore.ModbusRx.Message.ReadWriteMultipleRegistersRequest.MinimumFrameSize`

```csharp
public int MinimumFrameSize { get; }
```
Inherits XML documentation from its implemented or overridden member.

- Value: The `MinimumFrameSize` value.

###### `P:IoT.DriverCore.ModbusRx.Message.ReadWriteMultipleRegistersRequest.ProtocolDataUnit`

```csharp
public byte[] ProtocolDataUnit { get; }
```
Inherits XML documentation from its implemented or overridden member.

- Value: The `ProtocolDataUnit` value.

###### `P:IoT.DriverCore.ModbusRx.Message.ReadWriteMultipleRegistersRequest.ReadRequest`

```csharp
public IoT.DriverCore.ModbusRx.Message.ReadHoldingInputRegistersRequest ReadRequest { get; }
```
Gets the read request.

- Value: The read request.

###### `P:IoT.DriverCore.ModbusRx.Message.ReadWriteMultipleRegistersRequest.WriteRequest`

```csharp
public IoT.DriverCore.ModbusRx.Message.WriteMultipleRegistersRequest WriteRequest { get; }
```
Gets the write request.

- Value: The write request.

#### `T:IoT.DriverCore.ModbusRx.Message.SlaveExceptionResponse`

```csharp
public class IoT.DriverCore.ModbusRx.Message.SlaveExceptionResponse
```
Provides SlaveExceptionResponse functionality.

##### Declared public members

###### `M:IoT.DriverCore.ModbusRx.Message.SlaveExceptionResponse.#ctor`

```csharp
public IoT.DriverCore.ModbusRx.Message.SlaveExceptionResponse()
```
Initializes a new instance of the `T:IoT.DriverCore.ModbusRx.Message.SlaveExceptionResponse` class.

###### `M:IoT.DriverCore.ModbusRx.Message.SlaveExceptionResponse.#ctor(System.Byte,System.Byte,System.Byte)`

```csharp
public IoT.DriverCore.ModbusRx.Message.SlaveExceptionResponse(byte slaveAddress, byte functionCode, byte exceptionCode)
```
Initializes a new instance of the `T:IoT.DriverCore.ModbusRx.Message.SlaveExceptionResponse` class.

- Parameter `slaveAddress`: The slave address.
- Parameter `functionCode`: The function code.
- Parameter `exceptionCode`: The exception code.

###### `M:IoT.DriverCore.ModbusRx.Message.SlaveExceptionResponse.ToString`

```csharp
public string ToString()
```
Returns a string that represents the current object.

- Returns: A `T:System.String` that represents the current `T:System.Object` .

###### `P:IoT.DriverCore.ModbusRx.Message.SlaveExceptionResponse.MinimumFrameSize`

```csharp
public int MinimumFrameSize { get; }
```
Inherits XML documentation from its implemented or overridden member.

- Value: The `MinimumFrameSize` value.

###### `P:IoT.DriverCore.ModbusRx.Message.SlaveExceptionResponse.SlaveExceptionCode`

```csharp
public byte SlaveExceptionCode { get; set; }
```
Gets or sets the slave exception code.

- Value: The slave exception code.

#### `T:IoT.DriverCore.ModbusRx.Message.WriteMultipleCoilsRequest`

```csharp
public class IoT.DriverCore.ModbusRx.Message.WriteMultipleCoilsRequest
```
Write Multiple Coils request.

##### Declared public members

###### `M:IoT.DriverCore.ModbusRx.Message.WriteMultipleCoilsRequest.#ctor`

```csharp
public IoT.DriverCore.ModbusRx.Message.WriteMultipleCoilsRequest()
```
Initializes a new instance of the `T:IoT.DriverCore.ModbusRx.Message.WriteMultipleCoilsRequest` class.

###### `M:IoT.DriverCore.ModbusRx.Message.WriteMultipleCoilsRequest.#ctor(System.Byte,System.UInt16,IoT.DriverCore.ModbusRx.Data.DiscreteCollection)`

```csharp
public IoT.DriverCore.ModbusRx.Message.WriteMultipleCoilsRequest(byte slaveAddress, ushort startAddress, IoT.DriverCore.ModbusRx.Data.DiscreteCollection data)
```
Initializes a new instance of the `T:IoT.DriverCore.ModbusRx.Message.WriteMultipleCoilsRequest` class.

- Parameter `slaveAddress`: The slave address.
- Parameter `startAddress`: The start address.
- Parameter `data`: The data.

###### `M:IoT.DriverCore.ModbusRx.Message.WriteMultipleCoilsRequest.ToString`

```csharp
public string ToString()
```
Inherits XML documentation from its implemented or overridden member.

- Returns: A `string` result.

###### `M:IoT.DriverCore.ModbusRx.Message.WriteMultipleCoilsRequest.ValidateResponse(IoT.DriverCore.ModbusRx.Message.IModbusMessage)`

```csharp
public void ValidateResponse(IoT.DriverCore.ModbusRx.Message.IModbusMessage response)
```
Inherits XML documentation from its implemented or overridden member.

- Parameter `response`: The `response` value.

###### `P:IoT.DriverCore.ModbusRx.Message.WriteMultipleCoilsRequest.ByteCount`

```csharp
public byte ByteCount { get; set; }
```
Gets or sets the byte count.

- Value: The byte count.

###### `P:IoT.DriverCore.ModbusRx.Message.WriteMultipleCoilsRequest.MinimumFrameSize`

```csharp
public int MinimumFrameSize { get; }
```
Inherits XML documentation from its implemented or overridden member.

- Value: The `MinimumFrameSize` value.

###### `P:IoT.DriverCore.ModbusRx.Message.WriteMultipleCoilsRequest.NumberOfPoints`

```csharp
public ushort NumberOfPoints { get; set; }
```
Gets or sets the number of points.

- Value: The `NumberOfPoints` value.

###### `P:IoT.DriverCore.ModbusRx.Message.WriteMultipleCoilsRequest.StartAddress`

```csharp
public ushort StartAddress { get; set; }
```
Gets or sets the start address.

- Value: The start address.

#### `T:IoT.DriverCore.ModbusRx.Message.WriteMultipleCoilsResponse`

```csharp
public class IoT.DriverCore.ModbusRx.Message.WriteMultipleCoilsResponse
```
Provides WriteMultipleCoilsResponse functionality.

##### Declared public members

###### `M:IoT.DriverCore.ModbusRx.Message.WriteMultipleCoilsResponse.#ctor`

```csharp
public IoT.DriverCore.ModbusRx.Message.WriteMultipleCoilsResponse()
```
Initializes a new instance of the `T:IoT.DriverCore.ModbusRx.Message.WriteMultipleCoilsResponse` class.

###### `M:IoT.DriverCore.ModbusRx.Message.WriteMultipleCoilsResponse.#ctor(System.Byte,System.UInt16,System.UInt16)`

```csharp
public IoT.DriverCore.ModbusRx.Message.WriteMultipleCoilsResponse(byte slaveAddress, ushort startAddress, ushort numberOfPoints)
```
Initializes a new instance of the `T:IoT.DriverCore.ModbusRx.Message.WriteMultipleCoilsResponse` class.

- Parameter `slaveAddress`: The slave address.
- Parameter `startAddress`: The start address.
- Parameter `numberOfPoints`: The number of points.

###### `M:IoT.DriverCore.ModbusRx.Message.WriteMultipleCoilsResponse.ToString`

```csharp
public string ToString()
```
Inherits XML documentation from its implemented or overridden member.

- Returns: A `string` result.

###### `P:IoT.DriverCore.ModbusRx.Message.WriteMultipleCoilsResponse.MinimumFrameSize`

```csharp
public int MinimumFrameSize { get; }
```
Inherits XML documentation from its implemented or overridden member.

- Value: The `MinimumFrameSize` value.

###### `P:IoT.DriverCore.ModbusRx.Message.WriteMultipleCoilsResponse.NumberOfPoints`

```csharp
public ushort NumberOfPoints { get; set; }
```
Gets or sets the number of points.

- Value: The `NumberOfPoints` value.

###### `P:IoT.DriverCore.ModbusRx.Message.WriteMultipleCoilsResponse.StartAddress`

```csharp
public ushort StartAddress { get; set; }
```
Gets or sets the start address.

- Value: The start address.

#### `T:IoT.DriverCore.ModbusRx.Message.WriteMultipleRegistersRequest`

```csharp
public class IoT.DriverCore.ModbusRx.Message.WriteMultipleRegistersRequest
```
Provides WriteMultipleRegistersRequest functionality.

##### Declared public members

###### `M:IoT.DriverCore.ModbusRx.Message.WriteMultipleRegistersRequest.#ctor`

```csharp
public IoT.DriverCore.ModbusRx.Message.WriteMultipleRegistersRequest()
```
Initializes a new instance of the `T:IoT.DriverCore.ModbusRx.Message.WriteMultipleRegistersRequest` class.

###### `M:IoT.DriverCore.ModbusRx.Message.WriteMultipleRegistersRequest.#ctor(System.Byte,System.UInt16,IoT.DriverCore.ModbusRx.Data.RegisterCollection)`

```csharp
public IoT.DriverCore.ModbusRx.Message.WriteMultipleRegistersRequest(byte slaveAddress, ushort startAddress, IoT.DriverCore.ModbusRx.Data.RegisterCollection data)
```
Initializes a new instance of the `T:IoT.DriverCore.ModbusRx.Message.WriteMultipleRegistersRequest` class.

- Parameter `slaveAddress`: The slave address.
- Parameter `startAddress`: The start address.
- Parameter `data`: The data.

###### `M:IoT.DriverCore.ModbusRx.Message.WriteMultipleRegistersRequest.ToString`

```csharp
public string ToString()
```
Inherits XML documentation from its implemented or overridden member.

- Returns: A `string` result.

###### `M:IoT.DriverCore.ModbusRx.Message.WriteMultipleRegistersRequest.ValidateResponse(IoT.DriverCore.ModbusRx.Message.IModbusMessage)`

```csharp
public void ValidateResponse(IoT.DriverCore.ModbusRx.Message.IModbusMessage response)
```
Inherits XML documentation from its implemented or overridden member.

- Parameter `response`: The `response` value.

###### `P:IoT.DriverCore.ModbusRx.Message.WriteMultipleRegistersRequest.ByteCount`

```csharp
public byte ByteCount { get; set; }
```
Gets or sets the byte count.

- Value: The byte count.

###### `P:IoT.DriverCore.ModbusRx.Message.WriteMultipleRegistersRequest.MinimumFrameSize`

```csharp
public int MinimumFrameSize { get; }
```
Inherits XML documentation from its implemented or overridden member.

- Value: The `MinimumFrameSize` value.

###### `P:IoT.DriverCore.ModbusRx.Message.WriteMultipleRegistersRequest.NumberOfPoints`

```csharp
public ushort NumberOfPoints { get; set; }
```
Gets or sets the number of points.

- Value: The `NumberOfPoints` value.

###### `P:IoT.DriverCore.ModbusRx.Message.WriteMultipleRegistersRequest.StartAddress`

```csharp
public ushort StartAddress { get; set; }
```
Gets or sets the start address.

- Value: The start address.

#### `T:IoT.DriverCore.ModbusRx.Message.WriteMultipleRegistersResponse`

```csharp
public class IoT.DriverCore.ModbusRx.Message.WriteMultipleRegistersResponse
```
Provides WriteMultipleRegistersResponse functionality.

##### Declared public members

###### `M:IoT.DriverCore.ModbusRx.Message.WriteMultipleRegistersResponse.#ctor`

```csharp
public IoT.DriverCore.ModbusRx.Message.WriteMultipleRegistersResponse()
```
Initializes a new instance of the `T:IoT.DriverCore.ModbusRx.Message.WriteMultipleRegistersResponse` class.

###### `M:IoT.DriverCore.ModbusRx.Message.WriteMultipleRegistersResponse.#ctor(System.Byte,System.UInt16,System.UInt16)`

```csharp
public IoT.DriverCore.ModbusRx.Message.WriteMultipleRegistersResponse(byte slaveAddress, ushort startAddress, ushort numberOfPoints)
```
Initializes a new instance of the `T:IoT.DriverCore.ModbusRx.Message.WriteMultipleRegistersResponse` class.

- Parameter `slaveAddress`: The slave address.
- Parameter `startAddress`: The start address.
- Parameter `numberOfPoints`: The number of points.

###### `M:IoT.DriverCore.ModbusRx.Message.WriteMultipleRegistersResponse.ToString`

```csharp
public string ToString()
```
Converts to string.

- Returns: A `T:System.String` that represents this instance.

###### `P:IoT.DriverCore.ModbusRx.Message.WriteMultipleRegistersResponse.MinimumFrameSize`

```csharp
public int MinimumFrameSize { get; }
```
Gets the minimum size of the frame.

- Value: The minimum size of the frame.

###### `P:IoT.DriverCore.ModbusRx.Message.WriteMultipleRegistersResponse.NumberOfPoints`

```csharp
public ushort NumberOfPoints { get; set; }
```
Gets or sets the number of points.

- Value: The `NumberOfPoints` value.

###### `P:IoT.DriverCore.ModbusRx.Message.WriteMultipleRegistersResponse.StartAddress`

```csharp
public ushort StartAddress { get; set; }
```
Gets or sets the start address.

- Value: The start address.

#### `T:IoT.DriverCore.ModbusRx.Message.WriteSingleCoilRequestResponse`

```csharp
public class IoT.DriverCore.ModbusRx.Message.WriteSingleCoilRequestResponse
```
Provides WriteSingleCoilRequestResponse functionality.

##### Declared public members

###### `M:IoT.DriverCore.ModbusRx.Message.WriteSingleCoilRequestResponse.#ctor`

```csharp
public IoT.DriverCore.ModbusRx.Message.WriteSingleCoilRequestResponse()
```
Initializes a new instance of the `T:IoT.DriverCore.ModbusRx.Message.WriteSingleCoilRequestResponse` class.

###### `M:IoT.DriverCore.ModbusRx.Message.WriteSingleCoilRequestResponse.#ctor(System.Byte,System.UInt16,System.Boolean)`

```csharp
public IoT.DriverCore.ModbusRx.Message.WriteSingleCoilRequestResponse(byte slaveAddress, ushort startAddress, bool coilState)
```
Initializes a new instance of the `T:IoT.DriverCore.ModbusRx.Message.WriteSingleCoilRequestResponse` class.

- Parameter `slaveAddress`: The slave address.
- Parameter `startAddress`: The start address.
- Parameter `coilState`: if set to true [coil state].

###### `M:IoT.DriverCore.ModbusRx.Message.WriteSingleCoilRequestResponse.ToString`

```csharp
public string ToString()
```
Converts to string.

- Returns: A `T:System.String` that represents this instance.

###### `M:IoT.DriverCore.ModbusRx.Message.WriteSingleCoilRequestResponse.ValidateResponse(IoT.DriverCore.ModbusRx.Message.IModbusMessage)`

```csharp
public void ValidateResponse(IoT.DriverCore.ModbusRx.Message.IModbusMessage response)
```
Validate the specified response against the current request.

- Parameter `response`: The Modbus Message.

###### `P:IoT.DriverCore.ModbusRx.Message.WriteSingleCoilRequestResponse.MinimumFrameSize`

```csharp
public int MinimumFrameSize { get; }
```
Gets the minimum size of the frame.

- Value: The minimum size of the frame.

###### `P:IoT.DriverCore.ModbusRx.Message.WriteSingleCoilRequestResponse.StartAddress`

```csharp
public ushort StartAddress { get; set; }
```
Gets or sets the start address.

- Value: The start address.

#### `T:IoT.DriverCore.ModbusRx.Message.WriteSingleRegisterRequestResponse`

```csharp
public class IoT.DriverCore.ModbusRx.Message.WriteSingleRegisterRequestResponse
```
Provides WriteSingleRegisterRequestResponse functionality.

##### Declared public members

###### `M:IoT.DriverCore.ModbusRx.Message.WriteSingleRegisterRequestResponse.#ctor`

```csharp
public IoT.DriverCore.ModbusRx.Message.WriteSingleRegisterRequestResponse()
```
Initializes a new instance of the `T:IoT.DriverCore.ModbusRx.Message.WriteSingleRegisterRequestResponse` class.

###### `M:IoT.DriverCore.ModbusRx.Message.WriteSingleRegisterRequestResponse.#ctor(System.Byte,System.UInt16,System.UInt16)`

```csharp
public IoT.DriverCore.ModbusRx.Message.WriteSingleRegisterRequestResponse(byte slaveAddress, ushort startAddress, ushort registerValue)
```
Initializes a new instance of the `T:IoT.DriverCore.ModbusRx.Message.WriteSingleRegisterRequestResponse` class.

- Parameter `slaveAddress`: The slave address.
- Parameter `startAddress`: The start address.
- Parameter `registerValue`: The register value.

###### `M:IoT.DriverCore.ModbusRx.Message.WriteSingleRegisterRequestResponse.ToString`

```csharp
public string ToString()
```
Converts to string.

- Returns: A `T:System.String` that represents this instance.

###### `M:IoT.DriverCore.ModbusRx.Message.WriteSingleRegisterRequestResponse.ValidateResponse(IoT.DriverCore.ModbusRx.Message.IModbusMessage)`

```csharp
public void ValidateResponse(IoT.DriverCore.ModbusRx.Message.IModbusMessage response)
```
Validate the specified response against the current request.

- Parameter `response`: The Modbus message.

###### `P:IoT.DriverCore.ModbusRx.Message.WriteSingleRegisterRequestResponse.MinimumFrameSize`

```csharp
public int MinimumFrameSize { get; }
```
Gets the minimum size of the frame.

- Value: The minimum size of the frame.

###### `P:IoT.DriverCore.ModbusRx.Message.WriteSingleRegisterRequestResponse.StartAddress`

```csharp
public ushort StartAddress { get; set; }
```
Gets or sets the start address.

- Value: The start address.

#### `T:IoT.DriverCore.ModbusRx.ModbusAsyncObservableExtensions`

```csharp
public class IoT.DriverCore.ModbusRx.ModbusAsyncObservableExtensions
```
Async-observable adapters for Modbus reactive operations.

##### Declared public members

###### `M:IoT.DriverCore.ModbusRx.ModbusAsyncObservableExtensions.ObserveCoilsObservable(IoT.DriverCore.ModbusRx.Device.ModbusServer,System.UInt16,System.UInt16,System.Double)`

```csharp
public static ReactiveUI.Primitives.Async.IObservableAsync<bool[]> ObserveCoilsObservable(IoT.DriverCore.ModbusRx.Device.ModbusServer server, ushort startAddress, ushort count, double interval)
```
Observes coil changes as an async observable.

- Parameter `server`: The extension receiver.
- Parameter `startAddress`: The starting address to monitor.
- Parameter `count`: The number of coils to monitor.
- Parameter `interval`: The polling interval in milliseconds.
- Returns: An async observable of coils.

###### `M:IoT.DriverCore.ModbusRx.ModbusAsyncObservableExtensions.ObserveDataChangesObservable(IoT.DriverCore.ModbusRx.Device.ModbusServer,System.Double)`

```csharp
public static ReactiveUI.Primitives.Async.IObservableAsync<System.ValueTuple<ushort[], ushort[], bool[], bool[]>> ObserveDataChangesObservable(IoT.DriverCore.ModbusRx.Device.ModbusServer server, double interval)
```
Observes server data changes as an async observable.

- Parameter `server`: The extension receiver.
- Parameter `interval`: The polling interval in milliseconds.
- Returns: An async observable of data snapshots.

###### `M:IoT.DriverCore.ModbusRx.ModbusAsyncObservableExtensions.ObserveDataStoreReadFromObservable(IoT.DriverCore.ModbusRx.Device.ModbusSlave)`

```csharp
public static ReactiveUI.Primitives.Async.IObservableAsync<IoT.DriverCore.ModbusRx.Data.DataStoreEventArgs> ObserveDataStoreReadFromObservable(IoT.DriverCore.ModbusRx.Device.ModbusSlave slave)
```
Observes data-store reads as an async observable.

- Parameter `slave`: The extension receiver.
- Returns: An async observable of data-store events.

###### `M:IoT.DriverCore.ModbusRx.ModbusAsyncObservableExtensions.ObserveDataStoreWrittenToObservable(IoT.DriverCore.ModbusRx.Device.ModbusSlave)`

```csharp
public static ReactiveUI.Primitives.Async.IObservableAsync<IoT.DriverCore.ModbusRx.Data.DataStoreEventArgs> ObserveDataStoreWrittenToObservable(IoT.DriverCore.ModbusRx.Device.ModbusSlave slave)
```
Observes data-store writes as an async observable.

- Parameter `slave`: The extension receiver.
- Returns: An async observable of data-store events.

###### `M:IoT.DriverCore.ModbusRx.ModbusAsyncObservableExtensions.ObserveDiscreteInputsObservable(IoT.DriverCore.ModbusRx.Device.ModbusServer,System.UInt16,System.UInt16,System.Double)`

```csharp
public static ReactiveUI.Primitives.Async.IObservableAsync<bool[]> ObserveDiscreteInputsObservable(IoT.DriverCore.ModbusRx.Device.ModbusServer server, ushort startAddress, ushort count, double interval)
```
Observes discrete input changes as an async observable.

- Parameter `server`: The extension receiver.
- Parameter `startAddress`: The starting address to monitor.
- Parameter `count`: The number of inputs to monitor.
- Parameter `interval`: The polling interval in milliseconds.
- Returns: An async observable of discrete inputs.

###### `M:IoT.DriverCore.ModbusRx.ModbusAsyncObservableExtensions.ObserveHoldingRegistersObservable(IoT.DriverCore.ModbusRx.Device.ModbusServer,System.UInt16,System.UInt16,System.Double)`

```csharp
public static ReactiveUI.Primitives.Async.IObservableAsync<ushort[]> ObserveHoldingRegistersObservable(IoT.DriverCore.ModbusRx.Device.ModbusServer server, ushort startAddress, ushort count, double interval)
```
Observes holding-register changes as an async observable.

- Parameter `server`: The extension receiver.
- Parameter `startAddress`: The starting address to monitor.
- Parameter `count`: The number of registers to monitor.
- Parameter `interval`: The polling interval in milliseconds.
- Returns: An async observable of holding registers.

###### `M:IoT.DriverCore.ModbusRx.ModbusAsyncObservableExtensions.ObserveInputRegistersObservable(IoT.DriverCore.ModbusRx.Device.ModbusServer,System.UInt16,System.UInt16,System.Double)`

```csharp
public static ReactiveUI.Primitives.Async.IObservableAsync<ushort[]> ObserveInputRegistersObservable(IoT.DriverCore.ModbusRx.Device.ModbusServer server, ushort startAddress, ushort count, double interval)
```
Observes input-register changes as an async observable.

- Parameter `server`: The extension receiver.
- Parameter `startAddress`: The starting address to monitor.
- Parameter `count`: The number of registers to monitor.
- Parameter `interval`: The polling interval in milliseconds.
- Returns: An async observable of input registers.

###### `M:IoT.DriverCore.ModbusRx.ModbusAsyncObservableExtensions.ObserveRequestObservable(IoT.DriverCore.ModbusRx.Device.ModbusSlave)`

```csharp
public static ReactiveUI.Primitives.Async.IObservableAsync<IoT.DriverCore.ModbusRx.Device.ModbusSlaveRequestEventArgs> ObserveRequestObservable(IoT.DriverCore.ModbusRx.Device.ModbusSlave slave)
```
Observes slave requests as an async observable.

- Parameter `slave`: The extension receiver.
- Returns: An async observable of request events.

###### `M:IoT.DriverCore.ModbusRx.ModbusAsyncObservableExtensions.ObserveWriteCompleteObservable(IoT.DriverCore.ModbusRx.Device.ModbusSlave)`

```csharp
public static ReactiveUI.Primitives.Async.IObservableAsync<IoT.DriverCore.ModbusRx.Device.ModbusSlaveRequestEventArgs> ObserveWriteCompleteObservable(IoT.DriverCore.ModbusRx.Device.ModbusSlave slave)
```
Observes write completion as an async observable.

- Parameter `slave`: The extension receiver.
- Returns: An async observable of request events.

###### `M:IoT.DriverCore.ModbusRx.ModbusAsyncObservableExtensions.ReadCoils(ReactiveUI.Primitives.Async.IObservableAsync`1{System.ValueTuple`3{System.Boolean,System.Exception,IoT.DriverCore.ModbusRx.Device.IModbusSerialMaster}},System.Byte,System.UInt16,System.UInt16,System.Double)`

```csharp
public static ReactiveUI.Primitives.Async.IObservableAsync<System.ValueTuple<bool[], System.Exception>> ReadCoils(ReactiveUI.Primitives.Async.IObservableAsync<System.ValueTuple<bool, System.Exception, IoT.DriverCore.ModbusRx.Device.IModbusSerialMaster>> source, byte slaveAddress, ushort startAddress, ushort numberOfPoints, double interval)
```
Executes the `ReadCoils` operation.

- Parameter `source`: The `source` value.
- Parameter `slaveAddress`: The `slaveAddress` value.
- Parameter `startAddress`: The `startAddress` value.
- Parameter `numberOfPoints`: The `numberOfPoints` value.
- Parameter `interval`: The `interval` value.
- Returns: A `ReactiveUI.Primitives.Async.IObservableAsync<System.ValueTuple<bool[], System.Exception>>` result.

###### `M:IoT.DriverCore.ModbusRx.ModbusAsyncObservableExtensions.ReadCoils(ReactiveUI.Primitives.Async.IObservableAsync`1{System.ValueTuple`3{System.Boolean,System.Exception,IoT.DriverCore.ModbusRx.Device.ModbusIpMaster}},System.UInt16,System.UInt16,System.Double)`

```csharp
public static ReactiveUI.Primitives.Async.IObservableAsync<System.ValueTuple<bool[], System.Exception>> ReadCoils(ReactiveUI.Primitives.Async.IObservableAsync<System.ValueTuple<bool, System.Exception, IoT.DriverCore.ModbusRx.Device.ModbusIpMaster>> source, ushort startAddress, ushort numberOfPoints, double interval)
```
Executes the `ReadCoils` operation.

- Parameter `source`: The `source` value.
- Parameter `startAddress`: The `startAddress` value.
- Parameter `numberOfPoints`: The `numberOfPoints` value.
- Parameter `interval`: The `interval` value.
- Returns: A `ReactiveUI.Primitives.Async.IObservableAsync<System.ValueTuple<bool[], System.Exception>>` result.

###### `M:IoT.DriverCore.ModbusRx.ModbusAsyncObservableExtensions.ReadCoilsObservable(System.IObservable`1{System.ValueTuple`3{System.Boolean,System.Exception,IoT.DriverCore.ModbusRx.Device.IModbusSerialMaster}},System.Byte,System.UInt16,System.UInt16,System.Double)`

```csharp
public static ReactiveUI.Primitives.Async.IObservableAsync<System.ValueTuple<bool[], System.Exception>> ReadCoilsObservable(System.IObservable<System.ValueTuple<bool, System.Exception, IoT.DriverCore.ModbusRx.Device.IModbusSerialMaster>> source, byte slaveAddress, ushort startAddress, ushort numberOfPoints, double interval)
```
Executes the `ReadCoilsObservable` operation.

- Parameter `source`: The `source` value.
- Parameter `slaveAddress`: The `slaveAddress` value.
- Parameter `startAddress`: The `startAddress` value.
- Parameter `numberOfPoints`: The `numberOfPoints` value.
- Parameter `interval`: The `interval` value.
- Returns: A `ReactiveUI.Primitives.Async.IObservableAsync<System.ValueTuple<bool[], System.Exception>>` result.

###### `M:IoT.DriverCore.ModbusRx.ModbusAsyncObservableExtensions.ReadCoilsObservable(System.IObservable`1{System.ValueTuple`3{System.Boolean,System.Exception,IoT.DriverCore.ModbusRx.Device.ModbusIpMaster}},System.UInt16,System.UInt16,System.Double)`

```csharp
public static ReactiveUI.Primitives.Async.IObservableAsync<System.ValueTuple<bool[], System.Exception>> ReadCoilsObservable(System.IObservable<System.ValueTuple<bool, System.Exception, IoT.DriverCore.ModbusRx.Device.ModbusIpMaster>> source, ushort startAddress, ushort numberOfPoints, double interval)
```
Executes the `ReadCoilsObservable` operation.

- Parameter `source`: The `source` value.
- Parameter `startAddress`: The `startAddress` value.
- Parameter `numberOfPoints`: The `numberOfPoints` value.
- Parameter `interval`: The `interval` value.
- Returns: A `ReactiveUI.Primitives.Async.IObservableAsync<System.ValueTuple<bool[], System.Exception>>` result.

###### `M:IoT.DriverCore.ModbusRx.ModbusAsyncObservableExtensions.ReadHoldingRegisters(ReactiveUI.Primitives.Async.IObservableAsync`1{System.ValueTuple`3{System.Boolean,System.Exception,IoT.DriverCore.ModbusRx.Device.IModbusSerialMaster}},System.Byte,System.UInt16,System.UInt16,System.Double)`

```csharp
public static ReactiveUI.Primitives.Async.IObservableAsync<System.ValueTuple<ushort[], System.Exception>> ReadHoldingRegisters(ReactiveUI.Primitives.Async.IObservableAsync<System.ValueTuple<bool, System.Exception, IoT.DriverCore.ModbusRx.Device.IModbusSerialMaster>> source, byte slaveAddress, ushort startAddress, ushort numberOfPoints, double interval)
```
Executes the `ReadHoldingRegisters` operation.

- Parameter `source`: The `source` value.
- Parameter `slaveAddress`: The `slaveAddress` value.
- Parameter `startAddress`: The `startAddress` value.
- Parameter `numberOfPoints`: The `numberOfPoints` value.
- Parameter `interval`: The `interval` value.
- Returns: A `ReactiveUI.Primitives.Async.IObservableAsync<System.ValueTuple<ushort[], System.Exception>>` result.

###### `M:IoT.DriverCore.ModbusRx.ModbusAsyncObservableExtensions.ReadHoldingRegisters(ReactiveUI.Primitives.Async.IObservableAsync`1{System.ValueTuple`3{System.Boolean,System.Exception,IoT.DriverCore.ModbusRx.Device.ModbusIpMaster}},System.UInt16,System.UInt16,System.Double)`

```csharp
public static ReactiveUI.Primitives.Async.IObservableAsync<System.ValueTuple<ushort[], System.Exception>> ReadHoldingRegisters(ReactiveUI.Primitives.Async.IObservableAsync<System.ValueTuple<bool, System.Exception, IoT.DriverCore.ModbusRx.Device.ModbusIpMaster>> source, ushort startAddress, ushort numberOfPoints, double interval)
```
Executes the `ReadHoldingRegisters` operation.

- Parameter `source`: The `source` value.
- Parameter `startAddress`: The `startAddress` value.
- Parameter `numberOfPoints`: The `numberOfPoints` value.
- Parameter `interval`: The `interval` value.
- Returns: A `ReactiveUI.Primitives.Async.IObservableAsync<System.ValueTuple<ushort[], System.Exception>>` result.

###### `M:IoT.DriverCore.ModbusRx.ModbusAsyncObservableExtensions.ReadHoldingRegistersObservable(System.IObservable`1{System.ValueTuple`3{System.Boolean,System.Exception,IoT.DriverCore.ModbusRx.Device.IModbusSerialMaster}},System.Byte,System.UInt16,System.UInt16,System.Double)`

```csharp
public static ReactiveUI.Primitives.Async.IObservableAsync<System.ValueTuple<ushort[], System.Exception>> ReadHoldingRegistersObservable(System.IObservable<System.ValueTuple<bool, System.Exception, IoT.DriverCore.ModbusRx.Device.IModbusSerialMaster>> source, byte slaveAddress, ushort startAddress, ushort numberOfPoints, double interval)
```
Executes the `ReadHoldingRegistersObservable` operation.

- Parameter `source`: The `source` value.
- Parameter `slaveAddress`: The `slaveAddress` value.
- Parameter `startAddress`: The `startAddress` value.
- Parameter `numberOfPoints`: The `numberOfPoints` value.
- Parameter `interval`: The `interval` value.
- Returns: A `ReactiveUI.Primitives.Async.IObservableAsync<System.ValueTuple<ushort[], System.Exception>>` result.

###### `M:IoT.DriverCore.ModbusRx.ModbusAsyncObservableExtensions.ReadHoldingRegistersObservable(System.IObservable`1{System.ValueTuple`3{System.Boolean,System.Exception,IoT.DriverCore.ModbusRx.Device.ModbusIpMaster}},System.UInt16,System.UInt16,System.Double)`

```csharp
public static ReactiveUI.Primitives.Async.IObservableAsync<System.ValueTuple<ushort[], System.Exception>> ReadHoldingRegistersObservable(System.IObservable<System.ValueTuple<bool, System.Exception, IoT.DriverCore.ModbusRx.Device.ModbusIpMaster>> source, ushort startAddress, ushort numberOfPoints, double interval)
```
Executes the `ReadHoldingRegistersObservable` operation.

- Parameter `source`: The `source` value.
- Parameter `startAddress`: The `startAddress` value.
- Parameter `numberOfPoints`: The `numberOfPoints` value.
- Parameter `interval`: The `interval` value.
- Returns: A `ReactiveUI.Primitives.Async.IObservableAsync<System.ValueTuple<ushort[], System.Exception>>` result.

###### `M:IoT.DriverCore.ModbusRx.ModbusAsyncObservableExtensions.ReadInputRegisters(ReactiveUI.Primitives.Async.IObservableAsync`1{System.ValueTuple`3{System.Boolean,System.Exception,IoT.DriverCore.ModbusRx.Device.IModbusSerialMaster}},System.Byte,System.UInt16,System.UInt16,System.Double)`

```csharp
public static ReactiveUI.Primitives.Async.IObservableAsync<System.ValueTuple<ushort[], System.Exception>> ReadInputRegisters(ReactiveUI.Primitives.Async.IObservableAsync<System.ValueTuple<bool, System.Exception, IoT.DriverCore.ModbusRx.Device.IModbusSerialMaster>> source, byte slaveAddress, ushort startAddress, ushort numberOfPoints, double interval)
```
Executes the `ReadInputRegisters` operation.

- Parameter `source`: The `source` value.
- Parameter `slaveAddress`: The `slaveAddress` value.
- Parameter `startAddress`: The `startAddress` value.
- Parameter `numberOfPoints`: The `numberOfPoints` value.
- Parameter `interval`: The `interval` value.
- Returns: A `ReactiveUI.Primitives.Async.IObservableAsync<System.ValueTuple<ushort[], System.Exception>>` result.

###### `M:IoT.DriverCore.ModbusRx.ModbusAsyncObservableExtensions.ReadInputRegisters(ReactiveUI.Primitives.Async.IObservableAsync`1{System.ValueTuple`3{System.Boolean,System.Exception,IoT.DriverCore.ModbusRx.Device.ModbusIpMaster}},System.UInt16,System.UInt16,System.Double)`

```csharp
public static ReactiveUI.Primitives.Async.IObservableAsync<System.ValueTuple<ushort[], System.Exception>> ReadInputRegisters(ReactiveUI.Primitives.Async.IObservableAsync<System.ValueTuple<bool, System.Exception, IoT.DriverCore.ModbusRx.Device.ModbusIpMaster>> source, ushort startAddress, ushort numberOfPoints, double interval)
```
Executes the `ReadInputRegisters` operation.

- Parameter `source`: The `source` value.
- Parameter `startAddress`: The `startAddress` value.
- Parameter `numberOfPoints`: The `numberOfPoints` value.
- Parameter `interval`: The `interval` value.
- Returns: A `ReactiveUI.Primitives.Async.IObservableAsync<System.ValueTuple<ushort[], System.Exception>>` result.

###### `M:IoT.DriverCore.ModbusRx.ModbusAsyncObservableExtensions.ReadInputRegistersObservable(System.IObservable`1{System.ValueTuple`3{System.Boolean,System.Exception,IoT.DriverCore.ModbusRx.Device.IModbusSerialMaster}},System.Byte,System.UInt16,System.UInt16,System.Double)`

```csharp
public static ReactiveUI.Primitives.Async.IObservableAsync<System.ValueTuple<ushort[], System.Exception>> ReadInputRegistersObservable(System.IObservable<System.ValueTuple<bool, System.Exception, IoT.DriverCore.ModbusRx.Device.IModbusSerialMaster>> source, byte slaveAddress, ushort startAddress, ushort numberOfPoints, double interval)
```
Executes the `ReadInputRegistersObservable` operation.

- Parameter `source`: The `source` value.
- Parameter `slaveAddress`: The `slaveAddress` value.
- Parameter `startAddress`: The `startAddress` value.
- Parameter `numberOfPoints`: The `numberOfPoints` value.
- Parameter `interval`: The `interval` value.
- Returns: A `ReactiveUI.Primitives.Async.IObservableAsync<System.ValueTuple<ushort[], System.Exception>>` result.

###### `M:IoT.DriverCore.ModbusRx.ModbusAsyncObservableExtensions.ReadInputRegistersObservable(System.IObservable`1{System.ValueTuple`3{System.Boolean,System.Exception,IoT.DriverCore.ModbusRx.Device.ModbusIpMaster}},System.UInt16,System.UInt16,System.Double)`

```csharp
public static ReactiveUI.Primitives.Async.IObservableAsync<System.ValueTuple<ushort[], System.Exception>> ReadInputRegistersObservable(System.IObservable<System.ValueTuple<bool, System.Exception, IoT.DriverCore.ModbusRx.Device.ModbusIpMaster>> source, ushort startAddress, ushort numberOfPoints, double interval)
```
Executes the `ReadInputRegistersObservable` operation.

- Parameter `source`: The `source` value.
- Parameter `startAddress`: The `startAddress` value.
- Parameter `numberOfPoints`: The `numberOfPoints` value.
- Parameter `interval`: The `interval` value.
- Returns: A `ReactiveUI.Primitives.Async.IObservableAsync<System.ValueTuple<ushort[], System.Exception>>` result.

###### `M:IoT.DriverCore.ModbusRx.ModbusAsyncObservableExtensions.ReadInputs(ReactiveUI.Primitives.Async.IObservableAsync`1{System.ValueTuple`3{System.Boolean,System.Exception,IoT.DriverCore.ModbusRx.Device.IModbusSerialMaster}},System.Byte,System.UInt16,System.UInt16,System.Double)`

```csharp
public static ReactiveUI.Primitives.Async.IObservableAsync<System.ValueTuple<bool[], System.Exception>> ReadInputs(ReactiveUI.Primitives.Async.IObservableAsync<System.ValueTuple<bool, System.Exception, IoT.DriverCore.ModbusRx.Device.IModbusSerialMaster>> source, byte slaveAddress, ushort startAddress, ushort numberOfPoints, double interval)
```
Executes the `ReadInputs` operation.

- Parameter `source`: The `source` value.
- Parameter `slaveAddress`: The `slaveAddress` value.
- Parameter `startAddress`: The `startAddress` value.
- Parameter `numberOfPoints`: The `numberOfPoints` value.
- Parameter `interval`: The `interval` value.
- Returns: A `ReactiveUI.Primitives.Async.IObservableAsync<System.ValueTuple<bool[], System.Exception>>` result.

###### `M:IoT.DriverCore.ModbusRx.ModbusAsyncObservableExtensions.ReadInputs(ReactiveUI.Primitives.Async.IObservableAsync`1{System.ValueTuple`3{System.Boolean,System.Exception,IoT.DriverCore.ModbusRx.Device.ModbusIpMaster}},System.UInt16,System.UInt16,System.Double)`

```csharp
public static ReactiveUI.Primitives.Async.IObservableAsync<System.ValueTuple<bool[], System.Exception>> ReadInputs(ReactiveUI.Primitives.Async.IObservableAsync<System.ValueTuple<bool, System.Exception, IoT.DriverCore.ModbusRx.Device.ModbusIpMaster>> source, ushort startAddress, ushort numberOfPoints, double interval)
```
Executes the `ReadInputs` operation.

- Parameter `source`: The `source` value.
- Parameter `startAddress`: The `startAddress` value.
- Parameter `numberOfPoints`: The `numberOfPoints` value.
- Parameter `interval`: The `interval` value.
- Returns: A `ReactiveUI.Primitives.Async.IObservableAsync<System.ValueTuple<bool[], System.Exception>>` result.

###### `M:IoT.DriverCore.ModbusRx.ModbusAsyncObservableExtensions.ReadInputsObservable(System.IObservable`1{System.ValueTuple`3{System.Boolean,System.Exception,IoT.DriverCore.ModbusRx.Device.IModbusSerialMaster}},System.Byte,System.UInt16,System.UInt16,System.Double)`

```csharp
public static ReactiveUI.Primitives.Async.IObservableAsync<System.ValueTuple<bool[], System.Exception>> ReadInputsObservable(System.IObservable<System.ValueTuple<bool, System.Exception, IoT.DriverCore.ModbusRx.Device.IModbusSerialMaster>> source, byte slaveAddress, ushort startAddress, ushort numberOfPoints, double interval)
```
Executes the `ReadInputsObservable` operation.

- Parameter `source`: The `source` value.
- Parameter `slaveAddress`: The `slaveAddress` value.
- Parameter `startAddress`: The `startAddress` value.
- Parameter `numberOfPoints`: The `numberOfPoints` value.
- Parameter `interval`: The `interval` value.
- Returns: A `ReactiveUI.Primitives.Async.IObservableAsync<System.ValueTuple<bool[], System.Exception>>` result.

###### `M:IoT.DriverCore.ModbusRx.ModbusAsyncObservableExtensions.ReadInputsObservable(System.IObservable`1{System.ValueTuple`3{System.Boolean,System.Exception,IoT.DriverCore.ModbusRx.Device.ModbusIpMaster}},System.UInt16,System.UInt16,System.Double)`

```csharp
public static ReactiveUI.Primitives.Async.IObservableAsync<System.ValueTuple<bool[], System.Exception>> ReadInputsObservable(System.IObservable<System.ValueTuple<bool, System.Exception, IoT.DriverCore.ModbusRx.Device.ModbusIpMaster>> source, ushort startAddress, ushort numberOfPoints, double interval)
```
Executes the `ReadInputsObservable` operation.

- Parameter `source`: The `source` value.
- Parameter `startAddress`: The `startAddress` value.
- Parameter `numberOfPoints`: The `numberOfPoints` value.
- Parameter `interval`: The `interval` value.
- Returns: A `ReactiveUI.Primitives.Async.IObservableAsync<System.ValueTuple<bool[], System.Exception>>` result.

###### `M:IoT.DriverCore.ModbusRx.ModbusAsyncObservableExtensions.ToAsyncObservable``1(System.IObservable`1{``0})`

```csharp
public static ReactiveUI.Primitives.Async.IObservableAsync<T> ToAsyncObservable<T>(System.IObservable<T> source)
```
Executes the `ToAsyncObservable` operation.

- Parameter `source`: The `source` value.
- Returns: A `ReactiveUI.Primitives.Async.IObservableAsync<T>` result.

###### `M:IoT.DriverCore.ModbusRx.ModbusAsyncObservableExtensions.ToModbusObservable(System.IObservable`1{System.ValueTuple`3{System.Boolean,System.Exception,IoT.DriverCore.ModbusRx.Device.IModbusSerialMaster}})`

```csharp
public static ReactiveUI.Primitives.Async.IObservableAsync<System.ValueTuple<bool, System.Exception, IoT.DriverCore.ModbusRx.Device.IModbusSerialMaster>> ToModbusObservable(System.IObservable<System.ValueTuple<bool, System.Exception, IoT.DriverCore.ModbusRx.Device.IModbusSerialMaster>> source)
```
Executes the `ToModbusObservable` operation.

- Parameter `source`: The `source` value.
- Returns: A `ReactiveUI.Primitives.Async.IObservableAsync<System.ValueTuple<bool, System.Exception, IoT.DriverCore.ModbusRx.Device.IModbusSerialMaster>>` result.

###### `M:IoT.DriverCore.ModbusRx.ModbusAsyncObservableExtensions.ToModbusObservable(System.IObservable`1{System.ValueTuple`3{System.Boolean,System.Exception,IoT.DriverCore.ModbusRx.Device.ModbusIpMaster}})`

```csharp
public static ReactiveUI.Primitives.Async.IObservableAsync<System.ValueTuple<bool, System.Exception, IoT.DriverCore.ModbusRx.Device.ModbusIpMaster>> ToModbusObservable(System.IObservable<System.ValueTuple<bool, System.Exception, IoT.DriverCore.ModbusRx.Device.ModbusIpMaster>> source)
```
Executes the `ToModbusObservable` operation.

- Parameter `source`: The `source` value.
- Returns: A `ReactiveUI.Primitives.Async.IObservableAsync<System.ValueTuple<bool, System.Exception, IoT.DriverCore.ModbusRx.Device.ModbusIpMaster>>` result.

###### `M:IoT.DriverCore.ModbusRx.ModbusAsyncObservableExtensions.ToObservable``1(ReactiveUI.Primitives.Async.IObservableAsync`1{``0})`

```csharp
public static System.IObservable<T> ToObservable<T>(ReactiveUI.Primitives.Async.IObservableAsync<T> source)
```
Executes the `ToObservable` operation.

- Parameter `source`: The `source` value.
- Returns: A `System.IObservable<T>` result.

###### `M:IoT.DriverCore.ModbusRx.ModbusAsyncObservableExtensions.WriteCoilDiscretes(ReactiveUI.Primitives.Async.IObservableAsync`1{IoT.DriverCore.ModbusRx.Device.ModbusSerialSlave},System.UInt16,ReactiveUI.Primitives.Async.IObservableAsync`1{System.Boolean[]})`

```csharp
public static ReactiveUI.Primitives.Async.IObservableAsync<IoT.DriverCore.ModbusRx.Device.ModbusSerialSlave> WriteCoilDiscretes(ReactiveUI.Primitives.Async.IObservableAsync<IoT.DriverCore.ModbusRx.Device.ModbusSerialSlave> slave, ushort startAddress, ReactiveUI.Primitives.Async.IObservableAsync<bool[]> valuesToWrite)
```
Executes the `WriteCoilDiscretes` operation.

- Parameter `slave`: The `slave` value.
- Parameter `startAddress`: The `startAddress` value.
- Parameter `valuesToWrite`: The `valuesToWrite` value.
- Returns: A `ReactiveUI.Primitives.Async.IObservableAsync<IoT.DriverCore.ModbusRx.Device.ModbusSerialSlave>` result.

###### `M:IoT.DriverCore.ModbusRx.ModbusAsyncObservableExtensions.WriteCoilDiscretes(ReactiveUI.Primitives.Async.IObservableAsync`1{IoT.DriverCore.ModbusRx.Device.ModbusTcpSlave},System.UInt16,ReactiveUI.Primitives.Async.IObservableAsync`1{System.Boolean[]})`

```csharp
public static ReactiveUI.Primitives.Async.IObservableAsync<IoT.DriverCore.ModbusRx.Device.ModbusTcpSlave> WriteCoilDiscretes(ReactiveUI.Primitives.Async.IObservableAsync<IoT.DriverCore.ModbusRx.Device.ModbusTcpSlave> slave, ushort startAddress, ReactiveUI.Primitives.Async.IObservableAsync<bool[]> valuesToWrite)
```
Executes the `WriteCoilDiscretes` operation.

- Parameter `slave`: The `slave` value.
- Parameter `startAddress`: The `startAddress` value.
- Parameter `valuesToWrite`: The `valuesToWrite` value.
- Returns: A `ReactiveUI.Primitives.Async.IObservableAsync<IoT.DriverCore.ModbusRx.Device.ModbusTcpSlave>` result.

###### `M:IoT.DriverCore.ModbusRx.ModbusAsyncObservableExtensions.WriteCoilDiscretes(ReactiveUI.Primitives.Async.IObservableAsync`1{IoT.DriverCore.ModbusRx.Device.ModbusUdpSlave},System.UInt16,ReactiveUI.Primitives.Async.IObservableAsync`1{System.Boolean[]})`

```csharp
public static ReactiveUI.Primitives.Async.IObservableAsync<IoT.DriverCore.ModbusRx.Device.ModbusUdpSlave> WriteCoilDiscretes(ReactiveUI.Primitives.Async.IObservableAsync<IoT.DriverCore.ModbusRx.Device.ModbusUdpSlave> slave, ushort startAddress, ReactiveUI.Primitives.Async.IObservableAsync<bool[]> valuesToWrite)
```
Executes the `WriteCoilDiscretes` operation.

- Parameter `slave`: The `slave` value.
- Parameter `startAddress`: The `startAddress` value.
- Parameter `valuesToWrite`: The `valuesToWrite` value.
- Returns: A `ReactiveUI.Primitives.Async.IObservableAsync<IoT.DriverCore.ModbusRx.Device.ModbusUdpSlave>` result.

###### `M:IoT.DriverCore.ModbusRx.ModbusAsyncObservableExtensions.WriteHoldingRegisters(ReactiveUI.Primitives.Async.IObservableAsync`1{IoT.DriverCore.ModbusRx.Device.ModbusSerialSlave},System.UInt16,ReactiveUI.Primitives.Async.IObservableAsync`1{System.UInt16[]})`

```csharp
public static ReactiveUI.Primitives.Async.IObservableAsync<IoT.DriverCore.ModbusRx.Device.ModbusSerialSlave> WriteHoldingRegisters(ReactiveUI.Primitives.Async.IObservableAsync<IoT.DriverCore.ModbusRx.Device.ModbusSerialSlave> slave, ushort startAddress, ReactiveUI.Primitives.Async.IObservableAsync<ushort[]> valuesToWrite)
```
Executes the `WriteHoldingRegisters` operation.

- Parameter `slave`: The `slave` value.
- Parameter `startAddress`: The `startAddress` value.
- Parameter `valuesToWrite`: The `valuesToWrite` value.
- Returns: A `ReactiveUI.Primitives.Async.IObservableAsync<IoT.DriverCore.ModbusRx.Device.ModbusSerialSlave>` result.

###### `M:IoT.DriverCore.ModbusRx.ModbusAsyncObservableExtensions.WriteHoldingRegisters(ReactiveUI.Primitives.Async.IObservableAsync`1{IoT.DriverCore.ModbusRx.Device.ModbusTcpSlave},System.UInt16,ReactiveUI.Primitives.Async.IObservableAsync`1{System.UInt16[]})`

```csharp
public static ReactiveUI.Primitives.Async.IObservableAsync<IoT.DriverCore.ModbusRx.Device.ModbusTcpSlave> WriteHoldingRegisters(ReactiveUI.Primitives.Async.IObservableAsync<IoT.DriverCore.ModbusRx.Device.ModbusTcpSlave> slave, ushort startAddress, ReactiveUI.Primitives.Async.IObservableAsync<ushort[]> valuesToWrite)
```
Executes the `WriteHoldingRegisters` operation.

- Parameter `slave`: The `slave` value.
- Parameter `startAddress`: The `startAddress` value.
- Parameter `valuesToWrite`: The `valuesToWrite` value.
- Returns: A `ReactiveUI.Primitives.Async.IObservableAsync<IoT.DriverCore.ModbusRx.Device.ModbusTcpSlave>` result.

###### `M:IoT.DriverCore.ModbusRx.ModbusAsyncObservableExtensions.WriteHoldingRegisters(ReactiveUI.Primitives.Async.IObservableAsync`1{IoT.DriverCore.ModbusRx.Device.ModbusUdpSlave},System.UInt16,ReactiveUI.Primitives.Async.IObservableAsync`1{System.UInt16[]})`

```csharp
public static ReactiveUI.Primitives.Async.IObservableAsync<IoT.DriverCore.ModbusRx.Device.ModbusUdpSlave> WriteHoldingRegisters(ReactiveUI.Primitives.Async.IObservableAsync<IoT.DriverCore.ModbusRx.Device.ModbusUdpSlave> slave, ushort startAddress, ReactiveUI.Primitives.Async.IObservableAsync<ushort[]> valuesToWrite)
```
Executes the `WriteHoldingRegisters` operation.

- Parameter `slave`: The `slave` value.
- Parameter `startAddress`: The `startAddress` value.
- Parameter `valuesToWrite`: The `valuesToWrite` value.
- Returns: A `ReactiveUI.Primitives.Async.IObservableAsync<IoT.DriverCore.ModbusRx.Device.ModbusUdpSlave>` result.

###### `M:IoT.DriverCore.ModbusRx.ModbusAsyncObservableExtensions.WriteInputDiscretes(ReactiveUI.Primitives.Async.IObservableAsync`1{IoT.DriverCore.ModbusRx.Device.ModbusSerialSlave},System.UInt16,ReactiveUI.Primitives.Async.IObservableAsync`1{System.Boolean[]})`

```csharp
public static ReactiveUI.Primitives.Async.IObservableAsync<IoT.DriverCore.ModbusRx.Device.ModbusSerialSlave> WriteInputDiscretes(ReactiveUI.Primitives.Async.IObservableAsync<IoT.DriverCore.ModbusRx.Device.ModbusSerialSlave> slave, ushort startAddress, ReactiveUI.Primitives.Async.IObservableAsync<bool[]> valuesToWrite)
```
Executes the `WriteInputDiscretes` operation.

- Parameter `slave`: The `slave` value.
- Parameter `startAddress`: The `startAddress` value.
- Parameter `valuesToWrite`: The `valuesToWrite` value.
- Returns: A `ReactiveUI.Primitives.Async.IObservableAsync<IoT.DriverCore.ModbusRx.Device.ModbusSerialSlave>` result.

###### `M:IoT.DriverCore.ModbusRx.ModbusAsyncObservableExtensions.WriteInputDiscretes(ReactiveUI.Primitives.Async.IObservableAsync`1{IoT.DriverCore.ModbusRx.Device.ModbusTcpSlave},System.UInt16,ReactiveUI.Primitives.Async.IObservableAsync`1{System.Boolean[]})`

```csharp
public static ReactiveUI.Primitives.Async.IObservableAsync<IoT.DriverCore.ModbusRx.Device.ModbusTcpSlave> WriteInputDiscretes(ReactiveUI.Primitives.Async.IObservableAsync<IoT.DriverCore.ModbusRx.Device.ModbusTcpSlave> slave, ushort startAddress, ReactiveUI.Primitives.Async.IObservableAsync<bool[]> valuesToWrite)
```
Executes the `WriteInputDiscretes` operation.

- Parameter `slave`: The `slave` value.
- Parameter `startAddress`: The `startAddress` value.
- Parameter `valuesToWrite`: The `valuesToWrite` value.
- Returns: A `ReactiveUI.Primitives.Async.IObservableAsync<IoT.DriverCore.ModbusRx.Device.ModbusTcpSlave>` result.

###### `M:IoT.DriverCore.ModbusRx.ModbusAsyncObservableExtensions.WriteInputDiscretes(ReactiveUI.Primitives.Async.IObservableAsync`1{IoT.DriverCore.ModbusRx.Device.ModbusUdpSlave},System.UInt16,ReactiveUI.Primitives.Async.IObservableAsync`1{System.Boolean[]})`

```csharp
public static ReactiveUI.Primitives.Async.IObservableAsync<IoT.DriverCore.ModbusRx.Device.ModbusUdpSlave> WriteInputDiscretes(ReactiveUI.Primitives.Async.IObservableAsync<IoT.DriverCore.ModbusRx.Device.ModbusUdpSlave> slave, ushort startAddress, ReactiveUI.Primitives.Async.IObservableAsync<bool[]> valuesToWrite)
```
Executes the `WriteInputDiscretes` operation.

- Parameter `slave`: The `slave` value.
- Parameter `startAddress`: The `startAddress` value.
- Parameter `valuesToWrite`: The `valuesToWrite` value.
- Returns: A `ReactiveUI.Primitives.Async.IObservableAsync<IoT.DriverCore.ModbusRx.Device.ModbusUdpSlave>` result.

###### `M:IoT.DriverCore.ModbusRx.ModbusAsyncObservableExtensions.WriteInputRegisters(ReactiveUI.Primitives.Async.IObservableAsync`1{IoT.DriverCore.ModbusRx.Device.ModbusSerialSlave},System.UInt16,ReactiveUI.Primitives.Async.IObservableAsync`1{System.UInt16[]})`

```csharp
public static ReactiveUI.Primitives.Async.IObservableAsync<IoT.DriverCore.ModbusRx.Device.ModbusSerialSlave> WriteInputRegisters(ReactiveUI.Primitives.Async.IObservableAsync<IoT.DriverCore.ModbusRx.Device.ModbusSerialSlave> slave, ushort startAddress, ReactiveUI.Primitives.Async.IObservableAsync<ushort[]> valuesToWrite)
```
Executes the `WriteInputRegisters` operation.

- Parameter `slave`: The `slave` value.
- Parameter `startAddress`: The `startAddress` value.
- Parameter `valuesToWrite`: The `valuesToWrite` value.
- Returns: A `ReactiveUI.Primitives.Async.IObservableAsync<IoT.DriverCore.ModbusRx.Device.ModbusSerialSlave>` result.

###### `M:IoT.DriverCore.ModbusRx.ModbusAsyncObservableExtensions.WriteInputRegisters(ReactiveUI.Primitives.Async.IObservableAsync`1{IoT.DriverCore.ModbusRx.Device.ModbusTcpSlave},System.UInt16,ReactiveUI.Primitives.Async.IObservableAsync`1{System.UInt16[]})`

```csharp
public static ReactiveUI.Primitives.Async.IObservableAsync<IoT.DriverCore.ModbusRx.Device.ModbusTcpSlave> WriteInputRegisters(ReactiveUI.Primitives.Async.IObservableAsync<IoT.DriverCore.ModbusRx.Device.ModbusTcpSlave> slave, ushort startAddress, ReactiveUI.Primitives.Async.IObservableAsync<ushort[]> valuesToWrite)
```
Executes the `WriteInputRegisters` operation.

- Parameter `slave`: The `slave` value.
- Parameter `startAddress`: The `startAddress` value.
- Parameter `valuesToWrite`: The `valuesToWrite` value.
- Returns: A `ReactiveUI.Primitives.Async.IObservableAsync<IoT.DriverCore.ModbusRx.Device.ModbusTcpSlave>` result.

###### `M:IoT.DriverCore.ModbusRx.ModbusAsyncObservableExtensions.WriteInputRegisters(ReactiveUI.Primitives.Async.IObservableAsync`1{IoT.DriverCore.ModbusRx.Device.ModbusUdpSlave},System.UInt16,ReactiveUI.Primitives.Async.IObservableAsync`1{System.UInt16[]})`

```csharp
public static ReactiveUI.Primitives.Async.IObservableAsync<IoT.DriverCore.ModbusRx.Device.ModbusUdpSlave> WriteInputRegisters(ReactiveUI.Primitives.Async.IObservableAsync<IoT.DriverCore.ModbusRx.Device.ModbusUdpSlave> slave, ushort startAddress, ReactiveUI.Primitives.Async.IObservableAsync<ushort[]> valuesToWrite)
```
Executes the `WriteInputRegisters` operation.

- Parameter `slave`: The `slave` value.
- Parameter `startAddress`: The `startAddress` value.
- Parameter `valuesToWrite`: The `valuesToWrite` value.
- Returns: A `ReactiveUI.Primitives.Async.IObservableAsync<IoT.DriverCore.ModbusRx.Device.ModbusUdpSlave>` result.

#### `T:IoT.DriverCore.ModbusRx.ModbusCommunicationException`

```csharp
public class IoT.DriverCore.ModbusRx.ModbusCommunicationException
```
Modbus Communication Exception.

##### Declared public members

###### `M:IoT.DriverCore.ModbusRx.ModbusCommunicationException.#ctor`

```csharp
public IoT.DriverCore.ModbusRx.ModbusCommunicationException()
```
Initializes a new instance of the `T:IoT.DriverCore.ModbusRx.ModbusCommunicationException` class.

###### `M:IoT.DriverCore.ModbusRx.ModbusCommunicationException.#ctor(System.String)`

```csharp
public IoT.DriverCore.ModbusRx.ModbusCommunicationException(string message)
```
Initializes a new instance of the `T:IoT.DriverCore.ModbusRx.ModbusCommunicationException` class.

- Parameter `message`: The message that describes the error.

###### `M:IoT.DriverCore.ModbusRx.ModbusCommunicationException.#ctor(System.String,System.Exception)`

```csharp
public IoT.DriverCore.ModbusRx.ModbusCommunicationException(string message, System.Exception inner)
```
Initializes a new instance of the `T:IoT.DriverCore.ModbusRx.ModbusCommunicationException` class.

- Parameter `message`: The message.
- Parameter `inner`: The inner.

#### `T:IoT.DriverCore.ModbusRx.ModbusObservationMetrics`

```csharp
public class IoT.DriverCore.ModbusRx.ModbusObservationMetrics
```
Provides deterministic work counters for event-driven Modbus server observation.

##### Declared public members

###### `M:IoT.DriverCore.ModbusRx.ModbusObservationMetrics.#ctor`

```csharp
public IoT.DriverCore.ModbusRx.ModbusObservationMetrics()
```
Initializes a new instance of `IoT.DriverCore.ModbusRx.ModbusObservationMetrics`.

###### `P:IoT.DriverCore.ModbusRx.ModbusObservationMetrics.SnapshotsCreated`

```csharp
public long SnapshotsCreated { get; }
```
Gets the number of snapshots constructed from accepted notifications.

- Value: The `SnapshotsCreated` value.

###### `P:IoT.DriverCore.ModbusRx.ModbusObservationMetrics.SnapshotsEmitted`

```csharp
public long SnapshotsEmitted { get; }
```
Gets the number of snapshots emitted to observers.

- Value: The `SnapshotsEmitted` value.

###### `P:IoT.DriverCore.ModbusRx.ModbusObservationMetrics.WriteNotifications`

```csharp
public long WriteNotifications { get; }
```
Gets the number of accepted data-store write notifications.

- Value: The `WriteNotifications` value.

#### `T:IoT.DriverCore.ModbusRx.ModbusSerialSlaveExtensions`

```csharp
public class IoT.DriverCore.ModbusRx.ModbusSerialSlaveExtensions
```
Writes observable values to serial slave data stores.

##### Declared public members

###### `M:IoT.DriverCore.ModbusRx.ModbusSerialSlaveExtensions.#ctor`

```csharp
public IoT.DriverCore.ModbusRx.ModbusSerialSlaveExtensions()
```
Initializes a new instance of the `T:IoT.DriverCore.ModbusRx.ModbusSerialSlaveExtensions` class.

###### `M:IoT.DriverCore.ModbusRx.ModbusSerialSlaveExtensions.#ctor(System.Byte)`

```csharp
public IoT.DriverCore.ModbusRx.ModbusSerialSlaveExtensions(byte unitIdentifier)
```
Initializes a new instance of the `T:IoT.DriverCore.ModbusRx.ModbusSerialSlaveExtensions` class.

- Parameter `unitIdentifier`: The Modbus unit identifier used by write requests.

###### `M:IoT.DriverCore.ModbusRx.ModbusSerialSlaveExtensions.WriteCoilDiscretes(System.IObservable`1{IoT.DriverCore.ModbusRx.Device.ModbusSerialSlave},System.UInt16,System.IObservable`1{System.Boolean[]})`

```csharp
public System.IObservable<IoT.DriverCore.ModbusRx.Device.ModbusSerialSlave> WriteCoilDiscretes(System.IObservable<IoT.DriverCore.ModbusRx.Device.ModbusSerialSlave> slave, ushort startAddress, System.IObservable<bool[]> valuesToWrite)
```
Executes the `WriteCoilDiscretes` operation.

- Parameter `slave`: The `slave` value.
- Parameter `startAddress`: The `startAddress` value.
- Parameter `valuesToWrite`: The `valuesToWrite` value.
- Returns: A `System.IObservable<IoT.DriverCore.ModbusRx.Device.ModbusSerialSlave>` result.

###### `M:IoT.DriverCore.ModbusRx.ModbusSerialSlaveExtensions.WriteHoldingRegisters(System.IObservable`1{IoT.DriverCore.ModbusRx.Device.ModbusSerialSlave},System.UInt16,System.IObservable`1{System.UInt16[]})`

```csharp
public System.IObservable<IoT.DriverCore.ModbusRx.Device.ModbusSerialSlave> WriteHoldingRegisters(System.IObservable<IoT.DriverCore.ModbusRx.Device.ModbusSerialSlave> slave, ushort startAddress, System.IObservable<ushort[]> valuesToWrite)
```
Executes the `WriteHoldingRegisters` operation.

- Parameter `slave`: The `slave` value.
- Parameter `startAddress`: The `startAddress` value.
- Parameter `valuesToWrite`: The `valuesToWrite` value.
- Returns: A `System.IObservable<IoT.DriverCore.ModbusRx.Device.ModbusSerialSlave>` result.

###### `M:IoT.DriverCore.ModbusRx.ModbusSerialSlaveExtensions.WriteInputDiscretes(System.IObservable`1{IoT.DriverCore.ModbusRx.Device.ModbusSerialSlave},System.UInt16,System.IObservable`1{System.Boolean[]})`

```csharp
public System.IObservable<IoT.DriverCore.ModbusRx.Device.ModbusSerialSlave> WriteInputDiscretes(System.IObservable<IoT.DriverCore.ModbusRx.Device.ModbusSerialSlave> slave, ushort startAddress, System.IObservable<bool[]> valuesToWrite)
```
Executes the `WriteInputDiscretes` operation.

- Parameter `slave`: The `slave` value.
- Parameter `startAddress`: The `startAddress` value.
- Parameter `valuesToWrite`: The `valuesToWrite` value.
- Returns: A `System.IObservable<IoT.DriverCore.ModbusRx.Device.ModbusSerialSlave>` result.

###### `M:IoT.DriverCore.ModbusRx.ModbusSerialSlaveExtensions.WriteInputRegisters(System.IObservable`1{IoT.DriverCore.ModbusRx.Device.ModbusSerialSlave},System.UInt16,System.IObservable`1{System.UInt16[]})`

```csharp
public System.IObservable<IoT.DriverCore.ModbusRx.Device.ModbusSerialSlave> WriteInputRegisters(System.IObservable<IoT.DriverCore.ModbusRx.Device.ModbusSerialSlave> slave, ushort startAddress, System.IObservable<ushort[]> valuesToWrite)
```
Executes the `WriteInputRegisters` operation.

- Parameter `slave`: The `slave` value.
- Parameter `startAddress`: The `startAddress` value.
- Parameter `valuesToWrite`: The `valuesToWrite` value.
- Returns: A `System.IObservable<IoT.DriverCore.ModbusRx.Device.ModbusSerialSlave>` result.

#### `T:IoT.DriverCore.ModbusRx.ModbusServerDataSnapshot`

```csharp
public class IoT.DriverCore.ModbusRx.ModbusServerDataSnapshot
```
Represents a snapshot of Modbus server data at a point in time.

##### Declared public members

###### `M:IoT.DriverCore.ModbusRx.ModbusServerDataSnapshot.#ctor`

```csharp
public IoT.DriverCore.ModbusRx.ModbusServerDataSnapshot()
```
Initializes a new instance of the `T:IoT.DriverCore.ModbusRx.ModbusServerDataSnapshot` class.

###### `M:IoT.DriverCore.ModbusRx.ModbusServerDataSnapshot.#ctor(System.UInt16[],System.UInt16[],System.Boolean[],System.Boolean[],System.DateTimeOffset)`

```csharp
public IoT.DriverCore.ModbusRx.ModbusServerDataSnapshot(ushort[] holdingRegisters, ushort[] inputRegisters, bool[] coils, bool[] inputs, System.DateTimeOffset timestamp)
```
Initializes a new instance of the `T:IoT.DriverCore.ModbusRx.ModbusServerDataSnapshot` class.

- Parameter `holdingRegisters`: The holding-register values.
- Parameter `inputRegisters`: The input-register values.
- Parameter `coils`: The coil values.
- Parameter `inputs`: The input values.
- Parameter `timestamp`: The time at which the snapshot was captured.

###### `M:IoT.DriverCore.ModbusRx.ModbusServerDataSnapshot.Equals(IoT.DriverCore.ModbusRx.ModbusServerDataSnapshot)`

```csharp
public bool Equals(IoT.DriverCore.ModbusRx.ModbusServerDataSnapshot other)
```
Determines whether the specified snapshot is equal to the current snapshot.

- Parameter `other`: The snapshot to compare with the current snapshot.
- Returns: True if the specified snapshot is equal to the current snapshot; otherwise, false.

###### `M:IoT.DriverCore.ModbusRx.ModbusServerDataSnapshot.Equals(System.Object)`

```csharp
public bool Equals(object obj)
```
Determines whether the specified object is equal to the current snapshot.

- Parameter `obj`: The object to compare with the current snapshot.
- Returns: True if the specified object is equal to the current snapshot; otherwise, false.

###### `M:IoT.DriverCore.ModbusRx.ModbusServerDataSnapshot.GetHashCode`

```csharp
public int GetHashCode()
```
Returns the hash code for this snapshot.

- Returns: A hash code for this snapshot.

###### `M:IoT.DriverCore.ModbusRx.ModbusServerDataSnapshot.op_Equality(IoT.DriverCore.ModbusRx.ModbusServerDataSnapshot,IoT.DriverCore.ModbusRx.ModbusServerDataSnapshot)`

```csharp
public static bool op_Equality(IoT.DriverCore.ModbusRx.ModbusServerDataSnapshot left, IoT.DriverCore.ModbusRx.ModbusServerDataSnapshot right)
```
Determines whether two snapshots are equal.

- Parameter `left`: The first snapshot to compare.
- Parameter `right`: The second snapshot to compare.
- Returns: True if the snapshots are equal; otherwise, false.

###### `M:IoT.DriverCore.ModbusRx.ModbusServerDataSnapshot.op_Inequality(IoT.DriverCore.ModbusRx.ModbusServerDataSnapshot,IoT.DriverCore.ModbusRx.ModbusServerDataSnapshot)`

```csharp
public static bool op_Inequality(IoT.DriverCore.ModbusRx.ModbusServerDataSnapshot left, IoT.DriverCore.ModbusRx.ModbusServerDataSnapshot right)
```
Determines whether two snapshots are not equal.

- Parameter `left`: The first snapshot to compare.
- Parameter `right`: The second snapshot to compare.
- Returns: True if the snapshots are not equal; otherwise, false.

###### `P:IoT.DriverCore.ModbusRx.ModbusServerDataSnapshot.Coils`

```csharp
public System.Collections.Generic.IReadOnlyList<bool> Coils { get; }
```
Gets the coils data.

- Value: The `Coils` value.

###### `P:IoT.DriverCore.ModbusRx.ModbusServerDataSnapshot.HoldingRegisters`

```csharp
public System.Collections.Generic.IReadOnlyList<ushort> HoldingRegisters { get; }
```
Gets the holding registers data.

- Value: The `HoldingRegisters` value.

###### `P:IoT.DriverCore.ModbusRx.ModbusServerDataSnapshot.InputRegisters`

```csharp
public System.Collections.Generic.IReadOnlyList<ushort> InputRegisters { get; }
```
Gets the input registers data.

- Value: The `InputRegisters` value.

###### `P:IoT.DriverCore.ModbusRx.ModbusServerDataSnapshot.Inputs`

```csharp
public System.Collections.Generic.IReadOnlyList<bool> Inputs { get; }
```
Gets the inputs data.

- Value: The `Inputs` value.

###### `P:IoT.DriverCore.ModbusRx.ModbusServerDataSnapshot.IsEmpty`

```csharp
public bool IsEmpty { get; }
```
Gets a value indicating whether this snapshot is empty.

- Value: The `IsEmpty` value.

###### `P:IoT.DriverCore.ModbusRx.ModbusServerDataSnapshot.Timestamp`

```csharp
public System.DateTimeOffset Timestamp { get; }
```
Gets the timestamp of this snapshot.

- Value: The `Timestamp` value.

#### `T:IoT.DriverCore.ModbusRx.ModbusServerExtensions`

```csharp
public class IoT.DriverCore.ModbusRx.ModbusServerExtensions
```
Reactive extensions for ModbusServer.

##### Declared public members

###### `M:IoT.DriverCore.ModbusRx.ModbusServerExtensions.CreateReactiveServer(System.Action`1{IoT.DriverCore.ModbusRx.Device.ModbusServer})`

```csharp
public static System.IObservable<IoT.DriverCore.ModbusRx.Device.ModbusServer> CreateReactiveServer(System.Action<IoT.DriverCore.ModbusRx.Device.ModbusServer> configureServer)
```
Executes the `CreateReactiveServer` operation.

- Parameter `configureServer`: The `configureServer` value.
- Returns: A `System.IObservable<IoT.DriverCore.ModbusRx.Device.ModbusServer>` result.

###### `M:IoT.DriverCore.ModbusRx.ModbusServerExtensions.ObserveCoils(IoT.DriverCore.ModbusRx.Device.ModbusServer,System.UInt16,System.UInt16,System.Double)`

```csharp
public static System.IObservable<bool[]> ObserveCoils(IoT.DriverCore.ModbusRx.Device.ModbusServer server, ushort startAddress, ushort count, double interval)
```
Observes changes to coils in the server data store.

- Parameter `server`: The extension receiver.
- Parameter `startAddress`: The starting address to monitor.
- Parameter `count`: The number of coils to monitor.
- Parameter `interval`: The polling interval in milliseconds.
- Returns: An observable stream of coil values.

###### `M:IoT.DriverCore.ModbusRx.ModbusServerExtensions.ObserveDataChanges(IoT.DriverCore.ModbusRx.Device.ModbusServer,System.Double)`

```csharp
public static System.IObservable<System.ValueTuple<ushort[], ushort[], bool[], bool[]>> ObserveDataChanges(IoT.DriverCore.ModbusRx.Device.ModbusServer server, double interval)
```
Creates an observable stream of data changes from the server.

- Parameter `server`: The extension receiver.
- Parameter `interval`: The polling interval in milliseconds.
- Returns: An observable stream of server data.

###### `M:IoT.DriverCore.ModbusRx.ModbusServerExtensions.ObserveDiscreteInputs(IoT.DriverCore.ModbusRx.Device.ModbusServer,System.UInt16,System.UInt16,System.Double)`

```csharp
public static System.IObservable<bool[]> ObserveDiscreteInputs(IoT.DriverCore.ModbusRx.Device.ModbusServer server, ushort startAddress, ushort count, double interval)
```
Observes changes to discrete inputs in the server data store.

- Parameter `server`: The extension receiver.
- Parameter `startAddress`: The starting address to monitor.
- Parameter `count`: The number of inputs to monitor.
- Parameter `interval`: The polling interval in milliseconds.
- Returns: An observable stream of discrete input values.

###### `M:IoT.DriverCore.ModbusRx.ModbusServerExtensions.ObserveHoldingRegisters(IoT.DriverCore.ModbusRx.Device.ModbusServer,System.UInt16,System.UInt16,System.Double)`

```csharp
public static System.IObservable<ushort[]> ObserveHoldingRegisters(IoT.DriverCore.ModbusRx.Device.ModbusServer server, ushort startAddress, ushort count, double interval)
```
Observes changes to holding registers in the server data store.

- Parameter `server`: The extension receiver.
- Parameter `startAddress`: The starting address to monitor.
- Parameter `count`: The number of registers to monitor.
- Parameter `interval`: The polling interval in milliseconds.
- Returns: An observable stream of holding register values.

###### `M:IoT.DriverCore.ModbusRx.ModbusServerExtensions.ObserveInputRegisters(IoT.DriverCore.ModbusRx.Device.ModbusServer,System.UInt16,System.UInt16,System.Double)`

```csharp
public static System.IObservable<ushort[]> ObserveInputRegisters(IoT.DriverCore.ModbusRx.Device.ModbusServer server, ushort startAddress, ushort count, double interval)
```
Observes changes to input registers in the server data store.

- Parameter `server`: The extension receiver.
- Parameter `startAddress`: The starting address to monitor.
- Parameter `count`: The number of registers to monitor.
- Parameter `interval`: The polling interval in milliseconds.
- Returns: An observable stream of input register values.

#### `T:IoT.DriverCore.ModbusRx.ModbusTcpSlaveExtensions`

```csharp
public class IoT.DriverCore.ModbusRx.ModbusTcpSlaveExtensions
```
Writes observable values to TCP slave data stores.

##### Declared public members

###### `M:IoT.DriverCore.ModbusRx.ModbusTcpSlaveExtensions.#ctor`

```csharp
public IoT.DriverCore.ModbusRx.ModbusTcpSlaveExtensions()
```
Initializes a new instance of the `T:IoT.DriverCore.ModbusRx.ModbusTcpSlaveExtensions` class.

###### `M:IoT.DriverCore.ModbusRx.ModbusTcpSlaveExtensions.#ctor(System.Byte)`

```csharp
public IoT.DriverCore.ModbusRx.ModbusTcpSlaveExtensions(byte unitIdentifier)
```
Initializes a new instance of the `T:IoT.DriverCore.ModbusRx.ModbusTcpSlaveExtensions` class.

- Parameter `unitIdentifier`: The Modbus unit identifier used by write requests.

###### `M:IoT.DriverCore.ModbusRx.ModbusTcpSlaveExtensions.WriteCoilDiscretes(System.IObservable`1{IoT.DriverCore.ModbusRx.Device.ModbusTcpSlave},System.UInt16,System.IObservable`1{System.Boolean[]})`

```csharp
public System.IObservable<IoT.DriverCore.ModbusRx.Device.ModbusTcpSlave> WriteCoilDiscretes(System.IObservable<IoT.DriverCore.ModbusRx.Device.ModbusTcpSlave> slave, ushort startAddress, System.IObservable<bool[]> valuesToWrite)
```
Executes the `WriteCoilDiscretes` operation.

- Parameter `slave`: The `slave` value.
- Parameter `startAddress`: The `startAddress` value.
- Parameter `valuesToWrite`: The `valuesToWrite` value.
- Returns: A `System.IObservable<IoT.DriverCore.ModbusRx.Device.ModbusTcpSlave>` result.

###### `M:IoT.DriverCore.ModbusRx.ModbusTcpSlaveExtensions.WriteHoldingRegisters(System.IObservable`1{IoT.DriverCore.ModbusRx.Device.ModbusTcpSlave},System.UInt16,System.IObservable`1{System.UInt16[]})`

```csharp
public System.IObservable<IoT.DriverCore.ModbusRx.Device.ModbusTcpSlave> WriteHoldingRegisters(System.IObservable<IoT.DriverCore.ModbusRx.Device.ModbusTcpSlave> slave, ushort startAddress, System.IObservable<ushort[]> valuesToWrite)
```
Executes the `WriteHoldingRegisters` operation.

- Parameter `slave`: The `slave` value.
- Parameter `startAddress`: The `startAddress` value.
- Parameter `valuesToWrite`: The `valuesToWrite` value.
- Returns: A `System.IObservable<IoT.DriverCore.ModbusRx.Device.ModbusTcpSlave>` result.

###### `M:IoT.DriverCore.ModbusRx.ModbusTcpSlaveExtensions.WriteInputDiscretes(System.IObservable`1{IoT.DriverCore.ModbusRx.Device.ModbusTcpSlave},System.UInt16,System.IObservable`1{System.Boolean[]})`

```csharp
public System.IObservable<IoT.DriverCore.ModbusRx.Device.ModbusTcpSlave> WriteInputDiscretes(System.IObservable<IoT.DriverCore.ModbusRx.Device.ModbusTcpSlave> slave, ushort startAddress, System.IObservable<bool[]> valuesToWrite)
```
Executes the `WriteInputDiscretes` operation.

- Parameter `slave`: The `slave` value.
- Parameter `startAddress`: The `startAddress` value.
- Parameter `valuesToWrite`: The `valuesToWrite` value.
- Returns: A `System.IObservable<IoT.DriverCore.ModbusRx.Device.ModbusTcpSlave>` result.

###### `M:IoT.DriverCore.ModbusRx.ModbusTcpSlaveExtensions.WriteInputRegisters(System.IObservable`1{IoT.DriverCore.ModbusRx.Device.ModbusTcpSlave},System.UInt16,System.IObservable`1{System.UInt16[]})`

```csharp
public System.IObservable<IoT.DriverCore.ModbusRx.Device.ModbusTcpSlave> WriteInputRegisters(System.IObservable<IoT.DriverCore.ModbusRx.Device.ModbusTcpSlave> slave, ushort startAddress, System.IObservable<ushort[]> valuesToWrite)
```
Executes the `WriteInputRegisters` operation.

- Parameter `slave`: The `slave` value.
- Parameter `startAddress`: The `startAddress` value.
- Parameter `valuesToWrite`: The `valuesToWrite` value.
- Returns: A `System.IObservable<IoT.DriverCore.ModbusRx.Device.ModbusTcpSlave>` result.

#### `T:IoT.DriverCore.ModbusRx.ModbusUdpSlaveExtensions`

```csharp
public class IoT.DriverCore.ModbusRx.ModbusUdpSlaveExtensions
```
Writes observable values to UDP slave data stores.

##### Declared public members

###### `M:IoT.DriverCore.ModbusRx.ModbusUdpSlaveExtensions.#ctor`

```csharp
public IoT.DriverCore.ModbusRx.ModbusUdpSlaveExtensions()
```
Initializes a new instance of the `T:IoT.DriverCore.ModbusRx.ModbusUdpSlaveExtensions` class.

###### `M:IoT.DriverCore.ModbusRx.ModbusUdpSlaveExtensions.#ctor(System.Byte)`

```csharp
public IoT.DriverCore.ModbusRx.ModbusUdpSlaveExtensions(byte unitIdentifier)
```
Initializes a new instance of the `T:IoT.DriverCore.ModbusRx.ModbusUdpSlaveExtensions` class.

- Parameter `unitIdentifier`: The Modbus unit identifier used by write requests.

###### `M:IoT.DriverCore.ModbusRx.ModbusUdpSlaveExtensions.WriteCoilDiscretes(System.IObservable`1{IoT.DriverCore.ModbusRx.Device.ModbusUdpSlave},System.UInt16,System.IObservable`1{System.Boolean[]})`

```csharp
public System.IObservable<IoT.DriverCore.ModbusRx.Device.ModbusUdpSlave> WriteCoilDiscretes(System.IObservable<IoT.DriverCore.ModbusRx.Device.ModbusUdpSlave> slave, ushort startAddress, System.IObservable<bool[]> valuesToWrite)
```
Executes the `WriteCoilDiscretes` operation.

- Parameter `slave`: The `slave` value.
- Parameter `startAddress`: The `startAddress` value.
- Parameter `valuesToWrite`: The `valuesToWrite` value.
- Returns: A `System.IObservable<IoT.DriverCore.ModbusRx.Device.ModbusUdpSlave>` result.

###### `M:IoT.DriverCore.ModbusRx.ModbusUdpSlaveExtensions.WriteHoldingRegisters(System.IObservable`1{IoT.DriverCore.ModbusRx.Device.ModbusUdpSlave},System.UInt16,System.IObservable`1{System.UInt16[]})`

```csharp
public System.IObservable<IoT.DriverCore.ModbusRx.Device.ModbusUdpSlave> WriteHoldingRegisters(System.IObservable<IoT.DriverCore.ModbusRx.Device.ModbusUdpSlave> slave, ushort startAddress, System.IObservable<ushort[]> valuesToWrite)
```
Executes the `WriteHoldingRegisters` operation.

- Parameter `slave`: The `slave` value.
- Parameter `startAddress`: The `startAddress` value.
- Parameter `valuesToWrite`: The `valuesToWrite` value.
- Returns: A `System.IObservable<IoT.DriverCore.ModbusRx.Device.ModbusUdpSlave>` result.

###### `M:IoT.DriverCore.ModbusRx.ModbusUdpSlaveExtensions.WriteInputDiscretes(System.IObservable`1{IoT.DriverCore.ModbusRx.Device.ModbusUdpSlave},System.UInt16,System.IObservable`1{System.Boolean[]})`

```csharp
public System.IObservable<IoT.DriverCore.ModbusRx.Device.ModbusUdpSlave> WriteInputDiscretes(System.IObservable<IoT.DriverCore.ModbusRx.Device.ModbusUdpSlave> slave, ushort startAddress, System.IObservable<bool[]> valuesToWrite)
```
Executes the `WriteInputDiscretes` operation.

- Parameter `slave`: The `slave` value.
- Parameter `startAddress`: The `startAddress` value.
- Parameter `valuesToWrite`: The `valuesToWrite` value.
- Returns: A `System.IObservable<IoT.DriverCore.ModbusRx.Device.ModbusUdpSlave>` result.

###### `M:IoT.DriverCore.ModbusRx.ModbusUdpSlaveExtensions.WriteInputRegisters(System.IObservable`1{IoT.DriverCore.ModbusRx.Device.ModbusUdpSlave},System.UInt16,System.IObservable`1{System.UInt16[]})`

```csharp
public System.IObservable<IoT.DriverCore.ModbusRx.Device.ModbusUdpSlave> WriteInputRegisters(System.IObservable<IoT.DriverCore.ModbusRx.Device.ModbusUdpSlave> slave, ushort startAddress, System.IObservable<ushort[]> valuesToWrite)
```
Executes the `WriteInputRegisters` operation.

- Parameter `slave`: The `slave` value.
- Parameter `startAddress`: The `startAddress` value.
- Parameter `valuesToWrite`: The `valuesToWrite` value.
- Returns: A `System.IObservable<IoT.DriverCore.ModbusRx.Device.ModbusUdpSlave>` result.

#### `T:IoT.DriverCore.ModbusRx.SlaveException`

```csharp
public class IoT.DriverCore.ModbusRx.SlaveException
```
Represents slave errors that occur during communication.

##### Declared public members

###### `M:IoT.DriverCore.ModbusRx.SlaveException.#ctor`

```csharp
public IoT.DriverCore.ModbusRx.SlaveException()
```
Initializes a new instance of the `T:IoT.DriverCore.ModbusRx.SlaveException` class.

###### `M:IoT.DriverCore.ModbusRx.SlaveException.#ctor(System.String)`

```csharp
public IoT.DriverCore.ModbusRx.SlaveException(string message)
```
Initializes a new instance of the `T:IoT.DriverCore.ModbusRx.SlaveException` class.

- Parameter `message`: The message.

###### `M:IoT.DriverCore.ModbusRx.SlaveException.#ctor(System.String,System.Exception)`

```csharp
public IoT.DriverCore.ModbusRx.SlaveException(string message, System.Exception innerException)
```
Initializes a new instance of the `T:IoT.DriverCore.ModbusRx.SlaveException` class.

- Parameter `message`: The message.
- Parameter `innerException`: The inner exception.

###### `P:IoT.DriverCore.ModbusRx.SlaveException.FunctionCode`

```csharp
public byte FunctionCode { get; }
```
Gets the response function code that caused the exception to occur, or 0.

- Value: The function code.

###### `P:IoT.DriverCore.ModbusRx.SlaveException.Message`

```csharp
public string Message { get; }
```
Gets a message that describes the current exception.

- Value: The error message that explains the reason for the exception, or an empty string.

###### `P:IoT.DriverCore.ModbusRx.SlaveException.SlaveAddress`

```csharp
public byte SlaveAddress { get; }
```
Gets the slave address, or 0.

- Value: The slave address.

###### `P:IoT.DriverCore.ModbusRx.SlaveException.SlaveExceptionCode`

```csharp
public byte SlaveExceptionCode { get; }
```
Gets the slave exception code, or 0.

- Value: The slave exception code.

#### `T:IoT.DriverCore.ModbusRx.Utility.DiscriminatedUnionOption`

```csharp
public enum IoT.DriverCore.ModbusRx.Utility.DiscriminatedUnionOption
```
Possible options for `T:IoT.DriverCore.ModbusRx.Utility.DiscriminatedUnion`2` .

##### Declared public members

###### `F:IoT.DriverCore.ModbusRx.Utility.DiscriminatedUnionOption.A`

```csharp
public static const IoT.DriverCore.ModbusRx.Utility.DiscriminatedUnionOption A
```
Option A.

###### `F:IoT.DriverCore.ModbusRx.Utility.DiscriminatedUnionOption.B`

```csharp
public static const IoT.DriverCore.ModbusRx.Utility.DiscriminatedUnionOption B
```
Option B.

#### `T:IoT.DriverCore.ModbusRx.Utility.DiscriminatedUnion`2`

```csharp
public class IoT.DriverCore.ModbusRx.Utility.DiscriminatedUnion`2
```
A data type that can store one of two possible strongly typed options.

##### Declared public members

###### `M:IoT.DriverCore.ModbusRx.Utility.DiscriminatedUnion`2.#ctor`

```csharp
public IoT.DriverCore.ModbusRx.Utility.DiscriminatedUnion<TA, TB>()
```
Initializes a new instance of `IoT.DriverCore.ModbusRx.Utility.DiscriminatedUnion`2`.

###### `M:IoT.DriverCore.ModbusRx.Utility.DiscriminatedUnion`2.CreateA(`0)`

```csharp
public static IoT.DriverCore.ModbusRx.Utility.DiscriminatedUnion<TA, TB> CreateA(TA a)
```
Factory method for creating DiscriminatedUnion with option A set.

- Parameter `a`: a.
- Returns: A DiscriminatedUnion.

###### `M:IoT.DriverCore.ModbusRx.Utility.DiscriminatedUnion`2.CreateB(`1)`

```csharp
public static IoT.DriverCore.ModbusRx.Utility.DiscriminatedUnion<TA, TB> CreateB(TB b)
```
Factory method for creating DiscriminatedUnion with option B set.

- Parameter `b`: The b.
- Returns: A DiscriminatedUnion.

###### `M:IoT.DriverCore.ModbusRx.Utility.DiscriminatedUnion`2.ToString`

```csharp
public string ToString()
```
Returns a string that represents the current object.

- Returns: A `T:System.String` that represents the current `T:System.Object` .

###### `P:IoT.DriverCore.ModbusRx.Utility.DiscriminatedUnion`2.A`

```csharp
public TA A { get; }
```
Gets the value of option A.

- Value: The `A` value.

###### `P:IoT.DriverCore.ModbusRx.Utility.DiscriminatedUnion`2.B`

```csharp
public TB B { get; }
```
Gets the value of option B.

- Value: The `B` value.

###### `P:IoT.DriverCore.ModbusRx.Utility.DiscriminatedUnion`2.Option`

```csharp
public IoT.DriverCore.ModbusRx.Utility.DiscriminatedUnionOption Option { get; }
```
Gets the discriminated value option set for this instance.

- Value: The `Option` value.

#### `T:IoT.DriverCore.ModbusRx.Utility.ModbusUtility`

```csharp
public class IoT.DriverCore.ModbusRx.Utility.ModbusUtility
```
Modbus utility methods with high-performance optimizations.

##### Declared public members

###### `M:IoT.DriverCore.ModbusRx.Utility.ModbusUtility.CalculateCrc(System.Byte[])`

```csharp
public static byte[] CalculateCrc(byte[] data)
```
Calculate Cyclical Redundancy Check.

- Parameter `data`: The data used in CRC.
- Returns: CRC value as byte array.

###### `M:IoT.DriverCore.ModbusRx.Utility.ModbusUtility.CalculateCrc(System.ReadOnlySpan`1{System.Byte},System.Span`1{System.Byte})`

```csharp
public static int CalculateCrc(System.ReadOnlySpan<byte> data, System.Span<byte> result)
```
Executes the `CalculateCrc` operation.

- Parameter `data`: The `data` value.
- Parameter `result`: The `result` value.
- Returns: A `int` result.

###### `M:IoT.DriverCore.ModbusRx.Utility.ModbusUtility.CalculateLrc(System.Byte[])`

```csharp
public static byte CalculateLrc(byte[] data)
```
Calculate Longitudinal Redundancy Check.

- Parameter `data`: The data used in LRC.
- Returns: LRC value.

###### `M:IoT.DriverCore.ModbusRx.Utility.ModbusUtility.CalculateLrc(System.ReadOnlySpan`1{System.Byte})`

```csharp
public static byte CalculateLrc(System.ReadOnlySpan<byte> data)
```
Executes the `CalculateLrc` operation.

- Parameter `data`: The `data` value.
- Returns: A `byte` result.

###### `M:IoT.DriverCore.ModbusRx.Utility.ModbusUtility.GetAsciiBytes(System.Byte[])`

```csharp
public static byte[] GetAsciiBytes(byte[] numbers)
```
Converts an array of bytes to an ASCII byte array.

- Parameter `numbers`: The byte array.
- Returns: An array of ASCII byte values.

###### `M:IoT.DriverCore.ModbusRx.Utility.ModbusUtility.GetAsciiBytes(System.ReadOnlySpan`1{System.Byte})`

```csharp
public static byte[] GetAsciiBytes(System.ReadOnlySpan<byte> numbers)
```
Executes the `GetAsciiBytes` operation.

- Parameter `numbers`: The `numbers` value.
- Returns: A `byte[]` result.

###### `M:IoT.DriverCore.ModbusRx.Utility.ModbusUtility.GetAsciiBytes(System.ReadOnlySpan`1{System.UInt16})`

```csharp
public static byte[] GetAsciiBytes(System.ReadOnlySpan<ushort> numbers)
```
Executes the `GetAsciiBytes` operation.

- Parameter `numbers`: The `numbers` value.
- Returns: A `byte[]` result.

###### `M:IoT.DriverCore.ModbusRx.Utility.ModbusUtility.GetAsciiBytes(System.UInt16[])`

```csharp
public static byte[] GetAsciiBytes(ushort[] numbers)
```
Converts an array of UInt16 to an ASCII byte array.

- Parameter `numbers`: The ushort array.
- Returns: An array of ASCII byte values.

###### `M:IoT.DriverCore.ModbusRx.Utility.ModbusUtility.GetDouble(System.UInt16,System.UInt16,System.UInt16,System.UInt16)`

```csharp
public static double GetDouble(ushort b3, ushort b2, ushort b1, ushort b0)
```
Converts four UInt16 values to an IEEE 64-bit floating-point value.

- Parameter `b3`: Highest-order ushort value.
- Parameter `b2`: Second-to-highest-order ushort value.
- Parameter `b1`: Second-to-lowest-order ushort value.
- Parameter `b0`: Lowest-order ushort value.
- Returns: IEEE 64 floating point value.

###### `M:IoT.DriverCore.ModbusRx.Utility.ModbusUtility.GetSingle(System.UInt16,System.UInt16)`

```csharp
public static float GetSingle(ushort highOrderValue, ushort lowOrderValue)
```
Converts two UInt16 values to an IEEE 32-bit floating-point value.

- Parameter `highOrderValue`: High order ushort value.
- Parameter `lowOrderValue`: Low order ushort value.
- Returns: IEEE 32 floating point value.

###### `M:IoT.DriverCore.ModbusRx.Utility.ModbusUtility.GetUInt32(System.UInt16,System.UInt16)`

```csharp
public static uint GetUInt32(ushort highOrderValue, ushort lowOrderValue)
```
Converts two UInt16 values into a UInt32 using optimized memory operations.

- Parameter `highOrderValue`: The high order value.
- Parameter `lowOrderValue`: The low order value.
- Returns: A UInt32 value.

###### `M:IoT.DriverCore.ModbusRx.Utility.ModbusUtility.HexToBytes(System.ReadOnlySpan`1{System.Char},System.Span`1{System.Byte})`

```csharp
public static int HexToBytes(System.ReadOnlySpan<char> hex, System.Span<byte> result)
```
Executes the `HexToBytes` operation.

- Parameter `hex`: The `hex` value.
- Parameter `result`: The `result` value.
- Returns: A `int` result.

###### `M:IoT.DriverCore.ModbusRx.Utility.ModbusUtility.HexToBytes(System.String)`

```csharp
public static byte[] HexToBytes(string hex)
```
Converts a hex string to a byte array.

- Parameter `hex`: The hex string.
- Returns: Array of bytes.

###### `M:IoT.DriverCore.ModbusRx.Utility.ModbusUtility.NetworkBytesToHostUInt16(System.Byte[])`

```csharp
public static ushort[] NetworkBytesToHostUInt16(byte[] networkBytes)
```
Converts a network order byte array to an array of UInt16 values in host order.

- Parameter `networkBytes`: The network order byte array.
- Returns: The host order ushort array.

###### `M:IoT.DriverCore.ModbusRx.Utility.ModbusUtility.NetworkBytesToHostUInt16(System.ReadOnlySpan`1{System.Byte},System.Span`1{System.UInt16})`

```csharp
public static int NetworkBytesToHostUInt16(System.ReadOnlySpan<byte> networkBytes, System.Span<ushort> result)
```
Executes the `NetworkBytesToHostUInt16` operation.

- Parameter `networkBytes`: The `networkBytes` value.
- Parameter `result`: The `result` value.
- Returns: A `int` result.

###### `M:IoT.DriverCore.ModbusRx.Utility.ModbusUtility.ReadDouble(System.ReadOnlySpan`1{System.UInt16},System.Boolean)`

```csharp
public static double ReadDouble(System.ReadOnlySpan<ushort> registers, bool swapWords)
```
Executes the `ReadDouble` operation.

- Parameter `registers`: The `registers` value.
- Parameter `swapWords`: The `swapWords` value.
- Returns: A `double` result.

###### `M:IoT.DriverCore.ModbusRx.Utility.ModbusUtility.ReadSingle(System.ReadOnlySpan`1{System.UInt16},System.Boolean)`

```csharp
public static float ReadSingle(System.ReadOnlySpan<ushort> registers, bool swapWords)
```
Executes the `ReadSingle` operation.

- Parameter `registers`: The `registers` value.
- Parameter `swapWords`: The `swapWords` value.
- Returns: A `float` result.

###### `M:IoT.DriverCore.ModbusRx.Utility.ModbusUtility.WriteDouble(System.Double,System.Span`1{System.UInt16},System.Boolean)`

```csharp
public static void WriteDouble(double value, System.Span<ushort> registers, bool swapWords)
```
Executes the `WriteDouble` operation.

- Parameter `value`: The `value` value.
- Parameter `registers`: The `registers` value.
- Parameter `swapWords`: The `swapWords` value.

###### `M:IoT.DriverCore.ModbusRx.Utility.ModbusUtility.WriteSingle(System.Single,System.Span`1{System.UInt16},System.Boolean)`

```csharp
public static void WriteSingle(float value, System.Span<ushort> registers, bool swapWords)
```
Executes the `WriteSingle` operation.

- Parameter `value`: The `value` value.
- Parameter `registers`: The `registers` value.
- Parameter `swapWords`: The `swapWords` value.

<!-- END GENERATED PUBLIC API -->
