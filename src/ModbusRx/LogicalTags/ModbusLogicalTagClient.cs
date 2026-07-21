// Copyright (c) 2019-2026 Chris Pulman and contributors. All rights reserved.
// Chris Pulman and contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;
using CP.IoT.Core;
#if REACTIVE_SHIM
using ModbusRx.Reactive.Device;
#else
using ModbusRx.Device;
#endif

#if REACTIVE_SHIM
namespace ModbusRx.Reactive.LogicalTags;
#else
namespace ModbusRx.LogicalTags;
#endif

/// <summary>Composes a raw Modbus master with logical-name catalog, persistence, and observation APIs.</summary>
public sealed class ModbusLogicalTagClient : ILogicalTagClient, IDisposable
{
    /// <summary>The protocol maximum number of bits per read.</summary>
    private const uint MaximumBitReadCount = 2000U;

    /// <summary>The protocol maximum number of registers per read.</summary>
    private const uint MaximumRegisterReadCount = 125U;

    /// <summary>Serializes access to the raw master transport.</summary>
    private readonly SemaphoreSlim _masterGate = new(1, 1);

    /// <summary>Indicates whether this client owns the catalog.</summary>
    private readonly bool _ownsCatalog;

    /// <summary>Stores the fallback observation interval.</summary>
    private readonly TimeSpan _defaultScanInterval;

    /// <summary>Stores the time provider value.</summary>
    private readonly TimeProvider _timeProvider;

    /// <summary>Stores the initialized persistence adapter.</summary>
    private ModbusTagSqliteStore? _store;

    /// <summary>Stores the disposal state.</summary>
    private int _disposed;

    /// <summary>Initializes a new instance of the <see cref="ModbusLogicalTagClient"/> class.</summary>
    /// <param name="master">The raw Modbus master.</param>
    /// <param name="catalog">The optional existing logical-tag catalog.</param>
    /// <param name="defaultScanInterval">The fallback observation interval.</param>
    public ModbusLogicalTagClient(
        IModbusMaster master,
        ModbusTagCatalog? catalog,
        TimeSpan? defaultScanInterval)
        : this(master, catalog, defaultScanInterval, TimeProvider.System)
    {
    }

    /// <summary>Initializes a new instance of the <see cref="ModbusLogicalTagClient"/> class.</summary>
    /// <param name="master">The raw Modbus master.</param>
    /// <param name="catalog">The optional existing logical-tag catalog.</param>
    /// <param name="defaultScanInterval">The fallback observation interval.</param>
    /// <param name="timeProvider">The time provider used for tag timestamps.</param>
    public ModbusLogicalTagClient(
        IModbusMaster master,
        ModbusTagCatalog? catalog,
        TimeSpan? defaultScanInterval,
        TimeProvider? timeProvider)
    {
        Master = master ?? throw new ArgumentNullException(nameof(master));
        Catalog = catalog ?? new ModbusTagCatalog();
        _ownsCatalog = catalog is null;
        _defaultScanInterval = defaultScanInterval ?? TimeSpan.FromSeconds(1);
        _timeProvider = timeProvider ?? TimeProvider.System;
        _ = _defaultScanInterval > TimeSpan.Zero
            ? true
            : throw new ArgumentOutOfRangeException(nameof(defaultScanInterval));
    }

    /// <summary>Gets the unchanged raw Modbus master.</summary>
    public IModbusMaster Master { get; }

    /// <summary>Gets the composed Modbus tag catalog.</summary>
    public ModbusTagCatalog Catalog { get; }

    /// <summary>Creates and registers a validated logical tag.</summary>
    /// <param name="configuration">The address and behavior configuration.</param>
    /// <returns>The registered definition.</returns>
    public ModbusLogicalTag CreateTag(ModbusTagConfiguration configuration)
    {
        var tag = ModbusTagCatalog.Create(configuration);
        RegisterTag(tag);
        return tag;
    }

    /// <summary>Adds or replaces a logical tag definition.</summary>
    /// <param name="tag">The definition to register.</param>
    public void RegisterTag(ModbusLogicalTag tag)
    {
        ThrowIfDisposed();
        Catalog.Upsert(tag);
    }

    /// <summary>Removes a logical tag definition.</summary>
    /// <param name="name">The logical name.</param>
    /// <returns>True when the definition existed.</returns>
    public bool RemoveTag(string name) => Catalog.TryRemove(name, out _);

    /// <summary>Imports and registers common RFC 4180 CSV definitions.</summary>
    /// <param name="reader">The CSV reader.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The number of imported definitions.</returns>
    public Task<int> ImportCsvAsync(TextReader reader, CancellationToken cancellationToken) =>
        Catalog.ImportCsvAsync(reader, cancellationToken);

    /// <summary>Exports registered definitions as common RFC 4180 CSV.</summary>
    /// <param name="writer">The CSV writer.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A task representing the operation.</returns>
    public Task ExportCsvAsync(TextWriter writer, CancellationToken cancellationToken) =>
        Catalog.ExportCsvAsync(writer, cancellationToken);

    /// <summary>Initializes the SQLite store used by CRUD forwarding methods.</summary>
    /// <param name="connectionString">The SQLite connection string.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A task representing the operation.</returns>
    public async Task InitializeStoreAsync(string connectionString, CancellationToken cancellationToken)
    {
        ThrowIfDisposed();
        var store = new ModbusTagSqliteStore(connectionString);
        await store.InitializeAsync(cancellationToken).ConfigureAwait(false);
        _store = store;
    }

    /// <summary>Replaces registered tags with the current SQLite snapshot.</summary>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The number of loaded definitions.</returns>
    public Task<int> LoadTagsAsync(CancellationToken cancellationToken) =>
        Catalog.LoadFromSqliteAsync(GetStore().CoreStore, cancellationToken);

    /// <summary>Gets a persisted tag by logical name.</summary>
    /// <param name="name">The logical name.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The stored definition, or null.</returns>
    public Task<ModbusLogicalTag?> GetStoredTagAsync(string name, CancellationToken cancellationToken) =>
        GetStore().GetAsync(name, cancellationToken);

    /// <summary>Lists persisted tags.</summary>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The stored definitions.</returns>
    public Task<IReadOnlyList<ModbusLogicalTag>> ListStoredTagsAsync(
        CancellationToken cancellationToken) =>
        GetStore().ListAsync(cancellationToken);

    /// <summary>Creates or replaces a persisted tag and updates the live catalog.</summary>
    /// <param name="tag">The definition to persist.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A task representing the operation.</returns>
    public async Task UpsertStoredTagAsync(ModbusLogicalTag tag, CancellationToken cancellationToken)
    {
        await GetStore().UpsertAsync(tag, cancellationToken).ConfigureAwait(false);
        Catalog.Upsert(tag);
    }

    /// <summary>Updates a persisted tag and the live catalog when it exists.</summary>
    /// <param name="tag">The definition to update.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>True when the definition existed.</returns>
    public async Task<bool> UpdateStoredTagAsync(ModbusLogicalTag tag, CancellationToken cancellationToken)
    {
        var updated = await GetStore().UpdateAsync(tag, cancellationToken).ConfigureAwait(false);
        if (updated)
        {
            Catalog.Upsert(tag);
        }

        return updated;
    }

    /// <summary>Deletes a persisted tag and removes it from the live catalog.</summary>
    /// <param name="name">The logical name.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>True when the definition existed.</returns>
    public async Task<bool> DeleteStoredTagAsync(string name, CancellationToken cancellationToken)
    {
        var deleted = await GetStore().DeleteAsync(name, cancellationToken).ConfigureAwait(false);
        if (deleted)
        {
            _ = Catalog.TryRemove(name, out _);
        }

        return deleted;
    }

    /// <inheritdoc/>
    public async Task<TagOperationResult<LogicalTagValue>> ReadAsync(
        string tagName,
        CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        if (!Catalog.TryGet(tagName, out var tag) || tag is null)
        {
            return TagOperationResult<LogicalTagValue>.Failure($"Logical tag '{tagName}' is not registered.");
        }

        if (tag.AccessMode == LogicalTagAccessMode.Write)
        {
            return TagOperationResult<LogicalTagValue>.Failure($"Logical tag '{tagName}' is write-only.");
        }

        await _masterGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var data = await ReadRawAsync(tag.UnitId, tag.DataArea, tag.Address, tag.Count).ConfigureAwait(false);
            cancellationToken.ThrowIfCancellationRequested();
            return TagOperationResult<LogicalTagValue>.Success(
                new LogicalTagValue(tag.Name, ModbusTagCodec.Decode(tag, data, 0), _timeProvider.GetUtcNow(), "Good"));
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            return TagOperationResult<LogicalTagValue>.Failure(exception.Message);
        }
        finally
        {
            _ = _masterGate.Release();
        }
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<TagOperationResult<LogicalTagValue>>> ReadManyAsync(
        IReadOnlyCollection<string> tagNames,
        CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        if (tagNames is null)
        {
            throw new ArgumentNullException(nameof(tagNames));
        }

        var names = tagNames.ToArray();
        var results = new TagOperationResult<LogicalTagValue>[names.Length];
        var requests = ResolveReadRequests(names, results);
        await _masterGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            await ReadGroupsAsync(requests, results, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _ = _masterGate.Release();
        }

        return results;
    }

    /// <inheritdoc/>
    public async Task<TagOperationResult<LogicalTagValue>> WriteAsync(
        LogicalTagValue value,
        CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        if (value is null)
        {
            throw new ArgumentNullException(nameof(value));
        }

        if (!Catalog.TryGet(value.TagName, out var tag) || tag is null)
        {
            return TagOperationResult<LogicalTagValue>.Failure($"Logical tag '{value.TagName}' is not registered.");
        }

        if (tag.AccessMode == LogicalTagAccessMode.Read ||
            tag.DataArea is ModbusDataArea.DiscreteInput or ModbusDataArea.InputRegister)
        {
            return TagOperationResult<LogicalTagValue>.Failure($"Logical tag '{value.TagName}' is read-only.");
        }

        try
        {
            var data = ModbusTagCodec.Encode(tag, value.Value);
            await _masterGate.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                await WriteRawAsync(tag, data).ConfigureAwait(false);
                cancellationToken.ThrowIfCancellationRequested();
            }
            finally
            {
                _ = _masterGate.Release();
            }

            return TagOperationResult<LogicalTagValue>.Success(
                new LogicalTagValue(tag.Name, value.Value, _timeProvider.GetUtcNow(), "Good"));
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            return TagOperationResult<LogicalTagValue>.Failure(exception.Message);
        }
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<TagOperationResult<LogicalTagValue>>> WriteManyAsync(
        IReadOnlyCollection<LogicalTagValue> values,
        CancellationToken cancellationToken = default)
    {
        if (values is null)
        {
            throw new ArgumentNullException(nameof(values));
        }

        var results = new List<TagOperationResult<LogicalTagValue>>(values.Count);
        foreach (var value in values)
        {
            results.Add(await WriteAsync(value, cancellationToken).ConfigureAwait(false));
        }

        return results;
    }

    /// <inheritdoc/>
    public IObservable<LogicalTagValue> Observe(string tagName) =>
        new AsyncEnumerableObservable<LogicalTagValue>(token => ObserveAsync(tagName, token));

    /// <inheritdoc/>
    public IObservable<LogicalTagValue> ObserveMany(IReadOnlyCollection<string> tagNames) =>
        new AsyncEnumerableObservable<LogicalTagValue>(token => ObserveManyAsync(tagNames, token));

    /// <inheritdoc/>
    public async IAsyncEnumerable<LogicalTagValue> ObserveAsync(
        string tagName,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var result = await ReadAsync(tagName, cancellationToken).ConfigureAwait(false);
            if (!result.Succeeded || result.Value is null)
            {
                throw new InvalidOperationException(result.Error);
            }

            yield return result.Value;
            await Task.Delay(GetScanInterval(tagName), cancellationToken).ConfigureAwait(false);
        }
    }

    /// <inheritdoc/>
    public async IAsyncEnumerable<LogicalTagValue> ObserveManyAsync(
        IReadOnlyCollection<string> tagNames,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var names = (tagNames ?? throw new ArgumentNullException(nameof(tagNames))).ToArray();
        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var results = await ReadManyAsync(names, cancellationToken).ConfigureAwait(false);
            foreach (var result in results)
            {
                if (!result.Succeeded || result.Value is null)
                {
                    throw new InvalidOperationException(result.Error);
                }

                yield return result.Value;
            }

            await Task.Delay(GetScanInterval(names), cancellationToken).ConfigureAwait(false);
        }
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0)
        {
            return;
        }

        _masterGate.Dispose();
        if (!_ownsCatalog)
        {
            return;
        }

        Catalog.Dispose();
    }

    /// <summary>Resolves readable requests and records expected lookup failures.</summary>
    /// <param name="names">The logical names.</param>
    /// <param name="results">The result destinations.</param>
    /// <returns>The readable requests.</returns>
    private List<ReadRequest> ResolveReadRequests(
        string[] names,
        TagOperationResult<LogicalTagValue>[] results)
    {
        var requests = new List<ReadRequest>(names.Length);
        for (var index = 0; index < names.Length; index++)
        {
            if (!Catalog.TryGet(names[index], out var tag) || tag is null)
            {
                results[index] = TagOperationResult<LogicalTagValue>.Failure(
                    $"Logical tag '{names[index]}' is not registered.");
            }
            else if (tag.AccessMode == LogicalTagAccessMode.Write)
            {
                results[index] = TagOperationResult<LogicalTagValue>.Failure(
                    $"Logical tag '{names[index]}' is write-only.");
            }
            else
            {
                requests.Add(new ReadRequest(index, tag));
            }
        }

        return requests;
    }

    /// <summary>Reads each unit/data-area group through coalesced ranges.</summary>
    /// <param name="requests">The resolved requests.</param>
    /// <param name="results">The result destinations.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A task representing the operation.</returns>
    private async Task ReadGroupsAsync(
        IEnumerable<ReadRequest> requests,
        TagOperationResult<LogicalTagValue>[] results,
        CancellationToken cancellationToken)
    {
        static (int End, uint RangeEnd) FindRangeEnd(IReadOnlyList<ReadRequest> orderedRequests, int startIndex)
        {
            var endIndex = startIndex + 1;
            var rawEnd = (uint)orderedRequests[startIndex].Tag.Address + orderedRequests[startIndex].Tag.Count;
            var maximum = orderedRequests[startIndex].Tag.DataArea is
                ModbusDataArea.Coil or ModbusDataArea.DiscreteInput
                ? MaximumBitReadCount
                : MaximumRegisterReadCount;
            while (endIndex < orderedRequests.Count)
            {
                var candidate = orderedRequests[endIndex].Tag;
                var candidateEnd = (uint)candidate.Address + candidate.Count;
                if (candidate.Address > rawEnd || candidateEnd - orderedRequests[startIndex].Tag.Address > maximum)
                {
                    break;
                }

                rawEnd = Math.Max(rawEnd, candidateEnd);
                endIndex++;
            }

            return (endIndex, rawEnd);
        }

        foreach (var group in requests.GroupBy(static request => (request.Tag.UnitId, request.Tag.DataArea)))
        {
            var ordered = group.OrderBy(static request => request.Tag.Address).ToArray();
            var start = 0;
            while (start < ordered.Length)
            {
                var (end, rangeEnd) = FindRangeEnd(ordered, start);
                await ReadRangeAsync(ordered, start, end, rangeEnd, results, cancellationToken).ConfigureAwait(false);
                start = end;
            }
        }
    }

    /// <summary>Reads and decodes one coalesced raw range.</summary>
    /// <param name="requests">The resolved requests.</param>
    /// <param name="start">The inclusive request index.</param>
    /// <param name="end">The exclusive request index.</param>
    /// <param name="rangeEnd">The exclusive raw range end.</param>
    /// <param name="results">The result destinations.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A task representing the operation.</returns>
    private async Task ReadRangeAsync(
        IReadOnlyList<ReadRequest> requests,
        int start,
        int end,
        uint rangeEnd,
        TagOperationResult<LogicalTagValue>[] results,
        CancellationToken cancellationToken)
    {
        var first = requests[start].Tag;
        try
        {
            var data = await ReadRawAsync(
                first.UnitId,
                first.DataArea,
                first.Address,
                checked((ushort)(rangeEnd - first.Address))).ConfigureAwait(false);
            cancellationToken.ThrowIfCancellationRequested();
            for (var index = start; index < end; index++)
            {
                var request = requests[index];
                try
                {
                    var value = ModbusTagCodec.Decode(request.Tag, data, request.Tag.Address - first.Address);
                    results[request.Index] = TagOperationResult<LogicalTagValue>.Success(
                        new LogicalTagValue(request.Tag.Name, value, _timeProvider.GetUtcNow(), "Good"));
                }
                catch (Exception exception)
                {
                    results[request.Index] = TagOperationResult<LogicalTagValue>.Failure(exception.Message);
                }
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            for (var index = start; index < end; index++)
            {
                results[requests[index].Index] = TagOperationResult<LogicalTagValue>.Failure(exception.Message);
            }
        }
    }

    /// <summary>Reads one raw Modbus range.</summary>
    /// <param name="unitId">The Modbus unit identifier.</param>
    /// <param name="area">The data area.</param>
    /// <param name="address">The starting address.</param>
    /// <param name="count">The point count.</param>
    /// <returns>The raw response.</returns>
    private async Task<Array> ReadRawAsync(byte unitId, ModbusDataArea area, ushort address, ushort count)
    {
        return area switch
        {
            ModbusDataArea.Coil => await Master.ReadCoilsAsync(unitId, address, count).ConfigureAwait(false),
            ModbusDataArea.DiscreteInput => await Master.ReadInputsAsync(unitId, address, count).ConfigureAwait(false),
            ModbusDataArea.HoldingRegister => await Master
                .ReadHoldingRegistersAsync(unitId, address, count)
                .ConfigureAwait(false),
            ModbusDataArea.InputRegister => await Master
                .ReadInputRegistersAsync(unitId, address, count)
                .ConfigureAwait(false),
            _ => throw new ArgumentOutOfRangeException(nameof(area)),
        };
    }

    /// <summary>Writes one encoded raw Modbus value.</summary>
    /// <param name="tag">The tag definition.</param>
    /// <param name="data">The encoded raw points.</param>
    /// <returns>A task representing the operation.</returns>
    private Task WriteRawAsync(ModbusLogicalTag tag, Array data)
    {
        if (tag.DataArea == ModbusDataArea.Coil)
        {
            var values = (bool[])data;
            return values.Length == 1
                ? Master.WriteSingleCoilAsync(tag.UnitId, tag.Address, values[0])
                : Master.WriteMultipleCoilsAsync(tag.UnitId, tag.Address, values);
        }

        if (tag.DataArea == ModbusDataArea.HoldingRegister)
        {
            var values = (ushort[])data;
            return values.Length == 1
                ? Master.WriteSingleRegisterAsync(tag.UnitId, tag.Address, values[0])
                : Master.WriteMultipleRegistersAsync(tag.UnitId, tag.Address, values);
        }

        throw new InvalidOperationException("Modbus input areas cannot be written.");
    }

    /// <summary>Gets the current observation interval for one tag.</summary>
    /// <param name="tagName">The logical name.</param>
    /// <returns>The scan interval.</returns>
    private TimeSpan GetScanInterval(string tagName) =>
        Catalog.TryGet(tagName, out var tag) && tag is not null
            ? tag.ScanInterval ?? _defaultScanInterval
            : _defaultScanInterval;

    /// <summary>Gets the shortest current observation interval for a set of tags.</summary>
    /// <param name="tagNames">The logical names.</param>
    /// <returns>The shortest scan interval.</returns>
    private TimeSpan GetScanInterval(IEnumerable<string> tagNames)
    {
        var interval = _defaultScanInterval;
        foreach (var name in tagNames)
        {
            var candidate = GetScanInterval(name);
            if (candidate < interval)
            {
                interval = candidate;
            }
        }

        return interval;
    }

    /// <summary>Gets the initialized persistence store.</summary>
    /// <returns>The initialized store.</returns>
    private ModbusTagSqliteStore GetStore()
    {
        ThrowIfDisposed();
        return _store ?? throw new InvalidOperationException(
            "InitializeStoreAsync must be called before using SQLite tag CRUD.");
    }

    /// <summary>Throws when this client has been disposed.</summary>
    private void ThrowIfDisposed()
    {
        _ = Volatile.Read(ref _disposed) == 0
            ? true
            : throw new ObjectDisposedException(nameof(ModbusLogicalTagClient));
    }

    /// <summary>Associates a resolved tag with its requested result index.</summary>
    /// <param name="index">The requested result index.</param>
    /// <param name="tag">The resolved definition.</param>
    private sealed class ReadRequest(int index, ModbusLogicalTag tag)
    {
        /// <summary>Gets the requested result index.</summary>
        public int Index { get; } = index;

        /// <summary>Gets the resolved definition.</summary>
        public ModbusLogicalTag Tag { get; } = tag;
    }

    /// <summary>Adapts a cold async-enumerable factory to the classic observable contract.</summary>
    /// <typeparam name="T">The notification type.</typeparam>
    /// <param name="factory">The cold async-enumerable factory.</param>
    private sealed class AsyncEnumerableObservable<T>(
        Func<CancellationToken, IAsyncEnumerable<T>> factory) : IObservable<T>
    {
        /// <inheritdoc/>
        public IDisposable Subscribe(IObserver<T> observer)
        {
            if (observer is null)
            {
                throw new ArgumentNullException(nameof(observer));
            }

            var cancellation = new CancellationTokenSource();
            _ = RunAsync(observer, cancellation.Token);
            return new CancellationSubscription(cancellation);
        }

        /// <summary>Forwards async-enumerable notifications to a classic observer.</summary>
        /// <param name="observer">The classic observer.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>A task representing the observation loop.</returns>
        private async Task RunAsync(IObserver<T> observer, CancellationToken cancellationToken)
        {
            try
            {
                await foreach (var value in factory(cancellationToken)
                    .WithCancellation(cancellationToken)
                    .ConfigureAwait(false))
                {
                    observer.OnNext(value);
                }

                observer.OnCompleted();
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
            }
            catch (Exception exception)
            {
                observer.OnError(exception);
            }
        }
    }

    /// <summary>Cancels an observation loop when its subscription is disposed.</summary>
    /// <param name="cancellation">The observation cancellation source.</param>
    private sealed class CancellationSubscription(CancellationTokenSource cancellation) : IDisposable
    {
        /// <summary>Stores the active cancellation source.</summary>
        private CancellationTokenSource? _cancellation = cancellation;

        /// <inheritdoc/>
        public void Dispose()
        {
            var source = _cancellation;
            _cancellation = null;
            if (source is null)
            {
                return;
            }

            source.Cancel();
            source.Dispose();
        }
    }
}
