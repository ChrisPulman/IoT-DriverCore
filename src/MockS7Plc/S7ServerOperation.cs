// Copyright (c) 2019-2026 Chris Pulman and contributors. All rights reserved.
// Chris Pulman and contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace IoT.DriverCore.S7PlcRx.Mock;

/// <summary>Identifies a server operation that can consume a scripted fault.</summary>
public enum S7ServerOperation
{
    /// <summary>Matches every server operation.</summary>
    Any,

    /// <summary>Matches COTP connection setup.</summary>
    Connect,

    /// <summary>Matches S7 communication setup.</summary>
    Setup,

    /// <summary>Matches an S7 ReadVar operation.</summary>
    Read,

    /// <summary>Matches an S7 WriteVar operation.</summary>
    Write,

    /// <summary>Matches an S7 SZL user-data read.</summary>
    Szl,
}
