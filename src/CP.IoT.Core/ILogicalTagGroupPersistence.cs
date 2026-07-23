// Copyright (c) 2019-2026 Chris Pulman and contributors. All rights reserved.
// Chris Pulman and contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace IoT.DriverCore.Core;

/// <summary>Provides common persistence operations for logical-tag groups.</summary>
public interface ILogicalTagGroupPersistence
{
    /// <summary>Gets a persisted logical-tag group by name.</summary>
    /// <param name="name">The group name.</param>
    /// <param name="cancellationToken">A token that cancels the operation.</param>
    /// <returns>The persisted group, or <see langword="null"/> when it is absent.</returns>
    Task<LogicalTagGroup?> GetGroupAsync(string name, CancellationToken cancellationToken);

    /// <summary>Lists persisted logical-tag groups in stable name order.</summary>
    /// <param name="cancellationToken">A token that cancels the operation.</param>
    /// <returns>The persisted logical-tag groups.</returns>
    Task<IReadOnlyList<LogicalTagGroup>> ListGroupsAsync(CancellationToken cancellationToken);

    /// <summary>Inserts or replaces a persisted logical-tag group.</summary>
    /// <param name="group">The logical-tag group.</param>
    /// <param name="cancellationToken">A token that cancels the operation.</param>
    /// <returns>A task that represents the asynchronous upsert operation.</returns>
    Task UpsertGroupAsync(LogicalTagGroup group, CancellationToken cancellationToken);

    /// <summary>Deletes a persisted logical-tag group.</summary>
    /// <param name="name">The group name.</param>
    /// <param name="cancellationToken">A token that cancels the operation.</param>
    /// <returns><see langword="true"/> when an existing group was deleted.</returns>
    Task<bool> DeleteGroupAsync(string name, CancellationToken cancellationToken);
}
