// Copyright (c) 2019-2026 Chris Pulman and contributors. All rights reserved.
// Chris Pulman and contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Net;
using System.Net.Sockets;
#if REACTIVE_SHIM
using IoT.DriverCore.ModbusRx.Reactive.Data;
using IoT.DriverCore.Serial.Reactive;
#else
using IoT.DriverCore.ModbusRx.Data;
using IoT.DriverCore.Serial;
#endif

#if REACTIVE_SHIM
namespace IoT.DriverCore.ModbusRx.Reactive.Device;
#else
namespace IoT.DriverCore.ModbusRx.Device;
#endif

/// <summary>Represents a running IPv4 Modbus TCP loopback endpoint.</summary>
public sealed class ModbusTcpLoopbackEndpoint : IDisposable
{
    /// <summary>Stores the TCP slave that owns the listener.</summary>
    private readonly ModbusTcpSlave _slave;

    /// <summary>Stores whether this endpoint has been disposed.</summary>
    private int _disposed;

    /// <summary>Initializes a new instance of the <see cref="ModbusTcpLoopbackEndpoint"/> class.</summary>
    /// <param name="unitId">The Modbus unit identifier.</param>
    /// <param name="dataStore">The shared simulator memory.</param>
    internal ModbusTcpLoopbackEndpoint(byte unitId, DataStore dataStore)
    {
        var listener = new TcpListener(IPAddress.Loopback, 0);
        _slave = ModbusTcpSlave.CreateTcp(unitId, listener);
        _slave.DataStore = dataStore;
        Completion = _slave.ListenAsync();
        EndPoint = (IPEndPoint)listener.LocalEndpoint;

        _ = Completion.ContinueWith(
            static task => _ = task.Exception,
            CancellationToken.None,
            TaskContinuationOptions.ExecuteSynchronously | TaskContinuationOptions.OnlyOnFaulted,
            TaskScheduler.Default);
    }

    /// <summary>Gets the bound IPv4 loopback endpoint.</summary>
    public IPEndPoint EndPoint { get; }

    /// <summary>Gets the operating-system assigned TCP port.</summary>
    public int Port => EndPoint.Port;

    /// <summary>Gets the listener completion task.</summary>
    public Task Completion { get; }

    /// <summary>Creates a Modbus IP master connected through the operating-system TCP stack.</summary>
    /// <returns>A connected master.</returns>
    public ModbusIpMaster CreateMaster()
    {
        ThrowIfDisposed();
        return ModbusIpMaster.CreateIp(new TcpClientRx(IPAddress.Loopback.ToString(), Port));
    }

    /// <summary>Stops the listener and disconnects its masters.</summary>
    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0)
        {
            return;
        }

        _slave.Dispose();
    }

    /// <summary>Throws when this endpoint is disposed.</summary>
    private void ThrowIfDisposed() =>
        _ = Volatile.Read(ref _disposed) != 0
            ? throw new ObjectDisposedException(nameof(ModbusTcpLoopbackEndpoint))
            : 0;
}
