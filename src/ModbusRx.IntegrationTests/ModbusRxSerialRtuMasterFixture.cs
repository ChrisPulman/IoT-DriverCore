// Copyright (c) 2019-2026 Chris Pulman and contributors. All rights reserved.
// Chris Pulman and contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System;
using System.Threading.Tasks;
using IoT.DriverCore.ModbusRx.Device;
using IoT.DriverCore.Serial;

namespace IoT.DriverCore.ModbusRx.IntegrationTests;

/// <summary>Tests the ModbusRxSerialRtuMasterFixture behavior.</summary>
public class ModbusRxSerialRtuMasterFixture : NetworkTestBase
{
    /// <summary>The intentionally unresponsive simulated slave address.</summary>
    private const byte SilentSlaveAddress = 100;

    /// <summary>The bounded timeout used by the simulated serial transport.</summary>
    private const int ReadTimeoutMilliseconds = 100;

    /// <summary>Tests the modbus RTU master read timeout.</summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous unit test.</returns>
    [TUnit.Core.Test]
    public async Task ModbusRxRtuMaster_ReadTimeoutAsync()
    {
        using var pair = new InMemoryPortRxPair("RTU-MASTER", "RTU-SILENT-PEER");
        pair.First.EnableAutoDataReceive = false;
        pair.Second.EnableAutoDataReceive = false;
        pair.First.ReadTimeout = ReadTimeoutMilliseconds;
        await pair.First.OpenAsync();
        await pair.Second.OpenAsync();

        using var master = ModbusSerialMaster.CreateRtu(pair.First);
        master.Transport!.ReadTimeout = ReadTimeoutMilliseconds;
        master.Transport.WriteTimeout = ReadTimeoutMilliseconds;
        master.Transport.Retries = 0;
        _ = await Assert.ThrowsAsync<TimeoutException>(
            () => master.ReadCoilsAsync(SilentSlaveAddress, 1, 1));
    }
}
