// Copyright (c) 2019-2026 Chris Pulman and contributors. All rights reserved.
// Chris Pulman and contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

#if REACTIVE_SHIM
namespace ModbusRx.Reactive.IO;
#else
namespace ModbusRx.IO;
#endif

/// <summary>Represents a serial resource. Implementor - http://en.wikipedia.org/wiki/Bridge_Pattern.</summary>
public interface IStreamResource : IDisposable
{
    /// <summary>Gets indicates that no timeout should occur.</summary>
    int InfiniteTimeout { get; }

    /// <summary>Gets or sets the read-operation timeout in milliseconds.</summary>
    int ReadTimeout { get; set; }

    /// <summary>Gets or sets the write-operation timeout in milliseconds.</summary>
    int WriteTimeout { get; set; }

    /// <summary>Purges the receive buffer.</summary>
    void DiscardInBuffer();

    /// <summary>Reads bytes into a byte array at the specified offset.</summary>
    /// <param name="buffer">The byte array to write the input to.</param>
    /// <param name="offset">The offset in the buffer array to begin writing.</param>
    /// <param name="count">The number of bytes to read.</param>
    /// <returns>The number of bytes read.</returns>
    Task<int> ReadAsync(byte[] buffer, int offset, int count);

    /// <summary>Writes bytes from an output buffer, starting at the specified offset.</summary>
    /// <param name="buffer">The byte array that contains the data to write to the port.</param>
    /// <param name="offset">The offset in the buffer array to begin writing.</param>
    /// <param name="count">The number of bytes to write.</param>
    void Write(byte[] buffer, int offset, int count);
}
