<p align="center">
  <img src="https://raw.githubusercontent.com/ChrisPulman/IoT-DriverCore/main/images/serial-port-rx.png" alt="SerialPortRx package logo" width="320" />
</p>

# SerialPortRx
A Reactive Serial, TCP, and UDP I/O library that exposes incoming data as IObservable streams and accepts writes via simple methods. Ideal for event-driven, message-framed, and polling scenarios.

## Overview

The package is named `SerialPortRx`; migrated application code imports `IoT.DriverCore.Serial`. It provides one receive contract over serial ports, TCP streams, UDP datagrams, deterministic in-memory links, request/response coordination, observables, async-observables, and a bundled source generator. `SerialPortRx.Reactive` mirrors the implementation under `IoT.DriverCore.Serial.Reactive` for System.Reactive-oriented consumers.

Choose exactly one receive owner for each connection: automatic observable reception, an explicit `StartDataReception` lease, or manual `Read*` calls. Combining these models makes bytes compete and breaks protocol framing.

## Safety

- Treat serial and network input as untrusted. Set device-appropriate timeouts, bound buffers/message sizes in your parser, validate commands, and authenticate any application protocol.
- Configure serial settings and `EnableAutoDataReceive` **before** `OpenAsync`. Subscribe to `ErrorReceived` and state before command traffic.
- Hold every subscription, reception lease, message handler, and port in an owned disposal scope. Call `Close` or `Dispose` before changing wires or reconfiguring endpoints.
- A TCP read is not a message boundary. Frame TCP chunks yourself. UDP batches preserve datagram boundaries but still need application validation.

## Package matrix and installation

| Package | Namespace | When to choose it | Generator |
|---|---|---|---|
| `SerialPortRx` | `IoT.DriverCore.Serial` | New code using ReactiveUI.Primitives | Includes the serial analyzer asset. |
| `SerialPortRx.Reactive` | `IoT.DriverCore.Serial.Reactive` | Existing System.Reactive-facing code | Includes the same analyzer behavior. |

```bash
dotnet add package SerialPortRx
# Or, for the reactive compatibility surface:
dotnet add package SerialPortRx.Reactive
```

Target frameworks are `net462`, `net472`, `net481`, `net8.0`, `net9.0`, `net10.0`, and `net11.0`; Windows-specific pin APIs are conditionally compiled for Windows-capable targets.

## Lifecycle and error model

`OpenAsync` starts a configured transport and starts the automatic receiver when enabled. `Close` and `Dispose` release the underlying port/socket and complete owned streams; a disposed object must not be reopened. `ErrorReceived` reports transport/parser errors; `IsOpenObservable` reports state transitions. `ReadAsync`, `ReadLineAsync`, and `ReadToAsync` can throw timeout, cancellation, I/O, and invalid-operation exceptions. Observe errors before opening and use a cancellation token/finite timeout for request paths.

```csharp
using IoT.DriverCore.Serial;

using var port = new SerialPortRx("COM3", 115200)
{
    NewLine = "\r\n",
    ReadTimeout = 1000,
    WriteTimeout = 1000,
    EnableAutoDataReceive = true,
};
using var state = port.IsOpenObservable.Subscribe(open => Console.WriteLine($"Open={open}"));
using var errors = port.ErrorReceived.Subscribe(Console.Error.WriteLine);
await port.OpenAsync();
try
{
    port.WriteLine("AT");
}
finally
{
    port.Close();
}
```

## Features
- SerialPortRx: Reactive wrapper for System.IO.Ports.SerialPort using ReactiveUI.Primitives
- UdpClientRx and TcpClientRx: Reactive wrappers exposing a common IPortRx interface
- Observables:
  - DataReceived: IObservable<char> for serial text flow
  - DataReceivedBytes: IObservable<byte> for raw byte stream (auto-receive mode)
  - Lines: IObservable<string> of complete lines split by NewLine
  - BytesReceived: IObservable<int> for byte stream emitted when using ReadAsync
  - IsOpenObservable: IObservable<bool> for connection state
  - ErrorReceived: IObservable<Exception> for errors
  - PinChanged: IObservable<SerialPinChangedEventArgs> for pin state changes (Windows only)
- Async observables:
  - Concrete serial, TCP, and UDP types expose IObservableAsync<T> counterparts.
  - IPortRx and ISerialPortRx extension methods bridge existing streams to IObservableAsync<T>.
  - SerialPortRx helpers include async BufferUntil, WhileIsOpenAsync, and PortNamesAsync variants.
- Synchronous read methods for manual data consumption
- TCP/UDP batched reads:
  - TcpClientRx.DataReceivedBatches: IObservable<byte[]> chunks per read loop
  - UdpClientRx.DataReceivedBatches: IObservable<byte[]> per received datagram
- Source generator support:
  - SerialPortReactiveStream attributes generate properties, IObservable<T>, IObservableAsync<T>, and a connection method for serial protocol values.
- Helpers:
  - PortNames(): reactive port enumeration with change notifications
  - BufferUntil(): message framing between start and end delimiters with timeout
  - WhileIsOpen(): periodic observable that fires only while a port is open
- Cross-targeted: net8.0, net9.0, net10.0, .NET Framework, and Windows-specific TFMs

## Installation
```bash
dotnet add package SerialPortRx
```

Use the default `SerialPortRx` package for new code. Version 5.0.x is a breaking release that replaces direct `System.Reactive` usage with `ReactiveUI.Primitives`, including Primitives signals, async observables, sequencers, and disposable helpers.

Existing Rx consumers should install the compatibility package:

```bash
dotnet add package SerialPortRx.Reactive
```

`SerialPortRx.Reactive` shares the same source as `SerialPortRx` and uses ReactiveUI.Primitives `.Reactive` package variants so existing `System.Reactive` `Unit`, `IScheduler`, and Rx operator conventions remain available.

The package includes the SerialPortRx source generator as an analyzer. No separate generator package is required.

### Breaking changes in 5.0.x
- The main `SerialPortRx` package no longer depends on `System.Reactive`; it is based on `ReactiveUI.Primitives`.
- `Unit`, scheduler, subject, and disposable implementation details are now Primitives-based in the default package.
- Use `SerialPortRx.Reactive` when an application or library must keep System.Reactive-facing APIs and Rx scheduler/unit conventions.
- ReactiveUI.Primitives analyzer assets are excluded from the SerialPortRx packages; no extra bridge generator package is included.
- The repository solution entry point is now `src/SerialPortRx.slnx`.

## Supported target frameworks
- net8.0, net9.0, net10.0
- net462, net472, net481
- net8.0-windows10.0.19041.0, net9.0-windows10.0.19041.0, net10.0-windows10.0.19041.0 (adds Windows-only APIs guarded by HasWindows)

## Quick start (Serial)
```csharp
using System;
using IoT.DriverCore.Serial;
using ReactiveUI.Primitives;

var port = new SerialPortRx("COM3", 115200) { ReadTimeout = -1, WriteTimeout = -1 };

// Observe line/state/errors
using var openSubscription = port.IsOpenObservable.Subscribe(isOpen => Console.WriteLine($"Open: {isOpen}"));
using var errorSubscription = port.ErrorReceived.Subscribe(ex => Console.WriteLine($"Error: {ex.Message}"));

// Raw character stream
using var dataSubscription = port.DataReceived.Subscribe(ch => Console.Write(ch));

await port.OpenAsync();
port.WriteLine("AT");

// Close when done
port.Close();
```

## Discovering serial ports
```csharp
// Emits the list of available port names whenever it changes
SerialPortRx.PortNames(pollInterval: 500)
    .Subscribe(names => Console.WriteLine(string.Join(", ", names)));
```

To auto-connect when a specific COM port appears:
```csharp
var target = "COM3";
var portDisposables = new List<IDisposable>();

using var portNamesSubscription = SerialPortRx.PortNames()
    .Subscribe(names =>
    {
        if (portDisposables.Count == 0 && Array.Exists(names, n => string.Equals(n, target, StringComparison.OrdinalIgnoreCase)))
        {
            var port = new SerialPortRx(target, 115200);
            portDisposables.Add(port);

            portDisposables.Add(port.ErrorReceived.Subscribe(Console.WriteLine));
            portDisposables.Add(port.IsOpenObservable.Subscribe(open => Console.WriteLine($"{target}: {(open ? "Open" : "Closed")}")));

            port.OpenAsync();
        }
        else if (!Array.Exists(names, n => string.Equals(n, target, StringComparison.OrdinalIgnoreCase)))
        {
            foreach (var disposable in portDisposables)
            {
                disposable.Dispose();
            }

            portDisposables.Clear();
        }
    });
```

## Async observables
SerialPortRx uses ReactiveUI.Primitives async observables for consumers that need asynchronous observer callbacks and full `IObservableAsync<T>` operators.

```csharp
using IoT.DriverCore.Serial;
using ReactiveUI.Primitives;
using ReactiveUI.Primitives.Async;

var port = new SerialPortRx("COM3", 115200);

await using var lines = await port.LinesAsync.SubscribeAsync(
    async (line, cancellationToken) =>
    {
        await ProcessLineAsync(line, cancellationToken);
    });

await port.OpenAsync();
```

Concrete types expose async properties:
- `SerialPortRx.DataReceivedAsync`, `DataReceivedBytesAsync`, `LinesAsync`, `BytesReceivedAsync`, `IsOpenObservableAsync`, `ErrorReceivedAsync`
- `TcpClientRx.DataReceivedAsync`, `DataReceivedBatchesAsync`, `BytesReceivedAsync`
- `UdpClientRx.DataReceivedAsync`, `DataReceivedBatchesAsync`, `BytesReceivedAsync`

Interface consumers can use extension methods without requiring a new interface contract:
```csharp
ISerialPortRx serial = port;
await using var state = await SerialPortRxMixins.IsOpenAsyncObservable(serial)
    .WhereTrue()
    .SubscribeAsync(_ => Console.WriteLine("Open"));

IPortRx common = port;
await using var bytes = await SerialPortRxMixins.BytesReceivedAsyncObservable(common)
    .SubscribeAsync(value => Console.WriteLine(value));
```

Async helper variants are also available:
```csharp
var start = SerialPortRxMixins.AsAsyncObservable(0x21);
var end = SerialPortRxMixins.AsAsyncObservable(0x0a);

await using var framed = await SerialPortRxMixins
    .BufferUntil(port.DataReceivedAsync, start, end, timeOut: 100)
    .SubscribeAsync(message => Console.WriteLine(message));

await using var names = await SerialPortRxMixins.PortNamesAsyncObservable()
    .SubscribeAsync(ports => Console.WriteLine(string.Join(", ", ports)));
```

## Message framing with BufferUntil
BufferUntil helps extract framed messages from the character stream between a start and end delimiter within a timeout.

```csharp
// Example: messages start with '!' and end with '\n' and must complete within 100ms
var start = SerialPortRxMixins.AsObservable(0x21);  // '!'
var end   = SerialPortRxMixins.AsObservable(0x0a);  // '\n'

SerialPortRxMixins
    .BufferUntil(port.DataReceived, start, end, timeOut: 100)
    .Subscribe(msg => Console.WriteLine($"MSG: {msg}"));
```

A variant returns a default message on timeout:
```csharp
SerialPortRxMixins
    .BufferUntil(port.DataReceived, start, end, defaultValue: Observable.Return("<timeout>"), timeOut: 100)
    .Subscribe(msg => Console.WriteLine($"MSG: {msg}"));
```

## Periodic work while the port is open
```csharp
// Write a heartbeat every 500ms but only while the port remains open
SerialPortRxMixins.WhileIsOpen(port, TimeSpan.FromMilliseconds(500))
    .Subscribe(_ => port.Write("PING\n"));
```

## Reading raw bytes with ReadAsync
Use ReadAsync for binary protocols or fixed-length reads. Each byte successfully read is also pushed to BytesReceived.

```csharp
var buffer = new byte[64];
int read = await port.ReadAsync(buffer, 0, buffer.Length);
Console.WriteLine($"Read {read} bytes");

port.BytesReceived.Subscribe(b => Console.WriteLine($"Byte: {b:X2}"));
```

Notes:
- DataReceived is a char stream produced from SerialPort.ReadExisting() when EnableAutoDataReceive is true (default).
- DataReceivedBytes emits raw bytes alongside DataReceived in auto-receive mode.
- BytesReceived emits bytes read by your ReadAsync calls (not from ReadExisting()).
- Concurrent ReadAsync calls are serialized internally for safety.

## Automatic vs Manual Data Reception
By default, `EnableAutoDataReceive = true` automatically feeds incoming data to `DataReceived` and `DataReceivedBytes` observables. Set this to `false` before calling `OpenAsync()` if you want to use synchronous read methods instead.

```csharp
// Automatic mode owns receive bytes and publishes them to observers.
using var automaticPort = new SerialPortRx("COM3", 115200);
using var automaticData = automaticPort.DataReceived.Subscribe(ch => Console.Write(ch));
await automaticPort.OpenAsync();
try
{
    automaticPort.WriteLine("STATUS?");
}
finally
{
    automaticPort.Close();
}
```

Manual mode is a separate connection. Disable automatic reception before opening it, then use only the synchronous/manual receive methods:

```csharp
using var manualPort = new SerialPortRx("COM4", 115200)
{
    EnableAutoDataReceive = false,
    ReadTimeout = 1000,
};
await manualPort.OpenAsync();
try
{
    string data = manualPort.ReadExisting();
    Console.WriteLine(data);
}
finally
{
    manualPort.Close();
}
```

If a manually configured port later needs observable reception, start exactly one reception lease after opening it; do not also call `Read*` while that lease is active:

```csharp
using var switchedPort = new SerialPortRx("COM5", 115200)
{
    EnableAutoDataReceive = false,
};
using var switchedData = switchedPort.DataReceived.Subscribe(ch => Console.Write(ch));
await switchedPort.OpenAsync();
using (var reception = switchedPort.StartDataReception(pollingIntervalMs: 10))
{
    // While this scope is active, the reactive receiver owns the serial bytes.
}

switchedPort.Close();
```

## Synchronous Read Methods
When `EnableAutoDataReceive = false`, use these synchronous methods for manual data consumption:

```csharp
var port = new SerialPortRx("COM3", 115200) { EnableAutoDataReceive = false, ReadTimeout = 1000 };
await port.OpenAsync();

// Read all available data as string
string existing = port.ReadExisting();

// Read a single byte (-1 if none available)
int b = port.ReadByte();

// Read a single character (-1 if none available)
int ch = port.ReadChar();

// Read into a byte buffer
var buffer = new byte[64];
int bytesRead = port.Read(buffer, 0, buffer.Length);

// Read into a char buffer
var charBuffer = new char[64];
int charsRead = port.Read(charBuffer, 0, charBuffer.Length);

// Read until newline (respects NewLine property)
string line = port.ReadLine();

// Read until a specific delimiter
string data = port.ReadTo(">");
```

## Reading lines
Use ReadLineAsync to await a single complete line split by the configured NewLine. Supports single- and multi-character newline sequences and respects ReadTimeout (> 0).

```csharp
port.NewLine = "\r\n"; // optional: default is "\n"
var line = await port.ReadLineAsync();
Console.WriteLine($"Line: {line}");
```

You can also pass a CancellationToken:
```csharp
using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
var line = await port.ReadLineAsync(cts.Token);
```

### ReadToAsync
Read data up to a specific delimiter asynchronously:
```csharp
// Read until '>' delimiter
var data = await port.ReadToAsync(">");
Console.WriteLine($"Received: {data}");

// With cancellation
using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
var data = await port.ReadToAsync(">", cts.Token);
```

## Line streaming with Lines
Subscribe to Lines to get a continuous stream of complete lines:
```csharp
port.NewLine = "\n";
port.Lines.Subscribe(line => Console.WriteLine($"LINE: {line}"));
```

## Source-generated serial properties
The package includes a source generator that can turn serial protocol messages into strongly typed properties with classic and async observable streams. Mark a partial class with one or more `SerialPortReactiveStream` attributes, then connect it to an `ISerialPortRx`.

```csharp
using IoT.DriverCore.Serial;
using IoT.DriverCore.Serial.SourceGeneration;
using ReactiveUI.Primitives;
using ReactiveUI.Primitives.Async;

[SerialPortReactiveStream("Temperature", typeof(double), @"^TEMP:(?<value>-?\d+(\.\d+)?)$")]
[SerialPortReactiveStream("DeviceReady", typeof(bool), @"^READY:(?<value>0|1)$", IgnoreCase = true)]
public partial class DeviceState
{
}

var port = new SerialPortRx("COM3", 115200);
var state = new DeviceState();
using var generatedBindings = state.ConnectReactiveSerialPort(port);

state.TemperatureObservable.Subscribe(value => Console.WriteLine($"Temperature: {value}"));

await using var ready = await state.DeviceReadyObservableAsync
    .SubscribeAsync(value => Console.WriteLine($"Ready: {value}"));

await port.OpenAsync();
```

Generated members:
- `Temperature` and `DeviceReady` properties with private setters
- `TemperatureObservable` / `DeviceReadyObservable`
- `TemperatureObservableAsync` / `DeviceReadyObservableAsync`
- `ConnectReactiveSerialPort(ISerialPortRx serialPort)` to wire the generated bindings

By default, generated bindings listen to `ISerialPortRx.Lines`. Set `Source` to `SerialPortReactiveSource.DataReceived`, `DataReceivedBytes`, `BytesReceived`, or `IsOpen` when a property should be driven by a different stream.

## Writing
- `port.Write(string text)` - Write a string
- `port.WriteLine(string text)` - Write a string followed by NewLine
- `port.Write(byte[] buffer)` - Write entire byte array
- `port.Write(byte[] buffer, int offset, int count)` - Write portion of byte array
- `port.Write(char[] buffer)` - Write entire char array
- `port.Write(char[] buffer, int offset, int count)` - Write portion of char array

### Modern .NET Write Overloads (net8.0+)
On modern .NET targets, additional Span-based overloads are available:
```csharp
// Write from ReadOnlySpan<byte>
ReadOnlySpan<byte> data = stackalloc byte[] { 0x01, 0x02, 0x03 };
port.Write(data);

// Write from ReadOnlyMemory<byte>
ReadOnlyMemory<byte> memory = new byte[] { 0x01, 0x02, 0x03 };
port.Write(memory);

// Write from ReadOnlySpan<char>
ReadOnlySpan<char> chars = "Hello".AsSpan();
port.Write(chars);
```

## Error handling and state
- Subscribe to `port.ErrorReceived` for exceptions and serial errors.
- Subscribe to `port.IsOpenObservable` to react to open/close transitions.
- Call `port.Close()` or dispose subscriptions (DisposeWith) to release the port.

### Buffer Management
```csharp
// Discard pending input data
port.DiscardInBuffer();

// Discard pending output data
port.DiscardOutBuffer();

// Check buffer sizes
Console.WriteLine($"Bytes to read: {port.BytesToRead}");
Console.WriteLine($"Bytes to write: {port.BytesToWrite}");
```

### Windows-only: Pin Changed Events
On Windows targets, subscribe to pin state changes:
```csharp
#if HasWindows
port.PinChanged.Subscribe(args => 
    Console.WriteLine($"Pin changed: {args.EventType}"));
#endif
```

## TCP/UDP variants
The TcpClientRx and UdpClientRx classes implement the same IPortRx interface for a similar reactive experience with sockets.

TCP example:
```csharp
var tcp = new TcpClientRx("example.com", 80);
await tcp.OpenAsync();
var req = System.Text.Encoding.ASCII.GetBytes("GET / HTTP/1.1\r\nHost: example.com\r\n\r\n");
tcp.Write(req, 0, req.Length);
var buf = new byte[1024];
var n = await tcp.ReadAsync(buf, 0, buf.Length);
Console.WriteLine(System.Text.Encoding.ASCII.GetString(buf, 0, n));
```

UDP example:
```csharp
var udp = new UdpClientRx(12345);
await udp.OpenAsync();
var buf = new byte[16];
var n = await udp.ReadAsync(buf, 0, buf.Length);
Console.WriteLine($"UDP read {n} bytes");
```

### Batched receive (TCP/UDP)
Subscribe to batched byte arrays for throughput-sensitive pipelines:
```csharp
// TCP batched chunks per read loop
new TcpClientRx("example.com", 80).DataReceivedBatches
    .Subscribe(chunk => Console.WriteLine($"TCP chunk size: {chunk.Length}"));

// UDP per-datagram batches
new UdpClientRx(12345).DataReceivedBatches
    .Subscribe(datagram => Console.WriteLine($"UDP datagram size: {datagram.Length}"));
```

## Request/response coordination, deterministic testing, and generated values

`SerialPortRxMessageHandler` is the ownership boundary for line-oriented command devices. Construct it with an open-capable `ISerialPortRx` and any device error prefixes. `RequestAsync(command)` waits for the next non-echo response; the overload accepting `Action<string>` parses/applies that response before completing. It uses `ReadTimeout` (or three seconds) and faults/cancels on error lines or timeout. `ResponsePrefix` supports devices that prefix replies. `PollingTasks`, `StartPolling`, `StopPolling`, and `WithPollingStoppedAsync` let periodic polling coexist safely with exclusive commands. Dispose the handler to stop polling and unsubscribe from `Lines`.

```csharp
using IoT.DriverCore.Serial;

using var port = new SerialPortRx("COM3", 115200) { NewLine = "\r\n", ReadTimeout = 1500 };
using var handler = new SerialPortRxMessageHandler(port, "ERR", "ERROR")
{
    ResponsePrefix = "1",
};
await port.OpenAsync();

string? identity = null;
await handler.RequestAsync("ID?", response => identity = response);
Console.WriteLine(identity);

handler.PollingTasks = () => handler.RequestAsync("MEAS?");
handler.StartPolling();
await handler.WithPollingStoppedAsync(() => handler.RequestAsync("CALIBRATE"));
handler.StopPolling();
```

`PendingRequest` is the immutable record used by the handler's correlation queue; application code normally consumes the handler instead of constructing it. `InMemoryPortRxPair` provides two connected `SerialPortRx` endpoints (`First`/`Second`) plus deterministic error injection. Use it for unit/integration tests without COM hardware; dispose the pair to dispose both endpoints. The physical-serial runtime adapters are implementation details and are deliberately not part of the package's public application API.

```csharp
using IoT.DriverCore.Serial;

using var pair = new InMemoryPortRxPair("TEST-A", "TEST-B");
pair.First.NewLine = pair.Second.NewLine = "\n";
using var received = pair.Second.Lines.Subscribe(Console.WriteLine);
await pair.First.OpenAsync();
await pair.Second.OpenAsync();
pair.First.WriteLine("TEMP:21.5");
pair.InjectFirstError(new IOException("Simulated cable fault"));
```

The packaged analyzer emits the `SerialPortReactiveStream` attribute and `SerialPortReactiveSource` enum in `IoT.DriverCore.Serial.SourceGeneration`. A partial target class receives a typed property, classic `IObservable<T>`, `IObservableAsync<T>`, and `ConnectReactiveSerialPort(ISerialPortRx)`. Use the optional regex `Pattern`, named `GroupName`/numeric `GroupNumber`, `IgnoreCase`, and `Source` settings to select and parse a stream. `SerialPortReactiveValueConverter.TryConvertMatch<T>` is public for applying the identical parsing/conversion rules manually; it returns `false` rather than throwing for a non-match/unconvertible value.

```csharp
using IoT.DriverCore.Serial;
using IoT.DriverCore.Serial.SourceGeneration;

[SerialPortReactiveStream(
    "AlarmCode", typeof(int), @"^ALARM:(?<value>\d+)$",
    GroupName = "value", IgnoreCase = true)]
[SerialPortReactiveStream(
    "Open", typeof(bool), Source = SerialPortReactiveSource.IsOpen)]
public partial class HmiState;

var state = new HmiState();
using var binding = state.ConnectReactiveSerialPort(port);
using var alarms = state.AlarmCodeObservable.Subscribe(code => Console.WriteLine($"Alarm {code}"));

if (SerialPortReactiveValueConverter.TryConvertMatch(
        "ALARM:42", @"^ALARM:(?<value>\d+)$", "value", 1, false, out int code))
{
    Console.WriteLine(code);
}
```

## Combined workflows

### Combined workflow 1: framed command channel with exclusive configuration

This combines automatic line reception, a `BufferUntil` parser for unsolicited frames, a message handler for correlated replies, error/state observation, and polling suspension around an exclusive command. The same serial bytes must have one parser owner; both subscriptions here are line/observable consumers, not manual reads.

```csharp
using IoT.DriverCore.Serial;

using var port = new SerialPortRx("COM7", 57600) { NewLine = "\r\n", ReadTimeout = 2000 };
using var errors = port.ErrorReceived.Subscribe(Console.Error.WriteLine);
using var handler = new SerialPortRxMessageHandler(port, "ERR");
var start = SerialPortRxMixins.AsObservable((byte)'!');
var end = SerialPortRxMixins.AsObservable((byte)'\n');
using var unsolicited = SerialPortRxMixins.BufferUntil(port.DataReceived, start, end, 500)
    .Subscribe(frame => Console.WriteLine($"Event: {frame}"));

await port.OpenAsync();
handler.PollingTasks = () => handler.RequestAsync("STATUS?");
handler.StartPolling();
await handler.WithPollingStoppedAsync(() => handler.RequestAsync("SET MODE=SERVICE"));
```

### Combined workflow 2: TCP/UDP telemetry with batch framing and async processing

This combines network lifecycle, preserved batch boundaries, application framing, and the async-observable bridge. TCP chunks are arbitrary; accumulate them in a protocol parser. UDP batches are datagrams and can normally be handled one-at-a-time.

```csharp
using IoT.DriverCore.Serial;

using var tcp = new TcpClientRx();
tcp.Connect("192.168.10.15", 9000);
using var tcpErrors = tcp.DataReceivedBatches.Subscribe(chunk => ParseTcpBytes(chunk));
await tcp.OpenAsync();

using var udp = new UdpClientRx(5000) { EnableBroadcast = true };
using var datagrams = udp.DataReceivedBatches.Subscribe(ParseTelemetryDatagram);
await udp.OpenAsync();

var asyncBatches = tcp.DataReceivedBatches.ToAsyncObservable();
await using var logger = await asyncBatches.SubscribeAsync(
    (batch, cancellationToken) => LogBatchAsync(batch, cancellationToken));
```

## Complete public API reference

| Type/member family | Purpose, overloads, return/error/lifecycle behavior |
|---|---|
| `IPortRx` | Common `BytesReceived`, `InfiniteTimeout`, read/write timeout properties; `OpenAsync`, `Close`, `Dispose`, `DiscardInBuffer`, and `ReadAsync(byte[]?, offset, count)`/`Write(byte[]?, offset, count)`. The task returns count; I/O/timeout exceptions propagate. |
| `IReceiveBatchPortRx` | Adds `DataReceivedBatches`; batches are original serial receive chunks, TCP reads, or UDP datagrams depending on implementation. |
| `ISerialPortRx` | Serial configuration and hardware state properties; character/byte/line/error/open streams; `DiscardOutBuffer`; all string, byte-array, char-array, and modern span/memory `Write` overloads; `WriteLine`; synchronous `Read`/`ReadByte`/`ReadChar`/`ReadExisting`/`ReadLine`/`ReadTo`; cancelable `ReadLineAsync`/`ReadToAsync`; `StartDataReception()` and interval overload. Pin stream is conditional. |
| `SerialPortRx` | Constructors for port-only through baud/data/parity/stop/handshake; all interface members plus raw byte batches, `DataReceived*Async`, `BytesReceivedAsync`, `LinesAsync`, `ErrorReceivedAsync`, `IsOpenObservableAsync`, conditional `PinChangedAsync`, and static `PortNames()`, `PortNames(interval)`, `PortNames(interval, limit)`. Configure before open; dispose/cancel the returned reception lease. |
| `TcpClientRx` | Constructors from client, endpoint, address family, hostname/port; `Connect` overloads for host, IP, endpoint, IP array; `Client`, `Stream`, timeouts; classic/async byte and batch streams; common open/read/write/close/dispose. Do not infer messages from a TCP batch. |
| `UdpClientRx` | Constructors for client, local endpoint, port, address family, host/port; `Available`, `Ttl`, `DontFragment`, `MulticastLoopback`, `EnableBroadcast`, `ExclusiveAddressUse`, `Client`; `AllowNatTraversal`, three `Connect` overloads, `ReceiveAsync`, `SendAsync`, and common port API. Validate source/endpoints at the application layer. |
| `SerialPortRxMixins` | Four classic and four async `BufferUntil` overloads (default/explicit timeout and boundaries); async observable bridges for every interface stream; serial event observers; `WhileIsOpen` and async counterpart; `AsObservable`/`AsAsyncObservable` for byte/int/short delimiters; async `PortNames` overloads. Dispose every subscription/async subscription. |
| `ObservableAsync` and `ObservableAsyncBridgeExtensions` | `Return<T>`, `ToAsyncObservable<T>`, and `ToObservable<T>` bridge the classic and Primitives async models. Use a single model per pipeline boundary, then dispose the produced subscription. |
| `SerialPortRxMessageHandler` / `PendingRequest` | Constructor, `ResponsePrefix`, `PollingTasks`, both `RequestAsync` overloads, polling controls, `WithPollingStoppedAsync`, `SendCommandAsync`, `Dispose`; request cancellation follows read timeout and device error lines. |
| `InMemoryPortRxPair` | Creates deterministic paired `SerialPortRx` endpoints for tests without physical COM hardware. Own it with `using`/`Dispose`; the package deliberately keeps its transport/runtime adapters internal. |
| `SerialPortReactiveValueConverter` / `SerialPortReactiveStreamGenerator` | Public `TryConvertMatch<T>` parsing helper and the analyzer implementation that emits the internal attribute/enum and typed members. Resolve generator diagnostics instead of hand-editing emitted code. |

## Operations and troubleshooting

| Symptom | Corrective action |
|---|---|
| No serial data or open failure | Verify port ownership, baud/data bits/parity/stop bits/handshake, driver permissions, and subscribe to `ErrorReceived` before `OpenAsync`. |
| Lines/frames are missing or corrupt | Set `NewLine`/encoding before open, select automatic **or** manual receive, and use a single framing parser with a realistic timeout. |
| Request times out | Confirm device echoes/error prefixes and `ResponsePrefix`, configure `ReadTimeout`, and keep the message handler alive/subscribed. |
| TCP parser loses messages | Accumulate `DataReceivedBatches`; a TCP read may split or join protocol frames. |
| Generator output is absent | Reference the runtime package/analyzer, use a partial class, fix stream attribute metadata and regex group/type conversion. |
| Tests need hardware independence | Use `InMemoryPortRxPair` and error injection; reserve physical/virtual COM tests for explicit integration coverage. |

## Testing
The test suite uses TUnit on Microsoft.Testing.Platform. Run it with:

```bash
dotnet test src/SerialPortRx.Test/SerialPortRx.Tests.csproj -c Debug -f net8.0
```

Serial integration tests expect a virtual COM port pair named `COM1` and `COM2`. The source-generator tests do not require serial hardware.

## Threading and scheduling
- The DataReceived and other streams run on the underlying event threads. Use ObserveOn to marshal to a UI or a dedicated scheduler when needed.
- ReadAsync uses a lightweight lock and offloads blocking reads, avoiding CPU spin.

## Tips and best practices
- Subscribe before calling `OpenAsync()` to ensure you do not miss events.
- Tune Encoding (default ASCII), BaudRate, Parity, StopBits, and Handshake to match your device.
- Use BufferUntil for delimited protocols. For binary protocols, use ReadAsync with fixed sizes.
- Use Lines when dealing with text protocols; use ReadLineAsync when you need a one-shot line.
- Always dispose subscriptions (DisposeWith) and call Close() when done.

## Example program (complete)
```csharp
using System;
using System.Collections.Generic;
using System.Linq;
using IoT.DriverCore.Serial;
using ReactiveUI.Primitives;

internal static class Program
{
    private static async System.Threading.Tasks.Task Main()
    {
        const string comPortName = "COM1";
        const string dataToWrite = "DataToWrite";
        var rootDisposables = new List<IDisposable>();

        var startChar = SerialPortRxMixins.AsObservable(0x21); // '!'
        var endChar = SerialPortRxMixins.AsObservable(0x0a);   // '\n'

        var portDisposables = new List<IDisposable>();

        using var portNamesSubscription = SerialPortRx.PortNames().Subscribe(names =>
        {
            if (portDisposables.Count == 0 && names.Contains(comPortName))
            {
                var port = new SerialPortRx(comPortName, 9600);
                portDisposables.Add(port);

                portDisposables.Add(port.ErrorReceived.Subscribe(Console.WriteLine));
                portDisposables.Add(port.IsOpenObservable.Subscribe(open => Console.WriteLine($"{comPortName} {(open ? "Open" : "Closed")}")));

                portDisposables.Add(SerialPortRxMixins
                    .BufferUntil(port.DataReceived, startChar, endChar, 100)
                    .Subscribe(data => Console.WriteLine($"Data: {data}")));

                portDisposables.Add(SerialPortRxMixins.WhileIsOpen(port, TimeSpan.FromMilliseconds(500))
                    .Subscribe(_ => port.Write(dataToWrite)));

                port.OpenAsync().GetAwaiter().GetResult();
            }
            else if (!names.Contains(comPortName))
            {
                foreach (var disposable in portDisposables)
                {
                    disposable.Dispose();
                }

                portDisposables.Clear();
                Console.WriteLine($"Port {comPortName} Disposed");
            }
        });

        rootDisposables.Add(portNamesSubscription);

        Console.ReadLine();
        foreach (var disposable in portDisposables)
        {
            disposable.Dispose();
        }

        foreach (var disposable in rootDisposables)
        {
            disposable.Dispose();
        }
    }
}
```


## License

This project is licensed under the MIT License; see the repository [LICENSE](https://github.com/ChrisPulman/IoT-DriverCore/blob/main/LICENSE).

## Contributing

Contributions are welcome! Please feel free to submit a Pull Request. For major changes, please open an issue first to discuss what you would like to change.

## Sponsorship

If you find this library useful and would like to support its development, consider sponsoring the project on [GitHub Sponsors](https://github.com/sponsors/ChrisPulman).

---

**SerialPortRx** - reactive transport primitives for industrial automation.

## AI skill

Load [`skills/serial-port-rx/SKILL.md`](../../skills/serial-port-rx/SKILL.md) for an agent-oriented checklist. This README is the full API, configuration, and combined-workflow reference.

<!-- BEGIN GENERATED PUBLIC API -->

## Exhaustive public API reference

This catalogue is generated from the packaged runtime assemblies and their XML documentation. It includes exported public types and their declared public members; inherited members and non-public implementation details are intentionally omitted.

### `SerialPortRx`

Exported public types: 13; declared public members: 258.

#### `T:IoT.DriverCore.Serial.IPortRx`

```csharp
public interface IoT.DriverCore.Serial.IPortRx
```
Represents a reactive receive port.

##### Declared public members

###### `M:IoT.DriverCore.Serial.IPortRx.Close`

```csharp
public void Close()
```
Closes this instance.

###### `M:IoT.DriverCore.Serial.IPortRx.DiscardInBuffer`

```csharp
public void DiscardInBuffer()
```
Purges the receive buffer.

###### `M:IoT.DriverCore.Serial.IPortRx.OpenAsync`

```csharp
public System.Threading.Tasks.Task OpenAsync()
```
Opens this instance.

- Returns: A Task.

###### `M:IoT.DriverCore.Serial.IPortRx.ReadAsync(System.Byte[],System.Int32,System.Int32)`

```csharp
public System.Threading.Tasks.Task<int> ReadAsync(byte[] buffer, int offset, int count)
```
Reads bytes from the input buffer into a buffer segment.

- Parameter `buffer`: The byte array to write the input to.
- Parameter `offset`: The offset in the buffer array to begin writing.
- Parameter `count`: The number of bytes to read.
- Returns: The number of bytes read.

###### `M:IoT.DriverCore.Serial.IPortRx.Write(System.Byte[],System.Int32,System.Int32)`

```csharp
public void Write(byte[] buffer, int offset, int count)
```
Writes a buffer segment to the port.

- Parameter `buffer`: The byte array that contains the data to write to the port.
- Parameter `offset`: The offset in the buffer array to begin writing.
- Parameter `count`: The number of bytes to write.

###### `P:IoT.DriverCore.Serial.IPortRx.BytesReceived`

```csharp
public System.IObservable<int> BytesReceived { get; }
```
Gets the data received after opening the receive port.

- Value: The byte read as a stream.

###### `P:IoT.DriverCore.Serial.IPortRx.InfiniteTimeout`

```csharp
public int InfiniteTimeout { get; }
```
Gets indicates that no timeout should occur.

- Value: The `InfiniteTimeout` value.

###### `P:IoT.DriverCore.Serial.IPortRx.ReadTimeout`

```csharp
public int ReadTimeout { get; set; }
```
Gets or sets the read timeout in milliseconds.

- Value: The `ReadTimeout` value.

###### `P:IoT.DriverCore.Serial.IPortRx.WriteTimeout`

```csharp
public int WriteTimeout { get; set; }
```
Gets or sets the write timeout in milliseconds.

- Value: The `WriteTimeout` value.

#### `T:IoT.DriverCore.Serial.IReceiveBatchPortRx`

```csharp
public interface IoT.DriverCore.Serial.IReceiveBatchPortRx
```
Represents a receive port that publishes the original boundaries of received byte batches.

##### Declared public members

###### `P:IoT.DriverCore.Serial.IReceiveBatchPortRx.DataReceivedBatches`

```csharp
public System.IObservable<byte[]> DataReceivedBatches { get; }
```
Gets the raw byte batches received after opening the port.

- Value: The `DataReceivedBatches` value.

#### `T:IoT.DriverCore.Serial.ISerialPortRx`

```csharp
public interface IoT.DriverCore.Serial.ISerialPortRx
```
Serial Port Rx interface.

##### Declared public members

###### `M:IoT.DriverCore.Serial.ISerialPortRx.DiscardOutBuffer`

```csharp
public void DiscardOutBuffer()
```
Discards the out buffer.

###### `M:IoT.DriverCore.Serial.ISerialPortRx.Read(System.Byte[],System.Int32,System.Int32)`

```csharp
public int Read(byte[] buffer, int offset, int count)
```
Reads the specified buffer.

- Parameter `buffer`: The buffer.
- Parameter `offset`: The offset.
- Parameter `count`: The count.
- Returns: An integer.

###### `M:IoT.DriverCore.Serial.ISerialPortRx.Read(System.Char[],System.Int32,System.Int32)`

```csharp
public int Read(char[] buffer, int offset, int count)
```
Reads the specified buffer.

- Parameter `buffer`: The buffer.
- Parameter `offset`: The offset.
- Parameter `count`: The count.
- Returns: An integer.

###### `M:IoT.DriverCore.Serial.ISerialPortRx.ReadByte`

```csharp
public int ReadByte()
```
Reads the byte.

- Returns: An integer.

###### `M:IoT.DriverCore.Serial.ISerialPortRx.ReadChar`

```csharp
public int ReadChar()
```
Reads the character.

- Returns: An integer.

###### `M:IoT.DriverCore.Serial.ISerialPortRx.ReadExisting`

```csharp
public string ReadExisting()
```
Reads the existing.

- Returns: A string.

###### `M:IoT.DriverCore.Serial.ISerialPortRx.ReadLine`

```csharp
public string ReadLine()
```
Reads the line.

- Returns: A string.

###### `M:IoT.DriverCore.Serial.ISerialPortRx.ReadLineAsync`

```csharp
public System.Threading.Tasks.Task<string> ReadLineAsync()
```
Reads the line asynchronous.

- Returns: A Task of string.

###### `M:IoT.DriverCore.Serial.ISerialPortRx.ReadLineAsync(System.Threading.CancellationToken)`

```csharp
public System.Threading.Tasks.Task<string> ReadLineAsync(System.Threading.CancellationToken cancellationToken)
```
Reads the line asynchronous with cancellation and respecting ReadTimeout (> 0) as a timeout.

- Parameter `cancellationToken`: Cancellation token to cancel waiting.
- Returns: A Task of string.

###### `M:IoT.DriverCore.Serial.ISerialPortRx.ReadTo(System.String)`

```csharp
public string ReadTo(string value)
```
Reads to.

- Parameter `value`: The value.
- Returns: A string.

###### `M:IoT.DriverCore.Serial.ISerialPortRx.ReadToAsync(System.String)`

```csharp
public System.Threading.Tasks.Task<string> ReadToAsync(string value)
```
Reads a string up to the specified value asynchronously.

- Parameter `value`: The value to read up to.
- Returns: The contents of the input buffer up to the specified value.

###### `M:IoT.DriverCore.Serial.ISerialPortRx.ReadToAsync(System.String,System.Threading.CancellationToken)`

```csharp
public System.Threading.Tasks.Task<string> ReadToAsync(string value, System.Threading.CancellationToken cancellationToken)
```
Reads a string up to the specified value asynchronously.

- Parameter `value`: The value to read up to.
- Parameter `cancellationToken`: Cancellation token to cancel waiting.
- Returns: The contents of the input buffer up to the specified value.

###### `M:IoT.DriverCore.Serial.ISerialPortRx.StartDataReception`

```csharp
public System.IDisposable StartDataReception()
```
Starts continuous data reception that feeds both DataReceived and DataReceivedBytes observables. Call this after Open() to enable reactive data streaming.

- Returns: A disposable that stops the data reception when disposed.

###### `M:IoT.DriverCore.Serial.ISerialPortRx.StartDataReception(System.Int32)`

```csharp
public System.IDisposable StartDataReception(int pollingIntervalMs)
```
Starts continuous data reception that feeds both DataReceived and DataReceivedBytes observables. Call this after Open() to enable reactive data streaming.

- Parameter `pollingIntervalMs`: Polling interval in milliseconds (default: 10ms).
- Returns: A disposable that stops the data reception when disposed.

###### `M:IoT.DriverCore.Serial.ISerialPortRx.Write(System.Byte[])`

```csharp
public void Write(byte[] byteArray)
```
Writes the specified byte array.

- Parameter `byteArray`: The byte array.

###### `M:IoT.DriverCore.Serial.ISerialPortRx.Write(System.Char[])`

```csharp
public void Write(char[] charArray)
```
Writes the specified character array.

- Parameter `charArray`: The character array.

###### `M:IoT.DriverCore.Serial.ISerialPortRx.Write(System.Char[],System.Int32,System.Int32)`

```csharp
public void Write(char[] charArray, int offset, int count)
```
Writes the specified character array.

- Parameter `charArray`: The character array.
- Parameter `offset`: The offset.
- Parameter `count`: The count.

###### `M:IoT.DriverCore.Serial.ISerialPortRx.Write(System.ReadOnlyMemory`1{System.Byte})`

```csharp
public void Write(System.ReadOnlyMemory<byte> data)
```
Executes the `Write` operation.

- Parameter `data`: The `data` value.

###### `M:IoT.DriverCore.Serial.ISerialPortRx.Write(System.ReadOnlySpan`1{System.Byte})`

```csharp
public void Write(System.ReadOnlySpan<byte> data)
```
Executes the `Write` operation.

- Parameter `data`: The `data` value.

###### `M:IoT.DriverCore.Serial.ISerialPortRx.Write(System.ReadOnlySpan`1{System.Char})`

```csharp
public void Write(System.ReadOnlySpan<char> data)
```
Executes the `Write` operation.

- Parameter `data`: The `data` value.

###### `M:IoT.DriverCore.Serial.ISerialPortRx.Write(System.String)`

```csharp
public void Write(string text)
```
Writes the specified text.

- Parameter `text`: The text.

###### `M:IoT.DriverCore.Serial.ISerialPortRx.WriteLine(System.String)`

```csharp
public void WriteLine(string text)
```
Writes the line.

- Parameter `text`: The text.

###### `P:IoT.DriverCore.Serial.ISerialPortRx.BaudRate`

```csharp
public int BaudRate { get; set; }
```
Gets or sets the baud rate.

- Value: The baud rate.

###### `P:IoT.DriverCore.Serial.ISerialPortRx.BreakState`

```csharp
public bool BreakState { get; set; }
```
Gets or sets a value indicating whether break state.

- Value: The break state.

###### `P:IoT.DriverCore.Serial.ISerialPortRx.BytesToRead`

```csharp
public int BytesToRead { get; }
```
Gets the number of bytes of data in the receive buffer.

- Value: The bytes to read.

###### `P:IoT.DriverCore.Serial.ISerialPortRx.BytesToWrite`

```csharp
public int BytesToWrite { get; }
```
Gets the number of bytes of data in the send buffer.

- Value: The bytes to write.

###### `P:IoT.DriverCore.Serial.ISerialPortRx.CDHolding`

```csharp
public bool CDHolding { get; }
```
Gets a value indicating whether the Carrier Detect (CD) signal is on.

- Value: The CD holding.

###### `P:IoT.DriverCore.Serial.ISerialPortRx.CtsHolding`

```csharp
public bool CtsHolding { get; }
```
Gets a value indicating whether the Clear-to-Send (CTS) signal is on.

- Value: The CTS holding.

###### `P:IoT.DriverCore.Serial.ISerialPortRx.DataBits`

```csharp
public int DataBits { get; set; }
```
Gets or sets the data bits.

- Value: The data bits.

###### `P:IoT.DriverCore.Serial.ISerialPortRx.DataReceived`

```csharp
public System.IObservable<char> DataReceived { get; }
```
Gets the data received as characters.

- Value: The data received.

###### `P:IoT.DriverCore.Serial.ISerialPortRx.DataReceivedBytes`

```csharp
public System.IObservable<byte> DataReceivedBytes { get; }
```
Gets the raw bytes received from the serial port.

- Value: The raw bytes received.

###### `P:IoT.DriverCore.Serial.ISerialPortRx.DiscardNull`

```csharp
public bool DiscardNull { get; set; }
```
Gets or sets a value indicating whether null bytes are ignored when transmitted between the port and the receive buffer.

- Value: The discard null.

###### `P:IoT.DriverCore.Serial.ISerialPortRx.DsrHolding`

```csharp
public bool DsrHolding { get; }
```
Gets a value indicating whether the Data Set Ready (DSR) signal is on.

- Value: The DSR holding.

###### `P:IoT.DriverCore.Serial.ISerialPortRx.DtrEnable`

```csharp
public bool DtrEnable { get; set; }
```
Gets or sets whether the Data Terminal Ready (DTR) signal is enabled.

- Value: The DTR enable.

###### `P:IoT.DriverCore.Serial.ISerialPortRx.EnableAutoDataReceive`

```csharp
public bool EnableAutoDataReceive { get; set; }
```
Gets or sets a value indicating whether to automatically consume received data and feed it to the DataReceived and DataReceivedBytes observables. Set to false if you want to use synchronous Read methods instead. Must be set before calling Open().

- Value: True to enable automatic data reception (default), false to use sync reads.

###### `P:IoT.DriverCore.Serial.ISerialPortRx.Encoding`

```csharp
public System.Text.Encoding Encoding { get; set; }
```
Gets or sets the encoding.

- Value: The encoding.

###### `P:IoT.DriverCore.Serial.ISerialPortRx.ErrorReceived`

```csharp
public System.IObservable<System.Exception> ErrorReceived { get; }
```
Gets the error received.

- Value: The error received.

###### `P:IoT.DriverCore.Serial.ISerialPortRx.Handshake`

```csharp
public System.IO.Ports.Handshake Handshake { get; set; }
```
Gets or sets the handshake.

- Value: The handshake.

###### `P:IoT.DriverCore.Serial.ISerialPortRx.IsDisposed`

```csharp
public bool IsDisposed { get; }
```
Gets a value indicating whether this instance is disposed.

- Value: true if this instance is disposed; otherwise, false .

###### `P:IoT.DriverCore.Serial.ISerialPortRx.IsOpen`

```csharp
public bool IsOpen { get; }
```
Gets a value indicating whether gets the is open.

- Value: The is open.

###### `P:IoT.DriverCore.Serial.ISerialPortRx.IsOpenObservable`

```csharp
public System.IObservable<bool> IsOpenObservable { get; }
```
Gets the is open observable.

- Value: The is open observable.

###### `P:IoT.DriverCore.Serial.ISerialPortRx.Lines`

```csharp
public System.IObservable<string> Lines { get; }
```
Gets a lazily-created observable sequence of complete lines split by the NewLine sequence.

- Value: The `Lines` value.

###### `P:IoT.DriverCore.Serial.ISerialPortRx.NewLine`

```csharp
public string NewLine { get; set; }
```
Gets or sets the new line.

- Value: The new line.

###### `P:IoT.DriverCore.Serial.ISerialPortRx.Parity`

```csharp
public System.IO.Ports.Parity Parity { get; set; }
```
Gets or sets the parity.

- Value: The parity.

###### `P:IoT.DriverCore.Serial.ISerialPortRx.ParityReplace`

```csharp
public byte ParityReplace { get; set; }
```
Gets or sets the parity replace.

- Value: The parity replace.

###### `P:IoT.DriverCore.Serial.ISerialPortRx.PortName`

```csharp
public string PortName { get; set; }
```
Gets or sets the port.

- Value: The port.

###### `P:IoT.DriverCore.Serial.ISerialPortRx.ReadBufferSize`

```csharp
public int ReadBufferSize { get; set; }
```
Gets or sets the size of the read buffer.

- Value: The size of the read buffer.

###### `P:IoT.DriverCore.Serial.ISerialPortRx.ReceivedBytesThreshold`

```csharp
public int ReceivedBytesThreshold { get; set; }
```
Gets or sets the byte threshold that raises DataReceived.

- Value: The received bytes threshold.

###### `P:IoT.DriverCore.Serial.ISerialPortRx.RtsEnable`

```csharp
public bool RtsEnable { get; set; }
```
Gets or sets whether the Request to Send (RTS) signal is enabled.

- Value: The RTS enable.

###### `P:IoT.DriverCore.Serial.ISerialPortRx.StopBits`

```csharp
public System.IO.Ports.StopBits StopBits { get; set; }
```
Gets or sets the stop bits.

- Value: The stop bits.

###### `P:IoT.DriverCore.Serial.ISerialPortRx.WriteBufferSize`

```csharp
public int WriteBufferSize { get; set; }
```
Gets or sets the size of the write buffer.

- Value: The size of the write buffer.

#### `T:IoT.DriverCore.Serial.InMemoryPortRxPair`

```csharp
public class IoT.DriverCore.Serial.InMemoryPortRxPair
```
Owns two deterministic, connected `T:IoT.DriverCore.Serial.SerialPortRx` instances that exercise the normal serial wrapper without requiring physical or virtual serial hardware.

##### Declared public members

###### `M:IoT.DriverCore.Serial.InMemoryPortRxPair.#ctor`

```csharp
public IoT.DriverCore.Serial.InMemoryPortRxPair()
```
Initializes a new instance of the `T:IoT.DriverCore.Serial.InMemoryPortRxPair` class.

###### `M:IoT.DriverCore.Serial.InMemoryPortRxPair.#ctor(System.String,System.String)`

```csharp
public IoT.DriverCore.Serial.InMemoryPortRxPair(string firstPortName, string secondPortName)
```
Initializes a new instance of the `T:IoT.DriverCore.Serial.InMemoryPortRxPair` class.

- Parameter `firstPortName`: The diagnostic name of the first endpoint.
- Parameter `secondPortName`: The diagnostic name of the second endpoint.

###### `M:IoT.DriverCore.Serial.InMemoryPortRxPair.Dispose`

```csharp
public void Dispose()
```
Inherits XML documentation from its implemented or overridden member.

###### `M:IoT.DriverCore.Serial.InMemoryPortRxPair.InjectFirstError(System.Exception)`

```csharp
public void InjectFirstError(System.Exception exception)
```
Injects a deterministic connection error into the first endpoint.

- Parameter `exception`: The error to publish.

###### `M:IoT.DriverCore.Serial.InMemoryPortRxPair.InjectSecondError(System.Exception)`

```csharp
public void InjectSecondError(System.Exception exception)
```
Injects a deterministic connection error into the second endpoint.

- Parameter `exception`: The error to publish.

###### `P:IoT.DriverCore.Serial.InMemoryPortRxPair.First`

```csharp
public IoT.DriverCore.Serial.SerialPortRx First { get; }
```
Gets the first connected serial endpoint.

- Value: The `First` value.

###### `P:IoT.DriverCore.Serial.InMemoryPortRxPair.Second`

```csharp
public IoT.DriverCore.Serial.SerialPortRx Second { get; }
```
Gets the second connected serial endpoint.

- Value: The `Second` value.

#### `T:IoT.DriverCore.Serial.ObservableAsync`

```csharp
public class IoT.DriverCore.Serial.ObservableAsync
```
Compatibility factory for async observables.

##### Declared public members

###### `M:IoT.DriverCore.Serial.ObservableAsync.Return``1(``0)`

```csharp
public static ReactiveUI.Primitives.Async.IObservableAsync<T> Return<T>(T value)
```
Creates an async observable that emits a single value.

- Parameter `value`: The value to emit.
- Returns: An async observable that emits `value` .

#### `T:IoT.DriverCore.Serial.ObservableAsyncBridgeExtensions`

```csharp
public class IoT.DriverCore.Serial.ObservableAsyncBridgeExtensions
```
Compatibility bridge between classic observables and ReactiveUI.Primitives async observables.

##### Declared public members

###### `M:IoT.DriverCore.Serial.ObservableAsyncBridgeExtensions.ToAsyncObservable``1(System.IObservable`1{``0})`

```csharp
public static ReactiveUI.Primitives.Async.IObservableAsync<T> ToAsyncObservable<T>(System.IObservable<T> source)
```
Executes the `ToAsyncObservable` operation.

- Parameter `source`: The `source` value.
- Returns: A `ReactiveUI.Primitives.Async.IObservableAsync<T>` result.

###### `M:IoT.DriverCore.Serial.ObservableAsyncBridgeExtensions.ToObservable``1(ReactiveUI.Primitives.Async.IObservableAsync`1{``0})`

```csharp
public static System.IObservable<T> ToObservable<T>(ReactiveUI.Primitives.Async.IObservableAsync<T> source)
```
Executes the `ToObservable` operation.

- Parameter `source`: The `source` value.
- Returns: A `System.IObservable<T>` result.

#### `T:IoT.DriverCore.Serial.PendingRequest`

```csharp
public class IoT.DriverCore.Serial.PendingRequest
```
Represents a pending command request awaiting a serial response.

##### Declared public members

###### `M:IoT.DriverCore.Serial.PendingRequest.#ctor(System.String,System.Action`1{System.String},System.Threading.Tasks.TaskCompletionSource`1{System.Boolean})`

```csharp
public IoT.DriverCore.Serial.PendingRequest(string Command, System.Action<string> Apply, System.Threading.Tasks.TaskCompletionSource<bool> Completion)
```
Initializes a new instance of `IoT.DriverCore.Serial.PendingRequest`.

- Parameter `Command`: The `Command` value.
- Parameter `Apply`: The `Apply` value.
- Parameter `Completion`: The `Completion` value.

###### `M:IoT.DriverCore.Serial.PendingRequest.Deconstruct(System.String@,System.Action`1{System.String}@,System.Threading.Tasks.TaskCompletionSource`1{System.Boolean}@)`

```csharp
public void Deconstruct(out string Command, out System.Action<string> Apply, out System.Threading.Tasks.TaskCompletionSource<bool> Completion)
```
Deconstructs the value into its component values.

- Parameter `Command`: The `Command` value.
- Parameter `Apply`: The `Apply` value.
- Parameter `Completion`: The `Completion` value.

###### `M:IoT.DriverCore.Serial.PendingRequest.Equals(IoT.DriverCore.Serial.PendingRequest)`

```csharp
public bool Equals(IoT.DriverCore.Serial.PendingRequest other)
```
Determines whether the supplied value is equal to the current value.

- Parameter `other`: The `other` value.
- Returns: A `bool` result.

###### `M:IoT.DriverCore.Serial.PendingRequest.Equals(System.Object)`

```csharp
public bool Equals(object obj)
```
Determines whether the supplied value is equal to the current value.

- Parameter `obj`: The `obj` value.
- Returns: A `bool` result.

###### `M:IoT.DriverCore.Serial.PendingRequest.GetHashCode`

```csharp
public int GetHashCode()
```
Returns the hash code for the current value.

- Returns: A `int` result.

###### `M:IoT.DriverCore.Serial.PendingRequest.ToString`

```csharp
public string ToString()
```
Returns a string representation of the current value.

- Returns: A `string` result.

###### `M:IoT.DriverCore.Serial.PendingRequest.op_Equality(IoT.DriverCore.Serial.PendingRequest,IoT.DriverCore.Serial.PendingRequest)`

```csharp
public static bool op_Equality(IoT.DriverCore.Serial.PendingRequest left, IoT.DriverCore.Serial.PendingRequest right)
```
Determines whether the two supplied values are equal.

- Parameter `left`: The `left` value.
- Parameter `right`: The `right` value.
- Returns: A `bool` result.

###### `M:IoT.DriverCore.Serial.PendingRequest.op_Inequality(IoT.DriverCore.Serial.PendingRequest,IoT.DriverCore.Serial.PendingRequest)`

```csharp
public static bool op_Inequality(IoT.DriverCore.Serial.PendingRequest left, IoT.DriverCore.Serial.PendingRequest right)
```
Determines whether the two supplied values are not equal.

- Parameter `left`: The `left` value.
- Parameter `right`: The `right` value.
- Returns: A `bool` result.

###### `P:IoT.DriverCore.Serial.PendingRequest.Apply`

```csharp
public System.Action<string> Apply { get; set; }
```
The action that applies the response payload.

- Value: The `Apply` value.

###### `P:IoT.DriverCore.Serial.PendingRequest.Command`

```csharp
public string Command { get; set; }
```
The command text sent to the serial port.

- Value: The `Command` value.

###### `P:IoT.DriverCore.Serial.PendingRequest.Completion`

```csharp
public System.Threading.Tasks.TaskCompletionSource<bool> Completion { get; set; }
```
The completion source signaled when a response arrives.

- Value: The `Completion` value.

#### `T:IoT.DriverCore.Serial.SerialPortRx`

```csharp
public class IoT.DriverCore.Serial.SerialPortRx
```
Implements a cohesive portion of the reactive serial port.

##### Declared public members

###### `M:IoT.DriverCore.Serial.SerialPortRx.#ctor`

```csharp
public IoT.DriverCore.Serial.SerialPortRx()
```
Initializes a new instance of the `T:IoT.DriverCore.Serial.SerialPortRx` class.

###### `M:IoT.DriverCore.Serial.SerialPortRx.#ctor(System.String)`

```csharp
public IoT.DriverCore.Serial.SerialPortRx(string port)
```
Initializes a new instance of the `T:IoT.DriverCore.Serial.SerialPortRx` class.

- Parameter `port`: The port.

###### `M:IoT.DriverCore.Serial.SerialPortRx.#ctor(System.String,System.Int32)`

```csharp
public IoT.DriverCore.Serial.SerialPortRx(string port, int baudRate)
```
Initializes a new instance of the `T:IoT.DriverCore.Serial.SerialPortRx` class.

- Parameter `port`: The port.
- Parameter `baudRate`: The baud rate.

###### `M:IoT.DriverCore.Serial.SerialPortRx.#ctor(System.String,System.Int32,System.Int32)`

```csharp
public IoT.DriverCore.Serial.SerialPortRx(string port, int baudRate, int dataBits)
```
Initializes a new instance of the `T:IoT.DriverCore.Serial.SerialPortRx` class.

- Parameter `port`: The port.
- Parameter `baudRate`: The baud rate.
- Parameter `dataBits`: The data bits.

###### `M:IoT.DriverCore.Serial.SerialPortRx.#ctor(System.String,System.Int32,System.Int32,System.IO.Ports.Parity)`

```csharp
public IoT.DriverCore.Serial.SerialPortRx(string port, int baudRate, int dataBits, System.IO.Ports.Parity parity)
```
Initializes a new instance of the `T:IoT.DriverCore.Serial.SerialPortRx` class.

- Parameter `port`: The port.
- Parameter `baudRate`: The baud rate.
- Parameter `dataBits`: The data bits.
- Parameter `parity`: The parity.

###### `M:IoT.DriverCore.Serial.SerialPortRx.#ctor(System.String,System.Int32,System.Int32,System.IO.Ports.Parity,System.IO.Ports.StopBits)`

```csharp
public IoT.DriverCore.Serial.SerialPortRx(string port, int baudRate, int dataBits, System.IO.Ports.Parity parity, System.IO.Ports.StopBits stopBits)
```
Initializes a new instance of the `T:IoT.DriverCore.Serial.SerialPortRx` class.

- Parameter `port`: The port.
- Parameter `baudRate`: The baud rate.
- Parameter `dataBits`: The data bits.
- Parameter `parity`: The parity.
- Parameter `stopBits`: The stop bits.

###### `M:IoT.DriverCore.Serial.SerialPortRx.#ctor(System.String,System.Int32,System.Int32,System.IO.Ports.Parity,System.IO.Ports.StopBits,System.IO.Ports.Handshake)`

```csharp
public IoT.DriverCore.Serial.SerialPortRx(string port, int baudRate, int dataBits, System.IO.Ports.Parity parity, System.IO.Ports.StopBits stopBits, System.IO.Ports.Handshake handshake)
```
Initializes a new instance of the `T:IoT.DriverCore.Serial.SerialPortRx` class.

- Parameter `port`: The port.
- Parameter `baudRate`: The baud rate.
- Parameter `dataBits`: The data bits.
- Parameter `parity`: The parity.
- Parameter `stopBits`: The stop bits.
- Parameter `handshake`: The handshake.

###### `M:IoT.DriverCore.Serial.SerialPortRx.Close`

```csharp
public void Close()
```
Closes this instance.

###### `M:IoT.DriverCore.Serial.SerialPortRx.DiscardInBuffer`

```csharp
public void DiscardInBuffer()
```
Discards the in buffer.

###### `M:IoT.DriverCore.Serial.SerialPortRx.DiscardOutBuffer`

```csharp
public void DiscardOutBuffer()
```
Discards the out buffer.

###### `M:IoT.DriverCore.Serial.SerialPortRx.Dispose`

```csharp
public void Dispose()
```
Releases owned resources.

###### `M:IoT.DriverCore.Serial.SerialPortRx.OpenAsync`

```csharp
public System.Threading.Tasks.Task OpenAsync()
```
Opens this instance.

- Returns: A Task.

###### `M:IoT.DriverCore.Serial.SerialPortRx.PortNames`

```csharp
public static System.IObservable<string[]> PortNames()
```
Gets the port names using the default polling interval.

- Returns: Observable string.

###### `M:IoT.DriverCore.Serial.SerialPortRx.PortNames(System.Int32)`

```csharp
public static System.IObservable<string[]> PortNames(int pollInterval)
```
Gets the port names.

- Parameter `pollInterval`: The poll interval.
- Returns: Observable string.

###### `M:IoT.DriverCore.Serial.SerialPortRx.PortNames(System.Int32,System.Int32)`

```csharp
public static System.IObservable<string[]> PortNames(int pollInterval, int pollLimit)
```
Gets the port names.

- Parameter `pollInterval`: The poll interval.
- Parameter `pollLimit`: The poll limit, once number is reached observable will complete.
- Returns: Observable string.
- Value: The port names.

###### `M:IoT.DriverCore.Serial.SerialPortRx.Read(System.Byte[],System.Int32,System.Int32)`

```csharp
public int Read(byte[] buffer, int offset, int count)
```
Reads bytes from the SerialPort input buffer into a byte array at the specified offset.

- Parameter `buffer`: The byte array to write the input to.
- Parameter `offset`: The offset in the buffer array to begin writing.
- Parameter `count`: The number of bytes to read.
- Returns: The number of bytes read.

###### `M:IoT.DriverCore.Serial.SerialPortRx.Read(System.Char[],System.Int32,System.Int32)`

```csharp
public int Read(char[] buffer, int offset, int count)
```
Reads characters from the SerialPort input buffer into a character array.

- Parameter `buffer`: The character array to write the input to.
- Parameter `offset`: The offset in the buffer array to begin writing.
- Parameter `count`: The number of characters to read.
- Returns: The number of characters read.

###### `M:IoT.DriverCore.Serial.SerialPortRx.ReadAsync(System.Byte[],System.Int32,System.Int32)`

```csharp
public System.Threading.Tasks.Task<int> ReadAsync(byte[] buffer, int offset, int count)
```
Reads the specified buffer.

- Parameter `buffer`: The buffer.
- Parameter `offset`: The offset.
- Parameter `count`: The count.
- Returns: The number of bytes read.

###### `M:IoT.DriverCore.Serial.SerialPortRx.ReadByte`

```csharp
public int ReadByte()
```
Synchronously reads one byte from the SerialPort input buffer.

- Returns: The byte, or -1 if no byte is available.

###### `M:IoT.DriverCore.Serial.SerialPortRx.ReadChar`

```csharp
public int ReadChar()
```
Synchronously reads one character from the SerialPort input buffer.

- Returns: The character, or -1 if no character is available.

###### `M:IoT.DriverCore.Serial.SerialPortRx.ReadExisting`

```csharp
public string ReadExisting()
```
Reads all immediately available encoded bytes from the SerialPort stream and input buffer.

- Returns: The contents of the input buffer and the stream.

###### `M:IoT.DriverCore.Serial.SerialPortRx.ReadLine`

```csharp
public string ReadLine()
```
Reads up to the NewLine value in the input buffer.

- Returns: The contents of the input buffer up to the first occurrence of a NewLine value.

###### `M:IoT.DriverCore.Serial.SerialPortRx.ReadLineAsync`

```csharp
public System.Threading.Tasks.Task<string> ReadLineAsync()
```
Reads the line asynchronous.

- Returns: A `T:System.Threading.Tasks.Task` representing the asynchronous operation.

###### `M:IoT.DriverCore.Serial.SerialPortRx.ReadLineAsync(System.Threading.CancellationToken)`

```csharp
public System.Threading.Tasks.Task<string> ReadLineAsync(System.Threading.CancellationToken cancellationToken)
```
Reads the line asynchronous with cancellation and respecting ReadTimeout (> 0) as a timeout.

- Parameter `cancellationToken`: Cancellation token to cancel waiting.
- Returns: A Task of string.

###### `M:IoT.DriverCore.Serial.SerialPortRx.ReadTo(System.String)`

```csharp
public string ReadTo(string value)
```
Reads a string up to the specified value in the input buffer.

- Parameter `value`: The value to read up to.
- Returns: The contents of the input buffer up to the specified value.

###### `M:IoT.DriverCore.Serial.SerialPortRx.ReadToAsync(System.String)`

```csharp
public System.Threading.Tasks.Task<string> ReadToAsync(string value)
```
Reads a string up to the specified value asynchronously.

- Parameter `value`: The value to read up to.
- Returns: The contents of the input buffer up to the specified value.

###### `M:IoT.DriverCore.Serial.SerialPortRx.ReadToAsync(System.String,System.Threading.CancellationToken)`

```csharp
public System.Threading.Tasks.Task<string> ReadToAsync(string value, System.Threading.CancellationToken cancellationToken)
```
Reads a string up to the specified value asynchronously.

- Parameter `value`: The value to read up to.
- Parameter `cancellationToken`: Cancellation token to cancel waiting.
- Returns: The contents of the input buffer up to the specified value.

###### `M:IoT.DriverCore.Serial.SerialPortRx.StartDataReception`

```csharp
public System.IDisposable StartDataReception()
```
Starts continuous data reception that feeds both DataReceived and DataReceivedBytes observables. Call this after Open() to enable reactive data streaming.

- Returns: A disposable that stops the data reception when disposed.

###### `M:IoT.DriverCore.Serial.SerialPortRx.StartDataReception(System.Int32)`

```csharp
public System.IDisposable StartDataReception(int pollingIntervalMs)
```
Starts continuous data reception that feeds both DataReceived and DataReceivedBytes observables. Call this after Open() to enable reactive data streaming.

- Parameter `pollingIntervalMs`: Polling interval in milliseconds (default: 10ms).
- Returns: A disposable that stops the data reception when disposed.

###### `M:IoT.DriverCore.Serial.SerialPortRx.Write(System.Byte[])`

```csharp
public void Write(byte[] byteArray)
```
Writes the specified byte array.

- Parameter `byteArray`: The byte array.

###### `M:IoT.DriverCore.Serial.SerialPortRx.Write(System.Byte[],System.Int32,System.Int32)`

```csharp
public void Write(byte[] buffer, int offset, int count)
```
Writes the specified byte array.

- Parameter `buffer`: The byte array.
- Parameter `offset`: The offset.
- Parameter `count`: The count.

###### `M:IoT.DriverCore.Serial.SerialPortRx.Write(System.Char[])`

```csharp
public void Write(char[] charArray)
```
Writes the specified character array.

- Parameter `charArray`: The character array.

###### `M:IoT.DriverCore.Serial.SerialPortRx.Write(System.Char[],System.Int32,System.Int32)`

```csharp
public void Write(char[] charArray, int offset, int count)
```
Writes the specified character array.

- Parameter `charArray`: The character array.
- Parameter `offset`: The offset.
- Parameter `count`: The count.

###### `M:IoT.DriverCore.Serial.SerialPortRx.Write(System.ReadOnlyMemory`1{System.Byte})`

```csharp
public void Write(System.ReadOnlyMemory<byte> data)
```
Executes the `Write` operation.

- Parameter `data`: The `data` value.

###### `M:IoT.DriverCore.Serial.SerialPortRx.Write(System.ReadOnlySpan`1{System.Byte})`

```csharp
public void Write(System.ReadOnlySpan<byte> data)
```
Executes the `Write` operation.

- Parameter `data`: The `data` value.

###### `M:IoT.DriverCore.Serial.SerialPortRx.Write(System.ReadOnlySpan`1{System.Char})`

```csharp
public void Write(System.ReadOnlySpan<char> data)
```
Executes the `Write` operation.

- Parameter `data`: The `data` value.

###### `M:IoT.DriverCore.Serial.SerialPortRx.Write(System.String)`

```csharp
public void Write(string text)
```
Writes the specified text.

- Parameter `text`: The text.

###### `M:IoT.DriverCore.Serial.SerialPortRx.WriteLine(System.String)`

```csharp
public void WriteLine(string text)
```
Writes the line.

- Parameter `text`: The text.

###### `P:IoT.DriverCore.Serial.SerialPortRx.BaudRate`

```csharp
public int BaudRate { get; set; }
```
Gets or sets the baud rate.

- Value: The baud rate.

###### `P:IoT.DriverCore.Serial.SerialPortRx.BreakState`

```csharp
public bool BreakState { get; set; }
```
Gets or sets a value indicating whether break state.

- Value: The break state.

###### `P:IoT.DriverCore.Serial.SerialPortRx.BytesReceived`

```csharp
public System.IObservable<int> BytesReceived { get; }
```
Gets the data received when executing ReadAsync.

- Value: The data received.

###### `P:IoT.DriverCore.Serial.SerialPortRx.BytesReceivedAsync`

```csharp
public ReactiveUI.Primitives.Async.IObservableAsync<int> BytesReceivedAsync { get; }
```
Gets the data received when executing ReadAsync via an async observable.

- Value: The data received.

###### `P:IoT.DriverCore.Serial.SerialPortRx.BytesToRead`

```csharp
public int BytesToRead { get; }
```
Gets the number of bytes of data in the receive buffer.

- Value: The bytes to read.

###### `P:IoT.DriverCore.Serial.SerialPortRx.BytesToWrite`

```csharp
public int BytesToWrite { get; }
```
Gets the number of bytes of data in the send buffer.

- Value: The bytes to write.

###### `P:IoT.DriverCore.Serial.SerialPortRx.CDHolding`

```csharp
public bool CDHolding { get; }
```
Gets a value indicating whether the Carrier Detect (CD) signal is on.

- Value: The CD holding.

###### `P:IoT.DriverCore.Serial.SerialPortRx.CtsHolding`

```csharp
public bool CtsHolding { get; }
```
Gets a value indicating whether the Clear-to-Send (CTS) signal is on.

- Value: The CTS holding.

###### `P:IoT.DriverCore.Serial.SerialPortRx.DataBits`

```csharp
public int DataBits { get; set; }
```
Gets or sets the data bits.

- Value: The data bits.

###### `P:IoT.DriverCore.Serial.SerialPortRx.DataReceived`

```csharp
public System.IObservable<char> DataReceived { get; }
```
Gets the data received as characters.

- Value: The data received.

###### `P:IoT.DriverCore.Serial.SerialPortRx.DataReceivedAsync`

```csharp
public ReactiveUI.Primitives.Async.IObservableAsync<char> DataReceivedAsync { get; }
```
Gets the data received as characters via an async observable.

- Value: The data received.

###### `P:IoT.DriverCore.Serial.SerialPortRx.DataReceivedBatches`

```csharp
public System.IObservable<byte[]> DataReceivedBatches { get; }
```
Gets raw byte batches received from the serial port.

- Value: The raw byte batches received.

###### `P:IoT.DriverCore.Serial.SerialPortRx.DataReceivedBytes`

```csharp
public System.IObservable<byte> DataReceivedBytes { get; }
```
Gets the raw bytes received from the serial port.

- Value: The raw bytes received.

###### `P:IoT.DriverCore.Serial.SerialPortRx.DataReceivedBytesAsync`

```csharp
public ReactiveUI.Primitives.Async.IObservableAsync<byte> DataReceivedBytesAsync { get; }
```
Gets the raw bytes received from the serial port via an async observable.

- Value: The raw bytes received.

###### `P:IoT.DriverCore.Serial.SerialPortRx.DiscardNull`

```csharp
public bool DiscardNull { get; set; }
```
Gets or sets a value indicating whether null bytes are ignored when transmitted between the port and the receive buffer.

- Value: The discard null.

###### `P:IoT.DriverCore.Serial.SerialPortRx.DsrHolding`

```csharp
public bool DsrHolding { get; }
```
Gets a value indicating whether the Data Set Ready (DSR) signal is on.

- Value: The DSR holding.

###### `P:IoT.DriverCore.Serial.SerialPortRx.DtrEnable`

```csharp
public bool DtrEnable { get; set; }
```
Gets or sets whether the Data Terminal Ready (DTR) signal is enabled.

- Value: The DTR enable.

###### `P:IoT.DriverCore.Serial.SerialPortRx.EnableAutoDataReceive`

```csharp
public bool EnableAutoDataReceive { get; set; }
```
Gets or sets a value indicating whether to automatically consume received data and feed it to the DataReceived and DataReceivedBytes observables. Set to false if you want to use synchronous Read methods instead. Must be set before calling Open().

- Value: True to enable automatic data reception (default), false to use sync reads.

###### `P:IoT.DriverCore.Serial.SerialPortRx.Encoding`

```csharp
public System.Text.Encoding Encoding { get; set; }
```
Gets or sets the encoding.

- Value: The encoding.

###### `P:IoT.DriverCore.Serial.SerialPortRx.ErrorReceived`

```csharp
public System.IObservable<System.Exception> ErrorReceived { get; }
```
Gets the error received.

- Value: The error received.

###### `P:IoT.DriverCore.Serial.SerialPortRx.ErrorReceivedAsync`

```csharp
public ReactiveUI.Primitives.Async.IObservableAsync<System.Exception> ErrorReceivedAsync { get; }
```
Gets the error received via an async observable.

- Value: The error received.

###### `P:IoT.DriverCore.Serial.SerialPortRx.Handshake`

```csharp
public System.IO.Ports.Handshake Handshake { get; set; }
```
Gets or sets the handshake.

- Value: The handshake.

###### `P:IoT.DriverCore.Serial.SerialPortRx.InfiniteTimeout`

```csharp
public int InfiniteTimeout { get; }
```
Gets indicates that no timeout should occur.

- Value: The `InfiniteTimeout` value.

###### `P:IoT.DriverCore.Serial.SerialPortRx.IsDisposed`

```csharp
public bool IsDisposed { get; }
```
Gets a value indicating whether this instance is disposed.

- Value: true if this instance is disposed; otherwise, false .

###### `P:IoT.DriverCore.Serial.SerialPortRx.IsOpen`

```csharp
public bool IsOpen { get; }
```
Gets a value indicating whether gets the is open.

- Value: The is open.

###### `P:IoT.DriverCore.Serial.SerialPortRx.IsOpenObservable`

```csharp
public System.IObservable<bool> IsOpenObservable { get; }
```
Gets the is open observable.

- Value: The is open observable.

###### `P:IoT.DriverCore.Serial.SerialPortRx.IsOpenObservableAsync`

```csharp
public ReactiveUI.Primitives.Async.IObservableAsync<bool> IsOpenObservableAsync { get; }
```
Gets the is open async observable.

- Value: The is open async observable.

###### `P:IoT.DriverCore.Serial.SerialPortRx.Lines`

```csharp
public System.IObservable<string> Lines { get; }
```
Gets a lazily-created observable sequence of complete lines split by the NewLine sequence.

- Value: The `Lines` value.

###### `P:IoT.DriverCore.Serial.SerialPortRx.LinesAsync`

```csharp
public ReactiveUI.Primitives.Async.IObservableAsync<string> LinesAsync { get; }
```
Gets complete lines as an async observable.

- Value: The `LinesAsync` value.

###### `P:IoT.DriverCore.Serial.SerialPortRx.NewLine`

```csharp
public string NewLine { get; set; }
```
Gets or sets creates new line.

- Value: The new line.

###### `P:IoT.DriverCore.Serial.SerialPortRx.Parity`

```csharp
public System.IO.Ports.Parity Parity { get; set; }
```
Gets or sets the parity.

- Value: The parity.

###### `P:IoT.DriverCore.Serial.SerialPortRx.ParityReplace`

```csharp
public byte ParityReplace { get; set; }
```
Gets or sets the parity replace.

- Value: The parity replace.

###### `P:IoT.DriverCore.Serial.SerialPortRx.PortName`

```csharp
public string PortName { get; set; }
```
Gets or sets the port.

- Value: The port.

###### `P:IoT.DriverCore.Serial.SerialPortRx.ReadBufferSize`

```csharp
public int ReadBufferSize { get; set; }
```
Gets or sets the size of the read buffer.

- Value: The size of the read buffer.

###### `P:IoT.DriverCore.Serial.SerialPortRx.ReadTimeout`

```csharp
public int ReadTimeout { get; set; }
```
Gets or sets the read timeout.

- Value: The read timeout.

###### `P:IoT.DriverCore.Serial.SerialPortRx.ReceivedBytesThreshold`

```csharp
public int ReceivedBytesThreshold { get; set; }
```
Gets or sets the byte threshold that raises DataReceived.

- Value: The received bytes threshold.

###### `P:IoT.DriverCore.Serial.SerialPortRx.RtsEnable`

```csharp
public bool RtsEnable { get; set; }
```
Gets or sets whether the Request to Send (RTS) signal is enabled.

- Value: The RTS enable.

###### `P:IoT.DriverCore.Serial.SerialPortRx.StopBits`

```csharp
public System.IO.Ports.StopBits StopBits { get; set; }
```
Gets or sets the stop bits.

- Value: The stop bits.

###### `P:IoT.DriverCore.Serial.SerialPortRx.WriteBufferSize`

```csharp
public int WriteBufferSize { get; set; }
```
Gets or sets the size of the write buffer.

- Value: The size of the write buffer.

###### `P:IoT.DriverCore.Serial.SerialPortRx.WriteTimeout`

```csharp
public int WriteTimeout { get; set; }
```
Gets or sets the write timeout.

- Value: The write timeout.

#### `T:IoT.DriverCore.Serial.SerialPortRxMessageHandler`

```csharp
public class IoT.DriverCore.Serial.SerialPortRxMessageHandler
```
Coordinates command requests and responses over a reactive serial port.

##### Declared public members

###### `M:IoT.DriverCore.Serial.SerialPortRxMessageHandler.#ctor(IoT.DriverCore.Serial.ISerialPortRx,System.String[])`

```csharp
public IoT.DriverCore.Serial.SerialPortRxMessageHandler(IoT.DriverCore.Serial.ISerialPortRx port, string[] errorLine)
```
Initializes a new instance of the `T:IoT.DriverCore.Serial.SerialPortRxMessageHandler` class.

- Parameter `port`: The port.
- Parameter `errorLine`: The error line.

###### `M:IoT.DriverCore.Serial.SerialPortRxMessageHandler.Dispose`

```csharp
public void Dispose()
```
Releases owned resources.

###### `M:IoT.DriverCore.Serial.SerialPortRxMessageHandler.RequestAsync(System.String)`

```csharp
public System.Threading.Tasks.Task RequestAsync(string cmd)
```
Requests the asynchronous.

- Parameter `cmd`: The command.
- Returns: A `T:System.Threading.Tasks.Task` representing the asynchronous operation.

###### `M:IoT.DriverCore.Serial.SerialPortRxMessageHandler.RequestAsync(System.String,System.Action`1{System.String})`

```csharp
public System.Threading.Tasks.Task RequestAsync(string cmd, System.Action<string> apply)
```
Executes the `RequestAsync` operation.

- Parameter `cmd`: The `cmd` value.
- Parameter `apply`: The `apply` value.
- Returns: A `System.Threading.Tasks.Task` result.

###### `M:IoT.DriverCore.Serial.SerialPortRxMessageHandler.SendCommandAsync(System.String)`

```csharp
public System.Threading.Tasks.Task SendCommandAsync(string fullCmd)
```
Sends the command asynchronous.

- Parameter `fullCmd`: The full command.
- Returns: A `T:System.Threading.Tasks.Task` representing the asynchronous operation.

###### `M:IoT.DriverCore.Serial.SerialPortRxMessageHandler.StartPolling`

```csharp
public void StartPolling()
```
Starts the polling.

###### `M:IoT.DriverCore.Serial.SerialPortRxMessageHandler.StopPolling`

```csharp
public void StopPolling()
```
Stops the polling.

###### `M:IoT.DriverCore.Serial.SerialPortRxMessageHandler.WithPollingStoppedAsync(System.Func`1{System.Threading.Tasks.Task})`

```csharp
public System.Threading.Tasks.Task WithPollingStoppedAsync(System.Func<System.Threading.Tasks.Task> action)
```
Executes the `WithPollingStoppedAsync` operation.

- Parameter `action`: The `action` value.
- Returns: A `System.Threading.Tasks.Task` result.

###### `P:IoT.DriverCore.Serial.SerialPortRxMessageHandler.PollingTasks`

```csharp
public System.Func<System.Threading.Tasks.Task> PollingTasks { get; set; }
```
Gets or sets the polling task.

- Value: The polling task.

###### `P:IoT.DriverCore.Serial.SerialPortRxMessageHandler.ResponsePrefix`

```csharp
public string ResponsePrefix { get; set; }
```
Gets or sets an optional prefix that the device may prepend to responses (e.g., "1"). When set, echo detection and response normalization will account for it.

- Value: The `ResponsePrefix` value.

#### `T:IoT.DriverCore.Serial.SerialPortRxMixins`

```csharp
public class IoT.DriverCore.Serial.SerialPortRxMixins
```
Provides serial port reactive extension methods.

##### Declared public members

###### `M:IoT.DriverCore.Serial.SerialPortRxMixins.AsAsyncObservable(System.Byte)`

```csharp
public static ReactiveUI.Primitives.Async.IObservableAsync<char> AsAsyncObservable(byte value)
```
Transforms a byte into a single value async observable.

- Parameter `value`: The source byte.
- Returns: An async observable char.

###### `M:IoT.DriverCore.Serial.SerialPortRxMixins.AsAsyncObservable(System.Int16)`

```csharp
public static ReactiveUI.Primitives.Async.IObservableAsync<char> AsAsyncObservable(short value)
```
Transforms a short into a single value async observable.

- Parameter `value`: The source short.
- Returns: An async observable char.

###### `M:IoT.DriverCore.Serial.SerialPortRxMixins.AsAsyncObservable(System.Int32)`

```csharp
public static ReactiveUI.Primitives.Async.IObservableAsync<char> AsAsyncObservable(int value)
```
Transforms an int into a single value async observable.

- Parameter `value`: The source integer.
- Returns: An async observable char.

###### `M:IoT.DriverCore.Serial.SerialPortRxMixins.AsObservable(System.Byte)`

```csharp
public static System.IObservable<char> AsObservable(byte value)
```
Transforms a byte into a single value observable.

- Parameter `value`: The source byte.
- Returns: An observable char.

###### `M:IoT.DriverCore.Serial.SerialPortRxMixins.AsObservable(System.Int16)`

```csharp
public static System.IObservable<char> AsObservable(short value)
```
Transforms a short into a single value observable.

- Parameter `value`: The source short.
- Returns: An observable char.

###### `M:IoT.DriverCore.Serial.SerialPortRxMixins.AsObservable(System.Int32)`

```csharp
public static System.IObservable<char> AsObservable(int value)
```
Transforms an int into a single value observable.

- Parameter `value`: The source integer.
- Returns: An observable char.

###### `M:IoT.DriverCore.Serial.SerialPortRxMixins.BufferUntil(ReactiveUI.Primitives.Async.IObservableAsync`1{System.Char},ReactiveUI.Primitives.Async.IObservableAsync`1{System.Char},ReactiveUI.Primitives.Async.IObservableAsync`1{System.Char},ReactiveUI.Primitives.Async.IObservableAsync`1{System.String},System.Int32)`

```csharp
public static ReactiveUI.Primitives.Async.IObservableAsync<string> BufferUntil(ReactiveUI.Primitives.Async.IObservableAsync<char> source, ReactiveUI.Primitives.Async.IObservableAsync<char> startsWith, ReactiveUI.Primitives.Async.IObservableAsync<char> endsWith, ReactiveUI.Primitives.Async.IObservableAsync<string> defaultValue, int timeOut)
```
Executes the `BufferUntil` operation.

- Parameter `source`: The `source` value.
- Parameter `startsWith`: The `startsWith` value.
- Parameter `endsWith`: The `endsWith` value.
- Parameter `defaultValue`: The `defaultValue` value.
- Parameter `timeOut`: The `timeOut` value.
- Returns: A `ReactiveUI.Primitives.Async.IObservableAsync<string>` result.

###### `M:IoT.DriverCore.Serial.SerialPortRxMixins.BufferUntil(ReactiveUI.Primitives.Async.IObservableAsync`1{System.Char},ReactiveUI.Primitives.Async.IObservableAsync`1{System.Char},ReactiveUI.Primitives.Async.IObservableAsync`1{System.Char},ReactiveUI.Primitives.Async.IObservableAsync`1{System.String},System.Int32,ReactiveUI.Primitives.Concurrency.ISequencer)`

```csharp
public static ReactiveUI.Primitives.Async.IObservableAsync<string> BufferUntil(ReactiveUI.Primitives.Async.IObservableAsync<char> source, ReactiveUI.Primitives.Async.IObservableAsync<char> startsWith, ReactiveUI.Primitives.Async.IObservableAsync<char> endsWith, ReactiveUI.Primitives.Async.IObservableAsync<string> defaultValue, int timeOut, ReactiveUI.Primitives.Concurrency.ISequencer scheduler)
```
Executes the `BufferUntil` operation.

- Parameter `source`: The `source` value.
- Parameter `startsWith`: The `startsWith` value.
- Parameter `endsWith`: The `endsWith` value.
- Parameter `defaultValue`: The `defaultValue` value.
- Parameter `timeOut`: The `timeOut` value.
- Parameter `scheduler`: The `scheduler` value.
- Returns: A `ReactiveUI.Primitives.Async.IObservableAsync<string>` result.

###### `M:IoT.DriverCore.Serial.SerialPortRxMixins.BufferUntil(ReactiveUI.Primitives.Async.IObservableAsync`1{System.Char},ReactiveUI.Primitives.Async.IObservableAsync`1{System.Char},ReactiveUI.Primitives.Async.IObservableAsync`1{System.Char},System.Int32)`

```csharp
public static ReactiveUI.Primitives.Async.IObservableAsync<string> BufferUntil(ReactiveUI.Primitives.Async.IObservableAsync<char> source, ReactiveUI.Primitives.Async.IObservableAsync<char> startsWith, ReactiveUI.Primitives.Async.IObservableAsync<char> endsWith, int timeOut)
```
Executes the `BufferUntil` operation.

- Parameter `source`: The `source` value.
- Parameter `startsWith`: The `startsWith` value.
- Parameter `endsWith`: The `endsWith` value.
- Parameter `timeOut`: The `timeOut` value.
- Returns: A `ReactiveUI.Primitives.Async.IObservableAsync<string>` result.

###### `M:IoT.DriverCore.Serial.SerialPortRxMixins.BufferUntil(ReactiveUI.Primitives.Async.IObservableAsync`1{System.Char},ReactiveUI.Primitives.Async.IObservableAsync`1{System.Char},ReactiveUI.Primitives.Async.IObservableAsync`1{System.Char},System.Int32,ReactiveUI.Primitives.Concurrency.ISequencer)`

```csharp
public static ReactiveUI.Primitives.Async.IObservableAsync<string> BufferUntil(ReactiveUI.Primitives.Async.IObservableAsync<char> source, ReactiveUI.Primitives.Async.IObservableAsync<char> startsWith, ReactiveUI.Primitives.Async.IObservableAsync<char> endsWith, int timeOut, ReactiveUI.Primitives.Concurrency.ISequencer scheduler)
```
Executes the `BufferUntil` operation.

- Parameter `source`: The `source` value.
- Parameter `startsWith`: The `startsWith` value.
- Parameter `endsWith`: The `endsWith` value.
- Parameter `timeOut`: The `timeOut` value.
- Parameter `scheduler`: The `scheduler` value.
- Returns: A `ReactiveUI.Primitives.Async.IObservableAsync<string>` result.

###### `M:IoT.DriverCore.Serial.SerialPortRxMixins.BufferUntil(System.IObservable`1{System.Char},System.IObservable`1{System.Char},System.IObservable`1{System.Char},System.IObservable`1{System.String},System.Int32)`

```csharp
public static System.IObservable<string> BufferUntil(System.IObservable<char> source, System.IObservable<char> startsWith, System.IObservable<char> endsWith, System.IObservable<string> defaultValue, int timeOut)
```
Executes the `BufferUntil` operation.

- Parameter `source`: The `source` value.
- Parameter `startsWith`: The `startsWith` value.
- Parameter `endsWith`: The `endsWith` value.
- Parameter `defaultValue`: The `defaultValue` value.
- Parameter `timeOut`: The `timeOut` value.
- Returns: A `System.IObservable<string>` result.

###### `M:IoT.DriverCore.Serial.SerialPortRxMixins.BufferUntil(System.IObservable`1{System.Char},System.IObservable`1{System.Char},System.IObservable`1{System.Char},System.IObservable`1{System.String},System.Int32,ReactiveUI.Primitives.Concurrency.ISequencer)`

```csharp
public static System.IObservable<string> BufferUntil(System.IObservable<char> source, System.IObservable<char> startsWith, System.IObservable<char> endsWith, System.IObservable<string> defaultValue, int timeOut, ReactiveUI.Primitives.Concurrency.ISequencer scheduler)
```
Executes the `BufferUntil` operation.

- Parameter `source`: The `source` value.
- Parameter `startsWith`: The `startsWith` value.
- Parameter `endsWith`: The `endsWith` value.
- Parameter `defaultValue`: The `defaultValue` value.
- Parameter `timeOut`: The `timeOut` value.
- Parameter `scheduler`: The `scheduler` value.
- Returns: A `System.IObservable<string>` result.

###### `M:IoT.DriverCore.Serial.SerialPortRxMixins.BufferUntil(System.IObservable`1{System.Char},System.IObservable`1{System.Char},System.IObservable`1{System.Char},System.Int32)`

```csharp
public static System.IObservable<string> BufferUntil(System.IObservable<char> source, System.IObservable<char> startsWith, System.IObservable<char> endsWith, int timeOut)
```
Executes the `BufferUntil` operation.

- Parameter `source`: The `source` value.
- Parameter `startsWith`: The `startsWith` value.
- Parameter `endsWith`: The `endsWith` value.
- Parameter `timeOut`: The `timeOut` value.
- Returns: A `System.IObservable<string>` result.

###### `M:IoT.DriverCore.Serial.SerialPortRxMixins.BufferUntil(System.IObservable`1{System.Char},System.IObservable`1{System.Char},System.IObservable`1{System.Char},System.Int32,ReactiveUI.Primitives.Concurrency.ISequencer)`

```csharp
public static System.IObservable<string> BufferUntil(System.IObservable<char> source, System.IObservable<char> startsWith, System.IObservable<char> endsWith, int timeOut, ReactiveUI.Primitives.Concurrency.ISequencer scheduler)
```
Executes the `BufferUntil` operation.

- Parameter `source`: The `source` value.
- Parameter `startsWith`: The `startsWith` value.
- Parameter `endsWith`: The `endsWith` value.
- Parameter `timeOut`: The `timeOut` value.
- Parameter `scheduler`: The `scheduler` value.
- Returns: A `System.IObservable<string>` result.

###### `M:IoT.DriverCore.Serial.SerialPortRxMixins.BytesReceivedAsyncObservable(IoT.DriverCore.Serial.IPortRx)`

```csharp
public static ReactiveUI.Primitives.Async.IObservableAsync<int> BytesReceivedAsyncObservable(IoT.DriverCore.Serial.IPortRx port)
```
Gets the data received after opening a receive port as an async observable.

- Parameter `port`: The source port.
- Returns: An async observable of received byte values.

###### `M:IoT.DriverCore.Serial.SerialPortRxMixins.DataReceivedAsyncObservable(IoT.DriverCore.Serial.ISerialPortRx)`

```csharp
public static ReactiveUI.Primitives.Async.IObservableAsync<char> DataReceivedAsyncObservable(IoT.DriverCore.Serial.ISerialPortRx serialPort)
```
Gets serial characters as an async observable.

- Parameter `serialPort`: The source serial port.
- Returns: An async observable of received characters.

###### `M:IoT.DriverCore.Serial.SerialPortRxMixins.DataReceivedBytesAsyncObservable(IoT.DriverCore.Serial.ISerialPortRx)`

```csharp
public static ReactiveUI.Primitives.Async.IObservableAsync<byte> DataReceivedBytesAsyncObservable(IoT.DriverCore.Serial.ISerialPortRx serialPort)
```
Gets serial bytes as an async observable.

- Parameter `serialPort`: The source serial port.
- Returns: An async observable of received bytes.

###### `M:IoT.DriverCore.Serial.SerialPortRxMixins.DataReceivedObserver(System.IO.Ports.SerialPort)`

```csharp
public static System.IObservable<ReactiveUI.Primitives.Core.EventPattern<System.IO.Ports.SerialDataReceivedEventArgs>> DataReceivedObserver(System.IO.Ports.SerialPort serialPort)
```
Monitors the received observer.

- Parameter `serialPort`: The source serial port.
- Returns: Observable value.

###### `M:IoT.DriverCore.Serial.SerialPortRxMixins.ErrorReceivedAsyncObservable(IoT.DriverCore.Serial.ISerialPortRx)`

```csharp
public static ReactiveUI.Primitives.Async.IObservableAsync<System.Exception> ErrorReceivedAsyncObservable(IoT.DriverCore.Serial.ISerialPortRx serialPort)
```
Gets serial errors as an async observable.

- Parameter `serialPort`: The source serial port.
- Returns: An async observable of errors.

###### `M:IoT.DriverCore.Serial.SerialPortRxMixins.ErrorReceivedObserver(System.IO.Ports.SerialPort)`

```csharp
public static System.IObservable<ReactiveUI.Primitives.Core.EventPattern<System.IO.Ports.SerialErrorReceivedEventArgs>> ErrorReceivedObserver(System.IO.Ports.SerialPort serialPort)
```
Monitors the Errors observer.

- Parameter `serialPort`: The source serial port.
- Returns: Observable value.

###### `M:IoT.DriverCore.Serial.SerialPortRxMixins.IsOpenAsyncObservable(IoT.DriverCore.Serial.ISerialPortRx)`

```csharp
public static ReactiveUI.Primitives.Async.IObservableAsync<bool> IsOpenAsyncObservable(IoT.DriverCore.Serial.ISerialPortRx serialPort)
```
Gets serial open-state changes as an async observable.

- Parameter `serialPort`: The source serial port.
- Returns: An async observable of open-state changes.

###### `M:IoT.DriverCore.Serial.SerialPortRxMixins.LinesAsyncObservable(IoT.DriverCore.Serial.ISerialPortRx)`

```csharp
public static ReactiveUI.Primitives.Async.IObservableAsync<string> LinesAsyncObservable(IoT.DriverCore.Serial.ISerialPortRx serialPort)
```
Gets serial lines as an async observable.

- Parameter `serialPort`: The source serial port.
- Returns: An async observable of received lines.

###### `M:IoT.DriverCore.Serial.SerialPortRxMixins.PortNamesAsyncObservable`

```csharp
public static ReactiveUI.Primitives.Async.IObservableAsync<string[]> PortNamesAsyncObservable()
```
Emits the list of available port names whenever it changes as an async observable.

- Returns: An async observable of port name arrays.

###### `M:IoT.DriverCore.Serial.SerialPortRxMixins.PortNamesAsyncObservable(System.Int32)`

```csharp
public static ReactiveUI.Primitives.Async.IObservableAsync<string[]> PortNamesAsyncObservable(int pollInterval)
```
Emits the list of available port names whenever it changes as an async observable.

- Parameter `pollInterval`: The poll interval.
- Returns: An async observable of port name arrays.

###### `M:IoT.DriverCore.Serial.SerialPortRxMixins.PortNamesAsyncObservable(System.Int32,System.Int32)`

```csharp
public static ReactiveUI.Primitives.Async.IObservableAsync<string[]> PortNamesAsyncObservable(int pollInterval, int pollLimit)
```
Emits the list of available port names whenever it changes as an async observable.

- Parameter `pollInterval`: The poll interval.
- Parameter `pollLimit`: The poll limit.
- Returns: An async observable of port name arrays.

###### `M:IoT.DriverCore.Serial.SerialPortRxMixins.WhileIsOpen(IoT.DriverCore.Serial.SerialPortRx,System.TimeSpan)`

```csharp
public static System.IObservable<bool> WhileIsOpen(IoT.DriverCore.Serial.SerialPortRx serialPort, System.TimeSpan timespan)
```
Executes while port is open at the given TimeSpan.

- Parameter `serialPort`: The source serial port.
- Parameter `timespan`: The timespan at which to notify.
- Returns: Observable value.

###### `M:IoT.DriverCore.Serial.SerialPortRxMixins.WhileIsOpenAsyncObservable(IoT.DriverCore.Serial.SerialPortRx,System.TimeSpan)`

```csharp
public static ReactiveUI.Primitives.Async.IObservableAsync<bool> WhileIsOpenAsyncObservable(IoT.DriverCore.Serial.SerialPortRx serialPort, System.TimeSpan timespan)
```
Executes while port is open at the given TimeSpan via an async observable.

- Parameter `serialPort`: The source serial port.
- Parameter `timespan`: The timespan at which to notify.
- Returns: Async observable value.

#### `T:IoT.DriverCore.Serial.SourceGeneration.SerialPortReactiveValueConverter`

```csharp
public class IoT.DriverCore.Serial.SourceGeneration.SerialPortReactiveValueConverter
```
Converts generated serial stream values into strongly typed reactive properties.

##### Declared public members

###### `M:IoT.DriverCore.Serial.SourceGeneration.SerialPortReactiveValueConverter.TryConvertMatch``1(System.Object,System.String,System.String,System.Int32,System.Boolean,``0@)`

```csharp
public static bool TryConvertMatch<T>(object value, string pattern, string groupName, int groupNumber, bool ignoreCase, out T result)
```
Tries to match and convert a serial stream value.

- Parameter `value`: The raw stream value.
- Parameter `pattern`: The optional regular expression pattern.
- Parameter `groupName`: The optional named group to convert.
- Parameter `groupNumber`: The fallback group number to convert.
- Parameter `ignoreCase`: A value indicating whether the match ignores case.
- Parameter `result`: The converted value.
- Returns: true when a value was matched and converted; otherwise, false .

#### `T:IoT.DriverCore.Serial.TcpClientRx`

```csharp
public class IoT.DriverCore.Serial.TcpClientRx
```
Provides a reactive wrapper around `T:System.Net.Sockets.TcpClient` .

##### Declared public members

###### `M:IoT.DriverCore.Serial.TcpClientRx.#ctor`

```csharp
public IoT.DriverCore.Serial.TcpClientRx()
```
Initializes a new instance of the `T:IoT.DriverCore.Serial.TcpClientRx` class.

###### `M:IoT.DriverCore.Serial.TcpClientRx.#ctor(System.Net.IPEndPoint)`

```csharp
public IoT.DriverCore.Serial.TcpClientRx(System.Net.IPEndPoint localEP)
```
Initializes a new instance of the `T:IoT.DriverCore.Serial.TcpClientRx` class.

- Parameter `localEP`: The local ep.

###### `M:IoT.DriverCore.Serial.TcpClientRx.#ctor(System.Net.Sockets.AddressFamily)`

```csharp
public IoT.DriverCore.Serial.TcpClientRx(System.Net.Sockets.AddressFamily family)
```
Initializes a new instance of the `T:IoT.DriverCore.Serial.TcpClientRx` class.

- Parameter `family`: The family.

###### `M:IoT.DriverCore.Serial.TcpClientRx.#ctor(System.Net.Sockets.TcpClient)`

```csharp
public IoT.DriverCore.Serial.TcpClientRx(System.Net.Sockets.TcpClient tcpClient)
```
Initializes a new instance of the `T:IoT.DriverCore.Serial.TcpClientRx` class.

- Parameter `tcpClient`: The TCP client.

###### `M:IoT.DriverCore.Serial.TcpClientRx.#ctor(System.String,System.Int32)`

```csharp
public IoT.DriverCore.Serial.TcpClientRx(string hostname, int port)
```
Initializes a new instance of the `T:IoT.DriverCore.Serial.TcpClientRx` class.

- Parameter `hostname`: The hostname.
- Parameter `port`: The port.

###### `M:IoT.DriverCore.Serial.TcpClientRx.Close`

```csharp
public void Close()
```
Closes this instance.

###### `M:IoT.DriverCore.Serial.TcpClientRx.Connect(System.Net.IPAddress,System.Int32)`

```csharp
public void Connect(System.Net.IPAddress address, int port)
```
Connects the specified address.

- Parameter `address`: The address.
- Parameter `port`: The port.

###### `M:IoT.DriverCore.Serial.TcpClientRx.Connect(System.Net.IPAddress[],System.Int32)`

```csharp
public void Connect(System.Net.IPAddress[] addresses, int port)
```
Connects the specified IP addresses.

- Parameter `addresses`: The IP addresses.
- Parameter `port`: The port.

###### `M:IoT.DriverCore.Serial.TcpClientRx.Connect(System.Net.IPEndPoint)`

```csharp
public void Connect(System.Net.IPEndPoint remoteEP)
```
Connects the specified remote ep.

- Parameter `remoteEP`: The remote ep.

###### `M:IoT.DriverCore.Serial.TcpClientRx.Connect(System.String,System.Int32)`

```csharp
public void Connect(string hostname, int port)
```
Connects the specified hostname.

- Parameter `hostname`: The hostname.
- Parameter `port`: The port.

###### `M:IoT.DriverCore.Serial.TcpClientRx.DiscardInBuffer`

```csharp
public void DiscardInBuffer()
```
Discards the in buffer.

###### `M:IoT.DriverCore.Serial.TcpClientRx.Dispose`

```csharp
public void Dispose()
```
Releases owned resources.

###### `M:IoT.DriverCore.Serial.TcpClientRx.OpenAsync`

```csharp
public System.Threading.Tasks.Task OpenAsync()
```
Opens this instance.

- Returns: A Task.

###### `M:IoT.DriverCore.Serial.TcpClientRx.ReadAsync(System.Byte[],System.Int32,System.Int32)`

```csharp
public System.Threading.Tasks.Task<int> ReadAsync(byte[] buffer, int offset, int count)
```
Reads the specified buffer.

- Parameter `buffer`: The buffer.
- Parameter `offset`: The offset.
- Parameter `count`: The count.
- Returns: A int.

###### `M:IoT.DriverCore.Serial.TcpClientRx.Write(System.Byte[],System.Int32,System.Int32)`

```csharp
public void Write(byte[] buffer, int offset, int count)
```
Writes the specified buffer.

- Parameter `buffer`: The buffer.
- Parameter `offset`: The offset.
- Parameter `count`: The count.

###### `P:IoT.DriverCore.Serial.TcpClientRx.BytesReceived`

```csharp
public System.IObservable<int> BytesReceived { get; }
```
Gets the data received From ReadAsync.

- Value: The data received.

###### `P:IoT.DriverCore.Serial.TcpClientRx.BytesReceivedAsync`

```csharp
public ReactiveUI.Primitives.Async.IObservableAsync<int> BytesReceivedAsync { get; }
```
Gets the data received from ReadAsync as an async observable.

- Value: The data received.

###### `P:IoT.DriverCore.Serial.TcpClientRx.Client`

```csharp
public System.Net.Sockets.Socket Client { get; }
```
Gets the underlying System.Net.Sockets.Socket.

- Value: The underlying network System.Net.Sockets.Socket.

###### `P:IoT.DriverCore.Serial.TcpClientRx.DataReceived`

```csharp
public System.IObservable<int> DataReceived { get; }
```
Gets the data received after calling Open.

- Value: The data received.

###### `P:IoT.DriverCore.Serial.TcpClientRx.DataReceivedAsync`

```csharp
public ReactiveUI.Primitives.Async.IObservableAsync<int> DataReceivedAsync { get; }
```
Gets the data received after calling Open as an async observable.

- Value: The data received.

###### `P:IoT.DriverCore.Serial.TcpClientRx.DataReceivedBatches`

```csharp
public System.IObservable<byte[]> DataReceivedBatches { get; }
```
Gets stream chunks (byte arrays) produced by the internal read loop.

- Value: The `DataReceivedBatches` value.

###### `P:IoT.DriverCore.Serial.TcpClientRx.DataReceivedBatchesAsync`

```csharp
public ReactiveUI.Primitives.Async.IObservableAsync<byte[]> DataReceivedBatchesAsync { get; }
```
Gets stream chunks produced by the internal read loop as an async observable.

- Value: The `DataReceivedBatchesAsync` value.

###### `P:IoT.DriverCore.Serial.TcpClientRx.InfiniteTimeout`

```csharp
public int InfiniteTimeout { get; }
```
Gets the infinite timeout.

- Value: The infinite timeout.

###### `P:IoT.DriverCore.Serial.TcpClientRx.ReadTimeout`

```csharp
public int ReadTimeout { get; set; }
```
Gets or sets the read timeout.

- Value: The read timeout.

###### `P:IoT.DriverCore.Serial.TcpClientRx.Stream`

```csharp
public System.Net.Sockets.NetworkStream Stream { get; }
```
Gets the System.Net.Sockets.NetworkStream used to send and receive data.

- Value: The stream.

###### `P:IoT.DriverCore.Serial.TcpClientRx.WriteTimeout`

```csharp
public int WriteTimeout { get; set; }
```
Gets or sets the write timeout.

- Value: The write timeout.

#### `T:IoT.DriverCore.Serial.UdpClientRx`

```csharp
public class IoT.DriverCore.Serial.UdpClientRx
```
Provides a reactive wrapper around `T:System.Net.Sockets.UdpClient` .

##### Declared public members

###### `M:IoT.DriverCore.Serial.UdpClientRx.#ctor`

```csharp
public IoT.DriverCore.Serial.UdpClientRx()
```
Initializes a new instance of the `T:IoT.DriverCore.Serial.UdpClientRx` class.

###### `M:IoT.DriverCore.Serial.UdpClientRx.#ctor(System.Int32)`

```csharp
public IoT.DriverCore.Serial.UdpClientRx(int port)
```
Initializes a new instance of the `T:IoT.DriverCore.Serial.UdpClientRx` class.

- Parameter `port`: The port.

###### `M:IoT.DriverCore.Serial.UdpClientRx.#ctor(System.Int32,System.Net.Sockets.AddressFamily)`

```csharp
public IoT.DriverCore.Serial.UdpClientRx(int port, System.Net.Sockets.AddressFamily family)
```
Initializes a new instance of the `T:IoT.DriverCore.Serial.UdpClientRx` class.

- Parameter `port`: The port.
- Parameter `family`: The family.

###### `M:IoT.DriverCore.Serial.UdpClientRx.#ctor(System.Net.IPEndPoint)`

```csharp
public IoT.DriverCore.Serial.UdpClientRx(System.Net.IPEndPoint localEP)
```
Initializes a new instance of the `T:IoT.DriverCore.Serial.UdpClientRx` class.

- Parameter `localEP`: The local ep.

###### `M:IoT.DriverCore.Serial.UdpClientRx.#ctor(System.Net.Sockets.AddressFamily)`

```csharp
public IoT.DriverCore.Serial.UdpClientRx(System.Net.Sockets.AddressFamily family)
```
Initializes a new instance of the `T:IoT.DriverCore.Serial.UdpClientRx` class.

- Parameter `family`: The family.

###### `M:IoT.DriverCore.Serial.UdpClientRx.#ctor(System.Net.Sockets.UdpClient)`

```csharp
public IoT.DriverCore.Serial.UdpClientRx(System.Net.Sockets.UdpClient udpClient)
```
Initializes a new instance of the `T:IoT.DriverCore.Serial.UdpClientRx` class.

- Parameter `udpClient`: The UDP client.

###### `M:IoT.DriverCore.Serial.UdpClientRx.#ctor(System.String,System.Int32)`

```csharp
public IoT.DriverCore.Serial.UdpClientRx(string hostname, int port)
```
Initializes a new instance of the `T:IoT.DriverCore.Serial.UdpClientRx` class.

- Parameter `hostname`: The hostname.
- Parameter `port`: The port.

###### `M:IoT.DriverCore.Serial.UdpClientRx.Close`

```csharp
public void Close()
```
Closes this instance.

###### `M:IoT.DriverCore.Serial.UdpClientRx.Connect(System.Net.IPAddress,System.Int32)`

```csharp
public void Connect(System.Net.IPAddress addr, int port)
```
Connects the specified addr.

- Parameter `addr`: The addr.
- Parameter `port`: The port.

###### `M:IoT.DriverCore.Serial.UdpClientRx.Connect(System.Net.IPEndPoint)`

```csharp
public void Connect(System.Net.IPEndPoint endPoint)
```
Connects the specified end point.

- Parameter `endPoint`: The end point.

###### `M:IoT.DriverCore.Serial.UdpClientRx.Connect(System.String,System.Int32)`

```csharp
public void Connect(string hostname, int port)
```
Connects the specified hostname.

- Parameter `hostname`: The hostname.
- Parameter `port`: The port.

###### `M:IoT.DriverCore.Serial.UdpClientRx.DiscardInBuffer`

```csharp
public void DiscardInBuffer()
```
Discards the in buffer.

###### `M:IoT.DriverCore.Serial.UdpClientRx.Dispose`

```csharp
public void Dispose()
```
Releases owned resources.

###### `M:IoT.DriverCore.Serial.UdpClientRx.OpenAsync`

```csharp
public System.Threading.Tasks.Task OpenAsync()
```
Opens this instance.

- Returns: A Task.

###### `M:IoT.DriverCore.Serial.UdpClientRx.ReadAsync(System.Byte[],System.Int32,System.Int32)`

```csharp
public System.Threading.Tasks.Task<int> ReadAsync(byte[] buffer, int offset, int count)
```
Reads the specified buffer.

- Parameter `buffer`: The buffer.
- Parameter `offset`: The offset.
- Parameter `count`: The count.
- Returns: A int.

###### `M:IoT.DriverCore.Serial.UdpClientRx.ReceiveAsync`

```csharp
public System.Threading.Tasks.Task<System.Net.Sockets.UdpReceiveResult> ReceiveAsync()
```
Returns a UDP datagram asynchronously that was sent by a remote host.

- Returns: The task object representing the asynchronous operation.

###### `M:IoT.DriverCore.Serial.UdpClientRx.SendAsync(System.Byte[],System.Int32,System.Net.IPEndPoint)`

```csharp
public System.Threading.Tasks.Task<int> SendAsync(byte[] dataGram, int bytes, System.Net.IPEndPoint endPoint)
```
Sends a UDP datagram asynchronously to a remote host.

- Parameter `dataGram`: The data gram.
- Parameter `bytes`: The bytes.
- Parameter `endPoint`: The end point.
- Returns: A Task of int.

###### `M:IoT.DriverCore.Serial.UdpClientRx.Write(System.Byte[],System.Int32,System.Int32)`

```csharp
public void Write(byte[] buffer, int offset, int count)
```
Writes the specified buffer.

- Parameter `buffer`: The buffer.
- Parameter `offset`: The offset.
- Parameter `count`: The count.

###### `P:IoT.DriverCore.Serial.UdpClientRx.Available`

```csharp
public int Available { get; }
```
Gets the available.

- Value: The available.

###### `P:IoT.DriverCore.Serial.UdpClientRx.BytesReceived`

```csharp
public System.IObservable<int> BytesReceived { get; }
```
Gets the data received.

- Value: The data received.

###### `P:IoT.DriverCore.Serial.UdpClientRx.BytesReceivedAsync`

```csharp
public ReactiveUI.Primitives.Async.IObservableAsync<int> BytesReceivedAsync { get; }
```
Gets the data received from ReadAsync as an async observable.

- Value: The data received.

###### `P:IoT.DriverCore.Serial.UdpClientRx.Client`

```csharp
public System.Net.Sockets.Socket Client { get; set; }
```
Gets or sets the underlying System.Net.Sockets.Socket.

- Value: The underlying network System.Net.Sockets.Socket.

###### `P:IoT.DriverCore.Serial.UdpClientRx.DataReceived`

```csharp
public System.IObservable<int> DataReceived { get; }
```
Gets the data received.

- Value: The data received.

###### `P:IoT.DriverCore.Serial.UdpClientRx.DataReceivedAsync`

```csharp
public ReactiveUI.Primitives.Async.IObservableAsync<int> DataReceivedAsync { get; }
```
Gets the data received as an async observable.

- Value: The data received.

###### `P:IoT.DriverCore.Serial.UdpClientRx.DataReceivedBatches`

```csharp
public System.IObservable<byte[]> DataReceivedBatches { get; }
```
Gets stream chunks (byte arrays) for each received UDP datagram.

- Value: The `DataReceivedBatches` value.

###### `P:IoT.DriverCore.Serial.UdpClientRx.DataReceivedBatchesAsync`

```csharp
public ReactiveUI.Primitives.Async.IObservableAsync<byte[]> DataReceivedBatchesAsync { get; }
```
Gets stream chunks for each received UDP datagram as an async observable.

- Value: The `DataReceivedBatchesAsync` value.

###### `P:IoT.DriverCore.Serial.UdpClientRx.DontFragment`

```csharp
public bool DontFragment { get; set; }
```
Gets or sets a value indicating whether [dont fragment].

- Value: true if [dont fragment]; otherwise, false .

###### `P:IoT.DriverCore.Serial.UdpClientRx.EnableBroadcast`

```csharp
public bool EnableBroadcast { get; set; }
```
Gets or sets a value indicating whether [enable broadcast].

- Value: true if [enable broadcast]; otherwise, false .

###### `P:IoT.DriverCore.Serial.UdpClientRx.ExclusiveAddressUse`

```csharp
public bool ExclusiveAddressUse { get; set; }
```
Gets or sets a value indicating whether [exclusive address use].

- Value: true if [exclusive address use]; otherwise, false .

###### `P:IoT.DriverCore.Serial.UdpClientRx.InfiniteTimeout`

```csharp
public int InfiniteTimeout { get; }
```
Gets the infinite timeout.

- Value: The infinite timeout.

###### `P:IoT.DriverCore.Serial.UdpClientRx.MulticastLoopback`

```csharp
public bool MulticastLoopback { get; set; }
```
Gets or sets a value indicating whether [multicast loopback].

- Value: true if [multicast loopback]; otherwise, false .

###### `P:IoT.DriverCore.Serial.UdpClientRx.ReadTimeout`

```csharp
public int ReadTimeout { get; set; }
```
Gets or sets the read timeout.

- Value: The read timeout.

###### `P:IoT.DriverCore.Serial.UdpClientRx.Ttl`

```csharp
public short Ttl { get; set; }
```
Gets or sets the TTL.

- Value: The TTL.

###### `P:IoT.DriverCore.Serial.UdpClientRx.WriteTimeout`

```csharp
public int WriteTimeout { get; set; }
```
Gets or sets the write timeout.

- Value: The write timeout.

<!-- END GENERATED PUBLIC API -->
