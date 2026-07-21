// Copyright (c) 2019-2026 Chris Pulman and contributors. All rights reserved.
// Chris Pulman and contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

#if REACTIVE_SHIM
using ModbusRx.Reactive.Message;
#else
using ModbusRx.Message;
#endif

#if REACTIVE_SHIM
namespace ModbusRx.Reactive.Device;
#else
namespace ModbusRx.Device;
#endif

/// <summary>Modbus Slave request event args containing information on the message.</summary>
public class ModbusSlaveRequestEventArgs : EventArgs
{
    /// <summary>Initializes a new instance of the Modbus Slave Request Event Args class.</summary>
    /// <param name="message">The message value.</param>
    internal ModbusSlaveRequestEventArgs(IModbusMessage message) => Message = message;

    /// <summary>Gets the message.</summary>
    public IModbusMessage Message { get; }
}
