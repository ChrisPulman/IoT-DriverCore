// Copyright (c) 2019-2026 Chris Pulman and contributors. All rights reserved.
// Chris Pulman and contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using IoT.DriverCore.Core;

#if REACTIVE_SHIM

namespace IoT.DriverCore.MitsubishiRx.Reactive;

#else

namespace IoT.DriverCore.MitsubishiRx;

#endif

/// <summary>Composes common logical-tag operations with Mitsubishi protocol transports.</summary>
public sealed partial class MitsubishiLogicalTagClient
{
    /// <inheritdoc/>
    public Task<IReadOnlyList<LogicalTag>> ListTagsAsync(
        CancellationToken cancellationToken) =>
        GetStore().ListTagsAsync(cancellationToken);

    /// <inheritdoc/>
    public Task<LogicalTagGroup?> GetGroupAsync(
        string name,
        CancellationToken cancellationToken) =>
        GetStore().GetGroupAsync(name, cancellationToken);

    /// <inheritdoc/>
    public Task<IReadOnlyList<LogicalTagGroup>> ListGroupsAsync(
        CancellationToken cancellationToken) =>
        GetStore().ListGroupsAsync(cancellationToken);

    /// <inheritdoc/>
    public Task UpsertGroupAsync(
        LogicalTagGroup group,
        CancellationToken cancellationToken) =>
        GetStore().UpsertGroupAsync(group, cancellationToken);

    /// <inheritdoc/>
    public Task<bool> DeleteGroupAsync(
        string name,
        CancellationToken cancellationToken) =>
        GetStore().DeleteGroupAsync(name, cancellationToken);
}
