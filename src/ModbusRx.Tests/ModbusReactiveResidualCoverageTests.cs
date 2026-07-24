// Copyright (c) 2019-2026 Chris Pulman and contributors. All rights reserved.
// Chris Pulman and contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using IoT.DriverCore.ModbusRx.Device;
using NativeAssert = TUnit.Assertions.Assert;

namespace IoT.DriverCore.ModbusRx.UnitTests;

/// <summary>Exercises deterministic residual behavior in the Modbus reactive server and creation APIs.</summary>
public sealed class ModbusReactiveResidualCoverageTests
{
    /// <summary>The default Modbus unit identifier used by overload-forwarding tests.</summary>
    private const byte UnitId = 1;

    /// <summary>The read address used by overload-forwarding tests.</summary>
    private const ushort Address = 0;

    /// <summary>The one-point read count used by overload-forwarding tests.</summary>
    private const ushort Count = 1;

    /// <summary>A dormant interval which never elapses because each source emits synchronously.</summary>
    private const double DormantInterval = 60_000D;

    /// <summary>The first value written through the event-driven observation.</summary>
    private const ushort FirstValue = 42;

    /// <summary>The second value written after subscriptions are disposed.</summary>
    private const ushort SecondValue = 43;

    /// <summary>Observes direct data-store writes with both event-driven overload shapes and unsubscribes deterministically.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [TUnit.Core.Test]
    public async Task EventDrivenObservation_UsesFallbackTimeProviderAndDetachesOnDisposeAsync()
    {
        using var server = new ModbusServer();
        var metrics = new ModbusObservationMetrics();
        var explicitSnapshots = new List<ModbusServerDataSnapshot>();
        var defaultSnapshots = new List<ModbusServerDataSnapshot>();
        using var explicitSubscription = EnhancedModbusServerExtensions
            .ObserveDataChangesEventDriven(server, null, metrics)
            .Subscribe(explicitSnapshots.Add);
        using var defaultSubscription = EnhancedModbusServerExtensions
            .ObserveDataChangesEventDriven(server)
            .Subscribe(defaultSnapshots.Add);
        var dataStore = server.DataStore!;
        ushort[] firstValues = [FirstValue];

        dataStore.WriteDataOptimized(firstValues, dataStore.HoldingRegisters, Address);

        await NativeAssert.That(explicitSnapshots.Count).IsEqualTo(1);
        await NativeAssert.That(defaultSnapshots.Count).IsEqualTo(1);
        await NativeAssert.That(explicitSnapshots[0].HoldingRegisters[0]).IsEqualTo(FirstValue);
        await NativeAssert.That(metrics.WriteNotifications).IsEqualTo(1L);
        await NativeAssert.That(metrics.SnapshotsCreated).IsEqualTo(1L);
        await NativeAssert.That(metrics.SnapshotsEmitted).IsEqualTo(1L);

        explicitSubscription.Dispose();
        defaultSubscription.Dispose();
        ushort[] secondValues = [SecondValue];
        dataStore.WriteDataOptimized(secondValues, dataStore.HoldingRegisters, Address);

        await NativeAssert.That(explicitSnapshots.Count).IsEqualTo(1);
        await NativeAssert.That(defaultSnapshots.Count).IsEqualTo(1);
        await NativeAssert.That(metrics.WriteNotifications).IsEqualTo(1L);
    }

    /// <summary>Verifies a reactive server is configured, started, and disposed with its subscription.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [TUnit.Core.Test]
    public async Task ReactiveServerSubscription_OwnsConfiguredServerLifecycleAsync()
    {
        ModbusServer? created = null;
        var configured = false;
        var subscription = ModbusServerExtensions.CreateReactiveServer(server =>
            {
                configured = true;
                created = server;
            })
            .Subscribe();

        await NativeAssert.That(configured).IsTrue();
        await NativeAssert.That(created).IsNotNull();
        var runningStates = new List<bool>();
        using var stateSubscription = created!.IsRunning.Subscribe(runningStates.Add);
        await NativeAssert.That(runningStates[0]).IsTrue();

        subscription.Dispose();

        await NativeAssert.That(runningStates[1]).IsFalse();
    }

    /// <summary>Verifies every public create read overload preserves a disconnected source error without waiting for a timer.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [TUnit.Core.Test]
    public async Task CreateReadOverloads_ForwardDisconnectedSerialAndIpSourcesAsync()
    {
        var failure = new IOException("disconnected");
        IObservable<(bool Connected, Exception? Error, IModbusSerialMaster? Master)> serialSource =
            Observable.Return((false, (Exception?)failure, (IModbusSerialMaster?)null));
        IObservable<(bool Connected, Exception? Error, ModbusIpMaster? Master)> networkSource =
            Observable.Return((false, (Exception?)failure, (ModbusIpMaster?)null));

        await AssertDisconnectedReadAsync(
            Create.ReadCoils(serialSource, UnitId, Address, Count, DormantInterval),
            failure);
        await AssertDisconnectedReadAsync(Create.ReadCoils(serialSource, Address, Count, DormantInterval), failure);
        await AssertDisconnectedReadAsync(Create.ReadCoils(networkSource, Address, Count, DormantInterval), failure);
        await AssertDisconnectedReadAsync(
            Create.ReadHoldingRegisters(serialSource, UnitId, Address, Count, DormantInterval),
            failure);
        await AssertDisconnectedReadAsync(
            Create.ReadHoldingRegisters(serialSource, Address, Count, DormantInterval),
            failure);
        await AssertDisconnectedReadAsync(
            Create.ReadHoldingRegisters(networkSource, Address, Count, DormantInterval),
            failure);
        await AssertDisconnectedReadAsync(
            Create.ReadInputRegisters(serialSource, UnitId, Address, Count, DormantInterval),
            failure);
        await AssertDisconnectedReadAsync(
            Create.ReadInputRegisters(serialSource, Address, Count, DormantInterval),
            failure);
        await AssertDisconnectedReadAsync(
            Create.ReadInputRegisters(networkSource, Address, Count, DormantInterval),
            failure);
        await AssertDisconnectedReadAsync(
            Create.ReadInputs(serialSource, UnitId, Address, Count, DormantInterval),
            failure);
        await AssertDisconnectedReadAsync(Create.ReadInputs(serialSource, Address, Count, DormantInterval), failure);
        await AssertDisconnectedReadAsync(Create.ReadInputs(networkSource, Address, Count, DormantInterval), failure);
    }

    /// <summary>Verifies extension creation read overloads forward their disconnected source without waiting for a timer.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [TUnit.Core.Test]
    public async Task CreateExtensionReadOverloads_ForwardDisconnectedSourcesAsync()
    {
        var failure = new IOException("disconnected");
        IObservable<(bool Connected, Exception? Error, IModbusSerialMaster? Master)> serialSource =
            Observable.Return((false, (Exception?)failure, (IModbusSerialMaster?)null));
        IObservable<(bool Connected, Exception? Error, ModbusIpMaster? Master)> networkSource =
            Observable.Return((false, (Exception?)failure, (ModbusIpMaster?)null));

        await AssertDisconnectedReadAsync(
            CreateExtensions.ReadCoils(serialSource, UnitId, Address, Count, DormantInterval),
            failure);
        await AssertDisconnectedReadAsync(
            CreateExtensions.ReadCoils(serialSource, Address, Count, DormantInterval),
            failure);
        await AssertDisconnectedReadAsync(
            CreateExtensions.ReadCoils(networkSource, Address, Count, DormantInterval),
            failure);
        await AssertDisconnectedReadAsync(
            CreateExtensions.ReadHoldingRegisters(serialSource, UnitId, Address, Count, DormantInterval),
            failure);
        await AssertDisconnectedReadAsync(
            CreateExtensions.ReadHoldingRegisters(serialSource, Address, Count, DormantInterval),
            failure);
        await AssertDisconnectedReadAsync(
            CreateExtensions.ReadHoldingRegisters(networkSource, Address, Count, DormantInterval),
            failure);
        await AssertDisconnectedReadAsync(
            CreateExtensions.ReadInputRegisters(serialSource, UnitId, Address, Count, DormantInterval),
            failure);
        await AssertDisconnectedReadAsync(
            CreateExtensions.ReadInputRegisters(serialSource, Address, Count, DormantInterval),
            failure);
        await AssertDisconnectedReadAsync(
            CreateExtensions.ReadInputRegisters(networkSource, Address, Count, DormantInterval),
            failure);
        await AssertDisconnectedReadAsync(
            CreateExtensions.ReadInputs(serialSource, UnitId, Address, Count, DormantInterval),
            failure);
        await AssertDisconnectedReadAsync(
            CreateExtensions.ReadInputs(serialSource, Address, Count, DormantInterval),
            failure);
        await AssertDisconnectedReadAsync(
            CreateExtensions.ReadInputs(networkSource, Address, Count, DormantInterval),
            failure);
    }

    /// <summary>Asserts a read adapter emits the source error without materializing data.</summary>
    /// <typeparam name="TValue">The read value type.</typeparam>
    /// <param name="source">The disconnected read adapter.</param>
    /// <param name="failure">The expected source failure.</param>
    /// <returns>A task representing the asynchronous assertion.</returns>
    private static async Task AssertDisconnectedReadAsync<TValue>(
        IObservable<(TValue[]? Data, Exception? Error)> source,
        Exception failure)
    {
        var result = await source.FirstAsync();

        await NativeAssert.That(result.Data).IsNull();
        await NativeAssert.That(result.Error).IsSameReferenceAs(failure);
    }
}
