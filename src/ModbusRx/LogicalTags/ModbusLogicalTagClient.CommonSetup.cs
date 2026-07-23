// Copyright (c) 2019-2026 Chris Pulman and contributors. All rights reserved.
// Chris Pulman and contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using IoT.DriverCore.Core;

#if REACTIVE_SHIM
namespace IoT.DriverCore.ModbusRx.Reactive.LogicalTags;
#else
namespace IoT.DriverCore.ModbusRx.LogicalTags;
#endif

/// <summary>Adapts the Modbus catalog and configured store to the common logical-tag setup contracts.</summary>
public sealed partial class ModbusLogicalTagClient
{
    /// <inheritdoc/>
    ILogicalTagCatalog ILogicalTagRegistry.Catalog => Catalog.CoreCatalog;

    /// <inheritdoc/>
    void ILogicalTagRegistry.RegisterTag(LogicalTag tag) =>
        RegisterTag(ModbusLogicalTag.FromLogicalTag(tag));

    /// <inheritdoc/>
    async Task<IReadOnlyList<LogicalTag>> ILogicalTagDefinitionExchange.ImportCsvAsync(
        TextReader reader,
        char delimiter,
        CancellationToken cancellationToken)
    {
        var tags = await LogicalTagCsv
            .ImportAsync(reader, delimiter, cancellationToken)
            .ConfigureAwait(false);
        foreach (var tag in tags)
        {
            Catalog.CoreCatalog.Upsert(ModbusLogicalTag.FromLogicalTag(tag).ToLogicalTag());
        }

        return tags;
    }

    /// <inheritdoc/>
    Task ILogicalTagDefinitionExchange.ExportCsvAsync(
        TextWriter writer,
        char delimiter,
        CancellationToken cancellationToken) =>
        LogicalTagCsv.ExportAsync(Catalog.CoreCatalog.List(), writer, delimiter, cancellationToken);

    /// <inheritdoc/>
    Task ILogicalTagPersistence.InitializeStoreAsync(CancellationToken cancellationToken) =>
        GetStore().InitializeAsync(cancellationToken);

    /// <inheritdoc/>
    async Task<IReadOnlyList<LogicalTag>> ILogicalTagPersistence.LoadTagsAsync(
        CancellationToken cancellationToken)
    {
        _ = await LoadTagsAsync(cancellationToken).ConfigureAwait(false);
        return Catalog.CoreCatalog.List();
    }

    /// <inheritdoc/>
    Task<LogicalTag?> ILogicalTagPersistence.GetTagAsync(
        string name,
        CancellationToken cancellationToken) =>
        GetStore().CoreStore.GetTagAsync(name, cancellationToken);

    /// <inheritdoc/>
    Task<IReadOnlyList<LogicalTag>> ILogicalTagPersistence.ListTagsAsync(
        CancellationToken cancellationToken) =>
        GetStore().CoreStore.ListTagsAsync(cancellationToken);

    /// <inheritdoc/>
    async Task ILogicalTagPersistence.UpsertTagAsync(
        LogicalTag tag,
        CancellationToken cancellationToken)
    {
        await GetStore().CoreStore.UpsertTagAsync(tag, cancellationToken).ConfigureAwait(false);
        Catalog.CoreCatalog.Upsert(tag);
    }

    /// <inheritdoc/>
    async Task<bool> ILogicalTagPersistence.EditTagAsync(
        LogicalTag tag,
        CancellationToken cancellationToken)
    {
        var updated = await GetStore().CoreStore
            .UpdateTagAsync(tag, cancellationToken)
            .ConfigureAwait(false);
        if (updated)
        {
            Catalog.CoreCatalog.Upsert(tag);
        }

        return updated;
    }

    /// <inheritdoc/>
    Task<bool> ILogicalTagPersistence.DeleteTagAsync(
        string name,
        CancellationToken cancellationToken) =>
        DeleteStoredTagAsync(name, cancellationToken);

    /// <inheritdoc/>
    Task<LogicalTagGroup?> ILogicalTagGroupPersistence.GetGroupAsync(
        string name,
        CancellationToken cancellationToken) =>
        GetStore().CoreStore.GetGroupAsync(name, cancellationToken);

    /// <inheritdoc/>
    Task<IReadOnlyList<LogicalTagGroup>> ILogicalTagGroupPersistence.ListGroupsAsync(
        CancellationToken cancellationToken) =>
        GetStore().CoreStore.ListGroupsAsync(cancellationToken);

    /// <inheritdoc/>
    Task ILogicalTagGroupPersistence.UpsertGroupAsync(
        LogicalTagGroup group,
        CancellationToken cancellationToken) =>
        GetStore().CoreStore.UpsertGroupAsync(group, cancellationToken);

    /// <inheritdoc/>
    Task<bool> ILogicalTagGroupPersistence.DeleteGroupAsync(
        string name,
        CancellationToken cancellationToken) =>
        GetStore().CoreStore.DeleteGroupAsync(name, cancellationToken);
}
