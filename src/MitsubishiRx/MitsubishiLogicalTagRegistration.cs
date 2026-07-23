// Copyright (c) 2019-2026 Chris Pulman and contributors. All rights reserved.
// Chris Pulman and contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using IoT.DriverCore.Core;

#if REACTIVE_SHIM

namespace IoT.DriverCore.MitsubishiRx.Reactive;

#else

namespace IoT.DriverCore.MitsubishiRx;

#endif

/// <summary>Describes a logical Mitsubishi tag to create and register.</summary>
/// <param name="Name">The logical tag name.</param>
/// <param name="Address">The Mitsubishi device address.</param>
/// <param name="DataType">The declared data type.</param>
/// <param name="GroupName">The optional primary group.</param>
/// <param name="Description">The optional description.</param>
/// <param name="Metadata">The driver-specific metadata.</param>
/// <param name="AccessMode">The tag access mode.</param>
/// <param name="ScanInterval">The optional scan interval.</param>
public sealed record MitsubishiLogicalTagRegistration(
    string Name,
    string Address,
    string DataType,
    string? GroupName,
    string? Description,
    IReadOnlyDictionary<string, string>? Metadata,
    LogicalTagAccessMode AccessMode,
    TimeSpan? ScanInterval)
{
    /// <summary>Creates the common immutable tag model.</summary>
    /// <returns>The common logical tag.</returns>
    public LogicalTag ToLogicalTag()
    {
        var groupName = GroupName;
        var description = Description;
        var metadata = Metadata;
        var accessMode = AccessMode;
        var scanInterval = ScanInterval;
        return new(Name, Address, DataType, new LogicalTagOptions
        {
            GroupName = groupName,
            Description = description,
            Metadata = metadata,
            AccessMode = accessMode,
            ScanInterval = scanInterval,
        });
    }
}
