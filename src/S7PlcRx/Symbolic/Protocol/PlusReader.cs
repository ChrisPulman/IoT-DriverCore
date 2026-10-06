// Copyright (c) 2019-2026 Chris Pulman and contributors. All rights reserved.
// Chris Pulman and contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.IO;

#if REACTIVE_SHIM
namespace IoT.Driver.S7PlcRx.Reactive.Symbolic.Protocol;
#else
namespace IoT.Driver.S7PlcRx.Symbolic.Protocol;
#endif

/// <summary>Provides plus reader.</summary>
internal sealed class PlusReader
{
    /// <summary>Provides data.</summary>
    private readonly byte[] _data;

    /// <summary>Provides position.</summary>
    private int _position;

    /// <summary>Initializes a new instance of the <see cref="PlusReader"/> class.</summary>
    /// <param name="data">The data.</param>
    internal PlusReader(byte[] data)
    {
#if NET6_0_OR_GREATER
        ArgumentNullException.ThrowIfNull(data);
#else
        if (data is null)
        {
            throw new ArgumentNullException(nameof(data));
        }
#endif
        _data = data;
    }

    /// <summary>Gets or sets position.</summary>
    internal int Position
    {
        get => _position;
        set
        {
            if (value < 0 || value > _data.Length)
            {
                throw new InvalidDataException("Position is outside the message.");
            }

            _position = value;
        }
    }

    /// <summary>Gets remaining.</summary>
    internal int Remaining => _data.Length - _position;

    /// <summary>Provides byte.</summary>
    /// <returns>The decoded or converted result.</returns>
    internal byte Byte()
    {
        Require(1);
        var value = _data[_position];
        _position++;
        return value;
    }

    /// <summary>Provides u int16.</summary>
    /// <returns>The decoded or converted result.</returns>
    internal ushort UInt16() => (ushort)((Byte() << 0x8) | Byte());

    /// <summary>Provides u int32.</summary>
    /// <returns>The decoded or converted result.</returns>
    internal uint UInt32() => ((uint)UInt16() << 0x10) | UInt16();

    /// <summary>Provides u int64.</summary>
    /// <returns>The decoded or converted result.</returns>
    internal ulong UInt64() => ((ulong)UInt32() << 0x20) | UInt32();

    /// <summary>Provides var u int32.</summary>
    /// <returns>The decoded or converted result.</returns>
    internal uint VarUInt32()
    {
        ulong value = 0;
        for (var i = 0; i < 0x5; i++)
        {
            var octet = Byte();
            value = (value << 7) | (uint)(octet & 0x7f);
            if (value > uint.MaxValue)
            {
                throw new InvalidDataException("Unsigned VLQ exceeds 32 bits.");
            }

            if ((octet & 0x80) == 0)
            {
                return (uint)value;
            }
        }

        throw new InvalidDataException("Unterminated 32-bit VLQ.");
    }

    /// <summary>Provides var u int64.</summary>
    /// <returns>The decoded or converted result.</returns>
    internal ulong VarUInt64()
    {
        ulong value = 0;
        for (var i = 0; i < 0x8; i++)
        {
            var octet = Byte();
            value = (value << 7) | (uint)(octet & 0x7f);
            if ((octet & 0x80) == 0)
            {
                return value;
            }
        }

        return (value << 0x8) | Byte();
    }

    /// <summary>Provides var int32.</summary>
    /// <returns>The decoded or converted result.</returns>
    internal int VarInt32()
    {
        var value = ReadSigned(false);
        if (value is < int.MinValue or > int.MaxValue)
        {
            throw new InvalidDataException("Signed VLQ exceeds 32 bits.");
        }

        return (int)value;
    }

    /// <summary>Provides var int64.</summary>
    /// <returns>The decoded or converted result.</returns>
    internal long VarInt64() => ReadSigned(true);

    /// <summary>Reads bytes.</summary>
    /// <returns>The decoded or converted result.</returns>
    /// <param name="length">The length.</param>
    internal byte[] ReadBytes(int length)
    {
        Require(length);
        var result = new byte[length];
        Buffer.BlockCopy(_data, _position, result, 0, length);
        _position += length;
        return result;
    }

    /// <summary>Provides skip.</summary>
    /// <param name="length">The length.</param>
    internal void Skip(int length)
    {
        Require(length);
        _position += length;
    }

    /// <summary>Reads signed.</summary>
    /// <returns>The decoded or converted result.</returns>
    /// <param name="wide">The wide.</param>
    private long ReadSigned(bool wide)
    {
        var first = Byte();
        long value = (first & 0x40) == 0 ? first & 0x3f : (first & 0x3f) - 0x40;
        var octet = first;
        var maximum = wide ? 0x8 : 0x5;
        for (var i = 1; i < maximum && (octet & 0x80) != 0; i++)
        {
            octet = Byte();
            value = (value << 7) | (uint)(octet & 0x7f);
        }

        if ((octet & 0x80) != 0)
        {
            if (!wide)
            {
                throw new InvalidDataException("Unterminated signed VLQ.");
            }

            return (value << 0x8) | Byte();
        }

        return value;
    }

    /// <summary>Provides require.</summary>
    /// <param name="length">The length.</param>
    private void Require(int length)
    {
        if (length >= 0 && length <= Remaining)
        {
            return;
        }

        throw new InvalidDataException("Truncated S7Plus message.");
    }
}
