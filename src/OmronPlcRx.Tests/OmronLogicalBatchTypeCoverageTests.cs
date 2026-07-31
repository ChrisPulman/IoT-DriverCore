// Copyright (c) 2019-2026 Chris Pulman and contributors. All rights reserved.
// Chris Pulman and contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Reflection;
using System.Text;
using IoT.Driver.Core;
using IoT.Driver.OmronPlcRx.Core;
using IoT.Driver.OmronPlcRx.Core.Channels;
using IoT.Driver.OmronPlcRx.Core.Responses;
using IoT.Driver.OmronPlcRx.Core.Results;
using IoT.Driver.OmronPlcRx.Core.Types;
using IoT.Driver.OmronPlcRx.Enums;
using IoT.Driver.OmronPlcRx.Tags;
using TUnit.Core;

namespace IoT.Driver.OmronPlcRx.Tests;

/// <summary>Verifies grouped logical writes dispatch every supported runtime payload type.</summary>
public sealed class OmronLogicalBatchTypeCoverageTests
{
    /// <summary>Gets the local FINS node identifier.</summary>
    private const byte LocalNode = 1;

    /// <summary>Gets the remote FINS node identifier.</summary>
    private const byte RemoteNode = 2;

    /// <summary>Gets the deterministic request timeout.</summary>
    private const int TimeoutMilliseconds = 100;

    /// <summary>Gets the first address used by the memory-area probes.</summary>
    private const int AreaBaseAddress = 400;

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

    /// <summary>Gets the canonical logical type count.</summary>
    private const int CanonicalTypeCount = 13;

    /// <summary>Gets the private value-type helper name.</summary>
    private const string GetValueTypeMethodName = "GetValueType";

    /// <summary>Gets the private batch-conversion helper name.</summary>
    private const string ConvertBatchValueMethodName = "ConvertBatchValue";

    /// <summary>Stores the Unix epoch in a form supported by every target framework.</summary>
    private static readonly DateTimeOffset Epoch = DateTimeOffset.FromUnixTimeSeconds(0);

    /// <summary>Gets unsupported keys that traverse every string-dispatch decision family.</summary>
    private static readonly string[] UnsupportedTypeProbes =
    [
        "BOOLEAX",
        "BOOX",
        "BYTX",
        "INT1X",
        "SHORX",
        "UINT1X",
        "USHORX",
        "INT3X",
        "INX",
        "UINT3X",
        "UIX",
        "SINGLX",
        "FLOAX",
        "DOUBLX",
        "STRINX",
        "BCD1X",
        "BCDU1X",
        "BCD3X",
        "BCDU3X",
        "UNRECOGNIZED_TYPE",
    ];

    /// <summary>Gets every supported string-dispatch key.</summary>
    private static readonly string[] SupportedDispatchKeys =
    [
        "BOOLEAN",
        "BOOL",
        "BYTE",
        "INT16",
        "SHORT",
        "UINT16",
        "USHORT",
        "INT32",
        "INT",
        "UINT32",
        "UINT",
        "SINGLE",
        "FLOAT",
        "DOUBLE",
        "STRING",
        "BCD16",
        "BCDU16",
        "BCD32",
        "BCDU32",
    ];

    /// <summary>Verifies the grouped writer converts every supported logical type before its FINS transfer.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task WriteManyAsync_GroupedTransportConvertsEverySupportedLogicalTypeAsync()
    {
        var channel = new BatchTestChannel();
        using var connection = CreateConnection(channel);
        using var driver = new OmronPlcRx(
            connection,
            TimeSpan.FromMilliseconds(TimeoutMilliseconds),
            false);
        using var client = new OmronLogicalTagClient(driver);
        RegisterTags(client);
        var timestamp = Epoch;

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
        await Assert.That(AllSucceeded(results)).IsTrue();
        await Assert.That(channel.SendCount).IsEqualTo(ExpectedNativeTransfers);
        LogicalTagKey<Bcd16> bcd16Key = new(Bcd16Tag);
        LogicalTagKey<BcdU32> bcdU32Key = new(BcdU32Tag);
        Bcd16 expectedBcd16 = new(Bcd16Value);
        BcdU32 expectedBcdU32 = new(BcdU32Value);
        await Assert.That(driver.GetValue(bcd16Key)).IsEqualTo(expectedBcd16);
        await Assert.That(driver.GetValue(bcdU32Key)).IsEqualTo(expectedBcdU32);
    }

    /// <summary>Verifies grouped writes accept alternate logical type names and retain an unsupported item failure.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task WriteManyAsync_GroupedTransportCoversAliasesAndUnsupportedTypeAsync()
    {
        var channel = new BatchTestChannel();
        var catalog = new LogicalTagCatalog();
        using var connection = CreateConnection(channel);
        using var driver = new OmronPlcRx(
            connection,
            TimeSpan.FromMilliseconds(TimeoutMilliseconds),
            false);
        using var client = new OmronLogicalTagClient(driver, catalog);
        RegisterAliasTags(client, catalog);
        var timestamp = Epoch;

        var results = await client.WriteManyAsync(
        [
            new LogicalTagValue(BooleanAliasTag, true, timestamp),
            new LogicalTagValue(ShortAliasTag, ShortValue, timestamp),
            new LogicalTagValue(UnsignedShortAliasTag, UnsignedShortValue, timestamp),
            new LogicalTagValue(IntegerAliasTag, IntegerValue, timestamp),
            new LogicalTagValue(UnsignedIntegerAliasTag, UnsignedIntegerValue, timestamp),
            new LogicalTagValue(SingleAliasTag, SingleValue, timestamp),
            new(UnsupportedAliasTag, decimal.Zero, timestamp),
        ],
            CancellationToken.None);

        await Assert.That(results.Count).IsEqualTo(AliasItemCount);
        var supportedAliasesSucceeded = true;
        for (var index = 0; index < SupportedAliasItemCount; index++)
        {
            if (!results[index].Succeeded)
            {
                supportedAliasesSucceeded = false;
                break;
            }
        }

        var unsupportedResult = results[results.Count - 1];
        await Assert.That(supportedAliasesSucceeded).IsTrue();
        await Assert.That(unsupportedResult.Succeeded).IsFalse();
        await Assert.That(unsupportedResult.Error).Contains("not supported");
        await Assert.That(channel.SendCount).IsEqualTo(ExpectedNativeTransfers);

        var writes = await WriteAliasValuesAsync(client, timestamp);
        var reads = await ReadAliasValuesAsync(client);
        _ = client.Observe(BooleanAliasTag);
        _ = client.Observe(ShortAliasTag);
        _ = client.Observe(UnsignedShortAliasTag);
        _ = client.Observe(IntegerAliasTag);
        _ = client.Observe(UnsignedIntegerAliasTag);
        _ = client.Observe(SingleAliasTag);
        var unsupportedRead = await client.ReadAsync(UnsupportedAliasTag, CancellationToken.None);
        var unsupportedWrite = await client.WriteAsync(
            new(UnsupportedAliasTag, decimal.Zero, timestamp),
            CancellationToken.None);

        await Assert.That(AllSucceeded(writes)).IsTrue();
        await Assert.That(AllFailed(reads)).IsTrue();
        await Assert.That(unsupportedRead.Succeeded).IsFalse();
        await Assert.That(unsupportedWrite.Succeeded).IsFalse();
        await AssertThrowsAsync<NotSupportedException>(
            () => Task.Run(() => client.Observe(UnsupportedAliasTag)));
    }

    /// <summary>Verifies canonical fully qualified type names across every logical dispatch path.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task CanonicalTypes_CoverIndividualAndGroupedDispatchAsync()
    {
        var channel = new BatchTestChannel();
        using var connection = CreateConnection(channel);
        using var driver = new OmronPlcRx(
            connection,
            TimeSpan.FromMilliseconds(TimeoutMilliseconds),
            false);
        using var client = new OmronLogicalTagClient(driver);
        var cases = CreateCanonicalCases();

        foreach (var item in cases)
        {
            client.RegisterTag(
                new(
                    item.Name,
                    item.Address,
                    item.DataType));
        }

        foreach (var item in cases)
        {
            _ = await client.ReadAsync(item.Name, CancellationToken.None);
            _ = await client.WriteAsync(
                new(item.Name, item.Value, Epoch),
                CancellationToken.None);
            _ = client.Observe(item.Name);
        }

        var names = new string[cases.Length];
        var values = new LogicalTagValue[cases.Length];
        for (var index = 0; index < cases.Length; index++)
        {
            var item = cases[index];
            names[index] = item.Name;
            values[index] = new(item.Name, item.Value, Epoch);
        }

        var reads = await client.ReadManyAsync(names, CancellationToken.None);
        var writes = await client.WriteManyAsync(values, CancellationToken.None);
        var nullWrite = await client.WriteAsync(
            new("CanonicalString", null, Epoch),
            CancellationToken.None);

        await Assert.That(cases.Length).IsEqualTo(CanonicalTypeCount);
        await Assert.That(reads.Count).IsEqualTo(CanonicalTypeCount);
        await Assert.That(writes.Count).IsEqualTo(CanonicalTypeCount);
        await Assert.That(nullWrite.Succeeded).IsTrue();
        await Assert.That(nullWrite.Value!.Value).IsNull();
        await Assert.That(channel.SendCount).IsGreaterThan(0);
    }

    /// <summary>Verifies unsupported keys traverse and reject every dispatch decision family.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task UnsupportedTypes_AreRejectedAcrossEveryDispatchPathAsync()
    {
        var channel = new BatchTestChannel();
        var catalog = new LogicalTagCatalog();
        using var connection = CreateConnection(channel);
        using var driver = new OmronPlcRx(
            connection,
            TimeSpan.FromMilliseconds(TimeoutMilliseconds),
            false);
        using var client = new OmronLogicalTagClient(driver, catalog);
        var index = 0;

        foreach (var dataType in CreateUnsupportedTypeProbes())
        {
            var tag = new LogicalTag($"Unsupported{index}", "D300", dataType);
            index++;
            await AssertThrowsAsync<NotSupportedException>(
                () => Task.Run(() => client.RegisterTag(tag)));
            catalog.Upsert(tag);

            var read = await client.ReadAsync(tag.Name, CancellationToken.None);
            var write = await client.WriteAsync(
                new(tag.Name, decimal.Zero, Epoch),
                CancellationToken.None);
            await AssertThrowsAsync<NotSupportedException>(
                () => Task.Run(() => client.Observe(tag.Name)));

            var valueTypeError = InvokeUnsupportedHelper(
                GetValueTypeMethodName,
                tag,
                null);
            var conversionError = InvokeUnsupportedHelper(
                ConvertBatchValueMethodName,
                tag,
                decimal.Zero);

            await Assert.That(read.Succeeded).IsFalse();
            await Assert.That(write.Succeeded).IsFalse();
            await Assert.That(valueTypeError).IsTypeOf<NotSupportedException>();
            await Assert.That(conversionError).IsTypeOf<NotSupportedException>();
        }
    }

    /// <summary>Verifies all supported area aliases and string metadata forms reach native writes.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task AddressAreasAndStringMetadata_CoverEveryConversionPathAsync()
    {
        var channel = new BatchTestChannel();
        using var connection = CreateConnection(channel);
        using var driver = new OmronPlcRx(
            connection,
            TimeSpan.FromMilliseconds(TimeoutMilliseconds),
            false);
        using var client = new OmronLogicalTagClient(driver);
        string[] areas = ["D", "DM", "C", "CIO", "W", "H", "A"];
        var results = new List<TagOperationResult<LogicalTagValue>>();
        for (var index = 0; index < areas.Length; index++)
        {
            var tagName = $"Area{areas[index]}";
            client.RegisterTag(
                new(
                    tagName,
                    $"{areas[index]}{AreaBaseAddress + index}.0",
                    typeof(bool).FullName!));
            results.Add(
                await client.WriteAsync(
                    new(tagName, true, Epoch),
                    CancellationToken.None));
        }

        client.RegisterTag(
            new("StringDefault", "D500", typeof(string).FullName!));
        client.RegisterTag(
            new("StringInvalidLength", "D520[0]", typeof(string).FullName!));
        results.Add(
            await client.WriteAsync(
                new("StringDefault", "AB", Epoch),
                CancellationToken.None));
        results.Add(
            await client.WriteAsync(
                new("StringInvalidLength", "CD", Epoch),
                CancellationToken.None));
        var groupedValues = new LogicalTagValue[areas.Length];
        for (var index = 0; index < areas.Length; index++)
        {
            groupedValues[index] = new($"Area{areas[index]}", true, Epoch);
        }

        var grouped = await client.WriteManyAsync(groupedValues, CancellationToken.None);

        client.RegisterTag(
            new("UnsupportedArea", "Z600.0", typeof(bool).FullName!));
        var unsupported = await client.WriteAsync(
            new("UnsupportedArea", true, Epoch),
            CancellationToken.None);

        await Assert.That(AllSucceeded(results)).IsTrue();
        await Assert.That(AllSucceeded(grouped)).IsTrue();
        await Assert.That(unsupported.Succeeded).IsFalse();
    }

    /// <summary>Writes every abbreviated type through the individual logical dispatch path.</summary>
    /// <param name="client">Logical client under test.</param>
    /// <param name="timestamp">Deterministic logical value timestamp.</param>
    /// <returns>The caller-positioned write results.</returns>
    private static async Task<TagOperationResult<LogicalTagValue>[]> WriteAliasValuesAsync(
        OmronLogicalTagClient client,
        DateTimeOffset timestamp)
    {
        LogicalTagValue[] values =
        [
            new(BooleanAliasTag, true, timestamp),
            new(ShortAliasTag, ShortValue, timestamp),
            new(UnsignedShortAliasTag, UnsignedShortValue, timestamp),
            new(IntegerAliasTag, IntegerValue, timestamp),
            new(UnsignedIntegerAliasTag, UnsignedIntegerValue, timestamp),
            new(SingleAliasTag, SingleValue, timestamp),
        ];
        var results = new List<TagOperationResult<LogicalTagValue>>(SupportedAliasItemCount);
        foreach (var value in values)
        {
            results.Add(await client.WriteAsync(value, CancellationToken.None).ConfigureAwait(false));
        }

        return results.ToArray();
    }

    /// <summary>Reads every abbreviated type through the individual logical dispatch path.</summary>
    /// <param name="client">Logical client under test.</param>
    /// <returns>The caller-positioned read results.</returns>
    private static async Task<TagOperationResult<LogicalTagValue>[]> ReadAliasValuesAsync(
        OmronLogicalTagClient client)
    {
        string[] names =
        [
            BooleanAliasTag,
            ShortAliasTag,
            UnsignedShortAliasTag,
            IntegerAliasTag,
            UnsignedIntegerAliasTag,
            SingleAliasTag,
        ];
        var results = new List<TagOperationResult<LogicalTagValue>>(SupportedAliasItemCount);
        foreach (var name in names)
        {
            results.Add(await client.ReadAsync(name, CancellationToken.None).ConfigureAwait(false));
        }

        return results.ToArray();
    }

    /// <summary>Determines whether every operation result succeeded.</summary>
    /// <param name="results">Operation results to inspect.</param>
    /// <returns><see langword="true"/> when all operations succeeded; otherwise, <see langword="false"/>.</returns>
    private static bool AllSucceeded(IReadOnlyList<TagOperationResult<LogicalTagValue>> results)
    {
        foreach (var result in results)
        {
            if (!result.Succeeded)
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>Determines whether every operation result failed.</summary>
    /// <param name="results">Operation results to inspect.</param>
    /// <returns><see langword="true"/> when no operation succeeded; otherwise, <see langword="false"/>.</returns>
    private static bool AllFailed(IReadOnlyList<TagOperationResult<LogicalTagValue>> results)
    {
        foreach (var result in results)
        {
            if (result.Succeeded)
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>Captures and verifies an asynchronous exception.</summary>
    /// <typeparam name="TException">Expected exception type.</typeparam>
    /// <param name="action">Action to invoke.</param>
    /// <returns>A task representing the assertion.</returns>
    private static async Task AssertThrowsAsync<TException>(Func<Task> action)
        where TException : Exception
    {
        try
        {
            await action().ConfigureAwait(false);
        }
        catch (TException exception)
        {
            await Assert.That(exception).IsNotNull();
            return;
        }

        throw new InvalidOperationException($"Expected {nameof(TException)}.");
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
        client.RegisterTag(new(BooleanAliasTag, "D130.0", BooleanAliasDataType));
        client.RegisterTag(new(ShortAliasTag, "D131", ShortAliasDataType));
        client.RegisterTag(new(UnsignedShortAliasTag, "D132", UnsignedShortAliasDataType));
        client.RegisterTag(new(IntegerAliasTag, "D133", IntegerAliasDataType));
        client.RegisterTag(new(UnsignedIntegerAliasTag, "D135", UnsignedIntegerAliasDataType));
        client.RegisterTag(new(SingleAliasTag, "D137", SingleAliasDataType));
        catalog.Upsert(new(UnsupportedAliasTag, "D139", UnsupportedDataType));
    }

    /// <summary>Creates one deterministic case for every canonical logical type.</summary>
    /// <returns>The canonical type cases.</returns>
    private static CanonicalTypeCase[] CreateCanonicalCases() =>
    [
        new("CanonicalBoolean", "D200.0", typeof(bool).FullName!, true),
        new("CanonicalByte", "D201", typeof(byte).FullName!, byte.MaxValue),
        new("CanonicalInt16", "D202", typeof(short).FullName!, ShortValue),
        new("CanonicalUInt16", "D203", typeof(ushort).FullName!, UnsignedShortValue),
        new("CanonicalInt32", "D204", typeof(int).FullName!, IntegerValue),
        new("CanonicalUInt32", "D206", typeof(uint).FullName!, UnsignedIntegerValue),
        new("CanonicalSingle", "D208", typeof(float).FullName!, SingleValue),
        new("CanonicalDouble", "D210", typeof(double).FullName!, DoubleValue),
        new("CanonicalString", "D214[4]", typeof(string).FullName!, "AB"),
        new("CanonicalBcd16", "D216", typeof(Bcd16).FullName!, new Bcd16(Bcd16Value)),
        new("CanonicalBcdU16", "D217", typeof(BcdU16).FullName!, new BcdU16(BcdU16Value)),
        new("CanonicalBcd32", "D218", typeof(Bcd32).FullName!, new Bcd32(Bcd32Value)),
        new("CanonicalBcdU32", "D220", typeof(BcdU32).FullName!, new BcdU32(BcdU32Value)),
    ];

    /// <summary>Creates probes that differ at every decision character in every supported key.</summary>
    /// <returns>The unsupported type probes.</returns>
    private static string[] CreateUnsupportedTypeProbes()
    {
        var probes = new HashSet<string>(UnsupportedTypeProbes, StringComparer.Ordinal);
        foreach (var supported in SupportedDispatchKeys)
        {
            for (var index = 0; index < supported.Length; index++)
            {
                var characters = new StringBuilder(supported);
                characters[index] = characters[index] == 'Z' ? 'Y' : 'Z';
                _ = probes.Add(characters.ToString());
            }
        }

        var result = new string[probes.Count];
        probes.CopyTo(result);
        return result;
    }

    /// <summary>Invokes a private batch helper and returns its semantic exception.</summary>
    /// <param name="methodName">Helper method name.</param>
    /// <param name="tag">Unsupported logical tag.</param>
    /// <param name="value">Optional logical value.</param>
    /// <returns>The semantic helper exception.</returns>
    private static Exception InvokeUnsupportedHelper(
        string methodName,
        LogicalTag tag,
        object? value)
    {
        var method = typeof(OmronLogicalTagClient).GetMethod(
            methodName,
            BindingFlags.NonPublic | BindingFlags.Static)
            ?? throw new InvalidOperationException($"Method '{methodName}' was not found.");
        var arguments = methodName == GetValueTypeMethodName
            ? new object?[] { tag }
            : [tag, value];
        try
        {
            _ = method.Invoke(null, arguments);
        }
        catch (TargetInvocationException exception) when (exception.InnerException is not null)
        {
            return exception.InnerException;
        }

        throw new InvalidOperationException($"Method '{methodName}' did not reject the type.");
    }

    /// <summary>Creates an initialized deterministic FINS connection.</summary>
    /// <param name="channel">Injected channel.</param>
    /// <returns>The initialized connection.</returns>
    private static OmronPLCConnection CreateConnection(BatchTestChannel channel) =>
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

    /// <summary>Provides a deterministic channel for grouped-write protocol tests on every target framework.</summary>
    private sealed class BatchTestChannel : BaseChannel
    {
        /// <summary>Gets the deterministic test channel port.</summary>
        private const int TestChannelPort = 9600;

        /// <summary>Gets the service identifier byte offset.</summary>
        private const int ServiceIdOffset = 9;

        /// <summary>Gets the command-code byte offset.</summary>
        private const int CommandCodeOffset = 10;

        /// <summary>Gets the command-subcode byte offset.</summary>
        private const int CommandSubcodeOffset = 11;

        /// <summary>Stores the most recently sent request.</summary>
        private byte[] _lastSent = [];

        /// <summary>Initializes a new instance of the <see cref="BatchTestChannel"/> class.</summary>
        internal BatchTestChannel()
            : base("test", TestChannelPort)
        {
        }

        /// <summary>Gets the number of requests sent through the channel.</summary>
        internal int SendCount { get; private set; }

        /// <inheritdoc />
        internal override Task InitializeAsync(int timeout, CancellationToken cancellationToken) =>
            Task.CompletedTask;

        /// <inheritdoc />
        protected override Task DestroyAndInitializeClientAsync(
            int timeout,
            CancellationToken cancellationToken) =>
            Task.CompletedTask;

        /// <inheritdoc />
        protected override Task<SendMessageResult> SendMessageAsync(
            ReadOnlyMemory<byte> message,
            int timeout,
            CancellationToken cancellationToken)
        {
            SendCount++;
            _lastSent = message.ToArray();
            return Task.FromResult(
                new SendMessageResult
                {
                    Bytes = _lastSent.Length,
                    Packets = 1,
                });
        }

        /// <inheritdoc />
        protected override Task<ReceiveMessageResult> ReceiveMessageAsync(
            int timeout,
            CancellationToken cancellationToken)
        {
            var response = new byte[
                FINSResponse.HeaderLength
                + FINSResponse.CommandLength
                + FINSResponse.ResponseCodeLength];
            response[ServiceIdOffset] = _lastSent[ServiceIdOffset];
            response[CommandCodeOffset] = _lastSent[CommandCodeOffset];
            response[CommandSubcodeOffset] = _lastSent[CommandSubcodeOffset];
            return Task.FromResult(
                new ReceiveMessageResult
                {
                    Bytes = response.Length,
                    Packets = 1,
                    Message = response,
                });
        }

        /// <inheritdoc />
        protected override Task PurgeReceiveBufferAsync(
            int timeout,
            CancellationToken cancellationToken) =>
            Task.CompletedTask;
    }

    /// <summary>Describes one canonical logical type case.</summary>
    /// <param name="name">Logical tag name.</param>
    /// <param name="address">PLC address.</param>
    /// <param name="dataType">Fully qualified data type.</param>
    /// <param name="value">Deterministic value.</param>
    private sealed class CanonicalTypeCase(
        string name,
        string address,
        string dataType,
        object value)
    {
        /// <summary>Gets the logical tag name.</summary>
        internal string Name { get; } = name;

        /// <summary>Gets the PLC address.</summary>
        internal string Address { get; } = address;

        /// <summary>Gets the fully qualified data type.</summary>
        internal string DataType { get; } = dataType;

        /// <summary>Gets the deterministic value.</summary>
        internal object Value { get; } = value;
    }
}
