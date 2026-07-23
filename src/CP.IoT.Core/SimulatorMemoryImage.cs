// Copyright (c) 2019-2026 Chris Pulman and contributors. All rights reserved.
// Chris Pulman and contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Collections.ObjectModel;

namespace IoT.DriverCore.Core;

/// <summary>Provides a thread-safe sparse byte image, typed codec views, ordered journaling, and change subscriptions.</summary>
public sealed class SimulatorMemoryImage : IObservable<SimulatorMemoryChange>
{
    /// <summary>The default retained journal capacity.</summary>
    private const int DefaultJournalCapacity = 1024;

    /// <summary>Provides timestamps for journal changes.</summary>
    private readonly ISimulatorClock _clock;

    /// <summary>Protects all mutable image state.</summary>
    private readonly Lock _gate = new();

    /// <summary>Limits retained journal entries.</summary>
    private readonly int _journalCapacity;

    /// <summary>Contains retained changes in ascending sequence order.</summary>
    private readonly List<SimulatorMemoryChange> _journal = [];

    /// <summary>Contains sparse bytes by physical memory partition.</summary>
    private readonly Dictionary<MemoryPartition, Dictionary<long, byte>> _partitions = [];

    /// <summary>Contains active change observers.</summary>
    private readonly Dictionary<long, IObserver<SimulatorMemoryChange>> _observers = [];

    /// <summary>Stores the last assigned observer identifier.</summary>
    private long _nextObserverId;

    /// <summary>Stores the last assigned journal sequence.</summary>
    private long _sequence;

    /// <summary>Initializes a new instance of the <see cref="SimulatorMemoryImage"/> class using system time.</summary>
    public SimulatorMemoryImage()
        : this(SystemSimulatorClock.Instance, DefaultJournalCapacity)
    {
    }

    /// <summary>Initializes a new instance of the <see cref="SimulatorMemoryImage"/> class.</summary>
    /// <param name="clock">The clock used to timestamp changes.</param>
    public SimulatorMemoryImage(ISimulatorClock clock)
        : this(clock, DefaultJournalCapacity)
    {
    }

    /// <summary>Initializes a new instance of the <see cref="SimulatorMemoryImage"/> class.</summary>
    /// <param name="clock">The clock used to timestamp changes.</param>
    /// <param name="journalCapacity">The positive maximum number of changes retained in memory.</param>
    public SimulatorMemoryImage(ISimulatorClock clock, int journalCapacity)
    {
        if (journalCapacity <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(journalCapacity));
        }

        _clock = clock ?? throw new ArgumentNullException(nameof(clock));
        _journalCapacity = journalCapacity;
    }

    /// <summary>Gets the most recently assigned change sequence, or zero before the first write.</summary>
    public long CurrentSequence
    {
        get
        {
            lock (_gate)
            {
                return _sequence;
            }
        }
    }

    /// <summary>Reads an exact byte range, returning zero for every location that has not been written.</summary>
    /// <param name="address">The byte-addressed range to read.</param>
    /// <returns>A defensive byte-array snapshot.</returns>
    public byte[] Read(TagTransportAddress address)
    {
        if (address is null)
        {
            throw new ArgumentNullException(nameof(address));
        }

        if (address.Length > int.MaxValue)
        {
            throw new ArgumentOutOfRangeException(nameof(address), "Simulator byte ranges cannot exceed Int32.MaxValue.");
        }

        lock (_gate)
        {
            return ReadUnderLock(address);
        }
    }

    /// <summary>Reads and decodes an exact byte range using a caller-provided typed codec.</summary>
    /// <typeparam name="T">The decoded value type.</typeparam>
    /// <param name="address">The byte-addressed range to read.</param>
    /// <param name="decoder">The decoder applied to the defensive byte snapshot.</param>
    /// <returns>The decoded value.</returns>
    public T Read<T>(TagTransportAddress address, Func<IReadOnlyList<byte>, T> decoder)
    {
        if (decoder is null)
        {
            throw new ArgumentNullException(nameof(decoder));
        }

        return decoder(Read(address));
    }

    /// <summary>Writes an exact byte range and records one journal entry.</summary>
    /// <param name="address">The byte-addressed range to write.</param>
    /// <param name="bytes">Exactly <see cref="TagTransportAddress.Length"/> bytes.</param>
    /// <returns>The immutable change appended to the journal.</returns>
    public SimulatorMemoryChange Write(TagTransportAddress address, IReadOnlyList<byte> bytes)
    {
        if (address is null)
        {
            throw new ArgumentNullException(nameof(address));
        }

        if (bytes is null)
        {
            throw new ArgumentNullException(nameof(bytes));
        }

        if (address.Length != bytes.Count)
        {
            throw new ArgumentException("The byte count must equal the transport-address length.", nameof(bytes));
        }

        var current = bytes.ToArray();
        SimulatorMemoryChange change;
        IObserver<SimulatorMemoryChange>[] observers;
        lock (_gate)
        {
            var previous = ReadUnderLock(address);
            var partition = GetOrAddPartitionUnderLock(address);
            for (var index = 0; index < current.Length; index++)
            {
                partition[address.Offset + index] = current[index];
            }

            _sequence = checked(_sequence + 1);
            change = new(_sequence, _clock.UtcNow, address, previous, current);
            _journal.Add(change);
            if (_journal.Count > _journalCapacity)
            {
                _journal.RemoveAt(0);
            }

            observers = _observers.Values.ToArray();
        }

        foreach (var observer in observers)
        {
            observer.OnNext(change);
        }

        return change;
    }

    /// <summary>Encodes and writes a typed value using a caller-provided codec.</summary>
    /// <typeparam name="T">The encoded value type.</typeparam>
    /// <param name="address">The byte-addressed range to write.</param>
    /// <param name="value">The typed value to encode.</param>
    /// <param name="encoder">The encoder that produces the exact byte range.</param>
    /// <returns>The immutable change appended to the journal.</returns>
    public SimulatorMemoryChange Write<T>(
        TagTransportAddress address,
        T value,
        Func<T, IReadOnlyList<byte>> encoder)
    {
        if (encoder is null)
        {
            throw new ArgumentNullException(nameof(encoder));
        }

        return Write(address, encoder(value));
    }

    /// <summary>Returns retained changes with a sequence greater than the supplied cursor.</summary>
    /// <returns>An immutable snapshot in ascending sequence order.</returns>
    public IReadOnlyList<SimulatorMemoryChange> GetChanges() => GetChanges(0);

    /// <summary>Returns retained changes with a sequence greater than the supplied cursor.</summary>
    /// <param name="afterSequence">The non-negative exclusive sequence cursor.</param>
    /// <returns>An immutable snapshot in ascending sequence order.</returns>
    public IReadOnlyList<SimulatorMemoryChange> GetChanges(long afterSequence)
    {
        if (afterSequence < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(afterSequence));
        }

        lock (_gate)
        {
            return new ReadOnlyCollection<SimulatorMemoryChange>(
                _journal.Where(change => change.Sequence > afterSequence).ToArray());
        }
    }

    /// <inheritdoc/>
    public IDisposable Subscribe(IObserver<SimulatorMemoryChange> observer)
    {
        if (observer is null)
        {
            throw new ArgumentNullException(nameof(observer));
        }

        long observerId;
        lock (_gate)
        {
            observerId = checked(_nextObserverId + 1);
            _nextObserverId = observerId;
            _observers.Add(observerId, observer);
        }

        return new Subscription(this, observerId);
    }

    /// <summary>Gets or creates a sparse physical partition while the image lock is held.</summary>
    /// <param name="address">The address identifying the partition.</param>
    /// <returns>The sparse partition.</returns>
    private Dictionary<long, byte> GetOrAddPartitionUnderLock(TagTransportAddress address)
    {
        var key = new MemoryPartition(address);
        if (!_partitions.TryGetValue(key, out var partition))
        {
            partition = [];
            _partitions.Add(key, partition);
        }

        return partition;
    }

    /// <summary>Reads a byte snapshot while the image lock is held.</summary>
    /// <param name="address">The exact byte range.</param>
    /// <returns>The byte snapshot.</returns>
    private byte[] ReadUnderLock(TagTransportAddress address)
    {
        var bytes = new byte[checked((int)address.Length)];
        if (_partitions.TryGetValue(new(address), out var partition))
        {
            for (var index = 0; index < bytes.Length; index++)
            {
                if (partition.TryGetValue(address.Offset + index, out var value))
                {
                    bytes[index] = value;
                }
            }
        }

        return bytes;
    }

    /// <summary>Removes an observer registration.</summary>
    /// <param name="observerId">The observer identifier.</param>
    private void Unsubscribe(long observerId)
    {
        lock (_gate)
        {
            _ = _observers.Remove(observerId);
        }
    }

    /// <summary>Identifies bytes that share a physical transport partition, memory area, and route.</summary>
    private readonly struct MemoryPartition : IEquatable<MemoryPartition>
    {
        /// <summary>The memory area.</summary>
        private readonly string _memoryArea;

        /// <summary>The route.</summary>
        private readonly string _route;

        /// <summary>The transport partition.</summary>
        private readonly string _transportPartition;

        /// <summary>Initializes a new instance of the <see cref="MemoryPartition"/> struct.</summary>
        /// <param name="address">The source address.</param>
        internal MemoryPartition(TagTransportAddress address)
        {
            _transportPartition = address.TransportPartition;
            _memoryArea = address.MemoryArea;
            _route = address.Route;
        }

        /// <inheritdoc/>
        public bool Equals(MemoryPartition other) =>
            StringComparer.Ordinal.Equals(_transportPartition, other._transportPartition)
            && StringComparer.Ordinal.Equals(_memoryArea, other._memoryArea)
            && StringComparer.Ordinal.Equals(_route, other._route);

        /// <inheritdoc/>
        public override bool Equals(object? obj) => obj is MemoryPartition other && Equals(other);

        /// <inheritdoc/>
        public override int GetHashCode()
        {
            unchecked
            {
                var hash = StringComparer.Ordinal.GetHashCode(_transportPartition);
                hash = (hash * 397) ^ StringComparer.Ordinal.GetHashCode(_memoryArea);
                return (hash * 397) ^ StringComparer.Ordinal.GetHashCode(_route);
            }
        }
    }

    /// <summary>Removes an observer registration exactly once.</summary>
    private sealed class Subscription : IDisposable
    {
        /// <summary>The observer identifier.</summary>
        private readonly long _observerId;

        /// <summary>The owning image until disposal.</summary>
        private SimulatorMemoryImage? _owner;

        /// <summary>Initializes a new instance of the <see cref="Subscription"/> class.</summary>
        /// <param name="owner">The owning image.</param>
        /// <param name="observerId">The observer identifier.</param>
        internal Subscription(SimulatorMemoryImage owner, long observerId)
        {
            _owner = owner;
            _observerId = observerId;
        }

        /// <inheritdoc/>
        public void Dispose()
        {
            var owner = Interlocked.Exchange(ref _owner, null);
            owner?.Unsubscribe(_observerId);
        }
    }
}
