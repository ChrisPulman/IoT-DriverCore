// Copyright (c) 2019-2026 Chris Pulman and contributors. All rights reserved.
// Chris Pulman and contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

#if NETSTANDARD2_0

namespace System;

/// <summary>Represents a type that can be used to index a collection from either end.</summary>
internal readonly struct Index : IEquatable<Index>
{
    /// <summary>Raw backing value; negative values indicate from-end using bitwise complement.</summary>
    private readonly int _value;

    /// <summary>Initializes a new instance of the <see cref="Index"/> class.</summary>
    /// <param name="value">The index value; must be non-negative.</param>
    public Index(int value)
    {
        if (value < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(value));
        }

        _value = value;
    }

    /// <summary>Initializes a new instance of the <see cref="Index"/> class.</summary>
    /// <param name="value">The index value; must be non-negative.</param>
    /// <param name="fromEnd">Whether this index counts from the end of the collection.</param>
    public Index(int value, bool fromEnd)
    {
        if (value < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(value));
        }

        _value = fromEnd ? ~value : value;
    }

    /// <summary>Gets an <see cref="Index"/> that points to the first element.</summary>
    internal static Index Start => new(0, false);

    /// <summary>Gets an <see cref="Index"/> that points beyond the last element.</summary>
    internal static Index End => new(0, true);

    /// <summary>Gets whether this index counts from the end of the collection.</summary>
    internal bool IsFromEnd => _value < 0;

    /// <summary>Gets the index value relative to the direction indicated by <see cref="IsFromEnd"/>.</summary>
    internal int Value => _value < 0 ? ~_value : _value;

    /// <summary>Converts an integer to an <see cref="Index"/> from the start.</summary>
    /// <param name="value">The zero-based start offset to convert.</param>
    public static implicit operator Index(int value) => new(value);

    /// <inheritdoc/>
    public bool Equals(Index other) => _value == other._value;

    /// <inheritdoc/>
    public override bool Equals(object? obj) => obj is Index other && _value == other._value;

    /// <inheritdoc/>
    public override int GetHashCode() => _value;

    /// <inheritdoc/>
    public override string ToString() => IsFromEnd ? $"^{Value}" : Value.ToString();

    /// <summary>Calculates the zero-based offset from the start for a collection of the given length.</summary>
    /// <param name="length">The total number of elements in the collection.</param>
    /// <returns>The zero-based start offset.</returns>
    internal int GetOffset(int length) => IsFromEnd ? length + _value + 1 : _value;
}

#endif
