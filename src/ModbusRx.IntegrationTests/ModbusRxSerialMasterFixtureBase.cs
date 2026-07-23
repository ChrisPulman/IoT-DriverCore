// Copyright (c) 2019-2026 Chris Pulman and contributors. All rights reserved.
// Chris Pulman and contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using IoT.DriverCore.ModbusRx.Device;

namespace IoT.DriverCore.ModbusRx.IntegrationTests;

/// <summary>Tests the ModbusSerialMasterFixture behavior.</summary>
/// <seealso cref="ModbusRxMasterFixtureBase" />
[TUnit.Core.InheritsTests]
public abstract class ModbusRxSerialMasterFixtureBase : ModbusRxMasterFixtureBase
{
    /// <summary>Gets the serial device addressed by the fixture.</summary>
    protected abstract string SerialDeviceName { get; }

    /// <summary>Gets the transport used by the fixture.</summary>
    protected override string TransportName => "Serial";

    /// <summary>Returns the query data.</summary>
    [TUnit.Core.Test]
    public virtual void ReturnQueryData()
    {
        const ushort firstQueryAddress = 18;
        const ushort secondQueryAddress = 5;

        Assert.True(((ModbusSerialMaster)Master!).ReturnQueryData(SlaveAddress, firstQueryAddress));
        Assert.True(((ModbusSerialMaster)Master).ReturnQueryData(SlaveAddress, secondQueryAddress));
    }
}
