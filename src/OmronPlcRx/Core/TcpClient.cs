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
/// TCP socket client wrapper providing connect, send and receive operations with timeout and cancellation
/// support across multiple target frameworks.
/// </summary>
internal sealed class TcpClient : IDisposable
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

    /// <summary>Initializes a new instance of the <see cref="TcpClient"/> class.</summary>
    /// <param name="host">The h os t value.</param>
    /// <param name="port">The p or t value.</param>
    internal TcpClient(string host, int port)
    {
        _remoteHost = host ?? throw new ArgumentNullException(nameof(host));

        if (port is < IPEndPoint.MinPort or > IPEndPoint.MaxPort)
        {
            throw new ArgumentOutOfRangeException(
                nameof(port),
                "The Port Number specified is outside the valid Range of IPEndPoint.MinPort or IPEndPoint.MaxPort");
        }

        _remotePort = port;

        _socket = new(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp)
        {
            LingerState = new(true, 0),
        };
    }

    /// <summary>Initializes a new instance of the <see cref="TcpClient"/> class.</summary>
    /// <param name="address">The a dd re ss value.</param>
    /// <param name="port">The p or t value.</param>
    internal TcpClient(IPAddress address, int port)
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

        _socket = new(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp)
        {
            LingerState = new(true, 0),
        };
    }

    /// <summary>Initializes a new instance of the <see cref="TcpClient"/> class.</summary>
    /// <param name="acceptedSocket">The a cc ep te ds oc ke t value.</param>
    internal TcpClient(Socket acceptedSocket)
    {
        _socket = acceptedSocket ?? throw new ArgumentNullException(nameof(acceptedSocket));
        TcpSocketConfiguration.ConfigureZeroLinger(_socket);
        (_remoteHost, _remotePort) = TcpSocketConfiguration.GetRemoteHostAndPort(_socket.RemoteEndPoint);
    }

    /// <summary>Gets the available value.</summary>
    internal int Available => _disposed ? 0 : _socket.Available;

    /// <summary>Gets the connected value.</summary>
    internal bool Connected => !_disposed && _socket.Connected;

    /// <summary>Gets the socket value.</summary>
    internal Socket? Socket => _disposed ? null : _socket;

    /// <summary>Gets or sets the no delay value.</summary>
    internal bool NoDelay
    {
        get => _disposed ? false : _socket.NoDelay;

        set
        {
            if (_disposed)
            {
                return;
            }

            _socket.NoDelay = value;
        }
    }

    /// <summary>Gets or sets the linger state value.</summary>
    internal LingerOption? LingerState
    {
        get => _disposed ? null : _socket.LingerState;

        set
        {
            if (_disposed || value is null)
            {
                return;
            }

            _socket.LingerState = value;
        }
    }

    /// <summary>Gets or sets the keep alive enabled value.</summary>
    internal bool KeepAliveEnabled
    {
        get => GetBooleanSocketOption(SocketOptionLevel.Socket, SocketOptionName.KeepAlive);

        set
        {
            if (_disposed)
            {
                return;
            }

            _socket.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.KeepAlive, value);
        }
    }

#if NET6_0_OR_GREATER
    /// <summary>Gets or sets the keep alive internal value.</summary>
    internal int KeepAliveInternal
    {
        get => GetIntegerSocketOption(SocketOptionLevel.Tcp, SocketOptionName.TcpKeepAliveInterval);

        set
        {
            if (_disposed)
            {
                return;
            }

            _socket.SetSocketOption(SocketOptionLevel.Tcp, SocketOptionName.TcpKeepAliveInterval, value);
        }
    }

    /// <summary>Gets or sets the keep alive delay value.</summary>
    internal int KeepAliveDelay
    {
        get => GetIntegerSocketOption(SocketOptionLevel.Tcp, SocketOptionName.TcpKeepAliveTime);

        set
        {
            if (_disposed)
            {
                return;
            }

            _socket.SetSocketOption(SocketOptionLevel.Tcp, SocketOptionName.TcpKeepAliveTime, value);
        }
    }

    /// <summary>Gets or sets the keep alive retry count value.</summary>
    internal int KeepAliveRetryCount
    {
        get => GetIntegerSocketOption(SocketOptionLevel.Tcp, SocketOptionName.TcpKeepAliveRetryCount);

        set
        {
            if (_disposed)
            {
                return;
            }

            _socket.SetSocketOption(SocketOptionLevel.Tcp, SocketOptionName.TcpKeepAliveRetryCount, value);
        }
    }
#endif

    /// <summary>Disposes the TCP client and underlying socket.</summary>
    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    /// <summary>Connects to the remote endpoint with a timeout in milliseconds.</summary>
    /// <param name="timeout">The t im eo ut value.</param>
    /// <param name="cancellationToken">The c an ce ll at io nt ok en value.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    internal Task ConnectAsync(int timeout, CancellationToken cancellationToken) =>
        ConnectAsync(TimeSpan.FromMilliseconds(timeout), cancellationToken);

    /// <summary>Connects to the remote endpoint with a timeout.</summary>
    /// <param name="timeout">The t im eo ut value.</param>
    /// <param name="cancellationToken">The c an ce ll at io nt ok en value.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    internal async Task ConnectAsync(TimeSpan timeout, CancellationToken cancellationToken)
    {
        ThrowIfDisposed();

#if NET6_0_OR_GREATER
        if (timeout == Timeout.InfiniteTimeSpan || cancellationToken.IsCancellationRequested)
        {
            await _socket.ConnectAsync(_remoteHost, _remotePort, cancellationToken).ConfigureAwait(false);
            return;
        }

        using var connectCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        var connectValueTask = _socket.ConnectAsync(_remoteHost, _remotePort, connectCts.Token);

        if (connectValueTask.IsCompleted || connectValueTask.IsCanceled || cancellationToken.IsCancellationRequested)
        {
            await connectValueTask.ConfigureAwait(false);
            return;
        }

        using var delayCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        var connectTask = connectValueTask.AsTask();
        var delayTask = Task.Delay(timeout, delayCts.Token);

        if (connectTask == await Task.WhenAny(connectTask, delayTask).ConfigureAwait(false))
        {
            await SocketOperationCleanup.CancelDelayAsync(delayCts, delayTask).ConfigureAwait(false);

            await connectTask.ConfigureAwait(false);
            return;
        }

        await SocketOperationCleanup.CancelSocketOperationAsync(connectCts, connectTask).ConfigureAwait(false);

        await delayTask.ConfigureAwait(false);

        throw new TimeoutException(CreateTimeoutMessage("Connect"));
#else
        if (timeout == Timeout.InfiniteTimeSpan || cancellationToken.IsCancellationRequested)
        {
            await Task.Factory
                .FromAsync(_socket.BeginConnect, _socket.EndConnect, _remoteHost, _remotePort, null)
                .ConfigureAwait(false);
            return;
        }

        using var connectCts =
            CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        var connectTask = Task.Factory.FromAsync(
            _socket.BeginConnect,
            _socket.EndConnect,
            _remoteHost,
            _remotePort,
            null);
        if (connectTask.IsCompleted
            || connectTask.IsCanceled
            || cancellationToken.IsCancellationRequested)
        {
            await connectTask.ConfigureAwait(false);
            return;
        }

        using var delayCts =
            CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        var delayTask = Task.Delay(timeout, delayCts.Token);
        if (connectTask == await Task.WhenAny(connectTask, delayTask).ConfigureAwait(false))
        {
            await SocketOperationCleanup
                .CancelDelayAsync(delayCts, delayTask)
                .ConfigureAwait(false);
            await connectTask.ConfigureAwait(false);
            return;
        }

        await SocketOperationCleanup
            .CancelSocketOperationAsync(connectCts, connectTask)
            .ConfigureAwait(false);
        await delayTask.ConfigureAwait(false);
        throw new TimeoutException(CreateTimeoutMessage("Connect"));
#endif
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

        Func<AsyncCallback, object, IAsyncResult> begin = (callback, state) =>
            _socket.BeginSend(buffer, 0, buffer.Length, SocketFlags.None, callback, state);

        if (timeout == Timeout.InfiniteTimeSpan || cancellationToken.IsCancellationRequested)
        {
            return await Task.Factory.FromAsync(begin, _socket.EndSend, null).ConfigureAwait(false);
        }

        using var sendCts =
            CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        var sendTask = Task.Factory.FromAsync(begin, _socket.EndSend, null);
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

        Func<AsyncCallback, object, IAsyncResult> begin = (callback, state) =>
            _socket.BeginReceive(buffer, 0, buffer.Length, SocketFlags.None, callback, state);

        if (timeout == Timeout.InfiniteTimeSpan || cancellationToken.IsCancellationRequested)
        {
            return await Task.Factory.FromAsync(begin, _socket.EndReceive, null).ConfigureAwait(false);
        }

        using var receiveCts =
            CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        var receiveTask = Task.Factory.FromAsync(begin, _socket.EndReceive, null);
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

    /// <summary>Gets a Boolean socket option when the socket is active.</summary>
    /// <param name="optionLevel">The socket option level.</param>
    /// <param name="optionName">The socket option name.</param>
    /// <returns>The socket option value.</returns>
    private bool GetBooleanSocketOption(SocketOptionLevel optionLevel, SocketOptionName optionName)
    {
        if (_disposed)
        {
            return false;
        }

        var value = _socket.GetSocketOption(optionLevel, optionName);
        return value is not null && Convert.ToBoolean(value);
    }

#if NET6_0_OR_GREATER
    /// <summary>Gets an integer socket option when the socket is active.</summary>
    /// <param name="optionLevel">The socket option level.</param>
    /// <param name="optionName">The socket option name.</param>
    /// <returns>The socket option value.</returns>
    private int GetIntegerSocketOption(SocketOptionLevel optionLevel, SocketOptionName optionName)
    {
        if (_disposed)
        {
            return 0;
        }

        var value = _socket.GetSocketOption(optionLevel, optionName);
        return value is null ? 0 : Convert.ToInt32(value);
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

    /// <summary>Releases managed resources used by the TCP client.</summary>
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

    /// <summary>Throws if the TCP client has been disposed.</summary>
    private void ThrowIfDisposed()
    {
        if (!_disposed)
        {
            return;
        }

        throw new ObjectDisposedException(GetType().FullName);
    }
}
