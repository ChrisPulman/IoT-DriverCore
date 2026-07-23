// Copyright (c) 2019-2026 Chris Pulman and contributors. All rights reserved.
// Chris Pulman and contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using IoT.DriverCore.TwinCATRx;

namespace IoT.DriverCore.TwinCATRx.TestConsole;

/// <summary>Source-generated pressure high PV stream bindings.</summary>
[TwinCatPlcConnection(
    PressureHighVariables.AdsAddress,
    PressureHighVariables.AdsPort,
    SettingsId = "PressureHighSourceGeneratedExample")]
internal sealed partial class PressureHighSourceGeneratedStreams
{
    /// <summary>Gets the observed pressure value from the structured rig notification.</summary>
    [StructuredNotification(
        PressureHighVariables.RootVariable,
        PressureHighVariables.RelativeObservedVariable,
        CanWrite = false)]
    internal float PressureHighValue { get; private set; }

    /// <summary>Gets the most recent simulation value queued for write.</summary>
    [WriteOnly("GlobalVariables.Rig.Casing.Pressure.High.PV.SimulationVal")]
    internal float PressureHighSimulationValue { get; private set; }

    /// <summary>Gets the generated stream type marker.</summary>
    internal string GeneratedTypeMarker => nameof(PressureHighSourceGeneratedStreams);
}
