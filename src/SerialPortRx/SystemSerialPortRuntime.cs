// Copyright (c) 2019-2026 Chris Pulman and contributors. All rights reserved.
// Chris Pulman and contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

#if REACTIVE_SHIM
namespace IoT.DriverCore.Serial.Reactive;
#else
namespace IoT.DriverCore.Serial;
#endif

/// <summary>Production event adapter for <see cref="SerialPort"/>.</summary>
internal sealed class SystemSerialPortRuntime : ISerialPortRuntime
{
    /// <summary>The adapted system serial port.</summary>
    private readonly SerialPort _port;

    /// <summary>Initializes a new instance of the <see cref="SystemSerialPortRuntime"/> class.</summary>
    /// <param name="port">The system serial port whose events are adapted.</param>
    internal SystemSerialPortRuntime(SerialPort port)
    {
        _port = port;
        _port.DataReceived += OnDataReceived;
        _port.ErrorReceived += OnErrorReceived;
    }

    /// <inheritdoc/>
    public event EventHandler? DataReceived;

    /// <inheritdoc/>
    public event EventHandler<SerialPortConnectionErrorEventArgs>? ErrorReceived;

    /// <inheritdoc/>
    public void Dispose()
    {
        _port.DataReceived -= OnDataReceived;
        _port.ErrorReceived -= OnErrorReceived;
    }

    /// <summary>Forwards an available-data notification.</summary>
    /// <param name="sender">The system serial port that raised the notification.</param>
    /// <param name="eventArgs">The serial data event arguments.</param>
    private void OnDataReceived(object sender, SerialDataReceivedEventArgs eventArgs) => DataReceived?.Invoke(this, EventArgs.Empty);

    /// <summary>Converts a system error notification.</summary>
    /// <param name="sender">The system serial port that raised the notification.</param>
    /// <param name="eventArgs">The serial error event arguments.</param>
    private void OnErrorReceived(object sender, SerialErrorReceivedEventArgs eventArgs) =>
        ErrorReceived?.Invoke(this, new SerialPortConnectionErrorEventArgs(new InvalidOperationException(eventArgs.EventType.ToString())));
}
