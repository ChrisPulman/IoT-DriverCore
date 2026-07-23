// Copyright (c) 2019-2026 Chris Pulman and contributors. All rights reserved.
// Chris Pulman and contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Net;
using System.Net.Sockets;
using System.Text;

#if REACTIVE_SHIM

namespace IoT.DriverCore.MitsubishiRx.Reactive.Tests;

#else

namespace IoT.DriverCore.MitsubishiRx.Tests;

#endif

/// <summary>Exercises the managed socket transport against deterministic loopback peers.</summary>
internal sealed class MitsubishiSocketTransportTests
{
    /// <summary>Stores the two-byte partial response length.</summary>
    private const int PartialResponseLength = 2;

    /// <summary>Stores the expected dropped-response length.</summary>
    private const int DroppedResponseLength = 4;

    /// <summary>Stores the deterministic server response delay.</summary>
    private const int ServerResponseDelayMilliseconds = 10;

    /// <summary>Stores the timeout applied to local socket tests.</summary>
    private static readonly TimeSpan SocketTimeout = TimeSpan.FromSeconds(5);

    /// <summary>Verifies fixed-length and protocol-length TCP receive paths.</summary>
    /// <returns>A task that represents the asynchronous test.</returns>
    [Test]
    internal async Task TcpReceiveModesRoundTripAgainstLoopbackPeerAsync()
    {
        var fixedResponse = new byte[] { 1, 2, 3, 4, 5 };
        var fixedResult = await ExchangeTcpAsync(
            MitsubishiFrameType.ThreeE,
            CommunicationDataCode.Binary,
            fixedResponse,
            fixedResponse.Length);
        await Assert.That(fixedResult).IsEquivalentTo(fixedResponse);

        var threeEBinary = new byte[]
        {
            0xD0, 0x00, 0x00, 0xFF, 0xFF, 0x03, 0x00,
            0x06, 0x00, 0x00, 0x00, 0x34, 0x12, 0x78, 0x56,
        };
        var threeEResult = await ExchangeTcpAsync(
            MitsubishiFrameType.ThreeE,
            CommunicationDataCode.Binary,
            threeEBinary,
            null);
        await Assert.That(threeEResult).IsEquivalentTo(threeEBinary);

        var fourEBinary = new byte[]
        {
            0xD4, 0x00, 0x34, 0x12, 0x00, 0x00, 0x00, 0xFF, 0xFF, 0x03, 0x00,
            0x04, 0x00, 0x00, 0x00, 0xCD, 0xAB,
        };
        var fourEResult = await ExchangeTcpAsync(
            MitsubishiFrameType.FourE,
            CommunicationDataCode.Binary,
            fourEBinary,
            null);
        await Assert.That(fourEResult).IsEquivalentTo(fourEBinary);

        var oneEBinary = new byte[]
        {
            0x81, 0x00, 0x34, 0x12, 0x78, 0x56, 0x9A, 0xBC, 0xDE, 0xF0, 0x11,
        };
        var oneEResult = await ExchangeTcpAsync(
            MitsubishiFrameType.OneE,
            CommunicationDataCode.Binary,
            oneEBinary,
            null);
        await Assert.That(oneEResult).IsEquivalentTo(oneEBinary);
    }

    /// <summary>Verifies both ASCII header sizes and UDP datagram reception.</summary>
    /// <returns>A task that represents the asynchronous test.</returns>
    [Test]
    internal async Task AsciiTcpAndUdpRoundTripAgainstLoopbackPeersAsync()
    {
        var threeEAscii = Encoding.ASCII.GetBytes("D00000FF03FF00000400001234");
        var threeEResult = await ExchangeTcpAsync(
            MitsubishiFrameType.ThreeE,
            CommunicationDataCode.Ascii,
            threeEAscii,
            null);
        await Assert.That(threeEResult).IsEquivalentTo(threeEAscii);

        var fourEAscii = Encoding.ASCII.GetBytes("D4001234000000FF03FF0000020000");
        var fourEResult = await ExchangeTcpAsync(
            MitsubishiFrameType.FourE,
            CommunicationDataCode.Ascii,
            fourEAscii,
            null);
        await Assert.That(fourEResult).IsEquivalentTo(fourEAscii);

        using var server = new UdpClient(new IPEndPoint(IPAddress.Loopback, 0));
        var endpoint = (IPEndPoint)server.Client.LocalEndPoint!;
        var udpResponse = new byte[] { 9, 8, 7, 6 };
        var serverTask = Task.Run(async () =>
        {
            var received = await server.ReceiveAsync().WaitAsync(SocketTimeout);
            _ = await server.SendAsync(udpResponse, received.RemoteEndPoint)
                .AsTask().WaitAsync(SocketTimeout);
        });

        var options = CreateOptions(
            endpoint.Port,
            MitsubishiFrameType.ThreeE,
            CommunicationDataCode.Binary,
            MitsubishiTransportKind.Udp);
        await using var transport = new SocketMitsubishiTransport();
        await transport.ConnectAsync(options, CancellationToken.None);
        var result = await transport.ExchangeAsync(
            new MitsubishiTransportRequest([0xAA], null, "UDP loopback"),
            CancellationToken.None);
        await serverTask;

        await Assert.That(result).IsEquivalentTo(udpResponse);
        await Assert.That(transport.IsConnected).IsTrue();
    }

    /// <summary>Verifies configuration, connection-drop, reconnect, and disposal paths.</summary>
    /// <returns>A task that represents the asynchronous test.</returns>
    [Test]
    internal async Task SocketFailuresAndLifecycleAreDeterministicAsync()
    {
        var unconfigured = new SocketMitsubishiTransport();
        _ = Assert.Throws<InvalidOperationException>(
            () => unconfigured.ExchangeAsync(
                new MitsubishiTransportRequest([1], 1, "Unconfigured"),
                CancellationToken.None).AsTask().GetAwaiter().GetResult());
        unconfigured.Dispose();

        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var endpoint = (IPEndPoint)listener.LocalEndpoint;
        var serverTask = Task.Run(async () =>
        {
            using var peer = await listener.AcceptSocketAsync().WaitAsync(SocketTimeout);
            var request = new byte[16];
            _ = await peer.ReceiveAsync(request, SocketFlags.None).WaitAsync(SocketTimeout);
            _ = await peer.SendAsync(
                    new byte[PartialResponseLength],
                    SocketFlags.None)
                .WaitAsync(SocketTimeout);
            peer.Shutdown(SocketShutdown.Both);
        });

        var options = CreateOptions(
            endpoint.Port,
            MitsubishiFrameType.ThreeE,
            CommunicationDataCode.Binary,
            MitsubishiTransportKind.Tcp);
        await using var transport = new SocketMitsubishiTransport();
        await transport.ConnectAsync(options, CancellationToken.None);
        await transport.ConnectAsync(options, CancellationToken.None);
        _ = Assert.Throws<IOException>(
            () => transport.ExchangeAsync(
                new MitsubishiTransportRequest(
                    [1],
                    DroppedResponseLength,
                    "Dropped TCP response"),
                CancellationToken.None).AsTask().GetAwaiter().GetResult());
        await serverTask;
        await transport.DisconnectAsync(CancellationToken.None);

        await Assert.That(transport.IsConnected).IsFalse();
        _ = Assert.Throws<ArgumentNullException>(
            () => transport.ConnectAsync(null!, CancellationToken.None)
                .AsTask().GetAwaiter().GetResult());
        _ = Assert.Throws<ArgumentNullException>(
            () => transport.ExchangeAsync(null!, CancellationToken.None)
                .AsTask().GetAwaiter().GetResult());
    }

    /// <summary>Runs one TCP exchange against a local deterministic peer.</summary>
    /// <param name="frameType">The MC frame type.</param>
    /// <param name="dataCode">The MC data code.</param>
    /// <param name="response">The complete server response.</param>
    /// <param name="expectedResponseLength">The optional fixed receive length.</param>
    /// <returns>The bytes returned by the socket transport.</returns>
    private static async Task<byte[]> ExchangeTcpAsync(
        MitsubishiFrameType frameType,
        CommunicationDataCode dataCode,
        byte[] response,
        int? expectedResponseLength)
    {
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var endpoint = (IPEndPoint)listener.LocalEndpoint;
        var serverTask = Task.Run(async () =>
        {
            using var peer = await listener.AcceptSocketAsync().WaitAsync(SocketTimeout);
            var request = new byte[32];
            _ = await peer.ReceiveAsync(request, SocketFlags.None).WaitAsync(SocketTimeout);
            var split = Math.Max(1, response.Length / PartialResponseLength);
            _ = await peer.SendAsync(response.AsMemory(0, split), SocketFlags.None)
                .AsTask().WaitAsync(SocketTimeout);
            await Task.Delay(TimeSpan.FromMilliseconds(ServerResponseDelayMilliseconds));
            _ = await peer.SendAsync(response.AsMemory(split), SocketFlags.None)
                .AsTask().WaitAsync(SocketTimeout);
        });

        var options = CreateOptions(
            endpoint.Port,
            frameType,
            dataCode,
            MitsubishiTransportKind.Tcp);
        await using var transport = new SocketMitsubishiTransport();
        await transport.ConnectAsync(options, CancellationToken.None);
        var result = await transport.ExchangeAsync(
            new MitsubishiTransportRequest([0x01, 0x02], expectedResponseLength, "TCP loopback"),
            CancellationToken.None);
        await serverTask;
        await transport.DisconnectAsync(CancellationToken.None);
        return result;
    }

    /// <summary>Creates options for loopback socket tests.</summary>
    /// <param name="port">The local peer port.</param>
    /// <param name="frameType">The MC frame type.</param>
    /// <param name="dataCode">The MC data code.</param>
    /// <param name="transportKind">The socket transport kind.</param>
    /// <returns>The client options.</returns>
    private static MitsubishiClientOptions CreateOptions(
        int port,
        MitsubishiFrameType frameType,
        CommunicationDataCode dataCode,
        MitsubishiTransportKind transportKind) =>
        new(
            "localhost",
            port,
            frameType,
            dataCode,
            transportKind,
            Timeout: SocketTimeout);
}
