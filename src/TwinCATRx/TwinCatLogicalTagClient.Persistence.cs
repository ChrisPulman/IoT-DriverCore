// Copyright (c) 2019-2026 Chris Pulman and contributors. All rights reserved.
// Chris Pulman and contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using CP.IoT.Core;

#if REACTIVE_SHIM
namespace CP.TwinCatRx.Reactive;
#else
namespace CP.TwinCatRx;
#endif

/// <summary>Maps logical CP.IoT tags onto an event-driven TwinCAT ADS client.</summary>
public sealed partial class TwinCatLogicalTagClient
{
    /// <summary>Imports CSV definitions into the live registry.</summary>
    /// <param name="reader">The CSV reader.</param>
    /// <returns>The imported tags.</returns>
    public Task<IReadOnlyList<LogicalTag>> ImportCsvAsync(TextReader reader) =>
        ImportCsvAsync(reader, replaceExisting: true, CancellationToken.None);

    /// <summary>Imports CSV definitions into the live registry.</summary>
    /// <param name="reader">The CSV reader.</param>
    /// <param name="replaceExisting">Whether imported tags replace matching live tags.</param>
    /// <returns>The imported tags.</returns>
    public Task<IReadOnlyList<LogicalTag>> ImportCsvAsync(TextReader reader, bool replaceExisting) =>
        ImportCsvAsync(reader, replaceExisting, CancellationToken.None);

    /// <summary>Imports CSV definitions into the live registry.</summary>
    /// <param name="reader">The CSV reader.</param>
    /// <param name="replaceExisting">Whether imported tags replace matching live tags.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The imported tags.</returns>
    public async Task<IReadOnlyList<LogicalTag>> ImportCsvAsync(
        TextReader reader,
        bool replaceExisting,
        CancellationToken cancellationToken)
    {
        ThrowIfDisposed();
        var tags = await LogicalTagCsv.ImportAsync(reader, cancellationToken: cancellationToken).ConfigureAwait(false);
        foreach (var tag in tags)
        {
            if (replaceExisting)
            {
                Catalog.Upsert(tag);
            }
            else
            {
                _ = Catalog.TryAdd(tag);
            }
        }

        return tags;
    }

    /// <summary>Exports the live registry as CSV.</summary>
    /// <param name="writer">The CSV writer.</param>
    /// <returns>The export operation.</returns>
    public Task ExportCsvAsync(TextWriter writer) => ExportCsvAsync(writer, CancellationToken.None);

    /// <summary>Exports the live registry as CSV.</summary>
    /// <param name="writer">The CSV writer.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The export operation.</returns>
    public Task ExportCsvAsync(TextWriter writer, CancellationToken cancellationToken)
    {
        ThrowIfDisposed();
        return LogicalTagCsv.ExportAsync(Catalog.List(), writer, cancellationToken: cancellationToken);
    }

    /// <summary>Initializes the configured SQLite store.</summary>
    /// <returns>The initialization operation.</returns>
    public Task InitializeStoreAsync() => InitializeStoreAsync(CancellationToken.None);

    /// <summary>Initializes the configured SQLite store.</summary>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The initialization operation.</returns>
    public Task InitializeStoreAsync(CancellationToken cancellationToken) =>
        RequireStore().InitializeAsync(cancellationToken);

    /// <summary>Dynamically loads persisted tags into the live registry.</summary>
    /// <returns>The loaded tags.</returns>
    public Task<IReadOnlyList<LogicalTag>> LoadTagsAsync() =>
        LoadTagsAsync(replaceExisting: true, CancellationToken.None);

    /// <summary>Dynamically loads persisted tags into the live registry.</summary>
    /// <param name="replaceExisting">Whether persisted tags replace matching live tags.</param>
    /// <returns>The loaded tags.</returns>
    public Task<IReadOnlyList<LogicalTag>> LoadTagsAsync(bool replaceExisting) =>
        LoadTagsAsync(replaceExisting, CancellationToken.None);

    /// <summary>Dynamically loads persisted tags into the live registry.</summary>
    /// <param name="replaceExisting">Whether persisted tags replace matching live tags.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The loaded tags.</returns>
    public async Task<IReadOnlyList<LogicalTag>> LoadTagsAsync(
        bool replaceExisting,
        CancellationToken cancellationToken)
    {
        ThrowIfDisposed();
        var tags = await RequireStore().ListTagsAsync(cancellationToken).ConfigureAwait(false);
        foreach (var tag in tags)
        {
            if (replaceExisting)
            {
                Catalog.Upsert(tag);
            }
            else
            {
                _ = Catalog.TryAdd(tag);
            }
        }

        return tags;
    }

    /// <summary>Gets a persisted tag.</summary>
    /// <param name="name">The logical tag name.</param>
    /// <returns>The persisted tag, or null.</returns>
    public Task<LogicalTag?> GetTagAsync(string name) => GetTagAsync(name, CancellationToken.None);

    /// <summary>Gets a persisted tag.</summary>
    /// <param name="name">The logical tag name.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The persisted tag, or null.</returns>
    public Task<LogicalTag?> GetTagAsync(string name, CancellationToken cancellationToken) =>
        RequireStore().GetTagAsync(TwinCatLogicalTagHelpers.Required(name, nameof(name)), cancellationToken);

    /// <summary>Upserts a tag in SQLite and the live registry.</summary>
    /// <param name="tag">The logical tag.</param>
    /// <returns>The upsert operation.</returns>
    public Task UpsertTagAsync(LogicalTag tag) => UpsertTagAsync(tag, CancellationToken.None);

    /// <summary>Upserts a tag in SQLite and the live registry.</summary>
    /// <param name="tag">The logical tag.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The upsert operation.</returns>
    public async Task UpsertTagAsync(LogicalTag tag, CancellationToken cancellationToken)
    {
        ThrowIfDisposed();
        if (tag is null)
        {
            throw new ArgumentNullException(nameof(tag));
        }

        await RequireStore().UpsertTagAsync(tag, cancellationToken).ConfigureAwait(false);
        Catalog.Upsert(tag);
    }

    /// <summary>Edits an existing SQLite tag and refreshes the live registry.</summary>
    /// <param name="tag">The logical tag.</param>
    /// <returns>Whether an existing tag was edited.</returns>
    public Task<bool> EditTagAsync(LogicalTag tag) => EditTagAsync(tag, CancellationToken.None);

    /// <summary>Edits an existing SQLite tag and refreshes the live registry.</summary>
    /// <param name="tag">The logical tag.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>Whether an existing tag was edited.</returns>
    public async Task<bool> EditTagAsync(LogicalTag tag, CancellationToken cancellationToken)
    {
        ThrowIfDisposed();
        if (tag is null)
        {
            throw new ArgumentNullException(nameof(tag));
        }

        var changed = await RequireStore().EditTagAsync(tag, cancellationToken).ConfigureAwait(false);
        if (changed)
        {
            Catalog.Upsert(tag);
        }

        return changed;
    }

    /// <summary>Deletes a tag from SQLite and the live registry.</summary>
    /// <param name="name">The logical tag name.</param>
    /// <returns>Whether an existing tag was deleted.</returns>
    public Task<bool> DeleteTagAsync(string name) => DeleteTagAsync(name, CancellationToken.None);

    /// <summary>Deletes a tag from SQLite and the live registry.</summary>
    /// <param name="name">The logical tag name.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>Whether an existing tag was deleted.</returns>
    public async Task<bool> DeleteTagAsync(string name, CancellationToken cancellationToken)
    {
        ThrowIfDisposed();
        var checkedName = TwinCatLogicalTagHelpers.Required(name, nameof(name));
        var deleted = await RequireStore().DeleteTagAsync(checkedName, cancellationToken).ConfigureAwait(false);
        if (deleted)
        {
            _ = Catalog.TryRemove(checkedName, out _);
        }

        return deleted;
    }
}
