// Copyright (c) 2019-2026 Chris Pulman and contributors. All rights reserved.
// Chris Pulman and contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using IoT.DriverCore.Core;

#if REACTIVE_SHIM
namespace IoT.DriverCore.ModbusRx.Reactive.LogicalTags;
#else
namespace IoT.DriverCore.ModbusRx.LogicalTags;
#endif

/// <summary>Provides Modbus-specific CRUD over the common SQLite logical-tag store.</summary>
public sealed class ModbusTagSqliteStore
{
    /// <summary>Initializes a new instance of the <see cref="ModbusTagSqliteStore"/> class.</summary>
    /// <param name="connectionString">The SQLite connection string.</param>
    public ModbusTagSqliteStore(string connectionString) =>
        CoreStore = new(connectionString);

    /// <summary>Gets the composed common store.</summary>
    public LogicalTagSqliteStore CoreStore { get; }

    /// <summary>Creates or upgrades the common schema.</summary>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A task representing the operation.</returns>
    public Task InitializeAsync(CancellationToken cancellationToken) =>
        CoreStore.InitializeAsync(cancellationToken);

    /// <summary>Gets a Modbus tag by logical name.</summary>
    /// <param name="name">The logical name.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The stored definition, or null.</returns>
    public async Task<ModbusLogicalTag?> GetAsync(string name, CancellationToken cancellationToken)
    {
        var tag = await CoreStore.GetTagAsync(name, cancellationToken).ConfigureAwait(false);
        return tag is null ? null : ModbusLogicalTag.FromLogicalTag(tag);
    }

    /// <summary>Lists all stored Modbus tags.</summary>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The stored definitions.</returns>
    public async Task<IReadOnlyList<ModbusLogicalTag>> ListAsync(CancellationToken cancellationToken) =>
        (await CoreStore.ListTagsAsync(cancellationToken).ConfigureAwait(false))
            .Select(ModbusLogicalTag.FromLogicalTag)
            .ToArray();

    /// <summary>Creates or replaces a stored Modbus tag.</summary>
    /// <param name="tag">The definition to persist.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A task representing the operation.</returns>
    public Task UpsertAsync(ModbusLogicalTag tag, CancellationToken cancellationToken)
    {
        if (tag is null)
        {
            throw new ArgumentNullException(nameof(tag));
        }

        return CoreStore.UpsertTagAsync(tag.ToLogicalTag(), cancellationToken);
    }

    /// <summary>Updates an existing stored Modbus tag.</summary>
    /// <param name="tag">The definition to update.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>True when the definition existed.</returns>
    public Task<bool> UpdateAsync(ModbusLogicalTag tag, CancellationToken cancellationToken)
    {
        if (tag is null)
        {
            throw new ArgumentNullException(nameof(tag));
        }

        return CoreStore.UpdateTagAsync(tag.ToLogicalTag(), cancellationToken);
    }

    /// <summary>Deletes a stored Modbus tag.</summary>
    /// <param name="name">The logical name.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>True when the definition existed.</returns>
    public Task<bool> DeleteAsync(string name, CancellationToken cancellationToken) =>
        CoreStore.DeleteTagAsync(name, cancellationToken);

    /// <summary>Loads a new in-memory Modbus catalog from SQLite.</summary>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The loaded catalog.</returns>
    public async Task<ModbusTagCatalog> LoadCatalogAsync(CancellationToken cancellationToken)
    {
        var catalog = new ModbusTagCatalog();
        _ = await catalog.LoadFromSqliteAsync(CoreStore, cancellationToken).ConfigureAwait(false);
        return catalog;
    }
}
