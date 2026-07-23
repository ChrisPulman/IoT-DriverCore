// Copyright (c) 2019-2026 Chris Pulman and contributors. All rights reserved.
// Chris Pulman and contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

#if SERIAL
using System.Threading.Tasks;
using IoT.DriverCore.ModbusRx.Device;

namespace IoT.DriverCore.ModbusRx.IntegrationTests;

/// <summary>
/// NModbusSerialRtuMasterNModbusSerialRtuSlaveFixture.
/// </summary>
/// <seealso cref="IoT.DriverCore.ModbusRx.IntegrationTests.ModbusSerialMasterFixtureBase" />
public class NModbusSerialRtuMasterNModbusSerialRtuSlaveFixture : ModbusSerialMasterFixtureBase
{
    /// <summary>Gets the serial device addressed by the fixture.</summary>
    protected override string SerialDeviceName => "ModbusRx RTU";

    /// <summary>
    /// Initializes a new instance of the <see cref="NModbusSerialRtuMasterNModbusSerialRtuSlaveFixture"/> class.
    /// </summary>
    public NModbusSerialRtuMasterNModbusSerialRtuSlaveFixture()
    {
        SetupSlaveSerialPort();
        Slave = ModbusSerialSlave.CreateRtu(SlaveAddress, SlaveSerialPort!);
        StartSlave();

        MasterSerialPort = CreateAndOpenSerialPort(DefaultMasterSerialPortName);
        Master = ModbusSerialMaster.CreateRtu(MasterSerialPort);
    }

    /// <summary>
    /// Reads the coils.
    /// </summary>
    [TUnit.Core.Test]
    public override Task ReadCoilsAsync() =>
        base.ReadCoilsAsync();

    /// <summary>
    /// Reads the holding registers.
    /// </summary>
    [TUnit.Core.Test]
    public override Task ReadHoldingRegistersAsync() =>
        base.ReadHoldingRegistersAsync();

    /// <summary>
    /// Reads the inputs.
    /// </summary>
    [TUnit.Core.Test]
    public override Task ReadInputsAsync() =>
        base.ReadInputsAsync();

    /// <summary>
    /// Writes the single coil.
    /// </summary>
    [TUnit.Core.Test]
    public override Task WriteSingleCoilAsync() =>
        base.WriteSingleCoilAsync();

    /// <summary>
    /// Writes the multiple coils.
    /// </summary>
    [TUnit.Core.Test]
    public override Task WriteMultipleCoilsAsync() =>
        base.WriteMultipleCoilsAsync();

    /// <summary>
    /// Writes the single register.
    /// </summary>
    [TUnit.Core.Test]
    public override Task WriteSingleRegisterAsync() =>
        base.WriteSingleRegisterAsync();

    /// <summary>
    /// Writes the multiple registers.
    /// </summary>
    [TUnit.Core.Test]
    public override Task WriteMultipleRegistersAsync() =>
        base.WriteMultipleRegistersAsync();

    /// <summary>
    /// Reads the write multiple registers.
    /// </summary>
    [TUnit.Core.Test]
    public override Task ReadWriteMultipleRegistersAsync() =>
        base.ReadWriteMultipleRegistersAsync();

    /// <summary>
    /// Returns the query data.
    /// </summary>
    [TUnit.Core.Test]
    public override void ReturnQueryData() =>
        base.ReturnQueryData();
}
#endif
