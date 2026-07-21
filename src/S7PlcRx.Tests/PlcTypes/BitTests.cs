// Copyright (c) 2019-2026 Chris Pulman and contributors. All rights reserved.
// Chris Pulman and contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Collections;
using S7PlcRx.PlcTypes;

namespace S7PlcRx.Tests.PlcTypes;

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
    [Test]
    [Arguments(0b0000_0001, 0, true)]
    [Arguments(0b0000_0001, 1, false)]
    [Arguments(0b1000_0000, 7, true)]
    public void FromByte_ShouldReturnExpected(byte value, byte bit, bool expected)
    {
        Assert.That(DebuggerDisplay, Is.Not.Null);
        Assert.That(Bit.FromByte(value, bit), Is.EqualTo(expected));
    }

    /// <summary>Ensures FromSpan validates byte index.</summary>
    [Test]
    public void FromSpan_WhenByteIndexOutOfRange_ShouldThrow()
    {
        _ = Assert.Throws<ArgumentOutOfRangeException>(() => Bit.FromSpan(stackalloc byte[1], 1, 0));
    }

    /// <summary>Ensures FromSpan validates bit index.</summary>
    /// <param name="bitIndex">Index of the bit.</param>
    [Test]
    [Arguments(-1)]
    [Arguments(8)]
    public void FromSpan_WhenBitIndexInvalid_ShouldThrow(int bitIndex)
    {
        _ = Assert.Throws<ArgumentOutOfRangeException>(() => Bit.FromSpan(stackalloc byte[1], 0, bitIndex));
    }

    /// <summary>Ensures SetBit sets and clears the selected bit.</summary>
    [Test]
    public void SetBit_ShouldSetAndClear()
    {
        Span<byte> bytes = stackalloc byte[1];
        Bit.SetBit(bytes, 0, SetBitIndex, true);
        Assert.That(bytes[0], Is.EqualTo(0b0000_1000));

        Bit.SetBit(bytes, 0, SetBitIndex, false);
        Assert.That(bytes[0], Is.EqualTo(0));
    }

    /// <summary>Ensures ToBitArray throws when length is null.</summary>
    [Test]
    public void ToBitArray_WhenLengthNull_ShouldThrow()
    {
        Assert.That(DebuggerDisplay, Is.Not.Null);
        _ = Assert.Throws<ArgumentNullException>(() => Bit.ToBitArray([0x00], length: null));
    }

    /// <summary>Ensures ToBitArray throws when bytes span is empty.</summary>
    [Test]
    public void ToBitArray_WhenEmptySpan_ShouldThrow()
    {
        Assert.That(DebuggerDisplay, Is.Not.Null);
        _ = Assert.Throws<ArgumentException>(() => Bit.ToBitArray(ReadOnlySpan<byte>.Empty, 1));
    }

    /// <summary>Ensures ToBitArray throws when length exceeds available bits.</summary>
    [Test]
    public void ToBitArray_WhenLengthTooLarge_ShouldThrow()
    {
        Assert.That(DebuggerDisplay, Is.Not.Null);
        _ = Assert.Throws<ArgumentException>(() => Bit.ToBitArray([0x00], length: 9));
    }

    /// <summary>Ensures ToBitArray returns exactly the requested number of bits.</summary>
    [Test]
    public void ToBitArray_ShouldRespectLength()
    {
        Assert.That(DebuggerDisplay, Is.Not.Null);
        var bits = Bit.ToBitArray([0b0000_1111], length: RequestedBitCount);
        Assert.That(bits.Length, Is.EqualTo(RequestedBitCount));
        Assert.That(bits[0], Is.True);
        Assert.That(bits[3], Is.True);
    }

    /// <summary>Ensures GetBits reads multiple positions correctly.</summary>
    [Test]
    public void GetBits_ShouldReturnExpected()
    {
        byte[] bytes = [0b0000_0011];
        Span<(int ByteIndex, int BitIndex)> positions = stackalloc (int, int)[2];
        positions[0] = (0, 0);
        positions[1] = (0, 1);

        var results = Bit.GetBits(bytes, positions);
        Assert.That(results, Is.EqualTo(Expected));
    }

    /// <summary>Ensures SetBits applies multiple updates correctly.</summary>
    [Test]
    public void SetBits_ShouldApplyMultipleUpdates()
    {
        Span<byte> bytes = stackalloc byte[1];
        Span<(int ByteIndex, int BitIndex, bool Value)> updates = stackalloc (int, int, bool)[2];
        updates[0] = (0, 0, true);
        updates[1] = (0, LastBitIndex, true);

        Bit.SetBits(bytes, updates);
        Assert.That(bytes[0], Is.EqualTo(0b1000_0001));
    }
}
