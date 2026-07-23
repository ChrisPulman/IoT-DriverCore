// Copyright (c) 2019-2026 Chris Pulman and contributors. All rights reserved.
// Chris Pulman and contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

#if REACTIVE_SHIM
namespace IoT.DriverCore.ModbusRx.Reactive.Device;
#else
namespace IoT.DriverCore.ModbusRx.Device;
#endif

/// <summary>Represents a disposable resource whose disposed state can be inspected.</summary>
public interface ICancelable : IDisposable
{
    /// <summary>Gets a value indicating whether the resource has been disposed.</summary>
    bool IsDisposed { get; }
}
