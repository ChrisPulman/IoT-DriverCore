// Copyright (c) 2019-2026 Chris Pulman and contributors. All rights reserved.
// Chris Pulman and contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

#if REACTIVE_SHIM
namespace OmronPlcRx.Reactive.Core;
#else
namespace OmronPlcRx.Core;
#endif

/// <summary>
/// UDP socket client wrapper providing send and receive operations with timeout and cancellation support
/// across multiple target frameworks.
/// </summary>
internal sealed class UdpClient : IDisposable
{
    /// <summary>Stores the timeout message suffix.</summary>
    private const string TimeoutMessageSuffix = "' within the Timeout Period";

    /// <summary>Stores the s oc ke t value.</summary>
    private readonly Socket _socket;

    /// <summary>Stores the r em ot eh os t value.</summary>
    private readonly string _remoteHost;

    /// <summary>Stores the r em ot ep or t value.</summary>
    private readonly int _remotePort;

    /// <summary>Stores the d is po se d value.</summary>
    private bool _disposed;

    /// <summary>Initializes a new instance of the <see cref="UdpClient"/> class.</summary>
    /// <param name="host">The h os t value.</param>
    /// <param name="port">The p or t value.</param>
    internal UdpClient(string host, int port)
    {
        _remoteHost = host ?? throw new ArgumentNullException(nameof(host));

        if (port is < IPEndPoint.MinPort or > IPEndPoint.MaxPort)
        {
            throw new ArgumentOutOfRangeException(
                nameof(port),
                "The Port Number specified is outside the valid Range of IPEndPoint.MinPort or IPEndPoint.MaxPort");
        }

        _remotePort = port;

        _socket = new(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
        _socket.Connect(_remoteHost, _remotePort);
    }

    /// <summary>Initializes a new instance of the <see cref="UdpClient"/> class.</summary>
    /// <param name="address">The a dd re ss value.</param>
    /// <param name="port">The p or t value.</param>
    internal UdpClient(IPAddress address, int port)
    {
        if (address is null)
        {
            throw new ArgumentNullException(nameof(address));
        }

        _remoteHost = address.ToString();

        if (port is < IPEndPoint.MinPort or > IPEndPoint.MaxPort)
        {
            throw new ArgumentOutOfRangeException(
                nameof(port),
                "The Port Number specified is outside the valid Range of IPEndPoint.MinPort or IPEndPoint.MaxPort");
        }

        _remotePort = port;

        _socket = new(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
        _socket.Connect(_remoteHost, _remotePort);
    }

    /// <summary>Gets the available value.</summary>
    internal int Available => _disposed ? 0 : _socket.Available;

    /// <summary>Gets the socket value.</summary>
    internal Socket? Socket => _disposed ? null : _socket;

    /// <summary>Disposes the UDP client and underlying socket.</summary>
    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    /// <summary>Sends bytes with no finite timeout.</summary>
    /// <param name="buffer">The b uf fe r value.</param>
    /// <param name="cancellationToken">The c an ce ll at io nt ok en value.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
#if NET6_0_OR_GREATER
    internal Task<int> SendAsync(byte[] buffer, CancellationToken cancellationToken) =>
        SendAsync(buffer.AsMemory(), Timeout.InfiniteTimeSpan, cancellationToken);
#else
    internal Task<int> SendAsync(byte[] buffer, CancellationToken cancellationToken) =>
        SendAsync(buffer, Timeout.InfiniteTimeSpan, cancellationToken);
#endif

    /// <summary>Sends bytes with a timeout in milliseconds.</summary>
    /// <param name="buffer">The b uf fe r value.</param>
    /// <param name="timeout">The t im eo ut value.</param>
    /// <param name="cancellationToken">The c an ce ll at io nt ok en value.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
#if NET6_0_OR_GREATER
    internal Task<int> SendAsync(byte[] buffer, int timeout, CancellationToken cancellationToken) =>
        SendAsync(buffer.AsMemory(), TimeSpan.FromMilliseconds(timeout), cancellationToken);
#else
    internal Task<int> SendAsync(byte[] buffer, int timeout, CancellationToken cancellationToken) =>
        SendAsync(buffer, TimeSpan.FromMilliseconds(timeout), cancellationToken);
#endif

#if NET6_0_OR_GREATER
    /// <summary>Sends bytes from read-only memory with no finite timeout.</summary>
    /// <param name="buffer">The b uf fe r value.</param>
    /// <param name="cancellationToken">The c an ce ll at io nt ok en value.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    internal Task<int> SendAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken) =>
        SendAsync(buffer, Timeout.InfiniteTimeSpan, cancellationToken);

    /// <summary>Sends bytes from read-only memory with a timeout in milliseconds.</summary>
    /// <param name="buffer">The b uf fe r value.</param>
    /// <param name="timeout">The t im eo ut value.</param>
    /// <param name="cancellationToken">The c an ce ll at io nt ok en value.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    internal Task<int> SendAsync(
        ReadOnlyMemory<byte> buffer,
        int timeout,
        CancellationToken cancellationToken) =>
        SendAsync(buffer, TimeSpan.FromMilliseconds(timeout), cancellationToken);

    /// <summary>Sends bytes from read-only memory with a timeout.</summary>
    /// <param name="buffer">The b uf fe r value.</param>
    /// <param name="timeout">The t im eo ut value.</param>
    /// <param name="cancellationToken">The c an ce ll at io nt ok en value.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    internal async Task<int> SendAsync(
        ReadOnlyMemory<byte> buffer,
        TimeSpan timeout,
        CancellationToken cancellationToken)
    {
        ThrowIfDisposed();

        if (timeout == Timeout.InfiniteTimeSpan || cancellationToken.IsCancellationRequested)
        {
            return await _socket.SendAsync(buffer, SocketFlags.None, cancellationToken).ConfigureAwait(false);
        }

        using var sendCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        var sendValueTask = _socket.SendAsync(buffer, SocketFlags.None, sendCts.Token);

        if (sendValueTask.IsCompleted || sendValueTask.IsCanceled || cancellationToken.IsCancellationRequested)
        {
            return await sendValueTask.ConfigureAwait(false);
        }

        using var delayCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        var sendTask = sendValueTask.AsTask();
        var delayTask = Task.Delay(timeout, delayCts.Token);

        if (sendTask == await Task.WhenAny(sendTask, delayTask).ConfigureAwait(false))
        {
            await SocketOperationCleanup.CancelDelayAsync(delayCts, delayTask).ConfigureAwait(false);

            return await sendTask.ConfigureAwait(false);
        }

        await SocketOperationCleanup.CancelSocketOperationAsync(sendCts, sendTask).ConfigureAwait(false);

        await delayTask.ConfigureAwait(false);

        throw new TimeoutException(CreateTimeoutMessage("Send"));
    }
#endif

#if NET6_0_OR_GREATER
    /// <summary>Sends bytes with a timeout.</summary>
    /// <param name="buffer">The b uf fe r value.</param>
    /// <param name="timeout">The t im eo ut value.</param>
    /// <param name="cancellationToken">The c an ce ll at io nt ok en value.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    internal Task<int> SendAsync(byte[] buffer, TimeSpan timeout, CancellationToken cancellationToken)
    {
        ThrowIfDisposed();

        return SendAsync(buffer.AsMemory(), timeout, cancellationToken);
    }
#else
    /// <summary>Sends bytes with a timeout.</summary>
    /// <param name="buffer">The b uf fe r value.</param>
    /// <param name="timeout">The t im eo ut value.</param>
    /// <param name="cancellationToken">The c an ce ll at io nt ok en value.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    internal async Task<int> SendAsync(byte[] buffer, TimeSpan timeout, CancellationToken cancellationToken)
    {
        ThrowIfDisposed();

        Func<AsyncCallback, object, IAsyncResult> beginSend = (callback, state) =>
            _socket.BeginSend(buffer, 0, buffer.Length, SocketFlags.None, callback, state);

        if (timeout == Timeout.InfiniteTimeSpan || cancellationToken.IsCancellationRequested)
        {
            return await Task.Factory.FromAsync(beginSend, _socket.EndSend, null).ConfigureAwait(false);
        }

        using var sendCts =
            CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        var sendTask = Task.Factory.FromAsync(beginSend, _socket.EndSend, null);
        if (sendTask.IsCompleted
            || sendTask.IsCanceled
            || cancellationToken.IsCancellationRequested)
        {
            return await sendTask.ConfigureAwait(false);
        }

        using var delayCts =
            CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        var delayTask = Task.Delay(timeout, delayCts.Token);
        if (sendTask == await Task.WhenAny(sendTask, delayTask).ConfigureAwait(false))
        {
            await SocketOperationCleanup
                .CancelDelayAsync(delayCts, delayTask)
                .ConfigureAwait(false);
            return await sendTask.ConfigureAwait(false);
        }

        await SocketOperationCleanup
            .CancelSocketOperationAsync(sendCts, sendTask)
            .ConfigureAwait(false);
        await delayTask.ConfigureAwait(false);
        throw new TimeoutException(CreateTimeoutMessage("Send"));
    }
#endif

    /// <summary>Receives bytes with no finite timeout.</summary>
    /// <param name="buffer">The b uf fe r value.</param>
    /// <param name="cancellationToken">The c an ce ll at io nt ok en value.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
#if NET6_0_OR_GREATER
    internal Task<int> ReceiveAsync(byte[] buffer, CancellationToken cancellationToken) =>
        ReceiveAsync(buffer.AsMemory(), Timeout.InfiniteTimeSpan, cancellationToken);
#else
    internal Task<int> ReceiveAsync(byte[] buffer, CancellationToken cancellationToken) =>
        ReceiveAsync(buffer, Timeout.InfiniteTimeSpan, cancellationToken);
#endif

    /// <summary>Receives bytes with a timeout in milliseconds.</summary>
    /// <param name="buffer">The b uf fe r value.</param>
    /// <param name="timeout">The t im eo ut value.</param>
    /// <param name="cancellationToken">The c an ce ll at io nt ok en value.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
#if NET6_0_OR_GREATER
    internal Task<int> ReceiveAsync(byte[] buffer, int timeout, CancellationToken cancellationToken) =>
        ReceiveAsync(buffer.AsMemory(), TimeSpan.FromMilliseconds(timeout), cancellationToken);
#else
    internal Task<int> ReceiveAsync(byte[] buffer, int timeout, CancellationToken cancellationToken) =>
        ReceiveAsync(buffer, TimeSpan.FromMilliseconds(timeout), cancellationToken);
#endif

#if NET6_0_OR_GREATER
    /// <summary>Receives bytes into memory with no finite timeout.</summary>
    /// <param name="buffer">The b uf fe r value.</param>
    /// <param name="cancellationToken">The c an ce ll at io nt ok en value.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    internal Task<int> ReceiveAsync(Memory<byte> buffer, CancellationToken cancellationToken) =>
        ReceiveAsync(buffer, Timeout.InfiniteTimeSpan, cancellationToken);

    /// <summary>Receives bytes into memory with a timeout in milliseconds.</summary>
    /// <param name="buffer">The b uf fe r value.</param>
    /// <param name="timeout">The t im eo ut value.</param>
    /// <param name="cancellationToken">The c an ce ll at io nt ok en value.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    internal Task<int> ReceiveAsync(
        Memory<byte> buffer,
        int timeout,
        CancellationToken cancellationToken) =>
        ReceiveAsync(buffer, TimeSpan.FromMilliseconds(timeout), cancellationToken);

    /// <summary>Receives bytes into memory with a timeout.</summary>
    /// <param name="buffer">The b uf fe r value.</param>
    /// <param name="timeout">The t im eo ut value.</param>
    /// <param name="cancellationToken">The c an ce ll at io nt ok en value.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    internal async Task<int> ReceiveAsync(Memory<byte> buffer, TimeSpan timeout, CancellationToken cancellationToken)
    {
        ThrowIfDisposed();

        if (timeout == Timeout.InfiniteTimeSpan || cancellationToken.IsCancellationRequested)
        {
            return await _socket.ReceiveAsync(buffer, SocketFlags.None, cancellationToken).ConfigureAwait(false);
        }

        using var receiveCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        var receiveValueTask = _socket.ReceiveAsync(buffer, SocketFlags.None, receiveCts.Token);

        if (receiveValueTask.IsCompleted || receiveValueTask.IsCanceled || cancellationToken.IsCancellationRequested)
        {
            return await receiveValueTask.ConfigureAwait(false);
        }

        using var delayCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        var receiveTask = receiveValueTask.AsTask();
        var delayTask = Task.Delay(timeout, delayCts.Token);

        if (receiveTask == await Task.WhenAny(receiveTask, delayTask).ConfigureAwait(false))
        {
            await SocketOperationCleanup.CancelDelayAsync(delayCts, delayTask).ConfigureAwait(false);

            return await receiveTask.ConfigureAwait(false);
        }

        await SocketOperationCleanup.CancelSocketOperationAsync(receiveCts, receiveTask).ConfigureAwait(false);

        await delayTask.ConfigureAwait(false);

        throw new TimeoutException(CreateTimeoutMessage("Receive"));
    }
#endif

#if NET6_0_OR_GREATER
    /// <summary>Receives bytes into an array with a timeout.</summary>
    /// <param name="buffer">The b uf fe r value.</param>
    /// <param name="timeout">The t im eo ut value.</param>
    /// <param name="cancellationToken">The c an ce ll at io nt ok en value.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    internal Task<int> ReceiveAsync(byte[] buffer, TimeSpan timeout, CancellationToken cancellationToken)
    {
        ThrowIfDisposed();

        return ReceiveAsync(buffer.AsMemory(), timeout, cancellationToken);
    }
#else
    /// <summary>Receives bytes into an array with a timeout.</summary>
    /// <param name="buffer">The b uf fe r value.</param>
    /// <param name="timeout">The t im eo ut value.</param>
    /// <param name="cancellationToken">The c an ce ll at io nt ok en value.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    internal async Task<int> ReceiveAsync(byte[] buffer, TimeSpan timeout, CancellationToken cancellationToken)
    {
        ThrowIfDisposed();

        Func<AsyncCallback, object, IAsyncResult> beginReceive = (callback, state) =>
            _socket.BeginReceive(buffer, 0, buffer.Length, SocketFlags.None, callback, state);

        if (timeout == Timeout.InfiniteTimeSpan || cancellationToken.IsCancellationRequested)
        {
            return await Task.Factory.FromAsync(beginReceive, _socket.EndReceive, null).ConfigureAwait(false);
        }

        using var receiveCts =
            CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        var receiveTask = Task.Factory.FromAsync(beginReceive, _socket.EndReceive, null);
        if (receiveTask.IsCompleted
            || receiveTask.IsCanceled
            || cancellationToken.IsCancellationRequested)
        {
            return await receiveTask.ConfigureAwait(false);
        }

        using var delayCts =
            CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        var delayTask = Task.Delay(timeout, delayCts.Token);
        if (receiveTask == await Task.WhenAny(receiveTask, delayTask).ConfigureAwait(false))
        {
            await SocketOperationCleanup
                .CancelDelayAsync(delayCts, delayTask)
                .ConfigureAwait(false);
            return await receiveTask.ConfigureAwait(false);
        }

        await SocketOperationCleanup
            .CancelSocketOperationAsync(receiveCts, receiveTask)
            .ConfigureAwait(false);
        await delayTask.ConfigureAwait(false);
        throw new TimeoutException(CreateTimeoutMessage("Receive"));
    }
#endif

    /// <summary>Creates a timeout message for a socket operation.</summary>
    /// <param name="operation">The operation that timed out.</param>
    /// <returns>The composed timeout message.</returns>
    private string CreateTimeoutMessage(string operation)
    {
        var endpoint = $"{_remoteHost}:{_remotePort}";
        return $"Failed to {operation} to the Remote Host '{endpoint}{TimeoutMessageSuffix}";
    }

    /// <summary>Releases managed resources used by the UDP client.</summary>
    /// <param name="disposing">The d is po si ng value.</param>
    private void Dispose(bool disposing)
    {
        if (_disposed)
        {
            return;
        }

        if (disposing && _socket is not null)
        {
            try
            {
                _socket.Shutdown(SocketShutdown.Both);
            }
            catch (SocketException)
            {
            }
            catch (ObjectDisposedException)
            {
            }
            finally
            {
                _socket.Dispose();
            }
        }

        _disposed = true;
    }

    /// <summary>Throws if the UDP client has been disposed.</summary>
    private void ThrowIfDisposed()
    {
        if (!_disposed)
        {
            return;
        }

        throw new ObjectDisposedException(GetType().FullName);
    }
}
