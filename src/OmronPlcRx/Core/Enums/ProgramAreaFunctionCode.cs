// Copyright (c) 2019-2026 Chris Pulman and contributors. All rights reserved.
// Chris Pulman and contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

#if REACTIVE_SHIM
namespace OmronPlcRx.Reactive.Core.Enums;
#else
namespace OmronPlcRx.Core.Enums;
#endif

/// <summary>Represents the p ro gr am ar ea fu nc ti on co de enumeration.</summary>
internal enum ProgramAreaFunctionCode
{
    /// <summary>Represents the r ea d enum value.</summary>
    Read = 0x06,
    /// <summary>Represents the w ri te enum value.</summary>
    Write = 0x07,
    /// <summary>Represents the c le ar enum value.</summary>
    Clear = 0x08,
}
