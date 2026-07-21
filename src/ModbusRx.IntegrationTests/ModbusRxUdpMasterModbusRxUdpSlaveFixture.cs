// Copyright (c) 2022-2026 Chris Pulman. All rights reserved.
// Chris Pulman licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System;
using System.Net;
using System.Threading.Tasks;
using ModbusRx.Device;

namespace ModbusRx.IntegrationTests;

/// <summary>Tests the NModbusUdpMasterNModbusUdpSlaveFixture behavior.</summary>
/// <seealso cref="ModbusRxMasterFixtureBase" />
[TUnit.Core.InheritsTests]
public class ModbusRxUdpMasterModbusRxUdpSlaveFixture : ModbusRxMasterFixtureBase
{
    /// <summary>The delay used to let the slave begin listening.</summary>
    private const int SlaveStartupDelayMilliseconds = 200;

    /// <summary>Initializes a new instance of the <see cref="ModbusRxUdpMasterModbusRxUdpSlaveFixture"/>.</summary>
    public ModbusRxUdpMasterModbusRxUdpSlaveFixture() => InitializeAsync().GetAwaiter().GetResult();

    /// <summary>Gets the transport used by this fixture.</summary>
    protected override string TransportName => "UDP";

    /// <summary>Initializes the UDP connections asynchronously with CI-safe port allocation.</summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
    private async Task InitializeAsync()
    {
        // Use dynamic port allocation to avoid conflicts in CI
        var port = await GetAvailablePortAsync();

        SlaveUdp = new(port);
        Slave = ModbusUdpSlave.CreateUdp(SlaveUdp);
        StartSlave();

        // Give slave time to start listening
        await Task.Delay(
            GetEnvironmentAppropriateTimeout(TimeSpan.FromMilliseconds(SlaveStartupDelayMilliseconds)),
            CancellationToken);

        MasterUdp = new();
        var endPoint = new IPEndPoint(TcpHost, port);
        MasterUdp.Connect(endPoint);
        Master = ModbusIpMaster.CreateIp(MasterUdp);
    }
}
