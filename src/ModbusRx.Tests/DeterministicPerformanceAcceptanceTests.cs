// Copyright (c) 2019-2026 Chris Pulman and contributors. All rights reserved.
// Chris Pulman and contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Net;
using System.Net.Sockets;
using IoT.DriverCore.ModbusRx.Data;
using IoT.DriverCore.ModbusRx.Device;
using IoT.DriverCore.ModbusRx.IO;
using NativeAssert = TUnit.Assertions.Assert;

namespace IoT.DriverCore.ModbusRx.UnitTests;

/// <summary>Deterministic performance acceptance coverage for Modbus range, pooling, observation, and lifecycle work.</summary>
public sealed class DeterministicPerformanceAcceptanceTests
{
    /// <summary>The representative slave unit identifier.</summary>
    private const byte UnitId = 1;

    /// <summary>The first legal Modbus data address.</summary>
    private const ushort FirstAddress = 0;

    /// <summary>The three-point range used by range-operation tests.</summary>
    private const ushort ThreePoints = 3;

    /// <summary>The first register value.</summary>
    private const ushort FirstValue = 11;

    /// <summary>The second register value.</summary>
    private const ushort SecondValue = 22;

    /// <summary>The third register value.</summary>
    private const ushort ThirdValue = 33;

    /// <summary>The compact data-store capacity.</summary>
    private const ushort StoreCapacity = 8;

    /// <summary>The byte-buffer rent length.</summary>
    private const int ByteBufferLength = 8;

    /// <summary>The word and boolean-buffer rent length.</summary>
    private const int SmallBufferLength = 4;

    /// <summary>The first integer used by copy coverage.</summary>
    private const int IntegerOne = 1;

    /// <summary>The second integer used by copy coverage.</summary>
    private const int IntegerTwo = 2;

    /// <summary>The third integer used by copy coverage.</summary>
    private const int IntegerThree = 3;

    /// <summary>The fourth integer used by copy coverage.</summary>
    private const int IntegerFour = 4;

    /// <summary>The expected range copy count.</summary>
    private const long SixElementCopies = 6L;

    /// <summary>The expected three-rent and three-return count.</summary>
    private const long ThreeOperations = 3L;

    /// <summary>Proves a range write and read perform one logical operation and one element copy per value.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [TUnit.Core.Test]
    public async Task DataStore_RangeOperationsExposeExactCopyAndMaterializationMetricsAsync()
    {
        using var dataStore = DataStoreFactory.CreateDefaultDataStore(
            StoreCapacity,
            StoreCapacity,
            StoreCapacity,
            StoreCapacity);
        var values = new[] { FirstValue, SecondValue, ThirdValue };

        dataStore.WriteDataOptimized(values, dataStore.HoldingRegisters, FirstAddress);
        var result = dataStore.ReadDataOptimized(
            dataStore.HoldingRegisters,
            static () => new RegisterCollection(),
            FirstAddress,
            ThreePoints);
        var metrics = dataStore.GetOperationMetrics();

        await NativeAssert.That(result).IsEquivalentTo(values);
        await NativeAssert.That(metrics.WriteOperations).IsEqualTo(1L);
        await NativeAssert.That(metrics.ReadOperations).IsEqualTo(1L);
        await NativeAssert.That(metrics.ElementCopies).IsEqualTo(SixElementCopies);
        await NativeAssert.That(metrics.ResultCollectionAllocations).IsEqualTo(1L);
        await NativeAssert.That(metrics.InputMaterializations).IsEqualTo(0L);
    }

    /// <summary>Proves pooled rents, returns, and tracked copies report exact deterministic work counts.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [TUnit.Core.Test]
    public async Task BufferManager_ReportsExactRentReturnAndCopyMetricsAsync()
    {
        using var manager = new ModbusBufferManager();
        var bytes = manager.RentByteBuffer(ByteBufferLength);
        var registers = manager.RentUshortBuffer(SmallBufferLength);
        var coils = manager.RentBoolBuffer(SmallBufferLength);
        var destination = new int[ThreePoints];

        var copied = manager.CopyDataAndTrack([IntegerOne, IntegerTwo, IntegerThree, IntegerFour], 0, destination, 0, ThreePoints);
        manager.ReturnByteBuffer(bytes, false);
        manager.ReturnUshortBuffer(registers, false);
        manager.ReturnBoolBuffer(coils, false);
        var metrics = manager.GetMetrics();

        await NativeAssert.That(copied).IsEqualTo((int)ThreePoints);
        await NativeAssert.That(metrics.RentOperations).IsEqualTo(ThreeOperations);
        await NativeAssert.That(metrics.ReturnOperations).IsEqualTo(ThreeOperations);
        await NativeAssert.That(metrics.CopyOperations).IsEqualTo(1L);
        await NativeAssert.That(metrics.CopiedElements).IsEqualTo(ThreeOperations);
#if NET8_0_OR_GREATER
        await NativeAssert.That(metrics.DedicatedAllocations).IsEqualTo(0L);
#else
        await NativeAssert.That(metrics.DedicatedAllocations).IsEqualTo(ThreeOperations);
#endif
    }

    /// <summary>Proves event-driven observation emits exactly once per completed write without a timer threshold.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [TUnit.Core.Test]
    public async Task EventDrivenObservation_EmitsOneSnapshotWithExactMetricsAsync()
    {
        using var server = new ModbusServer();
        var metrics = new ModbusObservationMetrics();
        var observed = new TaskCompletionSource<ModbusServerDataSnapshot>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        using var subscription = EnhancedModbusServerExtensions
            .ObserveDataChangesEventDriven(server, new FixedTimeProvider(), metrics)
            .Subscribe(snapshot => _ = observed.TrySetResult(snapshot));

        server.DataStore!.WriteDataOptimized(
            [FirstValue],
            server.DataStore.HoldingRegisters,
            FirstAddress);
        var snapshot = await observed.Task;

        await NativeAssert.That(snapshot.HoldingRegisters[0]).IsEqualTo(FirstValue);
        await NativeAssert.That(metrics.WriteNotifications).IsEqualTo(1L);
        await NativeAssert.That(metrics.SnapshotsCreated).IsEqualTo(1L);
        await NativeAssert.That(metrics.SnapshotsEmitted).IsEqualTo(1L);
    }

    /// <summary>Proves a TCP slave accepts one listener loop and disposal completes it without a socket-race failure.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [TUnit.Core.Test]
    public async Task TcpSlave_ListenLifecycleIsIdempotentAndStopsDeterministicallyAsync()
    {
#if NET5_0_OR_GREATER
        using var listener = new TcpListener(IPAddress.Loopback, 0);
#else
        var listener = new TcpListener(IPAddress.Loopback, 0);
#endif
        using var slave = ModbusTcpSlave.CreateTcp(UnitId, listener);
        var firstListen = slave.ListenAsync();

        await NativeAssert.That(slave.IsListening).IsTrue();
        await slave.ListenAsync();
        await NativeAssert.That(slave.IsListening).IsTrue();

        slave.Dispose();
        await firstListen;
        await NativeAssert.That(slave.IsListening).IsFalse();
    }

    /// <summary>Proves a malformed serial request is discarded and the listener exits cleanly when disposed.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [TUnit.Core.Test]
    public async Task SerialSlave_MalformedRequestIsDiscardedWithoutTerminatingTheListenerAsync()
    {
        using var resource = new MalformedAsciiStreamResource();
        using var slave = ModbusSerialSlave.CreateAscii(UnitId, resource);
        var listenTask = slave.ListenAsync();

        await resource.Discarded.Task;
        await NativeAssert.That(resource.DiscardCalls).IsEqualTo(1);

        slave.Dispose();
        await listenTask;
    }

    /// <summary>Provides one malformed ASCII request then blocks until disposal.</summary>
    private sealed class MalformedAsciiStreamResource : IStreamResource
    {
        /// <summary>Stores the malformed frame characters.</summary>
        private readonly Queue<byte> _bytes = new(":Z\r\n"u8.ToArray());

        /// <summary>Completes the pending read when disposed.</summary>
        private readonly TaskCompletionSource<int> _disposed = new(TaskCreationOptions.RunContinuationsAsynchronously);

        /// <summary>Initializes a new instance of the <see cref="MalformedAsciiStreamResource"/> class.</summary>
        public MalformedAsciiStreamResource() => Discarded = new(TaskCreationOptions.RunContinuationsAsynchronously);

        /// <summary>Gets the deterministic discard signal.</summary>
        public TaskCompletionSource<bool> Discarded { get; }

        /// <summary>Gets the number of discarded malformed frames.</summary>
        public int DiscardCalls { get; private set; }

        /// <inheritdoc/>
        public int InfiniteTimeout => Timeout.Infinite;

        /// <inheritdoc/>
        public int ReadTimeout { get; set; } = Timeout.Infinite;

        /// <inheritdoc/>
        public int WriteTimeout { get; set; } = Timeout.Infinite;

        /// <inheritdoc/>
        public void DiscardInBuffer()
        {
            DiscardCalls++;
            _ = Discarded.TrySetResult(true);
        }

        /// <inheritdoc/>
        public Task<int> ReadAsync(byte[] buffer, int offset, int count)
        {
            if (_bytes.Count == 0)
            {
                return _disposed.Task;
            }

            buffer[offset] = _bytes.Dequeue();
            return Task.FromResult(1);
        }

        /// <inheritdoc/>
        public void Write(byte[] buffer, int offset, int count)
        {
        }

        /// <inheritdoc/>
        public void Dispose() => _ = _disposed.TrySetException(
            new ObjectDisposedException(nameof(MalformedAsciiStreamResource)));
    }

    /// <summary>Supplies a stable timestamp to event-driven observation tests.</summary>
    private sealed class FixedTimeProvider : TimeProvider
    {
        /// <inheritdoc/>
        public override DateTimeOffset GetUtcNow() => DateTimeOffset.FromUnixTimeSeconds(0);
    }
}
