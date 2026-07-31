// Copyright (c) 2019-2026 Chris Pulman and contributors. All rights reserved.
// Chris Pulman and contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

#if REACTIVE_SHIM
namespace IoT.Driver.ModbusRx.Reactive.Utility;
#else
namespace IoT.Driver.ModbusRx.Utility;
#endif

/// <summary>Provides target-framework-independent argument and lifetime validation.</summary>
internal static class ModbusGuard
{
    /// <summary>Returns a non-null reference or throws for a null argument.</summary>
    /// <typeparam name="T">The reference type being validated.</typeparam>
    /// <param name="value">The value to validate.</param>
    /// <param name="parameterName">The parameter name.</param>
    /// <returns>The validated value.</returns>
    internal static T NotNull<T>(T? value, string parameterName)
        where T : class => value ?? throw new ArgumentNullException(parameterName);

    /// <summary>Returns when an instance is available or throws when it has been disposed.</summary>
    /// <param name="isDisposed">Whether the instance has been disposed.</param>
    /// <param name="objectName">The disposed object name.</param>
    /// <returns><c>true</c> when the instance is available.</returns>
    internal static bool IsNotDisposed(bool isDisposed, string objectName) =>
        isDisposed ? throw new ObjectDisposedException(objectName) : true;
}
