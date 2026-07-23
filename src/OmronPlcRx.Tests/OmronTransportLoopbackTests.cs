// Copyright (c) 2019-2026 Chris Pulman and contributors. All rights reserved.
// Chris Pulman and contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Net;
using System.Net.Sockets;
using System.Reflection;
using IoT.DriverCore.OmronPlcRx.Core;
using IoT.DriverCore.OmronPlcRx.Core.Channels;
using IoT.DriverCore.OmronPlcRx.Core.Requests;
using IoT.DriverCore.OmronPlcRx.Enums;
using TUnit.Core;
using NetUdpClient = System.Net.Sockets.UdpClient;

namespace IoT.DriverCore.OmronPlcRx.Tests;

/// <summary>Exercises FINS TCP and UDP channels against deterministic loopback PLC peers.</summary>
public sealed class OmronTransportLoopbackTests
{
    /// <summary>Gets the TCP FINS header length.</summary>
    private const int TcpHeaderLength = 16;

    /// <summary>Gets the TCP envelope length-field offset.</summary>
    private const int TcpLengthOffset = 4;

    /// <summary>Gets the TCP envelope command-field offset.</summary>
    private const int TcpCommandOffset = 11;

    /// <summary>Gets the TCP envelope bytes following the length field.</summary>
    private const int TcpLengthOverhead = 8;

    /// <summary>Gets the FINS response length without payload data.</summary>
    private const int EmptyFinsResponseLength = 14;

    /// <summary>Gets the FINS service identifier offset.</summary>
    private const int FinsServiceIdOffset = 9;

    /// <summary>Gets the FINS command-code offset.</summary>
    private const int FinsCommandCodeOffset = 10;

    /// <summary>Gets the FINS subcommand-code offset.</summary>
    private const int FinsSubcommandCodeOffset = 11;

    /// <summary>Gets the deterministic transport timeout.</summary>
    private const int TimeoutMilliseconds = 2000;

    /// <summary>Gets the deterministic transport error timeout.</summary>
    private const int ErrorTimeoutMilliseconds = 30;

    /// <summary>Gets the deterministic peer hold duration.</summary>
    private const int PeerHoldMilliseconds = 100;

    /// <summary>Gets the local FINS node identifier.</summary>
    private const byte LocalNode = 1;

    /// <summary>Gets the remote FINS node identifier.</summary>
    private const byte RemoteNode = 2;

    /// <summary>Gets the TCP node-address response command.</summary>
    private const byte NodeAddressResponseCommand = 1;

    /// <summary>Gets the TCP FINS-frame command.</summary>
    private const byte FinsFrameCommand = 2;

    /// <summary>Gets the private TCP header validator name.</summary>
    private const string ValidateTcpHeaderMethod = "ValidateTcpHeader";

    /// <summary>Gets the private TCP payload validator name.</summary>
    private const string ValidateTcpPayloadMethod = "ValidateTcpPayload";

    /// <summary>Gets the number of deterministic TCP error codes.</summary>
    private const int TcpErrorCodeCount = 10;

    /// <summary>Gets the protected purge method name.</summary>
    private const string PurgeReceiveBufferMethod = "PurgeReceiveBufferAsync";

    /// <summary>Gets the protected reinitialize method name.</summary>
    private const string ReinitializeClientMethod = "DestroyAndInitializeClientAsync";

    /// <summary>Gets the first reserved TCP error code.</summary>
    private const byte ReservedErrorCode = 3;

    /// <summary>Gets the TCP unsupported-command error code.</summary>
    private const byte UnsupportedCommandErrorCode = 20;

    /// <summary>Gets the TCP request-too-long error code.</summary>
    private const byte RequestTooLongErrorCode = 21;

    /// <summary>Gets the TCP request-too-short error code.</summary>
    private const byte RequestTooShortErrorCode = 22;

    /// <summary>Gets the TCP request-format error code.</summary>
    private const byte RequestFormatErrorCode = 23;

    /// <summary>Gets the TCP unsupported-protocol error code.</summary>
    private const byte UnsupportedProtocolErrorCode = 24;

    /// <summary>Gets the TCP connection-limit error code.</summary>
    private const byte ConnectionLimitErrorCode = 25;

    /// <summary>Gets the shift for the most significant byte of a 32-bit integer.</summary>
    private const int MostSignificantByteShift = 24;

    /// <summary>Gets the shift for the second byte of a 32-bit integer.</summary>
    private const int SecondByteShift = 16;

    /// <summary>Gets the shift for the third byte of a 32-bit integer.</summary>
    private const int ThirdByteShift = 8;

    /// <summary>Verifies TCP negotiation and FINS request framing over a real loopback socket.</summary>
    /// <returns>A task that represents the asynchronous test.</returns>
    [Test]
    public async Task TcpChannel_NegotiatesAndProcessesFinsFrameOverLoopbackAsync()
    {
        var portSource = CreatePortSource();
        var peer = RunTcpPeerAsync(portSource);
        var port = await portSource.Task;
        using var channel = new TCPChannel(IPAddress.Loopback.ToString(), port);
        using var requestConnection = CreateRequestConnection();
        try
        {
            await channel.InitializeAsync(TimeoutMilliseconds, CancellationToken.None);
            var request = ReadClockRequest.CreateNew(requestConnection);
            var result = await channel.ProcessRequestAsync(
                request,
                TimeoutMilliseconds,
                0,
                CancellationToken.None);
            var peerResult = await peer;

            await Assert.That(channel.LocalNodeID).IsEqualTo(LocalNode);
            await Assert.That(channel.RemoteNodeID).IsEqualTo(RemoteNode);
            await Assert.That(result.Response.ServiceID).IsEqualTo(request.ServiceID);
            await Assert.That(result.BytesSent > 0).IsTrue();
            await Assert.That(result.BytesReceived > 0).IsTrue();
            await Assert.That(peerResult.NegotiationCommand).IsEqualTo((byte)0);
            await Assert.That(peerResult.FinsCommand).IsEqualTo(FinsFrameCommand);
        }
        finally
        {
            await peer;
        }
    }

    /// <summary>Verifies UDP FINS request and response framing over a real loopback datagram socket.</summary>
    /// <returns>A task that represents the asynchronous test.</returns>
    [Test]
    public async Task UdpChannel_ProcessesFinsFrameOverLoopbackAsync()
    {
        var portSource = CreatePortSource();
        var peer = RunUdpPeerAsync(portSource);
        var port = await portSource.Task;
        using var channel = new UDPChannel(IPAddress.Loopback.ToString(), port);
        using var requestConnection = CreateRequestConnection();
        try
        {
            await channel.InitializeAsync(TimeoutMilliseconds, CancellationToken.None);
            var request = ReadClockRequest.CreateNew(requestConnection);
            var result = await channel.ProcessRequestAsync(
                request,
                TimeoutMilliseconds,
                0,
                CancellationToken.None);
            var receivedLength = await peer;

            await Assert.That(receivedLength > 0).IsTrue();
            await Assert.That(result.Response.ServiceID).IsEqualTo(request.ServiceID);
            await Assert.That(result.PacketsSent).IsEqualTo(1);
            await Assert.That(result.PacketsReceived).IsEqualTo(1);
        }
        finally
        {
            await peer;
        }
    }

    /// <summary>Verifies uninitialized channels and TCP parser validation errors.</summary>
    /// <returns>A task that represents the asynchronous test.</returns>
    [Test]
    public async Task Channels_RejectUninitializedAndMalformedFramesAsync()
    {
        using var requestConnection = CreateRequestConnection();
        var request = ReadClockRequest.CreateNew(requestConnection);
        using var tcp = new TCPChannel(IPAddress.Loopback.ToString(), TimeoutMilliseconds);
        using var udp = new UDPChannel(IPAddress.Loopback.ToString(), TimeoutMilliseconds);

        await AssertThrowsAsync<OmronPLCException>(
            () => tcp.ProcessRequestAsync(request, TimeoutMilliseconds, 0, CancellationToken.None));
        await AssertThrowsAsync<OmronPLCException>(
            () => udp.ProcessRequestAsync(request, TimeoutMilliseconds, 0, CancellationToken.None));

        await AssertPrivateThrowsAsync(tcp, ValidateTcpHeaderMethod, new List<byte>());
        await AssertPrivateThrowsAsync(
            tcp,
            ValidateTcpHeaderMethod,
            new List<byte>(new byte[TcpLengthOverhead]));
        await AssertPrivateThrowsAsync(
            tcp,
            ValidateTcpHeaderMethod,
            new List<byte>(new byte[TcpHeaderLength]));
    }

    /// <summary>Verifies malformed TCP lengths, commands, and payloads are rejected.</summary>
    /// <returns>A task that represents the asynchronous test.</returns>
    [Test]
    public async Task TcpChannel_RejectsMalformedPayloadsAsync()
    {
        using var tcp = new TCPChannel(IPAddress.Loopback.ToString(), TimeoutMilliseconds);
        var invalidLength = CreateTcpFrame(FinsFrameCommand, []);
        invalidLength[TcpLengthOffset + ReservedErrorCode] = TcpLengthOverhead;
        await AssertPrivateThrowsAsync(
            tcp,
            "GetTcpMessageDataLength",
            new List<byte>(invalidLength));

        var wrongCommand = CreateTcpFrame(NodeAddressResponseCommand, CreateNodeAddressPayload());
        await AssertPrivateThrowsAsync(
            tcp,
            "ValidateTcpCommand",
            new List<byte>(wrongCommand),
            TcpCommandCode.FINSFrame);
        await AssertPrivateThrowsAsync(
            tcp,
            "ValidateFinsFrameLength",
            TcpCommandCode.FINSFrame,
            1);
        await AssertPrivateThrowsAsync(
            tcp,
            ValidateTcpPayloadMethod,
            new List<byte>(),
            TcpCommandCode.FINSFrame,
            EmptyFinsResponseLength);
        await AssertPrivateThrowsAsync(
            tcp,
            ValidateTcpPayloadMethod,
            new List<byte>(new byte[EmptyFinsResponseLength - 1]),
            TcpCommandCode.FINSFrame,
            EmptyFinsResponseLength);
        await AssertPrivateThrowsAsync(
            tcp,
            ValidateTcpPayloadMethod,
            new List<byte>(new byte[EmptyFinsResponseLength]),
            TcpCommandCode.FINSFrame,
            EmptyFinsResponseLength);

        await AssertTcpErrorMessagesAsync(tcp);
    }

    /// <summary>Verifies TCP negotiation rejects short and invalid node assignments.</summary>
    /// <returns>A task that represents the asynchronous test.</returns>
    [Test]
    public async Task TcpChannel_RejectsInvalidNegotiationAssignmentsAsync()
    {
        await AssertTcpNegotiationFailureAsync([]);
        await AssertTcpNegotiationFailureAsync(
            [0, 0, 0, 0, 0, 0, 0, RemoteNode]);
        await AssertTcpNegotiationFailureAsync(
            [0, 0, 0, LocalNode, 0, 0, 0, 0]);
    }

    /// <summary>Verifies TCP and UDP receive timeouts are translated to Omron errors.</summary>
    /// <returns>A task that represents the asynchronous test.</returns>
    [Test]
    public async Task Channels_TranslateLoopbackReceiveTimeoutsAsync()
    {
        using var connection = CreateRequestConnection();
        var request = ReadClockRequest.CreateNew(connection);
        var tcpPortSource = CreatePortSource();
        var tcpPeer = RunTcpTimeoutPeerAsync(tcpPortSource);
        using var tcp = new TCPChannel(
            IPAddress.Loopback.ToString(),
            await tcpPortSource.Task);
        await tcp.InitializeAsync(TimeoutMilliseconds, CancellationToken.None);
        await AssertThrowsAsync<OmronPLCException>(
            () => tcp.ProcessRequestAsync(
                request,
                ErrorTimeoutMilliseconds,
                0,
                CancellationToken.None));
        await tcpPeer;

        var udpPortSource = CreatePortSource();
        var udpPeer = RunUdpTimeoutPeerAsync(udpPortSource);
        using var udp = new UDPChannel(
            IPAddress.Loopback.ToString(),
            await udpPortSource.Task);
        await udp.InitializeAsync(TimeoutMilliseconds, CancellationToken.None);
        await AssertThrowsAsync<OmronPLCException>(
            () => udp.ProcessRequestAsync(
                request,
                ErrorTimeoutMilliseconds,
                0,
                CancellationToken.None));
        await udpPeer;
    }

    /// <summary>Verifies channel purge paths safely handle uninitialized clients.</summary>
    /// <returns>A task that represents the asynchronous test.</returns>
    [Test]
    public async Task Channels_PurgeUninitializedClientsAsync()
    {
        using var uninitializedTcp = new TCPChannel(
            IPAddress.Loopback.ToString(),
            TimeoutMilliseconds);
        using var uninitializedUdp = new UDPChannel(
            IPAddress.Loopback.ToString(),
            TimeoutMilliseconds);
        await InvokePrivateTaskAsync(
            uninitializedTcp,
            PurgeReceiveBufferMethod,
            ErrorTimeoutMilliseconds,
            CancellationToken.None);
        await InvokePrivateTaskAsync(
            uninitializedUdp,
            PurgeReceiveBufferMethod,
            ErrorTimeoutMilliseconds,
            CancellationToken.None);
    }

    /// <summary>Verifies initialized channels purge surplus loopback packets.</summary>
    /// <returns>A task that represents the asynchronous test.</returns>
    [Test]
    public async Task Channels_PurgeSurplusLoopbackPacketsAsync()
    {
        using var connection = CreateRequestConnection();
        var request = ReadClockRequest.CreateNew(connection);
        var tcpPortSource = CreatePortSource();
        var tcpPeer = RunTcpSurplusPeerAsync(tcpPortSource);
        using var tcp = new TCPChannel(
            IPAddress.Loopback.ToString(),
            await tcpPortSource.Task);
        await tcp.InitializeAsync(TimeoutMilliseconds, CancellationToken.None);
        _ = await tcp.ProcessRequestAsync(
            request,
            TimeoutMilliseconds,
            0,
            CancellationToken.None);
        await InvokePrivateTaskAsync(
            tcp,
            PurgeReceiveBufferMethod,
            TimeoutMilliseconds,
            CancellationToken.None);
        await tcpPeer;

        var udpPortSource = CreatePortSource();
        var udpPeer = RunUdpSurplusPeerAsync(udpPortSource);
        using var udp = new UDPChannel(
            IPAddress.Loopback.ToString(),
            await udpPortSource.Task);
        await udp.InitializeAsync(TimeoutMilliseconds, CancellationToken.None);
        _ = await udp.ProcessRequestAsync(
            request,
            TimeoutMilliseconds,
            0,
            CancellationToken.None);
        await InvokePrivateTaskAsync(
            udp,
            PurgeReceiveBufferMethod,
            TimeoutMilliseconds,
            CancellationToken.None);
        await udpPeer;

        await Assert.That(tcp.LocalNodeID).IsEqualTo(LocalNode);
    }

    /// <summary>Verifies channel reinitialization replaces live transport clients.</summary>
    /// <returns>A task that represents the asynchronous test.</returns>
    [Test]
    public async Task Channels_ReinitializeLiveLoopbackClientsAsync()
    {
        var tcpPortSource = CreatePortSource();
        var tcpPeer = RunTcpReinitializePeerAsync(tcpPortSource);
        using var tcp = new TCPChannel(
            IPAddress.Loopback.ToString(),
            await tcpPortSource.Task);
        await tcp.InitializeAsync(TimeoutMilliseconds, CancellationToken.None);
        await InvokePrivateTaskAsync(
            tcp,
            ReinitializeClientMethod,
            TimeoutMilliseconds,
            CancellationToken.None);
        await tcpPeer;

        using var udp = new UDPChannel(
            IPAddress.Loopback.ToString(),
            TimeoutMilliseconds);
        await udp.InitializeAsync(TimeoutMilliseconds, CancellationToken.None);
        await InvokePrivateTaskAsync(
            udp,
            ReinitializeClientMethod,
            TimeoutMilliseconds,
            CancellationToken.None);

        await Assert.That(tcp.RemoteNodeID).IsEqualTo(RemoteNode);
    }

    /// <summary>Verifies disposed socket clients are translated by channel send and receive operations.</summary>
    /// <returns>A task that represents the asynchronous test.</returns>
    [Test]
    public async Task Channels_TranslateDisposedClientOperationsAsync()
    {
        var tcpPortSource = CreatePortSource();
        var tcpPeer = RunTcpNegotiationHoldPeerAsync(tcpPortSource);
        using var tcp = new TCPChannel(
            IPAddress.Loopback.ToString(),
            await tcpPortSource.Task);
        await tcp.InitializeAsync(TimeoutMilliseconds, CancellationToken.None);
        DisposeTransportClient(tcp);
        await AssertThrowsAsync<OmronPLCException>(
            () => InvokePrivateTaskAsync(
                tcp,
                "SendMessageAsync",
                ReadOnlyMemory<byte>.Empty,
                ErrorTimeoutMilliseconds,
                CancellationToken.None));
        await AssertThrowsAsync<OmronPLCException>(
            () => InvokePrivateTaskAsync(
                tcp,
                "ReceiveMessageAsync",
                ErrorTimeoutMilliseconds,
                CancellationToken.None));
        await tcpPeer;

        using var udp = new UDPChannel(
            IPAddress.Loopback.ToString(),
            TimeoutMilliseconds);
        await udp.InitializeAsync(TimeoutMilliseconds, CancellationToken.None);
        DisposeTransportClient(udp);
        await AssertThrowsAsync<OmronPLCException>(
            () => InvokePrivateTaskAsync(
                udp,
                "SendMessageAsync",
                ReadOnlyMemory<byte>.Empty,
                ErrorTimeoutMilliseconds,
                CancellationToken.None));
        await AssertThrowsAsync<OmronPLCException>(
            () => InvokePrivateTaskAsync(
                udp,
                "ReceiveMessageAsync",
                ErrorTimeoutMilliseconds,
                CancellationToken.None));
    }

    /// <summary>Verifies empty, disposed, and refused-client cleanup paths.</summary>
    /// <returns>A task that represents the asynchronous test.</returns>
    [Test]
    public async Task Channels_CleanupEmptyDisposedAndRefusedClientsAsync()
    {
        var tcpPortSource = CreatePortSource();
        var tcpPeer = RunTcpNegotiationHoldPeerAsync(tcpPortSource);
        using var tcp = new TCPChannel(
            IPAddress.Loopback.ToString(),
            await tcpPortSource.Task);
        await tcp.InitializeAsync(TimeoutMilliseconds, CancellationToken.None);
        await InvokePrivateTaskAsync(
            tcp,
            PurgeReceiveBufferMethod,
            ErrorTimeoutMilliseconds,
            CancellationToken.None);
        DisposeTransportClient(tcp);
        await InvokePrivateTaskAsync(
            tcp,
            PurgeReceiveBufferMethod,
            ErrorTimeoutMilliseconds,
            CancellationToken.None);
        await tcpPeer;

        using var udp = new UDPChannel(
            IPAddress.Loopback.ToString(),
            TimeoutMilliseconds);
        await udp.InitializeAsync(TimeoutMilliseconds, CancellationToken.None);
        await InvokePrivateTaskAsync(
            udp,
            PurgeReceiveBufferMethod,
            ErrorTimeoutMilliseconds,
            CancellationToken.None);
        DisposeTransportClient(udp);
        await InvokePrivateTaskAsync(
            udp,
            PurgeReceiveBufferMethod,
            ErrorTimeoutMilliseconds,
            CancellationToken.None);

        var refusedPort = ReserveUnusedTcpPort();
        using var refused = new TCPChannel(
            IPAddress.Loopback.ToString(),
            refusedPort);
        await AssertThrowsAsync<OmronPLCException>(
            () => InvokePrivateTaskAsync(
                refused,
                ReinitializeClientMethod,
                ErrorTimeoutMilliseconds,
                CancellationToken.None));
    }

    /// <summary>Verifies UDP purge classification and payload validation diagnostics.</summary>
    /// <returns>A task that represents the asynchronous test.</returns>
    [Test]
    public async Task UdpChannel_ClassifiesPurgeAndPayloadFailuresAsync()
    {
        using var udp = new UDPChannel(
            IPAddress.Loopback.ToString(),
            TimeoutMilliseconds);
        foreach (var exception in new Exception[]
        {
            new TimeoutException(),
            new ObjectDisposedException(nameof(udp)),
            new SocketException((int)SocketError.ConnectionReset),
        })
        {
            await Assert.That(InvokePrivate<bool>(udp, "IsPurgeException", exception)).IsTrue();
        }

        await Assert.That(
            InvokePrivate<bool>(
                udp,
                "IsPurgeException",
                new InvalidOperationException()))
            .IsFalse();
        await AssertPrivateThrowsAsync(
            udp,
            "ValidateReceivedData",
            new List<byte>(new byte[EmptyFinsResponseLength - 1]));
        await AssertPrivateThrowsAsync(
            udp,
            "ValidateReceivedData",
            new List<byte>(new byte[EmptyFinsResponseLength]));
    }

    /// <summary>Asserts one invalid TCP node negotiation fails deterministically.</summary>
    /// <param name="payload">Node response payload.</param>
    /// <returns>A task that represents the assertion.</returns>
    private static async Task AssertTcpNegotiationFailureAsync(byte[] payload)
    {
        var portSource = CreatePortSource();
        var peer = RunTcpNegotiationPeerAsync(portSource, payload);
        using var channel = new TCPChannel(
            IPAddress.Loopback.ToString(),
            await portSource.Task);
        await AssertThrowsAsync<OmronPLCException>(
            () => channel.InitializeAsync(
                TimeoutMilliseconds,
                CancellationToken.None));
        await peer;
    }

    /// <summary>Runs one TCP peer that returns a selected node-negotiation payload.</summary>
    /// <param name="portSource">Receives the bound loopback port.</param>
    /// <param name="payload">Negotiation response payload.</param>
    /// <returns>A task that represents the peer lifetime.</returns>
    private static async Task RunTcpNegotiationPeerAsync(
        TaskCompletionSource<int> portSource,
        byte[] payload)
    {
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        portSource.SetResult(((IPEndPoint)listener.LocalEndpoint).Port);
        using var client = await listener.AcceptTcpClientAsync();
        await using var stream = client.GetStream();
        _ = await ReadTcpFrameAsync(stream);
        await WriteSplitAsync(
            stream,
            CreateTcpFrame(NodeAddressResponseCommand, payload));
    }

    /// <summary>Runs one TCP peer that negotiates then withholds its FINS response.</summary>
    /// <param name="portSource">Receives the bound loopback port.</param>
    /// <returns>A task that represents the peer lifetime.</returns>
    private static async Task RunTcpTimeoutPeerAsync(TaskCompletionSource<int> portSource)
    {
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        portSource.SetResult(((IPEndPoint)listener.LocalEndpoint).Port);
        using var client = await listener.AcceptTcpClientAsync();
        await using var stream = client.GetStream();
        _ = await ReadTcpFrameAsync(stream);
        await WriteSplitAsync(
            stream,
            CreateTcpFrame(
                NodeAddressResponseCommand,
                CreateNodeAddressPayload()));
        _ = await ReadTcpFrameAsync(stream);
        await Task.Delay(PeerHoldMilliseconds);
    }

    /// <summary>Runs one TCP peer that negotiates and then holds the connection open.</summary>
    /// <param name="portSource">Receives the bound loopback port.</param>
    /// <returns>A task that represents the peer lifetime.</returns>
    private static async Task RunTcpNegotiationHoldPeerAsync(TaskCompletionSource<int> portSource)
    {
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        portSource.SetResult(((IPEndPoint)listener.LocalEndpoint).Port);
        using var client = await listener.AcceptTcpClientAsync();
        await using var stream = client.GetStream();
        _ = await ReadTcpFrameAsync(stream);
        await WriteSplitAsync(
            stream,
            CreateTcpFrame(
                NodeAddressResponseCommand,
                CreateNodeAddressPayload()));
        await Task.Delay(PeerHoldMilliseconds);
    }

    /// <summary>Runs one UDP peer that receives then withholds its FINS response.</summary>
    /// <param name="portSource">Receives the bound loopback port.</param>
    /// <returns>A task that represents the peer lifetime.</returns>
    private static async Task RunUdpTimeoutPeerAsync(TaskCompletionSource<int> portSource)
    {
        using var socket = new NetUdpClient(new IPEndPoint(IPAddress.Loopback, 0));
        portSource.SetResult(((IPEndPoint)socket.Client.LocalEndPoint!).Port);
        _ = await socket.ReceiveAsync(CancellationToken.None);
        await Task.Delay(PeerHoldMilliseconds);
    }

    /// <summary>Runs a TCP peer that appends surplus bytes after a valid response.</summary>
    /// <param name="portSource">Receives the bound loopback port.</param>
    /// <returns>A task that represents the peer lifetime.</returns>
    private static async Task RunTcpSurplusPeerAsync(TaskCompletionSource<int> portSource)
    {
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        portSource.SetResult(((IPEndPoint)listener.LocalEndpoint).Port);
        using var client = await listener.AcceptTcpClientAsync();
        await using var stream = client.GetStream();
        _ = await ReadTcpFrameAsync(stream);
        await WriteSplitAsync(
            stream,
            CreateTcpFrame(
                NodeAddressResponseCommand,
                CreateNodeAddressPayload()));
        var fins = await ReadTcpFrameAsync(stream);
        await WriteSplitAsync(
            stream,
            CreateTcpFrame(
                FinsFrameCommand,
                CreateFinsResponse(fins.Payload)));
        await stream.WriteAsync(
            new byte[] { LocalNode, RemoteNode, ReservedErrorCode },
            CancellationToken.None);
        await stream.FlushAsync(CancellationToken.None);
        await Task.Delay(PeerHoldMilliseconds);
    }

    /// <summary>Runs a UDP peer that appends a surplus datagram after a valid response.</summary>
    /// <param name="portSource">Receives the bound loopback port.</param>
    /// <returns>A task that represents the peer lifetime.</returns>
    private static async Task RunUdpSurplusPeerAsync(TaskCompletionSource<int> portSource)
    {
        using var socket = new NetUdpClient(new IPEndPoint(IPAddress.Loopback, 0));
        portSource.SetResult(((IPEndPoint)socket.Client.LocalEndPoint!).Port);
        var request = await socket.ReceiveAsync(CancellationToken.None);
        var response = CreateFinsResponse(request.Buffer);
        _ = await socket.SendAsync(response, response.Length, request.RemoteEndPoint);
        _ = await socket.SendAsync(
            [LocalNode, RemoteNode, ReservedErrorCode],
            ReservedErrorCode,
            request.RemoteEndPoint);
        await Task.Delay(PeerHoldMilliseconds);
    }

    /// <summary>Runs a TCP peer that negotiates two successive client connections.</summary>
    /// <param name="portSource">Receives the bound loopback port.</param>
    /// <returns>A task that represents the peer lifetime.</returns>
    private static async Task RunTcpReinitializePeerAsync(TaskCompletionSource<int> portSource)
    {
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        portSource.SetResult(((IPEndPoint)listener.LocalEndpoint).Port);
        for (var index = 0; index < RemoteNode; index++)
        {
            using var client = await listener.AcceptTcpClientAsync();
            await using var stream = client.GetStream();
            _ = await ReadTcpFrameAsync(stream);
            await WriteSplitAsync(
                stream,
                CreateTcpFrame(
                    NodeAddressResponseCommand,
                    CreateNodeAddressPayload()));
        }
    }

    /// <summary>Verifies every TCP error mapping produces a distinct exception message.</summary>
    /// <param name="channel">Channel instance.</param>
    /// <returns>A task that represents the assertion.</returns>
    private static async Task AssertTcpErrorMessagesAsync(TCPChannel channel)
    {
        var errors = new HashSet<string>(StringComparer.Ordinal);
        foreach (var errorCode in new byte[]
        {
            NodeAddressResponseCommand,
            FinsFrameCommand,
            ReservedErrorCode,
            UnsupportedCommandErrorCode,
            RequestTooLongErrorCode,
            RequestTooShortErrorCode,
            RequestFormatErrorCode,
            UnsupportedProtocolErrorCode,
            ConnectionLimitErrorCode,
            byte.MaxValue,
        })
        {
            var exception = InvokePrivate<OmronPLCException>(
                channel,
                "CreateTcpErrorException",
                errorCode);
            _ = errors.Add(exception.Message);
        }

        await Assert.That(errors.Count).IsEqualTo(TcpErrorCodeCount);
    }

    /// <summary>Runs one deterministic TCP FINS peer.</summary>
    /// <param name="portSource">Receives the bound loopback port.</param>
    /// <returns>Observed TCP command values.</returns>
    private static async Task<TcpPeerResult> RunTcpPeerAsync(
        TaskCompletionSource<int> portSource)
    {
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        portSource.SetResult(((IPEndPoint)listener.LocalEndpoint).Port);
        try
        {
            using var client = await listener.AcceptTcpClientAsync();
            await using var stream = client.GetStream();
            var negotiation = await ReadTcpFrameAsync(stream);
            var negotiationResponse = CreateTcpFrame(
                NodeAddressResponseCommand,
                CreateNodeAddressPayload());
            await WriteSplitAsync(stream, negotiationResponse);

            var fins = await ReadTcpFrameAsync(stream);
            var response = CreateTcpFrame(
                FinsFrameCommand,
                CreateFinsResponse(fins.Payload));
            await WriteSplitAsync(stream, response);
            return new(negotiation.Command, fins.Command);
        }
        finally
        {
            listener.Stop();
        }
    }

    /// <summary>Runs one deterministic UDP FINS peer.</summary>
    /// <param name="portSource">Receives the bound loopback port.</param>
    /// <returns>The received request length.</returns>
    private static async Task<int> RunUdpPeerAsync(TaskCompletionSource<int> portSource)
    {
        using var socket = new NetUdpClient(new IPEndPoint(IPAddress.Loopback, 0));
        portSource.SetResult(((IPEndPoint)socket.Client.LocalEndPoint!).Port);
        var request = await socket.ReceiveAsync(CancellationToken.None);
        var response = CreateFinsResponse(request.Buffer);
        _ = await socket.SendAsync(
            response,
            response.Length,
            request.RemoteEndPoint);
        return request.Buffer.Length;
    }

    /// <summary>Creates a continuation-safe port publication source.</summary>
    /// <returns>The source used by a loopback peer.</returns>
    private static TaskCompletionSource<int> CreatePortSource() =>
        new(TaskCreationOptions.RunContinuationsAsynchronously);

    /// <summary>Reserves and releases one loopback port for connection-refusal tests.</summary>
    /// <returns>The released TCP port.</returns>
    private static int ReserveUnusedTcpPort()
    {
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;
        listener.Stop();
        return port;
    }

    /// <summary>Reads one complete FINS TCP frame.</summary>
    /// <param name="stream">Connected TCP stream.</param>
    /// <returns>The decoded command and payload.</returns>
    private static async Task<TcpFrame> ReadTcpFrameAsync(NetworkStream stream)
    {
        var header = new byte[TcpHeaderLength];
        await ReadExactlyAsync(stream, header);
        var bodyLength = ReadBigEndianInt32(header, TcpLengthOffset);
        var payloadLength = bodyLength - TcpLengthOverhead;
        var payload = new byte[payloadLength];
        await ReadExactlyAsync(stream, payload);
        return new(header[TcpCommandOffset], payload);
    }

    /// <summary>Reads an exact buffer length from a network stream.</summary>
    /// <param name="stream">Connected network stream.</param>
    /// <param name="buffer">Destination buffer.</param>
    /// <returns>A task that represents the operation.</returns>
    private static async Task ReadExactlyAsync(NetworkStream stream, byte[] buffer)
    {
        var offset = 0;
        while (offset < buffer.Length)
        {
            var read = await stream.ReadAsync(buffer.AsMemory(offset), CancellationToken.None);
            if (read == 0)
            {
                throw new EndOfStreamException();
            }

            offset += read;
        }
    }

    /// <summary>Writes a frame in two packets to exercise incremental receive framing.</summary>
    /// <param name="stream">Connected network stream.</param>
    /// <param name="frame">Frame bytes.</param>
    /// <returns>A task that represents the operation.</returns>
    private static async Task WriteSplitAsync(NetworkStream stream, byte[] frame)
    {
        await stream.WriteAsync(frame.AsMemory(0, TcpHeaderLength), CancellationToken.None);
        await stream.FlushAsync(CancellationToken.None);
        await stream.WriteAsync(frame.AsMemory(TcpHeaderLength), CancellationToken.None);
        await stream.FlushAsync(CancellationToken.None);
    }

    /// <summary>Creates an initialized connection used only to construct FINS requests.</summary>
    /// <returns>The request connection.</returns>
    private static OmronPLCConnection CreateRequestConnection()
    {
        var channel = new CoreProtocolCoverageTests.TestChannel();
        return new(
            new OmronConnectionOptions(
                LocalNode,
                RemoteNode,
                ConnectionMethod.UDP,
                IPAddress.Loopback.ToString())
            {
                Timeout = TimeoutMilliseconds,
                Retries = 0,
            },
            channel,
            PlcType.CJ2,
            "CJ2M",
            "1.0",
            true);
    }

    /// <summary>Creates a FINS TCP envelope.</summary>
    /// <param name="command">TCP command.</param>
    /// <param name="payload">Command payload.</param>
    /// <returns>The encoded envelope.</returns>
    private static byte[] CreateTcpFrame(byte command, byte[] payload)
    {
        var frame = new byte[TcpHeaderLength + payload.Length];
        frame[0] = (byte)'F';
        frame[1] = (byte)'I';
        frame[2] = (byte)'N';
        frame[3] = (byte)'S';
        WriteBigEndianInt32(frame, TcpLengthOffset, payload.Length + TcpLengthOverhead);
        frame[TcpCommandOffset] = command;
        Array.Copy(payload, 0, frame, TcpHeaderLength, payload.Length);
        return frame;
    }

    /// <summary>Creates the TCP node-address negotiation payload.</summary>
    /// <returns>The node-address payload.</returns>
    private static byte[] CreateNodeAddressPayload() =>
        [0, 0, 0, LocalNode, 0, 0, 0, RemoteNode];

    /// <summary>Creates a successful empty FINS response from a received request.</summary>
    /// <param name="request">Binary FINS request.</param>
    /// <returns>The binary FINS response.</returns>
    private static byte[] CreateFinsResponse(byte[] request)
    {
        var response = new byte[EmptyFinsResponseLength];
        response[0] = 0xC0;
        response[FinsServiceIdOffset] = request[FinsServiceIdOffset];
        response[FinsCommandCodeOffset] = request[FinsCommandCodeOffset];
        response[FinsSubcommandCodeOffset] = request[FinsSubcommandCodeOffset];
        return response;
    }

    /// <summary>Reads a big-endian 32-bit integer.</summary>
    /// <param name="buffer">Source buffer.</param>
    /// <param name="offset">Source offset.</param>
    /// <returns>The decoded value.</returns>
    private static int ReadBigEndianInt32(byte[] buffer, int offset) =>
        (buffer[offset] << MostSignificantByteShift)
        | (buffer[offset + 1] << SecondByteShift)
        | (buffer[offset + FinsFrameCommand] << ThirdByteShift)
        | buffer[offset + ReservedErrorCode];

    /// <summary>Writes a big-endian 32-bit integer.</summary>
    /// <param name="buffer">Destination buffer.</param>
    /// <param name="offset">Destination offset.</param>
    /// <param name="value">Value to encode.</param>
    private static void WriteBigEndianInt32(byte[] buffer, int offset, int value)
    {
        buffer[offset] = (byte)(value >> MostSignificantByteShift);
        buffer[offset + 1] = (byte)(value >> SecondByteShift);
        buffer[offset + FinsFrameCommand] = (byte)(value >> ThirdByteShift);
        buffer[offset + ReservedErrorCode] = (byte)value;
    }

    /// <summary>Invokes a private instance method and returns its value.</summary>
    /// <typeparam name="T">Return type.</typeparam>
    /// <param name="target">Invocation target.</param>
    /// <param name="methodName">Method name.</param>
    /// <param name="arguments">Method arguments.</param>
    /// <returns>The private method result.</returns>
    private static T InvokePrivate<T>(
        object target,
        string methodName,
        params object?[] arguments)
    {
        var method = target
            .GetType()
            .GetMethod(
                methodName,
                BindingFlags.Instance | BindingFlags.Static | BindingFlags.NonPublic)
            ?? throw new MissingMethodException(target.GetType().FullName, methodName);
        return (T)method.Invoke(method.IsStatic ? null : target, arguments)!;
    }

    /// <summary>Invokes a private asynchronous channel method.</summary>
    /// <param name="target">Invocation target.</param>
    /// <param name="methodName">Method name.</param>
    /// <param name="arguments">Method arguments.</param>
    /// <returns>A task that represents the private operation.</returns>
    private static Task InvokePrivateTaskAsync(
        object target,
        string methodName,
        params object?[] arguments) =>
        InvokePrivate<Task>(target, methodName, arguments);

    /// <summary>Disposes a channel's private transport client while retaining the field reference.</summary>
    /// <param name="channel">TCP or UDP channel.</param>
    private static void DisposeTransportClient(object channel)
    {
        var clientField = channel
            .GetType()
            .GetField("_client", BindingFlags.Instance | BindingFlags.NonPublic)
            ?? throw new MissingFieldException(channel.GetType().FullName, "_client");
        var client = clientField.GetValue(channel) as IDisposable
            ?? throw new InvalidOperationException("The channel client was not initialized.");
        client.Dispose();
    }

    /// <summary>Verifies a private protocol validator throws an Omron PLC exception.</summary>
    /// <param name="target">Invocation target.</param>
    /// <param name="methodName">Method name.</param>
    /// <param name="arguments">Method arguments.</param>
    /// <returns>A task that represents the assertion.</returns>
    private static async Task AssertPrivateThrowsAsync(
        object target,
        string methodName,
        params object?[] arguments)
    {
        var method = target
            .GetType()
            .GetMethod(methodName, BindingFlags.Instance | BindingFlags.NonPublic)
            ?? throw new MissingMethodException(target.GetType().FullName, methodName);
        var exception = CaptureException<TargetInvocationException>(
            () => method.Invoke(target, arguments));
        await Assert.That(exception.InnerException).IsTypeOf<OmronPLCException>();
    }

    /// <summary>Captures and verifies an asynchronous exception.</summary>
    /// <typeparam name="TException">Expected exception type.</typeparam>
    /// <param name="action">Action to invoke.</param>
    /// <returns>A task that represents the assertion.</returns>
    private static async Task AssertThrowsAsync<TException>(Func<Task> action)
        where TException : Exception
    {
        try
        {
            await action().ConfigureAwait(false);
        }
        catch (TException ex)
        {
            await Assert.That(ex).IsNotNull();
            return;
        }

        throw new InvalidOperationException($"Expected {nameof(TException)}.");
    }

    /// <summary>Captures an expected synchronous exception.</summary>
    /// <typeparam name="TException">Expected exception type.</typeparam>
    /// <param name="action">Action to invoke.</param>
    /// <returns>The captured exception.</returns>
    private static TException CaptureException<TException>(Action action)
        where TException : Exception
    {
        try
        {
            action();
        }
        catch (TException ex)
        {
            return ex;
        }

        throw new InvalidOperationException($"Expected {nameof(TException)}.");
    }

    /// <summary>Stores observed TCP command values.</summary>
    /// <param name="NegotiationCommand">Node negotiation command.</param>
    /// <param name="FinsCommand">FINS frame command.</param>
    private sealed record TcpPeerResult(byte NegotiationCommand, byte FinsCommand);

    /// <summary>Stores a decoded TCP frame.</summary>
    /// <param name="Command">TCP command.</param>
    /// <param name="Payload">Command payload.</param>
    private sealed record TcpFrame(byte Command, byte[] Payload);
}
