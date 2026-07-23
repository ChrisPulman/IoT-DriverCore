// Copyright (c) 2019-2026 Chris Pulman and contributors. All rights reserved.
// Chris Pulman and contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

#if NET8_0_OR_GREATER
using System.Buffers;
#endif

#if REACTIVE_SHIM
namespace IoT.DriverCore.ModbusRx.Reactive.IO;
#else
namespace IoT.DriverCore.ModbusRx.IO;
#endif

/// <summary>High-performance buffer manager for Modbus message processing with cross-platform compatibility.</summary>
public sealed class ModbusBufferManager : IDisposable
{
#if NET8_0_OR_GREATER
    /// <summary>Stores the byte Pool value.</summary>
    private readonly ArrayPool<byte> _bytePool;

    /// <summary>Stores the ushort Pool value.</summary>
    private readonly ArrayPool<ushort> _ushortPool;

    /// <summary>Stores the bool Pool value.</summary>
    private readonly ArrayPool<bool> _boolPool;
#endif
    /// <summary>Stores the lock value.</summary>
    private readonly Lock _lock = new();

    /// <summary>Stores the disposed value.</summary>
    private bool _disposed;

    /// <summary>Stores successful buffer rents.</summary>
    private long _rentOperations;

    /// <summary>Stores successful buffer returns.</summary>
    private long _returnOperations;

    /// <summary>Stores arrays allocated on platforms without an array pool.</summary>
    private long _dedicatedAllocations;

    /// <summary>Stores tracked copy calls.</summary>
    private long _copyOperations;

    /// <summary>Stores elements copied by tracked copy calls.</summary>
    private long _copiedElements;

    /// <summary>Initializes a new instance of the <see cref="ModbusBufferManager"/> class.</summary>
    public ModbusBufferManager()
    {
#if NET8_0_OR_GREATER
        _bytePool = ArrayPool<byte>.Shared;
        _ushortPool = ArrayPool<ushort>.Shared;
        _boolPool = ArrayPool<bool>.Shared;
#endif
        _dedicatedAllocations = 0;
    }

    /// <summary>Copies data efficiently between arrays.</summary>
    /// <typeparam name="T">The type of data to copy.</typeparam>
    /// <param name="source">The source array.</param>
    /// <param name="sourceIndex">The source index.</param>
    /// <param name="destination">The destination array.</param>
    /// <param name="destinationIndex">The destination index.</param>
    /// <param name="length">The length to copy.</param>
    /// <returns>The number of elements copied.</returns>
    public static int CopyData<T>(T[] source, int sourceIndex, T[] destination, int destinationIndex, int length)
    {
        if (source is null || destination is null)
        {
            return 0;
        }

        var copyCount = Math.Min(length, Math.Min(source.Length - sourceIndex, destination.Length - destinationIndex));
        if (copyCount <= 0)
        {
            return 0;
        }

        Array.Copy(source, sourceIndex, destination, destinationIndex, copyCount);
        return copyCount;
    }

    /// <summary>Performs a high-performance comparison between two arrays.</summary>
    /// <typeparam name="T">The type of data to compare.</typeparam>
    /// <param name="array1">The first array.</param>
    /// <param name="array2">The second array.</param>
    /// <returns>True if the arrays are equal in content.</returns>
    public static bool CompareArrays<T>(T[] array1, T[] array2)
        where T : IEquatable<T>
    {
        if (array1 is null && array2 is null)
        {
            return true;
        }

        if (array1 is null || array2 is null)
        {
            return false;
        }

        if (array1.Length != array2.Length)
        {
            return false;
        }

        for (var i = 0; i < array1.Length; i++)
        {
            if (!array1[i].Equals(array2[i]))
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>Clears an array with high performance.</summary>
    /// <typeparam name="T">The type of data to clear.</typeparam>
    /// <param name="array">The array to clear.</param>
    public static void ClearArray<T>(T[] array)
    {
        if (array is null)
        {
            return;
        }

        Array.Clear(array, 0, array.Length);
    }

    /// <summary>Copies data and records deterministic operation and element-copy counts.</summary>
    /// <typeparam name="T">The type of data to copy.</typeparam>
    /// <param name="source">The source array.</param>
    /// <param name="sourceIndex">The source index.</param>
    /// <param name="destination">The destination array.</param>
    /// <param name="destinationIndex">The destination index.</param>
    /// <param name="length">The requested element count.</param>
    /// <returns>The number of copied elements.</returns>
    public int CopyDataAndTrack<T>(T[] source, int sourceIndex, T[] destination, int destinationIndex, int length)
    {
        var copied = CopyData(source, sourceIndex, destination, destinationIndex, length);
        _ = Interlocked.Increment(ref _copyOperations);
        _ = Interlocked.Add(ref _copiedElements, copied);
        return copied;
    }

    /// <summary>Gets a deterministic snapshot of buffer-manager work.</summary>
    /// <returns>The current operation counters.</returns>
    public ModbusBufferMetrics GetMetrics() => new(
        Interlocked.Read(ref _rentOperations),
        Interlocked.Read(ref _returnOperations),
        Interlocked.Read(ref _dedicatedAllocations),
        Interlocked.Read(ref _copyOperations),
        Interlocked.Read(ref _copiedElements));

    /// <summary>Rents a byte buffer from the pool or creates a new one.</summary>
    /// <param name="minimumLength">The minimum length required.</param>
    /// <returns>A rented buffer that should be returned when finished.</returns>
    public byte[] RentByteBuffer(int minimumLength)
    {
        lock (_lock)
        {
            if (_disposed)
            {
                throw new ObjectDisposedException(nameof(ModbusBufferManager));
            }

#if NET8_0_OR_GREATER
            var buffer = _bytePool.Rent(minimumLength);
#else
            var buffer = new byte[minimumLength];
            _ = Interlocked.Increment(ref _dedicatedAllocations);
#endif
            _ = Interlocked.Increment(ref _rentOperations);
            return buffer;
        }
    }

    /// <summary>Rents a ushort buffer from the pool or creates a new one.</summary>
    /// <param name="minimumLength">The minimum length required.</param>
    /// <returns>A rented buffer that should be returned when finished.</returns>
    public ushort[] RentUshortBuffer(int minimumLength)
    {
        lock (_lock)
        {
            if (_disposed)
            {
                throw new ObjectDisposedException(nameof(ModbusBufferManager));
            }

#if NET8_0_OR_GREATER
            var buffer = _ushortPool.Rent(minimumLength);
#else
            var buffer = new ushort[minimumLength];
            _ = Interlocked.Increment(ref _dedicatedAllocations);
#endif
            _ = Interlocked.Increment(ref _rentOperations);
            return buffer;
        }
    }

    /// <summary>Rents a bool buffer from the pool or creates a new one.</summary>
    /// <param name="minimumLength">The minimum length required.</param>
    /// <returns>A rented buffer that should be returned when finished.</returns>
    public bool[] RentBoolBuffer(int minimumLength)
    {
        lock (_lock)
        {
            if (_disposed)
            {
                throw new ObjectDisposedException(nameof(ModbusBufferManager));
            }

#if NET8_0_OR_GREATER
            var buffer = _boolPool.Rent(minimumLength);
#else
            var buffer = new bool[minimumLength];
            _ = Interlocked.Increment(ref _dedicatedAllocations);
#endif
            _ = Interlocked.Increment(ref _rentOperations);
            return buffer;
        }
    }

    /// <summary>Returns a byte buffer to the pool.</summary>
    /// <param name="buffer">The buffer to return.</param>
    /// <param name="clearArray">Whether to clear the array.</param>
    public void ReturnByteBuffer(byte[] buffer, bool clearArray) =>
#if NET8_0_OR_GREATER
        ReturnBuffer(buffer, clearArray, _bytePool.Return);
#else
        ReturnBuffer(buffer, clearArray);
#endif

    /// <summary>Returns a ushort buffer to the pool.</summary>
    /// <param name="buffer">The buffer to return.</param>
    /// <param name="clearArray">Whether to clear the array.</param>
    public void ReturnUshortBuffer(ushort[] buffer, bool clearArray)
    {
        var ushortBuffer = buffer;
#if NET8_0_OR_GREATER
        ReturnBuffer(ushortBuffer, clearArray, _ushortPool.Return);
#else
        ReturnBuffer(ushortBuffer, clearArray);
#endif
    }

    /// <summary>Returns a bool buffer to the pool.</summary>
    /// <param name="buffer">The buffer to return.</param>
    /// <param name="clearArray">Whether to clear the array.</param>
    public void ReturnBoolBuffer(bool[] buffer, bool clearArray)
    {
        var boolBuffer = buffer;
        var shouldClear = clearArray;
#if NET8_0_OR_GREATER
        ReturnBuffer(boolBuffer, shouldClear, _boolPool.Return);
#else
        ReturnBuffer(boolBuffer, shouldClear);
#endif
    }

    /// <summary>Disposes the buffer manager and releases all resources.</summary>
    public void Dispose()
    {
        lock (_lock)
        {
            _disposed = true;
        }
    }

    /// <summary>Returns a buffer using the provided pool action when available.</summary>
    /// <typeparam name="T">The element type.</typeparam>
    /// <param name="buffer">The buffer to return.</param>
    /// <param name="clearArray">Whether to clear the array.</param>
    /// <param name="returnAction">The pool return action.</param>
    private void ReturnBuffer<T>(T[] buffer, bool clearArray, Action<T[], bool>? returnAction = null)
    {
        if (buffer is null)
        {
            return;
        }

        lock (_lock)
        {
            if (_disposed)
            {
                return;
            }

            returnAction?.Invoke(buffer, clearArray);
            _ = Interlocked.Increment(ref _returnOperations);
        }
    }
}
