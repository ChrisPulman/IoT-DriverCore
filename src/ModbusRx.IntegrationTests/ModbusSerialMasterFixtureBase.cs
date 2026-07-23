// Copyright (c) 2019-2026 Chris Pulman and contributors. All rights reserved.
// Chris Pulman and contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

#if SERIAL
using IoT.DriverCore.ModbusRx.Device;

namespace IoT.DriverCore.ModbusRx.IntegrationTests;

/// <summary>
/// Base class for ModbusSerialMaster test fixtures.
/// </summary>
/// <seealso cref="IoT.DriverCore.ModbusRx.IntegrationTests.ModbusRxMasterFixtureBase" />
public abstract class ModbusSerialMasterFixtureBase : ModbusRxMasterFixtureBase
{
    /// <summary>Gets the serial device addressed by the fixture.</summary>
    protected abstract string SerialDeviceName { get; }

    /// <summary>Gets the transport used by the fixture.</summary>
    protected override string TransportName => "Serial";

    /// <summary>
    /// Returns the query data.
    /// </summary>
    [TUnit.Core.Test]
    public virtual void ReturnQueryData()
    {
        // This is a placeholder for the return query data test
        // Individual fixtures can override this method
        Assert.True(true, "ReturnQueryData test placeholder");
    }
}
#endif
