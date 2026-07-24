// Copyright (c) 2019-2026 Chris Pulman and contributors. All rights reserved.
// Chris Pulman and contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

#if REACTIVE_SHIM
namespace IoT.DriverCore.Serial.Reactive;
#else
namespace IoT.DriverCore.Serial;
#endif

/// <summary>Provides the event boundary of a system serial port.</summary>
internal interface ISerialPortRuntime : IDisposable
{
    /// <summary>Occurs when data becomes available.</summary>
    event EventHandler? DataReceived;

    /// <summary>Occurs when the runtime reports an error.</summary>
    event EventHandler<SerialPortConnectionErrorEventArgs>? ErrorReceived;
}
