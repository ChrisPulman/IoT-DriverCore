// Copyright (c) 2019-2026 Chris Pulman and contributors. All rights reserved.
// Chris Pulman and contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace CP.IoT.Core;

/// <summary>Describes an immutable logical tag group.</summary>
public sealed class LogicalTagGroup
{
    /// <summary>Initializes a new instance of the <see cref="LogicalTagGroup"/> class with a name only.</summary>
    /// <param name="name">The unique group name.</param>
    public LogicalTagGroup(string name)
        : this(name, null, null)
    {
    }

    /// <summary>Initializes a new instance of the <see cref="LogicalTagGroup"/> class with a name and description.</summary>
    /// <param name="name">The unique group name.</param>
    /// <param name="description">The optional human-readable description.</param>
    public LogicalTagGroup(string name, string? description)
        : this(name, description, null)
    {
    }

    /// <summary>Initializes a new instance of the <see cref="LogicalTagGroup"/> class.</summary>
    /// <param name="name">The unique group name.</param>
    /// <param name="description">The optional human-readable description.</param>
    /// <param name="metadata">The optional metadata dictionary; entries are copied defensively.</param>
    public LogicalTagGroup(string name, string? description, IReadOnlyDictionary<string, string>? metadata)
    {
        Name = LogicalTag.Required(name, nameof(name));
        Description = description?.Trim() ?? string.Empty;
        Metadata = LogicalTag.Copy(metadata);
    }

    /// <summary>Gets the unique group name.</summary>
    public string Name { get; }

    /// <summary>Gets the optional group description.</summary>
    public string Description { get; }

    /// <summary>Gets immutable group metadata.</summary>
    public IReadOnlyDictionary<string, string> Metadata { get; }
}
