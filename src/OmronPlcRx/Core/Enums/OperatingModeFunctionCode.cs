// Copyright (c) 2019-2026 Chris Pulman and contributors. All rights reserved.
// Chris Pulman and contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

#if REACTIVE_SHIM
namespace OmronPlcRx.Reactive.Core.Enums;
#else
namespace OmronPlcRx.Core.Enums;
#endif

/// <summary>Represents the o pe ra ti ng mo de fu nc ti on co de enumeration.</summary>
internal enum OperatingModeFunctionCode
{
    /// <summary>Represents the r un mo de enum value.</summary>
    RunMode = 0x01,
    /// <summary>Represents the s to pm od e enum value.</summary>
    StopMode = 0x02,
}
