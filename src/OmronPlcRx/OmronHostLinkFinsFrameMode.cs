// Copyright (c) 2019-2026 Chris Pulman and contributors. All rights reserved.
// Chris Pulman and contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

#if REACTIVE_SHIM
namespace IoT.DriverCore.OmronPlcRx.Reactive;
#else
namespace IoT.DriverCore.OmronPlcRx;
#endif

/// <summary>Specifies the Host Link FINS frame layout used over serial communications.</summary>
public enum OmronHostLinkFinsFrameMode
{
    /// <summary>Directly connected host-computer-to-CPU format using ICF/DA2/SA2/SID fields.</summary>
    Direct,

    /// <summary>Network-capable format using the complete FINS header.</summary>
    Network,
}
