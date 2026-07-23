// Copyright (c) 2019-2026 Chris Pulman and contributors. All rights reserved.
// Chris Pulman and contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System;
using System.Net;
using System.Threading.Tasks;
using IoT.DriverCore.ModbusRx.Data;
using IoT.DriverCore.ModbusRx.Device;
using TUnitAssert = TUnit.Assertions.Assert;

namespace IoT.DriverCore.ModbusRx.IntegrationTests;

/// <summary>Verifies the reusable Modbus simulator through the operating-system TCP loopback stack.</summary>
public sealed class ModbusSimulatorLoopbackTests
{
    /// <summary>The simulated unit identifier.</summary>
    private const byte UnitId = 1;

    /// <summary>The number of values allocated in each data area.</summary>
    private const ushort DataAreaSize = 32;

    /// <summary>The number of values in the multi-register exchange.</summary>
    private const ushort RegisterCount = 2;

    /// <summary>The first register value.</summary>
    private const ushort FirstValue = 1234;

    /// <summary>The second register value.</summary>
    private const ushort SecondValue = 5678;

    /// <summary>Verifies a real TCP read/write exchange on an automatically assigned loopback port.</summary>
    /// <returns>A task that represents the test.</returns>
    [TUnit.Core.Test]
    public async Task DynamicTcpLoopbackEndpointRoundTripsPersistentDeviceMemoryAsync()
    {
        using var dataStore = CreateDataStore();
        using var simulator = new ModbusSimulator(UnitId, dataStore);
        using var endpoint = simulator.StartTcpLoopback();
        using var master = endpoint.CreateMaster();

        await master.WriteMultipleRegistersAsync(
            UnitId,
            startAddress: 0,
            [FirstValue, SecondValue]);
        var values = await master.ReadHoldingRegistersAsync(UnitId, 0, RegisterCount);

        await TUnitAssert.That(endpoint.EndPoint.Address).IsEqualTo(IPAddress.Loopback);
        await TUnitAssert.That(endpoint.Port > 0).IsTrue();
        await TUnitAssert.That(values)
            .IsEquivalentTo([FirstValue, SecondValue]);
    }

    /// <summary>Verifies that a new loopback endpoint retains the simulator device memory.</summary>
    /// <returns>A task that represents the test.</returns>
    [TUnit.Core.Test]
    public async Task RestartedLoopbackEndpointRetainsMemoryAndReconnectsAsync()
    {
        using var dataStore = CreateDataStore();
        using var simulator = new ModbusSimulator(UnitId, dataStore);
        var firstEndpoint = simulator.StartTcpLoopback();
        using (var firstMaster = firstEndpoint.CreateMaster())
        {
            await firstMaster.WriteSingleRegisterAsync(UnitId, 0, FirstValue);
        }

        firstEndpoint.Dispose();
        await TUnitAssert.That(firstEndpoint.CreateMaster).Throws<ObjectDisposedException>();
        using var secondEndpoint = simulator.StartTcpLoopback();
        using var secondMaster = secondEndpoint.CreateMaster();

        var values = await secondMaster.ReadHoldingRegistersAsync(UnitId, 0, 1);

        await TUnitAssert.That(values).IsEquivalentTo([FirstValue]);
        await TUnitAssert.That(secondEndpoint.Port > 0).IsTrue();
    }

    /// <summary>Creates a bounded data store for loopback tests.</summary>
    /// <returns>The test data store.</returns>
    private static DataStore CreateDataStore() =>
        DataStoreFactory.CreateDefaultDataStore(
            DataAreaSize,
            DataAreaSize,
            DataAreaSize,
            DataAreaSize);
}
