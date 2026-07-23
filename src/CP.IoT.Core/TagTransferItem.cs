// Copyright (c) 2019-2026 Chris Pulman and contributors. All rights reserved.
// Chris Pulman and contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace IoT.DriverCore.Core;

/// <summary>Identifies a source request inside a planned transfer range.</summary>
public sealed class TagTransferItem
{
    /// <summary>Initializes a new instance of the <see cref="TagTransferItem"/> class.</summary>
    /// <param name="inputIndex">The zero-based position in the planner input.</param>
    /// <param name="request">The source request.</param>
    internal TagTransferItem(int inputIndex, TagTransferRequest request)
    {
        InputIndex = inputIndex;
        Request = request;
    }

    /// <summary>Gets the zero-based position in the planner input.</summary>
    public int InputIndex { get; }

    /// <summary>Gets the source request.</summary>
    public TagTransferRequest Request { get; }
}
