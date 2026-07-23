// Copyright (c) 2019-2026 Chris Pulman and contributors. All rights reserved.
// Chris Pulman and contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace IoT.DriverCore.Serial.Tests;

/// <summary>Deterministic tests for <see cref="SerialPortRx"/> over its in-memory connection seam.</summary>
[NotInParallel]
public sealed class SerialPortRxTests
{
    /// <summary>The repeated deterministic error message.</summary>
    private const string FaultMessage = "fault";

    /// <summary>Verifies paired endpoints execute the normal open, close, reconnect, and live-property paths.</summary>
    /// <returns>A task representing the asynchronous unit test.</returns>
    [Test]
    public async Task InMemoryPair_OpenCloseAndReconnect_UsesNormalConnectionPathAsync()
    {
        using var pair = new InMemoryPortRxPair("LEFT", "RIGHT");
        var states = new List<bool>();
        using var subscription = pair.First.IsOpenObservable.Subscribe(states.Add);
        pair.First.BreakState = true;
        pair.First.DiscardNull = true;
        pair.First.DtrEnable = true;
        pair.First.ParityReplace = ByteLetterA;
        pair.First.ReadBufferSize = OneThousandTwentyFour;
        pair.First.ReceivedBytesThreshold = Two;
        pair.First.RtsEnable = true;
        pair.First.WriteBufferSize = TwoThousand;

        await pair.First.OpenAsync();
        await pair.Second.OpenAsync();
        pair.First.BreakState = false;
        pair.First.DiscardNull = false;
        pair.First.DtrEnable = false;
        pair.First.ParityReplace = ByteLetterB;
        pair.First.ReadBufferSize = TwoThousand;
        pair.First.ReceivedBytesThreshold = Three;
        pair.First.RtsEnable = false;
        pair.First.WriteBufferSize = OneThousandTwentyFour;

        using (Assert.Multiple())
        {
            await Assert.That(pair.First.IsOpen).IsTrue();
            await Assert.That(pair.First.BreakState).IsFalse();
            await Assert.That(pair.First.DiscardNull).IsFalse();
            await Assert.That(pair.First.DtrEnable).IsFalse();
            await Assert.That(pair.First.ParityReplace).IsEqualTo(ByteLetterB);
            await Assert.That(pair.First.ReadBufferSize).IsEqualTo(TwoThousand);
            await Assert.That(pair.First.ReceivedBytesThreshold).IsEqualTo(Three);
            await Assert.That(pair.First.RtsEnable).IsFalse();
            await Assert.That(pair.First.WriteBufferSize).IsEqualTo(OneThousandTwentyFour);
            await Assert.That(pair.First.CDHolding).IsTrue();
            await Assert.That(pair.First.CtsHolding).IsTrue();
            await Assert.That(pair.First.DsrHolding).IsTrue();
            await Assert.That(pair.First.BytesToRead).IsEqualTo(0);
            await Assert.That(pair.First.BytesToWrite).IsEqualTo(0);
        }

        pair.First.Close();
        await Assert.That(pair.First.IsOpen).IsFalse();

        await pair.First.OpenAsync();
        await Assert.That(pair.First.IsOpen).IsTrue();
        await Assert.That(states).Contains(true);
        await Assert.That(states).Contains(false);
    }

    /// <summary>Verifies the common port contracts preserve byte and batch receive behavior.</summary>
    /// <returns>A task representing the asynchronous unit test.</returns>
    [Test]
    public async Task InMemoryPair_ImplementsPortAndBatchContractsAsync()
    {
        using var pair = new InMemoryPortRxPair();
        var first = pair.First;
        var second = pair.Second;
        var bytes = new List<int>();
        var batches = new List<byte[]>();
        using var byteSubscription = second.BytesReceived.Subscribe(bytes.Add);
        using var batchSubscription = second.DataReceivedBatches.Subscribe(batches.Add);
        pair.Second.EnableAutoDataReceive = false;
        await first.OpenAsync();
        await second.OpenAsync();
        await Assert.That(first is IPortRx).IsTrue();
        await Assert.That(second is IReceiveBatchPortRx).IsTrue();

        first.Write([1, Two, Three], 0, Three);
        var buffer = new byte[Five];
        var read = await second.ReadAsync(buffer, 1, Three);
        byte[] expectedBytes = [1, Two, Three];

        await Assert.That(read).IsEqualTo(Three);
        await Assert.That(buffer.Skip(1).Take(Three)).IsEquivalentTo(expectedBytes);
        await Assert.That(bytes).IsEquivalentTo([1, Two, Three]);
        await Assert.That(batches).IsEmpty();
    }

    /// <summary>Verifies automatic receive publishes raw bytes, characters, lines, and immutable batches.</summary>
    /// <returns>A task representing the asynchronous unit test.</returns>
    [Test]
    public async Task AutoReceive_PublishesBytesCharactersLinesAndBatchesAsync()
    {
        using var pair = new InMemoryPortRxPair();
        pair.First.NewLine = "\r\n";
        pair.Second.NewLine = "\r\n";
        var bytes = new List<byte>();
        var characters = new List<char>();
        var lines = new List<string>();
        var batches = new List<byte[]>();
        using var byteSubscription = pair.Second.DataReceivedBytes.Subscribe(bytes.Add);
        using var characterSubscription = pair.Second.DataReceived.Subscribe(characters.Add);
        using var lineSubscription = pair.Second.Lines.Subscribe(lines.Add);
        using var batchSubscription = pair.Second.DataReceivedBatches.Subscribe(batches.Add);
        await pair.First.OpenAsync();
        await pair.Second.OpenAsync();

        pair.First.WriteLine(HelloText);
        pair.First.Write([0x80, 0xff], 0, Two);
        byte[] expectedHighBytes = [0x80, 0xff];

        await Assert.That(lines).IsEquivalentTo([HelloText]);
        await Assert.That(characters.Take(HelloText.Length)).IsEquivalentTo(HelloText.ToCharArray());
        await Assert.That(bytes.Take(HelloText.Length)).IsEquivalentTo(Encoding.ASCII.GetBytes(HelloText));
        await Assert.That(bytes.TakeLast(Two)).IsEquivalentTo(expectedHighBytes);
        await Assert.That(batches.Count).IsEqualTo(Two);
        await Assert.That(batches[0]).IsEquivalentTo(Encoding.ASCII.GetBytes($"{HelloText}\r\n"));
        await Assert.That(batches[1]).IsEquivalentTo(expectedHighBytes);
    }

    /// <summary>Verifies synchronous byte, character, line, delimiter, and existing-data reads.</summary>
    /// <returns>A task representing the asynchronous unit test.</returns>
    [Test]
    public async Task ManualReceive_SupportsAllSynchronousReadFormsAsync()
    {
        using var pair = new InMemoryPortRxPair();
        pair.First.EnableAutoDataReceive = false;
        pair.Second.EnableAutoDataReceive = false;
        pair.First.NewLine = "\r\n";
        pair.Second.NewLine = "\r\n";
        pair.Second.ReadTimeout = Thousand;
        await pair.First.OpenAsync();
        await pair.Second.OpenAsync();

        pair.First.WriteLine("line");
        await Assert.That(pair.Second.ReadLine()).IsEqualTo("line");

        pair.First.Write("A>B");
        await Assert.That(pair.Second.ReadTo(">")).IsEqualTo("A");
        await Assert.That(pair.Second.ReadChar()).IsEqualTo(LetterB);

        pair.First.Write([1, Two, Three], 0, Three);
        var bytes = new byte[Three];
        byte[] expectedBytes = [1, Two, Three];
        await Assert.That(pair.Second.Read(bytes, 0, Three)).IsEqualTo(Three);
        await Assert.That(bytes).IsEquivalentTo(expectedBytes);

        pair.First.Write("XYZ");
        var characters = new char[Three];
        await Assert.That(pair.Second.Read(characters, 0, Three)).IsEqualTo(Three);
        await Assert.That(characters).IsEquivalentTo(['X', 'Y', 'Z']);

        pair.First.Write("remaining");
        await Assert.That(pair.Second.ReadExisting()).IsEqualTo("remaining");
    }

    /// <summary>Verifies asynchronous line and delimiter reads, caller cancellation, and configured timeout.</summary>
    /// <returns>A task representing the asynchronous unit test.</returns>
    [Test]
    public async Task AsyncTextReads_SupportSuccessCancellationAndTimeoutAsync()
    {
        using var pair = new InMemoryPortRxPair();
        pair.First.NewLine = "\n";
        pair.Second.NewLine = "\n";
        pair.Second.ReadTimeout = Hundred;
        await pair.First.OpenAsync();
        await pair.Second.OpenAsync();

        var lineTask = pair.Second.ReadLineAsync();
        pair.First.WriteLine("ready");
        await Assert.That(await lineTask).IsEqualTo("ready");

        var delimiterTask = pair.Second.ReadToAsync(">");
        pair.First.Write("value>tail");
        await Assert.That(await delimiterTask).IsEqualTo("value");

        using var cancellation = new CancellationTokenSource();
        var canceledTask = pair.Second.ReadToAsync("#", cancellation.Token);
        await cancellation.CancelAsync();

        async Task AwaitCanceledAsync() => _ = await canceledTask;
        async Task AwaitTimeoutAsync() => _ = await pair.Second.ReadLineAsync();

        await Assert.That(AwaitCanceledAsync).Throws<OperationCanceledException>();
        await Assert.That(AwaitTimeoutAsync).Throws<TimeoutException>();
    }

    /// <summary>Verifies manually started reception publishes data and stops on disposal.</summary>
    /// <param name="cancellationToken">The TUnit timeout cancellation token.</param>
    /// <returns>A task representing the asynchronous unit test.</returns>
    [Test]
    [Timeout(5000)]
    public async Task StartDataReception_ReadsUntilDisposedAsync(CancellationToken cancellationToken)
    {
        using var pair = new InMemoryPortRxPair();
        pair.First.EnableAutoDataReceive = false;
        pair.Second.EnableAutoDataReceive = false;
        await pair.First.OpenAsync();
        await pair.Second.OpenAsync();
        var received = new TaskCompletionSource<byte>(TaskCreationOptions.RunContinuationsAsynchronously);
        using var subscription = pair.Second.DataReceivedBytes.Subscribe(value => received.TrySetResult(value));
        using var reception = pair.Second.StartDataReception(1);

        pair.First.Write([ByteLetterA], 0, 1);

        await Assert.That(await received.Task.WaitAsync(TimeSpan.FromSeconds(Two), cancellationToken))
            .IsEqualTo(ByteLetterA);
        reception.Dispose();
        await Assert.That(() => reception.Dispose()).ThrowsNothing();
    }

    /// <summary>Verifies injected connection errors are distinct by message and survive non-terminal reporting.</summary>
    /// <returns>A task representing the asynchronous unit test.</returns>
    [Test]
    public async Task InjectedErrors_ArePublishedDistinctlyWithoutClosingPortAsync()
    {
        using var pair = new InMemoryPortRxPair();
        var errors = new List<Exception>();
        using var subscription = pair.First.ErrorReceived.Subscribe(errors.Add);
        await pair.First.OpenAsync();

        pair.InjectFirstError(new IOException(FaultMessage));
        pair.InjectFirstError(new InvalidOperationException(FaultMessage));
        pair.InjectFirstError(new InvalidOperationException("other"));

        await Assert.That(errors.Count).IsEqualTo(Two);
        await Assert.That(errors[0].Message).IsEqualTo(FaultMessage);
        await Assert.That(errors[1].Message).IsEqualTo("other");
        await Assert.That(pair.First.IsOpen).IsTrue();
    }

    /// <summary>Verifies discards, null filtering, empty writes, and invalid segments.</summary>
    /// <returns>A task representing the asynchronous unit test.</returns>
    [Test]
    public async Task BufferOperations_ValidateSegmentsAndDiscardDataAsync()
    {
        using var pair = new InMemoryPortRxPair();
        pair.First.EnableAutoDataReceive = false;
        pair.Second.EnableAutoDataReceive = false;
        pair.Second.DiscardNull = true;
        var errors = new List<Exception>();
        using var errorSubscription = pair.First.ErrorReceived.Subscribe(errors.Add);
        await pair.First.OpenAsync();
        await pair.Second.OpenAsync();

        pair.First.Write([0, ByteLetterA], 0, Two);
        await Assert.That(pair.Second.BytesToRead).IsEqualTo(1);
        pair.Second.DiscardInBuffer();
        await Assert.That(pair.Second.BytesToRead).IsEqualTo(0);
        await Assert.That(() => pair.Second.DiscardOutBuffer()).ThrowsNothing();
        byte[] empty = [];
        byte[] single = [1];
        await Assert.That(() => pair.First.Write(empty, 0, 0)).ThrowsNothing();
        await Assert.That(() => pair.First.Write((byte[]?)null, 0, 0)).Throws<ArgumentNullException>();
        pair.First.Write(single, -1, 1);
        pair.First.Write(single, 0, Two);
        await Assert.That(errors.Count).IsEqualTo(Two);
        await Assert.That(errors.All(error => error is ArgumentOutOfRangeException)).IsTrue();
    }

    /// <summary>Verifies constructors, defaults, observable caching, and unopened operations.</summary>
    /// <returns>A task representing the asynchronous unit test.</returns>
    [Test]
    public async Task ConstructorsDefaultsAndClosedGuards_AreDeterministicAsync()
    {
        using var port = new SerialPortRx(
            "COM3",
            HighBaudRate,
            Seven,
            Parity.Even,
            StopBits.Two,
            Handshake.RequestToSend);

        using (Assert.Multiple())
        {
            await Assert.That(port.PortName).IsEqualTo("COM3");
            await Assert.That(port.BaudRate).IsEqualTo(HighBaudRate);
            await Assert.That(port.DataBits).IsEqualTo(Seven);
            await Assert.That(port.Parity).IsEqualTo(Parity.Even);
            await Assert.That(port.StopBits).IsEqualTo(StopBits.Two);
            await Assert.That(port.Handshake).IsEqualTo(Handshake.RequestToSend);
            await Assert.That(port.InfiniteTimeout).IsEqualTo(Timeout.Infinite);
            await Assert.That(ReferenceEquals(port.DataReceived, port.DataReceived)).IsTrue();
            await Assert.That(ReferenceEquals(port.Lines, port.Lines)).IsTrue();
            await Assert.That(ReferenceEquals(port.ErrorReceived, port.ErrorReceived)).IsTrue();
        }

        await Assert.That(port.ReadLine).Throws<InvalidOperationException>();
        await Assert.That(port.ReadExisting).Throws<InvalidOperationException>();
        await Assert.That(port.StartDataReception).Throws<InvalidOperationException>();
        await Assert.That(() => port.ReadTo(string.Empty)).Throws<InvalidOperationException>();
    }

    /// <summary>Verifies a missing physical port still reports the real operating-system path failure.</summary>
    /// <returns>A task representing the asynchronous unit test.</returns>
    [Test]
    public async Task Open_WithMissingPhysicalPort_ThrowsAsync()
    {
        using var port = new SerialPortRx("COM-NOT-A-REAL-PORT");

        await Assert.That(port.OpenAsync).Throws<InvalidOperationException>();
        await Assert.That(port.IsOpen).IsFalse();
    }

    /// <summary>Verifies disposal closes both endpoints and is idempotent.</summary>
    /// <returns>A task representing the asynchronous unit test.</returns>
    [Test]
    public async Task Dispose_ClosesEndpointsAndIsIdempotentAsync()
    {
        var pair = new InMemoryPortRxPair();
        await pair.First.OpenAsync();
        await pair.Second.OpenAsync();

        pair.Dispose();
        pair.Dispose();

        await Assert.That(pair.First.IsDisposed).IsTrue();
        await Assert.That(pair.Second.IsDisposed).IsTrue();
        await Assert.That(pair.First.IsOpen).IsFalse();
        await Assert.That(pair.Second.IsOpen).IsFalse();
    }
}
