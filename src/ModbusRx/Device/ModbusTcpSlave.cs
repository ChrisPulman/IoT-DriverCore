// Copyright (c) 2019-2026 Chris Pulman and contributors. All rights reserved.
// Chris Pulman and contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Collections.Concurrent;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Net.Sockets;
#if REACTIVE_SHIM
using IoT.DriverCore.Serial.Reactive;
#else
using IoT.DriverCore.Serial;
#endif
#if TIMER
    using System.Timers;
#endif
#if REACTIVE_SHIM
using IoT.DriverCore.ModbusRx.Reactive.IO;
#else
using IoT.DriverCore.ModbusRx.IO;
#endif

#if REACTIVE_SHIM
namespace IoT.DriverCore.ModbusRx.Reactive.Device;
#else
namespace IoT.DriverCore.ModbusRx.Device;
#endif

/// <summary>Modbus TCP slave device.</summary>
public sealed class ModbusTcpSlave : ModbusSlave
{
    /// <summary>Stores the server Lock value.</summary>
    private readonly Lock _serverLock = new();

    /// <summary>Stores the masters value.</summary>
    private readonly ConcurrentDictionary<string, ModbusMasterTcpConnection> _masters =
        new();

    /// <summary>Stores the server value.</summary>
    private TcpListener? _server;

    /// <summary>Stores whether an accept loop currently owns the TCP listener.</summary>
    private bool _isListening;

#if TIMER
        private Timer _timer;
#endif
    /// <summary>Initializes a new instance of the Modbus Tcp Slave class.</summary>
    /// <param name="unitId">The unit Id value.</param>
    /// <param name="tcpListener">The tcp Listener value.</param>
    private ModbusTcpSlave(byte unitId, TcpListener tcpListener)
        : base(unitId, new EmptyTransport())
    {
        if (tcpListener is null)
        {
            throw new ArgumentNullException(nameof(tcpListener));
        }

        _server = tcpListener;
    }

#if TIMER
        private ModbusTcpSlave(byte unitId, TcpListener tcpListener, double timeInterval)
            : base(unitId, new EmptyTransport())
        {
            ArgumentNullException.ThrowIfNull(tcpListener);

            _server = tcpListener;
            _timer = new Timer(timeInterval);
            _timer.Elapsed += OnTimer;
            _timer.Enabled = true;
        }
#endif

    /// <summary>Gets the Modbus TCP Masters connected to this Modbus TCP Slave.</summary>
    public ReadOnlyCollection<TcpClientRx> Masters
    {
        get
        {
            var masters = new List<TcpClientRx>(_masters.Count);
            foreach (var masterConnection in _masters.Values)
            {
                masters.Add(masterConnection.TcpClient);
            }

            return new(masters);
        }
    }

    /// <summary>Gets a value indicating whether this slave currently owns an active accept loop.</summary>
    public bool IsListening
    {
        get
        {
            lock (_serverLock)
            {
                return _isListening;
            }
        }
    }

    /// <summary>Gets the server.</summary>
    /// <value>The server.</value>
    /// <remarks>This property is not thread safe, it should only be consumed within a lock.</remarks>
    private TcpListener Server =>
        _server ?? throw new ObjectDisposedException(nameof(ModbusTcpSlave));

    /// <summary>Modbus TCP slave factory method.</summary>
    /// <param name="unitId">The unit identifier.</param>
    /// <param name="tcpListener">The TCP listener.</param>
    /// <returns>A ModbusTcpSlave.</returns>
    public static ModbusTcpSlave CreateTcp(byte unitId, TcpListener tcpListener) =>
        new(unitId, tcpListener);

#if TIMER
/// <summary>
/// Creates ModbusTcpSlave with timer which polls connected clients every
/// <paramref name="pollInterval"/> milliseconds on that they are connected.
/// </summary>
        public static ModbusTcpSlave CreateTcp(byte unitId, TcpListener tcpListener, double pollInterval)
        {
            return new ModbusTcpSlave(unitId, tcpListener, pollInterval);
        }
#endif

    /// <summary>Start slave listening for requests.</summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
    public override async Task ListenAsync()
    {
        Debug.WriteLine("Start Modbus Tcp Server.");
        TcpListener server;
        lock (_serverLock)
        {
            if (_isListening)
            {
                return;
            }

            server = Server;
            _isListening = true;
        }

        try
        {
            server.Start();

            while (true)
            {
                var client = await server.AcceptTcpClientAsync().ConfigureAwait(false);
                var masterConnection = new ModbusMasterTcpConnection(new(client), this);
                masterConnection.ModbusMasterTcpConnectionClosed += OnMasterConnectionClosedHandler;
                var endpoint = client.Client.RemoteEndPoint?.ToString()
                    ?? throw new InvalidOperationException("The TCP client does not have a remote endpoint.");
                _ = _masters.TryAdd(endpoint, masterConnection);
            }
        }
        catch (ObjectDisposedException) when (IsStopping(server))
        {
            // Disposal stops the listener and completes the accept loop normally.
        }
        catch (SocketException) when (IsStopping(server))
        {
            // TcpListener.Stop unblocks AcceptTcpClientAsync with a socket error.
        }
        finally
        {
            lock (_serverLock)
            {
                _isListening = false;
            }
        }
    }

    /// <summary>Releases unmanaged and - optionally - managed resources.</summary>
    /// <param name="disposing">
    /// <remarks>Dispose is thread-safe.</remarks>
    ///     <c>true</c> to release both managed and unmanaged resources; <c>false</c> to release only
    ///     unmanaged resources.
    /// </param>
    protected override void Dispose(bool disposing)
    {
        try
        {
            if (!disposing)
            {
                return;
            }

            lock (_serverLock)
            {
                var server = _server;
                if (server is null)
                {
                    return;
                }

                _isListening = false;
                server.Stop();
                _server = null;

#if TIMER
                if (_timer is not null)
                {
                    _timer.Dispose();
                    _timer = null;
                }
#endif

                foreach (var key in _masters.Keys)
                {
                    if (_masters.TryRemove(key, out var connection))
                    {
                        connection.ModbusMasterTcpConnectionClosed -= OnMasterConnectionClosedHandler;
                        connection.Dispose();
                    }
                }
            }
        }
        finally
        {
            base.Dispose(disposing);
        }
    }

#if TIMER
        private void OnTimer(object sender, ElapsedEventArgs e)
        {
            foreach (var master in _masters.ToList())
            {
                if (IsSocketConnected(master.Value.TcpClient.Client) == false)
                {
                    master.Value.Dispose();
                }
            }
        }
#endif
    /// <summary>Executes the On Master Connection Closed Handler operation.</summary>
    /// <param name="sender">The sender value.</param>
    /// <param name="e">The e value.</param>
    private void OnMasterConnectionClosedHandler(object? sender, TcpConnectionEventArgs e)
    {
        if (!_masters.TryRemove(e.EndPoint, out var connection))
        {
            return;
        }

        connection.ModbusMasterTcpConnectionClosed -= OnMasterConnectionClosedHandler;
        Debug.WriteLine($"Removed Master {e.EndPoint}");
    }

    /// <summary>Determines whether an accept-loop exception was caused by disposal.</summary>
    /// <param name="server">The listener owned by the accept loop.</param>
    /// <returns><c>true</c> when the listener was stopped or replaced.</returns>
    private bool IsStopping(TcpListener server)
    {
        lock (_serverLock)
        {
            return !_isListening || !ReferenceEquals(_server, server);
        }
    }
}
