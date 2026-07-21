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
namespace OmronPlcRx.Reactive.Core;
#else
namespace OmronPlcRx.Core;
#endif

/// <summary>Provides validation and metadata helpers for PLC connections.</summary>
internal static class OmronPLCConnectionMetadata
{
    /// <summary>Stores known controller model prefixes.</summary>
    private static readonly (string Prefix, PlcType Type)[] ModelPrefixes =
    [
        ("NJ101", PlcType.NJ101),
        ("NJ301", PlcType.NJ301),
        ("NJ501", PlcType.NJ501),
        ("NX1P2", PlcType.NX1P2),
        ("NX102", PlcType.NX102),
        ("NX701", PlcType.NX701),
        ("CJ2", PlcType.CJ2),
        ("CP1", PlcType.CP1),
        ("C", PlcType.C_Series),
    ];

    /// <summary>Validates FINS node identifiers.</summary>
    /// <param name="localNodeId">The local node identifier.</param>
    /// <param name="remoteNodeId">The remote node identifier.</param>
    /// <param name="connectionMethod">The connection method.</param>
    internal static void ValidateNodeIdentifiers(byte localNodeId, byte remoteNodeId, ConnectionMethod connectionMethod)
    {
        ThrowIfReservedLocalNode(localNodeId);
        ThrowIfReservedRemoteNode(remoteNodeId, connectionMethod);
        ThrowIfSameNode(localNodeId, remoteNodeId);
    }

    /// <summary>Validates the remote host.</summary>
    /// <param name="remoteHost">The remote host.</param>
    /// <returns>The validated remote host.</returns>
    internal static string ValidateRemoteHost(string remoteHost)
    {
        var host = remoteHost ?? throw new ArgumentNullException(nameof(remoteHost), "The Remote Host cannot be Null");
        return host.Length == 0
            ? throw new ArgumentException("The Remote Host cannot be Empty", nameof(remoteHost))
            : host;
    }

    /// <summary>Validates the network port.</summary>
    /// <param name="connectionMethod">The connection method.</param>
    /// <param name="port">The network port.</param>
    internal static void ValidatePort(ConnectionMethod connectionMethod, int port)
    {
        if (connectionMethod == ConnectionMethod.Serial || port > 0)
        {
            return;
        }

        throw new ArgumentOutOfRangeException(nameof(port), "The Port cannot be less than 1");
    }

    /// <summary>Gets the PLC type for a controller model string.</summary>
    /// <param name="controllerModel">The controller model string.</param>
    /// <returns>The inferred PLC type.</returns>
    internal static PlcType GetPLCType(string controllerModel)
    {
        foreach (var (prefix, type) in ModelPrefixes)
        {
            if (controllerModel.StartsWith(prefix, StringComparison.Ordinal))
            {
                return type;
            }
        }

        return IsNSeriesController(controllerModel) ? PlcType.NJ_NX_NY_Series : PlcType.Unknown;
    }

    /// <summary>Throws when the local node identifier is reserved.</summary>
    /// <param name="localNodeId">The local node identifier.</param>
    private static void ThrowIfReservedLocalNode(byte localNodeId)
    {
        switch (localNodeId)
        {
            case 0:
                throw new ArgumentOutOfRangeException(nameof(localNodeId), "The Local Node ID cannot be set to 0");
            case ProtocolConstants.TwoHundredFiftyFive:
                throw new ArgumentOutOfRangeException(nameof(localNodeId), "The Local Node ID cannot be set to 255");
        }
    }

    /// <summary>Throws when the remote node identifier is reserved.</summary>
    /// <param name="remoteNodeId">The remote node identifier.</param>
    /// <param name="connectionMethod">The connection method.</param>
    private static void ThrowIfReservedRemoteNode(byte remoteNodeId, ConnectionMethod connectionMethod)
    {
        switch (remoteNodeId)
        {
            case 0 when connectionMethod != ConnectionMethod.Serial:
                throw new ArgumentOutOfRangeException(
                    nameof(remoteNodeId),
                    "The Remote Node ID cannot be set to 0 for Ethernet FINS connections");
            case ProtocolConstants.TwoHundredFiftyFive:
                throw new ArgumentOutOfRangeException(nameof(remoteNodeId), "The Remote Node ID cannot be set to 255");
        }
    }

    /// <summary>Throws when local and remote node identifiers match.</summary>
    /// <param name="localNodeId">The local node identifier.</param>
    /// <param name="remoteNodeId">The remote node identifier.</param>
    private static void ThrowIfSameNode(byte localNodeId, byte remoteNodeId)
    {
        if (remoteNodeId != localNodeId)
        {
            return;
        }

        throw new ArgumentException("The Remote Node ID cannot be the same as the Local Node ID", nameof(remoteNodeId));
    }

    /// <summary>Checks whether a controller model is in the N-series family.</summary>
    /// <param name="controllerModel">The controller model string.</param>
    /// <returns>A value indicating whether the model is in the N-series family.</returns>
    private static bool IsNSeriesController(string controllerModel) =>
        controllerModel.StartsWith("NJ", StringComparison.Ordinal) ||
        controllerModel.StartsWith("NX", StringComparison.Ordinal) ||
        controllerModel.StartsWith("NY", StringComparison.Ordinal);
}
