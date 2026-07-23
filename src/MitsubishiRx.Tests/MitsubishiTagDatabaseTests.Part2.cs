// Copyright (c) 2019-2026 Chris Pulman and contributors. All rights reserved.
// Chris Pulman and contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

#if REACTIVE_SHIM

namespace IoT.DriverCore.MitsubishiRx.Reactive.Tests;
#else

namespace IoT.DriverCore.MitsubishiRx.Tests;
#endif

/// <summary>Provides additional Mitsubishi tag database tests.</summary>
internal sealed partial class MitsubishiTagDatabaseTests
{
    /// <summary>Stores the <c>TagDatabasePart2LoopbackHost</c> test value.</summary>
    private const string TagDatabasePart2LoopbackHost = "127.0.0.1";

    /// <summary>Stores the <c>OperatorMessageTagName</c> test value.</summary>
    private const string OperatorMessageTagName = "OperatorMessage";

    /// <summary>Stores the expected scaled head-temperature value.</summary>
    private const double ExpectedScaledHeadTemperature = 15.0D;

    /// <summary>Stores the raw head-temperature word used for inverse scaling.</summary>
    private const ushort RawHeadTemperatureWord = 250;

    /// <summary>Stores the operator-message length in words.</summary>
    private const int OperatorMessageLengthWords = 2;

    /// <summary>Stores the expected signed temperature value.</summary>
    private const short ExpectedSignedTemperature = -100;

    /// <summary>Stores the expected scaled signed-temperature value.</summary>
    private const double ExpectedScaledSignedTemperature = -10.0D;

    /// <summary>Executes the ReadScaledDoubleByTagAsyncAppliesScaleAndOffset operation.</summary>
    /// <returns>The ReadScaledDoubleByTagAsyncAppliesScaleAndOffset operation result.</returns>
    [Test]
    internal async Task ReadScaledDoubleByTagAsyncAppliesScaleAndOffsetAsync()
    {
        await using var transport = new FakeTransport(
        [
            [0xD0, 0x00, 0x00, 0xFF, 0xFF, 0x03, 0x00, 0x04, 0x00, 0x00, 0x00, 0xFA, 0x00],
        ]);

        var options = new MitsubishiClientOptions(
            Host: TagDatabasePart2LoopbackHost,
            Port: 5025,
            FrameType: MitsubishiFrameType.ThreeE,
            DataCode: CommunicationDataCode.Binary,
            TransportKind: MitsubishiTransportKind.Tcp,
            Route: MitsubishiRoute.Default);

        await using var client = new MitsubishiRx(options, transport, Scheduler.Immediate)
        {
            TagDatabase = MitsubishiTagDatabase.FromCsv("""
Name,Address,DataType,Scale,Offset
HeadTemp,D200,Word,0.1,-10
"""),
        };

        var result = await client.ReadScaledDoubleByTagAsync("HeadTemp", CancellationToken.None);

        await Assert.That(result.IsSucceed).IsTrue();
        await Assert.That(result.Value).IsEqualTo(ExpectedScaledHeadTemperature);
    }

    /// <summary>Executes the WriteScaledDoubleByTagAsyncAppliesInverseScaleAndOffset operation.</summary>
    /// <returns>The WriteScaledDoubleByTagAsyncAppliesInverseScaleAndOffset operation result.</returns>
    [Test]
    internal async Task WriteScaledDoubleByTagAsyncAppliesInverseScaleAndOffsetAsync()
    {
        await using var transport = new FakeTransport(
        [
            [0xD0, 0x00, 0x00, 0xFF, 0xFF, 0x03, 0x00, 0x02, 0x00, 0x00, 0x00],
        ]);

        var options = new MitsubishiClientOptions(
            Host: TagDatabasePart2LoopbackHost,
            Port: 5026,
            FrameType: MitsubishiFrameType.ThreeE,
            DataCode: CommunicationDataCode.Binary,
            TransportKind: MitsubishiTransportKind.Tcp,
            Route: MitsubishiRoute.Default);

        await using var client = new MitsubishiRx(options, transport, Scheduler.Immediate)
        {
            TagDatabase = MitsubishiTagDatabase.FromCsv("""
Name,Address,DataType,Scale,Offset
HeadTemp,D200,Word,0.1,-10
"""),
        };

        await using var rawTransport = new FakeTransport(
        [
            [0xD0, 0x00, 0x00, 0xFF, 0xFF, 0x03, 0x00, 0x02, 0x00, 0x00, 0x00],
        ]);

        await using var rawClient = new MitsubishiRx(options, rawTransport, Scheduler.Immediate);
        var baseline = await rawClient.WriteWordsAsync("D200", [RawHeadTemperatureWord], CancellationToken.None);
        var result = await client.WriteScaledDoubleByTagAsync(
            "HeadTemp",
            ExpectedScaledHeadTemperature,
            CancellationToken.None);

        await Assert.That(baseline.IsSucceed).IsTrue();
        await Assert.That(result.IsSucceed).IsTrue();
        await Assert.That(transport.Requests[0].Payload).IsEquivalentTo(rawTransport.Requests[0].Payload);
    }

    /// <summary>Executes the ReadStringByTagAsyncDecodesPackedAsciiWords operation.</summary>
    /// <returns>The ReadStringByTagAsyncDecodesPackedAsciiWords operation result.</returns>
    [Test]
    internal async Task ReadStringByTagAsyncDecodesPackedAsciiWordsAsync()
    {
        await using var transport = new FakeTransport(
        [
            [0xD0, 0x00, 0x00, 0xFF, 0xFF, 0x03, 0x00, 0x06, 0x00, 0x00, 0x00, 0x4F, 0x4B, 0x21, 0x00],
        ]);

        var options = new MitsubishiClientOptions(
            Host: TagDatabasePart2LoopbackHost,
            Port: 5027,
            FrameType: MitsubishiFrameType.ThreeE,
            DataCode: CommunicationDataCode.Binary,
            TransportKind: MitsubishiTransportKind.Tcp,
            Route: MitsubishiRoute.Default);

        await using var client = new MitsubishiRx(options, transport, Scheduler.Immediate)
        {
            TagDatabase = MitsubishiTagDatabase.FromCsv("""
Name,Address,DataType
OperatorMessage,D600,String
"""),
        };

        var result = await client.ReadStringByTagAsync(
            OperatorMessageTagName,
            OperatorMessageLengthWords,
            CancellationToken.None);

        await Assert.That(result.IsSucceed).IsTrue();
        await Assert.That(result.Value).IsEqualTo("OK!");
    }

    /// <summary>Executes the WriteStringByTagAsyncEncodesPackedAsciiWords operation.</summary>
    /// <returns>The WriteStringByTagAsyncEncodesPackedAsciiWords operation result.</returns>
    [Test]
    internal async Task WriteStringByTagAsyncEncodesPackedAsciiWordsAsync()
    {
        await using var transport = new FakeTransport(
        [
            [0xD0, 0x00, 0x00, 0xFF, 0xFF, 0x03, 0x00, 0x02, 0x00, 0x00, 0x00],
        ]);

        var options = new MitsubishiClientOptions(
            Host: TagDatabasePart2LoopbackHost,
            Port: 5028,
            FrameType: MitsubishiFrameType.ThreeE,
            DataCode: CommunicationDataCode.Binary,
            TransportKind: MitsubishiTransportKind.Tcp,
            Route: MitsubishiRoute.Default);

        await using var client = new MitsubishiRx(options, transport, Scheduler.Immediate)
        {
            TagDatabase = MitsubishiTagDatabase.FromCsv("""
Name,Address,DataType
OperatorMessage,D600,String
"""),
        };

        await using var rawTransport = new FakeTransport(
        [
            [0xD0, 0x00, 0x00, 0xFF, 0xFF, 0x03, 0x00, 0x02, 0x00, 0x00, 0x00],
        ]);

        await using var rawClient = new MitsubishiRx(options, rawTransport, Scheduler.Immediate);
        var baseline = await rawClient.WriteWordsAsync("D600", [0x4B4F, 0x0021], CancellationToken.None);
        var result = await client.WriteStringByTagAsync(
            OperatorMessageTagName,
            "OK!",
            OperatorMessageLengthWords,
            CancellationToken.None);

        await Assert.That(baseline.IsSucceed).IsTrue();
        await Assert.That(result.IsSucceed).IsTrue();
        await Assert.That(transport.Requests[0].Payload).IsEquivalentTo(rawTransport.Requests[0].Payload);
    }

    /// <summary>Executes the CsvImportPreservesExtendedSchemaColumnsAndNormalizesValues operation.</summary>
    /// <returns>The CsvImportPreservesExtendedSchemaColumnsAndNormalizesValues operation result.</returns>
    [Test]
    internal async Task CsvImportPreservesExtendedSchemaColumnsAndNormalizesValuesAsync()
    {
        var database = MitsubishiTagDatabase.FromCsv("""
Name,Address,DataType,Length,Encoding,Units,Signed,ByteOrder
SignedTotal,D700,int32,2,utf8,items,true,bigendian
""");

        var tag = database.GetRequired("SignedTotal");

        await Assert.That(tag.DataType).IsEqualTo("Int32");
        await Assert.That(tag.Length).IsEqualTo(OperatorMessageLengthWords);
        await Assert.That(tag.Encoding).IsEqualTo("Utf8");
        await Assert.That(tag.Units).IsEqualTo("items");
        await Assert.That(tag.Signed).IsTrue();
        await Assert.That(tag.ByteOrder).IsEqualTo("BigEndian");
    }

    /// <summary>Executes the CsvImportRejectsUnsupportedByteOrderValues operation.</summary>
    /// <returns>The CsvImportRejectsUnsupportedByteOrderValues operation result.</returns>
    [Test]
    internal async Task CsvImportRejectsUnsupportedByteOrderValuesAsync()
    {
        try
        {
            _ = MitsubishiTagDatabase.FromCsv("""
Name,Address,DataType,ByteOrder
SignedTotal,D700,Int32,MiddleEndian
""");
            throw new InvalidOperationException("Expected CSV import to reject unknown ByteOrder values.");
        }
        catch (FormatException exception)
        {
            await Assert.That(exception.Message.Contains("ByteOrder", StringComparison.OrdinalIgnoreCase)).IsTrue();
        }
    }

    /// <summary>Executes the ReadInt16ByTagAsyncReadsTwosComplementWord operation.</summary>
    /// <returns>The ReadInt16ByTagAsyncReadsTwosComplementWord operation result.</returns>
    [Test]
    internal async Task ReadInt16ByTagAsyncReadsTwosComplementWordAsync()
    {
        await using var transport = new FakeTransport(
        [
            [0xD0, 0x00, 0x00, 0xFF, 0xFF, 0x03, 0x00, 0x04, 0x00, 0x00, 0x00, 0x9C, 0xFF],
        ]);

        var options = new MitsubishiClientOptions(
            Host: TagDatabasePart2LoopbackHost,
            Port: 5029,
            FrameType: MitsubishiFrameType.ThreeE,
            DataCode: CommunicationDataCode.Binary,
            TransportKind: MitsubishiTransportKind.Tcp,
            Route: MitsubishiRoute.Default);

        await using var client = new MitsubishiRx(options, transport, Scheduler.Immediate)
        {
            TagDatabase = MitsubishiTagDatabase.FromCsv("""
Name,Address,DataType,Signed
SignedTemp,D700,Int16,true
"""),
        };

        var result = await client.ReadInt16ByTagAsync("SignedTemp", CancellationToken.None);

        await Assert.That(result.IsSucceed).IsTrue();
        await Assert.That(result.Value).IsEqualTo(ExpectedSignedTemperature);
    }

    /// <summary>Executes the WriteInt32ByTagAsyncHonorsBigEndianByteOrder operation.</summary>
    /// <returns>The WriteInt32ByTagAsyncHonorsBigEndianByteOrder operation result.</returns>
    [Test]
    internal async Task WriteInt32ByTagAsyncHonorsBigEndianByteOrderAsync()
    {
        await using var transport = new FakeTransport(
        [
            [0xD0, 0x00, 0x00, 0xFF, 0xFF, 0x03, 0x00, 0x02, 0x00, 0x00, 0x00],
        ]);

        var options = new MitsubishiClientOptions(
            Host: TagDatabasePart2LoopbackHost,
            Port: 5030,
            FrameType: MitsubishiFrameType.ThreeE,
            DataCode: CommunicationDataCode.Binary,
            TransportKind: MitsubishiTransportKind.Tcp,
            Route: MitsubishiRoute.Default);

        await using var client = new MitsubishiRx(options, transport, Scheduler.Immediate)
        {
            TagDatabase = MitsubishiTagDatabase.FromCsv("""
Name,Address,DataType,ByteOrder
SignedTotal,D700,Int32,BigEndian
"""),
        };

        await using var rawTransport = new FakeTransport(
        [
            [0xD0, 0x00, 0x00, 0xFF, 0xFF, 0x03, 0x00, 0x02, 0x00, 0x00, 0x00],
        ]);

        await using var rawClient = new MitsubishiRx(options, rawTransport, Scheduler.Immediate);
        var baseline = await rawClient.WriteWordsAsync("D700", [0x1234, 0x5678], CancellationToken.None);
        var result = await client.WriteInt32ByTagAsync("SignedTotal", 0x12345678, CancellationToken.None);

        await Assert.That(baseline.IsSucceed).IsTrue();
        await Assert.That(result.IsSucceed).IsTrue();
        await Assert.That(transport.Requests[0].Payload).IsEquivalentTo(rawTransport.Requests[0].Payload);
    }

    /// <summary>Executes the ReadStringByTagAsyncWithoutExplicitLengthUsesTagLengthAndEncoding operation.</summary>
    /// <returns>The ReadStringByTagAsyncWithoutExplicitLengthUsesTagLengthAndEncoding operation result.</returns>
    [Test]
    internal async Task ReadStringByTagAsyncWithoutExplicitLengthUsesTagLengthAndEncodingAsync()
    {
        await using var transport = new FakeTransport(
        [
            [0xD0, 0x00, 0x00, 0xFF, 0xFF, 0x03, 0x00, 0x06, 0x00, 0x00, 0x00, 0x41, 0xC3, 0xA9, 0x00],
        ]);

        var options = new MitsubishiClientOptions(
            Host: TagDatabasePart2LoopbackHost,
            Port: 5031,
            FrameType: MitsubishiFrameType.ThreeE,
            DataCode: CommunicationDataCode.Binary,
            TransportKind: MitsubishiTransportKind.Tcp,
            Route: MitsubishiRoute.Default);

        await using var client = new MitsubishiRx(options, transport, Scheduler.Immediate)
        {
            TagDatabase = MitsubishiTagDatabase.FromCsv("""
Name,Address,DataType,Length,Encoding
OperatorMessage,D600,String,2,Utf8
"""),
        };

        var result = await client.ReadStringByTagAsync(
            OperatorMessageTagName,
            CancellationToken.None);

        await Assert.That(result.IsSucceed).IsTrue();
        await Assert.That(result.Value).IsEqualTo("Aé");
    }

    /// <summary>Tests inferred tag length and big-endian byte order for string writes.</summary>
    /// <returns>
    /// The WriteStringByTagAsyncWithoutExplicitLengthUsesTagLengthAndBigEndianByteOrder operation result.
    /// </returns>
    [Test]
    internal async Task WriteStringByTagAsyncWithoutExplicitLengthUsesTagLengthAndBigEndianByteOrderAsync()
    {
        await using var transport = new FakeTransport(
        [
            [0xD0, 0x00, 0x00, 0xFF, 0xFF, 0x03, 0x00, 0x02, 0x00, 0x00, 0x00],
        ]);

        var options = new MitsubishiClientOptions(
            Host: TagDatabasePart2LoopbackHost,
            Port: 5032,
            FrameType: MitsubishiFrameType.ThreeE,
            DataCode: CommunicationDataCode.Binary,
            TransportKind: MitsubishiTransportKind.Tcp,
            Route: MitsubishiRoute.Default);

        await using var client = new MitsubishiRx(options, transport, Scheduler.Immediate)
        {
            TagDatabase = MitsubishiTagDatabase.FromCsv("""
Name,Address,DataType,Length,Encoding,ByteOrder
OperatorMessage,D600,String,1,Ascii,BigEndian
"""),
        };

        await using var rawTransport = new FakeTransport(
        [
            [0xD0, 0x00, 0x00, 0xFF, 0xFF, 0x03, 0x00, 0x02, 0x00, 0x00, 0x00],
        ]);

        await using var rawClient = new MitsubishiRx(options, rawTransport, Scheduler.Immediate);
        var baseline = await rawClient.WriteWordsAsync("D600", [0x4F4B], CancellationToken.None);
        var result = await client.WriteStringByTagAsync(
            OperatorMessageTagName,
            "OK",
            CancellationToken.None);

        await Assert.That(baseline.IsSucceed).IsTrue();
        await Assert.That(result.IsSucceed).IsTrue();
        await Assert.That(transport.Requests[0].Payload).IsEquivalentTo(rawTransport.Requests[0].Payload);
    }

    /// <summary>Executes the ReadScaledDoubleByTagAsyncUsesSignedWordMetadata operation.</summary>
    /// <returns>The ReadScaledDoubleByTagAsyncUsesSignedWordMetadata operation result.</returns>
    [Test]
    internal async Task ReadScaledDoubleByTagAsyncUsesSignedWordMetadataAsync()
    {
        await using var transport = new FakeTransport(
        [
            [0xD0, 0x00, 0x00, 0xFF, 0xFF, 0x03, 0x00, 0x04, 0x00, 0x00, 0x00, 0x9C, 0xFF],
        ]);

        var options = new MitsubishiClientOptions(
            Host: TagDatabasePart2LoopbackHost,
            Port: 5033,
            FrameType: MitsubishiFrameType.ThreeE,
            DataCode: CommunicationDataCode.Binary,
            TransportKind: MitsubishiTransportKind.Tcp,
            Route: MitsubishiRoute.Default);

        await using var client = new MitsubishiRx(options, transport, Scheduler.Immediate)
        {
            TagDatabase = MitsubishiTagDatabase.FromCsv("""
Name,Address,DataType,Scale,Offset,Signed
SignedTemp,D700,Word,0.1,0,true
"""),
        };

        var result = await client.ReadScaledDoubleByTagAsync("SignedTemp", CancellationToken.None);

        await Assert.That(result.IsSucceed).IsTrue();
        await Assert.That(result.Value).IsEqualTo(ExpectedScaledSignedTemperature);
    }
}
