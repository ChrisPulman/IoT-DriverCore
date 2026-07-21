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
    /// <summary>Reads holding registers from a serial master stream.</summary>
    /// <param name="source">The source serial master stream.</param>
    /// <param name="slaveAddress">The Modbus slave address.</param>
    /// <param name="startAddress">The starting address.</param>
    /// <param name="numberOfPoints">The number of points to read.</param>
    /// <param name="interval">The polling interval in milliseconds.</param>
    /// <returns>An observable sequence producing the result data or error.</returns>
    public static IObservable<(ushort[]? Data, Exception? Error)> ReadHoldingRegisters(
        IObservable<(bool Connected, Exception? Error, IModbusSerialMaster? Master)> source,
        byte slaveAddress,
        ushort startAddress,
        ushort numberOfPoints,
        double interval) =>
        ReadHoldingRegistersCore(source, slaveAddress, startAddress, numberOfPoints, interval);

    /// <summary>Reads holding registers from slave address 1 on a serial master stream.</summary>
    /// <param name="source">The source serial master stream.</param>
    /// <param name="startAddress">The starting address.</param>
    /// <param name="numberOfPoints">The number of points to read.</param>
    /// <param name="interval">The polling interval in milliseconds.</param>
    /// <returns>An observable sequence producing the result data or error.</returns>
    public static IObservable<(ushort[]? Data, Exception? Error)> ReadHoldingRegisters(
        IObservable<(bool Connected, Exception? Error, IModbusSerialMaster? Master)> source,
        ushort startAddress,
        ushort numberOfPoints,
        double interval) =>
        ReadHoldingRegistersCore(source, startAddress, numberOfPoints, interval);

    /// <summary>Reads holding registers from an IP master stream.</summary>
    /// <param name="source">The source IP master stream.</param>
    /// <param name="startAddress">The starting address.</param>
    /// <param name="numberOfPoints">The number of points to read.</param>
    /// <param name="interval">The polling interval in milliseconds.</param>
    /// <returns>An observable sequence producing the result data or error.</returns>
    public static IObservable<(ushort[]? Data, Exception? Error)> ReadHoldingRegisters(
        IObservable<(bool Connected, Exception? Error, ModbusIpMaster? Master)> source,
        ushort startAddress,
        ushort numberOfPoints,
        double interval) =>
        CreateExtensions.ReadHoldingRegisters(source, startAddress, numberOfPoints, interval);

    /// <summary>Reads input registers from a serial master stream.</summary>
    /// <param name="source">The source serial master stream.</param>
    /// <param name="slaveAddress">The Modbus slave address.</param>
    /// <param name="startAddress">The starting address.</param>
    /// <param name="numberOfPoints">The number of points to read.</param>
    /// <param name="interval">The polling interval in milliseconds.</param>
    /// <returns>An observable sequence producing the result data or error.</returns>
    public static IObservable<(ushort[]? Data, Exception? Error)> ReadInputRegisters(
        IObservable<(bool Connected, Exception? Error, IModbusSerialMaster? Master)> source,
        byte slaveAddress,
        ushort startAddress,
        ushort numberOfPoints,
        double interval) =>
        ReadInputRegistersCore(source, slaveAddress, startAddress, numberOfPoints, interval);

    /// <summary>Reads input registers from slave address 1 on a serial master stream.</summary>
    /// <param name="source">The source serial master stream.</param>
    /// <param name="startAddress">The starting address.</param>
    /// <param name="numberOfPoints">The number of points to read.</param>
    /// <param name="interval">The polling interval in milliseconds.</param>
    /// <returns>An observable sequence producing the result data or error.</returns>
    public static IObservable<(ushort[]? Data, Exception? Error)> ReadInputRegisters(
        IObservable<(bool Connected, Exception? Error, IModbusSerialMaster? Master)> source,
        ushort startAddress,
        ushort numberOfPoints,
        double interval) =>
        ReadInputRegistersCore(source, startAddress, numberOfPoints, interval);

    /// <summary>Reads input registers from an IP master stream.</summary>
    /// <param name="source">The source IP master stream.</param>
    /// <param name="startAddress">The starting address.</param>
    /// <param name="numberOfPoints">The number of points to read.</param>
    /// <param name="interval">The polling interval in milliseconds.</param>
    /// <returns>An observable sequence producing the result data or error.</returns>
    public static IObservable<(ushort[]? Data, Exception? Error)> ReadInputRegisters(
        IObservable<(bool Connected, Exception? Error, ModbusIpMaster? Master)> source,
        ushort startAddress,
        ushort numberOfPoints,
        double interval) =>
        CreateExtensions.ReadInputRegisters(source, startAddress, numberOfPoints, interval);

    /// <summary>Reads coils from a serial master stream.</summary>
    /// <param name="source">The source serial master stream.</param>
    /// <param name="slaveAddress">The Modbus slave address.</param>
    /// <param name="startAddress">The starting address.</param>
    /// <param name="numberOfPoints">The number of points to read.</param>
    /// <param name="interval">The polling interval in milliseconds.</param>
    /// <returns>An observable sequence producing the result data or error.</returns>
    public static IObservable<(bool[]? Data, Exception? Error)> ReadCoils(
        IObservable<(bool Connected, Exception? Error, IModbusSerialMaster? Master)> source,
        byte slaveAddress,
        ushort startAddress,
        ushort numberOfPoints,
        double interval) =>
        ReadCoilsCore(source, slaveAddress, startAddress, numberOfPoints, interval);

    /// <summary>Reads coils from slave address 1 on a serial master stream.</summary>
    /// <param name="source">The source serial master stream.</param>
    /// <param name="startAddress">The starting address.</param>
    /// <param name="numberOfPoints">The number of points to read.</param>
    /// <param name="interval">The polling interval in milliseconds.</param>
    /// <returns>An observable sequence producing the result data or error.</returns>
    public static IObservable<(bool[]? Data, Exception? Error)> ReadCoils(
        IObservable<(bool Connected, Exception? Error, IModbusSerialMaster? Master)> source,
        ushort startAddress,
        ushort numberOfPoints,
        double interval) =>
        ReadCoilsCore(source, startAddress, numberOfPoints, interval);

    /// <summary>Reads coils from an IP master stream.</summary>
    /// <param name="source">The source IP master stream.</param>
    /// <param name="startAddress">The starting address.</param>
    /// <param name="numberOfPoints">The number of points to read.</param>
    /// <param name="interval">The polling interval in milliseconds.</param>
    /// <returns>An observable sequence producing the result data or error.</returns>
    public static IObservable<(bool[]? Data, Exception? Error)> ReadCoils(
        IObservable<(bool Connected, Exception? Error, ModbusIpMaster? Master)> source,
        ushort startAddress,
        ushort numberOfPoints,
        double interval) =>
        CreateExtensions.ReadCoils(source, startAddress, numberOfPoints, interval);

    /// <summary>Reads discrete inputs from a serial master stream.</summary>
    /// <param name="source">The source serial master stream.</param>
    /// <param name="slaveAddress">The Modbus slave address.</param>
    /// <param name="startAddress">The starting address.</param>
    /// <param name="numberOfPoints">The number of points to read.</param>
    /// <param name="interval">The polling interval in milliseconds.</param>
    /// <returns>An observable sequence producing the result data or error.</returns>
    public static IObservable<(bool[]? Data, Exception? Error)> ReadInputs(
        IObservable<(bool Connected, Exception? Error, IModbusSerialMaster? Master)> source,
        byte slaveAddress,
        ushort startAddress,
        ushort numberOfPoints,
        double interval) =>
        ReadInputsCore(source, slaveAddress, startAddress, numberOfPoints, interval);

    /// <summary>Reads discrete inputs from slave address 1 on a serial master stream.</summary>
    /// <param name="source">The source serial master stream.</param>
    /// <param name="startAddress">The starting address.</param>
    /// <param name="numberOfPoints">The number of points to read.</param>
    /// <param name="interval">The polling interval in milliseconds.</param>
    /// <returns>An observable sequence producing the result data or error.</returns>
    public static IObservable<(bool[]? Data, Exception? Error)> ReadInputs(
        IObservable<(bool Connected, Exception? Error, IModbusSerialMaster? Master)> source,
        ushort startAddress,
        ushort numberOfPoints,
        double interval) =>
        ReadInputsCore(source, startAddress, numberOfPoints, interval);

    /// <summary>Reads discrete inputs from an IP master stream.</summary>
    /// <param name="source">The source IP master stream.</param>
    /// <param name="startAddress">The starting address.</param>
    /// <param name="numberOfPoints">The number of points to read.</param>
    /// <param name="interval">The polling interval in milliseconds.</param>
    /// <returns>An observable sequence producing the result data or error.</returns>
    public static IObservable<(bool[]? Data, Exception? Error)> ReadInputs(
        IObservable<(bool Connected, Exception? Error, ModbusIpMaster? Master)> source,
        ushort startAddress,
        ushort numberOfPoints,
        double interval) =>
        CreateExtensions.ReadInputs(source, startAddress, numberOfPoints, interval);
}
