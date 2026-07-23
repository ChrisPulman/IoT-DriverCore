// Copyright (c) 2019-2026 Chris Pulman and contributors. All rights reserved.
// Chris Pulman and contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

#if REACTIVE_SHIM
namespace IoT.DriverCore.OmronPlcRx.Reactive.Results;
#else
namespace IoT.DriverCore.OmronPlcRx.Results;
#endif

/// <summary>Result of a Write Clock operation.</summary>
public readonly record struct WriteClockResult
{
    /// <summary>Gets or sets the bytes sent value.</summary>
    public int BytesSent { get; init; }

    /// <summary>Gets or sets the packets sent value.</summary>
    public int PacketsSent { get; init; }

    /// <summary>Gets or sets the bytes received value.</summary>
    public int BytesReceived { get; init; }

    /// <summary>Gets or sets the packets received value.</summary>
    public int PacketsReceived { get; init; }

    /// <summary>Gets or sets the duration value.</summary>
    public double Duration { get; init; }
}
