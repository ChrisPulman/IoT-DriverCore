// Copyright (c) 2019-2026 Chris Pulman and contributors. All rights reserved.
// Chris Pulman and contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Collections.Concurrent;
using System.Globalization;
using System.Text;

#if REACTIVE_SHIM

namespace IoT.DriverCore.MitsubishiRx.Reactive;

#else

namespace IoT.DriverCore.MitsubishiRx;

#endif

/// <summary>
/// Provides a deterministic, in-memory Mitsubishi transport for simulations,
/// examples, integration tests, and applications that do not have a physical PLC.
/// </summary>
public sealed partial class MitsubishiSimulatorTransport : IMitsubishiTransport
{
    /// <summary>Stores the number of bytes in a protocol word.</summary>
    private const int ProtocolWordByteCount = 2;

    /// <summary>Stores connection faults in deterministic order.</summary>
    private readonly ConcurrentQueue<Exception> _connectFaults = new();

    /// <summary>Stores immutable snapshots of exchanged requests.</summary>
    private readonly ConcurrentQueue<MitsubishiTransportRequest> _requests = new();

    /// <summary>Stores scripted exchange outcomes in deterministic order.</summary>
    private readonly ConcurrentQueue<SimulatorExchange> _script = new();

    /// <summary>Stores the optional request-based response factory.</summary>
    private readonly Func<MitsubishiTransportRequest, byte[]>? _responseFactory;

    /// <summary>Synchronizes connection and disposal state.</summary>
    private readonly object _stateGate = new();

    /// <summary>Stores the options supplied by the most recent successful connection.</summary>
    private MitsubishiClientOptions? _connectedOptions;

    /// <summary>Stores whether the simulator is connected.</summary>
    private bool _connected;

    /// <summary>Stores whether the simulator is disposed.</summary>
    private bool _disposed;

    /// <summary>Stores the successful connection count.</summary>
    private int _connectCount;

    /// <summary>Stores the disconnect operation count.</summary>
    private int _disconnectCount;

    /// <summary>Initializes a new instance of the <see cref="MitsubishiSimulatorTransport"/> class.</summary>
    public MitsubishiSimulatorTransport()
        : this(new MitsubishiSimulatorMemory(), null)
    {
    }

    /// <summary>Initializes a new instance of the <see cref="MitsubishiSimulatorTransport"/> class.</summary>
    /// <param name="responses">The responses to enqueue.</param>
    public MitsubishiSimulatorTransport(IEnumerable<byte[]> responses)
        : this()
    {
        ArgumentNullException.ThrowIfNull(responses);
        foreach (var response in responses)
        {
            EnqueueResponse(response);
        }
    }

    /// <summary>Initializes a new instance of the <see cref="MitsubishiSimulatorTransport"/> class.</summary>
    /// <param name="responseFactory">The deterministic response factory.</param>
    public MitsubishiSimulatorTransport(Func<MitsubishiTransportRequest, byte[]> responseFactory)
        : this(
            new MitsubishiSimulatorMemory(),
            responseFactory ?? throw new ArgumentNullException(nameof(responseFactory)))
    {
    }

    /// <summary>Initializes a new instance of the <see cref="MitsubishiSimulatorTransport"/> class.</summary>
    /// <param name="memory">The stateful device-memory image.</param>
    public MitsubishiSimulatorTransport(MitsubishiSimulatorMemory memory)
        : this(memory, null)
    {
    }

    /// <summary>Initializes a new instance of the <see cref="MitsubishiSimulatorTransport"/> class.</summary>
    /// <param name="memory">The stateful device-memory image.</param>
    /// <param name="responseFactory">The optional deterministic response factory.</param>
    private MitsubishiSimulatorTransport(
        MitsubishiSimulatorMemory memory,
        Func<MitsubishiTransportRequest, byte[]>? responseFactory)
    {
        Memory = memory ?? throw new ArgumentNullException(nameof(memory));
        _responseFactory = responseFactory;
    }

    /// <inheritdoc/>
    public bool IsConnected
    {
        get
        {
            lock (_stateGate)
            {
                return _connected;
            }
        }
    }

    /// <summary>Gets the options supplied by the most recent successful connection.</summary>
    public MitsubishiClientOptions? ConnectedOptions
    {
        get
        {
            lock (_stateGate)
            {
                return _connectedOptions;
            }
        }
    }

    /// <summary>Gets immutable snapshots of requests in their exchange order.</summary>
    public IReadOnlyList<MitsubishiTransportRequest> Requests => _requests.ToArray();

    /// <summary>Gets the number of successful connection attempts.</summary>
    public int ConnectCount => Volatile.Read(ref _connectCount);

    /// <summary>Gets the number of disconnect operations.</summary>
    public int DisconnectCount => Volatile.Read(ref _disconnectCount);

    /// <summary>Gets the stateful device-memory image used by automatic responses.</summary>
    public MitsubishiSimulatorMemory Memory { get; }

    /// <summary>Creates a complete protocol response containing the supplied decoded payload.</summary>
    /// <param name="options">The client options defining the response framing.</param>
    /// <param name="payload">The payload expected after protocol decoding.</param>
    /// <returns>The complete wire response.</returns>
    public static byte[] CreateSuccessResponse(
        MitsubishiClientOptions options,
        ReadOnlySpan<byte> payload)
    {
        ArgumentNullException.ThrowIfNull(options);
        return options.TransportKind == MitsubishiTransportKind.Serial
            ? CreateSerialResponse(options, payload, 0)
            : CreateMcResponse(options, payload, 0);
    }

    /// <summary>Creates a complete protocol response containing a PLC end code.</summary>
    /// <param name="options">The client options defining the response framing.</param>
    /// <param name="endCode">The PLC end code.</param>
    /// <returns>The complete wire response.</returns>
    public static byte[] CreateErrorResponse(
        MitsubishiClientOptions options,
        ushort endCode)
    {
        ArgumentNullException.ThrowIfNull(options);
        if (endCode == 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(endCode),
                "An error response requires a non-zero end code.");
        }

        return options.TransportKind == MitsubishiTransportKind.Serial
            ? CreateSerialResponse(options, [], endCode)
            : CreateMcResponse(options, [], endCode);
    }

    /// <summary>Queues a response to be returned by the next exchange.</summary>
    /// <param name="response">The complete wire response.</param>
    public void EnqueueResponse(ReadOnlySpan<byte> response) =>
        _script.Enqueue(SimulatorExchange.FromResponse(response.ToArray()));

    /// <summary>Queues a fault to be thrown by the next exchange.</summary>
    /// <param name="exception">The fault to throw.</param>
    public void EnqueueFault(Exception exception)
    {
        ArgumentNullException.ThrowIfNull(exception);
        _script.Enqueue(SimulatorExchange.FromFault(exception));
    }

    /// <summary>Queues a fault to be thrown by the next connection attempt.</summary>
    /// <param name="exception">The fault to throw.</param>
    public void EnqueueConnectFault(Exception exception)
    {
        ArgumentNullException.ThrowIfNull(exception);
        _connectFaults.Enqueue(exception);
    }

    /// <summary>Clears the captured request history.</summary>
    public void ClearRequests() => _requests.Clear();

    /// <inheritdoc/>
    public ValueTask ConnectAsync(
        MitsubishiClientOptions options,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(options);
        cancellationToken.ThrowIfCancellationRequested();
        ThrowIfDisposed();

        if (_connectFaults.TryDequeue(out var fault))
        {
            throw fault;
        }

        lock (_stateGate)
        {
            ThrowIfDisposed();
            _connectedOptions = options;
            _connected = true;
        }

        _ = Interlocked.Increment(ref _connectCount);
        return ValueTask.CompletedTask;
    }

    /// <inheritdoc/>
    public ValueTask DisconnectAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        lock (_stateGate)
        {
            if (_disposed)
            {
                return ValueTask.CompletedTask;
            }

            _connected = false;
        }

        _ = Interlocked.Increment(ref _disconnectCount);
        return ValueTask.CompletedTask;
    }

    /// <inheritdoc/>
    public ValueTask<byte[]> ExchangeAsync(
        MitsubishiTransportRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();
        MitsubishiClientOptions options;
        lock (_stateGate)
        {
            ThrowIfDisposed();
            if (!_connected || _connectedOptions is null)
            {
                throw new InvalidOperationException("The Mitsubishi simulator is not connected.");
            }

            options = _connectedOptions;
        }

        var requestSnapshot = request with { Payload = request.Payload.ToArray() };
        _requests.Enqueue(requestSnapshot);

        if (_script.TryDequeue(out var exchange))
        {
            if (exchange.Fault is not null)
            {
                lock (_stateGate)
                {
                    _connected = false;
                }

                throw exchange.Fault;
            }

            return ValueTask.FromResult(exchange.Response!.ToArray());
        }

        var response = (_responseFactory is null
            ? CreateStatefulResponse(options, requestSnapshot)
            : _responseFactory(requestSnapshot))
            ?? throw new InvalidOperationException(
                "The Mitsubishi simulator response factory returned null.");

        return ValueTask.FromResult(response.ToArray());
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        lock (_stateGate)
        {
            if (_disposed)
            {
                return;
            }

            _connected = false;
            _disposed = true;
        }
    }

    /// <inheritdoc/>
    public ValueTask DisposeAsync()
    {
        Dispose();
        return ValueTask.CompletedTask;
    }

    /// <summary>Creates an MC protocol response.</summary>
    /// <param name="options">The client options.</param>
    /// <param name="payload">The decoded payload.</param>
    /// <param name="endCode">The PLC end code.</param>
    /// <returns>The complete response.</returns>
    private static byte[] CreateMcResponse(
        MitsubishiClientOptions options,
        ReadOnlySpan<byte> payload,
        ushort endCode)
    {
        return options.DataCode == CommunicationDataCode.Ascii
            ? CreateMcAsciiResponse(options, payload, endCode)
            : CreateMcBinaryResponse(options, payload, endCode);
    }

    /// <summary>Creates a binary MC protocol response.</summary>
    /// <param name="options">The client options.</param>
    /// <param name="payload">The decoded payload.</param>
    /// <param name="endCode">The PLC end code.</param>
    /// <returns>The complete response.</returns>
    private static byte[] CreateMcBinaryResponse(
        MitsubishiClientOptions options,
        ReadOnlySpan<byte> payload,
        ushort endCode)
    {
        if (options.FrameType == MitsubishiFrameType.OneE)
        {
            return endCode == 0
                ? [0x81, 0x00, .. payload]
                : [0x81, 0x5B, (byte)(endCode & 0xFF), (byte)(endCode >> 8)];
        }

        var route = options.ResolvedRoute;
        var responseLength = checked((ushort)(payload.Length + ProtocolWordByteCount));
        var serialNumber = options.GetNextSerialNumber();
        byte[] header = options.FrameType switch
        {
            MitsubishiFrameType.ThreeE =>
            [
                0xD0, 0x00,
                route.NetworkNumber,
                route.StationNumber,
                (byte)(route.ModuleIoNumber & 0xFF),
                (byte)(route.ModuleIoNumber >> 8),
                route.MultidropStationNumber,
            ],
            MitsubishiFrameType.FourE =>
            [
                0xD4, 0x00,
                (byte)(serialNumber & 0xFF),
                (byte)(serialNumber >> 8),
                0x00, 0x00,
                route.NetworkNumber,
                route.StationNumber,
                (byte)(route.ModuleIoNumber & 0xFF),
                (byte)(route.ModuleIoNumber >> 8),
                route.MultidropStationNumber,
            ],
            _ => throw new ArgumentOutOfRangeException(nameof(options.FrameType)),
        };

        return
        [
            .. header,
            (byte)(responseLength & 0xFF),
            (byte)(responseLength >> 8),
            (byte)(endCode & 0xFF),
            (byte)(endCode >> 8),
            .. payload,
        ];
    }

    /// <summary>Creates an ASCII MC protocol response.</summary>
    /// <param name="options">The client options.</param>
    /// <param name="payload">The decoded payload.</param>
    /// <param name="endCode">The PLC end code.</param>
    /// <returns>The complete response.</returns>
    private static byte[] CreateMcAsciiResponse(
        MitsubishiClientOptions options,
        ReadOnlySpan<byte> payload,
        ushort endCode)
    {
        if (options.FrameType == MitsubishiFrameType.OneE)
        {
            var prefix = endCode == 0 ? "8100" : $"815B{endCode:X4}";
            return Encoding.ASCII.GetBytes(prefix + Encoding.ASCII.GetString(payload));
        }

        if ((payload.Length & 1) != 0)
        {
            throw new ArgumentException("An MC ASCII payload must contain complete hexadecimal byte pairs.", nameof(payload));
        }

        var route = options.ResolvedRoute;
        var subheader = options.FrameType switch
        {
            MitsubishiFrameType.ThreeE => "D000",
            MitsubishiFrameType.FourE => $"D400{options.GetNextSerialNumber():X4}0000",
            _ => throw new ArgumentOutOfRangeException(nameof(options.FrameType)),
        };
        var routeText =
            $"{route.NetworkNumber:X2}{route.StationNumber:X2}{route.ModuleIoNumber:X4}{route.MultidropStationNumber:X2}";
        var responseLength = checked(
            (ushort)((payload.Length / ProtocolWordByteCount) + ProtocolWordByteCount));
        return Encoding.ASCII.GetBytes(
            $"{subheader}{routeText}{responseLength:X4}{endCode:X4}{Encoding.ASCII.GetString(payload)}");
    }

    /// <summary>Creates a serial protocol response.</summary>
    /// <param name="options">The client options.</param>
    /// <param name="payload">The decoded payload.</param>
    /// <param name="endCode">The PLC end code.</param>
    /// <returns>The complete response.</returns>
    private static byte[] CreateSerialResponse(
        MitsubishiClientOptions options,
        ReadOnlySpan<byte> payload,
        ushort endCode)
    {
        var serial = options.ResolvedSerial;
        if (serial.MessageFormat == MitsubishiSerialMessageFormat.Format5)
        {
            if (options.FrameType != MitsubishiFrameType.FourC)
            {
                throw new NotSupportedException("Serial format 5 responses require a 4C frame.");
            }

            return CreateSerialBinaryResponse(serial, payload, endCode);
        }

        var payloadText = GetSerialPayloadText(payload, endCode);
        var responseMarker = endCode == 0 ? '\u0006' : '\u0015';
        var body = options.FrameType switch
        {
            MitsubishiFrameType.OneC =>
                $"{responseMarker}{serial.StationNumber:X2}{(endCode == 0 ? payloadText : $"{endCode & 0xFF:X2}")}",
            MitsubishiFrameType.ThreeC =>
                endCode == 0
                    ? $"{responseMarker}F9{serial.StationNumber:X2}{payloadText}"
                    : $"{responseMarker}F9{serial.StationNumber:X2}0{endCode & 0xFF:X2}",
            MitsubishiFrameType.FourC =>
                $"{responseMarker}F8{serial.StationNumber:X2}{serial.NetworkNumber:X2}{serial.PcNumber:X2}"
                + $"{serial.RequestDestinationModuleIoNumber >> 8:X2}"
                + (endCode == 0 ? payloadText : $"{endCode & 0xFF:X2}"),
            _ => throw new ArgumentOutOfRangeException(nameof(options.FrameType)),
        };
        var checksum = ComputeChecksum(Encoding.ASCII.GetBytes(body));
        return serial.MessageFormat switch
        {
            MitsubishiSerialMessageFormat.Format1 =>
                Encoding.ASCII.GetBytes(body + checksum),
            MitsubishiSerialMessageFormat.Format4 =>
                Encoding.ASCII.GetBytes($"\r\n{body}{checksum}\r\n"),
            _ => throw new NotSupportedException(
                $"Serial response format '{serial.MessageFormat}' is not supported."),
        };
    }

    /// <summary>Gets response payload text with the required acknowledgement placeholder.</summary>
    /// <param name="payload">The decoded response payload.</param>
    /// <param name="endCode">The PLC end code.</param>
    /// <returns>The serial response payload text.</returns>
    private static string GetSerialPayloadText(ReadOnlySpan<byte> payload, ushort endCode) =>
        payload.IsEmpty && endCode == 0
            ? "0"
            : Encoding.ASCII.GetString(payload);

    /// <summary>Creates a binary 4C serial response.</summary>
    /// <param name="serial">The serial options.</param>
    /// <param name="payload">The decoded payload.</param>
    /// <param name="endCode">The PLC end code.</param>
    /// <returns>The complete response.</returns>
    private static byte[] CreateSerialBinaryResponse(
        MitsubishiSerialOptions serial,
        ReadOnlySpan<byte> payload,
        ushort endCode)
    {
        var body = new List<byte>
        {
            0xF8,
            serial.StationNumber,
            serial.NetworkNumber,
            serial.PcNumber,
            (byte)(serial.RequestDestinationModuleIoNumber & 0xFF),
            (byte)(serial.RequestDestinationModuleIoNumber >> 8),
            serial.RequestDestinationModuleStationNumber,
            serial.SelfStationNumber,
            0xFF,
            0xFF,
            (byte)(endCode & 0xFF),
            (byte)(endCode >> 8),
        };
        body.AddRange(payload.ToArray());
        body.Add(0x10);
        body.Add(0x03);

        var byteCount = checked((ushort)(body.Count - ProtocolWordByteCount));
        var response = new List<byte>
        {
            0x10,
            0x02,
            (byte)(byteCount & 0xFF),
            (byte)(byteCount >> 8),
        };
        response.AddRange(body);
        response.AddRange(
            Encoding.ASCII.GetBytes(
                ComputeChecksum(
                    response
                        .Skip(ProtocolWordByteCount)
                        .Take(ProtocolWordByteCount + byteCount))));
        return response.ToArray();
    }

    /// <summary>Computes the Mitsubishi additive checksum.</summary>
    /// <param name="bytes">The bytes to checksum.</param>
    /// <returns>The uppercase hexadecimal checksum.</returns>
    private static string ComputeChecksum(IEnumerable<byte> bytes)
    {
        var sum = bytes.Aggregate(0, static (current, value) => current + value);
        return (sum & 0xFF).ToString("X2", CultureInfo.InvariantCulture);
    }

    /// <summary>Throws when the simulator has been disposed.</summary>
    private void ThrowIfDisposed() => ObjectDisposedException.ThrowIf(_disposed, this);

    /// <summary>Represents one deterministic exchange outcome.</summary>
    /// <param name="Response">The optional response.</param>
    /// <param name="Fault">The optional fault.</param>
    private sealed record SimulatorExchange(byte[]? Response, Exception? Fault)
    {
        /// <summary>Creates a response outcome.</summary>
        /// <param name="response">The response bytes.</param>
        /// <returns>The exchange outcome.</returns>
        internal static SimulatorExchange FromResponse(byte[] response) => new(response, null);

        /// <summary>Creates a fault outcome.</summary>
        /// <param name="fault">The fault.</param>
        /// <returns>The exchange outcome.</returns>
        internal static SimulatorExchange FromFault(Exception fault) => new(null, fault);
    }
}
