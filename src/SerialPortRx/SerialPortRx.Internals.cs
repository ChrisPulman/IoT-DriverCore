// Copyright (c) 2019-2026 Chris Pulman and contributors. All rights reserved.
// Chris Pulman and contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

#if REACTIVE_SHIM
namespace CP.IO.Ports.Reactive;
#else
namespace CP.IO.Ports;
#endif

/// <summary>Implements a cohesive portion of the reactive serial port.</summary>
public partial class SerialPortRx
{
    /// <summary>Attempts to extract a complete line from the StringBuilder, optimized for common cases.</summary>
    /// <param name="sb">The buffer containing received characters.</param>
    /// <param name="newLine">The configured line terminator.</param>
    /// <param name="newLineLength">The configured line terminator length.</param>
    /// <param name="newLineFirstChar">The first character in the configured line terminator.</param>
    /// <param name="lastChar">The last character appended to the buffer.</param>
    /// <param name="line">The extracted line when a complete line is found.</param>
    /// <returns>
    /// <see langword="true"/> when a complete line was extracted; otherwise, <see langword="false"/>.
    /// </returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool TryExtractLine(
        StringBuilder sb,
        string newLine,
        int newLineLength,
        char newLineFirstChar,
        char lastChar,
        out string line)
    {
        line = string.Empty;

        // Fast path for single-character newline (most common: \n)
        if (newLineLength == 1)
        {
            if (lastChar == newLineFirstChar)
            {
                sb.Length--;
                line = sb.ToString();
                _ = sb.Clear();
                return true;
            }

            return false;
        }

        // Multi-character newline handling
        if (sb.Length < newLineLength)
        {
            return false;
        }

        // Fast path for two-character newline (\r\n)
        if (newLineLength == TwoCharacterNewLineLength)
        {
            var len = sb.Length;
            if (sb[len - TwoCharacterNewLineLength] == newLine[0] && sb[len - 1] == newLine[1])
            {
                sb.Length -= TwoCharacterNewLineLength;
                line = sb.ToString();
                _ = sb.Clear();
                return true;
            }

            return false;
        }

        // General case for longer newlines
        return TryExtractLineGeneral(sb, newLine, newLineLength, out line);
    }

    /// <summary>General case line extraction for newlines longer than 2 characters.</summary>
    /// <param name="sb">The buffer containing received characters.</param>
    /// <param name="newLine">The configured line terminator.</param>
    /// <param name="newLineLength">The configured line terminator length.</param>
    /// <param name="line">The extracted line when a complete line is found.</param>
    /// <returns>
    /// <see langword="true"/> when a complete line was extracted; otherwise, <see langword="false"/>.
    /// </returns>
    private static bool TryExtractLineGeneral(StringBuilder sb, string newLine, int newLineLength, out string line)
    {
        line = string.Empty;
        var start = sb.Length - newLineLength;

        // Use stackalloc for small newlines, ArrayPool for larger ones
        if (newLineLength <= DefaultLineBuilderCapacity)
        {
            Span<char> tail = stackalloc char[newLineLength];
            for (var i = 0; i < newLineLength; i++)
            {
                tail[i] = sb[start + i];
            }

            if (tail.SequenceEqual(newLine.AsSpan()))
            {
                sb.Length -= newLineLength;
                line = sb.ToString();
                _ = sb.Clear();
                return true;
            }
        }
        else
        {
            var buffer = ArrayPool<char>.Shared.Rent(newLineLength);
            try
            {
                var tail = buffer.AsSpan(0, newLineLength);
                for (var i = 0; i < newLineLength; i++)
                {
                    tail[i] = sb[start + i];
                }

                if (tail.SequenceEqual(newLine.AsSpan()))
                {
                    sb.Length -= newLineLength;
                    line = sb.ToString();
                    _ = sb.Clear();
                    return true;
                }
            }
            finally
            {
                ArrayPool<char>.Shared.Return(buffer);
            }
        }

        return false;
    }

    /// <summary>Checks if the StringBuilder ends with the specified value.</summary>
    /// <param name="sb">The buffer to inspect.</param>
    /// <param name="value">The suffix value to match.</param>
    /// <param name="valueLength">The suffix value length.</param>
    /// <returns>
    /// <see langword="true"/> when the buffer ends with the value; otherwise, <see langword="false"/>.
    /// </returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool TryMatchSuffix(StringBuilder sb, string value, int valueLength)
    {
        var start = sb.Length - valueLength;
        for (var i = 0; i < valueLength; i++)
        {
            if (sb[start + i] != value[i])
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>Determines whether a serial port name is available on the system.</summary>
    /// <param name="portName">The port name to find.</param>
    /// <returns><see langword="true"/> when the port exists; otherwise, <see langword="false"/>.</returns>
    private static bool PortExists(string portName)
    {
        foreach (var name in SerialPort.GetPortNames())
        {
            if (name.Equals(portName, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Returns the first value from an observable sequence as a task.</summary>
    /// <typeparam name="T">The observed value type.</typeparam>
    /// <param name="source">The source observable.</param>
    /// <returns>A task that completes with the first observed value.</returns>
    private static Task<T> FirstValueAsync<T>(IObservable<T> source)
    {
#if REACTIVE_SHIM
        return source.Take(1).ToTask();
#else
        return source.Take(1).FirstAsync();
#endif
    }

    /// <summary>Ensures the serial port is currently open.</summary>
    [System.Diagnostics.CodeAnalysis.MemberNotNull(nameof(_serialPort))]
    private void EnsureOpen()
    {
        if (_serialPort is { IsOpen: true })
        {
            return;
        }

        throw new InvalidOperationException(SerialPortNotOpenMessage);
    }

    /// <summary>Publishes a serial port error if the instance can still accept notifications.</summary>
    /// <param name="exception">The exception to publish.</param>
    private void ReportError(Exception exception)
    {
        if (IsDisposed)
        {
            return;
        }

        try
        {
            _errors.OnNext(exception);
        }
        catch (ObjectDisposedException)
        {
        }
        catch (InvalidOperationException)
        {
        }
    }

    /// <summary>Publishes the current open state if the instance can still accept notifications.</summary>
    /// <param name="isOpen">The open state to publish.</param>
    private void TryPublishIsOpen(bool isOpen)
    {
        if (IsDisposed)
        {
            return;
        }

        try
        {
            _isOpenValue.OnNext(isOpen);
        }
        catch (ObjectDisposedException)
        {
        }
        catch (InvalidOperationException)
        {
        }
    }

    /// <summary>Creates and caches a thread-safe observable.</summary>
    /// <typeparam name="T">The observed value type.</typeparam>
    /// <param name="cached">The cached observable reference.</param>
    /// <param name="subject">The backing subject to cache.</param>
    /// <returns>The cached observable.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private IObservable<T> GetOrCreateCachedObservable<T>(ref IObservable<T>? cached, IObservable<T> subject)
    {
        var current = Volatile.Read(ref cached);
        if (current is not null)
        {
            return current;
        }

        lock (_observableCacheLock)
        {
            current = Volatile.Read(ref cached);
            if (current is not null)
            {
                return current;
            }

            Volatile.Write(ref cached, subject);
            return subject;
        }
    }

    /// <summary>Creates a cached observable from a factory that is thread-safe.</summary>
    /// <typeparam name="T">The observed value type.</typeparam>
    /// <param name="cached">The cached observable reference.</param>
    /// <param name="factory">The observable factory to invoke when no cached value exists.</param>
    /// <returns>The cached observable.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private IObservable<T> GetOrCreateCachedObservable<T>(ref IObservable<T>? cached, Func<IObservable<T>> factory)
    {
        var current = Volatile.Read(ref cached);
        if (current is not null)
        {
            return current;
        }

        lock (_observableCacheLock)
        {
            current = Volatile.Read(ref cached);
            if (current is not null)
            {
                return current;
            }

            var observable = factory();
            Volatile.Write(ref cached, observable);
            return observable;
        }
    }

    /// <summary>Creates a cached async observable that is thread-safe.</summary>
    /// <typeparam name="T">The observed value type.</typeparam>
    /// <param name="cached">The cached async observable reference.</param>
    /// <param name="source">The source observable to adapt and cache.</param>
    /// <returns>The cached async observable.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private IObservableAsync<T> GetOrCreateCachedAsyncObservable<T>(
        ref IObservableAsync<T>? cached,
        IObservable<T> source)
    {
        var current = Volatile.Read(ref cached);
        if (current is not null)
        {
            return current;
        }

        lock (_observableCacheLock)
        {
            current = Volatile.Read(ref cached);
            if (current is not null)
            {
                return current;
            }

            var observable = ObservableAsyncBridgeExtensions.ToAsyncObservable(source);
            Volatile.Write(ref cached, observable);
            return observable;
        }
    }

    /// <summary>Creates the optimized lines observable with efficient line parsing.</summary>
    /// <returns>A cached line observable sequence.</returns>
    private IObservable<string> CreateLinesObservable() =>
        Observable.Defer(() =>
            Observable.Create<string>(obs =>
            {
                var sb = new StringBuilder(DefaultLineBuilderCapacity);
                var newLineLocal = NewLine ?? "\n";
                var newLineLength = newLineLocal.Length;
                var newLineFirstChar = newLineLocal[0];

                return DataReceived.Subscribe(
                    ch =>
                    {
                        _ = sb.Append(ch);
                        if (!TryExtractLine(sb, newLineLocal, newLineLength, newLineFirstChar, ch, out var line))
                        {
                            return;
                        }

                        obs.OnNext(line);
                    },
                    obs.OnError);
            }));

    /// <summary>Processes a queued asynchronous read request.</summary>
    /// <param name="request">The requested target buffer segment.</param>
    /// <returns>A task representing the read operation.</returns>
    private async Task ProcessReadRequestAsync(
        (byte[] Buffer, int Offset, int Count) request)
    {
        await _readLock.WaitAsync().ConfigureAwait(false);
        try
        {
            // Yield to avoid blocking the OnNext caller's thread.
            await Task.Yield();

            var port = _serialPort;
            if (port is null || port.BytesToRead == 0)
            {
                _bytesRead.OnNext(0);
                return;
            }

            var bytesToRead = Math.Min(port.BytesToRead, request.Count);
            var bytesRead = port.Read(request.Buffer, request.Offset, bytesToRead);
            for (var index = 0; index < bytesRead; index++)
            {
                _bytesReceived.OnNext(request.Buffer[request.Offset + index]);
            }

            _bytesRead.OnNext(bytesRead);
        }
        catch (TimeoutException)
        {
            _bytesRead.OnNext(0);
        }
        catch (Exception exception)
        {
            ReportError(exception);
        }
        finally
        {
            _ = _readLock.Release();
        }
    }

    /// <summary>Creates an error observable that suppresses duplicate message values.</summary>
    /// <returns>An observable sequence of distinct serial port errors.</returns>
    private IObservable<Exception> CreateDistinctErrorObservable() => Observable.Create<Exception>(observer =>
    {
        var seenMessages = new HashSet<string>(StringComparer.Ordinal);
        return _errors.Subscribe(
            exception =>
            {
                if (!seenMessages.Add(exception.Message))
                {
                    return;
                }

                observer.OnNext(exception);
            },
            observer.OnError,
            observer.OnCompleted);
    });

    /// <summary>Runs the background data reception loop until cancellation or port closure.</summary>
    /// <param name="pollingIntervalMs">The polling delay used when no bytes are available.</param>
    /// <param name="cancellationToken">The token used to stop reception.</param>
    /// <returns>A task that completes when the data reception loop stops.</returns>
    private async Task RunDataReceptionLoopAsync(int pollingIntervalMs, CancellationToken cancellationToken)
    {
        var buffer = ArrayPool<byte>.Shared.Rent(ReadBufferSize);
        try
        {
            while (!cancellationToken.IsCancellationRequested && IsOpen)
            {
                try
                {
                    if (!await TryReadAvailableDataAsync(buffer, cancellationToken).ConfigureAwait(false))
                    {
                        await Task.Delay(pollingIntervalMs, cancellationToken).ConfigureAwait(false);
                    }
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    _errors.OnNext(ex);
                }
            }
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(buffer);
        }
    }

    /// <summary>Reads and publishes any bytes currently available on the serial port.</summary>
    /// <param name="buffer">The reusable receive buffer.</param>
    /// <param name="cancellationToken">The token used to cancel lock acquisition.</param>
    /// <returns>
    /// <see langword="true"/> when data was available to process; otherwise, <see langword="false"/>.
    /// </returns>
    private async Task<bool> TryReadAvailableDataAsync(byte[] buffer, CancellationToken cancellationToken)
    {
        var port = _serialPort;
        if (port is null || port.BytesToRead <= 0)
        {
            return false;
        }

        await _readLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            _ = SerialPortReceiveProcessor.DrainAndPublish(
                () => port.BytesToRead,
                buffer,
                port.Read,
                _dataReceivedBytes.OnNext,
                _dataReceived.OnNext,
                _dataReceivedBatches.OnNext);
            return true;
        }
        finally
        {
            _ = _readLock.Release();
        }
    }

    /// <summary>Coordinates caller cancellation with the configured serial read timeout.</summary>
    private sealed class ReadCancellationScope : IDisposable
    {
        /// <summary>The completion source canceled or faulted by this scope.</summary>
        private readonly TaskCompletionSource<string> _completion;

        /// <summary>The linked cancellation source, when both caller cancellation and a timeout are active.</summary>
        private readonly CancellationTokenSource? _linkedCancellation;

        /// <summary>The registration that observes the effective cancellation token.</summary>
        private readonly CancellationTokenRegistration _registration;

        /// <summary>The timeout cancellation source, when a positive timeout is configured.</summary>
        private readonly CancellationTokenSource? _timeoutCancellation;

        /// <summary>The exception message used when the timeout expires.</summary>
        private readonly string _timeoutMessage;

        /// <summary>Initializes a new instance of the <see cref="ReadCancellationScope"/> class.</summary>
        /// <param name="timeoutMilliseconds">The configured serial read timeout.</param>
        /// <param name="completion">The completion source to cancel or fault.</param>
        /// <param name="timeoutMessage">The exception message used when the timeout expires.</param>
        /// <param name="callerToken">The caller-provided cancellation token.</param>
        public ReadCancellationScope(
            int timeoutMilliseconds,
            TaskCompletionSource<string> completion,
            string timeoutMessage,
            CancellationToken callerToken)
        {
            _completion = completion;
            _timeoutMessage = timeoutMessage;

            if (timeoutMilliseconds > 0)
            {
                _timeoutCancellation = new(timeoutMilliseconds);
                if (callerToken.CanBeCanceled)
                {
                    _linkedCancellation = CancellationTokenSource.CreateLinkedTokenSource(
                        callerToken,
                        _timeoutCancellation.Token);
                }
            }

            Token = _linkedCancellation?.Token ?? _timeoutCancellation?.Token ?? callerToken;
            _registration = Token.Register(CancelCompletion);
        }

        /// <summary>Gets the effective cancellation token.</summary>
        private CancellationToken Token { get; }

        /// <inheritdoc/>
        public void Dispose()
        {
            _registration.Dispose();
            _linkedCancellation?.Dispose();
            _timeoutCancellation?.Dispose();
        }

        /// <summary>Cancels or faults the completion source for the effective cancellation reason.</summary>
        private void CancelCompletion()
        {
            if (_timeoutCancellation?.IsCancellationRequested == true)
            {
                _ = _completion.TrySetException(new TimeoutException(_timeoutMessage));
                return;
            }

            _ = _completion.TrySetCanceled(Token);
        }
    }

    /// <summary>Owns the cancellation lifetime for a background data reception loop.</summary>
    private sealed class DataReceptionLifetime : IDisposable
    {
        /// <summary>Signals cancellation to the owned receive loop.</summary>
        private readonly CancellationTokenSource _cancellation = new();

        /// <summary>Initializes a new instance of the <see cref="DataReceptionLifetime"/> class.</summary>
        /// <param name="owner">The serial port wrapper that owns the receive loop.</param>
        /// <param name="pollingIntervalMilliseconds">The idle polling delay.</param>
        public DataReceptionLifetime(SerialPortRx owner, int pollingIntervalMilliseconds)
        {
            _ = Task.Run(
                () => owner.RunDataReceptionLoopAsync(pollingIntervalMilliseconds, _cancellation.Token),
                _cancellation.Token);
        }

        /// <inheritdoc/>
        public void Dispose()
        {
            _cancellation.Cancel();
            _cancellation.Dispose();
        }
    }
}
