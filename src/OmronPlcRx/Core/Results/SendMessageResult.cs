// Copyright (c) 2019-2026 Chris Pulman and contributors. All rights reserved.
// Chris Pulman and contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

#if REACTIVE_SHIM
namespace IoT.DriverCore.OmronPlcRx.Reactive.Core.Results;
#else
namespace IoT.DriverCore.OmronPlcRx.Core.Results;
#endif

/// <summary>Represents the s en dm es sa ge re su lt type.</summary>
internal readonly record struct SendMessageResult
{
    /// <summary>Gets or sets the bytes value.</summary>
    internal int Bytes { get; init; }

    /// <summary>Gets or sets the packets value.</summary>
    internal int Packets { get; init; }
}
