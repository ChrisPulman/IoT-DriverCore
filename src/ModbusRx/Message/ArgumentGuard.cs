// Copyright (c) 2019-2026 Chris Pulman and contributors. All rights reserved.
// Chris Pulman and contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

#if REACTIVE_SHIM
namespace IoT.Driver.ModbusRx.Reactive.Message;
#else
namespace IoT.Driver.ModbusRx.Message;
#endif

/// <summary>Provides target-framework-compatible argument validation.</summary>
internal static class ArgumentGuard
{
    /// <summary>Returns a non-null value or throws for a null argument.</summary>
    /// <typeparam name="T">The reference type being validated.</typeparam>
    /// <param name="value">The value to validate.</param>
    /// <param name="parameterName">The parameter name.</param>
    /// <returns>The validated value.</returns>
    internal static T NotNull<T>(T value, string parameterName)
        where T : class
    {
#if NET6_0_OR_GREATER
        ArgumentNullException.ThrowIfNull(value, parameterName);
        return value;
#else
        return value ?? throw new ArgumentNullException(parameterName);
#endif
    }
}
