// Copyright (c) 2019-2026 Chris Pulman and contributors. All rights reserved.
// Chris Pulman and contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

#if REACTIVE_SHIM
namespace IoT.DriverCore.Serial.Reactive;
#else
namespace IoT.DriverCore.Serial;
#endif

/// <summary>Compatibility factory for async observables.</summary>
public static class ObservableAsync
{
    /// <summary>Creates an async observable that emits a single value.</summary>
    /// <typeparam name="T">The value type.</typeparam>
    /// <param name="value">The value to emit.</param>
    /// <returns>An async observable that emits <paramref name="value"/>.</returns>
    public static IObservableAsync<T> Return<T>(T value) => SignalAsync.Return(value);
}
