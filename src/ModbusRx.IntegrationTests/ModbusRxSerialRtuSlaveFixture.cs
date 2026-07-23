// Copyright (c) 2019-2026 Chris Pulman and contributors. All rights reserved.
// Chris Pulman and contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System;
using System.Threading.Tasks;
using IoT.DriverCore.ModbusRx.Data;
using IoT.DriverCore.ModbusRx.Device;

namespace IoT.DriverCore.ModbusRx.IntegrationTests;

/// <summary>Tests the ModbusRxSerialRtuSlaveFixture behavior.</summary>
public class ModbusRxSerialRtuSlaveFixture : NetworkTestBase
{
    /// <summary>The unit identifier exercised by the deterministic serial link.</summary>
    private const byte UnitId = 1;

    /// <summary>The bounded master timeout used for complete simulated RTU exchanges.</summary>
    private const int MasterReadTimeoutMilliseconds = 1000;

    /// <summary>The bounded slave timeout used to flush a truncated RTU frame.</summary>
    private const int SlaveReadTimeoutMilliseconds = 250;

    /// <summary>The short delay that lets the simulated slave enter its first read.</summary>
    private const int SlaveStartupDelayMilliseconds = 25;

    /// <summary>The number of coils read in each recovery assertion.</summary>
    private const ushort PointCount = 2;

    /// <summary>The first holding-register address read by the combined RTU operation.</summary>
    private const ushort ReadStartAddress = 120;

    /// <summary>The first holding-register address written by the combined RTU operation.</summary>
    private const ushort WriteStartAddress = 50;

    /// <summary>The number of holding registers read by the combined RTU operation.</summary>
    private const ushort RegisterCount = 5;

    /// <summary>The delay multiplier that allows the truncated frame timeout to elapse.</summary>
    private const int RecoveryDelayMultiplier = 2;

    /// <summary>The maximum number of seconds allowed for simulated slave shutdown.</summary>
    private const int ShutdownTimeoutSeconds = 2;

    /// <summary>The upper bound for stopping the simulated serial slave.</summary>
    private static readonly TimeSpan ShutdownTimeout = TimeSpan.FromSeconds(ShutdownTimeoutSeconds);

    /// <summary>Tests the modbus serial rtu slave bonus character verify timeout.</summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous unit test.</returns>
    [TUnit.Core.Test]
    public async Task ModbusRxSerialRtuSlave_BonusCharacter_VerifyTimeoutAsync()
    {
        using var pair = new InMemoryStreamResourcePair();
        pair.First.ReadTimeout = MasterReadTimeoutMilliseconds;
        pair.First.WriteTimeout = MasterReadTimeoutMilliseconds;
        pair.Second.ReadTimeout = SlaveReadTimeoutMilliseconds;
        pair.Second.WriteTimeout = SlaveReadTimeoutMilliseconds;

        using var master = ModbusSerialMaster.CreateRtu(pair.First);
        using var slave = ModbusSerialSlave.CreateRtu(UnitId, pair.Second);
        master.Transport!.ReadTimeout = MasterReadTimeoutMilliseconds;
        master.Transport.WriteTimeout = MasterReadTimeoutMilliseconds;
        master.Transport.Retries = 0;
        slave.DataStore = DataStoreFactory.CreateTestDataStore();

        var slaveTask = Task.Run(async () =>
        {
            try
            {
                await slave.ListenAsync();
            }
            catch (OperationCanceledException)
            {
                // Expected when the test disposes a pending simulated read.
            }
            catch (InvalidOperationException)
            {
                // Expected when the test disposes the simulated transport.
            }
        });

        try
        {
            bool[] expected = [false, true];
            await Task.Delay(SlaveStartupDelayMilliseconds, CancellationToken);
            Assert.Equal(expected, await master.ReadCoilsAsync(UnitId, 1, PointCount));

            pair.First.Write([(byte)'*'], 0, 1);
            await Task.Delay(SlaveReadTimeoutMilliseconds * RecoveryDelayMultiplier, CancellationToken);

            Assert.Equal(expected, await master.ReadCoilsAsync(UnitId, 1, PointCount));
        }
        finally
        {
            slave.Dispose();
            await slaveTask.WaitAsync(ShutdownTimeout);
        }
    }

    /// <summary>Verifies function 23 over the deterministic in-memory RTU link.</summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous unit test.</returns>
    [TUnit.Core.Test]
    public async Task ModbusRxSerialRtuSlave_ReadWriteMultipleRegisters_RoundTripsAsync()
    {
        using var pair = new InMemoryStreamResourcePair();
        pair.First.ReadTimeout = MasterReadTimeoutMilliseconds;
        pair.First.WriteTimeout = MasterReadTimeoutMilliseconds;
        pair.Second.ReadTimeout = MasterReadTimeoutMilliseconds;
        pair.Second.WriteTimeout = MasterReadTimeoutMilliseconds;

        using var master = ModbusSerialMaster.CreateRtu(pair.First);
        using var slave = ModbusSerialSlave.CreateRtu(UnitId, pair.Second);
        master.Transport!.ReadTimeout = MasterReadTimeoutMilliseconds;
        master.Transport.WriteTimeout = MasterReadTimeoutMilliseconds;
        master.Transport.Retries = 0;
        slave.DataStore = DataStoreFactory.CreateTestDataStore();

        var slaveTask = Task.Run(async () =>
        {
            try
            {
                await slave.ListenAsync();
            }
            catch (OperationCanceledException)
            {
                // Expected when the test disposes a pending simulated read.
            }
            catch (InvalidOperationException)
            {
                // Expected when the test disposes the simulated transport.
            }
        });

        try
        {
            ushort[] valuesToWrite = [10, 20, 30, 40, 50];
            var expectedRead = await master.ReadHoldingRegistersAsync(
                UnitId,
                ReadStartAddress,
                RegisterCount);

            var actualRead = await master.ReadWriteMultipleRegistersAsync(
                UnitId,
                ReadStartAddress,
                RegisterCount,
                WriteStartAddress,
                valuesToWrite);
            var actualWritten = await master.ReadHoldingRegistersAsync(
                UnitId,
                WriteStartAddress,
                (ushort)valuesToWrite.Length);

            Assert.Equal(expectedRead, actualRead);
            Assert.Equal(valuesToWrite, actualWritten);
        }
        finally
        {
            slave.Dispose();
            await slaveTask.WaitAsync(ShutdownTimeout);
        }
    }
}
