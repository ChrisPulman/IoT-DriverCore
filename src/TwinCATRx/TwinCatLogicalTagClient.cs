// Copyright (c) 2019-2026 Chris Pulman and contributors. All rights reserved.
// Chris Pulman and contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics.CodeAnalysis;
using CP.IoT.Core;
#if REACTIVE_SHIM
using CP.Collections.Reactive;
#else
using CP.Collections;
#endif

#if REACTIVE_SHIM
namespace CP.TwinCatRx.Reactive;
#else
namespace CP.TwinCatRx;
#endif

/// <summary>Maps logical CP.IoT tags onto an event-driven TwinCAT ADS client.</summary>
public sealed partial class TwinCatLogicalTagClient : ILogicalTagClient, IDisposable
{
    /// <summary>Stores the member-path metadata key.</summary>
    private const string MemberAddressMetadata = "MemberAddress";

    /// <summary>Stores the structure-root metadata key.</summary>
    private const string RootAddressMetadata = "StructureRoot";

    /// <summary>Stores the alternate write-address metadata key.</summary>
    private const string WriteAddressMetadata = "WriteAddress";

    /// <summary>Stores the composed ADS client.</summary>
    private readonly IRxTcAdsClient _client;

    /// <summary>Stores the construction-time-approved HashTableRx materializer.</summary>
    private readonly Func<object?, HashTableRx> _createStructureTable;

    /// <summary>Stores the construction-time-approved HashTableRx root projector.</summary>
    private readonly Func<HashTableRx, object?> _getStructure;

    /// <summary>Stores whether this instance owns the catalog lifetime.</summary>
    private readonly bool _ownsCatalog;

    /// <summary>Stores the optional persistence service.</summary>
    private readonly LogicalTagSqliteStore? _store;

    /// <summary>Stores the time provider used to obtain the current time.</summary>
    private readonly TimeProvider _timeProvider;

    /// <summary>Stores the last operation correlation number.</summary>
    private long _operationId;

    /// <summary>Stores whether this instance is disposed.</summary>
    private int _disposed;

    /// <summary>Initializes a new instance of the <see cref="TwinCatLogicalTagClient"/> class.</summary>
    /// <param name="nativeClient">The composed ADS client.</param>
    [RequiresUnreferencedCode("Logical TwinCAT tags may route through HashTableRx structure materialization.")]
    public TwinCatLogicalTagClient(IRxTcAdsClient nativeClient)
        : this(nativeClient, new LogicalTagCatalog(), null, ownsCatalog: true, TimeProvider.System)
    {
    }

    /// <summary>Initializes a new instance of the <see cref="TwinCatLogicalTagClient"/> class.</summary>
    /// <param name="nativeClient">The composed ADS client.</param>
    /// <param name="timeProvider">The time provider.</param>
    [RequiresUnreferencedCode("Logical TwinCAT tags may route through HashTableRx structure materialization.")]
    public TwinCatLogicalTagClient(IRxTcAdsClient nativeClient, TimeProvider timeProvider)
        : this(nativeClient, new LogicalTagCatalog(), null, ownsCatalog: true, timeProvider)
    {
    }

    /// <summary>Initializes a new instance of the <see cref="TwinCatLogicalTagClient"/> class.</summary>
    /// <param name="nativeClient">The composed ADS client.</param>
    /// <param name="catalog">The caller-owned catalog.</param>
    [RequiresUnreferencedCode("Logical TwinCAT tags may route through HashTableRx structure materialization.")]
    public TwinCatLogicalTagClient(IRxTcAdsClient nativeClient, ILogicalTagCatalog catalog)
        : this(nativeClient, catalog, null, ownsCatalog: false, TimeProvider.System)
    {
    }

    /// <summary>Initializes a new instance of the <see cref="TwinCatLogicalTagClient"/> class.</summary>
    /// <param name="nativeClient">The composed ADS client.</param>
    /// <param name="catalog">The caller-owned catalog.</param>
    /// <param name="timeProvider">The time provider.</param>
    [RequiresUnreferencedCode("Logical TwinCAT tags may route through HashTableRx structure materialization.")]
    public TwinCatLogicalTagClient(IRxTcAdsClient nativeClient, ILogicalTagCatalog catalog, TimeProvider timeProvider)
        : this(nativeClient, catalog, null, ownsCatalog: false, timeProvider)
    {
    }

    /// <summary>Initializes a new instance of the <see cref="TwinCatLogicalTagClient"/> class.</summary>
    /// <param name="nativeClient">The composed ADS client.</param>
    /// <param name="store">The SQLite store.</param>
    [RequiresUnreferencedCode("Logical TwinCAT tags may route through HashTableRx structure materialization.")]
    public TwinCatLogicalTagClient(IRxTcAdsClient nativeClient, LogicalTagSqliteStore store)
        : this(nativeClient, new LogicalTagCatalog(), store, ownsCatalog: true, TimeProvider.System)
    {
    }

    /// <summary>Initializes a new instance of the <see cref="TwinCatLogicalTagClient"/> class.</summary>
    /// <param name="nativeClient">The composed ADS client.</param>
    /// <param name="store">The SQLite store.</param>
    /// <param name="timeProvider">The time provider.</param>
    [RequiresUnreferencedCode("Logical TwinCAT tags may route through HashTableRx structure materialization.")]
    public TwinCatLogicalTagClient(IRxTcAdsClient nativeClient, LogicalTagSqliteStore store, TimeProvider timeProvider)
        : this(nativeClient, new LogicalTagCatalog(), store, ownsCatalog: true, timeProvider)
    {
    }

    /// <summary>Initializes a new instance of the <see cref="TwinCatLogicalTagClient"/> class.</summary>
    /// <param name="nativeClient">The composed ADS client.</param>
    /// <param name="catalog">The caller-owned catalog.</param>
    /// <param name="store">The SQLite store.</param>
    [RequiresUnreferencedCode("Logical TwinCAT tags may route through HashTableRx structure materialization.")]
    public TwinCatLogicalTagClient(
        IRxTcAdsClient nativeClient,
        ILogicalTagCatalog catalog,
        LogicalTagSqliteStore store)
        : this(nativeClient, catalog, store, ownsCatalog: false, TimeProvider.System)
    {
    }

    /// <summary>Initializes a new instance of the <see cref="TwinCatLogicalTagClient"/> class.</summary>
    /// <param name="nativeClient">The composed ADS client.</param>
    /// <param name="catalog">The caller-owned catalog.</param>
    /// <param name="store">The SQLite store.</param>
    /// <param name="timeProvider">The time provider.</param>
    [RequiresUnreferencedCode("Logical TwinCAT tags may route through HashTableRx structure materialization.")]
    public TwinCatLogicalTagClient(
        IRxTcAdsClient nativeClient,
        ILogicalTagCatalog catalog,
        LogicalTagSqliteStore store,
        TimeProvider timeProvider)
        : this(nativeClient, catalog, store, ownsCatalog: false, timeProvider)
    {
    }

    /// <summary>Initializes a new instance of the <see cref="TwinCatLogicalTagClient"/> class.</summary>
    /// <param name="nativeClient">The composed ADS client.</param>
    /// <param name="catalog">The tag catalog.</param>
    /// <param name="store">The optional SQLite store.</param>
    /// <param name="ownsCatalog">Whether the client owns the catalog.</param>
    /// <param name="timeProvider">The time provider.</param>
    [RequiresUnreferencedCode("Logical TwinCAT tags may route through HashTableRx structure materialization.")]
    private TwinCatLogicalTagClient(
        IRxTcAdsClient nativeClient,
        ILogicalTagCatalog catalog,
        LogicalTagSqliteStore? store,
        bool ownsCatalog,
        TimeProvider timeProvider)
    {
        _client = nativeClient ?? throw new ArgumentNullException(nameof(nativeClient));
        Catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
        _ownsCatalog = ownsCatalog;
        _store = store;
        _timeProvider = timeProvider;
        _createStructureTable = static data =>
        {
            var table = new HashTableRx(false);
            table.SetStructure(data);
            return table;
        };
        _getStructure = static table => table.Structure;
    }

    /// <summary>Gets the logical tag catalog.</summary>
    public ILogicalTagCatalog Catalog { get; }

    /// <summary>Creates and registers a logical TwinCAT tag.</summary>
    /// <param name="name">The logical name.</param>
    /// <param name="address">The ADS address.</param>
    /// <param name="dataType">The logical data type.</param>
    /// <returns>The registered tag.</returns>
    public LogicalTag CreateTag(string name, string address, string dataType)
    {
        return CreateTag(new LogicalTag(name, address, dataType));
    }

    /// <summary>Creates a tag from a complete shared tag definition and registers it.</summary>
    /// <param name="tag">The complete shared tag definition.</param>
    /// <returns>The registered tag.</returns>
    public LogicalTag CreateTag(LogicalTag tag)
    {
        RegisterTag(tag);
        return tag;
    }

    /// <summary>Adds or replaces a logical tag in the live registry.</summary>
    /// <param name="tag">The logical tag.</param>
    public void RegisterTag(LogicalTag tag)
    {
        ThrowIfDisposed();
        Catalog.Upsert(tag ?? throw new ArgumentNullException(nameof(tag)));
    }

    /// <summary>Removes a logical tag from the live registry.</summary>
    /// <param name="name">The logical tag name.</param>
    /// <returns>Whether the tag was removed.</returns>
    public bool RemoveTag(string name)
    {
        ThrowIfDisposed();
        return Catalog.TryRemove(TwinCatLogicalTagHelpers.Required(name, nameof(name)), out _);
    }

    /// <inheritdoc/>
    public async Task<TagOperationResult<LogicalTagValue>> ReadAsync(
        string tagName,
        CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        if (!TryGetReadableTag(tagName, out var tag, out var failure))
        {
            return failure;
        }

        var route = ResolveRoute(tag);
        var data = await ReadAddressAsync(route.RootAddress, cancellationToken).ConfigureAwait(false);
        return CreateReadResult(tag, route, data);
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
        var requests = new List<ReadRequest>();
        for (var index = 0; index < names.Length; index++)
        {
            if (!TryGetReadableTag(names[index], out var tag, out var failure))
            {
                results[index] = failure;
                continue;
            }

            requests.Add(new ReadRequest(index, tag, ResolveRoute(tag)));
        }

        foreach (var group in requests.GroupBy(
                     static request => request.Route.RootAddress,
                     StringComparer.OrdinalIgnoreCase))
        {
            cancellationToken.ThrowIfCancellationRequested();
            var data = await ReadAddressAsync(group.Key, cancellationToken).ConfigureAwait(false);
            foreach (var request in group)
            {
                results[request.Index] = CreateReadResult(request.Tag, request.Route, data);
            }
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

        if (!TryGetWritableTag(value.TagName, out var tag, out var failure))
        {
            return failure;
        }

        var route = ResolveRoute(tag);
        if (route.MemberAddress is null)
        {
            return await WriteAddressAsync(route.WriteAddress, value, cancellationToken).ConfigureAwait(false);
        }

        var rootValue = await ReadAddressAsync(route.RootAddress, cancellationToken).ConfigureAwait(false);
        using var table = _createStructureTable(rootValue);
        table[route.MemberAddress] = value.Value;
        var structure = _getStructure(table);
        return structure is null
            ? TagOperationResult<LogicalTagValue>.Failure(
                $"TwinCAT structure '{route.RootAddress}' could not be materialized.")
            : await WriteAddressAsync(route.RootAddress, value, cancellationToken, structure).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<TagOperationResult<LogicalTagValue>>> WriteManyAsync(
        IReadOnlyCollection<LogicalTagValue> values,
        CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        if (values is null)
        {
            throw new ArgumentNullException(nameof(values));
        }

        var items = values.ToArray();
        var results = new TagOperationResult<LogicalTagValue>[items.Length];
        var structured = new List<WriteRequest>();
        for (var index = 0; index < items.Length; index++)
        {
            var value = items[index] ??
                throw new ArgumentException("Values cannot contain null entries.", nameof(values));
            await PrepareWriteRequestAsync(index, value, results, structured, cancellationToken).ConfigureAwait(false);
        }

        foreach (var group in structured.GroupBy(
                     static request => request.Route.RootAddress,
                     StringComparer.OrdinalIgnoreCase))
        {
            await WriteStructureGroupAsync(group, results, cancellationToken).ConfigureAwait(false);
        }

        return results;
    }

    /// <inheritdoc/>
    public IObservable<LogicalTagValue> Observe(string tagName)
    {
        ThrowIfDisposed();
        if (!Catalog.TryGet(TwinCatLogicalTagHelpers.Required(tagName, nameof(tagName)), out var tag) || tag is null)
        {
            throw new KeyNotFoundException($"Logical TwinCAT tag '{tagName}' is not registered.");
        }

        if (tag.AccessMode == LogicalTagAccessMode.Write)
        {
            throw new InvalidOperationException($"Logical TwinCAT tag '{tag.Name}' is write-only.");
        }

        var route = ResolveRoute(tag);
        return Observable.Create<LogicalTagValue>(observer =>
            ObservableBridgeExtensions.SubscribeTo(
                _client.DataReceived,
                data => PublishObservedValue(observer, tag, route, data),
                observer.OnError,
                observer.OnCompleted));
    }

    /// <inheritdoc/>
    public IObservable<LogicalTagValue> ObserveMany(IReadOnlyCollection<string> tagNames)
    {
        ThrowIfDisposed();
        if (tagNames is null)
        {
            throw new ArgumentNullException(nameof(tagNames));
        }

        var sources = tagNames.Select(Observe).ToArray();
        return Observable.Create<LogicalTagValue>(observer =>
        {
            var subscriptions = new CompositeDisposable();
            foreach (var source in sources)
            {
                subscriptions.Add(source.Subscribe(observer));
            }

            return subscriptions;
        });
    }

    /// <inheritdoc/>
    public IAsyncEnumerable<LogicalTagValue> ObserveAsync(
        string tagName,
        CancellationToken cancellationToken = default) =>
        LogicalTagObservableAsyncBridge.ObserveAsync(Observe(tagName), cancellationToken);

    /// <inheritdoc/>
    public IAsyncEnumerable<LogicalTagValue> ObserveManyAsync(
        IReadOnlyCollection<string> tagNames,
        CancellationToken cancellationToken = default) =>
        LogicalTagObservableAsyncBridge.ObserveAsync(ObserveMany(tagNames), cancellationToken);

    /// <summary>Releases registry-owned resources without disposing the composed ADS client.</summary>
    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0)
        {
            return;
        }

        if (!_ownsCatalog)
        {
            return;
        }

        (Catalog as IDisposable)?.Dispose();
    }

    /// <summary>Prepares one direct or structure-backed bulk write.</summary>
    /// <param name="index">The result index.</param>
    /// <param name="value">The logical value.</param>
    /// <param name="results">The result buffer.</param>
    /// <param name="structured">The pending structure writes.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The preparation operation.</returns>
    private async Task PrepareWriteRequestAsync(
        int index,
        LogicalTagValue value,
        TagOperationResult<LogicalTagValue>[] results,
        List<WriteRequest> structured,
        CancellationToken cancellationToken)
    {
        if (!TryGetWritableTag(value.TagName, out var tag, out var failure))
        {
            results[index] = failure;
            return;
        }

        var route = ResolveRoute(tag);
        if (route.MemberAddress is not null)
        {
            structured.Add(new WriteRequest(index, value, route));
            return;
        }

        results[index] = await WriteAddressAsync(route.WriteAddress, value, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Flushes a group of member writes as one root-structure write.</summary>
    /// <param name="group">The writes for one root.</param>
    /// <param name="results">The result buffer.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The write operation.</returns>
    private async Task WriteStructureGroupAsync(
        IGrouping<string, WriteRequest> group,
        TagOperationResult<LogicalTagValue>[] results,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var rootValue = await ReadAddressAsync(group.Key, cancellationToken).ConfigureAwait(false);
        using var table = _createStructureTable(rootValue);
        foreach (var request in group)
        {
            var memberAddress = request.Route.MemberAddress
                ?? throw new InvalidOperationException("A structured write route requires a member address.");
            table[memberAddress] = request.Value.Value;
        }

        var structure = _getStructure(table);
        if (structure is null)
        {
            foreach (var request in group)
            {
                results[request.Index] = TagOperationResult<LogicalTagValue>.Failure(
                    $"TwinCAT structure '{group.Key}' could not be materialized.");
            }

            return;
        }

        var representative = group.First().Value;
        var writeResult = await WriteAddressAsync(
            group.Key,
            representative,
            cancellationToken,
            structure).ConfigureAwait(false);
        foreach (var request in group)
        {
            results[request.Index] = writeResult.Succeeded
                ? TagOperationResult<LogicalTagValue>.Success(request.Value)
                : TagOperationResult<LogicalTagValue>.Failure(writeResult.Error);
        }
    }

    /// <summary>Resolves the native root and optional structure member for a tag.</summary>
    /// <param name="tag">The logical tag.</param>
    /// <returns>The native route.</returns>
    private TagRoute ResolveRoute(LogicalTag tag)
    {
        if (TwinCatLogicalTagHelpers.TryMetadata(tag, RootAddressMetadata, out var metadataRoot))
        {
            _ = TwinCatLogicalTagHelpers.TryMetadata(tag, MemberAddressMetadata, out var metadataMember);
            metadataMember ??= TwinCatLogicalTagHelpers.GetMemberAddress(metadataRoot, tag.Address);
            return new TagRoute(metadataRoot, metadataRoot, metadataMember);
        }

        var root = FindStructureRoot(tag.Address);
        _ = TwinCatLogicalTagHelpers.TryMetadata(tag, WriteAddressMetadata, out var writeAddress);
        return root is null
            ? new TagRoute(tag.Address, writeAddress ?? tag.Address, null)
            : new TagRoute(root, root, TwinCatLogicalTagHelpers.GetMemberAddress(root, tag.Address));
    }

    /// <summary>Finds the longest configured notification root for an address.</summary>
    /// <param name="address">The full ADS address.</param>
    /// <returns>The configured structure root, or null.</returns>
    private string? FindStructureRoot(string address)
    {
        string? root = null;
        foreach (var notification in _client.Settings?.Notifications ?? [])
        {
            var candidate = notification.Variable;
            if (candidate is null ||
                candidate.Trim().Length == 0 ||
                !address.StartsWith($"{candidate}.", StringComparison.OrdinalIgnoreCase) ||
                (root is not null && candidate.Length <= root.Length))
            {
                continue;
            }

            root = candidate;
        }

        return root;
    }

    /// <summary>Tries to resolve a readable tag.</summary>
    /// <param name="name">The logical name.</param>
    /// <param name="tag">The resolved tag.</param>
    /// <param name="failure">The expected failure.</param>
    /// <returns>Whether a readable tag was found.</returns>
    private bool TryGetReadableTag(
        string name,
        [NotNullWhen(true)] out LogicalTag? tag,
        [NotNullWhen(false)] out TagOperationResult<LogicalTagValue>? failure)
    {
        var checkedName = TwinCatLogicalTagHelpers.Required(name, nameof(name));
        if (!Catalog.TryGet(checkedName, out tag) || tag is null)
        {
            failure = TagOperationResult<LogicalTagValue>.Failure(
                $"Logical TwinCAT tag '{checkedName}' is not registered.");
            return false;
        }

        if (!TwinCatLogicalTagHelpers.CanRead(tag.AccessMode))
        {
            failure = TagOperationResult<LogicalTagValue>.Failure(
                $"Logical TwinCAT tag '{checkedName}' is write-only.");
            return false;
        }

        failure = null;
        return true;
    }

    /// <summary>Tries to resolve a writable tag.</summary>
    /// <param name="name">The logical name.</param>
    /// <param name="tag">The resolved tag.</param>
    /// <param name="failure">The expected failure.</param>
    /// <returns>Whether a writable tag was found.</returns>
    private bool TryGetWritableTag(
        string name,
        [NotNullWhen(true)] out LogicalTag? tag,
        [NotNullWhen(false)] out TagOperationResult<LogicalTagValue>? failure)
    {
        var checkedName = TwinCatLogicalTagHelpers.Required(name, nameof(name));
        if (!Catalog.TryGet(checkedName, out tag) || tag is null)
        {
            failure = TagOperationResult<LogicalTagValue>.Failure(
                $"Logical TwinCAT tag '{checkedName}' is not registered.");
            return false;
        }

        if (!TwinCatLogicalTagHelpers.CanWrite(tag.AccessMode))
        {
            failure = TagOperationResult<LogicalTagValue>.Failure($"Logical TwinCAT tag '{checkedName}' is read-only.");
            return false;
        }

        failure = null;
        return true;
    }

    /// <summary>Reads one native address using a correlation identifier.</summary>
    /// <param name="address">The ADS address.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The native value.</returns>
    private async Task<object?> ReadAddressAsync(string address, CancellationToken cancellationToken)
    {
        var operationId = NextOperationId("read");
        var completion = new TaskCompletionSource<object?>(TaskCreationOptions.RunContinuationsAsynchronously);
        using var registration = TwinCatLogicalTagHelpers.RegisterCancellation(
            () => completion.TrySetCanceled(cancellationToken),
            cancellationToken);
        using IDisposable subscription = ObservableBridgeExtensions.SubscribeTo(
            _client.DataReceived,
            value =>
            {
                if (!string.Equals(value.Variable, address, StringComparison.OrdinalIgnoreCase) ||
                    !string.Equals(value.Id, operationId, StringComparison.Ordinal))
                {
                    return;
                }

                _ = completion.TrySetResult(value.Data);
            },
            error => completion.TrySetException(error),
            () => completion.TrySetException(
                new InvalidOperationException(
                    "The TwinCAT data stream completed before the read response arrived.")));
        _client.Read(address, operationId);
        return await completion.Task.ConfigureAwait(false);
    }

    /// <summary>Writes one native address using a correlation identifier.</summary>
    /// <param name="address">The ADS address.</param>
    /// <param name="value">The logical value.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <param name="nativeValue">An optional root-structure payload.</param>
    /// <returns>The correlated result.</returns>
    private async Task<TagOperationResult<LogicalTagValue>> WriteAddressAsync(
        string address,
        LogicalTagValue value,
        CancellationToken cancellationToken,
        object? nativeValue = null)
    {
        var operationId = NextOperationId("write");
        var completion = new TaskCompletionSource<string?>(TaskCreationOptions.RunContinuationsAsynchronously);
        using var registration = TwinCatLogicalTagHelpers.RegisterCancellation(
            () => completion.TrySetCanceled(cancellationToken),
            cancellationToken);
        using IDisposable subscription = ObservableBridgeExtensions.SubscribeTo(
            _client.OnWrite,
            result =>
            {
                if (result is null ||
                    result.Trim().Length == 0 ||
                    (!string.Equals(result, operationId, StringComparison.Ordinal) &&
                     !result.EndsWith($",{operationId}", StringComparison.Ordinal)))
                {
                    return;
                }

                _ = completion.TrySetResult(result);
            },
            error => completion.TrySetException(error),
            () => completion.TrySetException(
                new InvalidOperationException(
                    "The TwinCAT write stream completed before the write response arrived.")));
        var payload = nativeValue ?? value.Value ??
            throw new ArgumentNullException(nameof(value), "TwinCAT write values cannot be null.");
        _client.Write(address, payload, operationId);
        var response = await completion.Task.ConfigureAwait(false);
        return response?.StartsWith("Success", StringComparison.OrdinalIgnoreCase) == true
            ? TagOperationResult<LogicalTagValue>.Success(value)
            : TagOperationResult<LogicalTagValue>.Failure(response ?? "TwinCAT returned an empty write result.");
    }

    /// <summary>Creates the next unique operation identifier.</summary>
    /// <param name="operation">The operation kind.</param>
    /// <returns>The identifier.</returns>
    private string NextOperationId(string operation)
    {
        var identifier = Interlocked.Increment(ref _operationId)
            .ToString(System.Globalization.CultureInfo.InvariantCulture);
        return $"TwinCatLogicalTagClient:{operation}:{identifier}";
    }

    /// <summary>Projects a native root payload into a logical value.</summary>
    /// <param name="tag">The logical tag.</param>
    /// <param name="route">The native route.</param>
    /// <param name="data">The native payload.</param>
    /// <returns>The logical operation result.</returns>
    private TagOperationResult<LogicalTagValue> CreateReadResult(LogicalTag tag, TagRoute route, object? data)
    {
        if (route.MemberAddress is not null)
        {
            using var table = _createStructureTable(data);
            data = table[route.MemberAddress];
        }

        return TagOperationResult<LogicalTagValue>.Success(
            new LogicalTagValue(tag.Name, data, _timeProvider.GetUtcNow(), "Good"));
    }

    /// <summary>Publishes a matching native event as a logical value.</summary>
    /// <param name="observer">The destination observer.</param>
    /// <param name="tag">The logical tag.</param>
    /// <param name="route">The native route.</param>
    /// <param name="data">The native event.</param>
    private void PublishObservedValue(
        IObserver<LogicalTagValue> observer,
        LogicalTag tag,
        TagRoute route,
        (string Variable, object? Data, string? Id) data)
    {
        if (!string.Equals(data.Variable, route.RootAddress, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        try
        {
            var value = CreateReadResult(tag, route, data.Data);
            var logicalValue = value.Value
                ?? throw new InvalidOperationException("The logical tag value was not created.");
            observer.OnNext(logicalValue);
        }
        catch (Exception error)
        {
            observer.OnError(error);
        }
    }

    /// <summary>Gets the configured SQLite store.</summary>
    /// <returns>The configured store.</returns>
    private LogicalTagSqliteStore RequireStore()
    {
        ThrowIfDisposed();
        return _store ??
            throw new InvalidOperationException(
                "No LogicalTagSqliteStore was supplied to this TwinCAT logical tag client.");
    }

    /// <summary>Throws when this instance has been disposed.</summary>
    private void ThrowIfDisposed()
    {
        if (Volatile.Read(ref _disposed) == 0)
        {
            return;
        }

        throw new ObjectDisposedException(nameof(TwinCatLogicalTagClient));
    }

    /// <summary>Describes one native TwinCAT tag route.</summary>
    /// <param name="rootAddress">The read/root address.</param>
    /// <param name="writeAddress">The write address.</param>
    /// <param name="memberAddress">The optional structure member.</param>
    private sealed class TagRoute(string rootAddress, string writeAddress, string? memberAddress)
    {
        /// <summary>Gets the read/root address.</summary>
        public string RootAddress { get; } = rootAddress;

        /// <summary>Gets the write address.</summary>
        public string WriteAddress { get; } = writeAddress;

        /// <summary>Gets the optional structure member.</summary>
        public string? MemberAddress { get; } = memberAddress;
    }

    /// <summary>Stores one indexed bulk read.</summary>
    /// <param name="index">The result index.</param>
    /// <param name="tag">The logical tag.</param>
    /// <param name="route">The native route.</param>
    private sealed class ReadRequest(int index, LogicalTag tag, TagRoute route)
    {
        /// <summary>Gets the result index.</summary>
        public int Index { get; } = index;

        /// <summary>Gets the logical tag.</summary>
        public LogicalTag Tag { get; } = tag;

        /// <summary>Gets the native route.</summary>
        public TagRoute Route { get; } = route;
    }

    /// <summary>Stores one indexed bulk write.</summary>
    /// <param name="index">The result index.</param>
    /// <param name="value">The logical value.</param>
    /// <param name="route">The native route.</param>
    private sealed class WriteRequest(int index, LogicalTagValue value, TagRoute route)
    {
        /// <summary>Gets the result index.</summary>
        public int Index { get; } = index;

        /// <summary>Gets the logical value.</summary>
        public LogicalTagValue Value { get; } = value;

        /// <summary>Gets the native route.</summary>
        public TagRoute Route { get; } = route;
    }
}
