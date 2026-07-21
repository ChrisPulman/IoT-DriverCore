// Copyright (c) 2019-2026 Chris Pulman and contributors. All rights reserved.
// Chris Pulman and contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System;

#if REACTIVE_SHIM
namespace OmronPlcRx.Reactive.Core.Results;
#else
namespace OmronPlcRx.Core.Results;
#endif

/// <summary>Represents the r ec ei ve me ss ag er es ul t type.</summary>
internal readonly record struct ReceiveMessageResult
{
    /// <summary>Gets or sets the message value.</summary>
    internal Memory<byte> Message { get; init; }

    /// <summary>Gets or sets the bytes value.</summary>
    internal int Bytes { get; init; }

    /// <summary>Gets or sets the packets value.</summary>
    internal int Packets { get; init; }
}
