// Copyright (c) 2019-2026 Chris Pulman and contributors. All rights reserved.
// Chris Pulman and contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

#if REACTIVE_SHIM
namespace IoT.Driver.ModbusRx.Reactive;
#else
namespace IoT.Driver.ModbusRx;
#endif

/// <summary>Identifies the Modbus master kind used by a generated reactive device.</summary>
public enum ModbusReactiveMasterKind
{
    /// <summary>Infer the master kind from the configured connection member.</summary>
    Auto,

    /// <summary>Use an IP Modbus master.</summary>
    Ip,

    /// <summary>Use a serial Modbus master.</summary>
    Serial,
}
