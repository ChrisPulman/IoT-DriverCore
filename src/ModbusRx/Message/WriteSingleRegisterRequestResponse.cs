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

/// <summary>Provides WriteSingleRegisterRequestResponse functionality.</summary>
public class WriteSingleRegisterRequestResponse : AbstractModbusMessageWithData<RegisterCollection>, IModbusRequest
{
    /// <summary>Initializes a new instance of the <see cref="WriteSingleRegisterRequestResponse"/> class.</summary>
    public WriteSingleRegisterRequestResponse()
    {
    }

    /// <summary>Initializes a new instance of the <see cref="WriteSingleRegisterRequestResponse"/> class.</summary>
    /// <param name="slaveAddress">The slave address.</param>
    /// <param name="startAddress">The start address.</param>
    /// <param name="registerValue">The register value.</param>
    public WriteSingleRegisterRequestResponse(byte slaveAddress, ushort startAddress, ushort registerValue)
        : base(slaveAddress, Modbus.WriteSingleRegister)
    {
        StartAddress = startAddress;
        Data = new(registerValue);
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

        return $"Write single holding register {data} at address {StartAddress}.";
    }

    /// <summary>Validate the specified response against the current request.</summary>
    /// <param name="response">The Modbus message.</param>
    public void ValidateResponse(IModbusMessage response)
    {
        var typedResponse = (WriteSingleRegisterRequestResponse)response;

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
        Data = new((ushort)IPAddress.NetworkToHostOrder(BitConverter.ToInt16(frame, Four)));
    }

    /// <summary>Gets the single data value in a write request or response.</summary>
    /// <param name="data">The data values.</param>
    /// <returns>The single data value.</returns>
    private static ushort GetSingleData(RegisterCollection? data)
    {
        if (data is null || data.Count != 1)
        {
            throw new InvalidOperationException("A single-register message must contain exactly one data value.");
        }

        return data[0];
    }
}
