// Copyright (c) 2019-2026 Chris Pulman and contributors. All rights reserved.
// Chris Pulman and contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

#if REACTIVE_SHIM
namespace IoT.DriverCore.OmronPlcRx.Reactive.Enums;
#else
namespace IoT.DriverCore.OmronPlcRx.Enums;
#endif

/// <summary>Word-addressable PLC memory areas.</summary>
public enum MemoryWordDataType
{
    /// <summary>No word-addressable memory area.</summary>
    None = 0,

    /// <summary>Data memory area (DM).</summary>
    DataMemory = 0x82,

    /// <summary>Common I/O area (CIO).</summary>
    CommonIO = 0xB0,

    /// <summary>Work area (W).</summary>
    Work = 0xB1,

    /// <summary>Holding area (H).</summary>
    Holding = 0xB2,

    /// <summary>Auxiliary area (A).</summary>
    Auxiliary = 0xB3,
}
