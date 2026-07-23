// Copyright (c) 2019-2026 Chris Pulman and contributors. All rights reserved.
// Chris Pulman and contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace IoT.DriverCore.Core;

/// <summary>Provides common logical-tag persistence operations over a configured store.</summary>
public interface ILogicalTagPersistence
{
    /// <summary>Initializes the configured persistence store.</summary>
    /// <param name="cancellationToken">A token that cancels the operation.</param>
    /// <returns>A task that represents the asynchronous initialization operation.</returns>
    Task InitializeStoreAsync(CancellationToken cancellationToken);

    /// <summary>Loads persisted tags into the live client.</summary>
    /// <param name="cancellationToken">A token that cancels the operation.</param>
    /// <returns>The loaded logical tags.</returns>
    Task<IReadOnlyList<LogicalTag>> LoadTagsAsync(CancellationToken cancellationToken);

    /// <summary>Gets a persisted tag by name.</summary>
    /// <param name="name">The logical tag name.</param>
    /// <param name="cancellationToken">A token that cancels the operation.</param>
    /// <returns>The persisted tag, or <see langword="null"/> when it is absent.</returns>
    Task<LogicalTag?> GetTagAsync(string name, CancellationToken cancellationToken);

    /// <summary>Lists persisted tags in stable name order.</summary>
    /// <param name="cancellationToken">A token that cancels the operation.</param>
    /// <returns>The persisted logical tags.</returns>
    Task<IReadOnlyList<LogicalTag>> ListTagsAsync(CancellationToken cancellationToken);

    /// <summary>Inserts or replaces a persisted tag and synchronizes the live client.</summary>
    /// <param name="tag">The logical tag definition.</param>
    /// <param name="cancellationToken">A token that cancels the operation.</param>
    /// <returns>A task that represents the asynchronous upsert operation.</returns>
    Task UpsertTagAsync(LogicalTag tag, CancellationToken cancellationToken);

    /// <summary>Edits a persisted tag and synchronizes the live client when it exists.</summary>
    /// <param name="tag">The replacement logical tag definition.</param>
    /// <param name="cancellationToken">A token that cancels the operation.</param>
    /// <returns><see langword="true"/> when an existing tag was edited.</returns>
    Task<bool> EditTagAsync(LogicalTag tag, CancellationToken cancellationToken);

    /// <summary>Deletes a persisted tag and removes it from the live client when it exists.</summary>
    /// <param name="name">The logical tag name.</param>
    /// <param name="cancellationToken">A token that cancels the operation.</param>
    /// <returns><see langword="true"/> when an existing tag was deleted.</returns>
    Task<bool> DeleteTagAsync(string name, CancellationToken cancellationToken);
}
