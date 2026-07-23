// Copyright (c) 2019-2026 Chris Pulman and contributors. All rights reserved.
// Chris Pulman and contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

#if REACTIVE_SHIM
namespace IoT.DriverCore.ModbusRx.Reactive.Device;
#else
namespace IoT.DriverCore.ModbusRx.Device;
#endif

/// <summary>Identifies a deterministic fault applied to the next simulator request.</summary>
public enum ModbusSimulatorFaultKind
{
    /// <summary>No fault is applied.</summary>
    None = 0,

    /// <summary>The request fails while it is written to the in-memory transport.</summary>
    IOException = 1,

    /// <summary>The response read fails with a timeout.</summary>
    Timeout = 2,

    /// <summary>The device returns the Modbus slave-device-busy exception.</summary>
    SlaveDeviceBusy = 3,

    /// <summary>The device returns a response with a different transaction identifier.</summary>
    CorruptTransactionId = 4,
}
