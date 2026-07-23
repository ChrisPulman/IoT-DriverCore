// Copyright (c) 2019-2026 Chris Pulman and contributors. All rights reserved.
// Chris Pulman and contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

#if REACTIVE_SHIM
namespace IoT.DriverCore.OmronPlcRx.Reactive.Enums;
#else
namespace IoT.DriverCore.OmronPlcRx.Enums;
#endif

/// <summary>Supported Omron PLC types used to adjust message capabilities and limits.</summary>
public enum PlcType
{
    /// <summary>Omron NJ101 series.</summary>
    NJ101,

    /// <summary>Omron NJ301 series.</summary>
    NJ301,

    /// <summary>Omron NJ501 series.</summary>
    NJ501,

    /// <summary>Omron NX1P2 series.</summary>
    NX1P2,

    /// <summary>Omron NX102 series.</summary>
    NX102,

    /// <summary>Omron NX701 series.</summary>
    NX701,

    /// <summary>Omron NY512 series.</summary>
    NY512,

    /// <summary>Omron NY532 series.</summary>
    NY532,

    /// <summary>Generic NJ/NX/NY series.</summary>
    NJ_NX_NY_Series,

    /// <summary>Omron CJ2 series.</summary>
    CJ2,

    /// <summary>Omron CP1 series.</summary>
    CP1,

    /// <summary>Omron C-series (legacy).</summary>
    C_Series,

    /// <summary>Unknown or not yet identified.</summary>
    Unknown,
}
