---
name: modbus-rx
description: Implement, review, or troubleshoot Modbus TCP, UDP, RTU, ASCII, master, slave, simulation, logical-tag, observable, and generated register-map workflows with ModbusRx.
---

# ModbusRx

## Use this skill when

Use this skill for Modbus clients, servers/slaves, gateways, simulators, logical tags, polling, or generated device maps.

Read the co-packaged `../../README.md`; in a repository checkout use `packagereadme/ModbusRx/README.md`. Inspect `src/ModbusRx` before describing undocumented members because the reactive package compiles shared source under a different namespace.

## Package choice

- Use `ModbusRx` with `IoT.DriverCore.ModbusRx` by default.
- Use `ModbusRx.Reactive` and its `.Reactive` namespace for System.Reactive applications.
- Runtime packages do not embed the analyzer. Install `ModbusRx.Generators` alongside the selected runtime only when generated register maps are required.

## Master workflow

Create the transport first, then create the master. Match endpoint, timeouts, unit ID, and serial parameters to the device.

```csharp
using IoT.DriverCore.ModbusRx.Device;
using IoT.DriverCore.Serial;

using var tcp = new TcpClientRx("192.168.0.20", 502);
using var master = ModbusIpMaster.CreateIp(tcp);

var registers = await master.ReadHoldingRegistersAsync(
    slaveAddress: 1,
    startAddress: 0,
    numberOfPoints: 2);

await master.WriteSingleRegisterAsync(
    slaveAddress: 1,
    registerAddress: 0,
    value: 42);
```

For serial links, configure `SerialPortRx`, then call `ModbusSerialMaster.CreateRtu` or `CreateAscii`. Dispose both the master and its owned/non-owned transport according to the constructor contract.

## Addressing and limits

API addresses are zero-based offsets; do not pass display references such as 40001 directly. Preserve the unit ID for serial devices and TCP/UDP gateways.

Respect protocol limits:

- read up to 2,000 bits or 125 registers;
- write up to 1,968 coils or 123 registers;
- combined read/write supports up to 121 write registers.

Batch contiguous coils/registers and decode byte/word order explicitly. Do not assume every vendor uses the same 32-bit or floating-point ordering.

## Function families and failures

Use `ReadCoilsAsync`, `ReadInputsAsync`, `ReadHoldingRegistersAsync`, `ReadInputRegistersAsync`, single/multiple write methods, and `ReadWriteMultipleRegistersAsync` for their matching function codes.

Catch and classify `SlaveException`, `InvalidModbusRequestException`, and `ModbusCommunicationException`. Include unit ID, function, and zero-based range in diagnostics without exposing secrets. Retry only idempotent work unless the application can prove the original write was not accepted.

## Logical tags and observables

Use the logical-tag catalogue/client surfaces for stable typed application names. The adapter owns unit ID, table, address, conversion, and batch planning. Use observable connection/polling helpers with bounded intervals and dispose subscriptions.

When using Enron helpers or custom conversion, verify that the target device implements the non-standard convention.

## Slave/server workflow

Use `DataStore` with `ModbusTcpSlave`, `ModbusUdpSlave`, or `ModbusSerialSlave`; compose multiple units with the server/aggregator APIs. Synchronize direct `DataStore` access and validate writes before applying them to physical outputs.

Subscribe to request/operation observation when auditing or testing. Dispose listeners and transports so ports are released.

## Simulation and generation

Use `DataStore`, `ModbusSimulator`, or `ModbusTcpLoopbackEndpoint` for deterministic tests. Cover each used function, unit routing, exception responses, timeouts, malformed requests, word order, boundary ranges, and reconnect behavior.

For generated maps, install `ModbusRx.Generators`, declare supported attributes on partial types, build once, inspect diagnostics and generated members, then test the map against a simulator.

## Safety checklist

- Verify transport, unit ID, table, zero-based address, count, type, and word order.
- Treat every coil/register write as safety-sensitive.
- Require access control and equipment interlocks at the server boundary.
- Batch within protocol limits and keep retries idempotent.
- Synchronize shared data stores.
- Test with a simulator and safe rig before production devices.
