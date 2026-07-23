// Copyright (c) 2019-2026 Chris Pulman and contributors. All rights reserved.
// Chris Pulman and contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

#if REACTIVE_SHIM
namespace IoT.DriverCore.Serial.Reactive;
#else
namespace IoT.DriverCore.Serial;
#endif

/// <summary>Abstracts the byte-oriented connection used by <see cref="SerialPortRx"/>.</summary>
internal interface ISerialPortConnection : IDisposable
{
    /// <summary>Occurs when bytes become available to read.</summary>
    event EventHandler? DataReceived;

    /// <summary>Occurs when the connection reports an error.</summary>
    event EventHandler<SerialPortConnectionErrorEventArgs>? ErrorReceived;

#if HasWindows
    /// <summary>Occurs when a serial pin changes.</summary>
    event EventHandler<SerialPinChangedEventArgs>? PinChanged;
#endif

    /// <summary>Gets or sets the break state.</summary>
    bool BreakState { get; set; }

    /// <summary>Gets the number of bytes available to read.</summary>
    int BytesToRead { get; }

    /// <summary>Gets the number of bytes waiting to be written.</summary>
    int BytesToWrite { get; }

    /// <summary>Gets a value indicating whether carrier detect is active.</summary>
    bool CDHolding { get; }

    /// <summary>Gets a value indicating whether clear-to-send is active.</summary>
    bool CtsHolding { get; }

    /// <summary>Gets or sets a value indicating whether null bytes are discarded.</summary>
    bool DiscardNull { get; set; }

    /// <summary>Gets a value indicating whether data-set-ready is active.</summary>
    bool DsrHolding { get; }

    /// <summary>Gets or sets a value indicating whether data-terminal-ready is enabled.</summary>
    bool DtrEnable { get; set; }

    /// <summary>Gets the text encoding.</summary>
    Encoding Encoding { get; }

    /// <summary>Gets a value indicating whether the connection is open.</summary>
    bool IsOpen { get; }

    /// <summary>Gets the configured line terminator.</summary>
    string NewLine { get; }

    /// <summary>Gets or sets the parity replacement byte.</summary>
    byte ParityReplace { get; set; }

    /// <summary>Gets or sets the read buffer size.</summary>
    int ReadBufferSize { get; set; }

    /// <summary>Gets the configured read timeout.</summary>
    int ReadTimeout { get; }

    /// <summary>Gets or sets the received-byte notification threshold.</summary>
    int ReceivedBytesThreshold { get; set; }

    /// <summary>Gets or sets a value indicating whether request-to-send is enabled.</summary>
    bool RtsEnable { get; set; }

    /// <summary>Gets or sets the write buffer size.</summary>
    int WriteBufferSize { get; set; }

    /// <summary>Discards bytes waiting in the receive buffer.</summary>
    void DiscardInBuffer();

    /// <summary>Discards bytes waiting in the send buffer.</summary>
    void DiscardOutBuffer();

    /// <summary>Opens the connection.</summary>
    void Open();

    /// <summary>Reads bytes into a buffer segment.</summary>
    /// <param name="buffer">The target buffer.</param>
    /// <param name="offset">The target offset.</param>
    /// <param name="count">The maximum number of bytes to read.</param>
    /// <returns>The number of bytes read.</returns>
    int Read(byte[] buffer, int offset, int count);

    /// <summary>Reads characters into a buffer segment.</summary>
    /// <param name="buffer">The target buffer.</param>
    /// <param name="offset">The target offset.</param>
    /// <param name="count">The number of characters to read.</param>
    /// <returns>The number of characters read.</returns>
    int Read(char[] buffer, int offset, int count);

    /// <summary>Reads one byte.</summary>
    /// <returns>The byte value.</returns>
    int ReadByte();

    /// <summary>Reads one character.</summary>
    /// <returns>The character value.</returns>
    int ReadChar();

    /// <summary>Reads all immediately available text.</summary>
    /// <returns>The available text.</returns>
    string ReadExisting();

    /// <summary>Reads one terminated line.</summary>
    /// <returns>The line without its terminator.</returns>
    string ReadLine();

    /// <summary>Writes a byte-array segment.</summary>
    /// <param name="buffer">The source buffer.</param>
    /// <param name="offset">The source offset.</param>
    /// <param name="count">The number of bytes to write.</param>
    void Write(byte[] buffer, int offset, int count);

    /// <summary>Writes a character-array segment.</summary>
    /// <param name="buffer">The source buffer.</param>
    /// <param name="offset">The source offset.</param>
    /// <param name="count">The number of characters to write.</param>
    void Write(char[] buffer, int offset, int count);

    /// <summary>Writes text.</summary>
    /// <param name="value">The text to write.</param>
    void Write(string value);

    /// <summary>Writes text followed by the configured line terminator.</summary>
    /// <param name="value">The text to write.</param>
    void WriteLine(string value);
}
