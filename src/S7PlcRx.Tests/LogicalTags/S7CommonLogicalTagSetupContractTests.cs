// Copyright (c) 2019-2026 Chris Pulman and contributors. All rights reserved.
// Chris Pulman and contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using IoT.DriverCore.Core;
using IoT.DriverCore.S7PlcRx.Enums;
using IoT.DriverCore.S7PlcRx.LogicalTags;

namespace IoT.DriverCore.S7PlcRx.Tests.LogicalTags;

/// <summary>Validates S7 adoption of the common logical-tag setup contract.</summary>
public sealed class S7CommonLogicalTagSetupContractTests
{
    /// <summary>Verifies catalog, CSV, tag persistence, and group persistence through the shared interface.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task ManagedClientProvidesCommonSetupOperationsAsync()
    {
        var databasePath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.sqlite");

        try
        {
            using var plc = new RxS7(new(new(CpuType.S71500, "127.0.0.1", 0, 1)));
            using var catalog = new LogicalTagCatalog();
            var store = new LogicalTagSqliteStore($"Data Source={databasePath};Pooling=False");
            using var concrete = new S7LogicalTagClient(plc, catalog, store);
            var group = new LogicalTagGroup("Process", "Process values");
            var tag = new LogicalTag("Counter", "DB1.DBD0", "DINT", new LogicalTagOptions
            {
                GroupName = group.Name,
            });

            await TUnit.Assertions.Assert
                .That(typeof(IManagedLogicalTagClient).IsAssignableFrom(concrete.GetType()))
                .IsTrue();
            await concrete.InitializeStoreAsync(CancellationToken.None);
            await concrete.UpsertGroupAsync(group, CancellationToken.None);
            await concrete.UpsertTagAsync(tag, CancellationToken.None);

            var storedTags = await concrete.ListTagsAsync(CancellationToken.None);
            var storedGroups = await concrete.ListGroupsAsync(CancellationToken.None);

            await TUnit.Assertions.Assert.That(concrete.Catalog.TryGet(tag.Name, out _)).IsTrue();
            await TUnit.Assertions.Assert.That(storedTags.Count).IsEqualTo(1);
            await TUnit.Assertions.Assert.That(storedGroups.Count).IsEqualTo(1);

            using var reader = new StringReader(
                "Name;Address;DataType;GroupName;Description;Metadata;AccessMode;ScanIntervalMilliseconds\r\n" +
                "Enabled;DB1.DBX4.0;BOOL;Process;Enabled state;;ReadWrite;\r\n");
            var imported = await concrete.ImportCsvAsync(reader, ';', CancellationToken.None);
            using var writer = new StringWriter();
            await concrete.ExportCsvAsync(writer, ';', CancellationToken.None);

            await TUnit.Assertions.Assert.That(imported.Count).IsEqualTo(1);
            await TUnit.Assertions.Assert
                .That(writer.ToString().Contains("DB1.DBX4.0", StringComparison.Ordinal))
                .IsTrue();
            await TUnit.Assertions.Assert
                .That(await concrete.DeleteTagAsync(tag.Name, CancellationToken.None))
                .IsTrue();
            await TUnit.Assertions.Assert
                .That(await concrete.DeleteGroupAsync(group.Name, CancellationToken.None))
                .IsTrue();
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
