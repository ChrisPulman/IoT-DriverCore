// Copyright (c) 2019-2026 Chris Pulman and contributors. All rights reserved.
// Chris Pulman and contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;

#if REACTIVE_SHIM
namespace IoT.DriverCore.OmronPlcRx.Reactive.Core.Converters;
#else
namespace IoT.DriverCore.OmronPlcRx.Core.Converters;
#endif

/// <summary>Converts between BCD encoded values and numeric values.</summary>
public static class BCDConverter
{
    /// <summary>Stores the BCD bytes array cannot be null message.</summary>
    private const string BcdBytesArrayCannotBeNullMessage = "The BCD Bytes Array cannot be null";

    /// <summary>Converts to byte.</summary>
    /// <param name="bcdByte">The BCD byte.</param>
    /// <returns>A byte representing the converted value.</returns>
    public static byte ToByte(byte bcdByte) => ConvertToBinaryBytes([bcdByte])[0];

    /// <summary>Converts to int16.</summary>
    /// <param name="bcdWord">The BCD word.</param>
    /// <returns>A short.</returns>
    public static short ToInt16(short bcdWord) => ToInt16(BitConverter.GetBytes(bcdWord));

    /// <summary>Converts to int16.</summary>
    /// <param name="bcdBytes">The BCD bytes.</param>
    /// <returns>A short.</returns>
    /// <exception cref="ArgumentOutOfRangeException">
    /// bcdBytes - The BCD Bytes Array Length must be '2' for conversion to Int16.
    /// </exception>
    public static short ToInt16(byte[] bcdBytes)
    {
        if (bcdBytes is null)
        {
            throw new ArgumentNullException(nameof(bcdBytes), BcdBytesArrayCannotBeNullMessage);
        }

        if (bcdBytes.Length != 2)
        {
            throw new ArgumentOutOfRangeException(
                nameof(bcdBytes),
                "The BCD Bytes Array Length must be '2' for conversion to Int16");
        }

        var converted = ConvertToBinaryBytes(bcdBytes);
        return BitConverter.ToInt16(converted, 0);
    }

    /// <summary>Converts to uint16.</summary>
    /// <param name="bcdWord">The BCD word.</param>
    /// <returns>A ushort.</returns>
    public static ushort ToUInt16(short bcdWord) => ToUInt16(BitConverter.GetBytes(bcdWord));

    /// <summary>Converts to uint16.</summary>
    /// <param name="bcdBytes">The BCD bytes.</param>
    /// <returns>A ushort.</returns>
    /// <exception cref="ArgumentOutOfRangeException">
    /// bcdBytes - The BCD Bytes Array Length must be '2' for conversion to UInt16.
    /// </exception>
    public static ushort ToUInt16(byte[] bcdBytes)
    {
        if (bcdBytes is null)
        {
            throw new ArgumentNullException(nameof(bcdBytes), BcdBytesArrayCannotBeNullMessage);
        }

        if (bcdBytes.Length != 2)
        {
            throw new ArgumentOutOfRangeException(
                nameof(bcdBytes),
                "The BCD Bytes Array Length must be '2' for conversion to UInt16");
        }

        var converted = ConvertToBinaryBytes(bcdBytes);
        return BitConverter.ToUInt16(converted, 0);
    }

    /// <summary>Converts to int32.</summary>
    /// <param name="bcdWord1">The BCD word1.</param>
    /// <param name="bcdWord2">The BCD word2.</param>
    /// <returns>An int.</returns>
    public static int ToInt32(short bcdWord1, short bcdWord2)
    {
        var integerBytes = new List<byte>(ProtocolConstants.Four);
        integerBytes.AddRange(BitConverter.GetBytes(bcdWord1));
        integerBytes.AddRange(BitConverter.GetBytes(bcdWord2));

        return ToInt32([.. integerBytes]);
    }

    /// <summary>Converts to int32.</summary>
    /// <param name="bcdBytes">The BCD bytes.</param>
    /// <returns>An int.</returns>
    /// <exception cref="ArgumentOutOfRangeException">
    /// bcdBytes - The BCD Bytes Array Length must be '4' for conversion to Int32.
    /// </exception>
    public static int ToInt32(byte[] bcdBytes)
    {
        if (bcdBytes is null)
        {
            throw new ArgumentNullException(nameof(bcdBytes), BcdBytesArrayCannotBeNullMessage);
        }

        if (bcdBytes.Length != 4)
        {
            throw new ArgumentOutOfRangeException(
                nameof(bcdBytes),
                "The BCD Bytes Array Length must be '4' for conversion to Int32");
        }

        var converted = ConvertToBinaryBytes(bcdBytes);
        return BitConverter.ToInt32(converted, 0);
    }

    /// <summary>Converts to uint32.</summary>
    /// <param name="bcdWord1">The BCD word1.</param>
    /// <param name="bcdWord2">The BCD word2.</param>
    /// <returns>A uint.</returns>
    public static uint ToUInt32(short bcdWord1, short bcdWord2)
    {
        var integerBytes = new List<byte>(ProtocolConstants.Four);
        integerBytes.AddRange(BitConverter.GetBytes(bcdWord1));
        integerBytes.AddRange(BitConverter.GetBytes(bcdWord2));

        return ToUInt32([.. integerBytes]);
    }

    /// <summary>Converts to uint32.</summary>
    /// <param name="bcdBytes">The BCD bytes.</param>
    /// <returns>A uint.</returns>
    /// <exception cref="ArgumentOutOfRangeException">
    /// bcdBytes - The BCD Bytes Array Length must be '4' for conversion to UInt32.
    /// </exception>
    public static uint ToUInt32(byte[] bcdBytes)
    {
        if (bcdBytes is null)
        {
            throw new ArgumentNullException(nameof(bcdBytes), BcdBytesArrayCannotBeNullMessage);
        }

        if (bcdBytes.Length != 4)
        {
            throw new ArgumentOutOfRangeException(
                nameof(bcdBytes),
                "The BCD Bytes Array Length must be '4' for conversion to UInt32");
        }

        var converted = ConvertToBinaryBytes(bcdBytes);
        return BitConverter.ToUInt32(converted, 0);
    }

    /// <summary>Gets the BCD byte.</summary>
    /// <param name="binaryValue">The binary value.</param>
    /// <returns>A BCD-encoded byte.</returns>
    public static byte GetBCDByte(byte binaryValue) => ConvertToBCDBytes(binaryValue, 1)[0];

    /// <summary>Gets the BCD word.</summary>
    /// <param name="binaryValue">The binary value.</param>
    /// <returns>A BCD-encoded word.</returns>
    public static short GetBCDWord(short binaryValue) =>
        BitConverter.ToInt16(ConvertToBCDBytes(binaryValue, ProtocolConstants.Two), 0);

    /// <summary>Gets the BCD word.</summary>
    /// <param name="binaryValue">The binary value.</param>
    /// <returns>A BCD-encoded word.</returns>
    public static short GetBCDWord(ushort binaryValue) =>
        GetBCDWord(unchecked((short)binaryValue));

    /// <summary>Gets the BCD words.</summary>
    /// <param name="binaryValue">The binary value.</param>
    /// <returns>An array of two BCD-encoded words.</returns>
    public static short[] GetBCDWords(int binaryValue)
    {
        var bcdBytes = ConvertToBCDBytes(binaryValue, ProtocolConstants.Four);
        return [BitConverter.ToInt16(bcdBytes, 0), BitConverter.ToInt16(bcdBytes, ProtocolConstants.Two)];
    }

    /// <summary>Gets the BCD words.</summary>
    /// <param name="binaryValue">The binary value.</param>
    /// <returns>An array of two BCD-encoded words.</returns>
    public static short[] GetBCDWords(uint binaryValue) =>
        GetBCDWords(unchecked((int)binaryValue));

    /// <summary>Gets the BCD bytes.</summary>
    /// <param name="binaryValue">The binary value.</param>
    /// <returns>A BCD-encoded byte array.</returns>
    public static byte[] GetBCDBytes(short binaryValue) => ConvertToBCDBytes(binaryValue, ProtocolConstants.Two);

    /// <summary>Gets the BCD bytes.</summary>
    /// <param name="binaryValue">The binary value.</param>
    /// <returns>A BCD-encoded byte array.</returns>
    public static byte[] GetBCDBytes(ushort binaryValue) => GetBCDBytes(unchecked((short)binaryValue));

    /// <summary>Gets the BCD bytes.</summary>
    /// <param name="binaryValue">The binary value.</param>
    /// <returns>A BCD-encoded byte array.</returns>
    public static byte[] GetBCDBytes(int binaryValue) => ConvertToBCDBytes(binaryValue, ProtocolConstants.Four);

    /// <summary>Gets the BCD bytes.</summary>
    /// <param name="binaryValue">The binary value.</param>
    /// <returns>A BCD-encoded byte array.</returns>
    public static byte[] GetBCDBytes(uint binaryValue) => GetBCDBytes(unchecked((int)binaryValue));

    /// <summary>Initializes a new instance of the <see cref="ConvertToBinaryBytes"/> class.</summary>
    /// <param name="bcdBytes">The b cd by te s value.</param>
    /// <returns>The result produced by the operation.</returns>
    private static byte[] ConvertToBinaryBytes(byte[] bcdBytes)
    {
        if (bcdBytes.Length == 0)
        {
            throw new ArgumentOutOfRangeException(nameof(bcdBytes), "The BCD Bytes Length cannot be Zero");
        }

        if (bcdBytes.Length > 4)
        {
            throw new ArgumentOutOfRangeException(nameof(bcdBytes), "The BCD Bytes Length cannot be greater than 4");
        }

        long binaryValue = 0;

        for (var i = bcdBytes.Length - 1; i >= 0; i--)
        {
            var bcdByte = bcdBytes[i];
            binaryValue *= ProtocolConstants.OneHundred;
            binaryValue += (long)(ProtocolConstants.Ten * (bcdByte >> 4));
            binaryValue += (long)(bcdByte & 0xF);
        }

        var binaryBytes = BitConverter.GetBytes(binaryValue);
        var result = new byte[bcdBytes.Length];
        Array.Copy(binaryBytes, 0, result, 0, result.Length);
        return result;
    }

    /// <summary>Initializes a new instance of the <see cref="ConvertToBCDBytes"/> class.</summary>
    /// <param name="binaryValue">The b in ar yv al ue value.</param>
    /// <param name="byteLength">The b yt el en gt h value.</param>
    /// <returns>The result produced by the operation.</returns>
    private static byte[] ConvertToBCDBytes(long binaryValue, int byteLength)
    {
        var bcdBytes = new byte[byteLength];

        for (var i = 0; i < bcdBytes.Length; i++)
        {
            var lowDigit = binaryValue % ProtocolConstants.Ten;
            var highDigit = (binaryValue % ProtocolConstants.OneHundred) - lowDigit;

            if (highDigit != 0)
            {
                highDigit /= ProtocolConstants.Ten;
            }

            lowDigit = lowDigit < 0 ? -lowDigit : lowDigit;
            highDigit = highDigit < 0 ? -highDigit : highDigit;

            bcdBytes[i] = (byte)((highDigit << 4) | lowDigit);

            if (binaryValue == 0)
            {
                break;
            }

            binaryValue /= ProtocolConstants.OneHundred;
        }

        return bcdBytes;
    }
}
