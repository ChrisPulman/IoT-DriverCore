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

/// <summary>Represents the w ri te me mo ry ar ea wo rd re qu es t type.</summary>
internal sealed class WriteMemoryAreaWordRequest : FINSRequest
{
    /// <summary>Initializes a new instance of the <see cref="WriteMemoryAreaWordRequest"/> class.</summary>
    /// <param name="plc">The p lc value.</param>
    /// <param name="values">The values to write.</param>
    private WriteMemoryAreaWordRequest(OmronPLCConnection plc, short[] values)
        : base(plc)
    {
        Values = values;
    }

    /// <summary>Gets or sets the start address value.</summary>
    internal ushort StartAddress { get; set; }

    /// <summary>Gets or sets the data type value.</summary>
    internal MemoryWordDataType DataType { get; set; }

    /// <summary>Gets the values value.</summary>
    internal short[] Values { get; }

    /// <summary>Initializes a new instance of the <see cref="CreateNew"/> class.</summary>
    /// <param name="plc">The p lc value.</param>
    /// <param name="startAddress">The s ta rt ad dr es s value.</param>
    /// <param name="dataType">The d at yp e value.</param>
    /// <param name="values">The v al ue s value.</param>
    /// <returns>The result produced by the operation.</returns>
    internal static WriteMemoryAreaWordRequest CreateNew(
        OmronPLCConnection plc,
        ushort startAddress,
        MemoryWordDataType dataType,
        short[] values) => new(plc, values)
    {
        FunctionCode = (byte)Enums.FunctionCode.MemoryArea,
        SubFunctionCode = (byte)MemoryAreaFunctionCode.Write,
        StartAddress = startAddress,
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
        var len = BitConverter.GetBytes((ushort)Values.Length);
        Array.Reverse(len);
        data.AddRange(len);

        // Word Values (big-endian)
        foreach (var value in Values)
        {
            var bytes = BitConverter.GetBytes(value);
            Array.Reverse(bytes);
            data.AddRange(bytes);
        }

        return data;
    }
}
