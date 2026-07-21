// Copyright (c) 2019-2026 Chris Pulman and contributors. All rights reserved.
// Chris Pulman and contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using ModbusRx.Data;
using ModbusRx.Message;

namespace ModbusRx.UnitTests.Message;

/// <summary>Tests the ReturnQueryDataRequestResponseFixture behavior.</summary>
public class ReturnQueryDataRequestResponseFixture
{
    /// <summary>Returns the query data request response.</summary>
    [TUnit.Core.Test]
    public void ReturnQueryDataRequestResponse()
    {
        var data = new RegisterCollection(1, Num.Value2, Num.Value3, Num.Value4);
        var request = new DiagnosticsRequestResponse(Modbus.DiagnosticsReturnQueryData, Num.Value5, data);
        Assert.Equal(Modbus.Diagnostics, request.FunctionCode);
        Assert.Equal(Modbus.DiagnosticsReturnQueryData, request.SubFunctionCode);
        Assert.Equal(Num.Value5, request.SlaveAddress);
        Assert.Equal(data.NetworkBytes, request.Data.NetworkBytes);
    }

    /// <summary>Protocols the data unit.</summary>
    [TUnit.Core.Test]
    public void ProtocolDataUnit()
    {
        var data = new RegisterCollection(1, Num.Value2, Num.Value3, Num.Value4);
        var request = new DiagnosticsRequestResponse(Modbus.DiagnosticsReturnQueryData, Num.Value5, data);
        Assert.Equal([ Num.Value8, 0, 0, 0, 1, 0, Num.Value2, 0, Num.Value3, 0, Num.Value4], request.ProtocolDataUnit);
    }
}
