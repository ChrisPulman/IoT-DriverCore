// Copyright (c) 2019-2026 Chris Pulman and contributors. All rights reserved.
// Chris Pulman and contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Net;
using System.Net.Sockets;

#if REACTIVE_SHIM
namespace IoT.DriverCore.OmronPlcRx.Reactive.Core;
#else
namespace IoT.DriverCore.OmronPlcRx.Core;
#endif

/// <summary>Provides TCP socket setup helpers.</summary>
internal static class TcpSocketConfiguration
{
    /// <summary>Normalizes a socket linger state for immediate close behavior.</summary>
    /// <param name="socket">The socket to configure.</param>
    internal static void ConfigureZeroLinger(Socket socket)
    {
        var lingerState = socket.LingerState;
        if (lingerState is null)
        {
            socket.LingerState = new(true, 0);
            return;
        }

        lingerState.Enabled = true;
        lingerState.LingerTime = 0;
        socket.LingerState = lingerState;
    }

    /// <summary>Gets host and port values from an endpoint.</summary>
    /// <param name="endPoint">The endpoint to inspect.</param>
    /// <returns>The host and port values.</returns>
    internal static (string Host, int Port) GetRemoteHostAndPort(EndPoint? endPoint) => endPoint switch
    {
        IPEndPoint ipEndPoint => (ipEndPoint.Address.ToString(), ipEndPoint.Port),
        DnsEndPoint dnsEndPoint => (dnsEndPoint.Host, dnsEndPoint.Port),
        _ => (string.Empty, IPEndPoint.MinPort),
    };
}
