// Copyright (c) 2019-2026 Chris Pulman and contributors. All rights reserved.
// Chris Pulman and contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

#if REACTIVE_SHIM
namespace IoT.DriverCore.OmronPlcRx.Reactive.Core.Enums;
#else
namespace IoT.DriverCore.OmronPlcRx.Core.Enums;
#endif

/// <summary>Represents the e rr or lo gf un ct io nc od e enumeration.</summary>
internal enum ErrorLogFunctionCode
{
    /// <summary>Represents the c le ar me ss ag es enum value.</summary>
    ClearMessages = 0x01,
    /// <summary>Represents the r ea d enum value.</summary>
    Read = 0x02,
    /// <summary>Represents the c le ar lo g enum value.</summary>
    ClearLog = 0x03,
}
