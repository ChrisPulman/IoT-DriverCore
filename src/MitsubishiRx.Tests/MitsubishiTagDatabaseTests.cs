// Copyright (c) 2019-2026 Chris Pulman and contributors. All rights reserved.
// Chris Pulman and contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

#if REACTIVE_SHIM

namespace MitsubishiRx.Reactive.Tests;
#else

namespace MitsubishiRx.Tests;
#endif

/// <summary>Provides the MitsubishiTagDatabaseTests type.</summary>
internal sealed partial class MitsubishiTagDatabaseTests
{
    /// <summary>Stores the <c>MotorSpeedTagName</c> test value.</summary>
    private const string MotorSpeedTagName = "MotorSpeed";

    /// <summary>Stores the <c>PumpRunningTagName</c> test value.</summary>
    private const string PumpRunningTagName = "PumpRunning";

    /// <summary>Stores the <c>RecipeNumberTagName</c> test value.</summary>
    private const string RecipeNumberTagName = "RecipeNumber";

    /// <summary>Stores the <c>TagDatabaseLoopbackHost</c> test value.</summary>
    private const string TagDatabaseLoopbackHost = "127.0.0.1";

    /// <summary>Stores the expected number of imported CSV tags.</summary>
    private const int ExpectedImportedTagCount = 3;

    /// <summary>Stores the configured motor-speed scale factor.</summary>
    private const double MotorSpeedScaleFactor = 0.1;

    /// <summary>Stores the expected process-value single-precision sample.</summary>
    private const float ProcessValueSample = 12.5F;

    /// <summary>Stores the number of values read by two-value tag read tests.</summary>
    private const int TwoTagReadCount = 2;

    /// <summary>Executes the CsvImportBuildsTagDatabaseAndPreservesMetadata operation.</summary>
    /// <returns>The CsvImportBuildsTagDatabaseAndPreservesMetadata operation result.</returns>
    [Test]
    internal async Task CsvImportBuildsTagDatabaseAndPreservesMetadataAsync()
    {
        const string csv = """
Name,Address,DataType,Description,Scale,Offset,Notes
MotorSpeed,D100,Word,Main spindle RPM,0.1,0,From commissioning sheet
PumpRunning,M10,Bit,Coolant pump running,1,0,
HeadTemp,D200,Word,Head temperature,1.0,-10,Degrees C
""";

        var database = MitsubishiTagDatabase.FromCsv(csv);
        var speed = database.GetRequired(MotorSpeedTagName);
        var pump = database.GetRequired(PumpRunningTagName);

        await Assert.That(database.Count).IsEqualTo(ExpectedImportedTagCount);
        await Assert.That(speed.Address).IsEqualTo("D100");
        await Assert.That(speed.DataType).IsEqualTo("Word");
        await Assert.That(speed.Description).IsEqualTo("Main spindle RPM");
        await Assert.That(speed.Scale).IsEqualTo(MotorSpeedScaleFactor);
        await Assert.That(speed.Offset).IsEqualTo(0.0);
        await Assert.That(speed.Notes).IsEqualTo("From commissioning sheet");
        await Assert.That(pump.Address).IsEqualTo("M10");
        await Assert.That(pump.DataType).IsEqualTo("Bit");
    }

    /// <summary>Executes the CsvImportNormalizesSupportedDataTypeValues operation.</summary>
    /// <returns>The CsvImportNormalizesSupportedDataTypeValues operation result.</returns>
    [Test]
    internal async Task CsvImportNormalizesSupportedDataTypeValuesAsync()
    {
        var database = MitsubishiTagDatabase.FromCsv("""
Name,Address,DataType
MotorSpeed,D100,word
PumpRunning,M10,bIt
RecipeNumber,D300,dword
""");

        await Assert.That(database.GetRequired(MotorSpeedTagName).DataType).IsEqualTo("Word");
        await Assert.That(database.GetRequired(PumpRunningTagName).DataType).IsEqualTo("Bit");
        await Assert.That(database.GetRequired(RecipeNumberTagName).DataType).IsEqualTo("DWord");
    }

    /// <summary>Executes the CsvImportRejectsUnknownDataTypeValues operation.</summary>
    /// <returns>The CsvImportRejectsUnknownDataTypeValues operation result.</returns>
    [Test]
    internal async Task CsvImportRejectsUnknownDataTypeValuesAsync()
    {
        try
        {
            _ = MitsubishiTagDatabase.FromCsv("""
Name,Address,DataType
MotorSpeed,D100,Boolean
""");
            throw new InvalidOperationException("Expected CSV import to reject unknown DataType values.");
        }
        catch (FormatException exception)
        {
            await Assert.That(exception.Message.Contains("DataType", StringComparison.OrdinalIgnoreCase)).IsTrue();
        }
    }

    /// <summary>Executes the ReadWordsByTagAsyncUsesResolvedTagAddress operation.</summary>
    /// <returns>The ReadWordsByTagAsyncUsesResolvedTagAddress operation result.</returns>
    [Test]
    internal async Task ReadWordsByTagAsyncUsesResolvedTagAddressAsync()
    {
        await using var transport = new FakeTransport(
        [
            [0xD0, 0x00, 0x00, 0xFF, 0xFF, 0x03, 0x00, 0x06, 0x00, 0x00, 0x00, 0x34, 0x12, 0x78, 0x56],
        ]);

        var options = new MitsubishiClientOptions(
            Host: TagDatabaseLoopbackHost,
            Port: 5015,
            FrameType: MitsubishiFrameType.ThreeE,
            DataCode: CommunicationDataCode.Binary,
            TransportKind: MitsubishiTransportKind.Tcp,
            Route: MitsubishiRoute.Default);

        var tags = MitsubishiTagDatabase.FromCsv("""
Name,Address,DataType,Description
MotorSpeed,D100,Word,Main spindle RPM
""");

        await using var client = new MitsubishiRx(options, transport, Scheduler.Immediate)
        {
            TagDatabase = tags,
        };

        var result = await client.ReadWordsByTagAsync(MotorSpeedTagName, TwoTagReadCount, CancellationToken.None);

        await Assert.That(result.IsSucceed).IsTrue();
        await Assert.That(result.Value!.Select(static value => (int)value).ToArray())
            .IsEquivalentTo([ 0x1234, 0x5678]);
        await Assert.That(transport.Requests.Count).IsEqualTo(1);
        await Assert.That(transport.Requests[0].Description).IsEqualTo("Read words D100");
    }

    /// <summary>Executes the ReadBitsByTagAsyncUsesResolvedTagAddress operation.</summary>
    /// <returns>The ReadBitsByTagAsyncUsesResolvedTagAddress operation result.</returns>
    [Test]
    internal async Task ReadBitsByTagAsyncUsesResolvedTagAddressAsync()
    {
        await using var transport = new FakeTransport(
        [
            [0xD0, 0x00, 0x00, 0xFF, 0xFF, 0x03, 0x00, 0x03, 0x00, 0x00, 0x00, 0x11],
        ]);

        var options = new MitsubishiClientOptions(
            Host: TagDatabaseLoopbackHost,
            Port: 5016,
            FrameType: MitsubishiFrameType.ThreeE,
            DataCode: CommunicationDataCode.Binary,
            TransportKind: MitsubishiTransportKind.Tcp,
            Route: MitsubishiRoute.Default);

        await using var client = new MitsubishiRx(options, transport, Scheduler.Immediate)
        {
            TagDatabase = MitsubishiTagDatabase.FromCsv("""
Name,Address,DataType,Description
PumpRunning,M10,Bit,Coolant pump running
"""),
        };

        var result = await client.ReadBitsByTagAsync(PumpRunningTagName, TwoTagReadCount, CancellationToken.None);

        await Assert.That(result.IsSucceed).IsTrue();
        await Assert.That(result.Value).IsEquivalentTo([true, true]);
        await Assert.That(transport.Requests[0].Description).IsEqualTo("Read bits M10");
    }

    /// <summary>Executes the WriteWordsByTagAsyncUsesResolvedTagAddress operation.</summary>
    /// <returns>The WriteWordsByTagAsyncUsesResolvedTagAddress operation result.</returns>
    [Test]
    internal async Task WriteWordsByTagAsyncUsesResolvedTagAddressAsync()
    {
        await using var transport = new FakeTransport(
        [
            [0xD0, 0x00, 0x00, 0xFF, 0xFF, 0x03, 0x00, 0x02, 0x00, 0x00, 0x00],
        ]);

        var options = new MitsubishiClientOptions(
            Host: TagDatabaseLoopbackHost,
            Port: 5017,
            FrameType: MitsubishiFrameType.ThreeE,
            DataCode: CommunicationDataCode.Binary,
            TransportKind: MitsubishiTransportKind.Tcp,
            Route: MitsubishiRoute.Default);

        await using var client = new MitsubishiRx(options, transport, Scheduler.Immediate)
        {
            TagDatabase = MitsubishiTagDatabase.FromCsv("""
Name,Address,DataType
MotorSpeed,D100,Word
"""),
        };

        await using var rawTransport = new FakeTransport(
        [
            [0xD0, 0x00, 0x00, 0xFF, 0xFF, 0x03, 0x00, 0x02, 0x00, 0x00, 0x00],
        ]);

        await using var rawClient = new MitsubishiRx(options, rawTransport, Scheduler.Immediate);
        var baseline = await rawClient.WriteWordsAsync("D100", [0x1234, 0x5678], CancellationToken.None);
        var result = await client.WriteWordsByTagAsync(MotorSpeedTagName, [0x1234, 0x5678], CancellationToken.None);

        await Assert.That(baseline.IsSucceed).IsTrue();
        await Assert.That(result.IsSucceed).IsTrue();
        await Assert.That(transport.Requests[0].Description).IsEqualTo("Write words D100");
        await Assert.That(transport.Requests[0].Payload).IsEquivalentTo(rawTransport.Requests[0].Payload);
    }

    /// <summary>Executes the WriteBitsByTagAsyncUsesResolvedTagAddress operation.</summary>
    /// <returns>The WriteBitsByTagAsyncUsesResolvedTagAddress operation result.</returns>
    [Test]
    internal async Task WriteBitsByTagAsyncUsesResolvedTagAddressAsync()
    {
        await using var transport = new FakeTransport(
        [
            [0xD0, 0x00, 0x00, 0xFF, 0xFF, 0x03, 0x00, 0x02, 0x00, 0x00, 0x00],
        ]);

        var options = new MitsubishiClientOptions(
            Host: TagDatabaseLoopbackHost,
            Port: 5018,
            FrameType: MitsubishiFrameType.ThreeE,
            DataCode: CommunicationDataCode.Binary,
            TransportKind: MitsubishiTransportKind.Tcp,
            Route: MitsubishiRoute.Default);

        await using var client = new MitsubishiRx(options, transport, Scheduler.Immediate)
        {
            TagDatabase = MitsubishiTagDatabase.FromCsv("""
Name,Address,DataType
PumpRunning,M10,Bit
"""),
        };

        await using var rawTransport = new FakeTransport(
        [
            [0xD0, 0x00, 0x00, 0xFF, 0xFF, 0x03, 0x00, 0x02, 0x00, 0x00, 0x00],
        ]);

        await using var rawClient = new MitsubishiRx(options, rawTransport, Scheduler.Immediate);
        var baseline = await rawClient.WriteBitsAsync("M10", [true, false, true, true], CancellationToken.None);
        var result = await client.WriteBitsByTagAsync(
            PumpRunningTagName,
            [true, false, true, true],
            CancellationToken.None);

        await Assert.That(baseline.IsSucceed).IsTrue();
        await Assert.That(result.IsSucceed).IsTrue();
        await Assert.That(transport.Requests[0].Description).IsEqualTo("Write bits M10");
        await Assert.That(transport.Requests[0].Payload).IsEquivalentTo(rawTransport.Requests[0].Payload);
    }

    /// <summary>Executes the RandomReadWordsByTagAsyncUsesResolvedAddressesInRequestOrder operation.</summary>
    /// <returns>The RandomReadWordsByTagAsyncUsesResolvedAddressesInRequestOrder operation result.</returns>
    [Test]
    internal async Task RandomReadWordsByTagAsyncUsesResolvedAddressesInRequestOrderAsync()
    {
        await using var transport = new FakeTransport(
        [
            [0xD0, 0x00, 0x00, 0xFF, 0xFF, 0x03, 0x00, 0x04, 0x00, 0x00, 0x00, 0x34, 0x12],
        ]);

        var options = new MitsubishiClientOptions(
            Host: TagDatabaseLoopbackHost,
            Port: 5019,
            FrameType: MitsubishiFrameType.ThreeE,
            DataCode: CommunicationDataCode.Binary,
            TransportKind: MitsubishiTransportKind.Tcp,
            Route: MitsubishiRoute.Default);

        await using var client = new MitsubishiRx(options, transport, Scheduler.Immediate)
        {
            TagDatabase = MitsubishiTagDatabase.FromCsv("""
Name,Address,DataType
MotorSpeed,D100,Word
RecipeNumber,D300,Word
"""),
        };

        await using var rawTransport = new FakeTransport(
        [
            [0xD0, 0x00, 0x00, 0xFF, 0xFF, 0x03, 0x00, 0x04, 0x00, 0x00, 0x00, 0x34, 0x12],
        ]);

        await using var rawClient = new MitsubishiRx(options, rawTransport, Scheduler.Immediate);
        var baseline = await rawClient.RandomReadWordsAsync(["D100", "D300"], CancellationToken.None);
        var result = await client.RandomReadWordsByTagAsync(
            [MotorSpeedTagName, RecipeNumberTagName],
            CancellationToken.None);

        await Assert.That(baseline.IsSucceed).IsTrue();
        await Assert.That(result.IsSucceed).IsTrue();
        await Assert.That(result.Value!.Select(static value => (int)value).ToArray()).IsEquivalentTo([ 0x1234]);
        await Assert.That(transport.Requests[0].Payload).IsEquivalentTo(rawTransport.Requests[0].Payload);
    }

    /// <summary>Executes the RandomWriteWordsByTagAsyncUsesResolvedAddressesInRequestOrder operation.</summary>
    /// <returns>The RandomWriteWordsByTagAsyncUsesResolvedAddressesInRequestOrder operation result.</returns>
    [Test]
    internal async Task RandomWriteWordsByTagAsyncUsesResolvedAddressesInRequestOrderAsync()
    {
        await using var transport = new FakeTransport(
        [
            [0xD0, 0x00, 0x00, 0xFF, 0xFF, 0x03, 0x00, 0x02, 0x00, 0x00, 0x00],
        ]);

        var options = new MitsubishiClientOptions(
            Host: TagDatabaseLoopbackHost,
            Port: 5020,
            FrameType: MitsubishiFrameType.ThreeE,
            DataCode: CommunicationDataCode.Binary,
            TransportKind: MitsubishiTransportKind.Tcp,
            Route: MitsubishiRoute.Default);

        await using var client = new MitsubishiRx(options, transport, Scheduler.Immediate)
        {
            TagDatabase = MitsubishiTagDatabase.FromCsv("""
Name,Address,DataType
MotorSpeed,D100,Word
RecipeNumber,D300,Word
"""),
        };

        var values = new[]
        {
            new KeyValuePair<string, ushort>(MotorSpeedTagName, 0x1234),
            new KeyValuePair<string, ushort>(RecipeNumberTagName, 0x5678),
        };

        await using var rawTransport = new FakeTransport(
        [
            [0xD0, 0x00, 0x00, 0xFF, 0xFF, 0x03, 0x00, 0x02, 0x00, 0x00, 0x00],
        ]);

        await using var rawClient = new MitsubishiRx(options, rawTransport, Scheduler.Immediate);
        var baseline = await rawClient.RandomWriteWordsAsync(
        [
            new KeyValuePair<string, ushort>("D100", 0x1234),
            new KeyValuePair<string, ushort>("D300", 0x5678),
        ], CancellationToken.None);
        var result = await client.RandomWriteWordsByTagAsync(values, CancellationToken.None);

        await Assert.That(baseline.IsSucceed).IsTrue();
        await Assert.That(result.IsSucceed).IsTrue();
        await Assert.That(transport.Requests[0].Payload).IsEquivalentTo(rawTransport.Requests[0].Payload);
    }

    /// <summary>Executes the ReadDWordByTagAsyncReadsLittleEndianDoubleWord operation.</summary>
    /// <returns>The ReadDWordByTagAsyncReadsLittleEndianDoubleWord operation result.</returns>
    [Test]
    internal async Task ReadDWordByTagAsyncReadsLittleEndianDoubleWordAsync()
    {
        await using var transport = new FakeTransport(
        [
            [0xD0, 0x00, 0x00, 0xFF, 0xFF, 0x03, 0x00, 0x06, 0x00, 0x00, 0x00, 0x78, 0x56, 0x34, 0x12],
        ]);

        var options = new MitsubishiClientOptions(
            Host: TagDatabaseLoopbackHost,
            Port: 5021,
            FrameType: MitsubishiFrameType.ThreeE,
            DataCode: CommunicationDataCode.Binary,
            TransportKind: MitsubishiTransportKind.Tcp,
            Route: MitsubishiRoute.Default);

        await using var client = new MitsubishiRx(options, transport, Scheduler.Immediate)
        {
            TagDatabase = MitsubishiTagDatabase.FromCsv("""
Name,Address,DataType
TotalCount,D400,DWord
"""),
        };

        var result = await client.ReadDWordByTagAsync("TotalCount", CancellationToken.None);

        await Assert.That(result.IsSucceed).IsTrue();
        await Assert.That(result.Value).IsEqualTo(0x12345678U);
    }

    /// <summary>Executes the WriteDWordByTagAsyncEncodesLittleEndianDoubleWord operation.</summary>
    /// <returns>The WriteDWordByTagAsyncEncodesLittleEndianDoubleWord operation result.</returns>
    [Test]
    internal async Task WriteDWordByTagAsyncEncodesLittleEndianDoubleWordAsync()
    {
        await using var transport = new FakeTransport(
        [
            [0xD0, 0x00, 0x00, 0xFF, 0xFF, 0x03, 0x00, 0x02, 0x00, 0x00, 0x00],
        ]);

        var options = new MitsubishiClientOptions(
            Host: TagDatabaseLoopbackHost,
            Port: 5022,
            FrameType: MitsubishiFrameType.ThreeE,
            DataCode: CommunicationDataCode.Binary,
            TransportKind: MitsubishiTransportKind.Tcp,
            Route: MitsubishiRoute.Default);

        await using var client = new MitsubishiRx(options, transport, Scheduler.Immediate)
        {
            TagDatabase = MitsubishiTagDatabase.FromCsv("""
Name,Address,DataType
TotalCount,D400,DWord
"""),
        };

        await using var rawTransport = new FakeTransport(
        [
            [0xD0, 0x00, 0x00, 0xFF, 0xFF, 0x03, 0x00, 0x02, 0x00, 0x00, 0x00],
        ]);

        await using var rawClient = new MitsubishiRx(options, rawTransport, Scheduler.Immediate);
        var baseline = await rawClient.WriteWordsAsync("D400", [0x5678, 0x1234], CancellationToken.None);
        var result = await client.WriteDWordByTagAsync("TotalCount", 0x12345678U, CancellationToken.None);

        await Assert.That(baseline.IsSucceed).IsTrue();
        await Assert.That(result.IsSucceed).IsTrue();
        await Assert.That(transport.Requests[0].Payload).IsEquivalentTo(rawTransport.Requests[0].Payload);
    }

    /// <summary>Executes the ReadFloatByTagAsyncReadsLittleEndianSinglePrecisionValue operation.</summary>
    /// <returns>The ReadFloatByTagAsyncReadsLittleEndianSinglePrecisionValue operation result.</returns>
    [Test]
    internal async Task ReadFloatByTagAsyncReadsLittleEndianSinglePrecisionValueAsync()
    {
        await using var transport = new FakeTransport(
        [
            [0xD0, 0x00, 0x00, 0xFF, 0xFF, 0x03, 0x00, 0x06, 0x00, 0x00, 0x00, 0x00, 0x00, 0x48, 0x41],
        ]);

        var options = new MitsubishiClientOptions(
            Host: TagDatabaseLoopbackHost,
            Port: 5023,
            FrameType: MitsubishiFrameType.ThreeE,
            DataCode: CommunicationDataCode.Binary,
            TransportKind: MitsubishiTransportKind.Tcp,
            Route: MitsubishiRoute.Default);

        await using var client = new MitsubishiRx(options, transport, Scheduler.Immediate)
        {
            TagDatabase = MitsubishiTagDatabase.FromCsv("""
Name,Address,DataType
ProcessValue,D500,Float
"""),
        };

        var result = await client.ReadFloatByTagAsync("ProcessValue", CancellationToken.None);

        await Assert.That(result.IsSucceed).IsTrue();
        await Assert.That(result.Value).IsEqualTo(ProcessValueSample);
    }

    /// <summary>Executes the WriteFloatByTagAsyncEncodesLittleEndianSinglePrecisionValue operation.</summary>
    /// <returns>The WriteFloatByTagAsyncEncodesLittleEndianSinglePrecisionValue operation result.</returns>
    [Test]
    internal async Task WriteFloatByTagAsyncEncodesLittleEndianSinglePrecisionValueAsync()
    {
        await using var transport = new FakeTransport(
        [
            [0xD0, 0x00, 0x00, 0xFF, 0xFF, 0x03, 0x00, 0x02, 0x00, 0x00, 0x00],
        ]);

        var options = new MitsubishiClientOptions(
            Host: TagDatabaseLoopbackHost,
            Port: 5024,
            FrameType: MitsubishiFrameType.ThreeE,
            DataCode: CommunicationDataCode.Binary,
            TransportKind: MitsubishiTransportKind.Tcp,
            Route: MitsubishiRoute.Default);

        await using var client = new MitsubishiRx(options, transport, Scheduler.Immediate)
        {
            TagDatabase = MitsubishiTagDatabase.FromCsv("""
Name,Address,DataType
ProcessValue,D500,Float
"""),
        };

        await using var rawTransport = new FakeTransport(
        [
            [0xD0, 0x00, 0x00, 0xFF, 0xFF, 0x03, 0x00, 0x02, 0x00, 0x00, 0x00],
        ]);

        await using var rawClient = new MitsubishiRx(options, rawTransport, Scheduler.Immediate);
        var baseline = await rawClient.WriteWordsAsync("D500", [0x0000, 0x4148], CancellationToken.None);
        var result = await client.WriteFloatByTagAsync("ProcessValue", ProcessValueSample, CancellationToken.None);

        await Assert.That(baseline.IsSucceed).IsTrue();
        await Assert.That(result.IsSucceed).IsTrue();
        await Assert.That(transport.Requests[0].Payload).IsEquivalentTo(rawTransport.Requests[0].Payload);
    }
}
