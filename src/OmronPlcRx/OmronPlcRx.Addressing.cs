// Copyright (c) 2019-2026 Chris Pulman and contributors. All rights reserved.
// Chris Pulman and contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System;
#if REACTIVE_SHIM
using OmronPlcRx.Reactive.Enums;
#else
using OmronPlcRx.Enums;
#endif

#if REACTIVE_SHIM
namespace OmronPlcRx.Reactive;
#else
namespace OmronPlcRx;
#endif

/// <summary>Contains PLC tag address parsing helpers.</summary>
public sealed partial class OmronPlcRx
{
    /// <summary>Converts a boxed tag value.</summary>
    /// <typeparam name="T">Target value type.</typeparam>
    /// <param name="value">Value to convert.</param>
    /// <returns>The converted value.</returns>
    private static object? ConvertTo<T>(object value) =>
        value is T typed ? typed : (T)Convert.ChangeType(value, typeof(T));

    /// <summary>Throws when a string argument is null, empty, or whitespace.</summary>
    /// <param name="value">The value to validate.</param>
    /// <param name="paramName">The parameter name.</param>
    private static void ThrowIfNullOrWhiteSpace(string? value, string paramName)
    {
        switch (value)
        {
            case null:
                throw new ArgumentNullException(paramName);
            case var text when text.Trim().Length == 0:
                throw new ArgumentException("The value cannot be empty or whitespace.", paramName);
        }
    }

    /// <summary>Parses a PLC address.</summary>
    /// <param name="address">Address to parse.</param>
    /// <returns>The memory area, address, and optional bit index.</returns>
    private static (string Area, ushort Address, byte? BitIndex) ParseAddress(string address)
    {
        var baseForParse = RemoveLengthSpecifier(address);
        var (basePart, bitPart) = SplitBitPart(baseForParse);
        var bitIndex = ParseBitIndex(bitPart, address);
        var firstDigit = FindFirstDigit(basePart);
        if (firstDigit < 0)
        {
            throw new FormatException($"No numeric portion in address '{address}'");
        }

        var area = basePart[..firstDigit].ToUpperInvariant();
        var numberPart = basePart.Remove(0, firstDigit);
        if (!ushort.TryParse(numberPart, out var parsedAddress))
        {
            throw new FormatException($"Invalid numeric address in '{address}'");
        }

        return (area, parsedAddress, bitIndex);
    }

    /// <summary>Removes a string length specifier from an address.</summary>
    /// <param name="address">The address to parse.</param>
    /// <returns>The address without a length specifier.</returns>
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

    /// <summary>Splits an address into base and bit-index parts.</summary>
    /// <param name="address">The address to parse.</param>
    /// <returns>The base and bit-index parts.</returns>
    private static (string BasePart, string? BitPart) SplitBitPart(string address)
    {
        var dotIndex = address.IndexOf('.');
        return dotIndex >= 0
            ? (address[..dotIndex], address[(dotIndex + 1)..])
            : (address, null);
    }

    /// <summary>Parses an optional bit index.</summary>
    /// <param name="bitPart">The bit-index text.</param>
    /// <param name="address">The source address.</param>
    /// <returns>The bit index, if present.</returns>
    private static byte? ParseBitIndex(string? bitPart, string address)
    {
        if (bitPart is null)
        {
            return null;
        }

        if (byte.TryParse(bitPart, out var bitIndex)
            && bitIndex <= ProtocolConstants.Fifteen)
        {
            return bitIndex;
        }

        throw new FormatException($"Invalid bit index in address '{address}'");
    }

    /// <summary>Finds the first digit in a string.</summary>
    /// <param name="value">The value to scan.</param>
    /// <returns>The zero-based digit index, or -1.</returns>
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

    /// <summary>Extracts an optional string length from an address.</summary>
    /// <param name="address">Address to parse.</param>
    /// <returns>The base address and string length.</returns>
    private static (string BaseAddress, int Length) ExtractStringMeta(string address)
    {
        const int DefaultLength = 16;
        var bracketIndex = address.IndexOf('[');
        if (bracketIndex < 0)
        {
            return (address, DefaultLength);
        }

        var end = address.IndexOf(']', bracketIndex + 1);
        if (end <= bracketIndex)
        {
            return (address, DefaultLength);
        }

        var lengthPart = address[(bracketIndex + 1)..end];
        return int.TryParse(lengthPart, out var length) && length > 0
            ? (address.Remove(bracketIndex, end - bracketIndex + 1), length)
            : (address, DefaultLength);
    }

    /// <summary>Converts an address area to a bit memory type.</summary>
    /// <param name="area">Address area.</param>
    /// <returns>The bit memory type.</returns>
    private static MemoryBitDataType ToBitType(string area) =>
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

    /// <summary>Converts an address area to a word memory type.</summary>
    /// <param name="area">Address area.</param>
    /// <returns>The word memory type.</returns>
    private static MemoryWordDataType ToWordType(string area) =>
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
}
