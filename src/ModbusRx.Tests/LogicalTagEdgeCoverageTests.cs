// Copyright (c) 2019-2026 Chris Pulman and contributors. All rights reserved.
// Chris Pulman and contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using IoT.DriverCore.Core;
using IoT.DriverCore.ModbusRx.LogicalTags;
using NativeAssert = TUnit.Assertions.Assert;

namespace IoT.DriverCore.ModbusRx.UnitTests;

/// <summary>Exercises logical-tag catalog, validation, conversion, and persistence edge paths.</summary>
public sealed class LogicalTagEdgeCoverageTests
{
    /// <summary>The representative logical name.</summary>
    private const string TagName = "Logical";

    /// <summary>The deliberately absent logical name.</summary>
    private const string MissingName = "Absent";

    /// <summary>The representative unit identifier.</summary>
    private const byte UnitId = 1;

    /// <summary>The representative updated address.</summary>
    private const ushort UpdatedAddress = 5;

    /// <summary>A caller-owned metadata key.</summary>
    private const string MetadataKey = "site";

    /// <summary>A caller-owned metadata value.</summary>
    private const string MetadataValue = "north";

    /// <summary>Exercises catalog add, replace, remove, list, CSV, null, and ownership behavior.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [TUnit.Core.Test]
    public async Task Catalog_ExercisesCrudCsvAndOwnershipEdgesAsync()
    {
        using var catalog = new ModbusTagCatalog();
        var tag = CreateTag();
        await NativeAssert.That(catalog.TryAdd(tag)).IsTrue();
        await NativeAssert.That(catalog.TryAdd(tag)).IsFalse();
        catalog.Upsert(CreateTag(UpdatedAddress));
        await NativeAssert.That(catalog.List().Single().Address).IsEqualTo(UpdatedAddress);
        await NativeAssert.That(catalog.TryRemove(TagName, out var removed)).IsTrue();
        await NativeAssert.That(removed?.Name).IsEqualTo(TagName);
        await NativeAssert.That(catalog.TryRemove(MissingName, out var absent)).IsFalse();
        await NativeAssert.That(absent).IsNull();
        await NativeAssert.That(() => catalog.TryAdd(null!)).Throws<ArgumentNullException>();
        await NativeAssert.That(() => catalog.Upsert(null!)).Throws<ArgumentNullException>();
        await NativeAssert.That(() => new ModbusTagCatalog(null!)).Throws<ArgumentNullException>();
        await NativeAssert.That(
                async () => await catalog.LoadFromSqliteAsync(null!, CancellationToken.None))
            .Throws<ArgumentNullException>();

        catalog.Upsert(tag);
        using var writer = new StringWriter();
        await catalog.ExportCsvAsync(writer, CancellationToken.None);
        using var imported = new ModbusTagCatalog();
        var count = await imported.ImportCsvAsync(new StringReader(writer.ToString()), CancellationToken.None);
        await NativeAssert.That(count).IsEqualTo(UnitId);
        await NativeAssert.That(imported.TryGet(TagName, out var importedTag)).IsTrue();
        await NativeAssert.That(importedTag?.Metadata[MetadataKey]).IsEqualTo(MetadataValue);

        using var coreCatalog = new LogicalTagCatalog();
        var borrowed = new ModbusTagCatalog(coreCatalog);
        borrowed.Dispose();
        await NativeAssert.That(coreCatalog.List()).IsEmpty();
    }

    /// <summary>Exercises SQLite wrapper null guards, missing values, and catalog loading.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [TUnit.Core.Test]
    public async Task SqliteStore_LoadsCatalogAndValidatesNullTagsAsync()
    {
        var databasePath = Path.Combine(Path.GetTempPath(), $"modbusrx-store-{Guid.NewGuid():N}.db");
        try
        {
            var store = new ModbusTagSqliteStore($"Data Source={databasePath};Pooling=False");
            await store.InitializeAsync(CancellationToken.None);
            await NativeAssert.That(await store.GetAsync(MissingName, CancellationToken.None)).IsNull();
            await NativeAssert.That(
                    async () => await store.UpsertAsync(null!, CancellationToken.None))
                .Throws<ArgumentNullException>();
            await NativeAssert.That(
                    async () => await store.UpdateAsync(null!, CancellationToken.None))
                .Throws<ArgumentNullException>();

            await store.UpsertAsync(CreateTag(), CancellationToken.None);
            using var loaded = await store.LoadCatalogAsync(CancellationToken.None);
            await NativeAssert.That(loaded.TryGet(TagName, out var tag)).IsTrue();
            await NativeAssert.That(tag?.Name).IsEqualTo(TagName);
        }
        finally
        {
            if (File.Exists(databasePath))
            {
                File.Delete(databasePath);
            }
        }
    }

    /// <summary>Exercises Modbus-tag conversion metadata and constructor validation paths.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [TUnit.Core.Test]
    public async Task LogicalTag_ConvertsMetadataAndRejectsInvalidDefinitionsAsync()
    {
        var tag = CreateTag();
        var common = tag.ToLogicalTag();
        var roundTrip = ModbusLogicalTag.FromLogicalTag(common);

        await NativeAssert.That(roundTrip.Metadata[MetadataKey]).IsEqualTo(MetadataValue);
        await NativeAssert.That(() => new ModbusLogicalTag(null!)).Throws<ArgumentNullException>();
        await NativeAssert.That(() => ModbusLogicalTag.FromLogicalTag(null!)).Throws<ArgumentNullException>();
        await NativeAssert.That(() => CreateTag(metadata: new Dictionary<string, string> { [" "] = "x" }))
            .Throws<ArgumentException>();
        await NativeAssert.That(
                () => CreateTag(metadata: new Dictionary<string, string> { ["modbus.unitId"] = "x" }))
            .Throws<ArgumentException>();
        await NativeAssert.That(() => CreateTag(dataArea: (ModbusDataArea)int.MaxValue))
            .Throws<ArgumentOutOfRangeException>();
        await NativeAssert.That(() => CreateTag(byteOrder: (ModbusByteOrder)int.MaxValue))
            .Throws<ArgumentOutOfRangeException>();
        await NativeAssert.That(
                () => CreateTag(dataArea: ModbusDataArea.InputRegister))
            .Throws<ArgumentException>();
        await NativeAssert.That(() => CreateTag(scanInterval: TimeSpan.Zero))
            .Throws<ArgumentOutOfRangeException>();
    }

    /// <summary>Creates one representative logical tag.</summary>
    /// <param name="address">The register address.</param>
    /// <param name="metadata">Optional caller metadata.</param>
    /// <param name="dataArea">The data area.</param>
    /// <param name="byteOrder">The register byte order.</param>
    /// <param name="accessMode">The access mode.</param>
    /// <param name="scanInterval">The scan interval.</param>
    /// <returns>The validated tag.</returns>
    private static ModbusLogicalTag CreateTag(
        ushort address = 0,
        IReadOnlyDictionary<string, string>? metadata = null,
        ModbusDataArea dataArea = ModbusDataArea.HoldingRegister,
        ModbusByteOrder byteOrder = ModbusByteOrder.BigEndian,
        LogicalTagAccessMode accessMode = LogicalTagAccessMode.ReadWrite,
        TimeSpan? scanInterval = null) =>
        new(new ModbusTagConfiguration(TagName, UnitId, dataArea, address, UnitId, typeof(ushort))
        {
            Metadata = metadata ?? new Dictionary<string, string> { [MetadataKey] = MetadataValue },
            ByteOrder = byteOrder,
            AccessMode = accessMode,
            ScanInterval = scanInterval,
        });
}
