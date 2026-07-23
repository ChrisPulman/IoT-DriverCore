// Copyright (c) 2019-2026 Chris Pulman and contributors. All rights reserved.
// Chris Pulman and contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers.Binary;
using System.Text;

#if REACTIVELIST_REACTIVE
namespace IoT.DriverCore.ABPlcRx.Reactive;
#else
namespace IoT.DriverCore.ABPlcRx;
#endif

/// <summary>Encodes supported public simulator values using libplctag byte layout.</summary>
internal static class ABPlcSimulatorValueCodec
{
    /// <summary>String payload bytes following the four-byte length header.</summary>
    private const int MaximumStringLength = 84;

    /// <summary>Writes to a byte span without allocating an intermediate buffer.</summary>
    /// <param name="bytes">The destination bytes.</param>
    private delegate void SpanWriter(Span<byte> bytes);

    /// <summary>Reads a value from a byte span.</summary>
    /// <typeparam name="T">The decoded value type.</typeparam>
    /// <param name="bytes">The source bytes.</param>
    /// <returns>The decoded value.</returns>
    private delegate T SpanReader<T>(ReadOnlySpan<byte> bytes);

    /// <summary>Encodes a supported scalar value.</summary>
    /// <typeparam name="T">The scalar type.</typeparam>
    /// <param name="value">The value.</param>
    /// <returns>The encoded bytes.</returns>
    internal static byte[] Encode<T>(T value)
    {
        if (value is null)
        {
            throw new ArgumentNullException(nameof(value));
        }

        return value switch
        {
            bool item => [item ? (byte)1 : (byte)0],
            byte item => [item],
            sbyte item => [unchecked((byte)item)],
            short item => Write(sizeof(short), bytes => BinaryPrimitives.WriteInt16LittleEndian(bytes, item)),
            ushort item => Write(sizeof(ushort), bytes => BinaryPrimitives.WriteUInt16LittleEndian(bytes, item)),
            int item => Write(sizeof(int), bytes => BinaryPrimitives.WriteInt32LittleEndian(bytes, item)),
            uint item => Write(sizeof(uint), bytes => BinaryPrimitives.WriteUInt32LittleEndian(bytes, item)),
            long item => Write(sizeof(long), bytes => BinaryPrimitives.WriteInt64LittleEndian(bytes, item)),
            ulong item => Write(sizeof(ulong), bytes => BinaryPrimitives.WriteUInt64LittleEndian(bytes, item)),
            float item => Write(
                sizeof(float),
                bytes => BinaryPrimitives.WriteInt32LittleEndian(bytes, BitConverterCompatibility.SingleToInt32Bits(item))),
            double item => Write(
                sizeof(double),
                bytes => BinaryPrimitives.WriteInt64LittleEndian(bytes, BitConverterCompatibility.DoubleToInt64Bits(item))),
            string item => EncodeString(item),
            _ => throw new NotSupportedException(
                $"Simulator scalar type '{value.GetType().FullName}' is not supported. Use SetTagBytes for structured values."),
        };
    }

    /// <summary>Decodes a supported scalar value.</summary>
    /// <typeparam name="T">The scalar type.</typeparam>
    /// <param name="bytes">The encoded bytes.</param>
    /// <returns>The decoded value.</returns>
    internal static T Decode<T>(byte[] bytes)
    {
        ArgumentExceptionHelper.ThrowIfNull(bytes, nameof(bytes));
        var type = typeof(T);
        object value = Type.GetTypeCode(type) switch
        {
            TypeCode.Boolean => Read(bytes, sizeof(byte), data => data[0] != 0),
            TypeCode.Byte => Read(bytes, sizeof(byte), data => data[0]),
            TypeCode.SByte => Read(bytes, sizeof(byte), data => unchecked((sbyte)data[0])),
            TypeCode.Int16 => Read(bytes, sizeof(short), BinaryPrimitives.ReadInt16LittleEndian),
            TypeCode.UInt16 => Read(bytes, sizeof(ushort), BinaryPrimitives.ReadUInt16LittleEndian),
            TypeCode.Int32 => Read(bytes, sizeof(int), BinaryPrimitives.ReadInt32LittleEndian),
            TypeCode.UInt32 => Read(bytes, sizeof(uint), BinaryPrimitives.ReadUInt32LittleEndian),
            TypeCode.Int64 => Read(bytes, sizeof(long), BinaryPrimitives.ReadInt64LittleEndian),
            TypeCode.UInt64 => Read(bytes, sizeof(ulong), BinaryPrimitives.ReadUInt64LittleEndian),
            TypeCode.Single => BitConverterCompatibility.Int32BitsToSingle(
                Read(bytes, sizeof(float), BinaryPrimitives.ReadInt32LittleEndian)),
            TypeCode.Double => BitConverterCompatibility.Int64BitsToDouble(
                Read(bytes, sizeof(double), BinaryPrimitives.ReadInt64LittleEndian)),
            TypeCode.String => DecodeString(bytes),
            _ => throw new NotSupportedException(
                $"Simulator scalar type '{type.FullName}' is not supported. Use GetTagBytes for structured values."),
        };
        return (T)value;
    }

    /// <summary>Allocates bytes and applies a span writer.</summary>
    /// <param name="length">The byte count.</param>
    /// <param name="writer">The writer.</param>
    /// <returns>The encoded bytes.</returns>
    private static byte[] Write(int length, SpanWriter writer)
    {
        var bytes = new byte[length];
        writer(bytes);
        return bytes;
    }

    /// <summary>Checks length before applying a span reader.</summary>
    /// <typeparam name="T">The decoded type.</typeparam>
    /// <param name="bytes">The source bytes.</param>
    /// <param name="minimumLength">The required byte count.</param>
    /// <param name="reader">The reader.</param>
    /// <returns>The decoded value.</returns>
    private static T Read<T>(byte[] bytes, int minimumLength, SpanReader<T> reader)
    {
        if (bytes.Length < minimumLength)
        {
            throw new InvalidOperationException(
                $"Simulator tag contains {bytes.Length} bytes; {minimumLength} are required.");
        }

        return reader(bytes);
    }

    /// <summary>Encodes a Logix string.</summary>
    /// <param name="value">The string.</param>
    /// <returns>The fixed-size string buffer.</returns>
    private static byte[] EncodeString(string value)
    {
        var payload = Encoding.UTF8.GetBytes(value);
        if (payload.Length > MaximumStringLength)
        {
            throw new ArgumentOutOfRangeException(
                nameof(value),
                $"Encoded string length must be at most {MaximumStringLength} bytes.");
        }

        var bytes = new byte[DataLength.STRING];
        BinaryPrimitives.WriteInt32LittleEndian(bytes, payload.Length);
        payload.CopyTo(bytes, sizeof(int));
        return bytes;
    }

    /// <summary>Decodes a Logix string.</summary>
    /// <param name="bytes">The string buffer.</param>
    /// <returns>The string.</returns>
    private static string DecodeString(byte[] bytes)
    {
        var length = Read(bytes, sizeof(int), BinaryPrimitives.ReadInt32LittleEndian);
        if (length is < 0 or > MaximumStringLength || bytes.Length < sizeof(int) + length)
        {
            throw new InvalidOperationException("Simulator tag contains an invalid Logix string.");
        }

        return Encoding.UTF8.GetString(bytes, sizeof(int), length);
    }
}
