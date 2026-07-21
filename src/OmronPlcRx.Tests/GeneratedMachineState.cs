// Copyright (c) 2019-2026 Chris Pulman and contributors. All rights reserved.
// Chris Pulman and contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using OmronPlcRx.Core.Types;

namespace OmronPlcRx.Tests;

/// <summary>Generated PLC tag fixture used by source-generator tests.</summary>
public sealed partial class GeneratedMachineState
{
    /// <summary>Stores the generated tank level tag backing field.</summary>
    [PlcTag("D100", Writable = true)]
    private short _tankLevel;

    /// <summary>Stores the generated pump running tag backing field.</summary>
    [PlcTag("D100.0", TagName = "PumpRun")]
    private bool _pumpRunning;

    /// <summary>Stores the generated line name tag backing field.</summary>
    [PlcTag("D600[20]")]
    private string? _lineName;

    /// <summary>Stores the generated BCD temperature tag backing field.</summary>
    [PlcTag("D700")]
    private Bcd16 _bcdTemp;
}
