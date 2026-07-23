// Copyright (c) 2019-2026 Chris Pulman and contributors. All rights reserved.
// Chris Pulman and contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Net;
using System.Net.Sockets;
using IoT.DriverCore.OmronPlcRx.Core;
using IoT.DriverCore.OmronPlcRx.Core.Channels;
using IoT.DriverCore.OmronPlcRx.Core.Converters;
using IoT.DriverCore.OmronPlcRx.Core.Requests;
using IoT.DriverCore.OmronPlcRx.Core.Responses;
using IoT.DriverCore.OmronPlcRx.Core.Types;
using IoT.DriverCore.OmronPlcRx.Enums;
using TUnit.Core;

namespace IoT.DriverCore.OmronPlcRx.Tests;

/// <summary>Exercises remaining parser and boundary paths with deterministic protocol frames.</summary>
public sealed class OmronParserResidualCoverageTests
{
    /// <summary>Maximum two-byte BCD value.</summary>
    private const short MaximumBcd16 = 9999;

    /// <summary>Maximum four-byte BCD value.</summary>
    private const uint MaximumBcd32 = 99_999_999U;

    /// <summary>Expected direct FINS remote node.</summary>
    private const byte NegotiatedRemoteNode = 6;

    /// <summary>Expected direct FINS local node.</summary>
    private const byte NegotiatedLocalNode = 5;

    /// <summary>FINS response payload offset.</summary>
    private const int ResponsePayloadOffset = 14;

    /// <summary>TCP negotiation timeout in milliseconds.</summary>
    private const int NegotiationTimeoutMilliseconds = 1000;

    /// <summary>TCP negotiation frame payload size.</summary>
    private const int NegotiationPayloadLength = 16;

    /// <summary>TCP negotiation request size.</summary>
    private const int NegotiationRequestLength = 20;

    /// <summary>TCP negotiation frame size.</summary>
    private const int NegotiationResponseLength = 24;

    /// <summary>Clock response day-of-week value.</summary>
    private const byte ClockDayOfWeek = 7;

    /// <summary>Expected signed BCD value.</summary>
    private const short ExpectedBcd16 = 1234;

    /// <summary>Expected unsigned BCD value.</summary>
    private const ushort ExpectedBcdU16 = 9876;

    /// <summary>Verifies clock extraction accepts the supported nineteenth-century protocol year range.</summary>
    /// <returns>A task that represents the asynchronous test.</returns>
    [Test]
    public async Task ClockResponse_MapsNinetyNineToNineteenNinetyNineAsync()
    {
        using var connection = CreateUdpConnection();
        var request = ReadClockRequest.CreateNew(connection);
        _ = request.BuildMessage(1);
        var response = CreateResponse(request, [0x99, 0x12, 0x31, 0x23, 0x59, 0x58, 0x07]);

        var clock = ReadClockResponse.ExtractClock(request, response);

        await Assert.That(clock.ClockDateTime).IsEqualTo(
            new DateTime(1999, 12, 31, 23, 59, 58, DateTimeKind.Utc));
        await Assert.That(clock.DayOfWeek).IsEqualTo(ClockDayOfWeek);
    }

    /// <summary>Verifies parser value conversions preserve low bytes, unsigned words, and BCD values.</summary>
    /// <returns>A task that represents the asynchronous test.</returns>
    [Test]
    public async Task TagValueCodec_ConvertsBoundarySingleWordValuesAsync()
    {
        var byteValue = PlcTagValueCodec.ConvertReadWords(typeof(byte), [unchecked((short)0xABFF)]);
        var unsignedValue = PlcTagValueCodec.ConvertReadWords(typeof(ushort), [unchecked((short)0xFEDC)]);
        var signedBcdValue = (Bcd16)PlcTagValueCodec.ConvertReadWords(typeof(Bcd16), [0x1234]);
        var unsignedBcdValue = (BcdU16)PlcTagValueCodec.ConvertReadWords(typeof(BcdU16), [unchecked((short)0x9876)]);

        await Assert.That(byteValue).IsEqualTo((byte)0xFF);
        await Assert.That(unsignedValue).IsEqualTo((ushort)0xFEDC);
        await Assert.That(signedBcdValue.Value).IsEqualTo(ExpectedBcd16);
        await Assert.That(unsignedBcdValue.Value).IsEqualTo(ExpectedBcdU16);
    }

    /// <summary>Verifies BCD boundary values and metadata classification use the documented protocol branches.</summary>
    /// <returns>A task that represents the asynchronous test.</returns>
    [Test]
    public async Task BcdAndMetadata_HandleBoundaryProtocolValuesAsync()
    {
        await Assert.That(BCDConverter.ToUInt32([0x99, 0x99, 0x99, 0x99])).IsEqualTo(MaximumBcd32);
        await Assert.That(BCDConverter.ToInt16([0x99, 0x99])).IsEqualTo(MaximumBcd16);
        await Assert.That(OmronPLCConnectionMetadata.GetPLCType("C200H")).IsEqualTo(PlcType.C_Series);
        await Assert.That(OmronPLCConnectionMetadata.GetPLCType("NY512")).IsEqualTo(PlcType.NJ_NX_NY_Series);
        OmronPLCConnectionMetadata.ValidateNodeIdentifiers(1, 0, ConnectionMethod.Serial);
    }

    /// <summary>Verifies TCP FINS requests use the node identities negotiated by the TCP channel.</summary>
    /// <returns>A task that represents the asynchronous test.</returns>
    [Test]
    public async Task FinsRequest_UsesNegotiatedTcpNodeIdentifiersAsync()
    {
        var portSource = new TaskCompletionSource<int>(TaskCreationOptions.RunContinuationsAsynchronously);
        var peer = RunTcpNegotiationPeerAsync(portSource);
        var port = await portSource.Task.ConfigureAwait(false);
        using var channel = new TCPChannel(IPAddress.Loopback.ToString(), port);
        await channel.InitializeAsync(NegotiationTimeoutMilliseconds, CancellationToken.None).ConfigureAwait(false);
        using var connection = new OmronPLCConnection(
            new OmronConnectionOptions(1, NegotiatedRemoteNode, ConnectionMethod.TCP, IPAddress.Loopback.ToString()) { Port = port },
            channel,
            PlcType.CJ2,
            "CJ2M",
            "1.0",
            true);
        var request = ReadClockRequest.CreateNew(connection);

        var message = request.BuildMessage(0x44).ToArray();

        await Assert.That(message[4]).IsEqualTo(NegotiatedRemoteNode);
        await Assert.That(message[7]).IsEqualTo(NegotiatedLocalNode);
        await peer.ConfigureAwait(false);
    }

    /// <summary>Verifies direct Host Link and Toolbus decoders reject malformed frames after valid framing setup.</summary>
    /// <returns>A task that represents the asynchronous test.</returns>
    [Test]
    public async Task SerialFrameCodecs_ValidateDirectPayloadAndToolbusLengthAsync()
    {
        var hostLink = new HostLinkFinsFrameCodec(new OmronSerialOptions("COM1"));
        const string oddPayloadBody = "@00FA000";
        var oddPayloadFrame = $"{oddPayloadBody}{HostLinkFinsFrameCodec.CalculateFcs(oddPayloadBody)}*\r";
        var hostLinkException = CaptureException<OmronPLCException>(() => hostLink.DecodeResponse(oddPayloadFrame));
        var toolbusException = CaptureException<OmronPLCException>(
            () => ToolbusFinsFrameCodec.DecodeResponse(new byte[] { 0xAB, 0x00, 0x01, 0x00, 0xAC }));

        await Assert.That(hostLinkException.Message).Contains("payload");
        await Assert.That(toolbusException.Message).Contains("length");
    }

    /// <summary>Creates a parsed successful response for a request.</summary>
    /// <param name="request">The source request.</param>
    /// <param name="data">The response data.</param>
    /// <returns>The parsed FINS response.</returns>
    private static FINSResponse CreateResponse(FINSRequest request, byte[] data)
    {
        var message = new byte[FINSResponse.HeaderLength + FINSResponse.CommandLength + FINSResponse.ResponseCodeLength + data.Length];
        message[9] = request.ServiceID;
        message[10] = request.FunctionCode;
        message[11] = request.SubFunctionCode;
        Array.Copy(data, 0, message, ResponsePayloadOffset, data.Length);
        return FINSResponse.CreateNew(message, request);
    }

    /// <summary>Creates an in-memory UDP connection without opening a socket.</summary>
    /// <returns>The connection.</returns>
    private static OmronPLCConnection CreateUdpConnection() =>
        new(new OmronConnectionOptions(1, NegotiatedRemoteNode, ConnectionMethod.UDP, IPAddress.Loopback.ToString()));

    /// <summary>Publishes a TCP negotiation reply with deterministic local and remote node IDs.</summary>
    /// <param name="portSource">Receives the bound loopback port.</param>
    /// <returns>A task that represents the peer lifetime.</returns>
    private static async Task RunTcpNegotiationPeerAsync(TaskCompletionSource<int> portSource)
    {
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        portSource.SetResult(((IPEndPoint)listener.LocalEndpoint).Port);
        using var accepted = await listener.AcceptTcpClientAsync().ConfigureAwait(false);
        await using var stream = accepted.GetStream();
        var request = new byte[NegotiationRequestLength];
        await stream.ReadExactlyAsync(request, CancellationToken.None).ConfigureAwait(false);
        var response = new byte[NegotiationResponseLength];
        response[0] = (byte)'F';
        response[1] = (byte)'I';
        response[2] = (byte)'N';
        response[3] = (byte)'S';
        response[7] = NegotiationPayloadLength;
        response[11] = 1;
        response[19] = NegotiatedLocalNode;
        response[23] = NegotiatedRemoteNode;
        await stream.WriteAsync(response, CancellationToken.None).ConfigureAwait(false);
        await stream.FlushAsync(CancellationToken.None).ConfigureAwait(false);
    }

    /// <summary>Captures an expected synchronous exception.</summary>
    /// <typeparam name="TException">The expected exception type.</typeparam>
    /// <param name="action">The action to invoke.</param>
    /// <returns>The captured exception.</returns>
    private static TException CaptureException<TException>(Action action)
        where TException : Exception
    {
        try
        {
            action();
        }
        catch (TException exception)
        {
            return exception;
        }

        throw new InvalidOperationException($"Expected {nameof(TException)}.");
    }
}
