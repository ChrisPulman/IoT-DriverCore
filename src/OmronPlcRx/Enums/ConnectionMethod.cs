// Copyright (c) 2019-2026 Chris Pulman and contributors. All rights reserved.
// Chris Pulman and contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

#if REACTIVE_SHIM
namespace OmronPlcRx.Reactive.Enums;
#else
namespace OmronPlcRx.Enums;
#endif

/// <summary>Transport protocol used for communication with the PLC.</summary>
public enum ConnectionMethod
{
    /// <summary>Transmission Control Protocol.</summary>
    TCP,

    /// <summary>User Datagram Protocol.</summary>
    UDP,

    /// <summary>Serial FINS protocol using Host Link FINS or Toolbus framing.</summary>
    Serial,
}
