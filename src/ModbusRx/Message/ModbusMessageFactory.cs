// Copyright (c) 2019-2026 Chris Pulman and contributors. All rights reserved.
// Chris Pulman and contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

#if REACTIVE_SHIM
namespace IoT.DriverCore.ModbusRx.Reactive.Message;
#else
namespace IoT.DriverCore.ModbusRx.Message;
#endif

/// <summary>Modbus message factory.</summary>
public static class ModbusMessageFactory
{
    /// <summary>Minimum request frame length.</summary>
    private const int MinRequestFrameLength = 3;

    /// <summary>Create a Modbus message.</summary>
    /// <typeparam name="T">Modbus message type.</typeparam>
    /// <param name="message">The message instance to initialize.</param>
    /// <param name="frame">Bytes of Modbus frame.</param>
    /// <returns>New Modbus message based on type and frame bytes.</returns>
    public static T CreateModbusMessage<T>(T message, byte[] frame)
        where T : IModbusMessage
    {
        if (message is null)
        {
            throw new ArgumentNullException(nameof(message));
        }

        message.Initialize(frame);

        return message;
    }

    /// <summary>Create a Modbus request.</summary>
    /// <param name="frame">Bytes of Modbus frame.</param>
    /// <returns>Modbus request.</returns>
    public static IModbusMessage CreateModbusRequest(byte[] frame)
    {
        if (frame is null || frame.Length < MinRequestFrameLength)
        {
            throw new FormatException(
                $"Argument 'frame' must have a length of at least {MinRequestFrameLength} bytes.");
        }

        var functionCode = frame[1];
        return functionCode switch
        {
            Modbus.ReadCoils or Modbus.ReadInputs => CreateModbusMessage(new ReadCoilsInputsRequest(), frame),
            Modbus.ReadHoldingRegisters or Modbus.ReadInputRegisters =>
                CreateModbusMessage(new ReadHoldingInputRegistersRequest(), frame),
            Modbus.WriteSingleCoil => CreateModbusMessage(new WriteSingleCoilRequestResponse(), frame),
            Modbus.WriteSingleRegister => CreateModbusMessage(new WriteSingleRegisterRequestResponse(), frame),
            Modbus.Diagnostics => CreateModbusMessage(new DiagnosticsRequestResponse(), frame),
            Modbus.WriteMultipleCoils => CreateModbusMessage(new WriteMultipleCoilsRequest(), frame),
            Modbus.WriteMultipleRegisters => CreateModbusMessage(new WriteMultipleRegistersRequest(), frame),
            Modbus.ReadWriteMultipleRegisters => CreateModbusMessage(new ReadWriteMultipleRegistersRequest(), frame),
            _ => throw new ArgumentException($"Unsupported function code {functionCode}", nameof(frame)),
        };
    }
}
