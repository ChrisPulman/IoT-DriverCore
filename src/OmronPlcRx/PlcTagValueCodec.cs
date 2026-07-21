// Copyright (c) 2019-2026 Chris Pulman and contributors. All rights reserved.
// Chris Pulman and contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
#if REACTIVE_SHIM
using OmronPlcRx.Reactive.Core;
using OmronPlcRx.Reactive.Core.Converters;
using OmronPlcRx.Reactive.Core.Types;
using OmronPlcRx.Reactive.Enums;
#else
using OmronPlcRx.Core;
using OmronPlcRx.Core.Converters;
using OmronPlcRx.Core.Types;
using OmronPlcRx.Enums;
#endif

#if REACTIVE_SHIM
namespace OmronPlcRx.Reactive;
#else
namespace OmronPlcRx;
#endif

/// <summary>Converts tag values to and from PLC word representations.</summary>
internal static class PlcTagValueCodec
{
    /// <summary>Stores read word counts by supported tag type.</summary>
    private static readonly Dictionary<Type, int> ReadWordCounts = new()
    {
        [typeof(short)] = 1,
        [typeof(byte)] = 1,
        [typeof(ushort)] = 1,
        [typeof(Bcd16)] = 1,
        [typeof(BcdU16)] = 1,
        [typeof(int)] = 2,
        [typeof(uint)] = 2,
        [typeof(float)] = 2,
        [typeof(Bcd32)] = 2,
        [typeof(BcdU32)] = 2,
        [typeof(double)] = 4,
    };

    /// <summary>Throws when a string tag uses bit indexing.</summary>
    /// <param name="bitIndex">The optional bit index.</param>
    internal static void ThrowIfBitIndexedString(byte? bitIndex)
    {
        if (bitIndex is null)
        {
            return;
        }

        throw new NotSupportedException("Bit indexing not supported for string types.");
    }

    /// <summary>Gets PLC words for a string value.</summary>
    /// <param name="value">The string value.</param>
    /// <param name="length">The string length.</param>
    /// <returns>The PLC words.</returns>
    internal static short[] GetStringWords(object value, int length)
    {
        var bytes = GetSizedStringBytes(value, length);
        var wordCount = bytes.Length / ProtocolConstants.Two;
        var words = new short[wordCount];
        for (var i = 0; i < wordCount; i++)
        {
            var highByte = bytes[i * ProtocolConstants.Two];
            var lowByte = bytes[(i * ProtocolConstants.Two) + 1];
            words[i] = (short)((highByte << 8) | lowByte);
        }

        return words;
    }

    /// <summary>Attempts to convert a value to one PLC word.</summary>
    /// <param name="type">The value type.</param>
    /// <param name="value">The value to convert.</param>
    /// <param name="word">The PLC word.</param>
    /// <returns>A value indicating whether conversion succeeded.</returns>
    internal static bool TryGetSingleWord(Type type, object value, out short word)
    {
        if (type == typeof(byte))
        {
            word = Convert.ToByte(value);
            return true;
        }

        if (type == typeof(ushort))
        {
            word = unchecked((short)Convert.ToUInt16(value));
            return true;
        }

        if (type == typeof(short))
        {
            word = Convert.ToInt16(value);
            return true;
        }

        if (type == typeof(Bcd16))
        {
            word = BCDConverter.GetBCDWord(((Bcd16)value).Value);
            return true;
        }

        if (type == typeof(BcdU16))
        {
            word = BCDConverter.GetBCDWord(((BcdU16)value).Value);
            return true;
        }

        word = 0;
        return false;
    }

    /// <summary>Attempts to convert a value to PLC words.</summary>
    /// <param name="type">The value type.</param>
    /// <param name="value">The value to convert.</param>
    /// <param name="words">The PLC words.</param>
    /// <returns>A value indicating whether conversion succeeded.</returns>
    internal static bool TryGetWordArray(Type type, object value, out short[] words)
    {
        if (type == typeof(int))
        {
            words = GetInt32Words(Convert.ToInt32(value));
            return true;
        }

        if (type == typeof(uint))
        {
            words = GetUInt32Words(Convert.ToUInt32(value));
            return true;
        }

        if (type == typeof(float))
        {
            words = GetSingleWords(Convert.ToSingle(value));
            return true;
        }

        if (type == typeof(double))
        {
            words = GetDoubleWords(Convert.ToDouble(value));
            return true;
        }

        if (type == typeof(Bcd32))
        {
            words = BCDConverter.GetBCDWords(((Bcd32)value).Value);
            return true;
        }

        if (type == typeof(BcdU32))
        {
            words = BCDConverter.GetBCDWords(((BcdU32)value).Value);
            return true;
        }

        words = [];
        return false;
    }

    /// <summary>Reads a Boolean value from the PLC.</summary>
    /// <param name="plc">The PLC connection.</param>
    /// <param name="wordType">The word memory type.</param>
    /// <param name="bitType">The bit memory type.</param>
    /// <param name="addr">The memory address.</param>
    /// <param name="bitIndex">The optional bit index.</param>
    /// <param name="ct">The cancellation token.</param>
    /// <returns>The Boolean value.</returns>
    internal static async Task<object> ReadBooleanValueAsync(
        OmronPLCConnection plc,
        MemoryWordDataType wordType,
        MemoryBitDataType bitType,
        ushort addr,
        byte? bitIndex,
        CancellationToken ct)
    {
        if (bitIndex is null)
        {
            var word = await plc.ReadWordAsync(addr, wordType, ct).ConfigureAwait(false);
            return word.Values[0] != 0;
        }

        var bits = await plc.ReadBitsAsync(addr, bitIndex.Value, 1, bitType, ct).ConfigureAwait(false);
        return bits.Values[0];
    }

    /// <summary>Gets the word count needed to read a type.</summary>
    /// <param name="type">The value type.</param>
    /// <returns>The word count.</returns>
    internal static int GetReadWordCount(Type type) =>
        ReadWordCounts.TryGetValue(type, out var count) ? count : 0;

    /// <summary>Converts PLC words to a typed value.</summary>
    /// <param name="type">The target type.</param>
    /// <param name="values">The PLC word values.</param>
    /// <returns>The converted value.</returns>
    internal static object ConvertReadWords(Type type, short[] values)
    {
        if (TryConvertSingleReadWord(type, values, out var singleValue))
        {
            return singleValue;
        }

        if (TryConvertMultipleReadWords(type, values, out var multipleValue))
        {
            return multipleValue;
        }

        throw new NotSupportedException($"Tag type '{type.Name}' not supported.");
    }

    /// <summary>Gets a string from PLC words.</summary>
    /// <param name="values">The PLC word values.</param>
    /// <param name="length">The string length.</param>
    /// <param name="wordCount">The word count.</param>
    /// <returns>The string value.</returns>
    internal static string GetStringFromWords(short[] values, int length, int wordCount)
    {
        var bytes = new List<byte>(wordCount * ProtocolConstants.Two);
        for (var i = 0; i < wordCount; i++)
        {
            var word = (ushort)values[i];
            bytes.Add((byte)(word >> 8));
            bytes.Add((byte)(word & 0xFF));
        }

        TrimStringBytes(bytes, length);
        var nullIndex = bytes.IndexOf(0);
        var bytesToDecode = nullIndex >= 0 ? bytes.GetRange(0, nullIndex) : bytes;
        return Encoding.ASCII.GetString(bytesToDecode.ToArray());
    }

    /// <summary>Gets an even-length byte array for a fixed-length string.</summary>
    /// <param name="value">The string value.</param>
    /// <param name="length">The string length.</param>
    /// <returns>The string bytes.</returns>
    private static byte[] GetSizedStringBytes(object value, int length)
    {
        var str = Convert.ToString(value) ?? string.Empty;
        var bytes = Encoding.ASCII.GetBytes(str);
        Array.Resize(ref bytes, length);
        if (bytes.Length % ProtocolConstants.Two != 0)
        {
            Array.Resize(ref bytes, bytes.Length + 1);
        }

        return bytes;
    }

    /// <summary>Gets PLC words for a signed 32-bit value.</summary>
    /// <param name="value">The value.</param>
    /// <returns>The PLC words.</returns>
    private static short[] GetInt32Words(int value)
    {
        var highWord = (ushort)((value >> 16) & 0xFFFF);
        var lowWord = (ushort)(value & 0xFFFF);
        return [unchecked((short)lowWord), unchecked((short)highWord)];
    }

    /// <summary>Gets PLC words for an unsigned 32-bit value.</summary>
    /// <param name="value">The value.</param>
    /// <returns>The PLC words.</returns>
    private static short[] GetUInt32Words(uint value) => GetInt32Words(unchecked((int)value));

    /// <summary>Gets PLC words for a single-precision value.</summary>
    /// <param name="value">The value.</param>
    /// <returns>The PLC words.</returns>
    private static short[] GetSingleWords(float value)
    {
        var bytes = GetBigEndianBytes(BitConverter.GetBytes(value));
        var highWord = (ushort)((bytes[0] << 8) | bytes[1]);
        var lowWord = (ushort)((bytes[2] << 8) | bytes[3]);
        return [unchecked((short)lowWord), unchecked((short)highWord)];
    }

    /// <summary>Gets PLC words for a double-precision value.</summary>
    /// <param name="value">The value.</param>
    /// <returns>The PLC words.</returns>
    private static short[] GetDoubleWords(double value)
    {
        var bytes = GetBigEndianBytes(BitConverter.GetBytes(value));
        var highOrderWord0 = (ushort)((bytes[0] << 8) | bytes[1]);
        var highOrderWord1 = (ushort)((bytes[2] << 8) | bytes[3]);
        var lowOrderWord0 = (ushort)((bytes[4] << 8) | bytes[5]);
        var lowOrderWord1 = (ushort)((bytes[6] << 8) | bytes[7]);
        return
        [
            unchecked((short)lowOrderWord0),
            unchecked((short)lowOrderWord1),
            unchecked((short)highOrderWord0),
            unchecked((short)highOrderWord1),
        ];
    }

    /// <summary>Gets bytes in big-endian order.</summary>
    /// <param name="bytes">The source bytes.</param>
    /// <returns>The big-endian bytes.</returns>
    private static byte[] GetBigEndianBytes(byte[] bytes)
    {
        if (BitConverter.IsLittleEndian)
        {
            Array.Reverse(bytes);
        }

        return bytes;
    }

    /// <summary>Attempts to convert a single PLC word.</summary>
    /// <param name="type">The target type.</param>
    /// <param name="values">The PLC word values.</param>
    /// <param name="value">The converted value.</param>
    /// <returns>A value indicating whether conversion succeeded.</returns>
    private static bool TryConvertSingleReadWord(Type type, short[] values, out object value)
    {
        if (type == typeof(short))
        {
            value = values[0];
            return true;
        }

        if (type == typeof(byte))
        {
            value = (byte)(values[0] & 0xFF);
            return true;
        }

        if (type == typeof(ushort))
        {
            value = (ushort)values[0];
            return true;
        }

        if (type == typeof(Bcd16))
        {
            value = new Bcd16(BCDConverter.ToInt16(values[0]));
            return true;
        }

        if (type == typeof(BcdU16))
        {
            value = new BcdU16(BCDConverter.ToUInt16(values[0]));
            return true;
        }

        value = default!;
        return false;
    }

    /// <summary>Attempts to convert multiple PLC words.</summary>
    /// <param name="type">The target type.</param>
    /// <param name="values">The PLC word values.</param>
    /// <param name="value">The converted value.</param>
    /// <returns>A value indicating whether conversion succeeded.</returns>
    private static bool TryConvertMultipleReadWords(Type type, short[] values, out object value)
    {
        if (type == typeof(int))
        {
            value = GetInt32FromWords(values);
            return true;
        }

        if (type == typeof(uint))
        {
            value = GetUInt32FromWords(values);
            return true;
        }

        if (type == typeof(float))
        {
            value = GetSingleFromWords(values);
            return true;
        }

        if (type == typeof(double))
        {
            value = GetDoubleFromWords(values);
            return true;
        }

        if (type == typeof(Bcd32))
        {
            value = new Bcd32(BCDConverter.ToInt32(values[0], values[1]));
            return true;
        }

        if (type == typeof(BcdU32))
        {
            value = new BcdU32(BCDConverter.ToUInt32(values[0], values[1]));
            return true;
        }

        value = default!;
        return false;
    }

    /// <summary>Trims fixed-length string bytes.</summary>
    /// <param name="bytes">The bytes to trim.</param>
    /// <param name="length">The string length.</param>
    private static void TrimStringBytes(List<byte> bytes, int length)
    {
        if (bytes.Count <= length)
        {
            return;
        }

        bytes.RemoveRange(length, bytes.Count - length);
    }

    /// <summary>Gets a signed 32-bit value from PLC words.</summary>
    /// <param name="values">The PLC word values.</param>
    /// <returns>The signed 32-bit value.</returns>
    private static int GetInt32FromWords(short[] values)
    {
        var lowWord = (ushort)values[0];
        var highWord = (ushort)values[1];
        var composite = ((uint)highWord << 16) | lowWord;
        return unchecked((int)composite);
    }

    /// <summary>Gets an unsigned 32-bit value from PLC words.</summary>
    /// <param name="values">The PLC word values.</param>
    /// <returns>The unsigned 32-bit value.</returns>
    private static uint GetUInt32FromWords(short[] values)
    {
        var lowWord = (uint)(ushort)values[0];
        var highWord = (uint)(ushort)values[1];
        return (highWord << 16) | lowWord;
    }

    /// <summary>Gets a single-precision value from PLC words.</summary>
    /// <param name="values">The PLC word values.</param>
    /// <returns>The single-precision value.</returns>
    private static float GetSingleFromWords(short[] values)
    {
        var lowWord = (ushort)values[0];
        var highWord = (ushort)values[1];
        var bytes = GetLittleEndianHostBytes([
            (byte)(highWord >> 8), (byte)(highWord & 0xFF), (byte)(lowWord >> 8), (byte)(lowWord & 0xFF),
        ]);
        return BitConverter.ToSingle(bytes, 0);
    }

    /// <summary>Gets a double-precision value from PLC words.</summary>
    /// <param name="values">The PLC word values.</param>
    /// <returns>The double-precision value.</returns>
    private static double GetDoubleFromWords(short[] values)
    {
        var highOrderWord0 = (ushort)values[2];
        var highOrderWord1 = (ushort)values[3];
        var lowOrderWord0 = (ushort)values[0];
        var lowOrderWord1 = (ushort)values[1];
        var bytes = GetLittleEndianHostBytes([
            (byte)(highOrderWord0 >> 8), (byte)(highOrderWord0 & 0xFF),
            (byte)(highOrderWord1 >> 8), (byte)(highOrderWord1 & 0xFF),
            (byte)(lowOrderWord0 >> 8), (byte)(lowOrderWord0 & 0xFF),
            (byte)(lowOrderWord1 >> 8), (byte)(lowOrderWord1 & 0xFF),
        ]);
        return BitConverter.ToDouble(bytes, 0);
    }

    /// <summary>Gets bytes in the current host endian order.</summary>
    /// <param name="bytes">The source bytes.</param>
    /// <returns>The host-ordered bytes.</returns>
    private static byte[] GetLittleEndianHostBytes(byte[] bytes) => GetBigEndianBytes(bytes);
}
