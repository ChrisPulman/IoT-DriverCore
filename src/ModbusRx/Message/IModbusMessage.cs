// Copyright (c) 2019-2026 Chris Pulman and contributors. All rights reserved.
// Chris Pulman and contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

#if REACTIVE_SHIM
namespace IoT.Driver.ModbusRx.Reactive.Message;
#else
namespace IoT.Driver.ModbusRx.Message;
#endif

/// <summary>A message built by the master (client) that initiates a Modbus transaction.</summary>
public interface IModbusMessage
{
    /// <summary>Gets or sets the function code tells the server what kind of action to perform.</summary>
    byte FunctionCode { get; set; }

    /// <summary>Gets or sets address of the slave (server).</summary>
    byte SlaveAddress { get; set; }

    /// <summary>Gets or sets a unique identifier assigned to a message when using the IP protocol.</summary>
    ushort TransactionId { get; set; }

    /// <summary>Creates a composition of the slave address and protocol data unit.</summary>
    /// <returns>A newly allocated Modbus message frame.</returns>
    byte[] ToMessageFrame();

    /// <summary>Creates a composition of the function code and message data.</summary>
    /// <returns>A newly allocated protocol data unit.</returns>
    byte[] ToProtocolDataUnit();

    /// <summary>Initializes a modbus message from the specified message frame.</summary>
    /// <param name="frame">Bytes of Modbus frame.</param>
    void Initialize(byte[] frame);
}
