// Copyright (c) 2019-2026 Chris Pulman and contributors. All rights reserved.
// Chris Pulman and contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using System.Net.Sockets;
using System.Runtime.CompilerServices;
#if REACTIVE_SHIM
using S7PlcRx.Reactive.Enums;
using S7PlcRx.Reactive.PlcTypes;
#else
using S7PlcRx.Enums;
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
    /// <summary>Retrieves System-Zustandsliste (SZL) data from the PLC for the specified SZL area and index.</summary>
    /// <remarks>This method performs a low-level SZL read operation using enhanced communication protocols.
    /// The returned data format depends on the specified SZL area and index. If the requested SZL area or index is not
    /// available, the returned data will be empty. This method is intended for advanced scenarios where direct access
    /// to PLC system information is required.</remarks>
    /// <param name="szlArea">The SZL area code that identifies the type of system information to retrieve.</param>
    /// <param name="index">The index within the SZL area to access. The default is 0.</param>
    /// <returns>A tuple containing a byte array with the SZL data and a ushort indicating the size of the data. Returns
    /// an empty
    /// array and zero size if the operation fails.</returns>
    internal (byte[] Data, ushort Size) GetSZLData(ushort szlArea, ushort index = 0)
    {
        var (s7Szl1, s7Szl2) = CreateSzlRequestPackets();

        var data = _bufferPool.Rent(SzlBufferSize);
        var resultData = _bufferPool.Rent(SzlBufferSize);

        try
        {
            var tag = new Tag();
            int length;
            var done = false;
            var first = true;
            byte seqIn = 0;
            ushort seqOut = 0;
            var lastError = 0;
            var offset = 0;
            ushort lengthOfDataRead = 0;
            var requestData = new SzlRequest(tag, s7Szl1, s7Szl2, szlArea, index);
            do
            {
                SendSzlRequest(requestData, first, seqIn, ref seqOut);

                length = ReceiveIsoData(tag, ref data);

                if (length > MinimumSzlResponseLength)
                {
                    lastError = ProcessSzlResponse(
                        data,
                        resultData,
                        first,
                        ref done,
                        ref seqIn,
                        ref lengthOfDataRead,
                        ref offset);
                    if (lastError == 0)
                    {
                        first = false;
                    }
                }
                else
                {
                    lastError = (int)ErrorCode.WrongNumberReceivedBytes;
                }
            }
            while (!done && lastError == 0);

            if (lastError == 0)
            {
                var result = new byte[offset];
                Array.Copy(resultData, result, offset);
                return (result, lengthOfDataRead);
            }

            return ([], 0);
        }
        finally
        {
            _bufferPool.Return(data);
            _bufferPool.Return(resultData);
        }
    }

    /// <summary>Receives ISO protocol data for a tag into a byte array.</summary>
    /// <remarks>The method expects the incoming data to conform to the ISO protocol format. If the received
    /// data does not meet protocol requirements, the method returns 0 to indicate failure.</remarks>
    /// <param name="tag">The tag representing the communication endpoint from which to receive ISO data.</param>
    /// <param name="bytes">A reference to the byte array that receives the data. The array must be large enough
    /// to hold the received
    /// ISO
    /// data.</param>
    /// <returns>
    /// The total number of bytes received if the operation is successful; otherwise, 0 if an error occurs.
    /// </returns>
    internal int ReceiveIsoData(Tag tag, ref byte[] bytes)
    {
        var size = 0;
        var done = false;

        while (!done)
        {
            if (!TryReceiveIsoHeader(tag, bytes, out size, out done))
            {
                return 0;
            }
        }

        // Get PDU Type
        if (ReceiveExact(tag, bytes, CotpDataHeaderLength, TpktHeaderLength) != CotpDataHeaderLength)
        {
            return 0;
        }

        // Receive S7 ISO Payload
        return ReceiveExact(tag, bytes, size - IsoDataHeaderLength, IsoDataHeaderLength) ==
            size - IsoDataHeaderLength ? size : 0;
    }

    /// <summary>Releases unmanaged and - optionally - managed resources.</summary>
    /// <param name="disposing">
    /// <c>true</c> to release both managed and unmanaged resources; <c>false</c> to release
    /// only unmanaged resources.
    /// </param>
    protected virtual void Dispose(bool disposing)
    {
        if (_disposedValue)
        {
            return;
        }

        if (disposing)
        {
            // Stop connection/retry loops before disposing subjects
            try
            {
                _disposable?.Dispose();
            }
            catch (Exception ex)
            {
                LogError($"Subscription disposal failed: {ex.Message}", _timeProvider);
            }

            _metricsTimer?.Dispose();
            CloseSocketOptimized(_socket, _timeProvider);
            _socket = null;

            try
            {
                _socketExceptionSubject?.Dispose();
            }
            catch (Exception ex)
            {
                LogError($"Socket exception subject disposal failed: {ex.Message}", _timeProvider);
            }

            _metricsSubject?.Dispose();
            _connectionLock?.Dispose();
        }

        _disposedValue = true;
    }

    /// <summary>Logs an error message to the debug output when a debugger is attached.</summary>
    /// <param name="message">The error message to log.</param>
    /// <param name="timeProvider">The time provider.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void LogError(string message, TimeProvider timeProvider)
    {
        if (!Debugger.IsAttached)
        {
            return;
        }

        Debug.WriteLine($"[ERROR] {timeProvider.GetUtcNow().LocalDateTime:yyyy'-'MM'-'dd HH':'mm':'ss.fff} {message}");
    }

    /// <summary>Writes an informational message to the debug output when a debugger is attached.</summary>
    /// <param name="message">The informational message to write to the debug output.</param>
    /// <param name="timeProvider">The time provider.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void LogInfo(string message, TimeProvider timeProvider)
    {
        if (!Debugger.IsAttached)
        {
            return;
        }

        Debug.WriteLine($"[INFO] {timeProvider.GetUtcNow().LocalDateTime:yyyy'-'MM'-'dd HH':'mm':'ss.fff} {message}");
    }

    /// <summary>Writes a warning message to the debug output when a debugger is attached.</summary>
    /// <param name="message">The warning message to write to the debug output.</param>
    /// <param name="timeProvider">The time provider.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void LogWarning(string message, TimeProvider timeProvider)
    {
        if (!Debugger.IsAttached)
        {
            return;
        }

        Debug.WriteLine($"[WARN] {timeProvider.GetUtcNow().LocalDateTime:yyyy'-'MM'-'dd HH':'mm':'ss.fff} {message}");
    }

    /// <summary>Processes a received SZL response packet.</summary>
    /// <param name="data">The response buffer.</param>
    /// <param name="resultData">The accumulated result buffer.</param>
    /// <param name="first">A value indicating whether this is the first packet.</param>
    /// <param name="done">A value indicating whether the response is complete.</param>
    /// <param name="sequenceIn">The incoming sequence value.</param>
    /// <param name="lengthOfDataRead">The accumulated data length.</param>
    /// <param name="offset">The accumulated result offset.</param>
    /// <returns>Zero when the packet is valid; otherwise, an S7 error code.</returns>
    private static int ProcessSzlResponse(
        byte[] data,
        byte[] resultData,
        bool first,
        ref bool done,
        ref byte sequenceIn,
        ref ushort lengthOfDataRead,
        ref int offset)
    {
        if (Word.FromByteArray(data, SzlErrorCodeOffset) != 0 || data[SzlReturnCodeOffset] != S7ReturnCodeSuccess)
        {
            return (int)ErrorCode.WrongVarFormat;
        }

        var sourceOffset = first ? SzlFirstPayloadOffset : SzlContinuationPayloadOffset;
        var szlDataLength = first
            ? Word.FromByteArray(data, SzlDataLengthOffset) - SzlFirstPacketMetadataLength
            : Word.FromByteArray(data, SzlDataLengthOffset);
        done = data[SzlLastDataUnitOffset] == 0;
        sequenceIn = data[SzlSequenceOffset];
        Array.Copy(data, sourceOffset, resultData, offset, szlDataLength);
        offset += szlDataLength;
        lengthOfDataRead = first
            ? Word.FromByteArray(data, SzlTotalLengthOffset)
            : (ushort)(lengthOfDataRead + szlDataLength);

        return 0;
    }

    /// <summary>Checks whether the socket is still connected.</summary>
    /// <param name="socket">The socket to check.</param>
    /// <returns>
    /// <see langword="true"/> when the socket appears connected; otherwise, <see langword="false"/>.
    /// </returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool CheckConnectionStatusOptimized(Socket socket)
    {
        try
        {
            return socket.Connected &&
                !(socket.Poll(SocketPollMicroseconds, SelectMode.SelectRead) && socket.Available == 0);
        }
        catch (Exception ex) when (ex is SocketException or ObjectDisposedException)
        {
            return false;
        }
    }

    /// <summary>Determines the optimal data read length, in bytes, for the specified PLC type.</summary>
    /// <param name="plcType">The type of PLC for which to determine the optimal data read length.</param>
    /// <returns>The recommended number of bytes to read in a single operation for the specified PLC type.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static ushort GetOptimalDataReadLength(CpuType plcType) => plcType switch
    {
        CpuType.Logo0BA8 => LogoPduLength,
        CpuType.S7200 or CpuType.S7300 => StandardPduLength,
        CpuType.S7400 or CpuType.S71200 => ExtendedPduLength,
        CpuType.S71500 => HighPerformancePduLength,
        _ => StandardPduLength
    };

    /// <summary>Closes and disposes the specified socket.</summary>
    /// <param name="socket">The socket to close and dispose.</param>
    /// <param name="timeProvider">The time provider.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void CloseSocketOptimized(Socket? socket, TimeProvider timeProvider)
    {
        if (socket is null)
        {
            return;
        }

        try
        {
            if (socket.Connected)
            {
                try
                {
                    socket.Shutdown(SocketShutdown.Both);
                }
                catch (Exception ex)
                {
                    LogWarning($"Socket shutdown failed: {ex.Message}", timeProvider);
                }
            }
        }
        catch (Exception ex)
        {
            LogWarning($"Socket connection state check failed during close: {ex.Message}", timeProvider);
        }
        finally
        {
            try
            {
                socket.Close();
            }
            catch (Exception ex)
            {
                LogWarning($"Socket close failed: {ex.Message}", timeProvider);
            }

            try
            {
                socket.Dispose();
            }
            catch (Exception ex)
            {
                LogWarning($"Socket dispose failed: {ex.Message}", timeProvider);
            }
        }
    }

#if !NETFRAMEWORK
    /// <summary>Asynchronously receives a complete TPKT packet into the specified buffer.</summary>
    /// <param name="socket">The connected socket to read from.</param>
    /// <param name="buffer">The buffer that receives the TPKT packet data.</param>
    /// <param name="expectedMin">The minimum expected length of the TPKT packet, in bytes.</param>
    /// <returns>
    /// The total number of bytes read into the buffer, or a value less than 4 if the TPKT header could not be read.
    /// </returns>
    private async Task<int> ReceiveTpktExactModernAsync(Socket socket, byte[] buffer, int expectedMin)
    {
        var headerRead = await ReceiveExactAsync(socket, buffer, TpktHeaderLength, 0).ConfigureAwait(false);
        if (headerRead != TpktHeaderLength)
        {
            return headerRead;
        }

        var length = (buffer[TpktLengthHighByteOffset] << BitsPerByte) | buffer[TpktLengthLowByteOffset];
        if (length < TpktHeaderLength || length > buffer.Length)
        {
            LogWarning($"Invalid TPKT length {length} for receive buffer {buffer.Length}", _timeProvider);
            return 0;
        }

        if (length < expectedMin && expectedMin > 0)
        {
            LogWarning($"TPKT length {length} smaller than expected {expectedMin}", _timeProvider);
        }

        var remaining = length - TpktHeaderLength;
        if (remaining == 0)
        {
            return headerRead;
        }

        var bodyRead = await ReceiveExactAsync(socket, buffer, remaining, TpktHeaderLength).ConfigureAwait(false);
        return bodyRead <= 0 ? headerRead : headerRead + bodyRead;

        static async Task<int> ReceiveExactAsync(Socket socket, byte[] buffer, int size, int offset)
        {
            var total = 0;
            while (total < size)
            {
                var received = await socket.ReceiveAsync(
                    buffer.AsMemory(offset + total, size - total),
                    SocketFlags.None).ConfigureAwait(false);
                if (received <= 0)
                {
                    break;
                }

                total += received;
            }

            return total;
        }
    }
#endif

    /// <summary>Creates the initial and continuation SZL request packets.</summary>
    /// <returns>The initial and continuation packets.</returns>
    private (byte[] First, byte[] Continuation) CreateSzlRequestPackets() =>
    (
        [
            TpktVersion, 0, 0, SzlTelegramLength,
            CotpDataHeaderSize, CotpDataPduType, CotpEndOfTransmissionUnit,
            S7ProtocolIdentifier, S7UserDataMessageType, 0, 0, SzlFirstPduReference,
            0, 0, SzlFirstParameterLength, 0, SzlFirstDataLength,
            0, 1, S7UserDataParameterHead, SzlFirstParameterPayloadLength, SzlReadRequest,
            SzlFunctionGroup, SzlSubfunction, 0, S7ReturnCodeSuccess, S7OctetStringTransportSize,
            0, SzlRequestDataLength, 0, 0, 0, 0
        ],
        [
            TpktVersion, 0, 0, SzlTelegramLength,
            CotpDataHeaderSize, CotpDataPduType, CotpEndOfTransmissionUnit,
            S7ProtocolIdentifier, S7UserDataMessageType, 0, 0, SzlContinuationPduReference,
            0, 0, SzlContinuationParameterLength, 0, SzlContinuationDataLength,
            0, 1, S7UserDataParameterHead, SzlContinuationParameterPayloadLength, SzlReadResponse,
            SzlFunctionGroup, SzlSubfunction, SzlContinuationFlag, 0, 0, 0, 0,
            SzlContinuationDataBitLength, 0, 0, 0
        ]);

    /// <summary>Receives and validates an ISO packet header.</summary>
    /// <param name="tag">The related PLC tag.</param>
    /// <param name="bytes">The receive buffer.</param>
    /// <param name="size">The parsed packet size.</param>
    /// <param name="done">A value indicating whether the payload header has been reached.</param>
    /// <returns>true when the header is valid; otherwise, false.</returns>
    private bool TryReceiveIsoHeader(Tag tag, byte[] bytes, out int size, out bool done)
    {
        done = false;
        size = 0;
        if (ReceiveExact(tag, bytes, TpktHeaderLength) != TpktHeaderLength)
        {
            return false;
        }

        size = Word.FromByteArray(bytes, TpktLengthHighByteOffset);
        if (size == IsoDataHeaderLength)
        {
            return ReceiveExact(tag, bytes, CotpDataHeaderLength, TpktHeaderLength) == CotpDataHeaderLength;
        }

        if (size > DataReadLength + IsoDataHeaderLength || size < MinimumIsoPacketLength)
        {
            return false;
        }

        done = true;
        return true;
    }
}
