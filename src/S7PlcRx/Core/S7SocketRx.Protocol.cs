// Copyright (c) 2019-2026 Chris Pulman and contributors. All rights reserved.
// Chris Pulman and contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Runtime.CompilerServices;
#if REACTIVE_SHIM
using S7PlcRx.Reactive.PlcTypes;
#else
using S7PlcRx.PlcTypes;
#endif
using TimeSpan = System.TimeSpan;

#if REACTIVE_SHIM
namespace S7PlcRx.Reactive.Core;
#else
namespace S7PlcRx.Core;
#endif

/// <summary>Provides S7 socket connection functionality.</summary>
internal partial class S7SocketRx
{
    /// <summary>Receives exactly the requested number of bytes unless the socket closes first.</summary>
    /// <param name="tag">The tag associated with the receive operation.</param>
    /// <param name="buffer">The receive buffer.</param>
    /// <param name="size">The number of bytes to receive.</param>
    /// <param name="offset">The buffer offset.</param>
    /// <returns>The number of bytes received.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private int ReceiveExact(Tag tag, byte[] buffer, int size, int offset = 0)
    {
        var total = 0;
        while (total < size)
        {
            var read = ReceiveRaw(tag, buffer, size - total, offset + total);
            if (read <= 0)
            {
                return total > 0 ? total : read;
            }

            total += read;
        }

        return total;
    }

    /// <summary>Builds a connection request message.</summary>
    /// <remarks>The generated message is compatible with S7-1200, S7-300, and S7-400 devices, using a TPDU
    /// size of 512 bytes. The TSAP values are set based on the properties of the supplied profile.</remarks>
    /// <param name="profile">
    /// The TSAP profile containing source and destination transport service access point values used to construct the
    /// connection request.
    /// </param>
    /// <returns>
    /// A byte array representing the connection request, with TSAP fields set according to the provided profile.
    /// </returns>
    private byte[] GetConnectionRequestBytes(TsapProfile profile)
    {
        byte[] connectionRequest =
        [
            TpktVersion, 0, 0, ConnectionRequestTelegramLength,
            CotpConnectionRequestHeaderSize, CotpConnectionRequestPduType,
            0, 0, 0, CotpSourceReferenceLowByte, 0,
            CotpSourceTsapParameterCode, CotpTsapParameterLength,
            ProgrammingDeviceSourceTsapHighByte, DefaultSourceTsapLowByte,
            CotpDestinationTsapParameterCode, CotpTsapParameterLength,
            DefaultDestinationTsapHighByte, 0,
            CotpTpduSizeParameterCode, CotpTpduSizeParameterLength, CotpTpduSize512Bytes
        ];

        // Use TPDU size 512 (0x09) for S7-1200/300/400 compatibility
        // Source TSAP (C1)
        connectionRequest[ConnectionRequestSourceTsapHighOffset] = profile.SrcHi;
        connectionRequest[ConnectionRequestSourceTsapLowOffset] = profile.SrcLo;

        // Destination TSAP (C2)
        connectionRequest[ConnectionRequestDestinationTsapHighOffset] = profile.DstHi;
        connectionRequest[ConnectionRequestDestinationTsapLowOffset] = profile.DstLo(Rack, Slot);

        return connectionRequest;
    }

    /// <summary>Creates a PLC communication setup message.</summary>
    /// <remarks>The returned array includes protocol-specific configuration values, including the optimal PDU
    /// length for the target PLC type. This method is intended for internal use when establishing or configuring a PLC
    /// communication session.</remarks>
    /// <returns>A byte array representing the communication setup message to be sent to the PLC.</returns>
    private byte[] GetCommunicationSetupBytes()
    {
        byte[] communicationSetupRequest =
        [
            TpktVersion, 0, 0, CommunicationSetupTelegramLength,
            CotpDataHeaderSize, CotpDataPduType, CotpEndOfTransmissionUnit,
            S7ProtocolIdentifier, S7JobMessageType, 0, 0, CommunicationSetupPduReference,
            0, 0, CommunicationSetupParameterLength, 0, 0,
            S7SetupCommunicationFunction, 0, 0, 1, 0, 1, 0, DefaultRequestedPduLengthLowByte
        ];

        // Set optimal PDU length for the specific PLC type
        Word.ToByteArray(DataReadLength, communicationSetupRequest, CommunicationSetupPduLengthOffset);

        return communicationSetupRequest;
    }

    /// <summary>Records a successful send or receive operation, updating internal metrics and error counters.</summary>
    /// <param name="duration">The duration of the completed operation.</param>
    /// <param name="bytes">The number of bytes processed during the operation.</param>
    /// <param name="isReceive">true to record a receive operation; false to record a send operation.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void RecordSuccessfulOperation(TimeSpan duration, int bytes, bool isReceive)
    {
        _lastSuccessfulOperation = _timeProvider.GetUtcNow().UtcDateTime;
        _consecutiveErrors = 0;
        _consecutiveAvailabilityFailures = 0;

        if (isReceive)
        {
            _metrics.RecordReceive(duration, bytes);
        }
        else
        {
            _metrics.RecordSend(duration, bytes);
        }
    }

    /// <summary>Records an error occurrence and updates internal error tracking state.</summary>
    /// <remarks>If the number of consecutive errors exceeds a predefined threshold, this method initiates a
    /// connection restart to recover from persistent failures.</remarks>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void RecordError()
    {
        _consecutiveErrors++;
        _metrics.RecordError();

        // Trigger connection restart if too many consecutive errors
        if (_consecutiveErrors != ConsecutiveErrorRestartThreshold)
        {
            return;
        }

        LogWarning($"Excessive failures detected: {_consecutiveErrors}. Restarting connection.", _timeProvider);
        RestartConnection();
    }

    /// <summary>Attempts to asynchronously restart the network connection after a failure.</summary>
    /// <remarks>This method initiates a background task to close the current socket, reset connection state,
    /// and re-establish the connection. If the object has been disposed, the operation is not performed. Any exceptions
    /// encountered during the restart process are logged. This method is intended for internal use and is not
    /// thread-safe.</remarks>
    private void RestartConnection() =>
        Task.Run(async () =>
        {
            if (_disposedValue)
            {
                return;
            }

            if (Interlocked.CompareExchange(ref _restartInProgress, 1, 0) != 0)
            {
                return;
            }

            try
            {
                LogWarning("Restarting connection due to failures", _timeProvider);
                var socket = _socket;
                _socket = null;
                _initComplete = false;
                _isConnected = false;
                CloseSocketOptimized(socket, _timeProvider);

                await Task.Delay(ConnectionRestartDelayMilliseconds).ConfigureAwait(false);

                if (!_disposedValue)
                {
                    _disposable?.Dispose();
                    _disposable = Connect.Subscribe();
                }
            }
            catch (Exception ex)
            {
                LogError($"Connection restart failed: {ex.Message}", _timeProvider);
            }
            finally
            {
                _ = Interlocked.Exchange(ref _restartInProgress, 0);
            }
        });

    /// <summary>Reports the current set of collected metrics to subscribed observers.</summary>
    /// <remarks>Any exceptions encountered during reporting are logged and do not propagate to the caller.</remarks>
    private void ReportMetrics()
    {
        try
        {
            _metricsSubject.OnNext(_metrics.GetSnapshot());
        }
        catch (Exception ex)
        {
            LogError($"Failed to report metrics: {ex.Message}", _timeProvider);
        }
    }

    /// <summary>Asynchronously checks whether the configured IP address is reachable.</summary>
    /// <remarks>Returns <see langword="false"/> if the IP address is not set or is invalid, or if the ping
    /// operation fails due to network errors or exceptions.</remarks>
    /// <returns>
    /// A task that represents the asynchronous operation. The task result is <see langword="true"/> if the IP address
    /// responds successfully to a ping request; otherwise, <see langword="false"/>.
    /// </returns>
    private async Task<bool> CheckAvailabilityOptimizedAsync()
    {
        if (string.IsNullOrWhiteSpace(IP))
        {
            return false;
        }

        if (_initComplete && _socket is not null && CheckConnectionStatusOptimized(_socket) &&
            _timeProvider.GetUtcNow().UtcDateTime - _lastSuccessfulOperation <= RecentOperationAvailabilityWindow)
        {
            return true;
        }

        try
        {
            using var ping = new Ping();
            var result = await ping.SendPingAsync(IP, PingTimeoutMs).ConfigureAwait(false);
            if (result.Status == IPStatus.Success)
            {
                return true;
            }
        }
        catch (PingException)
        {
        }
        catch (Exception ex)
        {
            LogError($"Ping failed: {ex.Message}", _timeProvider);
        }

        return await CheckPortAvailabilityAsync().ConfigureAwait(false);
    }

    /// <summary>Stores the c he ck po rt av ai la bi li ty as y n c value.</summary>
    /// <returns>The resulting value.</returns>
    private async Task<bool> CheckPortAvailabilityAsync()
    {
        using var probeSocket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
        probeSocket.Blocking = false;

        try
        {
            var server = new IPEndPoint(IPAddress.Parse(IP), S7TcpPort);
#if NETFRAMEWORK
            var connectTask = Task.Factory.FromAsync(
                (callback, state) => probeSocket.BeginConnect(server, callback, state),
                probeSocket.EndConnect,
                null);
            var timeoutTask = Task.Delay(PortProbeTimeoutMs);
            var completedTask = await Task.WhenAny(connectTask, timeoutTask).ConfigureAwait(false);
            return completedTask == connectTask && probeSocket.Connected;
#else
            using var cts = new CancellationTokenSource(PortProbeTimeoutMs);
            await probeSocket.ConnectAsync(server, cts.Token).ConfigureAwait(false);
            return probeSocket.Connected;
#endif
        }
        catch (OperationCanceledException)
        {
            return false;
        }
        catch (SocketException)
        {
            return false;
        }
        catch (ObjectDisposedException)
        {
            return false;
        }
    }

    /// <summary>Determines whether the underlying socket connection is currently active.</summary>
    /// <remarks>This method performs a lightweight check to verify the connection status. If the connection
    /// appears active but has not been used for more than two minutes, an additional lightweight operation is performed
    /// to confirm connectivity. If the connection is found to be inactive and initialization is complete, the
    /// connection is automatically restarted. This method is intended for internal use to efficiently monitor
    /// connection health.</remarks>
    /// <returns>true if the connection is considered active; otherwise, false.</returns>
    private bool CheckConnectionStatusOptimized()
    {
        if (_socket is null)
        {
            return false;
        }

        try
        {
            var isConnected = _socket.Connected &&
                            !(_socket.Poll(SocketPollMicroseconds, SelectMode.SelectRead) && _socket.Available == 0);

            return isConnected &&
                _timeProvider.GetUtcNow().UtcDateTime - _lastSuccessfulOperation > TimeSpan.FromMinutes(ConnectionIdleCheckMinutes)
                ? PerformLightweightConnectionCheck()
                : isConnected;
        }
        catch (ObjectDisposedException)
        {
            return false;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>Performs a lightweight check of the underlying socket connection.</summary>
    /// <remarks>This method attempts to set a socket option as a way to verify the connection's health
    /// without sending data. It does not guarantee that the connection is fully operational, but can be used as a quick
    /// check before performing more expensive operations.</remarks>
    /// <returns>true if the connection check succeeds; otherwise, false.</returns>
    private bool PerformLightweightConnectionCheck()
    {
        try
        {
            _socket?.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.KeepAlive, true);
            return true;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>Represents the immutable details required to send an SZL request.</summary>
    /// <param name="Tag">The tag associated with the request.</param>
    /// <param name="FirstRequest">The first request packet.</param>
    /// <param name="ContinuationRequest">The continuation request packet.</param>
    /// <param name="SzlArea">The SZL area.</param>
    /// <param name="Index">The SZL index.</param>
    private readonly record struct SzlRequest(
        Tag Tag,
        byte[] FirstRequest,
        byte[] ContinuationRequest,
        ushort SzlArea,
        ushort Index);

    /// <summary>
    /// Represents a Transport Service Access Point (TSAP) addressing profile used for establishing communication with
    /// Siemens S7 devices.
    /// </summary>
    /// <remarks>TSAP profiles define how endpoints are addressed when connecting to Siemens S7 PLCs.
    /// Predefined profiles such as PG, OP, and PGAlt are provided for common communication scenarios. Use the
    /// appropriate profile based on the type of device or connection required.</remarks>
    /// <param name="SrcHi">The high byte of the source TSAP address.</param>
    /// <param name="SrcLo">The low byte of the source TSAP address.</param>
    /// <param name="DstHi">The high byte of the destination TSAP address.</param>
    /// <param name="DstLo">
    /// A function that computes the low byte of the destination TSAP address based on the rack and slot numbers.
    /// </param>
    /// <param name="Name">The name that identifies the TSAP profile.</param>
    private readonly record struct TsapProfile(
        byte SrcHi,
        byte SrcLo,
        byte DstHi,
        Func<short, short, byte> DstLo,
        string Name)
    {
        /// <summary>Defines the rack-address multiplier used to calculate the destination TSAP low byte.</summary>
        private const int RackAddressMultiplier = 2;

        /// <summary>Defines the number of slots represented by one rack-address unit.</summary>
        private const int SlotsPerRackAddressUnit = 16;

        /// <summary>Defines the source TSAP high byte for operator-panel connections.</summary>
        private const byte OperatorPanelSourceTsapHighByte = 0x02;

        /// <summary>Defines the alternate programming-device source TSAP high byte.</summary>
        private const byte AlternateProgrammingDeviceSourceTsapHighByte = 0x10;

        /// <summary>Gets the TSAP profile for the "PG" (Programming Device) connection type.</summary>
        /// <remarks>This profile is typically used when establishing a connection to a PLC as a
        /// programming device. The PG profile may have different access rights or communication behavior compared to
        /// other TSAP profiles, depending on the PLC configuration.</remarks>
        public static TsapProfile PG => new(
            ProgrammingDeviceSourceTsapHighByte,
            DefaultSourceTsapLowByte,
            DefaultDestinationTsapHighByte,
            (rack, slot) => (byte)((rack * RackAddressMultiplier * SlotsPerRackAddressUnit) + slot),
            nameof(PG));

        /// <summary>Gets the TSAP profile for the "OP" (Operator Panel) communication type.</summary>
        /// <remarks>Use this profile when establishing a connection that requires the Operator Panel TSAP
        /// settings. The profile includes predefined parameters suitable for typical OP communication
        /// scenarios.</remarks>
        public static TsapProfile OP => new(
            OperatorPanelSourceTsapHighByte,
            DefaultSourceTsapLowByte,
            DefaultDestinationTsapHighByte,
            (rack, slot) => (byte)((rack * RackAddressMultiplier * SlotsPerRackAddressUnit) + slot),
            nameof(OP));

        /// <summary>Gets the TSAP profile for the PGAlt (Programming Device Alternative) connection type.</summary>
        public static TsapProfile PGAlt => new(
            AlternateProgrammingDeviceSourceTsapHighByte,
            DefaultSourceTsapLowByte,
            DefaultDestinationTsapHighByte,
            (rack, slot) => (byte)((rack * RackAddressMultiplier * SlotsPerRackAddressUnit) + slot),
            nameof(PGAlt));
    }
}
