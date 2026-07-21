// Copyright (c) 2019-2026 Chris Pulman and contributors. All rights reserved.
// Chris Pulman and contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System;
using ModbusRx.Message;

namespace ModbusRx.UnitTests.Message;

/// <summary>Tests the WriteMultipleCoilsResponseFixture behavior.</summary>
public class WriteMultipleCoilsResponseFixture
{
    /// <summary>Creates the write multiple coils response.</summary>
    [TUnit.Core.Test]
    public void CreateWriteMultipleCoilsResponse()
    {
        var response = new WriteMultipleCoilsResponse(Num.Value17, Num.Value19, Num.Value45);
        Assert.Equal(Modbus.WriteMultipleCoils, response.FunctionCode);
        Assert.Equal(Num.Value17, response.SlaveAddress);
        Assert.Equal(Num.Value19, response.StartAddress);
        Assert.Equal(Num.Value45, response.NumberOfPoints);
    }

    /// <summary>Creates the write multiple coils response too much data.</summary>
    [TUnit.Core.Test]
    public void CreateWriteMultipleCoilsResponseTooMuchData() =>
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            _ = new WriteMultipleCoilsResponse(
                1,
                Num.Value2,
                Modbus.MaximumDiscreteRequestResponseSize + 1));

    /// <summary>Creates the maximum size of the write multiple coils response.</summary>
    [TUnit.Core.Test]
    public void CreateWriteMultipleCoilsResponseMaxSize()
    {
        var response = new WriteMultipleCoilsResponse(1, Num.Value2, Modbus.MaximumDiscreteRequestResponseSize);
        Assert.Equal(Modbus.MaximumDiscreteRequestResponseSize, response.NumberOfPoints);
    }

    /// <summary>Converts to string_test.</summary>
    [TUnit.Core.Test]
    public void ToString_Test()
    {
        var response = new WriteMultipleCoilsResponse(1, Num.Value2, Num.Value3);

        Assert.Equal("Wrote 3 coils starting at address 2.", response.ToString());
    }
}
