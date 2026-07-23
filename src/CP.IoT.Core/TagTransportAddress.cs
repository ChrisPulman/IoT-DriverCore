// Copyright (c) 2019-2026 Chris Pulman and contributors. All rights reserved.
// Chris Pulman and contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace IoT.DriverCore.Core;

/// <summary>Represents an opaque, parser-provided physical address suitable for protocol-neutral batch planning.</summary>
public sealed class TagTransportAddress : IEquatable<TagTransportAddress>
{
    /// <summary>Initializes a new instance of the <see cref="TagTransportAddress"/> class.</summary>
    /// <param name="transportPartition">The adapter-defined transport partition, such as a PLC, endpoint, or connection.</param>
    /// <param name="memoryArea">The adapter-defined memory area.</param>
    /// <param name="encoding">The adapter-defined transfer encoding.</param>
    /// <param name="access">The direction of the planned transfer.</param>
    /// <param name="route">The adapter-defined route; an empty route represents the adapter default route.</param>
    /// <param name="offset">The non-negative numeric offset within <paramref name="memoryArea"/>.</param>
    /// <param name="length">The positive number of addressable units occupied by the tag.</param>
    public TagTransportAddress(string transportPartition, string memoryArea, string encoding, TagTransferAccess access, string route, long offset, long length)
    {
        TransportPartition = LogicalTag.Required(transportPartition, nameof(transportPartition));
        MemoryArea = LogicalTag.Required(memoryArea, nameof(memoryArea));
        Encoding = LogicalTag.Required(encoding, nameof(encoding));
        if (!Enum.IsDefined(typeof(TagTransferAccess), access))
        {
            throw new ArgumentOutOfRangeException(nameof(access));
        }

        if (route is null)
        {
            throw new ArgumentNullException(nameof(route));
        }

        if (offset < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(offset));
        }

        if (length <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(length));
        }

        _ = checked(offset + length);
        Access = access;
        Route = route.Trim();
        Offset = offset;
        Length = length;
    }

    /// <summary>Gets the adapter-defined transport partition.</summary>
    public string TransportPartition { get; }

    /// <summary>Gets the adapter-defined memory area.</summary>
    public string MemoryArea { get; }

    /// <summary>Gets the adapter-defined encoding.</summary>
    public string Encoding { get; }

    /// <summary>Gets the direction of the planned transfer.</summary>
    public TagTransferAccess Access { get; }

    /// <summary>Gets the adapter-defined route, or an empty string for its default route.</summary>
    public string Route { get; }

    /// <summary>Gets the zero-based numeric offset within the memory area.</summary>
    public long Offset { get; }

    /// <summary>Gets the number of addressable units occupied by this address.</summary>
    public long Length { get; }

    /// <summary>Gets the exclusive end offset.</summary>
    public long EndOffset => checked(Offset + Length);

    /// <inheritdoc/>
    public bool Equals(TagTransportAddress? other) => other is not null
        && StringComparer.Ordinal.Equals(TransportPartition, other.TransportPartition)
        && StringComparer.Ordinal.Equals(MemoryArea, other.MemoryArea)
        && StringComparer.Ordinal.Equals(Encoding, other.Encoding)
        && Access == other.Access
        && StringComparer.Ordinal.Equals(Route, other.Route)
        && Offset == other.Offset
        && Length == other.Length;

    /// <inheritdoc/>
    public override bool Equals(object? obj) => Equals(obj as TagTransportAddress);

    /// <inheritdoc/>
    public override int GetHashCode()
    {
        unchecked
        {
            var hash = 17;
            hash = (hash * 31) + StringComparer.Ordinal.GetHashCode(TransportPartition);
            hash = (hash * 31) + StringComparer.Ordinal.GetHashCode(MemoryArea);
            hash = (hash * 31) + StringComparer.Ordinal.GetHashCode(Encoding);
            hash = (hash * 31) + Access.GetHashCode();
            hash = (hash * 31) + StringComparer.Ordinal.GetHashCode(Route);
            hash = (hash * 31) + Offset.GetHashCode();
            return (hash * 31) + Length.GetHashCode();
        }
    }

    /// <summary>Compares the partition fields that must match before two ranges can coalesce.</summary>
    /// <param name="left">The first address.</param>
    /// <param name="right">The second address.</param>
    /// <returns>A value indicating the deterministic ordinal sort order of the compatibility fields.</returns>
    internal static int ComparePartition(TagTransportAddress left, TagTransportAddress right)
    {
        var result = StringComparer.Ordinal.Compare(left.TransportPartition, right.TransportPartition);
        result = result != 0 ? result : StringComparer.Ordinal.Compare(left.MemoryArea, right.MemoryArea);
        result = result != 0 ? result : StringComparer.Ordinal.Compare(left.Encoding, right.Encoding);
        result = result != 0 ? result : left.Access.CompareTo(right.Access);
        return result != 0 ? result : StringComparer.Ordinal.Compare(left.Route, right.Route);
    }
}
