// Copyright (c) 2019-2026 Chris Pulman and contributors. All rights reserved.
// Chris Pulman and contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

#if REACTIVE_SHIM
using IoT.Driver.ModbusRx.Reactive.Utility;
using IoT.Driver.Serial.Reactive;
#else
using IoT.Driver.ModbusRx.Utility;
using IoT.Driver.Serial;
#endif

#if REACTIVE_SHIM
namespace IoT.Driver.ModbusRx.Reactive.IO;
#else
namespace IoT.Driver.ModbusRx.IO;
#endif

/// <summary>Concrete Implementor - http://en.wikipedia.org/wiki/Bridge_Pattern.</summary>
internal sealed class UdpClientAdapter : IStreamResource
{
    /// <summary>Stores the udp Client value.</summary>
    private readonly UdpClientRx _udpClient;

    /// <summary>Initializes a new instance of the Udp Client Adapter class.</summary>
    /// <param name="udpClient">The udp Client value.</param>
    public UdpClientAdapter(UdpClientRx udpClient)
    {
        _udpClient = ModbusGuard.NotNull(udpClient, nameof(udpClient));
    }

    /// <summary>Gets or sets the Infinite Timeout value.</summary>
    public int InfiniteTimeout => _udpClient.InfiniteTimeout;

    /// <summary>Gets or sets the Read Timeout value.</summary>
    public int ReadTimeout
    {
        get => _udpClient.ReadTimeout;
        set => _udpClient.ReadTimeout = value;
    }

    /// <summary>Gets or sets the Write Timeout value.</summary>
    public int WriteTimeout
    {
        get => _udpClient.WriteTimeout;
        set => _udpClient.WriteTimeout = value;
    }

    /// <summary>Executes the Discard In Buffer operation.</summary>
    public void DiscardInBuffer()
    {
        // no-op
    }

    /// <summary>Executes the Read Async operation.</summary>
    /// <param name="buffer">The buffer value.</param>
    /// <param name="offset">The offset value.</param>
    /// <param name="count">The count value.</param>
    /// <returns>The result.</returns>
    public Task<int> ReadAsync(byte[] buffer, int offset, int count) =>
        _udpClient.ReadAsync(buffer, offset, count);

    /// <summary>Executes the Write operation.</summary>
    /// <param name="buffer">The buffer value.</param>
    /// <param name="offset">The offset value.</param>
    /// <param name="count">The count value.</param>
    public void Write(byte[] buffer, int offset, int count) =>
        _udpClient.Write(buffer, offset, count);

    /// <summary>Executes the Dispose operation.</summary>
    public void Dispose()
    {
        Dispose(true);
    }

    /// <summary>Executes the Dispose operation.</summary>
    /// <param name="disposing">The disposing value.</param>
    private void Dispose(bool disposing)
    {
        if (!disposing)
        {
            return;
        }

        _udpClient.Dispose();
    }
}
