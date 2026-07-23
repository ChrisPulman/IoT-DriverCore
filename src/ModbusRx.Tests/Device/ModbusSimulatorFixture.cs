// Copyright (c) 2019-2026 Chris Pulman and contributors. All rights reserved.
// Chris Pulman and contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using System.Threading;
using IoT.DriverCore.ModbusRx.Data;
using IoT.DriverCore.ModbusRx.Device;
using IoT.DriverCore.ModbusRx.IO;
using IoT.DriverCore.ModbusRx.Message;
using TUnitAssert = TUnit.Assertions.Assert;

namespace IoT.DriverCore.ModbusRx.UnitTests.Device;

/// <summary>Verifies the deterministic Modbus simulator and its framed in-memory transport.</summary>
public sealed class ModbusSimulatorFixture
{
    /// <summary>The simulated unit identifier.</summary>
    private const byte UnitId = 1;

    /// <summary>The alternate unit identifier used to verify gateway failures.</summary>
    private const byte OtherUnitId = 2;

    /// <summary>The number of values allocated in each test data area.</summary>
    private const ushort DataAreaSize = 32;

    /// <summary>The number of points in a two-value request.</summary>
    private const ushort TwoPoints = 2;

    /// <summary>The number of points in a three-value request.</summary>
    private const ushort ThreePoints = 3;

    /// <summary>The expected number of successful round-trip requests.</summary>
    private const int ExpectedRoundTripRequestCount = 6;

    /// <summary>The expected number of requests when one retry occurs.</summary>
    private const long ExpectedRetryRequestCount = 2;

    /// <summary>The Modbus gateway-target-device-failed-to-respond exception code.</summary>
    private const byte GatewayTargetFailureCode = 11;

    /// <summary>The configured response delay in milliseconds.</summary>
    private const int ResponseDelayMilliseconds = 20;

    /// <summary>The minimum accepted measured response delay in milliseconds.</summary>
    private const int MinimumMeasuredDelayMilliseconds = 15;

    /// <summary>The transaction identifier used by direct stream-resource tests.</summary>
    private const ushort RequestTransactionId = 42;

    /// <summary>The non-default timeout value used by stream-resource tests.</summary>
    private const int StreamTimeout = 250;

    /// <summary>The first holding-register value.</summary>
    private const ushort FirstHoldingValue = 1200;

    /// <summary>The second holding-register value.</summary>
    private const ushort SecondHoldingValue = 3400;

    /// <summary>The input-register value.</summary>
    private const ushort InputValue = 5678;

    /// <summary>Verifies reads, writes, and request events through complete in-memory Modbus IP frames.</summary>
    /// <returns>A task that represents the test.</returns>
    [TUnit.Core.Test]
    public async Task InMemoryMasterRoundTripsAllDataAreasAndPublishesRequestsAsync()
    {
        using var dataStore = CreateDataStore();
        dataStore.WriteDataOptimized(
            [InputValue],
            dataStore.InputRegisters,
            startAddress: 0);
        dataStore.WriteDataOptimized(
            [true, false],
            dataStore.InputDiscretes,
            startAddress: 0);

        using var simulator = new ModbusSimulator(UnitId, dataStore);
        using var master = simulator.CreateMaster();
        var requests = new List<ModbusSimulatorRequestEventArgs>();
        simulator.RequestProcessed += (_, args) => requests.Add(args);

        await master.WriteMultipleRegistersAsync(
            UnitId,
            startAddress: 0,
            [FirstHoldingValue, SecondHoldingValue]);
        await master.WriteMultipleCoilsAsync(
            UnitId,
            startAddress: 0,
            [true, false, true]);

        var holding = await master.ReadHoldingRegistersAsync(UnitId, 0, TwoPoints);
        var coils = await master.ReadCoilsAsync(UnitId, 0, ThreePoints);
        var inputs = await master.ReadInputsAsync(UnitId, 0, TwoPoints);
        var inputRegisters = await master.ReadInputRegistersAsync(UnitId, 0, 1);

        await TUnitAssert.That(holding)
            .IsEquivalentTo([FirstHoldingValue, SecondHoldingValue]);
        await TUnitAssert.That(coils).IsEquivalentTo([true, false, true]);
        await TUnitAssert.That(inputs).IsEquivalentTo([true, false]);
        await TUnitAssert.That(inputRegisters).IsEquivalentTo([InputValue]);
        await TUnitAssert.That(simulator.RequestCount).IsEqualTo(requests.Count);
        await TUnitAssert.That(requests.Count).IsEqualTo(ExpectedRoundTripRequestCount);
        await TUnitAssert.That(requests.All(static request => request.Response is not null)).IsTrue();
        await TUnitAssert.That(requests.All(static request => request.Fault is null)).IsTrue();
    }

    /// <summary>Verifies that every scripted transport fault is deterministic and recoverable by retry.</summary>
    /// <param name="fault">The scripted fault.</param>
    /// <returns>A task that represents the test.</returns>
    [TUnit.Core.Test]
    [TUnit.Core.Arguments(ModbusSimulatorFaultKind.IOException)]
    [TUnit.Core.Arguments(ModbusSimulatorFaultKind.Timeout)]
    [TUnit.Core.Arguments(ModbusSimulatorFaultKind.SlaveDeviceBusy)]
    [TUnit.Core.Arguments(ModbusSimulatorFaultKind.CorruptTransactionId)]
    public async Task ScriptedFaultIsConsumedAndRetryUsesPersistentMemoryAsync(
        ModbusSimulatorFaultKind fault)
    {
        using var dataStore = CreateDataStore();
        dataStore.WriteDataOptimized(
            [FirstHoldingValue],
            dataStore.HoldingRegisters,
            startAddress: 0);
        using var simulator = new ModbusSimulator(UnitId, dataStore);
        using var master = simulator.CreateMaster();
        master.Transport!.Retries = 1;
        master.Transport.WaitToRetryMilliseconds = 0;
        master.Transport.SlaveBusyUsesRetryCount = true;
        simulator.QueueFault(fault);

        var values = await master.ReadHoldingRegistersAsync(UnitId, 0, 1);

        await TUnitAssert.That(values).IsEquivalentTo([FirstHoldingValue]);
        await TUnitAssert.That(simulator.RequestCount).IsEqualTo(ExpectedRetryRequestCount);
    }

    /// <summary>Verifies scripted fault validation, clearing, and a non-retried timeout.</summary>
    /// <returns>A task that represents the test.</returns>
    [TUnit.Core.Test]
    public async Task FaultQueueCanBeValidatedClearedAndObservedAsync()
    {
        using var dataStore = CreateDataStore();
        using var simulator = new ModbusSimulator(UnitId, dataStore);
        using var master = simulator.CreateMaster();
        master.Transport!.Retries = 0;
        ModbusSimulatorRequestEventArgs? observed = null;
        simulator.RequestProcessed += (_, args) => observed = args;

        await TUnitAssert.That(
                () => simulator.QueueFault(ModbusSimulatorFaultKind.None))
            .Throws<ArgumentOutOfRangeException>();
        simulator.QueueFault(ModbusSimulatorFaultKind.IOException);
        simulator.ClearFaults();
        _ = await master.ReadHoldingRegistersAsync(UnitId, 0, 1);

        simulator.QueueFault(ModbusSimulatorFaultKind.Timeout);
        await TUnitAssert.That(ReadAfterTimeoutAsync).Throws<TimeoutException>();

        await TUnitAssert.That(observed).IsNotNull();
        await TUnitAssert.That(observed!.Fault).IsEqualTo(ModbusSimulatorFaultKind.Timeout);
        await TUnitAssert.That(observed.Response).IsNull();

        async Task ReadAfterTimeoutAsync() =>
            _ = await master.ReadHoldingRegistersAsync(UnitId, 0, 1);
    }

    /// <summary>Verifies that another unit identifier receives a Modbus gateway exception.</summary>
    /// <returns>A task that represents the test.</returns>
    [TUnit.Core.Test]
    public async Task OtherUnitIdentifierReturnsGatewayTargetFailureAsync()
    {
        using var dataStore = CreateDataStore();
        using var simulator = new ModbusSimulator(UnitId, dataStore);
        using var master = simulator.CreateMaster();
        master.Transport!.Retries = 0;

        SlaveException? exception = null;
        try
        {
            _ = await master.ReadHoldingRegistersAsync(OtherUnitId, 0, 1);
        }
        catch (SlaveException caught)
        {
            exception = caught;
        }

        await TUnitAssert.That(exception).IsNotNull();
        await TUnitAssert.That(exception!.SlaveAddress).IsEqualTo(OtherUnitId);
        await TUnitAssert.That(exception.SlaveExceptionCode).IsEqualTo(GatewayTargetFailureCode);
    }

    /// <summary>Verifies latency and lifetime behavior without transferring external data-store ownership.</summary>
    /// <returns>A task that represents the test.</returns>
    [TUnit.Core.Test]
    public async Task ResponseDelayAndResourceOwnershipAreExplicitAsync()
    {
        using var dataStore = CreateDataStore();
        var simulator = new ModbusSimulator(UnitId, dataStore)
        {
            ResponseDelay = TimeSpan.FromMilliseconds(ResponseDelayMilliseconds),
        };
        using var master = simulator.CreateMaster();
        var started = Stopwatch.StartNew();

        _ = await master.ReadHoldingRegistersAsync(UnitId, 0, 1);

        await TUnitAssert.That(
                started.Elapsed >= TimeSpan.FromMilliseconds(MinimumMeasuredDelayMilliseconds))
            .IsTrue();
        await TUnitAssert.That(
                () => simulator.ResponseDelay = TimeSpan.FromTicks(-1))
            .Throws<ArgumentOutOfRangeException>();

        simulator.Dispose();
        await TUnitAssert.That(simulator.CreateMaster).Throws<ObjectDisposedException>();

        dataStore.Lock.EnterReadLock();
        dataStore.Lock.ExitReadLock();
    }

    /// <summary>Verifies constructor defaults, argument validation, owned memory, and idempotent disposal.</summary>
    /// <returns>A task that represents the test.</returns>
    [TUnit.Core.Test]
    public async Task SimulatorConstructorsAndFrameValidationAreDeterministicAsync()
    {
        var defaultSimulator = new ModbusSimulator();
        await TUnitAssert.That(defaultSimulator.UnitId).IsEqualTo((byte)0);
        defaultSimulator.Dispose();
        defaultSimulator.Dispose();

        using var specifiedSimulator = new ModbusSimulator(UnitId);
        await TUnitAssert.That(specifiedSimulator.UnitId).IsEqualTo(UnitId);
        await TUnitAssert.That(
                () => new ModbusSimulator(UnitId, (DataStore)null!))
            .Throws<ArgumentNullException>();

        using var dataStore = CreateDataStore();
        await TUnitAssert.That(
                () => new ModbusSimulator(UnitId, dataStore, null!))
            .Throws<ArgumentNullException>();
        await TUnitAssert.That(
                () => specifiedSimulator.ProcessFrame(null!))
            .Throws<ArgumentNullException>();
        await TUnitAssert.That(
                () => specifiedSimulator.ProcessFrame([0]))
            .Throws<FormatException>();

        var invalidProtocol = new byte[8];
        invalidProtocol[2] = 1;
        await TUnitAssert.That(
                () => specifiedSimulator.ProcessFrame(invalidProtocol))
            .Throws<FormatException>();

        var invalidLength = new byte[8];
        await TUnitAssert.That(
                () => specifiedSimulator.ProcessFrame(invalidLength))
            .Throws<FormatException>();
    }

    /// <summary>Verifies low-level in-memory stream validation, buffering, timeout properties, and disposal.</summary>
    /// <returns>A task that represents the test.</returns>
    [TUnit.Core.Test]
    public async Task InMemoryStreamResourceValidatesBuffersAndLifetimeAsync()
    {
        using var dataStore = CreateDataStore();
        using var simulator = new ModbusSimulator(UnitId, dataStore);
        await TUnitAssert.That(
                () => new InMemoryModbusStreamResource(null!))
            .Throws<ArgumentNullException>();

        var resource = new InMemoryModbusStreamResource(simulator)
        {
            ReadTimeout = StreamTimeout,
            WriteTimeout = StreamTimeout,
        };
        await TUnitAssert.That(resource.InfiniteTimeout).IsEqualTo(Timeout.Infinite);
        await TUnitAssert.That(resource.ReadTimeout).IsEqualTo(StreamTimeout);
        await TUnitAssert.That(resource.WriteTimeout).IsEqualTo(StreamTimeout);

        var oneByte = new byte[1];
        await TUnitAssert.That(await resource.ReadAsync(oneByte, 0, 1)).IsEqualTo(0);
        var requestFrame = BuildRequestFrame();
        resource.Write(requestFrame, 0, requestFrame.Length);
        resource.DiscardInBuffer();
        await TUnitAssert.That(await resource.ReadAsync(oneByte, 0, 1)).IsEqualTo(0);

        await TUnitAssert.That(
                () => resource.ReadAsync(null!, 0, 0))
            .Throws<ArgumentNullException>();
        await TUnitAssert.That(
                () => resource.ReadAsync(oneByte, -1, 1))
            .Throws<ArgumentOutOfRangeException>();
        await TUnitAssert.That(
                () => resource.ReadAsync(oneByte, 0, TwoPoints))
            .Throws<ArgumentOutOfRangeException>();
        await TUnitAssert.That(
                () => resource.Write(null!, 0, 0))
            .Throws<ArgumentNullException>();
        await TUnitAssert.That(
                () => resource.Write(oneByte, -1, 1))
            .Throws<ArgumentOutOfRangeException>();
        await TUnitAssert.That(
                () => resource.Write(oneByte, 0, TwoPoints))
            .Throws<ArgumentOutOfRangeException>();

        resource.Dispose();
        resource.Dispose();
        await TUnitAssert.That(resource.DiscardInBuffer).Throws<ObjectDisposedException>();
        await TUnitAssert.That(
                () => resource.ReadAsync(oneByte, 0, 1))
            .Throws<ObjectDisposedException>();
        await TUnitAssert.That(
                () => resource.Write(oneByte, 0, 1))
            .Throws<ObjectDisposedException>();
    }

    /// <summary>Builds a valid Modbus IP holding-register request frame.</summary>
    /// <returns>The complete request frame.</returns>
    private static byte[] BuildRequestFrame()
    {
        var request = new ReadHoldingInputRegistersRequest(
            Modbus.ReadHoldingRegisters,
            UnitId,
            startAddress: 0,
            numberOfPoints: 1)
        {
            TransactionId = RequestTransactionId,
        };
        var header = ModbusIpTransport.GetMbapHeader(request);
        var result = new byte[header.Length + request.ProtocolDataUnit.Length];
        Array.Copy(header, result, header.Length);
        Array.Copy(
            request.ProtocolDataUnit,
            0,
            result,
            header.Length,
            request.ProtocolDataUnit.Length);
        return result;
    }

    /// <summary>Creates a bounded data store for simulator tests.</summary>
    /// <returns>The test data store.</returns>
    private static DataStore CreateDataStore() =>
        DataStoreFactory.CreateDefaultDataStore(
            DataAreaSize,
            DataAreaSize,
            DataAreaSize,
            DataAreaSize);
}
