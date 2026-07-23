// Copyright (c) 2019-2026 Chris Pulman and contributors. All rights reserved.
// Chris Pulman and contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace IoT.DriverCore.OmronPlcRx.Tests;

/// <summary>Property-decorated fixture for the generated PLC binding surface.</summary>
[PlcTagBinding]
public sealed partial class GeneratedPropertyMachineState
{
    /// <summary>Gets the current conveyor speed.</summary>
    [PlcTag("D300", Writable = true)]
    public short ConveyorSpeed { get; private set; }

    /// <summary>Gets the current recipe name.</summary>
    [PlcTag("D400[20]")]
    public string RecipeName { get; private set; } = string.Empty;
}
