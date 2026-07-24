// Copyright (c) 2019-2026 Chris Pulman and contributors. All rights reserved.
// Chris Pulman and contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Net;
using IoT.DriverCore.ModbusRx.Data;
using IoT.DriverCore.ModbusRx.Device;
using IoT.DriverCore.ModbusRx.Message;
using IoT.DriverCore.Serial;
using NativeAssert = TUnit.Assertions.Assert;

namespace IoT.DriverCore.ModbusRx.UnitTests;

/// <summary>Exercises residual Modbus TCP device lifecycles and nullable request validation paths.</summary>
[TUnit.Core.NotInParallel]
public sealed class ModbusDeviceResidualCoverageTests
{
    /// <summary>The Modbus unit identifier used by the loopback devices.</summary>
    private const byte UnitId = 1;

    /// <summary>The compact size of the data stores used by TCP tests.</summary>
    private const ushort StoreSize = 16;

    /// <summary>The register value written through the real socket stack.</summary>
    private const ushort RegisterValue = 0x6A5C;

    /// <summary>The upper bound for a bounded loopback operation.</summary>
    private static readonly TimeSpan IoTimeout = TimeSpan.FromSeconds(5);

    /// <summary>The polling delay used while a connection removal is observed.</summary>
    private static readonly TimeSpan PollDelay = TimeSpan.FromMilliseconds(10);

    /// <summary>Round-trips an endpoint request and validates disposal prevents new masters.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [TUnit.Core.Test]
    public async Task TcpLoopbackEndpoint_RoundTripsAndCompletesWhenDisposedAsync()
    {
        using var simulator = new ModbusSimulator(UnitId);
        var endpoint = simulator.StartTcpLoopback();

        await NativeAssert.That(endpoint.EndPoint.Address).IsEqualTo(IPAddress.Loopback);
        await NativeAssert.That(endpoint.Port).IsGreaterThan(0);
        await NativeAssert.That(endpoint.Completion.IsCompleted).IsFalse();

        using (var master = endpoint.CreateMaster())
        {
            await master.WriteSingleRegisterAsync(UnitId, 0, RegisterValue).WaitAsync(IoTimeout);
            var registers = await master.ReadHoldingRegistersAsync(UnitId, 0, 1).WaitAsync(IoTimeout);

            await NativeAssert.That(registers).IsEquivalentTo([RegisterValue]);
        }

        endpoint.Dispose();
        endpoint.Dispose();
        await endpoint.Completion.WaitAsync(IoTimeout);

        await NativeAssert.That(() => endpoint.CreateMaster()).Throws<ObjectDisposedException>();
    }

    /// <summary>Tracks an accepted master and removes it after the direct TCP master closes.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [TUnit.Core.Test]
    public async Task TcpSlave_TracksAndReleasesDirectMasterConnectionsAsync()
    {
        using var listener = new TestTcpListener(IPAddress.Loopback, 0);
        using var dataStore = DataStoreFactory.CreateDefaultDataStore(StoreSize, StoreSize, StoreSize, StoreSize);
        using var slave = ModbusTcpSlave.CreateTcp(UnitId, listener);
        slave.DataStore = dataStore;
        var listenTask = slave.ListenAsync();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;

        using (var master = ModbusIpMaster.CreateIp(new TcpClientRx(IPAddress.Loopback.ToString(), port)))
        {
            await master.WriteSingleRegisterAsync(UnitId, 0, RegisterValue).WaitAsync(IoTimeout);
            await WaitForAsync(() => slave.Masters.Count == 1);

            await NativeAssert.That(slave.IsListening).IsTrue();
            await NativeAssert.That(slave.Masters.Count).IsEqualTo(1);
        }

        await WaitForAsync(() => slave.Masters.Count == 0);
        slave.Dispose();
        await listenTask.WaitAsync(IoTimeout);

        await NativeAssert.That(slave.IsListening).IsFalse();
        await NativeAssert.That(slave.Masters).IsEmpty();
    }

    /// <summary>Rejects absent responses consistently for the concrete public request implementations.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [TUnit.Core.Test]
    public async Task Requests_RejectNullResponsesAtTheirPublicValidationBoundaryAsync()
    {
        var coils = new ReadCoilsInputsRequest(Modbus.ReadCoils, UnitId, 0, 1);
        var registers = new ReadHoldingInputRegistersRequest(Modbus.ReadHoldingRegisters, UnitId, 0, 1);
        var multipleCoils = new WriteMultipleCoilsRequest(UnitId, 0, new DiscreteCollection(true));
        var multipleRegisters = new WriteMultipleRegistersRequest(UnitId, 0, new RegisterCollection(RegisterValue));
        var singleCoil = new WriteSingleCoilRequestResponse(UnitId, 0, true);
        var singleRegister = new WriteSingleRegisterRequestResponse(UnitId, 0, RegisterValue);

        await NativeAssert.That(() => coils.ValidateResponse(null!)).Throws<IOException>();
        await NativeAssert.That(() => registers.ValidateResponse(null!)).Throws<IOException>();
        await NativeAssert.That(() => multipleCoils.ValidateResponse(null!)).Throws<IOException>();
        await NativeAssert.That(() => multipleRegisters.ValidateResponse(null!)).Throws<IOException>();
        await NativeAssert.That(() => singleCoil.ValidateResponse(null!)).Throws<IOException>();
        await NativeAssert.That(() => singleRegister.ValidateResponse(null!)).Throws<IOException>();
    }

    /// <summary>Waits for a bounded connection-state transition.</summary>
    /// <param name="predicate">The condition to observe.</param>
    /// <returns>A task representing the asynchronous wait.</returns>
    private static async Task WaitForAsync(Func<bool> predicate)
    {
        var timeout = Task.Delay(IoTimeout);
        while (!predicate())
        {
            if (await Task.WhenAny(Task.Delay(PollDelay), timeout) == timeout)
            {
                throw new TimeoutException("The bounded loopback connection state did not transition in time.");
            }
        }
    }
}
