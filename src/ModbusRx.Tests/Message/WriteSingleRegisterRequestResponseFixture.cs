// Copyright (c) 2019-2026 Chris Pulman and contributors. All rights reserved.
// Chris Pulman and contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using IoT.DriverCore.ModbusRx.Message;

namespace IoT.DriverCore.ModbusRx.UnitTests.Message;

/// <summary>Tests the WriteSingleRegisterRequestResponseFixture behavior.</summary>
public class WriteSingleRegisterRequestResponseFixture
{
    /// <summary>Creates new writesingleregisterrequestresponse.</summary>
    [TUnit.Core.Test]
    public void NewWriteSingleRegisterRequestResponse()
    {
        var message = new WriteSingleRegisterRequestResponse(Num.Value12, Num.Value5, Num.Value1200);
        Assert.Equal(Num.Value12, message.SlaveAddress);
        Assert.Equal(Num.Value5, message.StartAddress);
        _ = Assert.Single(message.Data);
        Assert.Equal(Num.Value1200, message.Data[0]);
    }

    /// <summary>Converts to stringoverride.</summary>
    [TUnit.Core.Test]
    public void ToStringOverride()
    {
        var message = new WriteSingleRegisterRequestResponse(Num.Value12, Num.Value5, Num.Value1200);
        Assert.Equal("Write single holding register 1200 at address 5.", message.ToString());
    }
}
