// Copyright (c) 2019-2026 Chris Pulman and contributors. All rights reserved.
// Chris Pulman and contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using IoT.DriverCore.ModbusRx.Data;
using IoT.DriverCore.ModbusRx.Device;
using NativeAssert = TUnit.Assertions.Assert;

namespace IoT.DriverCore.ModbusRx.UnitTests;

/// <summary>Loopback-only behavioral coverage for optimized server observation.</summary>
public sealed class EnhancedReactiveCoverageTests
{
    /// <summary>The first address exposed by optimized server snapshots.</summary>
    private const ushort FirstAddress = 1;

    /// <summary>The first event-backed Modbus address used by range-filtered observations.</summary>
    private const ushort EventAddress = 2;

    /// <summary>The number of observed points.</summary>
    private const ushort PointCount = 2;

    /// <summary>The deterministic observation interval.</summary>
    private const int ObservationInterval = 1;

    /// <summary>The first non-default register value.</summary>
    private const ushort FirstRegisterValue = 11;

    /// <summary>The maximum time allowed for an optimized observation.</summary>
    private static readonly TimeSpan ObservationTimeout = TimeSpan.FromSeconds(5);

    /// <summary>Observes two snapshot changes with a fixed timestamp.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [TUnit.Core.Test]
    public async Task ObserveDataChangesOptimized_EmitsImmutableChangesAsync()
    {
        using var server = new ModbusServer();
        DataStoreExtensions.WriteHoldingRegistersOptimized(
            server.DataStore!,
            FirstAddress,
            [FirstRegisterValue]);

        var snapshot = await EnhancedModbusServerExtensions
            .ObserveDataChangesOptimized(server, ObservationInterval, new FixedTimeProvider())
            .FirstAsync();

        await NativeAssert.That(snapshot.Timestamp).IsEqualTo(TestFrameworkCompatibilityExtensions.UnixEpoch);
        await NativeAssert.That(snapshot.HoldingRegisters[0]).IsEqualTo(FirstRegisterValue);
    }

    /// <summary>Observes range-filtered holding-register changes.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [TUnit.Core.Test]
    public async Task ObserveHoldingRegistersOptimized_EmitsRelevantWritesAsync()
    {
        using var server = new ModbusServer();
        DataStoreExtensions.WriteHoldingRegistersOptimized(
            server.DataStore!,
            FirstAddress,
            [FirstRegisterValue, FirstRegisterValue]);

        var values = await EnhancedModbusServerExtensions
            .ObserveHoldingRegistersOptimized(server, FirstAddress, PointCount, ObservationInterval)
            .FirstAsync();

        await NativeAssert.That(values).IsEquivalentTo([FirstRegisterValue, FirstRegisterValue]);
    }

    /// <summary>Observes range-filtered coil changes.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [TUnit.Core.Test]
    public async Task ObserveCoilsOptimized_EmitsRelevantWritesAsync()
    {
        using var server = new ModbusServer();
        DataStoreExtensions.WriteCoilsOptimized(server.DataStore!, FirstAddress, [true, false]);

        var values = await EnhancedModbusServerExtensions
            .ObserveCoilsOptimized(server, FirstAddress, PointCount, ObservationInterval)
            .FirstAsync();

        await NativeAssert.That(values).IsEquivalentTo([true, false]);
    }

    /// <summary>Forwards post-subscription data-store writes through optimized range filters.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [TUnit.Core.Test]
    public async Task OptimizedObservations_ReactToPostSubscriptionWritesAsync()
    {
        using var server = new ModbusServer();
        var initialSnapshot = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var changedSnapshotTask = EnhancedModbusServerExtensions
            .ObserveDataChangesOptimized(server, ObservationInterval, new FixedTimeProvider())
            .Do(_ => initialSnapshot.TrySetResult(true))
            .Skip(1)
            .FirstAsync();
        await initialSnapshot.Task.WaitAsync(ObservationTimeout);

        server.DataStore!.WriteDataOptimized(
            [FirstRegisterValue],
            server.DataStore.HoldingRegisters,
            0);
        var changedSnapshot = await changedSnapshotTask.WaitAsync(ObservationTimeout);

        var holdingTask = EnhancedModbusServerExtensions
            .ObserveHoldingRegistersOptimized(server, EventAddress, PointCount, ObservationInterval)
            .FirstAsync();
        server.DataStore.WriteDataOptimized(
            [FirstRegisterValue, FirstRegisterValue],
            server.DataStore.HoldingRegisters,
            FirstAddress);
        var holding = await holdingTask.WaitAsync(ObservationTimeout);

        var coilsTask = EnhancedModbusServerExtensions
            .ObserveCoilsOptimized(server, EventAddress, PointCount, ObservationInterval)
            .FirstAsync();
        server.DataStore.WriteDataOptimized([true, false], server.DataStore.CoilDiscretes, FirstAddress);
        var coils = await coilsTask.WaitAsync(ObservationTimeout);

        await NativeAssert.That(changedSnapshot.HoldingRegisters[0]).IsEqualTo(FirstRegisterValue);
        await NativeAssert.That(holding).IsEquivalentTo([FirstRegisterValue, FirstRegisterValue]);
        await NativeAssert.That(coils).IsEquivalentTo([true, false]);
    }

    /// <summary>Exercises both buffered overloads and their immutable batch projection.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [TUnit.Core.Test]
    public async Task ObserveDataChangesBuffered_EmitsCopiedBatchAsync()
    {
        using var server = new ModbusServer();
        var systemBatch = await EnhancedModbusServerExtensions
            .ObserveDataChangesBuffered(server, 1, ObservationInterval)
            .FirstAsync();
        var fixedBatch = await EnhancedModbusServerExtensions
            .ObserveDataChangesBuffered(server, 1, ObservationInterval, new FixedTimeProvider())
            .FirstAsync();

        await NativeAssert.That(systemBatch.Length).IsEqualTo(1);
        await NativeAssert.That(fixedBatch.Length).IsEqualTo(1);
        await NativeAssert.That(fixedBatch[0].Timestamp).IsEqualTo(TestFrameworkCompatibilityExtensions.UnixEpoch);
    }

    /// <summary>Verifies optimized observation guards reject null servers.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [TUnit.Core.Test]
    public async Task OptimizedObservations_RejectNullServerAsync()
    {
        await NativeAssert.That(
                () => EnhancedModbusServerExtensions.ObserveDataChangesOptimized(null!, ObservationInterval))
            .Throws<ArgumentNullException>();
        await NativeAssert.That(
                () => EnhancedModbusServerExtensions.ObserveHoldingRegistersOptimized(
                    null!,
                    FirstAddress,
                    PointCount,
                    ObservationInterval))
            .Throws<ArgumentNullException>();
        await NativeAssert.That(
                () => EnhancedModbusServerExtensions.ObserveCoilsOptimized(
                    null!,
                    FirstAddress,
                    PointCount,
                    ObservationInterval))
            .Throws<ArgumentNullException>();
        await NativeAssert.That(
                () => EnhancedModbusServerExtensions.ObserveDataChangesBuffered(
                    null!,
                    1,
                    ObservationInterval))
            .Throws<ArgumentNullException>();
    }

    /// <summary>Exercises classic server polling, range projection, and reactive lifecycle paths.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [TUnit.Core.Test]
    public async Task ServerExtensions_ObserveRangesAndLifecycleAsync()
    {
        using var server = new ModbusServer();
        server.LoadSimulationData(
            [FirstRegisterValue, FirstRegisterValue],
            [FirstRegisterValue, FirstRegisterValue],
            [true, false],
            [false, true]);

        var data = await ModbusServerExtensions.ObserveDataChanges(server, ObservationInterval).FirstAsync();
        var holding = await ModbusServerExtensions
            .ObserveHoldingRegisters(server, 0, PointCount, ObservationInterval)
            .FirstAsync();
        var input = await ModbusServerExtensions
            .ObserveInputRegisters(server, 0, PointCount, ObservationInterval)
            .FirstAsync();
        var coils = await ModbusServerExtensions
            .ObserveCoils(server, 0, PointCount, ObservationInterval)
            .FirstAsync();
        var discretes = await ModbusServerExtensions
            .ObserveDiscreteInputs(server, 0, PointCount, ObservationInterval)
            .FirstAsync();
        var empty = await ModbusServerExtensions
            .ObserveHoldingRegisters(server, ushort.MaxValue, PointCount, ObservationInterval)
            .FirstAsync();
        var reactiveServer = await ModbusServerExtensions.CreateReactiveServer(static _ => { }).FirstAsync();

        await NativeAssert.That(data.HoldingRegisters[0]).IsEqualTo(FirstRegisterValue);
        await NativeAssert.That(holding).IsEquivalentTo([FirstRegisterValue, FirstRegisterValue]);
        await NativeAssert.That(input).IsEquivalentTo([FirstRegisterValue, FirstRegisterValue]);
        await NativeAssert.That(coils).IsEquivalentTo([true, false]);
        await NativeAssert.That(discretes).IsEquivalentTo([false, true]);
        await NativeAssert.That(empty).IsEmpty();
        await NativeAssert.That(reactiveServer).IsNotNull();
        await NativeAssert.That(
                async () => await ModbusServerExtensions
                    .CreateReactiveServer(static _ => throw new InvalidOperationException("configure"))
                    .FirstAsync())
            .Throws<InvalidOperationException>();
    }

    /// <summary>Provides a stable timestamp for snapshot assertions.</summary>
    private sealed class FixedTimeProvider : TimeProvider
    {
        /// <inheritdoc />
        public override DateTimeOffset GetUtcNow() => TestFrameworkCompatibilityExtensions.UnixEpoch;
    }
}
