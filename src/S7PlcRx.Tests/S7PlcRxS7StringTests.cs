// Copyright (c) 2019-2026 Chris Pulman and contributors. All rights reserved.
// Chris Pulman and contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using IoT.DriverCore.S7PlcRx;
using IoT.DriverCore.S7PlcRx.PlcTypes;

namespace IoT.DriverCore.S7PlcRx.Tests;

/// <summary>Tests S7 string helpers.</summary>
[System.Diagnostics.DebuggerDisplay("{DebuggerDisplay,nq}")]
public class S7PlcRxS7StringTests
{
    /// <summary>Gets the number of bytes in an S7 string header.</summary>
    private const int StringHeaderSize = 2;

    /// <summary>Gets the maximum S7 string reserved length.</summary>
    private const int MaximumS7StringReservedLength = 255;

    /// <summary>Gets the small reserved length used by string tests.</summary>
    private const int SmallReservedLength = 3;

    /// <summary>Gets the medium reserved length used by string tests.</summary>
    private const int MediumReservedLength = 5;

    /// <summary>Gets the expected number of bytes written for the medium reserved length.</summary>
    private const int MediumStringSlotSize = StringHeaderSize + MediumReservedLength;

    /// <summary>Gets the debugger display text.</summary>
    [System.Diagnostics.DebuggerBrowsable(System.Diagnostics.DebuggerBrowsableState.Never)]
    private string DebuggerDisplay
    {
        get => ToString() ?? string.Empty;
    }

    /// <summary>Ensures S7String roundtrip works.</summary>
    [Test]
    public void S7String_Roundtrip_ShouldPreserveValueWithinReservedLength()
    {
        Assert.That(DebuggerDisplay, Is.Not.Null);
        var bytes = S7String.ToByteArray("HELLO", reservedLength: 10);
        var value = S7String.FromByteArray(bytes);
        Assert.That(value, Is.EqualTo("HELLO"));
    }

    /// <summary>Ensures S7WString roundtrip works.</summary>
    [Test]
    public void S7WString_Roundtrip_ShouldPreserveUnicodeValue()
    {
        Assert.That(DebuggerDisplay, Is.Not.Null);
        var bytes = S7WString.ToByteArray("H�??�", reservedLength: 10);
        var value = S7WString.FromByteArray(bytes);
        Assert.That(value, Is.EqualTo("H�??�"));
    }

    /// <summary>Ensures too-short S7String payload throws.</summary>
    [Test]
    public void S7String_FromByteArray_WhenTooShort_ShouldThrowPlcException()
    {
        Assert.That(DebuggerDisplay, Is.Not.Null);
        var ex = Assert.Throws<PlcException>(() => S7String.FromByteArray([0x01]));
        Assert.That(ex!.Message, Does.Contain("too short"));
    }

    /// <summary>Ensures invalid S7String header with length larger than capacity throws.</summary>
    [Test]
    public void S7String_FromByteArray_WhenLengthExceedsCapacity_ShouldThrowPlcException()
    {
        Assert.That(DebuggerDisplay, Is.Not.Null);

        // size=1, length=2 => invalid
        var ex = Assert.Throws<PlcException>(() => S7String.FromByteArray([0x01, 0x02, (byte)'A', (byte)'B']));
        Assert.That(ex!.Message, Does.Contain("length larger than capacity"));
    }

    /// <summary>Ensures S7String parsing with insufficient payload bytes throws.</summary>
    [Test]
    public void S7String_FromByteArray_WhenInsufficientPayload_ShouldThrowPlcException()
    {
        Assert.That(DebuggerDisplay, Is.Not.Null);

        // size=10, length=5 but only 1 byte payload present
        var ex = Assert.Throws<PlcException>(() => S7String.FromByteArray([0x0A, 0x05, (byte)'A']));
        Assert.That(ex!.Message, Does.Contain("Insufficient data"));
    }

    /// <summary>Ensures S7String reserved length constraint is enforced.</summary>
    [Test]
    public void S7String_ToSpan_WhenReservedLengthTooLarge_ShouldThrow()
    {
        Assert.That(DebuggerDisplay, Is.Not.Null);
        var dest = new byte[StringHeaderSize + MaximumS7StringReservedLength];
        _ = Assert.Throws<ArgumentException>(
            () => _ = S7String.ToSpan("A", reservedLength: MaximumS7StringReservedLength, dest));
    }

    /// <summary>Ensures S7String value length cannot exceed reserved length.</summary>
    [Test]
    public void S7String_ToSpan_WhenValueTooLongForReserved_ShouldThrow()
    {
        Assert.That(DebuggerDisplay, Is.Not.Null);
        var dest = new byte[StringHeaderSize + SmallReservedLength];
        _ = Assert.Throws<ArgumentException>(
            () => _ = S7String.ToSpan("ABCD", reservedLength: SmallReservedLength, dest));
    }

    /// <summary>Ensures TryToSpan returns false when destination is too small.</summary>
    [Test]
    public void S7String_TryToSpan_WhenDestinationTooSmall_ShouldReturnFalse()
    {
        Assert.That(DebuggerDisplay, Is.Not.Null);
        Span<byte> dest = stackalloc byte[StringHeaderSize + SmallReservedLength - 1];
        var ok = S7String.TryToSpan("A", reservedLength: SmallReservedLength, dest, out var written);
        Assert.That(ok, Is.False);
        Assert.That(written, Is.EqualTo(0));
    }

    /// <summary>Ensures TryToSpan returns false when value length exceeds reserved length.</summary>
    [Test]
    public void S7String_TryToSpan_WhenValueTooLong_ShouldReturnFalse()
    {
        Assert.That(DebuggerDisplay, Is.Not.Null);
        Span<byte> dest = stackalloc byte[StringHeaderSize + SmallReservedLength];
        var ok = S7String.TryToSpan("ABCD", reservedLength: SmallReservedLength, dest, out var written);
        Assert.That(ok, Is.False);
        Assert.That(written, Is.EqualTo(0));
    }

    /// <summary>Ensures ToSpan writes correct header and clears trailing bytes.</summary>
    [Test]
    public void S7String_ToSpan_ShouldWriteHeaderAndClearRemaining()
    {
        Assert.That(DebuggerDisplay, Is.Not.Null);
        Span<byte> dest = stackalloc byte[MediumStringSlotSize];
        dest.Fill(0xFF);

        var written = S7String.ToSpan("HI", reservedLength: MediumReservedLength, dest);
        Assert.That(written, Is.EqualTo(MediumStringSlotSize));
        Assert.That(dest[0], Is.EqualTo(MediumReservedLength));
        Assert.That(dest[1], Is.EqualTo(StringHeaderSize));
        Assert.That(dest[2], Is.EqualTo((byte)'H'));
        Assert.That(dest[3], Is.EqualTo((byte)'I'));
        Assert.That(dest[4], Is.EqualTo(0));
        Assert.That(dest[5], Is.EqualTo(0));
        Assert.That(dest[6], Is.EqualTo(0));
    }

    /// <summary>Ensures too-short S7WString payload throws.</summary>
    [Test]
    public void S7WString_FromByteArray_WhenTooShort_ShouldThrowPlcException()
    {
        Assert.That(DebuggerDisplay, Is.Not.Null);
        var ex = Assert.Throws<PlcException>(() => S7WString.FromByteArray([0x00, 0x01, 0x00]));
        Assert.That(ex!.Message, Does.Contain("too short"));
    }

    /// <summary>Ensures invalid S7WString header with length larger than capacity throws.</summary>
    [Test]
    public void S7WString_FromByteArray_WhenLengthExceedsCapacity_ShouldThrowPlcException()
    {
        Assert.That(DebuggerDisplay, Is.Not.Null);

        // size=1, length=2 => invalid
        var ex = Assert.Throws<PlcException>(
            () => S7WString.FromByteArray([0x00, 0x01, 0x00, 0x02, 0x00, 0x41, 0x00, 0x42]));
        Assert.That(ex!.Message, Does.Contain("length larger than capacity"));
    }

    /// <summary>Ensures S7WString ToByteArray rejects null.</summary>
    [Test]
    public void S7WString_ToByteArray_WhenNull_ShouldThrow()
    {
        Assert.That(DebuggerDisplay, Is.Not.Null);
        _ = Assert.Throws<ArgumentNullException>(
            () => _ = S7WString.ToByteArray(null, reservedLength: 1));
    }

    /// <summary>Ensures S7WString reserved length constraint is enforced.</summary>
    [Test]
    public void S7WString_ToByteArray_WhenReservedLengthTooLarge_ShouldThrow()
    {
        Assert.That(DebuggerDisplay, Is.Not.Null);
        _ = Assert.Throws<ArgumentException>(
            () => _ = S7WString.ToByteArray("A", reservedLength: 16_383));
    }
}
