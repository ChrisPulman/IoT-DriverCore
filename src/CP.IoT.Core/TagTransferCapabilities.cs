// Copyright (c) 2019-2026 Chris Pulman and contributors. All rights reserved.
// Chris Pulman and contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace IoT.DriverCore.Core;

/// <summary>Defines adapter capabilities that constrain a coalesced transport range.</summary>
public sealed class TagTransferCapabilities
{
    /// <summary>Initializes a new instance of the <see cref="TagTransferCapabilities"/> class with no practical item-count limit.</summary>
    /// <param name="maximumRangeLength">The largest number of addressable units one transfer may contain.</param>
    public TagTransferCapabilities(long maximumRangeLength)
        : this(maximumRangeLength, int.MaxValue)
    {
    }

    /// <summary>Initializes a new instance of the <see cref="TagTransferCapabilities"/> class.</summary>
    /// <param name="maximumRangeLength">The largest number of addressable units one transfer may contain.</param>
    /// <param name="maximumItemsPerRange">The largest number of logical tags represented by one transfer.</param>
    public TagTransferCapabilities(long maximumRangeLength, int maximumItemsPerRange)
    {
        if (maximumRangeLength <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(maximumRangeLength));
        }

        if (maximumItemsPerRange <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(maximumItemsPerRange));
        }

        MaximumRangeLength = maximumRangeLength;
        MaximumItemsPerRange = maximumItemsPerRange;
    }

    /// <summary>Gets the largest number of addressable units one transfer may contain.</summary>
    public long MaximumRangeLength { get; }

    /// <summary>Gets the largest number of logical tags represented by one transfer.</summary>
    public int MaximumItemsPerRange { get; }
}
