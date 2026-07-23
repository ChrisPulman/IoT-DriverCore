// Copyright (c) 2019-2026 Chris Pulman and contributors. All rights reserved.
// Chris Pulman and contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

#if REACTIVE_SHIM
namespace IoT.DriverCore.ModbusRx.Reactive.Data;
#else
namespace IoT.DriverCore.ModbusRx.Data;
#endif

/// <summary>Types of simulation patterns available.</summary>
public enum SimulationType
{
    /// <summary>Random values.</summary>
    Random,

    /// <summary>Counting up pattern.</summary>
    CountingUp,

    /// <summary>Counting down pattern.</summary>
    CountingDown,

    /// <summary>Sine wave pattern.</summary>
    SineWave,

    /// <summary>Square wave pattern.</summary>
    SquareWave,
}
