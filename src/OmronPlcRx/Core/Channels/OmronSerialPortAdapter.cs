// Copyright (c) 2019-2026 Chris Pulman and contributors. All rights reserved.
// Chris Pulman and contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System;
using System.Threading.Tasks;
#if REACTIVE_SHIM
using SerialPortRx = IoT.DriverCore.Serial.Reactive.SerialPortRx;
#else
using SerialPortRx = IoT.DriverCore.Serial.SerialPortRx;
#endif

#if REACTIVE_SHIM
namespace IoT.DriverCore.OmronPlcRx.Reactive.Core.Channels;
#else
namespace IoT.DriverCore.OmronPlcRx.Core.Channels;
#endif

/// <summary>Adapts the production serial implementation to the Omron serial channel contract.</summary>
internal sealed class OmronSerialPortAdapter : IOmronSerialPort
{
    /// <summary>Stores the wrapped production serial port.</summary>
    private readonly SerialPortRx _port;

    /// <summary>Initializes a new instance of the <see cref="OmronSerialPortAdapter"/> class.</summary>
    /// <param name="options">Serial connection options.</param>
    /// <param name="timeout">Read and write timeout in milliseconds.</param>
    internal OmronSerialPortAdapter(OmronSerialOptions options, int timeout)
    {
        if (options is null)
        {
            throw new ArgumentNullException(nameof(options));
        }

        _port = new(
            options.PortName,
            options.BaudRate,
            options.DataBits,
            options.Parity,
            options.StopBits,
            options.Handshake)
        {
            EnableAutoDataReceive = false,
            ReadTimeout = timeout,
            WriteTimeout = timeout,
            ReceivedBytesThreshold = 1,
            NewLine = "\r",
            RtsEnable = options.RtsEnable,
            DtrEnable = options.DtrEnable,
        };
    }

    /// <summary>Initializes a new instance of the <see cref="OmronSerialPortAdapter"/> class.</summary>
    /// <param name="port">Production serial port to compose.</param>
    internal OmronSerialPortAdapter(SerialPortRx port) =>
        _port = port ?? throw new ArgumentNullException(nameof(port));

    /// <inheritdoc />
    public int BytesToRead => _port.BytesToRead;

    /// <inheritdoc />
    public bool RtsEnable
    {
        get => _port.RtsEnable;
        set => _port.RtsEnable = value;
    }

    /// <inheritdoc />
    public bool DtrEnable
    {
        get => _port.DtrEnable;
        set => _port.DtrEnable = value;
    }

    /// <inheritdoc />
    public Task OpenAsync() => _port.OpenAsync();

    /// <inheritdoc />
    public void Close() => _port.Close();

    /// <inheritdoc />
    public void DiscardInBuffer() => _port.DiscardInBuffer();

    /// <inheritdoc />
    public void Write(byte[] buffer, int offset, int count) => _port.Write(buffer, offset, count);

    /// <inheritdoc />
    public int Read(byte[] buffer, int offset, int count) => _port.Read(buffer, offset, count);

    /// <inheritdoc />
    public void Dispose() => _port.Dispose();
}
