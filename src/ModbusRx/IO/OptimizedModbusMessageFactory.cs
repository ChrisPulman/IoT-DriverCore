// Copyright (c) 2019-2026 Chris Pulman and contributors. All rights reserved.
// Chris Pulman and contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers.Binary;
#if REACTIVE_SHIM
using IoT.DriverCore.ModbusRx.Reactive.Utility;
#else
using IoT.DriverCore.ModbusRx.Utility;
#endif

#if REACTIVE_SHIM
namespace IoT.DriverCore.ModbusRx.Reactive.IO;
#else
namespace IoT.DriverCore.ModbusRx.IO;
#endif

/// <summary>High-performance Modbus message factory with cross-platform optimizations.</summary>
public static class OptimizedModbusMessageFactory
{
    /// <summary>Executes the Buffer Manager operation.</summary>
    private static readonly ModbusBufferManager BufferManager = new();

    /// <summary>Creates a read holding registers request with high performance.</summary>
    /// <param name="slaveAddress">The slave address.</param>
    /// <param name="startAddress">The start address.</param>
    /// <param name="numberOfPoints">The number of points.</param>
    /// <returns>The serialized message bytes.</returns>
    public static byte[] CreateReadHoldingRegistersRequest(
        byte slaveAddress,
        ushort startAddress,
        ushort numberOfPoints)
    {
        var buffer = BufferManager.RentByteBuffer(Eight);
        try
        {
            buffer[0] = slaveAddress;
            buffer[1] = Modbus.ReadHoldingRegisters;
            BinaryPrimitives.WriteUInt16BigEndian(buffer.AsSpan(Two, Two), startAddress);
            BinaryPrimitives.WriteUInt16BigEndian(buffer.AsSpan(Four, Two), numberOfPoints);

            // Calculate and append CRC
            var crc = ModbusUtility.CalculateCrc(buffer.AsSpan(0, Six).ToArray());
            buffer[6] = crc[0];
            buffer[7] = crc[1];

            var result = new byte[8];
            Array.Copy(buffer, result, Eight);
            return result;
        }
        finally
        {
            BufferManager.ReturnByteBuffer(buffer, true);
        }
    }

    /// <summary>Creates a read coils request with high performance.</summary>
    /// <param name="slaveAddress">The slave address.</param>
    /// <param name="startAddress">The start address.</param>
    /// <param name="numberOfPoints">The number of points.</param>
    /// <returns>The serialized message bytes.</returns>
    public static byte[] CreateReadCoilsRequest(byte slaveAddress, ushort startAddress, ushort numberOfPoints)
    {
        var buffer = BufferManager.RentByteBuffer(Eight);
        try
        {
            buffer[0] = slaveAddress;
            buffer[1] = Modbus.ReadCoils;
            BinaryPrimitives.WriteUInt16BigEndian(buffer.AsSpan(Two, Two), startAddress);
            BinaryPrimitives.WriteUInt16BigEndian(buffer.AsSpan(Four, Two), numberOfPoints);

            // Calculate and append CRC
            var crc = ModbusUtility.CalculateCrc(buffer.AsSpan(0, Six).ToArray());
            buffer[6] = crc[0];
            buffer[7] = crc[1];

            var result = new byte[8];
            Array.Copy(buffer, result, Eight);
            return result;
        }
        finally
        {
            BufferManager.ReturnByteBuffer(buffer, true);
        }
    }

    /// <summary>Creates a write single register request with high performance.</summary>
    /// <param name="slaveAddress">The slave address.</param>
    /// <param name="registerAddress">The register address.</param>
    /// <param name="value">The value to write.</param>
    /// <returns>The serialized message bytes.</returns>
    public static byte[] CreateWriteSingleRegisterRequest(byte slaveAddress, ushort registerAddress, ushort value)
    {
        var buffer = BufferManager.RentByteBuffer(Eight);
        try
        {
            buffer[0] = slaveAddress;
            buffer[1] = Modbus.WriteSingleRegister;
            BinaryPrimitives.WriteUInt16BigEndian(buffer.AsSpan(Two, Two), registerAddress);
            BinaryPrimitives.WriteUInt16BigEndian(buffer.AsSpan(Four, Two), value);

            // Calculate and append CRC
            var crc = ModbusUtility.CalculateCrc(buffer.AsSpan(0, Six).ToArray());
            buffer[6] = crc[0];
            buffer[7] = crc[1];

            var result = new byte[8];
            Array.Copy(buffer, result, Eight);
            return result;
        }
        finally
        {
            BufferManager.ReturnByteBuffer(buffer, true);
        }
    }

    /// <summary>Creates a write multiple registers request with high performance.</summary>
    /// <param name="slaveAddress">The slave address.</param>
    /// <param name="startAddress">The start address.</param>
    /// <param name="values">The values to write.</param>
    /// <returns>The serialized message bytes.</returns>
    public static byte[] CreateWriteMultipleRegistersRequest(byte slaveAddress, ushort startAddress, ushort[] values)
    {
        if (values is null)
        {
            throw new ArgumentNullException(nameof(values));
        }

        var messageLength = Nine + (values.Length * Two); // Header + byte count + data + CRC
        var buffer = BufferManager.RentByteBuffer(messageLength);
        try
        {
            buffer[0] = slaveAddress;
            buffer[1] = Modbus.WriteMultipleRegisters;
            BinaryPrimitives.WriteUInt16BigEndian(buffer.AsSpan(Two, Two), startAddress);
            BinaryPrimitives.WriteUInt16BigEndian(buffer.AsSpan(Four, Two), (ushort)values.Length);
            buffer[6] = (byte)(values.Length * Two); // Byte count

            // Write register values
            var dataIndex = 7;
            for (var i = 0; i < values.Length; i++)
            {
                BinaryPrimitives.WriteUInt16BigEndian(buffer.AsSpan(dataIndex, Two), values[i]);
                dataIndex += Two;
            }

            // Calculate and append CRC
            var crc = ModbusUtility.CalculateCrc(buffer.AsSpan(0, messageLength - Two).ToArray());
            buffer[messageLength - Two] = crc[0];
            buffer[messageLength - 1] = crc[1];

            var result = new byte[messageLength];
            Array.Copy(buffer, result, messageLength);
            return result;
        }
        finally
        {
            BufferManager.ReturnByteBuffer(buffer, true);
        }
    }

    /// <summary>Creates a write single coil request with high performance.</summary>
    /// <param name="slaveAddress">The slave address.</param>
    /// <param name="coilAddress">The coil address.</param>
    /// <param name="value">The value to write.</param>
    /// <returns>The serialized message bytes.</returns>
    public static byte[] CreateWriteSingleCoilRequest(byte slaveAddress, ushort coilAddress, bool value)
    {
        var buffer = BufferManager.RentByteBuffer(Eight);
        try
        {
            buffer[0] = slaveAddress;
            buffer[1] = Modbus.WriteSingleCoil;
            BinaryPrimitives.WriteUInt16BigEndian(buffer.AsSpan(Two, Two), coilAddress);
            BinaryPrimitives.WriteUInt16BigEndian(buffer.AsSpan(Four, Two), value ? (ushort)0xFF00 : (ushort)0x0000);

            // Calculate and append CRC
            var crc = ModbusUtility.CalculateCrc(buffer.AsSpan(0, Six).ToArray());
            buffer[6] = crc[0];
            buffer[7] = crc[1];

            var result = new byte[8];
            Array.Copy(buffer, result, Eight);
            return result;
        }
        finally
        {
            BufferManager.ReturnByteBuffer(buffer, true);
        }
    }

    /// <summary>Creates a write multiple coils request with high performance.</summary>
    /// <param name="slaveAddress">The slave address.</param>
    /// <param name="startAddress">The start address.</param>
    /// <param name="values">The values to write.</param>
    /// <returns>The serialized message bytes.</returns>
    public static byte[] CreateWriteMultipleCoilsRequest(byte slaveAddress, ushort startAddress, bool[] values)
    {
        if (values is null)
        {
            throw new ArgumentNullException(nameof(values));
        }

        var byteCount = (values.Length + Seven) / Eight; // Round up to next byte
        var messageLength = Nine + byteCount; // Header + byte count + data + CRC
        var buffer = BufferManager.RentByteBuffer(messageLength);
        try
        {
            buffer[0] = slaveAddress;
            buffer[1] = Modbus.WriteMultipleCoils;
            BinaryPrimitives.WriteUInt16BigEndian(buffer.AsSpan(Two, Two), startAddress);
            BinaryPrimitives.WriteUInt16BigEndian(buffer.AsSpan(Four, Two), (ushort)values.Length);
            buffer[6] = (byte)byteCount;

            // Pack boolean values into bytes
            const int dataIndex = 7;
            for (var i = 0; i < values.Length; i++)
            {
                var byteIndex = dataIndex + (i / Eight);
                var bitIndex = i % Eight;

                if (values[i])
                {
                    buffer[byteIndex] |= (byte)(1 << bitIndex);
                }
            }

            // Calculate and append CRC
            var crc = ModbusUtility.CalculateCrc(buffer.AsSpan(0, messageLength - Two).ToArray());
            buffer[messageLength - Two] = crc[0];
            buffer[messageLength - 1] = crc[1];

            var result = new byte[messageLength];
            Array.Copy(buffer, result, messageLength);
            return result;
        }
        finally
        {
            BufferManager.ReturnByteBuffer(buffer, true);
        }
    }

    /// <summary>Parses a read holding registers response with high performance.</summary>
    /// <param name="responseData">The response data.</param>
    /// <returns>The parsed register values.</returns>
    /// <exception cref="ArgumentException">Thrown when response data is invalid.</exception>
    public static ushort[] ParseReadHoldingRegistersResponse(byte[] responseData)
    {
        if (responseData is null)
        {
            throw new ArgumentNullException(nameof(responseData));
        }

        if (responseData.Length < 5)
        {
            throw new ArgumentException("Response data too short.", nameof(responseData));
        }

        var byteCount = responseData[2];
        var expectedLength = Five + byteCount; // Slave + Function + ByteCount + Data + CRC

        if (responseData.Length < expectedLength)
        {
            throw new ArgumentException("Response data incomplete.", nameof(responseData));
        }

        var valueCount = byteCount / Two;
        var values = new ushort[valueCount];

        for (var i = 0; i < valueCount; i++)
        {
            var dataIndex = Three + (i * Two);
            values[i] = BinaryPrimitives.ReadUInt16BigEndian(responseData.AsSpan(dataIndex, Two));
        }

        return values;
    }

    /// <summary>Parses a read coils response with high performance.</summary>
    /// <param name="responseData">The response data.</param>
    /// <param name="numberOfCoils">The number of coils requested.</param>
    /// <returns>The parsed coil values.</returns>
    /// <exception cref="ArgumentException">Thrown when response data is invalid.</exception>
    public static bool[] ParseReadCoilsResponse(byte[] responseData, int numberOfCoils)
    {
        if (responseData is null)
        {
            throw new ArgumentNullException(nameof(responseData));
        }

        if (responseData.Length < 5)
        {
            throw new ArgumentException("Response data too short.", nameof(responseData));
        }

        var byteCount = responseData[2];
        var expectedLength = Five + byteCount; // Slave + Function + ByteCount + Data + CRC

        if (responseData.Length < expectedLength)
        {
            throw new ArgumentException("Response data incomplete.", nameof(responseData));
        }

        var values = new bool[numberOfCoils];

        for (var i = 0; i < numberOfCoils; i++)
        {
            var byteIndex = Three + (i / Eight);
            var bitIndex = i % Eight;

            if (byteIndex < responseData.Length)
            {
                values[i] = (responseData[byteIndex] & (1 << bitIndex)) != 0;
            }
        }

        return values;
    }

    /// <summary>Validates a Modbus message CRC with high performance.</summary>
    /// <param name="messageData">The complete message data including CRC.</param>
    /// <returns>True if CRC is valid.</returns>
    public static bool ValidateMessageCrc(byte[] messageData)
    {
        if (messageData is null || messageData.Length < 4)
        {
            return false;
        }

        var dataLength = messageData.Length - Two;
        var dataForCrc = new byte[dataLength];
        Array.Copy(messageData, dataForCrc, dataLength);

        var calculatedCrc = ModbusUtility.CalculateCrc(dataForCrc);
        var receivedCrc = new byte[2];
        Array.Copy(messageData, dataLength, receivedCrc, 0, Two);

        return ModbusBufferManager.CompareArrays(calculatedCrc, receivedCrc);
    }

    /// <summary>Disposes the shared buffer manager.</summary>
    public static void DisposeSharedResources() => BufferManager?.Dispose();
}
