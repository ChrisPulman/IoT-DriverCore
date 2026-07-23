// Copyright (c) 2019-2026 Chris Pulman and contributors. All rights reserved.
// Chris Pulman and contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using IoT.DriverCore.Core;
using TUnit.Core;

namespace IoT.DriverCore.OmronPlcRx.Tests;

/// <summary>Verifies Omron adopts the common managed logical-tag contract after device setup.</summary>
public sealed class OmronManagedLogicalTagContractTests
{
    /// <summary>Gets the CSV delimiter used by definition exchange.</summary>
    private const char CsvDelimiter = ',';

    /// <summary>Gets the managed logical tag name.</summary>
    private const string TagName = "ManagedTemperature";

    /// <summary>Gets the managed logical tag group name.</summary>
    private const string ManagedGroupName = "ManagedProcess";

    /// <summary>Verifies registry, exchange, tag persistence, and group persistence through the common interface.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task ManagedContract_ForwardsExistingOmronSetupOperations()
    {
        var databasePath = Path.Combine(
            Path.GetTempPath(),
            $"omron-managed-contract-{Guid.NewGuid():N}.db");
        try
        {
            using var plc = new FakeOmronPlcRx();
            using var client = new OmronLogicalTagClient(
                plc,
                $"Data Source={databasePath};Pooling=False");
            var tag = new LogicalTag(
                TagName,
                "D700",
                typeof(short).FullName!,
                new LogicalTagOptions { GroupName = ManagedGroupName });
            var group = new LogicalTagGroup(ManagedGroupName);

            await ExerciseManagedContractAsync(client, tag, group);
        }
        finally
        {
            if (File.Exists(databasePath))
            {
                File.Delete(databasePath);
            }
        }
    }

    /// <summary>Exercises the operations exposed by the common managed-client contract.</summary>
    /// <typeparam name="TClient">Managed logical-tag client type.</typeparam>
    /// <param name="managed">Managed logical-tag client.</param>
    /// <param name="tag">Tag used by the contract test.</param>
    /// <param name="group">Group used by the contract test.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    private static async Task ExerciseManagedContractAsync<TClient>(
        TClient managed,
        LogicalTag tag,
        LogicalTagGroup group)
        where TClient : IManagedLogicalTagClient
    {
        await managed.InitializeStoreAsync(CancellationToken.None);
        managed.RegisterTag(tag);
        await managed.UpsertGroupAsync(group, CancellationToken.None);
        await managed.UpsertTagAsync(tag, CancellationToken.None);
        await using var writer = new StringWriter();
        await managed.ExportCsvAsync(writer, CsvDelimiter, CancellationToken.None);

        var persistedTag = await managed.GetTagAsync(TagName, CancellationToken.None);
        var persistedGroup = await managed.GetGroupAsync(ManagedGroupName, CancellationToken.None);
        var tags = await managed.ListTagsAsync(CancellationToken.None);
        var groups = await managed.ListGroupsAsync(CancellationToken.None);
        var deletedTag = await managed.DeleteTagAsync(TagName, CancellationToken.None);
        var deletedGroup = await managed.DeleteGroupAsync(ManagedGroupName, CancellationToken.None);

        await Assert.That(managed.Catalog.TryGet(TagName, out _)).IsFalse();
        await Assert.That(persistedTag?.Name).IsEqualTo(TagName);
        await Assert.That(persistedGroup?.Name).IsEqualTo(ManagedGroupName);
        await Assert.That(tags.Count).IsEqualTo(1);
        await Assert.That(groups.Count).IsEqualTo(1);
        await Assert.That(writer.ToString()).Contains(TagName);
        await Assert.That(deletedTag).IsTrue();
        await Assert.That(deletedGroup).IsTrue();
    }
}
