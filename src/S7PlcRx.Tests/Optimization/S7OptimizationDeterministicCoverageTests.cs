// Copyright (c) 2019-2026 Chris Pulman and contributors. All rights reserved.
// Chris Pulman and contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using IoT.DriverCore.S7PlcRx.Optimization;

namespace IoT.DriverCore.S7PlcRx.Tests.Optimization;

/// <summary>Provides deterministic coverage for S7 optimization helpers.</summary>
public sealed class S7OptimizationDeterministicCoverageTests
{
    /// <summary>Defines the cached tag name.</summary>
    private const string CachedTagName = "DB1.DBW0";

    /// <summary>Defines the first cached value.</summary>
    private const int FirstValue = 11;

    /// <summary>Defines the second cached value.</summary>
    private const int SecondValue = 22;

    /// <summary>Defines a tag with no configured PLC value.</summary>
    private const string MissingTagName = "DB1.DBW8";

    /// <summary>Defines the fallback returned for a missing reference-type PLC value.</summary>
    private const string MissingFallback = "fallback";

    /// <summary>Defines the expected number of queued requests.</summary>
    private const int ExpectedRequestCount = 2;

    /// <summary>Defines the fast deterministic engine interval.</summary>
    private const int BatchIntervalMilliseconds = 1;

    /// <summary>Defines the expected cache hit ratio after one hit and one cache entry.</summary>
    private const double ExpectedCacheHitRatio = 0.5D;

    /// <summary>Verifies smart monitoring validates input and produces an observable without a transport connection.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task MonitorTagSmartValidatesArgumentsAndCreatesObservableAsync()
    {
        using var plc = new S7PlcRxAsyncExtensionsTests.TestPlc();
        var monitor = OptimizationExtensions.MonitorTagSmart(
            plc,
            CachedTagName,
            EqualityComparer<int>.Default,
            changeThreshold: 1,
            debounceMs: BatchIntervalMilliseconds,
            timeProvider: TimeProvider.System);

        await TUnit.Assertions.Assert.That(monitor).IsNotNull();
        await TUnit.Assertions.Assert.That(
                () => OptimizationExtensions.MonitorTagSmart(
                    null!,
                    CachedTagName,
                    EqualityComparer<int>.Default,
                    0,
                    BatchIntervalMilliseconds))
            .Throws<ArgumentNullException>();
        await TUnit.Assertions.Assert.That(
                () => OptimizationExtensions.MonitorTagSmart(
                    plc,
                    " ",
                    EqualityComparer<int>.Default,
                    0,
                    BatchIntervalMilliseconds))
            .Throws<ArgumentException>();
    }

    /// <summary>Verifies cache hit, expiration, statistics, and explicit clearing use only in-memory PLC reads.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task ValueCachedAsyncTracksHitsMissesExpiryAndClearAsync()
    {
        using var plc = new S7PlcRxAsyncExtensionsTests.TestPlc();
        OptimizationExtensions.ClearCache(plc);
        plc.SetSyncValue(CachedTagName, FirstValue);

        var miss = await OptimizationExtensions.ValueCachedAsync(
            plc,
            CachedTagName,
            fallbackValue: 0,
            cacheTimeout: TimeSpan.FromMinutes(1));
        var hit = await OptimizationExtensions.ValueCachedAsync(
            plc,
            CachedTagName,
            fallbackValue: 0,
            cacheTimeout: TimeSpan.FromMinutes(1));
        var statistics = OptimizationExtensions.GetCacheStatistics(plc);
        plc.SetSyncValue(CachedTagName, SecondValue);
        var expired = await OptimizationExtensions.ValueCachedAsync(
            plc,
            CachedTagName,
            fallbackValue: 0,
            cacheTimeout: TimeSpan.FromTicks(-1));
        var fallback = await OptimizationExtensions.ValueCachedAsync(
            plc,
            MissingTagName,
            fallbackValue: MissingFallback,
            cacheTimeout: TimeSpan.FromMinutes(1));
        OptimizationExtensions.ClearCache(plc, CachedTagName);
        var clearedStatistics = OptimizationExtensions.GetCacheStatistics(plc);
        OptimizationExtensions.ClearCache(plc);
        var emptyStatistics = OptimizationExtensions.GetCacheStatistics(plc);

        await TUnit.Assertions.Assert.That(miss).IsEqualTo(FirstValue);
        await TUnit.Assertions.Assert.That(hit).IsEqualTo(FirstValue);
        await TUnit.Assertions.Assert.That(plc.SyncReadCount).IsEqualTo(ExpectedRequestCount + 1);
        await TUnit.Assertions.Assert.That(statistics.TotalEntries).IsEqualTo(1);
        await TUnit.Assertions.Assert.That(statistics.TotalHits).IsEqualTo(1L);
        await TUnit.Assertions.Assert.That(expired).IsEqualTo(SecondValue);
        await TUnit.Assertions.Assert.That(fallback).IsEqualTo(MissingFallback);
        await TUnit.Assertions.Assert.That(clearedStatistics.TotalEntries).IsEqualTo(1);
        await TUnit.Assertions.Assert.That(emptyStatistics.TotalEntries).IsEqualTo(0);
        await TUnit.Assertions.Assert.That(() => OptimizationExtensions.ClearCache(null!)).Throws<ArgumentNullException>();
        await TUnit.Assertions.Assert.That(() => OptimizationExtensions.GetCacheStatistics(null!)).Throws<ArgumentNullException>();
    }

    /// <summary>Verifies the optimization engine groups queued requests, tracks cache entries, and disposes safely.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task OptimizationEngineGroupsRequestsCachesValuesAndDisposesAsync()
    {
        using var plc = new S7PlcRxAsyncExtensionsTests.TestPlc();
        var firstTag = new Tag("DB1.DBW0", "DB1.DBW0", typeof(int));
        var secondTag = new Tag("DB2.DBW0", "DB2.DBW0", typeof(int));
        plc.TagList.Add(firstTag);
        plc.TagList.Add(secondTag);
        using var engine = new OptimizationEngine(BatchIntervalMilliseconds, ExpectedRequestCount, TimeProvider.System);
        var firstRequest = new OptimizedRequest(firstTag, OptimizedRequestType.Read, OptimizationRequestPriority.High);
        var secondRequest = new OptimizedRequest(secondTag, OptimizedRequestType.Write, OptimizationRequestPriority.Critical);

        engine.UpdateCache(CachedTagName, FirstValue);
        var cached = engine.GetCachedValue(CachedTagName, TimeSpan.FromMinutes(1));
        var expired = engine.GetCachedValue(CachedTagName, TimeSpan.FromTicks(-1));
        engine.EnqueueRequest(firstRequest);
        engine.EnqueueRequest(secondRequest);
        await Task.WhenAll(
            AsyncCompatibility.WaitAsync(
                firstRequest.CompletionSource!.Task,
                TimeSpan.FromSeconds(1)),
            AsyncCompatibility.WaitAsync(
                secondRequest.CompletionSource!.Task,
                TimeSpan.FromSeconds(1)));
        var statistics = engine.CacheStats;
        engine.ClearExpiredCache(TimeSpan.FromTicks(-1));

        await TUnit.Assertions.Assert.That(cached).IsEqualTo(FirstValue);
        await TUnit.Assertions.Assert.That(expired).IsNull();
        await TUnit.Assertions.Assert.That(firstRequest.RequestType).IsEqualTo(OptimizedRequestType.Read);
        await TUnit.Assertions.Assert.That(secondRequest.Priority).IsEqualTo(OptimizationRequestPriority.Critical);
        await TUnit.Assertions.Assert.That(firstRequest.Timestamp).IsNotEqualTo(default(DateTime));
        await TUnit.Assertions.Assert.That(statistics.CachedValueCount).IsEqualTo(1);
        await TUnit.Assertions.Assert.That(statistics.CacheHitRatio).IsEqualTo(ExpectedCacheHitRatio);
        await TUnit.Assertions.Assert.That(engine.CacheStats.CachedValueCount).IsEqualTo(0);
    }
}
