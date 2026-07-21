// Copyright (c) 2019-2026 Chris Pulman and contributors. All rights reserved.
// Chris Pulman and contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System;
#if REACTIVE_SHIM
using OmronPlcRx.Reactive.Enums;
#else
using OmronPlcRx.Enums;
#endif

#if REACTIVE_SHIM
namespace OmronPlcRx.Reactive;
#else
namespace OmronPlcRx;
#endif

/// <summary>Configures an Omron PLC transport connection.</summary>
/// <param name="localNodeId">Local FINS node identifier.</param>
/// <param name="remoteNodeId">Remote PLC FINS node identifier.</param>
/// <param name="connectionMethod">Transport to use.</param>
/// <param name="remoteHost">PLC hostname, IP address, or serial port name.</param>
public sealed class OmronConnectionOptions(
    byte localNodeId,
    byte remoteNodeId,
    ConnectionMethod connectionMethod,
    string remoteHost)
{
    /// <summary>Gets the local FINS node identifier.</summary>
    public byte LocalNodeId { get; } = localNodeId;

    /// <summary>Gets the remote PLC FINS node identifier.</summary>
    public byte RemoteNodeId { get; } = remoteNodeId;

    /// <summary>Gets the transport to use.</summary>
    public ConnectionMethod ConnectionMethod { get; } = connectionMethod;

    /// <summary>Gets the PLC hostname, IP address, or serial port name.</summary>
    public string RemoteHost { get; } =
        remoteHost ?? throw new ArgumentNullException(nameof(remoteHost));

    /// <summary>Gets or initializes the network service port.</summary>
    public int Port { get; init; } = 9600;

    /// <summary>Gets or initializes the request timeout in milliseconds.</summary>
    public int Timeout { get; init; } = 2000;

    /// <summary>Gets or initializes the transient retry count.</summary>
    public int Retries { get; init; } = 1;

    /// <summary>Gets or initializes serial transport settings.</summary>
    public OmronSerialOptions? SerialOptions { get; init; }
}
