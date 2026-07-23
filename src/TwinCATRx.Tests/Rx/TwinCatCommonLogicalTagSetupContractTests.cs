// Copyright (c) 2019-2026 Chris Pulman and contributors. All rights reserved.
// Chris Pulman and contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

#if NET9_0_OR_GREATER
using System.Diagnostics.CodeAnalysis;
#endif
using IoT.DriverCore.Core;

namespace IoT.DriverCore.TwinCATRx.Tests.Rx;

/// <summary>Validates TwinCAT adoption of the common logical-tag setup contract.</summary>
public sealed class TwinCatCommonLogicalTagSetupContractTests
{
    /// <summary>Verifies catalog, CSV, tag persistence, and group persistence through the shared interface.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
#if NET9_0_OR_GREATER
    [RequiresUnreferencedCode("The logical adapter supports HashTableRx structure materialization.")]
#endif
    public async Task ManagedClientProvidesCommonSetupOperationsAsync()
    {
        var databasePath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.sqlite");

        try
        {
            using var native = new InMemoryAdsClient();
            using var catalog = new LogicalTagCatalog();
            var store = new LogicalTagSqliteStore($"Data Source={databasePath};Pooling=False");
            using var concrete = new TwinCatLogicalTagClient(native, catalog, store);
            var group = new LogicalTagGroup("Process", "Process values");
            var tag = new LogicalTag("Counter", ".Machine.Counter", "DINT", new LogicalTagOptions
            {
                GroupName = group.Name,
            });

            await TUnitAssert.That(typeof(IManagedLogicalTagClient).IsAssignableFrom(concrete.GetType())).IsTrue();
            await concrete.InitializeStoreAsync(CancellationToken.None);
            await concrete.UpsertGroupAsync(group, CancellationToken.None);
            await concrete.UpsertTagAsync(tag, CancellationToken.None);

            var storedTags = await concrete.ListTagsAsync(CancellationToken.None);
            var storedGroups = await concrete.ListGroupsAsync(CancellationToken.None);

            await TUnitAssert.That(concrete.Catalog.TryGet(tag.Name, out _)).IsTrue();
            await TUnitAssert.That(storedTags.Count).IsEqualTo(1);
            await TUnitAssert.That(storedGroups.Count).IsEqualTo(1);

            using var reader = new StringReader(
                "Name;Address;DataType;GroupName;Description;Metadata;AccessMode;ScanIntervalMilliseconds\r\n" +
                "Enabled;.Machine.Enabled;BOOL;Process;Enabled state;;ReadWrite;\r\n");
            var imported = await concrete.ImportCsvAsync(reader, ';', CancellationToken.None);
            using var writer = new StringWriter();
            await concrete.ExportCsvAsync(writer, ';', CancellationToken.None);

            await TUnitAssert.That(imported.Count).IsEqualTo(1);
            await TUnitAssert
                .That(writer.ToString().Contains(".Machine.Enabled", StringComparison.Ordinal))
                .IsTrue();
            await TUnitAssert
                .That(await concrete.DeleteTagAsync(tag.Name, CancellationToken.None))
                .IsTrue();
            await TUnitAssert
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
