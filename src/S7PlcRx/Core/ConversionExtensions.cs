// Copyright (c) 2019-2026 Chris Pulman and contributors. All rights reserved.
// Chris Pulman and contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Globalization;

#if REACTIVE_SHIM
namespace S7PlcRx.Reactive.PlcTypes;
#else
namespace S7PlcRx.PlcTypes;
#endif

/// <summary>Provides binary and numeric conversion helpers.</summary>
internal static class ConversionExtensions
{
    /// <summary>Defines the highest zero-based bit index in a byte.</summary>
    private const int MaximumBitIndex = 7;

    /// <summary>Defines the radix used for binary conversion.</summary>
    private const int BinaryRadix = 2;

    /// <summary>Determines whether the specified bit is set in a byte.</summary>
    /// <param name="data">The byte to inspect.</param>
    /// <param name="bitPosition">The zero-based position of the bit to check.</param>
    /// <returns>Whether the specified bit is set.</returns>
    internal static bool SelectBit(byte data, int bitPosition)
    {
        var mask = 1 << bitPosition;
        return (data & mask) != 0;
    }

    /// <summary>Sets a bit in a byte.</summary>
    /// <param name="data">The byte to modify.</param>
    /// <param name="index">The zero-based bit index.</param>
    /// <param name="value">The bit value.</param>
    internal static void SetBit(ref byte data, int index, bool value)
    {
        if ((uint)index > MaximumBitIndex)
        {
            return;
        }

        if (value)
        {
            data |= (byte)(1 << index);
            return;
        }

        data &= (byte)~(1 << index);
    }

    /// <summary>Reinterprets a single-precision value as an unsigned integer.</summary>
    /// <param name="input">The value to reinterpret.</param>
    /// <returns>The corresponding unsigned integer.</returns>
    internal static uint ConvertToUInt(float input) => DWord.FromByteArray(LReal.ToByteArray(input));

    /// <summary>Converts a signed integer to its unsigned representation.</summary>
    /// <param name="input">The signed value.</param>
    /// <returns>The corresponding unsigned value.</returns>
    internal static uint ConvertToUInt(int input) => uint.Parse(input.ToString("X"), NumberStyles.HexNumber);

    /// <summary>Converts a signed short to its unsigned representation.</summary>
    /// <param name="input">The signed value.</param>
    /// <returns>The corresponding unsigned value.</returns>
    internal static ushort ConvertToUshort(short input) => ushort.Parse(input.ToString("X"), NumberStyles.HexNumber);

    /// <summary>Converts a signed short to a 16-character binary string.</summary>
    /// <param name="input">The signed value.</param>
    /// <returns>The binary representation.</returns>
    internal static string ValToBinString(short input)
    {
        var longValue = (long)input;
        var text = string.Empty;
        for (var bit = 15; bit >= 0; bit--)
        {
            text += (longValue & (long)Math.Pow(BinaryRadix, bit)) > 0 ? "1" : "0";
        }

        return text;
    }

    /// <summary>Converts a binary string to a signed integer.</summary>
    /// <param name="text">The binary text.</param>
    /// <returns>The converted integer.</returns>
    internal static int BinStringToInt32(string text)
    {
        var result = 0;
        foreach (var character in text)
        {
            result = (result << 1) | (character == '1' ? 1 : 0);
        }

        return result;
    }

    /// <summary>Converts an eight-character binary string to a byte.</summary>
    /// <param name="text">The binary text.</param>
    /// <returns>The converted byte, or <see langword="null"/> when the length is invalid.</returns>
    internal static byte? BinStringToByte(string text) =>
        text.Length == 8 ? (byte)BinStringToInt32(text) : null;

    /// <summary>Reinterprets an unsigned integer as a double-precision value.</summary>
    /// <param name="input">The unsigned value.</param>
    /// <returns>The corresponding floating-point value.</returns>
    internal static double ConvertToDouble(uint input) => LReal.FromByteArray(DWord.ToByteArray(input));

    /// <summary>Reinterprets an unsigned integer as a single-precision value.</summary>
    /// <param name="input">The unsigned value.</param>
    /// <returns>The corresponding floating-point value.</returns>
    internal static float ConvertToFloat(uint input) => Real.FromByteArray(DWord.ToByteArray(input));

    /// <summary>Converts an unsigned integer to its signed representation.</summary>
    /// <param name="input">The unsigned value.</param>
    /// <returns>The corresponding signed value.</returns>
    internal static int ConvertToInt(uint input) => int.Parse(input.ToString("X"), NumberStyles.HexNumber);

    /// <summary>Converts an unsigned short to its signed representation.</summary>
    /// <param name="input">The unsigned value.</param>
    /// <returns>The corresponding signed value.</returns>
    internal static short ConvertToShort(ushort input) => short.Parse(input.ToString("X"), NumberStyles.HexNumber);
}
