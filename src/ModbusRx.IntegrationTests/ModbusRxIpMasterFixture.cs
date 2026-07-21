// Copyright (c) 2022-2026 Chris Pulman. All rights reserved.
// Chris Pulman licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using CP.IO.Ports;
using ModbusRx.Device;

namespace ModbusRx.IntegrationTests;

/// <summary>Tests the ModbusIpMasterFixture behavior.</summary>
public class ModbusRxIpMasterFixture : NetworkTestBase
{
    /// <summary>Overrides the timeout on TCP client.</summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous unit test.</returns>
    [TUnit.Core.Test]
    public async Task OverrideTimeoutOnTcpClientAsync()
    {
        const int slaveStartupTimeoutSeconds = 5;
        const int socketBindingDelayMilliseconds = 100;
        const int readTimeoutMilliseconds = 1500;
        const int writeTimeoutMilliseconds = 3000;

        // Arrange
        var port = await GetAvailablePortAsync();
        var listener = new TcpListener(ModbusRxMasterFixtureBase.TcpHost, port);
        var slave = ModbusTcpSlave.CreateTcp(ModbusRxMasterFixtureBase.SlaveAddress, listener);
        RegisterDisposable(slave);

        var startedEvent = new ManualResetEventSlim(false);
        _ = Task.Run(async () =>
        {
            try
            {
                startedEvent.Set();
                await slave.ListenAsync();
            }
            catch (OperationCanceledException)
            {
                // Expected when stopping
            }
            catch (ObjectDisposedException)
            {
                // Expected when listener is disposed
            }
        });

        // Wait for slave to start with timeout
        var started = await WaitForConditionAsync(
            () => startedEvent.IsSet,
            TimeSpan.FromSeconds(slaveStartupTimeoutSeconds));
        Assert.True(started, "Slave failed to start within timeout");

        await Task.Delay(socketBindingDelayMilliseconds, CancellationToken); // Give a bit more time for socket binding

        // Act & Assert
        using var client = new TcpClientRx(ModbusRxMasterFixtureBase.TcpHost.ToString(), port)
        {
            ReadTimeout = readTimeoutMilliseconds,
            WriteTimeout = writeTimeoutMilliseconds,
        };

        using var master = ModbusIpMaster.CreateIp(client);
        Assert.Equal(readTimeoutMilliseconds, client.ReadTimeout);
        Assert.Equal(writeTimeoutMilliseconds, client.WriteTimeout);
    }

    /// <summary>Overrides the timeout on network stream.</summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous unit test.</returns>
    [TUnit.Core.Test]
    public async Task OverrideTimeoutOnNetworkStreamAsync()
    {
        const int slaveStartupTimeoutSeconds = 5;
        const int socketBindingDelayMilliseconds = 100;
        const int readTimeoutMilliseconds = 1500;
        const int writeTimeoutMilliseconds = 3000;
        const int operationAbortedErrorCode = 995;

        // Arrange
        var port = await GetAvailablePortAsync();
        var listener = new TcpListener(ModbusRxMasterFixtureBase.TcpHost, port);
        var slave = ModbusTcpSlave.CreateTcp(ModbusRxMasterFixtureBase.SlaveAddress, listener);
        RegisterDisposable(slave);

        var startedEvent = new ManualResetEventSlim(false);
        _ = Task.Run(async () =>
        {
            try
            {
                startedEvent.Set();
                await slave.ListenAsync();
            }
            catch (OperationCanceledException)
            {
                // Expected when stopping
            }
            catch (ObjectDisposedException)
            {
                // Expected when listener is disposed
            }
            catch (SocketException ex) when (ex.ErrorCode == operationAbortedErrorCode)
            {
                // Expected when I/O operation is aborted due to thread exit or application request
                // This is normal during test cleanup in CI environments
            }
            catch (SocketException)
            {
                // Other socket exceptions during cleanup are also expected
            }
        });

        // Wait for slave to start with timeout
        var started = await WaitForConditionAsync(
            () => startedEvent.IsSet,
            TimeSpan.FromSeconds(slaveStartupTimeoutSeconds));
        Assert.True(started, "Slave failed to start within timeout");

        await Task.Delay(socketBindingDelayMilliseconds, CancellationToken); // Give a bit more time for socket binding

        // Act & Assert
        using var client = new TcpClientRx(ModbusRxMasterFixtureBase.TcpHost.ToString(), port);
        client.Stream.ReadTimeout = readTimeoutMilliseconds;
        client.Stream.WriteTimeout = writeTimeoutMilliseconds;

        using var master = ModbusIpMaster.CreateIp(client);
        Assert.Equal(readTimeoutMilliseconds, client.ReadTimeout);
        Assert.Equal(writeTimeoutMilliseconds, client.WriteTimeout);
    }
}
