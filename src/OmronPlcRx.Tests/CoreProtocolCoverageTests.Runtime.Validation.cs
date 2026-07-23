// Copyright (c) 2019-2026 Chris Pulman and contributors. All rights reserved.
// Chris Pulman and contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using IoT.DriverCore.OmronPlcRx.Core;
using IoT.DriverCore.OmronPlcRx.Enums;

namespace IoT.DriverCore.OmronPlcRx.Tests;

/// <summary>Contains invalid-operation assertions for runtime protocol coverage.</summary>
public sealed partial class CoreProtocolCoverageTests
{
    /// <summary>Verifies read operations reject unsupported addresses, lengths and memory areas.</summary>
    /// <returns>A task that represents the asynchronous assertion operation.</returns>
    /// <param name="plc">The initialized PLC connection under test.</param>
    /// <param name="uninitialized">The uninitialized PLC connection under test.</param>
    private static async Task AssertInvalidReadOperationsAsync(
        OmronPLCConnection plc,
        OmronPLCConnection uninitialized)
    {
        await AssertThrowsAsync<OmronPLCException>(
            () => uninitialized.ReadWordsAsync(
                DataMemoryAddress,
                SingleWordCount,
                MemoryWordDataType.DataMemory,
                CancellationToken.None));
        await AssertThrowsAsync<ArgumentOutOfRangeException>(
            () => plc.ReadBitsAsync(
                DataMemoryAddress,
                SixteenBitOffset,
                SingleWordCount,
                MemoryBitDataType.CommonIO,
                CancellationToken.None));
        await AssertThrowsAsync<ArgumentOutOfRangeException>(
            () => plc.ReadBitsAsync(
                DataMemoryAddress,
                0,
                0,
                MemoryBitDataType.CommonIO,
                CancellationToken.None));
        await AssertThrowsAsync<ArgumentOutOfRangeException>(
            () => plc.ReadBitsAsync(
                DataMemoryAddress,
                FifteenBitOffset,
                PairCount,
                MemoryBitDataType.CommonIO,
                CancellationToken.None));
        await AssertThrowsAsync<ArgumentException>(
            () => plc.ReadBitsAsync(
                DataMemoryAddress,
                0,
                SingleWordCount,
                MemoryBitDataType.None,
                CancellationToken.None));
        await AssertThrowsAsync<ArgumentOutOfRangeException>(
            () => plc.ReadBitsAsync(
                InvalidWordAddress,
                0,
                SingleWordCount,
                MemoryBitDataType.DataMemory,
                CancellationToken.None));
        await AssertInvalidWordReadOperationsAsync(plc);
    }

    /// <summary>Verifies word reads reject invalid addresses, lengths and memory areas.</summary>
    /// <returns>A task that represents the asynchronous assertion operation.</returns>
    /// <param name="plc">The initialized PLC connection under test.</param>
    private static async Task AssertInvalidWordReadOperationsAsync(OmronPLCConnection plc)
    {
        await AssertThrowsAsync<ArgumentOutOfRangeException>(
            () => plc.ReadWordsAsync(
                DataMemoryAddress,
                0,
                MemoryWordDataType.DataMemory,
                CancellationToken.None));
        await AssertThrowsAsync<ArgumentOutOfRangeException>(
            () => plc.ReadWordsAsync(
                DataMemoryAddress,
                MaximumWordCount,
                MemoryWordDataType.DataMemory,
                CancellationToken.None));
        await AssertThrowsAsync<ArgumentException>(
            () => plc.ReadWordsAsync(
                DataMemoryAddress,
                SingleWordCount,
                MemoryWordDataType.None,
                CancellationToken.None));
        await AssertThrowsAsync<ArgumentOutOfRangeException>(
            () => plc.ReadWordsAsync(
                InvalidWordAddress,
                SingleWordCount,
                MemoryWordDataType.DataMemory,
                CancellationToken.None));
    }

    /// <summary>Verifies write and clock operations reject invalid inputs.</summary>
    /// <returns>A task that represents the asynchronous assertion operation.</returns>
    /// <param name="plc">The initialized PLC connection under test.</param>
    private static async Task AssertInvalidWriteOperationsAsync(OmronPLCConnection plc)
    {
        await AssertThrowsAsync<ArgumentNullException>(
            () => plc.WriteBitsAsync(
                null!,
                DataMemoryAddress,
                0,
                MemoryBitDataType.CommonIO,
                CancellationToken.None));
        await AssertThrowsAsync<ArgumentOutOfRangeException>(
            () => plc.WriteBitsAsync(
                [],
                DataMemoryAddress,
                0,
                MemoryBitDataType.CommonIO,
                CancellationToken.None));
        await AssertThrowsAsync<ArgumentOutOfRangeException>(
            () => plc.WriteBitsAsync(
                [true, false],
                DataMemoryAddress,
                FifteenBitOffset,
                MemoryBitDataType.CommonIO,
                CancellationToken.None));
        await AssertThrowsAsync<ArgumentNullException>(
            () => plc.WriteWordsAsync(
                null!,
                DataMemoryAddress,
                MemoryWordDataType.DataMemory,
                CancellationToken.None));
        await AssertThrowsAsync<ArgumentOutOfRangeException>(
            () => plc.WriteWordsAsync(
                [],
                DataMemoryAddress,
                MemoryWordDataType.DataMemory,
                CancellationToken.None));
        await AssertThrowsAsync<ArgumentOutOfRangeException>(
            () => plc.WriteWordsAsync(
                new short[InvalidWriteWordCount],
                DataMemoryAddress,
                MemoryWordDataType.DataMemory,
                CancellationToken.None));
        await AssertThrowsAsync<ArgumentOutOfRangeException>(
            () => plc.WriteClockAsync(UnsupportedEarlyClockDate, CancellationToken.None));
        await AssertThrowsAsync<ArgumentOutOfRangeException>(
            () => plc.WriteClockAsync(UnsupportedLateClockDate, CancellationToken.None));
        await AssertThrowsAsync<ArgumentOutOfRangeException>(
            () => plc.WriteClockAsync(ProtocolDateTime, -1, CancellationToken.None));
        await AssertThrowsAsync<ArgumentOutOfRangeException>(
            () => plc.WriteClockAsync(ProtocolDateTime, MaximumWeekday, CancellationToken.None));
    }
}
