// Copyright (c) 2019-2026 Chris Pulman and contributors. All rights reserved.
// Chris Pulman and contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using IoT.DriverCore.S7PlcRx.Enums;
using IoT.DriverCore.S7PlcRx.Optimization;
using IoT.DriverCore.S7PlcRx.Performance;

namespace IoT.DriverCore.S7PlcRx.Tests.Performance;

/// <summary>Provides deterministic residual coverage for S7 performance helpers.</summary>
[NotInParallel]
public sealed class S7PerformanceResidualCoverageTests
{
    /// <summary>Defines the common first data-block address.</summary>
    private const string FirstAddress = "DB1.DBW0";

    /// <summary>Defines the second data-block address.</summary>
    private const string SecondAddress = "DB2.DBW0";

    /// <summary>Defines the first logical tag name.</summary>
    private const string FirstTagName = "First";

    /// <summary>Defines the second logical tag name.</summary>
    private const string SecondTagName = "Second";

    /// <summary>Defines the system-memory logical tag name.</summary>
    private const string SystemTagName = "System";

    /// <summary>Defines the empty-address logical tag name.</summary>
    private const string EmptyAddressTagName = "EmptyAddress";

    /// <summary>Defines the short data-block logical tag name.</summary>
    private const string ShortDbTagName = "ShortDb";

    /// <summary>Defines a tag name excluded from the group.</summary>
    private const string IgnoredTagName = "Ignored";

    /// <summary>Defines the expected tag and snapshot count.</summary>
    private const int ExpectedPairCount = 2;

    /// <summary>Defines the first deterministic read value.</summary>
    private const int FirstReadValue = 11;

    /// <summary>Defines the second deterministic read value.</summary>
    private const int SecondReadValue = 22;

    /// <summary>Defines the system-memory deterministic read value.</summary>
    private const int SystemReadValue = 33;

    /// <summary>Defines the empty-address deterministic read value.</summary>
    private const int EmptyAddressReadValue = 44;

    /// <summary>Defines the short data-block deterministic read value.</summary>
    private const int ShortDbReadValue = 55;

    /// <summary>Defines the first deterministic write and observed value.</summary>
    private const int FirstWriteValue = 10;

    /// <summary>Defines the second deterministic write and observed value.</summary>
    private const int SecondWriteValue = 20;

    /// <summary>Defines the deliberately mismatched read-back value.</summary>
    private const int MismatchedReadBackValue = 99;

    /// <summary>Defines the ignored observed value.</summary>
    private const int IgnoredObservedValue = 9;

    /// <summary>Defines the filtered group write value.</summary>
    private const int GroupWriteValue = 30;

    /// <summary>Defines the ignored group write value.</summary>
    private const int IgnoredWriteValue = 40;

    /// <summary>Defines the expected benchmark error count.</summary>
    private const int ExpectedBenchmarkErrorCount = 3;

    /// <summary>Defines the maximum benchmark score.</summary>
    private const int MaximumBenchmarkScore = 100;

    /// <summary>Defines the final retained-sample input.</summary>
    private const int LastSampleIndex = 100;

    /// <summary>Defines the total recorded sample count.</summary>
    private const int ExpectedOperationCount = 101;

    /// <summary>Defines the rate-measurement duration in seconds.</summary>
    private const int RateDurationSeconds = 10;

    /// <summary>Defines the expected retained-sample average.</summary>
    private const double ExpectedAverageResponseTime = 50.5;

    /// <summary>Defines the expected operation rate.</summary>
    private const double ExpectedOperationsPerSecond = 10.1;

    /// <summary>Defines the operation-and-error denominator.</summary>
    private const double OperationAndErrorCount = 102.0;

    /// <summary>Defines the connection uptime in minutes.</summary>
    private const int ConnectionUptimeMinutes = 3;

    /// <summary>Defines the tag read-operation count.</summary>
    private const int TagReadOperationCount = 4;

    /// <summary>Defines the tag write-operation count.</summary>
    private const int TagWriteOperationCount = 3;

    /// <summary>Defines the average tag read duration.</summary>
    private const double TagAverageReadTime = 1.25;

    /// <summary>Defines the average tag write duration.</summary>
    private const double TagAverageWriteTime = 2.5;

    /// <summary>Defines the successful tag-operation count.</summary>
    private const double SuccessfulTagOperationCount = 6.0;

    /// <summary>Defines the total tag-operation count.</summary>
    private const double TotalTagOperationCount = 7.0;

    /// <summary>Verifies monitored metrics count active tags and use the supplied clock.</summary>
    /// <returns>A task representing the asynchronous test operation.</returns>
    [Test]
    public async Task MonitorPerformanceReportsActiveTagsWithDeterministicTimestampAsync()
    {
        var timestamp = new DateTimeOffset(2026, 7, 23, 12, 0, 0, TimeSpan.Zero);
        var clock = new ManualTimeProvider(timestamp);
        using var plc = new RecordingPlc("performance-monitor");
        plc.TagList.Add(new Tag("Active", FirstAddress, 1, typeof(int)));
        var inactive = new Tag("Inactive", "DB1.DBW2", ExpectedPairCount, typeof(int));
        inactive.SetDoNotPoll(true);
        plc.TagList.Add(inactive);

        var metrics = await PerformanceExtensions
            .MonitorPerformance(plc, null, clock)
            .Take(1)
            .FirstAsync();

        await TUnit.Assertions.Assert.That(metrics.PLCIdentifier).IsEqualTo("performance-monitor_S71500");
        await TUnit.Assertions.Assert.That(metrics.Timestamp).IsEqualTo(timestamp);
        await TUnit.Assertions.Assert.That(metrics.TagCount).IsEqualTo(ExpectedPairCount);
        await TUnit.Assertions.Assert.That(metrics.ActiveTagCount).IsEqualTo(1);
        await TUnit.Assertions.Assert.That(metrics.IsConnected).IsTrue();
        await TUnit.Assertions.Assert.That(metrics.ConnectionUptime).IsGreaterThanOrEqualTo(TimeSpan.Zero);
        await TUnit.Assertions.Assert.That(
            () => PerformanceExtensions.MonitorPerformance(null!, TimeSpan.FromSeconds(1), clock))
            .Throws<ArgumentNullException>();
    }

    /// <summary>Verifies sequential reads, address grouping, inter-group delay, and read failures.</summary>
    /// <returns>A task representing the asynchronous test operation.</returns>
    [Test]
    public async Task OptimizedReadsCoverSequentialGroupsEmptyInputAndFailuresAsync()
    {
        using var plc = new RecordingPlc("performance-read");
        plc.AddTag(FirstTagName, FirstAddress, FirstReadValue);
        plc.AddTag(SecondTagName, SecondAddress, SecondReadValue);
        plc.AddTag(SystemTagName, "M0.0", SystemReadValue);
        plc.AddTag(EmptyAddressTagName, null, EmptyAddressReadValue);
        plc.AddTag(ShortDbTagName, "DB.", ShortDbReadValue);
        var config = new ReadOptimizationConfig
        {
            EnableParallelReads = false,
            InterGroupDelayMs = 1,
        };

        var values = await PerformanceExtensions.ReadOptimizedAsync(
            plc,
            [FirstTagName, SecondTagName, SystemTagName, EmptyAddressTagName, ShortDbTagName],
            0,
            config);
        var empty = await PerformanceExtensions.ReadOptimizedAsync(plc, [], 0, config);

        await TUnit.Assertions.Assert.That(values[FirstTagName]).IsEqualTo(FirstReadValue);
        await TUnit.Assertions.Assert.That(values[SecondTagName]).IsEqualTo(SecondReadValue);
        await TUnit.Assertions.Assert.That(values[SystemTagName]).IsEqualTo(SystemReadValue);
        await TUnit.Assertions.Assert.That(values[EmptyAddressTagName]).IsEqualTo(EmptyAddressReadValue);
        await TUnit.Assertions.Assert.That(values[ShortDbTagName]).IsEqualTo(ShortDbReadValue);
        await TUnit.Assertions.Assert.That(empty.Count).IsEqualTo(0);

        _ = plc.ThrowReads.Add(SecondTagName);
        Func<Task> failedRead = async () => _ = await PerformanceExtensions.ReadOptimizedAsync(
            plc,
            [FirstTagName, SecondTagName],
            0,
            new ReadOptimizationConfig { EnableParallelReads = true });
        Func<Task> nullTags = async () => _ = await PerformanceExtensions.ReadOptimizedAsync(
            plc,
            null!,
            0,
            config);

        await TUnit.Assertions.Assert.That(failedRead).Throws<InvalidOperationException>();
        await TUnit.Assertions.Assert.That(nullTags).Throws<ArgumentNullException>();

        var statistics = PerformanceExtensions.GetPerformanceStatistics(plc, new ManualTimeProvider(
            new DateTimeOffset(2026, 7, 23, 12, 1, 0, TimeSpan.Zero)));
        await TUnit.Assertions.Assert.That(statistics.TotalErrors).IsGreaterThanOrEqualTo(1);
    }

    /// <summary>Verifies parallel writes, read-back verification, failures, and outer grouping errors.</summary>
    /// <returns>A task representing the asynchronous test operation.</returns>
    [Test]
    public async Task OptimizedWritesCoverParallelVerificationAndFailureResultsAsync()
    {
        var clock = new ManualTimeProvider(new DateTimeOffset(2026, 7, 23, 13, 0, 0, TimeSpan.Zero));
        using var plc = new RecordingPlc("performance-write");
        plc.AddTag(FirstTagName, FirstAddress, 1);
        plc.AddTag(SecondTagName, SecondAddress, ExpectedPairCount);
        var verified = await PerformanceExtensions.WriteOptimizedAsync(
            plc,
            new Dictionary<string, int>
            {
                [FirstTagName] = FirstWriteValue,
                [SecondTagName] = SecondWriteValue,
            },
            new WriteOptimizationConfig
            {
                EnableParallelWrites = true,
                VerifyWrites = true,
                InterGroupDelayMs = 1,
            },
            clock);

        await TUnit.Assertions.Assert.That(verified.SuccessfulWrites.Count).IsEqualTo(ExpectedPairCount);
        await TUnit.Assertions.Assert.That(verified.FailedWrites.Count).IsEqualTo(0);
        await TUnit.Assertions.Assert.That(verified.OverallError).IsNull();

        plc.EchoWritesToReads = false;
        plc.ReadValues[FirstTagName] = MismatchedReadBackValue;
        var mismatch = await PerformanceExtensions.WriteOptimizedAsync(
            plc,
            new Dictionary<string, int> { [FirstTagName] = FirstWriteValue },
            new WriteOptimizationConfig { VerifyWrites = true },
            clock);
        await TUnit.Assertions.Assert.That(mismatch.FailedWrites.ContainsKey(FirstTagName)).IsTrue();
        await TUnit.Assertions.Assert.That(mismatch.FailedWrites[FirstTagName])
            .Contains("Write verification failed");
    }

    /// <summary>Verifies missing read-back values, write failures, grouping errors, and null input.</summary>
    /// <returns>A task representing the asynchronous test operation.</returns>
    [Test]
    public async Task OptimizedWritesCoverMissingReadBackAndGroupingFailuresAsync()
    {
        var clock = new ManualTimeProvider(new DateTimeOffset(2026, 7, 23, 13, 30, 0, TimeSpan.Zero));
        using var plc = new RecordingPlc("performance-write-failures")
        {
            EchoWritesToReads = false,
        };
        plc.AddTag(FirstTagName, FirstAddress, 1);
        plc.AddTag(SecondTagName, SecondAddress, ExpectedPairCount);

        _ = plc.ReadValues.Remove(SecondTagName);
        var missingReadBack = await PerformanceExtensions.WriteOptimizedAsync(
            plc,
            new Dictionary<string, int> { [SecondTagName] = SecondWriteValue },
            new WriteOptimizationConfig { VerifyWrites = true },
            clock);
        await TUnit.Assertions.Assert.That(missingReadBack.FailedWrites.ContainsKey(SecondTagName)).IsTrue();

        _ = plc.ThrowWrites.Add(FirstTagName);
        var writeFailure = await PerformanceExtensions.WriteOptimizedAsync(
            plc,
            new Dictionary<string, int> { [FirstTagName] = FirstWriteValue },
            new WriteOptimizationConfig(),
            clock);
        await TUnit.Assertions.Assert.That(writeFailure.FailedWrites.ContainsKey(FirstTagName)).IsTrue();

        using var groupingFailurePlc = new RecordingPlc("performance-write-grouping")
        {
            ThrowOnTagListAccess = true,
        };
        var groupingFailure = await PerformanceExtensions.WriteOptimizedAsync(
            groupingFailurePlc,
            new Dictionary<string, int> { [FirstTagName] = 1 },
            new WriteOptimizationConfig(),
            clock);
        Func<Task> nullValues = async () => _ = await PerformanceExtensions.WriteOptimizedAsync<int>(
            plc,
            null!,
            new WriteOptimizationConfig(),
            clock);

        await TUnit.Assertions.Assert.That(groupingFailure.OverallError).Contains("Tag list unavailable");
        await TUnit.Assertions.Assert.That(nullValues).Throws<ArgumentNullException>();
    }

    /// <summary>Verifies benchmark error aggregation and empty latency statistics.</summary>
    /// <returns>A task representing the asynchronous test operation.</returns>
    [Test]
    public async Task BenchmarkAggregatesLatencyThroughputAndReliabilityErrorsAsync()
    {
        var clock = new ManualTimeProvider(new DateTimeOffset(2026, 7, 23, 14, 0, 0, TimeSpan.Zero));
        using var plc = new RecordingPlc("performance-benchmark")
        {
            CpuInfoFactory = () => Observable.Throw<string[]>(new InvalidOperationException("CPU unavailable")),
        };
        var result = await PerformanceExtensions.RunBenchmarkAsync(
            plc,
            new BenchmarkConfig
            {
                LatencyTestCount = 1,
                ThroughputTestDuration = TimeSpan.FromSeconds(1),
                ReliabilityTestCount = 1,
            },
            clock);

        await TUnit.Assertions.Assert.That(result.Errors.Count).IsEqualTo(ExpectedBenchmarkErrorCount);
        await TUnit.Assertions.Assert.That(result.Errors[0]).Contains("Latency test 1 failed");
        await TUnit.Assertions.Assert.That(result.Errors[1]).Contains("Throughput test failed");
        await TUnit.Assertions.Assert.That(result.Errors[2]).Contains("Reliability test 1 failed");
        await TUnit.Assertions.Assert.That(result.AverageLatencyMs).IsEqualTo(0);
        await TUnit.Assertions.Assert.That(result.ReliabilityRate).IsEqualTo(0);

        Func<Task> nullPlc = async () => _ = await PerformanceExtensions.RunBenchmarkAsync(
            null!,
            new BenchmarkConfig(),
            clock);
        await TUnit.Assertions.Assert.That(nullPlc).Throws<ArgumentNullException>();
    }

    /// <summary>Verifies a successful minimal benchmark calculates statistics and a bounded score.</summary>
    /// <returns>A task representing the asynchronous test operation.</returns>
    [Test]
    public async Task BenchmarkCalculatesSuccessfulStatisticsAsync()
    {
        var clock = new ManualTimeProvider(new DateTimeOffset(2026, 7, 23, 14, 30, 0, TimeSpan.Zero));
        using var plc = new RecordingPlc("performance-benchmark-success");
        var result = await PerformanceExtensions.RunBenchmarkAsync(
            plc,
            new BenchmarkConfig
            {
                LatencyTestCount = 1,
                ThroughputTestDuration = TimeSpan.Zero,
                ReliabilityTestCount = 1,
            },
            clock);

        await TUnit.Assertions.Assert.That(result.Errors.Count).IsEqualTo(0);
        await TUnit.Assertions.Assert.That(result.ReliabilityRate).IsEqualTo(1);
        await TUnit.Assertions.Assert.That(result.MinLatencyMs).IsGreaterThanOrEqualTo(0);
        await TUnit.Assertions.Assert.That(result.MaxLatencyMs).IsGreaterThanOrEqualTo(result.MinLatencyMs);
        await TUnit.Assertions.Assert.That(result.OverallScore).IsGreaterThanOrEqualTo(0);
        await TUnit.Assertions.Assert.That(result.OverallScore).IsLessThanOrEqualTo(MaximumBenchmarkScore);
    }

    /// <summary>Verifies group observation, filtering, polling, writing, and idempotent disposal.</summary>
    /// <returns>A task representing the asynchronous test operation.</returns>
    [Test]
    public async Task HighPerformanceGroupObservesChangesFiltersWritesAndDisposesTwiceAsync()
    {
        using var plc = new S7PlcRxAsyncExtensionsTests.TestPlc();
        plc.TagList.Add(new Tag(FirstTagName, FirstAddress, 1, typeof(int)));
        plc.TagList.Add(new Tag(SecondTagName, "DB1.DBW2", ExpectedPairCount, typeof(int)));
        var group = new HighPerformanceTagGroup<int>(plc, "Observed", [FirstTagName, SecondTagName]);
        var snapshots = new List<Dictionary<string, int>>();
        using var subscription = group.ObserveGroup().Subscribe(snapshots.Add);

        plc.ObserveAllSubject.OnNext(new Tag { Name = null, Value = 1 });
        plc.PublishObservedValue(IgnoredTagName, IgnoredObservedValue, typeof(int));
        plc.PublishObservedValue(FirstTagName, "wrong type", typeof(string));
        plc.PublishObservedValue(FirstTagName, FirstWriteValue, typeof(int));
        plc.PublishObservedValue(SecondTagName, SecondWriteValue, typeof(int));
        await group.WriteAllAsync(new Dictionary<string, int>
        {
            [FirstTagName] = GroupWriteValue,
            [IgnoredTagName] = IgnoredWriteValue,
        });

        await TUnit.Assertions.Assert.That(group.GroupName).IsEqualTo("Observed");
        await TUnit.Assertions.Assert.That(group.CurrentValues.Count).IsEqualTo(0);
        await TUnit.Assertions.Assert.That(snapshots.Count).IsEqualTo(ExpectedPairCount);
        await TUnit.Assertions.Assert.That(snapshots[0][FirstTagName]).IsEqualTo(FirstWriteValue);
        await TUnit.Assertions.Assert.That(snapshots[1][SecondTagName]).IsEqualTo(SecondWriteValue);
        await TUnit.Assertions.Assert.That(plc.WrittenValues[FirstTagName]).IsEqualTo(GroupWriteValue);
        await TUnit.Assertions.Assert.That(plc.WrittenValues.ContainsKey(IgnoredTagName)).IsFalse();
        await TUnit.Assertions.Assert.That(plc.TagList[FirstTagName]!.DoNotPoll).IsFalse();

        group.Dispose();
        group.Dispose();

        await TUnit.Assertions.Assert.That(plc.TagList[FirstTagName]!.DoNotPoll).IsTrue();
        await TUnit.Assertions.Assert.That(plc.TagList[SecondTagName]!.DoNotPoll).IsTrue();
    }

    /// <summary>Verifies disposal attempts to remove an address tag added by a group.</summary>
    [Test]
    public void HighPerformanceGroupDisposesAnAutoAddedAddressTag()
    {
        const string address = "DB99.DBW0";
        using var plc = new S7PlcRxAsyncExtensionsTests.TestPlc();
        var group = new HighPerformanceTagGroup<int>(plc, "Auto", [address]);
        plc.TagList.Add(new Tag(address, address, 1, typeof(int)));

        group.Dispose();
        group.Dispose();
    }

    /// <summary>Verifies counters retain their recent window and calculate rate, average, and error ratio.</summary>
    /// <returns>A task representing the asynchronous test operation.</returns>
    [Test]
    public async Task PerformanceCounterRetainsRecentSamplesAndRecordsErrorsAsync()
    {
        var clock = new ManualTimeProvider(new DateTimeOffset(2026, 7, 23, 15, 0, 0, TimeSpan.Zero));
        var counter = new PerformanceCounter(clock);

        await TUnit.Assertions.Assert.That(counter.GetOperationsPerSecond()).IsEqualTo(0);
        await TUnit.Assertions.Assert.That(counter.GetAverageResponseTime()).IsEqualTo(0);
        await TUnit.Assertions.Assert.That(counter.GetErrorRate()).IsEqualTo(0);

        for (var index = 0; index <= LastSampleIndex; index++)
        {
            counter.RecordOperation(TimeSpan.FromMilliseconds(index));
        }

        counter.RecordError();
        clock.Advance(TimeSpan.FromSeconds(RateDurationSeconds));

        await TUnit.Assertions.Assert.That(counter.TotalOperations).IsEqualTo(ExpectedOperationCount);
        await TUnit.Assertions.Assert.That(counter.TotalErrors).IsEqualTo(1);
        await TUnit.Assertions.Assert.That(counter.GetAverageResponseTime()).IsEqualTo(ExpectedAverageResponseTime);
        await TUnit.Assertions.Assert.That(counter.GetOperationsPerSecond()).IsEqualTo(ExpectedOperationsPerSecond);
        await TUnit.Assertions.Assert.That(counter.GetErrorRate()).IsEqualTo(1.0 / OperationAndErrorCount);
    }

    /// <summary>Verifies connection and public tag metrics expose their complete state.</summary>
    /// <returns>A task representing the asynchronous test operation.</returns>
    [Test]
    public async Task ConnectionAndTagMetricsExposeRecordedStateAsync()
    {
        var timestamp = new DateTimeOffset(2026, 7, 23, 16, 0, 0, TimeSpan.Zero);
        var clock = new ManualTimeProvider(timestamp);
        var connection = new SimpleConnectionMetrics(clock);
        connection.RecordReconnection();
        clock.Advance(TimeSpan.FromMinutes(ConnectionUptimeMinutes));
        var tag = new TagPerformanceMetrics
        {
            TagName = FirstAddress,
            ReadOperations = TagReadOperationCount,
            WriteOperations = TagWriteOperationCount,
            AverageReadTimeMs = TagAverageReadTime,
            AverageWriteTimeMs = TagAverageWriteTime,
            FailedOperations = 1,
            SuccessRate = SuccessfulTagOperationCount / TotalTagOperationCount,
            LastOperationTime = timestamp,
        };

        await TUnit.Assertions.Assert.That(connection.ReconnectionCount).IsEqualTo(1);
        await TUnit.Assertions.Assert.That(connection.GetUptime())
            .IsEqualTo(TimeSpan.FromMinutes(ConnectionUptimeMinutes));
        await TUnit.Assertions.Assert.That(tag.TagName).IsEqualTo(FirstAddress);
        await TUnit.Assertions.Assert.That(tag.ReadOperations).IsEqualTo(TagReadOperationCount);
        await TUnit.Assertions.Assert.That(tag.WriteOperations).IsEqualTo(TagWriteOperationCount);
        await TUnit.Assertions.Assert.That(tag.AverageReadTimeMs).IsEqualTo(TagAverageReadTime);
        await TUnit.Assertions.Assert.That(tag.AverageWriteTimeMs).IsEqualTo(TagAverageWriteTime);
        await TUnit.Assertions.Assert.That(tag.FailedOperations).IsEqualTo(1);
        await TUnit.Assertions.Assert.That(tag.SuccessRate)
            .IsEqualTo(SuccessfulTagOperationCount / TotalTagOperationCount);
        await TUnit.Assertions.Assert.That(tag.LastOperationTime).IsEqualTo(timestamp);
    }

    /// <summary>Provides a manually advanced UTC clock.</summary>
    /// <param name="utcNow">The initial UTC time.</param>
    private sealed class ManualTimeProvider(DateTimeOffset utcNow) : TimeProvider
    {
        /// <summary>Stores the current UTC time.</summary>
        private DateTimeOffset _utcNow = utcNow;

        /// <inheritdoc/>
        public override DateTimeOffset GetUtcNow() => _utcNow;

        /// <summary>Advances the current time.</summary>
        /// <param name="duration">The duration to advance.</param>
        public void Advance(TimeSpan duration) => _utcNow = _utcNow.Add(duration);
    }

    /// <summary>Implements a deterministic in-memory S7 PLC for performance tests.</summary>
    /// <param name="ip">The deterministic PLC identifier.</param>
    private sealed class RecordingPlc(string ip) : IRxS7
    {
        /// <summary>Stores registered tags.</summary>
        private readonly global::IoT.DriverCore.S7PlcRx.Tags _tagList = [];

        /// <summary>Publishes observed values.</summary>
        private readonly Signal<Tag?> _updates = new();

        /// <summary>Gets values returned by reads.</summary>
        public Dictionary<string, object?> ReadValues { get; } = new(StringComparer.Ordinal);

        /// <summary>Gets tag names whose reads throw.</summary>
        public HashSet<string> ThrowReads { get; } = new(StringComparer.Ordinal);

        /// <summary>Gets tag names whose writes throw.</summary>
        public HashSet<string> ThrowWrites { get; } = new(StringComparer.Ordinal);

        /// <summary>Gets recorded writes.</summary>
        public Dictionary<string, object?> WrittenValues { get; } = new(StringComparer.Ordinal);

        /// <summary>Gets or sets whether writes update subsequent read values.</summary>
        public bool EchoWritesToReads { get; set; } = true;

        /// <summary>Gets or sets whether tag-list access throws.</summary>
        public bool ThrowOnTagListAccess { get; set; }

        /// <summary>Gets or sets the CPU-information sequence factory.</summary>
        public Func<IObservable<string[]>> CpuInfoFactory { get; set; } =
            () => Observable.Return<string[]>([]);

        /// <inheritdoc/>
        public string IP { get; } = ip;

        /// <inheritdoc/>
        public IObservable<bool> IsConnected => Observable.Return(true);

        /// <inheritdoc/>
        public bool IsConnectedValue => true;

        /// <inheritdoc/>
        public IObservable<string> LastError => Observable.Empty<string>();

        /// <inheritdoc/>
        public IObservable<ErrorCode> LastErrorCode => Observable.Empty<ErrorCode>();

        /// <inheritdoc/>
        public IObservable<Tag?> ObserveAll => _updates.AsObservable();

        /// <inheritdoc/>
        public CpuType PLCType => CpuType.S71500;

        /// <inheritdoc/>
        public short Rack => 0;

        /// <inheritdoc/>
        public short Slot => 1;

        /// <inheritdoc/>
        public IObservable<bool> IsPaused => Observable.Return(false);

        /// <inheritdoc/>
        public IObservable<string> Status => Observable.Empty<string>();

        /// <inheritdoc/>
        public global::IoT.DriverCore.S7PlcRx.Tags TagList =>
            ThrowOnTagListAccess ? throw new InvalidOperationException("Tag list unavailable") : _tagList;

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

        /// <summary>Adds a readable tag with optional address metadata.</summary>
        /// <param name="name">The logical tag name.</param>
        /// <param name="address">The optional PLC address.</param>
        /// <param name="value">The value returned by reads.</param>
        public void AddTag(string name, string? address, object value)
        {
            _tagList.Add(new Tag(name, address ?? string.Empty, value, value.GetType()) { Address = address });
            ReadValues[name] = value;
        }

        /// <inheritdoc/>
        public void Dispose()
        {
            if (IsDisposed)
            {
                return;
            }

            IsDisposed = true;
            _updates.Dispose();
        }

        /// <inheritdoc/>
        public IObservable<T?> Observe<T>(LogicalTagKey<T> tag) => _updates
            .Where(item => string.Equals(item?.Name, tag.Name, StringComparison.Ordinal))
            .Where(item => item?.Value is T)
            .Select(item => (T?)item!.Value);

        /// <inheritdoc/>
        public Task<T?> ReadAsync<T>(LogicalTagKey<T> tag)
        {
            if (ThrowReads.Contains(tag.Name))
            {
                return Task.FromException<T?>(new InvalidOperationException($"Read failed for {tag.Name}"));
            }

            return Task.FromResult(
                ReadValues.TryGetValue(tag.Name, out var value) && value is T typed ? typed : default);
        }

        /// <inheritdoc/>
        public Task<T?> ReadAsync<T>(LogicalTagKey<T> tag, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return ReadAsync(tag);
        }

        /// <inheritdoc/>
        public void Value<T>(string? variable, T? value)
        {
            var requiredVariable = variable ?? throw new ArgumentNullException(nameof(variable));
            if (ThrowWrites.Contains(requiredVariable))
            {
                throw new InvalidOperationException($"Write failed for {requiredVariable}");
            }

            WrittenValues[requiredVariable] = value;
            if (!EchoWritesToReads)
            {
                return;
            }

            ReadValues[requiredVariable] = value;
        }

        /// <inheritdoc/>
        public IObservable<string[]> GetCpuInfo() => CpuInfoFactory();
    }
}
