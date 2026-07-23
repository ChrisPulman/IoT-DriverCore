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

/// <summary>Provides ReadCoilsInputsResponse functionality.</summary>
/// <seealso cref="IModbusMessage" />
public class ReadCoilsInputsResponse : AbstractModbusMessageWithData<DiscreteCollection>, IModbusMessage
{
    /// <summary>Initializes a new instance of the <see cref="ReadCoilsInputsResponse"/> class.</summary>
    public ReadCoilsInputsResponse()
    {
    }

    /// <summary>Initializes a new instance of the <see cref="ReadCoilsInputsResponse"/> class.</summary>
    /// <param name="functionCode">The function code.</param>
    /// <param name="slaveAddress">The slave address.</param>
    /// <param name="byteCount">The byte count.</param>
    /// <param name="data">The data.</param>
    public ReadCoilsInputsResponse(byte functionCode, byte slaveAddress, byte byteCount, DiscreteCollection data)
        : base(slaveAddress, functionCode)
    {
        ByteCount = byteCount;
        Data = data;
    }

    /// <summary>Gets or sets the byte count.</summary>
/// <value>The byte count.</value>
    public byte ByteCount
    {
        get => MessageImpl.ByteCount!.Value;
        set => MessageImpl.ByteCount = value;
    }

    /// <inheritdoc/>
    public override int MinimumFrameSize => Three;

    /// <inheritdoc/>
    public override string ToString() =>
        $"Read {Data.Count} {(FunctionCode == Modbus.ReadInputs ? "inputs" : "coils")} - {Data}.";

    /// <inheritdoc/>
    protected override void InitializeUnique(byte[] frame)
    {
        if (frame is null || frame.Length < Three + frame[2])
        {
            throw new FormatException("Message frame data segment does not contain enough bytes.");
        }

        ByteCount = frame[2];
        var data = new byte[ByteCount];
        Array.Copy(frame, Three, data, 0, data.Length);
        Data = new(data);
    }
}
