// Copyright (c) 2019-2026 Chris Pulman and contributors. All rights reserved.
// Chris Pulman and contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

#if REACTIVE_SHIM
namespace IoT.Driver.Serial.Reactive.SourceGeneration;
#else
namespace IoT.Driver.Serial.SourceGeneration;
#endif

/// <summary>Selects the serial stream that drives a generated reactive property.</summary>
public enum SerialPortReactiveSource
{
    /// <summary>The complete line stream from <c>ISerialPortRx.Lines</c>.</summary>
    Lines = 0,

    /// <summary>The character stream from <c>ISerialPortRx.DataReceived</c>.</summary>
    DataReceived = 1,

    /// <summary>The raw byte stream from <c>ISerialPortRx.DataReceivedBytes</c>.</summary>
    DataReceivedBytes = 2,

    /// <summary>The byte stream emitted by <c>ReadAsync</c> through <c>IPortRx.BytesReceived</c>.</summary>
    BytesReceived = 3,

    /// <summary>The open-state stream from <c>ISerialPortRx.IsOpenObservable</c>.</summary>
    IsOpen = 4,
}
