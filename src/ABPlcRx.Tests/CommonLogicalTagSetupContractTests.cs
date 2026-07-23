// Copyright (c) 2019-2026 Chris Pulman and contributors. All rights reserved.
// Chris Pulman and contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using IoT.DriverCore.Core;
using TUnit.Assertions;
using TUnit.Core;

namespace IoT.DriverCore.ABPlcRx.Tests;

/// <summary>Validates Allen-Bradley adoption of the common logical-tag setup contract.</summary>
public sealed class CommonLogicalTagSetupContractTests
{
    /// <summary>Verifies catalog, CSV, tag persistence, and group persistence through the shared interface.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task ManagedClientProvidesCommonSetupOperationsAsync()
    {
        var databasePath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.sqlite");

        try
        {
            using var simulator = new ABPlcSimulator();
            using var catalog = new LogicalTagCatalog();
            var store = new LogicalTagSqliteStore($"Data Source={databasePath};Pooling=False");
            using var concrete = new ABLogicalTagClient(simulator, catalog, store);
            var group = new LogicalTagGroup("Process", "Process values");
            var tag = new LogicalTag("Counter", "CounterValue", "DINT", new LogicalTagOptions
            {
                GroupName = group.Name,
            });

            await Assert.That(typeof(IManagedLogicalTagClient).IsAssignableFrom(concrete.GetType())).IsTrue();
            await ((IManagedLogicalTagClient)concrete).InitializeStoreAsync(CancellationToken.None);
            await ((IManagedLogicalTagClient)concrete).UpsertGroupAsync(group, CancellationToken.None);
            await ((IManagedLogicalTagClient)concrete).UpsertTagAsync(tag, CancellationToken.None);

            var storedTag = await ((IManagedLogicalTagClient)concrete)
                .GetTagAsync(tag.Name, CancellationToken.None);
            var storedTags = await ((IManagedLogicalTagClient)concrete).ListTagsAsync(CancellationToken.None);
            var storedGroups = await ((IManagedLogicalTagClient)concrete).ListGroupsAsync(CancellationToken.None);

            await Assert.That(concrete.Catalog.TryGet(tag.Name, out _)).IsTrue();
            await Assert.That(storedTag?.Address).IsEqualTo(tag.Address);
            await Assert.That(storedTags.Count).IsEqualTo(1);
            await Assert.That(storedGroups.Count).IsEqualTo(1);

            using var reader = new StringReader(
                "Name;Address;DataType;GroupName;Description;Metadata;AccessMode;ScanIntervalMilliseconds\r\n" +
                "Enabled;EnabledValue;BOOL;Process;Enabled state;;ReadWrite;\r\n");
            var imported = await concrete.ImportCsvAsync(reader, ';', CancellationToken.None);
            using var writer = new StringWriter();
            await concrete.ExportCsvAsync(writer, ';', CancellationToken.None);

            await Assert.That(imported.Count).IsEqualTo(1);
            await Assert.That(writer.ToString().Contains("EnabledValue", StringComparison.Ordinal)).IsTrue();
            await Assert
                .That(await ((IManagedLogicalTagClient)concrete)
                    .DeleteTagAsync(tag.Name, CancellationToken.None))
                .IsTrue();
            await Assert
                .That(await ((IManagedLogicalTagClient)concrete)
                    .DeleteGroupAsync(group.Name, CancellationToken.None))
                .IsTrue();
            await Assert.That(((IManagedLogicalTagClient)concrete).RemoveTag(imported[0].Name)).IsTrue();
        }
        finally
        {
            if (File.Exists(databasePath))
            {
                File.Delete(databasePath);
            }
        }
    }
}
