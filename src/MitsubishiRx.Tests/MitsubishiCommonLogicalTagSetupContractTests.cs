// Copyright (c) 2019-2026 Chris Pulman and contributors. All rights reserved.
// Chris Pulman and contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using IoT.DriverCore.Core;

#if REACTIVE_SHIM

namespace IoT.DriverCore.MitsubishiRx.Reactive.Tests;

#else

namespace IoT.DriverCore.MitsubishiRx.Tests;

#endif

/// <summary>Validates Mitsubishi adoption of the common logical-tag setup contract.</summary>
internal sealed class MitsubishiCommonLogicalTagSetupContractTests
{
    /// <summary>Stores the loopback host.</summary>
    private const string LoopbackHost = "127.0.0.1";

    /// <summary>Stores the deterministic test port.</summary>
    private const int LoopbackPort = 5000;

    /// <summary>Stores the unsigned word type name.</summary>
    private const string UInt16DataType = "UInt16";

    /// <summary>Verifies registry, CSV, tag persistence, and group persistence through the shared contract.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    internal async Task ManagedClientProvidesCommonSetupOperationsAsync()
    {
        var databasePath = Path.Combine(
            Path.GetTempPath(),
            $"mitsubishi-common-setup-{Guid.NewGuid():N}.sqlite");
        try
        {
            var options = new MitsubishiClientOptions(
                LoopbackHost,
                LoopbackPort,
                MitsubishiFrameType.ThreeE,
                CommunicationDataCode.Binary,
                MitsubishiTransportKind.Tcp);
            var store = new LogicalTagSqliteStore(
                $"Data Source={databasePath};Pooling=False");
            await using var transport = new FakeTransport([]);
            await using var owner = new MitsubishiRx(options, transport, Scheduler.Immediate);
            using var concrete = owner.CreateLogicalTagClient(null, null, store);

            await Assert.That(typeof(IManagedLogicalTagClient).IsAssignableFrom(concrete.GetType()))
                .IsTrue();
            await VerifyManagedOperationsAsync(concrete);
        }
        finally
        {
            if (File.Exists(databasePath))
            {
                File.Delete(databasePath);
            }
        }
    }

    /// <summary>Exercises every setup component through the common interface.</summary>
    /// <typeparam name="TManaged">The managed logical-tag client implementation.</typeparam>
    /// <param name="managed">The common managed logical-tag client.</param>
    /// <returns>A task that represents the asynchronous assertions.</returns>
    private static async Task VerifyManagedOperationsAsync<TManaged>(TManaged managed)
        where TManaged : IManagedLogicalTagClient
    {
        var group = new LogicalTagGroup("Process", "Process values");
        var tag = new LogicalTag(
            "Counter",
            "D100",
            UInt16DataType,
            new LogicalTagOptions { GroupName = group.Name });
        await managed.InitializeStoreAsync(CancellationToken.None);
        await managed.UpsertGroupAsync(group, CancellationToken.None);
        await managed.UpsertTagAsync(tag, CancellationToken.None);

        var storedTag = await managed.GetTagAsync(tag.Name, CancellationToken.None);
        var storedGroup = await managed.GetGroupAsync(group.Name, CancellationToken.None);
        var storedTags = await managed.ListTagsAsync(CancellationToken.None);
        var storedGroups = await managed.ListGroupsAsync(CancellationToken.None);
        await Assert.That(managed.Catalog.TryGet(tag.Name, out _)).IsTrue();
        await Assert.That(storedTag?.Address).IsEqualTo(tag.Address);
        await Assert.That(storedGroup?.Description).IsEqualTo(group.Description);
        await Assert.That(storedTags).Count().IsEqualTo(1);
        await Assert.That(storedGroups).Count().IsEqualTo(1);

        using var reader = new StringReader(
            "Name;Address;DataType;GroupName;Description;Metadata;AccessMode;ScanIntervalMilliseconds\r\n"
            + "Enabled;M10;Bit;Process;Enabled state;;ReadWrite;\r\n");
        var imported = await managed.ImportCsvAsync(reader, ';', CancellationToken.None);
        using var writer = new StringWriter();
        await managed.ExportCsvAsync(writer, ';', CancellationToken.None);

        await Assert.That(imported).Count().IsEqualTo(1);
        await Assert.That(writer.ToString().Contains("M10", StringComparison.Ordinal)).IsTrue();
        await Assert.That(await managed.DeleteTagAsync(tag.Name, CancellationToken.None)).IsTrue();
        await Assert.That(await managed.DeleteGroupAsync(group.Name, CancellationToken.None)).IsTrue();
        await Assert.That(managed.RemoveTag(imported[0].Name)).IsTrue();
    }
}
