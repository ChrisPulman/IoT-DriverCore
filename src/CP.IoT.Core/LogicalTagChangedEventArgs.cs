// Copyright (c) 2019-2026 Chris Pulman and contributors. All rights reserved.
// Chris Pulman and contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace CP.IoT.Core;

/// <summary>Event data for a catalog mutation.</summary>
public sealed class LogicalTagChangedEventArgs : EventArgs
{
    /// <summary>Initializes a new instance of the <see cref="LogicalTagChangedEventArgs"/> class.</summary>
    /// <param name="kind">The mutation kind.</param>
    /// <param name="tag">The affected tag.</param>
    public LogicalTagChangedEventArgs(LogicalTagChangeKind kind, LogicalTag tag) =>
        (Kind, Tag) = (kind, tag ?? throw new ArgumentNullException(nameof(tag)));

    /// <summary>Gets the mutation kind.</summary>
    public LogicalTagChangeKind Kind { get; }

    /// <summary>Gets the affected tag.</summary>
    public LogicalTag Tag { get; }
}
