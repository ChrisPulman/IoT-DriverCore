// Copyright (c) 2019-2026 Chris Pulman and contributors. All rights reserved.
// Chris Pulman and contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Net;
#if REACTIVE_SHIM
using IoT.Driver.ModbusRx.Reactive.Data;
#else
using IoT.Driver.ModbusRx.Data;
#endif

#if REACTIVE_SHIM
namespace IoT.Driver.ModbusRx.Reactive.Message;
#else
namespace IoT.Driver.ModbusRx.Message;
#endif

/// <summary>
/// Class holding all implementation shared between two or more message types.
/// Interfaces expose subsets of type specific implementations.
/// </summary>
internal sealed class ModbusMessageImpl
{
    /// <summary>Initializes a new instance of the Modbus Message Impl class.</summary>
    public ModbusMessageImpl()
    {
    }

    /// <summary>Initializes a new instance of the Modbus Message Impl class.</summary>
    /// <param name="slaveAddress">The slave Address value.</param>
    /// <param name="functionCode">The function Code value.</param>
    public ModbusMessageImpl(byte slaveAddress, byte functionCode)
    {
        SlaveAddress = slaveAddress;
        FunctionCode = functionCode;
    }

    /// <summary>Gets or sets the Byte Count value.</summary>
    internal byte? ByteCount { get; set; }

    /// <summary>Gets or sets the Exception Code value.</summary>
    internal byte? ExceptionCode { get; set; }

    /// <summary>Gets or sets the Transaction Id value.</summary>
    internal ushort TransactionId { get; set; }

    /// <summary>Gets or sets the Function Code value.</summary>
    internal byte FunctionCode { get; set; }

    /// <summary>Gets or sets the Number Of Points value.</summary>
    internal ushort? NumberOfPoints { get; set; }

    /// <summary>Gets or sets the Slave Address value.</summary>
    internal byte SlaveAddress { get; set; }

    /// <summary>Gets or sets the Start Address value.</summary>
    internal ushort? StartAddress { get; set; }

    /// <summary>Gets or sets the Sub Function Code value.</summary>
    internal ushort? SubFunctionCode { get; set; }

    /// <summary>Gets or sets the Data value.</summary>
    internal IDataCollection? Data { get; set; }

    /// <summary>Creates the message frame.</summary>
    /// <returns>A newly allocated message frame.</returns>
    internal byte[] ToMessageFrame()
    {
        var pdu = ToProtocolDataUnit();
        using var frame = new MemoryStream(1 + pdu.Length);

        frame.WriteByte(SlaveAddress);
        frame.Write(pdu, 0, pdu.Length);

        return frame.ToArray();
    }

    /// <summary>Creates the protocol data unit.</summary>
    /// <returns>A newly allocated protocol data unit.</returns>
    internal byte[] ToProtocolDataUnit()
    {
        var pdu = new List<byte>
        {
            FunctionCode,
        };

        AddOptionalByte(pdu, ExceptionCode);
        AddOptionalNetworkOrder(pdu, SubFunctionCode);
        AddOptionalNetworkOrder(pdu, StartAddress);
        AddOptionalNetworkOrder(pdu, NumberOfPoints);
        AddOptionalByte(pdu, ByteCount);
        AddData(pdu, Data);

        return pdu.ToArray();

        static void AddOptionalByte(List<byte> target, byte? value)
        {
            if (!value.HasValue)
            {
                return;
            }

            target.Add(value.Value);
        }

        static void AddOptionalNetworkOrder(List<byte> target, ushort? value)
        {
            if (!value.HasValue)
            {
                return;
            }

            target.AddRange(BitConverter.GetBytes(IPAddress.HostToNetworkOrder((short)value.Value)));
        }

        static void AddData(List<byte> target, IDataCollection? data)
        {
            if (data is null)
            {
                return;
            }

            target.AddRange(data.ToNetworkBytes());
        }
    }

    /// <summary>Executes the Initialize operation.</summary>
    /// <param name="frame">The frame value.</param>
    internal void Initialize(byte[] frame)
    {
        frame = ArgumentGuard.NotNull(frame, nameof(frame));

        if (frame.Length < Modbus.MinimumFrameSize)
        {
            var msg = $"Message frame must contain at least {Modbus.MinimumFrameSize} bytes of data.";
            throw new FormatException(msg);
        }

        SlaveAddress = frame[0];
        FunctionCode = frame[1];
    }
}
