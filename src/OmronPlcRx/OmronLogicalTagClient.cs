// Copyright (c) 2019-2026 Chris Pulman and contributors. All rights reserved.
// Chris Pulman and contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using IoT.DriverCore.Core;
#if REACTIVE_SHIM
using IoT.DriverCore.OmronPlcRx.Reactive.Tags;
#else
using IoT.DriverCore.OmronPlcRx.Tags;
#endif

#if REACTIVE_SHIM
namespace IoT.DriverCore.OmronPlcRx.Reactive;

#else
namespace IoT.DriverCore.OmronPlcRx;

#endif

/// <summary>Composes an Omron PLC facade with the shared logical-tag catalog and persistence contracts.</summary>
public sealed partial class OmronLogicalTagClient : IManagedLogicalTagClient, IDisposable
{
    /// <summary>Omron PLC facade used for protocol operations.</summary>
    private readonly IOmronPlcRx _plc;

    /// <summary>Optional native grouped FINS operation provider.</summary>
    private readonly IOmronLogicalBatchOperations? _batchOperations;

    /// <summary>Indicates whether this instance owns the catalog lifetime.</summary>
    private readonly bool _ownsCatalog;

    /// <summary>Optional SQLite persistence store.</summary>
    private readonly LogicalTagSqliteStore? _store;

    /// <summary>Stores the time provider used for clock operations.</summary>
    private readonly TimeProvider _timeProvider;

    /// <summary>Indicates whether this instance has been disposed.</summary>
    private bool _disposed;

    /// <summary>Initializes a new instance of the <see cref="OmronLogicalTagClient"/> class.</summary>
    /// <param name="plc">Omron PLC facade used for protocol operations.</param>
    public OmronLogicalTagClient(IOmronPlcRx plc)
        : this(plc, new LogicalTagCatalog(), null, true) { }

    /// <summary>Initializes a new instance of the <see cref="OmronLogicalTagClient"/> class.</summary>
    /// <param name="plc">Omron PLC facade used for protocol operations.</param>
    /// <param name="catalog">Logical-tag catalog.</param>
    public OmronLogicalTagClient(IOmronPlcRx plc, ILogicalTagCatalog catalog)
        : this(plc, catalog, null, false) { }

    /// <summary>Initializes a new instance of the <see cref="OmronLogicalTagClient"/> class.</summary>
    /// <param name="plc">Omron PLC facade used for protocol operations.</param>
    /// <param name="sqliteConnectionString">SQLite connection string.</param>
    public OmronLogicalTagClient(IOmronPlcRx plc, string sqliteConnectionString)
        : this(
            plc,
            new LogicalTagCatalog(),
            new LogicalTagSqliteStore(sqliteConnectionString),
            true) { }

    /// <summary>Initializes a new instance of the <see cref="OmronLogicalTagClient"/> class.</summary>
    /// <param name="plc">Omron PLC facade used for protocol operations.</param>
    /// <param name="catalog">Logical-tag catalog.</param>
    /// <param name="store">Optional SQLite store.</param>
    public OmronLogicalTagClient(
        IOmronPlcRx plc,
        ILogicalTagCatalog catalog,
        LogicalTagSqliteStore? store)
        : this(plc, catalog, store, false) { }

    /// <summary>Initializes a new instance of the <see cref="OmronLogicalTagClient"/> class.</summary>
    /// <param name="plc">Omron PLC facade used for protocol operations.</param>
    /// <param name="catalog">Logical-tag catalog.</param>
    /// <param name="store">Optional SQLite store.</param>
    /// <param name="ownsCatalog">Whether this instance owns the catalog lifetime.</param>
    private OmronLogicalTagClient(
        IOmronPlcRx plc,
        ILogicalTagCatalog catalog,
        LogicalTagSqliteStore? store,
        bool ownsCatalog)
    {
        _plc = plc ?? throw new ArgumentNullException(nameof(plc));
        _batchOperations = plc as IOmronLogicalBatchOperations;
        Catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
        _store = store;
        _ownsCatalog = ownsCatalog;
        _timeProvider = TimeProvider.System;
    }

    /// <summary>Gets the logical-tag catalog composed by this client.</summary>
    public ILogicalTagCatalog Catalog { get; }

    /// <summary>Creates and registers a typed logical tag.</summary>
    /// <typeparam name="T">Logical tag payload type.</typeparam>
    /// <param name="tag">Typed PLC tag.</param>
    /// <returns>The registered logical tag.</returns>
    public LogicalTag CreateTag<T>(PlcTag<T> tag) =>
        CreateTag(
            tag,
            null,
            null,
            null,
            LogicalTagAccessMode.ReadWrite,
            null);

    /// <summary>Creates and registers a configured typed logical tag.</summary>
    /// <typeparam name="T">Logical tag payload type.</typeparam>
    /// <param name="tag">Typed PLC tag.</param>
    /// <param name="groupName">Optional group name.</param>
    /// <param name="description">Optional description.</param>
    /// <param name="metadata">Optional metadata.</param>
    /// <param name="accessMode">Allowed access mode.</param>
    /// <param name="scanInterval">Optional scan interval.</param>
    /// <returns>The registered logical tag.</returns>
    public LogicalTag CreateTag<T>(
        PlcTag<T> tag,
        string? groupName,
        string? description,
        IReadOnlyDictionary<string, string>? metadata,
        LogicalTagAccessMode accessMode,
        TimeSpan? scanInterval)
    {
        if (tag is null)
        {
            throw new ArgumentNullException(nameof(tag));
        }

        var dataType = typeof(T).FullName ?? nameof(T);
        var logicalTag = new LogicalTag(
            tag.TagName,
            tag.Address,
            dataType,
            new LogicalTagOptions
            {
                GroupName = groupName,
                Description = description,
                Metadata = metadata,
                AccessMode = accessMode,
                ScanInterval = scanInterval,
            });
        RegisterTag(logicalTag);
        return logicalTag;
    }

    /// <summary>Registers or replaces a logical tag in both the Omron facade and catalog.</summary>
    /// <param name="tag">Logical tag to register.</param>
    public void RegisterTag(LogicalTag tag)
    {
        if (tag is null)
        {
            throw new ArgumentNullException(nameof(tag));
        }

        ThrowIfDisposed();
        RegisterWithPlc(tag);
        Catalog.Upsert(tag);
    }

    /// <summary>Removes a logical tag from the Omron facade and catalog.</summary>
    /// <param name="name">Logical tag name.</param>
    /// <returns>True when either registration was removed; otherwise false.</returns>
    public bool RemoveTag(string name)
    {
        ThrowIfDisposed();
        var removedFromPlc = _plc.RemoveTagItem(name);
        var removedFromCatalog = Catalog.TryRemove(name, out _);
        return removedFromPlc || removedFromCatalog;
    }

    /// <summary>Imports RFC 4180 CSV definitions and registers them dynamically.</summary>
    /// <param name="reader">CSV source reader.</param>
    /// <param name="delimiter">CSV delimiter.</param>
    /// <param name="cancellationToken">Token used to cancel the import.</param>
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
            RegisterTag(tag);
        }

        return tags;
    }

    /// <summary>Exports the current catalog as RFC 4180 CSV.</summary>
    /// <param name="writer">CSV destination writer.</param>
    /// <param name="delimiter">CSV delimiter.</param>
    /// <param name="cancellationToken">Token used to cancel the export.</param>
    /// <returns>A task representing the export.</returns>
    public Task ExportCsvAsync(
        TextWriter writer,
        char delimiter,
        CancellationToken cancellationToken)
    {
        ThrowIfDisposed();
        return LogicalTagCsv.ExportAsync(Catalog.List(), writer, delimiter, cancellationToken);
    }

    /// <summary>Initializes the configured SQLite store.</summary>
    /// <param name="cancellationToken">Token used to cancel initialization.</param>
    /// <returns>A task representing initialization.</returns>
    public Task InitializeStoreAsync(CancellationToken cancellationToken) =>
        GetStore().InitializeAsync(cancellationToken);

    /// <summary>Loads and dynamically registers all tags from the configured SQLite store.</summary>
    /// <param name="cancellationToken">Token used to cancel the load.</param>
    /// <returns>The loaded logical tags.</returns>
    public async Task<IReadOnlyList<LogicalTag>> LoadTagsAsync(
        CancellationToken cancellationToken)
    {
        var tags = await GetStore().ListTagsAsync(cancellationToken).ConfigureAwait(false);
        foreach (var tag in tags)
        {
            RegisterTag(tag);
        }

        return tags;
    }

    /// <summary>Gets a persisted tag by name.</summary>
    /// <param name="name">Logical tag name.</param>
    /// <param name="cancellationToken">Token used to cancel the query.</param>
    /// <returns>The matching tag when present; otherwise null.</returns>
    public Task<LogicalTag?> GetTagAsync(
        string name,
        CancellationToken cancellationToken) => GetStore().GetTagAsync(name, cancellationToken);

    /// <summary>Lists persisted tags.</summary>
    /// <param name="cancellationToken">Token used to cancel the query.</param>
    /// <returns>The persisted logical tags.</returns>
    public Task<IReadOnlyList<LogicalTag>> ListTagsAsync(
        CancellationToken cancellationToken) => GetStore().ListTagsAsync(cancellationToken);

    /// <summary>Upserts a persisted tag and registers the resulting definition.</summary>
    /// <param name="tag">Logical tag to upsert.</param>
    /// <param name="cancellationToken">Token used to cancel the operation.</param>
    /// <returns>A task representing the operation.</returns>
    public async Task UpsertTagAsync(LogicalTag tag, CancellationToken cancellationToken)
    {
        await GetStore().UpsertTagAsync(tag, cancellationToken).ConfigureAwait(false);
        RegisterTag(tag);
    }

    /// <summary>Edits an existing persisted tag and refreshes its registration.</summary>
    /// <param name="tag">Logical tag definition.</param>
    /// <param name="cancellationToken">Token used to cancel the operation.</param>
    /// <returns>True when the persisted tag existed; otherwise false.</returns>
    public async Task<bool> EditTagAsync(
        LogicalTag tag,
        CancellationToken cancellationToken)
    {
        var edited = await GetStore().EditTagAsync(tag, cancellationToken).ConfigureAwait(false);
        if (edited)
        {
            RegisterTag(tag);
        }

        return edited;
    }

    /// <summary>Deletes a persisted and registered tag.</summary>
    /// <param name="name">Logical tag name.</param>
    /// <param name="cancellationToken">Token used to cancel the operation.</param>
    /// <returns>True when the persisted tag existed; otherwise false.</returns>
    public async Task<bool> DeleteTagAsync(
        string name,
        CancellationToken cancellationToken)
    {
        var deleted = await GetStore()
            .DeleteTagAsync(name, cancellationToken)
            .ConfigureAwait(false);
        if (deleted)
        {
            _ = RemoveTag(name);
        }

        return deleted;
    }

    /// <summary>Gets a persisted tag group.</summary>
    /// <param name="name">Logical tag group name.</param>
    /// <param name="cancellationToken">Token used to cancel the query.</param>
    /// <returns>The matching group when present; otherwise null.</returns>
    public Task<LogicalTagGroup?> GetGroupAsync(
        string name,
        CancellationToken cancellationToken) => GetStore().GetGroupAsync(name, cancellationToken);

    /// <summary>Lists persisted tag groups.</summary>
    /// <param name="cancellationToken">Token used to cancel the query.</param>
    /// <returns>The persisted groups.</returns>
    public Task<IReadOnlyList<LogicalTagGroup>> ListGroupsAsync(
        CancellationToken cancellationToken) => GetStore().ListGroupsAsync(cancellationToken);

    /// <summary>Upserts a persisted tag group.</summary>
    /// <param name="group">Logical tag group.</param>
    /// <param name="cancellationToken">Token used to cancel the operation.</param>
    /// <returns>A task representing the operation.</returns>
    public Task UpsertGroupAsync(
        LogicalTagGroup group,
        CancellationToken cancellationToken) => GetStore().UpsertGroupAsync(group, cancellationToken);

    /// <summary>Deletes a persisted tag group.</summary>
    /// <param name="name">Logical tag group name.</param>
    /// <param name="cancellationToken">Token used to cancel the operation.</param>
    /// <returns>True when the persisted group existed; otherwise false.</returns>
    public Task<bool> DeleteGroupAsync(
        string name,
        CancellationToken cancellationToken) => GetStore().DeleteGroupAsync(name, cancellationToken);

    /// <inheritdoc />
    public async Task<TagOperationResult<LogicalTagValue>> ReadAsync(
        string tagName,
        CancellationToken cancellationToken)
    {
        if (!TryGetTag(tagName, LogicalTagAccessMode.Read, out var tag, out var failure))
        {
            return failure;
        }

        try
        {
            var value = await ReadFromPlcAsync(tag!, cancellationToken).ConfigureAwait(false);
            var tagValue = new LogicalTagValue(tag!.Name, value, _timeProvider.GetUtcNow());
            return TagOperationResult<LogicalTagValue>.Success(tagValue);
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

    /// <inheritdoc />
    public async Task<IReadOnlyList<TagOperationResult<LogicalTagValue>>> ReadManyAsync(
        IReadOnlyCollection<string> tagNames,
        CancellationToken cancellationToken)
    {
        if (tagNames is null)
        {
            throw new ArgumentNullException(nameof(tagNames));
        }

        return await ReadManyCoreAsync(tagNames, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<TagOperationResult<LogicalTagValue>> WriteAsync(
        LogicalTagValue value,
        CancellationToken cancellationToken)
    {
        if (value is null)
        {
            throw new ArgumentNullException(nameof(value));
        }

        if (!TryGetTag(value.TagName, LogicalTagAccessMode.Write, out var tag, out var failure))
        {
            return failure;
        }

        try
        {
            var written = await WriteToPlcAsync(tag!, value.Value, cancellationToken)
                .ConfigureAwait(false);
            var tagValue = new LogicalTagValue(
                tag!.Name,
                written,
                _timeProvider.GetUtcNow(),
                value.Quality);
            return TagOperationResult<LogicalTagValue>.Success(tagValue);
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

    /// <inheritdoc />
    public async Task<IReadOnlyList<TagOperationResult<LogicalTagValue>>> WriteManyAsync(
        IReadOnlyCollection<LogicalTagValue> values,
        CancellationToken cancellationToken)
    {
        if (values is null)
        {
            throw new ArgumentNullException(nameof(values));
        }

        return await WriteManyCoreAsync(values, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public IObservable<LogicalTagValue> Observe(string tagName)
    {
        if (!TryGetTag(tagName, LogicalTagAccessMode.Read, out var tag, out var failure))
        {
            throw new KeyNotFoundException(failure.Error);
        }

        return ObserveFromPlc(tag!);
    }

    /// <inheritdoc />
    public IObservable<LogicalTagValue> ObserveMany(IReadOnlyCollection<string> tagNames)
    {
        if (tagNames is null)
        {
            throw new ArgumentNullException(nameof(tagNames));
        }

        return new MergedObservable(tagNames.Select(Observe).ToArray());
    }

    /// <inheritdoc />
    public IAsyncEnumerable<LogicalTagValue> ObserveAsync(
        string tagName,
        CancellationToken cancellationToken) => ToAsyncEnumerable(Observe(tagName), cancellationToken);

    /// <inheritdoc />
    public IAsyncEnumerable<LogicalTagValue> ObserveManyAsync(
        IReadOnlyCollection<string> tagNames,
        CancellationToken cancellationToken) => ToAsyncEnumerable(ObserveMany(tagNames), cancellationToken);

    /// <inheritdoc />
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        if (!_ownsCatalog || Catalog is not IDisposable disposable)
        {
            return;
        }

        disposable.Dispose();
    }
}
