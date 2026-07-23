// Copyright (c) 2019-2026 Chris Pulman and contributors. All rights reserved.
// Chris Pulman and contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

#if SERIAL
using System.Threading.Tasks;
using IoT.DriverCore.ModbusRx.Device;

namespace IoT.DriverCore.ModbusRx.IntegrationTests;

/// <summary>
/// ModbusRxSerialRtuMasterDl06SlaveFixture.
/// </summary>
/// <seealso cref="IoT.DriverCore.ModbusRx.IntegrationTests.ModbusSerialMasterFixtureBase" />
public class ModbusRxSerialRtuMasterDl06SlaveFixture : ModbusSerialMasterFixtureBase
{
    /// <summary>Gets the serial device addressed by the fixture.</summary>
    protected override string SerialDeviceName => "DL06";

    /// <summary>
    /// Initializes a new instance of the <see cref="ModbusRxSerialRtuMasterDl06SlaveFixture"/> class.
    /// </summary>
    public ModbusRxSerialRtuMasterDl06SlaveFixture()
    {
        MasterSerialPort = CreateAndOpenSerialPort("COM1");
        Master = ModbusSerialMaster.CreateRtu(MasterSerialPort);
    }

    /// <summary>
    /// Not supported by the DL06.
    /// </summary>
    public override Task ReadWriteMultipleRegistersAsync() =>
        Task.CompletedTask;

    /// <summary>
    /// Not supported by the DL06.
    /// </summary>
    public override void ReturnQueryData()
    {
    }

    /// <summary>
    /// Reads the coils.
    /// </summary>
    [TUnit.Core.Test]
    public override Task ReadCoilsAsync() =>
        base.ReadCoilsAsync();
}
#endif
