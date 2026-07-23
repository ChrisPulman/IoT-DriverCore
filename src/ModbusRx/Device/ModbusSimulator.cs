// Copyright (c) 2019-2026 Chris Pulman and contributors. All rights reserved.
// Chris Pulman and contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Collections.Concurrent;
using System.Net;
#if REACTIVE_SHIM
using IoT.DriverCore.ModbusRx.Reactive.Data;
using IoT.DriverCore.ModbusRx.Reactive.IO;
using IoT.DriverCore.ModbusRx.Reactive.Message;
#else
using IoT.DriverCore.ModbusRx.Data;
using IoT.DriverCore.ModbusRx.IO;
using IoT.DriverCore.ModbusRx.Message;
#endif

#if REACTIVE_SHIM
namespace IoT.DriverCore.ModbusRx.Reactive.Device;
#else
namespace IoT.DriverCore.ModbusRx.Device;
#endif

/// <summary>Provides a deterministic, stateful Modbus device for development, testing, and offline operation.</summary>
/// <remarks>
/// The simulator composes the production Modbus request processor and data store. Masters created by
/// <see cref="CreateMaster"/> exchange complete MBAP-framed messages without opening a socket. Use
/// <see cref="StartTcpLoopback"/> when the operating-system TCP path must also be exercised.
/// </remarks>
public sealed class ModbusSimulator : IDisposable
{
    /// <summary>Stores endpoints created by this simulator.</summary>
    private readonly List<ModbusTcpLoopbackEndpoint> _endpoints = [];

    /// <summary>Serializes endpoint lifetime operations.</summary>
    private readonly Lock _endpointLock = new();

    /// <summary>Stores scripted faults in request order.</summary>
    private readonly ConcurrentQueue<ModbusSimulatorFaultKind> _faults = new();

    /// <summary>Stores the time provider used by request events.</summary>
    private readonly TimeProvider _timeProvider;

    /// <summary>Stores the composed request processor.</summary>
    private readonly SimulatorSlave _slave;

    /// <summary>Indicates whether this simulator owns its data store.</summary>
    private readonly bool _ownsDataStore;

    /// <summary>Stores whether this simulator has been disposed.</summary>
    private int _disposed;

    /// <summary>Stores the total number of requests accepted by this simulator.</summary>
    private long _requestCount;

    /// <summary>Initializes a new instance of the <see cref="ModbusSimulator"/> class.</summary>
    public ModbusSimulator()
        : this(Modbus.DefaultIpSlaveUnitId)
    {
    }

    /// <summary>Initializes a new instance of the <see cref="ModbusSimulator"/> class.</summary>
    /// <param name="unitId">The Modbus unit identifier.</param>
    public ModbusSimulator(byte unitId)
        : this(
            unitId,
            DataStoreFactory.CreateDefaultDataStore(),
            TimeProvider.System,
            ownsDataStore: true)
    {
    }

    /// <summary>Initializes a new instance of the <see cref="ModbusSimulator"/> class.</summary>
    /// <param name="unitId">The Modbus unit identifier.</param>
    /// <param name="dataStore">The persistent device memory used by the simulator.</param>
    public ModbusSimulator(byte unitId, DataStore dataStore)
        : this(unitId, dataStore, TimeProvider.System)
    {
    }

    /// <summary>Initializes a new instance of the <see cref="ModbusSimulator"/> class.</summary>
    /// <param name="unitId">The Modbus unit identifier.</param>
    /// <param name="dataStore">The persistent device memory used by the simulator.</param>
    /// <param name="timeProvider">The time provider used for request-event timestamps.</param>
    public ModbusSimulator(byte unitId, DataStore dataStore, TimeProvider timeProvider)
        : this(
            unitId,
            dataStore ?? throw new ArgumentNullException(nameof(dataStore)),
            timeProvider ?? throw new ArgumentNullException(nameof(timeProvider)),
            ownsDataStore: false)
    {
    }

    /// <summary>Initializes a new instance of the <see cref="ModbusSimulator"/> class.</summary>
    /// <param name="unitId">The Modbus unit identifier.</param>
    /// <param name="dataStore">The persistent device memory used by the simulator.</param>
    /// <param name="timeProvider">The time provider used for request-event timestamps.</param>
    /// <param name="ownsDataStore">Whether the simulator disposes the data store.</param>
    private ModbusSimulator(
        byte unitId,
        DataStore dataStore,
        TimeProvider timeProvider,
        bool ownsDataStore)
    {
        DataStore = dataStore;
        _timeProvider = timeProvider;
        _ownsDataStore = ownsDataStore;
        _slave = new(unitId, dataStore);
    }

    /// <summary>Occurs after a complete request frame has been accepted by the simulator.</summary>
    public event EventHandler<ModbusSimulatorRequestEventArgs>? RequestProcessed;

    /// <summary>Gets the persistent Modbus device memory.</summary>
    public DataStore DataStore { get; }

    /// <summary>Gets the Modbus unit identifier served by this simulator.</summary>
    public byte UnitId => _slave.UnitId;

    /// <summary>Gets the number of requests accepted by the simulator.</summary>
    public long RequestCount => Interlocked.Read(ref _requestCount);

    /// <summary>Gets or sets a delay applied before each in-memory response is made available.</summary>
    public TimeSpan ResponseDelay
    {
        get;
        set
        {
            _ = value < TimeSpan.Zero
                ? throw new ArgumentOutOfRangeException(nameof(value))
                : 0;
            field = value;
        }
    }

    /// <summary>Creates a Modbus IP master connected through a complete in-memory MBAP transport.</summary>
    /// <returns>A master that communicates with this simulator.</returns>
    public ModbusIpMaster CreateMaster()
    {
        ThrowIfDisposed();
        return ModbusIpMaster.CreateIp(new InMemoryModbusStreamResource(this));
    }

    /// <summary>Starts an IPv4 loopback TCP endpoint on an operating-system assigned port.</summary>
    /// <returns>An endpoint that creates masters connected through the real socket stack.</returns>
    public ModbusTcpLoopbackEndpoint StartTcpLoopback()
    {
        lock (_endpointLock)
        {
            ThrowIfDisposed();
            var endpoint = new ModbusTcpLoopbackEndpoint(UnitId, DataStore);
            _endpoints.Add(endpoint);
            return endpoint;
        }
    }

    /// <summary>Queues a deterministic fault for the next request.</summary>
    /// <param name="fault">The fault to apply.</param>
    public void QueueFault(ModbusSimulatorFaultKind fault)
    {
        ThrowIfDisposed();
        _ = fault is not (
            ModbusSimulatorFaultKind.IOException or
            ModbusSimulatorFaultKind.Timeout or
            ModbusSimulatorFaultKind.SlaveDeviceBusy or
            ModbusSimulatorFaultKind.CorruptTransactionId)
            ? throw new ArgumentOutOfRangeException(nameof(fault))
            : 0;

        _faults.Enqueue(fault);
    }

    /// <summary>Removes every scripted fault that has not yet been applied.</summary>
    public void ClearFaults()
    {
        ThrowIfDisposed();
        var queuedFaultCount = _faults.Count;
        for (var index = 0; index < queuedFaultCount; index++)
        {
            _ = _faults.TryDequeue(out _);
        }
    }

    /// <summary>Disposes simulator endpoints and owned resources.</summary>
    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0)
        {
            return;
        }

        lock (_endpointLock)
        {
            foreach (var endpoint in _endpoints)
            {
                endpoint.Dispose();
            }

            _endpoints.Clear();
        }

        _slave.Dispose();
        var ownedDataStore = _ownsDataStore ? DataStore : null;
        ownedDataStore?.Dispose();
    }

    /// <summary>Processes a complete Modbus IP frame.</summary>
    /// <param name="frame">The complete MBAP and protocol-data-unit frame.</param>
    /// <returns>The scripted simulator response.</returns>
    internal ModbusSimulatorResponse ProcessFrame(byte[] frame)
    {
        ThrowIfDisposed();
        ValidateFrame(frame);

        var requestFrameLength = frame.Length - Six;
        var requestFrame = new byte[requestFrameLength];
        Array.Copy(frame, Six, requestFrame, 0, requestFrameLength);

        var request = ModbusMessageFactory.CreateModbusRequest(requestFrame);
        request.TransactionId = (ushort)IPAddress.NetworkToHostOrder(BitConverter.ToInt16(frame, 0));
        _ = Interlocked.Increment(ref _requestCount);

        var hasFault = _faults.TryDequeue(out var fault);
        IModbusMessage? response = null;

        try
        {
            var faultResponse = ApplyPreResponseFault(hasFault, fault, request);
            if (faultResponse is not null)
            {
                return faultResponse;
            }

            response = CreateResponse(hasFault, fault, request);
            return new(BuildIpFrame(response), null, ResponseDelay);
        }
        finally
        {
            RequestProcessed?.Invoke(
                this,
                new(
                    request,
                    response,
                    hasFault ? fault : null,
                    _timeProvider.GetUtcNow()));
        }
    }

    /// <summary>Builds a complete Modbus IP response frame.</summary>
    /// <param name="response">The response to frame.</param>
    /// <returns>The complete MBAP response frame.</returns>
    private static byte[] BuildIpFrame(IModbusMessage response)
    {
        var header = ModbusIpTransport.GetMbapHeader(response);
        var protocolDataUnit = response.ProtocolDataUnit;
        var result = new byte[header.Length + protocolDataUnit.Length];
        Array.Copy(header, result, header.Length);
        Array.Copy(protocolDataUnit, 0, result, header.Length, protocolDataUnit.Length);
        return result;
    }

    /// <summary>Gets a transaction identifier that cannot equal the supplied identifier.</summary>
    /// <param name="transactionId">The original transaction identifier.</param>
    /// <returns>A different transaction identifier.</returns>
    private static ushort GetDifferentTransactionId(ushort transactionId) =>
        unchecked((ushort)(transactionId + 1));

    /// <summary>Validates a complete Modbus IP request frame.</summary>
    /// <param name="frame">The frame to validate.</param>
    private static void ValidateFrame(byte[] frame)
    {
        _ = frame ?? throw new ArgumentNullException(nameof(frame));
        _ = frame.Length < Eight
            ? throw new FormatException("A Modbus IP frame must contain an MBAP header and protocol data unit.")
            : 0;
        _ = frame[Two] != 0 || frame[Three] != 0
            ? throw new FormatException("The Modbus protocol identifier must be zero.")
            : 0;

        var declaredLength = (ushort)IPAddress.NetworkToHostOrder(BitConverter.ToInt16(frame, Four));
        _ = declaredLength != frame.Length - Six
            ? throw new FormatException("The Modbus IP frame length does not match its MBAP header.")
            : 0;
    }

    /// <summary>Applies a fault that prevents a protocol response.</summary>
    /// <param name="hasFault">Whether a fault was dequeued.</param>
    /// <param name="fault">The dequeued fault.</param>
    /// <param name="request">The request being processed.</param>
    /// <returns>A read-failure result, or <c>null</c> to continue processing.</returns>
    private ModbusSimulatorResponse? ApplyPreResponseFault(
        bool hasFault,
        ModbusSimulatorFaultKind fault,
        IModbusMessage request)
    {
        if (!hasFault)
        {
            return null;
        }

        if (fault == ModbusSimulatorFaultKind.IOException)
        {
            throw new IOException("The scripted Modbus simulator write failed.");
        }

        return fault == ModbusSimulatorFaultKind.Timeout
            ? new(
                null,
                new TimeoutException(
                    $"The scripted response to function {request.FunctionCode} timed out."),
                ResponseDelay)
            : null;
    }

    /// <summary>Creates a normal or scripted protocol response.</summary>
    /// <param name="hasFault">Whether a fault was dequeued.</param>
    /// <param name="fault">The dequeued fault.</param>
    /// <param name="request">The request being processed.</param>
    /// <returns>The response.</returns>
    private IModbusMessage CreateResponse(
        bool hasFault,
        ModbusSimulatorFaultKind fault,
        IModbusMessage request)
    {
        var response = hasFault && fault == ModbusSimulatorFaultKind.SlaveDeviceBusy
            ? new SlaveExceptionResponse(
                request.SlaveAddress,
                (byte)(Modbus.ExceptionOffset + request.FunctionCode),
                Modbus.SlaveDeviceBusy)
            : ApplyRequest(request);

        response.TransactionId = hasFault && fault == ModbusSimulatorFaultKind.CorruptTransactionId
            ? GetDifferentTransactionId(request.TransactionId)
            : request.TransactionId;
        return response;
    }

    /// <summary>Applies a request or produces a gateway exception for another unit identifier.</summary>
    /// <param name="request">The request to process.</param>
    /// <returns>The Modbus response.</returns>
    private IModbusMessage ApplyRequest(IModbusMessage request) =>
        request.SlaveAddress == UnitId
            ? _slave.ApplyRequest(request)
            : new SlaveExceptionResponse(
                request.SlaveAddress,
                (byte)(Modbus.ExceptionOffset + request.FunctionCode),
                (byte)Eleven);

    /// <summary>Throws when this simulator is disposed.</summary>
    private void ThrowIfDisposed() =>
        _ = Volatile.Read(ref _disposed) != 0
            ? throw new ObjectDisposedException(nameof(ModbusSimulator))
            : 0;

    /// <summary>Provides the production request processor to the composed simulator.</summary>
    private sealed class SimulatorSlave : ModbusSlave
    {
        /// <summary>Initializes a new instance of the <see cref="SimulatorSlave"/> class.</summary>
        /// <param name="unitId">The unit identifier.</param>
        /// <param name="dataStore">The simulator data store.</param>
        internal SimulatorSlave(byte unitId, DataStore dataStore)
            : base(unitId, new EmptyTransport()) => DataStore = dataStore;

        /// <inheritdoc/>
        public override Task ListenAsync() => Task.CompletedTask;
    }
}
