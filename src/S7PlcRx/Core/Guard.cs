// Copyright (c) 2019-2026 Chris Pulman and contributors. All rights reserved.
// Chris Pulman and contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

#if REACTIVE_SHIM
namespace IoT.Driver.S7PlcRx.Reactive;
#else
namespace IoT.Driver.S7PlcRx;
#endif

/// <summary>Provides target-framework-independent argument validation.</summary>
internal static class Guard
{
    /// <summary>Requires a non-null reference.</summary>
    /// <typeparam name="T">The reference type.</typeparam>
    /// <param name="value">The value to validate.</param>
    /// <param name="parameterName">The parameter name.</param>
    internal static void NotNull<T>(T? value, string parameterName)
        where T : class
        => _ = value ?? throw new ArgumentNullException(parameterName);

    /// <summary>Requires an integer to be less than an exclusive upper bound.</summary>
    /// <param name="value">The value to validate.</param>
    /// <param name="exclusiveUpperBound">The exclusive upper bound.</param>
    /// <param name="parameterName">The parameter name.</param>
    /// <param name="message">The exception message.</param>
    internal static void LessThan(int value, int exclusiveUpperBound, string parameterName, string message)
    {
        if (value < exclusiveUpperBound)
        {
            return;
        }

        throw new ArgumentOutOfRangeException(parameterName, message);
    }

    /// <summary>Requires text containing at least one non-whitespace character.</summary>
    /// <param name="value">The value to validate.</param>
    /// <param name="parameterName">The parameter name.</param>
    internal static void NotNullOrWhiteSpace(string? value, string parameterName)
    {
#if NET8_0_OR_GREATER
        ArgumentException.ThrowIfNullOrWhiteSpace(value, parameterName);
#else
        if (!string.IsNullOrWhiteSpace(value))
        {
            return;
        }

        throw new ArgumentException("A non-empty value is required.", parameterName);
#endif
    }
}
