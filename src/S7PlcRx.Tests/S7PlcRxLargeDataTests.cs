// Copyright (c) 2019-2026 Chris Pulman and contributors. All rights reserved.
// Chris Pulman and contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Globalization;
using MockS7Plc;
using S7PlcRx.PlcTypes;
using BclTimeSpan = System.TimeSpan;
using TUnitAssert = TUnit.Assertions.Assert;

namespace S7PlcRx.Tests;

/// <summary>
/// Tests that large byte[] reads and writes work correctly across all payload sizes,
/// including payloads that require multiple PDU-sized chunks (multi-chunk path).
/// Each test case writes packed S7-String data via the PLC protocol, reads it back as
/// byte[], converts to a list of strings and compares, then writes modified data back and
/// reads once more to confirm the round-trip is correct.
/// </summary>
[NotInParallel]
[System.Diagnostics.DebuggerDisplay("{DebuggerDisplay,nq}")]
public class S7PlcRxLargeDataTests
{
    /// <summary>Gets the maximum number of characters stored in each S7 string slot.</summary>
    private const int StringReservedLength = 20;

    /// <summary>Gets the number of header bytes in an S7 string slot.</summary>
    private const int StringHeaderSize = 2;

    /// <summary>Gets the number of bytes in each S7 string slot.</summary>
    private const int StringSlotSize = StringHeaderSize + StringReservedLength;

    /// <summary>Gets the number of letters in the English alphabet.</summary>
    private const int AlphabetSize = 26;

    /// <summary>Gets the minimum size for the mock server DB1 data block.</summary>
    private const int MinimumDb1Size = 4_096;

    /// <summary>Gets the extra DB1 capacity required beyond the payload.</summary>
    private const int AdditionalDb1Capacity = 64;

    /// <summary>Gets the payload size that is close to the PDU size.</summary>
    private const int NearPduPayloadSize = 960;

    /// <summary>Gets the first multi-chunk payload size.</summary>
    private const int FirstMultiChunkPayloadSize = 2_000;

    /// <summary>Gets the second multi-chunk payload size.</summary>
    private const int SecondMultiChunkPayloadSize = 4_000;

    /// <summary>Gets the server connection timeout in seconds.</summary>
    private const int ConnectionTimeoutSeconds = 10;

    /// <summary>Gets the polling delay in milliseconds.</summary>
    private const int PollingDelayMilliseconds = 100;

    /// <summary>Gets the tag name used for the large string block.</summary>
    private const string LargeBlockTagName = "LargeBlock";

    /// <summary>Gets the PLC address used for the large string block.</summary>
    private const string LargeBlockAddress = "DB1.DBB0";

    /// <summary>Gets the debugger display text.</summary>
    [System.Diagnostics.DebuggerBrowsable(System.Diagnostics.DebuggerBrowsableState.Never)]
    private string DebuggerDisplay
    {
        get => ToString() ?? string.Empty;
    }

    /// <summary>Gets the large payload sizes covered by the round-trip test.</summary>
    /// <returns>The payload sizes in bytes.</returns>
    public static IEnumerable<int> DataSizes()
    {
        yield return AdditionalDb1Capacity;
        yield return NearPduPayloadSize;
        yield return FirstMultiChunkPayloadSize;
        yield return SecondMultiChunkPayloadSize;
    }

    /// <summary>
    /// Verifies that a byte[] payload of the given total size can be written to the MockServer,
    /// read back, converted to a list of strings, compared, then written back and read again.
    /// </summary>
    /// <param name="totalBytes">Total byte footprint to test.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Test]
    [MethodDataSource(nameof(DataSizes))]
    public async Task LargeStringBlock_SeedReadWriteRoundTrip_ShouldMatchAtAllSizesAsync(int totalBytes)
    {
        var stringCount = totalBytes / StringSlotSize;
        if (stringCount == 0)
        {
            stringCount = 1;
        }

        var actualTotalBytes = stringCount * StringSlotSize;
        var seedStrings = BuildStringList(stringCount);
        var seedBytes = StringListToBytes(seedStrings);
        await TUnitAssert.That(seedBytes.Length).IsEqualTo(actualTotalBytes);

        using var server = new MockServer
        {
            DefaultDb1Size = Math.Max(MinimumDb1Size, actualTotalBytes + AdditionalDb1Capacity),
        };
        await TUnitAssert.That(server.Start()).IsEqualTo(0);

        using var plc = new RxS7(new(new(S7PlcRx.Enums.CpuType.S71500, MockServer.Localhost, 0, 1)));
        _ = TagOperations.AddUpdateTagItem(
            plc,
            typeof(byte[]),
            LargeBlockTagName,
            LargeBlockAddress,
            actualTotalBytes).SetPolling(false);

        await plc.IsConnected
            .Where(static x => x)
            .Timeout(BclTimeSpan.FromSeconds(ConnectionTimeoutSeconds))
            .FirstAsync();

        plc.Value(LargeBlockTagName, seedBytes);

        var readBytes = await WaitForExpectedBytesAsync(plc, seedBytes);
        await TUnitAssert.That(readBytes.Length).IsEqualTo(actualTotalBytes);

        var readStrings = BytesToStringList(readBytes, stringCount);
        await TUnitAssert.That(readStrings).IsEquivalentTo(seedStrings);

        var altStrings = seedStrings.ConvertAll(ModifyString);
        var altBytes = StringListToBytes(altStrings);

        plc.Value(LargeBlockTagName, altBytes);
        var readBytes2 = await WaitForExpectedBytesAsync(plc, altBytes);
        await TUnitAssert.That(readBytes2.Length).IsEqualTo(actualTotalBytes);

        var readStrings2 = BytesToStringList(readBytes2, stringCount);
        await TUnitAssert.That(readStrings2).IsEquivalentTo(altStrings);
    }

    /// <summary>Builds deterministic strings of varying lengths.</summary>
    /// <param name="count">The number of strings to create.</param>
    /// <returns>The generated strings.</returns>
    private static List<string> BuildStringList(int count)
    {
        var list = new List<string>(count);
        for (var i = 0; i < count; i++)
        {
            // Create strings of varying lengths that fit within the reserved area.
            var len = (i % StringReservedLength) + 1;
            list.Add(
                new string((char)('A' + (i % AlphabetSize)), len)
                + i.ToString(CultureInfo.InvariantCulture));

            // Truncate to reserved length if the i.ToString() made it too long.
            if (list[i].Length > StringReservedLength)
            {
                list[i] = list[i][..StringReservedLength];
            }
        }

        return list;
    }

    /// <summary>Returns a modified version of the string by rotating the first character.</summary>
    /// <param name="value">The string to modify.</param>
    /// <returns>The modified string.</returns>
    private static string ModifyString(string value)
    {
        if (value.Length == 0)
        {
            return "X";
        }

        var rotated = (char)(((value[0] - 'A' + 1) % AlphabetSize) + 'A');
        return rotated + value[1..];
    }

    /// <summary>Waits for a PLC tag to return the expected bytes.</summary>
    /// <param name="plc">The PLC to query.</param>
    /// <param name="expected">The expected byte values.</param>
    /// <param name="timeProvider">The time provider to use for deadline tracking; defaults to <see cref="TimeProvider.System"/>.</param>
    /// <returns>The latest bytes read from the PLC.</returns>
    private static async Task<byte[]> WaitForExpectedBytesAsync(RxS7 plc, byte[] expected, TimeProvider? timeProvider = null)
    {
        var tp = timeProvider ?? TimeProvider.System;
        var deadline = tp.GetUtcNow().UtcDateTime + BclTimeSpan.FromSeconds(ConnectionTimeoutSeconds);
        byte[]? latest = null;

        while (tp.GetUtcNow().UtcDateTime < deadline)
        {
            latest = await plc.ReadAsync(new LogicalTagKey<byte[]>(LargeBlockTagName), CancellationToken.None);
            if (latest is { Length: > 0 }
                && latest.Length == expected.Length
                && latest.AsSpan().SequenceEqual(expected))
            {
                return latest;
            }

            await Task.Delay(PollingDelayMilliseconds);
        }

        return latest ?? Array.Empty<byte>();
    }

    /// <summary>Encodes strings as back-to-back S7 string slots.</summary>
    /// <param name="strings">The strings to encode.</param>
    /// <returns>The encoded bytes.</returns>
    private static byte[] StringListToBytes(List<string> strings)
    {
        var buf = new byte[strings.Count * StringSlotSize];
        var offset = 0;
        foreach (var s in strings)
        {
            _ = S7String.ToSpan(s, StringReservedLength, buf.AsSpan(offset, StringSlotSize));
            offset += StringSlotSize;
        }

        return buf;
    }

    /// <summary>Decodes a byte[] containing back-to-back S7 string slots back into a list of strings.</summary>
    /// <param name="bytes">The bytes to decode.</param>
    /// <param name="count">The maximum number of strings to decode.</param>
    /// <returns>The decoded strings.</returns>
    private static List<string> BytesToStringList(byte[] bytes, int count)
    {
        var list = new List<string>(count);
        var offset = 0;
        for (var i = 0; i < count; i++)
        {
            if (offset + StringSlotSize > bytes.Length)
            {
                break;
            }

            list.Add(S7String.FromSpan(bytes.AsSpan(offset, StringSlotSize)));
            offset += StringSlotSize;
        }

        return list;
    }
}
