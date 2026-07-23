// Copyright (c) 2019-2026 Chris Pulman and contributors. All rights reserved.
// Chris Pulman and contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

#if REACTIVE_SHIM
namespace IoT.DriverCore.OmronPlcRx.Reactive;
#else
namespace IoT.DriverCore.OmronPlcRx;
#endif

/// <summary>Identifies an operation that can be faulted by <see cref="OmronPlcSimulator"/>.</summary>
public enum OmronSimulatorOperation
{
    /// <summary>Opening or reopening the simulated connection.</summary>
    Connect,

    /// <summary>Reading a registered tag.</summary>
    Read,

    /// <summary>Writing a registered tag.</summary>
    Write,

    /// <summary>Reading the simulated real-time clock.</summary>
    ReadClock,

    /// <summary>Writing the simulated real-time clock.</summary>
    WriteClock,

    /// <summary>Reading simulated cycle-time statistics.</summary>
    ReadCycleTime,
}
