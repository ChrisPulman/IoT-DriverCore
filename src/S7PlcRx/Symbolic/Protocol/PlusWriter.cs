// Copyright (c) 2019-2026 Chris Pulman and contributors. All rights reserved.
// Chris Pulman and contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

#if REACTIVE_SHIM
namespace IoT.Driver.S7PlcRx.Reactive.Symbolic.Protocol;
#else
namespace IoT.Driver.S7PlcRx.Symbolic.Protocol;
#endif

/// <summary>Provides plus writer.</summary>
internal sealed class PlusWriter
{
    /// <summary>Provides buffer.</summary>
    private readonly List<byte> _buffer;

    /// <summary>Initializes a new instance of the <see cref="PlusWriter"/> class.</summary>
    internal PlusWriter()
        : this(0)
    {
    }

    /// <summary>Initializes a new instance of the <see cref="PlusWriter"/> class.</summary>
    /// <param name="capacity">The number of bytes to reserve before writing.</param>
    internal PlusWriter(int capacity)
    {
#if NET8_0_OR_GREATER
        ArgumentOutOfRangeException.ThrowIfNegative(capacity);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(capacity, S7SymbolicValue.MaximumLength);
#else
        if (capacity is < 0 or > S7SymbolicValue.MaximumLength)
        {
            throw new ArgumentOutOfRangeException(nameof(capacity));
        }
#endif
        _buffer = [with(capacity: capacity)];
    }

    /// <summary>Provides byte.</summary>
    /// <param name="value">The value.</param>
    internal void Byte(byte value) => _buffer.Add(value);

    /// <summary>Provides bytes.</summary>
    /// <param name="value">The value.</param>
    internal void Bytes(byte[] value)
    {
#if NET6_0_OR_GREATER
        ArgumentNullException.ThrowIfNull(value);
#else
        if (value is null)
        {
            throw new ArgumentNullException(nameof(value));
        }
#endif
        _buffer.AddRange(value);
    }

    /// <summary>Erases buffered bytes and resets the writer.</summary>
    internal void ClearSensitive()
    {
        for (var index = 0; index < _buffer.Count; index++)
        {
            _buffer[index] = 0;
        }

        _buffer.Clear();
    }

    /// <summary>Provides u int16.</summary>
    /// <param name="value">The value.</param>
    internal void UInt16(ushort value)
    {
        Byte((byte)(value >> 0x8));
        Byte((byte)value);
    }

    /// <summary>Provides u int32.</summary>
    /// <param name="value">The value.</param>
    internal void UInt32(uint value)
    {
        UInt16((ushort)(value >> 0x10));
        UInt16((ushort)value);
    }

    /// <summary>Provides u int64.</summary>
    /// <param name="value">The value.</param>
    internal void UInt64(ulong value)
    {
        UInt32((uint)(value >> 0x20));
        UInt32((uint)value);
    }

    /// <summary>Provides var u int32.</summary>
    /// <param name="value">The value.</param>
    internal void VarUInt32(uint value) => WriteUnsigned(value, false);

    /// <summary>Provides var u int64.</summary>
    /// <param name="value">The value.</param>
    internal void VarUInt64(ulong value) => WriteUnsigned(value, true);

    /// <summary>Provides var int32.</summary>
    /// <param name="value">The value.</param>
    internal void VarInt32(int value) => WriteSigned(value, false);

    /// <summary>Provides var int64.</summary>
    /// <param name="value">The value.</param>
    internal void VarInt64(long value) => WriteSigned(value, true);

    /// <summary>Provides to array.</summary>
    /// <returns>The decoded or converted result.</returns>
    internal byte[] ToArray() => _buffer.ToArray();

    /// <summary>Provides encode signed groups.</summary>
    /// <returns>The decoded or converted result.</returns>
    /// <param name="bytes">The bytes.</param>
    /// <param name="value">The value.</param>
    private static int EncodeSignedGroups(byte[] bytes, long value)
    {
        var count = 0;
        while (true)
        {
            var octet = (byte)(value & 0x7f);
            value >>= 7;
            bytes[count] = (byte)(octet | (count == 0 ? 0 : 0x80));
            count++;
            if ((value == 0 && (octet & 0x40) == 0) || (value == -1 && (octet & 0x40) != 0))
            {
                break;
            }
        }

        return count;
    }

    /// <summary>Writes unsigned.</summary>
    /// <param name="value">The value.</param>
    /// <param name="wide">The wide.</param>
    private void WriteUnsigned(ulong value, bool wide)
    {
        var bytes = new byte[9];
        var count = 0;
        if (wide && value > 0x00ffffffffffffffUL)
        {
            bytes[count] = (byte)value;
            count++;
            value >>= 0x8;
            for (var i = 0; i < 0x8; i++)
            {
                bytes[count] = (byte)((value & 0x7f) | 0x80);
                count++;
                value >>= 7;
            }
        }
        else
        {
            bytes[count] = (byte)(value & 0x7f);
            count++;
            while ((value >>= 7) != 0)
            {
                bytes[count] = (byte)((value & 0x7f) | 0x80);
                count++;
            }
        }

        while (count != 0)
        {
            count--;
            Byte(bytes[count]);
        }
    }

    /// <summary>Writes signed.</summary>
    /// <param name="value">The value.</param>
    /// <param name="wide">The wide.</param>
    private void WriteSigned(long value, bool wide)
    {
        var bytes = new byte[9];
        var count = 0;
        if (wide && (value is > 0x007fffffffffffffL or < -0x0080000000000000L))
        {
            bytes[count] = (byte)value;
            count++;
            value >>= 0x8;
            for (var i = 0; i < 0x8; i++)
            {
                bytes[count] = (byte)((value & 0x7f) | 0x80);
                count++;
                value >>= 7;
            }
        }
        else
        {
            count = EncodeSignedGroups(bytes, value);
        }

        while (count != 0)
        {
            count--;
            Byte(bytes[count]);
        }
    }
}
