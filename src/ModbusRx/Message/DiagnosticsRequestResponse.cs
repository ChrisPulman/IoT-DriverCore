// Copyright (c) 2019-2026 Chris Pulman and contributors. All rights reserved.
// Chris Pulman and contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using System.Net;
#if REACTIVE_SHIM
using IoT.DriverCore.ModbusRx.Reactive.Data;
#else
using IoT.DriverCore.ModbusRx.Data;
#endif

#if REACTIVE_SHIM
namespace IoT.DriverCore.ModbusRx.Reactive.Message;
#else
namespace IoT.DriverCore.ModbusRx.Message;
#endif

/// <summary>Provides Diagnostics Request Response functionality.</summary>
internal sealed class DiagnosticsRequestResponse : AbstractModbusMessageWithData<RegisterCollection>, IModbusMessage
{
    /// <summary>Initializes a new instance of the Diagnostics Request Response class.</summary>
    public DiagnosticsRequestResponse()
    {
    }

    /// <summary>Initializes a new instance of the Diagnostics Request Response class.</summary>
    /// <param name="subFunctionCode">The sub Function Code value.</param>
    /// <param name="slaveAddress">The slave Address value.</param>
    /// <param name="data">The data value.</param>
    public DiagnosticsRequestResponse(ushort subFunctionCode, byte slaveAddress, RegisterCollection data)
        : base(slaveAddress, Modbus.Diagnostics)
    {
        SubFunctionCode = subFunctionCode;
        Data = data;
    }

    public override int MinimumFrameSize => Six;

    /// <summary>Gets or sets the Sub Function Code value.</summary>
    internal ushort SubFunctionCode
    {
        get => MessageImpl.SubFunctionCode!.Value;
        set => MessageImpl.SubFunctionCode = value;
    }

    public override string ToString()
    {
        Debug.Assert(
            SubFunctionCode == Modbus.DiagnosticsReturnQueryData,
            "Need to add support for additional sub-function.");

        return $"Diagnostics message, sub-function return query data - {Data}.";
    }

    protected override void InitializeUnique(byte[] frame)
    {
        SubFunctionCode = (ushort)IPAddress.NetworkToHostOrder(BitConverter.ToInt16(frame, Two));
        var data = new byte[2];
        Array.Copy(frame, Four, data, 0, data.Length);
        Data = new(data);
    }
}
