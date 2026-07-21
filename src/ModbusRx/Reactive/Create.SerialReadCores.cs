// Copyright (c) 2019-2026 Chris Pulman and contributors. All rights reserved.
// Chris Pulman and contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

#if REACTIVE_SHIM
using ModbusRx.Reactive.Device;
#else
using ModbusRx.Device;
#endif

#if REACTIVE_SHIM
namespace ModbusRx.Reactive;
#else
namespace ModbusRx;
#endif

/// <summary>Provides ModbusRx functionality.</summary>
public static partial class Create
{
    /// <summary>Reads coils from a serial master.</summary>
    /// <param name="source">The source serial master stream.</param>
    /// <param name="slaveAddress">The Modbus slave address.</param>
    /// <param name="startAddress">The starting address.</param>
    /// <param name="numberOfPoints">The number of points to read.</param>
    /// <param name="interval">The polling interval in milliseconds.</param>
    /// <returns>An observable sequence producing data or an error.</returns>
    internal static IObservable<(bool[]? Data, Exception? Error)> ReadCoilsCore(
        IObservable<(bool Connected, Exception? Error, IModbusSerialMaster? Master)> source,
        byte slaveAddress,
        ushort startAddress,
        ushort numberOfPoints,
        double interval = 1000.0) =>
        ReadMasterCore(
            source,
            master => master.ReadCoilsAsync(slaveAddress, startAddress, numberOfPoints),
            "Read Coils Error",
            interval);

    /// <summary>Reads coils from slave address 1.</summary>
    /// <param name="source">The source serial master stream.</param>
    /// <param name="startAddress">The starting address.</param>
    /// <param name="numberOfPoints">The number of points to read.</param>
    /// <param name="interval">The polling interval in milliseconds.</param>
    /// <returns>An observable sequence producing data or an error.</returns>
    internal static IObservable<(bool[]? Data, Exception? Error)> ReadCoilsCore(
        IObservable<(bool Connected, Exception? Error, IModbusSerialMaster? Master)> source,
        ushort startAddress,
        ushort numberOfPoints,
        double interval = 1000.0) =>
        ReadCoilsCore(source, 1, startAddress, numberOfPoints, interval);

    /// <summary>Reads holding registers from a serial master.</summary>
    /// <param name="source">The source serial master stream.</param>
    /// <param name="slaveAddress">The Modbus slave address.</param>
    /// <param name="startAddress">The starting address.</param>
    /// <param name="numberOfPoints">The number of points to read.</param>
    /// <param name="interval">The polling interval in milliseconds.</param>
    /// <returns>An observable sequence producing data or an error.</returns>
    internal static IObservable<(ushort[]? Data, Exception? Error)> ReadHoldingRegistersCore(
        IObservable<(bool Connected, Exception? Error, IModbusSerialMaster? Master)> source,
        byte slaveAddress,
        ushort startAddress,
        ushort numberOfPoints,
        double interval = 1000.0) =>
        ReadMasterCore(
            source,
            master => master.ReadHoldingRegistersAsync(slaveAddress, startAddress, numberOfPoints),
            "Read Holding Registers Error",
            interval,
            nameof(ReadHoldingRegisters));

    /// <summary>Reads holding registers from slave address 1.</summary>
    /// <param name="source">The source serial master stream.</param>
    /// <param name="startAddress">The starting address.</param>
    /// <param name="numberOfPoints">The number of points to read.</param>
    /// <param name="interval">The polling interval in milliseconds.</param>
    /// <returns>An observable sequence producing data or an error.</returns>
    internal static IObservable<(ushort[]? Data, Exception? Error)> ReadHoldingRegistersCore(
        IObservable<(bool Connected, Exception? Error, IModbusSerialMaster? Master)> source,
        ushort startAddress,
        ushort numberOfPoints,
        double interval = 1000.0) =>
        ReadHoldingRegistersCore(source, 1, startAddress, numberOfPoints, interval);

    /// <summary>Reads input registers from a serial master.</summary>
    /// <param name="source">The source serial master stream.</param>
    /// <param name="slaveAddress">The Modbus slave address.</param>
    /// <param name="startAddress">The starting address.</param>
    /// <param name="numberOfPoints">The number of points to read.</param>
    /// <param name="interval">The polling interval in milliseconds.</param>
    /// <returns>An observable sequence producing data or an error.</returns>
    internal static IObservable<(ushort[]? Data, Exception? Error)> ReadInputRegistersCore(
        IObservable<(bool Connected, Exception? Error, IModbusSerialMaster? Master)> source,
        byte slaveAddress,
        ushort startAddress,
        ushort numberOfPoints,
        double interval = 1000.0) =>
        ReadMasterCore(
            source,
            master => master.ReadInputRegistersAsync(slaveAddress, startAddress, numberOfPoints),
            "Read Input Registers Error",
            interval);

    /// <summary>Reads input registers from slave address 1.</summary>
    /// <param name="source">The source serial master stream.</param>
    /// <param name="startAddress">The starting address.</param>
    /// <param name="numberOfPoints">The number of points to read.</param>
    /// <param name="interval">The polling interval in milliseconds.</param>
    /// <returns>An observable sequence producing data or an error.</returns>
    internal static IObservable<(ushort[]? Data, Exception? Error)> ReadInputRegistersCore(
        IObservable<(bool Connected, Exception? Error, IModbusSerialMaster? Master)> source,
        ushort startAddress,
        ushort numberOfPoints,
        double interval = 1000.0) =>
        ReadInputRegistersCore(source, 1, startAddress, numberOfPoints, interval);

    /// <summary>Reads discrete inputs from a serial master.</summary>
    /// <param name="source">The source serial master stream.</param>
    /// <param name="slaveAddress">The Modbus slave address.</param>
    /// <param name="startAddress">The starting address.</param>
    /// <param name="numberOfPoints">The number of points to read.</param>
    /// <param name="interval">The polling interval in milliseconds.</param>
    /// <returns>An observable sequence producing data or an error.</returns>
    internal static IObservable<(bool[]? Data, Exception? Error)> ReadInputsCore(
        IObservable<(bool Connected, Exception? Error, IModbusSerialMaster? Master)> source,
        byte slaveAddress,
        ushort startAddress,
        ushort numberOfPoints,
        double interval = 1000.0) =>
        ReadMasterCore(
            source,
            master => master.ReadInputsAsync(slaveAddress, startAddress, numberOfPoints),
            "Read Inputs Error",
            interval);

    /// <summary>Reads discrete inputs from slave address 1.</summary>
    /// <param name="source">The source serial master stream.</param>
    /// <param name="startAddress">The starting address.</param>
    /// <param name="numberOfPoints">The number of points to read.</param>
    /// <param name="interval">The polling interval in milliseconds.</param>
    /// <returns>An observable sequence producing data or an error.</returns>
    internal static IObservable<(bool[]? Data, Exception? Error)> ReadInputsCore(
        IObservable<(bool Connected, Exception? Error, IModbusSerialMaster? Master)> source,
        ushort startAddress,
        ushort numberOfPoints,
        double interval = 1000.0) =>
        ReadInputsCore(source, 1, startAddress, numberOfPoints, interval);
}
