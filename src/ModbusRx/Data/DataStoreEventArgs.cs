// Copyright (c) 2019-2026 Chris Pulman and contributors. All rights reserved.
// Chris Pulman and contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Collections.ObjectModel;
#if REACTIVE_SHIM
using IoT.Driver.ModbusRx.Reactive.Utility;
#else
using IoT.Driver.ModbusRx.Utility;
#endif

#if REACTIVE_SHIM
namespace IoT.Driver.ModbusRx.Reactive.Data;
#else
namespace IoT.Driver.ModbusRx.Data;
#endif

/// <summary>Event args for read write actions performed on the DataStore.</summary>
public sealed class DataStoreEventArgs : EventArgs
{
    /// <summary>Initializes a new instance of the Data Store Event Args class.</summary>
    /// <param name="startAddress">The start Address value.</param>
    /// <param name="modbusDataType">The modbus Data Type value.</param>
    private DataStoreEventArgs(ushort startAddress, ModbusDataType modbusDataType)
    {
        StartAddress = startAddress;
        ModbusDataType = modbusDataType;
    }

    /// <summary>Gets type of Modbus data (e.g. Holding register).</summary>
    public ModbusDataType ModbusDataType { get; }

    /// <summary>Gets start address of data.</summary>
    public ushort StartAddress { get; }

    /// <summary>Gets data that was read or written.</summary>
    public DiscriminatedUnion<ReadOnlyCollection<bool>, ReadOnlyCollection<ushort>>? Data { get; private set; }

    /// <summary>Executes the Create Data Store Event Args operation.</summary>
    /// <typeparam name="T">The T type.</typeparam>
    /// <param name="startAddress">The start Address value.</param>
    /// <param name="modbusDataType">The modbus Data Type value.</param>
    /// <param name="data">The data value.</param>
    /// <returns>The result.</returns>
    internal static DataStoreEventArgs CreateDataStoreEventArgs<T>(
        ushort startAddress,
        ModbusDataType modbusDataType,
        IEnumerable<T> data)
    {
        data = ArgumentGuard.NotNull(data, nameof(data));
        var valuesToConvert = data as ICollection<T> ?? [.. data];

        if (typeof(T) == typeof(bool))
        {
            var values = new List<bool>();
            foreach (var item in valuesToConvert)
            {
                if (item is bool value)
                {
                    values.Add(value);
                }
            }

            ReadOnlyCollection<bool> readOnlyValues = new(values);

            var eventArgs = new DataStoreEventArgs(startAddress, modbusDataType);
            eventArgs.Data = DiscriminatedUnion<ReadOnlyCollection<bool>, ReadOnlyCollection<ushort>>.CreateA(readOnlyValues);
            return eventArgs;
        }

        if (typeof(T) == typeof(ushort))
        {
            var values = new List<ushort>();
            foreach (var item in valuesToConvert)
            {
                if (item is ushort value)
                {
                    values.Add(value);
                }
            }

            ReadOnlyCollection<ushort> readOnlyValues = new(values);

            var eventArgs = new DataStoreEventArgs(startAddress, modbusDataType);
            eventArgs.Data = DiscriminatedUnion<ReadOnlyCollection<bool>, ReadOnlyCollection<ushort>>.CreateB(readOnlyValues);
            return eventArgs;
        }

        throw new ArgumentException("Generic type T should be of type bool or ushort");
    }
}
