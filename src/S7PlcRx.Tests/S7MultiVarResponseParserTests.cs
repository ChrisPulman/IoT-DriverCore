// Copyright (c) 2019-2026 Chris Pulman and contributors. All rights reserved.
// Chris Pulman and contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers;
using IoT.Driver.S7PlcRx.Core;
using IoT.Driver.S7PlcRx.Enums;

namespace IoT.Driver.S7PlcRx.Tests;

/// <summary>Tests for S7MultiVar response parsing.</summary>
[System.Diagnostics.DebuggerDisplay("{DebuggerDisplay,nq}")]
public class S7MultiVarResponseParserTests
{
    /// <summary>Length of the fixed response header.</summary>
    private const int ResponseHeaderLength = 21;

    /// <summary>Length of the response parameter section.</summary>
    private const int ResponseParameterLength = 2;

    /// <summary>Expected number of read items.</summary>
    private const int ReadItemCount = 2;

    /// <summary>Encoded length of the first read item including its padding byte.</summary>
    private const int FirstReadItemLength = 6;

    /// <summary>Encoded length of the final read item without a trailing padding byte.</summary>
    private const int FinalReadItemLength = 5;

    /// <summary>Offset of the high length byte in a read item.</summary>
    private const int ReadItemLengthHighByteOffset = 2;

    /// <summary>Offset of the low length byte in a read item.</summary>
    private const int ReadItemLengthLowByteOffset = 3;

    /// <summary>Offset of the first data byte in a read item.</summary>
    private const int ReadItemDataOffset = 4;

    /// <summary>Offset of the optional padding byte in a read item.</summary>
    private const int ReadItemPaddingOffset = 5;

    /// <summary>Expected number of write items.</summary>
    private const int WriteItemCount = 3;

    /// <summary>Offset of the first write-item return code.</summary>
    private const int FirstWriteItemOffset = ResponseHeaderLength;

    /// <summary>Offset of the second write-item return code.</summary>
    private const int SecondWriteItemOffset = FirstWriteItemOffset + 1;

    /// <summary>Offset of the third write-item return code.</summary>
    private const int ThirdWriteItemOffset = SecondWriteItemOffset + 1;

    /// <summary>Expected data for the first parsed read item.</summary>
    private static readonly byte[] FirstReadItemData = [0xAA];

    /// <summary>Expected data for the second parsed read item.</summary>
    private static readonly byte[] SecondReadItemData = [0xBB];

    /// <summary>Gets the compact representation displayed by the debugger.</summary>
    [System.Diagnostics.DebuggerBrowsable(System.Diagnostics.DebuggerBrowsableState.Never)]
    private string DebuggerDisplay
    {
        get => ToString() ?? GetType().Name;
    }

    /// <summary>Ensures read-var response parsing returns an empty list for short frames.</summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous unit test.</returns>
    [Test]
    public async Task ParseReadVarResponse_WhenTooShort_ShouldReturnEmpty()
    {
        await Assert.That(DebuggerDisplay, Is.Not.Null);
        var items = new[] { new S7MultiVar.ReadItem(DataType.DataBlock, 1, 0, 1, "T0") };

        var result = S7MultiVar.ParseReadVarResponse(ReadOnlySpan<byte>.Empty, items, ArrayPool<byte>.Shared);

        await Assert.That(result, Is.EmptyValue);
    }

    /// <summary>Ensures read-var response parsing respects item padding to even byte length.</summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous unit test.</returns>
    [Test]
    public async Task ParseReadVarResponse_WithOddLengthData_ShouldSkipPadByte()
    {
        await Assert.That(DebuggerDisplay, Is.Not.Null);
        var items = new[]
        {
            new S7MultiVar.ReadItem(DataType.DataBlock, 1, 0, 1, "T0"),
            new S7MultiVar.ReadItem(DataType.DataBlock, 1, 1, 1, "T1"),
        };

        // Build minimal frame with:
        // - AckData fixed header = 19 and paramLength = 2 (so dataStart = 21)
        // - data section has 2 items:
        //   item0: rc=0xFF, ts=0x04, bitLen=8 => 1 byte data + 1 pad byte
        //   item1: rc=0xFF, ts=0x04, bitLen=8 => 1 byte data (no need to include final pad)
        var response = new byte[ResponseHeaderLength + FirstReadItemLength + FinalReadItemLength];
        response[13] = 0x00;
        response[14] = ResponseParameterLength;

        var o = ResponseHeaderLength;

        // item 0 header
        response[o + 0] = 0xFF;
        response[o + 1] = 0x04;
        response[o + ReadItemLengthHighByteOffset] = 0x00;
        response[o + ReadItemLengthLowByteOffset] = 0x08;
        response[o + ReadItemDataOffset] = 0xAA;
        response[o + ReadItemPaddingOffset] = 0x00; // pad
        o += FirstReadItemLength;

        // item 1 header
        response[o + 0] = 0xFF;
        response[o + 1] = 0x04;
        response[o + ReadItemLengthHighByteOffset] = 0x00;
        response[o + ReadItemLengthLowByteOffset] = 0x08;
        response[o + ReadItemDataOffset] = 0xBB;

        var pool = ArrayPool<byte>.Shared;
        var result = S7MultiVar.ParseReadVarResponse(response, items, pool);
        try
        {
            await Assert.That(result.Count, Is.EqualTo(ReadItemCount));
            await Assert.That(result[0].Data.ToArray(), Is.EqualTo(FirstReadItemData));
            await Assert.That(result[1].Data.ToArray(), Is.EqualTo(SecondReadItemData));
        }
        finally
        {
            foreach (var r in result)
            {
                if (r.RentedBuffer is not null)
                {
                    pool.Return(r.RentedBuffer);
                }
            }
        }
    }

    /// <summary>Ensures write-var response parsing reads per-item return codes.</summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous unit test.</returns>
    [Test]
    public async Task ParseWriteVarResponse_ShouldReturnPerItemCodes()
    {
        await Assert.That(DebuggerDisplay, Is.Not.Null);
        var response = new byte[ResponseHeaderLength + WriteItemCount];
        response[13] = 0x00;
        response[14] = 0x02;

        response[FirstWriteItemOffset] = 0xFF;
        response[SecondWriteItemOffset] = 0x0A;
        response[ThirdWriteItemOffset] = 0xFF;

        var result = S7MultiVar.ParseWriteVarResponse(response, WriteItemCount);

        await Assert.That(result.Count, Is.EqualTo(WriteItemCount));
        await Assert.That(result[0].ReturnCode, Is.EqualTo(0xFF));
        await Assert.That(result[1].ReturnCode, Is.EqualTo(0x0A));
        await Assert.That(result[2].ReturnCode, Is.EqualTo(0xFF));
    }
}
