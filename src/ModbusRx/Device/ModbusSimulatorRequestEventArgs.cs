// Copyright (c) 2019-2026 Chris Pulman and contributors. All rights reserved.
// Chris Pulman and contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

#if REACTIVE_SHIM
using IoT.DriverCore.ModbusRx.Reactive.Message;
#else
using IoT.DriverCore.ModbusRx.Message;
#endif

#if REACTIVE_SHIM
namespace IoT.DriverCore.ModbusRx.Reactive.Device;
#else
namespace IoT.DriverCore.ModbusRx.Device;
#endif

/// <summary>Provides details about a request processed by a <see cref="ModbusSimulator"/>.</summary>
public sealed class ModbusSimulatorRequestEventArgs : EventArgs
{
    /// <summary>Initializes a new instance of the <see cref="ModbusSimulatorRequestEventArgs"/> class.</summary>
    /// <param name="request">The request received by the simulator.</param>
    /// <param name="response">The response produced by the simulator, if any.</param>
    /// <param name="fault">The scripted fault applied to the request, if any.</param>
    /// <param name="timestamp">The time at which the request was processed.</param>
    internal ModbusSimulatorRequestEventArgs(
        IModbusMessage request,
        IModbusMessage? response,
        ModbusSimulatorFaultKind? fault,
        DateTimeOffset timestamp)
    {
        Request = request;
        Response = response;
        Fault = fault;
        Timestamp = timestamp;
    }

    /// <summary>Gets the request received by the simulator.</summary>
    public IModbusMessage Request { get; }

    /// <summary>Gets the response produced by the simulator, if any.</summary>
    public IModbusMessage? Response { get; }

    /// <summary>Gets the scripted fault applied to the request, if any.</summary>
    public ModbusSimulatorFaultKind? Fault { get; }

    /// <summary>Gets the time at which the request was processed.</summary>
    public DateTimeOffset Timestamp { get; }
}
