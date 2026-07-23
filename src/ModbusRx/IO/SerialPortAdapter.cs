// Copyright (c) 2019-2026 Chris Pulman and contributors. All rights reserved.
// Chris Pulman and contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.IO.Ports;
#if REACTIVE_SHIM
using IoT.DriverCore.Serial.Reactive;
#else
using IoT.DriverCore.Serial;
#endif

#if REACTIVE_SHIM
namespace IoT.DriverCore.ModbusRx.Reactive.IO;
#else
namespace IoT.DriverCore.ModbusRx.IO;
#endif

/// <summary>Concrete Implementor - http://en.wikipedia.org/wiki/Bridge_Pattern.</summary>
public class SerialPortAdapter : IStreamResource
{
    /// <summary>Creates new line.</summary>
    private const string NewLine = "\r\n";

    /// <summary>The serial port.</summary>
    private SerialPortRx _serialPort;

    /// <summary>Initializes a new instance of the <see cref="SerialPortAdapter"/> class.</summary>
    /// <param name="serialPort">The serial port.</param>
    public SerialPortAdapter(SerialPortRx serialPort)
    {
        if (serialPort is null)
        {
            throw new ArgumentNullException(nameof(serialPort));
        }

        _serialPort = serialPort;
        _serialPort.NewLine = NewLine;
    }

    /// <summary>Gets indicates that no timeout should occur.</summary>
    public int InfiniteTimeout => SerialPort.InfiniteTimeout;

    /// <summary>Gets or sets the read-operation timeout in milliseconds.</summary>
    public int ReadTimeout
    {
        get => _serialPort.ReadTimeout;
        set => _serialPort.ReadTimeout = value;
    }

    /// <summary>Gets or sets the write-operation timeout in milliseconds.</summary>
    public int WriteTimeout
    {
        get => _serialPort.WriteTimeout;
        set => _serialPort.WriteTimeout = value;
    }

    /// <summary>Purges the receive buffer.</summary>
    public void DiscardInBuffer() => _serialPort.DiscardInBuffer();

    /// <summary>Reads bytes into a byte array at the specified offset.</summary>
    /// <param name="buffer">The byte array to write the input to.</param>
    /// <param name="offset">The offset in the buffer array to begin writing.</param>
    /// <param name="count">The number of bytes to read.</param>
    /// <returns>
    /// The number of bytes read.
    /// </returns>
    public Task<int> ReadAsync(byte[] buffer, int offset, int count) => _serialPort.ReadAsync(buffer, offset, count);

    /// <summary>Writes bytes from an output buffer, starting at the specified offset.</summary>
    /// <param name="buffer">The byte array that contains the data to write to the port.</param>
    /// <param name="offset">The offset in the buffer array to begin writing.</param>
    /// <param name="count">The number of bytes to write.</param>
    public void Write(byte[] buffer, int offset, int count) => _serialPort.Write(buffer, offset, count);

    /// <summary>Frees, releases, or resets unmanaged resources.</summary>
    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    /// <summary>Releases unmanaged and - optionally - managed resources.</summary>
    /// <param name="disposing">Whether to release managed as well as unmanaged resources.</param>
    protected virtual void Dispose(bool disposing)
    {
        if (!disposing)
        {
            return;
        }

        _serialPort.Dispose();
        _serialPort = null!;
    }
}
