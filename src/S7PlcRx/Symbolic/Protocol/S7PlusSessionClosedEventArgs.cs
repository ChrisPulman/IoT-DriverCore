// Copyright (c) 2019-2026 Chris Pulman and contributors. All rights reserved.
// Chris Pulman and contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.
#if REACTIVE_SHIM
namespace IoT.Driver.S7PlcRx.Reactive.Symbolic.Protocol;
#else
namespace IoT.Driver.S7PlcRx.Symbolic.Protocol;
#endif
/// <summary>Contains the reason a symbolic session stopped receiving messages.</summary>
internal sealed class S7PlusSessionClosedEventArgs : EventArgs
{
    /// <summary>Initializes a new instance of the <see cref = "S7PlusSessionClosedEventArgs"/> class.</summary>
    /// <param name = "exception">The exception.</param>
    internal S7PlusSessionClosedEventArgs(Exception exception) => Exception = exception;

    /// <summary>Gets the termination reason.</summary>
    internal Exception Exception { get; }
}
