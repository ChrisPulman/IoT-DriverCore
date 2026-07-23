// Copyright (c) 2019-2026 Chris Pulman and contributors. All rights reserved.
// Chris Pulman and contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System;
using System.Threading.Tasks;

#if REACTIVE_SHIM
namespace IoT.DriverCore.OmronPlcRx.Reactive.Core.Channels;
#else
namespace IoT.DriverCore.OmronPlcRx.Core.Channels;
#endif

/// <summary>Defines the serial-port operations required by the Omron serial FINS channel.</summary>
internal interface IOmronSerialPort : IDisposable
{
    /// <summary>Gets the number of bytes available to read.</summary>
    int BytesToRead { get; }

    /// <summary>Gets or sets a value indicating whether request-to-send is enabled.</summary>
    bool RtsEnable { get; set; }

    /// <summary>Gets or sets a value indicating whether data-terminal-ready is enabled.</summary>
    bool DtrEnable { get; set; }

    /// <summary>Opens the serial port.</summary>
    /// <returns>A task that represents the asynchronous operation.</returns>
    Task OpenAsync();

    /// <summary>Closes the serial port.</summary>
    void Close();

    /// <summary>Discards unread serial input.</summary>
    void DiscardInBuffer();

    /// <summary>Writes bytes to the serial port.</summary>
    /// <param name="buffer">Source buffer.</param>
    /// <param name="offset">Source offset.</param>
    /// <param name="count">Number of bytes to write.</param>
    void Write(byte[] buffer, int offset, int count);

    /// <summary>Reads available bytes from the serial port.</summary>
    /// <param name="buffer">Destination buffer.</param>
    /// <param name="offset">Destination offset.</param>
    /// <param name="count">Maximum number of bytes to read.</param>
    /// <returns>The number of bytes read.</returns>
    int Read(byte[] buffer, int offset, int count);
}
