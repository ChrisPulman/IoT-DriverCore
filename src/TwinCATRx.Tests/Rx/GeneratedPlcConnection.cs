// Copyright (c) 2019-2026 Chris Pulman and contributors. All rights reserved.
// Chris Pulman and contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using IoT.DriverCore.TwinCATRx;

namespace IoT.DriverCore.TwinCATRx.Tests.Rx;

/// <summary>Generated PLC connection test fixture.</summary>
[TwinCatPlcConnection("1.2.3.4.5.6", 851, SettingsId = "GeneratedSettings")]
internal sealed partial class GeneratedPlcConnection
{
    /// <summary>Gets the direct notification value.</summary>
    [DirectNotification(".DirectValue", CycleTime = 50, CanWrite = true)]
    internal int DirectValue { get; private set; }

    /// <summary>Gets the structured notification value.</summary>
    [StructuredNotification(".Struct", "Nested.Value", CycleTime = 200, CanWrite = false)]
    internal int StructuredValue { get; private set; }

    /// <summary>Gets the writable structured notification value.</summary>
    [StructuredNotification(".Struct", "Nested.Writable", CycleTime = 200, CanWrite = true)]
    internal int StructuredWritableValue { get; private set; }

    /// <summary>Gets the write-only value.</summary>
    [WriteOnly(".WriteOnly", Id = "write-only")]
    internal int WriteOnlyValue { get; private set; }

    /// <summary>Gets the structure-backed write-only value.</summary>
    [WriteOnly(".Struct.Nested.WriteOnly")]
    internal int StructuredWriteOnlyValue { get; private set; }
}
