// Copyright (c) 2019-2026 Chris Pulman and contributors. All rights reserved.
// Chris Pulman and contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using CP.IoT.Core;

#if REACTIVE_SHIM

namespace MitsubishiRx.Reactive.Tests;
#else

namespace MitsubishiRx.Tests;
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
            var tag = new LogicalTag("Mode", "D101", "UInt16", new LogicalTagOptions { GroupName = Line1GroupName });

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
}
