// Copyright (c) 2019-2026 Chris Pulman and contributors. All rights reserved.
// Chris Pulman and contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

#if REACTIVE_SHIM
using IoT.DriverCore.ModbusRx.Reactive.Device;
#else
using IoT.DriverCore.ModbusRx.Device;
#endif

#if REACTIVE_SHIM
namespace IoT.DriverCore.ModbusRx.Reactive.IO;
#else
namespace IoT.DriverCore.ModbusRx.IO;
#endif

/// <summary>Provides a complete in-memory byte-stream connection to a <see cref="ModbusSimulator"/>.</summary>
internal sealed class InMemoryModbusStreamResource : IStreamResource
{
    /// <summary>Serializes response state.</summary>
    private readonly Lock _lock = new();

    /// <summary>Stores the connected simulator.</summary>
    private readonly ModbusSimulator _simulator;

    /// <summary>Stores response bytes that have not yet been consumed.</summary>
    private readonly Queue<byte> _responseBytes = new();

    /// <summary>Stores whether this resource has been disposed.</summary>
    private int _disposed;

    /// <summary>Stores the delay before the next response read.</summary>
    private TimeSpan _pendingDelay;

    /// <summary>Stores a scripted read exception.</summary>
    private Exception? _readException;

    /// <summary>Initializes a new instance of the <see cref="InMemoryModbusStreamResource"/> class.</summary>
    /// <param name="simulator">The simulator that processes complete request frames.</param>
    internal InMemoryModbusStreamResource(ModbusSimulator simulator) =>
        _simulator = simulator ?? throw new ArgumentNullException(nameof(simulator));

    /// <inheritdoc/>
    public int InfiniteTimeout => Timeout.Infinite;

    /// <inheritdoc/>
    public int ReadTimeout { get; set; } = Timeout.Infinite;

    /// <inheritdoc/>
    public int WriteTimeout { get; set; } = Timeout.Infinite;

    /// <inheritdoc/>
    public void DiscardInBuffer()
    {
        ThrowIfDisposed();

        lock (_lock)
        {
            _responseBytes.Clear();
            _readException = null;
            _pendingDelay = TimeSpan.Zero;
        }
    }

    /// <inheritdoc/>
    public async Task<int> ReadAsync(byte[] buffer, int offset, int count)
    {
        ValidateBuffer(buffer, offset, count);
        ThrowIfDisposed();

        TimeSpan delay;
        lock (_lock)
        {
            delay = _pendingDelay;
            _pendingDelay = TimeSpan.Zero;
        }

        if (delay > TimeSpan.Zero)
        {
            await Task.Delay(delay).ConfigureAwait(false);
        }

        lock (_lock)
        {
            ThrowIfDisposed();

            if (_readException is { } readException)
            {
                _readException = null;
                throw readException;
            }

            var bytesToRead = Math.Min(count, _responseBytes.Count);
            for (var i = 0; i < bytesToRead; i++)
            {
                buffer[offset + i] = _responseBytes.Dequeue();
            }

            return bytesToRead;
        }
    }

    /// <inheritdoc/>
    public void Write(byte[] buffer, int offset, int count)
    {
        ValidateBuffer(buffer, offset, count);
        ThrowIfDisposed();

        var requestFrame = new byte[count];
        Array.Copy(buffer, offset, requestFrame, 0, count);
        var response = _simulator.ProcessFrame(requestFrame);

        lock (_lock)
        {
            ThrowIfDisposed();
            _responseBytes.Clear();
            _readException = response.ReadException;
            _pendingDelay = response.Delay;

            if (response.Frame is null)
            {
                return;
            }

            foreach (var value in response.Frame)
            {
                _responseBytes.Enqueue(value);
            }
        }
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0)
        {
            return;
        }

        lock (_lock)
        {
            _responseBytes.Clear();
            _readException = null;
            _pendingDelay = TimeSpan.Zero;
        }
    }

    /// <summary>Validates a buffer segment.</summary>
    /// <param name="buffer">The buffer.</param>
    /// <param name="offset">The segment offset.</param>
    /// <param name="count">The segment length.</param>
    private static void ValidateBuffer(byte[] buffer, int offset, int count)
    {
        _ = buffer ?? throw new ArgumentNullException(nameof(buffer));
        _ = offset < 0 || offset > buffer.Length
            ? throw new ArgumentOutOfRangeException(nameof(offset))
            : 0;
        _ = count < 0 || count > buffer.Length - offset
            ? throw new ArgumentOutOfRangeException(nameof(count))
            : 0;
    }

    /// <summary>Throws when this resource is disposed.</summary>
    private void ThrowIfDisposed() =>
        _ = Volatile.Read(ref _disposed) != 0
            ? throw new ObjectDisposedException(nameof(InMemoryModbusStreamResource))
            : 0;
}
