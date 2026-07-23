// Copyright (c) 2019-2026 Chris Pulman and contributors. All rights reserved.
// Chris Pulman and contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using IoT.DriverCore.Core;
using TUnit.Assertions;
using TUnit.Core;
using PlcController = IoT.DriverCore.ABPlcRx.ABPlcRx;

namespace IoT.DriverCore.ABPlcRx.Tests;

/// <summary>Exercises advanced logical, observable, bit-projection, and ping paths.</summary>
public sealed class ABPlcAdvancedCoverageTests
{
    /// <summary>Loopback address used by ping tests.</summary>
    private const string LoopbackAddress = "127.0.0.1";

    /// <summary>General logical tag name.</summary>
    private const string ValueTagName = "Value";

    /// <summary>General physical tag name.</summary>
    private const string ValuePhysicalTagName = "ValuePhysical";

    /// <summary>Logical bit tag name.</summary>
    private const string BitTagName = "Bit";

    /// <summary>Bit physical tag name.</summary>
    private const string BitPhysicalTagName = "BitPhysical";

    /// <summary>Group name used by observation tests.</summary>
    private const string GroupName = "Advanced";

    /// <summary>Future group tag name.</summary>
    private const string FutureTagName = "Future";

    /// <summary>Sample integer value.</summary>
    private const int SampleValue = 42;

    /// <summary>Fast observation interval.</summary>
    private static readonly TimeSpan ScanInterval = TimeSpan.FromMilliseconds(5);

    /// <summary>Operation timeout.</summary>
    private static readonly TimeSpan OperationTimeout = TimeSpan.FromMilliseconds(250);

    /// <summary>Validates logical client constructors, null arguments, unsupported types, and disposal.</summary>
    /// <returns><see cref="Task"/> representing the test.</returns>
    [Test]
    internal async Task LogicalClientConstructorsAndValidationPathsAsync()
    {
        using var simulator = CreateSimulator();
        using var catalog = new LogicalTagCatalog();
        var store = new LogicalTagSqliteStore("Data Source=:memory:");
        using var withClock = new ABLogicalTagClient(simulator, TimeProvider.System);
        using var withCatalog = new ABLogicalTagClient(simulator, catalog);
        using var withCatalogClock = new ABLogicalTagClient(simulator, catalog, TimeProvider.System);
        using var withStore = new ABLogicalTagClient(simulator, catalog, store);
        using var withStoreClock = new ABLogicalTagClient(simulator, catalog, store, TimeProvider.System);

        _ = Assert.Throws<ArgumentNullException>(
            () =>
            {
                using var invalid = new ABLogicalTagClient(null!);
            });
        _ = Assert.Throws<ArgumentNullException>(
            () =>
            {
                using var invalid = new ABLogicalTagClient(simulator, (ILogicalTagCatalog)null!);
            });
        _ = Assert.Throws<ArgumentNullException>(() => withClock.RegisterTag(null!));
        _ = Assert.Throws<NotSupportedException>(
            () => withClock.CreateTag("Unsupported", "UnsupportedPhysical", "decimal"));
        _ = Assert.Throws<ArgumentNullException>(
            () => withClock.ReadManyAsync(null!).GetAwaiter().GetResult());
        _ = Assert.Throws<ArgumentNullException>(
            () => withClock.WriteAsync(null!).GetAwaiter().GetResult());
        _ = Assert.Throws<ArgumentNullException>(
            () => withClock.WriteManyAsync(null!).GetAwaiter().GetResult());
        _ = Assert.Throws<ArgumentNullException>(() => withClock.ObserveMany(null!));

        withClock.Dispose();
        withClock.Dispose();
        _ = Assert.Throws<ObjectDisposedException>(
            () => withClock.CreateTag(ValueTagName, ValuePhysicalTagName, "int"));
        await Assert.That(withCatalog.Catalog).IsEqualTo(catalog);
        await Assert.That(withCatalogClock.Catalog).IsEqualTo(catalog);
        await Assert.That(withStore.Catalog).IsEqualTo(catalog);
        await Assert.That(withStoreClock.Catalog).IsEqualTo(catalog);
    }

    /// <summary>Exercises normal and bit writes, missing controller registrations, and logical observations.</summary>
    /// <returns><see cref="Task"/> representing the test.</returns>
    [Test]
    internal async Task LogicalClientWritesAndObservesSuccessAndFailurePathsAsync()
    {
        using var simulator = CreateSimulator();
        using var client = simulator.CreateLogicalTagClient();
        _ = client.CreateTag(ValueTagName, ValuePhysicalTagName, "int");
        RegisterBitTag(client);
        simulator.ScanEnabled = false;
        var now = TimeProvider.System.GetUtcNow();

        var normalWrite = await client.WriteAsync(new LogicalTagValue(ValueTagName, SampleValue, now));
        simulator.QueueFault(ABPlcSimulatorOperation.Write, PlcTagStatus.ErrWrite, 1, ValuePhysicalTagName);
        var failedWrite = await client.WriteAsync(new LogicalTagValue(ValueTagName, SampleValue, now));
        simulator.QueueFault(ABPlcSimulatorOperation.Write, PlcTagStatus.ErrWrite, 1, BitPhysicalTagName);
        var failedBitWrite = await client.WriteAsync(new LogicalTagValue(BitTagName, true, now));

        await Assert.That(normalWrite.Succeeded).IsTrue();
        await Assert.That(failedWrite.Succeeded).IsFalse();
        await Assert.That(failedBitWrite.Succeeded).IsFalse();

        await AssertLogicalObservationsAsync(simulator, client);
        await AssertMissingControllerRegistrationAsync(simulator, client, now);
    }

    /// <summary>Exercises every integral branch used to project logical bit values.</summary>
    /// <returns><see cref="Task"/> representing the test.</returns>
    [Test]
    internal async Task LogicalBitProjectionSupportsAllIntegralTypesAsync()
    {
        using var simulator = CreateSimulator();
        using var client = simulator.CreateLogicalTagClient();
        var projections = new[]
        {
            RegisterProjection<byte>(simulator, client, "ByteBit", "BytePhysical", "byte", 1),
            RegisterProjection<sbyte>(simulator, client, "SByteBit", "SBytePhysical", "sbyte", 1),
            RegisterProjection<ushort>(simulator, client, "UInt16Bit", "UInt16Physical", "ushort", 1),
            RegisterProjection<uint>(simulator, client, "UInt32Bit", "UInt32Physical", "uint", 1),
            RegisterProjection(simulator, client, "Int32Bit", "Int32Physical", "int", 1),
            RegisterProjection<ulong>(simulator, client, "UInt64Bit", "UInt64Physical", "ulong", 1),
            RegisterProjection<long>(simulator, client, "Int64Bit", "Int64Physical", "long", 1),
        };
        simulator.ScanEnabled = false;

        var results = await client.ReadManyAsync(projections);

        await Assert.That(results.All(static result => result.Succeeded)).IsTrue();
        await Assert.That(results.All(static result => result.Value!.Value is true)).IsTrue();
    }

    /// <summary>Exercises loopback ping paths and facade overload forwarding.</summary>
    /// <returns><see cref="Task"/> representing the test.</returns>
    [Test]
    internal async Task PlcAndFacadePingAndOverloadPathsAsync()
    {
        using var publicPlc = new ABPlc(LoopbackAddress, PlcType.SLC);
        using var publicPlcWithSlot = new ABPlc(LoopbackAddress, PlcType.SLC, null);
        _ = Assert.Throws<ArgumentException>(
            () =>
            {
                using var invalid = new ABPlc(LoopbackAddress, PlcType.LGX);
            });
        _ = Assert.Throws<ArgumentNullException>(
            () =>
            {
                using var invalid = new ABPlc(LoopbackAddress, PlcType.SLC, null, null!);
            });

        var native = new SimulatedPlcTagNative(TimeProvider.System);
        using var plc = new ABPlc(LoopbackAddress, PlcType.SLC, null, native);
        await Assert.That(plc.Ping()).IsTrue();
        await Assert.That(plc.Ping(echo: true)).IsTrue();
        await Assert.That(await plc.PingAsync(cancellationToken: CancellationToken.None)).IsTrue();
        await Assert.That(await plc.PingAsync(echo: true, CancellationToken.None)).IsTrue();

        using var facade = new PlcController(
            PlcType.SLC,
            LoopbackAddress,
            ScanInterval,
            OperationTimeout,
            path: null,
            native);
        facade.AddUpdateTagItem<int>("First", default);
        facade.AddUpdateTagItem<int>("Second", "SecondPhysical", default);
        await Assert.That(facade.IsDisposed).IsFalse();
        await Assert.That(facade.Ping(echo: false)).IsTrue();
        await Assert.That(await facade.PingAsync(echo: false, CancellationToken.None)).IsTrue();
        await AssertEmptyAndFutureGroupObservationAsync(facade);
        facade.Dispose();
        facade.Dispose();
        await Assert.That(facade.IsDisposed).IsTrue();
    }

    /// <summary>Registers the standard Boolean bit tag.</summary>
    /// <param name="client">The client.</param>
    private static void RegisterBitTag(ABLogicalTagClient client) =>
        client.RegisterTag(
            new LogicalTag(
                BitTagName,
                BitPhysicalTagName,
                "bool",
                new LogicalTagOptions
                {
                    GroupName = ABPlcAdvancedCoverageTests.GroupName,
                    Metadata = new Dictionary<string, string>(StringComparer.Ordinal) { [BitTagName] = "0" },
                }));

    /// <summary>Asserts logical sync and async observation surfaces.</summary>
    /// <param name="simulator">The simulator.</param>
    /// <param name="client">The logical client.</param>
    /// <returns><see cref="Task"/> representing the assertion.</returns>
    private static async Task AssertLogicalObservationsAsync(
        ABPlcSimulator simulator,
        ABLogicalTagClient client)
    {
        var observedValue = new TaskCompletionSource<LogicalTagValue>(TaskCreationOptions.RunContinuationsAsynchronously);
        var observedBit = new TaskCompletionSource<LogicalTagValue>(TaskCreationOptions.RunContinuationsAsynchronously);
        using var valueSubscription = client.Observe(ValueTagName).Subscribe(
            new CaptureObserver<LogicalTagValue>(value => observedValue.TrySetResult(value)));
        using var bitSubscription = client.Observe(BitTagName).Subscribe(
            new CaptureObserver<LogicalTagValue>(value => observedBit.TrySetResult(value)));
        _ = simulator.Read(ValueTagName);
        _ = simulator.Read(BitTagName);
        await Assert.That((await TestCompatibility.WaitAsync(observedValue.Task, OperationTimeout)).TagName)
            .IsEqualTo(ValueTagName);
        await Assert.That((await TestCompatibility.WaitAsync(observedBit.Task, OperationTimeout)).TagName)
            .IsEqualTo(BitTagName);

        _ = client.ObserveMany([]);
        _ = client.ObserveMany([ValueTagName, BitTagName]);
        using var cancellation = new CancellationTokenSource(OperationTimeout);
        await using var one = client.ObserveAsync(ValueTagName, cancellation.Token).GetAsyncEnumerator();
        _ = simulator.Read(ValueTagName);
        await Assert.That(await one.MoveNextAsync()).IsTrue();
        await using var many = client
            .ObserveManyAsync([ValueTagName, BitTagName], cancellation.Token)
            .GetAsyncEnumerator();
        _ = simulator.Read(ValueTagName);
        _ = simulator.Read(BitTagName);
        await Assert.That(await many.MoveNextAsync()).IsTrue();
    }

    /// <summary>Asserts catalog/controller divergence produces empty-result failures.</summary>
    /// <param name="simulator">The simulator.</param>
    /// <param name="client">The logical client.</param>
    /// <param name="now">The logical value timestamp.</param>
    /// <returns><see cref="Task"/> representing the assertion.</returns>
    private static async Task AssertMissingControllerRegistrationAsync(
        ABPlcSimulator simulator,
        ABLogicalTagClient client,
        DateTimeOffset now)
    {
        await Assert.That(simulator.RemoveTagItem(ValueTagName)).IsTrue();
        var missingWrite = await client.WriteAsync(new LogicalTagValue(ValueTagName, SampleValue, now));
        var missingRead = await client.ReadAsync(ValueTagName);
        var missingBulkWrite = await client.WriteManyAsync(
            [new LogicalTagValue(ValueTagName, SampleValue, now)]);
        var missingBulkRead = await client.ReadManyAsync([ValueTagName]);

        await Assert.That(missingWrite.Succeeded).IsFalse();
        await Assert.That(missingRead.Succeeded).IsFalse();
        await Assert.That(missingBulkWrite.Single().Succeeded).IsFalse();
        await Assert.That(missingBulkRead.Single().Succeeded).IsFalse();
    }

    /// <summary>Registers and seeds one integral bit projection.</summary>
    /// <typeparam name="T">The physical integral type.</typeparam>
    /// <param name="simulator">The simulator.</param>
    /// <param name="client">The logical client.</param>
    /// <param name="name">The logical name.</param>
    /// <param name="address">The physical name.</param>
    /// <param name="dataType">The configured data type.</param>
    /// <param name="value">The seeded physical value.</param>
    /// <returns>The registered logical projection name.</returns>
    private static string RegisterProjection<T>(
        ABPlcSimulator simulator,
        ABLogicalTagClient client,
        string name,
        string address,
        string dataType,
        T value)
    {
        client.RegisterTag(
            new LogicalTag(
                name,
                address,
                dataType,
                new LogicalTagOptions
                {
                    Metadata = new Dictionary<string, string>(StringComparer.Ordinal) { [BitTagName] = "0" },
                }));
        simulator.SetTagValue(address, value);
        return name;
    }

    /// <summary>Asserts empty-many and future-group observable branches.</summary>
    /// <param name="facade">The facade.</param>
    /// <returns><see cref="Task"/> representing the assertion.</returns>
    private static async Task AssertEmptyAndFutureGroupObservationAsync(PlcController facade)
    {
        var empty = new TaskCompletionSource<IReadOnlyDictionary<string, object?>>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        using var emptySubscription = facade.ObserveMany("Missing").Subscribe(
            new CaptureObserver<IReadOnlyDictionary<string, object?>>(value => empty.TrySetResult(value)));
        await Assert.That(await TestCompatibility.WaitAsync(empty.Task, OperationTimeout)).IsEmpty();

        var observed = new TaskCompletionSource<IPlcTag>(TaskCreationOptions.RunContinuationsAsynchronously);
        facade.AddUpdateTagItem<int>("Grouped", "GroupedPhysical", GroupName, default);
        facade.ScanEnabled = false;
        using var groupSubscription = facade.ObserveGroup(GroupName).Subscribe(
            new CaptureObserver<IPlcTag>(tag => observed.TrySetResult(tag)));
        facade.AddUpdateTagItem<int>(FutureTagName, "FuturePhysical", GroupName, default);
        _ = facade.Read(FutureTagName);
        await Assert.That((await TestCompatibility.WaitAsync(observed.Task, OperationTimeout)).Variable).IsEqualTo(FutureTagName);
    }

    /// <summary>Creates a fast ControlLogix simulator.</summary>
    /// <returns>The simulator.</returns>
    private static ABPlcSimulator CreateSimulator() =>
        new(PlcType.LGX, ScanInterval, OperationTimeout, "1,0", TimeProvider.System);

    /// <summary>Observer that forwards values to an action.</summary>
    /// <typeparam name="T">The observed value type.</typeparam>
    /// <param name="onNext">The forwarding action.</param>
    private sealed class CaptureObserver<T>(Action<T> onNext) : IObserver<T>
    {
        /// <inheritdoc/>
        public void OnCompleted()
        {
        }

        /// <inheritdoc/>
        public void OnError(Exception error)
        {
        }

        /// <inheritdoc/>
        public void OnNext(T value) => onNext(value);
    }
}
