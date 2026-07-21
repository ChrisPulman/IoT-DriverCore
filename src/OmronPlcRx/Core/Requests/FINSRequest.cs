// Copyright (c) 2019-2026 Chris Pulman and contributors. All rights reserved.
// Chris Pulman and contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
#if REACTIVE_SHIM
using OmronPlcRx.Reactive.Core.Channels;
#else
using OmronPlcRx.Core.Channels;
#endif

#if REACTIVE_SHIM
namespace OmronPlcRx.Reactive.Core.Requests;
#else
namespace OmronPlcRx.Core.Requests;
#endif

/// <summary>Represents the f in sr eq ue st type.</summary>
internal abstract class FINSRequest
{
    /// <summary>Stores the h ea de rl en gt h value.</summary>
    internal const int HeaderLength = 10;

    /// <summary>Stores the c om ma nd le ng th value.</summary>
    internal const int CommandLength = 2;

    /// <summary>Initializes a new instance of the <see cref="FINSRequest"/> class.</summary>
    /// <param name="plc">The p lc value.</param>
    protected FINSRequest(OmronPLCConnection plc)
    {
        if (plc.Channel is TCPChannel tCPChannel)
        {
            LocalNodeID = tCPChannel.LocalNodeID;
            RemoteNodeID = tCPChannel.RemoteNodeID;
        }
        else
        {
            LocalNodeID = plc.LocalNodeID;
            RemoteNodeID = plc.RemoteNodeID;
        }
    }

    /// <summary>Gets the local node id value.</summary>
    internal byte LocalNodeID { get; }

    /// <summary>Gets the remote node id value.</summary>
    internal byte RemoteNodeID { get; }

    /// <summary>Gets or sets the service id value.</summary>
    internal byte ServiceID { get; set; }

    /// <summary>Gets or sets the function code value.</summary>
    internal byte FunctionCode { get; set; }

    /// <summary>Gets or sets the sub function code value.</summary>
    internal byte SubFunctionCode { get; set; }

    /// <summary>Initializes a new instance of the <see cref="BuildMessage"/> class.</summary>
    /// <param name="requestId">The r eq ue st id value.</param>
    /// <returns>The result produced by the operation.</returns>
    internal ReadOnlyMemory<byte> BuildMessage(byte requestId)
    {
        ServiceID = requestId;

        var message = new List<byte>
        {
            // Information Control Field
            0x80,

            // Reserved by System
            0,

            // Permissible Number of Gateways
            0x02,

            // Destination Network Address
            0, // Local Network

            // Destination Node Address
            // 0 = Local PLC Unit
            // 1 to 254 = Destination Node Address
            // 255 = Broadcasting
            RemoteNodeID,

            // Destination Unit Address
            0, // PLC (CPU Unit)

            // Source Network Address
            0, // Local Network

            // Source Node Address
            LocalNodeID, // Local Server

            // Source Unit Address
            0,

            // Service ID
            ServiceID,

            // Main Function Code
            FunctionCode,

            // Sub Function Code
            SubFunctionCode,
        };

        // Request Data
        message.AddRange(BuildRequestData());

        return new ReadOnlyMemory<byte>([.. message]);
    }

    /// <summary>Initializes a new instance of the <see cref="BuildRequestData"/> class.</summary>
    /// <returns>The result produced by the operation.</returns>
    protected abstract List<byte> BuildRequestData();
}
