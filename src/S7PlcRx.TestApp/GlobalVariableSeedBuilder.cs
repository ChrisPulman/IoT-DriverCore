// Copyright (c) 2019-2026 Chris Pulman and contributors. All rights reserved.
// Chris Pulman and contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers.Binary;

namespace IoT.DriverCore.S7PlcRx.TestApp;

/// <summary>Builds the mock data-block payload while registering its logical tags.</summary>
internal sealed class GlobalVariableSeedBuilder
{
    /// <summary>The amount by which a Boolean value advances the logical address.</summary>
    private const double BitAddressIncrement = 0.125;

    /// <summary>The boundary used to align word-sized values.</summary>
    private const double WordAlignmentBoundary = 2.0;

    /// <summary>The PLC used to register generated tag definitions.</summary>
    private readonly RxS7 _plc;

    /// <summary>The next logical byte-and-bit offset.</summary>
    private double _offset;

    /// <summary>Initializes a new instance of the <see cref="GlobalVariableSeedBuilder"/> class.</summary>
    /// <param name="size">The data-block size.</param>
    /// <param name="plc">The PLC used to register logical tags.</param>
    internal GlobalVariableSeedBuilder(int size, RxS7 plc)
    {
        Data = new byte[size];
        _plc = plc;
    }

    /// <summary>Gets the generated data-block payload.</summary>
    internal byte[] Data { get; }

    /// <summary>Writes a seed value and registers its logical tag.</summary>
    /// <param name="path">The logical tag path.</param>
    /// <param name="value">The seed value.</param>
    internal void Write(string path, object value)
    {
        switch (value)
        {
            case bool boolValue:
                {
                    WriteBoolean(path, boolValue);
                    break;
                }

            case byte byteValue:
                {
                    WriteByte(path, byteValue);
                    break;
                }

            case sbyte sbyteValue:
                {
                    WriteByte(path, unchecked((byte)sbyteValue));
                    break;
                }

            case short shortValue:
                {
                    WriteInt16(path, shortValue);
                    break;
                }

            case ushort ushortValue:
                {
                    WriteUInt16(path, ushortValue);
                    break;
                }

            case int intValue:
                {
                    WriteInt32(path, intValue);
                    break;
                }

            case uint uintValue:
                {
                    WriteUInt32(path, uintValue);
                    break;
                }

            case float floatValue:
                {
                    WriteSingle(path, floatValue);
                    break;
                }

            default:
                throw new NotSupportedException($"Seed data type {value.GetType().Name} is not supported for {path}.");
        }
    }

    /// <summary>Aligns the current offset to the next byte.</summary>
    private void AlignByte() => _offset = Math.Ceiling(_offset);

    /// <summary>Aligns the current offset to the next word boundary.</summary>
    private void AlignWord()
    {
        _offset = Math.Ceiling(_offset);
        if ((_offset / WordAlignmentBoundary) <= Math.Floor(_offset / WordAlignmentBoundary))
        {
            return;
        }

        _offset++;
    }

    /// <summary>Ensures that a value fits in the generated payload.</summary>
    /// <param name="path">The logical tag path.</param>
    /// <param name="startIndex">The first byte used by the value.</param>
    /// <param name="length">The value length in bytes.</param>
    private void EnsureCapacity(string path, int startIndex, int length)
    {
        if (startIndex >= 0 && startIndex + length <= Data.Length)
        {
            return;
        }

        throw new InvalidOperationException($"Seed data buffer is too small for {path} at offset {startIndex}.");
    }

    /// <summary>Registers a logical tag with the PLC.</summary>
    /// <param name="path">The logical tag path.</param>
    /// <param name="type">The tag value type.</param>
    /// <param name="address">The PLC address.</param>
    private void RegisterTag(string path, Type type, string address)
        => TagOperations.AddUpdateTagItem(_plc, type, path, address).SetPolling(false);

    /// <summary>Writes a Boolean seed value.</summary>
    /// <param name="path">The logical tag path.</param>
    /// <param name="value">The value to write.</param>
    private void WriteBoolean(string path, bool value)
    {
        var byteOffset = (int)Math.Floor(_offset);
        var bitOffset = (int)((_offset - byteOffset) / BitAddressIncrement);
        EnsureCapacity(path, byteOffset, 1);
        RegisterTag(path, typeof(bool), $"DB1.DBX{byteOffset}.{bitOffset}");
        if (value)
        {
            Data[byteOffset] |= (byte)(1 << bitOffset);
        }
        else
        {
            Data[byteOffset] &= (byte)~(1 << bitOffset);
        }

        _offset += BitAddressIncrement;
    }

    /// <summary>Writes a byte seed value.</summary>
    /// <param name="path">The logical tag path.</param>
    /// <param name="value">The value to write.</param>
    private void WriteByte(string path, byte value)
    {
        AlignByte();
        RegisterTag(path, typeof(byte), $"DB1.DBB{(int)_offset}");
        WriteBytes(path, [value]);
    }

    /// <summary>Writes raw bytes at the current offset.</summary>
    /// <param name="path">The logical tag path.</param>
    /// <param name="value">The bytes to write.</param>
    private void WriteBytes(string path, ReadOnlySpan<byte> value)
    {
        var startIndex = (int)_offset;
        EnsureCapacity(path, startIndex, value.Length);
        value.CopyTo(Data.AsSpan(startIndex, value.Length));
        _offset += value.Length;
    }

    /// <summary>Writes a signed 16-bit seed value.</summary>
    /// <param name="path">The logical tag path.</param>
    /// <param name="value">The value to write.</param>
    private void WriteInt16(string path, short value)
    {
        AlignWord();
        RegisterTag(path, typeof(short), $"DB1.DBW{(int)_offset}");
        Span<byte> bytes = stackalloc byte[sizeof(short)];
        BinaryPrimitives.WriteInt16BigEndian(bytes, value);
        WriteBytes(path, bytes);
    }

    /// <summary>Writes a signed 32-bit seed value.</summary>
    /// <param name="path">The logical tag path.</param>
    /// <param name="value">The value to write.</param>
    private void WriteInt32(string path, int value)
    {
        AlignWord();
        RegisterTag(path, typeof(int), $"DB1.DBD{(int)_offset}");
        Span<byte> bytes = stackalloc byte[sizeof(int)];
        BinaryPrimitives.WriteInt32BigEndian(bytes, value);
        WriteBytes(path, bytes);
    }

    /// <summary>Writes a single-precision seed value.</summary>
    /// <param name="path">The logical tag path.</param>
    /// <param name="value">The value to write.</param>
    private void WriteSingle(string path, float value)
    {
        AlignWord();
        RegisterTag(path, typeof(float), $"DB1.DBD{(int)_offset}");
        var rawValue = BitConverter.SingleToUInt32Bits(value);
        Span<byte> bytes = stackalloc byte[sizeof(uint)];
        BinaryPrimitives.WriteUInt32BigEndian(bytes, rawValue);
        WriteBytes(path, bytes);
    }

    /// <summary>Writes an unsigned 16-bit seed value.</summary>
    /// <param name="path">The logical tag path.</param>
    /// <param name="value">The value to write.</param>
    private void WriteUInt16(string path, ushort value)
    {
        AlignWord();
        RegisterTag(path, typeof(ushort), $"DB1.DBW{(int)_offset}");
        Span<byte> bytes = stackalloc byte[sizeof(ushort)];
        BinaryPrimitives.WriteUInt16BigEndian(bytes, value);
        WriteBytes(path, bytes);
    }

    /// <summary>Writes an unsigned 32-bit seed value.</summary>
    /// <param name="path">The logical tag path.</param>
    /// <param name="value">The value to write.</param>
    private void WriteUInt32(string path, uint value)
    {
        AlignWord();
        RegisterTag(path, typeof(uint), $"DB1.DBD{(int)_offset}");
        Span<byte> bytes = stackalloc byte[sizeof(uint)];
        BinaryPrimitives.WriteUInt32BigEndian(bytes, value);
        WriteBytes(path, bytes);
    }
}
