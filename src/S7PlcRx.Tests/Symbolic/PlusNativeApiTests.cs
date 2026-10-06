// Copyright (c) 2019-2026 Chris Pulman and contributors. All rights reserved.
// Chris Pulman and contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.IO;
using IoT.Driver.S7PlcRx.Symbolic;
using IoT.Driver.S7PlcRx.Symbolic.Protocol;
using Org.BouncyCastle.Utilities.Encoders;
using TAssert = TUnit.Assertions.Assert;

namespace IoT.Driver.S7PlcRx.Tests.Symbolic;

/// <summary>Exercises public native APIs against independently encoded controller replies.</summary>
public sealed class PlusNativeApiTests
{
    /// <summary>The fixture LCID.</summary>
    private const uint English = 1033;

    /// <summary>The allocated subscription identifier.</summary>
    private const uint Subscription = 123;

    /// <summary>The subscribed marker local identifier.</summary>
    private const uint MarkerMember = 12;

    /// <summary>The native marker access area.</summary>
    private const uint MarkerAccessArea = 82;

    /// <summary>The reference array attribute.</summary>
    private const uint References = 1048;

    /// <summary>The subscription credit attribute.</summary>
    private const uint Credit = 1053;

    /// <summary>The required credit after the ninth tick.</summary>
    private const short RenewedCredit = 15;

    /// <summary>The requested variable cycle.</summary>
    private const int Cycle = 250;

    /// <summary>The received fixture value.</summary>
    private const uint FixtureValue = 42;

    /// <summary>The subscription class identifier.</summary>
    private const uint SubscriptionClass = 1001;

    /// <summary>The initial subscription credits.</summary>
    private const short InitialCredit = 10;

    /// <summary>The number of localized fixture languages.</summary>
    private const int LanguageCount = 2;

    /// <summary>The native fixture alarm state.</summary>
    private const byte AlarmState = 3;

    /// <summary>The native fixture alarm domain.</summary>
    private const ushort AlarmDomain = 7;

    /// <summary>The alarm function class.</summary>
    private const byte AlarmFunctionClass = 2;

    /// <summary>The alarm subsystem identifier.</summary>
    private const uint AlarmSubsystem = 8;

    /// <summary>The requested RUN state.</summary>
    private const int RunState = 3;

    /// <summary>The shared fixture alarm text.</summary>
    private const string FixtureAlarmText = "Fixture alarm";

    /// <summary>The fixture localized comment XML.</summary>
    private const string FixtureComment = "<Comment>Fixture</Comment>";

    /// <summary>The object comment attribute.</summary>
    private const uint ObjectComment = 4288;

    /// <summary>The fixture subscribed path.</summary>
    private const string MarkerPath = "MArea.Counter";

    /// <summary>The expected native marker reference array.</summary>
    private static readonly object?[] ExpectedReferences = [0x80010000U, 0U, 1U, 0x80040002U, 1U, 0U, MarkerAccessArea, 0U, 3736U, MarkerMember];

    /// <summary>The local member path.</summary>
    private static readonly uint[] ExpectedMembers = [MarkerMember];

    /// <summary>The requested alarm language array.</summary>
    private static readonly object?[] ExpectedLanguages = [English];

    /// <summary>The expected requested comment metadata.</summary>
    private static readonly uint[] ExpectedCommentAttributes = [ObjectComment, 2546];

    /// <summary>Checks actual symbol resolution, creation, early delivery, credit and deletion.</summary>
    /// <returns>The asynchronous test.</returns>
    [Test]
    public async Task VariableSubscriptionRoutesInitialNotificationAndReleasesNativeObject()
    {
        var peer = new NativePeer(false, new PlusSessionTests.FakeTransport());
        await using var client = peer.CreateClient();
        await client.ConnectAsync();
        peer.Start();
        await using var subscription = await client.SubscribeAsync([MarkerPath], TimeSpan.FromMilliseconds(Cycle));
        await using var iterator = subscription.Changes.GetAsyncEnumerator();
        await TAssert.That(await iterator.MoveNextAsync()).IsTrue();
        await TAssert.That(iterator.Current.Path).IsEqualTo(MarkerPath);
        await TAssert.That(iterator.Current.Value?.Value).IsEqualTo(FixtureValue);
        await TAssert.That(iterator.Current.Address.AccessArea).IsEqualTo(MarkerAccessArea);
        await TAssert.That(PlusLoopbackWire.Equal(iterator.Current.Address.LocalIds, ExpectedMembers)).IsTrue();
        var created = peer.Created ?? throw new InvalidDataException("No native Create request.");
        await TAssert.That(created.ClassId).IsEqualTo(SubscriptionClass);
        await TAssert.That(created.Attributes[1049].Value).IsEqualTo((uint)Cycle);
        await TAssert.That(created.Attributes[1040].Value).IsEqualTo((byte)0x14);
        await TAssert.That(created.Attributes[Credit].Value).IsEqualTo(InitialCredit);
        await TAssert.That(PlusLoopbackWire.Equal((object?[])created.Attributes[References].Value!, ExpectedReferences)).IsTrue();
        await TAssert.That(await peer.CreditUpdated.Task).IsEqualTo(RenewedCredit);
        await subscription.DisposeAsync();
        await TAssert.That(peer.Deleted).IsEqualTo(Subscription);
    }

    /// <summary>Checks alarm LCID configuration and native state, text and associated-value projection.</summary>
    /// <returns>The asynchronous test.</returns>
    [Test]
    public async Task AlarmSubscriptionRequestsLanguageAndProjectsNativeMetadata()
    {
        var peer = new NativePeer(true, new PlusSessionTests.FakeTransport());
        await using var client = peer.CreateClient();
        await client.ConnectAsync();
        peer.Start();
        await using var subscription = await client.SubscribeAlarmsAsync(English);
        await using var iterator = subscription.Alarms.GetAsyncEnumerator();
        await TAssert.That(await iterator.MoveNextAsync()).IsTrue();
        var metadata = iterator.Current.Metadata;
        await TAssert.That(metadata.LanguageId).IsEqualTo(English);
        await TAssert.That(metadata.Texts[2]).IsEqualTo(FixtureAlarmText);
        await TAssert.That(metadata.GetTexts(0).Count).IsEqualTo(LanguageCount);
        await TAssert.That(metadata.State).IsEqualTo((byte?)AlarmState);
        await TAssert.That(metadata.Domain).IsEqualTo((ushort?)AlarmDomain);
        await TAssert.That(metadata.IsComing).IsTrue();
        await TAssert.That(metadata.AssociatedValues?.Value).IsEqualTo(FixtureValue);
        var created = peer.Created ?? throw new InvalidDataException("No alarm Create request.");
        await TAssert.That(created.Children.Count).IsEqualTo(1);
        var reference = created.Children[0];
        await TAssert.That(created.Attributes[1082].Value).IsEqualTo(AlarmFunctionClass);
        await TAssert.That(PlusLoopbackWire.Equal((object?[])reference.Attributes[8181].Value!, ExpectedLanguages)).IsTrue();
        await TAssert.That((bool)reference.Attributes[8173].Value!).IsTrue();
        await TAssert.That(reference.Relations[2660]).IsEqualTo(AlarmSubsystem);
        await TAssert.That(await peer.CreditUpdated.Task).IsEqualTo(RenewedCredit);
        await subscription.DisposeAsync();
        await TAssert.That(peer.Deleted).IsEqualTo(Subscription);
    }

    /// <summary>Checks RUN request serialization and the explicitly requested state read.</summary>
    /// <returns>The asynchronous test.</returns>
    [Test]
    public async Task RequestedCpuStateUsesExecutionUnitAttribute()
    {
        var peer = new NativePeer(false, new PlusSessionTests.FakeTransport());
        await using var client = peer.CreateClient();
        await client.ConnectAsync();
        peer.Start();
        await client.SetOperatingStateAsync(S7OperatingState.Run);
        await TAssert.That(peer.StateRequest).IsEqualTo(RunState);
        await TAssert.That(await client.GetRequestedOperatingStateAsync()).IsEqualTo(S7OperatingState.Run);
        await TAssert.That(() => client.SetOperatingStateAsync(S7OperatingState.None)).Throws<ArgumentOutOfRangeException>();
    }

    /// <summary>Checks alarm browsing localization and comment exploration through public APIs.</summary>
    /// <returns>The asynchronous test.</returns>
    [Test]
    public async Task BrowseAndCommentsPreserveControllerMetadata()
    {
        var peer = new NativePeer(true, new PlusSessionTests.FakeTransport());
        await using var client = peer.CreateClient();
        await client.ConnectAsync();
        peer.Start();
        var alarms = await client.BrowseAlarmsAsync(English);
        await TAssert.That(alarms.Count).IsEqualTo(1);
        await TAssert.That(alarms[0].Texts[2]).IsEqualTo(FixtureAlarmText);
        var comments = await client.GetCommentsAsync(Subscription);
        await TAssert.That(comments.Count).IsEqualTo(1);
        await TAssert.That(comments[0].GetCommentXml()["4288:1033"]).IsEqualTo(FixtureComment);
        await TAssert.That(PlusLoopbackWire.Equal(peer.CommentAttributes, ExpectedCommentAttributes)).IsTrue();
    }

    /// <summary>Drains an in-flight credit transaction before deletion without closing the client session.</summary>
    /// <returns>The asynchronous test.</returns>
    [Test]
    public async Task SubscriptionDisposalDrainsCreditReplyAndKeepsClientConnected()
    {
        var peer = new NativePeer(false, new PlusSessionTests.FakeTransport());
        await using var client = peer.CreateClient();
        await client.ConnectAsync();
        peer.Start();
        peer.PauseCreditReplies();
        await using var subscription = await client.SubscribeAsync([MarkerPath], TimeSpan.FromMilliseconds(Cycle));
        await peer.CreditReplyBlocked.Task;
        var disposal = subscription.DisposeAsync().AsTask();
        try
        {
            await TAssert.That(disposal.IsCompleted).IsFalse();
            await TAssert.That(peer.Deleted).IsEqualTo(0U);
        }
        finally
        {
            _ = peer.ReleaseCreditReply.TrySetResult(true);
        }

        await disposal;
        await TAssert.That(peer.Deleted).IsEqualTo(Subscription);
        await TAssert.That(client.Session.IsConnected).IsTrue();
        await TAssert.That(await client.GetRequestedOperatingStateAsync()).IsEqualTo(S7OperatingState.Run);
    }

    /// <summary>Composes the bootstrap transport with a bounded native API peer.</summary>
    /// <param name="alarms">Whether the fixture serves native alarm objects.</param>
    /// <param name="transport">The transport whose ownership passes to the client session.</param>
    private sealed class NativePeer(bool alarms, PlusSessionTests.FakeTransport transport)
    {
        /// <summary>The request body offset.</summary>
        private const int BodyOffset = 14;

        /// <summary>The response protocol version.</summary>
        private const byte Version = 2;

        /// <summary>The native marker type relation.</summary>
        private const uint MarkerType = 0x90030000;

        /// <summary>The alarm text attribute.</summary>
        private const uint AlarmTexts = 2715;

        /// <summary>The fixed qualifier identifier width.</summary>
        private const int FixedWidth = 4;

        /// <summary>The typed scalar header width.</summary>
        private const int ValueHeaderWidth = 2;

        /// <summary>The request descriptor width.</summary>
        private const int DescriptorWidth = 3;

        /// <summary>The primitive native metadata declaration kind.</summary>
        private const int PrimitiveKind = 8;

        /// <summary>The native PLC program object.</summary>
        private const uint ProgramObject = 3;

        /// <summary>The subscription session relation.</summary>
        private const uint SubscriptionSession = 901;

        /// <summary>The main client session relation.</summary>
        private const uint MainSession = 900;

        /// <summary>The execution unit relation.</summary>
        private const uint ExecutionUnit = 52;

        /// <summary>The CPU requested state attribute.</summary>
        private const uint RequestedState = 2167;

        /// <summary>The fixture alarm object.</summary>
        private const uint AlarmObjectId = 45;

        /// <summary>The fixture alarm class.</summary>
        private const uint AlarmClass = 2681;

        /// <summary>The state attribute.</summary>
        private const uint StateAttribute = 2671;

        /// <summary>The domain attribute.</summary>
        private const uint DomainAttribute = 2672;

        /// <summary>The coming event attribute.</summary>
        private const uint ComingAttribute = 2673;

        /// <summary>The associated values field.</summary>
        private const uint AssociatedValues = 3476;

        /// <summary>The bootstrap-capable fake transport.</summary>
        private readonly PlusSessionTests.FakeTransport _transport = transport;

        /// <summary>Gets the native created subscription.</summary>
        internal PlusObject? Created { get; private set; }

        /// <summary>Gets the deleted relation.</summary>
        internal uint Deleted { get; private set; }

        /// <summary>Gets the captured RUN or STOP request.</summary>
        internal int StateRequest { get; private set; }

        /// <summary>Gets the requested comment attribute identifiers.</summary>
        internal uint[] CommentAttributes { get; private set; } = [];

        /// <summary>Gets the deterministic credit request barrier.</summary>
        internal TaskCompletionSource<short> CreditUpdated { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        /// <summary>Gets the in-flight credit response barrier.</summary>
        internal TaskCompletionSource<bool> CreditReplyBlocked { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        /// <summary>Gets the credit response release barrier.</summary>
        internal TaskCompletionSource<bool> ReleaseCreditReply { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        /// <summary>Composes the public client with a fake session.</summary>
        /// <returns>The disposable client.</returns>
        internal S7SymbolicClient CreateClient() => new(new S7PlusSession(new S7SymbolicConnectionOptions("fake"), _transport));

        /// <summary>Enables API replies after the established bootstrap.</summary>
        internal void Start() => _transport.ReplyOverride = Reply;

        /// <summary>Blocks only the native credit response until the test releases it.</summary>
        internal void PauseCreditReplies() => _transport.BeforeScriptedReply = WaitForCreditReleaseAsync;

        /// <summary>Reads only the execution unit requested state attribute.</summary>
        /// <param name="reader">The request body.</param>
        /// <param name="id">The execution unit.</param>
        /// <param name="sequence">The request sequence.</param>
        /// <returns>The Get response body.</returns>
        private static byte[] Get(PlusReader reader, uint id, ushort sequence)
        {
            reader.Skip(DescriptorWidth);
            if (id != ExecutionUnit || reader.VarUInt32() != RequestedState)
            {
                throw new InvalidDataException("Wrong requested-state attribute.");
            }

            reader.Skip(PlusLoopbackWire.Qualifier().Length + ValueHeaderWidth);
            return PlusLoopbackWire.Join(Hex.Decode("0000000803"), PlusLoopbackWire.Unsigned(sequence + reader.VarUInt32()));
        }

        /// <summary>Encodes two languages, native state and an associated-value event independently.</summary>
        /// <returns>The complete native alarm object.</returns>
        private static byte[] AlarmObject()
        {
            var texts = PlusLoopbackWire.Join(
                Hex.Decode("4014"),
                PlusLoopbackWire.Unsigned(0x04090002),
                PlusLoopbackWire.Slice(PlusLoopbackWire.Blob("Fixture alarm"u8.ToArray()), ValueHeaderWidth),
                PlusLoopbackWire.Unsigned(0x04070002),
                PlusLoopbackWire.Slice(PlusLoopbackWire.Blob("Fixture deutsch"u8.ToArray()), ValueHeaderWidth),
                Hex.Decode("00"));
            var coming = PlusLoopbackWire.Join(Hex.Decode("001700000001"), PlusLoopbackWire.Unsigned(AssociatedValues), PlusLoopbackWire.Number(FixtureValue), Hex.Decode("00"));
            return PlusLoopbackWire.Object(
                AlarmObjectId,
                AlarmClass,
                PlusLoopbackWire.Join(
                    PlusLoopbackWire.Attribute(AlarmTexts, texts),
                    PlusLoopbackWire.Attribute(StateAttribute, Hex.Decode("000203")),
                    PlusLoopbackWire.Attribute(DomainAttribute, Hex.Decode("00030007")),
                    PlusLoopbackWire.Attribute(ComingAttribute, coming)));
        }

        /// <summary>Holds a credit reply after transmission has begun.</summary>
        /// <param name="request">The outgoing native request.</param>
        /// <param name="cancellationToken">The bounded session operation lifetime.</param>
        /// <returns>The barrier operation.</returns>
        private async Task WaitForCreditReleaseAsync(PlusMessage request, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var reader = new PlusReader(request.Payload);
            reader.Skip(DescriptorWidth);
            if (reader.UInt16() != 0x04f2)
            {
                return;
            }

            reader.Position = BodyOffset;
            if (reader.UInt32() != Subscription)
            {
                return;
            }

            _ = CreditReplyBlocked.TrySetResult(true);
            await ReleaseCreditReply.Task;
        }

        /// <summary>Reads one request and returns independently encoded response bytes.</summary>
        /// <param name="request">The request packet.</param>
        /// <returns>The native response.</returns>
        private PlusMessage Reply(PlusMessage request)
        {
            var header = new PlusReader(request.Payload);
            header.Skip(DescriptorWidth);
            var function = header.UInt16();
            header.Skip(ValueHeaderWidth);
            var sequence = header.UInt16();
            var reader = new PlusReader(PlusLoopbackWire.Slice(request.Payload, BodyOffset));
            var id = reader.UInt32();
            var body = function switch
            {
                0x04ca => Create(reader, id, sequence),
                0x04bb => Explore(reader, id, sequence),
                0x04f2 => Set(reader, id, sequence),
                0x0586 => Get(reader, id, sequence),
                0x04d4 => Delete(reader, id, sequence),
                _ => throw new InvalidDataException("Unexpected API function.")
            };
            return new(Version, PlusLoopbackWire.Join(Hex.Decode("320000"), [(byte)(function >> 8), (byte)function, 0, 0, (byte)(sequence >> 8), (byte)sequence, 0x34], body));
        }

        /// <summary>Captures a native Create and emits the notification before its response.</summary>
        /// <param name="reader">The request body.</param>
        /// <param name="parent">The parent relation.</param>
        /// <param name="sequence">The request sequence.</param>
        /// <returns>The Create response body.</returns>
        private byte[] Create(PlusReader reader, uint parent, ushort sequence)
        {
            if (parent != SubscriptionSession)
            {
                throw new InvalidDataException("Subscription was created outside its subscription session.");
            }

            _ = S7SymbolicValue.ReadFrom(reader);
            _ = reader.UInt32();
            var integrity = reader.VarUInt32();
            Created = PlusObject.ReadFrom(reader);
            var header = PlusLoopbackWire.Join(Hex.Decode("33"), PlusLoopbackWire.Identifier(Subscription), Hex.Decode("00000000000009800101"));
            var contents = alarms
                ? PlusLoopbackWire.Join(Hex.Decode("00"), PlusLoopbackWire.Identifier(Subscription), Hex.Decode("000081"), AlarmObject(), Hex.Decode("00"))
                : Hex.Decode("9b0100042a00");
            _transport.Emit(new(Version, PlusLoopbackWire.Join(header, contents)));
            return PlusLoopbackWire.Join(Hex.Decode("0001"), PlusLoopbackWire.Unsigned(Subscription), PlusLoopbackWire.Unsigned(sequence + integrity), new byte[FixedWidth]);
        }

        /// <summary>Replies with native marker type, alarm or localized comment metadata.</summary>
        /// <param name="reader">The request body.</param>
        /// <param name="id">The explored relation.</param>
        /// <param name="sequence">The request sequence.</param>
        /// <returns>The Explore response body.</returns>
        private byte[] Explore(PlusReader reader, uint id, ushort sequence)
        {
            _ = reader.VarUInt32();
            reader.Skip(FixedWidth);
            var attributes = new uint[reader.VarUInt32()];
            for (var index = 0; index < attributes.Length; index++)
            {
                attributes[index] = reader.VarUInt32();
            }

            var integrity = reader.VarUInt32();
            var objects = Array.Empty<byte>();
            if (id == MarkerType)
            {
                objects = PlusLoopbackWire.Object(id, 1, PlusLoopbackWire.Metadata(PlusLoopbackWire.Member(MarkerMember, PrimitiveKind, 0), "Counter"));
            }
            else if (id == ProgramObject && alarms)
            {
                objects = AlarmObject();
            }
            else if (id == Subscription)
            {
                CommentAttributes = attributes;
                var localized = PlusLoopbackWire.Join(
                    Hex.Decode("4015"),
                    PlusLoopbackWire.Unsigned(English),
                    PlusLoopbackWire.Slice(PlusLoopbackWire.Text(FixtureComment), ValueHeaderWidth),
                    Hex.Decode("00"));
                objects = PlusLoopbackWire.Object(id, 1, PlusLoopbackWire.Attribute(ObjectComment, localized));
            }

            return PlusLoopbackWire.Join(Hex.Decode("00"), PlusLoopbackWire.Identifier(id), PlusLoopbackWire.Unsigned(sequence + integrity), objects, new byte[FixedWidth]);
        }

        /// <summary>Captures credit and CPU requests and validates their target relation.</summary>
        /// <param name="reader">The request body.</param>
        /// <param name="id">The target relation.</param>
        /// <param name="sequence">The request sequence.</param>
        /// <returns>The Set response body.</returns>
        private byte[] Set(PlusReader reader, uint id, ushort sequence)
        {
            _ = reader.VarUInt32();
            var attribute = reader.VarUInt32();
            var value = S7SymbolicValue.ReadFrom(reader);
            if (id == Subscription && attribute == Credit)
            {
                _ = CreditUpdated.TrySetResult((short)(value.Value ?? throw new InvalidDataException("No credit.")));
            }
            else if (id == ExecutionUnit && attribute == RequestedState)
            {
                StateRequest = (int)(value.Value ?? throw new InvalidDataException("No state."));
            }
            else
            {
                throw new InvalidDataException("Wrong Set target.");
            }

            reader.Skip(PlusLoopbackWire.Qualifier().Length + 1);
            return PlusLoopbackWire.Join(Hex.Decode("00"), PlusLoopbackWire.Unsigned(sequence + reader.VarUInt32()));
        }

        /// <summary>Captures native subscription cleanup.</summary>
        /// <param name="reader">The request body.</param>
        /// <param name="id">The deleted relation.</param>
        /// <param name="sequence">The request sequence.</param>
        /// <returns>The Delete response body.</returns>
        private byte[] Delete(PlusReader reader, uint id, ushort sequence)
        {
            Deleted = id;
            reader.Skip(PlusLoopbackWire.Qualifier().Length + 1);
            return id == MainSession
                ? PlusLoopbackWire.Join(Hex.Decode("00"), PlusLoopbackWire.Identifier(id))
                : PlusLoopbackWire.Join(Hex.Decode("00"), PlusLoopbackWire.Identifier(id), PlusLoopbackWire.Unsigned(sequence + reader.VarUInt32()));
        }
    }
}
