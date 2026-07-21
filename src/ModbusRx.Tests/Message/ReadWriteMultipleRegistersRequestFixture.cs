// Copyright (c) 2019-2026 Chris Pulman and contributors. All rights reserved.
// Chris Pulman and contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using ModbusRx.Data;
using ModbusRx.Message;

namespace ModbusRx.UnitTests.Message;

/// <summary>Tests the ReadWriteMultipleRegistersRequestFixture behavior.</summary>
public class ReadWriteMultipleRegistersRequestFixture
{
    /// <summary>Reads the write multiple registers request.</summary>
    [TUnit.Core.Test]
    public void ReadWriteMultipleRegistersRequest()
    {
        var writeCollection = new RegisterCollection(Num.Value255, Num.Value255, Num.Value255);
        var request = new ReadWriteMultipleRegistersRequest(
            Num.Value5,
            Num.Value3,
            Num.Value6,
            Num.Value14,
            writeCollection);
        Assert.Equal(Modbus.ReadWriteMultipleRegisters, request.FunctionCode);
        Assert.Equal(Num.Value5, request.SlaveAddress);

        // test read
        _ = Assert.NotNull(request.ReadRequest);
        Assert.Equal(request.SlaveAddress, request.ReadRequest!.SlaveAddress);
        Assert.Equal(Num.Value3, request.ReadRequest.StartAddress);
        Assert.Equal(Num.Value6, request.ReadRequest.NumberOfPoints);

        // test write
        _ = Assert.NotNull(request.WriteRequest);
        Assert.Equal(request.SlaveAddress, request.WriteRequest!.SlaveAddress);
        Assert.Equal(Num.Value14, request.WriteRequest.StartAddress);
        Assert.Equal(writeCollection.NetworkBytes, request.WriteRequest.Data.NetworkBytes);
    }

    /// <summary>Protocols the data unit.</summary>
    [TUnit.Core.Test]
    public void ProtocolDataUnit()
    {
        var writeCollection = new RegisterCollection(Num.Value255, Num.Value255, Num.Value255);
        var request = new ReadWriteMultipleRegistersRequest(
            Num.Value5,
            Num.Value3,
            Num.Value6,
            Num.Value14,
            writeCollection);
        byte[] pdu =
        {
                0x17, 0x00, 0x03, 0x00, 0x06, 0x00, 0x0e, 0x00, 0x03, 0x06, 0x00, 0xff, 0x00, 0xff, 0x00, 0xff,
        };
        Assert.Equal(pdu, request.ProtocolDataUnit);
    }

    /// <summary>Converts to string_readwritemultipleregistersrequest.</summary>
    [TUnit.Core.Test]
    public void ToString_ReadWriteMultipleRegistersRequest()
    {
        var writeCollection = new RegisterCollection(Num.Value255, Num.Value255, Num.Value255);
        var request = new ReadWriteMultipleRegistersRequest(
            Num.Value5,
            Num.Value3,
            Num.Value6,
            Num.Value14,
            writeCollection);

        Assert.Equal(
            "Write 3 holding registers starting at address 14, and read 6 registers starting at address 3.",
            request.ToString());
    }
}
