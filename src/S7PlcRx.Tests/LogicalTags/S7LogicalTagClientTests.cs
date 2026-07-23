// Copyright (c) 2019-2026 Chris Pulman and contributors. All rights reserved.
// Chris Pulman and contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using IoT.DriverCore.Core;
using IoT.DriverCore.S7PlcRx.Enums;
using IoT.DriverCore.S7PlcRx.LogicalTags;

namespace IoT.DriverCore.S7PlcRx.Tests.LogicalTags;

/// <summary>Tests the common logical-tag composition surface.</summary>
[System.Diagnostics.DebuggerDisplay("{DebuggerDisplay,nq}")]
public sealed class S7LogicalTagClientTests
{
    /// <summary>Defines the counter logical-tag name.</summary>
    private const string CounterTagName = "Counter";

    /// <summary>Gets the debugger display text.</summary>
    [System.Diagnostics.DebuggerBrowsable(System.Diagnostics.DebuggerBrowsableState.Never)]
    private string DebuggerDisplay
    {
        get => ToString() ?? string.Empty;
    }

    /// <summary>Verifies live catalog registration and robust common CSV parsing.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task ImportCsvAsyncRegistersQuotedLogicalTagsAsync()
    {
        System.Diagnostics.Debug.WriteLine(DebuggerDisplay);
        using var plc = new RxS7(new(new(CpuType.S71500, "127.0.0.1", 0, 1)));
        using var catalog = new LogicalTagCatalog();
        using var client = new S7LogicalTagClient(plc, catalog, (LogicalTagSqliteStore?)null);
        using var reader = new StringReader(
            "Name,Address,DataType,GroupName,Description,Metadata,AccessMode,ScanIntervalMilliseconds\r\n" +
            "Temperature,DB1.DBD0,REAL,Process,\"Line 1, vessel\",unit=C,Read,250\r\n");

        var imported = await client.ImportCsvAsync(reader, ',', CancellationToken.None);

        await TUnit.Assertions.Assert.That(imported.Count).IsEqualTo(1);
        await TUnit.Assertions.Assert.That(imported[0].Description).IsEqualTo("Line 1, vessel");
        var registeredTag = plc.TagList["Temperature"];
        await TUnit.Assertions.Assert.That(registeredTag).IsNotNull();
        await TUnit.Assertions.Assert.That(registeredTag?.Address).IsEqualTo("DB1.DBD0");
    }

    /// <summary>Verifies SQLite CRUD forwarding keeps the live catalog synchronized.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task SqliteCrudForwardsToStoreAndLiveCatalogAsync()
    {
        System.Diagnostics.Debug.WriteLine(DebuggerDisplay);
        var path = Path.Combine(Path.GetTempPath(), $"s7plcrx-{Guid.NewGuid():N}.db");
        try
        {
            using var plc = new RxS7(new(new(CpuType.S71500, "127.0.0.1", 0, 1)));
            using var catalog = new LogicalTagCatalog();
            using var client = new S7LogicalTagClient(plc, catalog, (LogicalTagSqliteStore?)null);
            var store = new LogicalTagSqliteStore($"Data Source={path};Pooling=False");
            var tag = new LogicalTag(CounterTagName, "DB2.DBW0", "WORD");

            await client.InitializeStoreAsync(store, CancellationToken.None);
            await client.UpsertTagAsync(tag, CancellationToken.None);

            var persistedTag = await client.GetTagAsync(CounterTagName, CancellationToken.None);
            await TUnit.Assertions.Assert.That(persistedTag?.Address).IsEqualTo("DB2.DBW0");
            await TUnit.Assertions.Assert.That(catalog.TryGet(CounterTagName, out _)).IsTrue();
            await TUnit.Assertions.Assert.That(
                await client.DeleteTagAsync(CounterTagName, CancellationToken.None)).IsTrue();
            await TUnit.Assertions.Assert.That(catalog.TryGet(CounterTagName, out _)).IsFalse();
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
