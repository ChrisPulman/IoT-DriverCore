// Copyright (c) 2019-2026 Chris Pulman and contributors. All rights reserved.
// Chris Pulman and contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

#if REACTIVE_SHIM
namespace IoT.DriverCore.S7PlcRx.Reactive.Cache;
#else
namespace IoT.DriverCore.S7PlcRx.Cache;
#endif

/// <summary>Represents a value stored in the cache along with its timestamp and access count.</summary>
/// <param name="value">The value to be cached. Can be null.</param>
/// <param name="timestamp">The date and time when the value was cached, in UTC.</param>
/// <param name="hitCount">The initial number of times the cached value has been accessed. Defaults to 0.</param>
internal class CachedValue(object? value, DateTime timestamp, long hitCount = 0)
{
    /// <summary>Gets the cached value.</summary>
    internal object? Value { get; } = value;

    /// <summary>Gets when the value was cached.</summary>
    internal DateTime Timestamp { get; } = timestamp;

    /// <summary>Gets or sets the number of times this value has been accessed.</summary>
    internal long HitCount { get; set; } = hitCount;

    /// <summary>Gets whether this cached value has expired.</summary>
    /// <param name="maxAge">The maximum age for cached values.</param>
    /// <param name="timeProvider">The time provider; defaults to <see cref="TimeProvider.System"/>.</param>
    /// <returns>True if the value has expired.</returns>
    internal bool IsExpired(TimeSpan maxAge, TimeProvider? timeProvider = null) =>
        (timeProvider ?? TimeProvider.System).GetUtcNow().UtcDateTime - Timestamp > maxAge;
}
