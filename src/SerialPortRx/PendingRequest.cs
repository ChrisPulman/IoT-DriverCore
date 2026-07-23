// Copyright (c) 2019-2026 Chris Pulman and contributors. All rights reserved.
// Chris Pulman and contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

#if REACTIVE_SHIM
namespace IoT.DriverCore.Serial.Reactive;
#else
namespace IoT.DriverCore.Serial;
#endif

/// <summary>Represents a pending command request awaiting a serial response.</summary>
/// <param name="Command">The command text sent to the serial port.</param>
/// <param name="Apply">The action that applies the response payload.</param>
/// <param name="Completion">The completion source signaled when a response arrives.</param>
[DebuggerDisplay("Command = {Command}")]
public sealed record PendingRequest(string Command, Action<string> Apply, TaskCompletionSource<bool> Completion);
