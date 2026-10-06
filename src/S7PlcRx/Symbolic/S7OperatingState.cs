// Copyright (c) 2019-2026 Chris Pulman and contributors. All rights reserved.
// Chris Pulman and contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.
#if REACTIVE_SHIM
namespace IoT.Driver.S7PlcRx.Reactive.Symbolic;
#else
namespace IoT.Driver.S7PlcRx.Symbolic;
#endif

/// <summary>PLC execution unit states accepted by the state request attribute.</summary>
public enum S7OperatingState
{
    /// <summary>No state request.</summary>
    None = 0,

    /// <summary>Stops program execution.</summary>
    Stop = 1,

    /// <summary>Runs the program.</summary>
    Run = 3,
}
