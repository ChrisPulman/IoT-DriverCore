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

/// <summary>Represents the w ri te me mo ry ar ea bi tr eq ue st type.</summary>
internal sealed class WriteMemoryAreaBitRequest : FINSRequest
{
    /// <summary>Initializes a new instance of the <see cref="WriteMemoryAreaBitRequest"/> class.</summary>
    /// <param name="plc">The p lc value.</param>
    /// <param name="values">The values to write.</param>
    private WriteMemoryAreaBitRequest(OmronPLCConnection plc, bool[] values)
        : base(plc)
    {
        Values = values;
    }

    /// <summary>Gets or sets the address value.</summary>
    internal ushort Address { get; set; }

    /// <summary>Gets or sets the start bit index value.</summary>
    internal byte StartBitIndex { get; set; }

    /// <summary>Gets or sets the data type value.</summary>
    internal MemoryBitDataType DataType { get; set; }

    /// <summary>Gets the values value.</summary>
    internal bool[] Values { get; }

    /// <summary>Initializes a new instance of the <see cref="CreateNew"/> class.</summary>
    /// <param name="plc">The p lc value.</param>
    /// <param name="address">The a dd re ss value.</param>
    /// <param name="startBitIndex">The s ta rt bi ti nd ex value.</param>
    /// <param name="dataType">The d at yp e value.</param>
    /// <param name="values">The v al ue s value.</param>
    /// <returns>A value indicating whether the operation succeeded.</returns>
    internal static WriteMemoryAreaBitRequest CreateNew(
        OmronPLCConnection plc,
        ushort address,
        byte startBitIndex,
        MemoryBitDataType dataType,
        bool[] values) => new(plc, values)
    {
        FunctionCode = (byte)Enums.FunctionCode.MemoryArea,
        SubFunctionCode = (byte)MemoryAreaFunctionCode.Write,
        Address = address,
        StartBitIndex = startBitIndex,
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
        var addr = BitConverter.GetBytes(Address);
        Array.Reverse(addr);
        data.AddRange(addr);

        // Bit Index
        data.Add(StartBitIndex);

        // Length (big-endian)
        var len = BitConverter.GetBytes((ushort)Values.Length);
        Array.Reverse(len);
        data.AddRange(len);

        // Bit Values
        for (var i = 0; i < Values.Length; i++)
        {
            data.Add(Values[i] ? (byte)1 : (byte)0);
        }

        return data;
    }
}
