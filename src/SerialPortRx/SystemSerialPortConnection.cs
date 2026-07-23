// Copyright (c) 2019-2026 Chris Pulman and contributors. All rights reserved.
// Chris Pulman and contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

#if REACTIVE_SHIM
namespace IoT.DriverCore.Serial.Reactive;
#else
namespace IoT.DriverCore.Serial;
#endif

/// <summary>Adapts <see cref="SerialPort"/> to <see cref="ISerialPortConnection"/>.</summary>
internal sealed class SystemSerialPortConnection : ISerialPortConnection
{
    /// <summary>The wrapped system serial port.</summary>
    private readonly SerialPort _port;

    /// <summary>Initializes a new instance of the <see cref="SystemSerialPortConnection"/> class.</summary>
    /// <param name="port">The wrapped serial port.</param>
    internal SystemSerialPortConnection(SerialPort port)
    {
        _port = port;
        _port.DataReceived += OnDataReceived;
        _port.ErrorReceived += OnErrorReceived;
#if HasWindows
        _port.PinChanged += OnPinChanged;
#endif
    }

    /// <inheritdoc/>
    public event EventHandler? DataReceived;

    /// <inheritdoc/>
    public event EventHandler<SerialPortConnectionErrorEventArgs>? ErrorReceived;

#if HasWindows
    /// <inheritdoc/>
    public event EventHandler<SerialPinChangedEventArgs>? PinChanged;
#endif

    /// <inheritdoc/>
    public bool BreakState { get => _port.BreakState; set => _port.BreakState = value; }

    /// <inheritdoc/>
    public int BytesToRead => _port.BytesToRead;

    /// <inheritdoc/>
    public int BytesToWrite => _port.BytesToWrite;

    /// <inheritdoc/>
    public bool CDHolding => _port.CDHolding;

    /// <inheritdoc/>
    public bool CtsHolding => _port.CtsHolding;

    /// <inheritdoc/>
    public bool DiscardNull { get => _port.DiscardNull; set => _port.DiscardNull = value; }

    /// <inheritdoc/>
    public bool DsrHolding => _port.DsrHolding;

    /// <inheritdoc/>
    public bool DtrEnable { get => _port.DtrEnable; set => _port.DtrEnable = value; }

    /// <inheritdoc/>
    public Encoding Encoding => _port.Encoding;

    /// <inheritdoc/>
    public bool IsOpen => _port.IsOpen;

    /// <inheritdoc/>
    public string NewLine => _port.NewLine;

    /// <inheritdoc/>
    public byte ParityReplace { get => _port.ParityReplace; set => _port.ParityReplace = value; }

    /// <inheritdoc/>
    public int ReadBufferSize { get => _port.ReadBufferSize; set => _port.ReadBufferSize = value; }

    /// <inheritdoc/>
    public int ReadTimeout => _port.ReadTimeout;

    /// <inheritdoc/>
    public int ReceivedBytesThreshold
    {
        get => _port.ReceivedBytesThreshold;
        set => _port.ReceivedBytesThreshold = value;
    }

    /// <inheritdoc/>
    public bool RtsEnable { get => _port.RtsEnable; set => _port.RtsEnable = value; }

    /// <inheritdoc/>
    public int WriteBufferSize { get => _port.WriteBufferSize; set => _port.WriteBufferSize = value; }

    /// <inheritdoc/>
    public void DiscardInBuffer() => _port.DiscardInBuffer();

    /// <inheritdoc/>
    public void DiscardOutBuffer() => _port.DiscardOutBuffer();

    /// <inheritdoc/>
    public void Dispose()
    {
        _port.DataReceived -= OnDataReceived;
        _port.ErrorReceived -= OnErrorReceived;
#if HasWindows
        _port.PinChanged -= OnPinChanged;
#endif
        _port.Dispose();
    }

    /// <inheritdoc/>
    public void Open() => _port.Open();

    /// <inheritdoc/>
    public int Read(byte[] buffer, int offset, int count) => _port.Read(buffer, offset, count);

    /// <inheritdoc/>
    public int Read(char[] buffer, int offset, int count)
    {
        return _port.Read(buffer, offset, count);
    }

    /// <inheritdoc/>
    public int ReadByte() => _port.ReadByte();

    /// <inheritdoc/>
    public int ReadChar() => _port.ReadChar();

    /// <inheritdoc/>
    public string ReadExisting() => _port.ReadExisting();

    /// <inheritdoc/>
    public string ReadLine() => _port.ReadLine();

    /// <inheritdoc/>
    public void Write(byte[] buffer, int offset, int count) => _port.Write(buffer, offset, count);

    /// <inheritdoc/>
    public void Write(char[] buffer, int offset, int count)
    {
        _port.Write(buffer, offset, count);
    }

    /// <inheritdoc/>
    public void Write(string value) => _port.Write(value);

    /// <inheritdoc/>
    public void WriteLine(string value) => _port.WriteLine(value);

    /// <summary>Forwards the system data-received event.</summary>
    /// <param name="sender">The system serial port.</param>
    /// <param name="eventArgs">The event arguments.</param>
    private void OnDataReceived(object sender, SerialDataReceivedEventArgs eventArgs) =>
        DataReceived?.Invoke(this, EventArgs.Empty);

    /// <summary>Converts the system error event to an exception.</summary>
    /// <param name="sender">The system serial port.</param>
    /// <param name="eventArgs">The event arguments.</param>
    private void OnErrorReceived(object sender, SerialErrorReceivedEventArgs eventArgs) =>
        ErrorReceived?.Invoke(
            this,
            new SerialPortConnectionErrorEventArgs(
                new InvalidOperationException(eventArgs.EventType.ToString())));

#if HasWindows
    /// <summary>Forwards the system pin-changed event.</summary>
    /// <param name="sender">The system serial port.</param>
    /// <param name="eventArgs">The event arguments.</param>
    private void OnPinChanged(object sender, SerialPinChangedEventArgs eventArgs) => PinChanged?.Invoke(this, eventArgs);
#endif
}
