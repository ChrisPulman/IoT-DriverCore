// Copyright (c) 2019-2026 Chris Pulman and contributors. All rights reserved.
// Chris Pulman and contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.
#if REACTIVE_SHIM
namespace IoT.Driver.S7PlcRx.Reactive.Symbolic.Protocol;
#else
namespace IoT.Driver.S7PlcRx.Symbolic.Protocol;
#endif
/// <summary>Contains an unsolicited symbolic protocol message.</summary>
internal sealed class S7PlusMessageEventArgs : EventArgs
{
    /// <summary>Initializes a new instance of the <see cref = "S7PlusMessageEventArgs"/> class.</summary>
    /// <param name = "message">The message.</param>
    internal S7PlusMessageEventArgs(PlusMessage message) => Message = message;

    /// <summary>Gets the received protocol message.</summary>
    internal PlusMessage Message { get; }
}
