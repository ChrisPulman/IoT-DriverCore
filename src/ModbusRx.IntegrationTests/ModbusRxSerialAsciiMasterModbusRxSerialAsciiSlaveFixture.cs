// Copyright (c) 2022-2026 Chris Pulman. All rights reserved.
// Chris Pulman licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.
#if SERIAL
using System.Threading.Tasks;
using ModbusRx.Device;

namespace ModbusRx.IntegrationTests;

/// <summary>
/// ModbusRxSerialAsciiMasterNModbusSerialAsciiSlaveFixture.
/// </summary>
/// <seealso cref="ModbusRx.IntegrationTests.ModbusSerialMasterFixtureBase" />
public class ModbusRxSerialAsciiMasterModbusRxSerialAsciiSlaveFixture : ModbusSerialMasterFixtureBase
{
    /// <summary>Gets the serial device addressed by the fixture.</summary>
    protected override string SerialDeviceName => "ModbusRx ASCII";

    /// <summary>
    /// Initializes a new instance of the <see cref="ModbusRxSerialAsciiMasterModbusRxSerialAsciiSlaveFixture"/> class.
    /// </summary>
    public ModbusRxSerialAsciiMasterModbusRxSerialAsciiSlaveFixture()
    {
        MasterSerialPort = CreateAndOpenSerialPort(DefaultMasterSerialPortName);
        Master = ModbusSerialMaster.CreateAscii(MasterSerialPort);
        SetupSlaveSerialPort();
        Slave = ModbusSerialSlave.CreateAscii(SlaveAddress, SlaveSerialPort!);

        StartSlave();
    }

    /// <summary>
    /// Reads the coils.
    /// </summary>
    [TUnit.Core.Test]
    public override Task ReadCoilsAsync() =>
        base.ReadCoilsAsync();

    /// <summary>
    /// Reads the inputs.
    /// </summary>
    [TUnit.Core.Test]
    public override Task ReadInputsAsync() =>
        base.ReadInputsAsync();

    /// <summary>
    /// Reads the holding registers.
    /// </summary>
    [TUnit.Core.Test]
    public override Task ReadHoldingRegistersAsync() =>
        base.ReadHoldingRegistersAsync();

    /// <summary>
    /// Reads the input registers.
    /// </summary>
    [TUnit.Core.Test]
    public override Task ReadInputRegistersAsync() =>
        base.ReadInputRegistersAsync();

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
