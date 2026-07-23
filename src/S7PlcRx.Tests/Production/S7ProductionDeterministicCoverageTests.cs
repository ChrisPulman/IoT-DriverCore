// Copyright (c) 2019-2026 Chris Pulman and contributors. All rights reserved.
// Chris Pulman and contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using IoT.DriverCore.S7PlcRx.Enterprise;
using IoT.DriverCore.S7PlcRx.Enums;
using IoT.DriverCore.S7PlcRx.Production;

namespace IoT.DriverCore.S7PlcRx.Tests.Production;

/// <summary>Exercises production resiliency with deterministic in-memory PLC collaborators.</summary>
public sealed class S7ProductionDeterministicCoverageTests
{
    /// <summary>Defines the configured transient retry count.</summary>
    private const int RequiredRetryAttempts = 2;

    /// <summary>Defines the attempt on which the transient operation succeeds.</summary>
    private const int SuccessfulAttemptNumber = 3;

    /// <summary>Defines the value returned after transient retry recovery.</summary>
    private const int ExpectedRetryResult = 42;

    /// <summary>Defines the value returned by half-open recovery.</summary>
    private const int RecoveredResult = 7;

    /// <summary>Defines the total operations made by the opening breaker.</summary>
    private const long ExpectedOpeningBreakerOperations = 3L;

    /// <summary>Defines a full percentage score.</summary>
    private const double PercentageScore = 100D;

    /// <summary>Defines the number of production readiness checks.</summary>
    private const int ExpectedValidationTestCount = 3;

    /// <summary>Defines the details produced by a failed reliability operation and its summary.</summary>
    private const int ExpectedFailedReliabilityDetailCount = 2;

    /// <summary>Defines the value returned after a linearly delayed retry.</summary>
    private const int LinearRetryResult = 4;

    /// <summary>Defines the expected number of captured failover events.</summary>
    private const int ExpectedFailoverEventCount = 1;

    /// <summary>Defines a short health-check timer interval.</summary>
    private static readonly TimeSpan LongHealthCheckInterval = TimeSpan.FromMinutes(1);

    /// <summary>Verifies retry, open-circuit rejection, and half-open recovery without wall-clock delays.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task CircuitBreakerRetriesOpensAndRecoversAfterItsConfiguredTimeoutAsync()
    {
        var clock = new SteppingTimeProvider(DateTimeOffset.Parse("2026-07-23T00:00:00+00:00"));
        await VerifyExponentialRetryAsync(clock);
        await VerifyLinearRetryAsync(clock);
        await VerifyOpenCircuitAndRecoveryAsync(clock);
    }

    /// <summary>Verifies production readiness reports successful and failed connectivity, performance, and reliability.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task ProductionReadinessRecordsSuccessfulAndFailedDeterministicChecksAsync()
    {
        var config = new ProductionValidationConfig
        {
            MaxAcceptableResponseTime = TimeSpan.FromSeconds(1),
            MinimumReliabilityRate = 1,
            ReliabilityTestCount = 1,
            MinimumProductionScore = PercentageScore,
        };
        var healthy = new DeterministicPlc(true, () => Observable.Return((string[])["CPU 1516"]));
        var unhealthy = new DeterministicPlc(false, () => Observable.Throw<string[]>(new InvalidOperationException("offline")));

        var healthyResult = await ProductionExtensions.ValidateProductionReadinessAsync(
            healthy,
            config,
            new SteppingTimeProvider(DateTimeOffset.Parse("2026-07-23T01:00:00+00:00")));
        var unhealthyResult = await ProductionExtensions.ValidateProductionReadinessAsync(
            unhealthy,
            config,
            new SteppingTimeProvider(DateTimeOffset.Parse("2026-07-23T02:00:00+00:00")));

        await TUnit.Assertions.Assert.That(healthyResult.IsProductionReady).IsTrue();
        await TUnit.Assertions.Assert.That(healthyResult.OverallScore).IsEqualTo(PercentageScore);
        await TUnit.Assertions.Assert.That(healthyResult.ValidationTests.Count).IsEqualTo(ExpectedValidationTestCount);
        await TUnit.Assertions.Assert.That(unhealthyResult.IsProductionReady).IsFalse();
        await TUnit.Assertions.Assert.That(unhealthyResult.OverallScore).IsEqualTo(0D);
        await TUnit.Assertions.Assert.That(unhealthyResult.ValidationTests[0].ErrorMessage).IsEqualTo("PLC is not connected");
        await TUnit.Assertions.Assert.That(unhealthyResult.ValidationTests[1].ErrorMessage).IsEqualTo("offline");
        await TUnit.Assertions.Assert.That(unhealthyResult.ValidationTests[2].Details.Count)
            .IsEqualTo(ExpectedFailedReliabilityDetailCount);
    }

    /// <summary>Verifies argument validation and the compositional production error-handler facade.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task ProductionErrorHandlingValidatesArgumentsAndExecutesThroughItsFacadeAsync()
    {
        var plc = new DeterministicPlc(true, () => Observable.Return((string[])["CPU"]));
        var config = new ProductionErrorConfig { MaxRetryAttempts = 0, BaseRetryDelayMs = 0 };

        await TUnit.Assertions.Assert.That(() => ProductionExtensions.EnableProductionErrorHandling(null!, config))
            .Throws<ArgumentNullException>();
        await TUnit.Assertions.Assert.That(() => ProductionExtensions.EnableProductionErrorHandling(plc, null!))
            .Throws<ArgumentNullException>();
        Func<Task<int>> nullOperation = null!;
        await TUnit.Assertions.Assert.That(() => ProductionExtensions.ExecuteWithErrorHandlingAsync(plc, nullOperation, config))
            .Throws<ArgumentNullException>();
        Func<Task<int>> successfulOperation = () => Task.FromResult(1);
        await TUnit.Assertions.Assert.That(() => ProductionExtensions.ExecuteWithErrorHandlingAsync(plc, successfulOperation, null!))
            .Throws<ArgumentNullException>();

        var handler = ProductionExtensions.EnableProductionErrorHandling(plc, config);
        var result = await handler.ExecuteAsync(() => Task.FromResult("handled"));

        await TUnit.Assertions.Assert.That(result).IsEqualTo("handled");

        var defaultResult = await ProductionExtensions.ExecuteWithErrorHandlingAsync(
            new DeterministicPlc(true, () => Observable.Return((string[])["CPU"]), "127.0.0.2"),
            () => Task.FromResult("default-configured"));
        await TUnit.Assertions.Assert.That(defaultResult).IsEqualTo("default-configured");
    }

    /// <summary>Verifies high-availability failover selects connected backups and publishes its real event data.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task HighAvailabilityFailoverPublishesSelectedBackupAndHandlesNoAvailableBackupAsync()
    {
        var time = new SteppingTimeProvider(DateTimeOffset.Parse("2026-07-23T03:00:00+00:00"));
        var disconnectedPrimary = new DeterministicPlc(false, () => Observable.Return((string[])[]), "10.0.0.1");
        var disconnectedBackup = new DeterministicPlc(false, () => Observable.Return((string[])[]), "10.0.0.2");
        var connectedBackup = new DeterministicPlc(true, () => Observable.Return((string[])["CPU"]), "10.0.0.3");
        var failoverCandidates = new List<IRxS7> { disconnectedBackup, connectedBackup };

        using var manager = new HighAvailabilityPlcManager(
            disconnectedPrimary,
            failoverCandidates,
            LongHealthCheckInterval,
            time);
        var eventTask = manager.FailoverEvents.Take(ExpectedFailoverEventCount).FirstAsync();
        var failoverSucceeded = await manager.TriggerFailoverAsync();
        var failoverEvent = await eventTask;

        await TUnit.Assertions.Assert.That(failoverSucceeded).IsTrue();
        await TUnit.Assertions.Assert.That(manager.ActivePLC).IsSameReferenceAs(connectedBackup);
        await TUnit.Assertions.Assert.That(failoverEvent.Reason).IsEqualTo("Manual failover triggered.");
        await TUnit.Assertions.Assert.That(failoverEvent.OldPlc).IsEqualTo("10.0.0.1:S71500");
        await TUnit.Assertions.Assert.That(failoverEvent.NewPlc).IsEqualTo("10.0.0.3:S71500");

        var unavailablePrimary = new DeterministicPlc(false, () => Observable.Return((string[])[]), "10.0.0.4");
        var unavailableBackups = new List<IRxS7>
        {
            new DeterministicPlc(false, () => Observable.Return((string[])[]), "10.0.0.5"),
        };
        using var unavailableManager = new HighAvailabilityPlcManager(
            unavailablePrimary,
            unavailableBackups,
            LongHealthCheckInterval,
            time);

        await TUnit.Assertions.Assert.That(await unavailableManager.TriggerFailoverAsync()).IsFalse();
        unavailableManager.Dispose();
    }

    /// <summary>Verifies a circuit breaker retries an operation with exponential backoff semantics.</summary>
    /// <param name="clock">The deterministic clock used by the breaker.</param>
    /// <returns>A task that represents the asynchronous verification.</returns>
    private static async Task VerifyExponentialRetryAsync(TimeProvider clock)
    {
        var retryingBreaker = new CircuitBreaker(
            new ProductionErrorConfig
            {
                MaxRetryAttempts = RequiredRetryAttempts,
                BaseRetryDelayMs = 0,
                UseExponentialBackoff = true,
                CircuitBreakerThreshold = RequiredRetryAttempts,
                CircuitBreakerTimeout = TimeSpan.FromMinutes(1),
            },
            clock);
        var attempts = 0;

        var result = await retryingBreaker.ExecuteAsync(() =>
        {
            attempts++;
            return attempts < SuccessfulAttemptNumber
                ? Task.FromException<int>(new InvalidOperationException("transient"))
                : Task.FromResult(ExpectedRetryResult);
        });

        await TUnit.Assertions.Assert.That(result).IsEqualTo(ExpectedRetryResult);
        await TUnit.Assertions.Assert.That(attempts).IsEqualTo(SuccessfulAttemptNumber);
        await TUnit.Assertions.Assert.That(retryingBreaker.State).IsEqualTo(CircuitBreakerState.Closed);
        await TUnit.Assertions.Assert.That(retryingBreaker.SuccessRate).IsEqualTo(PercentageScore);
    }

    /// <summary>Verifies a circuit breaker retries an operation with linear backoff semantics.</summary>
    /// <param name="clock">The deterministic clock used by the breaker.</param>
    /// <returns>A task that represents the asynchronous verification.</returns>
    private static async Task VerifyLinearRetryAsync(TimeProvider clock)
    {
        var linearRetryBreaker = new CircuitBreaker(
            new ProductionErrorConfig
            {
                MaxRetryAttempts = 1,
                BaseRetryDelayMs = 0,
                UseExponentialBackoff = false,
                CircuitBreakerThreshold = RequiredRetryAttempts,
                CircuitBreakerTimeout = TimeSpan.FromMinutes(1),
            },
            clock);
        var linearAttempts = 0;
        var linearResult = await linearRetryBreaker.ExecuteAsync(() =>
        {
            linearAttempts++;
            return linearAttempts == 1
                ? Task.FromException<int>(new InvalidOperationException("linear transient"))
                : Task.FromResult(LinearRetryResult);
        });

        await TUnit.Assertions.Assert.That(linearResult).IsEqualTo(LinearRetryResult);
        await TUnit.Assertions.Assert.That(linearRetryBreaker.SuccessRate).IsEqualTo(PercentageScore);
    }

    /// <summary>Verifies an open circuit rejects work until its timeout and then recovers.</summary>
    /// <param name="clock">The manually advanced clock used by the breaker.</param>
    /// <returns>A task that represents the asynchronous verification.</returns>
    private static async Task VerifyOpenCircuitAndRecoveryAsync(SteppingTimeProvider clock)
    {
        var openingBreaker = new CircuitBreaker(
            new ProductionErrorConfig
            {
                MaxRetryAttempts = 0,
                BaseRetryDelayMs = 0,
                UseExponentialBackoff = false,
                CircuitBreakerThreshold = 1,
                CircuitBreakerTimeout = TimeSpan.FromSeconds(1),
            },
            clock);

        Func<Task<int>> terminalOperation = () => Task.FromException<int>(new InvalidOperationException("terminal"));
        await TUnit.Assertions.Assert.That(() => openingBreaker.ExecuteAsync(terminalOperation))
            .Throws<InvalidOperationException>();
        await TUnit.Assertions.Assert.That(openingBreaker.State).IsEqualTo(CircuitBreakerState.Open);
        await TUnit.Assertions.Assert.That(() => openingBreaker.ExecuteAsync(() => Task.FromResult(1)))
            .Throws<InvalidOperationException>();

        clock.Advance(TimeSpan.FromSeconds(1));
        var recovered = await openingBreaker.ExecuteAsync(() => Task.FromResult(RecoveredResult));

        await TUnit.Assertions.Assert.That(recovered).IsEqualTo(RecoveredResult);
        await TUnit.Assertions.Assert.That(openingBreaker.State).IsEqualTo(CircuitBreakerState.Closed);
        await TUnit.Assertions.Assert.That(openingBreaker.TotalOperations).IsEqualTo(ExpectedOpeningBreakerOperations);
        await TUnit.Assertions.Assert.That(openingBreaker.FailedOperations).IsEqualTo(1L);

        var emptyBreaker = new CircuitBreaker(new ProductionErrorConfig(), clock);
        Func<Task<int>> nullOperation = null!;
        await TUnit.Assertions.Assert.That(emptyBreaker.SuccessRate).IsEqualTo(0D);
        await TUnit.Assertions.Assert.That(() => emptyBreaker.ExecuteAsync(nullOperation))
            .Throws<ArgumentNullException>();
    }

    /// <summary>Provides a deterministic clock that advances only when directed by the test.</summary>
    private sealed class SteppingTimeProvider : TimeProvider
    {
        /// <summary>Stores the deterministic current time.</summary>
        private DateTimeOffset _utcNow;

        /// <summary>Initializes a new instance of the <see cref="SteppingTimeProvider"/> class.</summary>
        /// <param name="utcNow">The initial UTC time.</param>
        public SteppingTimeProvider(DateTimeOffset utcNow) => _utcNow = utcNow;

        /// <inheritdoc/>
        public override DateTimeOffset GetUtcNow() => _utcNow;

        /// <summary>Moves the test clock forward by the supplied duration.</summary>
        /// <param name="duration">The duration to add.</param>
        public void Advance(TimeSpan duration) => _utcNow = _utcNow.Add(duration);
    }

    /// <summary>Implements the production-facing PLC contract without sockets or native-driver dependencies.</summary>
    /// <param name="connected">Whether the fake PLC is connected.</param>
    /// <param name="cpuInfoFactory">Creates the CPU-information stream.</param>
    /// <param name="ip">The deterministic PLC endpoint identifier.</param>
    private sealed class DeterministicPlc(bool connected, Func<IObservable<string[]>> cpuInfoFactory, string ip = "127.0.0.1") : IRxS7
    {
        /// <summary>Stores the configured connection state.</summary>
        private readonly bool _connected = connected;

        /// <summary>Stores the deterministic CPU-information stream factory.</summary>
        private readonly Func<IObservable<string[]>> _cpuInfoFactory = cpuInfoFactory;

        /// <inheritdoc/>
        public string IP => ip;

        /// <inheritdoc/>
        public IObservable<bool> IsConnected => Observable.Return(_connected);

        /// <inheritdoc/>
        public bool IsConnectedValue => _connected;

        /// <inheritdoc/>
        public IObservable<string> LastError => Observable.Empty<string>();

        /// <inheritdoc/>
        public IObservable<ErrorCode> LastErrorCode => Observable.Empty<ErrorCode>();

        /// <inheritdoc/>
        public IObservable<Tag?> ObserveAll => Observable.Empty<Tag?>();

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
        public global::IoT.DriverCore.S7PlcRx.Tags TagList { get; } = [];

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
        public void Dispose() => IsDisposed = true;

        /// <inheritdoc/>
        public IObservable<T?> Observe<T>(LogicalTagKey<T> tag) => Observable.Empty<T?>();

        /// <inheritdoc/>
        public Task<T?> ReadAsync<T>(LogicalTagKey<T> tag) => Task.FromResult<T?>(default);

        /// <inheritdoc/>
        public Task<T?> ReadAsync<T>(LogicalTagKey<T> tag, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult<T?>(default);
        }

        /// <inheritdoc/>
        public void Value<T>(string? variable, T? value)
        {
        }

        /// <inheritdoc/>
        public IObservable<string[]> GetCpuInfo() => _cpuInfoFactory();
    }
}
