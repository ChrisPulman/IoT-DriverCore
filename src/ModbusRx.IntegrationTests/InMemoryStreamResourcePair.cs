// Copyright (c) 2019-2026 Chris Pulman and contributors. All rights reserved.
// Chris Pulman and contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using IoT.DriverCore.ModbusRx.IO;

namespace IoT.DriverCore.ModbusRx.IntegrationTests;

/// <summary>Owns two deterministic duplex stream resources for serial protocol integration tests.</summary>
internal sealed class InMemoryStreamResourcePair : IDisposable
{
    /// <summary>Signals that the simulated slave endpoint has recovered from a timed-out read.</summary>
    private readonly TaskCompletionSource _secondReadTimeoutRecovery = new(
        TaskCreationOptions.RunContinuationsAsynchronously);

    /// <summary>Initializes a new instance of the <see cref="InMemoryStreamResourcePair"/> class.</summary>
    internal InMemoryStreamResourcePair()
    {
        var firstInbound = Channel.CreateUnbounded<byte>();
        var secondInbound = Channel.CreateUnbounded<byte>();
        First = new InMemoryStreamResource(firstInbound.Reader, secondInbound.Writer);
        Second = new InMemoryStreamResource(
            secondInbound.Reader,
            firstInbound.Writer,
            () => _secondReadTimeoutRecovery.TrySetResult());
    }

    /// <summary>Gets the first duplex endpoint.</summary>
    internal IStreamResource First { get; }

    /// <summary>Gets the second duplex endpoint.</summary>
    internal IStreamResource Second { get; }

    /// <summary>Gets a task that completes after the simulated slave clears a timed-out frame.</summary>
    internal Task SecondReadTimeoutRecovery => _secondReadTimeoutRecovery.Task;

    /// <inheritdoc />
    public void Dispose()
    {
        First.Dispose();
        Second.Dispose();
    }

    /// <summary>A deterministic channel-backed stream resource.</summary>
    private sealed class InMemoryStreamResource : IStreamResource
    {
        /// <summary>Receives bytes written by the peer endpoint.</summary>
        private readonly ChannelReader<byte> _reader;

        /// <summary>Publishes bytes to the peer endpoint.</summary>
        private readonly ChannelWriter<byte> _writer;

        /// <summary>Notifies the owner when this endpoint times out while reading.</summary>
        private readonly Action? _onReadTimeout;

        /// <summary>Cancels a pending read when this endpoint is disposed.</summary>
        private readonly CancellationTokenSource _disposeCancellation = new();

        /// <summary>Tracks a timeout until the corresponding malformed frame has been discarded.</summary>
        private int _readTimedOut;

        /// <summary>Tracks whether the endpoint has been disposed.</summary>
        private bool _disposed;

        /// <summary>Initializes a new instance of the <see cref="InMemoryStreamResource"/> class.</summary>
        /// <param name="reader">The inbound byte reader.</param>
        /// <param name="writer">The peer byte writer.</param>
        /// <param name="onReadTimeout">Optional callback invoked after a timed-out read.</param>
        internal InMemoryStreamResource(
            ChannelReader<byte> reader,
            ChannelWriter<byte> writer,
            Action? onReadTimeout = null)
        {
            _reader = reader;
            _writer = writer;
            _onReadTimeout = onReadTimeout;
        }

        /// <inheritdoc />
        public int InfiniteTimeout => Timeout.Infinite;

        /// <inheritdoc />
        public int ReadTimeout { get; set; } = Timeout.Infinite;

        /// <inheritdoc />
        public int WriteTimeout { get; set; } = Timeout.Infinite;

        /// <inheritdoc />
        public void DiscardInBuffer()
        {
            ThrowIfDisposed();
            while (_reader.TryRead(out var discarded))
            {
                _ = discarded;
            }

            if (Interlocked.Exchange(ref _readTimedOut, 0) == 0)
            {
                return;
            }

            _onReadTimeout?.Invoke();
        }

        /// <inheritdoc />
        public async Task<int> ReadAsync(byte[] buffer, int offset, int count)
        {
            ArgumentNullException.ThrowIfNull(buffer);
            ThrowIfDisposed();
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(_disposeCancellation.Token);
            if (ReadTimeout > 0)
            {
                timeout.CancelAfter(ReadTimeout);
            }

            try
            {
                buffer[offset] = await _reader.ReadAsync(timeout.Token);
                var bytesRead = 1;
                while (bytesRead < count && _reader.TryRead(out var value))
                {
                    buffer[offset + bytesRead] = value;
                    bytesRead++;
                }

                return bytesRead;
            }
            catch (OperationCanceledException) when (!_disposeCancellation.IsCancellationRequested)
            {
                _ = Interlocked.Exchange(ref _readTimedOut, 1);
                throw new TimeoutException("The in-memory serial read timed out.");
            }
        }

        /// <inheritdoc />
        public void Write(byte[] buffer, int offset, int count)
        {
            ArgumentNullException.ThrowIfNull(buffer);
            ThrowIfDisposed();
            for (var index = 0; index < count; index++)
            {
                if (!_writer.TryWrite(buffer[offset + index]))
                {
                    throw new InvalidOperationException("The in-memory serial peer is unavailable.");
                }
            }
        }

        /// <inheritdoc />
        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            _disposeCancellation.Cancel();
            _disposeCancellation.Dispose();
        }

        /// <summary>Throws when this endpoint has already been disposed.</summary>
        private void ThrowIfDisposed()
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
        }
    }
}
