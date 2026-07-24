// Copyright (c) 2019-2026 Chris Pulman and contributors. All rights reserved.
// Chris Pulman and contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

#if REACTIVE_SHIM
namespace IoT.DriverCore.Serial.Reactive;
#else
namespace IoT.DriverCore.Serial;
#endif

/// <summary>Implements a cohesive portion of the reactive serial port.</summary>
public partial class SerialPortRx
{
    /// <summary>Gets the connection observable that opens and wires the serial port.</summary>
    private IObservable<Unit> Connect => Observable.Create<Unit>(obs =>
    {
        var dis = new CompositeDisposable();

        // Check that the port exists
        if (_connectionFactory is null && !PortExists(PortName))
        {
            obs.OnError(new InvalidOperationException($"Serial Port {PortName} does not exist"));
            return dis;
        }

        ISerialPortConnection port;
        try
        {
            var newLine = NewLine;
            var handshake = Handshake;
            var readTimeout = ReadTimeout;
            var writeTimeout = WriteTimeout;
            var encoding = Encoding;
            port = _connectionFactory?.Invoke(this) ??
                new SystemSerialPortConnection(
                    new SerialPort(PortName, BaudRate, Parity, DataBits, StopBits)
                    {
                        NewLine = newLine,
                        Handshake = handshake,
                        ReadTimeout = readTimeout,
                        WriteTimeout = writeTimeout,
                        Encoding = encoding,
                        ReadBufferSize = _readBufferSize,
                        WriteBufferSize = _writeBufferSize,
                    });
        }
        catch (Exception ex)
        {
            obs.OnError(ex);
            return dis;
        }
#if HasWindows
        void OnPinChanged(object? _, SerialPinChangedEventArgs eventArgs) => _pinChanged.OnNext(eventArgs);
        port.PinChanged += OnPinChanged;
        dis.Add(Disposable.Create(() => port.PinChanged -= OnPinChanged));
#endif

        dis.Add(port);
        _serialPort = port;
        try
        {
            if (_connectionFactory is not null)
            {
                port.ReadBufferSize = _readBufferSize;
                port.WriteBufferSize = _writeBufferSize;
            }

            port.Open();
            port.BreakState = _breakState;
            port.DiscardNull = _discardNull;
            port.DtrEnable = _dtrEnable;
            port.ParityReplace = _parityReplace;
            port.ReceivedBytesThreshold = _receivedBytesThreshold;
            port.RtsEnable = _rtsEnable;
        }
        catch (Exception ex)
        {
            ReportError(ex);
            obs.OnError(ex);
            return dis;
        }

        TryPublishIsOpen(port.IsOpen);
        if (port.IsOpen)
        {
            obs.OnNext(Unit.Default);
        }

        // Clear any existing buffers
        if (IsOpen)
        {
            port.DiscardInBuffer();
            port.DiscardOutBuffer();
        }

        Thread.Sleep(OpenStabilizationDelayMilliseconds);

        // Subscribe to port errors
        void OnErrorReceived(object? _, SerialPortConnectionErrorEventArgs eventArgs) =>
            ReportError(eventArgs.Exception);
        port.ErrorReceived += OnErrorReceived;
        dis.Add(Disposable.Create(() => port.ErrorReceived -= OnErrorReceived));

        // Get the stream of data from the serial port using the DataReceived event
        // Only subscribe if EnableAutoDataReceive is true (allows sync reads when false)
        if (EnableAutoDataReceive)
        {
            var receiveBuffer = ArrayPool<byte>.Shared.Rent(Math.Max(1, ReadBufferSize));
            dis.Add(Disposable.Create(() => ArrayPool<byte>.Shared.Return(receiveBuffer)));
            void OnDataReceived(object? _, EventArgs __)
            {
                _ = __;
                try
                {
                    lock (_autoReceiveLock)
                    {
                        _ = SerialPortReceiveProcessor.DrainAndPublish(
                            () => port.BytesToRead,
                            receiveBuffer,
                            port.Read,
                            _dataReceivedBytes.OnNext,
                            _dataReceived.OnNext,
                            _dataReceivedBatches.OnNext);
                    }
                }
                catch (OperationCanceledException)
                {
                }
                catch (Exception ex)
                {
                    ReportError(ex);
                }
            }

            port.DataReceived += OnDataReceived;
            dis.Add(Disposable.Create(() => port.DataReceived -= OnDataReceived));
        }

        // setup Write streams
        dis.Add(_writeString.Subscribe(
            x =>
            {
                try
                {
                    port.Write(x);
                }
                catch (Exception ex)
                {
                    ReportError(ex);
                }
            },
            ReportError));
        dis.Add(_writeStringLine.Subscribe(
            x =>
            {
                try
                {
                    port.WriteLine(x);
                }
                catch (Exception ex)
                {
                    ReportError(ex);
                }
            },
            ReportError));
        dis.Add(_writeByte.Subscribe(
            x =>
            {
                try
                {
                    port.Write(x.ByteArray, x.Offset, x.Count);
                }
                catch (Exception ex)
                {
                    ReportError(ex);
                }
            },
            ReportError));
        dis.Add(_writeChar.Subscribe(
            x =>
            {
                try
                {
                    port.Write(x.CharArray, x.Offset, x.Count);
                }
                catch (Exception ex)
                {
                    ReportError(ex);
                }
            },
            ReportError));
        dis.Add(_discardInBuffer.Subscribe(
            _ =>
            {
                try
                {
                    port.DiscardInBuffer();
                }
                catch (Exception ex)
                {
                    ReportError(ex);
                }
            },
            ReportError));
        dis.Add(_discardOutBuffer.Subscribe(
            _ =>
            {
                try
                {
                    port.DiscardOutBuffer();
                }
                catch (Exception ex)
                {
                    ReportError(ex);
                }
            },
            ReportError));
        dis.Add(_readBytes
            .SelectMany(request => Observable.FromAsync(async () =>
            {
                await ProcessReadRequestAsync(request).ConfigureAwait(false);
                return Unit.Default;
            }))
            .Subscribe(_ => { }, ReportError));

        return Disposable.Create(() =>
        {
            if (IsDisposed)
            {
                return;
            }

            TryPublishIsOpen(false);
            _serialPort = null;
            dis.Dispose();
        });
    });

    /// <summary>Gets the port names using the default polling interval.</summary>
    /// <returns>Observable string.</returns>
    public static IObservable<string[]> PortNames() => PortNames(DefaultPortNamePollingIntervalMilliseconds, 0);

    /// <summary>Gets the port names.</summary>
    /// <param name="pollInterval">The poll interval.</param>
    /// <returns>Observable string.</returns>
    public static IObservable<string[]> PortNames(int pollInterval) => PortNames(pollInterval, 0);

    /// <summary>Gets the port names.</summary>
    /// <param name="pollInterval">The poll interval.</param>
    /// <param name="pollLimit">The poll limit, once number is reached observable will complete.</param>
    /// <returns>Observable string.</returns>
    /// <value>The port names.</value>
    public static IObservable<string[]> PortNames(int pollInterval, int pollLimit) =>
        Observable.Create<string[]>(obs =>
    {
        string[]? compare = null;
        var numberOfPolls = 0;
        var subscription = Observable.Interval(TimeSpan.FromMilliseconds(pollInterval)).Subscribe(_ =>
        {
            var compareNew = SerialPort.GetPortNames();
            if (compareNew.Length == 0)
            {
                compareNew = NoPorts;
            }

            if (compare is null)
            {
                compare = compareNew;
                obs.OnNext(compareNew);
            }

            if (compare?.SequenceEqual(compareNew) == false)
            {
                obs.OnNext(compareNew);
                compare = compareNew;
            }

            if (pollLimit <= 0)
            {
                return;
            }

            numberOfPolls++;
            if (numberOfPolls < pollLimit)
            {
                return;
            }

            obs.OnCompleted();
        });
        return Disposable.Create(() => subscription.Dispose());
    });

    /// <summary>Closes this instance.</summary>
    public void Close() => _disposablePort.Dispose();

    /// <summary>Discards the in buffer.</summary>
    public void DiscardInBuffer() => _discardInBuffer.OnNext(Unit.Default);

    /// <summary>Discards the out buffer.</summary>
    public void DiscardOutBuffer() => _discardOutBuffer.OnNext(Unit.Default);

    /// <summary>Releases owned resources.</summary>
    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    /// <summary>Opens this instance.</summary>
    /// <returns>
    /// A Task.
    /// </returns>
    public Task OpenAsync()
    {
        if (_disposablePort.IsDisposed)
        {
            _disposablePort = [];
        }

        if (_disposablePort.Count != 0)
        {
            return Task.CompletedTask;
        }

        var opened = new TaskCompletionSource<object?>(TaskCreationOptions.RunContinuationsAsynchronously);
        _disposablePort.Add(
            Connect.Subscribe(
                _ => opened.TrySetResult(null),
                ex =>
                {
                    ReportError(ex);
                    _ = opened.TrySetException(ex);
                },
                () =>
                {
                    if (!IsOpen)
                    {
                        return;
                    }

                    _ = opened.TrySetResult(null);
                }));

        return opened.Task;
    }

    /// <summary>Writes the specified text.</summary>
    /// <param name="text">The text.</param>
    public void Write(string text) =>
        _writeString.OnNext(text);

    /// <summary>Writes the specified byte array.</summary>
    /// <param name="buffer">The byte array.</param>
    /// <param name="offset">The offset.</param>
    /// <param name="count">The count.</param>
    public void Write(byte[]? buffer, int offset, int count)
    {
        ArgumentGuard.ThrowIfNull(buffer, nameof(buffer));
        _writeByte.OnNext((buffer, offset, count));
    }

    /// <summary>Writes the specified byte array.</summary>
    /// <param name="byteArray">The byte array.</param>
    public void Write(byte[] byteArray)
    {
#if NETFRAMEWORK
        if (byteArray is null)
        {
            throw new ArgumentNullException(nameof(byteArray));
        }
#else
        ArgumentGuard.ThrowIfNull(byteArray, nameof(byteArray));
#endif

        _writeByte.OnNext((byteArray, 0, byteArray.Length));
    }

    /// <summary>Writes the specified character array.</summary>
    /// <param name="charArray">The character array.</param>
    public void Write(char[] charArray)
    {
#if NETFRAMEWORK
        if (charArray is null)
        {
            throw new ArgumentNullException(nameof(charArray));
        }
#else
        ArgumentGuard.ThrowIfNull(charArray, nameof(charArray));
#endif

        _writeChar.OnNext((charArray, 0, charArray.Length));
    }

    /// <summary>Writes the specified character array.</summary>
    /// <param name="charArray">The character array.</param>
    /// <param name="offset">The offset.</param>
    /// <param name="count">The count.</param>
    public void Write(char[] charArray, int offset, int count) =>
        _writeChar.OnNext((charArray, offset, count));

#if !NETFRAMEWORK
    /// <summary>Writes the specified data from a ReadOnlySpan.</summary>
    /// <param name="data">The data to write.</param>
    public void Write(ReadOnlySpan<byte> data)
    {
        if (data.IsEmpty)
        {
            return;
        }

        var array = ArrayPool<byte>.Shared.Rent(data.Length);
        try
        {
            data.CopyTo(array);
            _writeByte.OnNext((array, 0, data.Length));
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(array);
        }
    }

    /// <summary>Writes the specified data from a ReadOnlyMemory.</summary>
    /// <param name="data">The data to write.</param>
    public void Write(ReadOnlyMemory<byte> data)
    {
        if (data.IsEmpty)
        {
            return;
        }

        Write(data.Span);
    }

    /// <summary>Writes the specified character data from a ReadOnlySpan.</summary>
    /// <param name="data">The character data to write.</param>
    public void Write(ReadOnlySpan<char> data)
    {
        if (data.IsEmpty)
        {
            return;
        }

        var array = ArrayPool<char>.Shared.Rent(data.Length);
        try
        {
            data.CopyTo(array);
            _writeChar.OnNext((array, 0, data.Length));
        }
        finally
        {
            ArrayPool<char>.Shared.Return(array);
        }
    }
#endif

    /// <summary>Writes the line.</summary>
    /// <param name="text">The text.</param>
    public void WriteLine(string text) =>
        _writeStringLine.OnNext(text);

    /// <summary>Reads the specified buffer.</summary>
    /// <param name="buffer">The buffer.</param>
    /// <param name="offset">The offset.</param>
    /// <param name="count">The count.</param>
    /// <returns>
    /// The number of bytes read.
    /// </returns>
    public async Task<int> ReadAsync(byte[]? buffer, int offset, int count)
    {
        ArgumentGuard.ThrowIfNull(buffer, nameof(buffer));
        EnsureOpen();
        var readTask = FirstValueAsync(_bytesRead);
        _readBytes.OnNext((buffer, offset, count));

        // Use timeout if configured, otherwise wait indefinitely
        if (ReadTimeout > 0)
        {
            var timeoutTask = Task.Delay(ReadTimeout);
            var completed = await Task.WhenAny(readTask, timeoutTask).ConfigureAwait(false);
            if (completed != readTask)
            {
                throw new TimeoutException("ReadAsync timed out.");
            }

            return await readTask.ConfigureAwait(false);
        }

        return await readTask.ConfigureAwait(false);
    }

    /// <summary>Reads bytes from the SerialPort input buffer into a byte array at the specified offset.</summary>
    /// <param name="buffer">The byte array to write the input to.</param>
    /// <param name="offset">The offset in the buffer array to begin writing.</param>
    /// <param name="count">The number of bytes to read.</param>
    /// <returns>The number of bytes read.</returns>
    public int Read(byte[] buffer, int offset, int count)
    {
#if NETFRAMEWORK
        if (buffer is null)
        {
            throw new ArgumentNullException(nameof(buffer));
        }
#else
        ArgumentGuard.ThrowIfNull(buffer, nameof(buffer));
#endif

        EnsureOpen();
        _readLock.Wait();
        try
        {
            for (var i = 0; i < count; i++)
            {
                var b = _serialPort.ReadByte();
                if (b == -1)
                {
                    throw new TimeoutException();
                }

                buffer[offset + i] = (byte)b;
            }

            return count;
        }
        finally
        {
            _ = _readLock.Release();
        }
    }

    /// <summary>Reads characters from the SerialPort input buffer into a character array.</summary>
    /// <param name="buffer">The character array to write the input to.</param>
    /// <param name="offset">The offset in the buffer array to begin writing.</param>
    /// <param name="count">The number of characters to read.</param>
    /// <returns>The number of characters read.</returns>
    public int Read(char[] buffer, int offset, int count)
    {
        EnsureOpen();
        _readLock.Wait();
        try
        {
            return _serialPort.Read(buffer, offset, count);
        }
        finally
        {
            _ = _readLock.Release();
        }
    }

    /// <summary>Synchronously reads one byte from the SerialPort input buffer.</summary>
    /// <returns>The byte, or -1 if no byte is available.</returns>
    public int ReadByte()
    {
        EnsureOpen();
        _readLock.Wait();
        try
        {
            return _serialPort.ReadByte();
        }
        finally
        {
            _ = _readLock.Release();
        }
    }

    /// <summary>Synchronously reads one character from the SerialPort input buffer.</summary>
    /// <returns>The character, or -1 if no character is available.</returns>
    public int ReadChar()
    {
        EnsureOpen();
        _readLock.Wait();
        try
        {
            return _serialPort.ReadChar();
        }
        finally
        {
            _ = _readLock.Release();
        }
    }

    /// <summary>Reads all immediately available encoded bytes from the SerialPort stream and input buffer.</summary>
    /// <returns>The contents of the input buffer and the stream.</returns>
    public string ReadExisting()
    {
        EnsureOpen();
        _readLock.Wait();
        try
        {
            return _serialPort.ReadExisting();
        }
        finally
        {
            _ = _readLock.Release();
        }
    }

    /// <summary>Reads up to the NewLine value in the input buffer.</summary>
    /// <returns>The contents of the input buffer up to the first occurrence of a NewLine value.</returns>
    public string ReadLine()
    {
        EnsureOpen();
        _readLock.Wait();
        try
        {
            return _serialPort.ReadLine();
        }
        finally
        {
            _ = _readLock.Release();
        }
    }

    /// <summary>Reads a string up to the specified value in the input buffer.</summary>
    /// <param name="value">The value to read up to.</param>
    /// <returns>The contents of the input buffer up to the specified value.</returns>
    public string ReadTo(string value)
    {
        EnsureOpen();
#if NETFRAMEWORK
        if (string.IsNullOrEmpty(value))
        {
            throw new ArgumentNullException(nameof(value));
        }
#else
        ArgumentException.ThrowIfNullOrEmpty(value);
#endif

        _readLock.Wait();
        try
        {
            var sb = new StringBuilder();
            while (true)
            {
                var c = _serialPort.ReadChar();
                if (c == -1)
                {
                    break;
                }

                _ = sb.Append((char)c);
                if (sb.Length >= value.Length && sb.ToString(sb.Length - value.Length, value.Length) == value)
                {
                    sb.Length -= value.Length;
                    break;
                }
            }

            return sb.ToString();
        }
        finally
        {
            _ = _readLock.Release();
        }
    }
}
