// Copyright (c) 2019-2026 Chris Pulman and contributors. All rights reserved.
// Chris Pulman and contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

#if REACTIVE_SHIM
namespace OmronPlcRx.Reactive.Core.Enums;
#else
namespace OmronPlcRx.Core.Enums;
#endif

/// <summary>Represents the m ac hi ne co nf ig ur at io nf un ct io nc od e enumeration.</summary>
internal enum MachineConfigurationFunctionCode
{
    /// <summary>Represents the r ea dc pu un it da ta enum value.</summary>
    ReadCPUUnitData = 0x01,
    /// <summary>Represents the r ea dc on ne ct io nd at a enum value.</summary>
    ReadConnectionData = 0x02,
}
