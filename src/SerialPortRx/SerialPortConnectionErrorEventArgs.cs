// Copyright (c) 2019-2026 Chris Pulman and contributors. All rights reserved.
// Chris Pulman and contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

#if REACTIVE_SHIM
namespace IoT.DriverCore.Serial.Reactive;
#else
namespace IoT.DriverCore.Serial;
#endif

/// <summary>Provides an exception reported by an underlying serial connection.</summary>
internal sealed class SerialPortConnectionErrorEventArgs : EventArgs
{
    /// <summary>Initializes a new instance of the <see cref="SerialPortConnectionErrorEventArgs"/> class.</summary>
    /// <param name="exception">The reported exception.</param>
    internal SerialPortConnectionErrorEventArgs(Exception exception) => Exception = exception;

    /// <summary>Gets the reported exception.</summary>
    internal Exception Exception { get; }
}
