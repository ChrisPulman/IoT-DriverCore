// Copyright (c) 2019-2026 Chris Pulman and contributors. All rights reserved.
// Chris Pulman and contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
#if REACTIVE_SHIM
using IoT.DriverCore.OmronPlcRx.Reactive.Core.Results;
#else
using IoT.DriverCore.OmronPlcRx.Core.Results;
#endif

#if REACTIVE_SHIM
namespace IoT.DriverCore.OmronPlcRx.Reactive.Core.Channels;
#else
namespace IoT.DriverCore.OmronPlcRx.Core.Channels;
#endif

/// <summary>Represents the s er ia lh os tl in kf in sc ha nn el type.</summary>
internal sealed class SerialHostLinkFinsChannel : BaseChannel
{
    /// <summary>Stores the o pt io ns value.</summary>
    private readonly OmronSerialOptions _options;

    /// <summary>Creates serial-port instances for channel initialization and reconnection.</summary>
    private readonly Func<int, IOmronSerialPort> _portFactory;

    /// <summary>Executes the r ec ei ve db yt es operation.</summary>
    private readonly ConcurrentQueue<byte> _receivedBytes = new();

    /// <summary>Stores the p or t value.</summary>
    private IOmronSerialPort? _port;

    /// <summary>Stores the h os tl in kc od ec value.</summary>
    private HostLinkFinsFrameCodec? _hostLinkCodec;

    /// <summary>Initializes a new instance of the <see cref="SerialHostLinkFinsChannel"/> class.</summary>
    /// <param name="options">The o pt io ns value.</param>
    internal SerialHostLinkFinsChannel(OmronSerialOptions options)
        : this(options, timeout => new OmronSerialPortAdapter(options, timeout))
    {
    }

    /// <summary>Initializes a new instance of the <see cref="SerialHostLinkFinsChannel"/> class.</summary>
    /// <param name="options">Serial connection options.</param>
    /// <param name="portFactory">Factory that composes the serial transport used by the channel.</param>
    internal SerialHostLinkFinsChannel(
        OmronSerialOptions options,
        Func<int, IOmronSerialPort> portFactory)
        : base(options?.PortName ?? throw new ArgumentNullException(nameof(options)), 0)
    {
        _options = options;
        _portFactory = portFactory ?? throw new ArgumentNullException(nameof(portFactory));
        _options.Validate();
    }

    public override void Dispose()
    {
        try
        {
            _port?.Close();
            _port?.Dispose();
        }
        finally
        {
            _port = null;
            base.Dispose();
        }
    }

    /// <summary>Creates a serial request-length error message.</summary>
    /// <param name="length">The request frame length.</param>
    /// <param name="maximum">The configured maximum frame length.</param>
    /// <returns>The formatted error message.</returns>
    internal static string CreateRequestLengthMessage(int length, int maximum) =>
        $"The serial FINS request length {length} exceeds the configured maximum frame length {maximum}.";

    /// <summary>Creates a serial response-length error message.</summary>
    /// <param name="protocol">The serial protocol description.</param>
    /// <param name="maximum">The configured maximum frame length.</param>
    /// <returns>The formatted error message.</returns>
    internal static string CreateFrameLengthMessage(string protocol, int maximum) =>
        $"The serial {protocol} exceeded the configured maximum frame length {maximum}.";

    /// <summary>Creates a serial no-data error message.</summary>
    /// <param name="protocol">The serial protocol description.</param>
    /// <param name="host">The remote serial port name.</param>
    /// <returns>The formatted error message.</returns>
    internal static string CreateNoDataMessage(string protocol, string host) =>
        $"Failed to Receive {protocol} FINS Message from Omron PLC serial port '{host}' - No Data was Received";

    /// <summary>Creates a serial timeout error message.</summary>
    /// <param name="protocol">The serial protocol description.</param>
    /// <param name="host">The remote serial port name.</param>
    /// <returns>The formatted error message.</returns>
    internal static string CreateTimeoutMessage(string protocol, string host) =>
        $"Failed to Receive {protocol} FINS Message within the Timeout Period from Omron PLC serial port '{host}'";

    internal override async Task InitializeAsync(int timeout, CancellationToken cancellationToken)
    {
        if (!Semaphore.Wait(0, cancellationToken))
        {
            await Semaphore.WaitAsync(cancellationToken).ConfigureAwait(false);
        }

        try
        {
            DestroyClient();
            await InitializeClientAsync(timeout, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _ = Semaphore.Release();
        }
    }

    protected override async Task DestroyAndInitializeClientAsync(
        int timeout,
        CancellationToken cancellationToken)
    {
        DestroyClient();
        await InitializeClientAsync(timeout, cancellationToken).ConfigureAwait(false);
    }

    protected override Task<SendMessageResult> SendMessageAsync(
        ReadOnlyMemory<byte> message,
        int timeout,
        CancellationToken cancellationToken)
    {
        if (_port is null)
        {
            throw new OmronPLCException($"The serial channel for '{RemoteHost}' is not initialized.");
        }

        ClearReceiveQueue();
        _port.DiscardInBuffer();
        var bytes = _options.Protocol == OmronSerialProtocol.Toolbus
            ? ToolbusFinsFrameCodec.EncodeRequest(message).ToArray()
            : Encoding.ASCII.GetBytes(GetHostLinkCodec().EncodeRequest(message));

        if (bytes.Length > _options.MaximumFrameLength)
        {
            throw new OmronPLCException(CreateRequestLengthMessage(bytes.Length, _options.MaximumFrameLength));
        }

        _port.Write(bytes, 0, bytes.Length);
        return Task.FromResult(new SendMessageResult
        {
            Bytes = bytes.Length,
            Packets = 1,
        });
    }

    protected override Task<ReceiveMessageResult> ReceiveMessageAsync(
        int timeout,
        CancellationToken cancellationToken) =>
        _options.Protocol == OmronSerialProtocol.Toolbus
            ? ReceiveToolbusMessageAsync(timeout, cancellationToken)
            : ReceiveHostLinkMessageAsync(timeout, cancellationToken);

    protected override Task PurgeReceiveBufferAsync(
        int timeout,
        CancellationToken cancellationToken)
    {
        ClearReceiveQueue();
        _port?.DiscardInBuffer();
        return Task.CompletedTask;
    }

    /// <summary>Initializes a new instance of the <see cref="ReceiveHostLinkMessageAsync"/> class.</summary>
    /// <param name="timeout">The t im eo ut value.</param>
    /// <param name="cancellationToken">The c an ce ll at io nt ok en value.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    private async Task<ReceiveMessageResult> ReceiveHostLinkMessageAsync(
        int timeout,
        CancellationToken cancellationToken)
    {
        var received = new List<byte>();
        var startTimestamp = TimeProvider.GetUtcNow();
        while (TimeProvider.GetUtcNow().Subtract(startTimestamp).TotalMilliseconds < timeout)
        {
            while (_receivedBytes.TryDequeue(out var value))
            {
                received.Add(value);
                if (received.Count > _options.MaximumFrameLength)
                {
                    throw new OmronPLCException(CreateFrameLengthMessage(
                        "Host Link FINS response",
                        _options.MaximumFrameLength));
                }

                if (
                    received.Count >= ProtocolConstants.Two
                    && received[received.Count - ProtocolConstants.Two] == (byte)'*'
                    && received[received.Count - 1] == 0x0D)
                {
                    var frame = Encoding.ASCII.GetString(received.ToArray());
                    return new ReceiveMessageResult
                    {
                        Bytes = received.Count,
                        Packets = 1,
                        Message = GetHostLinkCodec().DecodeResponse(frame),
                    };
                }
            }

            if (!await WaitForSerialDataAsync(startTimestamp, timeout, cancellationToken).ConfigureAwait(false))
            {
                break;
            }
        }

        if (received.Count == 0)
        {
            throw new OmronPLCException(CreateNoDataMessage("Host Link", RemoteHost));
        }

        throw new OmronPLCException(CreateTimeoutMessage("Host Link", RemoteHost));
    }

    /// <summary>Initializes a new instance of the <see cref="ReceiveToolbusMessageAsync"/> class.</summary>
    /// <param name="timeout">The t im eo ut value.</param>
    /// <param name="cancellationToken">The c an ce ll at io nt ok en value.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    private async Task<ReceiveMessageResult> ReceiveToolbusMessageAsync(
        int timeout,
        CancellationToken cancellationToken)
    {
        var received = new List<byte>();
        var startTimestamp = TimeProvider.GetUtcNow();
        while (TimeProvider.GetUtcNow().Subtract(startTimestamp).TotalMilliseconds < timeout)
        {
            while (_receivedBytes.TryDequeue(out var value))
            {
                if (TryCreateToolbusReceiveResult(received, value, out var result))
                {
                    return result;
                }
            }

            if (!await WaitForSerialDataAsync(startTimestamp, timeout, cancellationToken).ConfigureAwait(false))
            {
                break;
            }
        }

        if (received.Count == 0)
        {
            throw new OmronPLCException(CreateNoDataMessage("Toolbus", RemoteHost));
        }

        throw new OmronPLCException(CreateTimeoutMessage("Toolbus", RemoteHost));
    }

    /// <summary>Initializes a new instance of the <see cref="InitializeClientAsync"/> class.</summary>
    /// <param name="timeout">The t im eo ut value.</param>
    /// <param name="cancellationToken">The c an ce ll at io nt ok en value.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    private async Task InitializeClientAsync(int timeout, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        _hostLinkCodec =
            _options.Protocol == OmronSerialProtocol.HostLinkFins
                ? new HostLinkFinsFrameCodec(_options)
                : null;
        _port = _portFactory(timeout);

        await _port.OpenAsync().ConfigureAwait(false);
        _port.RtsEnable = _options.RtsEnable;
        _port.DtrEnable = _options.DtrEnable;
        if (_options.Protocol != OmronSerialProtocol.Toolbus)
        {
            return;
        }

        await SynchronizeToolbusAsync(timeout, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Initializes a new instance of the <see cref="SynchronizeToolbusAsync"/> class.</summary>
    /// <param name="timeout">The t im eo ut value.</param>
    /// <param name="cancellationToken">The c an ce ll at io nt ok en value.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    private async Task SynchronizeToolbusAsync(int timeout, CancellationToken cancellationToken)
    {
        if (_port is null)
        {
            throw new OmronPLCException($"The Toolbus serial channel for '{RemoteHost}' is not initialized.");
        }

        ClearReceiveQueue();
        _port.DiscardInBuffer();
        var sync = ToolbusFinsFrameCodec.SynchronizationFrame.ToArray();
        var received = new List<byte>();
        var startTimestamp = TimeProvider.GetUtcNow();
        var nextSync = DateTimeOffset.MinValue;
        while (TimeProvider.GetUtcNow().Subtract(startTimestamp).TotalMilliseconds < timeout)
        {
            WriteSynchronizationFrameIfDue(sync, ref nextSync);

            if (TryReadSynchronizationFrame(received, sync))
            {
                ClearReceiveQueue();
                return;
            }

            await WaitForSerialDataAsync(startTimestamp, timeout, cancellationToken).ConfigureAwait(false);
        }

        throw new OmronPLCException(
            $"Failed to synchronize Toolbus serial port '{RemoteHost}' within the timeout period.");
    }

    /// <summary>Initializes a new instance of the <see cref="WaitForSerialDataAsync"/> class.</summary>
    /// <param name="startTimestamp">The s ta rt ti me st am p value.</param>
    /// <param name="timeout">The t im eo ut value.</param>
    /// <param name="cancellationToken">The c an ce ll at io nt ok en value.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    private async Task<bool> WaitForSerialDataAsync(
        DateTimeOffset startTimestamp,
        int timeout,
        CancellationToken cancellationToken)
    {
        if (PumpReceiveBuffer() > 0)
        {
            return true;
        }

        var remaining = timeout - (int)TimeProvider.GetUtcNow().Subtract(startTimestamp).TotalMilliseconds;
        if (remaining <= 0)
        {
            return false;
        }

        await Task.Delay(Math.Min(remaining, ProtocolConstants.Twenty), cancellationToken).ConfigureAwait(false);
        _ = PumpReceiveBuffer();

        return true;
    }

    /// <summary>Initializes a new instance of the <see cref="PumpReceiveBuffer"/> class.</summary>
    /// <returns>The result produced by the operation.</returns>
    private int PumpReceiveBuffer()
    {
        if (_port is null)
        {
            return 0;
        }

        var totalRead = 0;
        while (_port.BytesToRead > 0)
        {
            var buffer = new byte[_port.BytesToRead];
            var read = _port.Read(buffer, 0, buffer.Length);
            for (var i = 0; i < read; i++)
            {
                _receivedBytes.Enqueue(buffer[i]);
            }

            totalRead += read;
        }

        return totalRead;
    }

    /// <summary>Attempts to decode a Toolbus response after receiving one byte.</summary>
    /// <param name="received">The accumulated received bytes.</param>
    /// <param name="value">The received byte.</param>
    /// <param name="result">The decoded receive result.</param>
    /// <returns>A value indicating whether a complete frame is available.</returns>
    private bool TryCreateToolbusReceiveResult(List<byte> received, byte value, out ReceiveMessageResult result)
    {
        received.Add(value);
        SerialToolbusFrameBuffer.TrimBeforeFrameStart(received);
        if (received.Count == 0)
        {
            result = default!;
            return false;
        }

        return TryDecodeCompleteToolbusFrame(received, out result);
    }

    /// <summary>Attempts to decode a complete Toolbus frame.</summary>
    /// <param name="received">The accumulated received bytes.</param>
    /// <param name="result">The decoded receive result.</param>
    /// <returns>A value indicating whether a complete frame is available.</returns>
    private bool TryDecodeCompleteToolbusFrame(List<byte> received, out ReceiveMessageResult result)
    {
        if (received.Count < 3)
        {
            result = default!;
            return false;
        }

        var declaredLength = (received[1] << 8) | received[2];
        var totalLength = declaredLength + ProtocolConstants.Three;
        if (declaredLength < ProtocolConstants.Two || totalLength > _options.MaximumFrameLength)
        {
            throw new OmronPLCException(
                $"The serial Toolbus FINS response declared invalid frame length {declaredLength}.");
        }

        if (received.Count < totalLength)
        {
            result = default!;
            return false;
        }

        var frame = received.GetRange(0, totalLength).ToArray();
        result = new ReceiveMessageResult
        {
            Bytes = totalLength,
            Packets = 1,
            Message = ToolbusFinsFrameCodec.DecodeResponse(frame),
        };
        return true;
    }

    /// <summary>Writes a Toolbus synchronization frame when the cadence allows it.</summary>
    /// <param name="sync">The synchronization frame.</param>
    /// <param name="nextSync">The next permitted send time.</param>
    private void WriteSynchronizationFrameIfDue(byte[] sync, ref DateTimeOffset nextSync)
    {
        if (TimeProvider.GetUtcNow() < nextSync)
        {
            return;
        }

        _port?.Write(sync, 0, sync.Length);
        nextSync = TimeProvider.GetUtcNow().AddMilliseconds(ProtocolConstants.TwoHundredFifty);
    }

    /// <summary>Reads queued bytes until a Toolbus synchronization frame is found.</summary>
    /// <param name="received">The accumulated received bytes.</param>
    /// <param name="sync">The synchronization frame.</param>
    /// <returns>A value indicating whether the synchronization frame was received.</returns>
    private bool TryReadSynchronizationFrame(List<byte> received, byte[] sync)
    {
        while (_receivedBytes.TryDequeue(out var value))
        {
            received.Add(value);
            TrimSynchronizationBuffer(received);
            if (SerialToolbusFrameBuffer.ContainsSynchronizationFrame(received, sync))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Trims the Toolbus synchronization buffer to the configured maximum frame length.</summary>
    /// <param name="received">The accumulated received bytes.</param>
    private void TrimSynchronizationBuffer(List<byte> received)
    {
        if (received.Count <= _options.MaximumFrameLength)
        {
            return;
        }

        received.RemoveAt(0);
    }

    /// <summary>Initializes a new instance of the <see cref="GetHostLinkCodec"/> class.</summary>
    /// <returns>The result produced by the operation.</returns>
    private HostLinkFinsFrameCodec GetHostLinkCodec() =>
        _hostLinkCodec
        ?? throw new OmronPLCException(
            $"The serial Host Link FINS codec for '{RemoteHost}' is not initialized.");

    /// <summary>Initializes a new instance of the <see cref="DestroyClient"/> class.</summary>
    private void DestroyClient()
    {
        try
        {
            _port?.Close();
            _port?.Dispose();
        }
        finally
        {
            _port = null;
            ClearReceiveQueue();
        }
    }

    /// <summary>Initializes a new instance of the <see cref="ClearReceiveQueue"/> class.</summary>
    private void ClearReceiveQueue()
    {
        while (_receivedBytes.TryDequeue(out var discarded))
        {
            _ = discarded;
        }
    }
}
