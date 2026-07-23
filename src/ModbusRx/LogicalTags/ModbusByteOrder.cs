// Copyright (c) 2019-2026 Chris Pulman and contributors. All rights reserved.
// Chris Pulman and contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

#if REACTIVE_SHIM
namespace IoT.DriverCore.ModbusRx.Reactive.LogicalTags;
#else
namespace IoT.DriverCore.ModbusRx.LogicalTags;
#endif

/// <summary>Describes byte and word ordering for register values.</summary>
public enum ModbusByteOrder
{
    /// <summary>Bytes and words are stored most-significant first (ABCD).</summary>
    BigEndian,

    /// <summary>Bytes and words are stored least-significant first (DCBA).</summary>
    LittleEndian,

    /// <summary>Big-endian bytes with least-significant word first (CDAB).</summary>
    BigEndianWordSwap,

    /// <summary>Little-endian bytes with most-significant word first (BADC).</summary>
    LittleEndianWordSwap,
}
