// Copyright (c) 2019-2026 Chris Pulman and contributors. All rights reserved.
// Chris Pulman and contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Net;
using System.Net.Sockets;

namespace IoT.DriverCore.ModbusRx.UnitTests;

/// <summary>Composition wrapper that gives a TCP listener portable test ownership.</summary>
internal sealed class TestTcpListener : IDisposable
{
    /// <summary>The owned platform listener.</summary>
    private readonly TcpListener _listener;

    /// <summary>Initializes a new instance of the <see cref="TestTcpListener"/> class.</summary>
    /// <param name="localAddress">The local address.</param>
    /// <param name="port">The local port.</param>
    internal TestTcpListener(IPAddress localAddress, int port)
    {
        _listener = new(localAddress, port);
    }

    /// <summary>Gets the listener's bound endpoint.</summary>
    internal EndPoint LocalEndpoint => _listener.LocalEndpoint;

    /// <summary>Converts the ownership wrapper to the underlying listener for production factories.</summary>
    /// <param name="listener">The listener wrapper.</param>
    public static implicit operator TcpListener(TestTcpListener listener) => listener._listener;

    /// <inheritdoc />
    public void Dispose()
    {
#if NETFRAMEWORK
        _listener.Stop();
#else
        _listener.Dispose();
#endif
    }

    /// <summary>Starts listening.</summary>
    internal void Start() => _listener.Start();

    /// <summary>Accepts one TCP client asynchronously.</summary>
    /// <returns>The accepted client.</returns>
    internal Task<TcpClient> AcceptTcpClientAsync() => _listener.AcceptTcpClientAsync();
}
