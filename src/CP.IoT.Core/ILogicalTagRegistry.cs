// Copyright (c) 2019-2026 Chris Pulman and contributors. All rights reserved.
// Chris Pulman and contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace IoT.DriverCore.Core;

/// <summary>Provides a common catalog and registration surface after protocol-specific client setup.</summary>
public interface ILogicalTagRegistry
{
    /// <summary>Gets the logical-tag catalog used by the client.</summary>
    ILogicalTagCatalog Catalog { get; }

    /// <summary>Adds or replaces a logical tag in the live client.</summary>
    /// <param name="tag">The logical tag definition.</param>
    void RegisterTag(LogicalTag tag);

    /// <summary>Removes a logical tag from the live client.</summary>
    /// <param name="name">The logical tag name.</param>
    /// <returns><see langword="true"/> when the tag existed.</returns>
    bool RemoveTag(string name);
}
