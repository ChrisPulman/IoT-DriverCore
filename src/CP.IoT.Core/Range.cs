// Copyright (c) 2019-2026 Chris Pulman and contributors. All rights reserved.
// Chris Pulman and contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

#if NETSTANDARD2_0

namespace System;

/// <summary>Represents a range that has start and end indexes.</summary>
internal readonly struct Range : IEquatable<Range>
{
    /// <summary>Initializes a new instance of the <see cref="Range"/> class.</summary>
    /// <param name="start">The inclusive start index of the range.</param>
    /// <param name="end">The exclusive end index of the range.</param>
    public Range(Index start, Index end)
    {
        Start = start;
        End = end;
    }

    /// <summary>Gets a <see cref="Range"/> that covers the entire collection.</summary>
    internal static Range All => new(Index.Start, Index.End);

    /// <summary>Gets the inclusive start index of the range.</summary>
    internal Index Start { get; }

    /// <summary>Gets the exclusive end index of the range.</summary>
    internal Index End { get; }

    /// <inheritdoc/>
    public bool Equals(Range other) => Start.Equals(other.Start) && End.Equals(other.End);

    /// <inheritdoc/>
    public override bool Equals(object? obj) => obj is Range other && Start.Equals(other.Start) && End.Equals(other.End);

    /// <inheritdoc/>
    public override int GetHashCode()
    {
        var h1 = Start.GetHashCode();
        var h2 = End.GetHashCode();
        return (int)(((uint)(h1 << 5) | (uint)(h1 >> 27)) + (uint)h1 ^ (uint)h2);
    }

    /// <inheritdoc/>
    public override string ToString() => $"{Start}..{End}";

    /// <summary>Creates a range from index 0 up to the specified end index.</summary>
    /// <param name="end">The exclusive end index.</param>
    /// <returns>A range from the start of the collection to <paramref name="end"/>.</returns>
    internal static Range EndAt(Index end) => new(Index.Start, end);

    /// <summary>Creates a range from the specified start index to the end of the collection.</summary>
    /// <param name="start">The inclusive start index.</param>
    /// <returns>A range from <paramref name="start"/> to the end of the collection.</returns>
    internal static Range StartAt(Index start) => new(start, Index.End);

    /// <summary>Calculates the start offset and element count for a collection of the given length.</summary>
    /// <param name="length">The total number of elements in the collection.</param>
    /// <returns>A tuple of the zero-based start offset and the element count covered by this range.</returns>
    internal (int Offset, int Length) GetOffsetAndLength(int length)
    {
        var start = Start.GetOffset(length);
        var end = End.GetOffset(length);

        if ((uint)end > (uint)length || (uint)start > (uint)end)
        {
            throw new ArgumentOutOfRangeException(nameof(length));
        }

        return (start, end - start);
    }
}

#endif
