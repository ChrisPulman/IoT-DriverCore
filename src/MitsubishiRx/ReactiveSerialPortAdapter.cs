// Copyright (c) 2019-2026 Chris Pulman and contributors. All rights reserved.
// Chris Pulman and contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.IO.Ports;
#if REACTIVE_SHIM
using PortRx = IoT.DriverCore.Serial.Reactive.SerialPortRx;
#else
using PortRx = IoT.DriverCore.Serial.SerialPortRx;
#endif

#if REACTIVE_SHIM

namespace IoT.DriverCore.MitsubishiRx.Reactive;

#else

namespace IoT.DriverCore.MitsubishiRx;

#endif

/// <summary>Provides the ReactiveSerialPortAdapter type.</summary>
internal sealed class ReactiveSerialPortAdapter : IDisposable
{
    /// <summary>Stores the serialPort field.</summary>
    private readonly PortRx _serialPort;

    /// <summary>Stores the writes field.</summary>
    private readonly Signal<byte[]> _writes = new();

    /// <summary>Initializes a new instance of the ReactiveSerialPortAdapter class.</summary>
    /// <param name="options">The options parameter.</param>
    public ReactiveSerialPortAdapter(MitsubishiSerialOptions options)
        : this(options, null)
    {
    }

    /// <summary>Initializes a new instance of the <see cref="ReactiveSerialPortAdapter"/> class over an optional injected serial endpoint.</summary>
    /// <param name="options">The serial options.</param>
    /// <param name="serialPort">The optional deterministic serial endpoint.</param>
    internal ReactiveSerialPortAdapter(MitsubishiSerialOptions options, PortRx? serialPort)
    {
        ArgumentNullException.ThrowIfNull(options);
        _serialPort = serialPort
            ?? new(
                options.PortName,
                options.BaudRate,
                options.DataBits,
                options.Parity,
                options.StopBits,
                options.Handshake);
        _serialPort.NewLine = options.NewLine;
        _serialPort.ReadBufferSize = options.ReadBufferSize;
        _serialPort.WriteBufferSize = options.WriteBufferSize;
        _serialPort.ReceivedBytesThreshold = 1;
        _serialPort.EnableAutoDataReceive = true;
        _serialPort.ReadTimeout = SerialPort.InfiniteTimeout;
        _serialPort.WriteTimeout = SerialPort.InfiniteTimeout;
    }

    /// <summary>Gets or sets the IsOpen property.</summary>
    internal bool IsOpen => _serialPort.IsOpen;

    /// <summary>Gets or sets the ReceivedBytes property.</summary>
    internal IObservable<byte[]> ReceivedBytes =>
        _serialPort.DataReceivedBytes.Select(static value => new byte[] { value });

    /// <summary>Gets or sets the WrittenBytes property.</summary>
    internal IObservable<byte[]> WrittenBytes => _writes.AsObservable();

    /// <summary>Executes the Dispose operation.</summary>
    public void Dispose()
    {
        try
        {
            _serialPort.Close();
        }
        catch (ObjectDisposedException) { }
        catch (IOException) { }
        catch (InvalidOperationException) { }

        _serialPort.Dispose();
        _writes.OnCompleted();
        _writes.Dispose();
    }

    /// <summary>Executes the OpenAsync operation.</summary>
    /// <returns>The OpenAsync operation result.</returns>
    internal Task OpenAsync() => _serialPort.OpenAsync();

    /// <summary>Executes the Close operation.</summary>
    internal void Close() => _serialPort.Close();

    /// <summary>Executes the DiscardInBuffer operation.</summary>
    internal void DiscardInBuffer() => _serialPort.DiscardInBuffer();

    /// <summary>Executes the DiscardOutBuffer operation.</summary>
    internal void DiscardOutBuffer() => _serialPort.DiscardOutBuffer();

    /// <summary>Executes the Write operation.</summary>
    /// <param name="buffer">The buffer parameter.</param>
    internal void Write(byte[] buffer)
    {
        ArgumentNullException.ThrowIfNull(buffer);
        _serialPort.Write(buffer, 0, buffer.Length);
        _writes.OnNext(buffer.ToArray());
    }

    /// <summary>Executes the ReadAsync operation.</summary>
    /// <param name="buffer">The buffer parameter.</param>
    /// <param name="offset">The offset parameter.</param>
    /// <param name="count">The count parameter.</param>
    /// <returns>The ReadAsync operation result.</returns>
    internal Task<int> ReadAsync(byte[] buffer, int offset, int count) =>
        _serialPort.ReadAsync(buffer, offset, count);
}
