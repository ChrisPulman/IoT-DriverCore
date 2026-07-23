// Copyright (c) 2019-2026 Chris Pulman and contributors. All rights reserved.
// Chris Pulman and contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Net;
using IoT.DriverCore.ModbusRx.Device;
using IoT.DriverCore.ModbusRx.IO;
using IoT.DriverCore.ModbusRx.Message;
using IoT.DriverCore.Serial;
using Moq;
using ReactiveUI.Primitives.Async;
using AsyncModbus = IoT.DriverCore.ModbusRx.ModbusAsyncObservableExtensions;
using NativeAssert = TUnit.Assertions.Assert;

namespace IoT.DriverCore.ModbusRx.UnitTests;

/// <summary>Deterministic coverage for synchronous and asynchronous reactive Modbus adapters.</summary>
public sealed class ReactiveAdapterCoverageTests
{
    /// <summary>The unit identifier used by disconnected read adapters.</summary>
    private const byte UnitId = 1;

    /// <summary>The number of points used by disconnected read adapters.</summary>
    private const ushort PointCount = 2;

    /// <summary>The polling interval that is never reached by disconnected sources.</summary>
    private const double DormantInterval = 60_000;

    /// <summary>The scalar value used to verify both adapter directions.</summary>
    private const int ExpectedValue = 42;

    /// <summary>The register value written through reactive slave adapters.</summary>
    private const ushort RegisterValue = 321;

    /// <summary>Round-trips a value through both adapter directions.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [TUnit.Core.Test]
    public async Task ObservableAdapters_RoundTripValuesAsync()
    {
        var asyncSource = AsyncModbus.ToAsyncObservable(Observable.Return(ExpectedValue));
        var value = await AsyncModbus.ToObservable(asyncSource).FirstAsync();

        await NativeAssert.That(value).IsEqualTo(ExpectedValue);
    }

    /// <summary>Exercises cancellation, terminal notifications, connect failures, races, and idempotent disposal.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [TUnit.Core.Test]
    public async Task AsyncToSyncAdapter_ExercisesLifecycleEdgesAsync()
    {
        var source = new ManualAsyncObservable<int>();
        var values = new List<int>();
        var errors = new List<Exception>();
        var completed = 0;
        var subscription = AsyncModbus.ToObservable(source)
            .Subscribe(values.Add, errors.Add, () => completed++);
        var observer = await source.Observer.Task.WaitAsync(TimeSpan.FromSeconds(1));

        await observer.OnNextAsync(ExpectedValue, CancellationToken.None);
        await observer.OnErrorResumeAsync(new IOException("forwarded"), CancellationToken.None);
        await observer.OnCompletedAsync(ReactiveUI.Primitives.Result.Success);
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        await observer.OnNextAsync(ExpectedValue, cancellation.Token);
        await observer.OnErrorResumeAsync(new IOException("ignored"), cancellation.Token);

        await NativeAssert.That(values).IsEquivalentTo([ExpectedValue]);
        await NativeAssert.That(errors.Count).IsEqualTo(UnitId);
        await NativeAssert.That(completed).IsEqualTo(0);

        subscription.Dispose();
        subscription.Dispose();
        await observer.OnNextAsync(ExpectedValue, CancellationToken.None);
        await observer.OnErrorResumeAsync(new IOException("disposed"), CancellationToken.None);
        await observer.OnCompletedAsync(ReactiveUI.Primitives.Result.Success);

        var delayed = new DelayedAsyncObservable<int>();
        var delayedSubscription = AsyncModbus.ToObservable(delayed).Subscribe(_ => { });
        delayedSubscription.Dispose();
        _ = delayed.Release();
        await delayed.SubscriptionDisposed.Task.WaitAsync(TimeSpan.FromSeconds(1));

        var connectError = new TaskCompletionSource<Exception>(TaskCreationOptions.RunContinuationsAsynchronously);
        using var failed = AsyncModbus.ToObservable(new FaultingAsyncObservable<int>())
            .Subscribe(_ => { }, exception => connectError.TrySetResult(exception));
        await NativeAssert.That(await connectError.Task.WaitAsync(TimeSpan.FromSeconds(1)))
            .IsTypeOf<IOException>();
    }

    /// <summary>Exercises synchronous-source cancellation, errors, and incomplete async observer work.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [TUnit.Core.Test]
    public async Task SyncToAsyncAdapter_ExercisesObserverEdgesAsync()
    {
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        var canceledObserver = new RecordingAsyncObserver<int>();
        await using var canceled = await AsyncModbus
            .ToAsyncObservable(Observable.Return(ExpectedValue))
            .SubscribeAsync(canceledObserver, cancellation.Token);

        var errorObserver = new RecordingAsyncObserver<int>();
        await using var failed = await AsyncModbus
            .ToAsyncObservable(Observable.Throw<int>(new IOException("source")))
            .SubscribeAsync(errorObserver, CancellationToken.None);

        var delayedObserver = new RecordingAsyncObserver<int>(delayNext: true);
        await using var delayed = await AsyncModbus
            .ToAsyncObservable(Observable.Return(ExpectedValue))
            .SubscribeAsync(delayedObserver, CancellationToken.None);
        delayedObserver.ReleaseNext();
        await delayedObserver.NextCompleted.Task.WaitAsync(TimeSpan.FromSeconds(1));

        await NativeAssert.That(canceledObserver.Values).IsEmpty();
        await NativeAssert.That(canceledObserver.Completed).IsTrue();
        await NativeAssert.That(errorObserver.Error).IsTypeOf<IOException>();
        await NativeAssert.That(delayedObserver.Values).IsEquivalentTo([ExpectedValue]);
    }

    /// <summary>Exercises every IP read adapter with a deterministic disconnected source.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [TUnit.Core.Test]
    public async Task IpReadAdapters_ForwardConnectionErrorsAsync()
    {
        var error = new InvalidOperationException("offline");
        var source = Observable.Return((false, (Exception?)error, (ModbusIpMaster?)null));
        var asyncSource = AsyncModbus.ToModbusObservable(source);

        var coil = await ObserveFirstAsync(AsyncModbus.ReadCoils(asyncSource, 0, PointCount, DormantInterval));
        var input = await ObserveFirstAsync(AsyncModbus.ReadInputs(asyncSource, 0, PointCount, DormantInterval));
        var holding = await ObserveFirstAsync(
            AsyncModbus.ReadHoldingRegisters(asyncSource, 0, PointCount, DormantInterval));
        var inputRegister = await ObserveFirstAsync(
            AsyncModbus.ReadInputRegisters(asyncSource, 0, PointCount, DormantInterval));

        await NativeAssert.That(coil.Error).IsSameReferenceAs(error);
        await NativeAssert.That(input.Error).IsSameReferenceAs(error);
        await NativeAssert.That(holding.Error).IsSameReferenceAs(error);
        await NativeAssert.That(inputRegister.Error).IsSameReferenceAs(error);
        await AssertSynchronousIpAdaptersAsync(source, error);
    }

    /// <summary>Exercises every serial read adapter with a deterministic disconnected source.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [TUnit.Core.Test]
    public async Task SerialReadAdapters_ForwardConnectionErrorsAsync()
    {
        var error = new InvalidOperationException("offline");
        var source = Observable.Return((false, (Exception?)error, (IModbusSerialMaster?)null));
        var asyncSource = AsyncModbus.ToModbusObservable(source);

        var coil = await ObserveFirstAsync(
            AsyncModbus.ReadCoils(asyncSource, UnitId, 0, PointCount, DormantInterval));
        var input = await ObserveFirstAsync(
            AsyncModbus.ReadInputs(asyncSource, UnitId, 0, PointCount, DormantInterval));
        var holding = await ObserveFirstAsync(
            AsyncModbus.ReadHoldingRegisters(asyncSource, UnitId, 0, PointCount, DormantInterval));
        var inputRegister = await ObserveFirstAsync(
            AsyncModbus.ReadInputRegisters(asyncSource, UnitId, 0, PointCount, DormantInterval));

        await NativeAssert.That(coil.Error).IsSameReferenceAs(error);
        await NativeAssert.That(input.Error).IsSameReferenceAs(error);
        await NativeAssert.That(holding.Error).IsSameReferenceAs(error);
        await NativeAssert.That(inputRegister.Error).IsSameReferenceAs(error);
        await AssertSynchronousSerialAdaptersAsync(source, error);
    }

    /// <summary>Creates every server-observation and asynchronous slave-write adapter.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [TUnit.Core.Test]
    public async Task ServerAndSlaveAdapters_CreateAllObservableShapesAsync()
    {
        using var server = new ModbusServer();
        using var listener = new TestTcpListener(IPAddress.Loopback, 0);
        using var tcpSlave = ModbusTcpSlave.CreateTcp(UnitId, listener);

        var observations = CreateServerObservations(server, tcpSlave);
        var writes = CreateSlaveWrites();

        await NativeAssert.That(observations.All(static value => value is not null)).IsTrue();
        await NativeAssert.That(writes.All(static value => value is not null)).IsTrue();
    }

    /// <summary>Writes every data area through TCP, UDP, and serial reactive slave adapters.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [TUnit.Core.Test]
    public async Task SlaveWriteAdapters_UpdateAllInMemoryDataAreasAsync()
    {
        using var listener = new TestTcpListener(IPAddress.Loopback, 0);
        using var tcpSlave = ModbusTcpSlave.CreateTcp(UnitId, listener);
        using var udpClient = new UdpClientRx();
        using var udpSlave = ModbusUdpSlave.CreateUdp(UnitId, udpClient);
        using var serialSlave = ModbusSerialSlave.CreateRtu(UnitId, new Mock<IStreamResource>().Object);

        ApplyWrites(tcpSlave);
        ApplyWrites(udpSlave);
        ApplyWrites(serialSlave);

        await AssertWrittenValuesAsync(tcpSlave);
        await AssertWrittenValuesAsync(udpSlave);
        await AssertWrittenValuesAsync(serialSlave);
    }

    /// <summary>Exercises float and double conversion facades and their span boundary behavior.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [TUnit.Core.Test]
    public async Task ConversionAdapters_RoundTripAndValidateBuffersAsync()
    {
        var doubles = new ushort[4];
        var singles = new ushort[PointCount];
        CreateExtensions.FromDouble(Math.PI, doubles, 0, true);
        CreateExtensions.FromFloat((float)Math.PI, singles, 0, false);
        var doubleFromArray = CreateExtensions.ToDouble(doubles, 0, true);
        var doubleFromSpan = CreateExtensions.ToDouble((ReadOnlySpan<ushort>)doubles, 0, true);
        var floatFromArray = CreateExtensions.ToFloat(singles, 0, false);
        var floatFromSpan = CreateExtensions.ToFloat((ReadOnlySpan<ushort>)singles, 0, false);
        CreateExtensions.FromDouble(Math.PI, doubles.AsSpan(), 0, false);
        CreateExtensions.FromFloat((float)Math.PI, singles.AsSpan(), 0, true);
        Create.FromDoubleCore(Math.PI, new ushort[1], 0, false);
        Create.FromFloatCore((float)Math.PI, null!, 0, false);

        await NativeAssert.That(doubleFromArray).IsEqualTo(Math.PI);
        await NativeAssert.That(doubleFromSpan).IsEqualTo(Math.PI);
        await NativeAssert.That(floatFromArray).IsEqualTo((float)Math.PI);
        await NativeAssert.That(floatFromSpan).IsEqualTo((float)Math.PI);
        await NativeAssert.That(CreateExtensions.ToDouble(null, 0, false)).IsNull();
        await NativeAssert.That(CreateExtensions.ToDouble(new ushort[1], 0, false)).IsNull();
        await NativeAssert.That(CreateExtensions.ToFloat(null, 0, false)).IsNull();
        await NativeAssert.That(CreateExtensions.ToFloat([], 0, false)).IsNull();
        await NativeAssert.That(() => CreateExtensions.FromDouble(Math.PI, null!, 0, false))
            .Throws<ArgumentNullException>();
        await NativeAssert.That(() => CreateExtensions.FromFloat((float)Math.PI, null!, 0, false))
            .Throws<ArgumentNullException>();
        await NativeAssert.That(
                () => CreateExtensions.FromDouble(Math.PI, new ushort[1], 0, false))
            .Throws<ArgumentException>();
        await NativeAssert.That(
                () => CreateExtensions.FromFloat((float)Math.PI, new ushort[1], 0, false))
            .Throws<ArgumentException>();
    }

    /// <summary>Observes request, completion, read, and write events from a loopback slave.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [TUnit.Core.Test]
    public async Task SlaveEventAdapters_ForwardEveryEventTypeAsync()
    {
        using var listener = new TestTcpListener(IPAddress.Loopback, 0);
        using var slave = ModbusTcpSlave.CreateTcp(UnitId, listener);
        var requests = 0;
        var completions = 0;
        var reads = 0;
        var writes = 0;
        using var requestSubscription = CreateExtensions.ObserveRequest(slave).Subscribe(_ => requests++);
        using var completionSubscription = CreateExtensions.ObserveWriteComplete(slave)
            .Subscribe(_ => completions++);
        using var readSubscription = CreateExtensions.ObserveDataStoreReadFrom(slave).Subscribe(_ => reads++);
        using var writeSubscription = CreateExtensions.ObserveDataStoreWrittenTo(slave).Subscribe(_ => writes++);

        _ = slave.ApplyRequest(new ReadCoilsInputsRequest(Modbus.ReadCoils, UnitId, 0, PointCount));
        _ = slave.ApplyRequest(new WriteSingleCoilRequestResponse(UnitId, 0, true));

        await NativeAssert.That(requests).IsEqualTo(PointCount);
        await NativeAssert.That(completions).IsEqualTo(UnitId);
        await NativeAssert.That(reads).IsEqualTo(UnitId);
        await NativeAssert.That(writes).IsEqualTo(UnitId);
    }

    /// <summary>Asserts the synchronous-source IP overloads preserve the source error.</summary>
    /// <param name="source">The disconnected source.</param>
    /// <param name="error">The expected error.</param>
    /// <returns>A task representing the asynchronous assertion.</returns>
    private static async Task AssertSynchronousIpAdaptersAsync(
        IObservable<(bool Connected, Exception? Error, ModbusIpMaster? Master)> source,
        Exception error)
    {
        var results = new[]
        {
            (await ObserveFirstAsync(AsyncModbus.ReadCoilsObservable(source, 0, PointCount, DormantInterval))).Error,
            (await ObserveFirstAsync(AsyncModbus.ReadInputsObservable(source, 0, PointCount, DormantInterval))).Error,
            (await ObserveFirstAsync(
                AsyncModbus.ReadHoldingRegistersObservable(source, 0, PointCount, DormantInterval))).Error,
            (await ObserveFirstAsync(
                AsyncModbus.ReadInputRegistersObservable(source, 0, PointCount, DormantInterval))).Error,
        };

        await NativeAssert.That(results.All(value => ReferenceEquals(value, error))).IsTrue();
    }

    /// <summary>Asserts the synchronous-source serial overloads preserve the source error.</summary>
    /// <param name="source">The disconnected source.</param>
    /// <param name="error">The expected error.</param>
    /// <returns>A task representing the asynchronous assertion.</returns>
    private static async Task AssertSynchronousSerialAdaptersAsync(
        IObservable<(bool Connected, Exception? Error, IModbusSerialMaster? Master)> source,
        Exception error)
    {
        var results = new[]
        {
            (await ObserveFirstAsync(
                AsyncModbus.ReadCoilsObservable(source, UnitId, 0, PointCount, DormantInterval))).Error,
            (await ObserveFirstAsync(
                AsyncModbus.ReadInputsObservable(source, UnitId, 0, PointCount, DormantInterval))).Error,
            (await ObserveFirstAsync(
                AsyncModbus.ReadHoldingRegistersObservable(
                    source,
                    UnitId,
                    0,
                    PointCount,
                    DormantInterval))).Error,
            (await ObserveFirstAsync(
                AsyncModbus.ReadInputRegistersObservable(
                    source,
                    UnitId,
                    0,
                    PointCount,
                    DormantInterval))).Error,
        };

        await NativeAssert.That(results.All(value => ReferenceEquals(value, error))).IsTrue();
    }

    /// <summary>Creates every public async server observation wrapper.</summary>
    /// <param name="server">The in-memory server.</param>
    /// <param name="slave">The loopback slave.</param>
    /// <returns>The adapter objects.</returns>
    private static object[] CreateServerObservations(ModbusServer server, ModbusSlave slave) =>
    [
        AsyncModbus.ObserveCoilsObservable(server, 0, PointCount, DormantInterval),
        AsyncModbus.ObserveDataChangesObservable(server, DormantInterval),
        AsyncModbus.ObserveDataStoreReadFromObservable(slave),
        AsyncModbus.ObserveDataStoreWrittenToObservable(slave),
        AsyncModbus.ObserveDiscreteInputsObservable(server, 0, PointCount, DormantInterval),
        AsyncModbus.ObserveHoldingRegistersObservable(server, 0, PointCount, DormantInterval),
        AsyncModbus.ObserveInputRegistersObservable(server, 0, PointCount, DormantInterval),
        AsyncModbus.ObserveRequestObservable(slave),
        AsyncModbus.ObserveWriteCompleteObservable(slave),
    ];

    /// <summary>Creates every public async slave write wrapper without external hardware.</summary>
    /// <returns>The adapter objects.</returns>
    private static object[] CreateSlaveWrites()
    {
        var serial = ToEmptyObservable<ModbusSerialSlave>();
        var tcp = ToEmptyObservable<ModbusTcpSlave>();
        var udp = ToEmptyObservable<ModbusUdpSlave>();
        var bits = ToEmptyObservable<bool[]>();
        var registers = ToEmptyObservable<ushort[]>();

        return
        [
            AsyncModbus.WriteCoilDiscretes(serial, 0, bits),
            AsyncModbus.WriteCoilDiscretes(tcp, 0, bits),
            AsyncModbus.WriteCoilDiscretes(udp, 0, bits),
            AsyncModbus.WriteHoldingRegisters(serial, 0, registers),
            AsyncModbus.WriteHoldingRegisters(tcp, 0, registers),
            AsyncModbus.WriteHoldingRegisters(udp, 0, registers),
            AsyncModbus.WriteInputDiscretes(serial, 0, bits),
            AsyncModbus.WriteInputDiscretes(tcp, 0, bits),
            AsyncModbus.WriteInputDiscretes(udp, 0, bits),
            AsyncModbus.WriteInputRegisters(serial, 0, registers),
            AsyncModbus.WriteInputRegisters(tcp, 0, registers),
            AsyncModbus.WriteInputRegisters(udp, 0, registers),
        ];
    }

    /// <summary>Applies all reactive writes to a TCP slave.</summary>
    /// <param name="slave">The target slave.</param>
    private static void ApplyWrites(ModbusTcpSlave slave)
    {
        var writer = new ModbusTcpSlaveExtensions(UnitId);
        _ = writer.WriteCoilDiscretes(Observable.Return(slave), 0, Observable.Return<bool[]>([true]));
        _ = writer.WriteInputDiscretes(Observable.Return(slave), 0, Observable.Return<bool[]>([true]));
        _ = writer.WriteHoldingRegisters(
            Observable.Return(slave),
            0,
            Observable.Return<ushort[]>([RegisterValue]));
        _ = writer.WriteInputRegisters(
            Observable.Return(slave),
            0,
            Observable.Return<ushort[]>([RegisterValue]));
    }

    /// <summary>Applies all reactive writes to a UDP slave.</summary>
    /// <param name="slave">The target slave.</param>
    private static void ApplyWrites(ModbusUdpSlave slave)
    {
        var writer = new ModbusUdpSlaveExtensions(UnitId);
        _ = writer.WriteCoilDiscretes(Observable.Return(slave), 0, Observable.Return<bool[]>([true]));
        _ = writer.WriteInputDiscretes(Observable.Return(slave), 0, Observable.Return<bool[]>([true]));
        _ = writer.WriteHoldingRegisters(
            Observable.Return(slave),
            0,
            Observable.Return<ushort[]>([RegisterValue]));
        _ = writer.WriteInputRegisters(
            Observable.Return(slave),
            0,
            Observable.Return<ushort[]>([RegisterValue]));
    }

    /// <summary>Applies all reactive writes to a serial slave.</summary>
    /// <param name="slave">The target slave.</param>
    private static void ApplyWrites(ModbusSerialSlave slave)
    {
        var writer = new ModbusSerialSlaveExtensions(UnitId);
        _ = writer.WriteCoilDiscretes(Observable.Return(slave), 0, Observable.Return<bool[]>([true]));
        _ = writer.WriteInputDiscretes(Observable.Return(slave), 0, Observable.Return<bool[]>([true]));
        _ = writer.WriteHoldingRegisters(
            Observable.Return(slave),
            0,
            Observable.Return<ushort[]>([RegisterValue]));
        _ = writer.WriteInputRegisters(
            Observable.Return(slave),
            0,
            Observable.Return<ushort[]>([RegisterValue]));
    }

    /// <summary>Asserts a slave received each reactive data-area write.</summary>
    /// <param name="slave">The updated slave.</param>
    /// <returns>A task representing the asynchronous assertion.</returns>
    private static async Task AssertWrittenValuesAsync(ModbusSlave slave)
    {
        await NativeAssert.That(slave.DataStore.CoilDiscretes[UnitId]).IsTrue();
        await NativeAssert.That(slave.DataStore.InputDiscretes[UnitId]).IsTrue();
        await NativeAssert.That(slave.DataStore.HoldingRegisters[UnitId]).IsEqualTo(RegisterValue);
        await NativeAssert.That(slave.DataStore.InputRegisters[UnitId]).IsEqualTo(RegisterValue);
    }

    /// <summary>Converts an empty observable to an async observable.</summary>
    /// <typeparam name="T">The item type.</typeparam>
    /// <returns>The empty async observable.</returns>
    private static IObservableAsync<T> ToEmptyObservable<T>() =>
        AsyncModbus.ToAsyncObservable(Observable.Empty<T>());

    /// <summary>Converts an async observable and awaits its first value.</summary>
    /// <typeparam name="T">The item type.</typeparam>
    /// <param name="source">The async source.</param>
    /// <returns>The first source value.</returns>
    private static Task<T> ObserveFirstAsync<T>(IObservableAsync<T> source) =>
        AsyncModbus.ToObservable(source).FirstAsync();

    /// <summary>Captures an async observer and returns a tracked subscription.</summary>
    /// <typeparam name="T">The observed value type.</typeparam>
    private sealed class ManualAsyncObservable<T> : IObservableAsync<T>
    {
        /// <summary>Gets the captured observer.</summary>
        internal TaskCompletionSource<IObserverAsync<T>> Observer { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        /// <inheritdoc />
        public ValueTask<IAsyncDisposable> SubscribeAsync(
            IObserverAsync<T> observer,
            CancellationToken cancellationToken)
        {
            _ = Observer.TrySetResult(observer);
            return new ValueTask<IAsyncDisposable>(new TrackedAsyncDisposable());
        }
    }

    /// <summary>Delays subscription completion until released by the test.</summary>
    /// <typeparam name="T">The observed value type.</typeparam>
    private sealed class DelayedAsyncObservable<T> : IObservableAsync<T>
    {
        /// <summary>The pending subscription completion.</summary>
        private readonly TaskCompletionSource<IAsyncDisposable> _subscription =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        /// <summary>Gets a completion source that records disposal of the eventual subscription.</summary>
        internal TaskCompletionSource<bool> SubscriptionDisposed { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        /// <inheritdoc />
        public ValueTask<IAsyncDisposable> SubscribeAsync(
            IObserverAsync<T> observer,
            CancellationToken cancellationToken) =>
            new(_subscription.Task);

        /// <summary>Completes the pending subscription.</summary>
        /// <returns>True when the subscription was completed by this call.</returns>
        internal bool Release() => _subscription.TrySetResult(new TrackedAsyncDisposable(SubscriptionDisposed));
    }

    /// <summary>Fails while connecting an asynchronous observer.</summary>
    /// <typeparam name="T">The observed value type.</typeparam>
    private sealed class FaultingAsyncObservable<T> : IObservableAsync<T>
    {
        /// <inheritdoc />
        public ValueTask<IAsyncDisposable> SubscribeAsync(
            IObserverAsync<T> observer,
            CancellationToken cancellationToken) =>
            new(Task.FromException<IAsyncDisposable>(new IOException("connect")));
    }

    /// <summary>Records async observer calls and optionally delays the next-value completion.</summary>
    /// <typeparam name="T">The observed value type.</typeparam>
    /// <param name="delayNext">Whether next-value completion waits for an explicit release.</param>
    private sealed class RecordingAsyncObserver<T>(bool delayNext = false) : IObserverAsync<T>
    {
        /// <summary>The optional next-value gate.</summary>
        private readonly TaskCompletionSource<bool>? _nextGate = delayNext
            ? new(TaskCreationOptions.RunContinuationsAsynchronously)
            : null;

        /// <summary>Gets recorded values.</summary>
        internal List<T> Values { get; } = [];

        /// <summary>Gets the recorded error.</summary>
        internal Exception? Error { get; private set; }

        /// <summary>Gets a value indicating whether completion was observed.</summary>
        internal bool Completed { get; private set; }

        /// <summary>Gets completion of delayed next-value work.</summary>
        internal TaskCompletionSource<bool> NextCompleted { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        /// <inheritdoc />
        public async ValueTask OnNextAsync(T value, CancellationToken cancellationToken)
        {
            Values.Add(value);
            if (_nextGate is not null)
            {
                await _nextGate.Task;
            }

            _ = NextCompleted.TrySetResult(true);
        }

        /// <inheritdoc />
        public ValueTask OnErrorResumeAsync(Exception error, CancellationToken cancellationToken)
        {
            Error = error;
            return default;
        }

        /// <inheritdoc />
        public ValueTask OnCompletedAsync(ReactiveUI.Primitives.Result result)
        {
            Completed = true;
            return default;
        }

        /// <inheritdoc />
        public ValueTask DisposeAsync() => default;

        /// <summary>Releases delayed next-value work.</summary>
        internal void ReleaseNext() => _nextGate?.TrySetResult(true);
    }

    /// <summary>Tracks asynchronous disposal.</summary>
    /// <param name="disposed">The optional disposal completion source.</param>
    private sealed class TrackedAsyncDisposable(TaskCompletionSource<bool>? disposed = null) : IAsyncDisposable
    {
        /// <inheritdoc />
        public ValueTask DisposeAsync()
        {
            _ = disposed?.TrySetResult(true);
            return default;
        }
    }
}
