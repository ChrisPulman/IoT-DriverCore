// Copyright (c) 2019-2026 Chris Pulman and contributors. All rights reserved.
// Chris Pulman and contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using IoT.DriverCore.Core;
using IoT.DriverCore.OmronPlcRx.Core;
using IoT.DriverCore.OmronPlcRx.Core.Types;
using IoT.DriverCore.OmronPlcRx.Enums;
using IoT.DriverCore.OmronPlcRx.Tags;
using TUnit.Core;

namespace IoT.DriverCore.OmronPlcRx.Tests;

/// <summary>Verifies grouped logical writes dispatch every supported runtime payload type.</summary>
public sealed class OmronLogicalBatchTypeCoverageTests
{
    /// <summary>Gets the local FINS node identifier.</summary>
    private const byte LocalNode = 1;

    /// <summary>Gets the remote FINS node identifier.</summary>
    private const byte RemoteNode = 2;

    /// <summary>Gets the deterministic request timeout.</summary>
    private const int TimeoutMilliseconds = 100;

    /// <summary>Gets the supported batch item count.</summary>
    private const int SupportedItemCount = 13;

    /// <summary>Gets the expected native transfers for a bit and word range.</summary>
    private const int ExpectedNativeTransfers = 2;

    /// <summary>Gets the deterministic signed 16-bit value.</summary>
    private const short ShortValue = 12;

    /// <summary>Gets the deterministic unsigned 16-bit value.</summary>
    private const ushort UnsignedShortValue = 13;

    /// <summary>Gets the deterministic signed 32-bit value.</summary>
    private const int IntegerValue = 14;

    /// <summary>Gets the deterministic unsigned 32-bit value.</summary>
    private const uint UnsignedIntegerValue = 15U;

    /// <summary>Gets the deterministic single-precision value.</summary>
    private const float SingleValue = 16.5F;

    /// <summary>Gets the deterministic double-precision value.</summary>
    private const double DoubleValue = 17.5D;

    /// <summary>Gets the deterministic signed BCD value.</summary>
    private const short Bcd16Value = 1234;

    /// <summary>Gets the deterministic unsigned BCD value.</summary>
    private const ushort BcdU16Value = 2345;

    /// <summary>Gets the deterministic signed 32-bit BCD value.</summary>
    private const int Bcd32Value = 345_678;

    /// <summary>Gets the deterministic unsigned 32-bit BCD value.</summary>
    private const uint BcdU32Value = 45_678U;

    /// <summary>Gets the signed 16-bit BCD tag name.</summary>
    private const string Bcd16Tag = nameof(Bcd16);

    /// <summary>Gets the unsigned 32-bit BCD tag name.</summary>
    private const string BcdU32Tag = nameof(BcdU32);

    /// <summary>Gets the byte tag name.</summary>
    private const string ByteTag = nameof(ByteTag);

    /// <summary>Gets the signed 16-bit tag name.</summary>
    private const string ShortTag = nameof(ShortTag);

    /// <summary>Gets the unsigned 16-bit tag name.</summary>
    private const string UnsignedShortTag = nameof(UnsignedShortTag);

    /// <summary>Gets the signed 32-bit tag name.</summary>
    private const string IntegerTag = nameof(IntegerTag);

    /// <summary>Gets the unsigned 32-bit tag name.</summary>
    private const string UnsignedIntegerTag = nameof(UnsignedIntegerTag);

    /// <summary>Gets the single-precision tag name.</summary>
    private const string SingleTag = nameof(SingleTag);

    /// <summary>Gets the double-precision tag name.</summary>
    private const string DoubleTag = nameof(DoubleTag);

    /// <summary>Gets the string tag name.</summary>
    private const string StringTag = nameof(StringTag);

    /// <summary>Gets the boolean alias tag name.</summary>
    private const string BooleanAliasTag = nameof(BooleanAliasTag);

    /// <summary>Gets the signed 16-bit alias tag name.</summary>
    private const string ShortAliasTag = nameof(ShortAliasTag);

    /// <summary>Gets the unsigned 16-bit alias tag name.</summary>
    private const string UnsignedShortAliasTag = nameof(UnsignedShortAliasTag);

    /// <summary>Gets the signed 32-bit alias tag name.</summary>
    private const string IntegerAliasTag = nameof(IntegerAliasTag);

    /// <summary>Gets the unsigned 32-bit alias tag name.</summary>
    private const string UnsignedIntegerAliasTag = nameof(UnsignedIntegerAliasTag);

    /// <summary>Gets the single-precision alias tag name.</summary>
    private const string SingleAliasTag = nameof(SingleAliasTag);

    /// <summary>Gets the unsupported alias tag name.</summary>
    private const string UnsupportedAliasTag = nameof(UnsupportedAliasTag);

    /// <summary>Gets the boolean alias data type.</summary>
    private const string BooleanAliasDataType = "BOOL";

    /// <summary>Gets the signed 16-bit alias data type.</summary>
    private const string ShortAliasDataType = "SHORT";

    /// <summary>Gets the unsigned 16-bit alias data type.</summary>
    private const string UnsignedShortAliasDataType = "USHORT";

    /// <summary>Gets the signed 32-bit alias data type.</summary>
    private const string IntegerAliasDataType = "INT";

    /// <summary>Gets the unsigned 32-bit alias data type.</summary>
    private const string UnsignedIntegerAliasDataType = "UINT";

    /// <summary>Gets the single-precision alias data type.</summary>
    private const string SingleAliasDataType = "FLOAT";

    /// <summary>Gets the unsupported data type.</summary>
    private const string UnsupportedDataType = "Decimal";

    /// <summary>Gets the alias batch item count.</summary>
    private const int AliasItemCount = 7;

    /// <summary>Gets the successful alias batch item count.</summary>
    private const int SupportedAliasItemCount = 6;

    /// <summary>Verifies the grouped writer converts every supported logical type before its FINS transfer.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task WriteManyAsync_GroupedTransportConvertsEverySupportedLogicalTypeAsync()
    {
        var channel = new CoreProtocolCoverageTests.TestChannel();
        using var connection = CreateConnection(channel);
        using var driver = new OmronPlcRx(
            connection,
            TimeSpan.FromMilliseconds(TimeoutMilliseconds),
            false);
        using var client = new OmronLogicalTagClient(driver);
        RegisterTags(client);
        var timestamp = DateTimeOffset.UnixEpoch;

        var results = await client.WriteManyAsync(
        [
            new LogicalTagValue("Bool", true, timestamp),
            new LogicalTagValue(ByteTag, byte.MaxValue, timestamp),
            new LogicalTagValue(ShortTag, ShortValue, timestamp),
            new LogicalTagValue(UnsignedShortTag, UnsignedShortValue, timestamp),
            new LogicalTagValue(IntegerTag, IntegerValue, timestamp),
            new LogicalTagValue(UnsignedIntegerTag, UnsignedIntegerValue, timestamp),
            new LogicalTagValue(SingleTag, SingleValue, timestamp),
            new LogicalTagValue(DoubleTag, DoubleValue, timestamp),
            new LogicalTagValue(StringTag, "AB", timestamp),
            new LogicalTagValue(Bcd16Tag, new Bcd16(Bcd16Value), timestamp),
            new LogicalTagValue(nameof(BcdU16), new BcdU16(BcdU16Value), timestamp),
            new LogicalTagValue(nameof(Bcd32), new Bcd32(Bcd32Value), timestamp),
            new LogicalTagValue(BcdU32Tag, new BcdU32(BcdU32Value), timestamp),
        ],
            CancellationToken.None);

        await Assert.That(results.Count).IsEqualTo(SupportedItemCount);
        await Assert.That(results.All(static result => result.Succeeded)).IsTrue();
        await Assert.That(channel.SendCount).IsEqualTo(ExpectedNativeTransfers);
        await Assert.That(driver.GetValue(new LogicalTagKey<Bcd16>(Bcd16Tag))).IsEqualTo(new Bcd16(Bcd16Value));
        await Assert.That(driver.GetValue(new LogicalTagKey<BcdU32>(BcdU32Tag))).IsEqualTo(new BcdU32(BcdU32Value));
    }

    /// <summary>Verifies grouped writes accept alternate logical type names and retain an unsupported item failure.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task WriteManyAsync_GroupedTransportCoversAliasesAndUnsupportedTypeAsync()
    {
        var channel = new CoreProtocolCoverageTests.TestChannel();
        var catalog = new LogicalTagCatalog();
        using var connection = CreateConnection(channel);
        using var driver = new OmronPlcRx(
            connection,
            TimeSpan.FromMilliseconds(TimeoutMilliseconds),
            false);
        using var client = new OmronLogicalTagClient(driver, catalog);
        RegisterAliasTags(client, catalog);
        var timestamp = DateTimeOffset.UnixEpoch;

        var results = await client.WriteManyAsync(
        [
            new LogicalTagValue(BooleanAliasTag, true, timestamp),
            new LogicalTagValue(ShortAliasTag, ShortValue, timestamp),
            new LogicalTagValue(UnsignedShortAliasTag, UnsignedShortValue, timestamp),
            new LogicalTagValue(IntegerAliasTag, IntegerValue, timestamp),
            new LogicalTagValue(UnsignedIntegerAliasTag, UnsignedIntegerValue, timestamp),
            new LogicalTagValue(SingleAliasTag, SingleValue, timestamp),
            new LogicalTagValue(UnsupportedAliasTag, decimal.Zero, timestamp),
        ],
            CancellationToken.None);

        await Assert.That(results.Count).IsEqualTo(AliasItemCount);
        await Assert.That(results.Take(SupportedAliasItemCount).All(static result => result.Succeeded)).IsTrue();
        await Assert.That(results.Last().Succeeded).IsFalse();
        await Assert.That(results.Last().Error).Contains("not supported");
        await Assert.That(channel.SendCount).IsEqualTo(ExpectedNativeTransfers);
    }

    /// <summary>Registers addresses laid out as one bit range and one contiguous word range.</summary>
    /// <param name="client">Logical client under test.</param>
    private static void RegisterTags(OmronLogicalTagClient client)
    {
        _ = client.CreateTag(new PlcTag<bool>("Bool", "D100.0"));
        _ = client.CreateTag(new PlcTag<byte>(ByteTag, "D101"));
        _ = client.CreateTag(new PlcTag<short>(ShortTag, "D102"));
        _ = client.CreateTag(new PlcTag<ushort>(UnsignedShortTag, "D103"));
        _ = client.CreateTag(new PlcTag<int>(IntegerTag, "D104"));
        _ = client.CreateTag(new PlcTag<uint>(UnsignedIntegerTag, "D106"));
        _ = client.CreateTag(new PlcTag<float>(SingleTag, "D108"));
        _ = client.CreateTag(new PlcTag<double>(DoubleTag, "D110"));
        _ = client.CreateTag(new PlcTag<string>(StringTag, "D114[4]"));
        _ = client.CreateTag(new PlcTag<Bcd16>(Bcd16Tag, "D116"));
        _ = client.CreateTag(new PlcTag<BcdU16>(nameof(BcdU16), "D117"));
        _ = client.CreateTag(new PlcTag<Bcd32>(nameof(Bcd32), "D118"));
        _ = client.CreateTag(new PlcTag<BcdU32>(BcdU32Tag, "D120"));
    }

    /// <summary>Registers alternate logical type spellings without changing their concrete PLC representation.</summary>
    /// <param name="client">Logical client under test.</param>
    /// <param name="catalog">Shared logical catalog used to retain the unsupported definition.</param>
    private static void RegisterAliasTags(OmronLogicalTagClient client, LogicalTagCatalog catalog)
    {
        client.RegisterTag(new LogicalTag(BooleanAliasTag, "D130.0", BooleanAliasDataType));
        client.RegisterTag(new LogicalTag(ShortAliasTag, "D131", ShortAliasDataType));
        client.RegisterTag(new LogicalTag(UnsignedShortAliasTag, "D132", UnsignedShortAliasDataType));
        client.RegisterTag(new LogicalTag(IntegerAliasTag, "D133", IntegerAliasDataType));
        client.RegisterTag(new LogicalTag(UnsignedIntegerAliasTag, "D135", UnsignedIntegerAliasDataType));
        client.RegisterTag(new LogicalTag(SingleAliasTag, "D137", SingleAliasDataType));
        catalog.Upsert(new LogicalTag(UnsupportedAliasTag, "D139", UnsupportedDataType));
    }

    /// <summary>Creates an initialized deterministic FINS connection.</summary>
    /// <param name="channel">Injected channel.</param>
    /// <returns>The initialized connection.</returns>
    private static OmronPLCConnection CreateConnection(CoreProtocolCoverageTests.TestChannel channel) =>
        new(
            new OmronConnectionOptions(LocalNode, RemoteNode, ConnectionMethod.UDP, "127.0.0.1")
            {
                Timeout = TimeoutMilliseconds,
                Retries = 0,
            },
            channel,
            PlcType.CJ2,
            "CJ2M",
            "1.0",
            true);
}
