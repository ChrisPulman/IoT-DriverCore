// Copyright (c) 2019-2026 Chris Pulman and contributors. All rights reserved.
// Chris Pulman and contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Collections.Generic;
#if REACTIVE_SHIM
using IoT.DriverCore.OmronPlcRx.Reactive.Core.Enums;
#else
using IoT.DriverCore.OmronPlcRx.Core.Enums;
#endif

#if REACTIVE_SHIM
namespace IoT.DriverCore.OmronPlcRx.Reactive.Core.Requests;
#else
namespace IoT.DriverCore.OmronPlcRx.Core.Requests;
#endif

/// <summary>Represents the r ea dc yc le ti me re qu es t type.</summary>
internal sealed class ReadCycleTimeRequest : FINSRequest
{
    /// <summary>Initializes a new instance of the <see cref="ReadCycleTimeRequest"/> class.</summary>
    /// <param name="plc">The p lc value.</param>
    private ReadCycleTimeRequest(OmronPLCConnection plc)
        : base(plc)
    {
    }

    /// <summary>Initializes a new instance of the <see cref="CreateNew"/> class.</summary>
    /// <param name="plc">The p lc value.</param>
    /// <returns>The result produced by the operation.</returns>
    internal static ReadCycleTimeRequest CreateNew(OmronPLCConnection plc) => new(plc)
    {
        FunctionCode = (byte)Enums.FunctionCode.Status,
        SubFunctionCode = (byte)StatusFunctionCode.ReadCycleTime,
    };

    /// <summary>Reads the PLC cycle time.</summary>
    /// <returns>The result produced by the operation.</returns>
    protected override List<byte> BuildRequestData() => [01];
}
