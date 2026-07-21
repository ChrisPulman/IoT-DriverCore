// Copyright (c) 2019-2026 Chris Pulman and contributors. All rights reserved.
// Chris Pulman and contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Net;
using System.Net.Sockets;
#if REACTIVE_SHIM
using S7PlcRx.Reactive.PlcTypes;
#else
using S7PlcRx.PlcTypes;
#endif

#if REACTIVE_SHIM
namespace S7PlcRx.Reactive.Core;
#else
namespace S7PlcRx.Core;
#endif

/// <summary>Provides S7 socket connection functionality.</summary>
internal partial class S7SocketRx
{
    /// <summary>Observes availability changes without blocking the subscription callback.</summary>
    /// <param name="observer">The observer to notify.</param>
    private void ObserveAvailability(IObserver<bool> observer) => _ = ObserveAvailabilityAsync(observer);

    /// <summary>Processes an availability change asynchronously.</summary>
    /// <param name="observer">The observer to notify.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    private async Task ObserveAvailabilityAsync(IObserver<bool> observer)
    {
        try
        {
            if (_isAvailable is null)
            {
                return;
            }

            if (_isAvailable.Value)
            {
                if (!_initComplete && !await InitializeSiemensConnectionOptimizedAsync().ConfigureAwait(false))
                {
                    CloseSocketOptimized(_socket, _timeProvider);
                    _socket = null;
                    observer.OnError(new S7Exception(DeviceNotConnectedMessage));
                    return;
                }

                if (_initComplete && _isConnected != true)
                {
                    CloseSocketOptimized(_socket, _timeProvider);
                    _socket = null;
                    observer.OnError(new S7Exception(DeviceNotConnectedMessage));
                }

                return;
            }

            CloseSocketOptimized(_socket, _timeProvider);
            _socket = null;
            observer.OnError(new S7Exception("Device Unavailable"));
        }
        catch (Exception ex)
        {
            CloseSocketOptimized(_socket, _timeProvider);
            _socket = null;
            observer.OnError(ex);
        }
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
        var attemptSocket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
        attemptSocket.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReceiveTimeout, DefaultTimeout);
        attemptSocket.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.SendTimeout, DefaultTimeout);
        attemptSocket.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.KeepAlive, true);
        attemptSocket.SetSocketOption(SocketOptionLevel.Tcp, SocketOptionName.NoDelay, true);

        var bufferSize = DataReadLength * SocketBufferPduMultiplier;
        attemptSocket.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReceiveBuffer, bufferSize);
        attemptSocket.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.SendBuffer, bufferSize);

        var server = new IPEndPoint(IPAddress.Parse(IP), S7TcpPort);
#if NETFRAMEWORK
        var connectTask = Task.Factory.FromAsync(
            (callback, state) => attemptSocket.BeginConnect(server, callback, state),
            attemptSocket.EndConnect,
            null);
        await connectTask.ConfigureAwait(false);
#else
        await attemptSocket.ConnectAsync(server).ConfigureAwait(false);
#endif

        if (CheckConnectionStatusOptimized(attemptSocket) &&
            await PerformOptimizedHandshakeAsync(attemptSocket, profile).ConfigureAwait(false))
        {
            return attemptSocket;
        }

        CloseSocketOptimized(attemptSocket, _timeProvider);
        return null;
    }

    /// <summary>Stores the p ro be av ai la bi li ty an dn ot if ya sy n c value.</summary>
    /// <param name="observer">The o bs er v e r value.</param>
    /// <returns>The resulting value.</returns>
    private async Task ProbeAvailabilityAndNotifyAsync(IObserver<bool> observer)
    {
        var isReachable = await CheckAvailabilityOptimizedAsync().ConfigureAwait(false);
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
    private void ProbeAvailabilityAndNotify(IObserver<bool> observer) => _ = ProbeAvailabilityAndNotifyAsync(observer);

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
