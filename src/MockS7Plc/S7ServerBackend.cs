// Copyright (c) 2019-2026 Chris Pulman and contributors. All rights reserved.
// Chris Pulman and contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace IoT.DriverCore.S7PlcRx.Mock;

/// <summary>Selects the implementation used by <see cref="MockServer"/>.</summary>
public enum S7ServerBackend
{
    /// <summary>Use the deterministic, fully managed ISO-on-TCP/S7 server.</summary>
    Managed,

    /// <summary>Use the bundled native Snap7 server.</summary>
    Snap7,
}
