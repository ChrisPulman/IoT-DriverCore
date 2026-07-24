// Copyright (c) 2019-2026 Chris Pulman and contributors. All rights reserved.
// Chris Pulman and contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

#if REACTIVE_SHIM
namespace IoT.DriverCore.Serial.Reactive;
#else
namespace IoT.DriverCore.Serial;
#endif

/// <summary>Implements one endpoint of a deterministic in-memory serial link.</summary>
internal sealed class InMemorySerialPortConnection : ISerialPortConnection
{
    /// <summary>The received byte queue.</summary>
    private readonly Queue<byte> _received = new();

    /// <summary>The shared duplex link.</summary>
    private readonly InMemorySerialLink _link;

    /// <summary>The endpoint side.</summary>
    private readonly int _side;

    /// <summary>Synchronizes receive queue access.</summary>
    private readonly object _sync = new();

    /// <summary>The optional deterministic notification raised immediately before an indefinite read waits.</summary>
    private readonly Action? _beforeIndefiniteReadWait;

    /// <summary>Tracks whether the connection has been disposed.</summary>
    private bool _disposed;

    /// <summary>Initializes a new instance of the <see cref="InMemorySerialPortConnection"/> class.</summary>
    /// <param name="link">The shared duplex link.</param>
    /// <param name="side">The endpoint side.</param>
    /// <param name="encoding">The text encoding.</param>
    /// <param name="newLine">The line terminator.</param>
    /// <param name="readTimeout">The read timeout.</param>
    /// <param name="beforeIndefiniteReadWait">An optional deterministic notification raised before an indefinite wait.</param>
    internal InMemorySerialPortConnection(
        InMemorySerialLink link,
        int side,
        Encoding encoding,
        string newLine,
        int readTimeout,
        Action? beforeIndefiniteReadWait = null)
    {
        _link = link;
        _side = side;
        Encoding = encoding;
        NewLine = newLine;
        ReadTimeout = readTimeout;
        _beforeIndefiniteReadWait = beforeIndefiniteReadWait;
    }

    /// <inheritdoc/>
    public event EventHandler? DataReceived;

    /// <inheritdoc/>
    public event EventHandler<SerialPortConnectionErrorEventArgs>? ErrorReceived;

#if HasWindows
    /// <inheritdoc/>
    public event EventHandler<SerialPinChangedEventArgs>? PinChanged
    {
        add { }
        remove { }
    }
#endif

    /// <inheritdoc/>
    public bool BreakState { get; set; }

    /// <inheritdoc/>
    public int BytesToRead
    {
        get
        {
            lock (_sync)
            {
                return _received.Count;
            }
        }
    }

    /// <inheritdoc/>
    public int BytesToWrite => 0;

    /// <inheritdoc/>
    public bool CDHolding => _link.IsPeerOpen(_side);

    /// <inheritdoc/>
    public bool CtsHolding => _link.IsPeerOpen(_side);

    /// <inheritdoc/>
    public bool DiscardNull { get; set; }

    /// <inheritdoc/>
    public bool DsrHolding => _link.IsPeerOpen(_side);

    /// <inheritdoc/>
    public bool DtrEnable { get; set; }

    /// <inheritdoc/>
    public Encoding Encoding { get; }

    /// <inheritdoc/>
    public bool IsOpen { get; private set; }

    /// <inheritdoc/>
    public string NewLine { get; }

    /// <inheritdoc/>
    public byte ParityReplace { get; set; } = 63;

    /// <inheritdoc/>
    public int ReadBufferSize { get; set; } = 4096;

    /// <inheritdoc/>
    public int ReadTimeout { get; }

    /// <inheritdoc/>
    public int ReceivedBytesThreshold { get; set; } = 1;

    /// <inheritdoc/>
    public bool RtsEnable { get; set; }

    /// <inheritdoc/>
    public int WriteBufferSize { get; set; } = 2048;

    /// <inheritdoc/>
    public void DiscardInBuffer()
    {
        lock (_sync)
        {
            _received.Clear();
        }
    }

    /// <inheritdoc/>
    public void DiscardOutBuffer()
    {
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        IsOpen = false;
        _link.Unregister(_side, this);
        lock (_sync)
        {
            _received.Clear();
            Monitor.PulseAll(_sync);
        }

        _disposed = true;
    }

    /// <inheritdoc/>
    public void Open()
    {
        if (_disposed)
        {
            throw new ObjectDisposedException(nameof(InMemorySerialPortConnection));
        }

        _link.Register(_side, this);
        IsOpen = true;
    }

    /// <inheritdoc/>
    public int Read(byte[] buffer, int offset, int count)
    {
        ArgumentGuard.ThrowIfNull(buffer, nameof(buffer));
        SerialPortBufferGuard.Validate(buffer.Length, offset, count);
        if (count == 0)
        {
            return 0;
        }

        var firstByte = (byte)ReadByte();
        lock (_sync)
        {
            buffer[offset] = firstByte;
            var bytesRead = 1;
            while (bytesRead < count && _received.Count > 0)
            {
                buffer[offset + bytesRead] = _received.Dequeue();
                bytesRead++;
            }

            return bytesRead;
        }
    }

    /// <inheritdoc/>
    public int Read(char[] buffer, int offset, int count)
    {
        ArgumentGuard.ThrowIfNull(buffer, nameof(buffer));
        SerialPortBufferGuard.Validate(buffer.Length, offset, count);
        for (var index = 0; index < count; index++)
        {
            buffer[offset + index] = (char)ReadByte();
        }

        return count;
    }

    /// <inheritdoc/>
    public int ReadByte()
    {
        lock (_sync)
        {
            EnsureOpen();
            if (_received.Count > 0)
            {
                return _received.Dequeue();
            }

            var signaled = ReadTimeout > 0
                ? Monitor.Wait(_sync, ReadTimeout)
                : WaitIndefinitely();
            if (!signaled || _received.Count == 0)
            {
                throw new TimeoutException();
            }

            return _received.Dequeue();
        }
    }

    /// <inheritdoc/>
    public int ReadChar() => ReadByte();

    /// <inheritdoc/>
    public string ReadExisting()
    {
        byte[] bytes;
        lock (_sync)
        {
            EnsureOpen();
            bytes = _received.ToArray();
            _received.Clear();
        }

        return Encoding.GetString(bytes);
    }

    /// <inheritdoc/>
    public string ReadLine()
    {
        var builder = new StringBuilder();
        while (true)
        {
            _ = builder.Append((char)ReadChar());
            if (builder.Length < NewLine.Length)
            {
                continue;
            }

            if (!string.Equals(
                    builder.ToString(builder.Length - NewLine.Length, NewLine.Length),
                    NewLine,
                    StringComparison.Ordinal))
            {
                continue;
            }

            builder.Length -= NewLine.Length;
            return builder.ToString();
        }
    }

    /// <inheritdoc/>
    public void Write(byte[] buffer, int offset, int count)
    {
        ArgumentGuard.ThrowIfNull(buffer, nameof(buffer));
        SerialPortBufferGuard.Validate(buffer.Length, offset, count);
        EnsureOpen();
        var batch = new byte[count];
        Buffer.BlockCopy(buffer, offset, batch, 0, count);
        _link.Deliver(_side, batch);
    }

    /// <inheritdoc/>
    public void Write(char[] buffer, int offset, int count)
    {
        ArgumentGuard.ThrowIfNull(buffer, nameof(buffer));
        SerialPortBufferGuard.Validate(buffer.Length, offset, count);
        WriteBytes(Encoding.GetBytes(buffer, offset, count));
    }

    /// <inheritdoc/>
    public void Write(string value)
    {
        ArgumentGuard.ThrowIfNull(value, nameof(value));
        WriteBytes(Encoding.GetBytes(value));
    }

    /// <inheritdoc/>
    public void WriteLine(string value)
    {
        ArgumentGuard.ThrowIfNull(value, nameof(value));
        Write(value + NewLine);
    }

    /// <summary>Receives an immutable byte batch from the opposite endpoint.</summary>
    /// <param name="batch">The byte batch.</param>
    internal void Receive(byte[] batch)
    {
        bool shouldNotify;
        lock (_sync)
        {
            if (!IsOpen)
            {
                return;
            }

            foreach (var value in batch)
            {
                if (!DiscardNull || value != 0)
                {
                    _received.Enqueue(value);
                }
            }

            shouldNotify = _received.Count >= ReceivedBytesThreshold;
            Monitor.PulseAll(_sync);
        }

        if (!shouldNotify)
        {
            return;
        }

        DataReceived?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>Publishes a deterministic connection error.</summary>
    /// <param name="exception">The error to publish.</param>
    internal void InjectError(Exception exception) =>
        ErrorReceived?.Invoke(this, new SerialPortConnectionErrorEventArgs(exception));

    /// <summary>Ensures the connection is open.</summary>
    private void EnsureOpen()
    {
        if (IsOpen)
        {
            return;
        }

        throw new InvalidOperationException("The in-memory serial endpoint is not open.");
    }

    /// <summary>Waits for deterministic input without imposing a transport timeout.</summary>
    /// <returns><see langword="true"/> when the wait was signaled.</returns>
    private bool WaitIndefinitely()
    {
        _beforeIndefiniteReadWait?.Invoke();
        return Monitor.Wait(_sync);
    }

    /// <summary>Writes an entire byte array.</summary>
    /// <param name="bytes">The bytes to write.</param>
    private void WriteBytes(byte[] bytes) => Write(bytes, 0, bytes.Length);
}
