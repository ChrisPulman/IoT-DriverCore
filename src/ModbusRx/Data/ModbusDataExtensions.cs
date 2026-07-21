// Copyright (c) 2019-2026 Chris Pulman and contributors. All rights reserved.
// Chris Pulman and contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

#if NET8_0_OR_GREATER
using System.Numerics;
#endif

#if REACTIVE_SHIM
namespace ModbusRx.Reactive.Data;
#else
namespace ModbusRx.Data;
#endif

/// <summary>High-performance data conversion extensions optimized for different target frameworks.</summary>
public static class ModbusDataExtensions
{
    /// <summary>Packs boolean values into bytes with optimized performance.</summary>
    /// <param name="values">The extension receiver.</param>
    /// <returns>Array of bytes containing packed boolean values.</returns>
    public static byte[] PackBooleans(bool[] values)
    {
        if (values is null)
        {
            return [];
        }

        var byteCount = (values.Length + Seven) / Eight;
        var result = new byte[byteCount];

#if NET8_0_OR_GREATER
        // Use vectorized operations for better performance on newer frameworks
        if (Vector.IsHardwareAccelerated && values.Length >= Vector<byte>.Count)
        {
            PackBooleansVectorized(values, result);
        }
        else
        {
            PackBooleansScalar(values, result);
        }
#else
        PackBooleansScalar(values, result);
#endif

        return result;
    }

    /// <summary>Unpacks bytes into boolean values with optimized performance.</summary>
    /// <param name="bytes">The extension receiver.</param>
    /// <param name="numberOfBooleans">The number of boolean values to extract.</param>
    /// <returns>Array of boolean values.</returns>
    public static bool[] UnpackBooleans(byte[] bytes, int numberOfBooleans)
    {
        if (bytes is null || numberOfBooleans <= 0)
        {
            return [];
        }

        var result = new bool[numberOfBooleans];

#if NET8_0_OR_GREATER
        // Use vectorized operations for better performance on newer frameworks
        if (Vector.IsHardwareAccelerated && numberOfBooleans >= Vector<byte>.Count)
        {
            UnpackBooleansVectorized(bytes, result, numberOfBooleans);
        }
        else
        {
            UnpackBooleansScalar(bytes, result, numberOfBooleans);
        }
#else
        UnpackBooleansScalar(bytes, result, numberOfBooleans);
#endif

        return result;
    }

    /// <summary>Performs a fast memory comparison between two byte arrays.</summary>
    /// <param name="bytes">The extension receiver.</param>
    /// <param name="array2">The second array.</param>
    /// <returns>True if arrays are equal.</returns>
    public static bool FastEquals(byte[] bytes, byte[] array2)
    {
        return bytes is null || array2 is null || bytes.Length != array2.Length
            ? bytes is null && array2 is null
            : FastEqualsSameLength(bytes, array2);
    }

    /// <summary>Converts a 32-bit integer to two 16-bit registers with optimized performance.</summary>
    /// <param name="value">The extension receiver.</param>
    /// <param name="swapWords">Whether to swap word order.</param>
    /// <returns>Array containing two 16-bit register values.</returns>
    public static ushort[] ToRegisters(int value, bool swapWords)
    {
        var bytes = BitConverter.GetBytes(value);
        var result = new ushort[2];

        if (swapWords)
        {
            result[0] = BitConverter.ToUInt16(bytes, Two);
            result[1] = BitConverter.ToUInt16(bytes, 0);
        }
        else
        {
            result[0] = BitConverter.ToUInt16(bytes, 0);
            result[1] = BitConverter.ToUInt16(bytes, Two);
        }

        return result;
    }

    /// <summary>Converts a 64-bit long to four 16-bit registers with optimized performance.</summary>
    /// <param name="value">The extension receiver.</param>
    /// <param name="swapWords">Whether to swap word order.</param>
    /// <returns>Array containing four 16-bit register values.</returns>
    public static ushort[] ToRegisters(long value, bool swapWords)
    {
        var bytes = BitConverter.GetBytes(value);
        var result = new ushort[4];

        if (swapWords)
        {
            result[0] = BitConverter.ToUInt16(bytes, Two);
            result[1] = BitConverter.ToUInt16(bytes, 0);
            result[2] = BitConverter.ToUInt16(bytes, Six);
            result[3] = BitConverter.ToUInt16(bytes, Four);
        }
        else
        {
            result[0] = BitConverter.ToUInt16(bytes, 0);
            result[1] = BitConverter.ToUInt16(bytes, Two);
            result[2] = BitConverter.ToUInt16(bytes, Four);
            result[3] = BitConverter.ToUInt16(bytes, Six);
        }

        return result;
    }

    /// <summary>Converts a 32-bit unsigned integer to two 16-bit registers with optimized performance.</summary>
    /// <param name="value">The extension receiver.</param>
    /// <param name="swapWords">Whether to swap word order.</param>
    /// <returns>Array containing two 16-bit register values.</returns>
    public static ushort[] ToRegisters(uint value, bool swapWords) =>
        ToRegisters(unchecked((int)value), swapWords);

    /// <summary>Converts two 16-bit registers to a 32-bit integer with optimized performance.</summary>
    /// <param name="registers">The extension receiver.</param>
    /// <param name="startIndex">The start index.</param>
    /// <param name="swapWords">Whether words are swapped.</param>
    /// <returns>The 32-bit integer value.</returns>
    public static int ToInt32(ushort[] registers, int startIndex, bool swapWords)
    {
        if (registers is null || registers.Length < startIndex + Two)
        {
            throw new ArgumentException("Insufficient registers for Int32 conversion.", nameof(registers));
        }

        var bytes = new byte[4];

        if (swapWords)
        {
            var bytes0 = BitConverter.GetBytes(registers[startIndex + 1]);
            var bytes1 = BitConverter.GetBytes(registers[startIndex]);
            Array.Copy(bytes0, 0, bytes, 0, Two);
            Array.Copy(bytes1, 0, bytes, Two, Two);
        }
        else
        {
            var bytes0 = BitConverter.GetBytes(registers[startIndex]);
            var bytes1 = BitConverter.GetBytes(registers[startIndex + 1]);
            Array.Copy(bytes0, 0, bytes, 0, Two);
            Array.Copy(bytes1, 0, bytes, Two, Two);
        }

        return BitConverter.ToInt32(bytes, 0);
    }

    /// <summary>Converts two 16-bit registers to a 32-bit unsigned integer with optimized performance.</summary>
    /// <param name="registers">The extension receiver.</param>
    /// <param name="startIndex">The start index.</param>
    /// <param name="swapWords">Whether words are swapped.</param>
    /// <returns>The 32-bit unsigned integer value.</returns>
    public static uint ToUInt32(ushort[] registers, int startIndex, bool swapWords)
    {
        if (registers is null || registers.Length < startIndex + Two)
        {
            throw new ArgumentException("Insufficient registers for UInt32 conversion.", nameof(registers));
        }

        var bytes = new byte[4];

        if (swapWords)
        {
            var bytes0 = BitConverter.GetBytes(registers[startIndex + 1]);
            var bytes1 = BitConverter.GetBytes(registers[startIndex]);
            Array.Copy(bytes0, 0, bytes, 0, Two);
            Array.Copy(bytes1, 0, bytes, Two, Two);
        }
        else
        {
            var bytes0 = BitConverter.GetBytes(registers[startIndex]);
            var bytes1 = BitConverter.GetBytes(registers[startIndex + 1]);
            Array.Copy(bytes0, 0, bytes, 0, Two);
            Array.Copy(bytes1, 0, bytes, Two, Two);
        }

        return BitConverter.ToUInt32(bytes, 0);
    }

    /// <summary>Converts four 16-bit registers to a 64-bit long with optimized performance.</summary>
    /// <param name="registers">The extension receiver.</param>
    /// <param name="startIndex">The start index.</param>
    /// <param name="swapWords">Whether words are swapped.</param>
    /// <returns>The 64-bit long value.</returns>
    public static long ToInt64(ushort[] registers, int startIndex, bool swapWords)
    {
        if (registers is null || registers.Length < startIndex + Four)
        {
            throw new ArgumentException("Insufficient registers for Int64 conversion.", nameof(registers));
        }

        var bytes = new byte[8];

        if (swapWords)
        {
            var bytes0 = BitConverter.GetBytes(registers[startIndex + 1]);
            var bytes1 = BitConverter.GetBytes(registers[startIndex]);
            var bytes2 = BitConverter.GetBytes(registers[startIndex + Three]);
            var bytes3 = BitConverter.GetBytes(registers[startIndex + Two]);

            Array.Copy(bytes0, 0, bytes, 0, Two);
            Array.Copy(bytes1, 0, bytes, Two, Two);
            Array.Copy(bytes2, 0, bytes, Four, Two);
            Array.Copy(bytes3, 0, bytes, Six, Two);
        }
        else
        {
            var bytes0 = BitConverter.GetBytes(registers[startIndex]);
            var bytes1 = BitConverter.GetBytes(registers[startIndex + 1]);
            var bytes2 = BitConverter.GetBytes(registers[startIndex + Two]);
            var bytes3 = BitConverter.GetBytes(registers[startIndex + Three]);

            Array.Copy(bytes0, 0, bytes, 0, Two);
            Array.Copy(bytes1, 0, bytes, Two, Two);
            Array.Copy(bytes2, 0, bytes, Four, Two);
            Array.Copy(bytes3, 0, bytes, Six, Two);
        }

        return BitConverter.ToInt64(bytes, 0);
    }

    /// <summary>Executes the Pack Booleans Scalar operation.</summary>
    /// <param name="values">The values value.</param>
    /// <param name="result">The result value.</param>
    private static void PackBooleansScalar(bool[] values, byte[] result)
    {
        for (var i = 0; i < values.Length; i++)
        {
            if (values[i])
            {
                var byteIndex = i / Eight;
                var bitIndex = i % Eight;
                result[byteIndex] |= (byte)(1 << bitIndex);
            }
        }
    }

    /// <summary>Executes the Unpack Booleans Scalar operation.</summary>
    /// <param name="bytes">The bytes value.</param>
    /// <param name="result">The result value.</param>
    /// <param name="numberOfBooleans">The number Of Booleans value.</param>
    private static void UnpackBooleansScalar(byte[] bytes, bool[] result, int numberOfBooleans)
    {
        for (var i = 0; i < numberOfBooleans; i++)
        {
            var byteIndex = i / Eight;
            var bitIndex = i % Eight;

            if (byteIndex < bytes.Length)
            {
                result[i] = (bytes[byteIndex] & (1 << bitIndex)) != 0;
            }
        }
    }

    /// <summary>Executes the Fast Equals Scalar operation.</summary>
    /// <param name="array1">The array1 value.</param>
    /// <param name="array2">The array2 value.</param>
    /// <returns>The result.</returns>
    private static bool FastEqualsScalar(byte[] array1, byte[] array2)
    {
        for (var i = 0; i < array1.Length; i++)
        {
            if (array1[i] != array2[i])
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>Compares two byte arrays known to have the same length.</summary>
    /// <param name="array1">The first array.</param>
    /// <param name="array2">The second array.</param>
    /// <returns>True when both arrays contain the same bytes.</returns>
    private static bool FastEqualsSameLength(byte[] array1, byte[] array2)
    {
#if NET8_0_OR_GREATER
        // Use vectorized comparison for better performance.
        return Vector.IsHardwareAccelerated && array1.Length >= Vector<byte>.Count
            ? FastEqualsVectorized(array1, array2)
            : FastEqualsScalar(array1, array2);
#else
        return FastEqualsScalar(array1, array2);
#endif
    }

#if NET8_0_OR_GREATER
    /// <summary>Executes the Pack Booleans Vectorized operation.</summary>
    /// <param name="values">The values value.</param>
    /// <param name="result">The result value.</param>
    private static void PackBooleansVectorized(bool[] values, byte[] result)
    {
        // For now, fall back to scalar implementation
        // In a real implementation, this would use SIMD instructions
        PackBooleansScalar(values, result);
    }

    /// <summary>Executes the Unpack Booleans Vectorized operation.</summary>
    /// <param name="bytes">The bytes value.</param>
    /// <param name="result">The result value.</param>
    /// <param name="numberOfBooleans">The number Of Booleans value.</param>
    private static void UnpackBooleansVectorized(byte[] bytes, bool[] result, int numberOfBooleans)
    {
        // For now, fall back to scalar implementation
        // In a real implementation, this would use SIMD instructions
        UnpackBooleansScalar(bytes, result, numberOfBooleans);
    }

    /// <summary>Executes the Fast Equals Vectorized operation.</summary>
    /// <param name="array1">The array1 value.</param>
    /// <param name="array2">The array2 value.</param>
    /// <returns>The result.</returns>
    private static bool FastEqualsVectorized(byte[] array1, byte[] array2)
    {
        var vectorSize = Vector<byte>.Count;
        var i = 0;

        // Process in vector-sized chunks
        for (; i <= array1.Length - vectorSize; i += vectorSize)
        {
            var v1 = new Vector<byte>(array1, i);
            var v2 = new Vector<byte>(array2, i);

            if (!Vector.EqualsAll(v1, v2))
            {
                return false;
            }
        }

        // Process remaining bytes
        for (; i < array1.Length; i++)
        {
            if (array1[i] != array2[i])
            {
                return false;
            }
        }

        return true;
    }
#endif
}
