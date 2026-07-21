// Copyright (c) 2019-2026 Chris Pulman and contributors. All rights reserved.
// Chris Pulman and contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using ModbusRx.Data;
using ModbusRx.Device;
using ReactiveModbusServer = ModbusRx.Reactive.Device.ModbusServer;
using ReactiveModbusServerExtensions = ModbusRx.Reactive.ModbusServerExtensions;

namespace ModbusRx.UnitTests;

/// <summary>Unit tests for ModbusServer.</summary>
public class ModbusServerTests
{
    /// <summary>The IPv4 loopback address used by network tests.</summary>
    private const string LoopbackAddress = "127.0.0.1";

    /// <summary>Gets a value indicating whether the tests are running in CI environment.</summary>
    private static bool IsRunningInCI =>
        !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("GITHUB_ACTIONS")) ||
        !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("CI"));

    /// <summary>Tests that ModbusServer can be created and disposed properly.</summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous unit test.</returns>
    [TUnit.Core.Test]
    public async Task ModbusServer_CreateAndDispose_ShouldNotThrowAsync()
    {
        // Arrange & Act & Assert
        using var server = new ModbusServer();
        _ = Assert.NotNull(server);
        var isRunning = await server.IsRunning.FirstAsync().ToTask();
        Assert.False(isRunning);
    }

    /// <summary>Tests that ModbusServer can start and stop properly.</summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous unit test.</returns>
    [TUnit.Core.Test]
    public async Task ModbusServer_StartAndStop_ShouldUpdateRunningStateAsync()
    {
        // Arrange
        using var server = new ModbusServer();

        // Act
        server.Start();

        // Assert
        var isRunning = await server.IsRunning.FirstAsync().ToTask();
        Assert.True(isRunning);

        // Act
        server.Stop();

        // Assert
        isRunning = await server.IsRunning.FirstAsync().ToTask();
        Assert.False(isRunning);
    }

    /// <summary>Tests that simulation mode can be enabled and disabled.</summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous unit test.</returns>
    [TUnit.Core.Test]
    public async Task ModbusServer_SimulationMode_ShouldUpdateDataStoreAsync()
    {
        // Arrange
        using var server = new ModbusServer();
        server.Start();

        // Capture initial state to verify change
        var initialData = server.GetCurrentData();
        var initialSum = SumFirst(initialData.HoldingRegisters, Num.Value10);

        // Act
        server.SimulationMode = true;

        // Wait for simulation to run - simulation runs every 500ms so wait at least that long
        var baseInterval = TimeSpan.FromMilliseconds(Num.Value700); // Longer than the 500ms simulation interval
        var timeout = GetEnvironmentTimeout(baseInterval);
        var maxRetries = IsRunningInCI ? Num.Value8 : Num.Value3; // More retries in CI due to slower execution
        var dataHasChanged = false;

        for (var retry = 0; retry < maxRetries && !dataHasChanged; retry++)
        {
            await Task.Delay(timeout);
            var currentData = server.GetCurrentData();

            // Check if any holding registers have non-zero values OR if data has changed from initial state
            var currentSum = SumFirst(currentData.HoldingRegisters, Num.Value10);
            var hasNonZeroValues = ContainsPositiveValue(currentData.HoldingRegisters);
            var sumChanged = currentSum != initialSum;

            dataHasChanged = hasNonZeroValues || sumChanged;

            if (!dataHasChanged && retry < maxRetries - 1)
            {
                // If no data yet, wait a bit more for next retry
                await Task.Delay(GetEnvironmentTimeout(TimeSpan.FromMilliseconds(Num.Value300)));
            }
        }

        // Assert
        var errorMessage = $"""
            Simulation should generate non-zero data or change from initial state after {maxRetries} attempts
            with {timeout.TotalMilliseconds}ms intervals. Initial sum: {initialSum}, simulation interval: 500ms
            """;

        Assert.True(dataHasChanged, errorMessage);

        // Act
        server.SimulationMode = false;
    }

    /// <summary>Tests that custom data can be loaded into the server.</summary>
    [TUnit.Core.Test]
    public void ModbusServer_LoadSimulationData_ShouldUpdateDataStore()
    {
        // Arrange
        using var server = new ModbusServer();
        var holdingRegs = new ushort[] { 1, 2, 3, 4, 5 };
        var inputRegs = new ushort[] { 10, 20, 30, 40, 50 };
        var coils = new bool[] { true, false, true, false, true };
        var inputs = new bool[] { false, true, false, true, false };

        // Act
        server.LoadSimulationData(holdingRegs, inputRegs, coils, inputs);
        var data = server.GetCurrentData();

        // Assert
        Assert.Equal(1, data.HoldingRegisters[0]);
        Assert.Equal(Num.Value2, data.HoldingRegisters[1]);
        Assert.Equal(Num.Value10, data.InputRegisters[0]);
        Assert.Equal(Num.Value20, data.InputRegisters[1]);
        Assert.True(data.Coils[0]);
        Assert.False(data.Coils[1]);
        Assert.False(data.Inputs[0]);
        Assert.True(data.Inputs[1]);
    }

    /// <summary>Tests that TCP server can be started and configured.</summary>
    [TUnit.Core.Test]
    public void ModbusServer_StartTcpServer_ShouldReturnDisposable()
    {
        // Arrange
        using var server = new ModbusServer();
        var port = GetAvailablePort();

        // Act
        var subscription = server.StartTcpServer(port, 1);

        // Assert
        _ = Assert.NotNull(subscription);

        // Cleanup
        subscription.Dispose();
    }

    /// <summary>Tests that UDP server can be started and configured.</summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous unit test.</returns>
    [TUnit.Core.Test]
    public async Task ModbusServer_StartUdpServer_ShouldReturnDisposableAsync()
    {
        // Arrange
        using var server = new ModbusServer();
        IDisposable? subscription = null;

        // Act
        for (var attempt = 0; attempt < Num.Value5; attempt++)
        {
            try
            {
                subscription = server.StartUdpServer(GetAvailableUdpPort(), 1);
                break;
            }
            catch (SocketException) when (attempt < Num.Value4)
            {
                await Task.Delay(Num.Value25);
            }
        }

        // Assert
        _ = Assert.NotNull(subscription);

        // Cleanup
        subscription!.Dispose();
    }

    /// <summary>Tests reactive server extensions.</summary>
    [TUnit.Core.Test]
    public void ModbusServerExtensions_CreateReactiveServer_ShouldWork()
    {
        // Arrange
        var serverCreated = false;

        // Act
        using var subscription = ReactiveModbusServerExtensions.CreateReactiveServer(server =>
        {
            server.SimulationMode = true;
            serverCreated = true;
        }).Subscribe();

        // Assert
        Assert.True(serverCreated);
    }

    /// <summary>Tests data observation extensions.</summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous unit test.</returns>
    [TUnit.Core.Test]
    public async Task ModbusServerExtensions_ObserveDataChanges_ShouldEmitDataAsync()
    {
        // Arrange
        using var server = new ReactiveModbusServer();
        server.Start();
        server.SimulationMode = true;

        var timeout = GetEnvironmentTimeout(TimeSpan.FromSeconds(Num.Value2));

        // Act
        var dataReceived = new TaskCompletionSource<
            (ushort[] HoldingRegisters, ushort[] InputRegisters, bool[] Coils, bool[] Inputs)>();
        using var subscription = ReactiveModbusServerExtensions.ObserveDataChanges(server, Num.Value50)
            .Subscribe(value => dataReceived.TrySetResult(value));

#if NET6_0_OR_GREATER
        var data = await dataReceived.Task.WaitAsync(timeout);
#else
        var dataTask = dataReceived.Task;
        if (await Task.WhenAny(dataTask, Task.Delay(timeout)).ConfigureAwait(false) != dataTask)
        {
            throw new TimeoutException("Timed out waiting for data changes.");
        }

        var data = await dataTask;
#endif

        _ = Assert.NotNull(data.HoldingRegisters);
        _ = Assert.NotNull(data.InputRegisters);
        _ = Assert.NotNull(data.Coils);
        _ = Assert.NotNull(data.Inputs);
    }

    /// <summary>Tests holding register observation.</summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous unit test.</returns>
    [TUnit.Core.Test]
    public async Task ModbusServerExtensions_ObserveHoldingRegisters_ShouldEmitChangesAsync()
    {
        // Arrange
        using var server = new ReactiveModbusServer();
        server.Start();

        var dataReceived = new TaskCompletionSource<bool>();
        var expectedData = new ushort[] { 1, 2, 3, 4, 5 };
        var timeout = GetEnvironmentTimeout(TimeSpan.FromSeconds(Num.Value2));

        // Act
        using var subscription = ReactiveModbusServerExtensions
            .ObserveHoldingRegisters(server, 0, Num.Value5, Num.Value50)
            .Take(1)
            .Subscribe(_ => dataReceived.TrySetResult(true));

        server.LoadSimulationData(expectedData, null, null, null);
        var completed = await Task.WhenAny(dataReceived.Task, Task.Delay(timeout)) == dataReceived.Task;

        // Assert
        Assert.True(completed);
    }

    /// <summary>Tests adding TCP client configuration.</summary>
    [TUnit.Core.Test]
    public void ModbusServer_AddTcpClient_WithValidParameters_ShouldReturnDisposable()
    {
        // Arrange
        using var server = new ModbusServer();
#if NET5_0_OR_GREATER
        using var listener = new TcpListener(IPAddress.Loopback, 0);
#else
        var listener = new TcpListener(IPAddress.Loopback, 0);
#endif
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;

        // Act
        var subscription = server.AddTcpClient("test", LoopbackAddress, port, 1);

        // Assert
        _ = Assert.NotNull(subscription);

        // Cleanup
        subscription.Dispose();
        listener.Stop();
    }

    /// <summary>Tests adding UDP client configuration.</summary>
    [TUnit.Core.Test]
    public void ModbusServer_AddUdpClient_WithValidParameters_ShouldReturnDisposable()
    {
        // Arrange
        using var server = new ModbusServer();

        // Act - Use a different approach that doesn't rely on specific network behavior
        var subscription = server.AddUdpClient("test", LoopbackAddress, GetAvailablePort(), 1);

        // Assert
        _ = Assert.NotNull(subscription);

        // Cleanup
        subscription.Dispose();
    }

    /// <summary>Tests that invalid client names throw exceptions.</summary>
    /// <param name="name">The name.</param>
    [TUnit.Core.Test]
    [TUnit.Core.Arguments(null)]
    [TUnit.Core.Arguments("")]
    [TUnit.Core.Arguments("   ")]
    public void ModbusServer_AddTcpClient_WithInvalidName_ShouldThrowException(string? name)
    {
        // Arrange
        using var server = new ModbusServer();

        // Act & Assert
        _ = Assert.Throws<ArgumentException>(
            () => server.AddTcpClient(name!, LoopbackAddress, Num.Value502, 1));
    }

    /// <summary>Tests custom data store assignment.</summary>
    [TUnit.Core.Test]
    public void ModbusServer_CustomDataStore_ShouldBeUsed()
    {
        // Arrange
        using var server = new ModbusServer();
        var customDataStore = DataStoreFactory.CreateTestDataStore();

        // Act
        server.DataStore = customDataStore;

        // Assert
        Assert.Equal(customDataStore, server.DataStore);
    }

    /// <summary>Tests that the server handles high-frequency data updates in CI environments.</summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous unit test.</returns>
    [TUnit.Core.Test]
    public async Task ModbusServer_HighFrequencyUpdates_ShouldWorkInCIAsync()
    {
        // Arrange
        using var server = new ReactiveModbusServer();
        server.Start();
        server.SimulationMode = true;

        var updateCount = 0;
        var maxUpdates = IsRunningInCI ? Num.Value3 : Num.Value10; // Reduce load in CI
        var observationTimeout = GetEnvironmentTimeout(TimeSpan.FromSeconds(Num.Value2));

        // Act
        using var subscription = ReactiveModbusServerExtensions.ObserveDataChanges(server, Num.Value100)
            .Take(maxUpdates)
            .Subscribe(_ => Interlocked.Increment(ref updateCount));

        await Task.Delay(observationTimeout);

        // Assert
        Assert.True(updateCount > 0, $"Expected some updates, got {updateCount}");
    }

    /// <summary>Gets an appropriate timeout based on the environment.</summary>
    /// <param name="normalTimeout">Normal timeout for local testing.</param>
    /// <returns>Appropriate timeout for the environment.</returns>
    private static TimeSpan GetEnvironmentTimeout(TimeSpan normalTimeout) => IsRunningInCI
        ? TimeSpan.FromMilliseconds(normalTimeout.TotalMilliseconds * Num.ValueHalf)
        : normalTimeout;

    /// <summary>Gets an available TCP port from the loopback interface.</summary>
    /// <returns>The available TCP port.</returns>
    private static int GetAvailablePort()
    {
#if NET5_0_OR_GREATER
        using var listener = new TcpListener(IPAddress.Loopback, 0);
#else
        var listener = new TcpListener(IPAddress.Loopback, 0);
#endif
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;
        listener.Stop();
        return port;
    }

    /// <summary>Gets an available UDP port from the loopback interface.</summary>
    /// <returns>The available UDP port.</returns>
    private static int GetAvailableUdpPort()
    {
        using var udpClient = new CP.IO.Ports.UdpClientRx(0);
        return ((IPEndPoint)udpClient.Client.LocalEndPoint!).Port;
    }

    /// <summary>Sums the first values in an array.</summary>
    /// <param name="values">The values to inspect.</param>
    /// <param name="count">The maximum number of values to sum.</param>
    /// <returns>The sum of the requested values.</returns>
    private static long SumFirst(ushort[] values, int count)
    {
        var total = 0L;
        var length = Math.Min(values.Length, count);
        for (var i = 0; i < length; i++)
        {
            total += values[i];
        }

        return total;
    }

    /// <summary>Determines whether any value is positive.</summary>
    /// <param name="values">The values to inspect.</param>
    /// <returns>A value indicating whether any value is positive.</returns>
    private static bool ContainsPositiveValue(ushort[] values)
    {
        foreach (var value in values)
        {
            if (value > 0)
            {
                return true;
            }
        }

        return false;
    }
}
