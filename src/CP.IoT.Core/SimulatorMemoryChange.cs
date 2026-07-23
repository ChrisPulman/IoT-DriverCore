// Copyright (c) 2019-2026 Chris Pulman and contributors. All rights reserved.
// Chris Pulman and contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Collections.ObjectModel;

namespace IoT.DriverCore.Core;

/// <summary>Represents one immutable, ordered write to a <see cref="SimulatorMemoryImage"/>.</summary>
public sealed class SimulatorMemoryChange
{
    /// <summary>Initializes a new instance of the <see cref="SimulatorMemoryChange"/> class.</summary>
    /// <param name="sequence">The change sequence.</param>
    /// <param name="timestampUtc">The change timestamp.</param>
    /// <param name="address">The changed address.</param>
    /// <param name="previousBytes">The previous bytes.</param>
    /// <param name="currentBytes">The current bytes.</param>
    internal SimulatorMemoryChange(
        long sequence,
        DateTimeOffset timestampUtc,
        TagTransportAddress address,
        byte[] previousBytes,
        byte[] currentBytes)
    {
        Sequence = sequence;
        TimestampUtc = timestampUtc.ToUniversalTime();
        Address = address;
        PreviousBytes = new ReadOnlyCollection<byte>((byte[])previousBytes.Clone());
        CurrentBytes = new ReadOnlyCollection<byte>((byte[])currentBytes.Clone());
    }

    /// <summary>Gets the monotonically increasing sequence number within the memory image.</summary>
    public long Sequence { get; }

    /// <summary>Gets the UTC instant supplied by the simulator clock.</summary>
    public DateTimeOffset TimestampUtc { get; }

    /// <summary>Gets the exact byte range written by the operation.</summary>
    public TagTransportAddress Address { get; }

    /// <summary>Gets a snapshot of the bytes present before the write.</summary>
    public IReadOnlyList<byte> PreviousBytes { get; }

    /// <summary>Gets a snapshot of the bytes present after the write.</summary>
    public IReadOnlyList<byte> CurrentBytes { get; }
}
