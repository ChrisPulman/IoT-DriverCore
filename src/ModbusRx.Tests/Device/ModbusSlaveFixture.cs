// Copyright (c) 2019-2026 Chris Pulman and contributors. All rights reserved.
// Chris Pulman and contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

#if SERIAL
using System.IO.Ports;
#endif

using System.Linq;
using IoT.DriverCore.ModbusRx.Data;
using IoT.DriverCore.ModbusRx.Device;
using IoT.DriverCore.ModbusRx.Message;
using IoT.DriverCore.ModbusRx.UnitTests.Message;

namespace IoT.DriverCore.ModbusRx.UnitTests.Device;

/// <summary>Tests the ModbusSlaveFixture behavior.</summary>
public class ModbusSlaveFixture
{
    /// <summary>The test data store used by slave operation tests.</summary>
    private readonly DataStore _testDataStore;

    /// <summary>Initializes a new instance of the <see cref="ModbusSlaveFixture"/> class.</summary>
    public ModbusSlaveFixture() => _testDataStore = DataStoreFactory.CreateTestDataStore();

    /// <summary>Reads the discretes coils.</summary>
    [TUnit.Core.Test]
    public void ReadDiscretesCoils()
    {
        var expectedResponse = new ReadCoilsInputsResponse(
            Modbus.ReadCoils,
            1,
            Num.Value2,
            new DiscreteCollection(false, true, false, true, false, true, false, true, false));
        var request = new ReadCoilsInputsRequest(Modbus.ReadCoils, 1, 1, Num.Value9);
        var response = ModbusSlave.ReadDiscretes(request, _testDataStore, _testDataStore.CoilDiscretes);
        ModbusMessageFixture.AssertModbusMessagePropertiesAreEqual(expectedResponse, response);
        Assert.Equal(expectedResponse.ByteCount, response.ByteCount);
    }

    /// <summary>Reads the discretes inputs.</summary>
    [TUnit.Core.Test]
    public void ReadDiscretesInputs()
    {
        var expectedResponse = new ReadCoilsInputsResponse(
            Modbus.ReadInputs,
            1,
            Num.Value2,
            new DiscreteCollection(true, false, true, false, true, false, true, false, true));
        var request = new ReadCoilsInputsRequest(Modbus.ReadInputs, 1, 1, Num.Value9);
        var response = ModbusSlave.ReadDiscretes(request, _testDataStore, _testDataStore.InputDiscretes);
        ModbusMessageFixture.AssertModbusMessagePropertiesAreEqual(expectedResponse, response);
        Assert.Equal(expectedResponse.ByteCount, response.ByteCount);
    }

    /// <summary>Reads the registers holding registers.</summary>
    [TUnit.Core.Test]
    public void ReadRegistersHoldingRegisters()
    {
        var expectedResponse = new ReadHoldingInputRegistersResponse(
            Modbus.ReadHoldingRegisters,
            1,
            new RegisterCollection(1, Num.Value2, Num.Value3, Num.Value4, Num.Value5, Num.Value6));
        var request = new ReadHoldingInputRegistersRequest(Modbus.ReadHoldingRegisters, 1, 0, Num.Value6);
        var response = ModbusSlave.ReadRegisters(request, _testDataStore, _testDataStore.HoldingRegisters);
        ModbusMessageFixture.AssertModbusMessagePropertiesAreEqual(expectedResponse, response);
        Assert.Equal(expectedResponse.ByteCount, response.ByteCount);
    }

    /// <summary>Reads the registers input registers.</summary>
    [TUnit.Core.Test]
    public void ReadRegistersInputRegisters()
    {
        var expectedResponse = new ReadHoldingInputRegistersResponse(
            Modbus.ReadInputRegisters,
            1,
            new RegisterCollection(Num.Value10, Num.Value20, Num.Value30, Num.Value40, Num.Value50, Num.Value60));
        var request = new ReadHoldingInputRegistersRequest(Modbus.ReadInputRegisters, 1, 0, Num.Value6);
        var response = ModbusSlave.ReadRegisters(request, _testDataStore, _testDataStore.InputRegisters);
        ModbusMessageFixture.AssertModbusMessagePropertiesAreEqual(expectedResponse, response);
        Assert.Equal(expectedResponse.ByteCount, response.ByteCount);
    }

    /// <summary>Writes the single coil.</summary>
    [TUnit.Core.Test]
    public void WriteSingleCoil()
    {
        const ushort addressToWrite = 35;
        var valueToWrite = !_testDataStore.CoilDiscretes[addressToWrite + 1];
        var expectedResponse = new WriteSingleCoilRequestResponse(1, addressToWrite, valueToWrite);
        var request = new WriteSingleCoilRequestResponse(1, addressToWrite, valueToWrite);
        var response = ModbusSlave.WriteSingleCoil(request, _testDataStore, _testDataStore.CoilDiscretes);
        ModbusMessageFixture.AssertModbusMessagePropertiesAreEqual(expectedResponse, response);
        Assert.Equal(valueToWrite, _testDataStore.CoilDiscretes[addressToWrite + 1]);
    }

    /// <summary>Writes the multiple coils.</summary>
    [TUnit.Core.Test]
    public void WriteMultipleCoils()
    {
        const ushort startAddress = 35;
        const ushort numberOfPoints = 10;
        var val = !_testDataStore.CoilDiscretes[startAddress + 1];
        var expectedResponse = new WriteMultipleCoilsResponse(1, startAddress, numberOfPoints);
        var request = new WriteMultipleCoilsRequest(
            1,
            startAddress,
            new DiscreteCollection(val, val, val, val, val, val, val, val, val, val));
        var response = ModbusSlave.WriteMultipleCoils(request, _testDataStore, _testDataStore.CoilDiscretes);
        ModbusMessageFixture.AssertModbusMessagePropertiesAreEqual(expectedResponse, response);
        Assert.Equal(
            [val, val, val, val, val, val, val, val, val, val],
            _testDataStore.CoilDiscretes.Skip(startAddress + 1).Take(numberOfPoints));
    }

    /// <summary>Writes the single register.</summary>
    [TUnit.Core.Test]
    public void WriteSingleRegister()
    {
        const ushort startAddress = 35;
        const ushort value = 45;
        Assert.NotEqual(value, _testDataStore.HoldingRegisters[startAddress - 1]);
        var expectedResponse = new WriteSingleRegisterRequestResponse(1, startAddress, value);
        var request = new WriteSingleRegisterRequestResponse(1, startAddress, value);
        var response = ModbusSlave.WriteSingleRegister(request, _testDataStore, _testDataStore.HoldingRegisters);
        ModbusMessageFixture.AssertModbusMessagePropertiesAreEqual(expectedResponse, response);
    }

    /// <summary>Writes the multiple registers.</summary>
    [TUnit.Core.Test]
    public void WriteMultipleRegisters()
    {
        const ushort startAddress = 35;
        ushort[] valuesToWrite = [1, 2, 3, 4, 5];
        Assert.NotEqual(
            valuesToWrite,
            _testDataStore.HoldingRegisters.Skip(startAddress - 1).Take(valuesToWrite.Length));
        var expectedResponse = new WriteMultipleRegistersResponse(1, startAddress, (ushort)valuesToWrite.Length);
        var request = new WriteMultipleRegistersRequest(1, startAddress, new RegisterCollection(valuesToWrite));
        var response = ModbusSlave.WriteMultipleRegisters(request, _testDataStore, _testDataStore.HoldingRegisters);
        ModbusMessageFixture.AssertModbusMessagePropertiesAreEqual(expectedResponse, response);
    }

#if SERIAL
    /// <summary>
    /// ApplyRequest_VerifyModbusRequestReceivedEventIsFired.
    /// </summary>
    [TUnit.Core.Test]
    public void ApplyRequest_VerifyModbusRequestReceivedEventIsFired()
    {
        var eventFired = false;
        ModbusSlave slave = ModbusSerialSlave.CreateAscii(1, new SerialPort());
        var request = new WriteSingleRegisterRequestResponse(1, 1, 1);
        slave.ModbusSlaveRequestReceived += (obj, args) =>
        {
            eventFired = true;
            Assert.Equal(request, args.Message);
        };

        slave.ApplyRequest(request);
        Assert.True(eventFired);
    }
#endif

    /// <summary>Writes the multip coils make sure we do not write remainder.</summary>
    [TUnit.Core.Test]
    public void WriteMultipCoils_MakeSureWeDoNotWriteRemainder()
    {
        // 0, false initialized data store
        var dataStore = DataStoreFactory.CreateDefaultDataStore();

        var request = new WriteMultipleCoilsRequest(1, 0, new DiscreteCollection(CreateCoils(Num.Value8, true)))
        { NumberOfPoints = Num.Value2 };
        _ = ModbusSlave.WriteMultipleCoils(request, dataStore, dataStore.CoilDiscretes);

        Assert.Equal(
            [true, true, false, false, false, false, false, false],
            dataStore.CoilDiscretes.Skip(1).Take(Num.Value8));
    }

    /// <summary>Creates a coil buffer filled with one value.</summary>
    /// <param name="count">The number of coils to create.</param>
    /// <param name="value">The coil value.</param>
    /// <returns>The populated coil buffer.</returns>
    private static bool[] CreateCoils(int count, bool value)
    {
        var result = new bool[count];
        for (var i = 0; i < result.Length; i++)
        {
            result[i] = value;
        }

        return result;
    }
}
