// Copyright (c) 2019-2026 Chris Pulman and contributors. All rights reserved.
// Chris Pulman and contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System;

#if REACTIVE_SHIM
namespace IoT.Driver.OmronPlcRx.Reactive.Core;
#else
namespace IoT.Driver.OmronPlcRx.Core;
#endif

/// <summary>Provides argument validation across all supported target frameworks.</summary>
internal static class OmronArgumentGuards
{
    /// <summary>Throws when a reference value is null.</summary>
    /// <typeparam name="T">Reference type to validate.</typeparam>
    /// <param name="value">Value to validate.</param>
    /// <param name="paramName">Parameter name.</param>
    internal static void ThrowIfNull<T>(T? value, string paramName)
        where T : class
    {
#if NET6_0_OR_GREATER
        ArgumentNullException.ThrowIfNull(value, paramName);
#else
        _ = value ?? throw new ArgumentNullException(paramName);
#endif
    }

    /// <summary>Throws when an integer value is not positive.</summary>
    /// <param name="value">Value to validate.</param>
    /// <param name="paramName">Parameter name.</param>
    internal static void ThrowIfNegativeOrZero(int value, string paramName)
    {
#if NET8_0_OR_GREATER
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(value, paramName);
#else
        _ = value > 0 ? value : throw new ArgumentOutOfRangeException(paramName);
#endif
    }

    /// <summary>Throws when an integer value is negative.</summary>
    /// <param name="value">Value to validate.</param>
    /// <param name="paramName">Parameter name.</param>
    internal static void ThrowIfNegative(int value, string paramName)
    {
#if NET8_0_OR_GREATER
        ArgumentOutOfRangeException.ThrowIfNegative(value, paramName);
#else
        _ = value >= 0 ? value : throw new ArgumentOutOfRangeException(paramName);
#endif
    }

    /// <summary>Throws when a byte value is zero.</summary>
    /// <param name="value">Value to validate.</param>
    /// <param name="paramName">Parameter name.</param>
    internal static void ThrowIfZero(byte value, string paramName)
    {
#if NET8_0_OR_GREATER
        ArgumentOutOfRangeException.ThrowIfZero(value, paramName);
#else
        _ = value != 0 ? value : throw new ArgumentOutOfRangeException(paramName);
#endif
    }

    /// <summary>Throws when an unsigned short value is zero.</summary>
    /// <param name="value">Value to validate.</param>
    /// <param name="paramName">Parameter name.</param>
    internal static void ThrowIfZero(ushort value, string paramName)
    {
#if NET8_0_OR_GREATER
        ArgumentOutOfRangeException.ThrowIfZero(value, paramName);
#else
        _ = value != 0 ? value : throw new ArgumentOutOfRangeException(paramName);
#endif
    }

    /// <summary>Throws when a comparable value exceeds its upper bound.</summary>
    /// <typeparam name="T">Comparable value type.</typeparam>
    /// <param name="value">Value to validate.</param>
    /// <param name="other">Inclusive upper bound.</param>
    /// <param name="paramName">Parameter name.</param>
    internal static void ThrowIfGreaterThan<T>(T value, T other, string paramName)
        where T : IComparable<T>
    {
#if NET8_0_OR_GREATER
        ArgumentOutOfRangeException.ThrowIfGreaterThan(value, other, paramName);
#else
        _ = value.CompareTo(other) <= 0 ? value : throw new ArgumentOutOfRangeException(paramName);
#endif
    }

    /// <summary>Throws when a string value is null, empty, or whitespace.</summary>
    /// <param name="value">Value to validate.</param>
    /// <param name="paramName">Parameter name.</param>
    internal static void ThrowIfNullOrWhiteSpace(string? value, string paramName)
    {
#if NET6_0_OR_GREATER
        ArgumentException.ThrowIfNullOrWhiteSpace(value, paramName);
#else
        switch (value)
        {
            case null:
                throw new ArgumentNullException(paramName);
            case var text when string.IsNullOrWhiteSpace(text):
                throw new ArgumentException("The value cannot be empty or whitespace.", paramName);
        }
#endif
    }
}
