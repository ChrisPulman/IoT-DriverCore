// Copyright (c) 2019-2026 Chris Pulman and contributors. All rights reserved.
// Chris Pulman and contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace IoT.DriverCore.S7PlcRx.Mock;

/// <summary>Provides deterministic, byte-addressable S7 memory.</summary>
public sealed class ManagedS7Memory
{
    /// <summary>Synchronizes memory access and registration.</summary>
    private readonly object _syncRoot = new();

    /// <summary>Stores registered memory areas.</summary>
    private readonly Dictionary<(S7MemoryArea Area, ushort DbNumber), byte[]> _areas = [];

    /// <summary>Occurs after bytes are written.</summary>
    public event EventHandler<S7MemoryChangedEventArgs>? Changed;

    /// <summary>Registers an existing buffer as a memory area.</summary>
    /// <param name="area">The area.</param>
    /// <param name="dbNumber">The DB number.</param>
    /// <param name="buffer">The backing buffer.</param>
    public void Register(S7MemoryArea area, ushort dbNumber, byte[] buffer)
    {
        if (buffer is null)
        {
            throw new ArgumentNullException(nameof(buffer));
        }

        lock (_syncRoot)
        {
            _areas[(area, NormalizeDbNumber(area, dbNumber))] = buffer;
        }
    }

    /// <summary>Creates and registers a memory area.</summary>
    /// <param name="area">The area.</param>
    /// <param name="dbNumber">The DB number.</param>
    /// <param name="size">The size in bytes.</param>
    /// <returns>The registered buffer.</returns>
    public byte[] Register(S7MemoryArea area, ushort dbNumber, int size)
    {
        if (size <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(size));
        }

        var buffer = new byte[size];
        Register(area, dbNumber, buffer);
        return buffer;
    }

    /// <summary>Removes a registered memory area.</summary>
    /// <param name="area">The area.</param>
    /// <param name="dbNumber">The DB number.</param>
    /// <returns><see langword="true"/> when an area was removed.</returns>
    public bool Unregister(S7MemoryArea area, ushort dbNumber)
    {
        lock (_syncRoot)
        {
            return _areas.Remove((area, NormalizeDbNumber(area, dbNumber)));
        }
    }

    /// <summary>Gets a registered backing buffer.</summary>
    /// <param name="area">The area.</param>
    /// <param name="dbNumber">The DB number.</param>
    /// <returns>The backing buffer.</returns>
    public byte[] GetBuffer(S7MemoryArea area, ushort dbNumber)
    {
        lock (_syncRoot)
        {
            return _areas.TryGetValue((area, NormalizeDbNumber(area, dbNumber)), out var buffer)
                ? buffer
                : throw new KeyNotFoundException($"S7 area {area}, DB {dbNumber} is not registered.");
        }
    }

    /// <summary>Reads a byte range.</summary>
    /// <param name="area">The area.</param>
    /// <param name="dbNumber">The DB number.</param>
    /// <param name="offset">The byte offset.</param>
    /// <param name="length">The number of bytes.</param>
    /// <returns>A copy of the requested bytes.</returns>
    public byte[] Read(S7MemoryArea area, ushort dbNumber, int offset, int length)
    {
        lock (_syncRoot)
        {
            var buffer = GetBufferCore(area, dbNumber);
            ValidateRange(buffer, offset, length);
            var result = new byte[length];
            Buffer.BlockCopy(buffer, offset, result, 0, length);
            return result;
        }
    }

    /// <summary>Writes a byte range.</summary>
    /// <param name="area">The area.</param>
    /// <param name="dbNumber">The DB number.</param>
    /// <param name="offset">The byte offset.</param>
    /// <param name="data">The bytes to write.</param>
    public void Write(S7MemoryArea area, ushort dbNumber, int offset, byte[] data)
    {
        if (data is null)
        {
            throw new ArgumentNullException(nameof(data));
        }

        byte[] snapshot;
        lock (_syncRoot)
        {
            var buffer = GetBufferCore(area, dbNumber);
            ValidateRange(buffer, offset, data.Length);
            Buffer.BlockCopy(data, 0, buffer, offset, data.Length);
            snapshot = (byte[])data.Clone();
        }

        Changed?.Invoke(this, new(area, NormalizeDbNumber(area, dbNumber), offset, snapshot));
    }

    /// <summary>Normalizes non-DB area numbers to zero.</summary>
    /// <param name="area">The S7 memory area.</param>
    /// <param name="dbNumber">The requested DB number.</param>
    /// <returns>The DB number used to address the backing store.</returns>
    private static ushort NormalizeDbNumber(S7MemoryArea area, ushort dbNumber) =>
        area == S7MemoryArea.DataBlock ? dbNumber : (ushort)0;

    /// <summary>Gets a registered buffer without taking the lock.</summary>
    /// <param name="area">The S7 memory area.</param>
    /// <param name="dbNumber">The DB number.</param>
    /// <returns>The registered backing buffer.</returns>
    private byte[] GetBufferCore(S7MemoryArea area, ushort dbNumber) =>
        _areas.TryGetValue((area, NormalizeDbNumber(area, dbNumber)), out var buffer)
            ? buffer
            : throw new KeyNotFoundException($"S7 area {area}, DB {dbNumber} is not registered.");

    /// <summary>Validates a byte range.</summary>
    /// <param name="buffer">The registered backing buffer.</param>
    /// <param name="offset">The requested byte offset.</param>
    /// <param name="length">The requested byte count.</param>
    private void ValidateRange(byte[] buffer, int offset, int length)
    {
        if (offset >= 0 && length >= 0 && offset <= buffer.Length - length)
        {
            return;
        }

        throw new ArgumentOutOfRangeException(nameof(offset), "The requested range is outside the S7 area.");
    }
}
