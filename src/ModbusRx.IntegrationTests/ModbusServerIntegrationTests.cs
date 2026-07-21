// Copyright (c) 2022-2026 Chris Pulman. All rights reserved.
// Chris Pulman licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Net;
using System.Threading.Tasks;
using CP.IO.Ports;
using ModbusRx.Data;
using ModbusRx.Device;
using ReactiveModbusServer = ModbusRx.Reactive.Device.ModbusServer;
using ReactiveModbusServerExtensions = ModbusRx.Reactive.ModbusServerExtensions;

namespace ModbusRx.IntegrationTests;

/// <summary>Integration tests for the new ModbusServer functionality.</summary>
public sealed class ModbusServerIntegrationTests : NetworkTestBase
{
    /// <summary>The local loopback address.</summary>
    private const string LoopbackAddress = "127.0.0.1";

    /// <summary>The intentionally unavailable TCP port used by client aggregation tests.</summary>
    private const int UnavailableTcpPort = 10_502;

    /// <summary>The number of registers used by the short read tests.</summary>
    private const int ShortReadRegisterCount = 5;

    /// <summary>The number of registers used by the multi-client read test.</summary>
    private const int MultiClientRegisterCount = 3;

    /// <summary>The delay used while TCP and UDP servers begin accepting connections.</summary>
    private const int ServerStartupDelayMilliseconds = 200;

    /// <summary>The delay used for simulation changes to become observable.</summary>
    private const int SimulationChangeDelayMilliseconds = 600;

    /// <summary>The polling interval used by the reactive observation test.</summary>
    private const int ReactiveObservationIntervalMilliseconds = 100;

    /// <summary>The number of values inspected when validating generated simulation data.</summary>
    private const int SimulationPatternSampleCount = 10;

    /// <summary>The number of registers loaded for the performance test.</summary>
    private const int PerformanceRegisterCount = 1_000;

    /// <summary>The number of concurrent requests in the performance test.</summary>
    private const int ConcurrentRequestCount = 10;

    /// <summary>The number of registers read by each performance request.</summary>
    private const int RegistersPerPerformanceRequest = 10;

    /// <summary>The first register index used by pattern assertions.</summary>
    private const int FirstRegisterIndex = 0;

    /// <summary>The second register index used by pattern assertions.</summary>
    private const int SecondRegisterIndex = 1;

    /// <summary>The third register index used by pattern assertions.</summary>
    private const int ThirdRegisterIndex = 2;

    /// <summary>The expected third value of the counting-up pattern.</summary>
    private const ushort ThirdCountingUpValue = 2;

    /// <summary>The final register index used by short read assertions.</summary>
    private const int FinalShortReadRegisterIndex = 4;

    /// <summary>The first standard simulation register value.</summary>
    private const ushort FirstStandardSimulationValue = 100;

    /// <summary>The second standard simulation register value.</summary>
    private const ushort SecondStandardSimulationValue = 200;

    /// <summary>The third standard simulation register value.</summary>
    private const ushort ThirdStandardSimulationValue = 300;

    /// <summary>The fourth standard simulation register value.</summary>
    private const ushort FourthStandardSimulationValue = 400;

    /// <summary>The final standard simulation register value.</summary>
    private const ushort FinalStandardSimulationValue = 500;

    /// <summary>The first value used by the multi-client test.</summary>
    private const ushort FirstMultiClientValue = 111;

    /// <summary>The second value used by the multi-client test.</summary>
    private const ushort SecondMultiClientValue = 222;

    /// <summary>The third value used by the multi-client test.</summary>
    private const ushort ThirdMultiClientValue = 333;

    /// <summary>The start address for multi-register write tests.</summary>
    private const ushort MultipleRegisterStartAddress = 10;

    /// <summary>The first multi-register write value.</summary>
    private const ushort FirstMultipleWriteValue = 1_000;

    /// <summary>The second multi-register write value.</summary>
    private const ushort SecondMultipleWriteValue = 2_000;

    /// <summary>The third multi-register write value.</summary>
    private const ushort ThirdMultipleWriteValue = 3_000;

    /// <summary>The value written by the single-register write integration test.</summary>
    private const ushort WrittenRegisterValue = 12_345;

    /// <summary>Tests that the ModbusServer can serve data over TCP.</summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous unit test.</returns>
    [TUnit.Core.Test]
    public async Task ModbusServer_TcpCommunication_ShouldWorkAsync()
    {
        // Arrange
        var server = new ModbusServer();
        RegisterDisposable(server);

        var testData = new ushort[]
        {
            FirstStandardSimulationValue,
            SecondStandardSimulationValue,
            ThirdStandardSimulationValue,
            FourthStandardSimulationValue,
            FinalStandardSimulationValue,
        };
        server.LoadSimulationData(testData, null, null, null);

        var tcpPort = await GetAvailablePortAsync();
        _ = server.StartTcpServer(tcpPort, 1);
        server.Start();

        // Give server time to start
        await Task.Delay(ServerStartupDelayMilliseconds, CancellationToken);

        // Create client
        var client = new TcpClientRx(LoopbackAddress, tcpPort);
        var master = ModbusIpMaster.CreateIp(client);
        RegisterDisposable(master);

        // Act
        var result = await master.ReadHoldingRegistersAsync(1, 0, ShortReadRegisterCount);

        // Assert
        Assert.Equal(ShortReadRegisterCount, result.Length);
        Assert.Equal(FirstStandardSimulationValue, result[FirstRegisterIndex]);
        Assert.Equal(FinalStandardSimulationValue, result[FinalShortReadRegisterIndex]);
    }

    /// <summary>Tests that the ModbusServer can serve data over UDP.</summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous unit test.</returns>
    [TUnit.Core.Test]
    public async Task ModbusServer_UdpCommunication_ShouldWorkAsync()
    {
        // Arrange
        var server = new ModbusServer();
        RegisterDisposable(server);

        var testCoils = new bool[] { true, false, true, false, true };
        server.LoadSimulationData(null, null, testCoils, null);

        var udpPort = await GetAvailablePortAsync();
        _ = server.StartUdpServer(udpPort, 1);
        server.Start();

        // Give server time to start
        await Task.Delay(ServerStartupDelayMilliseconds, CancellationToken);

        // Create UDP client
        var client = new UdpClientRx();
        var endPoint = new IPEndPoint(IPAddress.Loopback, udpPort);
        client.Connect(endPoint);
        var master = ModbusIpMaster.CreateIp(client);
        RegisterDisposable(master);

        // Act
        var result = await master.ReadCoilsAsync(1, 0, ShortReadRegisterCount);

        // Assert
        Assert.Equal(ShortReadRegisterCount, result.Length);
        Assert.True(result[FirstRegisterIndex]);
        Assert.False(result[SecondRegisterIndex]);
        Assert.True(result[FinalShortReadRegisterIndex]);
    }

    /// <summary>Tests that simulation mode generates changing data.</summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous unit test.</returns>
    [TUnit.Core.Test]
    public async Task ModbusServer_SimulationMode_ShouldGenerateDataAsync()
    {
        // Arrange
        var server = new ModbusServer();
        RegisterDisposable(server);

        server.SimulationMode = true;
        server.Start();

        // Wait for simulation to run
        await Task.Delay(SimulationChangeDelayMilliseconds, CancellationToken);

        // Act
        var data1 = server.GetCurrentData();
        await Task.Delay(SimulationChangeDelayMilliseconds, CancellationToken);
        var data2 = server.GetCurrentData();

        // Assert - data should have changed
        Assert.True(HasAnyChange(data1.HoldingRegisters, data2.HoldingRegisters));
    }

    /// <summary>Tests reactive data observation.</summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous unit test.</returns>
    [TUnit.Core.Test]
    public async Task ModbusServer_ReactiveObservation_ShouldEmitDataAsync()
    {
        // Arrange
        var server = new ReactiveModbusServer();
        RegisterDisposable(server);

        server.Start();
        server.SimulationMode = true;

        var dataReceived = false;

        // Act
        IDisposable? subscription = null;
        subscription = ReactiveModbusServerExtensions.ObserveDataChanges(
                server,
                ReactiveObservationIntervalMilliseconds)
            .Subscribe(_ =>
            {
                dataReceived = true;
                subscription?.Dispose();
            });
        RegisterDisposable(subscription);

        await Task.Delay(ServerStartupDelayMilliseconds, CancellationToken);

        // Assert
        Assert.True(dataReceived);
    }

    /// <summary>Tests that multiple clients can connect to the same server.</summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous unit test.</returns>
    [TUnit.Core.Test]
    public async Task ModbusServer_MultipleClients_ShouldWorkAsync()
    {
        // Arrange
        var server = new ModbusServer();
        RegisterDisposable(server);

        var testData = new ushort[] { FirstMultiClientValue, SecondMultiClientValue, ThirdMultiClientValue };
        server.LoadSimulationData(testData, null, null, null);

        var tcpPort = await GetAvailablePortAsync();
        _ = server.StartTcpServer(tcpPort, 1);
        server.Start();

        await Task.Delay(ServerStartupDelayMilliseconds, CancellationToken);

        // Create multiple clients
        var client1 = new TcpClientRx(LoopbackAddress, tcpPort);
        var master1 = ModbusIpMaster.CreateIp(client1);
        RegisterDisposable(master1);

        var client2 = new TcpClientRx(LoopbackAddress, tcpPort);
        var master2 = ModbusIpMaster.CreateIp(client2);
        RegisterDisposable(master2);

        // Act
        var result1 = await master1.ReadHoldingRegistersAsync(1, 0, MultiClientRegisterCount);
        var result2 = await master2.ReadHoldingRegistersAsync(1, 0, MultiClientRegisterCount);

        // Assert
        Assert.Equal(result1, result2);
        Assert.Equal(FirstMultiClientValue, result1[FirstRegisterIndex]);
        Assert.Equal(ThirdMultiClientValue, result1[ThirdRegisterIndex]);
    }

    /// <summary>Tests different simulation patterns.</summary>
    /// <param name="pattern">The test pattern to verify.</param>
    [TUnit.Core.Test]
    [TUnit.Core.Arguments(TestPattern.CountingUp)]
    [TUnit.Core.Arguments(TestPattern.SineWave)]
    [TUnit.Core.Arguments(TestPattern.SquareWave)]
    [TUnit.Core.Arguments(TestPattern.Random)]
    public void ModbusServer_SimulationPatterns_ShouldLoadCorrectly(TestPattern pattern)
    {
        // Arrange
        var server = new ModbusServer();
        RegisterDisposable(server);

        using var provider = new SimulationDataProvider();

        // Act
        provider.LoadTestPattern(server.DataStore!, pattern);
        var data = server.GetCurrentData();

        // Assert
        switch (pattern)
        {
            case TestPattern.CountingUp:
                {
                    Assert.Equal(0, data.HoldingRegisters[FirstRegisterIndex]);
                    Assert.Equal(1, data.HoldingRegisters[SecondRegisterIndex]);
                    Assert.Equal(ThirdCountingUpValue, data.HoldingRegisters[ThirdRegisterIndex]);
                    break;
                }

            case TestPattern.CountingDown:
                {
                    Assert.True(data.HoldingRegisters[FirstRegisterIndex] > 0);
                    break;
                }

            case TestPattern.SineWave or TestPattern.SquareWave or TestPattern.Random or TestPattern.AllOnes:
                {
                    // For these patterns, just verify data was loaded
                    Assert.True(ContainsPositiveValue(data.HoldingRegisters, SimulationPatternSampleCount));
                    break;
                }

            case TestPattern.AllZeros:
                {
                    Assert.Equal(0, data.HoldingRegisters[FirstRegisterIndex]);
                    break;
                }

            default:
                throw new ArgumentOutOfRangeException(nameof(pattern), pattern, null);
        }
    }

    /// <summary>Tests writing data to the server.</summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous unit test.</returns>
    [TUnit.Core.Test]
    public async Task ModbusServer_WriteOperations_ShouldWorkAsync()
    {
        // Arrange
        var server = new ModbusServer();
        RegisterDisposable(server);

        var tcpPort = await GetAvailablePortAsync();
        _ = server.StartTcpServer(tcpPort, 1);
        server.Start();

        await Task.Delay(ServerStartupDelayMilliseconds, CancellationToken);

        var client = new TcpClientRx(LoopbackAddress, tcpPort);
        var master = ModbusIpMaster.CreateIp(client);
        RegisterDisposable(master);

        // Act - Write single register
        await master.WriteSingleRegisterAsync(1, 0, WrittenRegisterValue);
        var readResult = await master.ReadHoldingRegistersAsync(1, 0, 1);

        // Assert
        Assert.Equal(WrittenRegisterValue, readResult[0]);

        // Act - Write multiple registers
        var writeData = new ushort[]
        {
            FirstMultipleWriteValue,
            SecondMultipleWriteValue,
            ThirdMultipleWriteValue,
        };
        await master.WriteMultipleRegistersAsync(1, MultipleRegisterStartAddress, writeData);
        var multiReadResult = await master.ReadHoldingRegistersAsync(
            1,
            MultipleRegisterStartAddress,
            MultiClientRegisterCount);

        // Assert
        Assert.Equal(writeData, multiReadResult);
    }

    /// <summary>Tests server start/stop functionality.</summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous unit test.</returns>
    [TUnit.Core.Test]
    public async Task ModbusServer_StartStop_ShouldWorkAsync()
    {
        // Arrange
        var server = new ModbusServer();
        RegisterDisposable(server);

        // Act & Assert - Initially not running
        Assert.False(await server.IsRunning.FirstAsync());

        // Start server
        server.Start();
        Assert.True(await server.IsRunning.FirstAsync());

        // Stop server
        server.Stop();
        Assert.False(await server.IsRunning.FirstAsync());
    }

    /// <summary>Tests that server properly handles client aggregation.</summary>
    [TUnit.Core.Test]
    public void ModbusServer_ClientAggregation_ShouldWork()
    {
        // Arrange
        var server = new ModbusServer();
        RegisterDisposable(server);

        // This test verifies the method doesn't throw during setup,
        // but the actual connection will fail since no server is running on that port
        // which is expected behavior in a test environment
        // Act & Assert - Should throw SocketException since no server is running
        _ = Assert.Throws<System.Net.Sockets.SocketException>(() =>
            server.AddTcpClient("test-client", LoopbackAddress, UnavailableTcpPort, 1));
    }

    /// <summary>Tests performance under load.</summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous unit test.</returns>
    [TUnit.Core.Test]
    public async Task ModbusServer_PerformanceTest_ShouldHandleMultipleRequestsAsync()
    {
        // Arrange
        var server = new ModbusServer();
        RegisterDisposable(server);

        server.LoadSimulationData(CreateSequentialRegisters(PerformanceRegisterCount), null, null, null);

        var tcpPort = await GetAvailablePortAsync();
        _ = server.StartTcpServer(tcpPort, 1);
        server.Start();

        await Task.Delay(ServerStartupDelayMilliseconds, CancellationToken);

        var client = new TcpClientRx(LoopbackAddress, tcpPort);
        var master = ModbusIpMaster.CreateIp(client);
        RegisterDisposable(master);

        // Act - Perform multiple concurrent reads
        var tasks = new List<Task<ushort[]>>();
        for (var i = 0; i < ConcurrentRequestCount; i++)
        {
            var startAddr = (ushort)(i * RegistersPerPerformanceRequest);
            tasks.Add(master.ReadHoldingRegistersAsync(1, startAddr, RegistersPerPerformanceRequest));
        }

        var results = await Task.WhenAll(tasks);

        // Assert
        Assert.Equal(ConcurrentRequestCount, results.Length);
        foreach (var result in results)
        {
            Assert.Equal(RegistersPerPerformanceRequest, result.Length);
        }
    }

    /// <summary>Creates a sequence of register values.</summary>
    /// <param name="count">The number of values to create.</param>
    /// <returns>The created register values.</returns>
    private static ushort[] CreateSequentialRegisters(int count)
    {
        var values = new ushort[count];

        for (var i = 0; i < values.Length; i++)
        {
            values[i] = (ushort)i;
        }

        return values;
    }

    /// <summary>Determines whether the first values contain a value greater than zero.</summary>
    /// <param name="values">The values to inspect.</param>
    /// <param name="count">The number of values to inspect.</param>
    /// <returns><c>true</c> when a matching value is found; otherwise, <c>false</c>.</returns>
    private static bool ContainsPositiveValue(ushort[] values, int count)
    {
        var length = Math.Min(values.Length, count);

        for (var i = 0; i < length; i++)
        {
            if (values[i] > 0)
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Determines whether any values differ between two arrays.</summary>
    /// <param name="left">The left values.</param>
    /// <param name="right">The right values.</param>
    /// <returns><c>true</c> when any value differs; otherwise, <c>false</c>.</returns>
    private static bool HasAnyChange(ushort[] left, ushort[] right)
    {
        var length = Math.Min(left.Length, right.Length);

        for (var i = 0; i < length; i++)
        {
            if (left[i] != right[i])
            {
                return true;
            }
        }

        return false;
    }
}
