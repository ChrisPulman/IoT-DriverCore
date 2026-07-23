// Copyright (c) 2019-2026 Chris Pulman and contributors. All rights reserved.
// Chris Pulman and contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Net;
using System.Net.Sockets;
using System.Reflection;
using IoT.DriverCore.ModbusRx.Data;
using IoT.DriverCore.ModbusRx.Device;
using IoT.DriverCore.Serial;
using NativeAssert = TUnit.Assertions.Assert;

namespace IoT.DriverCore.ModbusRx.UnitTests;

/// <summary>Deterministic coverage for synchronizing server data from a loopback Modbus client.</summary>
[TUnit.Core.NotInParallel]
public sealed class ModbusServerClientSyncCoverageTests
{
    /// <summary>The loopback slave identifier.</summary>
    private const byte UnitId = 1;

    /// <summary>The backing-store size required by the server's fixed bulk reads.</summary>
    private const ushort StoreSize = 128;

    /// <summary>The representative register value.</summary>
    private const ushort RegisterValue = 0x4567;

    /// <summary>The maximum time allowed for loopback synchronization.</summary>
    private static readonly TimeSpan SyncTimeout = TimeSpan.FromSeconds(5);

    /// <summary>The polling delay while awaiting periodic synchronization.</summary>
    private static readonly TimeSpan SyncPollDelay = TimeSpan.FromMilliseconds(20);

    /// <summary>Copies all four Modbus data areas from a live loopback client.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [TUnit.Core.Test]
    public async Task UpdateDataFromClient_CopiesAllAreasAndHandlesNullStoreAsync()
    {
        var port = ReserveTcpPort();
        using var source = DataStoreFactory.CreateDefaultDataStore(StoreSize, StoreSize, StoreSize, StoreSize);
        source.HoldingRegisters[UnitId] = RegisterValue;
        source.InputRegisters[UnitId] = RegisterValue;
        source.CoilDiscretes[UnitId] = true;
        source.InputDiscretes[UnitId] = true;
        using var destination = DataStoreFactory.CreateDefaultDataStore(StoreSize, StoreSize, StoreSize, StoreSize);
        ModbusTcpSlave? slave = null;
        using var subscription = Create.TcpIpSlave(IPAddress.Loopback.ToString(), port, UnitId)
            .Subscribe(value =>
            {
                value.DataStore = source;
                slave = value;
            });
        using var master = ModbusIpMaster.CreateIp(new TcpClientRx(IPAddress.Loopback.ToString(), port));
        using var server = new ModbusServer { DataStore = destination };
        var updateMethod = typeof(ModbusServer).GetMethod(
            "UpdateDataFromClientAsync",
            BindingFlags.Instance | BindingFlags.NonPublic);

        await NativeAssert.That(updateMethod).IsNotNull();
        await InvokeUpdateAsync(updateMethod!, server, master);

        await NativeAssert.That(destination.HoldingRegisters[UnitId]).IsEqualTo(RegisterValue);
        await NativeAssert.That(destination.InputRegisters[UnitId]).IsEqualTo(RegisterValue);
        await NativeAssert.That(destination.CoilDiscretes[UnitId]).IsTrue();
        await NativeAssert.That(destination.InputDiscretes[UnitId]).IsTrue();
        await NativeAssert.That(slave?.UnitId).IsEqualTo(UnitId);

        server.DataStore = null;
        await InvokeUpdateAsync(updateMethod!, server, master);
    }

    /// <summary>Synchronizes configured TCP and UDP clients while the server is running.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [TUnit.Core.Test]
    public async Task AddedLoopbackClients_PeriodicallySynchronizeAllAreasAsync()
    {
        var tcpPort = ReserveTcpPort();
        var udpPort = ReserveUdpPort();
        using var source = DataStoreFactory.CreateDefaultDataStore(StoreSize, StoreSize, StoreSize, StoreSize);
        source.HoldingRegisters[UnitId] = RegisterValue;
        source.InputRegisters[UnitId] = RegisterValue;
        source.CoilDiscretes[UnitId] = true;
        source.InputDiscretes[UnitId] = true;
        using var destination = DataStoreFactory.CreateDefaultDataStore(StoreSize, StoreSize, StoreSize, StoreSize);
        using var tcpSlave = Create.TcpIpSlave(IPAddress.Loopback.ToString(), tcpPort, UnitId)
            .Subscribe(value => value.DataStore = source);
        using var udpSlave = Create.UdpIpSlave(IPAddress.Loopback.ToString(), udpPort, UnitId)
            .Subscribe(value => value.DataStore = source);
        using var server = new ModbusServer { DataStore = destination };
        using var tcpClient = server.AddTcpClient(
            "tcp-loopback",
            IPAddress.Loopback.ToString(),
            tcpPort,
            UnitId);
        using var udpClient = server.AddUdpClient(
            "udp-loopback",
            IPAddress.Loopback.ToString(),
            udpPort,
            UnitId);

        server.Start();
        await WaitForAsync(
            () => destination.HoldingRegisters[UnitId] == RegisterValue
                && destination.InputRegisters[UnitId] == RegisterValue
                && destination.CoilDiscretes[UnitId]
                && destination.InputDiscretes[UnitId]);
        server.Stop();

        await NativeAssert.That(destination.HoldingRegisters[UnitId]).IsEqualTo(RegisterValue);
        await NativeAssert.That(destination.InputRegisters[UnitId]).IsEqualTo(RegisterValue);
        await NativeAssert.That(destination.CoilDiscretes[UnitId]).IsTrue();
        await NativeAssert.That(destination.InputDiscretes[UnitId]).IsTrue();
    }

    /// <summary>Exercises idempotent lifecycle and null-store behavior without starting endpoints.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [TUnit.Core.Test]
    public async Task NullDataStoreLifecycle_RemainsSafeAndReportsEmptyStateAsync()
    {
        using var server = new ModbusServer { DataStore = null };

        server.LoadSimulationData([RegisterValue], [RegisterValue], [true], [true]);
        server.SimulationMode = true;
        server.Start();
        server.Start();
        var data = server.GetCurrentData();
        var updateSimulation = typeof(ModbusServer).GetMethod(
            "UpdateSimulationData",
            BindingFlags.Instance | BindingFlags.NonPublic);
        updateSimulation?.Invoke(server, null);
        var getRandomInt32 = typeof(ModbusServer).GetMethod(
            "GetRandomInt32",
            BindingFlags.Instance | BindingFlags.NonPublic,
            binder: null,
            types: [typeof(int), typeof(int)],
            modifiers: null);
        var zeroWidthRandomValue = (int?)getRandomInt32?.Invoke(server, [UnitId, UnitId]);
        server.SimulationMode = false;
        server.Stop();

        await NativeAssert.That(server.SimulationMode).IsFalse();
        await NativeAssert.That(zeroWidthRandomValue).IsEqualTo(UnitId);
        await NativeAssert.That(data.HoldingRegisters).IsEmpty();
        await NativeAssert.That(data.InputRegisters).IsEmpty();
        await NativeAssert.That(data.Coils).IsEmpty();
        await NativeAssert.That(data.Inputs).IsEmpty();
        await NativeAssert.That(updateSimulation).IsNotNull();
        await NativeAssert.That(getRandomInt32).IsNotNull();
    }

    /// <summary>Invokes the private update operation through its deterministic composition boundary.</summary>
    /// <param name="method">The resolved update method.</param>
    /// <param name="server">The server under test.</param>
    /// <param name="master">The loopback master.</param>
    /// <returns>A task representing the invocation.</returns>
    private static Task InvokeUpdateAsync(MethodInfo method, ModbusServer server, ModbusIpMaster master) =>
        ((Task)method.Invoke(server, [master, UnitId])!).WaitAsync(SyncTimeout);

    /// <summary>Waits until a deterministic synchronization predicate succeeds.</summary>
    /// <param name="predicate">The completion predicate.</param>
    /// <returns>A task representing the wait.</returns>
    private static async Task WaitForAsync(Func<bool> predicate)
    {
        var timeout = Task.Delay(SyncTimeout);
        while (!predicate())
        {
            var delay = Task.Delay(SyncPollDelay);
            if (await Task.WhenAny(delay, timeout) == timeout)
            {
                throw new TimeoutException("The loopback client did not synchronize before the test timeout.");
            }
        }
    }

    /// <summary>Reserves and releases an available loopback TCP port.</summary>
    /// <returns>The available TCP port.</returns>
    private static int ReserveTcpPort()
    {
        using var listener = new TestTcpListener(IPAddress.Loopback, 0);
        listener.Start();
        return ((IPEndPoint)listener.LocalEndpoint).Port;
    }

    /// <summary>Reserves and releases an available loopback UDP port.</summary>
    /// <returns>The available UDP port.</returns>
    private static int ReserveUdpPort()
    {
        using var client = new UdpClient(new IPEndPoint(IPAddress.Loopback, 0));
        return ((IPEndPoint)client.Client.LocalEndPoint!).Port;
    }
}
