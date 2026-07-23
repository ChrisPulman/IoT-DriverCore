// Copyright (c) 2019-2026 Chris Pulman and contributors. All rights reserved.
// Chris Pulman and contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

#if REACTIVE_SHIM
namespace IoT.DriverCore.OmronPlcRx.Reactive;
#else
namespace IoT.DriverCore.OmronPlcRx;
#endif

/// <summary>Specifies the serial protocol used to carry FINS messages.</summary>
public enum OmronSerialProtocol
{
    /// <summary>Host Link FINS using ASCII FA frames.</summary>
    HostLinkFins,

    /// <summary>Omron Toolbus using binary 0xAB frames carrying binary FINS messages.</summary>
    Toolbus,
}
