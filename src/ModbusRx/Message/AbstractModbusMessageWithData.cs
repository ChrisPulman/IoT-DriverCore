// Copyright (c) 2019-2026 Chris Pulman and contributors. All rights reserved.
// Chris Pulman and contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

#if REACTIVE_SHIM
using IoT.DriverCore.ModbusRx.Reactive.Data;
#else
using IoT.DriverCore.ModbusRx.Data;
#endif

#if REACTIVE_SHIM
namespace IoT.DriverCore.ModbusRx.Reactive.Message;
#else
namespace IoT.DriverCore.ModbusRx.Message;
#endif

/// <summary>Provides AbstractModbusMessageWithData functionality.</summary>
/// <typeparam name="TData">The type of the data.</typeparam>
/// <seealso cref="AbstractModbusMessage" />
public abstract class AbstractModbusMessageWithData<TData> : AbstractModbusMessage
    where TData : IDataCollection
{
    /// <summary>Initializes a new instance of the Abstract Modbus Message With Data class.</summary>
    internal AbstractModbusMessageWithData()
    {
    }

    /// <summary>Initializes a new instance of the Abstract Modbus Message With Data class.</summary>
    /// <param name="slaveAddress">The slave Address value.</param>
    /// <param name="functionCode">The function Code value.</param>
    internal AbstractModbusMessageWithData(byte slaveAddress, byte functionCode)
        : base(slaveAddress, functionCode)
    {
    }

    /// <summary>Gets or sets the data.</summary>
    /// <value>The data.</value>
    public TData Data
    {
        get => (TData)MessageImpl.Data!;
        set => MessageImpl.Data = value;
    }
}
