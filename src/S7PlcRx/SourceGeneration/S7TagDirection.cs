// Copyright (c) 2019-2026 Chris Pulman and contributors. All rights reserved.
// Chris Pulman and contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

#if REACTIVE_SHIM
namespace IoT.DriverCore.S7PlcRx.Reactive.SourceGeneration;

#else
namespace IoT.DriverCore.S7PlcRx.SourceGeneration;

#endif

/// <summary>Defines PLC tag binding direction.</summary>
public enum S7TagDirection
{
    /// <summary>Reads and writes the PLC tag.</summary>
    ReadWrite,

    /// <summary>Reads the PLC tag only.</summary>
    ReadOnly,

    /// <summary>Writes the PLC tag only.</summary>
    WriteOnly,
}
