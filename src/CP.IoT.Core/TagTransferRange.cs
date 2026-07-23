// Copyright (c) 2019-2026 Chris Pulman and contributors. All rights reserved.
// Chris Pulman and contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Collections.ObjectModel;

namespace IoT.DriverCore.Core;

/// <summary>Represents one coalesced, adapter-compatible transfer range.</summary>
public sealed class TagTransferRange
{
    /// <summary>Initializes a new instance of the <see cref="TagTransferRange"/> class.</summary>
    /// <param name="sourceAddress">The compatibility coordinates shared by every source item.</param>
    /// <param name="offset">The inclusive numeric start offset.</param>
    /// <param name="length">The number of addressable units.</param>
    /// <param name="items">The source items in deterministic offset order.</param>
    internal TagTransferRange(TagTransportAddress sourceAddress, long offset, long length, IReadOnlyList<TagTransferItem> items)
    {
        Address = new(
            sourceAddress.TransportPartition,
            sourceAddress.MemoryArea,
            sourceAddress.Encoding,
            sourceAddress.Access,
            sourceAddress.Route,
            offset,
            length);
        Offset = offset;
        Length = length;
        Items = new ReadOnlyCollection<TagTransferItem>(items.ToArray());
        InputIndices = new ReadOnlyCollection<int>(items.Select(static item => item.InputIndex).OrderBy(static index => index).ToArray());
    }

    /// <summary>Gets the compatibility coordinates and precise span to execute.</summary>
    public TagTransportAddress Address { get; }

    /// <summary>Gets the inclusive numeric start offset of this transfer.</summary>
    public long Offset { get; }

    /// <summary>Gets the number of addressable units to transfer.</summary>
    public long Length { get; }

    /// <summary>Gets the exclusive numeric end offset of this transfer.</summary>
    public long EndOffset => checked(Offset + Length);

    /// <summary>Gets source items in deterministic offset order.</summary>
    public IReadOnlyList<TagTransferItem> Items { get; }

    /// <summary>Gets source indexes in original input order for safe result correlation.</summary>
    public IReadOnlyList<int> InputIndices { get; }
}
