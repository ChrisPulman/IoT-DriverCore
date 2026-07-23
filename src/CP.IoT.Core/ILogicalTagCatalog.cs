// Copyright (c) 2019-2026 Chris Pulman and contributors. All rights reserved.
// Chris Pulman and contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace IoT.DriverCore.Core;

/// <summary>Provides safe concurrent access to logical tag definitions.</summary>
public interface ILogicalTagCatalog
{
    /// <summary>Raised after a definition changes.</summary>
    event EventHandler<LogicalTagChangedEventArgs>? Changed;

    /// <summary>Adds a tag when its name is unused.</summary>
    /// <param name="tag">The tag to add.</param>
    /// <returns><see langword="true"/> if the tag was added; <see langword="false"/> if a tag with the same name already exists.</returns>
    bool TryAdd(LogicalTag tag);

    /// <summary>Adds or replaces a tag.</summary>
    /// <param name="tag">The tag to add or replace.</param>
    void Upsert(LogicalTag tag);

    /// <summary>Gets a tag by name.</summary>
    /// <param name="name">The tag name to look up.</param>
    /// <param name="tag">The matching tag, or <see langword="null"/> when not found.</param>
    /// <returns><see langword="true"/> if a tag with the given name was found.</returns>
    bool TryGet(string name, out LogicalTag? tag);

    /// <summary>Removes a tag by name.</summary>
    /// <param name="name">The name of the tag to remove.</param>
    /// <param name="tag">The removed tag, or <see langword="null"/> when not found.</param>
    /// <returns><see langword="true"/> if a tag was removed.</returns>
    bool TryRemove(string name, out LogicalTag? tag);

    /// <summary>Returns a stable ordered snapshot.</summary>
    /// <returns>A snapshot of all tags ordered by name.</returns>
    IReadOnlyList<LogicalTag> List();
}
