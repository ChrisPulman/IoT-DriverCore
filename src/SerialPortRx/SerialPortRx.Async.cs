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
    /// <summary>Reads the line asynchronous.</summary>
    /// <exception cref="InvalidOperationException">Serial port is not open.</exception>
    /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
    public Task<string> ReadLineAsync() => ReadLineAsync(CancellationToken.None);

    /// <summary>Reads the line asynchronous with cancellation and respecting ReadTimeout (> 0) as a timeout.</summary>
    /// <param name="cancellationToken">Cancellation token to cancel waiting.</param>
    /// <returns>A Task of string.</returns>
    public async Task<string> ReadLineAsync(CancellationToken cancellationToken)
    {
        EnsureOpen();

        await _readLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var tcs = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
            var sb = new StringBuilder(DefaultLineBuilderCapacity);
            var newLineLocal = NewLine ?? "\n";
            var newLineLength = newLineLocal.Length;
            var newLineFirstChar = newLineLocal[0];

            var subscription = DataReceived.Subscribe(
                ch =>
                {
                    _ = sb.Append(ch);
                    if (!TryExtractLine(sb, newLineLocal, newLineLength, newLineFirstChar, ch, out var line))
                    {
                        return;
                    }

                    _ = tcs.TrySetResult(line);
                },
                ex => _ = tcs.TrySetException(ex),
                () => _ = tcs.TrySetException(new InvalidOperationException(SerialPortNotOpenMessage)));

            using var cancellation = new ReadCancellationScope(
                ReadTimeout,
                tcs,
                "ReadLineAsync timed out.",
                cancellationToken);
            try
            {
                return await tcs.Task.ConfigureAwait(false);
            }
            finally
            {
                subscription.Dispose();
            }
        }
        finally
        {
            _ = _readLock.Release();
        }
    }

    /// <summary>Reads a string up to the specified value asynchronously.</summary>
    /// <param name="value">The value to read up to.</param>
    /// <returns>The contents of the input buffer up to the specified value.</returns>
    public Task<string> ReadToAsync(string value) => ReadToAsync(value, CancellationToken.None);

    /// <summary>Reads a string up to the specified value asynchronously.</summary>
    /// <param name="value">The value to read up to.</param>
    /// <param name="cancellationToken">Cancellation token to cancel waiting.</param>
    /// <returns>The contents of the input buffer up to the specified value.</returns>
    public async Task<string> ReadToAsync(string value, CancellationToken cancellationToken)
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

        await _readLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var tcs = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
            var sb = new StringBuilder(DelimiterBuilderCapacity);
            var valueLength = value.Length;

            var subscription = DataReceived.Subscribe(
                ch =>
                {
                    _ = sb.Append(ch);
                    if (sb.Length < valueLength || !TryMatchSuffix(sb, value, valueLength))
                    {
                        return;
                    }

                    sb.Length -= valueLength;
                    _ = tcs.TrySetResult(sb.ToString());
                },
                ex => _ = tcs.TrySetException(ex),
                () => _ = tcs.TrySetException(new InvalidOperationException(SerialPortNotOpenMessage)));

            using var cancellation = new ReadCancellationScope(
                ReadTimeout,
                tcs,
                "ReadToAsync timed out.",
                cancellationToken);
            try
            {
                return await tcs.Task.ConfigureAwait(false);
            }
            finally
            {
                subscription.Dispose();
            }
        }
        finally
        {
            _ = _readLock.Release();
        }
    }

    /// <summary>
    /// Starts continuous data reception that feeds both DataReceived and DataReceivedBytes observables.
    /// Call this after Open() to enable reactive data streaming.
    /// </summary>
    /// <returns>A disposable that stops the data reception when disposed.</returns>
    public IDisposable StartDataReception() => StartDataReception(DefaultReceivePollingIntervalMilliseconds);

    /// <summary>
    /// Starts continuous data reception that feeds both DataReceived and DataReceivedBytes observables.
    /// Call this after Open() to enable reactive data streaming.
    /// </summary>
    /// <param name="pollingIntervalMs">Polling interval in milliseconds (default: 10ms).</param>
    /// <returns>A disposable that stops the data reception when disposed.</returns>
    public IDisposable StartDataReception(int pollingIntervalMs)
    {
        if (!IsOpen)
        {
            throw new InvalidOperationException("Serial port must be open before starting data reception.");
        }

        return new DataReceptionLifetime(this, pollingIntervalMs);
    }

    /// <summary>Releases unmanaged and - optionally - managed resources.</summary>
    /// <param name="disposing">
    /// <c>true</c> to release both managed and unmanaged resources; <c>false</c> to release only
    /// unmanaged resources.
    /// </param>
    protected virtual void Dispose(bool disposing)
    {
        if (IsDisposed)
        {
            return;
        }

        if (disposing)
        {
            _disposablePort?.Dispose();
            _isOpenValue.Dispose();
            _dataReceived.Dispose();
            _dataReceivedBytes.Dispose();
            _dataReceivedBatches.Dispose();
            _errors.Dispose();
            _writeByte.Dispose();
            _writeChar.Dispose();
            _writeString.Dispose();
            _writeStringLine.Dispose();
            _discardInBuffer.Dispose();
            _discardOutBuffer.Dispose();
            _readBytes.Dispose();
            _bytesRead.Dispose();
            _bytesReceived.Dispose();
            _readLock.Dispose();
            _pinChanged.Dispose();
        }

        IsDisposed = true;
    }
}
