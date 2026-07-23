// Copyright (c) 2019-2026 Chris Pulman and contributors. All rights reserved.
// Chris Pulman and contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

#if REACTIVE_SHIM
namespace IoT.DriverCore.ModbusRx.Reactive.LogicalTags;
#else
namespace IoT.DriverCore.ModbusRx.LogicalTags;
#endif

/// <summary>Identifies a Modbus data area.</summary>
public enum ModbusDataArea
{
    /// <summary>Read/write coil bits.</summary>
    Coil,

    /// <summary>Read-only discrete input bits.</summary>
    DiscreteInput,

    /// <summary>Read/write holding registers.</summary>
    HoldingRegister,

    /// <summary>Read-only input registers.</summary>
    InputRegister,
}
