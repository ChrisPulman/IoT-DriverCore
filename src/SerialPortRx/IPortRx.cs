// Copyright (c) 2019-2026 Chris Pulman and contributors. All rights reserved.
// Chris Pulman and contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

#if REACTIVE_SHIM
namespace CP.IO.Ports.Reactive;
#else
namespace CP.IO.Ports;
#endif

/// <summary>Represents a reactive receive port.</summary>
/// <seealso cref="IDisposable" />
public interface IPortRx : IDisposable
{
    /// <summary>Gets the data received after opening the receive port.</summary>
    /// <value>
    /// The byte read as a stream.
    /// </value>
    IObservable<int> BytesReceived { get; }

    /// <summary>Gets indicates that no timeout should occur.</summary>
    int InfiniteTimeout { get; }

    /// <summary>Gets or sets the read timeout in milliseconds.</summary>
    int ReadTimeout { get; set; }

    /// <summary>Gets or sets the write timeout in milliseconds.</summary>
    int WriteTimeout { get; set; }

    /// <summary>Purges the receive buffer.</summary>
    void DiscardInBuffer();

    /// <summary>Reads bytes from the input buffer into a buffer segment.</summary>
    /// <param name="buffer">The byte array to write the input to.</param>
    /// <param name="offset">The offset in the buffer array to begin writing.</param>
    /// <param name="count">The number of bytes to read.</param>
    /// <returns>The number of bytes read.</returns>
    Task<int> ReadAsync(byte[]? buffer, int offset, int count);

    /// <summary>Writes a buffer segment to the port.</summary>
    /// <param name="buffer">The byte array that contains the data to write to the port.</param>
    /// <param name="offset">The offset in the buffer array to begin writing.</param>
    /// <param name="count">The number of bytes to write.</param>
    void Write(byte[]? buffer, int offset, int count);

    /// <summary>Opens this instance.</summary>
    /// <returns>A Task.</returns>
    Task OpenAsync();

    /// <summary>Closes this instance.</summary>
    void Close();
}
