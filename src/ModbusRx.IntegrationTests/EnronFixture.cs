// Copyright (c) 2019-2026 Chris Pulman and contributors. All rights reserved.
// Chris Pulman and contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

#if SERIAL
using IoT.DriverCore.ModbusRx.Extensions.Enron;

namespace IoT.DriverCore.ModbusRx.IntegrationTests;

/// <summary>
/// EnronFixture.
/// </summary>
/// <seealso cref="IoT.DriverCore.ModbusRx.IntegrationTests.NModbusSerialRtuMasterDl06SlaveFixture" />
public class EnronFixture : NModbusSerialRtuMasterDl06SlaveFixture
{
    /// <summary>
    /// Reads the holding registers32.
    /// </summary>
    [TUnit.Core.Test]
    public virtual void ReadHoldingRegisters32()
    {
        var registers = Master?.ReadHoldingRegisters32(SlaveAddress, 104, 2);
        Assert.Equal(new uint[] { 0, 0 }, registers);
    }

    /// <summary>
    /// Reads the input registers32.
    /// </summary>
    [TUnit.Core.Test]
    public virtual void ReadInputRegisters32()
    {
        var registers = Master?.ReadInputRegisters32(SlaveAddress, 104, 2);
        Assert.Equal(new uint[] { 0, 0 }, registers);
    }

    /// <summary>
    /// Writes the single register32.
    /// </summary>
    [TUnit.Core.Test]
    public virtual void WriteSingleRegister32()
    {
        const ushort testAddress = 200;
        const uint testValue = 350;

        var originalValue = Master!.ReadHoldingRegisters32(SlaveAddress, testAddress, 1)[0];
        EnronModbusExtensions.WriteSingleRegister32Async(Master!, SlaveAddress, testAddress, testValue)
            .GetAwaiter()
            .GetResult();
        Assert.Equal(testValue, Master?.ReadHoldingRegisters32(SlaveAddress, testAddress, 1)[0]);
        EnronModbusExtensions.WriteSingleRegister32Async(Master!, SlaveAddress, testAddress, originalValue)
            .GetAwaiter()
            .GetResult();
        Assert.Equal(originalValue, Master!.ReadHoldingRegisters(SlaveAddress, testAddress, 1)[0]);
    }

    /// <summary>
    /// Writes the multiple registers32.
    /// </summary>
    [TUnit.Core.Test]
    public virtual void WriteMultipleRegisters32()
    {
        const ushort testAddress = 120;
        var testValues = new uint[] { 10, 20, 30, 40, 50 };

        var originalValues = Master?.ReadHoldingRegisters32(SlaveAddress, testAddress, (ushort)testValues.Length);
        Master?.WriteMultipleRegisters32(SlaveAddress, testAddress, testValues);
        var newValues = Master?.ReadHoldingRegisters32(SlaveAddress, testAddress, (ushort)testValues.Length);
        Assert.Equal(testValues, newValues);
        Master?.WriteMultipleRegisters32(SlaveAddress, testAddress, originalValues!);
    }
}
#endif
