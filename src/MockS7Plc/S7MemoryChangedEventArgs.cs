// Copyright (c) 2019-2026 Chris Pulman and contributors. All rights reserved.
// Chris Pulman and contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace IoT.DriverCore.S7PlcRx.Mock;

/// <summary>Describes a deterministic simulator memory write.</summary>
/// <param name="area">The S7 memory area.</param>
/// <param name="dbNumber">The data-block number, or zero for non-DB areas.</param>
/// <param name="offset">The byte offset.</param>
/// <param name="data">A snapshot of the written bytes.</param>
public sealed class S7MemoryChangedEventArgs(
    S7MemoryArea area,
    ushort dbNumber,
    int offset,
    byte[] data) : EventArgs
{
    /// <summary>Gets the S7 memory area.</summary>
    public S7MemoryArea Area { get; } = area;

    /// <summary>Gets the data-block number.</summary>
    public ushort DbNumber { get; } = dbNumber;

    /// <summary>Gets the byte offset.</summary>
    public int Offset { get; } = offset;

    /// <summary>Gets a snapshot of the written bytes.</summary>
    public byte[] Data { get; } = data;
}
