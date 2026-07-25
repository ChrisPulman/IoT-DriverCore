---
name: modbus-rx
description: Implement, review, or document Modbus TCP, UDP, RTU, ASCII, server, simulation, logical-tag, or observable integrations using ModbusRx or ModbusRx.Reactive.
---

# ModbusRx

Before changing an integration, read the co-packaged `../../README.md`; in a repository checkout use `packagereadme/ModbusRx/README.md`.

- Import `IoT.DriverCore.ModbusRx` or its `.Reactive` counterpart to match the selected package.
- Install `ModbusRx.Generators` alongside the selected runtime package when attribute-driven register maps are required; the runtime packages do not embed this analyzer.
- Create masters through `ModbusIpMaster.CreateIp` or `ModbusSerialMaster.CreateRtu` / `CreateAscii`; match endpoint and serial settings to the device.
- Use zero-based API addresses, correct unit IDs, and protocol limits: 2,000 bits, 125 read registers, 1,968 coils, 123 write registers, and 121 combined-write registers.
- Batch contiguous ranges, dispose masters/slaves/servers and observable subscriptions, and make retryable writes idempotent.
- Use `DataStore`, `ModbusSimulator`, or `ModbusTcpLoopbackEndpoint` for tests before a physical device. Synchronize direct `DataStore` access.
- Treat every write as safety-sensitive; validate function, unit ID, address, value, access permissions, and interlocks.
- Inspect `src/ModbusRx` before describing an API; the reactive package compiles shared source under a different namespace.
