// Copyright (c) 2019-2026 Chris Pulman and contributors. All rights reserved.
// Chris Pulman and contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

#if REACTIVE_SHIM
namespace OmronPlcRx.Reactive.Core.Enums;
#else
namespace OmronPlcRx.Core.Enums;
#endif

/// <summary>Represents the t im ed at af un ct io nc od e enumeration.</summary>
internal enum TimeDataFunctionCode
{
    /// <summary>Represents the r ea dc lo ck enum value.</summary>
    ReadClock = 0x01,
    /// <summary>Represents the w ri te cl oc k enum value.</summary>
    WriteClock = 0x02,
}
