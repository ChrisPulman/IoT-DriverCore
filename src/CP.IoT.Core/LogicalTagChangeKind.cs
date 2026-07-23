// Copyright (c) 2019-2026 Chris Pulman and contributors. All rights reserved.
// Chris Pulman and contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace IoT.DriverCore.Core;

/// <summary>Describes a catalog mutation.</summary>
public enum LogicalTagChangeKind
{
    /// <summary>A tag was added to the catalog.</summary>
    Added,

    /// <summary>An existing tag was replaced in the catalog.</summary>
    Updated,

    /// <summary>A tag was removed from the catalog.</summary>
    Removed,
}
