// Copyright (c) 2019-2026 Chris Pulman and contributors. All rights reserved.
// Chris Pulman and contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

#if REACTIVE_SHIM
using IoT.DriverCore.ModbusRx.Reactive.Data;
#else
using IoT.DriverCore.ModbusRx.Data;
#endif
#if REACTIVE_SHIM
using IoT.DriverCore.ModbusRx.Reactive.Device;
#else
using IoT.DriverCore.ModbusRx.Device;
#endif

#if REACTIVE_SHIM
namespace IoT.DriverCore.ModbusRx.Reactive;
#else
namespace IoT.DriverCore.ModbusRx;
#endif

/// <summary>Extension methods for Modbus reactive creation helpers.</summary>
public static class CreateExtensions
{
    /// <summary>Reads coils from the serial master stream.</summary>
    /// <param name="source">The extension receiver.</param>
    /// <param name="slaveAddress">The Modbus slave address.</param>
    /// <param name="startAddress">The start address.</param>
    /// <param name="numberOfPoints">The number of points.</param>
    /// <param name="interval">The polling interval.</param>
    /// <returns>An observable of data and error tuples.</returns>
    public static IObservable<(bool[]? Data, Exception? Error)> ReadCoils(
        IObservable<(bool Connected, Exception? Error, IModbusSerialMaster? Master)> source,
        byte slaveAddress,
        ushort startAddress,
        ushort numberOfPoints,
        double interval) =>
        Create.ReadCoilsCore(source, slaveAddress, startAddress, numberOfPoints, interval);

    /// <summary>Reads coils from slave address 1.</summary>
    /// <param name="source">The extension receiver.</param>
    /// <param name="startAddress">The start address.</param>
    /// <param name="numberOfPoints">The number of points.</param>
    /// <param name="interval">The polling interval.</param>
    /// <returns>An observable of data and error tuples.</returns>
    public static IObservable<(bool[]? Data, Exception? Error)> ReadCoils(
        IObservable<(bool Connected, Exception? Error, IModbusSerialMaster? Master)> source,
        ushort startAddress,
        ushort numberOfPoints,
        double interval) =>
        Create.ReadCoils(source, startAddress, numberOfPoints, interval);

    /// <summary>Reads coils from the IP master stream.</summary>
    /// <param name="source">The extension receiver.</param>
    /// <param name="startAddress">The start address.</param>
    /// <param name="numberOfPoints">The number of points.</param>
    /// <param name="interval">The polling interval.</param>
    /// <returns>An observable of data and error tuples.</returns>
    public static IObservable<(bool[]? Data, Exception? Error)> ReadCoils(
        IObservable<(bool Connected, Exception? Error, ModbusIpMaster? Master)> source,
        ushort startAddress,
        ushort numberOfPoints,
        double interval) =>
        Create.ReadCoilsCore(source, startAddress, numberOfPoints, interval);

    /// <summary>Reads holding registers from the serial master stream.</summary>
    /// <param name="source">The extension receiver.</param>
    /// <param name="slaveAddress">The Modbus slave address.</param>
    /// <param name="startAddress">The start address.</param>
    /// <param name="numberOfPoints">The number of points.</param>
    /// <param name="interval">The polling interval.</param>
    /// <returns>An observable of data and error tuples.</returns>
    public static IObservable<(ushort[]? Data, Exception? Error)> ReadHoldingRegisters(
        IObservable<(bool Connected, Exception? Error, IModbusSerialMaster? Master)> source,
        byte slaveAddress,
        ushort startAddress,
        ushort numberOfPoints,
        double interval) =>
        Create.ReadHoldingRegistersCore(source, slaveAddress, startAddress, numberOfPoints, interval);

    /// <summary>Reads holding registers from slave address 1.</summary>
    /// <param name="source">The extension receiver.</param>
    /// <param name="startAddress">The start address.</param>
    /// <param name="numberOfPoints">The number of points.</param>
    /// <param name="interval">The polling interval.</param>
    /// <returns>An observable of data and error tuples.</returns>
    public static IObservable<(ushort[]? Data, Exception? Error)> ReadHoldingRegisters(
        IObservable<(bool Connected, Exception? Error, IModbusSerialMaster? Master)> source,
        ushort startAddress,
        ushort numberOfPoints,
        double interval) =>
        Create.ReadHoldingRegisters(source, startAddress, numberOfPoints, interval);

    /// <summary>Reads holding registers from the IP master stream.</summary>
    /// <param name="source">The extension receiver.</param>
    /// <param name="startAddress">The start address.</param>
    /// <param name="numberOfPoints">The number of points.</param>
    /// <param name="interval">The polling interval.</param>
    /// <returns>An observable of data and error tuples.</returns>
    public static IObservable<(ushort[]? Data, Exception? Error)> ReadHoldingRegisters(
        IObservable<(bool Connected, Exception? Error, ModbusIpMaster? Master)> source,
        ushort startAddress,
        ushort numberOfPoints,
        double interval) =>
        Create.ReadHoldingRegistersCore(source, startAddress, numberOfPoints, interval);

    /// <summary>Reads discrete inputs from the serial master stream.</summary>
    /// <param name="source">The extension receiver.</param>
    /// <param name="slaveAddress">The Modbus slave address.</param>
    /// <param name="startAddress">The start address.</param>
    /// <param name="numberOfPoints">The number of points.</param>
    /// <param name="interval">The polling interval.</param>
    /// <returns>An observable of data and error tuples.</returns>
    public static IObservable<(bool[]? Data, Exception? Error)> ReadInputs(
        IObservable<(bool Connected, Exception? Error, IModbusSerialMaster? Master)> source,
        byte slaveAddress,
        ushort startAddress,
        ushort numberOfPoints,
        double interval) =>
        Create.ReadInputsCore(source, slaveAddress, startAddress, numberOfPoints, interval);

    /// <summary>Reads discrete inputs from slave address 1.</summary>
    /// <param name="source">The extension receiver.</param>
    /// <param name="startAddress">The start address.</param>
    /// <param name="numberOfPoints">The number of points.</param>
    /// <param name="interval">The polling interval.</param>
    /// <returns>An observable of data and error tuples.</returns>
    public static IObservable<(bool[]? Data, Exception? Error)> ReadInputs(
        IObservable<(bool Connected, Exception? Error, IModbusSerialMaster? Master)> source,
        ushort startAddress,
        ushort numberOfPoints,
        double interval) =>
        Create.ReadInputs(source, startAddress, numberOfPoints, interval);

    /// <summary>Reads discrete inputs from the IP master stream.</summary>
    /// <param name="source">The extension receiver.</param>
    /// <param name="startAddress">The start address.</param>
    /// <param name="numberOfPoints">The number of points.</param>
    /// <param name="interval">The polling interval.</param>
    /// <returns>An observable of data and error tuples.</returns>
    public static IObservable<(bool[]? Data, Exception? Error)> ReadInputs(
        IObservable<(bool Connected, Exception? Error, ModbusIpMaster? Master)> source,
        ushort startAddress,
        ushort numberOfPoints,
        double interval) =>
        Create.ReadInputsCore(source, startAddress, numberOfPoints, interval);

    /// <summary>Reads input registers from the serial master stream.</summary>
    /// <param name="source">The extension receiver.</param>
    /// <param name="slaveAddress">The Modbus slave address.</param>
    /// <param name="startAddress">The start address.</param>
    /// <param name="numberOfPoints">The number of points.</param>
    /// <param name="interval">The polling interval.</param>
    /// <returns>An observable of data and error tuples.</returns>
    public static IObservable<(ushort[]? Data, Exception? Error)> ReadInputRegisters(
        IObservable<(bool Connected, Exception? Error, IModbusSerialMaster? Master)> source,
        byte slaveAddress,
        ushort startAddress,
        ushort numberOfPoints,
        double interval) =>
        Create.ReadInputRegistersCore(source, slaveAddress, startAddress, numberOfPoints, interval);

    /// <summary>Reads input registers from slave address 1.</summary>
    /// <param name="source">The extension receiver.</param>
    /// <param name="startAddress">The start address.</param>
    /// <param name="numberOfPoints">The number of points.</param>
    /// <param name="interval">The polling interval.</param>
    /// <returns>An observable of data and error tuples.</returns>
    public static IObservable<(ushort[]? Data, Exception? Error)> ReadInputRegisters(
        IObservable<(bool Connected, Exception? Error, IModbusSerialMaster? Master)> source,
        ushort startAddress,
        ushort numberOfPoints,
        double interval) =>
        Create.ReadInputRegisters(source, startAddress, numberOfPoints, interval);

    /// <summary>Reads input registers from the IP master stream.</summary>
    /// <param name="source">The extension receiver.</param>
    /// <param name="startAddress">The start address.</param>
    /// <param name="numberOfPoints">The number of points.</param>
    /// <param name="interval">The polling interval.</param>
    /// <returns>An observable of data and error tuples.</returns>
    public static IObservable<(ushort[]? Data, Exception? Error)> ReadInputRegisters(
        IObservable<(bool Connected, Exception? Error, ModbusIpMaster? Master)> source,
        ushort startAddress,
        ushort numberOfPoints,
        double interval) =>
        Create.ReadInputRegistersCore(source, startAddress, numberOfPoints, interval);

    /// <summary>Observes reads from the data store.</summary>
    /// <param name="slave">The extension receiver.</param>
    /// <returns>An observable of data-store events.</returns>
    public static IObservable<DataStoreEventArgs> ObserveDataStoreReadFrom(ModbusSlave slave) =>
        Create.ObserveDataStoreReadFromCore(slave);

    /// <summary>Observes received slave requests.</summary>
    /// <param name="slave">The extension receiver.</param>
    /// <returns>An observable of request events.</returns>
    public static IObservable<ModbusSlaveRequestEventArgs> ObserveRequest(ModbusSlave slave) =>
        Create.ObserveRequestCore(slave);

    /// <summary>Observes completed writes.</summary>
    /// <param name="slave">The extension receiver.</param>
    /// <returns>An observable of request events.</returns>
    public static IObservable<ModbusSlaveRequestEventArgs> ObserveWriteComplete(ModbusSlave slave) =>
        Create.ObserveWriteCompleteCore(slave);

    /// <summary>Observes writes to the data store.</summary>
    /// <param name="slave">The extension receiver.</param>
    /// <returns>An observable of data-store events.</returns>
    public static IObservable<DataStoreEventArgs> ObserveDataStoreWrittenTo(ModbusSlave slave) =>
        Create.ObserveDataStoreWrittenToCore(slave);

    /// <summary>Converts register data to a double.</summary>
    /// <param name="inputs">The extension receiver.</param>
    /// <param name="start">The start index.</param>
    /// <param name="swapWords">Whether to swap words.</param>
    /// <returns>A double value or null if insufficient data is available.</returns>
    public static double? ToDouble(ReadOnlySpan<ushort> inputs, int start, bool swapWords) =>
        Create.ToDoubleCore(inputs, start, swapWords);

    /// <summary>Converts register data to a double.</summary>
    /// <param name="inputs">The extension receiver.</param>
    /// <param name="start">The start index.</param>
    /// <param name="swapWords">Whether to swap words.</param>
    /// <returns>A double value or null if insufficient data is available.</returns>
    public static double? ToDouble(ushort[]? inputs, int start, bool swapWords) =>
        ToDouble(inputs.AsSpan(), start, swapWords);

    /// <summary>Writes the double value to a register span.</summary>
    /// <param name="input">The extension receiver.</param>
    /// <param name="output">The output span.</param>
    /// <param name="start">The start index.</param>
    /// <param name="swapWords">Whether to swap words.</param>
    public static void FromDouble(double input, Span<ushort> output, int start, bool swapWords) =>
        Create.FromDoubleCore(input, output, start, swapWords);

    /// <summary>Writes the double value to a register array.</summary>
    /// <param name="input">The extension receiver.</param>
    /// <param name="output">The output array.</param>
    /// <param name="start">The start index.</param>
    /// <param name="swapWords">Whether to swap words.</param>
    public static void FromDouble(double input, ushort[] output, int start, bool swapWords)
    {
        if (output is null)
        {
            throw new ArgumentNullException(nameof(output));
        }

        FromDouble(input, (Span<ushort>)output, start, swapWords);
    }

    /// <summary>Writes the float value to a register span.</summary>
    /// <param name="input">The extension receiver.</param>
    /// <param name="output">The output span.</param>
    /// <param name="start">The start index.</param>
    /// <param name="swapWords">Whether to swap words.</param>
    public static void FromFloat(float input, Span<ushort> output, int start, bool swapWords) =>
        Create.FromFloatCore(input, output, start, swapWords);

    /// <summary>Writes the float value to a register array.</summary>
    /// <param name="input">The extension receiver.</param>
    /// <param name="output">The output array.</param>
    /// <param name="start">The start index.</param>
    /// <param name="swapWords">Whether to swap words.</param>
    public static void FromFloat(float input, ushort[] output, int start, bool swapWords)
    {
        if (output is null)
        {
            throw new ArgumentNullException(nameof(output));
        }

        FromFloat(input, (Span<ushort>)output, start, swapWords);
    }

    /// <summary>Converts register data to a float.</summary>
    /// <param name="inputs">The extension receiver.</param>
    /// <param name="start">The start index.</param>
    /// <param name="swapWords">Whether to swap words.</param>
    /// <returns>A float value or null if insufficient data is available.</returns>
    public static float? ToFloat(ReadOnlySpan<ushort> inputs, int start, bool swapWords) =>
        Create.ToFloatCore(inputs, start, swapWords);

    /// <summary>Converts register data to a float.</summary>
    /// <param name="inputs">The extension receiver.</param>
    /// <param name="start">The start index.</param>
    /// <param name="swapWords">Whether to swap words.</param>
    /// <returns>A float value or null if insufficient data is available.</returns>
    public static float? ToFloat(ushort[]? inputs, int start, bool swapWords) =>
        ToFloat(inputs.AsSpan(), start, swapWords);
}
