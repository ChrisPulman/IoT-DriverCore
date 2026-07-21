// Copyright (c) 2019-2026 Chris Pulman and contributors. All rights reserved.
// Chris Pulman and contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System;
using ModbusRx.Data;
using ModbusRx.Message;

namespace ModbusRx.UnitTests.Message;

/// <summary>Tests the WriteMultipleCoilsRequestFixture behavior.</summary>
public class WriteMultipleCoilsRequestFixture
{
    /// <summary>Creates the write multiple coils request.</summary>
    [TUnit.Core.Test]
    public void CreateWriteMultipleCoilsRequest()
    {
        var col = new DiscreteCollection(true, false, true, false, true, true, true, false, false);
        var request = new WriteMultipleCoilsRequest(Num.Value34, Num.Value45, col);
        Assert.Equal(Modbus.WriteMultipleCoils, request.FunctionCode);
        Assert.Equal(Num.Value34, request.SlaveAddress);
        Assert.Equal(Num.Value45, request.StartAddress);
        Assert.Equal(Num.Value9, request.NumberOfPoints);
        Assert.Equal(Num.Value2, request.ByteCount);
        Assert.Equal(col.NetworkBytes, request.Data.NetworkBytes);
    }

    /// <summary>Creates the write multiple coils request too much data.</summary>
    [TUnit.Core.Test]
    public void CreateWriteMultipleCoilsRequestTooMuchData() =>
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            _ = new WriteMultipleCoilsRequest(
                1,
                Num.Value2,
                MessageUtility.CreateDefaultCollection(
                    new DiscreteCollection(),
                    true,
                    Modbus.MaximumDiscreteRequestResponseSize + 1)));

    /// <summary>Creates the maximum size of the write multiple coils request.</summary>
    [TUnit.Core.Test]
    public void CreateWriteMultipleCoilsRequestMaxSize()
    {
        var request = new WriteMultipleCoilsRequest(
            1,
            Num.Value2,
            MessageUtility.CreateDefaultCollection(
                new DiscreteCollection(),
                true,
                Modbus.MaximumDiscreteRequestResponseSize));

        Assert.Equal(Modbus.MaximumDiscreteRequestResponseSize, request.Data.Count);
    }

    /// <summary>Converts to string_writemultiplecoilsrequest.</summary>
    [TUnit.Core.Test]
    public void ToString_WriteMultipleCoilsRequest()
    {
        var col = new DiscreteCollection(true, false, true, false, true, true, true, false, false);
        var request = new WriteMultipleCoilsRequest(Num.Value34, Num.Value45, col);

        Assert.Equal("Write 9 coils starting at address 45.", request.ToString());
    }
}
