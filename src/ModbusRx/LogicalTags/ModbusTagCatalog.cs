// Copyright (c) 2019-2026 Chris Pulman and contributors. All rights reserved.
// Chris Pulman and contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using IoT.DriverCore.Core;

#if REACTIVE_SHIM
namespace IoT.DriverCore.ModbusRx.Reactive.LogicalTags;
#else
namespace IoT.DriverCore.ModbusRx.LogicalTags;
#endif

/// <summary>Provides strongly typed Modbus access over a common logical-tag catalog.</summary>
public sealed class ModbusTagCatalog : IDisposable
{
    /// <summary>Indicates whether this wrapper owns the common catalog.</summary>
    private readonly bool _ownsCatalog;

    /// <summary>Initializes a new instance of the <see cref="ModbusTagCatalog"/> class.</summary>
    public ModbusTagCatalog()
        : this(new LogicalTagCatalog(), true)
    {
    }

    /// <summary>Initializes a new instance of the <see cref="ModbusTagCatalog"/> class.</summary>
    /// <param name="catalog">The common catalog to wrap.</param>
    public ModbusTagCatalog(ILogicalTagCatalog catalog)
        : this(catalog, false)
    {
    }

    /// <summary>Initializes a new instance of the <see cref="ModbusTagCatalog"/> class.</summary>
    /// <param name="catalog">The common catalog.</param>
    /// <param name="ownsCatalog">Whether this wrapper owns the catalog.</param>
    private ModbusTagCatalog(ILogicalTagCatalog catalog, bool ownsCatalog)
    {
        CoreCatalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
        _ownsCatalog = ownsCatalog;
    }

    /// <summary>Gets the composed common catalog.</summary>
    public ILogicalTagCatalog CoreCatalog { get; }

    /// <summary>Creates a validated tag definition without registering it.</summary>
    /// <param name="configuration">The address and behavior configuration.</param>
    /// <returns>The validated definition.</returns>
    public static ModbusLogicalTag Create(ModbusTagConfiguration configuration) =>
        new(configuration);

    /// <summary>Adds a definition when its logical name is unused.</summary>
    /// <param name="tag">The definition to add.</param>
    /// <returns>True when the definition was added.</returns>
    public bool TryAdd(ModbusLogicalTag tag)
    {
        if (tag is null)
        {
            throw new ArgumentNullException(nameof(tag));
        }

        return CoreCatalog.TryAdd(tag.ToLogicalTag());
    }

    /// <summary>Adds or replaces a definition.</summary>
    /// <param name="tag">The definition to add or replace.</param>
    public void Upsert(ModbusLogicalTag tag)
    {
        if (tag is null)
        {
            throw new ArgumentNullException(nameof(tag));
        }

        CoreCatalog.Upsert(tag.ToLogicalTag());
    }

    /// <summary>Gets a definition by logical name.</summary>
    /// <param name="name">The logical name.</param>
    /// <param name="tag">The resolved definition.</param>
    /// <returns>True when the definition exists.</returns>
    public bool TryGet(string name, out ModbusLogicalTag? tag)
    {
        if (CoreCatalog.TryGet(name, out var logicalTag) && logicalTag is not null)
        {
            tag = ModbusLogicalTag.FromLogicalTag(logicalTag);
            return true;
        }

        tag = null;
        return false;
    }

    /// <summary>Removes a definition by logical name.</summary>
    /// <param name="name">The logical name.</param>
    /// <param name="tag">The removed definition.</param>
    /// <returns>True when the definition was removed.</returns>
    public bool TryRemove(string name, out ModbusLogicalTag? tag)
    {
        if (CoreCatalog.TryRemove(name, out var logicalTag) && logicalTag is not null)
        {
            tag = ModbusLogicalTag.FromLogicalTag(logicalTag);
            return true;
        }

        tag = null;
        return false;
    }

    /// <summary>Returns a stable logical-name-ordered snapshot.</summary>
    /// <returns>The current definitions.</returns>
    public IReadOnlyList<ModbusLogicalTag> List() =>
        CoreCatalog.List().Select(ModbusLogicalTag.FromLogicalTag).ToArray();

    /// <summary>Imports common RFC 4180 CSV definitions into this catalog.</summary>
    /// <param name="reader">The CSV reader.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The number of imported definitions.</returns>
    public async Task<int> ImportCsvAsync(TextReader reader, CancellationToken cancellationToken)
    {
        var tags = await LogicalTagCsv.ImportAsync(reader, cancellationToken: cancellationToken).ConfigureAwait(false);
        foreach (var tag in tags)
        {
            CoreCatalog.Upsert(ModbusLogicalTag.FromLogicalTag(tag).ToLogicalTag());
        }

        return tags.Count;
    }

    /// <summary>Exports this catalog using the common RFC 4180 CSV representation.</summary>
    /// <param name="writer">The CSV writer.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A task representing the operation.</returns>
    public Task ExportCsvAsync(TextWriter writer, CancellationToken cancellationToken) =>
        LogicalTagCsv.ExportAsync(CoreCatalog.List(), writer, cancellationToken: cancellationToken);

    /// <summary>Replaces the in-memory snapshot with tags currently stored in SQLite.</summary>
    /// <param name="store">The common SQLite store.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The number of loaded definitions.</returns>
    public async Task<int> LoadFromSqliteAsync(
        LogicalTagSqliteStore store,
        CancellationToken cancellationToken)
    {
        if (store is null)
        {
            throw new ArgumentNullException(nameof(store));
        }

        var loaded = await store.ListTagsAsync(cancellationToken).ConfigureAwait(false);
        var converted = loaded.Select(ModbusLogicalTag.FromLogicalTag).ToArray();
        foreach (var existing in CoreCatalog.List())
        {
            _ = CoreCatalog.TryRemove(existing.Name, out _);
        }

        foreach (var tag in converted)
        {
            CoreCatalog.Upsert(tag.ToLogicalTag());
        }

        return converted.Length;
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        if (!_ownsCatalog || CoreCatalog is not IDisposable disposable)
        {
            return;
        }

        disposable.Dispose();
    }
}
