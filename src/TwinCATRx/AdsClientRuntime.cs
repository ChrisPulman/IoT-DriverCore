// Copyright (c) 2019-2026 Chris Pulman and contributors. All rights reserved.
// Chris Pulman and contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using TwinCAT.Ads;

#if REACTIVE_SHIM
namespace IoT.DriverCore.TwinCATRx.Reactive;
#else
namespace IoT.DriverCore.TwinCATRx;
#endif

/// <summary>Adapts the Beckhoff ADS client to the runtime contract.</summary>
internal sealed class AdsClientRuntime : IAdsClientRuntime
{
    /// <summary>Stores the wrapped ADS client.</summary>
    private readonly AdsClient _client = new();

    /// <inheritdoc/>
    public bool IsConnected => _client.IsConnected;

    /// <inheritdoc/>
    public int? Port => _client.Address?.Port;

    /// <inheritdoc/>
    public void Connect(int port) => _client.Connect(port);

    /// <inheritdoc/>
    public void Connect(string adsAddress, int port) => _client.Connect(adsAddress, port);

    /// <inheritdoc/>
    public uint CreateVariableHandle(string variable) => _client.CreateVariableHandle(variable);

    /// <inheritdoc/>
    public void Dispose() => _client.Dispose();

    /// <inheritdoc/>
    public object ReadAny(uint handle, Type type) => _client.ReadAny(handle, type);

    /// <inheritdoc/>
    public object ReadAny(uint handle, Type type, int[] lengths) => _client.ReadAny(handle, type, lengths);

    /// <inheritdoc/>
    public StateInfo ReadState() => _client.ReadState();

    /// <inheritdoc/>
    public void WriteAny(uint handle, object value) => _client.WriteAny(handle, value);

    /// <inheritdoc/>
    public void WriteControl(StateInfo state) => _client.WriteControl(state);
}
