// Copyright (c) 2019-2026 Chris Pulman and contributors. All rights reserved.
// Chris Pulman and contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Net;
using System.Net.Sockets;
using System.Text;

namespace IoT.DriverCore.S7PlcRx.Mock;

/// <summary>Hosts a deterministic managed ISO-on-TCP/S7 server.</summary>
/// <remarks>
/// The server implements COTP setup, S7 communication negotiation, ReadVar, WriteVar, multi-item requests and the CPU
/// information SZLs used by <c>RxS7</c>. Memory remains registered across stop/start cycles.
/// </remarks>
public sealed class ManagedS7Server : IDisposable
{
    /// <summary>The default ISO-on-TCP port.</summary>
    public static readonly int DefaultPort = 102;

    /// <summary>The successful S7 item return code.</summary>
    private const byte SuccessReturnCode = 0xff;

    /// <summary>The S7 address-out-of-range return code.</summary>
    private const byte AddressOutOfRangeReturnCode = 0x05;

    /// <summary>The number of bits in one byte.</summary>
    private const int BitsPerByte = 8;

    /// <summary>The size of an S7ANY variable specification.</summary>
    private const int VariableSpecificationLength = 12;

    /// <summary>The byte offset of the request parameter length.</summary>
    private const int ParameterLengthOffset = 13;

    /// <summary>The length of the request header before parameter data.</summary>
    private const int RequestHeaderLength = 17;

    /// <summary>The byte offset of the first variable specification.</summary>
    private const int VariableSpecificationOffset = 19;

    /// <summary>The byte offset of AckData item data.</summary>
    private const int AckDataItemOffset = 21;

    /// <summary>The byte offset of the AckData data length.</summary>
    private const int AckDataLengthOffset = 15;

    /// <summary>The AckData parameter payload length.</summary>
    private const int AckDataParameterLength = 2;

    /// <summary>The size of one item-data header.</summary>
    private const int ItemDataHeaderLength = 4;

    /// <summary>The byte offset of the setup request PDU length.</summary>
    private const int SetupRequestedPduOffset = 23;

    /// <summary>The byte offset of the setup response PDU length.</summary>
    private const int SetupResponsePduOffset = 25;

    /// <summary>The byte offset of the requested SZL identifier.</summary>
    private const int SzlIdentifierOffset = 29;

    /// <summary>The byte offset of the SZL return data length.</summary>
    private const int SzlDataLengthOffset = 31;

    /// <summary>The byte offset of the S7 data-length header in an SZL response.</summary>
    private const int SzlHeaderDataLengthOffset = 15;

    /// <summary>The byte offset of the SZL payload length.</summary>
    private const int SzlTotalLengthOffset = 37;

    /// <summary>The byte offset of the SZL payload.</summary>
    private const int SzlPayloadOffset = 41;

    /// <summary>The SZL metadata length.</summary>
    private const int SzlMetadataLength = 8;

    /// <summary>The SZL data overhead length.</summary>
    private const int SzlDataOverhead = 12;

    /// <summary>The byte offset of the TPKT length.</summary>
    private const int TpktLengthOffset = 2;

    /// <summary>The TPKT protocol version.</summary>
    private const int TpktVersion = 3;

    /// <summary>The TPKT header length.</summary>
    private const int TpktHeaderLength = 4;

    /// <summary>The number of seconds allowed for listener shutdown.</summary>
    private const int ShutdownWaitSeconds = 2;

    /// <summary>The deterministic CPU information payload length.</summary>
    private const int CpuInformationLength = 204;

    /// <summary>The CPU AS-name field offset.</summary>
    private const int CpuAsNameOffset = 2;

    /// <summary>The CPU module-name field offset.</summary>
    private const int CpuModuleNameOffset = 36;

    /// <summary>The CPU copyright field offset.</summary>
    private const int CpuCopyrightOffset = 104;

    /// <summary>The CPU serial-number field offset.</summary>
    private const int CpuSerialNumberOffset = 138;

    /// <summary>The CPU module-type field offset.</summary>
    private const int CpuModuleTypeOffset = 172;

    /// <summary>The standard CPU information text length.</summary>
    private const int CpuTextLength = 24;

    /// <summary>The CPU copyright text length.</summary>
    private const int CpuCopyrightLength = 26;

    /// <summary>The CPU module-type text length.</summary>
    private const int CpuModuleTypeLength = 32;

    /// <summary>The deterministic order-code payload length.</summary>
    private const int OrderCodeLength = 25;

    /// <summary>The order-code text field offset.</summary>
    private const int OrderCodeOffset = 2;

    /// <summary>The order-code text field length.</summary>
    private const int OrderCodeTextLength = 20;

    /// <summary>The S7ANY syntax identifier offset.</summary>
    private const int S7AnySyntaxOffset = 2;

    /// <summary>The S7ANY element-count offset.</summary>
    private const int S7AnyCountOffset = 4;

    /// <summary>The S7ANY DB-number offset.</summary>
    private const int S7AnyDbNumberOffset = 6;

    /// <summary>The S7ANY area offset.</summary>
    private const int S7AnyAreaOffset = 8;

    /// <summary>The S7ANY high address-byte offset.</summary>
    private const int S7AnyAddressHighOffset = 9;

    /// <summary>The S7ANY middle address-byte offset.</summary>
    private const int S7AnyAddressMiddleOffset = 10;

    /// <summary>The S7ANY low address-byte offset.</summary>
    private const int S7AnyAddressLowOffset = 11;

    /// <summary>The shift applied to the high byte of a three-byte address.</summary>
    private const int HighAddressShift = 16;

    /// <summary>Synchronizes lifecycle and client tracking.</summary>
    private readonly object _syncRoot = new();

    /// <summary>Stores queued single-use faults.</summary>
    private readonly List<S7ServerFault> _faults = [];

    /// <summary>Stores connected clients.</summary>
    private readonly List<TcpClient> _clients = [];

    /// <summary>Stores active client sessions.</summary>
    private readonly List<ClientSession> _clientSessions = [];

    /// <summary>Cancellation for the active listener.</summary>
    private CancellationTokenSource? _cancellation;

    /// <summary>The active listener.</summary>
    private TcpListener? _listener;

    /// <summary>The accept loop task.</summary>
    private Task? _acceptLoop;

    /// <summary>Tracks disposal.</summary>
    private bool _disposed;

    /// <summary>Stores read operation count.</summary>
    private long _readCount;

    /// <summary>Stores write operation count.</summary>
    private long _writeCount;

    /// <summary>Initializes a new instance of the <see cref="ManagedS7Server"/> class.</summary>
    public ManagedS7Server()
        : this(new ManagedS7Memory())
    {
    }

    /// <summary>Initializes a new instance of the <see cref="ManagedS7Server"/> class.</summary>
    /// <param name="memory">The memory store.</param>
    public ManagedS7Server(ManagedS7Memory memory) =>
        Memory = memory ?? throw new ArgumentNullException(nameof(memory));

    /// <summary>Gets the server memory.</summary>
    public ManagedS7Memory Memory { get; }

    /// <summary>Gets or sets the negotiated PDU length.</summary>
    public ushort PduLength { get; set; } = 1440;

    /// <summary>Gets or sets the virtual CPU status.</summary>
    public int CpuStatus { get; set; } = 8;

    /// <summary>Gets whether the listener is running.</summary>
    public bool IsRunning { get; private set; }

    /// <summary>Gets the number of connected clients.</summary>
    public int ClientsCount
    {
        get
        {
            lock (_syncRoot)
            {
                return _clients.Count;
            }
        }
    }

    /// <summary>Gets the number of completed ReadVar requests.</summary>
    public long ReadCount => Interlocked.Read(ref _readCount);

    /// <summary>Gets the number of completed WriteVar requests.</summary>
    public long WriteCount => Interlocked.Read(ref _writeCount);

    /// <summary>Queues a single-use fault.</summary>
    /// <param name="fault">The scripted fault.</param>
    public void EnqueueFault(S7ServerFault fault)
    {
        if (fault is null)
        {
            throw new ArgumentNullException(nameof(fault));
        }

        lock (_syncRoot)
        {
            ThrowIfDisposed();
            _faults.Add(fault);
        }
    }

    /// <summary>Removes all queued faults.</summary>
    public void ClearFaults()
    {
        lock (_syncRoot)
        {
            _faults.Clear();
        }
    }

    /// <summary>Starts listening on the loopback address and default ISO-on-TCP port.</summary>
    public void Start() => Start("127.0.0.1", DefaultPort);

    /// <summary>Starts listening on the default ISO-on-TCP port.</summary>
    /// <param name="address">The bind address.</param>
    public void Start(string address) => Start(address, DefaultPort);

    /// <summary>Starts listening.</summary>
    /// <param name="address">The bind address.</param>
    /// <param name="port">The ISO-on-TCP port.</param>
    public void Start(string address, int port)
    {
#if NET8_0_OR_GREATER
        ArgumentException.ThrowIfNullOrWhiteSpace(address);
#else
        if (string.IsNullOrWhiteSpace(address))
        {
            throw new ArgumentException("A bind address is required.", nameof(address));
        }
#endif

        if (port is < IPEndPoint.MinPort or > IPEndPoint.MaxPort)
        {
            throw new ArgumentOutOfRangeException(nameof(port));
        }

        lock (_syncRoot)
        {
            ThrowIfDisposed();
            if (IsRunning)
            {
                return;
            }

            _cancellation = new();
            _listener = new(IPAddress.Parse(address), port);
            _listener.Start();
            IsRunning = true;
            _acceptLoop = AcceptLoopAsync(_listener, _cancellation.Token);
        }
    }

    /// <summary>Stops listening and disconnects all clients.</summary>
    public void Stop()
    {
        Task[] shutdownTasks;
        lock (_syncRoot)
        {
            if (!IsRunning)
            {
                return;
            }

            IsRunning = false;
            _cancellation?.Cancel();
            _listener?.Stop();
#if NET8_0_OR_GREATER
            _listener?.Dispose();
#endif

            foreach (var client in _clients.ToArray())
            {
                client.Dispose();
            }

            _clients.Clear();
            shutdownTasks = CaptureShutdownTasks();
            _acceptLoop = null;
            _listener = null;
        }

        try
        {
            if (shutdownTasks.Length > 0)
            {
                _ = Task.WaitAll(shutdownTasks, TimeSpan.FromSeconds(ShutdownWaitSeconds));
            }
        }
        catch (AggregateException)
        {
            // Listener and client shutdown intentionally interrupt pending network operations.
        }

        _cancellation?.Dispose();
        _cancellation = null;
    }

    /// <summary>Disposes the server.</summary>
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        Stop();
        _disposed = true;
    }

    /// <summary>Classifies an incoming frame.</summary>
    /// <param name="frame">The complete TPKT frame.</param>
    /// <returns>The corresponding simulated server operation.</returns>
    private static S7ServerOperation Classify(byte[] frame)
    {
        if (frame.Length > 5 && frame[5] == 0xe0)
        {
            return S7ServerOperation.Connect;
        }

        if (frame.Length <= 17 || frame[7] != 0x32)
        {
            return S7ServerOperation.Any;
        }

        return frame[8] == 0x07 ? S7ServerOperation.Szl : frame[17] switch
        {
            0xf0 => S7ServerOperation.Setup,
            0x04 => S7ServerOperation.Read,
            0x05 => S7ServerOperation.Write,
            _ => S7ServerOperation.Any,
        };
    }

    /// <summary>Accepts clients until stopped.</summary>
    /// <param name="listener">The active TCP listener.</param>
    /// <param name="cancellationToken">The shutdown token.</param>
    /// <returns>A task representing the accept loop.</returns>
    private async Task AcceptLoopAsync(TcpListener listener, CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            TcpClient client;
            try
            {
#if NET8_0_OR_GREATER
                client = await listener.AcceptTcpClientAsync(cancellationToken).ConfigureAwait(false);
#else
                client = await listener.AcceptTcpClientAsync().ConfigureAwait(false);
#endif
            }
            catch (Exception ex) when (ex is ObjectDisposedException or SocketException)
            {
                return;
            }

            client.NoDelay = true;
            lock (_syncRoot)
            {
                if (!IsRunning)
                {
                    client.Dispose();
                    return;
                }

                _clients.Add(client);
                _ = _clientSessions.RemoveAll(static session => session.Completion.IsCompleted);
                _clientSessions.Add(new ClientSession(this, client, cancellationToken));
            }
        }
    }

    /// <summary>Captures the listener and active-client tasks that must finish during shutdown.</summary>
    /// <returns>The tasks to drain.</returns>
    private Task[] CaptureShutdownTasks()
    {
        var shutdownTasks = new Task[_clientSessions.Count + (_acceptLoop is null ? 0 : 1)];
        if (_acceptLoop is not null)
        {
            shutdownTasks[0] = _acceptLoop;
        }

        var taskOffset = _acceptLoop is null ? 0 : 1;
        for (var sessionIndex = 0; sessionIndex < _clientSessions.Count; sessionIndex++)
        {
            shutdownTasks[sessionIndex + taskOffset] = _clientSessions[sessionIndex].Completion;
        }

        _clientSessions.Clear();
        return shutdownTasks;
    }

    /// <summary>Processes frames for one client.</summary>
    /// <param name="client">The accepted client.</param>
    /// <param name="cancellationToken">The shutdown token.</param>
    /// <returns>A task representing client processing.</returns>
    private async Task HandleClientAsync(TcpClient client, CancellationToken cancellationToken)
    {
        try
        {
            using var stream = client.GetStream();
            while (!cancellationToken.IsCancellationRequested)
            {
                var frame = await ReadFrameAsync(stream, cancellationToken).ConfigureAwait(false);
                if (frame is null)
                {
                    return;
                }

                var operation = Classify(frame);
                var fault = TakeFault(operation);
                if (!await ApplyFaultAsync(stream, fault, cancellationToken).ConfigureAwait(false))
                {
                    return;
                }

                var response = BuildResponse(frame, operation, fault);
                if (response.Length == 0)
                {
                    return;
                }

                await stream.WriteAsync(response, 0, response.Length, cancellationToken).ConfigureAwait(false);
            }
        }
        catch (Exception ex) when (
            ex is IOException or ObjectDisposedException or SocketException or OperationCanceledException)
        {
            // Client disconnect and server shutdown are normal lifecycle events.
        }
        finally
        {
            lock (_syncRoot)
            {
                _ = _clients.Remove(client);
            }
        }
    }

    /// <summary>Builds the response for one request.</summary>
    /// <param name="request">The request frame.</param>
    /// <param name="operation">The classified operation.</param>
    /// <param name="fault">The optional scripted fault.</param>
    /// <returns>The response frame.</returns>
    private byte[] BuildResponse(byte[] request, S7ServerOperation operation, S7ServerFault? fault)
    {
        var returnCode = fault?.Kind == S7ServerFaultKind.ReturnCode
            ? fault.ReturnCode
            : SuccessReturnCode;

        return operation switch
        {
            S7ServerOperation.Connect => BuildCotpConnectionResponse(request),
            S7ServerOperation.Setup => BuildSetupResponse(request),
            S7ServerOperation.Read => BuildReadResponse(request, returnCode),
            S7ServerOperation.Write => BuildWriteResponse(request, returnCode),
            S7ServerOperation.Szl => BuildSzlResponse(request, returnCode),
            _ => [],
        };
    }

    /// <summary>Consumes and applies one matching fault.</summary>
    /// <param name="stream">The connected network stream.</param>
    /// <param name="fault">The optional fault to apply.</param>
    /// <param name="cancellationToken">The shutdown token.</param>
    /// <returns><see langword="true"/> when normal response handling should continue.</returns>
    private async Task<bool> ApplyFaultAsync(
        NetworkStream stream,
        S7ServerFault? fault,
        CancellationToken cancellationToken)
    {
        if (fault is null || fault.Kind == S7ServerFaultKind.ReturnCode)
        {
            return true;
        }

        if (fault.Kind == S7ServerFaultKind.Delay)
        {
            if (fault.Delay > TimeSpan.Zero)
            {
                await Task.Delay(fault.Delay, cancellationToken).ConfigureAwait(false);
            }

            return true;
        }

        if (fault.Kind != S7ServerFaultKind.MalformedFrame)
        {
            return false;
        }

        byte[] invalid = [3, 0, 0, 3];
        await stream.WriteAsync(invalid, 0, invalid.Length, cancellationToken).ConfigureAwait(false);

        return false;
    }

    /// <summary>Gets and removes the first matching fault.</summary>
    /// <param name="operation">The current operation.</param>
    /// <returns>The matching fault, or <see langword="null"/>.</returns>
    private S7ServerFault? TakeFault(S7ServerOperation operation)
    {
        lock (_syncRoot)
        {
            for (var i = 0; i < _faults.Count; i++)
            {
                var fault = _faults[i];
                if (fault.Operation != S7ServerOperation.Any && fault.Operation != operation)
                {
                    continue;
                }

                _faults.RemoveAt(i);
                return fault;
            }
        }

        return null;
    }

    /// <summary>Builds a COTP connection-confirm packet.</summary>
    /// <param name="request">The COTP connection request.</param>
    /// <returns>The COTP connection-confirm frame.</returns>
    private byte[] BuildCotpConnectionResponse(byte[] request)
    {
        byte[] response =
        [
            0x03, 0x00, 0x00, 0x16, 0x11, 0xd0, 0x00, 0x00, 0x00, 0x01, 0x00,
            0xc1, 0x02, 0x01, 0x00, 0xc2, 0x02, 0x01, 0x02, 0xc0, 0x01, 0x09,
        ];
        if (request.Length > 18)
        {
            response[13] = request[13];
            response[14] = request[14];
            response[17] = request[17];
            response[18] = request[18];
        }

        return response;
    }

    /// <summary>Builds an S7 setup-communication response.</summary>
    /// <param name="request">The setup-communication request.</param>
    /// <returns>The setup-communication response.</returns>
    private byte[] BuildSetupResponse(byte[] request)
    {
        var requested = request.Length > SetupRequestedPduOffset + 1
            ? ReadUInt16(request, SetupRequestedPduOffset)
            : PduLength;
        var negotiated = Math.Min(PduLength, requested);
        byte[] response =
        [
            0x03, 0x00, 0x00, 0x1b, 0x02, 0xf0, 0x80,
            0x32, 0x03, 0x00, 0x00, 0x00, 0x04, 0x00, 0x08, 0x00, 0x00,
            0x00, 0x00, 0xf0, 0x00, 0x00, 0x01, 0x00, 0x01, 0x00, 0x00,
        ];
        CopyPduReference(request, response);
        WriteUInt16(response, SetupResponsePduOffset, negotiated);
        return response;
    }

    /// <summary>Builds a ReadVar response.</summary>
    /// <param name="request">The ReadVar request.</param>
    /// <param name="scriptedReturnCode">The scripted item return code.</param>
    /// <returns>The ReadVar AckData response.</returns>
    private byte[] BuildReadResponse(byte[] request, byte scriptedReturnCode)
    {
        var itemCount = request.Length > 18 ? request[18] : (byte)0;
        var data = new List<byte>();
        var specOffset = VariableSpecificationOffset;
        for (var i = 0; i < itemCount; i++, specOffset += VariableSpecificationLength)
        {
            var returnCode = scriptedReturnCode;
            byte[] value = [];
            if (returnCode == SuccessReturnCode)
            {
                try
                {
                    var spec = ParseVariableSpecification(request, specOffset);
                    value = Memory.Read(spec.Area, spec.DbNumber, spec.Offset, spec.Count);
                }
                catch (Exception ex) when (
                    ex is ArgumentOutOfRangeException or KeyNotFoundException or InvalidDataException)
                {
                    returnCode = AddressOutOfRangeReturnCode;
                }
            }

            data.Add(returnCode);
            data.Add(0x04);
            AddUInt16(data, (ushort)(value.Length * BitsPerByte));
            data.AddRange(value);
            if ((value.Length & 1) == 1 && i < itemCount - 1)
            {
                data.Add(0);
            }
        }

        _ = Interlocked.Increment(ref _readCount);
        return BuildAckDataResponse(request, 0x04, itemCount, data);
    }

    /// <summary>Builds a WriteVar response and applies successful writes.</summary>
    /// <param name="request">The WriteVar request.</param>
    /// <param name="scriptedReturnCode">The scripted item return code.</param>
    /// <returns>The WriteVar AckData response.</returns>
    private byte[] BuildWriteResponse(byte[] request, byte scriptedReturnCode)
    {
        var itemCount = request.Length > 18 ? request[18] : (byte)0;
        var parameterLength = request.Length > ParameterLengthOffset + 1
            ? ReadUInt16(request, ParameterLengthOffset)
            : 0;
        var dataOffset = RequestHeaderLength + parameterLength;
        var returnCodes = new List<byte>(itemCount);
        for (var i = 0; i < itemCount; i++)
        {
            var returnCode = scriptedReturnCode;
            if (dataOffset + ItemDataHeaderLength > request.Length)
            {
                returnCodes.Add(AddressOutOfRangeReturnCode);
                continue;
            }

            var bitLength = ReadUInt16(request, dataOffset + TpktLengthOffset);
            var byteLength = (bitLength + BitsPerByte - 1) / BitsPerByte;
            var valueOffset = dataOffset + ItemDataHeaderLength;
            if (valueOffset + byteLength > request.Length)
            {
                returnCodes.Add(AddressOutOfRangeReturnCode);
                continue;
            }

            if (returnCode == SuccessReturnCode)
            {
                try
                {
                    var spec = ParseVariableSpecification(
                        request,
                        VariableSpecificationOffset + (i * VariableSpecificationLength));
                    var value = new byte[byteLength];
                    Buffer.BlockCopy(request, valueOffset, value, 0, byteLength);
                    Memory.Write(spec.Area, spec.DbNumber, spec.Offset, value);
                }
                catch (Exception ex) when (
                    ex is ArgumentOutOfRangeException or KeyNotFoundException or InvalidDataException)
                {
                    returnCode = AddressOutOfRangeReturnCode;
                }
            }

            returnCodes.Add(returnCode);
            dataOffset = valueOffset + byteLength + ((byteLength & 1) == 1 ? 1 : 0);
        }

        _ = Interlocked.Increment(ref _writeCount);
        return BuildAckDataResponse(request, 0x05, itemCount, returnCodes);
    }

    /// <summary>Builds a one-packet SZL response.</summary>
    /// <param name="request">The SZL request.</param>
    /// <param name="returnCode">The scripted return code.</param>
    /// <returns>The SZL response.</returns>
    private byte[] BuildSzlResponse(byte[] request, byte returnCode)
    {
        var szlId = request.Length > SzlIdentifierOffset + 1
            ? ReadUInt16(request, SzlIdentifierOffset)
            : (ushort)0;
        var payload = szlId switch
        {
            0x001c => CreateCpuInformation(),
            0x0011 => CreateOrderCode(),
            _ => Array.Empty<byte>(),
        };
        var response = new byte[SzlPayloadOffset + payload.Length];
        response[0] = 0x03;
        response[3] = (byte)response.Length;
        response[4] = 0x02;
        response[5] = 0xf0;
        response[6] = 0x80;
        response[7] = 0x32;
        response[8] = 0x07;
        CopyPduReference(request, response);
        response[14] = 0x0c;
        WriteUInt16(response, SzlHeaderDataLengthOffset, (ushort)(payload.Length + SzlDataOverhead));
        response[19] = 0x12;
        response[20] = 0x08;
        response[21] = 0x12;
        response[22] = 0x44;
        response[23] = 0x01;
        response[24] = 0x01;
        response[26] = 0;
        response[29] = returnCode;
        response[30] = 0x09;
        WriteUInt16(response, SzlDataLengthOffset, (ushort)(payload.Length + SzlMetadataLength));
        WriteUInt16(response, SzlTotalLengthOffset, (ushort)payload.Length);
        payload.CopyTo(response, SzlPayloadOffset);
        return response;
    }

    /// <summary>Builds a standard S7 AckData response.</summary>
    /// <param name="request">The request frame.</param>
    /// <param name="function">The S7 function code.</param>
    /// <param name="itemCount">The number of response items.</param>
    /// <param name="data">The encoded item data.</param>
    /// <returns>The complete AckData response.</returns>
    private byte[] BuildAckDataResponse(
        byte[] request,
        byte function,
        byte itemCount,
        List<byte> data)
    {
        var response = new byte[AckDataItemOffset + data.Count];
        response[0] = 0x03;
        WriteUInt16(response, TpktLengthOffset, (ushort)response.Length);
        response[4] = 0x02;
        response[5] = 0xf0;
        response[6] = 0x80;
        response[7] = 0x32;
        response[8] = 0x03;
        CopyPduReference(request, response);
        WriteUInt16(response, ParameterLengthOffset, AckDataParameterLength);
        WriteUInt16(response, AckDataLengthOffset, (ushort)data.Count);
        response[19] = function;
        response[20] = itemCount;
        var responseOffset = AckDataItemOffset;
        foreach (var item in data)
        {
            response[responseOffset] = item;
            responseOffset++;
        }

        return response;
    }

    /// <summary>Parses one S7ANY variable specification.</summary>
    /// <param name="request">The request frame.</param>
    /// <param name="offset">The start of the variable specification.</param>
    /// <returns>The parsed variable specification.</returns>
    private VariableSpecification ParseVariableSpecification(byte[] request, int offset)
    {
        if (offset < 0 || offset + VariableSpecificationLength > request.Length ||
            request[offset] != 0x12 || request[offset + S7AnySyntaxOffset] != 0x10)
        {
            throw new InvalidDataException("Invalid S7ANY variable specification.");
        }

        var area = (S7MemoryArea)request[offset + S7AnyAreaOffset];
#if NET8_0_OR_GREATER
        if (!Enum.IsDefined(area))
#else
        if (!Enum.IsDefined(typeof(S7MemoryArea), area))
#endif
        {
            throw new InvalidDataException("Unsupported S7 memory area.");
        }

        var dbNumber = ReadUInt16(request, offset + S7AnyDbNumberOffset);
        var count = ReadUInt16(request, offset + S7AnyCountOffset);
        var address =
            (request[offset + S7AnyAddressHighOffset] << HighAddressShift) |
            (request[offset + S7AnyAddressMiddleOffset] << BitsPerByte) |
            request[offset + S7AnyAddressLowOffset];
        var byteOffset = area is S7MemoryArea.Timer or S7MemoryArea.Counter
            ? address
            : address / BitsPerByte;
        return new(area, dbNumber, byteOffset, count);
    }

    /// <summary>Reads a complete TPKT frame.</summary>
    /// <param name="stream">The connected network stream.</param>
    /// <param name="cancellationToken">The shutdown token.</param>
    /// <returns>The complete frame, or <see langword="null"/> for an invalid or closed stream.</returns>
    private async Task<byte[]?> ReadFrameAsync(NetworkStream stream, CancellationToken cancellationToken)
    {
        var header = new byte[TpktHeaderLength];
        if (!await ReadExactAsync(stream, header, header.Length, cancellationToken).ConfigureAwait(false))
        {
            return null;
        }

        var length = ReadUInt16(header, TpktLengthOffset);
        if (header[0] != TpktVersion || length < header.Length)
        {
            return null;
        }

        var frame = new byte[length];
        Buffer.BlockCopy(header, 0, frame, 0, header.Length);
        return length == header.Length ||
            await ReadExactAsync(stream, frame, length - header.Length, cancellationToken, header.Length)
                .ConfigureAwait(false)
            ? frame
            : null;
    }

    /// <summary>Reads exactly the requested number of bytes.</summary>
    /// <param name="stream">The connected network stream.</param>
    /// <param name="buffer">The destination buffer.</param>
    /// <param name="count">The number of bytes to read.</param>
    /// <param name="cancellationToken">The shutdown token.</param>
    /// <param name="offset">The destination offset.</param>
    /// <returns><see langword="true"/> when all bytes were read.</returns>
    private async Task<bool> ReadExactAsync(
        NetworkStream stream,
        byte[] buffer,
        int count,
        CancellationToken cancellationToken,
        int offset = 0)
    {
        var total = 0;
        while (total < count)
        {
            var read = await stream.ReadAsync(
                buffer,
                offset + total,
                count - total,
                cancellationToken).ConfigureAwait(false);
            if (read == 0)
            {
                return false;
            }

            total += read;
        }

        return true;
    }

    /// <summary>Creates deterministic CPU information.</summary>
    /// <returns>The CPU information payload.</returns>
    private byte[] CreateCpuInformation()
    {
        var data = new byte[CpuInformationLength];
        WriteAscii(data, CpuAsNameOffset, CpuTextLength, "SIMULATOR");
        WriteAscii(data, CpuModuleNameOffset, CpuTextLength, "Managed S7 PLC");
        WriteAscii(data, CpuCopyrightOffset, CpuCopyrightLength, "Copyright CP");
        WriteAscii(data, CpuSerialNumberOffset, CpuTextLength, "SIM0000001");
        WriteAscii(data, CpuModuleTypeOffset, CpuModuleTypeLength, "CPU 1516-3 PN/DP");
        return data;
    }

    /// <summary>Creates deterministic order-code information.</summary>
    /// <returns>The order-code payload.</returns>
    private byte[] CreateOrderCode()
    {
        var data = new byte[OrderCodeLength];
        WriteAscii(data, OrderCodeOffset, OrderCodeTextLength, "6ES7 516-3AN02-0AB0");
        data[22] = 1;
        data[23] = 0;
        data[24] = 0;
        return data;
    }

    /// <summary>Writes an ASCII field.</summary>
    /// <param name="destination">The destination buffer.</param>
    /// <param name="offset">The field offset.</param>
    /// <param name="length">The maximum field length.</param>
    /// <param name="value">The ASCII value.</param>
    private void WriteAscii(byte[] destination, int offset, int length, string value)
    {
        var bytes = Encoding.ASCII.GetBytes(value);
        Buffer.BlockCopy(bytes, 0, destination, offset, Math.Min(length, bytes.Length));
    }

    /// <summary>Copies an S7 PDU reference.</summary>
    /// <param name="request">The request frame.</param>
    /// <param name="response">The response frame.</param>
    private void CopyPduReference(byte[] request, byte[] response)
    {
        if (request.Length <= 12 || response.Length <= 12)
        {
            return;
        }

        response[11] = request[11];
        response[12] = request[12];
    }

    /// <summary>Reads an unsigned big-endian word.</summary>
    /// <param name="buffer">The source buffer.</param>
    /// <param name="offset">The word offset.</param>
    /// <returns>The decoded value.</returns>
    private ushort ReadUInt16(byte[] buffer, int offset) =>
        (ushort)((buffer[offset] << 8) | buffer[offset + 1]);

    /// <summary>Writes an unsigned big-endian word.</summary>
    /// <param name="buffer">The destination buffer.</param>
    /// <param name="offset">The word offset.</param>
    /// <param name="value">The value.</param>
    private void WriteUInt16(byte[] buffer, int offset, ushort value)
    {
        buffer[offset] = (byte)(value >> 8);
        buffer[offset + 1] = (byte)value;
    }

    /// <summary>Adds an unsigned big-endian word.</summary>
    /// <param name="destination">The destination list.</param>
    /// <param name="value">The value.</param>
    private void AddUInt16(List<byte> destination, ushort value)
    {
        destination.Add((byte)(value >> 8));
        destination.Add((byte)value);
    }

    /// <summary>Throws after disposal.</summary>
    private void ThrowIfDisposed()
    {
        if (!_disposed)
        {
            return;
        }

        throw new ObjectDisposedException(nameof(ManagedS7Server));
    }

    /// <summary>Represents one parsed S7ANY variable specification.</summary>
    private readonly struct VariableSpecification
    {
        /// <summary>Initializes a new instance of the <see cref="VariableSpecification"/> struct.</summary>
        /// <param name="area">The S7 memory area.</param>
        /// <param name="dbNumber">The DB number.</param>
        /// <param name="offset">The byte offset.</param>
        /// <param name="count">The requested byte count.</param>
        public VariableSpecification(S7MemoryArea area, ushort dbNumber, int offset, int count)
        {
            Area = area;
            DbNumber = dbNumber;
            Offset = offset;
            Count = count;
        }

        /// <summary>Gets the S7 memory area.</summary>
        public S7MemoryArea Area { get; }

        /// <summary>Gets the DB number.</summary>
        public ushort DbNumber { get; }

        /// <summary>Gets the byte offset.</summary>
        public int Offset { get; }

        /// <summary>Gets the requested byte count.</summary>
        public int Count { get; }
    }

    /// <summary>Owns one connected client until its processing task completes.</summary>
    private sealed class ClientSession
    {
        /// <summary>The client owned by this session.</summary>
        private readonly TcpClient _client;

        /// <summary>Initializes a new instance of the <see cref="ClientSession"/> class.</summary>
        /// <param name="server">The server that processes the client.</param>
        /// <param name="client">The connected client owned by this session.</param>
        /// <param name="cancellationToken">The server shutdown token.</param>
        public ClientSession(
            ManagedS7Server server,
            TcpClient client,
            CancellationToken cancellationToken)
        {
            _client = client;
            Completion = RunAsync(server, cancellationToken);
        }

        /// <summary>Gets the client-processing completion task.</summary>
        public Task Completion { get; }

        /// <summary>Processes the owned client and releases it when processing completes.</summary>
        /// <param name="server">The server that processes the client.</param>
        /// <param name="cancellationToken">The server shutdown token.</param>
        /// <returns>A task representing the client session.</returns>
        private async Task RunAsync(ManagedS7Server server, CancellationToken cancellationToken)
        {
            try
            {
                await server.HandleClientAsync(_client, cancellationToken).ConfigureAwait(false);
            }
            finally
            {
                _client.Dispose();
            }
        }
    }
}
