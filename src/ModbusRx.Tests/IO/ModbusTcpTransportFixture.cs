// Copyright (c) 2019-2026 Chris Pulman and contributors. All rights reserved.
// Chris Pulman and contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System;
using System.IO;
using System.Threading.Tasks;
using IoT.DriverCore.ModbusRx.Data;
using IoT.DriverCore.ModbusRx.IO;
using IoT.DriverCore.ModbusRx.Message;
using IoT.DriverCore.ModbusRx.UnitTests.Message;
using Moq;

namespace IoT.DriverCore.ModbusRx.UnitTests.IO;

/// <summary>Tests the ModbusTcpTransportFixture behavior.</summary>
public class ModbusTcpTransportFixture
{
    /// <summary>Gets the stream resource mock.</summary>
    /// <value>
    /// The stream resource mock.
    /// </value>
    private static IStreamResource StreamResourceMock => new Mock<IStreamResource>().Object;

    /// <summary>Builds the message frame.</summary>
    [TUnit.Core.Test]
    public void BuildMessageFrame()
    {
        using var transport = new ModbusIpTransport(StreamResourceMock);
        var message = new ReadCoilsInputsRequest(Modbus.ReadCoils, Num.Value2, Num.Value10, Num.Value5);

        var result = transport.BuildMessageFrame(message);
        Assert.Equal([ 0, 0, 0, 0, 0, Num.Value6, Num.Value2, 1, 0, Num.Value10, 0, Num.Value5], result);
    }

    /// <summary>Gets the mbap header.</summary>
    [TUnit.Core.Test]
    public void GetMbapHeader()
    {
        var registers = MessageUtility.CreateDefaultCollection(
            new RegisterCollection(),
            (ushort)0,
            Num.Value120);
        var message = new WriteMultipleRegistersRequest(Num.Value3, 1, registers);
        message.TransactionId = Num.Value45;
        Assert.Equal([ 0, Num.Value45, 0, 0, 0, Num.Value247, Num.Value3], ModbusIpTransport.GetMbapHeader(message));
    }

    /// <summary>Writes this instance.</summary>
    [TUnit.Core.Test]
    public void Write()
    {
        var streamMock = new Mock<IStreamResource>();
        using var transport = new ModbusIpTransport(streamMock.Object);
        var request = new ReadCoilsInputsRequest(Modbus.ReadCoils, 1, 1, Num.Value3);

        _ = streamMock.Setup(s => s.Write(It.IsNotNull<byte[]>(), 0, Num.Value12));

        transport.Write(request);

        Assert.Equal(1, request.TransactionId);

        streamMock.VerifyAll();
    }

    /// <summary>Reads the request response.</summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous unit test.</returns>
    [TUnit.Core.Test]
    public async Task ReadRequestResponseAsync()
    {
        var mock = new Mock<IStreamResource>(MockBehavior.Strict);
        var request = new ReadCoilsInputsRequest(Modbus.ReadCoils, 1, 1, Num.Value3);
        var calls = 0;
        var unitAndPdu = new byte[request.ProtocolDataUnit.Length + 1];
        unitAndPdu[0] = 1;
        Array.Copy(request.ProtocolDataUnit, 0, unitAndPdu, 1, request.ProtocolDataUnit.Length);
        byte[][] source =
        {
                new byte[] { Num.Value45, Num.Value63, 0, 0, 0, Num.Value6 },
                unitAndPdu,
        };

        _ = mock.Setup(s => s.ReadAsync(It.Is<byte[]>(x => x.Length == 6), 0, Num.Value6).Result)
            .Returns((byte[] buf, int offset, int count) =>
            {
                Array.Copy(source[calls], buf, Num.Value6);
                calls++;
                return Num.Value6;
            });

        Assert.Equal(
            [ Num.Value45, Num.Value63, 0, 0, 0, Num.Value6, 1, 1, 0, 1, 0, Num.Value3],
            await ModbusIpTransport.ReadRequestResponseAsync(mock.Object));

        mock.VerifyAll();
    }

    /// <summary>Reads the request response connection aborted while reading mbap header.</summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous unit test.</returns>
    [TUnit.Core.Test]
    public async Task ReadRequestResponse_ConnectionAbortedWhileReadingMBAPHeaderAsync()
    {
        var mock = new Mock<IStreamResource>(MockBehavior.Strict);
        _ = mock.Setup(s => s.ReadAsync(It.Is<byte[]>(x => x.Length == 6), 0, Num.Value6).Result).Returns(Num.Value3);
        _ = mock.Setup(s => s.ReadAsync(It.Is<byte[]>(x => x.Length == 6), Num.Value3, Num.Value3).Result).Returns(0);

        await Assert.ThrowsAsync<IOException>(() => ModbusIpTransport.ReadRequestResponseAsync(mock.Object));
        mock.VerifyAll();
    }

    /// <summary>Reads the request response connection aborted while reading message frame.</summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous unit test.</returns>
    [TUnit.Core.Test]
    public async Task ReadRequestResponse_ConnectionAbortedWhileReadingMessageFrameAsync()
    {
        var mock = new Mock<IStreamResource>(MockBehavior.Strict);

        _ = mock.Setup(s => s.ReadAsync(It.Is<byte[]>(x => x.Length == 6), 0, Num.Value6).Result).Returns(Num.Value6);
        _ = mock.Setup(s => s.ReadAsync(It.Is<byte[]>(x => x.Length == 6), 0, Num.Value6).Result).Returns(Num.Value3);
        _ = mock.Setup(s => s.ReadAsync(It.Is<byte[]>(x => x.Length == 6), Num.Value3, Num.Value3).Result).Returns(0);

        await Assert.ThrowsAsync<IOException>(() => ModbusIpTransport.ReadRequestResponseAsync(mock.Object));
        mock.VerifyAll();
    }

    /// <summary>Gets the new transaction identifier.</summary>
    [TUnit.Core.Test]
    public void GetNewTransactionId()
    {
        using var transport = new ModbusIpTransport(StreamResourceMock);

        Assert.Equal(1, transport.GetNewTransactionId());
        Assert.Equal(Num.Value2, transport.GetNewTransactionId());
    }

    /// <summary>Called when [should retry response returns true if within threshold].</summary>
    [TUnit.Core.Test]
    public void OnShouldRetryResponse_ReturnsTrue_IfWithinThreshold()
    {
        using var transport = new ModbusIpTransport(StreamResourceMock);
        var request = new ReadCoilsInputsRequest(Modbus.ReadCoils, 1, 1, 1);
        var response = new ReadCoilsInputsResponse(Modbus.ReadCoils, 1, 1, null!);

        request.TransactionId = Num.Value5;
        response.TransactionId = Num.Value4;
        transport.RetryOnOldResponseThreshold = Num.Value3;

        Assert.True(transport.OnShouldRetryResponse(request, response));
    }

    /// <summary>Called when [should retry response returns false if threshold disabled].</summary>
    [TUnit.Core.Test]
    public void OnShouldRetryResponse_ReturnsFalse_IfThresholdDisabled()
    {
        using var transport = new ModbusIpTransport(StreamResourceMock);
        var request = new ReadCoilsInputsRequest(Modbus.ReadCoils, 1, 1, 1);
        var response = new ReadCoilsInputsResponse(Modbus.ReadCoils, 1, 1, null!);

        request.TransactionId = Num.Value5;
        response.TransactionId = Num.Value4;
        transport.RetryOnOldResponseThreshold = 0;

        Assert.False(transport.OnShouldRetryResponse(request, response));
    }

    /// <summary>Called when [should retry response returns false if equal transaction identifier].</summary>
    [TUnit.Core.Test]
    public void OnShouldRetryResponse_ReturnsFalse_IfEqualTransactionId()
    {
        using var transport = new ModbusIpTransport(StreamResourceMock);
        var request = new ReadCoilsInputsRequest(Modbus.ReadCoils, 1, 1, 1);
        var response = new ReadCoilsInputsResponse(Modbus.ReadCoils, 1, 1, null!);

        request.TransactionId = Num.Value5;
        response.TransactionId = Num.Value5;
        transport.RetryOnOldResponseThreshold = Num.Value3;

        Assert.False(transport.OnShouldRetryResponse(request, response));
    }

    /// <summary>Called when [should retry response returns false if outside threshold].</summary>
    [TUnit.Core.Test]
    public void OnShouldRetryResponse_ReturnsFalse_IfOutsideThreshold()
    {
        using var transport = new ModbusIpTransport(StreamResourceMock);
        var request = new ReadCoilsInputsRequest(Modbus.ReadCoils, 1, 1, 1);
        var response = new ReadCoilsInputsResponse(Modbus.ReadCoils, 1, 1, null!);

        request.TransactionId = Num.Value5;
        response.TransactionId = Num.Value2;
        transport.RetryOnOldResponseThreshold = Num.Value3;

        Assert.False(transport.OnShouldRetryResponse(request, response));
    }

    /// <summary>Validates the response mismatching transaction ids.</summary>
    [TUnit.Core.Test]
    public void ValidateResponse_MismatchingTransactionIds()
    {
        using var transport = new ModbusIpTransport(StreamResourceMock);

        var request = new ReadCoilsInputsRequest(Modbus.ReadCoils, 1, 1, 1);
        request.TransactionId = Num.Value5;
        var response = new ReadCoilsInputsResponse(Modbus.ReadCoils, 1, 1, null!);
        response.TransactionId = Num.Value6;

        _ = Assert.Throws<IOException>(() => transport.ValidateResponse(request, response));
    }

    /// <summary>Validates the response.</summary>
    [TUnit.Core.Test]
    public void ValidateResponse()
    {
        using var transport = new ModbusIpTransport(StreamResourceMock);

        var request = new ReadCoilsInputsRequest(Modbus.ReadCoils, 1, 1, 1);
        request.TransactionId = Num.Value5;
        var response = new ReadCoilsInputsResponse(Modbus.ReadCoils, 1, 1, null!);
        response.TransactionId = Num.Value5;

        // no exception is thrown
        transport.ValidateResponse(request, response);
    }
}
