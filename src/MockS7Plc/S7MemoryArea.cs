// Copyright (c) 2019-2026 Chris Pulman and contributors. All rights reserved.
// Chris Pulman and contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace IoT.DriverCore.S7PlcRx.Mock;

/// <summary>Identifies an S7ANY memory area.</summary>
public enum S7MemoryArea
{
    /// <summary>No memory area is selected.</summary>
    None,

    /// <summary>Counter memory.</summary>
    Counter = 0x1c,

    /// <summary>Timer memory.</summary>
    Timer = 0x1d,

    /// <summary>Process inputs.</summary>
    Input = 0x81,

    /// <summary>Process outputs.</summary>
    Output = 0x82,

    /// <summary>Marker memory.</summary>
    Memory = 0x83,

    /// <summary>Data-block memory.</summary>
    DataBlock = 0x84,
}
