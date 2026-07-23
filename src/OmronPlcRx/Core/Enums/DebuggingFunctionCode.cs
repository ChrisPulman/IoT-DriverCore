// Copyright (c) 2019-2026 Chris Pulman and contributors. All rights reserved.
// Chris Pulman and contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

#if REACTIVE_SHIM
namespace IoT.DriverCore.OmronPlcRx.Reactive.Core.Enums;
#else
namespace IoT.DriverCore.OmronPlcRx.Core.Enums;
#endif

/// <summary>Represents the d eb ug gi ng fu nc ti on co de enumeration.</summary>
internal enum DebuggingFunctionCode
{
    /// <summary>Represents the f or ce bi ts enum value.</summary>
    ForceBits = 0x01,
    /// <summary>Represents the c le ar fo rc ed bi ts enum value.</summary>
    ClearForcedBits = 0x02,
}
