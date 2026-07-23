// Copyright (c) 2019-2026 Chris Pulman and contributors. All rights reserved.
// Chris Pulman and contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Collections.ObjectModel;
using System.Runtime.CompilerServices;

#if REACTIVE_SHIM
namespace IoT.DriverCore.ModbusRx.Reactive.Data;
#else
namespace IoT.DriverCore.ModbusRx.Data;
#endif

/// <summary>
/// Object simulation of device memory map.
/// The underlying collections are thread safe when using the ModbusMaster API to read/write values.
/// You can use the SyncRoot property to synchronize direct access to the DataStore collections.
/// </summary>
public class DataStore : IDisposable
{
    /// <summary>Stores the lock value.</summary>
    private readonly ReaderWriterLockSlim _lock = new();

    /// <summary>Stores the number of completed range reads.</summary>
    private long _readOperations;

    /// <summary>Stores the number of completed range writes.</summary>
    private long _writeOperations;

    /// <summary>Stores the number of copied range elements.</summary>
    private long _elementCopies;

    /// <summary>Stores the number of read-result collections created.</summary>
    private long _resultCollectionAllocations;

    /// <summary>Stores the number of non-indexable write inputs materialized.</summary>
    private long _inputMaterializations;

    /// <summary>Initializes a new instance of the <see cref="DataStore" /> class.</summary>
    public DataStore()
    {
        CoilDiscretes = new() { ModbusDataType = ModbusDataType.Coil };
        InputDiscretes = new() { ModbusDataType = ModbusDataType.Input };
        HoldingRegisters = new() { ModbusDataType = ModbusDataType.HoldingRegister };
        InputRegisters = new() { ModbusDataType = ModbusDataType.InputRegister };
    }

    /// <summary>Initializes a new instance of the <see cref="DataStore"/> class.</summary>
    /// <param name="coilDiscretes">List of discrete coil values.</param>
    /// <param name="inputDiscretes">List of discrete input values.</param>
    /// <param name="holdingRegisters">List of holding register values.</param>
    /// <param name="inputRegisters">List of input register values.</param>
    internal DataStore(
        IList<bool> coilDiscretes,
        IList<bool> inputDiscretes,
        IList<ushort> holdingRegisters,
        IList<ushort> inputRegisters)
    {
        CoilDiscretes = new(coilDiscretes) { ModbusDataType = ModbusDataType.Coil };
        InputDiscretes = new(inputDiscretes) { ModbusDataType = ModbusDataType.Input };
        HoldingRegisters = new(holdingRegisters) { ModbusDataType = ModbusDataType.HoldingRegister };
        InputRegisters = new(inputRegisters) { ModbusDataType = ModbusDataType.InputRegister };
    }

    /// <summary>Occurs when the DataStore is written to via a Modbus command.</summary>
    public event EventHandler<DataStoreEventArgs>? DataStoreWrittenTo;

    /// <summary>Occurs when the DataStore is read from via a Modbus command.</summary>
    public event EventHandler<DataStoreEventArgs>? DataStoreReadFrom;

    /// <summary>Gets the discrete coils.</summary>
    public ModbusDataCollection<bool> CoilDiscretes { get; }

    /// <summary>Gets the discrete inputs.</summary>
    public ModbusDataCollection<bool> InputDiscretes { get; }

    /// <summary>Gets the holding registers.</summary>
    public ModbusDataCollection<ushort> HoldingRegisters { get; }

    /// <summary>Gets the input registers.</summary>
    public ModbusDataCollection<ushort> InputRegisters { get; }

    /// <summary>Gets an object that can be used to synchronize direct access to the DataStore collections.</summary>
    public object SyncRoot { get; } = new();

    /// <summary>Gets the reader-writer lock for more granular access control.</summary>
    public ReaderWriterLockSlim Lock { get; } = new();

    /// <summary>Gets a deterministic snapshot of data-store range-operation work.</summary>
    /// <remarks>The counters describe logical operations; they do not use wall-clock measurements.</remarks>
    /// <returns>The current range-operation counters.</returns>
    public DataStoreOperationMetrics GetOperationMetrics() => new(
        Interlocked.Read(ref _readOperations),
        Interlocked.Read(ref _writeOperations),
        Interlocked.Read(ref _elementCopies),
        Interlocked.Read(ref _resultCollectionAllocations),
        Interlocked.Read(ref _inputMaterializations));

    /// <summary>Disposes the DataStore and releases resources.</summary>
    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    /// <summary>Performs a bulk read operation with optimized memory allocation.</summary>
    /// <typeparam name="T">The collection type.</typeparam>
    /// <typeparam name="TU">The type of elements in the collection.</typeparam>
    /// <param name="dataSource">The data source to read from.</param>
    /// <param name="resultFactory">Creates the result collection.</param>
    /// <param name="startAddress">The starting address.</param>
    /// <param name="count">The number of items to read.</param>
    /// <returns>The read data collection.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public T ReadDataOptimized<T, TU>(
        ModbusDataCollection<TU> dataSource,
        Func<T> resultFactory,
        ushort startAddress,
        ushort count)
        where T : Collection<TU>, new()
        where TU : struct
    {
        if (dataSource is null)
        {
            throw new ArgumentNullException(nameof(dataSource));
        }

        if (resultFactory is null)
        {
            throw new ArgumentNullException(nameof(resultFactory));
        }

        var startIndex = startAddress + 1;

        if (startIndex < 0 || dataSource.Count < startIndex + count)
        {
            throw new InvalidModbusRequestException(Modbus.IllegalDataAddress);
        }

        var result = resultFactory();
        _lock.EnterReadLock();
        try
        {
            for (var i = 0; i < count; i++)
            {
                result.Add(dataSource[startIndex + i]);
            }
        }
        finally
        {
            _lock.ExitReadLock();
        }

        RecordRead(count);
        var dataStoreEventArgs = DataStoreEventArgs.CreateDataStoreEventArgs(
            startAddress,
            dataSource.ModbusDataType,
            result);
        DataStoreReadFrom?.Invoke(this, dataStoreEventArgs);
        return result;
    }

    /// <summary>Performs a bulk write operation with optimized memory allocation.</summary>
    /// <typeparam name="TData">The type of the data.</typeparam>
    /// <param name="items">The items to write.</param>
    /// <param name="destination">The destination collection.</param>
    /// <param name="startAddress">The starting address.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void WriteDataOptimized<TData>(
        IEnumerable<TData> items,
        ModbusDataCollection<TData> destination,
        ushort startAddress)
        where TData : struct
    {
        if (destination is null)
        {
            throw new ArgumentNullException(nameof(destination));
        }

        var materializedItems = MaterializeItems(items, out var materialized);
        var startIndex = startAddress + 1;

        if (startIndex < 0 || destination.Count < startIndex + materializedItems.Count)
        {
            throw new InvalidModbusRequestException(Modbus.IllegalDataAddress);
        }

        _lock.EnterWriteLock();
        try
        {
            Update(materializedItems, destination, startIndex);
        }
        finally
        {
            _lock.ExitWriteLock();
        }

        RecordWrite(materializedItems.Count, materialized);

        var dataStoreEventArgs = DataStoreEventArgs.CreateDataStoreEventArgs(
            startAddress,
            destination.ModbusDataType,
            materializedItems);

        DataStoreWrittenTo?.Invoke(this, dataStoreEventArgs);
    }

    /// <summary>Retrieves subset of data from collection.</summary>
    /// <typeparam name="T">The collection type.</typeparam>
    /// <typeparam name="TU">The type of elements in the collection.</typeparam>
    /// <param name="dataStore">The data store raising the read event.</param>
    /// <param name="dataSource">The source collection.</param>
    /// <param name="startAddress">The starting Modbus address.</param>
    /// <param name="count">The number of values to read.</param>
    /// <param name="syncRoot">The synchronization root for the source collection.</param>
    /// <returns>The result.</returns>
    internal static T ReadData<T, TU>(
        DataStore dataStore,
        ModbusDataCollection<TU> dataSource,
        ushort startAddress,
        ushort count,
        object syncRoot)
        where T : Collection<TU>, new()
        where TU : struct
    {
        DataStoreEventArgs dataStoreEventArgs;
        var startIndex = startAddress + 1;

        if (startIndex < 0 || dataSource.Count < startIndex + count)
        {
            throw new InvalidModbusRequestException(Modbus.IllegalDataAddress);
        }

        var result = new T();
        lock (syncRoot)
        {
            for (var i = 0; i < count; i++)
            {
                result.Add(dataSource[startIndex + i]);
            }
        }

        dataStore.RecordRead(count);
        dataStoreEventArgs = DataStoreEventArgs.CreateDataStoreEventArgs(
            startAddress,
            dataSource.ModbusDataType,
            result);
        dataStore.DataStoreReadFrom?.Invoke(dataStore, dataStoreEventArgs);
        return result;
    }

    /// <summary>Write data to data store.</summary>
    /// <typeparam name="TData">The type of the data.</typeparam>
    /// <param name="dataStore">The data store raising the write event.</param>
    /// <param name="items">The values to write.</param>
    /// <param name="destination">The destination collection.</param>
    /// <param name="startAddress">The starting Modbus address.</param>
    /// <param name="syncRoot">The synchronization root for the destination collection.</param>
    internal static void WriteData<TData>(
        DataStore dataStore,
        IEnumerable<TData> items,
        ModbusDataCollection<TData> destination,
        ushort startAddress,
        object syncRoot)
        where TData : struct
    {
        DataStoreEventArgs dataStoreEventArgs;
        var startIndex = startAddress + 1;
        var materializedItems = MaterializeItems(items, out var materialized);

        if (startIndex < 0 || destination.Count < startIndex + materializedItems.Count)
        {
            throw new InvalidModbusRequestException(Modbus.IllegalDataAddress);
        }

        lock (syncRoot)
        {
            Update(materializedItems, destination, startIndex);
        }

        dataStore.RecordWrite(materializedItems.Count, materialized);

        dataStoreEventArgs = DataStoreEventArgs.CreateDataStoreEventArgs(
            startAddress,
            destination.ModbusDataType,
            materializedItems);

        dataStore.DataStoreWrittenTo?.Invoke(dataStore, dataStoreEventArgs);
    }

    /// <summary>Updates a subset of values in a collection.</summary>
    /// <typeparam name="T">The collection item type.</typeparam>
    /// <param name="items">The items to write.</param>
    /// <param name="destination">The destination collection.</param>
    /// <param name="startIndex">The zero-based destination index.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void Update<T>(IEnumerable<T> items, IList<T> destination, int startIndex)
    {
        var materializedItems = MaterializeItems(items, out _);

        if (startIndex < 0 || destination.Count < startIndex + materializedItems.Count)
        {
            throw new InvalidModbusRequestException(Modbus.IllegalDataAddress);
        }

        var index = startIndex;

        foreach (var item in materializedItems)
        {
            destination[index] = item;
            ++index;
        }
    }

    /// <summary>Protected virtual dispose method.</summary>
    /// <param name="disposing">Indicates if disposing.</param>
    protected virtual void Dispose(bool disposing)
    {
        if (!disposing)
        {
            return;
        }

        _lock.Dispose();
    }

    /// <summary>Returns indexable input without duplicating existing lists and arrays.</summary>
    /// <typeparam name="T">The T type.</typeparam>
    /// <param name="items">The items value.</param>
    /// <param name="materialized">Receives whether the input was materialized.</param>
    /// <returns>An indexable view of the input.</returns>
    private static IReadOnlyList<T> MaterializeItems<T>(IEnumerable<T> items, out bool materialized)
    {
        if (items is null)
        {
            throw new ArgumentNullException(nameof(items));
        }

        if (items is not IReadOnlyList<T> indexedItems)
        {
            materialized = true;
            return new List<T>(items);
        }

        materialized = false;
        return indexedItems;
    }

    /// <summary>Records one completed range read.</summary>
    /// <param name="elementCount">The number of elements copied to the result.</param>
    private void RecordRead(int elementCount)
    {
        _ = Interlocked.Increment(ref _readOperations);
        _ = Interlocked.Increment(ref _resultCollectionAllocations);
        _ = Interlocked.Add(ref _elementCopies, elementCount);
    }

    /// <summary>Records one completed range write.</summary>
    /// <param name="elementCount">The number of elements written.</param>
    /// <param name="materialized">Whether the input was materialized once.</param>
    private void RecordWrite(int elementCount, bool materialized)
    {
        _ = Interlocked.Increment(ref _writeOperations);
        _ = Interlocked.Add(ref _elementCopies, elementCount);
        if (!materialized)
        {
            return;
        }

        _ = Interlocked.Increment(ref _inputMaterializations);
    }
}
