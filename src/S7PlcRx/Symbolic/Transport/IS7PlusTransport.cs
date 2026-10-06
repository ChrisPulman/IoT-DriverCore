// Copyright (c) 2019-2026 Chris Pulman and contributors. All rights reserved.
// Chris Pulman and contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

#if REACTIVE_SHIM
using IoT.Driver.S7PlcRx.Reactive.Symbolic.Protocol;

namespace IoT.Driver.S7PlcRx.Reactive.Symbolic.Transport;
#else
using IoT.Driver.S7PlcRx.Symbolic.Protocol;

namespace IoT.Driver.S7PlcRx.Symbolic.Transport;
#endif

/// <summary>Transports clear or encrypted S7Plus protocol messages.</summary>
internal interface IS7PlusTransport : IAsyncDisposable
{
    /// <summary>Connects the ISO transport.</summary>
    /// <param name="cancellationToken">Cancels the connection.</param>
    /// <returns>The connection completion.</returns>
    Task ConnectAsync(CancellationToken cancellationToken);

    /// <summary>Upgrades the ISO transport to TLS.</summary>
    /// <param name="cancellationToken">Cancels the handshake.</param>
    /// <returns>The handshake completion.</returns>
    Task UpgradeToTlsAsync(CancellationToken cancellationToken);

    /// <summary>Disconnects the current transport.</summary>
    /// <param name="cancellationToken">Cancels waiting for active operations.</param>
    /// <returns>The disconnection completion.</returns>
    Task DisconnectAsync(CancellationToken cancellationToken);

    /// <summary>Sends a protocol message.</summary>
    /// <param name="message">The message.</param>
    /// <param name="cancellationToken">Cancels the send.</param>
    /// <returns>The send completion.</returns>
    Task SendAsync(PlusMessage message, CancellationToken cancellationToken);

    /// <summary>Receives a protocol message.</summary>
    /// <param name="cancellationToken">Cancels the receive.</param>
    /// <returns>The received message.</returns>
    Task<PlusMessage> ReceiveAsync(CancellationToken cancellationToken);

    /// <summary>Exports key material from the negotiated TLS session.</summary>
    /// <param name="label">The exporter label.</param>
    /// <param name="length">The requested byte count.</param>
    /// <returns>The exported bytes.</returns>
    byte[] ExportKeyingMaterial(string label, int length);
}
