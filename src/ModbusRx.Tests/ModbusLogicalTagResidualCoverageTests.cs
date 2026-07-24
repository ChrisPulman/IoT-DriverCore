// Copyright (c) 2019-2026 Chris Pulman and contributors. All rights reserved.
// Chris Pulman and contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using IoT.DriverCore.Core;
using IoT.DriverCore.ModbusRx.LogicalTags;
using NativeAssert = TUnit.Assertions.Assert;

namespace IoT.DriverCore.ModbusRx.UnitTests;

/// <summary>Exercises residual logical-tag metadata parsing and range validation.</summary>
public sealed class ModbusLogicalTagResidualCoverageTests
{
    /// <summary>A deliberately malformed serialized value.</summary>
    private const string InvalidValue = "invalid";

    /// <summary>Verifies required Modbus metadata is present and parseable.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [TUnit.Core.Test]
    public async Task FromLogicalTag_RejectsMissingAndMalformedMetadataAsync()
    {
        await NativeAssert.That(() => Convert(new Dictionary<string, string>())).Throws<FormatException>();
        await NativeAssert.That(() => Convert(CreateMetadata(unitId: InvalidValue))).Throws<FormatException>();
        await NativeAssert.That(() => Convert(CreateMetadata(dataArea: InvalidValue))).Throws<FormatException>();
        await NativeAssert.That(() => Convert(CreateMetadata(dataArea: "99"))).Throws<FormatException>();
        await NativeAssert.That(() => Convert(CreateMetadata(count: InvalidValue))).Throws<FormatException>();
        await NativeAssert.That(() => Convert(CreateMetadata(byteOrder: InvalidValue))).Throws<FormatException>();
        await NativeAssert.That(() => Convert(CreateMetadata(), InvalidValue)).Throws<FormatException>();
        await NativeAssert.That(() => Convert(CreateMetadata(), dataType: "Unsupported.Type"))
            .Throws<FormatException>();
    }

    /// <summary>Verifies range, type, scan, and metadata constructor guards.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [TUnit.Core.Test]
    public async Task Constructor_RejectsAllInvalidRangesAndOptionsAsync()
    {
        await NativeAssert.That(() => CreateTag(count: 0)).Throws<ArgumentOutOfRangeException>();
        await NativeAssert.That(() => CreateTag(count: 126)).Throws<ArgumentOutOfRangeException>();
        await NativeAssert.That(() => CreateTag(address: ushort.MaxValue, count: 2))
            .Throws<ArgumentOutOfRangeException>();
        await NativeAssert.That(() => CreateTag(
                dataArea: ModbusDataArea.Coil,
                count: 2001,
                clrType: typeof(bool[])))
            .Throws<ArgumentOutOfRangeException>();
        await NativeAssert.That(() => CreateTag(name: " ")).Throws<ArgumentException>();
        await NativeAssert.That(
                () => new ModbusLogicalTag(
                    new ModbusTagConfiguration(
                        "Tag",
                        1,
                        ModbusDataArea.HoldingRegister,
                        0,
                        1,
                        null!)))
            .Throws<ArgumentNullException>();
        await NativeAssert.That(() => CreateTag(scanInterval: TimeSpan.Zero))
            .Throws<ArgumentOutOfRangeException>();
    }

    /// <summary>Verifies caller metadata null values are normalized to empty strings.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [TUnit.Core.Test]
    public async Task Constructor_NormalizesNullableMetadataValuesAsync()
    {
        var metadata = new Dictionary<string, string>
        {
            ["caller"] = null!,
        };
        var tag = CreateTag(metadata: metadata);
        await NativeAssert.That(tag.Metadata["caller"]).IsEmpty();
        await NativeAssert.That(tag.ScanInterval).IsNull();
    }

    /// <summary>Converts a common tag with controlled metadata.</summary>
    /// <param name="metadata">The metadata to use.</param>
    /// <param name="address">The serialized address.</param>
    /// <param name="dataType">The serialized data type.</param>
    /// <returns>The converted Modbus tag.</returns>
    private static ModbusLogicalTag Convert(
        IReadOnlyDictionary<string, string> metadata,
        string address = "0",
        string dataType = "ushort") =>
        ModbusLogicalTag.FromLogicalTag(
            new LogicalTag(
                "Tag",
                address,
                dataType,
                new LogicalTagOptions { Metadata = metadata }));

    /// <summary>Creates protocol metadata with optional overrides.</summary>
    /// <param name="unitId">The unit identifier text.</param>
    /// <param name="dataArea">The data-area text.</param>
    /// <param name="count">The count text.</param>
    /// <param name="byteOrder">The byte-order text.</param>
    /// <returns>The metadata dictionary.</returns>
    private static Dictionary<string, string> CreateMetadata(
        string unitId = "1",
        string dataArea = "HoldingRegister",
        string count = "1",
        string byteOrder = "BigEndian") =>
        new Dictionary<string, string>
        {
            [ModbusLogicalTag.UnitIdMetadata] = unitId,
            [ModbusLogicalTag.DataAreaMetadata] = dataArea,
            [ModbusLogicalTag.CountMetadata] = count,
            [ModbusLogicalTag.ByteOrderMetadata] = byteOrder,
        };

    /// <summary>Creates a tag with controlled validation inputs.</summary>
    /// <param name="name">The tag name.</param>
    /// <param name="dataArea">The data area.</param>
    /// <param name="address">The start address.</param>
    /// <param name="count">The point count.</param>
    /// <param name="clrType">The exposed CLR type.</param>
    /// <param name="metadata">The caller metadata.</param>
    /// <param name="scanInterval">The scan interval.</param>
    /// <returns>The validated tag.</returns>
    private static ModbusLogicalTag CreateTag(
        string name = "Tag",
        ModbusDataArea dataArea = ModbusDataArea.HoldingRegister,
        ushort address = 0,
        ushort count = 1,
        Type? clrType = null,
        IReadOnlyDictionary<string, string>? metadata = null,
        TimeSpan? scanInterval = null)
    {
        var configuration = new ModbusTagConfiguration(
            name,
            1,
            dataArea,
            address,
            count,
            clrType ?? (dataArea is ModbusDataArea.Coil ? typeof(bool[]) : typeof(ushort)))
        {
            Metadata = metadata,
            ScanInterval = scanInterval,
        };
        return new ModbusLogicalTag(configuration);
    }
}
