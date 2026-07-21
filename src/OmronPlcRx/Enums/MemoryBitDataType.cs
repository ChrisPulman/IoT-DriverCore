// Copyright (c) 2019-2026 Chris Pulman and contributors. All rights reserved.
// Chris Pulman and contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

#if REACTIVE_SHIM
namespace OmronPlcRx.Reactive.Enums;
#else
namespace OmronPlcRx.Enums;
#endif

/// <summary>Bit-addressable PLC memory areas.</summary>
public enum MemoryBitDataType
{
    /// <summary>No bit-addressable memory area.</summary>
    None = 0,

    /// <summary>Data memory area (DM).</summary>
    DataMemory = 0x2,

    /// <summary>Common I/O area (CIO).</summary>
    CommonIO = 0x30,

    /// <summary>Work area (W).</summary>
    Work = 0x31,

    /// <summary>Holding area (H).</summary>
    Holding = 0x32,

    /// <summary>Auxiliary area (A).</summary>
    Auxiliary = 0x33,
}
