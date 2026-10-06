// Copyright (c) 2019-2026 Chris Pulman and contributors. All rights reserved.
// Chris Pulman and contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.IO;
using System.Text;
using IoT.Driver.S7PlcRx.Symbolic;
using IoT.Driver.S7PlcRx.Symbolic.Protocol;
using Org.BouncyCastle.Utilities.Zlib;
using EventAssert = TUnit.Assertions.Assert;

namespace IoT.Driver.S7PlcRx.Tests.Symbolic;

/// <summary>Handles the plus event tests.</summary>
public sealed class PlusEventTests
{
    /// <summary>The ReadValue fixture value.</summary>
    private const int ReadValue = -123;

    /// <summary>The ProtocolVersion fixture value.</summary>
    private const int ProtocolVersion = 2;

    /// <summary>The SubscriptionIdUnsigned fixture value.</summary>
    private const uint SubscriptionIdUnsigned = 123U;

    /// <summary>The CreditTick fixture value.</summary>
    private const int CreditTick = 9;

    /// <summary>The SequenceNumberUnsigned fixture value.</summary>
    private const uint SequenceNumberUnsigned = 128U;

    /// <summary>The AddressErrorStatus fixture value.</summary>
    private const int AddressErrorStatus = 3;

    /// <summary>The QueuedValue fixture value.</summary>
    private const int QueuedValue = 12;

    /// <summary>The AssociatedByte fixture value.</summary>
    private const int AssociatedByte = 7;

    /// <summary>The AlarmClass fixture value.</summary>
    private const int AlarmClass = 2_681;

    /// <summary>The SubscriptionIdWide fixture value.</summary>
    private const ulong SubscriptionIdWide = 123UL;

    /// <summary>The EnglishLanguageId fixture value.</summary>
    private const int EnglishLanguageId = 1_033;

    /// <summary>The SubscriptionId fixture value.</summary>
    private const int SubscriptionId = 123;

    /// <summary>The RenewedCreditLimit fixture value.</summary>
    private const int RenewedCreditLimit = 15;

    /// <summary>The AlarmObjectId fixture value.</summary>
    private const int AlarmObjectId = 45;

    /// <summary>The CpuAlarmIdentifierWide fixture value.</summary>
    private const ulong CpuAlarmIdentifierWide = 543UL;

    /// <summary>The BlobPrefixLength fixture value.</summary>
    private const int BlobPrefixLength = 4;

    /// <summary>The SequenceNumber fixture value.</summary>
    private const int SequenceNumber = 128;

    /// <summary>The TimestampMicroseconds fixture value.</summary>
    private const int TimestampMicroseconds = 1_000_000;

    /// <summary>The truncated notification fixture.</summary>
    private static readonly byte[] _truncatedNotification = [0x33];

    /// <summary>The associated-value fixture bytes.</summary>
    private static readonly byte[] _associatedData = [AssociatedByte];

    /// <summary>The deliberately mismatched preset dictionary.</summary>
    private static readonly byte[] _wrongDictionary = [1];

    /// <summary>Handles the notification preserves values errors and plc timestamp.</summary>
    /// <returns>The operation result.</returns>
    [Test]
    public async Task NotificationPreservesValuesErrorsAndPlcTimestamp()
    {
        var writer = Header(true);
        writer.Byte(0x9b);
        writer.VarUInt32(1);
        new S7SymbolicValue(S7SymbolicDataType.DInt, ReadValue).WriteTo(writer);
        writer.Byte(0x13);
        writer.UInt32(ProtocolVersion);
        writer.Byte(0);
        writer.UInt32(0);
        var notification = PlusNotification.Parse(new(ProtocolVersion, writer.ToArray()));
        await EventAssert.That(notification.ObjectId).IsEqualTo(SubscriptionIdUnsigned);
        await EventAssert.That(notification.CreditTick).IsEqualTo((byte)CreditTick);
        await EventAssert.That(notification.Sequence).IsEqualTo(SequenceNumberUnsigned);
        await EventAssert.That(notification.Values[1].Value).IsEqualTo(ReadValue);
        await EventAssert.That(notification.Errors[ProtocolVersion]).IsEqualTo((byte)0x13);
        await EventAssert.That(notification.Timestamp).IsEqualTo(new DateTimeOffset(1970, 1, 1, 0, 0, 1, TimeSpan.Zero));
    }

    /// <summary>Handles the notification without timestamp does not invent one.</summary>
    /// <returns>The operation result.</returns>
    [Test]
    public async Task NotificationWithoutTimestampDoesNotInventOne()
    {
        var writer = Header(false);
        writer.Byte(0);
        var notification = PlusNotification.Parse(new(ProtocolVersion, writer.ToArray()));
        await EventAssert.That(notification.Timestamp).IsNull();
    }

    /// <summary>Handles the truncated and unsupported notifications fail.</summary>
    /// <returns>The operation result.</returns>
    [Test]
    public async Task TruncatedAndUnsupportedNotificationsFail()
    {
        await EventAssert.That(static () => PlusNotification.Parse(new(ProtocolVersion, _truncatedNotification))).Throws<InvalidDataException>();
        var writer = Header(false);
        writer.Byte(0x9c);
        writer.UInt32(1);
        await EventAssert.That(() => PlusNotification.Parse(new(ProtocolVersion, writer.ToArray()))).Throws<InvalidDataException>();
    }

    /// <summary>Handles the duplicate item references fail.</summary>
    /// <returns>The operation result.</returns>
    [Test]
    public async Task DuplicateItemReferencesFail()
    {
        var writer = Header(false);
        writer.Byte(AddressErrorStatus);
        writer.UInt32(1);
        writer.Byte(AddressErrorStatus);
        writer.UInt32(1);
        writer.Byte(0);
        await EventAssert.That(() => PlusNotification.Parse(new(ProtocolVersion, writer.ToArray()))).Throws<InvalidDataException>();
    }

    /// <summary>Handles the queue overflow is an error instead of dropping values.</summary>
    /// <returns>The operation result.</returns>
    [Test]
    public async Task QueueOverflowIsAnErrorInsteadOfDroppingValues()
    {
        var queue = new S7SymbolicSubscriptionQueue<int>(1);
        await EventAssert.That(queue.Publish(1)).IsTrue();
        await EventAssert.That(queue.Publish(ProtocolVersion)).IsFalse();
        await EventAssert.That(async () => await queue.TakeAsync(CancellationToken.None)).Throws<InvalidDataException>();
    }

    /// <summary>Handles the queue delivers values before completion.</summary>
    /// <returns>The operation result.</returns>
    [Test]
    public async Task QueueDeliversValuesBeforeCompletion()
    {
        var queue = new S7SymbolicSubscriptionQueue<int>(ProtocolVersion);
        _ = queue.Publish(QueuedValue);
        queue.Complete();
        await EventAssert.That(await queue.TakeAsync(CancellationToken.None)).IsEqualTo(QueuedValue);
        await EventAssert.That(async () => await queue.TakeAsync(CancellationToken.None)).Throws<OperationCanceledException>();
    }

    /// <summary>Handles the alarm metadata maps localized text and associated value structure.</summary>
    /// <returns>The operation result.</returns>
    [Test]
    public async Task AlarmMetadataMapsLocalizedTextAndAssociatedValueStructure()
    {
        var associated = new S7SymbolicValue(S7SymbolicDataType.Blob, new object?[] { new S7SymbolicBlob(1, _associatedData) }, 0x10);
        var eventValue = new S7SymbolicValue(S7SymbolicDataType.Struct, new S7SymbolicStruct(1, new Dictionary<uint, S7SymbolicValue> { [3476] = associated }));
        var texts = new S7SymbolicValue(
            S7SymbolicDataType.Blob,
            new Dictionary<uint, object?> { [0x04090002] = new S7SymbolicBlob(0, "Overtemperature"u8.ToArray()), [0x04070002] = new S7SymbolicBlob(0, "Temperatur"u8.ToArray()) },
            0x40);
        var metadata = new S7SymbolicAlarmMetadata(
            1,
            AlarmClass,
            new Dictionary<uint, S7SymbolicValue> { [2670] = new(S7SymbolicDataType.LWord, SubscriptionIdWide), [2673] = eventValue, [2715] = texts },
            EnglishLanguageId);
        await EventAssert.That(metadata.CpuAlarmId).IsEqualTo(SubscriptionIdWide);
        await EventAssert.That(metadata.IsComing).IsTrue();
        await EventAssert.That(metadata.Texts[ProtocolVersion]).IsEqualTo("Overtemperature");
        await EventAssert.That(metadata.GetTexts(0).Count).IsEqualTo(ProtocolVersion);
        await EventAssert.That(metadata.AssociatedValues).IsSameReferenceAs(associated);
    }

    /// <summary>Handles the initial notification is queued before activation and credit renews.</summary>
    /// <returns>The operation result.</returns>
    [Test]
    public async Task InitialNotificationIsQueuedBeforeActivationAndCreditRenews()
    {
        EventHandler<S7PlusMessageEventArgs>? handler = null;
        var credit = new TaskCompletionSource<short>(TaskCreationOptions.RunContinuationsAsynchronously);
        var deleted = new TaskCompletionSource<uint>(TaskCreationOptions.RunContinuationsAsynchronously);
        var unregistered = false;
        await using var stream = new S7SymbolicSubscriptionStream<uint>(
            value => handler = value,
            value => unregistered = true,
            (id, token) =>
        {
            _ = deleted.TrySetResult(id);
            return Task.CompletedTask;
        },
            (id, limit, token) =>
        {
            _ = credit.TrySetResult(limit);
            return Task.CompletedTask;
        },
            static value => new[] { value.Sequence },
            CancellationToken.None);
        var writer = Header(false);
        writer.Byte(0);
        handler?.Invoke(null, new S7PlusMessageEventArgs(new(ProtocolVersion, writer.ToArray())));
        stream.Activate(SubscriptionId);
        await using var iterator = stream.ReadAll().GetAsyncEnumerator();
        await EventAssert.That(await iterator.MoveNextAsync()).IsTrue();
        await EventAssert.That(iterator.Current).IsEqualTo(SequenceNumberUnsigned);
        await EventAssert.That(await credit.Task).IsEqualTo((short)RenewedCreditLimit);
        await stream.DisposeAsync();
        await EventAssert.That(await deleted.Task).IsEqualTo(SubscriptionIdUnsigned);
        await EventAssert.That(unregistered).IsTrue();
    }

    /// <summary>Handles the concurrent disposal waits for one deletion.</summary>
    /// <returns>The operation result.</returns>
    [Test]
    public async Task ConcurrentDisposalWaitsForOneDeletion()
    {
        var deletionStarted = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var deletionComplete = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var deletes = 0;
        var stream = new S7SymbolicSubscriptionStream<int>(
            static value =>
        {
        },
            static value =>
        {
        },
            async (id, token) =>
        {
            _ = Interlocked.Increment(ref deletes);
            _ = deletionStarted.TrySetResult(true);
            await deletionComplete.Task;
        },
            static (id, limit, token) => Task.CompletedTask,
            static value => Array.Empty<int>(),
            CancellationToken.None);
        stream.Activate(SubscriptionId);
        var first = stream.DisposeAsync().AsTask();
        await deletionStarted.Task;
        var second = stream.DisposeAsync().AsTask();
        await EventAssert.That(second.IsCompleted).IsFalse();
        _ = deletionComplete.TrySetResult(true);
        await Task.WhenAll(first, second);
        await EventAssert.That(deletes).IsEqualTo(1);
    }

    /// <summary>Releases PLC resources when the subscription lifetime is canceled without a consumer.</summary>
    /// <returns>The asynchronous test result.</returns>
    [Test]
    public async Task LifetimeCancellationDeletesWithoutConsumer()
    {
        using var cancellation = new CancellationTokenSource();
        var deleted = new TaskCompletionSource<uint>(TaskCreationOptions.RunContinuationsAsynchronously);
        await using var stream = new S7SymbolicSubscriptionStream<int>(
            static value =>
        {
        },
            static value =>
        {
        },
            (id, token) =>
        {
            _ = deleted.TrySetResult(id);
            return Task.CompletedTask;
        },
            static (id, limit, token) => Task.CompletedTask,
            static value => Array.Empty<int>(),
            cancellation.Token);
        stream.Activate(SubscriptionId);
#if NET8_0_OR_GREATER
        await cancellation.CancelAsync();
#else
        cancellation.Cancel();
#endif
        await EventAssert.That(await deleted.Task).IsEqualTo(SubscriptionIdUnsigned);
    }

    /// <summary>Decodes alarm notification objects and their state attributes.</summary>
    /// <returns>The asynchronous test result.</returns>
    [Test]
    public async Task AlarmNotificationDecodesObjectAttributes()
    {
        var writer = Header(false);
        writer.Byte(0);
        writer.UInt32(SubscriptionId);
        writer.UInt16(0);
        writer.Byte(0x81);
        var alarm = new PlusObject(AlarmObjectId, AlarmClass);
        alarm.Attributes[2670] = new(S7SymbolicDataType.LWord, CpuAlarmIdentifierWide);
        alarm.WriteTo(writer);
        writer.Byte(0);
        var decoded = PlusNotification.Parse(new(ProtocolVersion, writer.ToArray()));
        await EventAssert.That(decoded.AlarmObjects.Count).IsEqualTo(1);
        await EventAssert.That(decoded.AlarmObjects[0].Attributes[2670].Value).IsEqualTo(CpuAlarmIdentifierWide);
    }

    /// <summary>Preserves localization keys and prohibits external entities in comment documents.</summary>
    /// <returns>The asynchronous test result.</returns>
    [Test]
    public async Task CommentXmlPreservesTextAndRejectsDtd()
    {
        const string xml = "<CommentDictionary><Comment>Temperature</Comment></CommentDictionary>";
        var metadata = new S7SymbolicAlarmMetadata(
            1,
            1,
            new Dictionary<uint, S7SymbolicValue> { [4288] = new(S7SymbolicDataType.WString, new Dictionary<uint, object?> { [EnglishLanguageId] = xml }, 0x40), });
        await EventAssert.That(metadata.GetCommentXml()["4288:1033"]).IsEqualTo(xml);
        var invalid = new S7SymbolicAlarmMetadata(
            1,
            1,
            new Dictionary<uint, S7SymbolicValue>
{
    [4288] = new(
    S7SymbolicDataType.WString,
    new Dictionary<uint, object?> { [EnglishLanguageId] = "<!DOCTYPE x [<!ENTITY a SYSTEM 'file:///invalid'>]><x>&a;</x>" },
    0x40),
});
        await EventAssert.That(() => invalid.GetCommentXml()).Throws<System.Xml.XmlException>();
    }

    /// <summary>Decodes caller dictionary compressed XML and rejects a dictionary with the wrong Adler32.</summary>
    /// <returns>The asynchronous test result.</returns>
    [Test]
    public async Task CommentsUseCallerSuppliedDictionaryBytes()
    {
        var dictionary = "<Example><Text>Caller dictionary vocabulary.</Text></Example>"u8.ToArray();
        const string xml = "<Example><Text>Caller dictionary vocabulary.</Text></Example>";
        var bytes = CompressXml(xml, dictionary);
        var metadata = new S7SymbolicAlarmMetadata(
            1,
            1,
            new Dictionary<uint, S7SymbolicValue> { [2546] = new(S7SymbolicDataType.Blob, new Dictionary<uint, object?> { [1] = new S7SymbolicBlob(0, bytes) }, 0x40), });
        uint requestedId = 0;
        var decoded = metadata.GetCommentXml(id =>
        {
            requestedId = id;
            return dictionary;
        });
        await EventAssert.That(decoded["2546:1"]).IsEqualTo(xml);
        await EventAssert.That(requestedId).IsNotEqualTo(0U);
        await EventAssert.That(() => metadata.GetCommentXml()).Throws<InvalidDataException>();
        await EventAssert.That(() => metadata.GetCommentXml(static id => _wrongDictionary)).Throws<InvalidDataException>();
        await EventAssert.That(metadata.Attributes[2546].Value).IsNotNull();
    }

    /// <summary>Terminates waiting observers when the connection closes.</summary>
    /// <returns>The asynchronous test result.</returns>
    [Test]
    public async Task ConnectionFailureTerminatesObservers()
    {
        var stream = new S7SymbolicSubscriptionStream<int>(
            static value =>
        {
        },
            static value =>
        {
        },
            static (id, token) => Task.CompletedTask,
            static (id, limit, token) => Task.CompletedTask,
            static value => Array.Empty<int>(),
            CancellationToken.None);
        stream.Activate(SubscriptionId);
        await using var iterator = stream.ReadAll().GetAsyncEnumerator();
        var waiting = iterator.MoveNextAsync().AsTask();
        stream.Fail(new IOException("PLC closed"));
        await EventAssert.That(() => waiting).Throws<IOException>();
        await EventAssert.That(() => stream.DisposeAsync().AsTask()).Throws<IOException>();
    }

    /// <summary>Generates independent zlib fixture bytes with an optional preset dictionary.</summary>
    /// <param name="xml">The fixture XML.</param>
    /// <param name="dictionary">The caller fixture dictionary.</param>
    /// <returns>A PLC-prefixed zlib blob.</returns>
    private static byte[] CompressXml(string xml, byte[] dictionary)
    {
        var compressor = new ZStream();
        if (compressor.deflateInit(JZlib.Z_DEFAULT_COMPRESSION) != JZlib.Z_OK)
        {
            throw new InvalidDataException("Fixture compressor initialization failed.");
        }

        try
        {
            if (compressor.deflateSetDictionary(dictionary, dictionary.Length) != JZlib.Z_OK)
            {
                throw new InvalidDataException("Fixture dictionary setup failed.");
            }

            compressor.next_in = Encoding.UTF8.GetBytes(xml);
            compressor.avail_in = compressor.next_in.Length;
            compressor.next_out = new byte[4096];
            compressor.avail_out = compressor.next_out.Length;
            if (compressor.deflate(JZlib.Z_FINISH) != JZlib.Z_STREAM_END)
            {
                throw new InvalidDataException("Fixture compression failed.");
            }

            var result = new byte[compressor.next_out_index + BlobPrefixLength];
            Buffer.BlockCopy(compressor.next_out, 0, result, BlobPrefixLength, compressor.next_out_index);
            return result;
        }
        finally
        {
            _ = compressor.deflateEnd();
        }
    }

    /// <summary>Handles the header.</summary>
    /// <param name="timestamp">The timestamp.</param>
    /// <returns>The operation result.</returns>
    private static PlusWriter Header(bool timestamp)
    {
        var writer = new PlusWriter();
        writer.Byte(0x33);
        writer.UInt32(SubscriptionId);
        writer.UInt16(0);
        writer.UInt16(0);
        writer.UInt16(0);
        writer.Byte(CreditTick);
        writer.VarUInt32(SequenceNumber);
        if (timestamp)
        {
            writer.UInt64(TimestampMicroseconds);
            writer.Byte(1);
        }
        else
        {
            writer.Byte(1);
        }

        return writer;
    }
}
