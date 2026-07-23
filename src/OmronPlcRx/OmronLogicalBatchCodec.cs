// Copyright (c) 2019-2026 Chris Pulman and contributors. All rights reserved.
// Chris Pulman and contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System;
#if REACTIVE_SHIM
using IoT.DriverCore.OmronPlcRx.Reactive.Enums;
#else
using IoT.DriverCore.OmronPlcRx.Enums;
#endif

#if REACTIVE_SHIM
namespace IoT.DriverCore.OmronPlcRx.Reactive;
#else
namespace IoT.DriverCore.OmronPlcRx;
#endif

/// <summary>Parses addresses and converts values for grouped FINS operations.</summary>
internal static class OmronLogicalBatchCodec
{
    /// <summary>Gets the read word count for a supported type.</summary>
    /// <param name="valueType">Runtime tag type.</param>
    /// <param name="stringLength">Configured string length.</param>
    /// <returns>The required word count.</returns>
    internal static int GetReadWordCount(Type valueType, int stringLength)
    {
        if (valueType == typeof(bool))
        {
            return 1;
        }

        if (valueType == typeof(string))
        {
            return (stringLength + 1) / ProtocolConstants.Two;
        }

        var count = PlcTagValueCodec.GetReadWordCount(valueType);
        return count > 0
            ? count
            : throw new NotSupportedException($"Tag type '{valueType.Name}' not supported.");
    }

    /// <summary>Converts a write value to its FINS word representation.</summary>
    /// <param name="item">Source item.</param>
    /// <param name="stringLength">Configured string length.</param>
    /// <returns>The word representation.</returns>
    internal static short[] GetWriteWords(OmronLogicalBatchItem item, int stringLength)
    {
        if (item.ValueType == typeof(bool))
        {
            return [(short)(Convert.ToBoolean(item.Value) ? 1 : 0)];
        }

        if (item.ValueType == typeof(string))
        {
            return PlcTagValueCodec.GetStringWords(item.Value!, stringLength);
        }

        if (PlcTagValueCodec.TryGetSingleWord(item.ValueType, item.Value!, out var word))
        {
            return [word];
        }

        if (PlcTagValueCodec.TryGetWordArray(item.ValueType, item.Value!, out var words))
        {
            return words;
        }

        throw new NotSupportedException($"Tag type '{item.ValueType.Name}' not supported.");
    }

    /// <summary>Decodes words read for one grouped item.</summary>
    /// <param name="valueType">Runtime tag type.</param>
    /// <param name="stringLength">Configured string length.</param>
    /// <param name="wordCount">Number of words occupied by the item.</param>
    /// <param name="words">Words belonging to the item.</param>
    /// <returns>The decoded value.</returns>
    internal static object DecodeWords(
        Type valueType,
        int stringLength,
        int wordCount,
        short[] words)
    {
        if (valueType == typeof(bool))
        {
            return words[0] != 0;
        }

        return valueType == typeof(string)
            ? PlcTagValueCodec.GetStringFromWords(words, stringLength, wordCount)
            : PlcTagValueCodec.ConvertReadWords(valueType, words);
    }

    /// <summary>Parses an Omron tag address.</summary>
    /// <param name="address">Address text.</param>
    /// <returns>The area, word address, and optional bit index.</returns>
    internal static (string Area, ushort Address, byte? BitIndex) ParseAddress(string address)
    {
        var baseForParse = RemoveLengthSpecifier(address);
        var dotIndex = baseForParse.IndexOf('.');
        var basePart = dotIndex >= 0 ? baseForParse[..dotIndex] : baseForParse;
        var bitPart = dotIndex >= 0 ? baseForParse[(dotIndex + 1)..] : null;
        var bitIndex = ParseBitIndex(bitPart, address);
        var firstDigit = FindFirstDigit(basePart);
        if (firstDigit < 0)
        {
            throw new FormatException($"No numeric portion in address '{address}'");
        }

        var area = basePart[..firstDigit].ToUpperInvariant();
        return ushort.TryParse(basePart[firstDigit..], out var parsedAddress)
            ? (area, parsedAddress, bitIndex)
            : throw new FormatException($"Invalid numeric address in '{address}'");
    }

    /// <summary>Extracts an optional fixed string length.</summary>
    /// <param name="address">Address text.</param>
    /// <returns>The base address and configured string length.</returns>
    internal static (string BaseAddress, int Length) ExtractStringMeta(string address)
    {
        const int defaultLength = 16;
        var bracketIndex = address.IndexOf('[');
        if (bracketIndex < 0)
        {
            return (address, defaultLength);
        }

        var end = address.IndexOf(']', bracketIndex + 1);
        if (end <= bracketIndex)
        {
            return (address, defaultLength);
        }

        var lengthPart = address[(bracketIndex + 1)..end];
        return int.TryParse(lengthPart, out var length) && length > 0
            ? (address.Remove(bracketIndex, end - bracketIndex + 1), length)
            : (address, defaultLength);
    }

    /// <summary>Converts an address-area alias to a FINS word data type.</summary>
    /// <param name="area">Address-area alias.</param>
    /// <returns>The FINS word data type.</returns>
    internal static MemoryWordDataType ToWordType(string area) =>
        area switch
        {
            "D" or "DM" => MemoryWordDataType.DataMemory,
            "C" or "CIO" => MemoryWordDataType.CommonIO,
            "W" => MemoryWordDataType.Work,
            "H" => MemoryWordDataType.Holding,
            "A" => MemoryWordDataType.Auxiliary,
            _ => throw new ArgumentOutOfRangeException(
                nameof(area),
                $"Unsupported word area '{area}'"),
        };

    /// <summary>Converts an address-area alias to a FINS bit data type.</summary>
    /// <param name="area">Address-area alias.</param>
    /// <returns>The FINS bit data type.</returns>
    internal static MemoryBitDataType ToBitType(string area) =>
        area switch
        {
            "D" or "DM" => MemoryBitDataType.DataMemory,
            "C" or "CIO" => MemoryBitDataType.CommonIO,
            "W" => MemoryBitDataType.Work,
            "H" => MemoryBitDataType.Holding,
            "A" => MemoryBitDataType.Auxiliary,
            _ => throw new ArgumentOutOfRangeException(
                nameof(area),
                $"Unsupported bit area '{area}'"),
        };

    /// <summary>Finds the first numeric character.</summary>
    /// <param name="value">Text to inspect.</param>
    /// <returns>The numeric character index, or -1.</returns>
    private static int FindFirstDigit(string value)
    {
        for (var index = 0; index < value.Length; index++)
        {
            if (char.IsDigit(value[index]))
            {
                return index;
            }
        }

        return -1;
    }

    /// <summary>Parses an optional bit index.</summary>
    /// <param name="bitPart">Optional bit-index text.</param>
    /// <param name="address">Source address.</param>
    /// <returns>The optional bit index.</returns>
    private static byte? ParseBitIndex(string? bitPart, string address)
    {
        if (bitPart is null)
        {
            return null;
        }

        return byte.TryParse(bitPart, out var parsedBit)
            && parsedBit <= ProtocolConstants.Fifteen
            ? parsedBit
            : throw new FormatException($"Invalid bit index in address '{address}'");
    }

    /// <summary>Removes a valid string length specifier.</summary>
    /// <param name="address">Address text.</param>
    /// <returns>The address without the length specifier.</returns>
    private static string RemoveLengthSpecifier(string address)
    {
        var bracketIndex = address.IndexOf('[');
        if (bracketIndex < 0)
        {
            return address;
        }

        var endBracket = address.IndexOf(']', bracketIndex + 1);
        return endBracket > bracketIndex
            ? address.Remove(bracketIndex, endBracket - bracketIndex + 1)
            : address;
    }
}
