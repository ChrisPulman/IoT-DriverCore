// Copyright (c) 2019-2026 Chris Pulman and contributors. All rights reserved.
// Chris Pulman and contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

#if REACTIVE_SHIM
namespace IoT.DriverCore.OmronPlcRx.Reactive.Core.Enums;
#else
namespace IoT.DriverCore.OmronPlcRx.Core.Enums;
#endif

/// <summary>Represents the a cc es sr ig ht sf un ct io nc od e enumeration.</summary>
internal enum AccessRightsFunctionCode
{
    /// <summary>Represents the a cq ui re enum value.</summary>
    Acquire = 0x01,
    /// <summary>Represents the f or ce da cq ui re enum value.</summary>
    ForcedAcquire = 0x02,
    /// <summary>Represents the r el ea se enum value.</summary>
    Release = 0x03,
}
