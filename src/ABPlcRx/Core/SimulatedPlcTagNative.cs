// Copyright (c) 2019-2026 Chris Pulman and contributors. All rights reserved.
// Chris Pulman and contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers.Binary;
using System.Collections.ObjectModel;

#if REACTIVELIST_REACTIVE
namespace IoT.DriverCore.ABPlcRx.Reactive;
#else
namespace IoT.DriverCore.ABPlcRx;
#endif

/// <summary>In-memory implementation of the libplctag adapter contract.</summary>
internal sealed class SimulatedPlcTagNative : IPlcTagNative, IDisposable
{
    /// <summary>First positive simulated handle.</summary>
    private const int FirstHandle = 1;

    /// <summary>Synchronizes simulator state.</summary>
    private readonly object _syncRoot = new();

    /// <summary>Physical device buffers keyed by tag name.</summary>
    private readonly Dictionary<string, byte[]> _deviceBuffers = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Staged buffers keyed by handle.</summary>
    private readonly Dictionary<int, HandleState> _handles = [];

    /// <summary>Recorded operations.</summary>
    private readonly List<ABPlcSimulatorLogEntry> _operationLog = [];

    /// <summary>Scripted results awaiting matching operations.</summary>
    private readonly List<ScriptedResult> _scriptedResults = [];

    /// <summary>Latest status keyed by physical tag name.</summary>
    private readonly Dictionary<string, int> _tagStatuses = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Time source for log entries.</summary>
    private readonly TimeProvider _timeProvider;

    /// <summary>Next handle value.</summary>
    private int _nextHandle = FirstHandle;

    /// <summary>Next log sequence.</summary>
    private long _nextSequence = 1;

    /// <summary>Status returned while disconnected.</summary>
    private int _disconnectedStatus = PlcTagStatus.ErrBadConnection;

    /// <summary>Tracks disposal.</summary>
    private bool _disposed;

    /// <summary>Initializes a new instance of the <see cref="SimulatedPlcTagNative"/> class.</summary>
    /// <param name="timeProvider">The operation-log time provider.</param>
    internal SimulatedPlcTagNative(TimeProvider timeProvider) => _timeProvider = timeProvider;

    /// <summary>Gets a value indicating whether simulated communications are connected.</summary>
    internal bool IsConnected { get; private set; } = true;

    /// <summary>Gets the number of live handles.</summary>
    internal int ActiveHandleCount
    {
        get
        {
            lock (_syncRoot)
            {
                return _handles.Count;
            }
        }
    }

    /// <summary>Gets a snapshot of simulator operations.</summary>
    internal IReadOnlyList<ABPlcSimulatorLogEntry> OperationLog
    {
        get
        {
            lock (_syncRoot)
            {
                return Array.AsReadOnly(_operationLog.ToArray());
            }
        }
    }

    /// <summary>Gets a snapshot of tag statuses.</summary>
    internal IReadOnlyDictionary<string, int> TagStatuses
    {
        get
        {
            lock (_syncRoot)
            {
                return new ReadOnlyDictionary<string, int>(
                    new Dictionary<string, int>(_tagStatuses, StringComparer.OrdinalIgnoreCase));
            }
        }
    }

    /// <summary>Extracts a value from a libplctag attribute string.</summary>
    /// <param name="url">The libplctag attribute string.</param>
    /// <param name="name">The attribute name.</param>
    /// <returns>The attribute value, when present.</returns>
    internal static string? GetAttribute(string url, string name)
    {
        var prefix = $"{name}=";
        return url.Split('&')
            .FirstOrDefault(part => part.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            ?.Substring(prefix.Length);
    }

    /// <summary>Parses a required positive numeric attribute.</summary>
    /// <param name="url">The libplctag attribute string.</param>
    /// <param name="name">The attribute name.</param>
    /// <returns>The parsed positive value.</returns>
    internal static int ParsePositiveAttribute(string url, string name)
    {
        var value = GetAttribute(url, name);
        return int.TryParse(
                   value,
                   System.Globalization.NumberStyles.Integer,
                   System.Globalization.CultureInfo.InvariantCulture,
                   out var result) &&
               result > 0
            ? result
            : throw new ArgumentException($"The tag URL contains an invalid {name}.", nameof(url));
    }

    /// <summary>Validates a physical tag name.</summary>
    /// <param name="tagName">The physical tag name.</param>
    internal static void ValidateTagName(string tagName) =>
        ArgumentExceptionHelper.ThrowIfNullOrWhiteSpace(tagName, nameof(tagName));

    /// <summary>Validates a direct buffer range.</summary>
    /// <param name="buffer">The buffer.</param>
    /// <param name="offset">The starting offset.</param>
    /// <param name="length">The requested length.</param>
    internal static void ValidateRange(byte[] buffer, int offset, int length)
    {
        ArgumentExceptionHelper.ThrowIfNegative(offset, nameof(offset));
        ArgumentExceptionHelper.ThrowIfNegative(length, nameof(length));
        ArgumentExceptionHelper.ThrowIfGreaterThan(offset, buffer.Length - length, nameof(offset));
    }

    /// <summary>Encodes a 32-bit floating-point value.</summary>
    /// <param name="value">The value.</param>
    /// <returns>The encoded bytes.</returns>
    internal static byte[] EncodeFloat32(float value)
    {
        var bytes = new byte[sizeof(float)];
        BinaryPrimitives.WriteInt32LittleEndian(bytes, BitConverterCompatibility.SingleToInt32Bits(value));
        return bytes;
    }

    /// <summary>Encodes a 64-bit floating-point value.</summary>
    /// <param name="value">The value.</param>
    /// <returns>The encoded bytes.</returns>
    internal static byte[] EncodeFloat64(double value)
    {
        var bytes = new byte[sizeof(double)];
        BinaryPrimitives.WriteInt64LittleEndian(bytes, BitConverterCompatibility.DoubleToInt64Bits(value));
        return bytes;
    }

    /// <summary>Encodes a signed 16-bit value.</summary>
    /// <param name="value">The value.</param>
    /// <returns>The encoded bytes.</returns>
    internal static byte[] EncodeInt16(short value)
    {
        var bytes = new byte[sizeof(short)];
        BinaryPrimitives.WriteInt16LittleEndian(bytes, value);
        return bytes;
    }

    /// <summary>Encodes a signed 32-bit value.</summary>
    /// <param name="value">The value.</param>
    /// <returns>The encoded bytes.</returns>
    internal static byte[] EncodeInt32(int value)
    {
        var bytes = new byte[sizeof(int)];
        BinaryPrimitives.WriteInt32LittleEndian(bytes, value);
        return bytes;
    }

    /// <summary>Encodes a signed 64-bit value.</summary>
    /// <param name="value">The value.</param>
    /// <returns>The encoded bytes.</returns>
    internal static byte[] EncodeInt64(long value)
    {
        var bytes = new byte[sizeof(long)];
        BinaryPrimitives.WriteInt64LittleEndian(bytes, value);
        return bytes;
    }

    /// <summary>Encodes a signed 8-bit value.</summary>
    /// <param name="value">The value.</param>
    /// <returns>The encoded bytes.</returns>
    internal static byte[] EncodeInt8(sbyte value) => [unchecked((byte)value)];

    /// <summary>Encodes an unsigned 16-bit value.</summary>
    /// <param name="value">The value.</param>
    /// <returns>The encoded bytes.</returns>
    internal static byte[] EncodeUInt16(ushort value)
    {
        var bytes = new byte[sizeof(ushort)];
        BinaryPrimitives.WriteUInt16LittleEndian(bytes, value);
        return bytes;
    }

    /// <summary>Encodes an unsigned 32-bit value.</summary>
    /// <param name="value">The value.</param>
    /// <returns>The encoded bytes.</returns>
    internal static byte[] EncodeUInt32(uint value)
    {
        var bytes = new byte[sizeof(uint)];
        BinaryPrimitives.WriteUInt32LittleEndian(bytes, value);
        return bytes;
    }

    /// <summary>Encodes an unsigned 64-bit value.</summary>
    /// <param name="value">The value.</param>
    /// <returns>The encoded bytes.</returns>
    internal static byte[] EncodeUInt64(ulong value)
    {
        var bytes = new byte[sizeof(ulong)];
        BinaryPrimitives.WriteUInt64LittleEndian(bytes, value);
        return bytes;
    }

    /// <summary>Encodes an unsigned 8-bit value.</summary>
    /// <param name="value">The value.</param>
    /// <returns>The encoded bytes.</returns>
    internal static byte[] EncodeUInt8(byte value) => [value];

    /// <summary>Gets exact native-operation counts for the current operation history.</summary>
    /// <returns>An immutable operation-count snapshot.</returns>
    internal ABPlcSimulatorOperationMetrics GetOperationMetrics()
    {
        lock (_syncRoot)
        {
            return new ABPlcSimulatorOperationMetrics(_operationLog);
        }
    }

    /// <summary>Disconnects communications.</summary>
    /// <param name="statusCode">The status returned by IO while disconnected.</param>
    /// <returns>True when the state changed.</returns>
    internal bool Disconnect(int statusCode)
    {
        if (!PlcTagStatus.IsError(statusCode))
        {
            throw new ArgumentOutOfRangeException(nameof(statusCode), "A disconnected status must be an error.");
        }

        lock (_syncRoot)
        {
            ThrowIfDisposed();
            _disconnectedStatus = statusCode;
            if (!IsConnected)
            {
                return false;
            }

            IsConnected = false;
            return true;
        }
    }

    /// <summary>Reconnects communications.</summary>
    /// <returns>True when the state changed.</returns>
    internal bool Reconnect()
    {
        lock (_syncRoot)
        {
            ThrowIfDisposed();
            if (IsConnected)
            {
                return false;
            }

            IsConnected = true;
            return true;
        }
    }

    /// <summary>Queues a scripted result.</summary>
    /// <param name="operation">The operation to match.</param>
    /// <param name="statusCode">The status to return.</param>
    /// <param name="repeatCount">The number of results to return.</param>
    /// <param name="tagName">The optional physical tag filter.</param>
    internal void QueueFault(
        ABPlcSimulatorOperation operation,
        int statusCode,
        int repeatCount,
        string? tagName)
    {
        if (operation is < ABPlcSimulatorOperation.Create or > ABPlcSimulatorOperation.Write)
        {
            throw new ArgumentOutOfRangeException(nameof(operation));
        }

        ArgumentExceptionHelper.ThrowIfNegativeOrZero(repeatCount, nameof(repeatCount));

        if (tagName is not null && string.IsNullOrWhiteSpace(tagName))
        {
            throw new ArgumentException("A tag filter cannot be empty.", nameof(tagName));
        }

        lock (_syncRoot)
        {
            ThrowIfDisposed();
            _scriptedResults.Add(new ScriptedResult(operation, statusCode, repeatCount, tagName));
        }
    }

    /// <summary>Clears all scripted results.</summary>
    internal void ClearFaults()
    {
        lock (_syncRoot)
        {
            ThrowIfDisposed();
            _scriptedResults.Clear();
        }
    }

    /// <summary>Clears operation history.</summary>
    internal void ClearOperationLog()
    {
        lock (_syncRoot)
        {
            ThrowIfDisposed();
            _operationLog.Clear();
            _nextSequence = 1;
        }
    }

    /// <summary>Sets physical device bytes.</summary>
    /// <param name="tagName">The physical tag name.</param>
    /// <param name="value">The device bytes.</param>
    internal void SetTagBytes(string tagName, IReadOnlyCollection<byte> value)
    {
        ValidateTagName(tagName);
        ArgumentExceptionHelper.ThrowIfNull(value, nameof(value));
        lock (_syncRoot)
        {
            ThrowIfDisposed();
            _deviceBuffers[tagName] = value.ToArray();
        }
    }

    /// <summary>Gets physical device bytes.</summary>
    /// <param name="tagName">The physical tag name.</param>
    /// <returns>A copy of the device bytes.</returns>
    internal byte[] GetTagBytes(string tagName)
    {
        ValidateTagName(tagName);
        lock (_syncRoot)
        {
            ThrowIfDisposed();
            return _deviceBuffers.TryGetValue(tagName, out var value)
                ? (byte[])value.Clone()
                : throw new KeyNotFoundException($"Simulated PLC tag '{tagName}' was not found.");
        }
    }

    /// <inheritdoc/>
    int IPlcTagNative.Create(string url, int timeout)
    {
        _ = timeout;
        ArgumentExceptionHelper.ThrowIfNull(url, nameof(url));
        lock (_syncRoot)
        {
            ThrowIfDisposed();
            var tagName = GetAttribute(url, "name")
                ?? throw new ArgumentException("The tag URL does not contain a name.", nameof(url));
            var size = ParsePositiveAttribute(url, "elem_size");
            var count = ParsePositiveAttribute(url, "elem_count");
            var byteCount = checked(size * count);
            var status = GetOperationStatus(ABPlcSimulatorOperation.Create, tagName, requiresConnection: true);
            if (PlcTagStatus.IsError(status))
            {
                Record(ABPlcSimulatorOperation.Create, tagName, status, status);
                return status;
            }

            var device = EnsureDeviceBuffer(tagName, byteCount);
            var handle = _nextHandle++;
            var working = new byte[byteCount];
            Array.Copy(device, working, Math.Min(device.Length, working.Length));
            _handles.Add(handle, new HandleState(tagName, working));
            Record(ABPlcSimulatorOperation.Create, tagName, handle, PlcTagStatus.StatusOK);
            return handle;
        }
    }

    /// <inheritdoc/>
    int IPlcTagNative.Destroy(int handle)
    {
        lock (_syncRoot)
        {
            ThrowIfDisposed();
            var tagName = GetTagName(handle);
            var status = GetOperationStatus(ABPlcSimulatorOperation.Destroy, tagName, requiresConnection: false);
            if (status == PlcTagStatus.StatusOK && !_handles.Remove(handle))
            {
                status = PlcTagStatus.ErrNotFound;
            }

            Record(ABPlcSimulatorOperation.Destroy, tagName, handle, status);
            return status;
        }
    }

    /// <inheritdoc/>
    int IPlcTagNative.Abort(int handle) => RunHandleOperation(handle, ABPlcSimulatorOperation.Abort);

    /// <inheritdoc/>
    int IPlcTagNative.GetSize(int handle)
    {
        lock (_syncRoot)
        {
            ThrowIfDisposed();
            return GetHandle(handle).Buffer.Length;
        }
    }

    /// <inheritdoc/>
    int IPlcTagNative.GetStatus(int handle) => RunHandleOperation(handle, ABPlcSimulatorOperation.GetStatus);

    /// <inheritdoc/>
    int IPlcTagNative.Lock(int handle) => RunHandleOperation(handle, ABPlcSimulatorOperation.Lock);

    /// <inheritdoc/>
    int IPlcTagNative.Read(int handle, int timeout)
    {
        _ = timeout;
        lock (_syncRoot)
        {
            ThrowIfDisposed();
            if (!_handles.TryGetValue(handle, out var state))
            {
                Record(ABPlcSimulatorOperation.Read, null, handle, PlcTagStatus.ErrNotFound);
                return PlcTagStatus.ErrNotFound;
            }

            var status = GetOperationStatus(ABPlcSimulatorOperation.Read, state.TagName, requiresConnection: true);
            if (status == PlcTagStatus.StatusOK)
            {
                var device = EnsureDeviceBuffer(state.TagName, state.Buffer.Length);
                Array.Copy(device, state.Buffer, state.Buffer.Length);
            }

            Record(ABPlcSimulatorOperation.Read, state.TagName, handle, status);
            return status;
        }
    }

    /// <inheritdoc/>
    int IPlcTagNative.Unlock(int handle) => RunHandleOperation(handle, ABPlcSimulatorOperation.Unlock);

    /// <inheritdoc/>
    int IPlcTagNative.Write(int handle, int timeout)
    {
        _ = timeout;
        lock (_syncRoot)
        {
            ThrowIfDisposed();
            if (!_handles.TryGetValue(handle, out var state))
            {
                Record(ABPlcSimulatorOperation.Write, null, handle, PlcTagStatus.ErrNotFound);
                return PlcTagStatus.ErrNotFound;
            }

            var status = GetOperationStatus(ABPlcSimulatorOperation.Write, state.TagName, requiresConnection: true);
            if (status == PlcTagStatus.StatusOK)
            {
                _deviceBuffers[state.TagName] = (byte[])state.Buffer.Clone();
            }

            Record(ABPlcSimulatorOperation.Write, state.TagName, handle, status);
            return status;
        }
    }

    /// <inheritdoc/>
    float IPlcTagNative.GetFloat32(int handle, int offset) =>
        ABPlcSimulatorValueCodec.Decode<float>(ReadBuffer(handle, offset, sizeof(float)));

    /// <inheritdoc/>
    double IPlcTagNative.GetFloat64(int handle, int offset) =>
        ABPlcSimulatorValueCodec.Decode<double>(ReadBuffer(handle, offset, sizeof(double)));

    /// <inheritdoc/>
    short IPlcTagNative.GetInt16(int handle, int offset) =>
        ABPlcSimulatorValueCodec.Decode<short>(ReadBuffer(handle, offset, sizeof(short)));

    /// <inheritdoc/>
    int IPlcTagNative.GetInt32(int handle, int offset) =>
        ABPlcSimulatorValueCodec.Decode<int>(ReadBuffer(handle, offset, sizeof(int)));

    /// <inheritdoc/>
    long IPlcTagNative.GetInt64(int handle, int offset) =>
        ABPlcSimulatorValueCodec.Decode<long>(ReadBuffer(handle, offset, sizeof(long)));

    /// <inheritdoc/>
    sbyte IPlcTagNative.GetInt8(int handle, int offset) =>
        ABPlcSimulatorValueCodec.Decode<sbyte>(ReadBuffer(handle, offset, sizeof(byte)));

    /// <inheritdoc/>
    ushort IPlcTagNative.GetUInt16(int handle, int offset) =>
        ABPlcSimulatorValueCodec.Decode<ushort>(ReadBuffer(handle, offset, sizeof(ushort)));

    /// <inheritdoc/>
    uint IPlcTagNative.GetUInt32(int handle, int offset) =>
        ABPlcSimulatorValueCodec.Decode<uint>(ReadBuffer(handle, offset, sizeof(uint)));

    /// <inheritdoc/>
    ulong IPlcTagNative.GetUInt64(int handle, int offset) =>
        ABPlcSimulatorValueCodec.Decode<ulong>(ReadBuffer(handle, offset, sizeof(ulong)));

    /// <inheritdoc/>
    byte IPlcTagNative.GetUInt8(int handle, int offset) =>
        ABPlcSimulatorValueCodec.Decode<byte>(ReadBuffer(handle, offset, sizeof(byte)));

    /// <inheritdoc/>
    void IPlcTagNative.SetFloat32(int handle, int offset, float value) =>
        WriteBuffer(handle, offset, EncodeFloat32(value), TypeCode.Single);

    /// <inheritdoc/>
    void IPlcTagNative.SetFloat64(int handle, int offset, double value) =>
        WriteBuffer(handle, offset, EncodeFloat64(value), TypeCode.Double);

    /// <inheritdoc/>
    void IPlcTagNative.SetInt16(int handle, int offset, short value) =>
        WriteBuffer(handle, offset, EncodeInt16(value), TypeCode.Int16);

    /// <inheritdoc/>
    void IPlcTagNative.SetInt32(int handle, int offset, int value) =>
        WriteBuffer(handle, offset, EncodeInt32(value), TypeCode.Int32);

    /// <inheritdoc/>
    void IPlcTagNative.SetInt64(int handle, int offset, long value) =>
        WriteBuffer(handle, offset, EncodeInt64(value), TypeCode.Int64);

    /// <inheritdoc/>
    void IPlcTagNative.SetInt8(int handle, int offset, sbyte value) =>
        WriteBuffer(handle, offset, EncodeInt8(value), TypeCode.SByte);

    /// <inheritdoc/>
    void IPlcTagNative.SetUInt16(int handle, int offset, ushort value) =>
        WriteBuffer(handle, offset, EncodeUInt16(value), TypeCode.UInt16);

    /// <inheritdoc/>
    void IPlcTagNative.SetUInt32(int handle, int offset, uint value) =>
        WriteBuffer(handle, offset, EncodeUInt32(value), TypeCode.UInt32);

    /// <inheritdoc/>
    void IPlcTagNative.SetUInt64(int handle, int offset, ulong value) =>
        WriteBuffer(handle, offset, EncodeUInt64(value), TypeCode.UInt64);

    /// <inheritdoc/>
    void IPlcTagNative.SetUInt8(int handle, int offset, byte value) =>
        WriteBuffer(handle, offset, EncodeUInt8(value), TypeCode.Byte);

    /// <inheritdoc/>
    void IDisposable.Dispose()
    {
        lock (_syncRoot)
        {
            if (_disposed)
            {
                return;
            }

            _handles.Clear();
            _deviceBuffers.Clear();
            _scriptedResults.Clear();
            _disposed = true;
            IsConnected = false;
        }
    }

    /// <summary>Runs a status-only handle operation.</summary>
    /// <param name="handle">The simulated handle.</param>
    /// <param name="operation">The operation to run.</param>
    /// <returns>The resulting status.</returns>
    private int RunHandleOperation(int handle, ABPlcSimulatorOperation operation)
    {
        lock (_syncRoot)
        {
            ThrowIfDisposed();
            if (!_handles.TryGetValue(handle, out var state))
            {
                Record(operation, null, handle, PlcTagStatus.ErrNotFound);
                return PlcTagStatus.ErrNotFound;
            }

            var status = GetOperationStatus(operation, state.TagName, requiresConnection: true);
            Record(operation, state.TagName, handle, status);
            return status;
        }
    }

    /// <summary>Reads bytes from a handle buffer.</summary>
    /// <param name="handle">The simulated handle.</param>
    /// <param name="offset">The byte offset.</param>
    /// <param name="length">The byte count.</param>
    /// <returns>A copy of the requested bytes.</returns>
    private byte[] ReadBuffer(int handle, int offset, int length)
    {
        lock (_syncRoot)
        {
            ThrowIfDisposed();
            var buffer = GetHandle(handle).Buffer;
            ValidateRange(buffer, offset, length);
            var result = new byte[length];
            Array.Copy(buffer, offset, result, 0, length);
            return result;
        }
    }

    /// <summary>Writes bytes to a handle buffer.</summary>
    /// <param name="handle">The simulated handle.</param>
    /// <param name="offset">The byte offset.</param>
    /// <param name="value">The source bytes.</param>
    /// <param name="valueType">The scalar type represented by the bytes.</param>
    private void WriteBuffer(int handle, int offset, byte[] value, TypeCode valueType)
    {
        if (valueType is TypeCode.Empty or TypeCode.Object or TypeCode.DBNull)
        {
            throw new ArgumentOutOfRangeException(nameof(valueType));
        }

        lock (_syncRoot)
        {
            ThrowIfDisposed();
            var buffer = GetHandle(handle).Buffer;
            ValidateRange(buffer, offset, value.Length);
            Array.Copy(value, 0, buffer, offset, value.Length);
        }
    }

    /// <summary>Gets or grows a device buffer.</summary>
    /// <param name="tagName">The physical tag name.</param>
    /// <param name="minimumLength">The required byte count.</param>
    /// <returns>The device buffer.</returns>
    private byte[] EnsureDeviceBuffer(string tagName, int minimumLength)
    {
        if (!_deviceBuffers.TryGetValue(tagName, out var buffer))
        {
            buffer = new byte[minimumLength];
            _deviceBuffers.Add(tagName, buffer);
        }
        else if (buffer.Length < minimumLength)
        {
            Array.Resize(ref buffer, minimumLength);
            _deviceBuffers[tagName] = buffer;
        }

        return buffer;
    }

    /// <summary>Gets a handle or throws for invalid direct-buffer access.</summary>
    /// <param name="handle">The simulated handle.</param>
    /// <returns>The handle state.</returns>
    private HandleState GetHandle(int handle) =>
        _handles.TryGetValue(handle, out var state)
            ? state
            : throw new ArgumentOutOfRangeException(nameof(handle), $"Simulator handle {handle} was not found.");

    /// <summary>Gets a tag name for a handle when present.</summary>
    /// <param name="handle">The simulated handle.</param>
    /// <returns>The physical tag name, when the handle exists.</returns>
    private string? GetTagName(int handle) =>
        _handles.TryGetValue(handle, out var state) ? state.TagName : null;

    /// <summary>Gets a scripted, connected, or successful status.</summary>
    /// <param name="operation">The operation to run.</param>
    /// <param name="tagName">The physical tag name.</param>
    /// <param name="requiresConnection">Whether the operation requires an active connection.</param>
    /// <returns>The resulting status.</returns>
    private int GetOperationStatus(
        ABPlcSimulatorOperation operation,
        string? tagName,
        bool requiresConnection)
    {
        for (var index = 0; index < _scriptedResults.Count; index++)
        {
            var scripted = _scriptedResults[index];
            if (scripted.Operation != operation ||
                (scripted.TagName is not null &&
                 !string.Equals(scripted.TagName, tagName, StringComparison.OrdinalIgnoreCase)))
            {
                continue;
            }

            scripted.Remaining--;
            if (scripted.Remaining == 0)
            {
                _scriptedResults.RemoveAt(index);
            }

            return scripted.StatusCode;
        }

        return requiresConnection && !IsConnected
            ? _disconnectedStatus
            : PlcTagStatus.StatusOK;
    }

    /// <summary>Records an operation and updates the tag status.</summary>
    /// <param name="operation">The operation that ran.</param>
    /// <param name="tagName">The physical tag name.</param>
    /// <param name="handle">The simulated handle.</param>
    /// <param name="statusCode">The resulting status.</param>
    private void Record(
        ABPlcSimulatorOperation operation,
        string? tagName,
        int handle,
        int statusCode)
    {
        var sequence = _nextSequence;
        _nextSequence++;
        _operationLog.Add(
            new ABPlcSimulatorLogEntry(
                sequence,
                _timeProvider.GetUtcNow(),
                operation,
                tagName,
                handle,
                statusCode));
        if (tagName is null)
        {
            return;
        }

        _tagStatuses[tagName] = statusCode;
    }

    /// <summary>Throws when the native simulator is disposed.</summary>
    private void ThrowIfDisposed() =>
        _ = !_disposed ? true : throw new ObjectDisposedException(nameof(SimulatedPlcTagNative));

    /// <summary>State retained for one native-style handle.</summary>
    /// <param name="tagName">The physical tag name.</param>
    /// <param name="buffer">The staged handle buffer.</param>
    private sealed class HandleState(string tagName, byte[] buffer)
    {
        /// <summary>Gets the physical tag name.</summary>
        internal string TagName { get; } = tagName;

        /// <summary>Gets the staged handle buffer.</summary>
        internal byte[] Buffer { get; } = buffer;
    }

    /// <summary>One queued scripted operation result.</summary>
    /// <param name="operation">The operation to match.</param>
    /// <param name="statusCode">The status to return.</param>
    /// <param name="remaining">The number of matches remaining.</param>
    /// <param name="tagName">The optional physical tag filter.</param>
    private sealed class ScriptedResult(
        ABPlcSimulatorOperation operation,
        int statusCode,
        int remaining,
        string? tagName)
    {
        /// <summary>Gets the operation to match.</summary>
        internal ABPlcSimulatorOperation Operation { get; } = operation;

        /// <summary>Gets the status to return.</summary>
        internal int StatusCode { get; } = statusCode;

        /// <summary>Gets or sets the remaining match count.</summary>
        internal int Remaining { get; set; } = remaining;

        /// <summary>Gets the optional physical tag filter.</summary>
        internal string? TagName { get; } = tagName;
    }
}
