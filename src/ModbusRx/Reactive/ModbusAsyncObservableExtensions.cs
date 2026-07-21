// Copyright (c) 2019-2026 Chris Pulman and contributors. All rights reserved.
// Chris Pulman and contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

#if REACTIVE_SHIM
using ModbusRx.Reactive.Data;
#else
using ModbusRx.Data;
#endif
#if REACTIVE_SHIM
using ModbusRx.Reactive.Device;
#else
using ModbusRx.Device;
#endif
using ReactiveUI.Primitives;
using ReactiveUI.Primitives.Async;

#if REACTIVE_SHIM
namespace ModbusRx.Reactive;
#else
namespace ModbusRx;
#endif

/// <summary>Async-observable adapters for Modbus reactive operations.</summary>
public static class ModbusAsyncObservableExtensions
{
    /// <summary>Gets the serial-slave writer.</summary>
    private static ModbusSerialSlaveExtensions SerialSlaveWriter { get; } = new();

    /// <summary>Gets the TCP-slave writer.</summary>
    private static ModbusTcpSlaveExtensions TcpSlaveWriter { get; } = new();

    /// <summary>Gets the UDP-slave writer.</summary>
    private static ModbusUdpSlaveExtensions UdpSlaveWriter { get; } = new();

    /// <summary>Observes coil changes as an async observable.</summary>
    /// <param name="server">The extension receiver.</param>
    /// <param name="startAddress">The starting address to monitor.</param>
    /// <param name="count">The number of coils to monitor.</param>
    /// <param name="interval">The polling interval in milliseconds.</param>
    /// <returns>An async observable of coils.</returns>
    public static IObservableAsync<bool[]> ObserveCoilsObservable(
        ModbusServer server,
        ushort startAddress,
        ushort count,
        double interval) =>
        ToAsyncObservable(ModbusServerExtensions.ObserveCoils(server, startAddress, count, interval));

    /// <summary>Observes server data changes as an async observable.</summary>
    /// <param name="server">The extension receiver.</param>
    /// <param name="interval">The polling interval in milliseconds.</param>
    /// <returns>An async observable of data snapshots.</returns>
    public static IObservableAsync<(
        ushort[] HoldingRegisters,
        ushort[] InputRegisters,
        bool[] Coils,
        bool[] Inputs)> ObserveDataChangesObservable(
            ModbusServer server,
            double interval) =>
        ToAsyncObservable(ModbusServerExtensions.ObserveDataChanges(server, interval));

    /// <summary>Observes data-store reads as an async observable.</summary>
    /// <param name="slave">The extension receiver.</param>
    /// <returns>An async observable of data-store events.</returns>
    public static IObservableAsync<DataStoreEventArgs> ObserveDataStoreReadFromObservable(ModbusSlave slave) =>
        ToAsyncObservable(CreateExtensions.ObserveDataStoreReadFrom(slave));

    /// <summary>Observes data-store writes as an async observable.</summary>
    /// <param name="slave">The extension receiver.</param>
    /// <returns>An async observable of data-store events.</returns>
    public static IObservableAsync<DataStoreEventArgs> ObserveDataStoreWrittenToObservable(ModbusSlave slave) =>
        ToAsyncObservable(CreateExtensions.ObserveDataStoreWrittenTo(slave));

    /// <summary>Observes discrete input changes as an async observable.</summary>
    /// <param name="server">The extension receiver.</param>
    /// <param name="startAddress">The starting address to monitor.</param>
    /// <param name="count">The number of inputs to monitor.</param>
    /// <param name="interval">The polling interval in milliseconds.</param>
    /// <returns>An async observable of discrete inputs.</returns>
    public static IObservableAsync<bool[]> ObserveDiscreteInputsObservable(
        ModbusServer server,
        ushort startAddress,
        ushort count,
        double interval) =>
        ToAsyncObservable(ModbusServerExtensions.ObserveDiscreteInputs(server, startAddress, count, interval));

    /// <summary>Observes holding-register changes as an async observable.</summary>
    /// <param name="server">The extension receiver.</param>
    /// <param name="startAddress">The starting address to monitor.</param>
    /// <param name="count">The number of registers to monitor.</param>
    /// <param name="interval">The polling interval in milliseconds.</param>
    /// <returns>An async observable of holding registers.</returns>
    public static IObservableAsync<ushort[]> ObserveHoldingRegistersObservable(
        ModbusServer server,
        ushort startAddress,
        ushort count,
        double interval) =>
        ToAsyncObservable(ModbusServerExtensions.ObserveHoldingRegisters(server, startAddress, count, interval));

    /// <summary>Observes input-register changes as an async observable.</summary>
    /// <param name="server">The extension receiver.</param>
    /// <param name="startAddress">The starting address to monitor.</param>
    /// <param name="count">The number of registers to monitor.</param>
    /// <param name="interval">The polling interval in milliseconds.</param>
    /// <returns>An async observable of input registers.</returns>
    public static IObservableAsync<ushort[]> ObserveInputRegistersObservable(
        ModbusServer server,
        ushort startAddress,
        ushort count,
        double interval) =>
        ToAsyncObservable(ModbusServerExtensions.ObserveInputRegisters(server, startAddress, count, interval));

    /// <summary>Observes slave requests as an async observable.</summary>
    /// <param name="slave">The extension receiver.</param>
    /// <returns>An async observable of request events.</returns>
    public static IObservableAsync<ModbusSlaveRequestEventArgs> ObserveRequestObservable(ModbusSlave slave) =>
        ToAsyncObservable(CreateExtensions.ObserveRequest(slave));

    /// <summary>Observes write completion as an async observable.</summary>
    /// <param name="slave">The extension receiver.</param>
    /// <returns>An async observable of request events.</returns>
    public static IObservableAsync<ModbusSlaveRequestEventArgs> ObserveWriteCompleteObservable(ModbusSlave slave) =>
        ToAsyncObservable(CreateExtensions.ObserveWriteComplete(slave));

    /// <summary>Reads coils from an async serial master stream.</summary>
    /// <param name="source">The extension receiver.</param>
    /// <param name="slaveAddress">The Modbus slave address.</param>
    /// <param name="startAddress">The starting address.</param>
    /// <param name="numberOfPoints">The number of points.</param>
    /// <param name="interval">The polling interval in milliseconds.</param>
    /// <returns>An async observable of coil data and errors.</returns>
    public static IObservableAsync<(bool[]? Data, Exception? Error)> ReadCoils(
        IObservableAsync<(bool Connected, Exception? Error, IModbusSerialMaster? Master)> source,
        byte slaveAddress,
        ushort startAddress,
        ushort numberOfPoints,
        double interval) =>
        ToAsyncObservable(
            CreateExtensions.ReadCoils(
                ToObservable(source),
                slaveAddress,
                startAddress,
                numberOfPoints,
                interval));

    /// <summary>Reads coils from an async IP master stream.</summary>
    /// <param name="source">The extension receiver.</param>
    /// <param name="startAddress">The starting address.</param>
    /// <param name="numberOfPoints">The number of points.</param>
    /// <param name="interval">The polling interval in milliseconds.</param>
    /// <returns>An async observable of coil data and errors.</returns>
    public static IObservableAsync<(bool[]? Data, Exception? Error)> ReadCoils(
        IObservableAsync<(bool Connected, Exception? Error, ModbusIpMaster? Master)> source,
        ushort startAddress,
        ushort numberOfPoints,
        double interval) =>
        ToAsyncObservable(CreateExtensions.ReadCoils(ToObservable(source), startAddress, numberOfPoints, interval));

    /// <summary>Reads coils from a serial master and exposes the polling result as an async observable.</summary>
    /// <param name="source">The extension receiver.</param>
    /// <param name="slaveAddress">The Modbus slave address.</param>
    /// <param name="startAddress">The starting address.</param>
    /// <param name="numberOfPoints">The number of points.</param>
    /// <param name="interval">The polling interval in milliseconds.</param>
    /// <returns>An async observable of coil data and errors.</returns>
    public static IObservableAsync<(bool[]? Data, Exception? Error)> ReadCoilsObservable(
        IObservable<(bool Connected, Exception? Error, IModbusSerialMaster? Master)> source,
        byte slaveAddress,
        ushort startAddress,
        ushort numberOfPoints,
        double interval) =>
        ToAsyncObservable(CreateExtensions.ReadCoils(source, slaveAddress, startAddress, numberOfPoints, interval));

    /// <summary>Reads coils and exposes the polling result as an async observable.</summary>
    /// <param name="source">The extension receiver.</param>
    /// <param name="startAddress">The starting address.</param>
    /// <param name="numberOfPoints">The number of points.</param>
    /// <param name="interval">The polling interval in milliseconds.</param>
    /// <returns>An async observable of coil data and errors.</returns>
    public static IObservableAsync<(bool[]? Data, Exception? Error)> ReadCoilsObservable(
        IObservable<(bool Connected, Exception? Error, ModbusIpMaster? Master)> source,
        ushort startAddress,
        ushort numberOfPoints,
        double interval) =>
        ToAsyncObservable(CreateExtensions.ReadCoils(source, startAddress, numberOfPoints, interval));

    /// <summary>Reads holding registers from an async serial master stream.</summary>
    /// <param name="source">The extension receiver.</param>
    /// <param name="slaveAddress">The Modbus slave address.</param>
    /// <param name="startAddress">The starting address.</param>
    /// <param name="numberOfPoints">The number of points.</param>
    /// <param name="interval">The polling interval in milliseconds.</param>
    /// <returns>An async observable of holding-register data and errors.</returns>
    public static IObservableAsync<(ushort[]? Data, Exception? Error)> ReadHoldingRegisters(
        IObservableAsync<(bool Connected, Exception? Error, IModbusSerialMaster? Master)> source,
        byte slaveAddress,
        ushort startAddress,
        ushort numberOfPoints,
        double interval) =>
        ToAsyncObservable(
            CreateExtensions.ReadHoldingRegisters(
                ToObservable(source),
                slaveAddress,
                startAddress,
                numberOfPoints,
                interval));

    /// <summary>Reads holding registers from an async IP master stream.</summary>
    /// <param name="source">The extension receiver.</param>
    /// <param name="startAddress">The starting address.</param>
    /// <param name="numberOfPoints">The number of points.</param>
    /// <param name="interval">The polling interval in milliseconds.</param>
    /// <returns>An async observable of holding-register data and errors.</returns>
    public static IObservableAsync<(ushort[]? Data, Exception? Error)> ReadHoldingRegisters(
        IObservableAsync<(bool Connected, Exception? Error, ModbusIpMaster? Master)> source,
        ushort startAddress,
        ushort numberOfPoints,
        double interval) =>
        ToAsyncObservable(
            CreateExtensions.ReadHoldingRegisters(
                ToObservable(source),
                startAddress,
                numberOfPoints,
                interval));

    /// <summary>Reads serial holding registers as an async observable.</summary>
    /// <param name="source">The extension receiver.</param>
    /// <param name="slaveAddress">The Modbus slave address.</param>
    /// <param name="startAddress">The starting address.</param>
    /// <param name="numberOfPoints">The number of points.</param>
    /// <param name="interval">The polling interval in milliseconds.</param>
    /// <returns>An async observable of holding-register data and errors.</returns>
    public static IObservableAsync<(ushort[]? Data, Exception? Error)> ReadHoldingRegistersObservable(
        IObservable<(bool Connected, Exception? Error, IModbusSerialMaster? Master)> source,
        byte slaveAddress,
        ushort startAddress,
        ushort numberOfPoints,
        double interval) =>
        ToAsyncObservable(
            CreateExtensions.ReadHoldingRegisters(source, slaveAddress, startAddress, numberOfPoints, interval));

    /// <summary>Reads holding registers and exposes the polling result as an async observable.</summary>
    /// <param name="source">The extension receiver.</param>
    /// <param name="startAddress">The starting address.</param>
    /// <param name="numberOfPoints">The number of points.</param>
    /// <param name="interval">The polling interval in milliseconds.</param>
    /// <returns>An async observable of holding-register data and errors.</returns>
    public static IObservableAsync<(ushort[]? Data, Exception? Error)> ReadHoldingRegistersObservable(
        IObservable<(bool Connected, Exception? Error, ModbusIpMaster? Master)> source,
        ushort startAddress,
        ushort numberOfPoints,
        double interval) =>
        ToAsyncObservable(CreateExtensions.ReadHoldingRegisters(source, startAddress, numberOfPoints, interval));

    /// <summary>Reads input registers from an async serial master stream.</summary>
    /// <param name="source">The extension receiver.</param>
    /// <param name="slaveAddress">The Modbus slave address.</param>
    /// <param name="startAddress">The starting address.</param>
    /// <param name="numberOfPoints">The number of points.</param>
    /// <param name="interval">The polling interval in milliseconds.</param>
    /// <returns>An async observable of input-register data and errors.</returns>
    public static IObservableAsync<(ushort[]? Data, Exception? Error)> ReadInputRegisters(
        IObservableAsync<(bool Connected, Exception? Error, IModbusSerialMaster? Master)> source,
        byte slaveAddress,
        ushort startAddress,
        ushort numberOfPoints,
        double interval) =>
        ToAsyncObservable(
            CreateExtensions.ReadInputRegisters(
                ToObservable(source),
                slaveAddress,
                startAddress,
                numberOfPoints,
                interval));

    /// <summary>Reads input registers from an async IP master stream.</summary>
    /// <param name="source">The extension receiver.</param>
    /// <param name="startAddress">The starting address.</param>
    /// <param name="numberOfPoints">The number of points.</param>
    /// <param name="interval">The polling interval in milliseconds.</param>
    /// <returns>An async observable of input-register data and errors.</returns>
    public static IObservableAsync<(ushort[]? Data, Exception? Error)> ReadInputRegisters(
        IObservableAsync<(bool Connected, Exception? Error, ModbusIpMaster? Master)> source,
        ushort startAddress,
        ushort numberOfPoints,
        double interval) =>
        ToAsyncObservable(
            CreateExtensions.ReadInputRegisters(
                ToObservable(source),
                startAddress,
                numberOfPoints,
                interval));

    /// <summary>Reads serial input registers as an async observable.</summary>
    /// <param name="source">The extension receiver.</param>
    /// <param name="slaveAddress">The Modbus slave address.</param>
    /// <param name="startAddress">The starting address.</param>
    /// <param name="numberOfPoints">The number of points.</param>
    /// <param name="interval">The polling interval in milliseconds.</param>
    /// <returns>An async observable of input-register data and errors.</returns>
    public static IObservableAsync<(ushort[]? Data, Exception? Error)> ReadInputRegistersObservable(
        IObservable<(bool Connected, Exception? Error, IModbusSerialMaster? Master)> source,
        byte slaveAddress,
        ushort startAddress,
        ushort numberOfPoints,
        double interval) =>
        ToAsyncObservable(
            CreateExtensions.ReadInputRegisters(source, slaveAddress, startAddress, numberOfPoints, interval));

    /// <summary>Reads input registers and exposes the polling result as an async observable.</summary>
    /// <param name="source">The extension receiver.</param>
    /// <param name="startAddress">The starting address.</param>
    /// <param name="numberOfPoints">The number of points.</param>
    /// <param name="interval">The polling interval in milliseconds.</param>
    /// <returns>An async observable of input-register data and errors.</returns>
    public static IObservableAsync<(ushort[]? Data, Exception? Error)> ReadInputRegistersObservable(
        IObservable<(bool Connected, Exception? Error, ModbusIpMaster? Master)> source,
        ushort startAddress,
        ushort numberOfPoints,
        double interval) =>
        ToAsyncObservable(CreateExtensions.ReadInputRegisters(source, startAddress, numberOfPoints, interval));

    /// <summary>Reads discrete inputs from an async serial master stream.</summary>
    /// <param name="source">The extension receiver.</param>
    /// <param name="slaveAddress">The Modbus slave address.</param>
    /// <param name="startAddress">The starting address.</param>
    /// <param name="numberOfPoints">The number of points.</param>
    /// <param name="interval">The polling interval in milliseconds.</param>
    /// <returns>An async observable of input data and errors.</returns>
    public static IObservableAsync<(bool[]? Data, Exception? Error)> ReadInputs(
        IObservableAsync<(bool Connected, Exception? Error, IModbusSerialMaster? Master)> source,
        byte slaveAddress,
        ushort startAddress,
        ushort numberOfPoints,
        double interval) =>
        ToAsyncObservable(
            CreateExtensions.ReadInputs(
                ToObservable(source),
                slaveAddress,
                startAddress,
                numberOfPoints,
                interval));

    /// <summary>Reads discrete inputs from an async IP master stream.</summary>
    /// <param name="source">The extension receiver.</param>
    /// <param name="startAddress">The starting address.</param>
    /// <param name="numberOfPoints">The number of points.</param>
    /// <param name="interval">The polling interval in milliseconds.</param>
    /// <returns>An async observable of input data and errors.</returns>
    public static IObservableAsync<(bool[]? Data, Exception? Error)> ReadInputs(
        IObservableAsync<(bool Connected, Exception? Error, ModbusIpMaster? Master)> source,
        ushort startAddress,
        ushort numberOfPoints,
        double interval) =>
        ToAsyncObservable(CreateExtensions.ReadInputs(ToObservable(source), startAddress, numberOfPoints, interval));

    /// <summary>Reads serial discrete inputs as an async observable.</summary>
    /// <param name="source">The extension receiver.</param>
    /// <param name="slaveAddress">The Modbus slave address.</param>
    /// <param name="startAddress">The starting address.</param>
    /// <param name="numberOfPoints">The number of points.</param>
    /// <param name="interval">The polling interval in milliseconds.</param>
    /// <returns>An async observable of input data and errors.</returns>
    public static IObservableAsync<(bool[]? Data, Exception? Error)> ReadInputsObservable(
        IObservable<(bool Connected, Exception? Error, IModbusSerialMaster? Master)> source,
        byte slaveAddress,
        ushort startAddress,
        ushort numberOfPoints,
        double interval) =>
        ToAsyncObservable(CreateExtensions.ReadInputs(source, slaveAddress, startAddress, numberOfPoints, interval));

    /// <summary>Reads discrete inputs and exposes the polling result as an async observable.</summary>
    /// <param name="source">The extension receiver.</param>
    /// <param name="startAddress">The starting address.</param>
    /// <param name="numberOfPoints">The number of points.</param>
    /// <param name="interval">The polling interval in milliseconds.</param>
    /// <returns>An async observable of input data and errors.</returns>
    public static IObservableAsync<(bool[]? Data, Exception? Error)> ReadInputsObservable(
        IObservable<(bool Connected, Exception? Error, ModbusIpMaster? Master)> source,
        ushort startAddress,
        ushort numberOfPoints,
        double interval) =>
        ToAsyncObservable(CreateExtensions.ReadInputs(source, startAddress, numberOfPoints, interval));

    /// <summary>Converts a synchronous observable to an async observable.</summary>
    /// <typeparam name="T">The extension receiver item type.</typeparam>
    /// <param name="source">The extension receiver.</param>
    /// <returns>The async observable adapter.</returns>
    public static IObservableAsync<T> ToAsyncObservable<T>(IObservable<T> source) =>
        new ObservableAsyncAdapter<T>(source);

    /// <summary>Converts a serial master connection stream to an async observable.</summary>
    /// <param name="source">The extension receiver.</param>
    /// <returns>The async observable connection stream.</returns>
    public static IObservableAsync<(
        bool Connected,
        Exception? Error,
        IModbusSerialMaster? Master)> ToModbusObservable(
            IObservable<(bool Connected, Exception? Error, IModbusSerialMaster? Master)> source) =>
        ToAsyncObservable(source);

    /// <summary>Converts an IP master connection stream to an async observable.</summary>
    /// <param name="source">The extension receiver.</param>
    /// <returns>The async observable connection stream.</returns>
    public static IObservableAsync<(
        bool Connected,
        Exception? Error,
        ModbusIpMaster? Master)> ToModbusObservable(
            IObservable<(bool Connected, Exception? Error, ModbusIpMaster? Master)> source) =>
        new ObservableAsyncAdapter<(bool Connected, Exception? Error, ModbusIpMaster? Master)>(source);

    /// <summary>Converts an async observable to a synchronous observable.</summary>
    /// <typeparam name="T">The extension receiver item type.</typeparam>
    /// <param name="source">The extension receiver.</param>
    /// <returns>The synchronous observable adapter.</returns>
    public static IObservable<T> ToObservable<T>(IObservableAsync<T> source) =>
        new ObservableAdapter<T>(source);

    /// <summary>Writes coils to serial slave streams from async observable values.</summary>
    /// <param name="slave">The extension receiver.</param>
    /// <param name="startAddress">The starting address.</param>
    /// <param name="valuesToWrite">The values to write.</param>
    /// <returns>The async slave stream.</returns>
    public static IObservableAsync<ModbusSerialSlave> WriteCoilDiscretes(
        IObservableAsync<ModbusSerialSlave> slave,
        ushort startAddress,
        IObservableAsync<bool[]> valuesToWrite) =>
        ToAsyncObservable(
            SerialSlaveWriter.WriteCoilDiscretes(
                ToObservable(slave),
                startAddress,
                ToObservable(valuesToWrite)));

    /// <summary>Writes coils to TCP slave streams from async observable values.</summary>
    /// <param name="slave">The extension receiver.</param>
    /// <param name="startAddress">The starting address.</param>
    /// <param name="valuesToWrite">The values to write.</param>
    /// <returns>The async slave stream.</returns>
    public static IObservableAsync<ModbusTcpSlave> WriteCoilDiscretes(
        IObservableAsync<ModbusTcpSlave> slave,
        ushort startAddress,
        IObservableAsync<bool[]> valuesToWrite) =>
        ToAsyncObservable(
            TcpSlaveWriter.WriteCoilDiscretes(
                ToObservable(slave),
                startAddress,
                ToObservable(valuesToWrite)));

    /// <summary>Writes coils to UDP slave streams from async observable values.</summary>
    /// <param name="slave">The extension receiver.</param>
    /// <param name="startAddress">The starting address.</param>
    /// <param name="valuesToWrite">The values to write.</param>
    /// <returns>The async slave stream.</returns>
    public static IObservableAsync<ModbusUdpSlave> WriteCoilDiscretes(
        IObservableAsync<ModbusUdpSlave> slave,
        ushort startAddress,
        IObservableAsync<bool[]> valuesToWrite) =>
        ToAsyncObservable(
            UdpSlaveWriter.WriteCoilDiscretes(
                ToObservable(slave),
                startAddress,
                ToObservable(valuesToWrite)));

    /// <summary>Writes holding registers to serial slave streams from async observable values.</summary>
    /// <param name="slave">The extension receiver.</param>
    /// <param name="startAddress">The starting address.</param>
    /// <param name="valuesToWrite">The values to write.</param>
    /// <returns>The async slave stream.</returns>
    public static IObservableAsync<ModbusSerialSlave> WriteHoldingRegisters(
        IObservableAsync<ModbusSerialSlave> slave,
        ushort startAddress,
        IObservableAsync<ushort[]> valuesToWrite) =>
        ToAsyncObservable(
            SerialSlaveWriter.WriteHoldingRegisters(
                ToObservable(slave),
                startAddress,
                ToObservable(valuesToWrite)));

    /// <summary>Writes holding registers to TCP slave streams from async observable values.</summary>
    /// <param name="slave">The extension receiver.</param>
    /// <param name="startAddress">The starting address.</param>
    /// <param name="valuesToWrite">The values to write.</param>
    /// <returns>The async slave stream.</returns>
    public static IObservableAsync<ModbusTcpSlave> WriteHoldingRegisters(
        IObservableAsync<ModbusTcpSlave> slave,
        ushort startAddress,
        IObservableAsync<ushort[]> valuesToWrite) =>
        ToAsyncObservable(
            TcpSlaveWriter.WriteHoldingRegisters(
                ToObservable(slave),
                startAddress,
                ToObservable(valuesToWrite)));

    /// <summary>Writes holding registers to UDP slave streams from async observable values.</summary>
    /// <param name="slave">The extension receiver.</param>
    /// <param name="startAddress">The starting address.</param>
    /// <param name="valuesToWrite">The values to write.</param>
    /// <returns>The async slave stream.</returns>
    public static IObservableAsync<ModbusUdpSlave> WriteHoldingRegisters(
        IObservableAsync<ModbusUdpSlave> slave,
        ushort startAddress,
        IObservableAsync<ushort[]> valuesToWrite) =>
        ToAsyncObservable(
            UdpSlaveWriter.WriteHoldingRegisters(
                ToObservable(slave),
                startAddress,
                ToObservable(valuesToWrite)));

    /// <summary>Writes discrete inputs to serial slave streams from async observable values.</summary>
    /// <param name="slave">The extension receiver.</param>
    /// <param name="startAddress">The starting address.</param>
    /// <param name="valuesToWrite">The values to write.</param>
    /// <returns>The async slave stream.</returns>
    public static IObservableAsync<ModbusSerialSlave> WriteInputDiscretes(
        IObservableAsync<ModbusSerialSlave> slave,
        ushort startAddress,
        IObservableAsync<bool[]> valuesToWrite) =>
        ToAsyncObservable(
            SerialSlaveWriter.WriteInputDiscretes(
                ToObservable(slave),
                startAddress,
                ToObservable(valuesToWrite)));

    /// <summary>Writes discrete inputs to TCP slave streams from async observable values.</summary>
    /// <param name="slave">The extension receiver.</param>
    /// <param name="startAddress">The starting address.</param>
    /// <param name="valuesToWrite">The values to write.</param>
    /// <returns>The async slave stream.</returns>
    public static IObservableAsync<ModbusTcpSlave> WriteInputDiscretes(
        IObservableAsync<ModbusTcpSlave> slave,
        ushort startAddress,
        IObservableAsync<bool[]> valuesToWrite) =>
        ToAsyncObservable(
            TcpSlaveWriter.WriteInputDiscretes(
                ToObservable(slave),
                startAddress,
                ToObservable(valuesToWrite)));

    /// <summary>Writes discrete inputs to UDP slave streams from async observable values.</summary>
    /// <param name="slave">The extension receiver.</param>
    /// <param name="startAddress">The starting address.</param>
    /// <param name="valuesToWrite">The values to write.</param>
    /// <returns>The async slave stream.</returns>
    public static IObservableAsync<ModbusUdpSlave> WriteInputDiscretes(
        IObservableAsync<ModbusUdpSlave> slave,
        ushort startAddress,
        IObservableAsync<bool[]> valuesToWrite) =>
        ToAsyncObservable(
            UdpSlaveWriter.WriteInputDiscretes(
                ToObservable(slave),
                startAddress,
                ToObservable(valuesToWrite)));

    /// <summary>Writes input registers to serial slave streams from async observable values.</summary>
    /// <param name="slave">The extension receiver.</param>
    /// <param name="startAddress">The starting address.</param>
    /// <param name="valuesToWrite">The values to write.</param>
    /// <returns>The async slave stream.</returns>
    public static IObservableAsync<ModbusSerialSlave> WriteInputRegisters(
        IObservableAsync<ModbusSerialSlave> slave,
        ushort startAddress,
        IObservableAsync<ushort[]> valuesToWrite) =>
        ToAsyncObservable(
            SerialSlaveWriter.WriteInputRegisters(
                ToObservable(slave),
                startAddress,
                ToObservable(valuesToWrite)));

    /// <summary>Writes input registers to TCP slave streams from async observable values.</summary>
    /// <param name="slave">The extension receiver.</param>
    /// <param name="startAddress">The starting address.</param>
    /// <param name="valuesToWrite">The values to write.</param>
    /// <returns>The async slave stream.</returns>
    public static IObservableAsync<ModbusTcpSlave> WriteInputRegisters(
        IObservableAsync<ModbusTcpSlave> slave,
        ushort startAddress,
        IObservableAsync<ushort[]> valuesToWrite) =>
        ToAsyncObservable(
            TcpSlaveWriter.WriteInputRegisters(
                ToObservable(slave),
                startAddress,
                ToObservable(valuesToWrite)));

    /// <summary>Writes input registers to UDP slave streams from async observable values.</summary>
    /// <param name="slave">The extension receiver.</param>
    /// <param name="startAddress">The starting address.</param>
    /// <param name="valuesToWrite">The values to write.</param>
    /// <returns>The async slave stream.</returns>
    public static IObservableAsync<ModbusUdpSlave> WriteInputRegisters(
        IObservableAsync<ModbusUdpSlave> slave,
        ushort startAddress,
        IObservableAsync<ushort[]> valuesToWrite) =>
        ToAsyncObservable(
            UdpSlaveWriter.WriteInputRegisters(
                ToObservable(slave),
                startAddress,
                ToObservable(valuesToWrite)));

    /// <summary>Provides Observable Adapter functionality.</summary>
    /// <typeparam name="T">The T type.</typeparam>
    /// <param name="source">The source value.</param>
    private sealed class ObservableAdapter<T>(IObservableAsync<T> source) : IObservable<T>
    {
        /// <summary>Executes the Subscribe operation.</summary>
        /// <param name="observer">The observer value.</param>
        /// <returns>The result.</returns>
        public IDisposable Subscribe(IObserver<T> observer)
        {
            var subscription = new AsyncToSyncSubscription<T>(source, observer);
            subscription.Connect();
            return subscription;
        }
    }

    /// <summary>Provides Async To Sync Subscription functionality.</summary>
    /// <typeparam name="T">The T type.</typeparam>
    /// <param name="source">The source value.</param>
    /// <param name="observer">The observer value.</param>
    private sealed class AsyncToSyncSubscription<T>(
        IObservableAsync<T> source,
        IObserver<T> observer) : IDisposable, IObserverAsync<T>
    {
        /// <summary>Stores the cancellation Token Source value.</summary>
        private readonly CancellationTokenSource _cancellationTokenSource = new();

        /// <summary>Stores the subscription value.</summary>
        private IAsyncDisposable? _subscription;

        /// <summary>Stores the disposed value.</summary>
        private bool _disposed;

        /// <summary>Executes the Connect operation.</summary>
        public void Connect()
        {
            _ = ConnectAsync();
        }

        /// <summary>Executes the On Next Async operation.</summary>
        /// <param name="value">The value.</param>
        /// <param name="cancellationToken">The cancellation Token value.</param>
        /// <returns>The result.</returns>
        public ValueTask OnNextAsync(T value, CancellationToken cancellationToken)
        {
            if (cancellationToken.IsCancellationRequested || _disposed)
            {
                return default;
            }

            observer.OnNext(value);

            return default;
        }

        /// <summary>Executes the On Error Resume Async operation.</summary>
        /// <param name="error">The error value.</param>
        /// <param name="cancellationToken">The cancellation Token value.</param>
        /// <returns>The result.</returns>
        public ValueTask OnErrorResumeAsync(Exception error, CancellationToken cancellationToken)
        {
            if (cancellationToken.IsCancellationRequested || _disposed)
            {
                return default;
            }

            observer.OnError(error);

            return default;
        }

        /// <summary>Executes the On Completed Async operation.</summary>
        /// <param name="result">The result value.</param>
        /// <returns>The result.</returns>
        public ValueTask OnCompletedAsync(Result result)
        {
            if (_disposed)
            {
                return default;
            }

            observer.OnCompleted();

            return default;
        }

        /// <summary>Executes the Dispose operation.</summary>
        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            _cancellationTokenSource.Cancel();
            var subscription = Interlocked.Exchange(ref _subscription, null);
            if (subscription is not null)
            {
                subscription.DisposeAsync().AsTask().GetAwaiter().GetResult();
            }

            _cancellationTokenSource.Dispose();
        }

        /// <summary>Executes the Dispose Async operation.</summary>
        /// <returns>The result.</returns>
        public ValueTask DisposeAsync()
        {
            Dispose();
            return default;
        }

        /// <summary>Executes the Connect Async operation.</summary>
        /// <returns>The result.</returns>
        private async Task ConnectAsync()
        {
            try
            {
                var subscription = await source.SubscribeAsync(this, _cancellationTokenSource.Token);
                if (Interlocked.CompareExchange(ref _subscription, subscription, null) is not null || _disposed)
                {
                    await subscription.DisposeAsync();
                }
            }
            catch (Exception ex) when (!_disposed)
            {
                observer.OnError(ex);
            }
        }
    }

    /// <summary>Provides Observable Async Adapter functionality.</summary>
    /// <typeparam name="T">The T type.</typeparam>
    /// <param name="source">The source value.</param>
    private sealed class ObservableAsyncAdapter<T>(IObservable<T> source) : IObservableAsync<T>
    {
        /// <summary>Executes the Subscribe Async operation.</summary>
        /// <param name="observer">The observer value.</param>
        /// <param name="cancellationToken">The cancellation Token value.</param>
        /// <returns>The result.</returns>
        public ValueTask<IAsyncDisposable> SubscribeAsync(
            IObserverAsync<T> observer,
            CancellationToken cancellationToken)
        {
            var subscription = source.Subscribe(new AsyncObserverAdapter<T>(observer, cancellationToken));

            return new ValueTask<IAsyncDisposable>(new AsyncSubscription(subscription));
        }
    }

    /// <summary>Provides Async Observer Adapter functionality.</summary>
    /// <typeparam name="T">The T type.</typeparam>
    /// <param name="observer">The observer value.</param>
    /// <param name="cancellationToken">The cancellation Token value.</param>
    private sealed class AsyncObserverAdapter<T>(
        IObserverAsync<T> observer,
        CancellationToken cancellationToken) : IObserver<T>
    {
        /// <summary>Executes the On Completed operation.</summary>
        public void OnCompleted()
        {
            CompleteValueTask(observer.OnCompletedAsync(Result.Success));
        }

        /// <summary>Executes the On Error operation.</summary>
        /// <param name="error">The error value.</param>
        public void OnError(Exception error)
        {
            CompleteValueTask(observer.OnErrorResumeAsync(error, cancellationToken));
        }

        /// <summary>Executes the On Next operation.</summary>
        /// <param name="value">The value.</param>
        public void OnNext(T value)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                return;
            }

            CompleteValueTask(observer.OnNextAsync(value, cancellationToken));
        }

        /// <summary>Completes a value task without blocking an already-completed operation.</summary>
        /// <param name="valueTask">The task to complete.</param>
        private static void CompleteValueTask(ValueTask valueTask)
        {
            if (valueTask.IsCompletedSuccessfully)
            {
                valueTask.GetAwaiter().GetResult();
                return;
            }

            _ = CompleteValueTaskAsync(valueTask);
        }

        /// <summary>Asynchronously completes a value task.</summary>
        /// <param name="valueTask">The task to complete.</param>
        /// <returns>A task that represents completion.</returns>
        private static async Task CompleteValueTaskAsync(ValueTask valueTask) =>
            await valueTask.ConfigureAwait(false);
    }

    /// <summary>Provides Async Subscription functionality.</summary>
    /// <param name="subscription">The subscription value.</param>
    private sealed class AsyncSubscription(IDisposable subscription) : IAsyncDisposable
    {
        /// <summary>Executes the Dispose Async operation.</summary>
        /// <returns>The result.</returns>
        public ValueTask DisposeAsync()
        {
            subscription.Dispose();
            return default;
        }
    }
}
