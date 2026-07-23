// Copyright (c) 2019-2026 Chris Pulman and contributors. All rights reserved.
// Chris Pulman and contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using ReactiveTwinCatRx = IoT.DriverCore.TwinCATRx.Reactive;

namespace IoT.DriverCore.TwinCATRx.Tests.Rx;

/// <summary>Generated System.Reactive PLC connection test fixture.</summary>
[ReactiveTwinCatRx.TwinCatPlcConnection("6.5.4.3.2.1", 851, SettingsId = "ReactiveGeneratedSettings")]
internal sealed partial class GeneratedReactivePlcConnection
{
    /// <summary>Gets the Reactive direct notification value.</summary>
    [ReactiveTwinCatRx.DirectNotification(".ReactiveDirect", CycleTime = 75, CanWrite = true)]
    internal int ReactiveDirectValue { get; private set; }
}
