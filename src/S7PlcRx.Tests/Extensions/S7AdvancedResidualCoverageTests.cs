// Copyright (c) 2019-2026 Chris Pulman and contributors. All rights reserved.
// Chris Pulman and contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using IoT.DriverCore.S7PlcRx.Advanced;
using IoT.DriverCore.S7PlcRx.Core;
using IoT.DriverCore.S7PlcRx.Enterprise;
using IoT.DriverCore.S7PlcRx.Enums;
using IoT.DriverCore.S7PlcRx.Production;
#if NET8_0_OR_GREATER
using ReactiveUI.Primitives;
using ReactiveUI.Primitives.Async;
#endif

namespace IoT.DriverCore.S7PlcRx.Tests.Extensions;

/// <summary>Exercises the reachable residual branches in the advanced S7 extension surface.</summary>
[NotInParallel]
public sealed class S7AdvancedResidualCoverageTests
{
    /// <summary>Defines the first deterministic tag.</summary>
    private const string FirstTag = "DB1.DBW0";

    /// <summary>Defines the second deterministic tag.</summary>
    private const string SecondTag = "DB2.DBW0";

    /// <summary>Defines the non-data-block tag name.</summary>
    private const string MarkerTag = "Marker";

    /// <summary>Defines the mismatched observed value.</summary>
    private const short MismatchedObservedValue = 2;

    /// <summary>Defines the first observed value.</summary>
    private const int FirstObservedValue = 3;

    /// <summary>Defines the second observed value.</summary>
    private const int SecondObservedValue = 4;

    /// <summary>Defines the expected number of emitted snapshots.</summary>
    private const int ExpectedSnapshotCount = 2;

    /// <summary>Defines the successful optimized-read value.</summary>
    private const int SuccessfulReadValue = 17;

    /// <summary>Defines the optimized-read timeout.</summary>
    private const int ReadTimeoutMilliseconds = 15;

    /// <summary>Defines the original value captured before writing.</summary>
    private const int OriginalWriteValue = 7;

    /// <summary>Defines the first requested write.</summary>
    private const int FirstWriteValue = 11;

    /// <summary>Defines the second requested write.</summary>
    private const int SecondWriteValue = 12;

    /// <summary>Defines the diagnostic tag count above the recommendation threshold.</summary>
    private const int LoadedTagCount = 201;

    /// <summary>Defines the number of inactive diagnostic tags.</summary>
    private const int InactiveTagCount = 100;

    /// <summary>Defines the simulated high connection latency.</summary>
    private const int HighLatencyMilliseconds = 600;

    /// <summary>Defines the expected number of diagnostic recommendations.</summary>
    private const int ExpectedDiagnosticRecommendationCount = 3;

    /// <summary>Defines the performance observation interval.</summary>
    private const int PerformanceObservationMilliseconds = 35;

    /// <summary>Defines the expected number of distinct tag changes.</summary>
    private const int ExpectedChangeCount = 2;

    /// <summary>Defines the expected number of production validation checks.</summary>
    private const int ExpectedValidationTestCount = 3;

    /// <summary>Defines the number of mapped enterprise data types.</summary>
    private const int EnterpriseDataTypeCount = 9;

    /// <summary>Defines the number of deterministic timestamps supplied to production validation.</summary>
    private const int ValidationTimestampCount = 12;

    /// <summary>Defines the timer interval used by deterministic health checks.</summary>
    private const int HealthCheckIntervalMilliseconds = 15;

    /// <summary>Defines the maximum time allowed for a deterministic health check.</summary>
    private const int HealthCheckTimeoutMilliseconds = 1000;

    /// <summary>Defines the interval between deterministic condition checks.</summary>
    private const int ConditionCheckIntervalMilliseconds = 5;

    /// <summary>Defines the deterministic CPU-query failure message.</summary>
    private const string CpuQueryFailureMessage = "CPU query failed";

#if NET8_0_OR_GREATER
    /// <summary>Defines the timeout used while awaiting asynchronous notifications.</summary>
    private static readonly TimeSpan ObservationTimeout = TimeSpan.FromSeconds(2);
#endif

    /// <summary>Verifies observation validation, automatic registration, filtering, change detection, and termination.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task ObserveBatchCoversRegistrationFilteringChangesAndTerminationAsync()
    {
        await TUnit.Assertions.Assert.That(
            () => AdvancedExtensions.ObserveBatch(null!, 0, FirstTag))
            .Throws<ArgumentNullException>();

        using var plc = new DeterministicPlc();
        var observer = new RecordingObserver<Dictionary<string, int>>();
        using var subscription = AdvancedExtensions.ObserveBatch(plc, 0, FirstTag, SecondTag)
            .Subscribe(observer);

        plc.Publish(null);
        plc.Publish(new Tag("Ignored", "DB9.DBW0", typeof(int)) { Value = 1 });
        plc.Publish(new Tag(FirstTag, FirstTag, typeof(short)) { Value = MismatchedObservedValue });
        plc.Publish(new Tag(FirstTag, FirstTag, typeof(int)) { Value = FirstObservedValue });
        plc.Publish(new Tag(FirstTag, FirstTag, typeof(int)) { Value = FirstObservedValue });
        plc.Publish(new Tag(SecondTag, SecondTag, typeof(int)) { Value = SecondObservedValue });
        var expectedError = new InvalidOperationException("observable failed");
        plc.FailObservations(expectedError);

        await TUnit.Assertions.Assert.That(plc.TagList.ContainsKey(FirstTag)).IsTrue();
        await TUnit.Assertions.Assert.That(plc.TagList.ContainsKey(SecondTag)).IsTrue();
        await TUnit.Assertions.Assert.That(observer.Values.Count).IsEqualTo(ExpectedSnapshotCount);
        await TUnit.Assertions.Assert.That(observer.Values[1][FirstTag]).IsEqualTo(FirstObservedValue);
        await TUnit.Assertions.Assert.That(observer.Values[1][SecondTag]).IsEqualTo(SecondObservedValue);
        await TUnit.Assertions.Assert.That(observer.Error).IsSameReferenceAs(expectedError);

        using var completingPlc = new DeterministicPlc();
        var completingObserver = new RecordingObserver<Dictionary<string, int>>();
        using var completingSubscription = AdvancedExtensions.ObserveBatch(completingPlc, 0, FirstTag)
            .Subscribe(completingObserver);
        completingPlc.CompleteObservations();
        await TUnit.Assertions.Assert.That(completingObserver.WasCompleted).IsTrue();
    }

    /// <summary>Verifies the batch wrappers preserve their documented null and empty-input behavior.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task BatchWrappersCoverNullAndEmptyGuardsAsync()
    {
        Func<Task> nullRead = async () =>
            _ = await AdvancedExtensions.ValueBatchAsync(null!, 0, FirstTag).ConfigureAwait(false);
        Func<Task> nullWrite = () => AdvancedExtensions.ValueBatchAsync(
            null!,
            new Dictionary<string, int> { [FirstTag] = 1 });
        Func<Task> nullOptimizedWrite = async () =>
            _ = await AdvancedExtensions.WriteBatchOptimizedAsync(
                null!,
                new Dictionary<string, int>(),
                false,
                false)
                .ConfigureAwait(false);

        await AdvancedExtensions.ValueBatchAsync(null!, new Dictionary<string, int>()).ConfigureAwait(false);

        await TUnit.Assertions.Assert.That(nullRead).Throws<ArgumentNullException>();
        await TUnit.Assertions.Assert.That(nullWrite).Throws<ArgumentNullException>();
        await TUnit.Assertions.Assert.That(nullOptimizedWrite).Throws<ArgumentNullException>();
    }

    /// <summary>Verifies optimized reads distinguish successful, timed-out, and faulted tags across memory areas.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task OptimizedReadReportsTimeoutAndFaultWithoutTransportAsync()
    {
        using var plc = new DeterministicPlc
        {
            CancellableRead = static async (name, cancellationToken) =>
            {
                if (name == FirstTag)
                {
                    await Task.Delay(Timeout.Infinite, cancellationToken).ConfigureAwait(false);
                }

                if (name == SecondTag)
                {
                    throw new InvalidOperationException("deterministic read failure");
                }

                return SuccessfulReadValue;
            },
        };
        var mappings = new Dictionary<string, string>
        {
            [FirstTag] = FirstTag,
            [SecondTag] = SecondTag,
            [MarkerTag] = "M0.0",
        };

        var result = await AdvancedExtensions.ReadBatchOptimizedAsync(plc, 0, mappings, ReadTimeoutMilliseconds)
            .ConfigureAwait(false);

        await TUnit.Assertions.Assert.That(result.OverallSuccess).IsFalse();
        await TUnit.Assertions.Assert.That(result.Errors[FirstTag]).IsEqualTo("Operation timed out");
        await TUnit.Assertions.Assert.That(result.Errors[SecondTag]).Contains("deterministic read failure");
        await TUnit.Assertions.Assert.That(result.Values[MarkerTag]).IsEqualTo(SuccessfulReadValue);
        await TUnit.Assertions.Assert.That(result.Success[MarkerTag]).IsTrue();
    }

    /// <summary>Verifies original-value capture, write, and rollback exceptions are reported per tag.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task OptimizedWriteReportsCaptureWriteAndRollbackFailuresAsync()
    {
        var writeCounts = new Dictionary<string, int>(StringComparer.InvariantCultureIgnoreCase);
        using var plc = new DeterministicPlc
        {
            Read = static name => name == FirstTag
                ? throw new InvalidOperationException("capture failure")
                : OriginalWriteValue,
            Write = (name, _) =>
            {
                writeCounts[name] = writeCounts.TryGetValue(name, out var count) ? count + 1 : 1;
                if (name != SecondTag)
                {
                    return;
                }

                throw new InvalidOperationException(
                    writeCounts[name] == 1 ? "write failure" : "rollback failure");
            },
        };

        var result = await AdvancedExtensions.WriteBatchOptimizedAsync(
            plc,
            new Dictionary<string, int>
            {
                [FirstTag] = FirstWriteValue,
                [SecondTag] = SecondWriteValue,
            },
            verifyWrites: false,
            enableRollback: true).ConfigureAwait(false);

        await TUnit.Assertions.Assert.That(result.OverallSuccess).IsFalse();
        await TUnit.Assertions.Assert.That(result.RollbackPerformed).IsTrue();
        await TUnit.Assertions.Assert.That(result.Errors[FirstTag]).Contains("capture failure");
        await TUnit.Assertions.Assert.That(result.Errors[SecondTag]).Contains("Rollback failed");
        await TUnit.Assertions.Assert.That(result.Success[SecondTag]).IsFalse();
    }

    /// <summary>Verifies diagnostics record collection faults and all production recommendations.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task DiagnosticsCoverFaultsSystemTagsAndRecommendationsAsync()
    {
        using var failingPlc = new DeterministicPlc
        {
            CpuInformation = static () => throw new InvalidOperationException("diagnostic failure"),
        };
        var failed = await AdvancedExtensions.GetDiagnosticsAsync(failingPlc).ConfigureAwait(false);

        using var nonFifteenHundred = new DeterministicPlc { Cpu = CpuType.S7300 };
        var otherCpu = await AdvancedExtensions.GetDiagnosticsAsync(nonFifteenHundred).ConfigureAwait(false);

        using var loadedPlc = new DeterministicPlc();
        for (var index = 0; index < LoadedTagCount; index++)
        {
            var address = index == 0 ? "M0.0" : $"DB{index}.DBW0";
            loadedPlc.TagList.Add(new Tag($"Tag{index}", address, typeof(int))
            {
                DoNotPoll = index < InactiveTagCount,
            });
        }

        var origin = new DateTimeOffset(2026, 7, 23, 12, 0, 0, TimeSpan.Zero);
        var clock = new SequenceTimeProvider(origin, origin, origin.AddMilliseconds(HighLatencyMilliseconds));
        var diagnostics = await AdvancedExtensions.GetDiagnosticsAsync(loadedPlc, clock).ConfigureAwait(false);

        await TUnit.Assertions.Assert.That(failed.Errors.Single()).Contains("diagnostic failure");
        await TUnit.Assertions.Assert.That(otherCpu.CPUInformation.Count).IsEqualTo(0);
        await TUnit.Assertions.Assert.That(diagnostics.ConnectionLatencyMs).IsEqualTo((double)HighLatencyMilliseconds);
        await TUnit.Assertions.Assert.That(diagnostics.TagMetrics.DataBlockDistribution["SYSTEM"]).IsEqualTo(1);
        await TUnit.Assertions.Assert.That(diagnostics.Recommendations.Count)
            .IsEqualTo(ExpectedDiagnosticRecommendationCount);
    }

    /// <summary>Verifies live analysis ignores null/duplicate updates and recommends batching fast-changing tags.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task PerformanceAnalysisTracksOnlyChangesAndCoversZeroDurationAsync()
    {
        await TUnit.Assertions.Assert.That(
            async () => _ = await AdvancedExtensions.AnalyzePerformanceAsync(
                null!,
                TimeSpan.Zero,
                TimeProvider.System).ConfigureAwait(false))
            .Throws<ArgumentNullException>();

        var plc = new DeterministicPlc();
        var analysisTask = AdvancedExtensions.AnalyzePerformanceAsync(
            plc,
            TimeSpan.FromMilliseconds(PerformanceObservationMilliseconds),
            TimeProvider.System);
        plc.Publish(null);
        plc.Publish(new Tag("Fast", FirstTag, typeof(int)) { Value = 1 });
        plc.Publish(new Tag("Fast", FirstTag, typeof(int)) { Value = 1 });
        plc.Publish(new Tag("Fast", FirstTag, typeof(int)) { Value = ExpectedChangeCount });
        var analysis = await analysisTask.ConfigureAwait(false);
        var zeroDuration = await AdvancedExtensions.AnalyzePerformanceAsync(
            plc,
            TimeSpan.Zero,
            TimeProvider.System).ConfigureAwait(false);
        plc.Dispose();

        await TUnit.Assertions.Assert.That(analysis.TotalTagChanges).IsEqualTo(ExpectedChangeCount);
        await TUnit.Assertions.Assert.That(analysis.TagChangeFrequencies["Fast"]).IsEqualTo(ExpectedChangeCount);
        await TUnit.Assertions.Assert.That(analysis.AverageChangesPerTag).IsEqualTo((double)ExpectedChangeCount);
        await TUnit.Assertions.Assert.That(analysis.Recommendations.Single()).Contains("fast-changing");
        await TUnit.Assertions.Assert.That(zeroDuration.Recommendations.Count).IsEqualTo(0);
    }

    /// <summary>Verifies async helpers validate null names and honor cancellation before any read or write.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task AsyncHelpersCoverNullNameAndCancellationGuardsAsync()
    {
        using var plc = new DeterministicPlc();
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        Func<Task> nullName = async () =>
            _ = await AsyncExtensions.ReadValueAsync(plc, 0, null, CancellationToken.None).ConfigureAwait(false);
        Func<Task> canceledRead = async () =>
            _ = await AsyncExtensions.ReadValuesAsync(
                plc,
                0,
                [FirstTag],
                cancellation.Token).ConfigureAwait(false);
        Func<Task> canceledWrite = async () =>
            await AsyncExtensions.WriteValuesAsync(
                plc,
                new Dictionary<string, int> { [FirstTag] = 1 },
                cancellation.Token).ConfigureAwait(false);

        await TUnit.Assertions.Assert.That(nullName).Throws<ArgumentNullException>();
        await TUnit.Assertions.Assert.That(canceledRead).Throws<OperationCanceledException>();
        await TUnit.Assertions.Assert.That(canceledWrite).Throws<OperationCanceledException>();
    }

    /// <summary>Verifies production validation default routing, exception capture, and slow-response reporting.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task ProductionValidationCoversDefaultsFailuresAndSlowResponsesAsync()
    {
        using var defaultPlc = new DeterministicPlc();
        var defaultResult = await ProductionExtensions.ValidateProductionReadinessAsync(defaultPlc)
            .ConfigureAwait(false);

        using var failingPlc = new DeterministicPlc
        {
            CpuInformation = static () => throw new InvalidOperationException(CpuQueryFailureMessage),
        };
        var failureConfig = new ProductionValidationConfig
        {
            ReliabilityTestCount = 1,
            MinimumReliabilityRate = 1,
        };
        var failureResult = await ProductionExtensions.ValidateProductionReadinessAsync(
            failingPlc,
            failureConfig,
            TimeProvider.System).ConfigureAwait(false);

        using var slowPlc = new DeterministicPlc();
        var origin = new DateTimeOffset(2026, 7, 23, 13, 0, 0, TimeSpan.Zero);
        var timestamps = Enumerable.Range(0, ValidationTimestampCount)
            .Select(index => origin.AddMilliseconds(index * HighLatencyMilliseconds))
            .ToArray();
        var slowConfig = new ProductionValidationConfig
        {
            MaxAcceptableResponseTime = TimeSpan.FromMilliseconds(1),
            ReliabilityTestCount = 1,
            MinimumReliabilityRate = 1,
        };
        var slowResult = await ProductionExtensions.ValidateProductionReadinessAsync(
            slowPlc,
            slowConfig,
            new SequenceTimeProvider(timestamps)).ConfigureAwait(false);

        using var outerFailurePlc = new DeterministicPlc();
        var outerFailure = await ProductionExtensions.ValidateProductionReadinessAsync(
            outerFailurePlc,
            failureConfig,
            new FaultAtCallTimeProvider(origin, ExpectedSnapshotCount)).ConfigureAwait(false);

        await TUnit.Assertions.Assert.That(defaultResult.ValidationTests.Count)
            .IsEqualTo(ExpectedValidationTestCount);
        await TUnit.Assertions.Assert.That(failureResult.ValidationTests[0].ErrorMessage)
            .Contains(CpuQueryFailureMessage);
        await TUnit.Assertions.Assert.That(failureResult.ValidationTests[1].ErrorMessage)
            .Contains(CpuQueryFailureMessage);
        await TUnit.Assertions.Assert.That(slowResult.ValidationTests[1].Success).IsFalse();
        await TUnit.Assertions.Assert.That(slowResult.ValidationTests[1].ErrorMessage)
            .Contains("exceeds maximum");
        await TUnit.Assertions.Assert.That(outerFailure.IsProductionReady).IsFalse();
        await TUnit.Assertions.Assert.That(outerFailure.CriticalErrors.Single()).Contains("clock failure");
    }

    /// <summary>Verifies enterprise default factories and all residual symbol type mappings without connecting.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task EnterpriseDefaultsMapSymbolsAndCreateDisconnectedManagersAsync()
    {
        using var plc = new DeterministicPlc();
        const string csv =
            "BoolValue,DB1.DBX0.0,BOOL\n" +
            "ByteValue,DB1.DBB1,BYTE\n" +
            "DwordValue,DB1.DBD2,DWORD\n" +
            "IntValue,DB1.DBW6,INT\n" +
            "LrealValue,DB1.DBD8,LREAL\n" +
            "StringValue,DB1.DBB16,STRING,8\n" +
            "ArrayValue,DB1.DBB24,ARRAY[0..3] OF BYTE,4\n" +
            "UnknownValue,DB1.DBB28,CUSTOM\n" +
            "WordValue,DB1.DBW30,WORD";

        var table = await EnterpriseExtensions.LoadSymbolTableAsync(plc, csv).ConfigureAwait(false);

        using var backup = new DeterministicPlc();
        using var manager = EnterpriseExtensions.CreateHighAvailabilityConnection(
            plc,
            [backup]);

        using var pool = EnterpriseExtensions.CreateConnectionPool(
            [
                new PlcConnectionConfig
                {
                    PLCType = CpuType.S71500,
                    IPAddress = "127.0.0.1",
                    Rack = 0,
                    Slot = 1,
                },
            ],
            new ConnectionPoolConfig { MaxConnections = 1 },
            new SequenceTimeProvider(new DateTimeOffset(2026, 7, 23, 14, 0, 0, TimeSpan.Zero)));

        await TUnit.Assertions.Assert.That(table.Symbols.Count).IsEqualTo(EnterpriseDataTypeCount);
        await TUnit.Assertions.Assert.That(table.Symbols["BoolValue"].DataType).IsEqualTo("BOOL");
        await TUnit.Assertions.Assert.That(table.Symbols["ArrayValue"].DataType).Contains("ARRAY");
        await TUnit.Assertions.Assert.That(manager.ActivePLC).IsSameReferenceAs(plc);
        await TUnit.Assertions.Assert.That(pool.AllConnections.Count()).IsEqualTo(1);
    }

    /// <summary>Verifies timer-driven health checks cover healthy, failed, and recovered failover paths.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task HighAvailabilityTimerCoversHealthyAndRecoveredHealthChecksAsync()
    {
        using var healthy = new DeterministicPlc();
        using var healthyManager = new HighAvailabilityPlcManager(
            healthy,
            [],
            TimeSpan.FromMilliseconds(HealthCheckIntervalMilliseconds));
        await Task.Delay(HealthCheckIntervalMilliseconds * ExpectedChangeCount).ConfigureAwait(false);

        var connectionChecks = 0;
        using var recoveringPrimary = new DeterministicPlc
        {
            ConnectedState = () =>
            {
                connectionChecks++;
                return connectionChecks == 1
                    ? throw new InvalidOperationException("health probe failed")
                    : false;
            },
        };
        using var connectedBackup = new DeterministicPlc();
        using var manager = new HighAvailabilityPlcManager(
            recoveringPrimary,
            [connectedBackup],
            TimeSpan.FromMilliseconds(HealthCheckIntervalMilliseconds));
        var observer = new RecordingObserver<PlcFailoverEvent>();
        using var subscription = manager.FailoverEvents.Subscribe(observer);

        await WaitUntilAsync(
            () => observer.Values.Count > 0,
            TimeSpan.FromMilliseconds(HealthCheckTimeoutMilliseconds)).ConfigureAwait(false);
        manager.Dispose();

        await TUnit.Assertions.Assert.That(manager.ActivePLC).IsSameReferenceAs(connectedBackup);
        await TUnit.Assertions.Assert.That(observer.Values.Single().Reason).Contains("Health check failed");
    }

#if NET8_0_OR_GREATER
    /// <summary>Verifies async observable adapters forward errors and completion and release subscriptions.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task AsyncObservableForwardsErrorCompletionAndDisposalAsync()
    {
        using var errorPlc = new DeterministicPlc();
        var errorObserver = new RecordingAsyncObserver<int>();
        await using (var subscription = await AsyncExtensions.ObserveValue(errorPlc, 0, FirstTag)
            .SubscribeAsync(errorObserver, CancellationToken.None).ConfigureAwait(false))
        {
            var expected = new InvalidOperationException("async observable failure");
            errorPlc.FailObservations(expected);
            var actual = await errorObserver.Error.Task.WaitAsync(ObservationTimeout).ConfigureAwait(false);
            await TUnit.Assertions.Assert.That(actual).IsSameReferenceAs(expected);
        }

        using var completionPlc = new DeterministicPlc();
        var completionObserver = new RecordingAsyncObserver<int>();
        await using (var subscription = await AsyncExtensions.ObserveValue(completionPlc, 0, FirstTag)
            .SubscribeAsync(completionObserver, CancellationToken.None).ConfigureAwait(false))
        {
            completionPlc.CompleteObservations();
            await completionObserver.Completed.Task.WaitAsync(ObservationTimeout).ConfigureAwait(false);
        }

        await TUnit.Assertions.Assert.That(completionObserver.WasCompleted).IsTrue();
    }
#endif

    /// <summary>Waits until a deterministic condition becomes true.</summary>
    /// <param name="condition">The condition to inspect.</param>
    /// <param name="timeout">The maximum wait duration.</param>
    /// <returns>A task that represents the asynchronous wait.</returns>
    private static async Task WaitUntilAsync(Func<bool> condition, TimeSpan timeout)
    {
        using var cancellation = new CancellationTokenSource(timeout);
        while (!condition())
        {
            await Task.Delay(ConditionCheckIntervalMilliseconds, cancellation.Token).ConfigureAwait(false);
        }
    }

#if NET8_0_OR_GREATER
    /// <summary>Records asynchronous source notifications deterministically.</summary>
    /// <typeparam name="T">The observed value type.</typeparam>
    private sealed class RecordingAsyncObserver<T> : IObserverAsync<T>
    {
        /// <summary>Gets the signal completed by a source completion notification.</summary>
        public TaskCompletionSource<bool> Completed { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        /// <summary>Gets the signal completed by a source error notification.</summary>
        public TaskCompletionSource<Exception> Error { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        /// <summary>Gets a value indicating whether the source completed.</summary>
        public bool WasCompleted { get; private set; }

        /// <inheritdoc />
        public ValueTask DisposeAsync() => ValueTask.CompletedTask;

        /// <inheritdoc />
        public ValueTask OnCompletedAsync(Result result)
        {
            WasCompleted = true;
            _ = Completed.TrySetResult(true);
            return ValueTask.CompletedTask;
        }

        /// <inheritdoc />
        public ValueTask OnErrorResumeAsync(Exception error, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            _ = Error.TrySetResult(error);
            return ValueTask.CompletedTask;
        }

        /// <inheritdoc />
        public ValueTask OnNextAsync(T value, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return ValueTask.CompletedTask;
        }
    }
#endif

    /// <summary>Records synchronous observable notifications.</summary>
    /// <typeparam name="T">The observed value type.</typeparam>
    private sealed class RecordingObserver<T> : IObserver<T>
    {
        /// <summary>Gets the received values.</summary>
        public List<T> Values { get; } = [];

        /// <summary>Gets the received error.</summary>
        public Exception? Error { get; private set; }

        /// <summary>Gets a value indicating whether completion was received.</summary>
        public bool WasCompleted { get; private set; }

        /// <inheritdoc />
        public void OnCompleted() => WasCompleted = true;

        /// <inheritdoc />
        public void OnError(Exception error) => Error = error;

        /// <inheritdoc />
        public void OnNext(T value) => Values.Add(value);
    }

    /// <summary>Provides a deterministic, transport-free PLC implementation.</summary>
    private sealed class DeterministicPlc : IRxS7
    {
        /// <summary>Publishes deterministic tag notifications.</summary>
        private readonly Signal<Tag?> _observations = new();

        /// <summary>Gets the optional cancellable read implementation.</summary>
        public Func<string, CancellationToken, Task<object?>>? CancellableRead { get; init; }

        /// <summary>Gets the optional synchronous read implementation.</summary>
        public Func<string, object?>? Read { get; init; }

        /// <summary>Gets the optional write implementation.</summary>
        public Action<string, object?>? Write { get; init; }

        /// <summary>Gets the optional CPU-information observable factory.</summary>
        public Func<IObservable<string[]>>? CpuInformation { get; init; }

        /// <summary>Gets the simulated CPU type.</summary>
        public CpuType Cpu { get; init; } = CpuType.S71500;

        /// <summary>Gets the optional dynamic connection-state implementation.</summary>
        public Func<bool>? ConnectedState { get; init; }

        /// <inheritdoc />
        public string IP => "127.0.0.1";

        /// <inheritdoc />
        public IObservable<bool> IsConnected => Observable.Return(IsConnectedValue);

        /// <inheritdoc />
        public bool IsConnectedValue => ConnectedState?.Invoke() ?? true;

        /// <inheritdoc />
        public bool IsDisposed { get; private set; }

        /// <inheritdoc />
        public IObservable<string> LastError => Observable.Empty<string>();

        /// <inheritdoc />
        public IObservable<ErrorCode> LastErrorCode => Observable.Empty<ErrorCode>();

        /// <inheritdoc />
        public IObservable<Tag?> ObserveAll => _observations.AsObservable();

        /// <inheritdoc />
        public CpuType PLCType => Cpu;

        /// <inheritdoc />
        public short Rack => 0;

        /// <inheritdoc />
        public short Slot => 1;

        /// <inheritdoc />
        public IObservable<bool> IsPaused => Observable.Return(false);

        /// <inheritdoc />
        public IObservable<string> Status => Observable.Empty<string>();

        /// <inheritdoc />
        public global::IoT.DriverCore.S7PlcRx.Tags TagList { get; } = [];

        /// <inheritdoc />
        public bool ShowWatchDogWriting { get; set; }

        /// <inheritdoc />
        public string? WatchDogAddress => null;

        /// <inheritdoc />
        public ushort WatchDogValueToWrite { get; set; }

        /// <inheritdoc />
        public int WatchDogWritingTime => 0;

        /// <inheritdoc />
        public IObservable<long> ReadTime => Observable.Empty<long>();

        /// <summary>Completes the deterministic observation stream.</summary>
        public void CompleteObservations() => _observations.OnCompleted();

        /// <inheritdoc />
        public void Dispose()
        {
            if (IsDisposed)
            {
                return;
            }

            IsDisposed = true;
            _observations.Dispose();
        }

        /// <summary>Faults the deterministic observation stream.</summary>
        /// <param name="error">The error to publish.</param>
        public void FailObservations(Exception error) => _observations.OnError(error);

        /// <inheritdoc />
        public IObservable<string[]> GetCpuInfo() =>
            CpuInformation?.Invoke() ?? Observable.Return((string[])["SIMULATED"]);

        /// <inheritdoc />
        public IObservable<T?> Observe<T>(LogicalTagKey<T> tag) => ObserveAll
            .Where(candidate => string.Equals(candidate?.Name, tag.Name, StringComparison.InvariantCultureIgnoreCase))
            .Where(candidate => candidate?.Value is T)
            .Select(candidate => (T?)candidate!.Value);

        /// <summary>Publishes a deterministic tag update.</summary>
        /// <param name="tag">The tag update to publish.</param>
        public void Publish(Tag? tag) => _observations.OnNext(tag);

        /// <inheritdoc />
        public Task<T?> ReadAsync<T>(LogicalTagKey<T> tag)
        {
            var value = Read?.Invoke(tag.Name);
            return Task.FromResult(value is T typed ? typed : default);
        }

        /// <inheritdoc />
        public async Task<T?> ReadAsync<T>(LogicalTagKey<T> tag, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var value = CancellableRead is null
                ? Read?.Invoke(tag.Name)
                : await CancellableRead(tag.Name, cancellationToken).ConfigureAwait(false);
            return value is T typed ? typed : default;
        }

        /// <inheritdoc />
        public void Value<T>(string? variable, T? value) => Write?.Invoke(variable!, value);
    }

    /// <summary>Returns a deterministic sequence of UTC timestamps.</summary>
    /// <param name="values">The timestamps returned in order.</param>
    private sealed class SequenceTimeProvider(params DateTimeOffset[] values) : TimeProvider
    {
        /// <summary>Stores timestamps that have not yet been returned.</summary>
        private readonly Queue<DateTimeOffset> _values = new(values);

        /// <summary>Stores the last timestamp returned by the provider.</summary>
        private DateTimeOffset _last = values.Length == 0 ? TestTime.UnixEpoch : values[^1];

        /// <inheritdoc />
        public override DateTimeOffset GetUtcNow()
        {
            if (_values.Count != 0)
            {
                _last = _values.Dequeue();
            }

            return _last;
        }
    }

    /// <summary>Throws exactly once at a configured UTC-clock call.</summary>
    /// <param name="utcNow">The timestamp returned by successful calls.</param>
    /// <param name="failureCall">The one-based call number that throws.</param>
    private sealed class FaultAtCallTimeProvider(DateTimeOffset utcNow, int failureCall) : TimeProvider
    {
        /// <summary>Counts UTC-clock calls.</summary>
        private int _callCount;

        /// <inheritdoc />
        public override DateTimeOffset GetUtcNow()
        {
            _callCount++;
            return _callCount == failureCall
                ? throw new InvalidOperationException("clock failure")
                : utcNow;
        }
    }
}
