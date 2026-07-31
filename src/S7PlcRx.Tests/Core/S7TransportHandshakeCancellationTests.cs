// Copyright (c) 2019-2026 Chris Pulman and contributors. All rights reserved.
// Chris Pulman and contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

#if !NETFRAMEWORK
using System.Net;
using System.Net.Sockets;
using IoT.Driver.S7PlcRx.Core;
using IoT.Driver.S7PlcRx.Enums;
using TUnitAssert = TUnit.Assertions.Assert;

namespace IoT.Driver.S7PlcRx.Tests.Core;

/// <summary>Provides deterministic coverage for the S7 connection pool, metrics, and socket transport.</summary>
public sealed partial class S7TransportCoreDeterministicCoverageTests
{
    /// <summary>Defines the timeout used to cancel a deliberately stalled handshake.</summary>
    private const int HandshakeCancellationTimeoutMilliseconds = 100;

    /// <summary>Verifies a modern asynchronous handshake stops when its cancellation token expires.</summary>
    /// <returns>A task that represents the asynchronous assertion.</returns>
    [Test]
    public async Task ModernHandshakeStopsWhenCancellationExpiresAsync()
    {
        var (transportSocket, peerSocket) = await CreateConnectedSocketPairAsync();
        using var peer = peerSocket;
        using var transport = new S7SocketRx(
            IPAddress.Loopback.ToString(),
            CpuType.S71500,
            RackNumber,
            SlotNumber,
            transportSocket,
            TimeProvider.System);
        using var cancellation = new CancellationTokenSource(
            TimeSpan.FromMilliseconds(HandshakeCancellationTimeoutMilliseconds));
        var profile = GetPrivateTsapProfile("PG");

        var result = await InvokePrivateTaskAsync<bool>(
            transport,
            "PerformOptimizedHandshakeModernAsync",
            [typeof(Socket), typeof(byte[]), profile.GetType(), typeof(CancellationToken)],
            transportSocket,
            new byte[HandshakeReceiveBufferLength],
            profile,
            cancellation.Token);

        await TUnitAssert.That(result).IsFalse();
    }
}
#endif
