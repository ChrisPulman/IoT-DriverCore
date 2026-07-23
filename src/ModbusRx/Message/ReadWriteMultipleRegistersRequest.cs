// Copyright (c) 2019-2026 Chris Pulman and contributors. All rights reserved.
// Chris Pulman and contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

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

/// <summary>Provides ReadWriteMultipleRegistersRequest functionality.</summary>
/// <seealso cref="AbstractModbusMessage" />
/// <seealso cref="IModbusRequest" />
public class ReadWriteMultipleRegistersRequest : AbstractModbusMessage, IModbusRequest
{
    /// <summary>Initializes a new instance of the <see cref="ReadWriteMultipleRegistersRequest"/> class.</summary>
    public ReadWriteMultipleRegistersRequest()
    {
    }

    /// <summary>Initializes a new instance of the <see cref="ReadWriteMultipleRegistersRequest"/> class.</summary>
    /// <param name="slaveAddress">The slave address.</param>
    /// <param name="startReadAddress">The start read address.</param>
    /// <param name="numberOfPointsToRead">The number of points to read.</param>
    /// <param name="startWriteAddress">The start write address.</param>
    /// <param name="writeData">The write data.</param>
    public ReadWriteMultipleRegistersRequest(
        byte slaveAddress,
        ushort startReadAddress,
        ushort numberOfPointsToRead,
        ushort startWriteAddress,
        RegisterCollection writeData)
        : base(slaveAddress, Modbus.ReadWriteMultipleRegisters)
    {
        ReadRequest = new(
            Modbus.ReadHoldingRegisters,
            slaveAddress,
            startReadAddress,
            numberOfPointsToRead);

        WriteRequest = new(
            slaveAddress,
            startWriteAddress,
            writeData);
    }

    /// <inheritdoc/>
    public override byte[] ProtocolDataUnit
    {
        get
        {
            var readPdu = ReadRequest?.ProtocolDataUnit
                ?? throw new InvalidOperationException("The read request is not initialized.");
            var writePdu = WriteRequest?.ProtocolDataUnit
                ?? throw new InvalidOperationException("The write request is not initialized.");
            using var stream = new MemoryStream(readPdu.Length + writePdu.Length);

            stream.WriteByte(FunctionCode);

            // read and write PDUs without function codes
            stream.Write(readPdu, 1, readPdu.Length - 1);
            stream.Write(writePdu, 1, writePdu.Length - 1);

            return stream.ToArray();
        }
    }

    /// <summary>Gets the read request.</summary>
/// <value>The read request.</value>
    public ReadHoldingInputRegistersRequest? ReadRequest { get; private set; }

    /// <summary>Gets the write request.</summary>
/// <value>The write request.</value>
    public WriteMultipleRegistersRequest? WriteRequest { get; private set; }

    /// <inheritdoc/>
    public override int MinimumFrameSize => Eleven;

    /// <inheritdoc/>
    public override string ToString() =>
        $"Write {WriteRequest?.NumberOfPoints} holding registers starting at address " +
        $"{WriteRequest?.StartAddress}, and read {ReadRequest?.NumberOfPoints} registers " +
        $"starting at address {ReadRequest?.StartAddress}.";

    /// <inheritdoc/>
    public void ValidateResponse(IModbusMessage response)
    {
        var typedResponse = (ReadHoldingInputRegistersResponse)response;
        var expectedByteCount = ReadRequest?.NumberOfPoints * Two;

        if (expectedByteCount == typedResponse?.ByteCount)
        {
            return;
        }

        var msg = $"Unexpected byte count in response. Expected {expectedByteCount}, " +
            $"received {typedResponse?.ByteCount}.";
        throw new IOException(msg);
    }

    /// <inheritdoc/>
    protected override void InitializeUnique(byte[] frame)
    {
        if (frame is null || frame.Length < MinimumFrameSize + frame[10])
        {
            throw new FormatException("Message frame does not contain enough bytes.");
        }

        var readFrame = new byte[Two + Four];
        var writeFrame = new byte[frame.Length - Six + Two];

        readFrame[0] = SlaveAddress;
        writeFrame[0] = SlaveAddress;
        readFrame[1] = FunctionCode;
        writeFrame[1] = FunctionCode;

        Buffer.BlockCopy(frame, Two, readFrame, Two, Four);
        Buffer.BlockCopy(frame, Six, writeFrame, Two, frame.Length - Six);

        ReadRequest = ModbusMessageFactory.CreateModbusMessage(new ReadHoldingInputRegistersRequest(), readFrame);
        WriteRequest = ModbusMessageFactory.CreateModbusMessage(new WriteMultipleRegistersRequest(), writeFrame);
    }
}
