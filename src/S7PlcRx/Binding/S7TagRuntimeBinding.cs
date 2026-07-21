// Copyright (c) 2019-2026 Chris Pulman and contributors. All rights reserved.
// Chris Pulman and contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Collections.Concurrent;
using Timer = System.Threading.Timer;
#if REACTIVE_SHIM
using S7PlcRx.Reactive.PlcTypes;
#else
using S7PlcRx.PlcTypes;
#endif

#if REACTIVE_SHIM
namespace S7PlcRx.Reactive.Binding;

#else
namespace S7PlcRx.Binding;

#endif

/// <summary>Runtime engine for tag bindings that polls and writes PLC DB values in byte-array batches.</summary>
public sealed class S7TagRuntimeBinding : IDisposable
{
    /// <summary>Defines the length of the DB address prefix.</summary>
    private const int DataBlockPrefixLength = 2;

    /// <summary>Defines the length of an S7 data-block type prefix.</summary>
    private const int DataBlockTypePrefixLength = 3;

    /// <summary>Defines the minimum length of an S7 data-block address component.</summary>
    private const int MinimumDataBlockAddressLength = 4;

    /// <summary>Defines the highest valid zero-based bit offset.</summary>
    private const int MaximumBitOffset = 7;

    /// <summary>Defines the size of an S7 word in bytes.</summary>
    private const int WordByteLength = 2;

    /// <summary>Defines the size of an S7 double word in bytes.</summary>
    private const int DoubleWordByteLength = 4;

    /// <summary>Defines the size of a double-precision value in bytes.</summary>
    private const int DoubleByteLength = 8;

    /// <summary>Defines the maximum gap between ranges that can be read together.</summary>
    private const int MaxReadGapBytes = 16;

    /// <summary>Defines the interval at which pending writes are flushed.</summary>
    private const int WriteFlushMs = 20;

    /// <summary>Stores the PLC used by this instance.</summary>
    private readonly IRxS7 _plc;

    /// <summary>Stores the tag definitions used by this instance.</summary>
    private readonly IReadOnlyList<S7TagDefinition> _definitions;

    /// <summary>Stores the runtime addresses used by this instance.</summary>
    private readonly Dictionary<string, S7TagRuntimeAddress> _addresses;

    /// <summary>Stores tag definitions indexed by name.</summary>
    private readonly Dictionary<string, S7TagDefinition> _definitionsByName;

    /// <summary>Stores the callback that applies read values.</summary>
    private readonly Action<string, object?> _applyRead;

    /// <summary>Stores the polling timers used by this instance.</summary>
    private readonly List<Timer> _timers = [];

    /// <summary>Stores pending values to write, indexed by tag name.</summary>
    private readonly ConcurrentDictionary<string, object?> _pendingWrites = new(
        StringComparer.InvariantCultureIgnoreCase);

    /// <summary>Stores the lock that serializes PLC I/O.</summary>
    private readonly SemaphoreSlim _ioLock = new(1, 1);

    /// <summary>Stores the timer that flushes pending writes.</summary>
    private readonly Timer _writeTimer;

    /// <summary>Indicates whether this instance has been disposed.</summary>
    private bool _disposed;

    /// <summary>Initializes a new instance of the <see cref="S7TagRuntimeBinding"/> class.</summary>
    /// <param name="plc">The PLC instance.</param>
    /// <param name="definitions">The generated tag definitions.</param>
    /// <param name="applyRead">The callback that applies polled values to generated backing fields.</param>
    private S7TagRuntimeBinding(
        IRxS7 plc,
        IReadOnlyList<S7TagDefinition> definitions,
        Action<string, object?> applyRead)
    {
        _plc = plc ?? throw new ArgumentNullException(nameof(plc));
        _definitions = definitions ?? throw new ArgumentNullException(nameof(definitions));
        _applyRead = applyRead ?? throw new ArgumentNullException(nameof(applyRead));
        _definitionsByName = new(StringComparer.InvariantCultureIgnoreCase);
        _addresses = new(StringComparer.InvariantCultureIgnoreCase);
        foreach (var definition in definitions)
        {
            _definitionsByName[definition.Name] = definition;
            _addresses[definition.Name] = ParseAddress(definition);
        }

        RegisterTags();
        StartPollers();
        _writeTimer = new(_ => FlushWrites(), null, WriteFlushMs, WriteFlushMs);
    }

    /// <summary>Creates and starts a runtime binding for generated PLC tag definitions.</summary>
    /// <param name="plc">The PLC instance.</param>
    /// <param name="definitions">The tag definitions emitted by the source generator.</param>
    /// <param name="applyRead">
    /// A generated callback that assigns PLC values to backing fields without re-writing them.
    /// </param>
    /// <returns>A disposable runtime binding.</returns>
    public static S7TagRuntimeBinding Bind(
        IRxS7 plc,
        IReadOnlyList<S7TagDefinition> definitions,
        Action<string, object?> applyRead) => new(plc, definitions, applyRead);

    /// <summary>Queues a generated property change for a grouped byte-array write.</summary>
    /// <param name="name">The generated tag/property name.</param>
    /// <param name="value">The new property value.</param>
    public void Write(string name, object? value)
    {
        if (_disposed || string.IsNullOrWhiteSpace(name))
        {
            return;
        }

        if (!_definitionsByName.TryGetValue(name, out var definition) || !definition.CanWrite)
        {
            return;
        }

        _pendingWrites[name] = value;
    }

    /// <summary>Releases timers and pending write state.</summary>
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _writeTimer.Dispose();
        foreach (var timer in _timers)
        {
            timer.Dispose();
        }

        _ioLock.Dispose();
        _pendingWrites.Clear();
    }

    /// <summary>Creates the internal tag name used to read or write a byte range.</summary>
    /// <param name="range">The byte range.</param>
    /// <returns>The generated internal tag name.</returns>
    private static string RangeTagName(S7TagRange range) =>
        $"__s7_binding_db{range.Db}_{range.StartByte}_{range.Length}";

    /// <summary>Parses a tag definition into its runtime address.</summary>
    /// <param name="definition">The tag definition.</param>
    /// <returns>The resulting value.</returns>
    private static S7TagRuntimeAddress ParseAddress(S7TagDefinition definition)
    {
        var address = definition.Address.ToUpperInvariant().Replace(" ", string.Empty);
        if (!address.StartsWith("DB", StringComparison.Ordinal))
        {
            throw new ArgumentException(
                "Only DB addresses are supported by generated S7 tag byte-array bindings.",
                nameof(definition));
        }

        var parts = address.Split(['.']);
        if (
            parts.Length < DataBlockPrefixLength
            || !int.TryParse(parts[0][DataBlockPrefixLength..], out var db))
        {
            throw new ArgumentException(
                $"Invalid S7 DB address '{definition.Address}'.",
                nameof(definition));
        }

        var dbPart = parts[1];
        if (dbPart.StartsWith("DBX", StringComparison.Ordinal))
        {
            return ParseBitAddress(definition, parts, dbPart, db);
        }

        if (
            dbPart.Length < MinimumDataBlockAddressLength
            || !int.TryParse(dbPart[DataBlockTypePrefixLength..], out var startByte))
        {
            throw new ArgumentException(
                $"Invalid S7 DB address '{definition.Address}'.",
                nameof(definition));
        }

        var byteLength = GetByteLength(definition, dbPart[..DataBlockTypePrefixLength]);
        return new S7TagRuntimeAddress(db, startByte, null, byteLength);
    }

    /// <summary>Parses a bit tag definition into its runtime address.</summary>
    /// <param name="definition">The tag definition.</param>
    /// <param name="parts">The address components.</param>
    /// <param name="dbPart">The data-block address component.</param>
    /// <param name="db">The data-block number.</param>
    /// <returns>The resulting value.</returns>
    private static S7TagRuntimeAddress ParseBitAddress(
        S7TagDefinition definition,
        string[] parts,
        string dbPart,
        int db)
    {
        if (
            parts.Length < DataBlockTypePrefixLength
            || !int.TryParse(dbPart[DataBlockTypePrefixLength..], out var byteOffset)
            || !int.TryParse(parts[DataBlockPrefixLength], out var bitOffset))
        {
            throw new ArgumentException(
                $"Invalid S7 DB bit address '{definition.Address}'.",
                nameof(definition));
        }

        if ((uint)bitOffset > MaximumBitOffset)
        {
            throw new ArgumentOutOfRangeException(
                nameof(definition),
                "DBX bit offset must be between 0 and 7.");
        }

        return new(db, byteOffset, bitOffset, 1);
    }

    /// <summary>Gets the byte length occupied by a tag definition.</summary>
    /// <param name="definition">The tag definition.</param>
    /// <param name="dbType">The S7 data-block value type.</param>
    /// <returns>The resulting value.</returns>
    private static int GetByteLength(S7TagDefinition definition, string dbType)
    {
        if (definition.ValueType == typeof(byte[]))
        {
            return definition.ArrayLength;
        }

        if (definition.ValueType == typeof(string))
        {
            return definition.ArrayLength;
        }

        var multiplier = definition.ValueType.IsArray ? definition.ArrayLength : 1;
        var elementType = definition.ValueType.IsArray
            ? definition.ValueType.GetElementType()!
            : definition.ValueType;
        return dbType switch
        {
            "DBB" => Math.Max(1, multiplier),
            "DBW" => WordByteLength * multiplier,
            "DBD" when elementType == typeof(double) => DoubleByteLength * multiplier,
            "DBD" => DoubleWordByteLength * multiplier,
            _ => throw new ArgumentException(
                $"Unsupported S7 DB address type '{dbType}'.",
                nameof(definition)),
        };
    }

    /// <summary>Builds contiguous PLC read and write ranges for the supplied tags.</summary>
    /// <param name="tags">The tags to group into ranges.</param>
    /// <returns>The resulting value.</returns>
    private static List<S7TagRange> BuildRanges(
        IEnumerable<KeyValuePair<S7TagDefinition, S7TagRuntimeAddress>> tags)
    {
        var ranges = new List<S7TagRange>();
        var sortedTags = new List<KeyValuePair<S7TagDefinition, S7TagRuntimeAddress>>(tags);
        sortedTags.Sort(
            static (left, right) =>
            {
                var dbComparison = left.Value.Db.CompareTo(right.Value.Db);
                return dbComparison != 0
                    ? dbComparison
                    : left.Value.StartByte.CompareTo(right.Value.StartByte);
            });

        foreach (var item in sortedTags)
        {
            var endByte = item.Value.StartByte + item.Value.ByteLength;
            var current = ranges.Count > 0 ? ranges[^1] : null;
            if (
                current is not null
                && current.Db == item.Value.Db
                && item.Value.StartByte <= current.EndByte + MaxReadGapBytes)
            {
                current.EndByte = Math.Max(current.EndByte, endByte);
                current.Items.Add(item);
            }
            else
            {
                ranges.Add(new S7TagRange(item.Value.Db, item.Value.StartByte, endByte, [item]));
            }
        }

        return ranges;
    }

    /// <summary>Decodes a tag value from a byte buffer.</summary>
    /// <param name="definition">The tag definition.</param>
    /// <param name="address">The runtime tag address.</param>
    /// <param name="buffer">The source byte buffer.</param>
    /// <param name="rangeStart">The first byte represented by the buffer.</param>
    /// <returns>The resulting value.</returns>
    private static object? Decode(
        S7TagDefinition definition,
        S7TagRuntimeAddress address,
        byte[] buffer,
        int rangeStart)
    {
        var offset = address.StartByte - rangeStart;
        var span = buffer.AsSpan(offset, address.ByteLength);
        if (address.BitOffset.HasValue)
        {
            return Bit.FromByte(span[0], (byte)address.BitOffset.Value);
        }

        return definition.ValueType.IsArray && definition.ValueType != typeof(byte[])
            ? DecodeArray(definition.ValueType, span)
            : DecodeScalar(definition.ValueType, span);
    }

    /// <summary>Decodes a scalar value from an S7 byte span.</summary>
    /// <param name="valueType">The expected scalar type.</param>
    /// <param name="span">The source bytes.</param>
    /// <returns>The resulting value.</returns>
    private static object? DecodeScalar(Type valueType, ReadOnlySpan<byte> span) =>
        valueType switch
        {
            _ when valueType == typeof(byte[]) => span.ToArray(),
            _ when valueType == typeof(byte) => span[0],
            _ when valueType == typeof(bool) => span[0] != 0,
            _ when valueType == typeof(short) => Int.FromSpan(span),
            _ when valueType == typeof(ushort) => Word.FromSpan(span),
            _ when valueType == typeof(int) => DInt.FromSpan(span),
            _ when valueType == typeof(uint) => DWord.FromSpan(span),
            _ when valueType == typeof(float) => Real.FromSpan(span),
            _ when valueType == typeof(double) => LReal.FromSpan(span),
            _ when valueType == typeof(string) => PlcTypes
                .String.FromByteArray(span.ToArray())
                .Replace("\0", string.Empty),
            _ => null,
        };

    /// <summary>Decodes an array value from an S7 byte span.</summary>
    /// <param name="valueType">The expected array type.</param>
    /// <param name="span">The source bytes.</param>
    /// <returns>The resulting value.</returns>
    private static object? DecodeArray(Type valueType, ReadOnlySpan<byte> span)
    {
        if (valueType == typeof(short[]))
        {
            return Int.ToArray(span);
        }

        if (valueType == typeof(ushort[]))
        {
            return Word.ToArray(span);
        }

        if (valueType == typeof(int[]))
        {
            return DInt.ToArray(span);
        }

        if (valueType == typeof(uint[]))
        {
            return DWord.ToArray(span);
        }

        if (valueType == typeof(float[]))
        {
            return Real.ToArray(span);
        }

        return valueType == typeof(double[]) ? LReal.ToArray(span) : null;
    }

    /// <summary>Encodes a tag value into a byte buffer.</summary>
    /// <param name="definition">The tag definition.</param>
    /// <param name="address">The runtime tag address.</param>
    /// <param name="value">The value to encode.</param>
    /// <param name="buffer">The destination byte buffer.</param>
    /// <param name="rangeStart">The first byte represented by the buffer.</param>
    private static void Encode(
        S7TagDefinition definition,
        S7TagRuntimeAddress address,
        object? value,
        byte[] buffer,
        int rangeStart)
    {
        if (value is null)
        {
            return;
        }

        var offset = address.StartByte - rangeStart;
        var span = buffer.AsSpan(offset, address.ByteLength);
        if (address.BitOffset.HasValue)
        {
            var mask = (byte)(1 << address.BitOffset.Value);
            if ((bool)Convert.ChangeType(value, typeof(bool)))
            {
                span[0] |= mask;
            }
            else
            {
                span[0] &= (byte)~mask;
            }

            return;
        }

        var data = ToBytes(definition.ValueType, value);
        data.AsSpan(0, Math.Min(data.Length, span.Length)).CopyTo(span);
    }

    /// <summary>Converts a scalar or array tag value to S7 bytes.</summary>
    /// <param name="valueType">The tag value type.</param>
    /// <param name="value">The value to convert.</param>
    /// <returns>The resulting value.</returns>
    private static byte[] ToBytes(Type valueType, object value)
    {
        return valueType.IsArray && valueType != typeof(byte[])
            ? ToArrayBytes(valueType, value)
            : ToScalarBytes(valueType, value);
    }

    /// <summary>Converts a scalar tag value to S7 bytes.</summary>
    /// <param name="valueType">The scalar type.</param>
    /// <param name="value">The value to convert.</param>
    /// <returns>The resulting value.</returns>
    private static byte[] ToScalarBytes(Type valueType, object value) =>
        valueType switch
        {
            _ when valueType == typeof(byte[]) => (byte[])value,
            _ when valueType == typeof(byte) => [(byte)Convert.ChangeType(value, typeof(byte))],
            _ when valueType == typeof(bool) =>
            [
                (bool)Convert.ChangeType(value, typeof(bool)) ? (byte)1 : (byte)0,],
            _ when valueType == typeof(short) => Int.ToByteArray(
                (short)Convert.ChangeType(value, typeof(short))),
            _ when valueType == typeof(ushort) => Word.ToByteArray(
                (ushort)Convert.ChangeType(value, typeof(ushort))),
            _ when valueType == typeof(int) => DInt.ToByteArray(
                (int)Convert.ChangeType(value, typeof(int))),
            _ when valueType == typeof(uint) => DWord.ToByteArray(
                (uint)Convert.ChangeType(value, typeof(uint))),
            _ when valueType == typeof(float) => Real.ToByteArray(
                (float)Convert.ChangeType(value, typeof(float))),
            _ when valueType == typeof(double) => LReal.ToByteArray(
                (double)Convert.ChangeType(value, typeof(double))),
            _ when valueType == typeof(string) => PlcTypes.String.ToByteArray(value as string),
            _ => [],
        };

    /// <summary>Converts an array tag value to S7 bytes.</summary>
    /// <param name="valueType">The array type.</param>
    /// <param name="value">The value to convert.</param>
    /// <returns>The resulting value.</returns>
    private static byte[] ToArrayBytes(Type valueType, object value) =>
        valueType switch
        {
            _ when valueType == typeof(short[]) => Int.ToByteArray((short[])value),
            _ when valueType == typeof(ushort[]) => Word.ToByteArray((ushort[])value),
            _ when valueType == typeof(int[]) => DInt.ToByteArray((int[])value),
            _ when valueType == typeof(uint[]) => DWord.ToByteArray((uint[])value),
            _ when valueType == typeof(float[]) => Real.ToByteArray((float[])value),
            _ when valueType == typeof(double[]) => LReal.ToByteArray((double[])value),
            _ => [],
        };

    /// <summary>Registers the configured tags with the PLC client.</summary>
    private void RegisterTags()
    {
        foreach (var definition in _definitions)
        {
            _ = TagOperations.AddUpdateTagItem(
                    _plc,
                    definition.ValueType,
                    definition.Name,
                    definition.Address,
                    definition.ArrayLength)
                .SetPolling(false);
        }
    }

    /// <summary>Starts polling timers for readable tags.</summary>
    private void StartPollers()
    {
        var intervals = new Dictionary<int, List<S7TagDefinition>>();
        foreach (var definition in _definitions)
        {
            if (!definition.CanRead)
            {
                continue;
            }

            if (!intervals.TryGetValue(definition.PollIntervalMs, out var value))
            {
                value = [];
                intervals[definition.PollIntervalMs] = value;
            }

            value.Add(definition);
        }

        foreach (var interval in intervals)
        {
            _timers.Add(
                new Timer(PollInterval, interval.Value.ToArray(), interval.Key, interval.Key));
        }
    }

    /// <summary>Starts polling for a timer interval.</summary>
    /// <param name="state">The tag definitions assigned to the timer.</param>
    private void PollInterval(object? state)
    {
        if (_disposed || state is not S7TagDefinition[] definitions || definitions.Length == 0)
        {
            return;
        }

        _ = PollIntervalAsync(definitions);
    }

    /// <summary>Polls a group of tag definitions.</summary>
    /// <param name="definitions">The tag definitions to poll.</param>
    /// <returns>The resulting value.</returns>
    private async Task PollIntervalAsync(S7TagDefinition[] definitions)
    {
        if (!await _ioLock.WaitAsync(0).ConfigureAwait(false))
        {
            return;
        }

        try
        {
            var tags = new List<KeyValuePair<S7TagDefinition, S7TagRuntimeAddress>>(
                definitions.Length);
            foreach (var definition in definitions)
            {
                tags.Add(new(definition, _addresses[definition.Name]));
            }

            foreach (var range in BuildRanges(tags))
            {
                var bytes = await ReadRangeAsync(range).ConfigureAwait(false);
                if (bytes is null || bytes.Length == 0)
                {
                    continue;
                }

                foreach (var item in range.Items)
                {
                    var value = Decode(item.Key, item.Value, bytes, range.StartByte);
                    _applyRead(item.Key.Name, value);
                }
            }
        }
        finally
        {
            _ = _ioLock.Release();
        }
    }

    /// <summary>Begins flushing pending tag writes.</summary>
    private void FlushWrites()
    {
        if (_disposed || _pendingWrites.IsEmpty)
        {
            return;
        }

        _ = FlushWritesAsync();
    }

    /// <summary>Flushes pending tag writes.</summary>
    /// <returns>The resulting value.</returns>
    private async Task FlushWritesAsync()
    {
        if (!await _ioLock.WaitAsync(0).ConfigureAwait(false))
        {
            return;
        }

        try
        {
            var pending = new List<KeyValuePair<S7TagDefinition, object?>>();
            foreach (var entry in _pendingWrites.ToArray())
            {
                if (
                    _pendingWrites.TryRemove(entry.Key, out var value)
                    && _definitionsByName.TryGetValue(entry.Key, out var definition))
                {
                    pending.Add(new KeyValuePair<S7TagDefinition, object?>(definition, value));
                }
            }

            var tags = new List<KeyValuePair<S7TagDefinition, S7TagRuntimeAddress>>(pending.Count);
            var values = new Dictionary<string, object?>(StringComparer.InvariantCultureIgnoreCase);
            foreach (var pendingWrite in pending)
            {
                tags.Add(new(pendingWrite.Key, _addresses[pendingWrite.Key.Name]));
                values[pendingWrite.Key.Name] = pendingWrite.Value;
            }

            foreach (var range in BuildRanges(tags))
            {
                var bytes =
                    await ReadRangeAsync(range).ConfigureAwait(false) ?? new byte[range.Length];
                if (bytes.Length < range.Length)
                {
                    Array.Resize(ref bytes, range.Length);
                }

                foreach (var item in range.Items)
                {
                    Encode(item.Key, item.Value, values[item.Key.Name], bytes, range.StartByte);
                }

                _plc.Value(RangeTagName(range), bytes);
            }
        }
        finally
        {
            _ = _ioLock.Release();
        }
    }

    /// <summary>Reads a PLC byte range.</summary>
    /// <param name="range">The byte range to read.</param>
    /// <returns>The resulting value.</returns>
    private Task<byte[]?> ReadRangeAsync(S7TagRange range)
    {
        var tagName = RangeTagName(range);
        _ = TagOperations.AddUpdateTagItem(
                _plc,
                typeof(byte[]),
                tagName,
                $"DB{range.Db}.DBB{range.StartByte}",
                range.Length)
            .SetPolling(false);
        return _plc.ReadAsync(new LogicalTagKey<byte[]>(tagName));
    }

    /// <summary>Represents the runtime address of an S7 tag.</summary>
    private readonly struct S7TagRuntimeAddress
    {
        /// <summary>Initializes a new instance of the <see cref="S7TagRuntimeAddress"/> struct.</summary>
        /// <param name="db">The data block number.</param>
        /// <param name="startByte">The first byte for the tag value.</param>
        /// <param name="bitOffset">The optional bit offset for boolean tags.</param>
        /// <param name="byteLength">The number of bytes occupied by the tag value.</param>
        public S7TagRuntimeAddress(int db, int startByte, int? bitOffset, int byteLength)
        {
            Db = db;
            StartByte = startByte;
            BitOffset = bitOffset;
            ByteLength = byteLength;
        }

        /// <summary>Gets the data block number.</summary>
        public int Db { get; }

        /// <summary>Gets the first byte offset.</summary>
        public int StartByte { get; }

        /// <summary>Gets the optional bit offset.</summary>
        public int? BitOffset { get; }

        /// <summary>Gets the number of bytes occupied by the tag value.</summary>
        public int ByteLength { get; }
    }

    /// <summary>Represents a byte range used by S7 PLC operations.</summary>
    private sealed class S7TagRange
    {
        /// <summary>Initializes a new instance of the <see cref="S7TagRange"/> class.</summary>
        /// <param name="db">The data block number.</param>
        /// <param name="startByte">The first byte included in the range.</param>
        /// <param name="endByte">The byte after the last byte included in the range.</param>
        /// <param name="items">The tag definitions contained in the range.</param>
        public S7TagRange(
            int db,
            int startByte,
            int endByte,
            List<KeyValuePair<S7TagDefinition, S7TagRuntimeAddress>> items)
        {
            Db = db;
            StartByte = startByte;
            EndByte = endByte;
            Items = items;
        }

        /// <summary>Gets the data block number.</summary>
        public int Db { get; }

        /// <summary>Gets the first byte offset.</summary>
        public int StartByte { get; }

        /// <summary>Gets or sets the byte offset after the range.</summary>
        public int EndByte { get; set; }

        /// <summary>Gets the range length in bytes.</summary>
        public int Length => EndByte - StartByte;

        /// <summary>Gets the tag definitions contained in this range.</summary>
        public List<KeyValuePair<S7TagDefinition, S7TagRuntimeAddress>> Items { get; }
    }
}
