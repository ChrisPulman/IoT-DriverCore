// Copyright (c) 2019-2026 Chris Pulman and contributors. All rights reserved.
// Chris Pulman and contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.
#if NET462 || NET472 || NET481
using System;

namespace System;

/// <summary>Provides compiler support for index expressions on older target frameworks.</summary>
internal readonly struct Index : IEquatable<Index>
{
    /// <summary>Stores the encoded index value.</summary>
    private readonly int _value;

    /// <summary>Initializes a new instance of the <see cref="Index"/> struct.</summary>
    /// <param name="value">The zero-based index value.</param>
    /// <param name="fromEnd">A value indicating whether the index is relative to the end.</param>
    public Index(int value, bool fromEnd = false)
    {
        if (value < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(value), "The index value cannot be negative.");
        }

        _value = fromEnd ? ~value : value;
    }

    /// <summary>Gets the first index.</summary>
    internal static Index Start => new(0);

    /// <summary>Gets the end index.</summary>
    internal static Index End => new(0, fromEnd: true);

    /// <summary>Gets the index value.</summary>
    internal int Value => _value < 0 ? ~_value : _value;

    /// <summary>Gets a value indicating whether the index is relative to the end.</summary>
    internal bool IsFromEnd => _value < 0;

    /// <summary>Converts an integer into an index.</summary>
    /// <param name="value">The index value.</param>
    public static implicit operator Index(int value) => new(value);

    /// <inheritdoc />
    public bool Equals(Index other) => _value == other._value;

    /// <inheritdoc />
    public override bool Equals(object? obj) => obj is Index index && Equals(index);

    /// <inheritdoc />
    public override int GetHashCode() => _value;

    /// <summary>Calculates the offset from a collection length.</summary>
    /// <param name="length">The collection length.</param>
    /// <returns>The zero-based offset.</returns>
    internal int GetOffset(int length) => IsFromEnd ? length - Value : Value;
}
#endif
