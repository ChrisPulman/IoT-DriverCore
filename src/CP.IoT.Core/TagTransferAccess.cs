// Copyright (c) 2019-2026 Chris Pulman and contributors. All rights reserved.
// Chris Pulman and contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace IoT.DriverCore.Core;

/// <summary>Identifies the direction of a protocol transfer.</summary>
public enum TagTransferAccess
{
    /// <summary>Reads values from a device.</summary>
    Read = 0,

    /// <summary>Writes values to a device.</summary>
    Write = 1,
}
