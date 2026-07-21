// Copyright (c) 2019-2026 Chris Pulman and contributors. All rights reserved.
// Chris Pulman and contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

#if REACTIVE_SHIM
namespace ModbusRx.Reactive.LogicalTags;
#else
namespace ModbusRx.LogicalTags;
#endif

/// <summary>Converts supported CLR values to and from raw Modbus points.</summary>
internal static class ModbusTagCodec
{
    /// <summary>The number of bytes in one Modbus register.</summary>
    private const int BytesPerRegister = 2;

    /// <summary>The number of registers occupied by a 32-bit value.</summary>
    private const int RegistersPer32BitValue = 2;

    /// <summary>The number of registers occupied by a 64-bit value.</summary>
    private const int RegistersPer64BitValue = 4;

    /// <summary>The number of bytes in a 32-bit value.</summary>
    private const int BytesPer32BitValue = 4;

    /// <summary>The final byte offset in a 32-bit value.</summary>
    private const int Final32BitByteOffset = 3;

    /// <summary>Validates a CLR type and point count for a data area.</summary>
    /// <param name="area">The Modbus data area.</param>
    /// <param name="count">The point count.</param>
    /// <param name="type">The CLR type.</param>
    internal static void ValidateType(ModbusDataArea area, ushort count, Type type)
    {
        var elementType = GetElementType(type);
        var pointsPerValue = GetPointsPerValue(area, elementType);
        if (!type.IsArray && count != pointsPerValue)
        {
            throw new ArgumentException(
                $"CLR type '{type}' requires exactly {pointsPerValue} Modbus point(s).",
                nameof(count));
        }

        _ = count % pointsPerValue == 0
            ? true
            : throw new ArgumentException(
                "The point count must contain a whole number of CLR values.",
                nameof(count));
    }

    /// <summary>Decodes one tag value from a raw data-area response.</summary>
    /// <param name="tag">The tag definition.</param>
    /// <param name="data">The raw response.</param>
    /// <param name="offset">The point offset.</param>
    /// <returns>The decoded CLR value.</returns>
    internal static object Decode(ModbusLogicalTag tag, Array data, int offset)
    {
        var elementType = GetElementType(tag.ClrDataType);
        var pointsPerValue = GetPointsPerValue(tag.DataArea, elementType);
        if (!tag.ClrDataType.IsArray)
        {
            return DecodeElement(tag.DataArea, elementType, tag.ByteOrder, data, offset);
        }

        var result = Array.CreateInstance(elementType, tag.Count / pointsPerValue);
        for (var index = 0; index < result.Length; index++)
        {
            result.SetValue(
                DecodeElement(tag.DataArea, elementType, tag.ByteOrder, data, offset + (index * pointsPerValue)),
                index);
        }

        return result;
    }

    /// <summary>Encodes one tag value for a raw Modbus write.</summary>
    /// <param name="tag">The tag definition.</param>
    /// <param name="value">The CLR value.</param>
    /// <returns>The encoded raw points.</returns>
    internal static Array Encode(ModbusLogicalTag tag, object? value)
    {
        ValidateValue(tag, value);
        var nonNullValue = value ?? throw new ArgumentNullException(nameof(value));
        var elementType = GetElementType(tag.ClrDataType);
        var pointsPerValue = GetPointsPerValue(tag.DataArea, elementType);
        if (tag.DataArea is ModbusDataArea.Coil or ModbusDataArea.DiscreteInput)
        {
            return tag.ClrDataType == typeof(bool)
                ? [(bool)nonNullValue]
                : ((bool[])nonNullValue).ToArray();
        }

        var registers = new ushort[tag.Count];
        if (tag.ClrDataType.IsArray)
        {
            var values = (Array)nonNullValue;
            for (var index = 0; index < values.Length; index++)
            {
                var element = values.GetValue(index)
                    ?? throw new ArgumentException("Array values cannot contain null elements.", nameof(value));
                EncodeElement(elementType, element, tag.ByteOrder, registers, index * pointsPerValue);
            }
        }
        else
        {
            EncodeElement(elementType, nonNullValue, tag.ByteOrder, registers, 0);
        }

        return registers;
    }

    /// <summary>Validates a value before encoding.</summary>
    /// <param name="tag">The tag definition.</param>
    /// <param name="value">The candidate CLR value.</param>
    private static void ValidateValue(ModbusLogicalTag tag, object? value)
    {
        _ = value is not null && tag.ClrDataType.IsInstanceOfType(value)
            ? true
            : throw new ArgumentException(
                $"Tag '{tag.Name}' requires a value of CLR type '{tag.ClrDataType}'.",
                nameof(value));
        var elementType = GetElementType(tag.ClrDataType);
        var pointsPerValue = GetPointsPerValue(tag.DataArea, elementType);
        var valueCount = tag.ClrDataType.IsArray ? ((Array)value).Length : 1;
        _ = valueCount * pointsPerValue == tag.Count
            ? true
            : throw new ArgumentException(
                $"Tag '{tag.Name}' requires exactly {tag.Count / pointsPerValue} value(s).",
                nameof(value));
    }

    /// <summary>Gets the scalar element type represented by a CLR type.</summary>
    /// <param name="type">The CLR type.</param>
    /// <returns>The scalar type.</returns>
    private static Type GetElementType(Type type) => type.IsArray
        ? type.GetElementType() ?? throw new ArgumentException("Array types must define an element type.", nameof(type))
        : type;

    /// <summary>Gets the number of Modbus points occupied by one CLR value.</summary>
    /// <param name="area">The Modbus data area.</param>
    /// <param name="type">The scalar CLR type.</param>
    /// <returns>The point count.</returns>
    private static int GetPointsPerValue(ModbusDataArea area, Type type)
    {
        if (area is ModbusDataArea.Coil or ModbusDataArea.DiscreteInput)
        {
            return type == typeof(bool)
                ? 1
                : throw new NotSupportedException("Modbus bit areas support only Boolean values.");
        }

        if (type == typeof(ushort) || type == typeof(short))
        {
            return 1;
        }

        if (type == typeof(uint) || type == typeof(int) || type == typeof(float))
        {
            return RegistersPer32BitValue;
        }

        if (type == typeof(double))
        {
            return RegistersPer64BitValue;
        }

        throw new NotSupportedException($"CLR type '{type}' is not supported by Modbus register areas.");
    }

    /// <summary>Decodes one scalar CLR value.</summary>
    /// <param name="area">The Modbus data area.</param>
    /// <param name="type">The scalar CLR type.</param>
    /// <param name="order">The byte order.</param>
    /// <param name="data">The raw response.</param>
    /// <param name="offset">The point offset.</param>
    /// <returns>The decoded scalar value.</returns>
    private static object DecodeElement(ModbusDataArea area, Type type, ModbusByteOrder order, Array data, int offset)
    {
        if (area is ModbusDataArea.Coil or ModbusDataArea.DiscreteInput)
        {
            return ((bool[])data)[offset];
        }

        var points = GetPointsPerValue(area, type);
        var bytes = new byte[points * BytesPerRegister];
        var registers = (ushort[])data;
        for (var index = 0; index < points; index++)
        {
            var register = registers[offset + index];
            bytes[index * BytesPerRegister] = (byte)(register >> 8);
            bytes[(index * BytesPerRegister) + 1] = (byte)register;
        }

        Transform(bytes, order);
        if (type == typeof(ushort))
        {
            return (ushort)((bytes[0] << 8) | bytes[1]);
        }

        if (type == typeof(short))
        {
            return unchecked((short)((bytes[0] << 8) | bytes[1]));
        }

        if (type == typeof(uint))
        {
            return ReadUInt32(bytes);
        }

        if (type == typeof(int))
        {
            return unchecked((int)ReadUInt32(bytes));
        }

        if (BitConverter.IsLittleEndian)
        {
            Array.Reverse(bytes);
        }

        return type == typeof(float)
            ? BitConverter.ToSingle(bytes, 0)
            : BitConverter.ToDouble(bytes, 0);
    }

    /// <summary>Encodes one scalar register value.</summary>
    /// <param name="type">The scalar CLR type.</param>
    /// <param name="value">The scalar value.</param>
    /// <param name="order">The byte order.</param>
    /// <param name="registers">The destination registers.</param>
    /// <param name="offset">The register offset.</param>
    private static void EncodeElement(Type type, object value, ModbusByteOrder order, ushort[] registers, int offset)
    {
        byte[] bytes;
        if (type == typeof(ushort))
        {
            var typed = (ushort)value;
            bytes = [(byte)(typed >> 8), (byte)typed];
        }
        else if (type == typeof(short))
        {
            var typed = unchecked((ushort)(short)value);
            bytes = [(byte)(typed >> 8), (byte)typed];
        }
        else if (type == typeof(uint))
        {
            bytes = GetBytes((uint)value);
        }
        else if (type == typeof(int))
        {
            bytes = GetBytes(unchecked((uint)(int)value));
        }
        else
        {
            bytes = type == typeof(float) ? BitConverter.GetBytes((float)value) : BitConverter.GetBytes((double)value);
            if (BitConverter.IsLittleEndian)
            {
                Array.Reverse(bytes);
            }
        }

        Transform(bytes, order);
        for (var index = 0; index < bytes.Length / BytesPerRegister; index++)
        {
            registers[offset + index] = (ushort)(
                (bytes[index * BytesPerRegister] << 8) |
                bytes[(index * BytesPerRegister) + 1]);
        }
    }

    /// <summary>Reads a big-endian UInt32.</summary>
    /// <param name="bytes">The source bytes.</param>
    /// <returns>The decoded value.</returns>
    private static uint ReadUInt32(byte[] bytes) =>
        ((uint)bytes[0] << 24) | ((uint)bytes[1] << 16) | ((uint)bytes[2] << 8) | bytes[3];

    /// <summary>Gets the big-endian bytes for a UInt32.</summary>
    /// <param name="value">The source value.</param>
    /// <returns>The encoded bytes.</returns>
    private static byte[] GetBytes(uint value) =>
        [(byte)(value >> 24), (byte)(value >> 16), (byte)(value >> 8), (byte)value];

    /// <summary>Applies the configured reversible byte and word transform.</summary>
    /// <param name="bytes">The bytes to transform.</param>
    /// <param name="order">The configured order.</param>
    private static void Transform(byte[] bytes, ModbusByteOrder order)
    {
        switch (order)
        {
            case ModbusByteOrder.BigEndian:
                {
                    return;
                }

            case ModbusByteOrder.LittleEndian:
                {
                    Array.Reverse(bytes);
                    return;
                }

            case ModbusByteOrder.BigEndianWordSwap:
                {
                    for (var index = 0; index + Final32BitByteOffset < bytes.Length; index += BytesPer32BitValue)
                    {
                        (bytes[index], bytes[index + BytesPerRegister]) =
                            (bytes[index + BytesPerRegister], bytes[index]);
                        (bytes[index + 1], bytes[index + Final32BitByteOffset]) =
                            (bytes[index + Final32BitByteOffset], bytes[index + 1]);
                    }

                    return;
                }

            case ModbusByteOrder.LittleEndianWordSwap:
                {
                    for (var index = 0; index + 1 < bytes.Length; index += BytesPerRegister)
                    {
                        (bytes[index], bytes[index + 1]) = (bytes[index + 1], bytes[index]);
                    }

                    return;
                }

            default:
                throw new ArgumentOutOfRangeException(nameof(order));
        }
    }
}
