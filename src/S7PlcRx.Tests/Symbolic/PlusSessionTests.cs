// Copyright (c) 2019-2026 Chris Pulman and contributors. All rights reserved.
// Chris Pulman and contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.
using System.IO;
using IoT.Driver.S7PlcRx.Symbolic;
using IoT.Driver.S7PlcRx.Symbolic.Protocol;
using IoT.Driver.S7PlcRx.Symbolic.Transport;
using Org.BouncyCastle.Utilities.Encoders;
using TAssert = TUnit.Assertions.Assert;

namespace IoT.Driver.S7PlcRx.Tests.Symbolic;

/// <summary>Coordinates Plus session tests.</summary>
public sealed class PlusSessionTests
{
    /// <summary>The read fixture input size.</summary>
    private const int ReadInputSize = 5;

    /// <summary>The simulated read request limit.</summary>
    private const int ReadBatchSize = 2;

    /// <summary>The simulated write request limit.</summary>
    private const int WriteBatchSize = 3;

    /// <summary>The simulated authentication protection level.</summary>
    private const uint ProtectedLevel = 3;

    /// <summary>The fixture access area.</summary>
    private const uint TestAccessArea = 100;

    /// <summary>The fixture subarea.</summary>
    private const uint TestSubArea = 2;

    /// <summary>The ordinary protocol version.</summary>
    private const byte ProtocolVersion = 2;

    /// <summary>The simulated main session identifier.</summary>
    private const uint MainSessionId = 900;

    /// <summary>The simulated subscription session identifier.</summary>
    private const uint SubscriptionId = 901;

    /// <summary>The legacy authentication attribute identifier.</summary>
    private const uint LegacyAttribute = 304;

    /// <summary>The modern authentication attribute identifier.</summary>
    private const uint ModernAttribute = 1846;

    /// <summary>The simulated item write error.</summary>
    private const ulong ItemWriteError = 123;

    /// <summary>The fixture write value.</summary>
    private const int WriteValue = 12;

    /// <summary>The immutable fixture address.</summary>
    private static readonly S7SymbolicAddress TestAddress = new(TestAccessArea, TestSubArea, [1]);

    /// <summary>The single item read fixture.</summary>
    private static readonly S7SymbolicAddress[] SingleRead = [TestAddress];

    /// <summary>The expected resource and read batch sizes.</summary>
    private static readonly int[] ExpectedReadSizes = [ReadInputSize, ReadBatchSize, ReadBatchSize, 1];

    /// <summary>Verifies handshake, session allocation, and controller batch limits.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task ConnectNegotiatesLimitsAndReadSplitsBatches()
    {
        var transport = new FakeTransport();
        await using var session = new S7PlusSession(new S7SymbolicConnectionOptions("fake"), transport);
        await session.ConnectAsync(CancellationToken.None);
        var addresses = new S7SymbolicAddress[ReadInputSize];
        for (var index = 0; index < addresses.Length; index++)
        {
            addresses[index] = new(TestAccessArea, TestSubArea, [(uint)index + 1]);
        }

        var results = await session.ReadAsync(addresses, CancellationToken.None);
        await TAssert.That(session.SessionId).IsEqualTo(MainSessionId);
        await TAssert.That(session.SubscriptionSessionId).IsEqualTo(SubscriptionId);
        await TAssert.That(session.ReadLimit).IsEqualTo(ReadBatchSize);
        await TAssert.That(session.WriteLimit).IsEqualTo(WriteBatchSize);
        await TAssert.That(transport.TlsUpgraded).IsTrue();
        await TAssert.That(results.Length).IsEqualTo(ReadInputSize);
        await TAssert.That(transport.ReadSizes.ToArray()).IsEquivalentTo(ExpectedReadSizes);
    }

    /// <summary>Verifies item errors stay attached to their input addresses.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task WriteReturnsItemErrorsWithoutFailingConnection()
    {
        var transport = new FakeTransport();
        await using var session = new S7PlusSession(new S7SymbolicConnectionOptions("fake"), transport);
        await session.ConnectAsync(CancellationToken.None);
        transport.WriteError = ItemWriteError;
        var address = TestAddress;
        var results = await session.WriteAsync([new(address, new(S7SymbolicDataType.DInt, WriteValue))], CancellationToken.None);
        await TAssert.That(results[0].Address).IsSameReferenceAs(address);
        await TAssert.That(results[0].ErrorCode).IsEqualTo(ItemWriteError);
        await TAssert.That(session.IsConnected).IsTrue();
    }

    /// <summary>Verifies mismatched replies make the session unusable.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task WrongSequenceTerminatesConnection()
    {
        var transport = new FakeTransport
        {
            WrongSequence = true
        };
        await using var session = new S7PlusSession(new S7SymbolicConnectionOptions("fake"), transport);
        await TAssert.That(
            () => session.ConnectAsync(CancellationToken.None)).Throws<InvalidDataException>();
        await TAssert.That(session.IsConnected).IsFalse();
        await TAssert.That(transport.Disconnected).IsTrue();
    }

    /// <summary>Verifies malformed integrity cannot leave an active session.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task WrongIntegrityTerminatesConnection()
    {
        var transport = new FakeTransport();
        await using var session = new S7PlusSession(new S7SymbolicConnectionOptions("fake"), transport);
        await session.ConnectAsync(CancellationToken.None);
        transport.WrongIntegrity = true;
        await TAssert.That(
            async Task () => _ = await session.ReadAsync(SingleRead, CancellationToken.None)).Throws<InvalidDataException>();
        await TAssert.That(session.IsConnected).IsFalse();
    }

    /// <summary>Verifies the legacy password response combines SHA1 and the controller challenge.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task LegacyAuthenticationUsesChallengeResponse()
    {
        var transport = new FakeTransport
        {
            ProtectionLevel = ProtectedLevel
        };
        var options = new S7SymbolicConnectionOptions("fake")
        {
            Password = "example",
            AuthenticationMode = S7SymbolicAuthenticationMode.Legacy,
        };
        await using var session = new S7PlusSession(options, transport);
        await session.ConnectAsync(CancellationToken.None);
        var expected = Hex.Decode("c3489e242d760c788877f18d7aa423c47f9b2d9c");
        await TAssert.That(transport.AuthenticationResponse).IsEquivalentTo(expected);
        await TAssert.That(transport.AuthenticationAttribute).IsEqualTo(LegacyAttribute);
    }

    /// <summary>Verifies modern authentication encrypts the typed username and password payload.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task ModernAuthenticationEncryptsTypedCredentials()
    {
        var transport = new FakeTransport
        {
            ProtectionLevel = ProtectedLevel
        };
        var options = new S7SymbolicConnectionOptions("fake")
        {
            Username = "user",
            Password = "example",
            AuthenticationMode = S7SymbolicAuthenticationMode.Modern,
        };
        await using var session = new S7PlusSession(options, transport);
        await session.ConnectAsync(CancellationToken.None);

        // Independently encrypted wire plaintext starts with Struct type 40_400, encoded as 00009dd0.
        const string expected = "4ddf268b7a45500dac33eb0f3db24ce0091225741915cbcc87e6cc2b9f295ba1357da17e0ae860e8ca3f607528be5e94";
        await TAssert.That(transport.AuthenticationAttribute).IsEqualTo(ModernAttribute);
        await TAssert.That(Hex.ToHexString(transport.AuthenticationResponse)).IsEqualTo(expected);
    }

    /// <summary>Verifies cancelling an in flight request closes its stream before later operations.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task CancelledRequestTerminatesSession()
    {
        var transport = new FakeTransport();
        await using var session = new S7PlusSession(new S7SymbolicConnectionOptions("fake"), transport);
        await session.ConnectAsync(CancellationToken.None);
        transport.PauseReads = true;
        using var cancellation = new CancellationTokenSource();
        var read = session.ReadAsync(SingleRead, cancellation.Token);
        await transport.PausedReadSent.Task;
#if NETFRAMEWORK
        cancellation.Cancel();
#else
        await cancellation.CancelAsync();
#endif
        await TAssert.That(
            async Task () => _ = await read).Throws<OperationCanceledException>();
        await TAssert.That(session.IsConnected).IsFalse();
        await TAssert.That(transport.Disconnected).IsTrue();
    }

    /// <summary>Verifies fatal controller events reach observers without another request.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task FatalSystemEventReportsConnectionClosure()
    {
        var transport = new FakeTransport();
        await using var session = new S7PlusSession(new S7SymbolicConnectionOptions("fake"), transport);
        await session.ConnectAsync(CancellationToken.None);
        TaskCompletionSource<Exception> closed = new(TaskCreationOptions.RunContinuationsAsynchronously);
        session.ConnectionClosed += (_, args) => _ = closed.TrySetResult(args.Exception);
        transport.Emit(new(0xfe, []));
        var failure = await closed.Task;
        await TAssert.That(failure).IsTypeOf<IOException>();
        await TAssert.That(session.IsConnected).IsFalse();
    }

    /// <summary>Verifies notifications do not consume an outstanding response slot.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task NotificationDoesNotCompletePendingRequest()
    {
        var transport = new FakeTransport();
        await using var session = new S7PlusSession(new S7SymbolicConnectionOptions("fake"), transport);
        await session.ConnectAsync(CancellationToken.None);
        TaskCompletionSource<PlusMessage> received = new(TaskCreationOptions.RunContinuationsAsynchronously);
        session.NotificationReceived += (_, args) => _ = received.TrySetResult(args.Message);
        transport.PauseReads = true;
        using var cancellation = new CancellationTokenSource();
        var read = session.ReadAsync(SingleRead, cancellation.Token);
        await transport.PausedReadSent.Task;
        var notification = new PlusMessage(ProtocolVersion, [0x33]);
        transport.Emit(notification);
        await TAssert.That(await received.Task).IsSameReferenceAs(notification);
        await TAssert.That(read.IsCompleted).IsFalse();
#if NETFRAMEWORK
        cancellation.Cancel();
#else
        await cancellation.CancelAsync();
#endif
        await TAssert.That(
            async Task () => _ = await read).Throws<OperationCanceledException>();
    }

    /// <summary>Verifies disposal cancels queued requests and every disposer waits for transport cleanup.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task DisposeCancelsQueuedReadsAndSharesCleanup()
    {
        var transport = new FakeTransport();
        await using var session = new S7PlusSession(new S7SymbolicConnectionOptions("fake"), transport);
        await session.ConnectAsync(CancellationToken.None);
        transport.PauseReads = true;
        transport.PauseDisposal = true;
        var active = session.ReadAsync(SingleRead, CancellationToken.None);
        await transport.PausedReadSent.Task;
        var queued = session.ReadAsync(SingleRead, CancellationToken.None);
        var firstDisposal = session.DisposeAsync().AsTask();
        var secondDisposal = session.DisposeAsync().AsTask();
        await transport.DisposalStarted.Task;
        try
        {
            await TAssert.That(async Task () => _ = await active).Throws<OperationCanceledException>();
            await TAssert.That(async Task () => _ = await queued).Throws<ObjectDisposedException>();
            await TAssert.That(secondDisposal.IsCompleted).IsFalse();
        }
        finally
        {
            _ = transport.AllowDisposal.TrySetResult(true);
            await Task.WhenAll(firstDisposal, secondDisposal);
        }

        await TAssert.That(transport.DisposeCalls).IsEqualTo(1);
    }

    /// <summary>Verifies canonical exploration scopes retain the returned objects.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task ExploreAcceptsCanonicalScopeIdentifier()
    {
        const uint requestedScope = 3;
        const uint returnedObject = 0x12345678;
        const string reply = "32000004bb0000000634000000000109a112345678010000a200000000";
        var transport = new FakeTransport();
        await using var session = new S7PlusSession(new S7SymbolicConnectionOptions("fake"), transport);
        await session.ConnectAsync(CancellationToken.None);
        transport.ReplyOverride = static _ => new(ProtocolVersion, Hex.Decode(reply));
        var objects = await session.ExploreAsync(requestedScope, false, [], CancellationToken.None);
        await TAssert.That(objects.Length).IsEqualTo(1);
        await TAssert.That(objects[0].Id).IsEqualTo(returnedObject);
        await TAssert.That(session.IsConnected).IsTrue();
    }

    /// <summary>Verifies object creation can return an allocation without an object payload.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task CreateAcceptsAllocationWithoutObjectPayload()
    {
        const uint allocatedId = 0x70000e49;
        const string reply = "32000004ca000000063400018780809c490700000000";
        var transport = new FakeTransport();
        await using var session = new S7PlusSession(new S7SymbolicConnectionOptions("fake"), transport);
        await session.ConnectAsync(CancellationToken.None);
        transport.ReplyOverride = static _ => new(ProtocolVersion, Hex.Decode(reply));
        var ids = await session.CreateObjectAsync(MainSessionId, new(1, 1), CancellationToken.None);
        await TAssert.That(ids.Length).IsEqualTo(1);
        await TAssert.That(ids[0]).IsEqualTo(allocatedId);
        await TAssert.That(session.IsConnected).IsTrue();
    }

    /// <summary>Verifies allocation replies validate their mutation integrity.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task CreateRejectsWrongAllocationIntegrity()
    {
        const string reply = "32000004ca000000063400018780809c490800000000";
        var transport = new FakeTransport();
        await using var session = new S7PlusSession(new S7SymbolicConnectionOptions("fake"), transport);
        await session.ConnectAsync(CancellationToken.None);
        transport.ReplyOverride = static _ => new(ProtocolVersion, Hex.Decode(reply));
        await TAssert.That(
            async Task () => _ = await session.CreateObjectAsync(MainSessionId, new(1, 1), CancellationToken.None)).Throws<InvalidDataException>();
        await TAssert.That(session.IsConnected).IsFalse();
    }

    /// <summary>Verifies deletion replies echo the object and mutation integrity.</summary>
    /// <param name="reply">The independently encoded controller reply.</param>
    /// <param name="valid">Whether the reply is valid.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    [Arguments("32000004d400000006340070000e490700000000", true)]
    [Arguments("32000004d400000006340070000e480700000000", false)]
    [Arguments("32000004d400000006340070000e490800000000", false)]
    public async Task DeleteValidatesObjectAndIntegrity(string reply, bool valid)
    {
        const uint objectId = 0x70000e49;
        var transport = new FakeTransport();
        await using var session = new S7PlusSession(new S7SymbolicConnectionOptions("fake"), transport);
        await session.ConnectAsync(CancellationToken.None);
        transport.ReplyOverride = _ => new(ProtocolVersion, Hex.Decode(reply));
        if (valid)
        {
            await session.DeleteObjectAsync(objectId, CancellationToken.None);
        }
        else
        {
            await TAssert.That(() => session.DeleteObjectAsync(objectId, CancellationToken.None)).Throws<InvalidDataException>();
        }

        await TAssert.That(session.IsConnected).IsEqualTo(valid);
    }

    /// <summary>Verifies self deletion sends integrity and accepts terminal status without reply integrity.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task SelfDeleteSendsIntegrityAndAcceptsTerminalStatus()
    {
        const string reply = "32000004d40000000634010000038400000000";
        const string requestSuffix = "0100000000";
        var transport = new FakeTransport();
        await using var session = new S7PlusSession(new S7SymbolicConnectionOptions("fake"), transport);
        await session.ConnectAsync(CancellationToken.None);
        TaskCompletionSource<string> sent = new(TaskCreationOptions.RunContinuationsAsynchronously);
        transport.ReplyOverride = message =>
        {
            _ = sent.TrySetResult(Hex.ToHexString(message.Payload));
            return new(ProtocolVersion, Hex.Decode(reply));
        };
        await session.DeleteObjectAsync(MainSessionId, CancellationToken.None);
        var request = await sent.Task;
        await TAssert.That(request.EndsWith(requestSuffix, StringComparison.Ordinal)).IsTrue();
    }

    /// <summary>Verifies self deletion honors caller cancellation before sending.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task SelfDeleteHonorsCallerCancellation()
    {
        var transport = new FakeTransport();
        await using var session = new S7PlusSession(new S7SymbolicConnectionOptions("fake"), transport);
        await session.ConnectAsync(CancellationToken.None);
        using var cancellation = new CancellationTokenSource();
#if NETFRAMEWORK
        cancellation.Cancel();
#else
        await cancellation.CancelAsync();
#endif
        await TAssert.That(() => session.DeleteObjectAsync(MainSessionId, cancellation.Token)).Throws<OperationCanceledException>();
    }

    /// <summary>Coordinates Fake transport.</summary>
    internal sealed class FakeTransport : IS7PlusTransport
    {
        /// <summary>The simulated controller fixture constant.</summary>
        private const int AttributeDescriptorSize = 3;

        /// <summary>The simulated session class.</summary>
        private const uint SessionClass = 287;

        /// <summary>The simulated version attribute.</summary>
        private const uint VersionAttribute = 306;

        /// <summary>The simulated version structure type.</summary>
        private const uint VersionType = 314;

        /// <summary>The controller model version field.</summary>
        private const uint PaomField = 319;

        /// <summary>The controller challenge attribute.</summary>
        private const uint ChallengeAttribute = 303;

        /// <summary>The simulated read value.</summary>
        private const int ReadValue = 42;

        /// <summary>Stores replies.</summary>
        private readonly Queue<PlusMessage> _replies = new();

        /// <summary>Stores available.</summary>
        private readonly SemaphoreSlim _available = new(0);

        /// <summary>Stores read integrity.</summary>
        private uint _readIntegrity;

        /// <summary>Stores write integrity.</summary>
        private uint _writeIntegrity;

        /// <summary>Gets or sets Wrong sequence.</summary>
        internal bool WrongSequence { get; set; }

        /// <summary>Gets or sets Wrong integrity.</summary>
        internal bool WrongIntegrity { get; set; }

        /// <summary>Gets or sets Pause reads.</summary>
        internal bool PauseReads { get; set; }

        /// <summary>Gets or sets whether transport disposal waits at the test barrier.</summary>
        internal bool PauseDisposal { get; set; }

        /// <summary>Gets the transport disposal entry barrier.</summary>
        internal TaskCompletionSource<bool> DisposalStarted { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        /// <summary>Gets the transport disposal release barrier.</summary>
        internal TaskCompletionSource<bool> AllowDisposal { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        /// <summary>Gets how many times transport cleanup ran.</summary>
        internal int DisposeCalls { get; private set; }

        /// <summary>Gets Paused read sent.</summary>
        internal TaskCompletionSource<bool> PausedReadSent { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        /// <summary>Gets Tls upgraded.</summary>
        internal bool TlsUpgraded { get; private set; }

        /// <summary>Gets Disconnected.</summary>
        internal bool Disconnected { get; private set; }

        /// <summary>Gets or sets Protection level.</summary>
        internal uint ProtectionLevel { get; set; } = 1;

        /// <summary>Gets or sets Write error.</summary>
        internal ulong WriteError { get; set; }

        /// <summary>Gets Authentication attribute.</summary>
        internal uint AuthenticationAttribute { get; private set; }

        /// <summary>Gets Authentication response.</summary>
        internal byte[] AuthenticationResponse { get; private set; } = Array.Empty<byte>();

        /// <summary>Gets Read sizes.</summary>
        internal List<int> ReadSizes { get; } = new();

        /// <summary>Gets or sets a raw scripted response for operations after bootstrap.</summary>
        internal Func<PlusMessage, PlusMessage?>? ReplyOverride { get; set; }

        /// <summary>Gets or sets the barrier before a scripted response is emitted.</summary>
        internal Func<PlusMessage, CancellationToken, Task>? BeforeScriptedReply { get; set; }

        /// <summary>Handles Connect async.</summary>
        /// <param name = "cancellationToken">The cancellationToken.</param>
        /// <returns>A task representing the operation.</returns>
        public Task ConnectAsync(CancellationToken cancellationToken) => Task.CompletedTask;

        /// <summary>Handles Upgrade to tls async.</summary>
        /// <param name = "cancellationToken">The cancellationToken.</param>
        /// <returns>A task representing the operation.</returns>
        public Task UpgradeToTlsAsync(CancellationToken cancellationToken)
        {
            TlsUpgraded = true;
            return Task.CompletedTask;
        }

        /// <summary>Handles Disconnect async.</summary>
        /// <param name = "cancellationToken">The cancellationToken.</param>
        /// <returns>A task representing the operation.</returns>
        public Task DisconnectAsync(CancellationToken cancellationToken)
        {
            Disconnected = true;
            return Task.CompletedTask;
        }

        /// <summary>Handles Export keying material.</summary>
        /// <param name = "label">The label.</param>
        /// <param name = "length">The length.</param>
        /// <returns>The operation result.</returns>
        public byte[] ExportKeyingMaterial(string label, int length) => new byte[length];

        /// <summary>Handles Dispose async.</summary>
        /// <returns>A task representing the operation.</returns>
        public async ValueTask DisposeAsync()
        {
            DisposeCalls++;
            _ = DisposalStarted.TrySetResult(true);
            if (PauseDisposal)
            {
                await AllowDisposal.Task;
            }

            _available.Dispose();
        }

        /// <summary>Handles Receive async.</summary>
        /// <param name = "cancellationToken">The cancellationToken.</param>
        /// <returns>A task representing the operation.</returns>
        public async Task<PlusMessage> ReceiveAsync(CancellationToken cancellationToken)
        {
            await _available.WaitAsync(cancellationToken);
            lock (_replies)
            {
                return _replies.Dequeue();
            }
        }

        /// <summary>Handles Send async.</summary>
        /// <param name = "message">The message.</param>
        /// <param name = "cancellationToken">The cancellationToken.</param>
        /// <returns>A task representing the operation.</returns>
        public async Task SendAsync(PlusMessage message, CancellationToken cancellationToken)
        {
            var scripted = ReplyOverride?.Invoke(message);
            if (scripted is not null)
            {
                if (BeforeScriptedReply is { } beforeReply)
                {
                    await beforeReply(message, cancellationToken);
                }

                Emit(scripted);
                return;
            }

            var reader = new PlusReader(message.Payload);
            _ = reader.Byte();
            _ = reader.UInt16();
            var function = reader.UInt16();
            _ = reader.UInt16();
            var sequence = reader.UInt16();
            _ = reader.UInt32();
            _ = reader.Byte();
            var body = new PlusWriter();
            body.VarUInt64(0);
            if (function == 0x054c && PauseReads)
            {
                _ = PausedReadSent.TrySetResult(true);
                return;
            }

            FillResponse(function, reader, body, sequence);

            var response = new PlusWriter();
            response.Byte(0x32);
            response.UInt16(0);
            response.UInt16(function);
            response.UInt16(0);
            response.UInt16(WrongSequence ? (ushort)(sequence + 1) : sequence);
            response.Byte(0x34);
            response.Bytes(body.ToArray());
            lock (_replies)
            {
                _replies.Enqueue(new(message.Version, response.ToArray()));
            }

            _ = _available.Release();
        }

        /// <summary>Queues an unsolicited controller message.</summary>
        /// <param name = "message">The controller message.</param>
        internal void Emit(PlusMessage message)
        {
            lock (_replies)
            {
                _replies.Enqueue(message);
            }

            _ = _available.Release();
        }

        /// <summary>Handles Fill session.</summary>
        /// <param name = "body">The body.</param>
        private void FillSession(PlusWriter body)
        {
            if (!TlsUpgraded)
            {
                throw new InvalidOperationException("Session allocation preceded TLS.");
            }

            body.Byte(ReadBatchSize);
            body.VarUInt32(MainSessionId);
            body.VarUInt32(SubscriptionId);
            var server = new PlusObject(MainSessionId, SessionClass);
            var fields = new Dictionary<uint, S7SymbolicValue>
            {
                [PaomField] = new(S7SymbolicDataType.WString, "1;6ES7 515-1AM01-0AB0;V3.1"),
            };
            server.Attributes.Add(
                VersionAttribute,
                new(S7SymbolicDataType.Struct, new S7SymbolicStruct(VersionType, fields)));
            server.WriteTo(body);
        }

        /// <summary>Handles Fill read.</summary>
        /// <param name = "reader">The reader.</param>
        /// <param name = "body">The body.</param>
        /// <param name = "sequence">The sequence.</param>
        private void FillRead(PlusReader reader, PlusWriter body, ushort sequence)
        {
            _ = reader.UInt32();
            var count = reader.VarUInt32();
            ReadSizes.Add((int)count);
            for (uint index = 1; index <= count; index++)
            {
                body.VarUInt32(index);
                var value = ReadValue;
                if (ReadSizes.Count == 1)
                {
                    value = index == 1 ? ReadBatchSize : WriteBatchSize;
                }

                new S7SymbolicValue(S7SymbolicDataType.DInt, value).WriteTo(body);
            }

            body.Byte(0);
            body.Byte(0);
            _readIntegrity++;
            body.VarUInt32((uint)sequence + _readIntegrity + (WrongIntegrity ? 1U : 0U));
        }

        /// <summary>Handles Fill write.</summary>
        /// <param name = "reader">The reader.</param>
        /// <param name = "body">The body.</param>
        /// <param name = "sequence">The sequence.</param>
        private void FillWrite(PlusReader reader, PlusWriter body, ushort sequence)
        {
            var objectId = reader.UInt32();
            if (objectId != 0)
            {
                body.Byte(0);
                return;
            }

            if (WriteError != 0)
            {
                body.VarUInt32(1);
                body.VarUInt64(WriteError);
            }

            body.Byte(0);
            _writeIntegrity++;
            body.VarUInt32((uint)sequence + _writeIntegrity);
        }

        /// <summary>Dispatches the bootstrap and ordinary fixture responses.</summary>
        /// <param name="function">The request function.</param>
        /// <param name="reader">The request body.</param>
        /// <param name="body">The response body.</param>
        /// <param name="sequence">The request sequence.</param>
        private void FillResponse(ushort function, PlusReader reader, PlusWriter body, ushort sequence)
        {
            if (function == 0x04ca)
            {
                FillSession(body);
            }
            else if (function == 0x054c)
            {
                FillRead(reader, body, sequence);
            }
            else if (function == 0x0542)
            {
                FillWrite(reader, body, sequence);
            }
            else if (function == 0x0586)
            {
                FillAttribute(reader, body, sequence);
            }
            else if (function == 0x04d4)
            {
                var objectId = reader.UInt32();
                body.UInt32(objectId);
                if (objectId != MainSessionId)
                {
                    _writeIntegrity++;
                    body.VarUInt32((uint)sequence + _writeIntegrity);
                }

                body.UInt32(0);
            }
            else if (function == 0x04f2)
            {
                FillAuthentication(reader, body, sequence);
            }
        }

        /// <summary>Handles Fill attribute.</summary>
        /// <param name = "reader">The reader.</param>
        /// <param name = "body">The body.</param>
        /// <param name = "sequence">The sequence.</param>
        private void FillAttribute(PlusReader reader, PlusWriter body, ushort sequence)
        {
            _ = reader.UInt32();
            reader.Skip(AttributeDescriptorSize);
            var attribute = reader.VarUInt32();
            body.Byte(0);
            if (attribute == ChallengeAttribute)
            {
                new S7SymbolicValue(S7SymbolicDataType.USInt, Hex.Decode("000102030405060708090a0b0c0d0e0f10111213"), 0x10).WriteTo(body);
            }
            else
            {
                new S7SymbolicValue(S7SymbolicDataType.UDInt, ProtectionLevel).WriteTo(body);
            }

            _readIntegrity++;
            body.VarUInt32((uint)sequence + _readIntegrity);
        }

        /// <summary>Handles Fill authentication.</summary>
        /// <param name = "reader">The reader.</param>
        /// <param name = "body">The body.</param>
        /// <param name = "sequence">The sequence.</param>
        private void FillAuthentication(PlusReader reader, PlusWriter body, ushort sequence)
        {
            _ = reader.UInt32();
            _ = reader.VarUInt32();
            AuthenticationAttribute = reader.VarUInt32();
            var value = S7SymbolicValue.ReadFrom(reader);
            if (value.Value is S7SymbolicBlob blob)
            {
                AuthenticationResponse = blob.Data;
            }
            else if (value.Value is object?[] elements)
            {
                AuthenticationResponse = new byte[elements.Length];
                for (var index = 0; index < elements.Length; index++)
                {
                    AuthenticationResponse[index] = Convert.ToByte(elements[index]);
                }
            }
            else
            {
                throw new InvalidDataException("The authentication request was not a byte array or blob.");
            }

            _writeIntegrity++;
            body.VarUInt32((uint)sequence + _writeIntegrity);
        }
    }
}
