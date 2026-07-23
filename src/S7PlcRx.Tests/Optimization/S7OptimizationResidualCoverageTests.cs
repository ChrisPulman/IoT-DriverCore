// Copyright (c) 2019-2026 Chris Pulman and contributors. All rights reserved.
// Chris Pulman and contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using IoT.DriverCore.S7PlcRx.Optimization;
using S7CpuType = IoT.DriverCore.S7PlcRx.Enums.CpuType;
using S7ErrorCode = IoT.DriverCore.S7PlcRx.Enums.ErrorCode;
using S7Tags = IoT.DriverCore.S7PlcRx.Tags;

namespace IoT.DriverCore.S7PlcRx.Tests.Optimization;

/// <summary>Exercises residual deterministic paths in the S7 optimization implementation.</summary>
public sealed class S7OptimizationResidualCoverageTests
{
    /// <summary>Defines the short interval used solely to activate the engine's batch timer.</summary>
    private const int BatchIntervalMilliseconds = 1;

    /// <summary>Defines the first typed cache value.</summary>
    private const int FirstCachedValue = 17;

    /// <summary>Defines the second typed cache value.</summary>
    private const int SecondCachedValue = 23;

    /// <summary>Defines the number of seconds between cache entries.</summary>
    private const int CacheEntryOffsetSeconds = 10;

    /// <summary>Defines the elapsed minutes used to expire cache entries.</summary>
    private const int CacheExpiryAdvanceMinutes = 2;

    /// <summary>Defines the expected cache entry count in the cache-statistics scenarios.</summary>
    private const int ExpectedCacheEntryCount = 2;

    /// <summary>Defines the denominator for one cache hit across two entries.</summary>
    private const double CacheHitRateDenominator = 3D;

    /// <summary>Defines the second observed integer value.</summary>
    private const int SecondObservedValue = 2;

    /// <summary>Defines the final observed integer value.</summary>
    private const int FinalObservedValue = 5;

    /// <summary>Defines the minimum numeric difference that should produce a monitor event.</summary>
    private const int NumericChangeThreshold = 3;

    /// <summary>Defines the expected numeric amount for the threshold-monitor event.</summary>
    private const double ExpectedNumericChangeAmount = 3D;

    /// <summary>Defines the threshold used to establish nonnumeric comparison behavior.</summary>
    private const int TextChangeThreshold = 10;

    /// <summary>Defines the retained cache value used solely to exercise expiration.</summary>
    private const int ExpiredCachedValue = 3;

    /// <summary>Defines the maximum request count processed by the grouping scenario.</summary>
    private const int GroupingBatchSize = 8;

    /// <summary>Defines the request count processed by the terminal-state scenario.</summary>
    private const int TerminalStateBatchSize = 3;

    /// <summary>Defines the shared cache key and first textual change value.</summary>
    private const string FirstValueName = "first";

    /// <summary>Defines the second textual change value.</summary>
    private const string SecondValueName = "second";

    /// <summary>Defines a cache key that is intentionally never populated.</summary>
    private const string MissingCacheKey = nameof(MissingCacheKey);

    /// <summary>Defines the monitor tag for the zero-threshold scenario.</summary>
    private const string ZeroThresholdTagName = "DB72.DBW0";

    /// <summary>Defines the monitor tag for the numeric-threshold scenario.</summary>
    private const string NumericThresholdTagName = "DB72.DBW2";

    /// <summary>Defines the monitor tag for the nonnumeric-threshold scenario.</summary>
    private const string TextThresholdTagName = "DB72.DBB4";

    /// <summary>Verifies the shared value cache respects value types, clock-controlled expiry, and entry timestamps.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task ValueCacheTracksTypeMismatchExpiryAndTimestampBoundsAsync()
    {
        using var plc = new OptimizationTestPlc("cache-residual");
        var clock = new ManualTimeProvider(new DateTimeOffset(2026, 7, 23, 12, 0, 0, TimeSpan.Zero));
        const string firstTag = "DB71.DBW0";
        const string secondTag = "DB71.DBW2";

        OptimizationExtensions.ClearCache(plc);
        plc.SetSyncValue(firstTag, FirstCachedValue);
        plc.SetSyncValue(secondTag, SecondCachedValue);

        var first = await OptimizationExtensions.ValueCachedAsync(plc, firstTag, -1, TimeSpan.FromMinutes(1), clock);
        clock.Advance(TimeSpan.FromSeconds(CacheEntryOffsetSeconds));
        var second = await OptimizationExtensions.ValueCachedAsync(plc, secondTag, -1, TimeSpan.FromMinutes(1), clock);
        var typeMismatch = await OptimizationExtensions.ValueCachedAsync(plc, firstTag, "fallback", TimeSpan.FromMinutes(1), clock);
        var statistics = OptimizationExtensions.GetCacheStatistics(plc, clock);
        clock.Advance(TimeSpan.FromMinutes(CacheExpiryAdvanceMinutes));
        var expired = await OptimizationExtensions.ValueCachedAsync(plc, firstTag, -1, TimeSpan.FromMinutes(1), clock);

        await TUnit.Assertions.Assert.That(first).IsEqualTo(FirstCachedValue);
        await TUnit.Assertions.Assert.That(second).IsEqualTo(SecondCachedValue);
        await TUnit.Assertions.Assert.That(typeMismatch).IsEqualTo("fallback");
        await TUnit.Assertions.Assert.That(statistics.TotalEntries).IsEqualTo(ExpectedCacheEntryCount);
        await TUnit.Assertions.Assert.That(statistics.TotalHits).IsEqualTo(1L);
        await TUnit.Assertions.Assert.That(statistics.HitRate).IsEqualTo(1D / CacheHitRateDenominator);
        await TUnit.Assertions.Assert.That(statistics.OldestEntry).IsEqualTo(new DateTimeOffset(2026, 7, 23, 12, 0, 0, TimeSpan.Zero));
        await TUnit.Assertions.Assert.That(statistics.NewestEntry).IsEqualTo(new DateTimeOffset(2026, 7, 23, 12, 0, 10, TimeSpan.Zero));
        await TUnit.Assertions.Assert.That(expired).IsEqualTo(FirstCachedValue);

        OptimizationExtensions.ClearCache(plc);
        await TUnit.Assertions.Assert.That(() => OptimizationExtensions.ValueCachedAsync(null!, firstTag, 0, TimeSpan.Zero, clock))
            .Throws<ArgumentNullException>();
        await TUnit.Assertions.Assert.That(() => OptimizationExtensions.ValueCachedAsync(plc, " ", 0, TimeSpan.Zero, clock))
            .Throws<ArgumentException>();
    }

    /// <summary>Verifies smart monitoring accepts only significant numeric and nonnumeric changes.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task SmartMonitorEmitsSignificantNumericAndNonnumericChangesAsync()
    {
        using var plc = new OptimizationTestPlc("monitor-residual");
        var clock = new ManualTimeProvider(new DateTimeOffset(2026, 7, 23, 13, 0, 0, TimeSpan.Zero));
        var zeroThresholdChange = new TaskCompletionSource<SmartTagChange<int>>(TaskCreationOptions.RunContinuationsAsynchronously);
        var numericThresholdChange = new TaskCompletionSource<SmartTagChange<int>>(TaskCreationOptions.RunContinuationsAsynchronously);
        var textChange = new TaskCompletionSource<SmartTagChange<string>>(TaskCreationOptions.RunContinuationsAsynchronously);

        using var zeroThresholdSubscription = OptimizationExtensions.MonitorTagSmart(
                plc,
                ZeroThresholdTagName,
                EqualityComparer<int>.Default,
                changeThreshold: 0,
                debounceMs: BatchIntervalMilliseconds,
                timeProvider: clock)
            .Subscribe(change => zeroThresholdChange.TrySetResult(change));
        using var numericThresholdSubscription = OptimizationExtensions.MonitorTagSmart(
                plc,
                NumericThresholdTagName,
                EqualityComparer<int>.Default,
                changeThreshold: NumericChangeThreshold,
                debounceMs: BatchIntervalMilliseconds,
                timeProvider: clock)
            .Subscribe(change => numericThresholdChange.TrySetResult(change));
        using var textSubscription = OptimizationExtensions.MonitorTagSmart(
                plc,
                TextThresholdTagName,
                EqualityComparer<string>.Default,
                changeThreshold: TextChangeThreshold,
                debounceMs: BatchIntervalMilliseconds,
                timeProvider: clock)
            .Subscribe(change => textChange.TrySetResult(change));

        plc.PublishObservedValue(ZeroThresholdTagName, 1, typeof(int));
        plc.PublishObservedValue(ZeroThresholdTagName, 1, typeof(int));
        plc.PublishObservedValue(ZeroThresholdTagName, SecondObservedValue, typeof(int));
        plc.PublishObservedValue(NumericThresholdTagName, 1, typeof(int));
        plc.PublishObservedValue(NumericThresholdTagName, SecondObservedValue, typeof(int));
        plc.PublishObservedValue(NumericThresholdTagName, FinalObservedValue, typeof(int));
        plc.PublishObservedValue(TextThresholdTagName, FirstValueName, typeof(string));
        plc.PublishObservedValue(TextThresholdTagName, SecondValueName, typeof(string));

        var zeroThreshold = await AsyncCompatibility.WaitAsync(
            zeroThresholdChange.Task,
            TimeSpan.FromSeconds(1));
        var numericThreshold = await AsyncCompatibility.WaitAsync(
            numericThresholdChange.Task,
            TimeSpan.FromSeconds(1));
        var text = await AsyncCompatibility.WaitAsync(
            textChange.Task,
            TimeSpan.FromSeconds(1));

        await TUnit.Assertions.Assert.That(zeroThreshold.PreviousValue).IsEqualTo(1);
        await TUnit.Assertions.Assert.That(zeroThreshold.CurrentValue).IsEqualTo(SecondObservedValue);
        await TUnit.Assertions.Assert.That(zeroThreshold.ChangeAmount).IsEqualTo(1D);
        await TUnit.Assertions.Assert.That(numericThreshold.PreviousValue).IsEqualTo(SecondObservedValue);
        await TUnit.Assertions.Assert.That(numericThreshold.CurrentValue).IsEqualTo(FinalObservedValue);
        await TUnit.Assertions.Assert.That(numericThreshold.ChangeAmount).IsEqualTo(ExpectedNumericChangeAmount);
        await TUnit.Assertions.Assert.That(text.PreviousValue).IsEqualTo(FirstValueName);
        await TUnit.Assertions.Assert.That(text.CurrentValue).IsEqualTo(SecondValueName);
        await TUnit.Assertions.Assert.That(text.ChangeAmount).IsEqualTo(0D);
        await TUnit.Assertions.Assert.That(text.ChangeTime).IsEqualTo(clock.GetUtcNow());
    }

    /// <summary>Verifies batching groups valid and malformed data-block addresses without losing completion signals.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task OptimizationEngineGroupsAddressFormsAndUpdatesCacheAsync()
    {
        var clock = new ManualTimeProvider(new DateTimeOffset(2026, 7, 23, 14, 0, 0, TimeSpan.Zero));
        using var engine = new OptimizationEngine(BatchIntervalMilliseconds, GroupingBatchSize, clock);
        var requests = new[]
        {
            CreateRequest("DB73.DBW0"),
            CreateRequest("DB73.DBW2"),
            CreateRequest(string.Empty),
            CreateRequest("MW0"),
            CreateRequest("DB."),
            CreateRequest("DBbad.DBW0"),
        };

        engine.UpdateCache(FirstValueName, 1);
        clock.Advance(TimeSpan.FromMinutes(1));
        engine.UpdateCache(FirstValueName, SecondObservedValue);
        engine.UpdateCache("expired", ExpiredCachedValue);
        var cached = engine.GetCachedValue(FirstValueName, TimeSpan.FromMinutes(1));
        var missing = engine.GetCachedValue(MissingCacheKey, TimeSpan.FromMinutes(1));
        var beforeBatch = engine.CacheStats;
        foreach (var request in requests)
        {
            engine.EnqueueRequest(request);
        }

        await Task.WhenAll(requests.Select(request => AsyncCompatibility.WaitAsync(
            request.CompletionSource!.Task,
            TimeSpan.FromSeconds(1))));
        clock.Advance(TimeSpan.FromMinutes(CacheExpiryAdvanceMinutes));
        engine.ClearExpiredCache(TimeSpan.FromMinutes(1));

        await TUnit.Assertions.Assert.That(cached).IsEqualTo(SecondObservedValue);
        await TUnit.Assertions.Assert.That(missing).IsNull();
        await TUnit.Assertions.Assert.That(beforeBatch.CachedValueCount).IsEqualTo(ExpectedCacheEntryCount);
        await TUnit.Assertions.Assert.That(beforeBatch.CacheHitRatio).IsEqualTo(1D / CacheHitRateDenominator);
        await TUnit.Assertions.Assert.That(engine.CacheStats.PendingRequestCount).IsEqualTo(0);
        await TUnit.Assertions.Assert.That(engine.CacheStats.CachedValueCount).IsEqualTo(0);
    }

    /// <summary>Verifies an already terminal request cannot prevent later data-block groups from completing.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task OptimizationEngineContinuesAfterCompletedAndCancelledRequestsAsync()
    {
        using var engine = new OptimizationEngine(
            BatchIntervalMilliseconds,
            TerminalStateBatchSize,
            new ManualTimeProvider(TestTime.UnixEpoch));
        var completed = CreateRequest("DB74.DBW0");
        var cancelled = CreateRequest("DB75.DBW0");
        var succeeding = CreateRequest("DB76.DBW0");
        completed.CompletionSource!.SetResult(false);
        cancelled.CompletionSource!.SetCanceled();

        engine.EnqueueRequest(completed);
        engine.EnqueueRequest(cancelled);
        engine.EnqueueRequest(succeeding);

        var result = await AsyncCompatibility.WaitAsync(
            succeeding.CompletionSource!.Task,
            TimeSpan.FromSeconds(1));

        await TUnit.Assertions.Assert.That(completed.CompletionSource.Task.Result).IsFalse();
        await TUnit.Assertions.Assert.That(cancelled.CompletionSource.Task.IsCanceled).IsTrue();
        await TUnit.Assertions.Assert.That(result).IsTrue();
    }

    /// <summary>Verifies the default engine constructor and disposal lifecycle are safe and idempotent.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task OptimizationEngineDefaultLifecycleIsIdempotentAsync()
    {
        var engine = new OptimizationEngine();
        engine.UpdateCache("value", 1);

        ((IDisposable)engine).Dispose();
        ((IDisposable)engine).Dispose();

        await TUnit.Assertions.Assert.That(engine.CacheStats.CachedValueCount).IsEqualTo(0);
    }

    /// <summary>Creates a queued optimization request for a deterministic tag address.</summary>
    /// <param name="address">The tag address used for grouping.</param>
    /// <returns>The newly-created request.</returns>
    private static OptimizedRequest CreateRequest(string address) => new(
        new Tag(address, address, typeof(int)),
        OptimizedRequestType.Read,
        OptimizationRequestPriority.Normal);

    /// <summary>Provides a mutable clock for cache and monitor assertions.</summary>
    /// <param name="utcNow">The initial instant reported by the clock.</param>
    private sealed class ManualTimeProvider(DateTimeOffset utcNow) : TimeProvider
    {
        /// <summary>Stores the mutable current UTC instant.</summary>
        private DateTimeOffset _utcNow = utcNow;

        /// <inheritdoc/>
        public override DateTimeOffset GetUtcNow() => _utcNow;

        /// <summary>Advances the clock by the supplied duration.</summary>
        /// <param name="duration">The duration to add.</param>
        internal void Advance(TimeSpan duration) => _utcNow = _utcNow.Add(duration);
    }

    /// <summary>Implements the small deterministic PLC surface required by optimization tests.</summary>
    /// <param name="ip">The isolated cache namespace for this fake PLC.</param>
    private sealed class OptimizationTestPlc(string ip) : IRxS7
    {
        /// <summary>Stores values returned by typed reads.</summary>
        private readonly Dictionary<string, object?> _values = new(StringComparer.Ordinal);

        /// <summary>Publishes tags to typed observers.</summary>
        private readonly Signal<Tag?> _observedTags = new();

        /// <inheritdoc/>
        public string IP => ip;

        /// <inheritdoc/>
        public IObservable<bool> IsConnected => Observable.Return(true);

        /// <inheritdoc/>
        public bool IsConnectedValue => true;

        /// <inheritdoc/>
        public IObservable<string> LastError => Observable.Empty<string>();

        /// <inheritdoc/>
        public IObservable<S7ErrorCode> LastErrorCode => Observable.Empty<S7ErrorCode>();

        /// <inheritdoc/>
        public IObservable<Tag?> ObserveAll => _observedTags.AsObservable();

        /// <inheritdoc/>
        public S7CpuType PLCType => S7CpuType.S71500;

        /// <inheritdoc/>
        public short Rack => 0;

        /// <inheritdoc/>
        public short Slot => 1;

        /// <inheritdoc/>
        public IObservable<bool> IsPaused => Observable.Return(false);

        /// <inheritdoc/>
        public IObservable<string> Status => Observable.Empty<string>();

        /// <inheritdoc/>
        public S7Tags TagList { get; } = [];

        /// <inheritdoc/>
        public bool ShowWatchDogWriting { get; set; }

        /// <inheritdoc/>
        public string? WatchDogAddress => null;

        /// <inheritdoc/>
        public ushort WatchDogValueToWrite { get; set; }

        /// <inheritdoc/>
        public int WatchDogWritingTime => 0;

        /// <inheritdoc/>
        public IObservable<long> ReadTime => Observable.Empty<long>();

        /// <inheritdoc/>
        public bool IsDisposed { get; private set; }

        /// <inheritdoc/>
        public void Dispose()
        {
            IsDisposed = true;
            _observedTags.Dispose();
        }

        /// <inheritdoc/>
        public IObservable<T?> Observe<T>(LogicalTagKey<T> tag) => _observedTags
            .Where(observedTag => string.Equals(observedTag?.Name, tag.Name, StringComparison.Ordinal))
            .Where(observedTag => observedTag?.Value is T)
            .Select(observedTag => (T?)observedTag!.Value);

        /// <inheritdoc/>
        public Task<T?> ReadAsync<T>(LogicalTagKey<T> tag) => Task.FromResult(
            _values.TryGetValue(tag.Name, out var value) && value is T typed ? typed : default(T));

        /// <inheritdoc/>
        public Task<T?> ReadAsync<T>(LogicalTagKey<T> tag, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return ReadAsync(tag);
        }

        /// <inheritdoc/>
        public void Value<T>(string? variable, T? value) => _values[variable!] = value;

        /// <inheritdoc/>
        public IObservable<string[]> GetCpuInfo() => Observable.Return<string[]>([]);

        /// <summary>Sets the value returned from an uncancelled typed read.</summary>
        /// <param name="tagName">The logical tag name.</param>
        /// <param name="value">The value returned by the fake.</param>
        public void SetSyncValue(string tagName, object? value) => _values[tagName] = value;

        /// <summary>Publishes a typed value to smart-monitor subscribers.</summary>
        /// <param name="tagName">The logical tag name.</param>
        /// <param name="value">The value to publish.</param>
        /// <param name="type">The declared PLC type.</param>
        public void PublishObservedValue(string tagName, object? value, Type type)
        {
            var tag = TagList[tagName] ?? new Tag(tagName, tagName, type);
            tag.Value = value;

            if (TagList[tagName] is null)
            {
                TagList.Add(tag);
            }

            _observedTags.OnNext(tag);
        }
    }
}
