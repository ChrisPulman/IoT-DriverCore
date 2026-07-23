// Copyright (c) 2019-2026 Chris Pulman and contributors. All rights reserved.
// Chris Pulman and contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
#if REACTIVE_SHIM
using IoT.DriverCore.OmronPlcRx.Reactive.Core.Enums;
using IoT.DriverCore.OmronPlcRx.Reactive.Enums;
#else
using IoT.DriverCore.OmronPlcRx.Core.Enums;
using IoT.DriverCore.OmronPlcRx.Enums;
#endif

#if REACTIVE_SHIM
namespace IoT.DriverCore.OmronPlcRx.Reactive.Core.Requests;
#else
namespace IoT.DriverCore.OmronPlcRx.Core.Requests;
#endif

/// <summary>Represents the r ea dm em or ya re aw or dr eq ue st type.</summary>
internal sealed class ReadMemoryAreaWordRequest : FINSRequest
{
    /// <summary>Initializes a new instance of the <see cref="ReadMemoryAreaWordRequest"/> class.</summary>
    /// <param name="plc">The p lc value.</param>
    private ReadMemoryAreaWordRequest(OmronPLCConnection plc)
        : base(plc)
    {
    }

    /// <summary>Gets or sets the start address value.</summary>
    internal ushort StartAddress { get; set; }

    /// <summary>Gets or sets the length value.</summary>
    internal ushort Length { get; set; }

    /// <summary>Gets or sets the data type value.</summary>
    internal MemoryWordDataType DataType { get; set; }

    /// <summary>Initializes a new instance of the <see cref="CreateNew"/> class.</summary>
    /// <param name="plc">The p lc value.</param>
    /// <param name="startAddress">The s ta rt ad dr es s value.</param>
    /// <param name="length">The l en gt h value.</param>
    /// <param name="dataType">The d at yp e value.</param>
    /// <returns>The result produced by the operation.</returns>
    internal static ReadMemoryAreaWordRequest CreateNew(
        OmronPLCConnection plc,
        ushort startAddress,
        ushort length,
        MemoryWordDataType dataType) => new(plc)
    {
        FunctionCode = (byte)Enums.FunctionCode.MemoryArea,
        SubFunctionCode = (byte)MemoryAreaFunctionCode.Read,
        StartAddress = startAddress,
        Length = length,
        DataType = dataType,
    };

    protected override List<byte> BuildRequestData()
    {
        var data = new List<byte>
        {
            // Memory Area Data Type
            (byte)DataType,
        };

        // Address (big-endian)
        var addr = BitConverter.GetBytes(StartAddress);
        Array.Reverse(addr);
        data.AddRange(addr);

        // Reserved
        data.Add(0);

        // Length (big-endian)
        var len = BitConverter.GetBytes(Length);
        Array.Reverse(len);
        data.AddRange(len);

        return data;
    }
}
