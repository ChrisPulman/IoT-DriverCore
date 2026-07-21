// Copyright (c) 2022-2026 Chris Pulman. All rights reserved.
// Chris Pulman licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System;
using System.IO.Ports;
using System.Net;
using System.Net.Sockets;
using System.Threading.Tasks;
using CP.IO.Ports;
using ModbusRx.Data;
using ModbusRx.Device;

namespace ModbusRx.IntegrationTests;

/// <summary>
/// Test case examples for different Modbus communication modes.
/// These are not unit tests but examples for manual testing and development.
/// </summary>
internal static class TestCases
{
    /// <summary>The serial baud rate used by the manual examples.</summary>
    private const int BaudRate = 9600;

    /// <summary>The number of data bits used by the manual serial example.</summary>
    private const int DataBits = 8;

    /// <summary>The Modbus slave address used by the manual examples.</summary>
    private const byte SlaveAddress = 1;

    /// <summary>The TCP and UDP port used by the manual examples.</summary>
    private const int ModbusTcpPort = 502;

    /// <summary>The number of registers read by each manual example.</summary>
    private const ushort RegisterCount = 5;

    /// <summary>Runs the serial communication example, which requires physical hardware.</summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
    internal static async Task SerialAsync()
    {
        using var masterPort = new SerialPortRx("COM2");
        using var slavePort = new SerialPortRx("COM1");

        // Configure serial ports
        masterPort.BaudRate = BaudRate;
        slavePort.BaudRate = BaudRate;
        masterPort.DataBits = DataBits;
        slavePort.DataBits = DataBits;
        masterPort.Parity = Parity.None;
        slavePort.Parity = Parity.None;
        masterPort.StopBits = StopBits.One;
        slavePort.StopBits = StopBits.One;
        await masterPort.OpenAsync();
        await slavePort.OpenAsync();

        using var slave = ModbusSerialSlave.CreateRtu(SlaveAddress, slavePort);
        StartSlave(slave);

        // Create modbus master
        using var master = ModbusSerialMaster.CreateRtu(masterPort);
        await ReadRegistersAsync(master);
    }

    /// <summary>Example for TCP communication testing. CI-safe as it uses localhost only.</summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
    internal static async Task TcpAsync()
    {
        var slaveClient = new TcpListener(IPAddress.Loopback, ModbusTcpPort);
        using var slave = ModbusTcpSlave.CreateTcp(SlaveAddress, slaveClient);
        StartSlave(slave);

        var masterClient = new TcpClientRx(IPAddress.Loopback.ToString(), ModbusTcpPort);

        using var master = ModbusIpMaster.CreateIp(masterClient);
        await ReadRegistersAsync(master);
    }

    /// <summary>Example for UDP communication testing. CI-safe as it uses localhost only.</summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
    internal static async Task UdpAsync()
    {
        var slaveClient = new UdpClientRx(ModbusTcpPort);
        using var slave = ModbusUdpSlave.CreateUdp(slaveClient);
        StartSlave(slave);

        var masterClient = new UdpClientRx();
        var endPoint = new IPEndPoint(IPAddress.Loopback, ModbusTcpPort);
        masterClient.Connect(endPoint);

        using var master = ModbusIpMaster.CreateIp(masterClient);
        await ReadRegistersAsync(master);
    }

    /// <summary>Starts a slave with background task instead of Thread.</summary>
    /// <param name="slave">The slave to start.</param>
    private static void StartSlave(ModbusSlave slave)
    {
        slave.DataStore = DataStoreFactory.CreateTestDataStore();

        // Use Task.Run instead of Thread for better async patterns
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
        });
    }

    /// <summary>Reads registers asynchronously.</summary>
    /// <param name="master">The master to read from.</param>
    /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
    private static async Task ReadRegistersAsync(IModbusMaster master)
    {
        var result = await master.ReadHoldingRegistersAsync(SlaveAddress, 0, RegisterCount);

        System.Diagnostics.Debug.WriteLine($"Read {result.Length} registers: [{string.Join(", ", result)}]");
    }
}
