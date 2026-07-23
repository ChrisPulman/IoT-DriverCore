// Copyright (c) 2019-2026 Chris Pulman and contributors. All rights reserved.
// Chris Pulman and contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

#if REACTIVE_SHIM
namespace IoT.DriverCore.ModbusRx.Reactive.Data;
#else
namespace IoT.DriverCore.ModbusRx.Data;
#endif

/// <summary>Modbus message containing data.</summary>
public interface IDataCollection
{
    /// <summary>Gets the network bytes.</summary>
    byte[] NetworkBytes { get; }

    /// <summary>Gets the byte count.</summary>
    byte ByteCount { get; }
}
