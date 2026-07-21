// Copyright (c) 2019-2026 Chris Pulman and contributors. All rights reserved.
// Chris Pulman and contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.
#if NET462 || NET472 || NET481
using System;

namespace System;

/// <summary>Provides compiler support for range expressions on older target frameworks.</summary>
internal readonly struct Range : IEquatable<Range>
{
    /// <summary>Initializes a new instance of the <see cref="Range"/> struct.</summary>
    /// <param name="start">The inclusive start index.</param>
    /// <param name="end">The exclusive end index.</param>
    public Range(Index start, Index end)
    {
        Start = start;
        End = end;
    }

    /// <summary>Gets the inclusive start index.</summary>
    internal Index Start { get; }

    /// <summary>Gets the exclusive end index.</summary>
    internal Index End { get; }

    /// <inheritdoc />
    public bool Equals(Range other) => Start.Equals(other.Start) && End.Equals(other.End);

    /// <inheritdoc />
    public override bool Equals(object? obj) => obj is Range range && Equals(range);

    /// <inheritdoc />
    public override int GetHashCode() => (Start.GetHashCode() * 397) ^ End.GetHashCode();

    /// <summary>Calculates an offset and length from a collection length.</summary>
    /// <param name="length">The collection length.</param>
    /// <returns>The zero-based offset and length.</returns>
    internal (int Offset, int Length) GetOffsetAndLength(int length)
    {
        var start = Start.GetOffset(length);
        var end = End.GetOffset(length);
        if ((uint)end > (uint)length || (uint)start > (uint)end)
        {
            throw new ArgumentOutOfRangeException(nameof(length), "The range is outside the collection length.");
        }

        return (start, end - start);
    }
}
#endif
