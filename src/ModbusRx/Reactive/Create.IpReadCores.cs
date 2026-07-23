// Copyright (c) 2019-2026 Chris Pulman and contributors. All rights reserved.
// Chris Pulman and contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.IO.Ports;
#if REACTIVE_SHIM
using IoT.DriverCore.ModbusRx.Reactive.Device;
using IoT.DriverCore.Serial.Reactive;
#else
using IoT.DriverCore.ModbusRx.Device;
using IoT.DriverCore.Serial;
#endif

#if REACTIVE_SHIM
namespace IoT.DriverCore.ModbusRx.Reactive;
#else
namespace IoT.DriverCore.ModbusRx;
#endif

/// <summary>Provides ModbusRx functionality.</summary>
public static partial class Create
{
    /// <summary>Gets or sets the serial-port factory override used by deterministic internal testing.</summary>
    internal static Func<string, int, int, Parity, StopBits, Handshake, SerialPortRx>?
        SerialPortFactoryOverride { get; set; }

    /// <summary>Gets or sets the available-port observation override used by deterministic internal testing.</summary>
    internal static Func<IObservable<string[]>>? SerialPortNamesOverride { get; set; }

    /// <summary>Reads coils from an IP master.</summary>
    /// <param name="source">The source master stream.</param>
    /// <param name="startAddress">The starting address.</param>
    /// <param name="numberOfPoints">The number of points to read.</param>
    /// <param name="interval">The polling interval in milliseconds.</param>
    /// <returns>An observable sequence producing data or an error.</returns>
    internal static IObservable<(bool[]? Data, Exception? Error)> ReadCoilsCore(
        IObservable<(bool Connected, Exception? Error, ModbusIpMaster? Master)> source,
        ushort startAddress,
        ushort numberOfPoints,
        double interval = 1000.0) =>
        ReadMasterCore(
            source,
            master => master.ReadCoilsAsync(startAddress, numberOfPoints),
            "Read Coils Error",
            interval);

    /// <summary>Reads holding registers from an IP master.</summary>
    /// <param name="source">The source master stream.</param>
    /// <param name="startAddress">The starting address.</param>
    /// <param name="numberOfPoints">The number of points to read.</param>
    /// <param name="interval">The polling interval in milliseconds.</param>
    /// <returns>An observable sequence producing data or an error.</returns>
    internal static IObservable<(ushort[]? Data, Exception? Error)> ReadHoldingRegistersCore(
        IObservable<(bool Connected, Exception? Error, ModbusIpMaster? Master)> source,
        ushort startAddress,
        ushort numberOfPoints,
        double interval = 1000.0) =>
        ReadMasterCore(
            source,
            master => master.ReadHoldingRegistersAsync(startAddress, numberOfPoints),
            "Read Holding Registers Error",
            interval,
            nameof(ReadHoldingRegisters));

    /// <summary>Reads input registers from an IP master.</summary>
    /// <param name="source">The source master stream.</param>
    /// <param name="startAddress">The starting address.</param>
    /// <param name="numberOfPoints">The number of points to read.</param>
    /// <param name="interval">The polling interval in milliseconds.</param>
    /// <returns>An observable sequence producing data or an error.</returns>
    internal static IObservable<(ushort[]? Data, Exception? Error)> ReadInputRegistersCore(
        IObservable<(bool Connected, Exception? Error, ModbusIpMaster? Master)> source,
        ushort startAddress,
        ushort numberOfPoints,
        double interval = 1000.0) =>
        ReadMasterCore(
            source,
            master => master.ReadInputRegistersAsync(startAddress, numberOfPoints),
            "Read Input Registers Error",
            interval);

    /// <summary>Reads discrete inputs from an IP master.</summary>
    /// <param name="source">The source master stream.</param>
    /// <param name="startAddress">The starting address.</param>
    /// <param name="numberOfPoints">The number of points to read.</param>
    /// <param name="interval">The polling interval in milliseconds.</param>
    /// <returns>An observable sequence producing data or an error.</returns>
    internal static IObservable<(bool[]? Data, Exception? Error)> ReadInputsCore(
        IObservable<(bool Connected, Exception? Error, ModbusIpMaster? Master)> source,
        ushort startAddress,
        ushort numberOfPoints,
        double interval = 1000.0) =>
        ReadMasterCore(
            source,
            master => master.ReadInputsAsync(startAddress, numberOfPoints),
            "Read Inputs Error",
            interval);

    /// <summary>Creates a serial port resource configured with the requested serial settings.</summary>
    /// <param name="port">The COM port name.</param>
    /// <param name="baudRate">The baud rate.</param>
    /// <param name="dataBits">The data bits.</param>
    /// <param name="parity">The parity.</param>
    /// <param name="stopBits">The stop bits.</param>
    /// <param name="handshake">The handshake.</param>
    /// <returns>The configured serial port resource.</returns>
    private static SerialPortRx CreateSerialPort(
        string port,
        int baudRate,
        int dataBits,
        Parity parity,
        StopBits stopBits,
        Handshake handshake)
    {
        return SerialPortFactoryOverride is not null
            ? SerialPortFactoryOverride(port, baudRate, dataBits, parity, stopBits, handshake)
            : new(port, baudRate)
            {
                DataBits = dataBits,
                Parity = parity,
                StopBits = stopBits,
                Handshake = handshake,
            };
    }

    /// <summary>Gets the current available-port observation source.</summary>
    /// <returns>The available serial-port name snapshots.</returns>
    private static IObservable<string[]> ObserveSerialPortNames() =>
        SerialPortNamesOverride?.Invoke() ?? SerialPortRx.PortNames();

    /// <summary>Determines whether any available port name contains the requested port token.</summary>
    /// <param name="portNames">The available port names.</param>
    /// <param name="port">The requested port token.</param>
    /// <returns>True when a matching port name is present.</returns>
    private static bool ContainsPortName(IEnumerable<string> portNames, string port)
    {
        foreach (var portName in portNames)
        {
            if (portName.Contains(port))
            {
                return true;
            }
        }

        return false;
    }
}
