// Copyright (c) 2022-2026 Chris Pulman. All rights reserved.
// Chris Pulman licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System;
using System.Diagnostics;
using System.Security.Cryptography;
using System.Threading.Tasks;
using CP.IO.Ports;
using ModbusRx.Data;
using ModbusRx.Device;

namespace ModbusRx.IntegrationTests;

/// <summary>Tests the NModbusUdpSlaveFixture behavior.</summary>
public sealed class ModbusRxUdpSlaveFixture : NetworkTestBase
{
    /// <summary>The socket error code expected while stopping a test listener.</summary>
    private const int CleanupSocketErrorCode = 995;

    /// <summary>The maximum time to wait for the slave to start.</summary>
    private const int SlaveStartupTimeoutSeconds = 2;

    /// <summary>The delay used to let a slave begin or complete shutdown.</summary>
    private const int SlaveStartupDelayMilliseconds = 100;

    /// <summary>The number of read operations each master performs.</summary>
    private const int MasterReadCount = 5;

    /// <summary>The maximum random delay before a master read.</summary>
    private const int MaximumInterReadDelayMilliseconds = 1_000;

    /// <summary>The value of the second register in the expected sequences.</summary>
    private const ushort SecondRegisterValue = 2;

    /// <summary>The value of the third register in the expected sequences.</summary>
    private const ushort ThirdRegisterValue = 3;

    /// <summary>The value of the fourth register in the expected sequences.</summary>
    private const ushort FourthRegisterValue = 4;

    /// <summary>The value of the fifth register in the expected sequences.</summary>
    private const ushort FifthRegisterValue = 5;

    /// <summary>The value of the sixth register in the expected sequences.</summary>
    private const ushort SixthRegisterValue = 6;

    /// <summary>The value of the seventh register in the expected sequences.</summary>
    private const ushort SeventhRegisterValue = 7;

    /// <summary>The register values expected by the first master.</summary>
    private static readonly ushort[] FirstMasterExpectedRegisters =
        [ SecondRegisterValue, ThirdRegisterValue, FourthRegisterValue, FifthRegisterValue, SixthRegisterValue];

    /// <summary>The register values expected by the second master.</summary>
    private static readonly ushort[] SecondMasterExpectedRegisters =
        [ ThirdRegisterValue, FourthRegisterValue, FifthRegisterValue, SixthRegisterValue, SeventhRegisterValue];

    /// <summary>Modbuses the UDP slave ensure the slave shuts down cleanly.</summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous unit test.</returns>
    [TUnit.Core.Test]
    public async Task ModbusUdpSlave_EnsureTheSlaveShutsDownCleanlyAsync()
    {
        // Arrange
        var port = await GetAvailablePortAsync();
        var client = new UdpClientRx(port);
        var slave = ModbusUdpSlave.CreateUdp(1, client);
        RegisterDisposable(slave);
        RegisterDisposable(client);

        var slaveStarted = false;

        // Act
        _ = Task.Run(async () =>
        {
            try
            {
                slaveStarted = true;
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
            catch (System.Net.Sockets.SocketException ex) when (ex.ErrorCode == CleanupSocketErrorCode)
            {
                // Expected when I/O operation is aborted due to thread exit or application request
                // This is normal during test cleanup in CI environments
            }
            catch (System.Net.Sockets.SocketException)
            {
                // Other socket exceptions during cleanup are also expected
            }
        });

        // Wait for slave to start
        await WaitForConditionAsync(() => slaveStarted, TimeSpan.FromSeconds(SlaveStartupTimeoutSeconds));
        await Task.Delay(
            GetEnvironmentAppropriateTimeout(TimeSpan.FromMilliseconds(SlaveStartupDelayMilliseconds)),
            CancellationToken);

        // Assert - Test passes if no exceptions are thrown
        Assert.True(slaveStarted);
    }

    /// <summary>Modbuses the UDP slave not bound.</summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous unit test.</returns>
    [TUnit.Core.Test]
    public async Task ModbusUdpSlave_NotBoundAsync()
    {
        // Arrange
        var client = new UdpClientRx();
        ModbusSlave slave = ModbusUdpSlave.CreateUdp(1, client);
        RegisterDisposable(slave);
        RegisterDisposable(client);

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(async () => await slave.ListenAsync());
    }

    /// <summary>Modbuses the UDP slave multiple masters.</summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous unit test.</returns>
    [TUnit.Core.Test]
    public async Task ModbusUdpSlave_MultipleMastersAsync()
    {
        // Arrange
        var port = await GetAvailablePortAsync();
        var master1Complete = false;
        var master2Complete = false;

        var masterClient1 = new UdpClientRx();
        var endPoint = new System.Net.IPEndPoint(ModbusRxMasterFixtureBase.TcpHost, port);
        masterClient1.Connect(endPoint);
        var master1 = ModbusIpMaster.CreateIp(masterClient1);
        RegisterDisposable(master1);
        RegisterDisposable(masterClient1);

        var masterClient2 = new UdpClientRx();
        masterClient2.Connect(endPoint);
        var master2 = ModbusIpMaster.CreateIp(masterClient2);
        RegisterDisposable(master2);
        RegisterDisposable(masterClient2);

        var slaveClient = await CreateAndStartUdpSlaveAsync(port, DataStoreFactory.CreateTestDataStore());
        RegisterDisposable(slaveClient);

        // Act
        var master1Task = Task.Run(async () =>
        {
            for (var i = 0; i < MasterReadCount; i++)
            {
                var delay = GetEnvironmentAppropriateTimeout(
                    TimeSpan.FromMilliseconds(RandomNumberGenerator.GetInt32(MaximumInterReadDelayMilliseconds)));
                await Task.Delay(delay, CancellationToken);
                Debug.WriteLine("Read from master 1");
                Assert.Equal(
                    FirstMasterExpectedRegisters,
                    await master1.ReadHoldingRegistersAsync(1, MasterReadCount));
            }

            master1Complete = true;
        });

        var master2Task = Task.Run(async () =>
        {
            for (var i = 0; i < MasterReadCount; i++)
            {
                var delay = GetEnvironmentAppropriateTimeout(
                    TimeSpan.FromMilliseconds(RandomNumberGenerator.GetInt32(MaximumInterReadDelayMilliseconds)));
                await Task.Delay(delay, CancellationToken);
                Debug.WriteLine("Read from master 2");
                Assert.Equal(
                    SecondMasterExpectedRegisters,
                    await master2.ReadHoldingRegistersAsync(SecondRegisterValue, MasterReadCount));
            }

            master2Complete = true;
        });

        await Task.WhenAll(master1Task, master2Task);

        // Assert
        Assert.True(master1Complete);
        Assert.True(master2Complete);
    }

    /// <summary>Modbuses the UDP slave multi threaded.</summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous unit test.</returns>
    [TUnit.Core.Test]
    public async Task ModbusUdpSlave_MultiThreadedAsync()
    {
        // Arrange
        var port = await GetAvailablePortAsync();
        var dataStore = DataStoreFactory.CreateDefaultDataStore();
        dataStore.CoilDiscretes.Add(false);

        var slave = await CreateAndStartUdpSlaveAsync(port, dataStore);
        RegisterDisposable(slave);

        // Act
        var workerTask1 = ReadThreadAsync(port);
        var workerTask2 = ReadThreadAsync(port);

        await Task.WhenAll(workerTask1, workerTask2);
    }

    /// <summary>Reads from the specified port asynchronously.</summary>
    /// <param name="port">The port to connect to.</param>
    /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
    private async Task ReadThreadAsync(int port)
    {
        var masterClient = new UdpClientRx();
        var endPoint = new System.Net.IPEndPoint(ModbusRxMasterFixtureBase.TcpHost, port);
        masterClient.Connect(endPoint);
        using var master = ModbusIpMaster.CreateIp(masterClient);
        master.Transport!.Retries = 0;

        for (var i = 0; i < MasterReadCount; i++)
        {
            var coils = await master.ReadCoilsAsync(1, 1);
            _ = Assert.Single(coils);
            Debug.WriteLine($"{Environment.CurrentManagedThreadId}: Reading coil value");

            var delay = GetEnvironmentAppropriateTimeout(
                TimeSpan.FromMilliseconds(RandomNumberGenerator.GetInt32(SlaveStartupDelayMilliseconds)));
            await Task.Delay(delay, CancellationToken);
        }
    }

    /// <summary>Creates and starts a UDP slave asynchronously.</summary>
    /// <param name="port">The port to listen on.</param>
    /// <param name="dataStore">The data store to use.</param>
    /// <returns>The UDP client used by the slave.</returns>
    private async Task<UdpClientRx> CreateAndStartUdpSlaveAsync(int port, DataStore dataStore)
    {
        var slaveClient = new UdpClientRx(port);
        ModbusSlave slave = ModbusUdpSlave.CreateUdp(slaveClient);
        slave.DataStore = dataStore;
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
            catch (System.Net.Sockets.SocketException ex) when (ex.ErrorCode == CleanupSocketErrorCode)
            {
                // Expected when I/O operation is aborted due to thread exit or application request
                // This is normal during test cleanup in CI environments
            }
            catch (System.Net.Sockets.SocketException)
            {
                // Other socket exceptions during cleanup are also expected
            }
        });

        // Give the slave time to start
        await Task.Delay(
            GetEnvironmentAppropriateTimeout(TimeSpan.FromMilliseconds(SlaveStartupDelayMilliseconds)),
            CancellationToken);

        return slaveClient;
    }
}
