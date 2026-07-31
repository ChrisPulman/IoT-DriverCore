// Copyright (c) 2019-2026 Chris Pulman and contributors. All rights reserved.
// Chris Pulman and contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

#if REACTIVE_SHIM
namespace IoT.Driver.ModbusRx.Reactive;
#else
namespace IoT.Driver.ModbusRx;
#endif

/// <summary>Identifies the value representation used by a generated Modbus point.</summary>
public enum ModbusReactiveDataType
{
    /// <summary>Infer the value representation from the decorated property.</summary>
    Auto,

    /// <summary>Use a Boolean value.</summary>
    Bool,

    /// <summary>Use an unsigned 16-bit value.</summary>
    UInt16,

    /// <summary>Use a signed 16-bit value.</summary>
    Int16,

    /// <summary>Use an unsigned 32-bit value.</summary>
    UInt32,

    /// <summary>Use a signed 32-bit value.</summary>
    Int32,

    /// <summary>Use a single-precision floating-point value.</summary>
    Float32,

    /// <summary>Use a double-precision floating-point value.</summary>
    Float64,
}
