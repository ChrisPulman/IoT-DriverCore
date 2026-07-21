// Copyright (c) 2022-2026 Chris Pulman. All rights reserved.
// Chris Pulman licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.IO.Ports;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using CP.IO.Ports;
using ModbusRx.Data;
using ModbusRx.Device;
using ModbusRx.IntegrationTests.CustomMessages;

namespace ModbusRx.IntegrationTests;

/// <summary>Tests the ModbusMasterFixture behavior.</summary>
/// <seealso cref="System.IDisposable" />
public abstract class ModbusRxMasterFixtureBase : NetworkTestBase
{
    /// <summary>The port.</summary>
    public static readonly int Port = 502;

    /// <summary>The slave address.</summary>
    public static readonly byte SlaveAddress = 1;

    /// <summary>The default master serial port name.</summary>
    public static readonly string DefaultMasterSerialPortName = "COM1";

    /// <summary>The default slave serial port name.</summary>
    public static readonly string DefaultSlaveSerialPortName = "COM2";

    /// <summary>A value indicating whether this fixture has been disposed.</summary>
    private bool _disposedValue;

    /// <summary>Gets the TCP host.</summary>
    /// <value>
    /// The TCP host.
    /// </value>
    public static IPAddress TcpHost { get; } = IPAddress.Loopback;

    /// <summary>Gets the default modbus ip end point.</summary>
    /// <value>
    /// The default modbus ip end point.
    /// </value>
    public static IPEndPoint DefaultModbusIPEndPoint { get; } = new(TcpHost, Port);

    /// <summary>Gets the average read time.</summary>
    /// <value>
    /// The average read time.
    /// </value>
    public static double AverageReadTime
    {
        get
        {
            const int continuousIntegrationAverageReadTimeMilliseconds = 300;
            const int defaultAverageReadTimeMilliseconds = 150;

            return IsRunningInCI
                ? continuousIntegrationAverageReadTimeMilliseconds
                : defaultAverageReadTimeMilliseconds;
        }
    }

    /// <summary>Gets the transport used by the fixture.</summary>
    protected abstract string TransportName { get; }

    /// <summary>Gets or sets the master.</summary>
    /// <value>
    /// The master.
    /// </value>
    protected ModbusMaster? Master { get; set; }

    /// <summary>Gets or sets the master serial port.</summary>
    /// <value>
    /// The master serial port.
    /// </value>
    protected SerialPortRx? MasterSerialPort { get; set; }

    /// <summary>Gets or sets the master TCP.</summary>
    /// <value>
    /// The master TCP.
    /// </value>
    protected TcpClientRx? MasterTcp { get; set; }

    /// <summary>Gets or sets the master UDP.</summary>
    /// <value>
    /// The master UDP.
    /// </value>
    protected UdpClientRx? MasterUdp { get; set; }

    /// <summary>Gets or sets the slave.</summary>
    /// <value>
    /// The slave.
    /// </value>
    protected ModbusSlave? Slave { get; set; }

    /// <summary>Gets or sets the slave serial port.</summary>
    /// <value>
    /// The slave serial port.
    /// </value>
    protected SerialPortRx? SlaveSerialPort { get; set; }

    /// <summary>Gets or sets the slave TCP.</summary>
    /// <value>
    /// The slave TCP.
    /// </value>
    protected TcpListener? SlaveTcp { get; set; }

    /// <summary>Gets or sets the slave UDP.</summary>
    /// <value>
    /// The slave UDP.
    /// </value>
    protected UdpClientRx? SlaveUdp { get; set; }

    /// <summary>Gets or sets the slave task.</summary>
    /// <value>
    /// The slave task.
    /// </value>
    private Task? SlaveTask { get; set; }

    /// <summary>Gets or sets the slave cancellation token source.</summary>
    /// <value>
    /// The slave cancellation token source.
    /// </value>
    private CancellationTokenSource? SlaveCancellationTokenSource { get; set; }

    /// <summary>Gets or sets the jamod.</summary>
    /// <value>
    /// The jamod.
    /// </value>
    private Process? Jamod { get; set; }

    /// <summary>Creates the and open serial port.</summary>
    /// <param name="portName">Name of the port.</param>
    /// <returns>A SerialPort.</returns>
    public static SerialPortRx CreateAndOpenSerialPort(string portName)
    {
        var port = new SerialPortRx(portName)
        {
            Parity = Parity.None,
        };
        _ = port.OpenAsync();

        return port;
    }

    /// <summary>Reads the coils.</summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous unit test.</returns>
    [TUnit.Core.Test]
    public virtual async Task ReadCoilsAsync()
    {
        const ushort startAddress = 2048;
        const ushort numberOfPoints = 8;

        var coils = await Master!.ReadCoilsAsync(SlaveAddress, startAddress, numberOfPoints);
        Assert.Equal([false, false, false, false, false, false, false, false,], coils);
    }

    /// <summary>Reads the inputs.</summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous unit test.</returns>
    [TUnit.Core.Test]
    public virtual async Task ReadInputsAsync()
    {
        const ushort startAddress = 150;
        const ushort numberOfPoints = 3;

        var inputs = await Master!.ReadInputsAsync(SlaveAddress, startAddress, numberOfPoints);
        Assert.Equal([false, false, false,], inputs);
    }

    /// <summary>Reads the holding registers.</summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous unit test.</returns>
    [TUnit.Core.Test]
    public virtual async Task ReadHoldingRegistersAsync()
    {
        const ushort startAddress = 104;
        const ushort numberOfPoints = 2;

        var registers = await Master!.ReadHoldingRegistersAsync(SlaveAddress, startAddress, numberOfPoints);
        Assert.Equal([0, 0,], registers);
    }

    /// <summary>Reads the input registers.</summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous unit test.</returns>
    [TUnit.Core.Test]
    public virtual async Task ReadInputRegistersAsync()
    {
        const ushort startAddress = 104;
        const ushort numberOfPoints = 2;

        var registers = await Master!.ReadInputRegistersAsync(SlaveAddress, startAddress, numberOfPoints);
        Assert.Equal([0, 0,], registers);
    }

    /// <summary>Writes the single coil.</summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous unit test.</returns>
    [TUnit.Core.Test]
    public virtual async Task WriteSingleCoilAsync()
    {
        const ushort coilAddress = 10;

        var coilValue = await Master!.ReadCoilsAsync(SlaveAddress, coilAddress, 1);
        await Master.WriteSingleCoilAsync(SlaveAddress, coilAddress, !coilValue[0]);
        Assert.Equal(!coilValue[0], (await Master.ReadCoilsAsync(SlaveAddress, coilAddress, 1))[0]);
        await Master.WriteSingleCoilAsync(SlaveAddress, coilAddress, coilValue[0]);
        Assert.Equal(coilValue[0], (await Master.ReadCoilsAsync(SlaveAddress, coilAddress, 1))[0]);
    }

    /// <summary>Writes the single register.</summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous unit test.</returns>
    [TUnit.Core.Test]
    public virtual async Task WriteSingleRegisterAsync()
    {
        const ushort testAddress = 200;
        const ushort testValue = 350;

        var originalValue = await Master!.ReadHoldingRegistersAsync(SlaveAddress, testAddress, 1);
        await Master.WriteSingleRegisterAsync(SlaveAddress, testAddress, testValue);
        Assert.Equal(testValue, (await Master.ReadHoldingRegistersAsync(SlaveAddress, testAddress, 1))[0]);
        await Master.WriteSingleRegisterAsync(SlaveAddress, testAddress, originalValue[0]);
        Assert.Equal(originalValue[0], (await Master.ReadHoldingRegistersAsync(SlaveAddress, testAddress, 1))[0]);
    }

    /// <summary>Writes the multiple registers.</summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous unit test.</returns>
    [TUnit.Core.Test]
    public virtual async Task WriteMultipleRegistersAsync()
    {
        const ushort testAddress = 120;
        var testValues = new ushort[] { 10, 20, 30, 40, 50 };

        var originalValues = await Master!.ReadHoldingRegistersAsync(
            SlaveAddress,
            testAddress,
            (ushort)testValues.Length);
        await Master.WriteMultipleRegistersAsync(SlaveAddress, testAddress, testValues);
        var newValues = await Master.ReadHoldingRegistersAsync(SlaveAddress, testAddress, (ushort)testValues.Length);
        Assert.Equal(testValues, newValues);
        await Master.WriteMultipleRegistersAsync(SlaveAddress, testAddress, originalValues);
    }

    /// <summary>Writes the multiple coils.</summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous unit test.</returns>
    [TUnit.Core.Test]
    public virtual async Task WriteMultipleCoilsAsync()
    {
        const ushort testAddress = 200;
        var testValues = new bool[] { true, false, true, false, false, false, true, false, true, false };

        var originalValues = await Master!.ReadCoilsAsync(SlaveAddress, testAddress, (ushort)testValues.Length);
        await Master.WriteMultipleCoilsAsync(SlaveAddress, testAddress, testValues);
        var newValues = await Master.ReadCoilsAsync(SlaveAddress, testAddress, (ushort)testValues.Length);
        Assert.Equal(testValues, newValues);
        await Master.WriteMultipleCoilsAsync(SlaveAddress, testAddress, originalValues);
    }

    /// <summary>Reads the maximum number of holding registers.</summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous unit test.</returns>
    [TUnit.Core.Test]
    public virtual async Task ReadMaximumNumberOfHoldingRegistersAsync()
    {
        const ushort startAddress = 104;
        const ushort maximumNumberOfRegisters = 125;

        var registers = await Master!.ReadHoldingRegistersAsync(
            SlaveAddress,
            startAddress,
            maximumNumberOfRegisters);
        Assert.Equal(maximumNumberOfRegisters, registers.Length);
    }

    /// <summary>Reads the write multiple registers.</summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous unit test.</returns>
    [TUnit.Core.Test]
    public virtual async Task ReadWriteMultipleRegistersAsync()
    {
        const ushort startReadAddress = 120;
        const ushort numberOfPointsToRead = 5;
        const ushort startWriteAddress = 50;
        var valuesToWrite = new ushort[] { 10, 20, 30, 40, 50 };

        var valuesToRead = await Master!.ReadHoldingRegistersAsync(
            SlaveAddress,
            startReadAddress,
            numberOfPointsToRead);
        var readValues = await Master.ReadWriteMultipleRegistersAsync(
            SlaveAddress,
            startReadAddress,
            numberOfPointsToRead,
            startWriteAddress,
            valuesToWrite);
        Assert.Equal(valuesToRead, readValues);

        var writtenValues = await Master.ReadHoldingRegistersAsync(
            SlaveAddress,
            startWriteAddress,
            (ushort)valuesToWrite.Length);
        Assert.Equal(valuesToWrite, writtenValues);
    }

    /// <summary>Simples the read registers performance test.</summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous unit test.</returns>
    [TUnit.Core.Test]
    public virtual async Task SimpleReadRegistersPerformanceTestAsync()
    {
        const int retryCount = 5;

        Skip.If(IsRunningInCI, "Performance timing assertions are unreliable in CI environments.");

        var retries = Master!.Transport!.Retries;
        Master.Transport!.Retries = retryCount;
        var actualAverageReadTime = await CalculateAverageAsync(Master);
        Master.Transport.Retries = retries;
        Assert.True(
            actualAverageReadTime < AverageReadTime,
            string.Format(
                CultureInfo.InvariantCulture,
                "Test failed, actual average read time {0} is greater than expected {1}",
                actualAverageReadTime,
                AverageReadTime));
    }

    /// <summary>Executes the custom message read holding registers.</summary>
    [TUnit.Core.Test]
    public virtual void ExecuteCustomMessage_ReadHoldingRegisters()
    {
        const byte functionCode = 3;
        const ushort startAddress = 104;
        const ushort numberOfPoints = 2;

        var request = new CustomReadHoldingRegistersRequest(
            functionCode,
            SlaveAddress,
            startAddress,
            numberOfPoints);
        var response = Master!.ExecuteCustomMessage(request, static () => new CustomReadHoldingRegistersResponse());
        Assert.Equal([0, 0,], response.Data);
    }

    /// <summary>Executes the custom message write multiple registers.</summary>
    [TUnit.Core.Test]
    public virtual void ExecuteCustomMessage_WriteMultipleRegisters()
    {
        const ushort testAddress = 120;
        const byte readFunctionCode = 3;
        const byte writeFunctionCode = 16;
        var testValues = new ushort[] { 10, 20, 30, 40, 50 };
        var readRequest = new CustomReadHoldingRegistersRequest(
            readFunctionCode,
            SlaveAddress,
            testAddress,
            (ushort)testValues.Length);
        var writeRequest = new CustomWriteMultipleRegistersRequest(
            writeFunctionCode,
            SlaveAddress,
            testAddress,
            new RegisterCollection(testValues));

        var response = Master!.ExecuteCustomMessage(
            readRequest,
            static () => new CustomReadHoldingRegistersResponse());
        var originalValues = response.Data;
        _ = Master.ExecuteCustomMessage(writeRequest, static () => new CustomWriteMultipleRegistersResponse());
        response = Master.ExecuteCustomMessage(
            readRequest,
            static () => new CustomReadHoldingRegistersResponse());
        var newValues = response.Data;
        Assert.Equal(testValues, newValues);
        writeRequest = new(
            writeFunctionCode,
            SlaveAddress,
            testAddress,
            new RegisterCollection(originalValues));
        _ = Master.ExecuteCustomMessage(writeRequest, static () => new CustomWriteMultipleRegistersResponse());
    }

    /// <summary>Calculates the average.</summary>
    /// <param name="master">The master.</param>
    /// <returns>A double.</returns>
    internal static async Task<double> CalculateAverageAsync(IModbusMaster master)
    {
        const ushort startAddress = 5;
        const ushort numRegisters = 5;
        const double continuousIntegrationReadCount = 25.0;
        const double defaultReadCount = 50.0;

        // JIT compile the IL
        await master.ReadHoldingRegistersAsync(SlaveAddress, startAddress, numRegisters);

        var stopwatch = new Stopwatch();
        long sum = 0;
        var numberOfReads = IsRunningInCI ? continuousIntegrationReadCount : defaultReadCount;

        for (var i = 0; i < numberOfReads; i++)
        {
            stopwatch.Reset();
            stopwatch.Start();
            await master.ReadHoldingRegistersAsync(SlaveAddress, startAddress, numRegisters);
            stopwatch.Stop();

            checked
            {
                sum += stopwatch.ElapsedMilliseconds;
            }
        }

        return sum / numberOfReads;
    }

    /// <summary>Setups the slave serial port.</summary>
    protected void SetupSlaveSerialPort()
    {
        SlaveSerialPort = new(DefaultSlaveSerialPortName)
        {
            Parity = Parity.None,
        };
        _ = SlaveSerialPort.OpenAsync();
        RegisterDisposable(SlaveSerialPort);
    }

    /// <summary>Starts the slave.</summary>
    protected void StartSlave()
    {
        const int operationAbortedErrorCode = 995;

        if (Slave is null)
        {
            return;
        }

        RegisterDisposable(Slave);

        // Create cancellation token source for the slave
        SlaveCancellationTokenSource = new();
        RegisterDisposable(SlaveCancellationTokenSource);

        SlaveTask = Task.Run(
            async () =>
            {
                try
                {
                    await Slave.ListenAsync();
                }
                catch (OperationCanceledException)
                {
                    // Expected when cancellation is requested
                }
                catch (ObjectDisposedException)
                {
                    // Expected when resources are disposed
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
            },
            SlaveCancellationTokenSource.Token);
    }

    /// <summary>Starts the jamod slave.</summary>
    /// <param name="program">The program.</param>
    /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
    protected async Task StartJamodSlaveAsync(string program)
    {
        const int startupTimeoutSeconds = 4;
        const int continuousIntegrationStartupTimeoutSeconds = 2;

        var assemblyDirectory = Path.GetDirectoryName(typeof(ModbusRxMasterFixtureBase).Assembly.Location)
            ?? throw new DirectoryNotFoundException("The integration test assembly directory could not be located.");
        var pathToJamod = Path.Combine(assemblyDirectory, "../../../../tools/jamod");
        var classpath = string.Format(
            @"-classpath ""{0};{1};{2}""",
            Path.Combine(pathToJamod, "jamod.jar"),
            Path.Combine(pathToJamod, "comm.jar"),
            Path.Combine(pathToJamod, "."));
        var startInfo = new ProcessStartInfo(
            "java",
            string.Format(CultureInfo.InvariantCulture, "{0} {1}", classpath, program));
        Jamod = Process.Start(startInfo);

        var timeout = GetEnvironmentAppropriateTimeout(
            TimeSpan.FromSeconds(startupTimeoutSeconds),
            TimeSpan.FromSeconds(continuousIntegrationStartupTimeoutSeconds));
        await Task.Delay(timeout, CancellationToken);
        Assert.False(Jamod?.HasExited ?? true, "Jamod Serial Ascii Slave did not start correctly.");
    }

    /// <summary>Releases unmanaged and - optionally - managed resources.</summary>
    /// <param name="disposing">
    /// <c>true</c> to release both managed and unmanaged resources; <c>false</c> to release only unmanaged resources.
    /// </param>
    protected override void Dispose(bool disposing)
    {
        if (_disposedValue)
        {
            base.Dispose(disposing);
            return;
        }

        if (disposing)
        {
            CancelSlaveOperations();
            StopSlaveTcpListener();
            DisposeSlaveAndMaster();
            WaitForSlaveTask();
            StopJamodProcess();
            RegisterAdditionalResources();
        }

        _disposedValue = true;
        base.Dispose(disposing);
    }

    /// <summary>Executes cleanup while ignoring expected cleanup exceptions.</summary>
    /// <param name="action">The cleanup action.</param>
    private static void IgnoreExpectedCleanupException(Action action)
    {
        try
        {
            action();
        }
        catch (ObjectDisposedException)
        {
            // Expected during fixture cleanup.
        }
        catch (SocketException)
        {
            // Expected during fixture cleanup.
        }
        catch (IOException)
        {
            // Expected during fixture cleanup.
        }
        catch (InvalidOperationException)
        {
            // Expected during fixture cleanup.
        }
        catch (AggregateException)
        {
            // Expected during fixture cleanup.
        }
        catch (Win32Exception)
        {
            // Expected during fixture cleanup.
        }
    }

    /// <summary>Cancels slave operations during cleanup.</summary>
    private void CancelSlaveOperations()
    {
        IgnoreExpectedCleanupException(() => SlaveCancellationTokenSource?.Cancel());
    }

    /// <summary>Stops the TCP listener before slave disposal.</summary>
    private void StopSlaveTcpListener()
    {
        if (SlaveTcp is null)
        {
            return;
        }

        IgnoreExpectedCleanupException(SlaveTcp.Stop);
    }

    /// <summary>Disposes slave and master instances.</summary>
    private void DisposeSlaveAndMaster()
    {
        IgnoreExpectedCleanupException(() => Slave?.Dispose());
        IgnoreExpectedCleanupException(() => Master?.Dispose());
    }

    /// <summary>Waits briefly for the slave task to complete.</summary>
    private void WaitForSlaveTask()
    {
        const int timeoutSeconds = 2;
        const int continuousIntegrationTimeoutSeconds = 1;

        if (SlaveTask?.IsCompleted != false)
        {
            return;
        }

        IgnoreExpectedCleanupException(() =>
        {
            var timeout = GetEnvironmentAppropriateTimeout(
                TimeSpan.FromSeconds(timeoutSeconds),
                TimeSpan.FromSeconds(continuousIntegrationTimeoutSeconds));
            _ = SlaveTask.Wait(timeout);
        });
    }

    /// <summary>Stops the Jamod process during cleanup.</summary>
    private void StopJamodProcess()
    {
        const int timeoutSeconds = 2;
        const int continuousIntegrationTimeoutSeconds = 1;

        if (Jamod is null)
        {
            return;
        }

        IgnoreExpectedCleanupException(() =>
        {
            Jamod.Kill();
            Thread.Sleep(
                GetEnvironmentAppropriateTimeout(
                    TimeSpan.FromSeconds(timeoutSeconds),
                    TimeSpan.FromSeconds(continuousIntegrationTimeoutSeconds)));
        });
    }

    /// <summary>Registers remaining transport resources for base cleanup.</summary>
    private void RegisterAdditionalResources()
    {
        RegisterDisposableIfNotNull(MasterTcp);
        RegisterDisposableIfNotNull(MasterUdp);
        RegisterDisposableIfNotNull(MasterSerialPort);
        RegisterDisposableIfNotNull(SlaveSerialPort);
        RegisterDisposableIfNotNull(SlaveUdp);
    }

    /// <summary>Registers a disposable resource when it is available.</summary>
    /// <param name="disposable">The disposable resource.</param>
    private void RegisterDisposableIfNotNull(IDisposable? disposable)
    {
        if (disposable is null)
        {
            return;
        }

        RegisterDisposable(disposable);
    }
}
