// Copyright (c) 2019-2026 Chris Pulman and contributors. All rights reserved.
// Chris Pulman and contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using IoT.DriverCore.ModbusRx.Message;

namespace IoT.DriverCore.ModbusRx.UnitTests.Message;

/// <summary>Tests the SlaveExceptionResponseFixture behavior.</summary>
public class SlaveExceptionResponseFixture
{
    /// <summary>Creates the slave exception response.</summary>
    [TUnit.Core.Test]
    public void CreateSlaveExceptionResponse()
    {
        var response = new SlaveExceptionResponse(Num.Value11, Modbus.ReadCoils + Modbus.ExceptionOffset, Num.Value2);
        Assert.Equal(Num.Value11, response.SlaveAddress);
        Assert.Equal(Modbus.ReadCoils + Modbus.ExceptionOffset, response.FunctionCode);
        Assert.Equal(Num.Value2, response.SlaveExceptionCode);
    }

    /// <summary>Slaves the exception response pdu.</summary>
    [TUnit.Core.Test]
    public void SlaveExceptionResponsePDU()
    {
        var response = new SlaveExceptionResponse(Num.Value11, Modbus.ReadCoils + Modbus.ExceptionOffset, Num.Value2);
        Assert.Equal([ response.FunctionCode, response.SlaveExceptionCode], response.ProtocolDataUnit);
    }
}
