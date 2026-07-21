// Copyright (c) 2019-2026 Chris Pulman and contributors. All rights reserved.
// Chris Pulman and contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

#if REACTIVE_SHIM
namespace ModbusRx.Reactive;
#else
namespace ModbusRx;
#endif

/// <summary>Defines constants related to the Modbus protocol.</summary>
internal static class Modbus
{
    /// <summary>Supported function codes.</summary>
    internal const byte ReadCoils = 1;

    /// <summary>Defines the Read Inputs value.</summary>
    internal const byte ReadInputs = 2;

    /// <summary>Defines the Read Holding Registers value.</summary>
    internal const byte ReadHoldingRegisters = 3;

    /// <summary>Defines the Read Input Registers value.</summary>
    internal const byte ReadInputRegisters = 4;

    /// <summary>Defines the Write Single Coil value.</summary>
    internal const byte WriteSingleCoil = 5;

    /// <summary>Defines the Write Single Register value.</summary>
    internal const byte WriteSingleRegister = 6;

    /// <summary>Defines the Diagnostics value.</summary>
    internal const byte Diagnostics = 8;

    /// <summary>Defines the Diagnostics Return Query Data value.</summary>
    internal const ushort DiagnosticsReturnQueryData = 0;

    /// <summary>Defines the Write Multiple Coils value.</summary>
    internal const byte WriteMultipleCoils = 15;

    /// <summary>Defines the Write Multiple Registers value.</summary>
    internal const byte WriteMultipleRegisters = 16;

    /// <summary>Defines the Read Write Multiple Registers value.</summary>
    internal const byte ReadWriteMultipleRegisters = 23;

    /// <summary>Defines the Maximum Discrete Request Response Size value.</summary>
    internal const int MaximumDiscreteRequestResponseSize = 2040;

    /// <summary>Defines the Maximum Register Request Response Size value.</summary>
    internal const int MaximumRegisterRequestResponseSize = 127;

    /// <summary>Modbus slave exception offset that is added to the function code, to flag an exception.</summary>
    internal const byte ExceptionOffset = 128;

    /// <summary>Modbus slave exception codes.</summary>
    internal const byte IllegalFunction = 1;

    /// <summary>Defines the Illegal Data Address value.</summary>
    internal const byte IllegalDataAddress = 2;

    /// <summary>Defines the Acknowledge value.</summary>
    internal const byte Acknowledge = 5;

    /// <summary>Defines the Slave Device Busy value.</summary>
    internal const byte SlaveDeviceBusy = 6;

    /// <summary>Default setting for number of retries for IO operations.</summary>
    internal const int DefaultRetries = 3;

    /// <summary>Default retry delay after an acknowledge or slave-device-busy exception response.</summary>
    internal const int DefaultWaitToRetryMilliseconds = 250;

    /// <summary>Default setting for IO timeouts in milliseconds.</summary>
    internal const int DefaultTimeout = 1000;

    /// <summary>Smallest supported message frame size (sans checksum).</summary>
    internal const int MinimumFrameSize = 2;

    /// <summary>Defines the Coil On value.</summary>
    internal const ushort CoilOn = 0xFF00;

    /// <summary>Defines the Coil Off value.</summary>
    internal const ushort CoilOff = 0x0000;

    /// <summary>IP slaves should be addressed by IP.</summary>
    internal const byte DefaultIpSlaveUnitId = 0;

    /// <summary>An existing connection was forcibly closed by the remote host.</summary>
    internal const int ConnectionResetByPeer = 10_054;

    /// <summary>Existing socket connection is being closed.</summary>
    internal const int WSACancelBlockingCall = 10_004;

    /// <summary>Used by the ASCII tranport to indicate end of message.</summary>
    internal const string NewLine = "\r\n";
}
