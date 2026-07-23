// Copyright (c) 2019-2026 Chris Pulman and contributors. All rights reserved.
// Chris Pulman and contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using IoT.DriverCore.Core;
#if REACTIVE_SHIM
using IoT.DriverCore.S7PlcRx.Reactive.Binding;

namespace IoT.DriverCore.S7PlcRx.Reactive.LogicalTags;

#else
using IoT.DriverCore.S7PlcRx.Binding;

namespace IoT.DriverCore.S7PlcRx.LogicalTags;

#endif

/// <summary>Composes an S7 connection with the common logical-tag catalog, persistence, and client contracts.</summary>
public sealed partial class S7LogicalTagClient : IManagedLogicalTagClient, IDisposable
{
    /// <summary>Defines the number of characters in an array type suffix.</summary>
    private const int ArrayTypeSuffixLength = 2;

    /// <summary>Maps supported S7 data-type names to their runtime types.</summary>
    private static readonly Dictionary<string, Type> TypeMap = new(StringComparer.Ordinal)
    {
        ["BOOL"] = typeof(bool),
        ["BOOLEAN"] = typeof(bool),
        ["BYTE"] = typeof(byte),
        ["DINT"] = typeof(int),
        ["DOUBLE"] = typeof(double),
        ["DWORD"] = typeof(uint),
        ["INT"] = typeof(short),
        ["INT16"] = typeof(short),
        ["INT32"] = typeof(int),
        ["LREAL"] = typeof(double),
        ["REAL"] = typeof(float),
        ["SINGLE"] = typeof(float),
        ["STRING"] = typeof(string),
        ["UDINT"] = typeof(uint),
        ["UINT"] = typeof(ushort),
        ["UINT16"] = typeof(ushort),
        ["UINT32"] = typeof(uint),
        ["UINT8"] = typeof(byte),
        ["WORD"] = typeof(ushort),
    };

    /// <summary>Provides the S7 connection used for tag operations.</summary>
    private readonly IRxS7 _plc;

    /// <summary>Stores the time provider used by this client.</summary>
    private readonly TimeProvider _timeProvider;

    /// <summary>Provides concrete S7 multi-variable operations when available.</summary>
    private readonly IS7LogicalBatchOperations? _batchOperations;

    /// <summary>Tracks the logical tags registered with the S7 connection.</summary>
    private readonly HashSet<string> _registeredTags = new(StringComparer.Ordinal);

    /// <summary>Stores persisted logical tags when persistence is configured.</summary>
    private LogicalTagSqliteStore? _store;

    /// <summary>Indicates whether this client has been disposed.</summary>
    private bool _disposed;

    /// <summary>Initializes a new instance of the <see cref="S7LogicalTagClient"/> class.</summary>
    /// <param name="plc">The S7 connection.</param>
    /// <param name="catalog">The common logical-tag catalog.</param>
    public S7LogicalTagClient(
        IRxS7 plc,
        ILogicalTagCatalog catalog)
        : this(plc, catalog, store: null, TimeProvider.System)
    {
    }

    /// <summary>Initializes a new instance of the <see cref="S7LogicalTagClient"/> class.</summary>
    /// <param name="plc">The S7 connection.</param>
    /// <param name="catalog">The common logical-tag catalog.</param>
    /// <param name="timeProvider">The time provider.</param>
    public S7LogicalTagClient(
        IRxS7 plc,
        ILogicalTagCatalog catalog,
        TimeProvider timeProvider)
        : this(plc, catalog, store: null, timeProvider)
    {
    }

    /// <summary>Initializes a new instance of the <see cref="S7LogicalTagClient"/> class.</summary>
    /// <param name="plc">The S7 connection.</param>
    /// <param name="catalog">The common logical-tag catalog.</param>
    /// <param name="store">The SQLite store used by persistence forwarding methods, or <see langword="null"/>.</param>
    public S7LogicalTagClient(
        IRxS7 plc,
        ILogicalTagCatalog catalog,
        LogicalTagSqliteStore? store)
        : this(plc, catalog, store, TimeProvider.System)
    {
    }

    /// <summary>Initializes a new instance of the <see cref="S7LogicalTagClient"/> class.</summary>
    /// <param name="plc">The S7 connection.</param>
    /// <param name="catalog">The common logical-tag catalog.</param>
    /// <param name="store">The SQLite store used by persistence forwarding methods, or <see langword="null"/>.</param>
    /// <param name="timeProvider">The time provider.</param>
    public S7LogicalTagClient(
        IRxS7 plc,
        ILogicalTagCatalog catalog,
        LogicalTagSqliteStore? store,
        TimeProvider timeProvider)
        : this(plc, catalog, store, timeProvider, batchOperations: null)
    {
    }

    /// <summary>Initializes a new instance of the <see cref="S7LogicalTagClient"/> class.</summary>
    /// <param name="plc">The S7 connection.</param>
    /// <param name="catalog">The common logical-tag catalog.</param>
    /// <param name="store">The SQLite store used by persistence forwarding methods, or <see langword="null"/>.</param>
    /// <param name="timeProvider">The time provider.</param>
    /// <param name="batchOperations">The optional multi-variable operation adapter.</param>
    internal S7LogicalTagClient(
        IRxS7 plc,
        ILogicalTagCatalog catalog,
        LogicalTagSqliteStore? store,
        TimeProvider timeProvider,
        IS7LogicalBatchOperations? batchOperations)
    {
        _plc = plc ?? throw new ArgumentNullException(nameof(plc));
        _timeProvider = timeProvider;
        _batchOperations = batchOperations
            ?? (plc is RxS7 rx ? new RxS7LogicalBatchOperations(rx) : null);
        Catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
        _store = store;
        Catalog.Changed += OnCatalogChanged;
        foreach (var tag in Catalog.List())
        {
            RegisterWithPlc(tag);
        }
    }

    /// <summary>Gets the mutable logical-tag catalog observed by this client.</summary>
    public ILogicalTagCatalog Catalog { get; }

    /// <summary>Adds or replaces a logical definition and registers it with the S7 connection.</summary>
    /// <param name="tag">The logical tag.</param>
    public void RegisterTag(LogicalTag tag)
    {
        ThrowIfDisposed();
        Catalog.Upsert(tag ?? throw new ArgumentNullException(nameof(tag)));
    }

    /// <summary>Registers and returns an S7 logical tag.</summary>
    /// <param name="tag">The logical tag to register.</param>
    /// <returns>The registered logical tag.</returns>
    public LogicalTag CreateTag(LogicalTag tag)
    {
        Guard.NotNull(tag, nameof(tag));
        RegisterTag(tag);
        return tag;
    }

    /// <summary>Removes a logical definition and its S7 registration.</summary>
    /// <param name="name">The logical tag name.</param>
    /// <returns>True when the tag existed.</returns>
    public bool RemoveTag(string name)
    {
        ThrowIfDisposed();
        return Catalog.TryRemove(Required(name, nameof(name)), out _);
    }

    /// <summary>Imports RFC 4180 common-tag CSV and dynamically upserts every definition.</summary>
    /// <param name="reader">The CSV reader.</param>
    /// <returns>The imported logical tags.</returns>
    public Task<IReadOnlyList<LogicalTag>> ImportCsvAsync(TextReader reader) =>
        ImportCsvAsync(reader, ',', CancellationToken.None);

    /// <summary>Imports common-tag CSV and dynamically upserts every definition.</summary>
    /// <param name="reader">The CSV reader.</param>
    /// <param name="delimiter">The CSV delimiter.</param>
    /// <returns>The imported logical tags.</returns>
    public Task<IReadOnlyList<LogicalTag>> ImportCsvAsync(TextReader reader, char delimiter) =>
        ImportCsvAsync(reader, delimiter, CancellationToken.None);

    /// <summary>Imports common-tag CSV and dynamically upserts every definition.</summary>
    /// <param name="reader">The CSV reader.</param>
    /// <param name="delimiter">The CSV delimiter.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The imported logical tags.</returns>
    public async Task<IReadOnlyList<LogicalTag>> ImportCsvAsync(
        TextReader reader,
        char delimiter,
        CancellationToken cancellationToken)
    {
        ThrowIfDisposed();
        var tags = await LogicalTagCsv
            .ImportAsync(reader, delimiter, cancellationToken)
            .ConfigureAwait(false);
        foreach (var tag in tags)
        {
            Catalog.Upsert(tag);
        }

        return tags;
    }

    /// <summary>Exports the catalog using the common RFC 4180 CSV representation.</summary>
    /// <param name="writer">The CSV writer.</param>
    /// <returns>A task that represents the export operation.</returns>
    public Task ExportCsvAsync(TextWriter writer) =>
        ExportCsvAsync(writer, ',', CancellationToken.None);

    /// <summary>Exports the catalog using the common CSV representation.</summary>
    /// <param name="writer">The CSV writer.</param>
    /// <param name="delimiter">The CSV delimiter.</param>
    /// <returns>A task that represents the export operation.</returns>
    public Task ExportCsvAsync(TextWriter writer, char delimiter) =>
        ExportCsvAsync(writer, delimiter, CancellationToken.None);

    /// <summary>Exports the catalog using the common CSV representation.</summary>
    /// <param name="writer">The CSV writer.</param>
    /// <param name="delimiter">The CSV delimiter.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A task that represents the export operation.</returns>
    public Task ExportCsvAsync(
        TextWriter writer,
        char delimiter,
        CancellationToken cancellationToken)
    {
        ThrowIfDisposed();
        return LogicalTagCsv.ExportAsync(Catalog.List(), writer, delimiter, cancellationToken);
    }

    /// <summary>Assigns and initializes the SQLite store used by this client.</summary>
    /// <param name="store">The SQLite store.</param>
    /// <returns>A task that represents the initialization operation.</returns>
    public Task InitializeStoreAsync(LogicalTagSqliteStore store) =>
        InitializeStoreAsync(store, CancellationToken.None);

    /// <summary>Assigns and initializes the SQLite store used by this client.</summary>
    /// <param name="store">The SQLite store.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A task that represents the initialization operation.</returns>
    public async Task InitializeStoreAsync(
        LogicalTagSqliteStore store,
        CancellationToken cancellationToken)
    {
        ThrowIfDisposed();
        _store = store ?? throw new ArgumentNullException(nameof(store));
        await store.InitializeAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public Task InitializeStoreAsync(CancellationToken cancellationToken) =>
        GetStore().InitializeAsync(cancellationToken);

    /// <summary>Loads every SQLite tag into the live catalog.</summary>
    /// <returns>The loaded logical tags.</returns>
    public Task<IReadOnlyList<LogicalTag>> LoadTagsAsync() =>
        LoadTagsAsync(CancellationToken.None);

    /// <summary>Loads every SQLite tag into the live catalog.</summary>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The loaded logical tags.</returns>
    public async Task<IReadOnlyList<LogicalTag>> LoadTagsAsync(
        CancellationToken cancellationToken)
    {
        ThrowIfDisposed();
        var tags = await GetStore().ListTagsAsync(cancellationToken).ConfigureAwait(false);
        foreach (var tag in tags)
        {
            Catalog.Upsert(tag);
        }

        return tags;
    }

    /// <summary>Gets a persisted logical tag.</summary>
    /// <param name="name">The logical tag name.</param>
    /// <returns>The persisted logical tag, if found.</returns>
    public Task<LogicalTag?> GetTagAsync(string name) =>
        GetTagAsync(name, CancellationToken.None);

    /// <summary>Gets a persisted logical tag.</summary>
    /// <param name="name">The logical tag name.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The persisted logical tag, if found.</returns>
    public Task<LogicalTag?> GetTagAsync(
        string name,
        CancellationToken cancellationToken) => GetStore().GetTagAsync(name, cancellationToken);

    /// <summary>Lists persisted logical tags.</summary>
    /// <returns>The persisted logical tags.</returns>
    public Task<IReadOnlyList<LogicalTag>> ListTagsAsync() =>
        ListTagsAsync(CancellationToken.None);

    /// <summary>Lists persisted logical tags.</summary>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The persisted logical tags.</returns>
    public Task<IReadOnlyList<LogicalTag>> ListTagsAsync(
        CancellationToken cancellationToken) => GetStore().ListTagsAsync(cancellationToken);

    /// <summary>Persists and dynamically registers a logical tag.</summary>
    /// <param name="tag">The logical tag.</param>
    /// <returns>A task that represents the persistence operation.</returns>
    public Task UpsertTagAsync(LogicalTag tag) =>
        UpsertTagAsync(tag, CancellationToken.None);

    /// <summary>Persists and dynamically registers a logical tag.</summary>
    /// <param name="tag">The logical tag.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A task that represents the persistence operation.</returns>
    public async Task UpsertTagAsync(LogicalTag tag, CancellationToken cancellationToken)
    {
        await GetStore().UpsertTagAsync(tag, cancellationToken).ConfigureAwait(false);
        Catalog.Upsert(tag);
    }

    /// <summary>Edits an existing persisted logical tag and updates the live catalog when successful.</summary>
    /// <param name="tag">The logical tag.</param>
    /// <returns>True when the tag was updated.</returns>
    public Task<bool> EditTagAsync(LogicalTag tag) =>
        EditTagAsync(tag, CancellationToken.None);

    /// <summary>Edits an existing persisted logical tag and updates the live catalog when successful.</summary>
    /// <param name="tag">The logical tag.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>True when the tag was updated.</returns>
    public async Task<bool> EditTagAsync(
        LogicalTag tag,
        CancellationToken cancellationToken)
    {
        var edited = await GetStore().EditTagAsync(tag, cancellationToken).ConfigureAwait(false);
        if (edited)
        {
            Catalog.Upsert(tag);
        }

        return edited;
    }

    /// <summary>Alias for <see cref="EditTagAsync(LogicalTag)"/>.</summary>
    /// <param name="tag">The logical tag.</param>
    /// <returns>True when the tag was updated.</returns>
    public Task<bool> UpdateTagAsync(LogicalTag tag) =>
        UpdateTagAsync(tag, CancellationToken.None);

    /// <summary>Alias for <see cref="EditTagAsync(LogicalTag, CancellationToken)"/>.</summary>
    /// <param name="tag">The logical tag.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>True when the tag was updated.</returns>
    public Task<bool> UpdateTagAsync(
        LogicalTag tag,
        CancellationToken cancellationToken) => EditTagAsync(tag, cancellationToken);

    /// <summary>Deletes a persisted tag and removes it from the live catalog when successful.</summary>
    /// <param name="name">The logical tag name.</param>
    /// <returns>True when the tag was deleted.</returns>
    public Task<bool> DeleteTagAsync(string name) =>
        DeleteTagAsync(name, CancellationToken.None);

    /// <summary>Deletes a persisted tag and removes it from the live catalog when successful.</summary>
    /// <param name="name">The logical tag name.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>True when the tag was deleted.</returns>
    public async Task<bool> DeleteTagAsync(
        string name,
        CancellationToken cancellationToken)
    {
        var deleted = await GetStore()
            .DeleteTagAsync(name, cancellationToken)
            .ConfigureAwait(false);
        if (deleted)
        {
            _ = Catalog.TryRemove(name, out _);
        }

        return deleted;
    }

    /// <summary>Gets a persisted logical group.</summary>
    /// <param name="name">The logical group name.</param>
    /// <returns>The persisted logical group, if found.</returns>
    public Task<LogicalTagGroup?> GetGroupAsync(string name) =>
        GetGroupAsync(name, CancellationToken.None);

    /// <summary>Gets a persisted logical group.</summary>
    /// <param name="name">The logical group name.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The persisted logical group, if found.</returns>
    public Task<LogicalTagGroup?> GetGroupAsync(
        string name,
        CancellationToken cancellationToken) => GetStore().GetGroupAsync(name, cancellationToken);

    /// <summary>Lists persisted logical groups.</summary>
    /// <returns>The persisted logical groups.</returns>
    public Task<IReadOnlyList<LogicalTagGroup>> ListGroupsAsync() =>
        ListGroupsAsync(CancellationToken.None);

    /// <summary>Lists persisted logical groups.</summary>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The persisted logical groups.</returns>
    public Task<IReadOnlyList<LogicalTagGroup>> ListGroupsAsync(
        CancellationToken cancellationToken) => GetStore().ListGroupsAsync(cancellationToken);

    /// <summary>Persists a logical group.</summary>
    /// <param name="group">The logical group.</param>
    /// <returns>A task that represents the persistence operation.</returns>
    public Task UpsertGroupAsync(LogicalTagGroup group) =>
        UpsertGroupAsync(group, CancellationToken.None);

    /// <summary>Persists a logical group.</summary>
    /// <param name="group">The logical group.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A task that represents the persistence operation.</returns>
    public Task UpsertGroupAsync(
        LogicalTagGroup group,
        CancellationToken cancellationToken) => GetStore().UpsertGroupAsync(group, cancellationToken);

    /// <summary>Deletes a persisted logical group.</summary>
    /// <param name="name">The logical group name.</param>
    /// <returns>True when the group was deleted.</returns>
    public Task<bool> DeleteGroupAsync(string name) =>
        DeleteGroupAsync(name, CancellationToken.None);

    /// <summary>Deletes a persisted logical group.</summary>
    /// <param name="name">The logical group name.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>True when the group was deleted.</returns>
    public Task<bool> DeleteGroupAsync(
        string name,
        CancellationToken cancellationToken) => GetStore().DeleteGroupAsync(name, cancellationToken);

    /// <inheritdoc />
    public async Task<TagOperationResult<LogicalTagValue>> ReadAsync(
        string tagName,
        CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        if (!TryGetTag(tagName, LogicalTagAccessMode.Write, out var tag, out var failure))
        {
            return failure!;
        }

        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            var value = await ReadValueAsync(tag!, cancellationToken).ConfigureAwait(false);
            return TagOperationResult<LogicalTagValue>.Success(CreateValue(tagName, value, _timeProvider));
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            return TagOperationResult<LogicalTagValue>.Failure(ex.GetBaseException().Message);
        }
    }

    /// <inheritdoc />
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
        var pending = CreatePendingReads(names, results);

        cancellationToken.ThrowIfCancellationRequested();
        return _batchOperations is not null && pending.Count > 0
            ? ReadMultiple(_batchOperations, pending, results)
            : await ReadIndividuallyAsync(pending, results, cancellationToken)
                .ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<TagOperationResult<LogicalTagValue>> WriteAsync(
        LogicalTagValue value,
        CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        if (value is null)
        {
            throw new ArgumentNullException(nameof(value));
        }

        if (
            !TryGetTag(
                value.TagName,
                LogicalTagAccessMode.Read,
                out var definition,
                out var failure))
        {
            return failure!;
        }

        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            var converted = ConvertValue(value.Value, ResolveType(definition!));
            if (_plc is RxS7 rx)
            {
                var runtimeTag =
                    _plc.TagList[definition!.Name]
                    ?? throw new InvalidOperationException(
                        $"S7 tag '{definition.Name}' is not registered.");
                runtimeTag.NewValue = converted;
                if (!rx.WriteMultiVar([runtimeTag]))
                {
                    return TagOperationResult<LogicalTagValue>.Failure(
                        $"S7 write failed for '{definition.Name}'.");
                }
            }
            else
            {
                InvokeWrite(definition!, converted);
            }

            return TagOperationResult<LogicalTagValue>.Success(
                CreateValue(definition!.Name, converted, _timeProvider));
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            return TagOperationResult<LogicalTagValue>.Failure(ex.GetBaseException().Message);
        }
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<TagOperationResult<LogicalTagValue>>> WriteManyAsync(
        IReadOnlyCollection<LogicalTagValue> values,
        CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        if (values is null)
        {
            throw new ArgumentNullException(nameof(values));
        }

        var materialized = values.ToArray();
        var results = new TagOperationResult<LogicalTagValue>[materialized.Length];
        var pending = CreatePendingWrites(materialized, results, nameof(values));

        cancellationToken.ThrowIfCancellationRequested();
        return _batchOperations is not null && pending.Count > 0
            ? WriteMultiple(_batchOperations, pending, results)
            : await WriteIndividuallyAsync(pending, results, cancellationToken)
                .ConfigureAwait(false);
    }

    /// <inheritdoc />
    public IObservable<LogicalTagValue> Observe(string tagName) => ObserveMany([tagName]);

    /// <inheritdoc />
    public IObservable<LogicalTagValue> ObserveMany(IReadOnlyCollection<string> tagNames)
    {
        ThrowIfDisposed();
        if (tagNames is null)
        {
            throw new ArgumentNullException(nameof(tagNames));
        }

        var names = new HashSet<string>(
            tagNames.Select(name => Required(name, nameof(tagNames))),
            StringComparer.Ordinal);
        foreach (var name in names)
        {
            if (!Catalog.TryGet(name, out var tag) || tag!.AccessMode == LogicalTagAccessMode.Write)
            {
                throw new KeyNotFoundException($"Readable logical tag '{name}' was not found.");
            }
        }

        return _plc
            .ObserveAll.Where(tag => tag?.Name is not null && names.Contains(tag.Name))
            .Select(tag => CreateValue(tag!.Name!, tag.Value, _timeProvider));
    }

    /// <inheritdoc />
    public IAsyncEnumerable<LogicalTagValue> ObserveAsync(
        string tagName,
        CancellationToken cancellationToken = default) =>
        WithCancellationToken(
            S7TagObservableAdapter.ToAsyncEnumerable(Observe(tagName)),
            cancellationToken);

    /// <inheritdoc />
    public IAsyncEnumerable<LogicalTagValue> ObserveManyAsync(
        IReadOnlyCollection<string> tagNames,
        CancellationToken cancellationToken = default) =>
        WithCancellationToken(
            S7TagObservableAdapter.ToAsyncEnumerable(ObserveMany(tagNames)),
            cancellationToken);

    /// <inheritdoc />
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        Catalog.Changed -= OnCatalogChanged;
        _disposed = true;
    }
}
