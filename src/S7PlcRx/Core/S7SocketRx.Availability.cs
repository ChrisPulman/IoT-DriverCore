// Copyright (c) 2019-2026 Chris Pulman and contributors. All rights reserved.
// Chris Pulman and contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Net;
using System.Net.Sockets;
#if REACTIVE_SHIM
using IoT.DriverCore.S7PlcRx.Reactive.PlcTypes;
#else
using IoT.DriverCore.S7PlcRx.PlcTypes;
#endif

#if REACTIVE_SHIM
namespace IoT.DriverCore.S7PlcRx.Reactive.Core;
#else
namespace IoT.DriverCore.S7PlcRx.Core;
#endif

/// <summary>Provides S7 socket connection functionality.</summary>
internal partial class S7SocketRx
{
    /// <summary>Observes availability changes without blocking the subscription callback.</summary>
    /// <param name="observer">The observer to notify.</param>
    private void ObserveAvailability(IObserver<bool> observer)
    {
        lock (_lifecycleSync)
        {
            if (_disposedValue || !_availabilityObservationTask.IsCompleted)
            {
                return;
            }

            _availabilityObservationTask = Task.Run(() => ObserveAvailabilityAsync(observer));
        }
    }

    /// <summary>Processes an availability change asynchronously.</summary>
    /// <param name="observer">The observer to notify.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    private async Task ObserveAvailabilityAsync(IObserver<bool> observer)
    {
        try
        {
            await ProcessAvailabilityAsync(observer).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (_lifetimeCancellation.IsCancellationRequested)
        {
        }
        catch (ObjectDisposedException) when (_disposedValue)
        {
        }
        catch (Exception ex)
        {
            if (!LifecycleIsStopped())
            {
                ReportAvailabilityError(observer, ex);
            }
        }
    }

    /// <summary>Processes the current availability state for a connection observer.</summary>
    /// <param name="observer">The observer to notify.</param>
    /// <returns>A task representing the connection initialization, when required.</returns>
    private async Task ProcessAvailabilityAsync(IObserver<bool> observer)
    {
        if (LifecycleIsStopped() || _isAvailable is null)
        {
            return;
        }

        if (!_isAvailable.Value)
        {
            ReportAvailabilityError(observer, new S7Exception("Device Unavailable"));
            return;
        }

        var initialized = _initComplete ||
            await InitializeSiemensConnectionOptimizedAsync().ConfigureAwait(false);
        if (LifecycleIsStopped())
        {
            return;
        }

        if (initialized && (!_initComplete || _isConnected == true))
        {
            return;
        }

        ReportAvailabilityError(observer, new S7Exception(DeviceNotConnectedMessage));
    }

    /// <summary>Closes the active socket and reports an availability error.</summary>
    /// <param name="observer">The observer to notify.</param>
    /// <param name="exception">The error to report.</param>
    private void ReportAvailabilityError(IObserver<bool> observer, Exception exception)
    {
        CloseSocketOptimized(_socket, _timeProvider);
        _socket = null;
        observer.OnError(exception);
    }

    /// <summary>Sends an SZL request packet.</summary>
    /// <param name="requestData">The SZL request details.</param>
    /// <param name="first">Whether this is the initial request.</param>
    /// <param name="sequenceIn">The incoming sequence value.</param>
    /// <param name="sequenceOut">The outgoing sequence value.</param>
    private void SendSzlRequest(
        SzlRequest requestData,
        bool first,
        byte sequenceIn,
        ref ushort sequenceOut)
    {
        sequenceOut++;
        var request = first ? requestData.FirstRequest : requestData.ContinuationRequest;
        Word.ToByteArray(sequenceOut, request, SzlRequestSequenceOffset);
        if (first)
        {
            Word.ToByteArray(requestData.SzlArea, request, SzlAreaOffset);
            Word.ToByteArray(requestData.Index, request, SzlIndexOffset);
        }
        else
        {
            request[SzlContinuationSequenceOffset] = sequenceIn;
        }

        _ = Send(requestData.Tag, request, request.Length);
    }

    /// <summary>Connects and performs a handshake using a TSAP profile.</summary>
    /// <param name="profile">The TSAP profile to use.</param>
    /// <returns>The connected socket, or <see langword="null"/> when the attempt fails.</returns>
    private async Task<Socket?> ConnectWithProfileAsync(TsapProfile profile)
    {
        if (LifecycleIsStopped())
        {
            return null;
        }

        var attemptSocket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
        if (!TryTrackConnectionAttempt(attemptSocket))
        {
            attemptSocket.Dispose();
            return null;
        }

        var socketAccepted = false;
        using var cancellationRegistration = _lifetimeCancellation.Token.Register(
            () => CloseSocketOptimized(attemptSocket, _timeProvider));
        try
        {
            attemptSocket.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReceiveTimeout, DefaultTimeout);
            attemptSocket.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.SendTimeout, DefaultTimeout);
            attemptSocket.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.KeepAlive, true);
            attemptSocket.SetSocketOption(SocketOptionLevel.Tcp, SocketOptionName.NoDelay, true);

            var bufferSize = DataReadLength * SocketBufferPduMultiplier;
            attemptSocket.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReceiveBuffer, bufferSize);
            attemptSocket.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.SendBuffer, bufferSize);

            var server = new IPEndPoint(IPAddress.Parse(IP), _s7TcpPort);
#if NETFRAMEWORK
            var connected = await ConnectSocketNetFrameworkAsync(
                attemptSocket,
                server,
                DefaultTimeout,
                _lifetimeCancellation.Token).ConfigureAwait(false);
#else
            using var connectCancellation = CancellationTokenSource.CreateLinkedTokenSource(
                _lifetimeCancellation.Token);
            connectCancellation.CancelAfter(DefaultTimeout);
            await attemptSocket.ConnectAsync(server, connectCancellation.Token).ConfigureAwait(false);
            var connected = attemptSocket.Connected;
#endif

            if (!connected || _lifetimeCancellation.IsCancellationRequested)
            {
                return null;
            }

            if (CheckConnectionStatusOptimized(attemptSocket) &&
                await PerformOptimizedHandshakeAsync(attemptSocket, profile).ConfigureAwait(false))
            {
                socketAccepted = true;
                return attemptSocket;
            }

            return null;
        }
        catch (OperationCanceledException) when (_lifetimeCancellation.IsCancellationRequested)
        {
            return null;
        }
        catch (ObjectDisposedException) when (_disposedValue || _lifetimeCancellation.IsCancellationRequested)
        {
            return null;
        }
        finally
        {
            if (!socketAccepted)
            {
                ClearConnectionAttempt(attemptSocket);
                CloseSocketOptimized(attemptSocket, _timeProvider);
            }
        }
    }

    /// <summary>Registers a new connection-attempt socket when the transport is still active.</summary>
    /// <param name="attemptSocket">The socket to track.</param>
    /// <returns><see langword="true"/> when the transport accepted ownership.</returns>
    private bool TryTrackConnectionAttempt(Socket attemptSocket)
    {
        lock (_lifecycleSync)
        {
            if (_disposedValue)
            {
                return false;
            }

            _connectionAttemptSocket = attemptSocket;
            return true;
        }
    }

    /// <summary>Clears an in-progress connection socket when it is still the active attempt.</summary>
    /// <param name="attemptSocket">The socket whose ownership is being released.</param>
    private void ClearConnectionAttempt(Socket attemptSocket)
    {
        lock (_lifecycleSync)
        {
            if (ReferenceEquals(_connectionAttemptSocket, attemptSocket))
            {
                _connectionAttemptSocket = null;
            }
        }
    }

    /// <summary>Stores the p ro be av ai la bi li ty an dn ot if ya sy n c value.</summary>
    /// <param name="observer">The o bs er v e r value.</param>
    /// <returns>The resulting value.</returns>
    private async Task ProbeAvailabilityAndNotifyAsync(IObserver<bool> observer)
    {
        var isReachable = await CheckAvailabilityOptimizedAsync().ConfigureAwait(false);
        if (_disposedValue || _lifetimeCancellation.IsCancellationRequested)
        {
            return;
        }

        if (isReachable)
        {
            _consecutiveAvailabilityFailures = 0;
            _isAvailable = true;
        }
        else if (_initComplete && _isConnected == true)
        {
            _consecutiveAvailabilityFailures++;

            if (_consecutiveAvailabilityFailures < AvailabilityFailureThreshold)
            {
                _isAvailable = true;
            }
            else
            {
                _isAvailable = false;
                LogWarning(
                    string.Concat(
                        $"Availability probe failed {_consecutiveAvailabilityFailures} times consecutively. ",
                        "Marking PLC unavailable."),
                    _timeProvider);
            }
        }
        else
        {
            _consecutiveAvailabilityFailures = AvailabilityFailureThreshold;
            _isAvailable = false;
        }

        observer.OnNext(_isAvailable == true);
    }

    /// <summary>Starts an availability probe without blocking the timer callback.</summary>
    /// <param name="observer">The observer to notify.</param>
    private void ProbeAvailabilityAndNotify(IObserver<bool> observer)
    {
        lock (_lifecycleSync)
        {
            if (_disposedValue || !_availabilityProbeTask.IsCompleted)
            {
                return;
            }

            _availabilityProbeTask = Task.Run(() => ProbeAvailabilityAndNotifyAsync(observer));
        }
    }

    /// <summary>Stores the e va lu at ec on ne ct io ns ta te wi th hy st er es i s value.</summary>
    /// <returns>The resulting value.</returns>
    private bool EvaluateConnectionStateWithHysteresis()
    {
        if (!_initComplete)
        {
            _consecutiveConnectionFailures = 0;
            return false;
        }

        var isConnectedNow = CheckConnectionStatusOptimized();
        if (isConnectedNow)
        {
            _consecutiveConnectionFailures = 0;
            return true;
        }

        _consecutiveConnectionFailures++;

        if (_initComplete && _isConnected == true && _consecutiveConnectionFailures < ConnectionFailureThreshold)
        {
            return true;
        }

        if (!_initComplete || _consecutiveConnectionFailures != ConnectionFailureThreshold)
        {
            return false;
        }

        LogWarning(
            string.Concat(
                $"Connection probe failed {_consecutiveConnectionFailures} times consecutively. ",
                "Restarting connection."),
            _timeProvider);
        RestartConnection();
        return false;
    }

    /// <summary>Stores the u pd at en eg ot ia te dp du le ng t h value.</summary>
    /// <param name="response">The r es po n s e value.</param>
    /// <param name="responseLength">The r es po ns el en g t h value.</param>
    private void UpdateNegotiatedPduLength(byte[] response, int responseLength)
    {
        if (responseLength < CommunicationSetupResponseLength)
        {
            return;
        }

        var negotiatedPduLength = Word.FromByteArray(response, NegotiatedPduLengthOffset);
        if (negotiatedPduLength is < MinimumNegotiatedPduLength or > MaximumNegotiatedPduLength)
        {
            return;
        }

        if (negotiatedPduLength != DataReadLength)
        {
            LogInfo($"Negotiated PDU length updated to {negotiatedPduLength} bytes.", _timeProvider);
        }

        DataReadLength = negotiatedPduLength;
    }
}
