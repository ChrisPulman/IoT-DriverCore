// Copyright (c) 2019-2026 Chris Pulman and contributors. All rights reserved.
// Chris Pulman and contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace IoT.DriverCore.Serial.Tests;

/// <summary>Targeted deterministic coverage for serial connection and wrapper edge paths.</summary>
[NotInParallel]
public sealed class SerialPortDeterministicCoverageTests
{
    /// <summary>The terminator size that forces the pooled-buffer parsing path.</summary>
    private const int LargeTerminatorLength = 257;

    /// <summary>The representative system receive buffer size.</summary>
    private const int SystemReadBufferSize = 4096;

    /// <summary>The representative system transmit buffer size.</summary>
    private const int SystemWriteBufferSize = 2048;

    /// <summary>Verifies every constructor overload and async observable cache is reachable without hardware.</summary>
    /// <returns>A task representing the asynchronous unit test.</returns>
    [Test]
    public async Task ConstructorsAndAsyncObservableCaches_AreCoveredAsync()
    {
        using var five = new SerialPortRx("P5", DefaultBaudRate, Eight, Parity.Odd, StopBits.Two);
        using var four = new SerialPortRx("P4", DefaultBaudRate, Seven, Parity.Even);
        using var three = new SerialPortRx("P3", HighBaudRate, Eight);
        using var two = new SerialPortRx("P2", HighBaudRate);
        using var one = new SerialPortRx("P1");
        using var pair = new InMemoryPortRxPair();

        await Assert.That(five.StopBits).IsEqualTo(StopBits.Two);
        await Assert.That(four.Parity).IsEqualTo(Parity.Even);
        await Assert.That(three.DataBits).IsEqualTo(Eight);
        await Assert.That(two.BaudRate).IsEqualTo(HighBaudRate);
        await Assert.That(one.PortName).IsEqualTo("P1");
        await Assert.That(ReferenceEquals(pair.First.DataReceivedAsync, pair.First.DataReceivedAsync)).IsTrue();
        await Assert.That(ReferenceEquals(pair.First.DataReceivedBytesAsync, pair.First.DataReceivedBytesAsync)).IsTrue();
        await Assert.That(ReferenceEquals(pair.First.BytesReceivedAsync, pair.First.BytesReceivedAsync)).IsTrue();
        await Assert.That(ReferenceEquals(pair.First.ErrorReceivedAsync, pair.First.ErrorReceivedAsync)).IsTrue();
        await Assert.That(ReferenceEquals(pair.First.IsOpenObservableAsync, pair.First.IsOpenObservableAsync)).IsTrue();
        await Assert.That(ReferenceEquals(pair.First.LinesAsync, pair.First.LinesAsync)).IsTrue();
    }

    /// <summary>Verifies byte, character, memory, span, text, line, and empty write overloads.</summary>
    /// <returns>A task representing the asynchronous unit test.</returns>
    [Test]
    public async Task WriteOverloads_TransmitThroughTheConnectionPipelineAsync()
    {
        using var pair = new InMemoryPortRxPair();
        pair.First.EnableAutoDataReceive = false;
        pair.Second.EnableAutoDataReceive = false;
        await pair.First.OpenAsync();
        await pair.Second.OpenAsync();
        await pair.First.OpenAsync();
        byte[] bytes = [ByteLetterA, ByteLetterB];
        char[] characters = ['C', 'D'];

        pair.First.Write(bytes);
        pair.First.Write(characters);
        pair.First.Write(characters, 1, 1);
#if !NETFRAMEWORK
        pair.First.Write(bytes.AsSpan());
        pair.First.Write(bytes.AsMemory());
        pair.First.Write(characters.AsSpan());
        pair.First.Write(ReadOnlySpan<byte>.Empty);
        pair.First.Write(ReadOnlyMemory<byte>.Empty);
        pair.First.Write(ReadOnlySpan<char>.Empty);
#endif
        pair.First.Write("E");
        pair.First.NewLine = "\n";
        pair.First.WriteLine("F");

#if NETFRAMEWORK
        const string expected = "ABCDDEF\n";
#else
        const string expected = "ABCDDABABCDEF\n";
#endif
        await Assert.That(pair.Second.ReadExisting()).IsEqualTo(expected);
    }

    /// <summary>Verifies general line parsing uses both stack and pooled suffix buffers.</summary>
    /// <returns>A task representing the asynchronous unit test.</returns>
    [Test]
    public async Task LineParsing_SupportsShortAndLargeTerminatorsAsync()
    {
        using var shortPair = new InMemoryPortRxPair();
        shortPair.First.NewLine = "<END>";
        shortPair.Second.NewLine = "<END>";
        var shortLines = new List<string>();
        using var shortSubscription = shortPair.Second.Lines.Subscribe(shortLines.Add);
        await shortPair.First.OpenAsync();
        await shortPair.Second.OpenAsync();
        shortPair.First.WriteLine("short");

        var longTerminator = new string('#', LargeTerminatorLength);
        using var longPair = new InMemoryPortRxPair();
        longPair.First.NewLine = longTerminator;
        longPair.Second.NewLine = longTerminator;
        var longLines = new List<string>();
        using var longSubscription = longPair.Second.Lines.Subscribe(longLines.Add);
        await longPair.First.OpenAsync();
        await longPair.Second.OpenAsync();
        longPair.First.WriteLine("long");

        await Assert.That(shortLines).IsEquivalentTo(["short"]);
        await Assert.That(longLines).IsEquivalentTo(["long"]);
    }

    /// <summary>Verifies injected factory-construction and connection-open failures report errors.</summary>
    /// <returns>A task representing the asynchronous unit test.</returns>
    [Test]
    public async Task OpenAsync_InjectedFailures_ReportAndFaultAsync()
    {
        using var factoryFailure = new SerialPortRx(_ => throw new IOException("factory failed"));
        var factoryErrors = new List<Exception>();
        using var factorySubscription = factoryFailure.ErrorReceived.Subscribe(factoryErrors.Add);
        await Assert.That(factoryFailure.OpenAsync).Throws<IOException>();

        var link = new InMemorySerialLink();
        var disposedConnection = new InMemorySerialPortConnection(
            link,
            0,
            Encoding.ASCII,
            "\n",
            Hundred);
        disposedConnection.Dispose();
        using var openFailure = new SerialPortRx(_ => disposedConnection);
        var openErrors = new List<Exception>();
        using var openSubscription = openFailure.ErrorReceived.Subscribe(openErrors.Add);
        await Assert.That(openFailure.OpenAsync).Throws<ObjectDisposedException>();

        await Assert.That(factoryErrors.Count).IsEqualTo(1);
        await Assert.That(openErrors.Count).IsEqualTo(1);
    }

    /// <summary>Verifies direct in-memory connection guards, duplicate registration, and empty reads.</summary>
    /// <returns>A task representing the asynchronous unit test.</returns>
    [Test]
    public async Task InMemoryConnection_GuardsInvalidLifecycleAndSegmentsAsync()
    {
        var link = new InMemorySerialLink();
        using var first = new InMemorySerialPortConnection(link, 0, Encoding.ASCII, "\n", Hundred);
        using var duplicate = new InMemorySerialPortConnection(link, 0, Encoding.ASCII, "\n", Hundred);
        using var second = new InMemorySerialPortConnection(link, 1, Encoding.ASCII, "\n", Hundred);
        first.Open();
        second.Open();
        var buffer = new byte[Two];

        await Assert.That(first.Read(buffer, 0, 0)).IsEqualTo(0);
        await Assert.That(duplicate.Open).Throws<InvalidOperationException>();
        await Assert.That(() => first.Read(buffer, -1, 1)).Throws<ArgumentOutOfRangeException>();
        await Assert.That(() => first.Read(buffer, 0, Three)).Throws<ArgumentOutOfRangeException>();
        await Assert.That(() => first.Write(buffer, -1, 1)).Throws<ArgumentOutOfRangeException>();
        await Assert.That(() => first.Write(buffer, 0, Three)).Throws<ArgumentOutOfRangeException>();
        second.Write([ByteLetterA], 0, 1);
        await Assert.That(first.ReadByte()).IsEqualTo(LetterA);
        second.Dispose();
        second.Receive([ByteLetterA]);
        await Assert.That(second.ReadByte).Throws<InvalidOperationException>();
        using var timeoutConnection = new InMemorySerialPortConnection(
            new InMemorySerialLink(),
            0,
            Encoding.ASCII,
            "\n",
            TwentyFive);
        timeoutConnection.Open();
        await Assert.That(timeoutConnection.ReadByte).Throws<TimeoutException>();
        first.Dispose();
        first.Dispose();
        await Assert.That(first.Open).Throws<ObjectDisposedException>();
    }

    /// <summary>Verifies impossible transport read counts are rejected before publication.</summary>
    /// <returns>A task representing the asynchronous unit test.</returns>
    [Test]
    public async Task ReceiveProcessor_WhenTransportReturnsInvalidCount_ThrowsAsync()
    {
        var buffer = new byte[1];

        await Assert.That(() => SerialPortReceiveProcessor.ReadAndPublish(
            1,
            buffer,
            (_, _, _) => Two,
            _ => { },
            _ => { },
            _ => { })).Throws<InvalidOperationException>();
    }

    /// <summary>Verifies write failures from an opened connection are reported by each write subscription.</summary>
    /// <returns>A task representing the asynchronous unit test.</returns>
    [Test]
    public async Task OpenConnection_WhenUnderlyingEndpointCloses_ReportsAllWriteFailuresAsync()
    {
        var link = new InMemorySerialLink();
        using var connection = new InMemorySerialPortConnection(link, 0, Encoding.ASCII, "\n", Hundred);
        using var port = new SerialPortRx(_ => connection);
        var errors = new List<Exception>();
        using var subscription = port.ErrorReceived.Subscribe(errors.Add);
        await port.OpenAsync();
        connection.Dispose();

        port.Write("text");
        port.WriteLine("line");
        port.Write([ByteLetterA], 0, 1);
        port.Write(['A'], 0, 1);

        await Assert.That(errors).IsNotEmpty();
        await Assert.That(errors[0]).IsTypeOf<InvalidOperationException>();
    }

    /// <summary>Verifies all serial-port-name polling overloads publish a deterministic first snapshot.</summary>
    /// <param name="cancellationToken">The TUnit timeout cancellation token.</param>
    /// <returns>A task representing the asynchronous unit test.</returns>
    [Test]
    [Timeout(5000)]
    public async Task PortNames_AllPollingOverloads_PublishAsync(CancellationToken cancellationToken)
    {
        var defaultValue = new TaskCompletionSource<string[]>(TaskCreationOptions.RunContinuationsAsynchronously);
        var intervalValue = new TaskCompletionSource<string[]>(TaskCreationOptions.RunContinuationsAsynchronously);
        var limitedValue = new TaskCompletionSource<string[]>(TaskCreationOptions.RunContinuationsAsynchronously);
        using var defaultSubscription = SerialPortRx.PortNames().Subscribe(value => defaultValue.TrySetResult(value));
        using var intervalSubscription = SerialPortRx.PortNames(1).Subscribe(value => intervalValue.TrySetResult(value));
        using var limitedSubscription = SerialPortRx.PortNames(1, 1).Subscribe(
            value => limitedValue.TrySetResult(value));

        var first = await defaultValue.Task.WaitAsync(TimeSpan.FromSeconds(Two), cancellationToken);
        var second = await intervalValue.Task.WaitAsync(TimeSpan.FromSeconds(Two), cancellationToken);
        var third = await limitedValue.Task.WaitAsync(TimeSpan.FromSeconds(Two), cancellationToken);

        await Assert.That(first).IsNotEmpty();
        await Assert.That(second).IsNotEmpty();
        await Assert.That(third).IsNotEmpty();
    }

    /// <summary>Verifies the system adapter delegates every closed-port property and operation deterministically.</summary>
    /// <returns>A task representing the asynchronous unit test.</returns>
    [Test]
    public async Task SystemSerialPortConnection_DelegatesClosedPortSurfaceAsync()
    {
        using var systemPort = new SerialPort("COM-NOT-A-REAL-PORT")
        {
            DiscardNull = true,
            DtrEnable = true,
            Encoding = Encoding.UTF8,
            NewLine = "\r\n",
            ParityReplace = ByteLetterA,
            ReadBufferSize = SystemReadBufferSize,
            ReadTimeout = Hundred,
            ReceivedBytesThreshold = Two,
            RtsEnable = true,
            WriteBufferSize = SystemWriteBufferSize,
            WriteTimeout = Hundred,
        };
        using var adapter = new SystemSerialPortConnection(systemPort);
        using var dataSubscription = new EventSubscription(
            () => adapter.DataReceived += IgnoreEvent,
            () => adapter.DataReceived -= IgnoreEvent);
        using var errorSubscription = new EventSubscription(
            () => adapter.ErrorReceived += IgnoreError,
            () => adapter.ErrorReceived -= IgnoreError);
        var bytes = new byte[1];
        var characters = new char[1];

        await Assert.That(adapter.IsOpen).IsFalse();
        await Assert.That(adapter.DiscardNull).IsTrue();
        await Assert.That(adapter.DtrEnable).IsTrue();
        await Assert.That(adapter.Encoding).IsEqualTo(Encoding.UTF8);
        await Assert.That(adapter.NewLine).IsEqualTo("\r\n");
        await Assert.That(adapter.ParityReplace).IsEqualTo(ByteLetterA);
        await Assert.That(adapter.ReadBufferSize).IsEqualTo(SystemReadBufferSize);
        await Assert.That(adapter.ReadTimeout).IsEqualTo(Hundred);
        await Assert.That(adapter.ReceivedBytesThreshold).IsEqualTo(Two);
        await Assert.That(adapter.RtsEnable).IsTrue();
        await Assert.That(adapter.WriteBufferSize).IsEqualTo(SystemWriteBufferSize);
        await Assert.That(adapter.Open).Throws<Exception>();
        await Assert.That(() => adapter.BreakState).Throws<InvalidOperationException>();
        await Assert.That(() => adapter.BytesToRead).Throws<InvalidOperationException>();
        await Assert.That(() => adapter.BytesToWrite).Throws<InvalidOperationException>();
        await Assert.That(() => adapter.CDHolding).Throws<InvalidOperationException>();
        await Assert.That(() => adapter.CtsHolding).Throws<InvalidOperationException>();
        await Assert.That(() => adapter.DsrHolding).Throws<InvalidOperationException>();
        await Assert.That(adapter.DiscardInBuffer).Throws<InvalidOperationException>();
        await Assert.That(adapter.DiscardOutBuffer).Throws<InvalidOperationException>();
        await Assert.That(() => adapter.Read(bytes, 0, 1)).Throws<InvalidOperationException>();
        await Assert.That(() => adapter.Read(characters, 0, 1)).Throws<InvalidOperationException>();
        await Assert.That(adapter.ReadByte).Throws<InvalidOperationException>();
        await Assert.That(adapter.ReadChar).Throws<InvalidOperationException>();
        await Assert.That(adapter.ReadExisting).Throws<InvalidOperationException>();
        await Assert.That(adapter.ReadLine).Throws<InvalidOperationException>();
        await Assert.That(() => adapter.Write(bytes, 0, 1)).Throws<InvalidOperationException>();
        await Assert.That(() => adapter.Write(characters, 0, 1)).Throws<InvalidOperationException>();
        await Assert.That(() => adapter.Write("text")).Throws<InvalidOperationException>();
        await Assert.That(() => adapter.WriteLine("line")).Throws<InvalidOperationException>();
    }

    /// <summary>Ignores a standard event.</summary>
    /// <param name="sender">The event sender.</param>
    /// <param name="eventArgs">The event arguments.</param>
    private static void IgnoreEvent(object? sender, EventArgs eventArgs)
    {
        _ = sender;
        _ = eventArgs;
    }

    /// <summary>Ignores a connection error event.</summary>
    /// <param name="sender">The event sender.</param>
    /// <param name="eventArgs">The connection error arguments.</param>
    private static void IgnoreError(object? sender, SerialPortConnectionErrorEventArgs eventArgs)
    {
        IgnoreEvent(sender, eventArgs);
    }

    /// <summary>Owns an explicit event subscription.</summary>
    private sealed class EventSubscription : IDisposable
    {
        /// <summary>The unsubscribe action.</summary>
        private readonly Action _unsubscribe;

        /// <summary>Initializes a new instance of the <see cref="EventSubscription"/> class.</summary>
        /// <param name="subscribe">The subscribe action.</param>
        /// <param name="unsubscribe">The unsubscribe action.</param>
        internal EventSubscription(Action subscribe, Action unsubscribe)
        {
            subscribe();
            _unsubscribe = unsubscribe;
        }

        /// <inheritdoc/>
        public void Dispose() => _unsubscribe();
    }
}
