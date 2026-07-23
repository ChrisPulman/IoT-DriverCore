// Copyright (c) 2019-2026 Chris Pulman and contributors. All rights reserved.
// Chris Pulman and contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using IoT.DriverCore.Core;

#if REACTIVE_SHIM

namespace IoT.DriverCore.MitsubishiRx.Reactive.Tests;
#else

namespace IoT.DriverCore.MitsubishiRx.Tests;
#endif

/// <summary>Tests common logical-tag composition.</summary>
internal sealed class MitsubishiLogicalTagClientTests
{
    /// <summary>Stores the <c>LoopbackHost</c> test value.</summary>
    private const string LoopbackHost = "127.0.0.1";

    /// <summary>Stores the <c>MotorSpeedTagName</c> test value.</summary>
    private const string MotorSpeedTagName = "MotorSpeed";

    /// <summary>Stores the TCP port used by logical-tag client tests.</summary>
    private const int LogicalTagClientPort = 5000;

    /// <summary>Stores the registered tag scan interval in milliseconds.</summary>
    private const int RegisteredTagScanIntervalMilliseconds = 250;

    /// <summary>Stores the <c>Line1GroupName</c> test value.</summary>
    private const string Line1GroupName = "Line1";

    /// <summary>Stores the logical test value.</summary>
    private const ushort LogicalTestValue = 0x1234;

    /// <summary>Stores the UInt16 data type name.</summary>
    private const string UInt16DataType = "UInt16";

    /// <summary>Stores the count tag name.</summary>
    private const string CountTagName = "Count";

    /// <summary>Stores the created tag name.</summary>
    private const string CreatedTagName = "Created";

    /// <summary>Verifies registration updates both the common catalog and rich Mitsubishi database.</summary>
    /// <returns>The RegisterTagSynchronizesCatalogDatabaseAndGroup operation result.</returns>
    [Test]
    internal async Task RegisterTagSynchronizesCatalogDatabaseAndGroupAsync()
    {
        await using var transport = new FakeTransport([]);
        await using var owner = new MitsubishiRx(
            new MitsubishiClientOptions(
                LoopbackHost,
                LogicalTagClientPort,
                MitsubishiFrameType.ThreeE,
                CommunicationDataCode.Binary,
                MitsubishiTransportKind.Tcp),
            transport,
            null);
        using var logical = owner.CreateLogicalTagClient(null, null, null);

        logical.RegisterTag(new LogicalTag(
            MotorSpeedTagName,
            "D100",
            "Float",
            new LogicalTagOptions
            {
                GroupName = Line1GroupName,
                Metadata = new Dictionary<string, string>
                {
                    ["Units"] = "rpm",
                    ["ByteOrder"] = "LittleEndian",
                },
                ScanInterval = TimeSpan.FromMilliseconds(RegisteredTagScanIntervalMilliseconds),
            }));

        await Assert.That(logical.Catalog.TryGet(MotorSpeedTagName, out var registered)).IsTrue();
        await Assert.That(registered!.ScanInterval)
            .IsEqualTo(TimeSpan.FromMilliseconds(RegisteredTagScanIntervalMilliseconds));
        await Assert.That(owner.TagDatabase!.GetRequired(MotorSpeedTagName).Units).IsEqualTo("rpm");
        await Assert.That(owner.TagDatabase.GetRequiredGroup(Line1GroupName).ResolvedTagNames)
            .IsEquivalentTo([MotorSpeedTagName]);
    }

    /// <summary>Verifies the common SQLite forwarding surface dynamically loads tags.</summary>
    /// <returns>The SqliteCrudAndDynamicLoadRoundTripTag operation result.</returns>
    [Test]
    internal async Task SqliteCrudAndDynamicLoadRoundTripTagAsync()
    {
        var path = Path.Combine(Path.GetTempPath(), $"mitsubishirx-logical-{Guid.NewGuid():N}.db");
        try
        {
            var store = new LogicalTagSqliteStore($"Data Source={path};Pooling=False");
            await using var transport = new FakeTransport([]);
            await using var owner = new MitsubishiRx(
                new MitsubishiClientOptions(
                    LoopbackHost,
                    LogicalTagClientPort,
                    MitsubishiFrameType.ThreeE,
                    CommunicationDataCode.Binary,
                    MitsubishiTransportKind.Tcp),
                transport,
                null);
            using var logical = owner.CreateLogicalTagClient(null, null, store);
            var tag = new LogicalTag("Mode", "D101", UInt16DataType, new LogicalTagOptions { GroupName = Line1GroupName });

            await logical.InitializeStoreAsync(CancellationToken.None);
            await logical.UpsertTagAsync(tag, CancellationToken.None);
            await Assert.That(await logical.GetTagAsync("Mode", CancellationToken.None)).IsNotNull();

            await using var reloadedTransport = new FakeTransport([]);
            await using var reloadedOwner = new MitsubishiRx(
                new MitsubishiClientOptions(
                    LoopbackHost,
                    LogicalTagClientPort,
                    MitsubishiFrameType.ThreeE,
                    CommunicationDataCode.Binary,
                    MitsubishiTransportKind.Tcp),
                reloadedTransport,
                null);
            using var reloaded = reloadedOwner.CreateLogicalTagClient(null, null, store);
            var loaded = await reloaded.LoadTagsAsync(CancellationToken.None);

            await Assert.That(loaded.Select(static item => item.Name)).IsEquivalentTo(["Mode"]);
            await Assert.That(reloadedOwner.TagDatabase!.GetRequired("Mode").Address).IsEqualTo("D101");
            await Assert.That(await reloaded.DeleteTagAsync("Mode", CancellationToken.None)).IsTrue();
            await Assert.That(await reloaded.GetTagAsync("Mode", CancellationToken.None)).IsNull();
        }
        finally
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
    }

    /// <summary>Verifies logical read/write, typed, batch, access, and missing-tag paths.</summary>
    /// <returns>A task that represents the asynchronous test.</returns>
    [Test]
    internal async Task LogicalOperationsRoundTripThroughSimulatorAsync()
    {
        var options = new MitsubishiClientOptions(
            LoopbackHost,
            LogicalTagClientPort,
            MitsubishiFrameType.ThreeE,
            CommunicationDataCode.Binary,
            MitsubishiTransportKind.Tcp);
        await using var simulator = new MitsubishiSimulatorTransport(
            request => MitsubishiSimulatorTransport.CreateSuccessResponse(
                options,
                request.Description.StartsWith("Read", StringComparison.Ordinal)
                    ? [0x34, 0x12]
                    : []));
        await using var owner = new MitsubishiRx(options, simulator, Scheduler.Immediate);
        using var logical = owner.CreateLogicalTagClient(null, null, null);
        var created = RegisterLogicalOperationTags(logical);

        var read = await logical.ReadAsync(CountTagName, CancellationToken.None);
        var typedRead = await logical.ReadAsync(
            new LogicalTagKey<ushort>(CountTagName),
            CancellationToken.None);
        var wrongTypedRead = await logical.ReadAsync(
            new LogicalTagKey<string>(CountTagName),
            CancellationToken.None);
        var reads = await logical.ReadManyAsync(
            [CountTagName, "Missing", "WriteOnly"],
            CancellationToken.None);
        var write = await logical.WriteAsync(
            CreateLogicalValue(CountTagName),
            CancellationToken.None);
        var typedWrite = await logical.WriteAsync(CountTagName, LogicalTestValue, CancellationToken.None);
        var writes = await logical.WriteManyAsync(
        [
            CreateLogicalValue(CountTagName),
            CreateLogicalValue("ReadOnly"),
            CreateLogicalValue("Missing"),
        ],
            CancellationToken.None);

        await Assert.That(created.Name).IsEqualTo(CreatedTagName);
        await Assert.That(read.Succeeded).IsTrue();
        await Assert.That(read.Value!.Value).IsEqualTo(LogicalTestValue);
        await Assert.That(typedRead.Succeeded).IsTrue();
        await Assert.That(typedRead.Value).IsEqualTo(LogicalTestValue);
        await Assert.That(wrongTypedRead.Succeeded).IsFalse();
        await Assert.That(reads[0].Succeeded).IsTrue();
        await Assert.That(reads[1].Succeeded).IsFalse();
        await Assert.That(reads[2].Succeeded).IsFalse();
        await Assert.That(write.Succeeded).IsTrue();
        await Assert.That(typedWrite.Succeeded).IsTrue();
        await Assert.That(writes[0].Succeeded).IsTrue();
        await Assert.That(writes[1].Succeeded).IsFalse();
        await Assert.That(writes[2].Succeeded).IsFalse();
        await Assert.That(logical.RemoveTag(CreatedTagName)).IsTrue();

        logical.Dispose();
        _ = Assert.Throws<ObjectDisposedException>(
            () => logical.RegisterTag(new LogicalTag("Disposed", "D200", UInt16DataType)));
    }

    /// <summary>Registers tags used by logical operation tests.</summary>
    /// <param name="logical">The logical Mitsubishi client.</param>
    /// <returns>The tag created through the registration DTO.</returns>
    private static LogicalTag RegisterLogicalOperationTags(MitsubishiLogicalTagClient logical)
    {
        logical.RegisterRange(
        [
            new LogicalTag(CountTagName, "D100", UInt16DataType),
            new LogicalTag(
                "ReadOnly",
                "D101",
                UInt16DataType,
                new LogicalTagOptions { AccessMode = LogicalTagAccessMode.Read }),
            new LogicalTag(
                "WriteOnly",
                "D102",
                UInt16DataType,
                new LogicalTagOptions { AccessMode = LogicalTagAccessMode.Write }),
        ]);
        return logical.CreateTag(
            new MitsubishiLogicalTagRegistration(
                CreatedTagName,
                "D103",
                UInt16DataType,
                Line1GroupName,
                "Created by test",
                null,
                LogicalTagAccessMode.ReadWrite,
                null));
    }

    /// <summary>Creates a deterministic logical tag value.</summary>
    /// <param name="tagName">The tag name.</param>
    /// <returns>The logical tag value.</returns>
    private static LogicalTagValue CreateLogicalValue(string tagName) =>
        new(tagName, LogicalTestValue, DateTimeOffset.UnixEpoch);
}
