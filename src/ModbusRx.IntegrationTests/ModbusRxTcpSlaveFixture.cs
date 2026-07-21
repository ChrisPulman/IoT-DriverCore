// Copyright (c) 2022-2026 Chris Pulman. All rights reserved.
// Chris Pulman licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System;
using System.Diagnostics;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using CP.IO.Ports;
using ModbusRx.Device;

namespace ModbusRx.IntegrationTests;

/// <summary>Tests the NModbusTcpSlaveFixture behavior.</summary>
public sealed class ModbusRxTcpSlaveFixture : NetworkTestBase
{
    /// <summary>The socket error code expected while stopping a test listener.</summary>
    private const int CleanupSocketErrorCode = 995;

    /// <summary>The maximum time to wait for the listener to start.</summary>
    private const int SlaveStartTimeoutSeconds = 5;

    /// <summary>The delay used to let a slave begin or complete shutdown.</summary>
    private const int SlaveStartupDelayMilliseconds = 100;

    /// <summary>The number of read operations each concurrent worker performs.</summary>
    private const int WorkerReadCount = 5;

    /// <summary>
    /// Tests possible exception when master closes gracefully immediately after transaction
    /// The goal is to test WriteCompleted when the slave reads another request from an already closed master.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous unit test.</returns>
    [TUnit.Core.Test]
    public async Task ModbusTcpSlave_ConnectionClosesGracefullyAsync()
    {
        // Arrange
        var port = await GetAvailablePortAsync();
        var slaveListener = new TcpListener(ModbusRxMasterFixtureBase.TcpHost, port);
        var slave = ModbusTcpSlave.CreateTcp(ModbusRxMasterFixtureBase.SlaveAddress, slaveListener);
        RegisterDisposable(slave);

        _ = Task.Run(async () =>
        {
            try
            {
                await slave.ListenAsync();
            }
            catch (OperationCanceledException)
            {
                // Expected when stopping
            }
            catch (ObjectDisposedException)
            {
                // Expected when disposed
            }
            catch (SocketException ex) when (ex.ErrorCode == CleanupSocketErrorCode)
            {
                // Expected when I/O operation is aborted due to thread exit or application request
                // This is normal during test cleanup in CI environments
            }
            catch (SocketException)
            {
                // Other socket exceptions during cleanup are also expected
            }
        });

        // Wait for slave to start
        await Task.Delay(
            GetEnvironmentAppropriateTimeout(TimeSpan.FromMilliseconds(SlaveStartupDelayMilliseconds)),
            CancellationToken);

        // Act
        var masterClient = new TcpClientRx(ModbusRxMasterFixtureBase.TcpHost.ToString(), port);
        using (var master = ModbusIpMaster.CreateIp(masterClient))
        {
            master.Transport!.Retries = 0;

            var coils = await master.ReadCoilsAsync(1, 1);

            _ = Assert.Single(coils);
            _ = Assert.Single(slave.Masters);
        }

        // Give the slave some time to remove the master
        await Task.Delay(
            GetEnvironmentAppropriateTimeout(TimeSpan.FromMilliseconds(SlaveStartupDelayMilliseconds)),
            CancellationToken);

        Assert.Empty(slave.Masters);
    }

    /// <summary>Tests the zero-byte header read produced by a graceful master disconnect.</summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous unit test.</returns>
    [TUnit.Core.Test]
    public async Task ModbusTcpSlave_ConnectionSlowlyClosesGracefullyAsync()
    {
        // Arrange
        var port = await GetAvailablePortAsync();
        var slaveListener = new TcpListener(ModbusRxMasterFixtureBase.TcpHost, port);
        var slave = ModbusTcpSlave.CreateTcp(ModbusRxMasterFixtureBase.SlaveAddress, slaveListener);
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
            catch (SocketException ex) when (ex.ErrorCode == CleanupSocketErrorCode)
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
            TimeSpan.FromSeconds(SlaveStartTimeoutSeconds));
        Assert.True(started, "Slave failed to start within timeout");

        await Task.Delay(
            GetEnvironmentAppropriateTimeout(TimeSpan.FromMilliseconds(SlaveStartupDelayMilliseconds)),
            CancellationToken);

        // Act
        var masterClient = new TcpClientRx(ModbusRxMasterFixtureBase.TcpHost.ToString(), port);
        using (var master = ModbusIpMaster.CreateIp(masterClient))
        {
            master.Transport!.Retries = 0;

            var coils = await master.ReadCoilsAsync(1, 1);
            _ = Assert.Single(coils);

            _ = Assert.Single(slave.Masters);

            // Wait a bit to let slave move on to read header
            await Task.Delay(
                GetEnvironmentAppropriateTimeout(TimeSpan.FromMilliseconds(SlaveStartupDelayMilliseconds)),
                CancellationToken);
        }

        // Give the slave some time to remove the master
        await Task.Delay(
            GetEnvironmentAppropriateTimeout(TimeSpan.FromMilliseconds(SlaveStartupDelayMilliseconds)),
            CancellationToken);
        Assert.Empty(slave.Masters);
    }

    /// <summary>Modbuses the TCP slave multi threaded.</summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous unit test.</returns>
    [TUnit.Core.Test]
    public async Task ModbusTcpSlave_MultiThreadedAsync()
    {
        // Arrange
        var port = await GetAvailablePortAsync();
        var slaveListener = new TcpListener(ModbusRxMasterFixtureBase.TcpHost, port);
        var slave = ModbusTcpSlave.CreateTcp(ModbusRxMasterFixtureBase.SlaveAddress, slaveListener);
        RegisterDisposable(slave);

        _ = Task.Run(async () =>
        {
            try
            {
                await slave.ListenAsync();
            }
            catch (OperationCanceledException)
            {
                // Expected when stopping
            }
            catch (ObjectDisposedException)
            {
                // Expected when disposed
            }
            catch (SocketException ex) when (ex.ErrorCode == CleanupSocketErrorCode)
            {
                // Expected when I/O operation is aborted due to thread exit or application request
                // This is normal during test cleanup in CI environments
            }
            catch (SocketException)
            {
                // Other socket exceptions during cleanup are also expected
            }
        });

        // Wait for slave to start
        await Task.Delay(
            GetEnvironmentAppropriateTimeout(TimeSpan.FromMilliseconds(SlaveStartupDelayMilliseconds)),
            CancellationToken);

        // Act
        var workerTask1 = ReadAsync(port);
        var workerTask2 = ReadAsync(port);

        await Task.WhenAll(workerTask1, workerTask2);
    }

    /// <summary>Reads from the specified port asynchronously.</summary>
    /// <param name="port">The port to connect to.</param>
    /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
    private async Task ReadAsync(int port)
    {
        var masterClient = new TcpClientRx(ModbusRxMasterFixtureBase.TcpHost.ToString(), port);
        using var master = ModbusIpMaster.CreateIp(masterClient);
        master.Transport!.Retries = 0;

        for (var i = 0; i < WorkerReadCount; i++)
        {
            var coils = await master.ReadCoilsAsync(1, 1);
            _ = Assert.Single(coils);
            Debug.WriteLine($"{Environment.CurrentManagedThreadId}: Reading coil value");

            var delay = GetEnvironmentAppropriateTimeout(
                TimeSpan.FromMilliseconds(RandomNumberGenerator.GetInt32(SlaveStartupDelayMilliseconds)));
            await Task.Delay(delay, CancellationToken);
        }
    }
}
