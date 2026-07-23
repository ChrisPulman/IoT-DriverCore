// Copyright (c) 2019-2026 Chris Pulman and contributors. All rights reserved.
// Chris Pulman and contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

#if REACTIVE_SHIM
namespace IoT.DriverCore.ModbusRx.Reactive.Data;
#else
namespace IoT.DriverCore.ModbusRx.Data;
#endif

/// <summary>Boolean pattern types.</summary>
public enum BooleanPattern
{
    /// <summary>All true values.</summary>
    AllTrue,

    /// <summary>All false values.</summary>
    AllFalse,

    /// <summary>Alternating true/false pattern.</summary>
    Alternating,

    /// <summary>Random true/false values.</summary>
    Random,
}
