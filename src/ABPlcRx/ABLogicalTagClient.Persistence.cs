// Copyright (c) 2019-2026 Chris Pulman and contributors. All rights reserved.
// Chris Pulman and contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using CP.IoT.Core;

#if REACTIVELIST_REACTIVE
namespace ABPlcRx.Reactive;
#else
namespace ABPlcRx;
#endif

/// <summary>Adapts an Allen-Bradley controller to shared logical-tag contracts through composition.</summary>
public sealed partial class ABLogicalTagClient
{
    /// <summary>Imports CSV definitions through <see cref="LogicalTagCsv"/> and registers them.</summary>
    /// <param name="reader">The CSV reader.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>The imported definitions.</returns>
    public async Task<IReadOnlyList<LogicalTag>> ImportCsvAsync(
        TextReader reader,
        CancellationToken cancellationToken)
    {
        var tags = await LogicalTagCsv.ImportAsync(reader, cancellationToken: cancellationToken)
            .ConfigureAwait(false);
        foreach (var tag in tags)
        {
            RegisterTag(tag);
        }

        return tags;
    }

    /// <summary>Exports the current catalog through <see cref="LogicalTagCsv"/>.</summary>
    /// <param name="writer">The CSV writer.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>A task that completes after export.</returns>
    public Task ExportCsvAsync(TextWriter writer, CancellationToken cancellationToken) =>
        LogicalTagCsv.ExportAsync(Catalog.List(), writer, cancellationToken: cancellationToken);

    /// <summary>Initializes and retains a SQLite store for CRUD operations.</summary>
    /// <param name="store">The SQLite store.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>A task that completes after schema initialization.</returns>
    public async Task InitializeStoreAsync(
        LogicalTagSqliteStore store,
        CancellationToken cancellationToken)
    {
        _store = store ?? throw new ArgumentNullException(nameof(store));
        await _store.InitializeAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Loads the configured SQLite catalog and dynamically registers every definition.</summary>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>The loaded definitions.</returns>
    public async Task<IReadOnlyList<LogicalTag>> LoadTagsAsync(CancellationToken cancellationToken)
    {
        ThrowIfDisposed();
        using var loadedCatalog = await GetStore().LoadCatalogAsync(cancellationToken).ConfigureAwait(false);
        var loadedTags = loadedCatalog.List();
        var loadedNames = loadedTags.Select(tag => tag.Name).ToHashSet(StringComparer.Ordinal);
        foreach (var existing in Catalog.List().Where(tag => !loadedNames.Contains(tag.Name)))
        {
            _ = RemoveTag(existing.Name);
        }

        foreach (var tag in loadedTags)
        {
            RegisterTag(tag);
        }

        return loadedTags;
    }

    /// <summary>Gets a tag from the configured SQLite store.</summary>
    /// <param name="tagName">The logical tag name.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>The stored tag, or null.</returns>
    public Task<LogicalTag?> GetTagAsync(string tagName, CancellationToken cancellationToken) =>
        GetStore().GetTagAsync(tagName, cancellationToken);

    /// <summary>Upserts a SQLite tag and synchronizes the live catalog.</summary>
    /// <param name="tag">The tag definition.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>A task that completes after synchronization.</returns>
    public async Task UpsertTagAsync(LogicalTag tag, CancellationToken cancellationToken)
    {
        await GetStore().UpsertTagAsync(tag, cancellationToken).ConfigureAwait(false);
        RegisterTag(tag);
    }

    /// <summary>Edits a SQLite tag and synchronizes the live catalog when found.</summary>
    /// <param name="tag">The replacement definition.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>True when the persisted tag existed.</returns>
    public async Task<bool> EditTagAsync(LogicalTag tag, CancellationToken cancellationToken)
    {
        var edited = await GetStore().EditTagAsync(tag, cancellationToken).ConfigureAwait(false);
        if (edited)
        {
            RegisterTag(tag);
        }

        return edited;
    }

    /// <summary>Deletes a SQLite tag and removes it from the live catalog when found.</summary>
    /// <param name="tagName">The logical tag name.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>True when the persisted tag existed.</returns>
    public async Task<bool> DeleteTagAsync(string tagName, CancellationToken cancellationToken)
    {
        var deleted = await GetStore().DeleteTagAsync(tagName, cancellationToken).ConfigureAwait(false);
        if (deleted)
        {
            _ = RemoveTag(tagName);
        }

        return deleted;
    }

    /// <summary>Gets the configured SQLite store.</summary>
    /// <returns>The configured store.</returns>
    private LogicalTagSqliteStore GetStore()
    {
        ThrowIfDisposed();
        return _store ?? throw new InvalidOperationException(
            "Configure a SQLite store before using persistent tag operations.");
    }
}
