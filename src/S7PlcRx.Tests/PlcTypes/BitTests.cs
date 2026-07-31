// Copyright (c) 2019-2026 Chris Pulman and contributors. All rights reserved.
// Chris Pulman and contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Collections;
using IoT.Driver.S7PlcRx.PlcTypes;

namespace IoT.Driver.S7PlcRx.Tests.PlcTypes;

/// <summary>Tests Bit PlcType helpers.</summary>
[System.Diagnostics.DebuggerDisplay("{DebuggerDisplay,nq}")]
public class BitTests
{
    /// <summary>The bit index used when setting a bit.</summary>
    private const int SetBitIndex = 3;

    /// <summary>The number of bits requested from the test buffer.</summary>
    private const int RequestedBitCount = 4;

    /// <summary>The last valid bit index in a byte.</summary>
    private const int LastBitIndex = 7;

    /// <summary>The expected values returned by the multi-bit read.</summary>
    private static readonly bool[] Expected = [true, true];

    /// <summary>Gets a debugger-friendly test description.</summary>
    [System.Diagnostics.DebuggerBrowsable(System.Diagnostics.DebuggerBrowsableState.Never)]
    private string DebuggerDisplay => ToString() ?? string.Empty;

    /// <summary>Ensures FromByte extracts correct bit values.</summary>
    /// <param name="value">The value.</param>
    /// <param name="bit">The bit.</param>
    /// <param name="expected">if set to <c>true</c> [expected].</param>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    [Arguments(0b0000_0001, 0, true)]
    [Arguments(0b0000_0001, 1, false)]
    [Arguments(0b1000_0000, 7, true)]
    public async Task FromByte_ShouldReturnExpected(byte value, byte bit, bool expected)
    {
        await Assert.That(DebuggerDisplay, Is.Not.Null);
        await Assert.That(Bit.FromByte(value, bit), Is.EqualTo(expected));
    }

    /// <summary>Ensures FromSpan validates byte index.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task FromSpan_WhenByteIndexOutOfRange_ShouldThrow()
    {
        _ = await Assert.Throws<ArgumentOutOfRangeException>(static () => Bit.FromSpan(stackalloc byte[1], 1, 0));
    }

    /// <summary>Ensures FromSpan validates bit index.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    /// <param name="bitIndex">Index of the bit.</param>
    [Test]
    [Arguments(-1)]
    [Arguments(8)]
    public async Task FromSpan_WhenBitIndexInvalid_ShouldThrow(int bitIndex)
    {
        _ = await Assert.Throws<ArgumentOutOfRangeException>(() => Bit.FromSpan(stackalloc byte[1], 0, bitIndex));
    }

    /// <summary>Ensures SetBit sets and clears the selected bit.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task SetBit_ShouldSetAndClear()
    {
        var bytes = new byte[1];
        Bit.SetBit(bytes, 0, SetBitIndex, true);
        await Assert.That(bytes[0], Is.EqualTo(0b0000_1000));

        Bit.SetBit(bytes, 0, SetBitIndex, false);
        await Assert.That(bytes[0], Is.EqualTo(0));
    }

    /// <summary>Ensures ToBitArray throws when length is null.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task ToBitArray_WhenLengthNull_ShouldThrow()
    {
        await Assert.That(DebuggerDisplay, Is.Not.Null);
        _ = await Assert.Throws<ArgumentNullException>(static () => Bit.ToBitArray([0x00], length: null));
    }

    /// <summary>Ensures ToBitArray throws when bytes span is empty.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task ToBitArray_WhenEmptySpan_ShouldThrow()
    {
        await Assert.That(DebuggerDisplay, Is.Not.Null);
        _ = await Assert.Throws<ArgumentException>(static () => Bit.ToBitArray(ReadOnlySpan<byte>.Empty, 1));
    }

    /// <summary>Ensures ToBitArray throws when length exceeds available bits.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task ToBitArray_WhenLengthTooLarge_ShouldThrow()
    {
        await Assert.That(DebuggerDisplay, Is.Not.Null);
        _ = await Assert.Throws<ArgumentException>(static () => Bit.ToBitArray([0x00], length: 9));
    }

    /// <summary>Ensures ToBitArray returns exactly the requested number of bits.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task ToBitArray_ShouldRespectLength()
    {
        await Assert.That(DebuggerDisplay, Is.Not.Null);
        var bits = Bit.ToBitArray([0b0000_1111], length: RequestedBitCount);
        await Assert.That(bits.Length, Is.EqualTo(RequestedBitCount));
        await Assert.That(bits[0], Is.True);
        await Assert.That(bits[3], Is.True);
    }

    /// <summary>Ensures GetBits reads multiple positions correctly.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task GetBits_ShouldReturnExpected()
    {
        byte[] bytes = [0b0000_0011];
        var positions = new (int ByteIndex, int BitIndex)[2];
        positions[0] = (0, 0);
        positions[1] = (0, 1);

        var results = Bit.GetBits(bytes, positions);
        await Assert.That(results, Is.EqualTo(Expected));
    }

    /// <summary>Ensures SetBits applies multiple updates correctly.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task SetBits_ShouldApplyMultipleUpdates()
    {
        var bytes = new byte[1];
        var updates = new (int ByteIndex, int BitIndex, bool Value)[2];
        updates[0] = (0, 0, true);
        updates[1] = (0, LastBitIndex, true);

        Bit.SetBits(bytes, updates);
        await Assert.That(bytes[0], Is.EqualTo(0b1000_0001));
    }
}
