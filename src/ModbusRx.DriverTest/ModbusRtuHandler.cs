// Copyright (c) 2022-2026 Chris Pulman. All rights reserved.
// Chris Pulman licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers.Binary;

namespace ModbusRx.DriverTest;

/// <summary>Handles Modbus RTU request frames for the test emulator.</summary>
/// <remarks>
/// Initializes a new instance of the <see cref="ModbusRtuHandler"/> class.
/// </remarks>
/// <param name="controller">The controller.</param>
/// <param name="slaveId">The slave identifier.</param>
public class ModbusRtuHandler(DummyTemperatureController controller, byte slaveId)
{
    /// <summary>The minimum valid request frame length.</summary>
    private const int MinimumFrameLength = 4;

    /// <summary>The CRC field length.</summary>
    private const int CrcLength = 2;

    /// <summary>The function code for reading holding registers.</summary>
    private const byte ReadHoldingRegistersFunction = 0x03;

    /// <summary>The function code for writing a single register.</summary>
    private const byte WriteSingleRegisterFunction = 0x06;

    /// <summary>The offset of request data after the address and function fields.</summary>
    private const int ReadRequestDataOffset = 2;

    /// <summary>The length of the read response header.</summary>
    private const int ReadResponseHeaderLength = 3;

    /// <summary>The length of a write response.</summary>
    private const int WriteResponseLength = 8;

    /// <summary>The length of a write request excluding its CRC.</summary>
    private const int WriteRequestLength = 6;

    /// <summary>The number of bytes in a Modbus register.</summary>
    private const int BytesPerRegister = 2;

    /// <summary>The slave identifier handled by this instance.</summary>
    private readonly byte _slaveId = slaveId;

    /// <summary>Handles the request.</summary>
    /// <param name="frame">The frame.</param>
    /// <param name="length">The length.</param>
    /// <returns>A byte array.</returns>
    public byte[]? HandleRequest(byte[] frame, int length)
    {
        if (length < MinimumFrameLength)
        {
            return null;
        }

        if (frame is null)
        {
            return null;
        }

        var slave = frame[0];
        if (slave != _slaveId)
        {
            return null; // Not for this slave
        }

        var function = frame[1];

        var crcReceived = (ushort)(frame[length - CrcLength] | (frame[length - 1] << 8));
        var crcCalc = ModbusCrc.Compute(frame, length - CrcLength);

        return crcReceived != crcCalc ? null : function switch
        {
            ReadHoldingRegistersFunction => HandleReadHoldingRegisters(frame),
            WriteSingleRegisterFunction => HandleWriteSingleRegister(frame),
            _ => null,
        };
    }

    /// <summary>Handles a read holding registers request.</summary>
    /// <param name="frame">The request frame.</param>
    /// <returns>The response frame.</returns>
    private byte[] HandleReadHoldingRegisters(byte[] frame)
    {
        var start = BinaryPrimitives.ReadUInt16BigEndian(frame.AsSpan(ReadRequestDataOffset));
        var count = BinaryPrimitives.ReadUInt16BigEndian(
            frame.AsSpan(ReadRequestDataOffset + BytesPerRegister));

        var response = new byte[ReadResponseHeaderLength + (count * BytesPerRegister) + CrcLength];
        response[0] = frame[0];
        response[1] = ReadHoldingRegistersFunction;
        response[2] = (byte)(count * BytesPerRegister);

        for (var i = 0; i < count; i++)
        {
            var value = controller.ReadRegister((ushort)(start + i));
            response[ReadResponseHeaderLength + (i * BytesPerRegister)] = (byte)(value >> 8);
            response[ReadResponseHeaderLength + 1 + (i * BytesPerRegister)] = (byte)(value & 0xFF);
        }

        var crc = ModbusCrc.Compute(response, response.Length - CrcLength);
        response[^CrcLength] = (byte)(crc & 0xFF);
        response[^1] = (byte)(crc >> 8);

        return response;
    }

    /// <summary>Handles a write single register request.</summary>
    /// <param name="frame">The request frame.</param>
    /// <returns>The response frame.</returns>
    private byte[] HandleWriteSingleRegister(byte[] frame)
    {
        var address = BinaryPrimitives.ReadUInt16BigEndian(frame.AsSpan(ReadRequestDataOffset));
        var value = BinaryPrimitives.ReadUInt16BigEndian(
            frame.AsSpan(ReadRequestDataOffset + BytesPerRegister));

        controller.WriteRegister(address, value);

        // Echo request back (Modbus spec)
        var response = new byte[WriteResponseLength];
        Array.Copy(frame, response, WriteRequestLength);

        var crc = ModbusCrc.Compute(response, WriteRequestLength);
        response[WriteRequestLength] = (byte)(crc & 0xFF);
        response[WriteRequestLength + 1] = (byte)(crc >> 8);

        return response;
    }
}
