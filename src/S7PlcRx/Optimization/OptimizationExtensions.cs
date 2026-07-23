// Copyright (c) 2019-2026 Chris Pulman and contributors. All rights reserved.
// Chris Pulman and contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Collections.Concurrent;
#if REACTIVE_SHIM
using IoT.DriverCore.S7PlcRx.Reactive.Cache;
#else
using IoT.DriverCore.S7PlcRx.Cache;
#endif

#if REACTIVE_SHIM
namespace IoT.DriverCore.S7PlcRx.Reactive.Optimization;
#else
namespace IoT.DriverCore.S7PlcRx.Optimization;
#endif

/// <summary>
/// Provides extension methods for IRxS7 to enable optimized tag monitoring, intelligent value caching, and cache
/// management for PLC data access.
/// </summary>
/// <remarks>These extensions enhance performance and usability when interacting with PLC tags by offering
/// adaptive polling, caching strategies, and cache statistics. All methods require a valid IRxS7 instance and are
/// designed to be thread-safe. Use these methods to reduce unnecessary network traffic, improve responsiveness, and
/// monitor tag changes efficiently.</remarks>
public static class OptimizationExtensions
{
    /// <summary>Gets the value cache used by all PLC instances.</summary>
    private static ConcurrentDictionary<string, CachedTagValue> ValueCache { get; } = new();

    /// <summary>Gets the lock used to protect shared cache mutations.</summary>
    private static Lock CacheLock { get; } = new();

    /// <summary>Observes changes to a specified PLC tag and emits significant value changes.</summary>
    /// <typeparam name="T">The type of the tag value to monitor.</typeparam>
    /// <param name="plc">The PLC instance.</param>
    /// <param name="tagName">The name of the tag to observe for changes.</param>
    /// <param name="comparer">The comparer used to determine whether two tag values are equal.</param>
    /// <param name="changeThreshold">The minimum change required between consecutive tag values.</param>
    /// <param name="debounceMs">The minimum interval, in milliseconds, between emitted change events.</param>
    /// <returns>An observable sequence of smart tag change events.</returns>
    public static IObservable<SmartTagChange<T>> MonitorTagSmart<T>(
        IRxS7 plc,
        string tagName,
        IEqualityComparer<T> comparer,
        double changeThreshold,
        int debounceMs) =>
        MonitorTagSmart(plc, tagName, comparer, changeThreshold, debounceMs, TimeProvider.System);

    /// <summary>Observes changes to a specified PLC tag and emits significant value changes.</summary>
    /// <typeparam name="T">The type of the tag value to monitor.</typeparam>
    /// <param name="plc">The PLC instance.</param>
    /// <param name="tagName">The name of the tag to observe for changes.</param>
    /// <param name="comparer">The comparer used to determine whether two tag values are equal.</param>
    /// <param name="changeThreshold">The minimum change required between consecutive tag values.</param>
    /// <param name="debounceMs">The minimum interval, in milliseconds, between emitted change events.</param>
    /// <param name="timeProvider">The time provider.</param>
    /// <returns>An observable sequence of smart tag change events.</returns>
    public static IObservable<SmartTagChange<T>> MonitorTagSmart<T>(
        IRxS7 plc,
        string tagName,
        IEqualityComparer<T> comparer,
        double changeThreshold,
        int debounceMs,
        TimeProvider timeProvider)
    {
        if (plc is null)
        {
            throw new ArgumentNullException(nameof(plc));
        }

#if NET8_0_OR_GREATER
        Guard.NotNullOrWhiteSpace(tagName, nameof(tagName));
#else
        if (string.IsNullOrWhiteSpace(tagName))
        {
            throw new ArgumentException("Tag name cannot be null or empty", nameof(tagName));
        }
#endif

        return plc.Observe(new LogicalTagKey<T>(tagName))
            .Timestamp()
            .Scan(
                (Previous: default(T), Current: default(T), PrevTime: DateTimeOffset.MinValue, IsFirst: true),
                (acc, timestamped) => (
                    Previous: acc.Current,
                    Current: timestamped.Value,
                    PrevTime: acc.IsFirst ? timestamped.Timestamp : acc.PrevTime,
                    IsFirst: false))
            .Where(state => !state.IsFirst)
            .Where(state => IsSignificantChange(state.Previous, state.Current, comparer, changeThreshold))
            .Select(state => new SmartTagChange<T>
                {
                    TagName = tagName,
                    PreviousValue = state.Previous,
                    CurrentValue = state.Current,
                    ChangeTime = timeProvider.GetUtcNow(),
                    ChangeAmount = CalculateChangeAmount(state.Previous, state.Current),
                })
            .Where(change => change is not null)
            .Sample(TimeSpan.FromMilliseconds(debounceMs))
            .Publish()
            .RefCount();
    }

    /// <summary>Retrieves a PLC tag value, using a valid cached value when available.</summary>
    /// <typeparam name="T">The type of the value to retrieve from the PLC tag.</typeparam>
    /// <param name="plc">The PLC instance.</param>
    /// <param name="tagName">The name of the PLC tag to read.</param>
    /// <param name="fallbackValue">The value returned when the PLC does not provide a value.</param>
    /// <param name="cacheTimeout">The maximum duration for which a cached value is considered valid.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    public static Task<T?> ValueCachedAsync<T>(
        IRxS7 plc,
        string tagName,
        T? fallbackValue,
        TimeSpan cacheTimeout) =>
        ValueCachedAsync(plc, tagName, fallbackValue, cacheTimeout, TimeProvider.System);

    /// <summary>Retrieves a PLC tag value, using a valid cached value when available.</summary>
    /// <typeparam name="T">The type of the value to retrieve from the PLC tag.</typeparam>
    /// <param name="plc">The PLC instance.</param>
    /// <param name="tagName">The name of the PLC tag to read.</param>
    /// <param name="fallbackValue">The value returned when the PLC does not provide a value.</param>
    /// <param name="cacheTimeout">The maximum duration for which a cached value is considered valid.</param>
    /// <param name="timeProvider">The time provider.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    public static async Task<T?> ValueCachedAsync<T>(
        IRxS7 plc,
        string tagName,
        T? fallbackValue,
        TimeSpan cacheTimeout,
        TimeProvider timeProvider)
    {
        if (plc is null)
        {
            throw new ArgumentNullException(nameof(plc));
        }

#if NET8_0_OR_GREATER
        Guard.NotNullOrWhiteSpace(tagName, nameof(tagName));
#else
        if (string.IsNullOrWhiteSpace(tagName))
        {
            throw new ArgumentException("Tag name cannot be null or empty", nameof(tagName));
        }
#endif

        var cacheKey = $"{plc.IP}_{tagName}";

        lock (CacheLock)
        {
            if (ValueCache.TryGetValue(cacheKey, out var cachedValue) &&
                timeProvider.GetUtcNow() - cachedValue.Timestamp <= cacheTimeout)
            {
                cachedValue.HitCount++;
                return cachedValue.Value is T value ? value : fallbackValue;
            }
        }

        var freshValue = await plc.ReadAsync(new LogicalTagKey<T>(tagName));

        lock (CacheLock)
        {
            ValueCache[cacheKey] = new CachedTagValue
            {
                Value = freshValue,
                Timestamp = timeProvider.GetUtcNow(),
                HitCount = 0,
            };
        }

        return freshValue is null ? fallbackValue : freshValue;
    }

    /// <summary>Clears all cached values for the specified PLC instance.</summary>
    /// <param name="plc">The PLC instance.</param>
    public static void ClearCache(IRxS7 plc) => ClearCache(plc, null);

    /// <summary>Clears a cached value for the specified PLC tag.</summary>
    /// <param name="plc">The PLC instance.</param>
    /// <param name="tagName">The name of the tag to clear from the cache.</param>
    public static void ClearCache(IRxS7 plc, string? tagName)
    {
        if (plc is null)
        {
            throw new ArgumentNullException(nameof(plc));
        }

        lock (CacheLock)
        {
            if (string.IsNullOrWhiteSpace(tagName))
            {
                var prefix = $"{plc.IP}_";
                var keysToRemove = new List<string>();
                foreach (var key in ValueCache.Keys)
                {
                    if (key.StartsWith(prefix, StringComparison.Ordinal))
                    {
                        keysToRemove.Add(key);
                    }
                }

                foreach (var key in keysToRemove)
                {
                    _ = ValueCache.TryRemove(key, out _);
                }
            }
            else
            {
                var cacheKey = $"{plc.IP}_{tagName}";
                _ = ValueCache.TryRemove(cacheKey, out _);
            }
        }
    }

    /// <summary>Retrieves cache usage statistics for the specified PLC instance.</summary>
    /// <param name="plc">The PLC instance.</param>
    /// <returns>A CacheStatistics object containing aggregated cache metrics for the specified PLC.</returns>
    public static CacheStatistics GetCacheStatistics(IRxS7 plc) =>
        GetCacheStatistics(plc, TimeProvider.System);

    /// <summary>Retrieves cache usage statistics for the specified PLC instance.</summary>
    /// <param name="plc">The PLC instance.</param>
    /// <param name="timeProvider">The time provider.</param>
    /// <returns>A CacheStatistics object containing aggregated cache metrics for the specified PLC.</returns>
    public static CacheStatistics GetCacheStatistics(IRxS7 plc, TimeProvider timeProvider)
    {
        if (plc is null)
        {
            throw new ArgumentNullException(nameof(plc));
        }

        lock (CacheLock)
        {
            var prefix = $"{plc.IP}_";
            var totalHits = 0L;
            var totalEntries = 0;
            var now = timeProvider.GetUtcNow();
            var oldestEntry = now;
            var newestEntry = now;
            foreach (var kvp in ValueCache)
            {
                if (!kvp.Key.StartsWith(prefix, StringComparison.Ordinal))
                {
                    continue;
                }

                totalEntries++;
                UpdateCacheStatistics(kvp.Value, totalEntries, ref totalHits, ref oldestEntry, ref newestEntry);
            }

            return new CacheStatistics
            {
                TotalEntries = totalEntries,
                TotalHits = totalHits,
                HitRate = totalEntries > 0 ? (double)totalHits / (totalHits + totalEntries) : 0.0,
                OldestEntry = totalEntries > 0 ? oldestEntry : now,
                NewestEntry = totalEntries > 0 ? newestEntry : now,
            };
        }
    }

    /// <summary>Determines whether a value change exceeds a threshold.</summary>
    /// <remarks>For numeric types, the method compares the absolute difference to the threshold. For
    /// non-numeric types, any inequality is considered significant unless the threshold is less than or equal to zero.
    /// If either value is <see langword="null"/>, or both are equal, the change is not considered
    /// significant.</remarks>
    /// <typeparam name="T">
    /// The value type, which must support equality and numeric conversion when a threshold is used.
    /// </typeparam>
    /// <param name="previous">The previous value to compare. Can be <see langword="null"/>.</param>
    /// <param name="current">The current value to compare. Can be <see langword="null"/>.</param>
    /// <param name="comparer">The comparer used to determine whether the values are equal.</param>
    /// <param name="threshold">
    /// The minimum numeric difference. A non-positive value accepts any change.
    /// </param>
    /// <returns>Whether the value change is significant.</returns>
    private static bool IsSignificantChange<T>(
        T? previous,
        T? current,
        IEqualityComparer<T> comparer,
        double threshold)
    {
        if (previous is null || current is null)
        {
            return false;
        }

        if (comparer.Equals(previous, current))
        {
            return false;
        }

        if (threshold <= 0)
        {
            return true;
        }

        if (!IsNumericType(typeof(T)))
        {
            return true;
        }

        try
        {
            var prevVal = Convert.ToDouble(previous);
            var currVal = Convert.ToDouble(current);
            return Math.Abs(currVal - prevVal) >= threshold;
        }
        catch
        {
            return true;
        }
    }

    /// <summary>Calculates the absolute difference between two numeric values of the specified type.</summary>
    /// <remarks>This method only operates on numeric types that can be converted to <see cref="double"/>. If
    /// the type parameter <typeparamref name="T"/> is not numeric or conversion fails, the method returns 0.</remarks>
    /// <typeparam name="T">The numeric type to compare.</typeparam>
    /// <param name="previous">The previous value.</param>
    /// <param name="current">The current value.</param>
    /// <returns>The absolute numeric difference, or zero when the values cannot be converted.</returns>
    private static double CalculateChangeAmount<T>(T? previous, T? current)
    {
        if (previous is null || current is null || !IsNumericType(typeof(T)))
        {
            return 0;
        }

        try
        {
            var prevVal = Convert.ToDouble(previous);
            var currVal = Convert.ToDouble(current);
            return Math.Abs(currVal - prevVal);
        }
        catch
        {
            return 0;
        }
    }

    /// <summary>Determines whether a type is numeric.</summary>
    /// <remarks>This method considers the following types as numeric: byte, sbyte, short, ushort, int, uint,
    /// long, ulong, float, double, and decimal, as well as their nullable forms.</remarks>
    /// <param name="type">The type to evaluate. This can be a non-nullable or nullable numeric type.</param>
    /// <returns>Whether the type is numeric.</returns>
    private static bool IsNumericType(Type type)
    {
        var underlyingType = Nullable.GetUnderlyingType(type) ?? type;
        var typeCode = Type.GetTypeCode(underlyingType);

        return typeCode is >= TypeCode.SByte and <= TypeCode.Decimal;
    }

    /// <summary>Updates aggregate cache statistics for a single cache entry.</summary>
    /// <param name="value">The cached tag value.</param>
    /// <param name="totalEntries">The total entry count after adding the current value.</param>
    /// <param name="totalHits">The aggregate hit count.</param>
    /// <param name="oldestEntry">The oldest entry timestamp.</param>
    /// <param name="newestEntry">The newest entry timestamp.</param>
    private static void UpdateCacheStatistics(
        CachedTagValue value,
        int totalEntries,
        ref long totalHits,
        ref DateTimeOffset oldestEntry,
        ref DateTimeOffset newestEntry)
    {
        totalHits += value.HitCount;
        oldestEntry = totalEntries == 1 || value.Timestamp < oldestEntry ? value.Timestamp : oldestEntry;
        newestEntry = totalEntries == 1 || value.Timestamp > newestEntry ? value.Timestamp : newestEntry;
    }
}
