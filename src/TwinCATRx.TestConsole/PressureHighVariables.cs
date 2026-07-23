// Copyright (c) 2019-2026 Chris Pulman and contributors. All rights reserved.
// Chris Pulman and contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace IoT.DriverCore.TwinCATRx.TestConsole;

/// <summary>Shared live PLC variable paths for the pressure high PV examples.</summary>
internal static class PressureHighVariables
{
    /// <summary>Stores the live PLC ADS address.</summary>
    internal const string AdsAddress = "10.1.180.147.1.1";

    /// <summary>Stores the root TwinCAT structure variable.</summary>
    internal const string RootVariable = "GlobalVariables.Rig";

    /// <summary>Stores the relative observed pressure process value variable.</summary>
    internal const string RelativeObservedVariable = "Casing.Pressure.High.PV.Value";

    /// <summary>Stores the relative writable pressure simulation variable.</summary>
    internal const string RelativeSimulationVariable = "Casing.Pressure.High.PV.SimulationVal";

    /// <summary>Stores the live PLC ADS port.</summary>
    internal const int AdsPort = 851;

    /// <summary>Gets the full observed pressure process value variable.</summary>
    internal static string FullObservedVariable => $"{RootVariable}.{RelativeObservedVariable}";

    /// <summary>Gets the full writable pressure simulation variable.</summary>
    internal static string FullSimulationVariable => $"{RootVariable}.{RelativeSimulationVariable}";
}
