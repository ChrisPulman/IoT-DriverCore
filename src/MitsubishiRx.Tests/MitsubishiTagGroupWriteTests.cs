// Copyright (c) 2019-2026 Chris Pulman and contributors. All rights reserved.
// Chris Pulman and contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

#if REACTIVE_SHIM

namespace MitsubishiRx.Reactive.Tests;
#else

namespace MitsubishiRx.Tests;
#endif

/// <summary>Provides the MitsubishiTagGroupWriteTests type.</summary>
internal sealed class MitsubishiTagGroupWriteTests
{
    /// <summary>Stores the <c>RecipeNumberTagName</c> test value.</summary>
    private const string RecipeNumberTagName = "RecipeNumber";

    /// <summary>Stores the <c>OperatorMessageTagName</c> test value.</summary>
    private const string OperatorMessageTagName = "OperatorMessage";

    /// <summary>Stores the <c>StringDataType</c> test value.</summary>
    private const string StringDataType = "String";

    /// <summary>Stores the <c>RecipeWriteGroupName</c> test value.</summary>
    private const string RecipeWriteGroupName = "RecipeWrite";

    /// <summary>Stores the <c>LoopbackHost</c> test value.</summary>
    private const string LoopbackHost = "127.0.0.1";

    /// <summary>Stores the <c>SignedTempTagName</c> test value.</summary>
    private const string SignedTempTagName = "SignedTemp";

    /// <summary>Stores the <c>TotalCountTagName</c> test value.</summary>
    private const string TotalCountTagName = "TotalCount";

    /// <summary>Stores the unsupported value supplied for validation.</summary>
    private const int UnsupportedValidationValue = 12;

    /// <summary>Stores the recipe number written by tag-group tests.</summary>
    private const ushort RecipeNumberValue = 7;

    /// <summary>Stores the expected write count for the subset tag-group operation.</summary>
    private const int ExpectedSubsetWriteCount = 2;

    /// <summary>Stores the signed temperature contained in the test snapshot.</summary>
    private const short SignedTemperatureValue = -100;

    /// <summary>Stores the expected write count for the snapshot operation.</summary>
    private const int ExpectedSnapshotWriteCount = 3;

    /// <summary>Executes the ValidateTagGroupWriteReportsUnsupportedValueTypesAndUnknownTags operation.</summary>
    /// <returns>The ValidateTagGroupWriteReportsUnsupportedValueTypesAndUnknownTags operation result.</returns>
    [Test]
    internal async Task ValidateTagGroupWriteReportsUnsupportedValueTypesAndUnknownTagsAsync()
    {
        var database = new MitsubishiTagDatabase(
        [
            new MitsubishiTagDefinition(RecipeNumberTagName, "D300", DataType: "UInt16"),
            new MitsubishiTagDefinition(OperatorMessageTagName, "D600", DataType: StringDataType, Length: 2),
        ]);
        database.AddGroup(
            new MitsubishiTagGroupDefinition(
                RecipeWriteGroupName,
                [RecipeNumberTagName, OperatorMessageTagName]));

        await using var transport = new FakeTransport([]);
        await using var client = new MitsubishiRx(
            new MitsubishiClientOptions(
            Host: LoopbackHost,
            Port: 5040,
            FrameType: MitsubishiFrameType.ThreeE,
            DataCode: CommunicationDataCode.Binary,
            TransportKind: MitsubishiTransportKind.Tcp,
            Route: MitsubishiRoute.Default),
            transport,
            Scheduler.Immediate)
        {
            TagDatabase = database,
        };

        var result = client.ValidateTagGroupWrite(
            RecipeWriteGroupName,
            new Dictionary<string, object?>
            {
                [RecipeNumberTagName] = "not-a-number",
                ["MissingTag"] = UnsupportedValidationValue,
            });

        await Assert.That(result.IsSucceed).IsFalse();
        await Assert.That(
                result.ErrList.Any(
                    static err => err.Contains(RecipeNumberTagName, StringComparison.OrdinalIgnoreCase)))
            .IsTrue();
        await Assert.That(
                result.ErrList.Any(
                    static err => err.Contains("MissingTag", StringComparison.OrdinalIgnoreCase)))
            .IsTrue();
    }

    /// <summary>Executes the WriteTagGroupValuesAsyncWritesOnlyProvidedValuesInGroupOrder operation.</summary>
    /// <returns>The WriteTagGroupValuesAsyncWritesOnlyProvidedValuesInGroupOrder operation result.</returns>
    [Test]
    internal async Task WriteTagGroupValuesAsyncWritesOnlyProvidedValuesInGroupOrderAsync()
    {
        await using var transport = new FakeTransport(
        [
            [0xD0, 0x00, 0x00, 0xFF, 0xFF, 0x03, 0x00, 0x02, 0x00, 0x00, 0x00],
            [0xD0, 0x00, 0x00, 0xFF, 0xFF, 0x03, 0x00, 0x02, 0x00, 0x00, 0x00],
        ]);

        var database = new MitsubishiTagDatabase(
        [
            new MitsubishiTagDefinition(RecipeNumberTagName, "D300", DataType: "UInt16"),
            new MitsubishiTagDefinition(OperatorMessageTagName, "D600", DataType: StringDataType, Length: 2),
            new MitsubishiTagDefinition("PumpRunning", "M10", DataType: "Bit"),
        ]);
        database.AddGroup(
            new MitsubishiTagGroupDefinition(
                RecipeWriteGroupName,
                [RecipeNumberTagName, OperatorMessageTagName, "PumpRunning"]));

        var options = new MitsubishiClientOptions(
            Host: LoopbackHost,
            Port: 5041,
            FrameType: MitsubishiFrameType.ThreeE,
            DataCode: CommunicationDataCode.Binary,
            TransportKind: MitsubishiTransportKind.Tcp,
            Route: MitsubishiRoute.Default);

        await using var client = new MitsubishiRx(options, transport, Scheduler.Immediate)
        {
            TagDatabase = database,
        };

        await using var rawTransport = new FakeTransport(
        [
            [0xD0, 0x00, 0x00, 0xFF, 0xFF, 0x03, 0x00, 0x02, 0x00, 0x00, 0x00],
            [0xD0, 0x00, 0x00, 0xFF, 0xFF, 0x03, 0x00, 0x02, 0x00, 0x00, 0x00],
        ]);

        await using var rawClient = new MitsubishiRx(options, rawTransport, Scheduler.Immediate);
        var baselineWord = await rawClient.WriteWordsAsync("D300", [RecipeNumberValue], CancellationToken.None);
        var baselineString = await rawClient.WriteWordsAsync("D600", [0x4B4F, 0x0021], CancellationToken.None);

        await Assert.That(baselineWord.IsSucceed).IsTrue();
        await Assert.That(baselineString.IsSucceed).IsTrue();

        var result = await client.WriteTagGroupValuesAsync(
            RecipeWriteGroupName,
            new Dictionary<string, object?>
            {
                [RecipeNumberTagName] = RecipeNumberValue,
                [OperatorMessageTagName] = "OK!",
            }, CancellationToken.None);

        await Assert.That(result.IsSucceed).IsTrue();
        await Assert.That(transport.Requests.Count).IsEqualTo(ExpectedSubsetWriteCount);
        await Assert.That(transport.Requests[0].Description).IsEqualTo("Write words D300");
        await Assert.That(transport.Requests[1].Description).IsEqualTo("Write words D600");
    }

    /// <summary>Executes the WriteTagGroupSnapshotAsyncWritesTypedSnapshotValues operation.</summary>
    /// <returns>The WriteTagGroupSnapshotAsyncWritesTypedSnapshotValues operation result.</returns>
    [Test]
    internal async Task WriteTagGroupSnapshotAsyncWritesTypedSnapshotValuesAsync()
    {
        await using var transport = new FakeTransport(
        [
            [0xD0, 0x00, 0x00, 0xFF, 0xFF, 0x03, 0x00, 0x02, 0x00, 0x00, 0x00],
            [0xD0, 0x00, 0x00, 0xFF, 0xFF, 0x03, 0x00, 0x02, 0x00, 0x00, 0x00],
            [0xD0, 0x00, 0x00, 0xFF, 0xFF, 0x03, 0x00, 0x02, 0x00, 0x00, 0x00],
        ]);

        var database = new MitsubishiTagDatabase(
        [
            new MitsubishiTagDefinition(SignedTempTagName, "D700", DataType: "Int16", Signed: true),
            new MitsubishiTagDefinition(TotalCountTagName, "D400", DataType: "UInt32"),
            new MitsubishiTagDefinition(OperatorMessageTagName, "D600", DataType: StringDataType, Length: 2),
        ]);
        database.AddGroup(
            new MitsubishiTagGroupDefinition(
                "Line1Overview",
                [SignedTempTagName, TotalCountTagName, OperatorMessageTagName]));

        var options = new MitsubishiClientOptions(
            Host: LoopbackHost,
            Port: 5042,
            FrameType: MitsubishiFrameType.ThreeE,
            DataCode: CommunicationDataCode.Binary,
            TransportKind: MitsubishiTransportKind.Tcp,
            Route: MitsubishiRoute.Default);

        await using var client = new MitsubishiRx(options, transport, Scheduler.Immediate)
        {
            TagDatabase = database,
        };

        var snapshot = new MitsubishiTagGroupSnapshot(
            "Line1Overview",
            new Dictionary<string, object?>
            {
                [SignedTempTagName] = SignedTemperatureValue,
                [TotalCountTagName] = 0x12345678U,
                [OperatorMessageTagName] = "OK!",
            });

        var result = await client.WriteTagGroupSnapshotAsync(snapshot, CancellationToken.None);

        await Assert.That(result.IsSucceed).IsTrue();
        await Assert.That(transport.Requests.Count).IsEqualTo(ExpectedSnapshotWriteCount);
        await Assert.That(transport.Requests.Select(static request => request.Description).ToArray()).IsEquivalentTo([
            "Write words D700",
            "Write words D400",
            "Write words D600",
        ]);
    }
}
