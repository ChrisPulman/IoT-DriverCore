// Copyright (c) 2019-2026 Chris Pulman and contributors. All rights reserved.
// Chris Pulman and contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
#if REACTIVE_SHIM
using ModbusRx.Reactive.Data;
#else
using ModbusRx.Data;
#endif
#if REACTIVE_SHIM
using ModbusRx.Reactive.IO;
#else
using ModbusRx.IO;
#endif
#if REACTIVE_SHIM
using ModbusRx.Reactive.Message;
#else
using ModbusRx.Message;
#endif

#if REACTIVE_SHIM
namespace ModbusRx.Reactive.Device;
#else
namespace ModbusRx.Device;
#endif

/// <summary>Modbus slave device.</summary>
public abstract class ModbusSlave : ModbusDevice
{
    /// <summary>Initializes a new instance of the Modbus Slave class.</summary>
    /// <param name="unitId">The unit Id value.</param>
    /// <param name="transport">The transport value.</param>
    internal ModbusSlave(byte unitId, ModbusTransport transport)
        : base(transport)
    {
        DataStore = DataStoreFactory.CreateDefaultDataStore();
        UnitId = unitId;
    }

    /// <summary>Raised when a Modbus slave receives a request, before processing request function.</summary>
    /// <exception cref="InvalidModbusRequestException">Thrown when a request requires an error response.</exception>
    public event EventHandler<ModbusSlaveRequestEventArgs>? ModbusSlaveRequestReceived;

    /// <summary>Raised after a Modbus slave processes the write portion of a request.</summary>
    /// <remarks>For function code 23, the event occurs after writing and before reading.</remarks>
    public event EventHandler<ModbusSlaveRequestEventArgs>? WriteComplete;

    /// <summary>Gets or sets the data store.</summary>
    public DataStore DataStore { get; set; }

    /// <summary>Gets or sets the unit ID.</summary>
    public byte UnitId { get; set; }

    /// <summary>Start slave listening for requests.</summary>
    /// <returns>A Task.</returns>
    public abstract Task ListenAsync();

    /// <summary>Defines the Read Discretes value.</summary>
    /// <param name="request">The read request.</param>
    /// <param name="dataStore">The data store to read from.</param>
    /// <param name="dataSource">The source collection.</param>
    /// <returns>The result.</returns>
    internal static ReadCoilsInputsResponse ReadDiscretes(
        ReadCoilsInputsRequest request,
        DataStore dataStore,
        ModbusDataCollection<bool> dataSource)
    {
        var data = DataStore.ReadData<DiscreteCollection, bool>(
            dataStore,
            dataSource,
            request.StartAddress,
            request.NumberOfPoints,
            dataStore.SyncRoot);

        return new ReadCoilsInputsResponse(
            request.FunctionCode,
            request.SlaveAddress,
            data.ByteCount,
            data);
    }

    /// <summary>Defines the Read Registers value.</summary>
    /// <param name="request">The read request.</param>
    /// <param name="dataStore">The data store to read from.</param>
    /// <param name="dataSource">The source collection.</param>
    /// <returns>The result.</returns>
    internal static ReadHoldingInputRegistersResponse ReadRegisters(
        ReadHoldingInputRegistersRequest? request,
        DataStore dataStore,
        ModbusDataCollection<ushort> dataSource)
    {
        var data = DataStore.ReadData<RegisterCollection, ushort>(
            dataStore,
            dataSource,
            request!.StartAddress,
            request.NumberOfPoints,
            dataStore.SyncRoot);

        return new ReadHoldingInputRegistersResponse(
            request.FunctionCode,
            request.SlaveAddress,
            data);
    }

    /// <summary>Defines the Write Single Coil value.</summary>
    /// <param name="request">The write request.</param>
    /// <param name="dataStore">The data store to write to.</param>
    /// <param name="dataSource">The destination collection.</param>
    /// <returns>The result.</returns>
    internal static WriteSingleCoilRequestResponse WriteSingleCoil(
        WriteSingleCoilRequestResponse request,
        DataStore dataStore,
        ModbusDataCollection<bool> dataSource)
    {
        DataStore.WriteData(
            dataStore,
            new DiscreteCollection(request.Data[0] == Modbus.CoilOn),
            dataSource,
            request.StartAddress,
            dataStore.SyncRoot);

        return request;
    }

    /// <summary>Defines the Write Multiple Coils value.</summary>
    /// <param name="request">The write request.</param>
    /// <param name="dataStore">The data store to write to.</param>
    /// <param name="dataSource">The destination collection.</param>
    /// <returns>The result.</returns>
    internal static WriteMultipleCoilsResponse WriteMultipleCoils(
        WriteMultipleCoilsRequest request,
        DataStore dataStore,
        ModbusDataCollection<bool> dataSource)
    {
        var coils = new bool[request.NumberOfPoints];
        for (var i = 0; i < coils.Length; i++)
        {
            coils[i] = request.Data[i];
        }

        DataStore.WriteData(
            dataStore,
            coils,
            dataSource,
            request.StartAddress,
            dataStore.SyncRoot);

        return new(
            request.SlaveAddress,
            request.StartAddress,
            request.NumberOfPoints);
    }

    /// <summary>Defines the Write Single Register value.</summary>
    /// <param name="request">The write request.</param>
    /// <param name="dataStore">The data store to write to.</param>
    /// <param name="dataSource">The destination collection.</param>
    /// <returns>The result.</returns>
    internal static WriteSingleRegisterRequestResponse WriteSingleRegister(
        WriteSingleRegisterRequestResponse request,
        DataStore dataStore,
        ModbusDataCollection<ushort> dataSource)
    {
        DataStore.WriteData(
            dataStore,
            request.Data,
            dataSource,
            request.StartAddress,
            dataStore.SyncRoot);

        return request;
    }

    /// <summary>Defines the Write Multiple Registers value.</summary>
    /// <param name="request">The write request.</param>
    /// <param name="dataStore">The data store to write to.</param>
    /// <param name="dataSource">The destination collection.</param>
    /// <returns>The result.</returns>
    internal static WriteMultipleRegistersResponse WriteMultipleRegisters(
        WriteMultipleRegistersRequest? request,
        DataStore dataStore,
        ModbusDataCollection<ushort> dataSource)
    {
        DataStore.WriteData(
            dataStore,
            request!.Data,
            dataSource,
            request.StartAddress,
            dataStore.SyncRoot);

        return new(
            request.SlaveAddress,
            request.StartAddress,
            request.NumberOfPoints);
    }

    /// <summary>Executes the Apply Request operation.</summary>
    /// <param name="request">The request value.</param>
    /// <returns>The result.</returns>
    internal IModbusMessage ApplyRequest(IModbusMessage request)
    {
        try
        {
            Debug.WriteLine(request.ToString());
            var eventArgs = new ModbusSlaveRequestEventArgs(request);
            ModbusSlaveRequestReceived?.Invoke(this, eventArgs);
            return ApplyRequestCore(request, eventArgs);
        }
        catch (InvalidModbusRequestException ex)
        {
            // Catches an illegal-function exception or a custom ModbusSlaveRequestReceived exception.
            return new SlaveExceptionResponse(
                request.SlaveAddress,
                (byte)(Modbus.ExceptionOffset + request.FunctionCode),
                ex.ExceptionCode);
        }
    }

    /// <summary>Throws an illegal-function response for an unsupported function code.</summary>
    /// <param name="functionCode">The unsupported function code.</param>
    /// <returns>This method does not return.</returns>
    private static IModbusMessage ThrowUnsupportedFunction(byte functionCode)
    {
        Debug.WriteLine($"Unsupported function code {functionCode}.");
        throw new InvalidModbusRequestException(Modbus.IllegalFunction);
    }

    /// <summary>Dispatches a validated request to the corresponding data-store operation.</summary>
    /// <param name="request">The request to apply.</param>
    /// <param name="eventArgs">The write-completion event data.</param>
    /// <returns>The response message.</returns>
    private IModbusMessage ApplyRequestCore(
        IModbusMessage request,
        ModbusSlaveRequestEventArgs eventArgs) =>
        request.FunctionCode switch
        {
            Modbus.ReadCoils => ReadDiscretes(
                (ReadCoilsInputsRequest)request,
                DataStore,
                DataStore.CoilDiscretes),
            Modbus.ReadInputs => ReadDiscretes(
                (ReadCoilsInputsRequest)request,
                DataStore,
                DataStore.InputDiscretes),
            Modbus.ReadHoldingRegisters => ReadRegisters(
                (ReadHoldingInputRegistersRequest)request,
                DataStore,
                DataStore.HoldingRegisters),
            Modbus.ReadInputRegisters => ReadRegisters(
                (ReadHoldingInputRegistersRequest)request,
                DataStore,
                DataStore.InputRegisters),
            Modbus.Diagnostics => request,
            Modbus.WriteSingleCoil => CompleteWrite(
                WriteSingleCoil(
                    (WriteSingleCoilRequestResponse)request,
                    DataStore,
                    DataStore.CoilDiscretes),
                eventArgs),
            Modbus.WriteSingleRegister => CompleteWrite(
                WriteSingleRegister(
                    (WriteSingleRegisterRequestResponse)request,
                    DataStore,
                    DataStore.HoldingRegisters),
                eventArgs),
            Modbus.WriteMultipleCoils => CompleteWrite(
                WriteMultipleCoils(
                    (WriteMultipleCoilsRequest)request,
                    DataStore,
                    DataStore.CoilDiscretes),
                eventArgs),
            Modbus.WriteMultipleRegisters => CompleteWrite(
                WriteMultipleRegisters(
                    (WriteMultipleRegistersRequest)request,
                    DataStore,
                    DataStore.HoldingRegisters),
                eventArgs),
            Modbus.ReadWriteMultipleRegisters => ApplyReadWriteRequest(
                (ReadWriteMultipleRegistersRequest)request,
                eventArgs),
            _ => ThrowUnsupportedFunction(request.FunctionCode),
        };

    /// <summary>Raises write completion and returns the generated response.</summary>
    /// <param name="response">The generated response.</param>
    /// <param name="eventArgs">The request event data.</param>
    /// <returns>The response emitted after completion.</returns>
    private IModbusMessage CompleteWrite(
        IModbusMessage response,
        ModbusSlaveRequestEventArgs eventArgs)
    {
        WriteComplete?.Invoke(this, eventArgs);
        return response;
    }

    /// <summary>Applies the write and read portions of a combined request.</summary>
    /// <param name="request">The combined request.</param>
    /// <param name="eventArgs">The request event data.</param>
    /// <returns>The read response.</returns>
    private ReadHoldingInputRegistersResponse ApplyReadWriteRequest(
        ReadWriteMultipleRegistersRequest request,
        ModbusSlaveRequestEventArgs eventArgs)
    {
        _ = WriteMultipleRegisters(request.WriteRequest, DataStore, DataStore.HoldingRegisters);
        WriteComplete?.Invoke(this, eventArgs);
        return ReadRegisters(request.ReadRequest, DataStore, DataStore.HoldingRegisters);
    }
}
