---
name: serial-port-rx
description: Implement, review, or troubleshoot reactive serial, TCP, and UDP I/O with SerialPortRx. Use for port configuration, receive ownership, message framing, connection streams, and deterministic in-memory port tests.
---

# SerialPortRx

Use `IoT.DriverCore.Serial` for the default package surface. Configure serial settings before `OpenAsync`, subscribe to errors before opening, and dispose subscriptions with the port.

The source generator is embedded in `SerialPortRx` and `SerialPortRx.Reactive`; there is no standalone `SerialPortRx.Generators` NuGet package to install.

Choose exactly one receive owner. Leave `EnableAutoDataReceive` enabled for `DataReceived`, `DataReceivedBytes`, and `DataReceivedBatches`; set it to false before opening for manual read APIs. Do not mix automatic parsing with competing manual reads.

Frame TCP and serial payloads explicitly. Treat TCP read batches as arbitrary transport chunks, use bounded `BufferUntil` or a protocol parser, and select finite timeouts for commands. Treat UDP batches as datagrams but still validate their source and content.

Use `InMemoryPortRxPair` for deterministic tests. For complete contracts, network client APIs, and platform restrictions, read the co-packaged `../../README.md`; in a repository checkout use `packagereadme/SerialPortRx/README.md`.
