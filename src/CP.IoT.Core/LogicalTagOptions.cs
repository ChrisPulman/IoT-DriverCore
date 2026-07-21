// Copyright (c) 2019-2026 Chris Pulman and contributors. All rights reserved.
// Chris Pulman and contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace CP.IoT.Core;

/// <summary>Groups the optional construction parameters for a <see cref="LogicalTag"/>.</summary>
public sealed class LogicalTagOptions
{
    /// <summary>Gets or sets the optional group name.</summary>
    public string? GroupName { get; set; }

    /// <summary>Gets or sets the optional human-readable description.</summary>
    public string? Description { get; set; }

    /// <summary>Gets or sets the optional metadata dictionary.</summary>
    public IReadOnlyDictionary<string, string>? Metadata { get; set; }

    /// <summary>Gets or sets the permitted read/write access mode.</summary>
    public LogicalTagAccessMode AccessMode { get; set; } = LogicalTagAccessMode.ReadWrite;

    /// <summary>Gets or sets the optional poll interval.</summary>
    public TimeSpan? ScanInterval { get; set; }
}
