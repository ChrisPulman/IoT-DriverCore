// Copyright (c) 2019-2026 Chris Pulman and contributors. All rights reserved.
// Chris Pulman and contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

#if REACTIVE_SHIM
namespace IoT.Driver.ModbusRx.Reactive.Data;
#else
namespace IoT.Driver.ModbusRx.Data;
#endif

/// <summary>Modbus message containing data.</summary>
public interface IDataCollection
{
    /// <summary>Gets the byte count.</summary>
    byte ByteCount { get; }

    /// <summary>Creates the current network-byte representation.</summary>
    /// <returns>A new byte array in Modbus network order.</returns>
    byte[] ToNetworkBytes();
}
