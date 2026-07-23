// Copyright (c) 2019-2026 Chris Pulman and contributors. All rights reserved.
// Chris Pulman and contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

#if REACTIVE_SHIM
namespace IoT.DriverCore.OmronPlcRx.Reactive;
#else
namespace IoT.DriverCore.OmronPlcRx;
#endif

/// <summary>Describes one completed simulator operation.</summary>
/// <param name="Sequence">Monotonic operation sequence number.</param>
/// <param name="Operation">Operation kind.</param>
/// <param name="TagName">Optional logical tag name.</param>
/// <param name="Value">Optional operation value.</param>
/// <param name="Succeeded">Whether the operation succeeded.</param>
public sealed record OmronSimulatorOperationRecord(
    long Sequence,
    OmronSimulatorOperation Operation,
    string? TagName,
    object? Value,
    bool Succeeded);
