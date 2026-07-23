// Copyright (c) 2019-2026 Chris Pulman and contributors. All rights reserved.
// Chris Pulman and contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using IoT.DriverCore.ModbusRx.Message;

namespace IoT.DriverCore.ModbusRx.UnitTests.Message;

/// <summary>Tests the WriteSingleCoilRequestResponseFixture behavior.</summary>
public class WriteSingleCoilRequestResponseFixture
{
    /// <summary>Creates new writesinglecoilrequestresponse.</summary>
    [TUnit.Core.Test]
    public void NewWriteSingleCoilRequestResponse()
    {
        var request = new WriteSingleCoilRequestResponse(Num.Value11, Num.Value5, true);
        Assert.Equal(Num.Value11, request.SlaveAddress);
        Assert.Equal(Num.Value5, request.StartAddress);
        _ = Assert.Single(request.Data);
        Assert.Equal(Modbus.CoilOn, request.Data[0]);
    }

    /// <summary>Converts to string_true.</summary>
    [TUnit.Core.Test]
    public void ToString_True()
    {
        var request = new WriteSingleCoilRequestResponse(Num.Value11, Num.Value5, true);

        Assert.Equal("Write single coil 1 at address 5.", request.ToString());
    }

    /// <summary>Converts to string_false.</summary>
    [TUnit.Core.Test]
    public void ToString_False()
    {
        var request = new WriteSingleCoilRequestResponse(Num.Value11, Num.Value5, false);

        Assert.Equal("Write single coil 0 at address 5.", request.ToString());
    }
}
