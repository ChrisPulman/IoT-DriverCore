// Copyright (c) 2019-2026 Chris Pulman and contributors. All rights reserved.
// Chris Pulman and contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using IoT.DriverCore.S7PlcRx.Advanced;
using IoT.DriverCore.S7PlcRx.BatchOperations;
using IoT.DriverCore.S7PlcRx.Cache;
using IoT.DriverCore.S7PlcRx.Core;

namespace IoT.DriverCore.S7PlcRx.Tests.Advanced;

/// <summary>Exercises deterministic, in-memory behavior of small S7 managed utility types.</summary>
public sealed class S7ManagedUtilityResidualCoverageTests
{
    /// <summary>Defines the first dictionary key.</summary>
    private const string FirstKey = "first";

    /// <summary>Defines the second dictionary key.</summary>
    private const string SecondKey = "second";

    /// <summary>Defines the expected request count.</summary>
    private const int ExpectedRequestCount = 2;

    /// <summary>Defines the initial cache hit count.</summary>
    private const long InitialHitCount = 2L;

    /// <summary>Defines the expected cache hit count after one access.</summary>
    private const long ExpectedHitCount = 3L;

    /// <summary>Defines the clock advancement duration in seconds.</summary>
    private const int ClockAdvanceSeconds = 2;

    /// <summary>Defines the batch duration in milliseconds.</summary>
    private const int BatchDurationMilliseconds = 20;

    /// <summary>Defines the average duration per operation in milliseconds.</summary>
    private const double AverageDurationMilliseconds = 10D;

    /// <summary>Defines a deterministic valid logical tag name.</summary>
    private const string UtilityTagName = "UtilityTag";

    /// <summary>Defines the tag used to produce an in-memory asynchronous read failure.</summary>
    private const string FailingTagName = "FailingUtilityTag";

    /// <summary>Defines the utility value used only to infer generic type parameters.</summary>
    private const int UtilityValue = 7;

    /// <summary>Verifies dictionary comparisons cover identity, null, count, key, value, and hash behavior.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task DictionaryEqualityComparerComparesDictionariesAndObjectInputsAsync()
    {
        var comparer = new DictionaryEqualityComparer<string, string?>();
        var values = new Dictionary<string, string?> { [FirstKey] = "one", [SecondKey] = null };
        var equalValues = new Dictionary<string, string?> { [SecondKey] = null, [FirstKey] = "one" };
        var differentCount = new Dictionary<string, string?> { [FirstKey] = "one" };
        var missingKey = new Dictionary<string, string?> { ["other"] = "one", [SecondKey] = null };
        var differentValue = new Dictionary<string, string?> { [FirstKey] = "two", [SecondKey] = null };
        System.Collections.IEqualityComparer objectComparer = comparer;
        var valuesHash = comparer.GetHashCode(values);
        var equalValuesHash = comparer.GetHashCode(equalValues);
        object valuesObject = values;

        await TUnit.Assertions.Assert.That(comparer.Equals(values, values)).IsTrue();
        await TUnit.Assertions.Assert.That(comparer.Equals(null, null)).IsTrue();
        await TUnit.Assertions.Assert.That(comparer.Equals(values, null)).IsFalse();
        await TUnit.Assertions.Assert.That(comparer.Equals(values, equalValues)).IsTrue();
        await TUnit.Assertions.Assert.That(comparer.Equals(values, differentCount)).IsFalse();
        await TUnit.Assertions.Assert.That(comparer.Equals(values, missingKey)).IsFalse();
        await TUnit.Assertions.Assert.That(comparer.Equals(values, differentValue)).IsFalse();
        await TUnit.Assertions.Assert.That(valuesHash).IsEqualTo(equalValuesHash);
        await TUnit.Assertions.Assert.That(objectComparer.Equals(values, equalValues)).IsTrue();
        await TUnit.Assertions.Assert.That(objectComparer.Equals(values, "not a dictionary")).IsFalse();
        await TUnit.Assertions.Assert.That(objectComparer.GetHashCode(valuesObject)).IsEqualTo(valuesHash);
        await TUnit.Assertions.Assert.That(objectComparer.GetHashCode("not a dictionary")).IsEqualTo(0);
    }

    /// <summary>Verifies queue draining, request timestamps, and priorities have no transport dependency.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task BatchRequestQueueDrainsRequestsInOrderWithConfiguredTimestampsAsync()
    {
        var time = new FixedTimeProvider(new DateTimeOffset(2026, 7, 23, 12, 0, 0, TimeSpan.Zero));
        var firstTag = new Tag("First", "DB1.DBW0", typeof(int));
        var secondTag = new Tag("Second", "DB1.DBW2", typeof(int));
        var queue = new BatchRequestQueue();
        var first = new BatchRequest(BatchRequestType.Read, firstTag, RequestPriority.High, time);
        var second = new BatchRequest(BatchRequestType.Write, secondTag, RequestPriority.Low, time);

        queue.Enqueue(first);
        queue.Enqueue(second);
        var drained = queue.DequeueAll();

        await TUnit.Assertions.Assert.That(queue.Count).IsEqualTo(0);
        await TUnit.Assertions.Assert.That(queue.IsEmpty).IsTrue();
        await TUnit.Assertions.Assert.That(drained.Count).IsEqualTo(ExpectedRequestCount);
        await TUnit.Assertions.Assert.That(drained[0].Type).IsEqualTo(BatchRequestType.Read);
        await TUnit.Assertions.Assert.That(drained[0].Tag).IsSameReferenceAs(firstTag);
        await TUnit.Assertions.Assert.That(drained[0].Priority).IsEqualTo(RequestPriority.High);
        await TUnit.Assertions.Assert.That(drained[0].Timestamp).IsEqualTo(time.GetUtcNow().UtcDateTime);
        await TUnit.Assertions.Assert.That(drained[1].Type).IsEqualTo(BatchRequestType.Write);
        await TUnit.Assertions.Assert.That(drained[1].Priority).IsEqualTo(RequestPriority.Low);
        await TUnit.Assertions.Assert.That(queue.DequeueAll()).IsEmpty();
    }

    /// <summary>Verifies cached values use the supplied clock and retain mutable hit counts.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task CachedValueTracksValuesHitCountsAndExpirationAgainstProvidedTimeAsync()
    {
        var timestamp = new DateTime(2026, 7, 23, 12, 0, 0, DateTimeKind.Utc);
        var clock = new FixedTimeProvider(new DateTimeOffset(timestamp));
        var cached = new CachedValue("value", timestamp, hitCount: InitialHitCount);

        await TUnit.Assertions.Assert.That(cached.Value).IsEqualTo("value");
        await TUnit.Assertions.Assert.That(cached.Timestamp).IsEqualTo(timestamp);
        await TUnit.Assertions.Assert.That(cached.HitCount).IsEqualTo(InitialHitCount);
        await TUnit.Assertions.Assert.That(cached.IsExpired(TimeSpan.Zero, clock)).IsFalse();
        cached.HitCount++;
        clock.Advance(TimeSpan.FromSeconds(ClockAdvanceSeconds));

        await TUnit.Assertions.Assert.That(cached.HitCount).IsEqualTo(ExpectedHitCount);
        await TUnit.Assertions.Assert.That(cached.IsExpired(TimeSpan.FromSeconds(1), clock)).IsTrue();
    }

    /// <summary>Verifies public batch result summaries reflect populated success state and elapsed time.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task BatchResultSummariesReflectSuccessfulAndFailedOperationsAsync()
    {
        var start = new DateTimeOffset(2026, 7, 23, 12, 0, 0, TimeSpan.Zero);
        var operation = new BatchOperationResult
        {
            StartTime = start,
            EndTime = start.AddMilliseconds(BatchDurationMilliseconds),
            OperationCount = ExpectedRequestCount,
            SuccessfulOperations = 1,
            FailedOperations = 1,
        };
        var noOperations = new BatchOperationResult { StartTime = start, EndTime = start.AddMilliseconds(1) };
        var read = new BatchReadResult<int>();
        read.Success["good"] = true;
        read.Success["bad"] = false;
        var write = new BatchWriteResult();
        write.Success["good"] = true;
        write.Success["bad"] = false;

        await TUnit.Assertions.Assert.That(operation.ProcessingTime).IsEqualTo(TimeSpan.FromMilliseconds(BatchDurationMilliseconds));
        await TUnit.Assertions.Assert.That(operation.AverageTimePerOperation).IsEqualTo(AverageDurationMilliseconds);
        await TUnit.Assertions.Assert.That(noOperations.AverageTimePerOperation).IsEqualTo(0D);
        await TUnit.Assertions.Assert.That(read.SuccessCount).IsEqualTo(1);
        await TUnit.Assertions.Assert.That(read.ErrorCount).IsEqualTo(1);
        await TUnit.Assertions.Assert.That(write.SuccessCount).IsEqualTo(1);
        await TUnit.Assertions.Assert.That(write.ErrorCount).IsEqualTo(1);
    }

    /// <summary>Verifies async read and write guards fail deterministically before any PLC transport work begins.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task AsyncExtensionsRejectNullDependenciesAndCanceledBatchOperationsAsync()
    {
        using var plc = new S7PlcRxAsyncExtensionsTests.TestPlc();
        IReadOnlyList<string>? missingVariables = null;
        IReadOnlyDictionary<string, int>? missingValues = null;
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        await TUnit.Assertions.Assert.That(
                async () => await AsyncExtensions.ReadValueAsync(
                    null!,
                    UtilityValue,
                    UtilityTagName,
                    CancellationToken.None).AsTask())
            .Throws<ArgumentNullException>();
        await TUnit.Assertions.Assert.That(
                async () => await AsyncExtensions.ReadValuesAsync(
                    null!,
                    UtilityValue,
                    [UtilityTagName],
                    CancellationToken.None).AsTask())
            .Throws<ArgumentNullException>();
        await TUnit.Assertions.Assert.That(
                async () => await AsyncExtensions.ReadValuesAsync(
                    plc,
                    UtilityValue,
                    missingVariables!,
                    CancellationToken.None).AsTask())
            .Throws<ArgumentNullException>();
        await TUnit.Assertions.Assert.That(
                async () => await AsyncExtensions.ReadValuesAsync(
                    plc,
                    UtilityValue,
                    [UtilityTagName],
                    cancellation.Token).AsTask())
            .Throws<OperationCanceledException>();
        await TUnit.Assertions.Assert.That(
                async () => await AsyncExtensions.WriteValuesAsync(
                    null!,
                    new Dictionary<string, int>(),
                    CancellationToken.None).AsTask())
            .Throws<ArgumentNullException>();
        await TUnit.Assertions.Assert.That(
                async () => await AsyncExtensions.WriteValuesAsync(
                    plc,
                    missingValues!,
                    CancellationToken.None).AsTask())
            .Throws<ArgumentNullException>();

        var emptyWrite = AsyncExtensions.WriteValuesAsync(
            plc,
            new Dictionary<string, int>(),
            CancellationToken.None);

        await TUnit.Assertions.Assert.That(emptyWrite.IsCompleted).IsTrue();
    }

    /// <summary>Verifies the async wrappers preserve successful reads and propagate deterministic underlying failures.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task AsyncExtensionsPreserveSuccessfulAndFailedReadOutcomesAsync()
    {
        using var plc = new S7PlcRxAsyncExtensionsTests.TestPlc();
        plc.SetSyncValue(UtilityTagName, UtilityValue);
        plc.SetAsyncFactory(
            FailingTagName,
            static _ => Task.FromException<object?>(new InvalidOperationException("deterministic utility failure")));
        using var cancellation = new CancellationTokenSource();

        var successfulValue = await AsyncExtensions.ReadValueAsync(
            plc,
            UtilityValue,
            UtilityTagName,
            CancellationToken.None).AsTask();

        await TUnit.Assertions.Assert.That(successfulValue).IsEqualTo(UtilityValue);
        await TUnit.Assertions.Assert.That(
                async () => await AsyncExtensions.ReadValuesAsync(
                    plc,
                    UtilityValue,
                    [FailingTagName],
                    cancellation.Token).AsTask())
            .Throws<InvalidOperationException>();
    }

#if NET8_0_OR_GREATER
    /// <summary>Verifies async observable guards reject a missing PLC before creating any subscription.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task AsyncObservableExtensionsRejectNullPlcAsync()
    {
        await TUnit.Assertions.Assert.That(
                () => AsyncExtensions.ObserveValue(null!, UtilityValue, UtilityTagName))
            .Throws<ArgumentNullException>();
        await TUnit.Assertions.Assert.That(
                () => AsyncExtensions.ObserveValues(null!, UtilityValue, UtilityTagName))
            .Throws<ArgumentNullException>();
    }
#endif

    /// <summary>Provides a mutable deterministic clock for timestamp-only utility tests.</summary>
    /// <param name="utcNow">The initial UTC instant.</param>
    private sealed class FixedTimeProvider(DateTimeOffset utcNow) : TimeProvider
    {
        /// <summary>Stores the current UTC instant.</summary>
        private DateTimeOffset _utcNow = utcNow;

        /// <inheritdoc/>
        public override DateTimeOffset GetUtcNow() => _utcNow;

        /// <summary>Advances the clock by the supplied duration.</summary>
        /// <param name="duration">The duration to add.</param>
        internal void Advance(TimeSpan duration) => _utcNow = _utcNow.Add(duration);
    }
}
