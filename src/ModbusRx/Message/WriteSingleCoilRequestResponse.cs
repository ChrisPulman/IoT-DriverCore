// Copyright (c) 2019-2026 Chris Pulman and contributors. All rights reserved.
// Chris Pulman and contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Net;
#if REACTIVE_SHIM
using ModbusRx.Reactive.Data;
#else
using ModbusRx.Data;
#endif

#if REACTIVE_SHIM
namespace ModbusRx.Reactive.Message;
#else
namespace ModbusRx.Message;
#endif

/// <summary>Provides WriteSingleCoilRequestResponse functionality.</summary>
public class WriteSingleCoilRequestResponse : AbstractModbusMessageWithData<RegisterCollection>, IModbusRequest
{
    /// <summary>Initializes a new instance of the <see cref="WriteSingleCoilRequestResponse"/> class.</summary>
    public WriteSingleCoilRequestResponse()
    {
    }

    /// <summary>Initializes a new instance of the <see cref="WriteSingleCoilRequestResponse"/> class.</summary>
    /// <param name="slaveAddress">The slave address.</param>
    /// <param name="startAddress">The start address.</param>
    /// <param name="coilState">if set to <c>true</c> [coil state].</param>
    public WriteSingleCoilRequestResponse(byte slaveAddress, ushort startAddress, bool coilState)
        : base(slaveAddress, Modbus.WriteSingleCoil)
    {
        StartAddress = startAddress;
        Data = new(coilState ? Modbus.CoilOn : Modbus.CoilOff);
    }

    /// <summary>Gets the minimum size of the frame.</summary>
/// <value>The minimum size of the frame.</value>
    public override int MinimumFrameSize => Six;

    /// <summary>Gets or sets the start address.</summary>
/// <value>The start address.</value>
    public ushort StartAddress
    {
        get => MessageImpl.StartAddress!.Value;
        set => MessageImpl.StartAddress = value;
    }

    /// <summary>Converts to string.</summary>
    /// <returns>
    /// A <see cref="string" /> that represents this instance.
    /// </returns>
    public override string ToString()
    {
        var data = GetSingleData(Data);

        return $"Write single coil {(data == Modbus.CoilOn ? 1 : 0)} at address {StartAddress}.";
    }

    /// <summary>Validate the specified response against the current request.</summary>
    /// <param name="response">The Modbus Message.</param>
    public void ValidateResponse(IModbusMessage response)
    {
        var typedResponse = (WriteSingleCoilRequestResponse)response;

        if (StartAddress != typedResponse?.StartAddress)
        {
            var msg = $"Unexpected start address in response. Expected {StartAddress}, " +
                $"received {typedResponse?.StartAddress}.";
            throw new IOException(msg);
        }

        var expectedData = GetSingleData(Data);
        var actualData = GetSingleData(typedResponse.Data);
        if (expectedData != actualData)
        {
            var msg = $"Unexpected data in response. Expected {expectedData}, received {actualData}.";
            throw new IOException(msg);
        }
    }

    /// <summary>Initializes the unique.</summary>
    /// <param name="frame">The frame.</param>
    protected override void InitializeUnique(byte[] frame)
    {
        StartAddress = (ushort)IPAddress.NetworkToHostOrder(BitConverter.ToInt16(frame, Two));
        var data = new byte[2];
        Array.Copy(frame, Four, data, 0, data.Length);
        Data = new(data);
    }

    /// <summary>Gets the single data value in a write request or response.</summary>
    /// <param name="data">The data values.</param>
    /// <returns>The single data value.</returns>
    private static ushort GetSingleData(RegisterCollection? data)
    {
        if (data is null || data.Count != 1)
        {
            throw new InvalidOperationException("A single-coil message must contain exactly one data value.");
        }

        return data[0];
    }
}
