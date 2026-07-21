// Copyright (c) 2019-2026 Chris Pulman and contributors. All rights reserved.
// Chris Pulman and contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using CP.IoT.Core;

#if REACTIVE_SHIM

namespace MitsubishiRx.Reactive.Tests;
#else

namespace MitsubishiRx.Tests;
#endif

/// <summary>Provides the MitsubishiTagGroupTests type.</summary>
internal sealed class MitsubishiTagGroupTests
{
    /// <summary>Stores the <c>BadWordTagName</c> test value.</summary>
    private const string BadWordTagName = "BadWord";

    /// <summary>Stores the <c>OperatorMessageTagName</c> test value.</summary>
    private const string OperatorMessageTagName = "OperatorMessage";

    /// <summary>Stores the <c>Line1GroupName</c> test value.</summary>
    private const string Line1GroupName = "Line1";

    /// <summary>Stores the <c>LoopbackHost</c> test value.</summary>
    private const string LoopbackHost = "127.0.0.1";

    /// <summary>Stores the <c>SignedTempTagName</c> test value.</summary>
    private const string SignedTempTagName = "SignedTemp";

    /// <summary>Stores the <c>TotalCountTagName</c> test value.</summary>
    private const string TotalCountTagName = "TotalCount";

    /// <summary>Stores the <c>PumpRunningTagName</c> test value.</summary>
    private const string PumpRunningTagName = "PumpRunning";

    /// <summary>Stores the <c>HeadTempTagName</c> test value.</summary>
    private const string HeadTempTagName = "HeadTemp";

    /// <summary>Stores the expected signed temperature snapshot value.</summary>
    private const short ExpectedSignedTemperature = -100;

    /// <summary>Stores the expected scaled head-temperature value.</summary>
    private const double ExpectedScaledHeadTemperature = 15.0D;

    /// <summary>Executes the ValidateTagDatabaseFailsForInvalidAddressesAndUnknownGroupMembers operation.</summary>
    /// <returns>The ValidateTagDatabaseFailsForInvalidAddressesAndUnknownGroupMembers operation result.</returns>
    [Test]
    internal async Task ValidateTagDatabaseFailsForInvalidAddressesAndUnknownGroupMembersAsync()
    {
        var database = new MitsubishiTagDatabase(
        [
            new MitsubishiTagDefinition(BadWordTagName, "BAD100", DataType: "Word"),
            new MitsubishiTagDefinition(OperatorMessageTagName, "D600", DataType: "String"),
        ]);

        database.AddGroup(
            new MitsubishiTagGroupDefinition(
                Line1GroupName,
                [BadWordTagName, "MissingTag", OperatorMessageTagName]));

        await using var transport = new FakeTransport([]);
        await using var client = new MitsubishiRx(
            new MitsubishiClientOptions(
            Host: LoopbackHost,
            Port: 5034,
            FrameType: MitsubishiFrameType.ThreeE,
            DataCode: CommunicationDataCode.Binary,
            TransportKind: MitsubishiTransportKind.Tcp,
            Route: MitsubishiRoute.Default),
            transport,
            Scheduler.Immediate)
        {
            TagDatabase = database,
        };

        var result = client.ValidateTagDatabase();

        await Assert.That(result.IsSucceed).IsFalse();
        await Assert.That(
                result.ErrList.Any(
                    static err => err.Contains(BadWordTagName, StringComparison.OrdinalIgnoreCase)))
            .IsTrue();
        await Assert.That(
                result.ErrList.Any(
                    static err => err.Contains("MissingTag", StringComparison.OrdinalIgnoreCase)))
            .IsTrue();
        await Assert.That(
                result.ErrList.Any(
                    static err => err.Contains("Length", StringComparison.OrdinalIgnoreCase)))
            .IsTrue();
    }

    /// <summary>Executes the ReadTagGroupSnapshotAsyncReturnsTypedValuesInGroupOrder operation.</summary>
    /// <returns>The ReadTagGroupSnapshotAsyncReturnsTypedValuesInGroupOrder operation result.</returns>
    [Test]
    internal async Task ReadTagGroupSnapshotAsyncReturnsTypedValuesInGroupOrderAsync()
    {
        await using var transport = new FakeTransport(
        [
            [0xD0, 0x00, 0x00, 0xFF, 0xFF, 0x03, 0x00, 0x04, 0x00, 0x00, 0x00, 0x9C, 0xFF],
            [0xD0, 0x00, 0x00, 0xFF, 0xFF, 0x03, 0x00, 0x06, 0x00, 0x00, 0x00, 0x78, 0x56, 0x34, 0x12],
            [0xD0, 0x00, 0x00, 0xFF, 0xFF, 0x03, 0x00, 0x06, 0x00, 0x00, 0x00, 0x4F, 0x4B, 0x21, 0x00],
            [0xD0, 0x00, 0x00, 0xFF, 0xFF, 0x03, 0x00, 0x03, 0x00, 0x00, 0x00, 0x11],
        ]);

        var database = new MitsubishiTagDatabase(
        [
            new MitsubishiTagDefinition(SignedTempTagName, "D700", DataType: "Int16", Signed: true),
            new MitsubishiTagDefinition(TotalCountTagName, "D400", DataType: "UInt32"),
            new MitsubishiTagDefinition(OperatorMessageTagName, "D600", DataType: "String", Length: 2),
            new MitsubishiTagDefinition(PumpRunningTagName, "M10", DataType: "Bit"),
        ]);
        database.AddGroup(
            new MitsubishiTagGroupDefinition(
                Line1GroupName,
                [SignedTempTagName, TotalCountTagName, OperatorMessageTagName, PumpRunningTagName]));

        await using var client = new MitsubishiRx(
            new MitsubishiClientOptions(
            Host: LoopbackHost,
            Port: 5035,
            FrameType: MitsubishiFrameType.ThreeE,
            DataCode: CommunicationDataCode.Binary,
            TransportKind: MitsubishiTransportKind.Tcp,
            Route: MitsubishiRoute.Default),
            transport,
            Scheduler.Immediate)
        {
            TagDatabase = database,
        };

        var result = await client.ReadTagGroupSnapshotAsync(Line1GroupName, CancellationToken.None);

        await Assert.That(result.IsSucceed).IsTrue();
        await Assert.That(result.Value!.GroupName).IsEqualTo(Line1GroupName);
        await Assert.That(result.Value.TagNames)
            .IsEquivalentTo(
                [SignedTempTagName, TotalCountTagName, OperatorMessageTagName, PumpRunningTagName]);
        await Assert.That(result.Value.GetRequired(new LogicalTagKey<short>(SignedTempTagName)))
            .IsEqualTo(ExpectedSignedTemperature);
        await Assert.That(result.Value.GetRequired(new LogicalTagKey<uint>(TotalCountTagName))).IsEqualTo(0x12345678U);
        await Assert.That(result.Value.GetRequired(new LogicalTagKey<string>(OperatorMessageTagName))).IsEqualTo("OK!");
        await Assert.That(result.Value.GetRequired(new LogicalTagKey<bool>(PumpRunningTagName))).IsTrue();
        await Assert.That(transport.Requests.Select(static request => request.Description).ToArray()).IsEquivalentTo([
            "Read words D700",
            "Read words D400",
            "Read words D600",
            "Read bits M10",
        ]);
    }

    /// <summary>Tests configured scaling of tag-group snapshot values.</summary>
    /// <returns>The ReadTagGroupSnapshotAsyncReturnsScaledEngineeringValuesWhenConfigured operation result.</returns>
    [Test]
    internal async Task ReadTagGroupSnapshotAsyncReturnsScaledEngineeringValuesWhenConfiguredAsync()
    {
        await using var transport = new FakeTransport(
        [
            [0xD0, 0x00, 0x00, 0xFF, 0xFF, 0x03, 0x00, 0x04, 0x00, 0x00, 0x00, 0xFA, 0x00],
        ]);

        var database = new MitsubishiTagDatabase(
        [
            new MitsubishiTagDefinition(
                HeadTempTagName,
                "D200",
                DataType: "Word",
                Scale: 0.1,
                Offset: -10,
                Units: "°C"),
        ]);
        database.AddGroup(new MitsubishiTagGroupDefinition("Thermals", [HeadTempTagName]));

        await using var client = new MitsubishiRx(
            new MitsubishiClientOptions(
            Host: LoopbackHost,
            Port: 5036,
            FrameType: MitsubishiFrameType.ThreeE,
            DataCode: CommunicationDataCode.Binary,
            TransportKind: MitsubishiTransportKind.Tcp,
            Route: MitsubishiRoute.Default),
            transport,
            Scheduler.Immediate)
        {
            TagDatabase = database,
        };

        var result = await client.ReadTagGroupSnapshotAsync("Thermals", CancellationToken.None);

        await Assert.That(result.IsSucceed).IsTrue();
        await Assert.That(result.Value!.GetRequired(new LogicalTagKey<double>(HeadTempTagName)))
            .IsEqualTo(ExpectedScaledHeadTemperature);
    }
}
