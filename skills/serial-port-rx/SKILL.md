---
name: serial-port-rx
description: Implement, review, or troubleshoot reactive serial, TCP, and UDP I/O with IoT-Driver.SerialPortRx, including configuration, receive ownership, framing, request-response coordination, errors, deterministic links, and generation.
---

# SerialPortRx

## Use this skill when

Use this skill for serial instruments, barcode readers, scales, gateways, TCP streams, UDP datagrams, message framing, command/response devices, or deterministic in-memory transport tests.

Read the co-packaged `../../README.md`; in a repository checkout use `packagereadme/SerialPortRx/README.md`. Inspect `src/SerialPortRx` before describing undocumented members.

## Package choice

- Use `IoT-Driver.SerialPortRx` with `IoT.Driver.Serial` by default.
- Use `IoT-Driver.SerialPortRx.Reactive` and `IoT.Driver.Serial.Reactive` for System.Reactive applications.
- Runtime packages never embed the generator. Install the standalone `IoT-Driver.SerialPortRx.Generators` package when generated stream models are required.
- Check the package matrix for target-framework and Windows-only serial features.

## Serial workflow

Set baud rate, data bits, parity, stop bits, handshake, timeouts, encoding, and delimiters before `OpenAsync`. Subscribe to state and errors before opening.

```csharp
using IoT.Driver.Serial;

using var port = new SerialPortRx("COM3", 115200)
{
    ReadTimeout = -1,
    WriteTimeout = -1,
};

using var state = port.IsOpenObservable
    .Subscribe(open => Console.WriteLine($"Open: {open}"));
using var errors = port.ErrorReceived
    .Subscribe(error => Console.Error.WriteLine(error.Message));
using var data = port.DataReceived.Subscribe(Console.Write);

await port.OpenAsync();
port.WriteLine("AT");
port.Close();
```

Dispose the port and subscriptions. Use `PortNames` only as a discovery stream; still handle disappearance and open failures.

## Choose one receive owner

Choose exactly one model per connection:

1. automatic receive through `DataReceived`, `DataReceivedBytes`, `DataReceivedBatches`, or `Lines`;
2. an explicit `StartDataReception` lease;
3. manual `Read*` calls with `EnableAutoDataReceive = false` before opening.

Do not mix owners. Competing consumers split bytes unpredictably and corrupt protocol framing.

## Framing and command coordination

Serial and TCP receive batches are arbitrary transport chunks, not application messages. Use bounded `BufferUntil`, line framing, or a protocol parser that handles partial and multiple frames. Enforce maximum frame sizes and finite request timeouts.

Use `SerialPortRxMessageHandler` for line-oriented request/response ownership. Configure device error prefixes, echo/response handling, and polling coordination. Use `WithPollingStoppedAsync` for exclusive commands and dispose the handler to stop polling.

## TCP and UDP

`TcpClientRx` and `UdpClientRx` implement `IPortRx`:

```csharp
using var tcp = new TcpClientRx("example.com", 80);
await tcp.OpenAsync();

using var udp = new UdpClientRx(12345);
await udp.OpenAsync();
```

TCP is a byte stream: frame across arbitrary chunks. UDP batch events correspond to datagrams, but validate source, expected length, sequence, and content. Handle disconnect/reconnect and socket errors explicitly.

## Writes and concurrency

Serialize writes when device protocols do not support pipelining. Respect encoding and terminators for text commands; use byte overloads for binary protocols. Bound queues so a slow connection cannot grow memory without limit.

## Testing and generation

Use `InMemoryPortRxPair` and related deterministic links for framing, request/response, timeout, cancellation, disconnect, partial-frame, multiple-frame, and error tests.

For generated serial properties, use documented attributes on partial types, build once, inspect diagnostics and generated members, and verify parsing, output, errors, and disposal against an in-memory link.

## Safety checklist

- Verify port/endpoint, electrical/interface standard, serial format, and handshake.
- Choose one receive owner and one framing authority.
- Bound frames, buffers, queues, retries, and timeouts.
- Validate every inbound message before acting on it.
- Treat protocol commands as potentially safety-sensitive writes.
- Dispose ports, socket clients, handlers, and subscriptions.
