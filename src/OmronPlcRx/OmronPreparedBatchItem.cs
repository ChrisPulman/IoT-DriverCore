// Copyright (c) 2019-2026 Chris Pulman and contributors. All rights reserved.
// Chris Pulman and contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using IoT.DriverCore.Core;

#if REACTIVE_SHIM
namespace IoT.DriverCore.OmronPlcRx.Reactive;
#else
namespace IoT.DriverCore.OmronPlcRx;
#endif

/// <summary>Contains parsed, encoded state for one logical batch item.</summary>
/// <param name="item">Source logical batch item.</param>
/// <param name="request">Planner request.</param>
/// <param name="area">Normalized address-area alias.</param>
/// <param name="address">Word address.</param>
/// <param name="wordCount">Number of words occupied by the item.</param>
/// <param name="stringLength">Configured fixed string length.</param>
/// <param name="words">Encoded write words.</param>
internal sealed class OmronPreparedBatchItem(
    OmronLogicalBatchItem item,
    TagTransferRequest request,
    string area,
    ushort address,
    int wordCount,
    int stringLength,
    short[]? words)
{
    /// <summary>Gets the source logical batch item.</summary>
    internal OmronLogicalBatchItem Item { get; } = item;

    /// <summary>Gets the planner request.</summary>
    internal TagTransferRequest Request { get; } = request;

    /// <summary>Gets the normalized address-area alias.</summary>
    internal string Area { get; } = area;

    /// <summary>Gets the word address.</summary>
    internal ushort Address { get; } = address;

    /// <summary>Gets the number of occupied words.</summary>
    internal int WordCount { get; } = wordCount;

    /// <summary>Gets the configured fixed string length.</summary>
    internal int StringLength { get; } = stringLength;

    /// <summary>Gets the encoded write words.</summary>
    internal short[]? Words { get; } = words;
}
