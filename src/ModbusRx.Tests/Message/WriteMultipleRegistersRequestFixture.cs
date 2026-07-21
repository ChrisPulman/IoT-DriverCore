// Copyright (c) 2019-2026 Chris Pulman and contributors. All rights reserved.
// Chris Pulman and contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System;
using ModbusRx.Data;
using ModbusRx.Message;

namespace ModbusRx.UnitTests.Message;

/// <summary>Tests the WriteMultipleRegistersRequestFixture behavior.</summary>
public class WriteMultipleRegistersRequestFixture
{
    /// <summary>Creates the write multiple registers request fixture.</summary>
    [TUnit.Core.Test]
    public void CreateWriteMultipleRegistersRequestFixture()
    {
        var col = new RegisterCollection(Num.Value10, Num.Value20, Num.Value30, Num.Value40, Num.Value50);
        var request = new WriteMultipleRegistersRequest(Num.Value11, Num.Value34, col);

        Assert.Equal(Modbus.WriteMultipleRegisters, request.FunctionCode);
        Assert.Equal(Num.Value11, request.SlaveAddress);
        Assert.Equal(Num.Value34, request.StartAddress);
        Assert.Equal(Num.Value10, request.ByteCount);
        Assert.Equal(col.NetworkBytes, request.Data.NetworkBytes);
    }

    /// <summary>Creates the write multiple registers request too much data.</summary>
    [TUnit.Core.Test]
    public void CreateWriteMultipleRegistersRequestTooMuchData() =>
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            _ = new WriteMultipleRegistersRequest(
                1,
                Num.Value2,
                MessageUtility.CreateDefaultCollection(
                    new RegisterCollection(),
                    (ushort)Num.Value3,
                    Modbus.MaximumRegisterRequestResponseSize + 1)));

    /// <summary>Creates the maximum size of the write multiple registers request.</summary>
    [TUnit.Core.Test]
    public void CreateWriteMultipleRegistersRequestMaxSize()
    {
        var request = new WriteMultipleRegistersRequest(
            1,
            Num.Value2,
            MessageUtility.CreateDefaultCollection(
                new RegisterCollection(),
                (ushort)Num.Value3,
                Modbus.MaximumRegisterRequestResponseSize));

        Assert.Equal(Modbus.MaximumRegisterRequestResponseSize, request.NumberOfPoints);
    }

    /// <summary>Converts to string_writemultipleregistersrequest.</summary>
    [TUnit.Core.Test]
    public void ToString_WriteMultipleRegistersRequest()
    {
        var col = new RegisterCollection(Num.Value10, Num.Value20, Num.Value30, Num.Value40, Num.Value50);
        var request = new WriteMultipleRegistersRequest(Num.Value11, Num.Value34, col);

        Assert.Equal("Write 5 holding registers starting at address 34.", request.ToString());
    }
}
