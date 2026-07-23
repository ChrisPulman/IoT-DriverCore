// Copyright (c) 2019-2026 Chris Pulman and contributors. All rights reserved.
// Chris Pulman and contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

#if REACTIVE_SHIM
using IoT.DriverCore.S7PlcRx.Reactive.Enums;
#else
using IoT.DriverCore.S7PlcRx.Enums;
#endif

#if REACTIVE_SHIM
namespace IoT.DriverCore.S7PlcRx.Reactive.Core;
#else
namespace IoT.DriverCore.S7PlcRx.Core;
#endif

/// <summary>Represents a PLC request and its optional tag.</summary>
/// <param name="request">The type of PLC request to perform.</param>
/// <param name="tag">The tag associated with the request, or null if the request does not require a tag.</param>
internal class PLCRequest(PlcRequestType request, Tag? tag)
{
    /// <summary>Gets the PLC request associated with this instance.</summary>
    internal PlcRequestType Request { get; } = request;

    /// <summary>Gets the tag associated with this instance, if any.</summary>
    internal Tag? Tag { get; } = tag;
}
