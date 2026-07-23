// Copyright (c) 2019-2026 Chris Pulman and contributors. All rights reserved.
// Chris Pulman and contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

#if JAMOD
using System.Threading.Tasks;
using IoT.DriverCore.ModbusRx.Device;

namespace IoT.DriverCore.ModbusRx.IntegrationTests;

/// <summary>Tests TCP master interoperability with a Jamod TCP slave.</summary>
/// <seealso cref="ModbusRxMasterFixtureBase" />
public class ModbusRxTcpMasterJamodTcpSlaveFixture : ModbusRxMasterFixtureBase
{
    /// <summary>Initializes a new instance of the <see cref="ModbusRxTcpMasterJamodTcpSlaveFixture"/> class.</summary>
    public ModbusRxTcpMasterJamodTcpSlaveFixture()
    {
        var program = $"TcpSlave {Port}";
        StartJamodSlave(program);

        MasterTcp = new TcpClientRx(TcpHost.ToString(), Port);
        Master = ModbusIpMaster.CreateIp(MasterTcp);
    }

    /// <summary>Gets the transport used by this fixture.</summary>
    protected override string TransportName => "TCP";

    /// <summary>
    /// Not supported by the Jamod Slave.
    /// </summary>
    public override Task ReadWriteMultipleRegistersAsync() => Task.CompletedTask;
}
#endif
