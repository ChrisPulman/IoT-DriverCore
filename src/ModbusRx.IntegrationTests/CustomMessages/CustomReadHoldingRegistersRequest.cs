// Copyright (c) 2019-2026 Chris Pulman and contributors. All rights reserved.
// Chris Pulman and contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Net;
using IoT.DriverCore.ModbusRx.Message;

namespace IoT.DriverCore.ModbusRx.IntegrationTests.CustomMessages;

/// <summary>Custom read holding registers request.</summary>
/// <seealso cref="IModbusMessage" />
/// <remarks>
/// Initializes a new instance of the <see cref="CustomReadHoldingRegistersRequest"/> class.
/// </remarks>
/// <param name="functionCode">The function code.</param>
/// <param name="slaveAddress">The slave address.</param>
/// <param name="startAddress">The start address.</param>
/// <param name="numberOfPoints">The number of points.</param>
public class CustomReadHoldingRegistersRequest(
    byte functionCode,
    byte slaveAddress,
    ushort startAddress,
    ushort numberOfPoints)
    : IModbusMessage
{
    /// <summary>The expected length of the request frame.</summary>
    private const int FrameLength = 6;

    /// <summary>The frame index containing the slave address.</summary>
    private const int SlaveAddressIndex = 0;

    /// <summary>The frame index containing the function code.</summary>
    private const int FunctionCodeIndex = 1;

    /// <summary>The frame index at which the start address begins.</summary>
    private const int StartAddressIndex = 2;

    /// <summary>The frame index at which the number of points begins.</summary>
    private const int NumberOfPointsIndex = 4;

    /// <summary>Gets composition of the slave address and protocol data unit.</summary>
    public byte[] MessageFrame
    {
        get
        {
            var frame = new List<byte>
            {
                SlaveAddress,
            };
            frame.AddRange(ProtocolDataUnit);

            return frame.ToArray();
        }
    }

    /// <summary>Gets composition of the function code and message data.</summary>
    public byte[] ProtocolDataUnit
    {
        get
        {
            var pdu = new List<byte>
            {
                FunctionCode,
            };
            pdu.AddRange(BitConverter.GetBytes(IPAddress.HostToNetworkOrder((short)StartAddress)));
            pdu.AddRange(BitConverter.GetBytes(IPAddress.HostToNetworkOrder((short)NumberOfPoints)));

            return pdu.ToArray();
        }
    }

    /// <summary>Gets or sets a unique identifier assigned to a message when using the IP protocol.</summary>
    public ushort TransactionId { get; set; }

    /// <summary>Gets or sets the function code tells the server what kind of action to perform.</summary>
    public byte FunctionCode { get; set; } = functionCode;

    /// <summary>Gets or sets address of the slave (server).</summary>
    public byte SlaveAddress { get; set; } = slaveAddress;

    /// <summary>Gets or sets the start address.</summary>
    /// <value>
    /// The start address.
    /// </value>
    public ushort StartAddress { get; set; } = startAddress;

    /// <summary>Gets or sets the number of points.</summary>
    /// <value>
    /// The number of points.
    /// </value>
    public ushort NumberOfPoints { get; set; } = numberOfPoints;

    /// <summary>Initializes a modbus message from the specified message frame.</summary>
    /// <param name="frame">Bytes of Modbus frame.</param>
    /// <exception cref="System.ArgumentNullException">frame.</exception>
    /// <exception cref="System.ArgumentException">Invalid frame. - frame.</exception>
    public void Initialize(byte[] frame)
    {
        if (frame is null)
        {
            throw new ArgumentNullException(nameof(frame));
        }

        if (frame.Length != FrameLength)
        {
            throw new ArgumentException("Invalid frame.", nameof(frame));
        }

        SlaveAddress = frame[SlaveAddressIndex];
        FunctionCode = frame[FunctionCodeIndex];
        StartAddress = (ushort)IPAddress.NetworkToHostOrder(BitConverter.ToInt16(frame, StartAddressIndex));
        NumberOfPoints = (ushort)IPAddress.NetworkToHostOrder(BitConverter.ToInt16(frame, NumberOfPointsIndex));
    }
}
