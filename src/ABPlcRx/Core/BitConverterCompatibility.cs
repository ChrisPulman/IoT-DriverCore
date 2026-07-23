// Copyright (c) 2019-2026 Chris Pulman and contributors. All rights reserved.
// Chris Pulman and contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

#if REACTIVELIST_REACTIVE
namespace IoT.DriverCore.ABPlcRx.Reactive;
#else
namespace IoT.DriverCore.ABPlcRx;
#endif

/// <summary>Provides floating-point bit conversion across supported target frameworks.</summary>
internal static class BitConverterCompatibility
{
    /// <summary>Gets the bit representation of a single-precision value.</summary>
    /// <param name="value">The value to convert.</param>
    /// <returns>The raw bits.</returns>
    internal static int SingleToInt32Bits(float value)
    {
#if NETSTANDARD2_1_OR_GREATER || NETCOREAPP3_0_OR_GREATER
        return BitConverter.SingleToInt32Bits(value);
#else
        return BitConverter.ToInt32(BitConverter.GetBytes(value), 0);
#endif
    }

    /// <summary>Gets the bit representation of a double-precision value.</summary>
    /// <param name="value">The value to convert.</param>
    /// <returns>The raw bits.</returns>
    internal static long DoubleToInt64Bits(double value)
    {
#if NETSTANDARD2_1_OR_GREATER || NETCOREAPP3_0_OR_GREATER
        return BitConverter.DoubleToInt64Bits(value);
#else
        return BitConverter.ToInt64(BitConverter.GetBytes(value), 0);
#endif
    }

    /// <summary>Creates a single-precision value from raw bits.</summary>
    /// <param name="value">The raw bits.</param>
    /// <returns>The converted value.</returns>
    internal static float Int32BitsToSingle(int value)
    {
#if NETSTANDARD2_1_OR_GREATER || NETCOREAPP3_0_OR_GREATER
        return BitConverter.Int32BitsToSingle(value);
#else
        return BitConverter.ToSingle(BitConverter.GetBytes(value), 0);
#endif
    }

    /// <summary>Creates a double-precision value from raw bits.</summary>
    /// <param name="value">The raw bits.</param>
    /// <returns>The converted value.</returns>
    internal static double Int64BitsToDouble(long value)
    {
#if NETSTANDARD2_1_OR_GREATER || NETCOREAPP3_0_OR_GREATER
        return BitConverter.Int64BitsToDouble(value);
#else
        return BitConverter.ToDouble(BitConverter.GetBytes(value), 0);
#endif
    }
}
