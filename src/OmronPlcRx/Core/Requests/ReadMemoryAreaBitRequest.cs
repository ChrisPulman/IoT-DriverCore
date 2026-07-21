// Copyright (c) 2019-2026 Chris Pulman and contributors. All rights reserved.
// Chris Pulman and contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
#if REACTIVE_SHIM
using OmronPlcRx.Reactive.Core.Enums;
using OmronPlcRx.Reactive.Enums;
#else
using OmronPlcRx.Core.Enums;
using OmronPlcRx.Enums;
#endif

#if REACTIVE_SHIM
namespace OmronPlcRx.Reactive.Core.Requests;
#else
namespace OmronPlcRx.Core.Requests;
#endif

/// <summary>Represents the r ea dm em or ya re ab it re qu es t type.</summary>
internal sealed class ReadMemoryAreaBitRequest : FINSRequest
{
    /// <summary>Initializes a new instance of the <see cref="ReadMemoryAreaBitRequest"/> class.</summary>
    /// <param name="plc">The p lc value.</param>
    private ReadMemoryAreaBitRequest(OmronPLCConnection plc)
        : base(plc)
    {
    }

    /// <summary>Gets or sets the address value.</summary>
    internal ushort Address { get; set; }

    /// <summary>Gets or sets the start bit index value.</summary>
    internal byte StartBitIndex { get; set; }

    /// <summary>Gets or sets the length value.</summary>
    internal ushort Length { get; set; }

    /// <summary>Gets or sets the data type value.</summary>
    internal MemoryBitDataType DataType { get; set; }

    /// <summary>Initializes a new instance of the <see cref="CreateNew"/> class.</summary>
    /// <param name="plc">The p lc value.</param>
    /// <param name="address">The a dd re ss value.</param>
    /// <param name="startBitIndex">The s ta rt bi ti nd ex value.</param>
    /// <param name="length">The l en gt h value.</param>
    /// <param name="dataType">The d at yp e value.</param>
    /// <returns>The result produced by the operation.</returns>
    internal static ReadMemoryAreaBitRequest CreateNew(
        OmronPLCConnection plc,
        ushort address,
        byte startBitIndex,
        ushort length,
        MemoryBitDataType dataType) => new(plc)
    {
        FunctionCode = (byte)Enums.FunctionCode.MemoryArea,
        SubFunctionCode = (byte)MemoryAreaFunctionCode.Read,
        Address = address,
        StartBitIndex = startBitIndex,
        Length = length,
        DataType = dataType,
    };

    protected override List<byte> BuildRequestData()
    {
        var data = new List<byte>(1 + ProtocolConstants.Two + 1 + ProtocolConstants.Two)
        {
            // Memory Area Data Type
            (byte)DataType,
        };

        // Address (big-endian)
        var addr = BitConverter.GetBytes(Address);
        Array.Reverse(addr);
        data.AddRange(addr);

        // Bit Index
        data.Add(StartBitIndex);

        // Length (big-endian)
        var len = BitConverter.GetBytes(Length);
        Array.Reverse(len);
        data.AddRange(len);

        return data;
    }
}
