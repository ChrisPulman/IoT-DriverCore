// Copyright (c) 2019-2026 Chris Pulman and contributors. All rights reserved.
// Chris Pulman and contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
#if REACTIVE_SHIM
using OmronPlcRx.Reactive.Core.Responses;
using OmronPlcRx.Reactive.Core.Results;
#else
using OmronPlcRx.Core.Responses;
using OmronPlcRx.Core.Results;
#endif

#if REACTIVE_SHIM
namespace OmronPlcRx.Reactive.Core.Channels;
#else
namespace OmronPlcRx.Core.Channels;
#endif

/// <summary>Represents the u dp ch an ne l type.</summary>
internal sealed class UDPChannel : BaseChannel
{
    /// <summary>Stores the failed to receive FINS message prefix.</summary>
    private const string FailedToReceiveFinsMessagePrefix = "Failed to Receive FINS Message from Omron PLC '";

    /// <summary>Stores the failed to send FINS message prefix.</summary>
    private const string FailedToSendFinsMessagePrefix = "Failed to Send FINS Message to Omron PLC '";

    /// <summary>Stores the socket connection closed message suffix.</summary>
    private const string SocketConnectionHasBeenClosedMessageSuffix =
        "' - The underlying Socket Connection has been Closed";

    /// <summary>Stores the smallest valid FINS response length.</summary>
    private const int MinimumResponseLength =
        FINSResponse.HeaderLength + FINSResponse.CommandLength + FINSResponse.ResponseCodeLength;

    /// <summary>Stores the c li en t value.</summary>
    private UdpClient? _client;

    /// <summary>Initializes a new instance of the <see cref="UDPChannel"/> class.</summary>
    /// <param name="remoteHost">The r em ot eh os t value.</param>
    /// <param name="port">The p or t value.</param>
    internal UDPChannel(string remoteHost, int port)
        : base(remoteHost, port)
    {
    }

    public override void Dispose()
    {
        try
        {
            _client?.Dispose();
        }
        finally
        {
            _client = null;
            base.Dispose();
        }
    }

    internal override async Task InitializeAsync(int timeout, CancellationToken cancellationToken)
    {
        if (!Semaphore.Wait(0, cancellationToken))
        {
            await Semaphore.WaitAsync(cancellationToken);
        }

        try
        {
            DestroyClient();

            await InitializeClientAsync();
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

        try
        {
            await InitializeClientAsync();
        }
        catch (ObjectDisposedException)
        {
            throw new OmronPLCException(
                BuildEndpointMessage(
                    "Failed to Re-Connect to Omron PLC '",
                    SocketConnectionHasBeenClosedMessageSuffix));
        }
        catch (TimeoutException)
        {
            throw new OmronPLCException(
                BuildEndpointMessage("Failed to Re-Connect within the Timeout Period to Omron PLC '", string.Empty));
        }
        catch (System.Net.Sockets.SocketException e)
        {
            throw new OmronPLCException(BuildEndpointMessage("Failed to Re-Connect to Omron PLC '", string.Empty), e);
        }
    }

    protected override async Task<SendMessageResult> SendMessageAsync(
        ReadOnlyMemory<byte> message,
        int timeout,
        CancellationToken cancellationToken)
    {
        var bytes = 0;
        var packets = 0;
        var client =
            _client
            ?? throw new OmronPLCException(
                BuildEndpointMessage(FailedToSendFinsMessagePrefix, " - The UDP Client is not Initialized"));

        try
        {
            // OmronPlcRx.Sockets.UdpClient expects byte[] and int timeout
            bytes += await client.SendAsync(message.ToArray(), timeout, cancellationToken);
            packets++;
        }
        catch (ObjectDisposedException)
        {
            throw new OmronPLCException(
                BuildEndpointMessage(FailedToSendFinsMessagePrefix, SocketConnectionHasBeenClosedMessageSuffix));
        }
        catch (TimeoutException)
        {
            throw new OmronPLCException(
                BuildEndpointMessage(
                    "Failed to Send FINS Message within the Timeout Period to Omron PLC '",
                    string.Empty));
        }
        catch (System.Net.Sockets.SocketException e)
        {
            throw new OmronPLCException(BuildEndpointMessage(FailedToSendFinsMessagePrefix, string.Empty), e);
        }

        return new SendMessageResult
        {
            Bytes = bytes,
            Packets = packets,
        };
    }

    protected override async Task<ReceiveMessageResult> ReceiveMessageAsync(
        int timeout,
        CancellationToken cancellationToken)
    {
        var client =
            _client
            ?? throw new OmronPLCException(
                BuildEndpointMessage(FailedToReceiveFinsMessagePrefix, " - The UDP Client is not Initialized"));

        try
        {
            return await ReceivePayloadAsync(client, timeout, cancellationToken);
        }
        catch (ObjectDisposedException)
        {
            throw new OmronPLCException(
                BuildEndpointMessage(FailedToReceiveFinsMessagePrefix, SocketConnectionHasBeenClosedMessageSuffix));
        }
        catch (TimeoutException)
        {
            throw new OmronPLCException(
                BuildEndpointMessage(
                    "Failed to Receive FINS Message within the Timeout Period from Omron PLC '",
                    string.Empty));
        }
        catch (System.Net.Sockets.SocketException e)
        {
            throw new OmronPLCException(BuildEndpointMessage(FailedToReceiveFinsMessagePrefix, string.Empty), e);
        }
    }

    protected override async Task PurgeReceiveBufferAsync(
        int timeout,
        CancellationToken cancellationToken)
    {
        var client = _client;
        if (client is null)
        {
            return;
        }

        try
        {
            if (client.Available == 0)
            {
                await Task.Delay(timeout / ProtocolConstants.Four, cancellationToken);
            }

            var startTimestamp = TimeProvider.GetUtcNow().UtcDateTime;
            var buffer = new byte[2000];

            while (client.Available > 0 && TimeProvider.GetUtcNow().UtcDateTime.Subtract(startTimestamp).TotalMilliseconds < timeout)
            {
                try
                {
                    await client.ReceiveAsync(buffer, timeout, cancellationToken);
                }
                catch (TimeoutException)
                {
                    return;
                }
                catch (ObjectDisposedException)
                {
                    return;
                }
                catch (System.Net.Sockets.SocketException)
                {
                    return;
                }
            }
        }
        catch (TimeoutException)
        {
        }
        catch (ObjectDisposedException)
        {
        }
        catch (System.Net.Sockets.SocketException)
        {
        }
    }

    /// <summary>Creates a PLC endpoint-specific error message.</summary>
    /// <param name="prefix">The message prefix, including an opening apostrophe.</param>
    /// <param name="detail">The message detail, beginning after the endpoint.</param>
    /// <returns>The completed error message.</returns>
    private string BuildEndpointMessage(string prefix, string detail) => $"{prefix}{RemoteHost}:{Port}'{detail}";

    /// <summary>Receives and validates one UDP FINS payload.</summary>
    /// <param name="client">Initialized UDP client.</param>
    /// <param name="timeout">Receive timeout in milliseconds.</param>
    /// <param name="cancellationToken">Token used to cancel the receive.</param>
    /// <returns>The received message and transport metrics.</returns>
    private async Task<ReceiveMessageResult> ReceivePayloadAsync(
        UdpClient client,
        int timeout,
        CancellationToken cancellationToken)
    {
        var bytes = 0;
        var packets = 0;
        var receivedData = new List<byte>();
        var startTimestamp = TimeProvider.GetUtcNow().UtcDateTime;
        while (
            TimeProvider.GetUtcNow().UtcDateTime.Subtract(startTimestamp).TotalMilliseconds < timeout
            && receivedData.Count < MinimumResponseLength)
        {
            var buffer = new byte[4096];
            var remainingMs = (int)Math.Max(
                0,
                timeout - (TimeProvider.GetUtcNow().UtcDateTime - startTimestamp).TotalMilliseconds);
            if (remainingMs < ProtocolConstants.Fifty)
            {
                continue;
            }

            var receivedBytes = await client.ReceiveAsync(
                buffer,
                remainingMs,
                cancellationToken);
            if (receivedBytes <= 0)
            {
                continue;
            }

            receivedData.AddRange(buffer.AsSpan(0, receivedBytes).ToArray());
            bytes += receivedBytes;
            packets++;
        }

        ValidateReceivedData(receivedData);
        return new ReceiveMessageResult
        {
            Bytes = bytes,
            Packets = packets,
            Message = receivedData.ToArray(),
        };
    }

    /// <summary>Validates a received UDP FINS payload.</summary>
    /// <param name="receivedData">Received payload bytes.</param>
    private void ValidateReceivedData(List<byte> receivedData)
    {
        if (receivedData.Count == 0)
        {
            throw new OmronPLCException(
                BuildEndpointMessage(FailedToReceiveFinsMessagePrefix, " - No Data was Received"));
        }

        if (receivedData.Count < MinimumResponseLength)
        {
            throw new OmronPLCException(
                BuildEndpointMessage(
                    "Failed to Receive FINS Message within the Timeout Period from Omron PLC '",
                    string.Empty));
        }

        if (receivedData[0] is 0xC0 or 0xC1)
        {
            return;
        }

        throw new OmronPLCException(
            BuildEndpointMessage(FailedToReceiveFinsMessagePrefix, " - The FINS Header was Invalid"));
    }

    /// <summary>Initializes a new instance of the <see cref="InitializeClientAsync"/> class.</summary>
    /// <returns>A task that represents the asynchronous operation.</returns>
    private Task InitializeClientAsync()
    {
        _client = new(RemoteHost, Port);

        return Task.CompletedTask;
    }

    /// <summary>Initializes a new instance of the <see cref="DestroyClient"/> class.</summary>
    private void DestroyClient()
    {
        try
        {
            _client?.Dispose();
        }
        finally
        {
            _client = null;
        }
    }
}
