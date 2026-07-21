// Copyright (c) 2022-2026 Chris Pulman. All rights reserved.
// Chris Pulman licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System;
using System.Threading.Tasks;
using CP.IO.Ports;
using ModbusRx.Device;

namespace ModbusRx.IntegrationTests;

/// <summary>Example integration tests demonstrating CI-safe network testing patterns.</summary>
public class CISafeNetworkTests : NetworkTestBase
{
    /// <summary>The address of the manually tested device.</summary>
    private const string LiveDeviceHost = "192.168.1.100";

    /// <summary>The local loopback address.</summary>
    private const string LoopbackAddress = "127.0.0.1";

    /// <summary>The default Modbus TCP port.</summary>
    private const int ModbusTcpPort = 502;

    /// <summary>The HTTP port used for the manually run connectivity test.</summary>
    private const int HttpPort = 80;

    /// <summary>The test register count.</summary>
    private const ushort RegisterCount = 5;

    /// <summary>The number of registers read from the manually tested device.</summary>
    private const ushort LiveDeviceRegisterCount = 10;

    /// <summary>The short server startup delay in milliseconds.</summary>
    private const int ServerStartupDelayMilliseconds = 200;

    /// <summary>The simulation startup delay in milliseconds.</summary>
    private const int SimulationStartupDelayMilliseconds = 100;

    /// <summary>The first expected test register value.</summary>
    private const ushort FirstRegisterValue = 100;

    /// <summary>The final expected test register value.</summary>
    private const ushort FinalRegisterValue = 500;

    /// <summary>The second expected test register value.</summary>
    private const ushort SecondRegisterValue = 200;

    /// <summary>The third expected test register value.</summary>
    private const ushort ThirdRegisterValue = 300;

    /// <summary>The fourth expected test register value.</summary>
    private const ushort FourthRegisterValue = 400;

    /// <summary>The external connectivity timeout in seconds.</summary>
    private const int ExternalConnectivityTimeoutSeconds = 10;

    /// <summary>Test that requires live network connectivity - skipped in CI environments.</summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous unit test.</returns>
    [TUnit.Core.Test]
    public async Task LiveNetworkTest_ShouldConnectToRealDeviceAsync()
    {
        // Skip this test if running in CI to avoid failures
        Skip.IfNot(!IsRunningInCI, "This test requires a real Modbus device on the network");

        // This test would only run in local development environments
        var canConnect = await TryConnectAsync(LiveDeviceHost, ModbusTcpPort);

        Skip.IfNot(canConnect, $"No Modbus device found at {LiveDeviceHost}:{ModbusTcpPort}");

        // Proceed with actual device testing
        var client = new TcpClientRx(LiveDeviceHost, ModbusTcpPort);
        using var master = ModbusIpMaster.CreateIp(client);
        RegisterDisposable(master);

        var registers = await master.ReadHoldingRegistersAsync(1, 0, LiveDeviceRegisterCount);
        _ = Assert.NotNull(registers);
        Assert.True(registers.Length > 0);
    }

    /// <summary>Test that works in both CI and local environments using localhost.</summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous unit test.</returns>
    [TUnit.Core.Test]
    public async Task LocalhostTest_ShouldWorkInAllEnvironmentsAsync()
    {
        // This test uses localhost/loopback only - safe for CI
        var server = new ModbusServer();
        RegisterDisposable(server);

        var testData = new ushort[]
        {
            FirstRegisterValue,
            SecondRegisterValue,
            ThirdRegisterValue,
            FourthRegisterValue,
            FinalRegisterValue,
        };
        server.LoadSimulationData(testData, null, null, null);

        var tcpPort = await GetAvailablePortAsync();
        _ = server.StartTcpServer(tcpPort, 1);
        server.Start();

        // Use CI-appropriate timeout
        await Task.Delay(ServerStartupDelayMilliseconds, CancellationToken);

        var client = new TcpClientRx(LoopbackAddress, tcpPort);
        var master = ModbusIpMaster.CreateIp(client);
        RegisterDisposable(master);

        var result = await master.ReadHoldingRegistersAsync(1, 0, RegisterCount);

        Assert.Equal(RegisterCount, result.Length);
        Assert.Equal(FirstRegisterValue, result[0]);
        Assert.Equal(FinalRegisterValue, result[4]);
    }

    /// <summary>Test that demonstrates conditional behavior based on environment.</summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous unit test.</returns>
    [TUnit.Core.Test]
    public async Task ConditionalNetworkTest_ShouldAdaptToEnvironmentAsync()
    {
        if (IsRunningInGitHubActions)
        {
            // In GitHub Actions, test with mock/simulation only
            var server = new ModbusServer();
            RegisterDisposable(server);

            server.SimulationMode = true;
            server.Start();

            await Task.Delay(SimulationStartupDelayMilliseconds, CancellationToken);
            var data = server.GetCurrentData();

            _ = Assert.NotNull(data.HoldingRegisters);
        }
        else
        {
            // In local environment, can test with more comprehensive scenarios
            var server = new ModbusServer();
            RegisterDisposable(server);

            // Start multiple endpoints
            var tcpPort = await GetAvailablePortAsync();
            var udpPort = await GetAvailablePortAsync();

            _ = server.StartTcpServer(tcpPort, 1);
            _ = server.StartUdpServer(udpPort, 1);
            server.Start();

            await Task.Delay(ServerStartupDelayMilliseconds, CancellationToken);

            // Test TCP connection
            var tcpClient = new TcpClientRx(LoopbackAddress, tcpPort);
            var tcpMaster = ModbusIpMaster.CreateIp(tcpClient);
            RegisterDisposable(tcpMaster);

            var tcpResult = await tcpMaster.ReadHoldingRegistersAsync(1, 0, RegisterCount);
            _ = Assert.NotNull(tcpResult);

            // Test UDP connection
            var udpClient = new UdpClientRx();
            var endPoint = new System.Net.IPEndPoint(System.Net.IPAddress.Loopback, udpPort);
            udpClient.Connect(endPoint);
            var udpMaster = ModbusIpMaster.CreateIp(udpClient);
            RegisterDisposable(udpMaster);

            var udpResult = await udpMaster.ReadHoldingRegistersAsync(1, 0, RegisterCount);
            _ = Assert.NotNull(udpResult);
        }
    }

    /// <summary>Test that requires external internet connectivity - always skipped in CI.</summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous unit test.</returns>
    [TUnit.Core.Test]
    [TUnit.Core.Skip("Requires external internet connectivity - run manually for development testing")]
    public async Task ExternalConnectivityTest_ManualTestOnlyAsync()
    {
        // This test is explicitly skipped but can be enabled manually
        // for development/debugging purposes        
        var canPing = await TryConnectAsync(
            "google.com",
            HttpPort,
            TimeSpan.FromSeconds(ExternalConnectivityTimeoutSeconds));
        Assert.True(canPing, "No internet connectivity available");

        // Additional external connectivity tests would go here
    }
}
