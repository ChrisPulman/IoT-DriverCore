// Copyright (c) 2019-2026 Chris Pulman and contributors. All rights reserved.
// Chris Pulman and contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using OmronPlcRx.Core;
using OmronPlcRx.Core.Channels;
using OmronPlcRx.Core.Requests;
using OmronPlcRx.Core.Responses;
using OmronPlcRx.Core.Results;
using OmronPlcRx.Enums;
using OmronPlcRx.Tags;
using CoreTcpClient = OmronPlcRx.Core.TcpClient;
using CoreUdpClient = OmronPlcRx.Core.UdpClient;
using NetTcpListener = System.Net.Sockets.TcpListener;
using NetUdpClient = System.Net.Sockets.UdpClient;

namespace OmronPlcRx.Tests;

/// <summary>Provides shared test fixtures and assertions for core protocol coverage.</summary>
public sealed partial class CoreProtocolCoverageTests
{
    /// <summary>Gets the source node used by fixture connections.</summary>
    private const byte TestSourceNode = 1;

    /// <summary>Gets the destination node used by fixture connections.</summary>
    private const byte TestDestinationNode = 2;

    /// <summary>Gets the connection timeout used by injected connections.</summary>
    private const int ConnectionTimeoutMilliseconds = 100;

    /// <summary>Gets the number of bytes in one PLC word.</summary>
    private const int BytesPerWord = 2;

    /// <summary>Gets the serial port used by the test channel.</summary>
    private const int TestSerialPort = 9600;

    /// <summary>Gets the response emitted by the TCP echo helper.</summary>
    private static readonly byte[] TcpEchoResponse = [4, 5];

    /// <summary>Creates a connection instance without opening a channel.</summary>
    /// <returns>The connection instance.</returns>
    private static OmronPLCConnection CreateConnection() =>
        new(
            new OmronConnectionOptions(TestSourceNode, TestDestinationNode, ConnectionMethod.UDP, LoopbackHost)
            {
                Retries = 0,
            });

    /// <summary>Creates an injected PLC connection for unit tests.</summary>
    /// <param name="channel">The test channel.</param>
    /// <param name="plcType">The PLC type.</param>
    /// <param name="isInitialized">A value indicating whether the connection starts initialized.</param>
    /// <returns>The injected connection.</returns>
    private static OmronPLCConnection CreateInjectedConnection(
        TestChannel channel,
        PlcType plcType = PlcType.CJ2,
        bool isInitialized = true) =>
        new(
            new OmronConnectionOptions(TestSourceNode, TestDestinationNode, ConnectionMethod.UDP, LoopbackHost)
            {
                Timeout = ConnectionTimeoutMilliseconds,
                Retries = 0,
            },
            channel,
            plcType,
            "CJ2M",
            "1.0",
            isInitialized);

    /// <summary>Creates a TCP client for constructor validation tests.</summary>
    /// <param name="host">The remote host.</param>
    /// <param name="port">The remote port.</param>
    /// <returns>The TCP client.</returns>
    private static CoreTcpClient CreateTcpClient(string host, int port) => new(host, port);

    /// <summary>Creates a TCP client for constructor validation tests.</summary>
    /// <param name="address">The remote address.</param>
    /// <param name="port">The remote port.</param>
    /// <returns>The TCP client.</returns>
    private static CoreTcpClient CreateTcpClient(System.Net.IPAddress address, int port) => new(address, port);

    /// <summary>Creates a UDP client for constructor validation tests.</summary>
    /// <param name="host">The remote host.</param>
    /// <param name="port">The remote port.</param>
    /// <returns>The UDP client.</returns>
    private static CoreUdpClient CreateUdpClient(string host, int port) => new CoreUdpClient(host, port);

    /// <summary>Creates a UDP client for constructor validation tests.</summary>
    /// <param name="address">The remote address.</param>
    /// <param name="port">The remote port.</param>
    /// <returns>The UDP client.</returns>
    private static CoreUdpClient CreateUdpClient(System.Net.IPAddress address, int port) => new CoreUdpClient(address, port);

    /// <summary>Echoes a TCP message using the core TCP socket wrapper.</summary>
    /// <param name="listener">The TCP listener.</param>
    /// <returns>The number of bytes received from the client.</returns>
    private static async Task<int> EchoTcpAsync(NetTcpListener listener)
    {
        using var acceptedSocket = await listener.AcceptSocketAsync().ConfigureAwait(false);
        using var server = new CoreTcpClient(acceptedSocket);
        var receiveBuffer = new byte[3];
        var received = await server.ReceiveAsync(
            receiveBuffer,
            SocketTimeoutMilliseconds,
            CancellationToken.None).ConfigureAwait(false);
        _ = await server.SendAsync(
            TcpEchoResponse,
            SocketTimeoutMilliseconds,
            CancellationToken.None).ConfigureAwait(false);
        return received;
    }

    /// <summary>Echoes a UDP message using the framework UDP server.</summary>
    /// <param name="server">The UDP server.</param>
    /// <returns>The number of bytes received from the client.</returns>
    private static async Task<int> EchoUdpAsync(NetUdpClient server)
    {
        var result = await server.ReceiveAsync(CancellationToken.None).ConfigureAwait(false);
        var response = new byte[] { 7, 8 };
        _ = await server.SendAsync(response, response.Length, result.RemoteEndPoint).ConfigureAwait(false);
        return result.Buffer.Length;
    }

    /// <summary>Creates Host Link codec instances for constructor validation tests.</summary>
    /// <param name="options">The serial options.</param>
    /// <returns>The codec.</returns>
    private static HostLinkFinsFrameCodec CreateHostLinkCodec(OmronSerialOptions options) => new(options);

    /// <summary>Creates serial options for constructor validation tests.</summary>
    /// <param name="portName">The port name.</param>
    /// <returns>The serial options.</returns>
    private static OmronSerialOptions CreateSerialOptions(string portName) => new(portName);

    /// <summary>Creates a PLC tag for constructor validation tests.</summary>
    /// <typeparam name="T">The tag value type.</typeparam>
    /// <param name="tagName">The tag name.</param>
    /// <param name="address">The tag address.</param>
    /// <returns>The tag.</returns>
    private static PlcTag<T> CreateTag<T>(string tagName, string address) => new(tagName, address);

    /// <summary>Creates a successful FINS response for a request.</summary>
    /// <param name="request">The request.</param>
    /// <param name="data">The response data.</param>
    /// <returns>The parsed response.</returns>
    private static FINSResponse CreateResponse(FINSRequest request, byte[] data) =>
        FINSResponse.CreateNew(BuildResponseFrame(request, data), request);

    /// <summary>Builds a raw FINS response frame.</summary>
    /// <param name="request">The request.</param>
    /// <param name="data">The response data.</param>
    /// <param name="serviceId">The optional service id.</param>
    /// <param name="functionCode">The optional function code.</param>
    /// <param name="subFunctionCode">The optional sub-function code.</param>
    /// <param name="mainResponseCode">The main response code.</param>
    /// <param name="subResponseCode">The sub response code.</param>
    /// <returns>The raw frame.</returns>
    private static byte[] BuildResponseFrame(
        FINSRequest request,
        byte[] data,
        byte? serviceId = null,
        byte? functionCode = null,
        byte? subFunctionCode = null,
        byte mainResponseCode = 0,
        byte subResponseCode = 0)
    {
        var message = new byte[
            FINSResponse.HeaderLength + FINSResponse.CommandLength + FINSResponse.ResponseCodeLength + data.Length];
        message[9] = serviceId ?? request.ServiceID;
        message[10] = functionCode ?? request.FunctionCode;
        message[11] = subFunctionCode ?? request.SubFunctionCode;
        message[12] = mainResponseCode;
        message[13] = subResponseCode;
        Array.Copy(
            data,
            0,
            message,
            FINSResponse.HeaderLength + FINSResponse.CommandLength + FINSResponse.ResponseCodeLength,
            data.Length);
        return message;
    }

    /// <summary>Builds CPU unit data with fixed controller model and version fields.</summary>
    /// <param name="model">The controller model.</param>
    /// <param name="version">The controller version.</param>
    /// <returns>The response payload.</returns>
    private static byte[] BuildCpuUnitData(string model, string version)
    {
        var data = new byte[ReadCPUUnitDataResponse.TotalResponseLength];
        CopyAscii(model, data, 0, ReadCPUUnitDataResponse.ControllerModelLength);
        CopyAscii(
            version,
            data,
            ReadCPUUnitDataResponse.ControllerModelLength,
            ReadCPUUnitDataResponse.ControllerVersionLength);
        return data;
    }

    /// <summary>Copies ASCII text to a fixed-width field.</summary>
    /// <param name="text">The text.</param>
    /// <param name="buffer">The destination buffer.</param>
    /// <param name="offset">The destination offset.</param>
    /// <param name="length">The maximum field length.</param>
    private static void CopyAscii(string text, byte[] buffer, int offset, int length)
    {
        var bytes = System.Text.Encoding.ASCII.GetBytes(text);
        Array.Copy(bytes, 0, buffer, offset, Math.Min(bytes.Length, length));
    }

    /// <summary>Converts PLC words to big-endian protocol bytes.</summary>
    /// <param name="words">The PLC words.</param>
    /// <returns>The protocol bytes.</returns>
    private static byte[] ToBigEndianBytes(short[] words)
    {
        var bytes = new byte[words.Length * BytesPerWord];
        for (var i = 0; i < words.Length; i++)
        {
            var word = (ushort)words[i];
            bytes[i * BytesPerWord] = (byte)(word >> 8);
            bytes[(i * BytesPerWord) + 1] = (byte)(word & 0xFF);
        }

        return bytes;
    }

    /// <summary>Converts bit values to compact assertion text.</summary>
    /// <param name="values">The bit values.</param>
    /// <returns>The bit text.</returns>
    private static string ToBitText(bool[] values)
    {
        var text = new System.Text.StringBuilder(values.Length + Math.Max(0, values.Length - 1));
        for (var i = 0; i < values.Length; i++)
        {
            if (i > 0)
            {
                _ = text.Append(',');
            }

            _ = text.Append(values[i] ? '1' : '0');
        }

        return text.ToString();
    }

    /// <summary>Converts a memory value to uppercase hexadecimal.</summary>
    /// <param name="memory">The memory value.</param>
    /// <returns>The hexadecimal text.</returns>
    private static string ToHex(ReadOnlyMemory<byte> memory) => Convert.ToHexString(memory.ToArray());

    /// <summary>Captures an expected exception from an action.</summary>
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
        catch (TException ex)
        {
            return ex;
        }

        throw new InvalidOperationException($"Expected exception of type {nameof(TException)}.");
    }

    /// <summary>Captures an expected exception from an asynchronous action.</summary>
    /// <typeparam name="TException">The expected exception type.</typeparam>
    /// <param name="action">The action to invoke.</param>
    /// <returns>The captured exception.</returns>
    private static async Task<TException> CaptureExceptionAsync<TException>(Func<Task> action)
        where TException : Exception
    {
        try
        {
            await action().ConfigureAwait(false);
        }
        catch (TException ex)
        {
            return ex;
        }

        throw new InvalidOperationException($"Expected exception of type {nameof(TException)}.");
    }

    /// <summary>Asserts that an asynchronous action throws the expected exception type.</summary>
    /// <typeparam name="TException">The expected exception type.</typeparam>
    /// <param name="action">The action to invoke.</param>
    /// <returns>A task that represents the asynchronous assertion.</returns>
    private static async Task AssertThrowsAsync<TException>(Func<Task> action)
        where TException : Exception
    {
        var ex = await CaptureExceptionAsync<TException>(action).ConfigureAwait(false);
        await Assert.That(ex).IsNotNull();
    }

    /// <summary>Fake channel used to exercise base-channel orchestration without sockets.</summary>
    internal sealed class TestChannel : BaseChannel
    {
        /// <summary>Stores the last sent request message.</summary>
        private byte[] _lastSent = [];

        /// <summary>Stores the response data emitted by the fixture.</summary>
        private byte[] _responseData = [];

        /// <summary>Initializes a new instance of the <see cref="TestChannel"/> class.</summary>
        public TestChannel()
            : base("test", TestSerialPort)
        {
        }

        /// <summary>Gets or sets a value indicating whether the first send should fail.</summary>
        internal bool FailFirstSend { get; set; }

        /// <summary>Gets or sets a value indicating whether the response service id should mismatch.</summary>
        internal bool ForceServiceIdMismatch { get; set; }

        /// <summary>Gets or sets a value indicating whether purge should throw.</summary>
        internal bool ThrowDuringPurge { get; set; }

        /// <summary>Gets the initialize call count.</summary>
        internal int InitializeCount { get; private set; }

        /// <summary>Gets the destroy call count.</summary>
        internal int DestroyCount { get; private set; }

        /// <summary>Gets the send call count.</summary>
        internal int SendCount { get; private set; }

        /// <summary>Gets the receive call count.</summary>
        internal int ReceiveCount { get; private set; }

        /// <summary>Gets the purge call count.</summary>
        internal int PurgeCount { get; private set; }

        /// <summary>Gets the response data to emit.</summary>
        private byte[] ResponseData => _responseData;

        /// <summary>Copies response data for the fixture to emit.</summary>
        /// <param name="responseData">The response data to copy.</param>
        internal void SetResponseData(ReadOnlySpan<byte> responseData) => _responseData = responseData.ToArray();

        /// <inheritdoc />
        internal override Task InitializeAsync(int timeout, CancellationToken cancellationToken)
        {
            InitializeCount++;
            return Task.CompletedTask;
        }

        /// <inheritdoc />
        protected override Task DestroyAndInitializeClientAsync(
            int timeout,
            CancellationToken cancellationToken)
        {
            DestroyCount++;
            return Task.CompletedTask;
        }

        /// <inheritdoc />
        protected override Task<SendMessageResult> SendMessageAsync(
            ReadOnlyMemory<byte> message,
            int timeout,
            CancellationToken cancellationToken)
        {
            SendCount++;
            if (FailFirstSend && SendCount == 1)
            {
                throw new TimeoutException("first send failed");
            }

            _lastSent = message.ToArray();
            return Task.FromResult(new SendMessageResult
            {
                Bytes = _lastSent.Length,
                Packets = 1,
            });
        }

        /// <inheritdoc />
        protected override Task<ReceiveMessageResult> ReceiveMessageAsync(
            int timeout,
            CancellationToken cancellationToken)
        {
            ReceiveCount++;
            var response = BuildResponseFromLastSent();
            return Task.FromResult(new ReceiveMessageResult
            {
                Bytes = response.Length,
                Packets = 1,
                Message = response,
            });
        }

        /// <inheritdoc />
        protected override Task PurgeReceiveBufferAsync(
            int timeout,
            CancellationToken cancellationToken)
        {
            PurgeCount++;
            return ThrowDuringPurge ? throw new TimeoutException("purge failed") : Task.CompletedTask;
        }

        /// <summary>Builds a response frame from the last request message.</summary>
        /// <returns>The response frame.</returns>
        private byte[] BuildResponseFromLastSent()
        {
            var response = new byte[
                FINSResponse.HeaderLength
                + FINSResponse.CommandLength
                + FINSResponse.ResponseCodeLength
                + ResponseData.Length];
            response[9] = (byte)(ForceServiceIdMismatch ? _lastSent[9] + 1 : _lastSent[9]);
            response[10] = _lastSent[10];
            response[11] = _lastSent[11];
            Array.Copy(
                ResponseData,
                0,
                response,
                FINSResponse.HeaderLength + FINSResponse.CommandLength + FINSResponse.ResponseCodeLength,
                ResponseData.Length);
            return response;
        }
    }
}
