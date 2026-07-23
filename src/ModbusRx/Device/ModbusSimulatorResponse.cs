// Copyright (c) 2019-2026 Chris Pulman and contributors. All rights reserved.
// Chris Pulman and contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

#if REACTIVE_SHIM
namespace IoT.DriverCore.ModbusRx.Reactive.Device;
#else
namespace IoT.DriverCore.ModbusRx.Device;
#endif

/// <summary>Contains a response or read failure produced by the simulator.</summary>
internal sealed class ModbusSimulatorResponse
{
    /// <summary>Initializes a new instance of the <see cref="ModbusSimulatorResponse"/> class.</summary>
    /// <param name="frame">The response frame.</param>
    /// <param name="readException">The exception raised while reading the response.</param>
    /// <param name="delay">The delay before the response is available.</param>
    internal ModbusSimulatorResponse(byte[]? frame, Exception? readException, TimeSpan delay)
    {
        Frame = frame;
        ReadException = readException;
        Delay = delay;
    }

    /// <summary>Gets the complete response frame.</summary>
    internal byte[]? Frame { get; }

    /// <summary>Gets the exception raised while reading the response.</summary>
    internal Exception? ReadException { get; }

    /// <summary>Gets the delay before the response is available.</summary>
    internal TimeSpan Delay { get; }
}
