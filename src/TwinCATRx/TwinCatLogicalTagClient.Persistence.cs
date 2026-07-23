// Copyright (c) 2019-2026 Chris Pulman and contributors. All rights reserved.
// Chris Pulman and contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using IoT.DriverCore.Core;

#if REACTIVE_SHIM
namespace IoT.DriverCore.TwinCATRx.Reactive;
#else
namespace IoT.DriverCore.TwinCATRx;
#endif

/// <summary>Maps logical CP.IoT tags onto an event-driven TwinCAT ADS client.</summary>
public sealed partial class TwinCatLogicalTagClient
{
    /// <summary>Imports CSV definitions into the live registry.</summary>
    /// <param name="reader">The CSV reader.</param>
    /// <returns>The imported tags.</returns>
    public Task<IReadOnlyList<LogicalTag>> ImportCsvAsync(TextReader reader) =>
        ImportCsvAsync(reader, replaceExisting: true, CancellationToken.None);

    /// <inheritdoc/>
    public Task<IReadOnlyList<LogicalTag>> ImportCsvAsync(
        TextReader reader,
        char delimiter,
        CancellationToken cancellationToken) =>
        ImportCsvAsync(reader, delimiter, replaceExisting: true, cancellationToken);

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
    public Task<IReadOnlyList<LogicalTag>> ImportCsvAsync(
        TextReader reader,
        bool replaceExisting,
        CancellationToken cancellationToken) =>
        ImportCsvAsync(reader, ',', replaceExisting, cancellationToken);

    /// <summary>Imports CSV definitions into the live registry.</summary>
    /// <param name="reader">The CSV reader.</param>
    /// <param name="delimiter">The CSV delimiter.</param>
    /// <param name="replaceExisting">Whether imported tags replace matching live tags.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The imported tags.</returns>
    public async Task<IReadOnlyList<LogicalTag>> ImportCsvAsync(
        TextReader reader,
        char delimiter,
        bool replaceExisting,
        CancellationToken cancellationToken)
    {
        ThrowIfDisposed();
        var tags = await LogicalTagCsv
            .ImportAsync(reader, delimiter, cancellationToken)
            .ConfigureAwait(false);
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
        => ExportCsvAsync(writer, ',', cancellationToken);

    /// <inheritdoc/>
    public Task ExportCsvAsync(
        TextWriter writer,
        char delimiter,
        CancellationToken cancellationToken)
    {
        ThrowIfDisposed();
        return LogicalTagCsv.ExportAsync(Catalog.List(), writer, delimiter, cancellationToken);
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

    /// <inheritdoc/>
    public Task<IReadOnlyList<LogicalTag>> LoadTagsAsync(CancellationToken cancellationToken) =>
        LoadTagsAsync(replaceExisting: true, cancellationToken);

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

    /// <inheritdoc/>
    public Task<IReadOnlyList<LogicalTag>> ListTagsAsync(CancellationToken cancellationToken) =>
        RequireStore().ListTagsAsync(cancellationToken);

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

    /// <inheritdoc/>
    public Task<LogicalTagGroup?> GetGroupAsync(
        string name,
        CancellationToken cancellationToken) =>
        RequireStore().GetGroupAsync(
            TwinCatLogicalTagHelpers.Required(name, nameof(name)),
            cancellationToken);

    /// <inheritdoc/>
    public Task<IReadOnlyList<LogicalTagGroup>> ListGroupsAsync(CancellationToken cancellationToken) =>
        RequireStore().ListGroupsAsync(cancellationToken);

    /// <inheritdoc/>
    public Task UpsertGroupAsync(
        LogicalTagGroup group,
        CancellationToken cancellationToken)
    {
        ThrowIfDisposed();
        return RequireStore().UpsertGroupAsync(
            group ?? throw new ArgumentNullException(nameof(group)),
            cancellationToken);
    }

    /// <inheritdoc/>
    public Task<bool> DeleteGroupAsync(
        string name,
        CancellationToken cancellationToken) =>
        RequireStore().DeleteGroupAsync(
            TwinCatLogicalTagHelpers.Required(name, nameof(name)),
            cancellationToken);
}
