// Copyright (c) 2019-2026 Chris Pulman and contributors. All rights reserved.
// Chris Pulman and contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

#if REACTIVE_SHIM
namespace IoT.DriverCore.ModbusRx.Reactive.Data;
#else
namespace IoT.DriverCore.ModbusRx.Data;
#endif

/// <summary>Types of data supported by the Modbus protocol.</summary>
public enum ModbusDataType
{
    /// <summary>Read/write register.</summary>
    HoldingRegister,

    /// <summary>Readonly register.</summary>
    InputRegister,

    /// <summary>Read/write discrete.</summary>
    Coil,

    /// <summary>Readonly discrete.</summary>
    Input,
}
