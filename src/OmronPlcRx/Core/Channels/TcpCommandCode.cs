// Copyright (c) 2019-2026 Chris Pulman and contributors. All rights reserved.
// Chris Pulman and contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

#if REACTIVE_SHIM
namespace IoT.DriverCore.OmronPlcRx.Reactive.Core.Channels;
#else
namespace IoT.DriverCore.OmronPlcRx.Core.Channels;
#endif

/// <summary>Represents the TCP command code enumeration.</summary>
internal enum TcpCommandCode
{
    /// <summary>Represents the node address to PLC enum value.</summary>
    NodeAddressToPLC = 0,

    /// <summary>Represents the node address from PLC enum value.</summary>
    NodeAddressFromPLC = 1,

    /// <summary>Represents the FINS frame enum value.</summary>
    FINSFrame = 2,
}
