// Copyright (c) 2019-2026 Chris Pulman and contributors. All rights reserved.
// Chris Pulman and contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System;
using IoT.DriverCore.ModbusRx.Data;
using IoT.DriverCore.ModbusRx.Message;

namespace IoT.DriverCore.ModbusRx.UnitTests.Message;

/// <summary>Tests the ModbusMessageFactoryFixture behavior.</summary>
public class ModbusMessageFactoryFixture
{
    /// <summary>Creates the modbus message read coils request.</summary>
    [TUnit.Core.Test]
    public void CreateModbusMessageReadCoilsRequest()
    {
        var request =
            ModbusMessageFactory.CreateModbusMessage(new ReadCoilsInputsRequest(), [
                Num.Value11, Modbus.ReadCoils, 0, Num.Value19, 0, Num.Value37,
            ]);

        var expectedRequest = new ReadCoilsInputsRequest(
            Modbus.ReadCoils,
            Num.Value11,
            Num.Value19,
            Num.Value37);
        ModbusMessageFixture.AssertModbusMessagePropertiesAreEqual(request, expectedRequest);
        Assert.Equal(expectedRequest.StartAddress, request.StartAddress);
        Assert.Equal(expectedRequest.NumberOfPoints, request.NumberOfPoints);
    }

    /// <summary>Creates the size of the modbus message read coils request with invalid frame.</summary>
    [TUnit.Core.Test]
    public void CreateModbusMessageReadCoilsRequestWithInvalidFrameSize()
    {
        byte[] frame = { 11, Modbus.ReadCoils, 4, 1, 2 };
        _ = Assert.Throws<FormatException>(() =>
            ModbusMessageFactory.CreateModbusMessage(new ReadCoilsInputsRequest(), frame));
    }

    /// <summary>Creates the modbus message read coils response.</summary>
    [TUnit.Core.Test]
    public void CreateModbusMessageReadCoilsResponse()
    {
        var response =
            ModbusMessageFactory.CreateModbusMessage(new ReadCoilsInputsResponse(), [
                Num.Value11, Modbus.ReadCoils, 1, 1,
            ]);

        var expectedResponse = new ReadCoilsInputsResponse(
            Modbus.ReadCoils,
            Num.Value11,
            1,
            new DiscreteCollection(true, false, false, false));

        ModbusMessageFixture.AssertModbusMessagePropertiesAreEqual(expectedResponse, response);

        Assert.Equal(expectedResponse.Data.NetworkBytes, response.Data.NetworkBytes);
    }

    /// <summary>Creates the modbus message read coils response with no byte count.</summary>
    [TUnit.Core.Test]
    public void CreateModbusMessageReadCoilsResponseWithNoByteCount()
    {
        byte[] frame = { 11, Modbus.ReadCoils };
        _ = Assert.Throws<FormatException>(() =>
            ModbusMessageFactory.CreateModbusMessage(new ReadCoilsInputsResponse(), frame));
    }

    /// <summary>Creates the size of the modbus message read coils response with invalid data.</summary>
    [TUnit.Core.Test]
    public void CreateModbusMessageReadCoilsResponseWithInvalidDataSize()
    {
        byte[] frame = { 11, Modbus.ReadCoils, 4, 1, 2, 3 };
        _ = Assert.Throws<FormatException>(() =>
            ModbusMessageFactory.CreateModbusMessage(new ReadCoilsInputsResponse(), frame));
    }

    /// <summary>Creates the modbus message read holding registers request.</summary>
    [TUnit.Core.Test]
    public void CreateModbusMessageReadHoldingRegistersRequest()
    {
        var request =
            ModbusMessageFactory.CreateModbusMessage(new ReadHoldingInputRegistersRequest(), [
                Num.Value17, Modbus.ReadHoldingRegisters, 0, Num.Value107, 0, Num.Value3,
            ]);

        var expectedRequest = new ReadHoldingInputRegistersRequest(
            Modbus.ReadHoldingRegisters,
            Num.Value17,
            Num.Value107,
            Num.Value3);

        ModbusMessageFixture.AssertModbusMessagePropertiesAreEqual(expectedRequest, request);

        Assert.Equal(expectedRequest.StartAddress, request.StartAddress);
        Assert.Equal(expectedRequest.NumberOfPoints, request.NumberOfPoints);
    }

    /// <summary>Creates the size of the modbus message read holding registers request with invalid frame.</summary>
    [TUnit.Core.Test]
    public void CreateModbusMessageReadHoldingRegistersRequestWithInvalidFrameSize() =>
        Assert.Throws<FormatException>(() =>
            ModbusMessageFactory.CreateModbusMessage(
                new ReadHoldingInputRegistersRequest(),
                [Num.Value11, Modbus.ReadHoldingRegisters, 0, 0, Num.Value5]));

    /// <summary>Creates the modbus message read holding registers response.</summary>
    [TUnit.Core.Test]
    public void CreateModbusMessageReadHoldingRegistersResponse()
    {
        var response =
            ModbusMessageFactory.CreateModbusMessage(new ReadHoldingInputRegistersResponse(), [
                Num.Value11, Modbus.ReadHoldingRegisters, Num.Value4, 0, Num.Value3, 0, Num.Value4,
            ]);

        var expectedResponse = new ReadHoldingInputRegistersResponse(
            Modbus.ReadHoldingRegisters,
            Num.Value11,
            new RegisterCollection(Num.Value3, Num.Value4));

        ModbusMessageFixture.AssertModbusMessagePropertiesAreEqual(expectedResponse, response);
    }

    /// <summary>Creates the size of the modbus message read holding registers response with invalid frame.</summary>
    [TUnit.Core.Test]
    public void CreateModbusMessageReadHoldingRegistersResponseWithInvalidFrameSize() =>
        Assert.Throws<FormatException>(() =>
            ModbusMessageFactory.CreateModbusMessage(
                new ReadHoldingInputRegistersResponse(),
                [Num.Value11, Modbus.ReadHoldingRegisters]));

    /// <summary>Creates the modbus message slave exception response.</summary>
    [TUnit.Core.Test]
    public void CreateModbusMessageSlaveExceptionResponse()
    {
        var response =
            ModbusMessageFactory.CreateModbusMessage(
                new SlaveExceptionResponse(),
                [Num.Value11, Num.Value129, Num.Value2]);

        var expectedException = new SlaveExceptionResponse(
            Num.Value11,
            Modbus.ReadCoils + Modbus.ExceptionOffset,
            Num.Value2);

        Assert.Equal(expectedException.FunctionCode, response.FunctionCode);
        Assert.Equal(expectedException.SlaveAddress, response.SlaveAddress);
        Assert.Equal(expectedException.MessageFrame, response.MessageFrame);
        Assert.Equal(expectedException.ProtocolDataUnit, response.ProtocolDataUnit);
    }

    /// <summary>Creates the modbus message slave exception response with invalid function code.</summary>
    [TUnit.Core.Test]
    public void CreateModbusMessageSlaveExceptionResponseWithInvalidFunctionCode() =>
        Assert.Throws<FormatException>(() =>
            ModbusMessageFactory.CreateModbusMessage(
                new SlaveExceptionResponse(),
                [Num.Value11, Num.Value128, Num.Value2]));

    /// <summary>Creates the size of the modbus message slave exception response with invalid frame.</summary>
    [TUnit.Core.Test]
    public void CreateModbusMessageSlaveExceptionResponseWithInvalidFrameSize() =>
        Assert.Throws<FormatException>(() =>
            ModbusMessageFactory.CreateModbusMessage(new SlaveExceptionResponse(), [ Num.Value11, Num.Value128]));

    /// <summary>Creates the modbus message write single coil request response.</summary>
    [TUnit.Core.Test]
    public void CreateModbusMessageWriteSingleCoilRequestResponse()
    {
        var request =
            ModbusMessageFactory.CreateModbusMessage(new WriteSingleCoilRequestResponse(), [
                Num.Value17, Modbus.WriteSingleCoil, 0, Num.Value172, byte.MaxValue, 0,
            ]);

        var expectedRequest = new WriteSingleCoilRequestResponse(Num.Value17, Num.Value172, true);
        ModbusMessageFixture.AssertModbusMessagePropertiesAreEqual(expectedRequest, request);

        Assert.Equal(expectedRequest.StartAddress, request.StartAddress);
        Assert.Equal(expectedRequest.Data.NetworkBytes, request.Data.NetworkBytes);
    }

    /// <summary>Creates the size of the modbus message write single coil request response with invalid frame.</summary>
    [TUnit.Core.Test]
    public void CreateModbusMessageWriteSingleCoilRequestResponseWithInvalidFrameSize() =>
        Assert.Throws<FormatException>(() =>
            ModbusMessageFactory.CreateModbusMessage(
                new WriteSingleCoilRequestResponse(),
                [Num.Value11, Modbus.WriteSingleCoil, 0, Num.Value105, byte.MaxValue]));

    /// <summary>Creates the modbus message write single register request response.</summary>
    [TUnit.Core.Test]
    public void CreateModbusMessageWriteSingleRegisterRequestResponse()
    {
        var request =
            ModbusMessageFactory.CreateModbusMessage(new WriteSingleRegisterRequestResponse(), [
                Num.Value17, Modbus.WriteSingleRegister, 0, 1, 0, Num.Value3,
            ]);

        var expectedRequest = new WriteSingleRegisterRequestResponse(Num.Value17, 1, Num.Value3);

        ModbusMessageFixture.AssertModbusMessagePropertiesAreEqual(expectedRequest, request);

        Assert.Equal(expectedRequest.StartAddress, request.StartAddress);
        Assert.Equal(expectedRequest.Data.NetworkBytes, request.Data.NetworkBytes);
    }

    /// <summary>Creates an invalid write single register response frame.</summary>
    [TUnit.Core.Test]
    public void CreateModbusMessageWriteSingleRegisterRequestResponseWithInvalidFrameSize() =>
        Assert.Throws<FormatException>(() =>
            ModbusMessageFactory.CreateModbusMessage(
                new WriteSingleRegisterRequestResponse(),
                [Num.Value11, Modbus.WriteSingleRegister, 0, 1, 0]));

    /// <summary>Creates the modbus message write multiple registers request.</summary>
    [TUnit.Core.Test]
    public void CreateModbusMessageWriteMultipleRegistersRequest()
    {
        var request =
            ModbusMessageFactory.CreateModbusMessage(new WriteMultipleRegistersRequest(), [
                Num.Value11, Modbus.WriteMultipleRegisters, 0, Num.Value5, 0, 1, Num.Value2, Num.Value255, Num.Value255,
            ]);

        var expectedRequest = new WriteMultipleRegistersRequest(
            Num.Value11,
            Num.Value5,
            new RegisterCollection(ushort.MaxValue));

        ModbusMessageFixture.AssertModbusMessagePropertiesAreEqual(expectedRequest, request);

        Assert.Equal(expectedRequest.StartAddress, request.StartAddress);
        Assert.Equal(expectedRequest.NumberOfPoints, request.NumberOfPoints);
        Assert.Equal(expectedRequest.ByteCount, request.ByteCount);
        Assert.Equal(expectedRequest.Data.NetworkBytes, request.Data.NetworkBytes);
    }

    /// <summary>Creates the size of the modbus message write multiple registers request with invalid frame.</summary>
    [TUnit.Core.Test]
    public void CreateModbusMessageWriteMultipleRegistersRequestWithInvalidFrameSize() =>
        Assert.Throws<FormatException>(() =>
            ModbusMessageFactory.CreateModbusMessage(
                new WriteMultipleRegistersRequest(),
                [Num.Value11, Modbus.WriteMultipleRegisters, 0, Num.Value5, 0, 1, Num.Value2]));

    /// <summary>Creates the modbus message write multiple registers response.</summary>
    [TUnit.Core.Test]
    public void CreateModbusMessageWriteMultipleRegistersResponse()
    {
        var response =
            ModbusMessageFactory.CreateModbusMessage(new WriteMultipleRegistersResponse(), [
                Num.Value17, Modbus.WriteMultipleRegisters, 0, 1, 0, Num.Value2,
            ]);
        var expectedResponse = new WriteMultipleRegistersResponse(Num.Value17, 1, Num.Value2);
        ModbusMessageFixture.AssertModbusMessagePropertiesAreEqual(expectedResponse, response);

        Assert.Equal(expectedResponse.StartAddress, response.StartAddress);
        Assert.Equal(expectedResponse.NumberOfPoints, response.NumberOfPoints);
    }

    /// <summary>Creates the modbus message write multiple coils request.</summary>
    [TUnit.Core.Test]
    public void CreateModbusMessageWriteMultipleCoilsRequest()
    {
        var request =
            ModbusMessageFactory.CreateModbusMessage(new WriteMultipleCoilsRequest(), [
                Num.Value17, Modbus.WriteMultipleCoils, 0, Num.Value19, 0, Num.Value10, Num.Value2, Num.Value205, 1,
            ]);

        var expectedRequest = new WriteMultipleCoilsRequest(
            Num.Value17,
            Num.Value19,
            new DiscreteCollection(
                true,
                false,
                true,
                true,
                false,
                false,
                true,
                true,
                true,
                false));

        ModbusMessageFixture.AssertModbusMessagePropertiesAreEqual(expectedRequest, request);

        Assert.Equal(expectedRequest.StartAddress, request.StartAddress);
        Assert.Equal(expectedRequest.NumberOfPoints, request.NumberOfPoints);
        Assert.Equal(expectedRequest.ByteCount, request.ByteCount);
        Assert.Equal(expectedRequest.Data.NetworkBytes, request.Data.NetworkBytes);
    }

    /// <summary>Creates the size of the modbus message write multiple coils request with invalid frame.</summary>
    [TUnit.Core.Test]
    public void CreateModbusMessageWriteMultipleCoilsRequestWithInvalidFrameSize() =>
        Assert.Throws<FormatException>(() =>
            ModbusMessageFactory.CreateModbusMessage(
                new WriteMultipleCoilsRequest(),
                [Num.Value17, Modbus.WriteMultipleCoils, 0, Num.Value19, 0, Num.Value10, Num.Value2]));

    /// <summary>Creates the modbus message write multiple coils response.</summary>
    [TUnit.Core.Test]
    public void CreateModbusMessageWriteMultipleCoilsResponse()
    {
        var response =
            ModbusMessageFactory.CreateModbusMessage(new WriteMultipleCoilsResponse(), [
                Num.Value17, Modbus.WriteMultipleCoils, 0, Num.Value19, 0, Num.Value10,
            ]);
        var expectedResponse = new WriteMultipleCoilsResponse(Num.Value17, Num.Value19, Num.Value10);
        ModbusMessageFixture.AssertModbusMessagePropertiesAreEqual(expectedResponse, response);

        Assert.Equal(expectedResponse.StartAddress, response.StartAddress);
        Assert.Equal(expectedResponse.NumberOfPoints, response.NumberOfPoints);
    }

    /// <summary>Creates the size of the modbus message write multiple coils response with invalid frame.</summary>
    [TUnit.Core.Test]
    public void CreateModbusMessageWriteMultipleCoilsResponseWithInvalidFrameSize() =>
        Assert.Throws<FormatException>(() =>
            ModbusMessageFactory.CreateModbusMessage(
                new WriteMultipleCoilsResponse(),
                [Num.Value17, Modbus.WriteMultipleCoils, 0, Num.Value19, 0]));

    /// <summary>Creates the modbus message read write multiple registers request.</summary>
    [TUnit.Core.Test]
    public void CreateModbusMessageReadWriteMultipleRegistersRequest()
    {
        var request =
            ModbusMessageFactory.CreateModbusMessage(new ReadWriteMultipleRegistersRequest(), [
                0x05, 0x17, 0x00, 0x03, 0x00, 0x06, 0x00, 0x0e, 0x00, 0x03, 0x06, 0x00, 0xff, 0x00, 0xff, 0x00, 0xff,
            ]);
        var writeCollection = new RegisterCollection(Num.Value255, Num.Value255, Num.Value255);
        var expectedRequest = new ReadWriteMultipleRegistersRequest(
            Num.Value5,
            Num.Value3,
            Num.Value6,
            Num.Value14,
            writeCollection);
        ModbusMessageFixture.AssertModbusMessagePropertiesAreEqual(expectedRequest, request);
    }

    /// <summary>Creates an invalid read-write multiple-register request frame.</summary>
    [TUnit.Core.Test]
    public void CreateModbusMessageReadWriteMultipleRegistersRequestWithInvalidFrameSize()
    {
        byte[] frame = { 17, Modbus.ReadWriteMultipleRegisters, 1, 2, 3 };
        _ = Assert.Throws<FormatException>(() =>
            ModbusMessageFactory.CreateModbusMessage(new ReadWriteMultipleRegistersRequest(), frame));
    }

    /// <summary>Creates the modbus message return query data request response.</summary>
    [TUnit.Core.Test]
    public void CreateModbusMessageReturnQueryDataRequestResponse()
    {
        const byte slaveAddress = 5;
        var data = new RegisterCollection(Num.Value50);
        var networkBytes = data.NetworkBytes;
        var frame = new byte[networkBytes.Length + Num.Value4];
        frame[0] = slaveAddress;
        frame[1] = Num.Value8;
        frame[2] = 0;
        frame[3] = 0;
        Array.Copy(networkBytes, 0, frame, Num.Value4, networkBytes.Length);
        var message =
            ModbusMessageFactory.CreateModbusMessage(new DiagnosticsRequestResponse(), frame);
        var expectedMessage = new DiagnosticsRequestResponse(Modbus.DiagnosticsReturnQueryData, slaveAddress, data);

        Assert.Equal(expectedMessage.SubFunctionCode, message.SubFunctionCode);
        ModbusMessageFixture.AssertModbusMessagePropertiesAreEqual(expectedMessage, message);
    }

    /// <summary>Creates the modbus message return query data request response too small.</summary>
    [TUnit.Core.Test]
    public void CreateModbusMessageReturnQueryDataRequestResponseTooSmall()
    {
        var frame = new byte[] { 5, 8, 0, 0, 5 };
        _ = Assert.Throws<FormatException>(() =>
            ModbusMessageFactory.CreateModbusMessage(new DiagnosticsRequestResponse(), frame));
    }

    /// <summary>Creates the modbus request with invalid message frame.</summary>
    [TUnit.Core.Test]
    public void CreateModbusRequestWithInvalidMessageFrame() =>
        Assert.Throws<FormatException>(() => ModbusMessageFactory.CreateModbusRequest([ 0, 1]));

    /// <summary>Creates the modbus request with invalid function code.</summary>
    [TUnit.Core.Test]
    public void CreateModbusRequestWithInvalidFunctionCode() =>
        Assert.Throws<ArgumentException>(() =>
            ModbusMessageFactory.CreateModbusRequest([1, Num.Value99, 0, 0, 0, 1, Num.Value23]));

    /// <summary>Creates the modbus request for read coils.</summary>
    [TUnit.Core.Test]
    public void CreateModbusRequestForReadCoils()
    {
        var req = new ReadCoilsInputsRequest(1, Num.Value2, 1, Num.Value10);
        var request = ModbusMessageFactory.CreateModbusRequest(req.MessageFrame);
        Assert.Equal(typeof(ReadCoilsInputsRequest), request.GetType());
    }

    /// <summary>Creates the modbus request for diagnostics.</summary>
    [TUnit.Core.Test]
    public void CreateModbusRequestForDiagnostics()
    {
        var diagnosticsRequest = new DiagnosticsRequestResponse(0, Num.Value2, new RegisterCollection(Num.Value45));
        var request = ModbusMessageFactory.CreateModbusRequest(diagnosticsRequest.MessageFrame);
        Assert.Equal(typeof(DiagnosticsRequestResponse), request.GetType());
    }
}
