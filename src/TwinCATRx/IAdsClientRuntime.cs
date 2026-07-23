// Copyright (c) 2019-2026 Chris Pulman and contributors. All rights reserved.
// Chris Pulman and contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using TwinCAT.Ads;

#if REACTIVE_SHIM
namespace IoT.DriverCore.TwinCATRx.Reactive;
#else
namespace IoT.DriverCore.TwinCATRx;
#endif

/// <summary>Defines the ADS operations consumed by <see cref="RxTcAdsClient"/>.</summary>
internal interface IAdsClientRuntime : IDisposable
{
    /// <summary>Gets a value indicating whether the ADS connection is active.</summary>
    bool IsConnected { get; }

    /// <summary>Gets the connected ADS port, when available.</summary>
    int? Port { get; }

    /// <summary>Connects to a local ADS port.</summary>
    /// <param name="port">The ADS port.</param>
    void Connect(int port);

    /// <summary>Connects to a remote ADS address and port.</summary>
    /// <param name="adsAddress">The ADS address.</param>
    /// <param name="port">The ADS port.</param>
    void Connect(string adsAddress, int port);

    /// <summary>Creates a native handle for one variable.</summary>
    /// <param name="variable">The variable name.</param>
    /// <returns>The native handle.</returns>
    uint CreateVariableHandle(string variable);

    /// <summary>Reads one scalar native value.</summary>
    /// <param name="handle">The native handle.</param>
    /// <param name="type">The value type.</param>
    /// <returns>The native value.</returns>
    object ReadAny(uint handle, Type type);

    /// <summary>Reads one array or string native value.</summary>
    /// <param name="handle">The native handle.</param>
    /// <param name="type">The value type.</param>
    /// <param name="lengths">The requested dimensions.</param>
    /// <returns>The native value.</returns>
    object ReadAny(uint handle, Type type, int[] lengths);

    /// <summary>Reads the current ADS and device state.</summary>
    /// <returns>The current state.</returns>
    StateInfo ReadState();

    /// <summary>Writes one native value.</summary>
    /// <param name="handle">The native handle.</param>
    /// <param name="value">The value.</param>
    void WriteAny(uint handle, object value);

    /// <summary>Writes the requested ADS control state.</summary>
    /// <param name="state">The requested state.</param>
    void WriteControl(StateInfo state);
}
