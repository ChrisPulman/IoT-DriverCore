// Copyright (c) 2019-2026 Chris Pulman and contributors. All rights reserved.
// Chris Pulman and contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using ReactiveBcd16 = global::IoT.DriverCore.OmronPlcRx.Reactive.Core.Types.Bcd16;

namespace IoT.DriverCore.OmronPlcRx.Reactive.Tests;

/// <summary>Generated reactive PLC tag fixture used by shim tests.</summary>
public sealed partial class ReactiveGeneratedMachineState
{
    /// <summary>Stores the generated tank level tag backing field.</summary>
    [PlcTag("D100", Writable = true)]
    private short _tankLevel;

    /// <summary>Stores the generated BCD temperature tag backing field.</summary>
    [PlcTag("D700")]
    private ReactiveBcd16 _bcdTemp;
}
