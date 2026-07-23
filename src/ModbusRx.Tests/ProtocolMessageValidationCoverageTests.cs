// Copyright (c) 2019-2026 Chris Pulman and contributors. All rights reserved.
// Chris Pulman and contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using IoT.DriverCore.ModbusRx.Data;
using IoT.DriverCore.ModbusRx.Message;
using NativeAssert = TUnit.Assertions.Assert;

namespace IoT.DriverCore.ModbusRx.UnitTests;

/// <summary>Exercises protocol-message validation at public frame boundaries.</summary>
public sealed class ProtocolMessageValidationCoverageTests
{
    /// <summary>Verifies multi-coil frame parsing and matching response validation.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [TUnit.Core.Test]
    public async Task WriteMultipleCoils_InitializesAndValidatesResponsesAsync()
    {
        var request = ModbusMessageFactory.CreateModbusMessage(
            new WriteMultipleCoilsRequest(),
            [1, Modbus.WriteMultipleCoils, 0, 0x02, 0, 0x09, 0x02, 0x55, 0x01]);

        await NativeAssert.That(request.StartAddress).IsEqualTo((ushort)0x02);
        await NativeAssert.That(request.NumberOfPoints).IsEqualTo((ushort)0x09);
        await NativeAssert.That(request.ByteCount).IsEqualTo((byte)0x02);
        await NativeAssert.That(request.Data.NetworkBytes).IsEquivalentTo((byte[])[0x55, 0x01]);
        request.ValidateResponse(new WriteMultipleCoilsResponse(1, 0x02, 0x09));

        await NativeAssert.That(
                () => request.ValidateResponse(new WriteMultipleCoilsResponse(1, 0x03, 0x09)))
            .Throws<IOException>();
        await NativeAssert.That(
                () => request.ValidateResponse(new WriteMultipleCoilsResponse(1, 0x02, 0x08)))
            .Throws<IOException>();
        await NativeAssert.That(
                () => ModbusMessageFactory.CreateModbusMessage(
                    new WriteMultipleCoilsRequest(),
                    [1, Modbus.WriteMultipleCoils, 0, 0x02, 0, 0x09, 0x03, 0x55, 0x01]))
            .Throws<FormatException>();

        DiscreteCollection? noCoils = null;
        await NativeAssert.That(
                () => new WriteMultipleCoilsRequest(1, 0, noCoils!))
            .Throws<ArgumentNullException>();
    }

    /// <summary>Verifies multi-register parsing, malformed byte counts, and response validation.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [TUnit.Core.Test]
    public async Task WriteMultipleRegisters_InitializesAndValidatesResponsesAsync()
    {
        var request = ModbusMessageFactory.CreateModbusMessage(
            new WriteMultipleRegistersRequest(),
            [1, Modbus.WriteMultipleRegisters, 0, 0x02, 0, 1, 0x02, 0x12, 0x34]);

        await NativeAssert.That(request.Data[0]).IsEqualTo((ushort)0x1234);
        request.ValidateResponse(new WriteMultipleRegistersResponse(1, 0x02, 1));

        await NativeAssert.That(
                () => request.ValidateResponse(new WriteMultipleRegistersResponse(1, 0x03, 1)))
            .Throws<IOException>();
        await NativeAssert.That(
                () => request.ValidateResponse(new WriteMultipleRegistersResponse(1, 0x02, 0x02)))
            .Throws<IOException>();
        await NativeAssert.That(
                () => ModbusMessageFactory.CreateModbusMessage(
                    new WriteMultipleRegistersRequest(),
                    [1, Modbus.WriteMultipleRegisters, 0, 0x02, 0, 1, 0x04, 0x12, 0x34]))
            .Throws<FormatException>();
    }

    /// <summary>Verifies single-write initialization and both response mismatch branches.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [TUnit.Core.Test]
    public async Task SingleWrites_InitializeValidateAndRejectShortFramesAsync()
    {
        var coil = ModbusMessageFactory.CreateModbusMessage(
            new WriteSingleCoilRequestResponse(),
            [1, Modbus.WriteSingleCoil, 0, 0x02, 0xff, 0]);
        var register = ModbusMessageFactory.CreateModbusMessage(
            new WriteSingleRegisterRequestResponse(),
            [1, Modbus.WriteSingleRegister, 0, 0x02, 0x12, 0x34]);

        await NativeAssert.That(coil.Data[0]).IsEqualTo(Modbus.CoilOn);
        await NativeAssert.That(register.Data[0]).IsEqualTo((ushort)0x1234);
        coil.ValidateResponse(new WriteSingleCoilRequestResponse(1, 0x02, true));
        register.ValidateResponse(new WriteSingleRegisterRequestResponse(1, 0x02, 0x1234));

        await NativeAssert.That(
                () => coil.ValidateResponse(new WriteSingleCoilRequestResponse(1, 0x03, true)))
            .Throws<IOException>();
        await NativeAssert.That(
                () => coil.ValidateResponse(new WriteSingleCoilRequestResponse(1, 0x02, false)))
            .Throws<IOException>();
        await NativeAssert.That(
                () => register.ValidateResponse(new WriteSingleRegisterRequestResponse(1, 0x03, 0x1234)))
            .Throws<IOException>();
        await NativeAssert.That(
                () => register.ValidateResponse(new WriteSingleRegisterRequestResponse(1, 0x02, 0x4321)))
            .Throws<IOException>();
        await NativeAssert.That(
                () => ModbusMessageFactory.CreateModbusMessage(
                    new WriteSingleCoilRequestResponse(),
                    [1, Modbus.WriteSingleCoil, 0, 0x02, 0xff]))
            .Throws<FormatException>();
        await NativeAssert.That(
                () => ModbusMessageFactory.CreateModbusMessage(
                    new WriteSingleRegisterRequestResponse(),
                    [1, Modbus.WriteSingleRegister, 0, 0x02, 0x12]))
            .Throws<FormatException>();

        coil.Data = [];
        register.Data = [];
        await NativeAssert.That(() => coil.ToString()).Throws<InvalidOperationException>();
        await NativeAssert.That(() => register.ToString()).Throws<InvalidOperationException>();
    }

    /// <summary>Verifies read-write register request frame parsing and byte-count validation.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [TUnit.Core.Test]
    public async Task ReadWriteMultipleRegisters_InitializesAndValidatesResponsesAsync()
    {
        var request = ModbusMessageFactory.CreateModbusMessage(
            new ReadWriteMultipleRegistersRequest(),
            [1, Modbus.ReadWriteMultipleRegisters, 0, 0x02, 0, 1, 0, 0x04, 0, 1, 0x02, 0, 0x2a]);

        await NativeAssert.That(request.ReadRequest).IsNotNull();
        await NativeAssert.That(request.WriteRequest).IsNotNull();
        await NativeAssert.That(request.ReadRequest!.StartAddress).IsEqualTo((ushort)0x02);
        await NativeAssert.That(request.WriteRequest!.Data[0]).IsEqualTo((ushort)0x2a);
        request.ValidateResponse(
            new ReadHoldingInputRegistersResponse(Modbus.ReadHoldingRegisters, 1, new RegisterCollection(0x2a)));

        await NativeAssert.That(
                () => request.ValidateResponse(
                    new ReadHoldingInputRegistersResponse(
                        Modbus.ReadHoldingRegisters,
                        1,
                        new RegisterCollection(0x2a, 0x2b))))
            .Throws<IOException>();
        await NativeAssert.That(
                () => ModbusMessageFactory.CreateModbusMessage(
                    new ReadWriteMultipleRegistersRequest(),
                    [1, Modbus.ReadWriteMultipleRegisters, 0, 0x02, 0, 1, 0, 0x04, 0, 1, 0x03, 0, 0x2a]))
            .Throws<FormatException>();
    }

    /// <summary>Verifies holding-register request and response validation paths.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [TUnit.Core.Test]
    public async Task HoldingRegisters_InitializeAndRejectInvalidResponsesAsync()
    {
        var request = ModbusMessageFactory.CreateModbusMessage(
            new ReadHoldingInputRegistersRequest(),
            [1, Modbus.ReadHoldingRegisters, 0, 0x02, 0, 1]);
        var response = ModbusMessageFactory.CreateModbusMessage(
            new ReadHoldingInputRegistersResponse(),
            [1, Modbus.ReadHoldingRegisters, 0x02, 0x12, 0x34]);

        await NativeAssert.That(request.NumberOfPoints).IsEqualTo((ushort)1);
        await NativeAssert.That(response.Data[0]).IsEqualTo((ushort)0x1234);
        request.ValidateResponse(response);

        await NativeAssert.That(
                () => request.ValidateResponse(new ReadCoilsInputsResponse(Modbus.ReadCoils, 1, 1, new DiscreteCollection(true))))
            .Throws<IOException>();
        await NativeAssert.That(
                () => request.ValidateResponse(
                    new ReadHoldingInputRegistersResponse(
                        Modbus.ReadHoldingRegisters,
                        1,
                        new RegisterCollection(1, 0x02))))
            .Throws<IOException>();
        await NativeAssert.That(
                () => ModbusMessageFactory.CreateModbusMessage(
                    new ReadHoldingInputRegistersResponse(),
                    [1, Modbus.ReadHoldingRegisters, 1, 0]))
            .Throws<FormatException>();
        await NativeAssert.That(
                () => ModbusMessageFactory.CreateModbusMessage(
                    new ReadHoldingInputRegistersResponse(),
                    [1, Modbus.ReadHoldingRegisters, 0x02, 0]))
            .Throws<FormatException>();
    }

    /// <summary>Verifies coil request parsing, byte-count validation, and frame-size validation.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [TUnit.Core.Test]
    public async Task Coils_InitializeValidateAndRejectShortFramesAsync()
    {
        var request = ModbusMessageFactory.CreateModbusMessage(
            new ReadCoilsInputsRequest(),
            [1, Modbus.ReadCoils, 0, 0x02, 0, 0x09]);

        await NativeAssert.That(request.StartAddress).IsEqualTo((ushort)0x02);
        request.ValidateResponse(
            new ReadCoilsInputsResponse(Modbus.ReadCoils, 1, 0x02, new DiscreteCollection(0x55, 0x01)));

        await NativeAssert.That(
                () => request.ValidateResponse(
                    new ReadCoilsInputsResponse(Modbus.ReadCoils, 1, 1, new DiscreteCollection(0x55))))
            .Throws<IOException>();
        await NativeAssert.That(
                () => ModbusMessageFactory.CreateModbusMessage(
                    new ReadCoilsInputsRequest(),
                    [1, Modbus.ReadCoils, 0, 0x02, 0]))
            .Throws<FormatException>();
        await NativeAssert.That(
                () => ModbusMessageFactory.CreateModbusMessage(
                    new ReadCoilsInputsResponse(),
                    [1, Modbus.ReadCoils, 0x02, 0x55]))
            .Throws<FormatException>();
    }

    /// <summary>Verifies public factory guards, request dispatch, and base initialization guards.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [TUnit.Core.Test]
    public async Task MessageFactory_RejectsNullShortAndUnsupportedFramesAsync()
    {
        byte[]? noFrame = null;
        ReadCoilsInputsRequest? noMessage = null;

        var request = ModbusMessageFactory.CreateModbusRequest([1, Modbus.ReadCoils, 0, 0x02, 0, 1]);
        await NativeAssert.That(request).IsTypeOf<ReadCoilsInputsRequest>();
        await NativeAssert.That(
                () => ModbusMessageFactory.CreateModbusMessage(noMessage!, [1, Modbus.ReadCoils, 0, 0x02, 0, 1]))
            .Throws<ArgumentNullException>();
        await NativeAssert.That(
                () => ModbusMessageFactory.CreateModbusRequest(noFrame!))
            .Throws<FormatException>();
        await NativeAssert.That(
                () => ModbusMessageFactory.CreateModbusRequest([1, 0x63, 0]))
            .Throws<ArgumentException>();
        await NativeAssert.That(
                () => new ReadCoilsInputsRequest().Initialize(noFrame!))
            .Throws<FormatException>();
    }

    /// <summary>Verifies valid, invalid, and unknown-code slave exception responses.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [TUnit.Core.Test]
    public async Task SlaveExceptionResponse_InitializesAndValidatesFunctionCodeAsync()
    {
        var response = ModbusMessageFactory.CreateModbusMessage(
            new SlaveExceptionResponse(),
            [1, Modbus.ReadCoils + Modbus.ExceptionOffset, Modbus.IllegalDataAddress]);

        await NativeAssert.That(response.SlaveExceptionCode).IsEqualTo(Modbus.IllegalDataAddress);
        await NativeAssert.That(new SlaveExceptionResponse(1, 0xff, 0xfe).ToString()).Contains("Unknown");
        await NativeAssert.That(
                () => ModbusMessageFactory.CreateModbusMessage(
                    new SlaveExceptionResponse(),
                    [1, Modbus.ExceptionOffset, Modbus.IllegalFunction]))
            .Throws<FormatException>();
        await NativeAssert.That(
                () => ModbusMessageFactory.CreateModbusMessage(new SlaveExceptionResponse(), [1, 0x81]))
            .Throws<FormatException>();
    }
}
