// Copyright (c) 2019-2026 Chris Pulman and contributors. All rights reserved.
// Chris Pulman and contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System;
using System.Threading;
using System.Threading.Tasks;
#if REACTIVE_SHIM
using IoT.DriverCore.OmronPlcRx.Reactive.Core.Requests;
using IoT.DriverCore.OmronPlcRx.Reactive.Core.Responses;
using IoT.DriverCore.OmronPlcRx.Reactive.Core.Results;
#else
using IoT.DriverCore.OmronPlcRx.Core.Requests;
using IoT.DriverCore.OmronPlcRx.Core.Responses;
using IoT.DriverCore.OmronPlcRx.Core.Results;
#endif

#if REACTIVE_SHIM
namespace IoT.DriverCore.OmronPlcRx.Reactive.Core.Channels;
#else
namespace IoT.DriverCore.OmronPlcRx.Core.Channels;
#endif

/// <summary>Represents the b as ec ha nn el type.</summary>
internal abstract class BaseChannel : IDisposable
{
    /// <summary>Stores the r eq ue st id value.</summary>
    private byte _requestId;

    /// <summary>Initializes a new instance of the <see cref="BaseChannel"/> class.</summary>
    /// <param name="remoteHost">The r em ot eh os t value.</param>
    /// <param name="port">The p or t value.</param>
    /// <param name="timeProvider">Optional time provider; defaults to <see cref="TimeProvider.System"/>.</param>
    internal BaseChannel(string remoteHost, int port, TimeProvider? timeProvider = null)
    {
        RemoteHost = remoteHost;
        Port = port;
        Semaphore = new(1, 1);
        TimeProvider = timeProvider ?? TimeProvider.System;
    }

    /// <summary>Gets the remote host value.</summary>
    internal string RemoteHost { get; }

    /// <summary>Gets the port value.</summary>
    internal int Port { get; }

    /// <summary>Gets the semaphore value.</summary>
    protected SemaphoreSlim Semaphore { get; }

    /// <summary>Gets the time provider used for clock operations.</summary>
    protected TimeProvider TimeProvider { get; }

    /// <summary>Releases the resources owned by this channel.</summary>
    public virtual void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    /// <summary>Initializes a new instance of the <see cref="InitializeAsync"/> class.</summary>
    /// <param name="timeout">The t im eo ut value.</param>
    /// <param name="cancellationToken">The c an ce ll at io nt ok en value.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    internal abstract Task InitializeAsync(int timeout, CancellationToken cancellationToken);

    /// <summary>Initializes a new instance of the <see cref="ProcessRequestAsync"/> class.</summary>
    /// <param name="request">The r eq ue st value.</param>
    /// <param name="timeout">The t im eo ut value.</param>
    /// <param name="retries">The r et ri es value.</param>
    /// <param name="cancellationToken">The c an ce ll at io nt ok en value.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    internal async Task<ProcessRequestResult> ProcessRequestAsync(
        FINSRequest request,
        int timeout,
        int retries,
        CancellationToken cancellationToken)
    {
        var startTimestamp = TimeProvider.GetUtcNow().UtcDateTime;
        var attemptResult = await SendAndReceiveWithRetriesAsync(request, timeout, retries, cancellationToken);

        try
        {
            return new ProcessRequestResult
            {
                BytesSent = attemptResult.BytesSent,
                PacketsSent = attemptResult.PacketsSent,
                BytesReceived = attemptResult.BytesReceived,
                PacketsReceived = attemptResult.PacketsReceived,
                Duration = TimeProvider.GetUtcNow().UtcDateTime.Subtract(startTimestamp).TotalMilliseconds,
                Response = FINSResponse.CreateNew(attemptResult.ResponseMessage, request),
            };
        }
        catch (FINSException e)
        {
            await PurgeReceiveBufferWhenServiceIdMismatchAsync(
                e,
                attemptResult.ResponseMessage,
                request,
                timeout,
                cancellationToken);

            throw new OmronPLCException(
                $"Received a FINS Error Response from Omron PLC '{RemoteHost}:{Port}'",
                e);
        }
    }

    /// <summary>Releases managed resources when requested.</summary>
    /// <param name="disposing">Whether managed resources should be released.</param>
    protected virtual void Dispose(bool disposing)
    {
        if (!disposing)
        {
            return;
        }

        Semaphore.Dispose();
    }

    /// <summary>Initializes a new instance of the <see cref="DestroyAndInitializeClientAsync"/> class.</summary>
    /// <param name="timeout">The t im eo ut value.</param>
    /// <param name="cancellationToken">The c an ce ll at io nt ok en value.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    protected abstract Task DestroyAndInitializeClientAsync(
        int timeout,
        CancellationToken cancellationToken);

    /// <summary>Initializes a new instance of the <see cref="SendMessageAsync"/> class.</summary>
    /// <param name="message">The m es sa ge value.</param>
    /// <param name="timeout">The t im eo ut value.</param>
    /// <param name="cancellationToken">The c an ce ll at io nt ok en value.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    protected abstract Task<SendMessageResult> SendMessageAsync(
        ReadOnlyMemory<byte> message,
        int timeout,
        CancellationToken cancellationToken);

    /// <summary>Initializes a new instance of the <see cref="ReceiveMessageAsync"/> class.</summary>
    /// <param name="timeout">The t im eo ut value.</param>
    /// <param name="cancellationToken">The c an ce ll at io nt ok en value.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    protected abstract Task<ReceiveMessageResult> ReceiveMessageAsync(int timeout, CancellationToken cancellationToken);

    /// <summary>Initializes a new instance of the <see cref="PurgeReceiveBufferAsync"/> class.</summary>
    /// <param name="timeout">The t im eo ut value.</param>
    /// <param name="cancellationToken">The c an ce ll at io nt ok en value.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    protected abstract Task PurgeReceiveBufferAsync(
        int timeout,
        CancellationToken cancellationToken);

    /// <summary>Attempts to send a request and receive its response.</summary>
    /// <param name="request">The request to send.</param>
    /// <param name="timeout">The timeout in milliseconds.</param>
    /// <param name="retries">The number of retry attempts.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The accumulated send and receive result.</returns>
    private async Task<RequestAttemptResult> SendAndReceiveWithRetriesAsync(
        FINSRequest request,
        int timeout,
        int retries,
        CancellationToken cancellationToken)
    {
        var result = new RequestAttemptResult();
        for (var attempts = 0; attempts <= retries; attempts++)
        {
            await WaitForChannelAsync(cancellationToken);

            try
            {
                await SendAndReceiveAttemptAsync(request, timeout, attempts, result, cancellationToken);
                return result;
            }
            catch (Exception) when (attempts < retries)
            {
                result.LastRetryFailed = true;
            }
            finally
            {
                _ = Semaphore.Release();
            }
        }

        return result;
    }

    /// <summary>Sends and receives one request attempt.</summary>
    /// <param name="request">The request to send.</param>
    /// <param name="timeout">The timeout in milliseconds.</param>
    /// <param name="attempt">The zero-based attempt number.</param>
    /// <param name="result">The accumulated result.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    private async Task SendAndReceiveAttemptAsync(
        FINSRequest request,
        int timeout,
        int attempt,
        RequestAttemptResult result,
        CancellationToken cancellationToken)
    {
        if (attempt > 0)
        {
            await DestroyAndInitializeClientAsync(timeout, cancellationToken);
        }

        var requestMessage = request.BuildMessage(GetNextRequestId());
        var sendResult = await SendMessageAsync(requestMessage, timeout, cancellationToken);
        result.BytesSent += sendResult.Bytes;
        result.PacketsSent += sendResult.Packets;

        var receiveResult = await ReceiveMessageAsync(timeout, cancellationToken);
        result.BytesReceived += receiveResult.Bytes;
        result.PacketsReceived += receiveResult.Packets;
        result.ResponseMessage = receiveResult.Message;
    }

    /// <summary>Purges stale data after a response service identifier mismatch.</summary>
    /// <param name="exception">The FINS exception.</param>
    /// <param name="responseMessage">The received response message.</param>
    /// <param name="request">The original request.</param>
    /// <param name="timeout">The timeout in milliseconds.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    private async Task PurgeReceiveBufferWhenServiceIdMismatchAsync(
        FINSException exception,
        Memory<byte> responseMessage,
        FINSRequest request,
        int timeout,
        CancellationToken cancellationToken)
    {
        if (!IsMismatch(exception, responseMessage, request))
        {
            return;
        }

        await WaitForChannelAsync(cancellationToken);

        try
        {
            await PurgeReceiveBufferAsync(timeout, cancellationToken);
        }
        catch (TimeoutException)
        {
        }
        catch (ObjectDisposedException)
        {
        }
        catch (OmronPLCException)
        {
        }
        finally
        {
            _ = Semaphore.Release();
        }

        static bool IsMismatch(FINSException exception, Memory<byte> responseMessage, FINSRequest request) =>
            exception.Message.Contains("Service ID")
            && responseMessage.Length > ProtocolConstants.Nine
            && responseMessage.Span[ProtocolConstants.Nine] != request.ServiceID;
    }

    /// <summary>Waits for exclusive access to the channel.</summary>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    private async Task WaitForChannelAsync(CancellationToken cancellationToken)
    {
        if (Semaphore.Wait(0, cancellationToken))
        {
            return;
        }

        await Semaphore.WaitAsync(cancellationToken);
    }

    /// <summary>Initializes a new instance of the <see cref="GetNextRequestId"/> class.</summary>
    /// <returns>The result produced by the operation.</returns>
    private byte GetNextRequestId()
    {
        if (_requestId == byte.MaxValue)
        {
            _requestId = byte.MinValue;
        }
        else
        {
            _requestId++;
        }

        return _requestId;
    }

    /// <summary>Contains accumulated request attempt data.</summary>
    private sealed class RequestAttemptResult
    {
        /// <summary>Gets or sets the sent byte count.</summary>
        public int BytesSent { get; set; }

        /// <summary>Gets or sets the sent packet count.</summary>
        public int PacketsSent { get; set; }

        /// <summary>Gets or sets the received byte count.</summary>
        public int BytesReceived { get; set; }

        /// <summary>Gets or sets the received packet count.</summary>
        public int PacketsReceived { get; set; }

        /// <summary>Gets or sets the response message.</summary>
        public Memory<byte> ResponseMessage { get; set; }

        /// <summary>Gets or sets a value indicating whether the last retry failed.</summary>
        public bool LastRetryFailed { get; set; }
    }
}
