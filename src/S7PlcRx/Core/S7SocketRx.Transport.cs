// Copyright (c) 2019-2026 Chris Pulman and contributors. All rights reserved.
// Chris Pulman and contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using System.Net.Sockets;

#if REACTIVE_SHIM
namespace IoT.DriverCore.S7PlcRx.Reactive.Core;
#else
namespace IoT.DriverCore.S7PlcRx.Core;
#endif

/// <summary>Provides S7 socket connection functionality.</summary>
internal partial class S7SocketRx
{
#if NETFRAMEWORK
    /// <summary>Sends a request through the legacy asynchronous socket API.</summary>
    /// <param name="socket">The connected socket.</param>
    /// <param name="request">The request bytes.</param>
    /// <returns>A task containing the number of bytes sent.</returns>
    private static Task<int> SendNetStandardAsync(Socket socket, byte[] request) =>
        Task.Factory.FromAsync(
            (callback, state) => socket.BeginSend(
                request,
                0,
                request.Length,
                SocketFlags.None,
                callback,
                state),
            socket.EndSend,
            null);
#endif

    /// <summary>Stores the r ec ei ve r a w value.</summary>
    /// <param name="tag">The t a g value.</param>
    /// <param name="buffer">The b uf f e r value.</param>
    /// <param name="size">The s i z e value.</param>
    /// <param name="offset">The o ff s e t value.</param>
    /// <returns>The resulting value.</returns>
    private int ReceiveRaw(Tag? tag, byte[] buffer, int size, int offset = 0)
        => ReceiveCore(tag, buffer, size, offset, traceOperation: false);

    /// <summary>Stores the r ec ei ve co r e value.</summary>
    /// <param name="tag">The t a g value.</param>
    /// <param name="buffer">The b uf f e r value.</param>
    /// <param name="size">The s i z e value.</param>
    /// <param name="offset">The o ff s e t value.</param>
    /// <param name="traceOperation">The t ra ce op er at i o n value.</param>
    /// <returns>The resulting value.</returns>
    private int ReceiveCore(Tag? tag, byte[] buffer, int size, int offset, bool traceOperation)
    {
        if (!_initComplete)
        {
            return -1;
        }

        try
        {
            var stopwatch = Stopwatch.StartNew();

            if (_socket?.Connected == true)
            {
                var received = _socket.Receive(buffer, offset, size, SocketFlags.None);

                stopwatch.Stop();
                RecordSuccessfulOperation(stopwatch.Elapsed, received, isReceive: true);

                if (traceOperation && tag is not null && Debugger.IsAttached)
                {
                    var result = buffer[S7ResponseReturnCodeOffset] == S7ReturnCodeSuccess ? Success : Failed;
                    Debug.WriteLine(
                        $"{_timeProvider.GetUtcNow().LocalDateTime} Read Tag: {tag.Name} value: {tag.Value} {result} " +
                        $"({received} bytes, {stopwatch.ElapsedMilliseconds}ms)");
                }

                return received;
            }

            RecordError();
            _socketExceptionSubject.OnNext(new S7Exception(DeviceNotConnectedMessage));
        }
        catch (Exception ex)
        {
            RecordError();
            _socketExceptionSubject.OnNext(ex);
        }

        return -1;
    }

    /// <summary>
    /// Attempts to establish and optimize a connection to a Siemens PLC using multiple TSAP profiles for maximum
    /// compatibility.
    /// </summary>
    /// <remarks>This method tries several known TSAP profiles to improve the likelihood of connecting to
    /// different Siemens PLC models. It configures socket options for optimal performance and handles connection
    /// retries internally. If the connection cannot be established with any profile, the method returns <see
    /// langword="false"/>. This method is thread-safe and should not be called concurrently with other connection
    /// initialization methods.</remarks>
    /// <returns>A task that represents the asynchronous operation. The task result is <see langword="true"/> if the
    /// connection
    /// is successfully established and initialized; otherwise, <see langword="false"/>.</returns>
    private async Task<bool> InitializeSiemensConnectionOptimizedAsync()
    {
        var lockTaken = false;
        try
        {
            await _connectionLock.WaitAsync(_lifetimeCancellation.Token).ConfigureAwait(false);
            lockTaken = true;
            if (_disposedValue)
            {
                return false;
            }

            if (_initComplete && _socket is not null && CheckConnectionStatusOptimized(_socket))
            {
                return true;
            }

            foreach (var profile in new[] { TsapProfile.PG, TsapProfile.OP, TsapProfile.PGAlt })
            {
                _lifetimeCancellation.Token.ThrowIfCancellationRequested();
                var attemptSocket = await ConnectWithProfileAsync(profile).ConfigureAwait(false);
                if (attemptSocket is not null && TryAdoptConnectionSocket(attemptSocket))
                {
                    return true;
                }
            }

            return false;
        }
        catch (OperationCanceledException) when (_lifetimeCancellation.IsCancellationRequested)
        {
            return false;
        }
        catch (Exception ex)
        {
            LogError($"Connection initialization failed: {ex.Message}", _timeProvider);
            return false;
        }
        finally
        {
            try
            {
                if (lockTaken)
                {
                    _ = _connectionLock.Release();
                }
            }
            catch (ObjectDisposedException)
            {
                // Teardown race: dispose may win while an async connect is completing.
            }
        }
    }

    /// <summary>Transfers a successfully initialized socket into the active connection.</summary>
    /// <param name="attemptSocket">The initialized connection-attempt socket.</param>
    /// <returns><see langword="true"/> when ownership was transferred.</returns>
    private bool TryAdoptConnectionSocket(Socket attemptSocket)
    {
        Socket? oldSocket;
        lock (_lifecycleSync)
        {
            if (LifecycleIsStopped())
            {
                if (ReferenceEquals(_connectionAttemptSocket, attemptSocket))
                {
                    _connectionAttemptSocket = null;
                }

                CloseSocketOptimized(attemptSocket, _timeProvider);
                return false;
            }

            oldSocket = _socket;
            _socket = attemptSocket;
            if (ReferenceEquals(_connectionAttemptSocket, attemptSocket))
            {
                _connectionAttemptSocket = null;
            }

            _initComplete = true;
            _isConnected = true;
            _lastSuccessfulOperation = _timeProvider.GetUtcNow().UtcDateTime;
            _consecutiveErrors = 0;
            _consecutiveAvailabilityFailures = 0;
        }

        CloseSocketOptimized(oldSocket, _timeProvider);
        LogInfo(
            $"Successfully connected to {PLCType} at {IP}:{_s7TcpPort} with PDU length {DataReadLength}",
            _timeProvider);
        return true;
    }

    /// <summary>Performs an optimized asynchronous handshake using the specified TSAP profile.</summary>
    /// <param name="socket">The connected socket used for the handshake.</param>
    /// <param name="profile">The TSAP profile to use for the handshake operation. Cannot be null.</param>
    /// <returns>A task that represents the asynchronous operation. The task result is <see langword="true"/> if the
    /// handshake
    /// succeeds; otherwise, <see langword="false"/>.</returns>
    private async Task<bool> PerformOptimizedHandshakeAsync(Socket socket, TsapProfile profile)
    {
        var receiveBuffer = _bufferPool.Rent(HandshakeReceiveBufferSize);
        try
        {
#if NETFRAMEWORK
            return await PerformOptimizedHandshakeNetStandardAsync(socket, receiveBuffer, profile)
                .ConfigureAwait(false);
#else
            return await PerformOptimizedHandshakeModernAsync(socket, receiveBuffer, profile).ConfigureAwait(false);
#endif
        }
        finally
        {
            _bufferPool.Return(receiveBuffer);
        }
    }

#if NETFRAMEWORK
    /// <summary>Receives a complete TPKT packet on .NET Framework using the legacy socket async pattern.</summary>
    /// <param name="socket">The connected socket to read from.</param>
    /// <param name="buffer">The destination buffer that receives the packet bytes.</param>
    /// <param name="expectedMin">The minimum expected packet length in bytes.</param>
    /// <returns>The number of bytes read, or a value less than four when the TPKT header could not be read.</returns>
    private async Task<int> ReceiveTpktExactNetStandardAsync(Socket socket, byte[] buffer, int expectedMin)
    {
        GC.KeepAlive(this);

        // Read TPKT header (4 bytes)
        var read = await ReceiveExactAsync(socket, buffer, TpktHeaderLength, 0).ConfigureAwait(false);
        if (read != TpktHeaderLength)
        {
            return read;
        }

        var length = (buffer[TpktLengthHighByteOffset] << BitsPerByte) | buffer[TpktLengthLowByteOffset];
        if (length < TpktHeaderLength || length > buffer.Length)
        {
            LogWarning($"Invalid TPKT length {length} for receive buffer {buffer.Length}", _timeProvider);
            return 0;
        }

        if (length < expectedMin && expectedMin > 0)
        {
            // Try to continue anyway, but report
            LogWarning($"TPKT length {length} smaller than expected {expectedMin}", _timeProvider);
        }

        var remaining = length - TpktHeaderLength;
        if (remaining == 0)
        {
            return read;
        }

        var bodyRead = await ReceiveExactAsync(socket, buffer, remaining, TpktHeaderLength).ConfigureAwait(false);
        return bodyRead <= 0 ? read : read + bodyRead;

        static async Task<int> ReceiveExactAsync(Socket socket, byte[] buffer, int size, int offset)
        {
            var total = 0;
            while (total < size)
            {
                var received = await Task.Factory.FromAsync(
                    (callback, state) => socket.BeginReceive(
                        buffer,
                        offset + total,
                        size - total,
                        SocketFlags.None,
                        callback,
                        state),
                    socket.EndReceive,
                    null).ConfigureAwait(false);

                if (received <= 0)
                {
                    break;
                }

                total += received;
            }

            return total;
        }
    }

    /// <summary>Performs the legacy asynchronous S7 handshake sequence on .NET Framework sockets.</summary>
    /// <param name="socket">The connected socket used for the handshake.</param>
    /// <param name="receiveBuffer">The shared buffer that receives handshake responses.</param>
    /// <param name="profile">The TSAP profile that defines the connection parameters.</param>
    /// <returns>
    /// <see langword="true"/> when the handshake completes successfully; otherwise, <see langword="false"/>.
    /// </returns>
    private async Task<bool> PerformOptimizedHandshakeNetStandardAsync(
        Socket socket,
        byte[] receiveBuffer,
        TsapProfile profile)
    {
        try
        {
            // Step 1: Initial connection request
            var connectionRequest = GetConnectionRequestBytes(profile);
            var sent = await SendNetStandardAsync(socket, connectionRequest).ConfigureAwait(false);
            if (sent != connectionRequest.Length)
            {
                LogError("Failed to send initial connection request", _timeProvider);
                return false;
            }

            // Step 2: Receive connection response (TPKT length based)
            var received = await ReceiveTpktExactNetStandardAsync(socket, receiveBuffer, ConnectionResponseLength)
                .ConfigureAwait(false);

            if (received < ConnectionResponseLength)
            {
                LogError($"Invalid connection response length {received}", _timeProvider);
                return false;
            }

            // Step 3: Communication setup request
            var communicationSetupRequest = GetCommunicationSetupBytes();
            sent = await SendNetStandardAsync(socket, communicationSetupRequest).ConfigureAwait(false);
            if (sent != communicationSetupRequest.Length)
            {
                LogError("Failed to send communication setup request", _timeProvider);
                return false;
            }

            // Step 4: Receive communication setup response (TPKT length based)
            received = await ReceiveTpktExactNetStandardAsync(socket, receiveBuffer, CommunicationSetupResponseLength)
                .ConfigureAwait(false);
            if (received < CommunicationSetupResponseLength)
            {
                LogError($"Invalid communication setup response length {received}", _timeProvider);
                return false;
            }

            UpdateNegotiatedPduLength(receiveBuffer, received);

            return true;
        }
        catch (Exception ex)
        {
            LogError($"Handshake failed: {ex.Message}", _timeProvider);
            return false;
        }
    }

#else
    /// <summary>Performs an optimized handshake sequence using the modern socket API.</summary>
    /// <remarks>This method sends and receives protocol-specific handshake messages to establish a
    /// connection. If any step in the handshake fails, the method logs an error and returns <see langword="false"/>.
    /// The method does not throw exceptions for handshake failures; instead, it returns <see langword="false"/> to
    /// indicate failure.</remarks>
    /// <param name="socket">The connected socket used for the handshake.</param>
    /// <param name="receiveBuffer">
    /// A buffer used to receive handshake response data from the remote endpoint. Must be large enough to hold the
    /// expected handshake messages.</param>
    /// <param name="profile">The connection profile containing parameters required for the handshake process.</param>
    /// <returns>A task that represents the asynchronous operation. The task result is <see langword="true"/> if the
    /// handshake
    /// completes successfully; otherwise, <see langword="false"/>.</returns>
    private async Task<bool> PerformOptimizedHandshakeModernAsync(
        Socket socket,
        byte[] receiveBuffer,
        TsapProfile profile)
    {
        try
        {
            // Step 1: Initial connection request
            var connectionRequest = GetConnectionRequestBytes(profile);
            var sent = await socket.SendAsync(connectionRequest, SocketFlags.None).ConfigureAwait(false);
            if (sent != connectionRequest.Length)
            {
                LogError("Failed to send initial connection request", _timeProvider);
                return false;
            }

            // Step 2: Receive connection response (TPKT length based)
            var received = await ReceiveTpktExactModernAsync(socket, receiveBuffer, ConnectionResponseLength)
                .ConfigureAwait(false);
            if (received < ConnectionResponseLength)
            {
                LogError($"Invalid connection response length {received}", _timeProvider);
                return false;
            }

            // Step 3: Communication setup request
            var communicationSetupRequest = GetCommunicationSetupBytes();
            sent = await socket.SendAsync(communicationSetupRequest, SocketFlags.None).ConfigureAwait(false);
            if (sent != communicationSetupRequest.Length)
            {
                LogError("Failed to send communication setup request", _timeProvider);
                return false;
            }

            // Step 4: Receive communication setup response (TPKT length based)
            received = await ReceiveTpktExactModernAsync(socket, receiveBuffer, CommunicationSetupResponseLength)
                .ConfigureAwait(false);
            if (received < CommunicationSetupResponseLength)
            {
                LogError($"Invalid communication setup response length {received}", _timeProvider);
                return false;
            }

            UpdateNegotiatedPduLength(receiveBuffer, received);

            return true;
        }
        catch (Exception ex)
        {
            LogError($"Handshake failed: {ex.Message}", _timeProvider);
            return false;
        }
    }

#endif
}
