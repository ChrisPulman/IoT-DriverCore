// Copyright (c) 2019-2026 Chris Pulman and contributors. All rights reserved.
// Chris Pulman and contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace IoT.DriverCore.S7PlcRx.Mock;

/// <summary>Identifies a deterministic server fault behavior.</summary>
public enum S7ServerFaultKind
{
    /// <summary>Delays the matching operation before processing it.</summary>
    Delay,

    /// <summary>Closes the client connection before returning a response.</summary>
    Disconnect,

    /// <summary>Returns the configured S7 item return code.</summary>
    ReturnCode,

    /// <summary>Returns an invalid TPKT frame.</summary>
    MalformedFrame,
}
