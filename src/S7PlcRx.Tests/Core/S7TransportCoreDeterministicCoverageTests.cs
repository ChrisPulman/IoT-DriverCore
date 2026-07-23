// Copyright (c) 2019-2026 Chris Pulman and contributors. All rights reserved.
// Chris Pulman and contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers;
using System.Buffers.Binary;
using System.Net;
using System.Net.Sockets;
using System.Reflection;
using IoT.DriverCore.S7PlcRx.Core;
using IoT.DriverCore.S7PlcRx.Enterprise;
using IoT.DriverCore.S7PlcRx.Enums;
using TUnitAssert = TUnit.Assertions.Assert;

namespace IoT.DriverCore.S7PlcRx.Tests.Core;

/// <summary>Provides deterministic coverage for the S7 connection pool, metrics, and socket transport.</summary>
[NotInParallel]
public sealed partial class S7TransportCoreDeterministicCoverageTests
{
    /// <summary>Defines the number of samples needed to roll a metrics queue.</summary>
    private const int RollingSampleCount = 101;

    /// <summary>Defines the maximum number of retained timing samples.</summary>
    private const int RetainedSampleCount = 100;

    /// <summary>Defines the multiplier applied to receive timing samples.</summary>
    private const long ReceiveTimingMultiplier = 2;

    /// <summary>Defines the expected rolling send-time average in ticks.</summary>
    private const long ExpectedAverageSendTicks = 51;

    /// <summary>Defines the expected rolling receive-time average in ticks.</summary>
    private const long ExpectedAverageReceiveTicks = 103;

    /// <summary>Defines the number of bytes recorded per send operation.</summary>
    private const int SendByteCount = 2;

    /// <summary>Defines the number of bytes recorded per receive operation.</summary>
    private const int ReceiveByteCount = 3;

    /// <summary>Defines the expected operation count after recording send, receive, and error operations.</summary>
    private const int ExpectedOperationCount = (RollingSampleCount * 2) + 1;

    /// <summary>Defines the number of connections retained by the pool.</summary>
    private const int PoolConnectionCount = 2;

    /// <summary>Defines the PDU length used by LOGO controllers.</summary>
    private const ushort LogoPduLength = 240;

    /// <summary>Defines the PDU length used by standard controllers.</summary>
    private const ushort StandardPduLength = 480;

    /// <summary>Defines the PDU length used by extended controllers.</summary>
    private const ushort ExtendedPduLength = 960;

    /// <summary>Defines the PDU length used by high-performance controllers.</summary>
    private const ushort HighPerformancePduLength = 1_440;

    /// <summary>Defines the test rack number.</summary>
    private const short RackNumber = 1;

    /// <summary>Defines the test slot number.</summary>
    private const short SlotNumber = 2;

    /// <summary>Defines the standard SZL request packet length.</summary>
    private const int SzlRequestLength = 33;

    /// <summary>Defines the first SZL response payload offset.</summary>
    private const int FirstSzlPayloadOffset = 41;

    /// <summary>Defines the continuation SZL response payload offset.</summary>
    private const int ContinuationSzlPayloadOffset = 37;

    /// <summary>Defines the SZL response sequence offset.</summary>
    private const int SzlSequenceOffset = 24;

    /// <summary>Defines the SZL response last-data-unit offset.</summary>
    private const int SzlLastDataUnitOffset = 26;

    /// <summary>Defines the SZL response return-code offset.</summary>
    private const int SzlReturnCodeOffset = 29;

    /// <summary>Defines the SZL response data-length offset.</summary>
    private const int SzlDataLengthOffset = 31;

    /// <summary>Defines the SZL total-length offset.</summary>
    private const int SzlTotalLengthOffset = 37;

    /// <summary>Defines the number of metadata bytes in a first SZL packet.</summary>
    private const int FirstSzlMetadataLength = 8;

    /// <summary>Defines the successful S7 return code.</summary>
    private const byte S7Success = 0xff;

    /// <summary>Defines a deterministic unsuccessful S7 return code.</summary>
    private const byte S7Failure = 0x05;

    /// <summary>Defines the expected complete SZL payload length.</summary>
    private const ushort CompleteSzlPayloadLength = 4;

    /// <summary>Defines the payload length reported by the first SZL response.</summary>
    private const ushort FirstSzlPayloadLength = 2;

    /// <summary>Defines the second SZL response sequence number.</summary>
    private const byte SecondSzlSequence = 2;

    /// <summary>Defines the TPKT packet-length offset.</summary>
    private const int TpktLengthOffset = 2;

    /// <summary>Defines the TPKT protocol version.</summary>
    private const byte TpktVersion = 0x03;

    /// <summary>Defines the encoded TPKT header length.</summary>
    private const byte TpktHeaderLength = 4;

    /// <summary>Defines a deliberately incomplete TPKT header length.</summary>
    private const int PartialTpktHeaderLength = 2;

    /// <summary>Defines an invalid declared TPKT packet length.</summary>
    private const byte InvalidTpktLength = 3;

    /// <summary>Defines the complete ISO header length.</summary>
    private const byte IsoHeaderLength = 7;

    /// <summary>Defines a valid test packet length that requires a three-byte payload.</summary>
    private const byte IsoPacketLengthWithPayload = 10;

    /// <summary>Defines the first byte of an S7 AckData response data section.</summary>
    private const int MultiVarResponseDataOffset = 19;

    /// <summary>Defines the response offset of the parameter-length high byte.</summary>
    private const int MultiVarParameterLengthHighOffset = 13;

    /// <summary>Defines the response offset of the parameter-length low byte.</summary>
    private const int MultiVarParameterLengthLowOffset = 14;

    /// <summary>Defines the encoded length of a multi-variable response item header.</summary>
    private const int MultiVarItemHeaderLength = 4;

    /// <summary>Defines the item-header offset of the declared bit-length low byte.</summary>
    private const int MultiVarBitLengthLowOffset = 3;

    /// <summary>Defines a two-byte declared response payload length in bits.</summary>
    private const byte MultiVarTwoBytePayloadBitLength = 16;

    /// <summary>Defines the consecutive send failures that trigger connection restart.</summary>
    private const int ConsecutiveSendFailureCount = 6;

    /// <summary>Defines the availability samples needed to enter the steady polling cadence.</summary>
    private const int AvailabilityObservationCount = 10;

    /// <summary>Defines the number of TSAP profiles attempted during connection initialization.</summary>
    private const int TsapProfileCount = 3;

    /// <summary>Defines the connection-request packet length consumed by the handshake peer.</summary>
    private const int ConnectionRequestLength = 22;

    /// <summary>Defines the private handshake receive-buffer length.</summary>
    private const int HandshakeReceiveBufferLength = 256;

    /// <summary>Defines the minimum communication-setup response length.</summary>
    private const int CommunicationSetupResponseLength = 27;

    /// <summary>Defines the negotiated PDU field offset.</summary>
    private const int NegotiatedPduLengthOffset = 25;

    /// <summary>Defines the reflected initialization-complete field name.</summary>
    private const string InitCompleteFieldName = "_initComplete";

    /// <summary>Defines the reflected connected-state field name.</summary>
    private const string IsConnectedFieldName = "_isConnected";

    /// <summary>Defines the reflected socket-exception subject field name.</summary>
    private const string SocketExceptionSubjectFieldName = "_socketExceptionSubject";

    /// <summary>Defines the reflected signal notification method name.</summary>
    private const string OnNextMethodName = "OnNext";

    /// <summary>Defines the reflected restart method name.</summary>
    private const string RestartConnectionMethodName = "RestartConnection";

    /// <summary>Defines the reflected initialization method name.</summary>
    private const string InitializeConnectionMethodName = "InitializeSiemensConnectionOptimizedAsync";

    /// <summary>Defines the reflected connection-state evaluator method name.</summary>
    private const string EvaluateConnectionStateMethodName = "EvaluateConnectionStateWithHysteresis";

    /// <summary>Defines the reflected consecutive connection-failure field name.</summary>
    private const string ConsecutiveConnectionFailuresFieldName = "_consecutiveConnectionFailures";

    /// <summary>Defines the reflected disposed-state field name.</summary>
    private const string DisposedValueFieldName = "_disposedValue";

    /// <summary>Defines the connection-failure threshold used by the transport.</summary>
    private const int ConnectionFailureThreshold = 3;

    /// <summary>Defines the timeout used to detect a stalled loopback exchange.</summary>
    private static readonly TimeSpan LoopbackTimeout = TimeSpan.FromSeconds(5);

    /// <summary>Defines enough time for one steady-state connection probe.</summary>
    private static readonly TimeSpan SteadyStateProbeDelay = TimeSpan.FromMilliseconds(1_250);

    /// <summary>Defines enough time for a scheduled restart task to begin.</summary>
    private static readonly TimeSpan RestartTaskStartDelay = TimeSpan.FromMilliseconds(100);

    /// <summary>Defines enough time for a disposed restart task to finish its delay.</summary>
    private static readonly TimeSpan RestartTaskCompletionDelay = TimeSpan.FromMilliseconds(1_100);

    /// <summary>Verifies rolling metrics, aggregate values, and snapshot isolation.</summary>
    /// <returns>A task that represents the asynchronous test.</returns>
    [Test]
    public async Task ConnectionMetricsRecordRollingValuesAndIndependentSnapshotsAsync()
    {
        var metrics = new ConnectionMetrics();

        await TUnitAssert.That(metrics.AverageSendTime).IsEqualTo(TimeSpan.Zero);
        await TUnitAssert.That(metrics.AverageReceiveTime).IsEqualTo(TimeSpan.Zero);
        await TUnitAssert.That(metrics.ErrorRate).IsEqualTo(0);

        for (var sample = 1; sample <= RollingSampleCount; sample++)
        {
            metrics.RecordSend(TimeSpan.FromTicks(sample), SendByteCount);
            metrics.RecordReceive(TimeSpan.FromTicks(sample * ReceiveTimingMultiplier), ReceiveByteCount);
        }

        metrics.RecordError();
        var snapshot = metrics.GetSnapshot();
        metrics.RecordSend(TimeSpan.Zero, SendByteCount);

        await TUnitAssert.That(snapshot.BytesSent).IsEqualTo(RollingSampleCount * SendByteCount);
        await TUnitAssert.That(snapshot.BytesReceived).IsEqualTo(RollingSampleCount * ReceiveByteCount);
        await TUnitAssert.That(snapshot.ErrorCount).IsEqualTo(1);
        await TUnitAssert.That(snapshot.OperationCount).IsEqualTo(ExpectedOperationCount);
        await TUnitAssert.That(snapshot.AverageSendTime).IsEqualTo(TimeSpan.FromTicks(ExpectedAverageSendTicks));
        await TUnitAssert.That(snapshot.AverageReceiveTime).IsEqualTo(TimeSpan.FromTicks(ExpectedAverageReceiveTicks));
        await TUnitAssert.That(snapshot.ErrorRate).IsEqualTo(1D / ExpectedOperationCount);
        await TUnitAssert.That(metrics.OperationCount).IsEqualTo(ExpectedOperationCount + 1);
    }

    /// <summary>Verifies composed connections are capped, rotated, counted, and disposed by the pool.</summary>
    /// <returns>A task that represents the asynchronous test.</returns>
    [Test]
    public async Task ConnectionPoolOwnsAndRotatesComposedConnectionsAsync()
    {
        var first = new S7PlcRxAsyncExtensionsTests.TestPlc();
        var second = new S7PlcRxAsyncExtensionsTests.TestPlc();
        var excess = new S7PlcRxAsyncExtensionsTests.TestPlc();
        var config = new ConnectionPoolConfig
        {
            MaxConnections = PoolConnectionCount,
            EnableConnectionReuse = true,
        };
        using var pool = new ConnectionPool([first, second, excess], config);

        var selectedFirst = pool.Connection;
        var selectedSecond = pool.Connection;
        var selectedThird = pool.Connection;

        await TUnitAssert.That(pool.MaxConnections).IsEqualTo(PoolConnectionCount);
        await TUnitAssert.That(pool.ActiveConnections).IsEqualTo(PoolConnectionCount);
        await TUnitAssert.That(pool.AllConnections.Count()).IsEqualTo(PoolConnectionCount);
        await TUnitAssert.That(ReferenceEquals(selectedFirst, first)).IsTrue();
        await TUnitAssert.That(ReferenceEquals(selectedSecond, second)).IsTrue();
        await TUnitAssert.That(ReferenceEquals(selectedThird, first)).IsTrue();

        pool.Dispose();
        pool.Dispose();
        await TUnitAssert.That(first.IsDisposed).IsTrue();
        await TUnitAssert.That(second.IsDisposed).IsTrue();
        await TUnitAssert.That(excess.IsDisposed).IsFalse();
        excess.Dispose();
    }

    /// <summary>Verifies the non-reuse strategy returns the first connected instance.</summary>
    /// <returns>A task that represents the asynchronous test.</returns>
    [Test]
    public async Task ConnectionPoolWithoutReuseSelectsFirstConnectedInstanceAsync()
    {
        var first = new S7PlcRxAsyncExtensionsTests.TestPlc();
        var second = new S7PlcRxAsyncExtensionsTests.TestPlc();
        var config = new ConnectionPoolConfig
        {
            MaxConnections = PoolConnectionCount,
            EnableConnectionReuse = false,
        };
        using var pool = new ConnectionPool([first, second], config);

        await TUnitAssert.That(ReferenceEquals(pool.Connection, first)).IsTrue();
        await TUnitAssert.That(ReferenceEquals(pool.Connection, first)).IsTrue();
    }

    /// <summary>Verifies configured pool construction, limits, null guards, and disconnected fallback selection.</summary>
    /// <returns>A task that represents the asynchronous test.</returns>
    [Test]
    public async Task ConnectionPoolConfiguredConstructionAndFallbackAreDeterministicAsync()
    {
        var config = new ConnectionPoolConfig
        {
            MaxConnections = 1,
            EnableConnectionReuse = false,
        };
        var connectionConfigs = new[]
        {
            new PlcConnectionConfig
            {
                PLCType = CpuType.S71500,
                IPAddress = IPAddress.Loopback.ToString(),
                Rack = RackNumber,
                Slot = SlotNumber,
            },
            new PlcConnectionConfig
            {
                PLCType = CpuType.S7300,
                IPAddress = IPAddress.Loopback.ToString(),
                Rack = RackNumber,
                Slot = SlotNumber,
            },
        };

        using var configured = new ConnectionPool(connectionConfigs, config);
        await TUnitAssert.That(configured.AllConnections.Count()).IsEqualTo(1);
        await TUnitAssert.That(configured.ActiveConnections).IsEqualTo(0);
        await TUnitAssert.That(configured.Connection).IsNotNull();
        await TUnitAssert.That(() => new ConnectionPool(null!)).Throws<ArgumentNullException>();
        await TUnitAssert.That(
            () => new ConnectionPool((IEnumerable<PlcConnectionConfig>)null!, config))
            .Throws<ArgumentNullException>();
        await TUnitAssert.That(
            () => new ConnectionPool((IEnumerable<IRxS7>)null!, config))
            .Throws<ArgumentNullException>();
        await TUnitAssert.That(
            () => new ConnectionPool(connectionConfigs, null!))
            .Throws<ArgumentNullException>();
        await TUnitAssert.That(
            () => new ConnectionPool((IRxS7[])[], null!))
            .Throws<ArgumentNullException>();
    }

    /// <summary>Verifies CPU-specific PDU sizing and disconnected transport failure behavior.</summary>
    /// <returns>A task that represents the asynchronous test.</returns>
    [Test]
    public async Task SocketTransportMapsPduSizesAndReportsDisconnectedOperationsAsync()
    {
        await VerifySocketConstructorGuardsAsync();

        var cases = new (CpuType CpuType, ushort PduLength)[]
        {
            (CpuType.Logo0BA8, LogoPduLength),
            (CpuType.S7200, StandardPduLength),
            (CpuType.S7300, StandardPduLength),
            (CpuType.S7400, ExtendedPduLength),
            (CpuType.S71200, ExtendedPduLength),
            (CpuType.S71500, HighPerformancePduLength),
            ((CpuType)int.MaxValue, StandardPduLength),
        };

        foreach (var (cpuType, expectedPduLength) in cases)
        {
            using var socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            using var transport = new S7SocketRx(
                IPAddress.Loopback.ToString(),
                cpuType,
                RackNumber,
                SlotNumber,
                socket,
                TimeProvider.System);
            var buffer = new byte[1];
            var szl = transport.GetSZLData(0);

            await TUnitAssert.That(transport.DataReadLength).IsEqualTo(expectedPduLength);
            await TUnitAssert.That(transport.Send(new Tag(), buffer, buffer.Length)).IsEqualTo(-1);
            await TUnitAssert.That(transport.Receive(new Tag(), buffer, buffer.Length)).IsEqualTo(-1);
            await TUnitAssert.That(szl.Data).IsEmpty();
            await TUnitAssert.That(szl.Size).IsEqualTo((ushort)0);
            await TUnitAssert.That(transport.Metrics).IsNotNull();
            ((IDisposable)transport).Dispose();
            var connectCompleted = false;
            using var subscription = transport.Connect.Subscribe(
                _ => { },
                _ => { },
                () => connectCompleted = true);
            await TUnitAssert.That(connectCompleted).IsTrue();
        }
    }

    /// <summary>Verifies successful send and receive operations over an injected loopback socket.</summary>
    /// <returns>A task that represents the asynchronous test.</returns>
    [Test]
    public async Task SocketTransportSendsAndReceivesOverLoopbackAsync()
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
        var tag = new Tag("Loopback", "DB1.DBB0", typeof(byte));
        byte[] request = [0x10, 0x20, 0x30];
        byte[] response = [0x40, 0x50, 0x60];
        var peerBuffer = new byte[request.Length];
        var receiveBuffer = new byte[response.Length + 1];

        var sent = transport.Send(tag, request, request.Length);
        var peerReceived = ReceiveExact(peer, peerBuffer);
        SendAll(peer, response);
        var received = transport.Receive(tag, receiveBuffer, response.Length, 1);

        await TUnitAssert.That(sent).IsEqualTo(request.Length);
        await TUnitAssert.That(peerReceived).IsEqualTo(request.Length);
        await TUnitAssert.That(peerBuffer).IsEquivalentTo(request);
        await TUnitAssert.That(received).IsEqualTo(response.Length);
        await TUnitAssert.That(receiveBuffer[1]).IsEqualTo(response[0]);
        await TUnitAssert.That(receiveBuffer[3]).IsEqualTo(response[2]);
        await TUnitAssert.That(
            transport.Send(tag, request, request.Length + 1)).IsEqualTo(-1);
        await TUnitAssert.That(
            transport.Receive(tag, receiveBuffer, receiveBuffer.Length + 1)).IsEqualTo(-1);
        await TUnitAssert.That(transport.IP).IsEqualTo(IPAddress.Loopback.ToString());
        await TUnitAssert.That(transport.Rack).IsEqualTo(RackNumber);
        await TUnitAssert.That(transport.Slot).IsEqualTo(SlotNumber);
    }

    /// <summary>Verifies multi-packet SZL data is accumulated through the real socket framing path.</summary>
    /// <returns>A task that represents the asynchronous test.</returns>
    [Test]
    public async Task SocketTransportAccumulatesMultiPacketSzlResponsesAsync()
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
        var responder = Task.Run(() => RespondToSzlRequests(peer));
        var readTask = Task.Run(() => transport.GetSZLData(0x0011));
        var completed = await Task.WhenAny(readTask, Task.Delay(LoopbackTimeout));

        await TUnitAssert.That(ReferenceEquals(completed, readTask)).IsTrue();
        var result = await readTask;
        await responder;

        await TUnitAssert.That(result.Size).IsEqualTo(CompleteSzlPayloadLength);
        await TUnitAssert.That(result.Data).IsEquivalentTo((byte[])[0x11, 0x22, 0x33, 0x44]);
    }

    /// <summary>Verifies an SZL protocol return code produces an empty result.</summary>
    /// <returns>A task that represents the asynchronous test.</returns>
    [Test]
    public async Task SocketTransportRejectsSzlProtocolErrorAsync()
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
        var responder = Task.Run(() => RespondWithSzlProtocolError(peer));
        var readTask = Task.Run(() => transport.GetSZLData(0x0011));
        var completed = await Task.WhenAny(readTask, Task.Delay(LoopbackTimeout));

        await TUnitAssert.That(ReferenceEquals(completed, readTask)).IsTrue();
        var result = await readTask;
        await responder;

        await TUnitAssert.That(result.Data).IsEmpty();
        await TUnitAssert.That(result.Size).IsEqualTo((ushort)0);
    }

    /// <summary>Verifies malformed and truncated ISO frames are rejected without escaping transport errors.</summary>
    /// <returns>A task that represents the asynchronous test.</returns>
    [Test]
    public async Task SocketTransportRejectsMalformedIsoFramesAsync()
    {
        await TUnitAssert.That(
            await ReceiveMalformedIsoFrameAsync(
                [TpktVersion, 0, 0, IsoHeaderLength - 1])).IsEqualTo(0);
        await TUnitAssert.That(
            await ReceiveMalformedIsoFrameAsync(
                [TpktVersion, 0, 0, IsoHeaderLength])).IsEqualTo(0);
        await TUnitAssert.That(
            await ReceiveMalformedIsoFrameAsync(
                [TpktVersion, 0, 0, IsoPacketLengthWithPayload, 0, 0])).IsEqualTo(0);
        await TUnitAssert.That(
            await ReceiveMalformedIsoFrameAsync(
                [TpktVersion, 0, 0, IsoPacketLengthWithPayload, 0, 0, 0])).IsEqualTo(0);
    }

    /// <summary>Verifies multi-variable codecs reject invalid inputs and truncated result sections.</summary>
    /// <returns>A task that represents the asynchronous assertions.</returns>
    [Test]
    public async Task MultiVarCodecRejectsInvalidAndTruncatedFramesAsync()
    {
        const string tagName = "Codec";
        var timer = new S7MultiVar.ReadItem(DataType.Timer, 0, 0, 1, tagName);
        var counter = new S7MultiVar.ReadItem(DataType.Counter, 0, 1, 1, tagName);
        var pool = ArrayPool<byte>.Shared;

        await TUnitAssert.That(() => S7MultiVar.BuildReadVarRequest(null!)).Throws<ArgumentNullException>();
        await TUnitAssert.That(S7MultiVar.BuildReadVarRequest([])).IsEmpty();
        await TUnitAssert.That(S7MultiVar.BuildReadVarRequest([timer, counter])).IsNotEmpty();
        await TUnitAssert.That(
            () => S7MultiVar.ParseReadVarResponse([], null!, pool)).Throws<ArgumentNullException>();
        await TUnitAssert.That(
            () => S7MultiVar.ParseReadVarResponse([], [timer], null!)).Throws<ArgumentNullException>();
        await TUnitAssert.That(
            S7MultiVar.ParseReadVarResponse(CreateResponseWithDataOutsideFrame(), [timer], pool)).IsEmpty();
        await TUnitAssert.That(
            S7MultiVar.ParseReadVarResponse(
                new byte[MultiVarResponseDataOffset + MultiVarBitLengthLowOffset],
                [timer],
                pool)).IsEmpty();
        await TUnitAssert.That(
            S7MultiVar.ParseReadVarResponse(CreateResponseWithTruncatedItemData(), [timer], pool)).IsEmpty();
        await TUnitAssert.That(() => S7MultiVar.BuildWriteVarRequest(null!)).Throws<ArgumentNullException>();
        await TUnitAssert.That(S7MultiVar.BuildWriteVarRequest([])).IsEmpty();
        await TUnitAssert.That(S7MultiVar.ParseWriteVarResponse([], 0)).IsEmpty();
        await TUnitAssert.That(
            S7MultiVar.ParseWriteVarResponse(new byte[MultiVarResponseDataOffset], 1)).IsEmpty();
    }

    /// <summary>Verifies the convenience constructor and blank endpoint availability backoff.</summary>
    /// <returns>A task that represents the asynchronous assertions.</returns>
    [Test]
    public async Task SocketAvailabilityRejectsBlankEndpointAndBacksOffAsync()
    {
        await TUnitAssert.That(
            () => new S7SocketRx(null!, CpuType.S71500, RackNumber, SlotNumber))
            .Throws<ArgumentNullException>();
        using var automatic = new S7SocketRx(" ", CpuType.S71500, RackNumber, SlotNumber);
        await TUnitAssert.That(automatic.DataReadLength).IsEqualTo(HighPerformancePduLength);
        using var socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
        using var transport = new S7SocketRx(
            " ",
            CpuType.S71500,
            RackNumber,
            SlotNumber,
            socket,
            TimeProvider.System);
        var valuesObserved = 0;
        var completion = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        using var subscription = transport.IsAvailable.Take(AvailabilityObservationCount).Subscribe(
            value =>
            {
                valuesObserved++;
                if (valuesObserved != AvailabilityObservationCount)
                {
                    return;
                }

                _ = completion.TrySetResult(value);
            },
            exception => _ = completion.TrySetException(exception));

        var completed = await Task.WhenAny(completion.Task, Task.Delay(LoopbackTimeout));

        await TUnitAssert.That(ReferenceEquals(completed, completion.Task)).IsTrue();
        await TUnitAssert.That(await completion.Task).IsFalse();
        await TUnitAssert.That(valuesObserved).IsEqualTo(AvailabilityObservationCount);
    }

    /// <summary>Verifies connected socket monitoring reaches its steady-state probe.</summary>
    /// <returns>A task that represents the asynchronous assertions.</returns>
    [Test]
    public async Task SocketConnectionMonitoringReachesSteadyStateAsync()
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
        var connected = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        using var subscription = transport.IsConnected.Subscribe(
            value =>
            {
                if (!value)
                {
                    return;
                }

                _ = connected.TrySetResult(true);
            },
            exception => _ = connected.TrySetException(exception));

        var firstProbe = await Task.WhenAny(connected.Task, Task.Delay(LoopbackTimeout));
        await TUnitAssert.That(ReferenceEquals(firstProbe, connected.Task)).IsTrue();
        await Task.Delay(SteadyStateProbeDelay);
        await TUnitAssert.That(transportSocket.Connected).IsTrue();
    }

    /// <summary>Verifies repeated disconnected sends schedule one restart and finish after disposal.</summary>
    /// <returns>A task that represents the asynchronous assertions.</returns>
    [Test]
    public async Task SocketTransportSchedulesRestartAfterConsecutiveErrorsAsync()
    {
        using var socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
        using var transport = new S7SocketRx(
            IPAddress.Loopback.ToString(),
            CpuType.S71500,
            RackNumber,
            SlotNumber,
            socket,
            TimeProvider.System);
        byte[] buffer = [byte.MaxValue];

        for (var failure = 0; failure < ConsecutiveSendFailureCount; failure++)
        {
            await TUnitAssert.That(transport.Send(new Tag(), buffer, buffer.Length)).IsEqualTo(-1);
        }

        await Task.Delay(RestartTaskStartDelay);
        ((IDisposable)transport).Dispose();
        await Task.Delay(RestartTaskCompletionDelay);
    }

    /// <summary>Verifies transport lifecycle guards, restart serialization, and defensive reporting paths.</summary>
    /// <returns>A task that represents the asynchronous assertions.</returns>
    [Test]
    public async Task SocketLifecycleFallbacksAndRestartGuardsAreDeterministicAsync()
    {
        var socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
        var transport = new S7SocketRx(
            IPAddress.Loopback.ToString(),
            CpuType.S71500,
            RackNumber,
            SlotNumber,
            socket,
            TimeProvider.System);
        SetPrivateField(transport, InitCompleteFieldName, false);
        await TUnitAssert.That(transport.Receive(new Tag(), new byte[1], 1)).IsEqualTo(-1);
        SetPrivateField(transport, InitCompleteFieldName, true);
        InvokePrivate(transport, "ReportMetrics", []);
        GetPrivateField<IDisposable>(transport, "_metricsSubject").Dispose();
        InvokePrivate(transport, "ReportMetrics", []);
        socket.Dispose();
        await TUnitAssert.That(
            InvokePrivate<bool>(transport, "PerformLightweightConnectionCheck", [])).IsFalse();
        SetPrivateField<Socket?>(transport, "_socket", null);
        await TUnitAssert.That(
            InvokePrivate<bool>(transport, "CheckConnectionStatusOptimized", [])).IsFalse();
        SetPrivateField(transport, "_disposable", CreateThrowingDisposable());
        ((IDisposable)transport).Dispose();
        InvokePrivate(transport, RestartConnectionMethodName, []);
        await Task.Delay(RestartTaskStartDelay);

        using var restartSocket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
        using var restartTransport = new S7SocketRx(
            " ",
            CpuType.S71500,
            RackNumber,
            SlotNumber,
            restartSocket,
            TimeProvider.System);
        InvokePrivate(restartTransport, RestartConnectionMethodName, []);
        InvokePrivate(restartTransport, RestartConnectionMethodName, []);
        await Task.Delay(RestartTaskCompletionDelay);

        using var throwingSocket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
        using var throwingTransport = new S7SocketRx(
            " ",
            CpuType.S71500,
            RackNumber,
            SlotNumber,
            throwingSocket,
            TimeProvider.System);
        SetPrivateField(throwingTransport, "_disposable", CreateThrowingDisposable());
        InvokePrivate(throwingTransport, RestartConnectionMethodName, []);
        await Task.Delay(RestartTaskCompletionDelay);
    }

    /// <summary>Verifies connection subject recovery and disconnected-state hysteresis.</summary>
    /// <returns>A task that represents the asynchronous assertions.</returns>
    [Test]
    public async Task SocketConnectionSubjectsRecoverAndApplyHysteresisAsync()
    {
        VerifyConnectionSubjectRecovery();
        await VerifyConnectionHysteresisAsync();
    }

    /// <summary>Verifies normal and recreated socket-exception subjects route notifications.</summary>
    private static void VerifyConnectionSubjectRecovery()
    {
        using var primarySocket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
        using var primary = new S7SocketRx(
            IPAddress.Broadcast.ToString(),
            CpuType.S71500,
            RackNumber,
            SlotNumber,
            primarySocket,
            TimeProvider.System);
        using (primary.Connect.Subscribe(_ => { }, _ => { }))
        {
            var subject = GetPrivateField<object>(primary, SocketExceptionSubjectFieldName);
            InvokeObjectMethod(subject, OnNextMethodName, null);
            InvokeObjectMethod(subject, OnNextMethodName, new S7Exception("scripted"));
        }

        using var recoveredSocket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
        using var recovered = new S7SocketRx(
            IPAddress.Broadcast.ToString(),
            CpuType.S71500,
            RackNumber,
            SlotNumber,
            recoveredSocket,
            TimeProvider.System);
        GetPrivateField<IDisposable>(recovered, SocketExceptionSubjectFieldName).Dispose();
        using (recovered.Connect.Subscribe(_ => { }, _ => { }))
        {
            var replacement = GetPrivateField<object>(recovered, SocketExceptionSubjectFieldName);
            InvokeObjectMethod(replacement, OnNextMethodName, null);
            InvokeObjectMethod(replacement, OnNextMethodName, new S7Exception("recovered"));
        }
    }

    /// <summary>Verifies connection hysteresis tolerates two failures before retrying.</summary>
    /// <returns>A task that represents the asynchronous assertions.</returns>
    private static async Task VerifyConnectionHysteresisAsync()
    {
        using var hysteresisSocket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
        using var hysteresis = new S7SocketRx(
            IPAddress.Broadcast.ToString(),
            CpuType.S71500,
            RackNumber,
            SlotNumber,
            hysteresisSocket,
            TimeProvider.System);

        SetPrivateField(hysteresis, InitCompleteFieldName, false);
        await TUnitAssert.That(
            InvokePrivate<bool>(hysteresis, EvaluateConnectionStateMethodName, [])).IsFalse();
        await TUnitAssert.That(
            GetPrivateField<int>(hysteresis, ConsecutiveConnectionFailuresFieldName)).IsEqualTo(0);

        SetPrivateField(hysteresis, InitCompleteFieldName, true);
        SetPrivateField<bool?>(hysteresis, IsConnectedFieldName, true);
        await TUnitAssert.That(
            InvokePrivate<bool>(hysteresis, EvaluateConnectionStateMethodName, [])).IsTrue();
        await TUnitAssert.That(
            InvokePrivate<bool>(hysteresis, EvaluateConnectionStateMethodName, [])).IsTrue();

        SetPrivateField(hysteresis, DisposedValueFieldName, true);
        await TUnitAssert.That(
            InvokePrivate<bool>(hysteresis, EvaluateConnectionStateMethodName, [])).IsFalse();
        await TUnitAssert.That(
            GetPrivateField<int>(hysteresis, ConsecutiveConnectionFailuresFieldName)).IsEqualTo(ConnectionFailureThreshold);
    }

    /// <summary>Verifies availability and connection failure hysteresis.</summary>
    /// <returns>A task that represents the asynchronous assertions.</returns>
    private static async Task VerifyAvailabilityFailureHysteresisAsync()
    {
        using var socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
        using var transport = new S7SocketRx(
            " ",
            CpuType.S71500,
            RackNumber,
            SlotNumber,
            socket,
            TimeProvider.System);
        SetPrivateField(transport, InitCompleteFieldName, true);
        SetPrivateField<bool?>(transport, IsConnectedFieldName, true);
        var observations = new RecordingObserver<bool>();
        for (var probe = 0; probe < TsapProfileCount; probe++)
        {
            await InvokePrivateTaskAsync(
                transport,
                "ProbeAvailabilityAndNotifyAsync",
                [typeof(IObserver<bool>)],
                observations);
        }

        await TUnitAssert.That(observations.Values).IsEquivalentTo((bool[])[true, true, false]);
        SetPrivateField(transport, ConsecutiveConnectionFailuresFieldName, 0);
        SetPrivateField(transport, InitCompleteFieldName, true);
        SetPrivateField<bool?>(transport, IsConnectedFieldName, true);
        await TUnitAssert.That(
            InvokePrivate<bool>(transport, EvaluateConnectionStateMethodName, [])).IsTrue();
    }

    /// <summary>Verifies availability observer failure and negotiated-PDU guards.</summary>
    /// <returns>A task that represents the asynchronous assertions.</returns>
    private static async Task VerifyAvailabilityObserverAndNegotiationAsync()
    {
        using var socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
        using var transport = new S7SocketRx(
            " ",
            CpuType.S71500,
            RackNumber,
            SlotNumber,
            socket,
            TimeProvider.System);
        SetPrivateField<bool?>(transport, "_isAvailable", true);
        SetPrivateField(transport, InitCompleteFieldName, true);
        SetPrivateField<bool?>(transport, IsConnectedFieldName, false);
        var observer = new RecordingObserver<bool>();
        await InvokePrivateTaskAsync(
            transport,
            "ObserveAvailabilityAsync",
            [typeof(IObserver<bool>)],
            observer);
        await TUnitAssert.That(observer.Errors.Count).IsEqualTo(1);

        var originalPduLength = transport.DataReadLength;
        InvokePrivate(
            transport,
            "UpdateNegotiatedPduLength",
            [typeof(byte[]), typeof(int)],
            new byte[CommunicationSetupResponseLength - 1],
            CommunicationSetupResponseLength - 1);
        var invalidPdu = new byte[CommunicationSetupResponseLength];
        BinaryPrimitives.WriteUInt16BigEndian(
            invalidPdu.AsSpan(NegotiatedPduLengthOffset),
            ushort.MaxValue);
        InvokePrivate(
            transport,
            "UpdateNegotiatedPduLength",
            [typeof(byte[]), typeof(int)],
            invalidPdu,
            invalidPdu.Length);
        await TUnitAssert.That(transport.DataReadLength).IsEqualTo(originalPduLength);
    }

    /// <summary>Verifies successful and refused port probes.</summary>
    /// <returns>A task that represents the asynchronous assertions.</returns>
    private static async Task VerifyPortProbingAsync()
    {
        var listener = new TcpListener(IPAddress.Loopback, 0);
        using var listenerLifetime = NetworkCompatibility.StopOnDispose(listener);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;
        using var socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
        using var transport = new S7SocketRx(
            IPAddress.Loopback.ToString(),
            CpuType.S71500,
            RackNumber,
            SlotNumber,
            socket,
            TimeProvider.System,
            port);
        var acceptTask = Task.Run(() => listener.AcceptSocket());
        await TUnitAssert.That(
            await InvokePrivateTaskAsync<bool>(transport, "CheckPortAvailabilityAsync", [])).IsTrue();
        using var accepted = await acceptTask;
        listener.Stop();
        await TUnitAssert.That(
            await InvokePrivateTaskAsync<bool>(transport, "CheckPortAvailabilityAsync", [])).IsFalse();
    }

    /// <summary>Verifies initialization tries and rejects every TSAP profile.</summary>
    /// <returns>A task that represents the asynchronous assertions.</returns>
    private static async Task VerifyAllTsapProfilesAreRejectedAsync()
    {
        var listener = new TcpListener(IPAddress.Loopback, 0);
        using var listenerLifetime = NetworkCompatibility.StopOnDispose(listener);
        listener.Start();
        var endpoint = (IPEndPoint)listener.LocalEndpoint;
        using var socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
        using var transport = new S7SocketRx(
            IPAddress.Loopback.ToString(),
            CpuType.S71500,
            RackNumber,
            SlotNumber,
            socket,
            TimeProvider.System,
            endpoint.Port);
        var responder = Task.Run(() => RejectHandshakeProfiles(listener));

        var initialized = await InvokePrivateTaskAsync<bool>(
            transport,
            InitializeConnectionMethodName,
            []);

        await TUnitAssert.That(initialized).IsFalse();
        await responder;
        listener.Stop();
    }

    /// <summary>Verifies incomplete, invalid, and header-only TPKT frames.</summary>
    /// <returns>A task that represents the asynchronous assertions.</returns>
    private static async Task VerifyMalformedTpktFramesAsync()
    {
        await TUnitAssert.That(
            await ReceivePrivateTpktAsync(
                [TpktVersion, 0],
                ConnectionRequestLength)).IsEqualTo(PartialTpktHeaderLength);
        await TUnitAssert.That(
            await ReceivePrivateTpktAsync(
                [TpktVersion, 0, 0, InvalidTpktLength],
                ConnectionRequestLength)).IsEqualTo(0);
        await TUnitAssert.That(
            await ReceivePrivateTpktAsync(
                [TpktVersion, 0, 0, TpktHeaderLength],
                ConnectionRequestLength)).IsEqualTo(TpktHeaderLength);
    }

    /// <summary>Verifies already-initialized, disposed, and failed-handshake initialization guards.</summary>
    /// <returns>A task that represents the asynchronous assertions.</returns>
    private static async Task VerifyConnectionInitializationGuardsAsync()
    {
        var (connectedSocket, peerSocket) = await CreateConnectedSocketPairAsync();
        using var peer = peerSocket;
        using var initializedTransport = new S7SocketRx(
            IPAddress.Loopback.ToString(),
            CpuType.S71500,
            RackNumber,
            SlotNumber,
            connectedSocket,
            TimeProvider.System);
        await TUnitAssert.That(
            await InvokePrivateTaskAsync<bool>(
                initializedTransport,
                InitializeConnectionMethodName,
                [])).IsTrue();

        using var disposedSocket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
        using var disposedTransport = new S7SocketRx(
            IPAddress.Loopback.ToString(),
            CpuType.S71500,
            RackNumber,
            SlotNumber,
            disposedSocket,
            TimeProvider.System);
        SetPrivateField(disposedTransport, DisposedValueFieldName, true);
        await TUnitAssert.That(
            await InvokePrivateTaskAsync<bool>(
                disposedTransport,
                InitializeConnectionMethodName,
                [])).IsFalse();
        SetPrivateField(disposedTransport, DisposedValueFieldName, false);

        disposedSocket.Dispose();
        var profile = GetPrivateTsapProfile("PG");
#if NETFRAMEWORK
        const string handshakeMethodName = "PerformOptimizedHandshakeNetStandardAsync";
#else
        const string handshakeMethodName = "PerformOptimizedHandshakeModernAsync";
#endif
        await TUnitAssert.That(
            await InvokePrivateTaskAsync<bool>(
                disposedTransport,
                handshakeMethodName,
                [typeof(Socket), typeof(byte[]), profile.GetType()],
                disposedSocket,
                new byte[HandshakeReceiveBufferLength],
                profile)).IsFalse();
    }

    /// <summary>Invokes a private transport method.</summary>
    /// <param name="transport">The transport instance.</param>
    /// <param name="methodName">The private method name.</param>
    /// <param name="parameterTypes">The method parameter types.</param>
    /// <param name="arguments">The invocation arguments.</param>
    private static void InvokePrivate(
        S7SocketRx transport,
        string methodName,
        Type[] parameterTypes,
        params object?[] arguments) =>
        _ = GetPrivateMethod(methodName, parameterTypes).Invoke(transport, arguments);

    /// <summary>Invokes a private transport method and returns its value.</summary>
    /// <typeparam name="T">The return type.</typeparam>
    /// <param name="transport">The transport instance.</param>
    /// <param name="methodName">The private method name.</param>
    /// <param name="parameterTypes">The method parameter types.</param>
    /// <param name="arguments">The invocation arguments.</param>
    /// <returns>The returned value.</returns>
    private static T InvokePrivate<T>(
        S7SocketRx transport,
        string methodName,
        Type[] parameterTypes,
        params object?[] arguments) =>
        (T)(GetPrivateMethod(methodName, parameterTypes).Invoke(transport, arguments) ??
            throw new InvalidOperationException($"{methodName} returned null."));

    /// <summary>Invokes a private asynchronous transport method.</summary>
    /// <param name="transport">The transport instance.</param>
    /// <param name="methodName">The private method name.</param>
    /// <param name="parameterTypes">The method parameter types.</param>
    /// <param name="arguments">The invocation arguments.</param>
    /// <returns>The invoked task.</returns>
    private static Task InvokePrivateTaskAsync(
        S7SocketRx transport,
        string methodName,
        Type[] parameterTypes,
        params object?[] arguments) =>
        (Task)(GetPrivateMethod(methodName, parameterTypes).Invoke(transport, arguments) ??
            throw new InvalidOperationException($"{methodName} returned null."));

    /// <summary>Invokes a private asynchronous transport method and returns its result.</summary>
    /// <typeparam name="T">The task result type.</typeparam>
    /// <param name="transport">The transport instance.</param>
    /// <param name="methodName">The private method name.</param>
    /// <param name="parameterTypes">The method parameter types.</param>
    /// <param name="arguments">The invocation arguments.</param>
    /// <returns>The invoked task.</returns>
    private static Task<T> InvokePrivateTaskAsync<T>(
        S7SocketRx transport,
        string methodName,
        Type[] parameterTypes,
        params object?[] arguments) =>
        (Task<T>)(GetPrivateMethod(methodName, parameterTypes).Invoke(transport, arguments) ??
            throw new InvalidOperationException($"{methodName} returned null."));

    /// <summary>Gets an exact private transport method.</summary>
    /// <param name="methodName">The method name.</param>
    /// <param name="parameterTypes">The method parameter types.</param>
    /// <returns>The reflected method.</returns>
    private static MethodInfo GetPrivateMethod(string methodName, Type[] parameterTypes) =>
        typeof(S7SocketRx).GetMethod(
            methodName,
            BindingFlags.Instance | BindingFlags.NonPublic,
            null,
            parameterTypes,
            null) ??
        throw new InvalidOperationException($"{methodName} was not found.");

    /// <summary>Gets a private field from a transport.</summary>
    /// <typeparam name="T">The field value type.</typeparam>
    /// <param name="transport">The transport instance.</param>
    /// <param name="fieldName">The private field name.</param>
    /// <returns>The field value.</returns>
    private static T GetPrivateField<T>(S7SocketRx transport, string fieldName) =>
        (T)(typeof(S7SocketRx).GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic)?.GetValue(transport) ??
            throw new InvalidOperationException($"{fieldName} was not found."));

    /// <summary>Sets a private transport field.</summary>
    /// <typeparam name="T">The field value type.</typeparam>
    /// <param name="transport">The transport instance.</param>
    /// <param name="fieldName">The private field name.</param>
    /// <param name="value">The value to assign.</param>
    private static void SetPrivateField<T>(S7SocketRx transport, string fieldName, T value)
    {
        var field = typeof(S7SocketRx).GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic) ??
            throw new InvalidOperationException($"{fieldName} was not found.");
        field.SetValue(transport, value);
    }

    /// <summary>Invokes a single-argument method on an opaque reflected object.</summary>
    /// <param name="instance">The target object.</param>
    /// <param name="methodName">The method name.</param>
    /// <param name="argument">The method argument.</param>
    private static void InvokeObjectMethod(object instance, string methodName, object? argument)
    {
        var method = instance.GetType().GetMethod(
            methodName,
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic) ??
            throw new InvalidOperationException($"{methodName} was not found.");
        _ = method.Invoke(instance, [argument]);
    }

    /// <summary>Gets one private TSAP profile.</summary>
    /// <param name="profileName">The profile property name.</param>
    /// <returns>The boxed profile.</returns>
    private static object GetPrivateTsapProfile(string profileName)
    {
        var profileType = typeof(S7SocketRx).GetNestedType("TsapProfile", BindingFlags.NonPublic) ??
            throw new InvalidOperationException("TsapProfile was not found.");
        return profileType.GetProperty(profileName, BindingFlags.Public | BindingFlags.Static)?.GetValue(null) ??
            throw new InvalidOperationException($"{profileName} was not found.");
    }

    /// <summary>Sends one frame and invokes the private modern TPKT receiver.</summary>
    /// <param name="frame">The frame bytes.</param>
    /// <param name="expectedMinimum">The expected minimum packet length.</param>
    /// <returns>The private receive result.</returns>
    private static async Task<int> ReceivePrivateTpktAsync(byte[] frame, int expectedMinimum)
    {
#if NETFRAMEWORK
        const string receiveMethodName = "ReceiveTpktExactNetStandardAsync";
#else
        const string receiveMethodName = "ReceiveTpktExactModernAsync";
#endif
        var (transportSocket, peerSocket) = await CreateConnectedSocketPairAsync();
        using var peer = peerSocket;
        using var transport = new S7SocketRx(
            IPAddress.Loopback.ToString(),
            CpuType.S71500,
            RackNumber,
            SlotNumber,
            transportSocket,
            TimeProvider.System);
        SendAll(peer, frame);
        peer.Shutdown(SocketShutdown.Send);
        return await InvokePrivateTaskAsync<int>(
            transport,
            receiveMethodName,
            [typeof(Socket), typeof(byte[]), typeof(int)],
            transportSocket,
            new byte[HandshakeReceiveBufferLength],
            expectedMinimum);
    }

    /// <summary>Rejects one connection handshake for every supported TSAP profile.</summary>
    /// <param name="listener">The loopback listener.</param>
    private static void RejectHandshakeProfiles(TcpListener listener)
    {
        for (var profile = 0; profile < TsapProfileCount; profile++)
        {
            using var peer = listener.AcceptSocket();
            _ = ReceiveExact(peer, new byte[ConnectionRequestLength]);
            SendAll(peer, [TpktVersion, 0, 0, TpktHeaderLength]);
        }
    }

    /// <summary>Creates a disposable that throws from its analyzer-owned callback.</summary>
    /// <returns>The throwing disposable.</returns>
    private static IDisposable CreateThrowingDisposable() =>
        ReactiveUI.Primitives.Disposables.Scope.Create(
            static () => throw new InvalidOperationException("Scripted disposal failure."));

    /// <summary>Creates a response whose parameter section begins outside the supplied frame.</summary>
    /// <returns>The malformed response.</returns>
    private static byte[] CreateResponseWithDataOutsideFrame()
    {
        var response = new byte[MultiVarResponseDataOffset];
        response[MultiVarParameterLengthHighOffset] = byte.MaxValue;
        response[MultiVarParameterLengthLowOffset] = byte.MaxValue;
        return response;
    }

    /// <summary>Creates a result that declares two payload bytes but supplies only one.</summary>
    /// <returns>The truncated response.</returns>
    private static byte[] CreateResponseWithTruncatedItemData()
    {
        var response = new byte[MultiVarResponseDataOffset + MultiVarItemHeaderLength + 1];
        response[MultiVarResponseDataOffset] = S7Success;
        response[MultiVarResponseDataOffset + MultiVarBitLengthLowOffset] = MultiVarTwoBytePayloadBitLength;
        return response;
    }

    /// <summary>Verifies the injected-socket constructor rejects null dependencies.</summary>
    /// <returns>A task that represents the asynchronous assertions.</returns>
    private static async Task VerifySocketConstructorGuardsAsync()
    {
        using var nullIpSocket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
        using var nullTimeProviderSocket = new Socket(
            AddressFamily.InterNetwork,
            SocketType.Stream,
            ProtocolType.Tcp);
        await TUnitAssert.That(
            () => new S7SocketRx(
                null!,
                CpuType.S71500,
                RackNumber,
                SlotNumber,
                nullIpSocket,
                TimeProvider.System))
            .Throws<ArgumentNullException>();
        await TUnitAssert.That(
            () => new S7SocketRx(
                IPAddress.Loopback.ToString(),
                CpuType.S71500,
                RackNumber,
                SlotNumber,
                null!,
                TimeProvider.System))
            .Throws<ArgumentNullException>();
        await TUnitAssert.That(
            () => new S7SocketRx(
                IPAddress.Loopback.ToString(),
                CpuType.S71500,
                RackNumber,
                SlotNumber,
                nullTimeProviderSocket,
                null!))
            .Throws<ArgumentNullException>();
    }

    /// <summary>Sends one malformed frame over loopback and returns the ISO receive result.</summary>
    /// <param name="frame">The malformed frame.</param>
    /// <returns>The ISO receive result.</returns>
    private static async Task<int> ReceiveMalformedIsoFrameAsync(byte[] frame)
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
        SendAll(peer, frame);
        peer.Shutdown(SocketShutdown.Send);
        var buffer = new byte[StandardPduLength];
        return transport.ReceiveIsoData(new Tag(), ref buffer);
    }

    /// <summary>Creates a connected IPv4 loopback socket pair.</summary>
    /// <returns>The transport and peer sockets.</returns>
    private static async Task<(Socket Transport, Socket Peer)> CreateConnectedSocketPairAsync()
    {
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        try
        {
            var endpoint = (IPEndPoint)listener.LocalEndpoint;
            var acceptTask = Task.Run(() => listener.AcceptSocket());
            var transport = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            transport.Connect(endpoint);
            return (transport, await acceptTask);
        }
        finally
        {
            listener.Stop();
        }
    }

    /// <summary>Reads exactly one complete buffer from a socket.</summary>
    /// <param name="socket">The source socket.</param>
    /// <param name="buffer">The destination buffer.</param>
    /// <returns>The number of bytes received.</returns>
    private static int ReceiveExact(Socket socket, byte[] buffer)
    {
        var total = 0;
        while (total < buffer.Length)
        {
            var received = socket.Receive(buffer, total, buffer.Length - total, SocketFlags.None);
            if (received <= 0)
            {
                break;
            }

            total += received;
        }

        return total;
    }

    /// <summary>Sends a complete buffer to a socket.</summary>
    /// <param name="socket">The destination socket.</param>
    /// <param name="buffer">The source buffer.</param>
    private static void SendAll(Socket socket, byte[] buffer)
    {
        var total = 0;
        while (total < buffer.Length)
        {
            total += socket.Send(buffer, total, buffer.Length - total, SocketFlags.None);
        }
    }

    /// <summary>Responds to the first and continuation SZL requests.</summary>
    /// <param name="peer">The loopback peer socket.</param>
    private static void RespondToSzlRequests(Socket peer)
    {
        _ = ReceiveExact(peer, new byte[SzlRequestLength]);
        SendAll(peer, CreateSzlResponse(true, false, 1, [0x11, 0x22]));
        _ = ReceiveExact(peer, new byte[SzlRequestLength]);
        SendAll(peer, CreateSzlResponse(false, true, SecondSzlSequence, [0x33, 0x44]));
    }

    /// <summary>Responds to one SZL request with a failing protocol return code.</summary>
    /// <param name="peer">The loopback peer socket.</param>
    private static void RespondWithSzlProtocolError(Socket peer)
    {
        _ = ReceiveExact(peer, new byte[SzlRequestLength]);
        var response = CreateSzlResponse(true, true, 1, [0x11, 0x22]);
        response[SzlReturnCodeOffset] = S7Failure;
        SendAll(peer, response);
    }

    /// <summary>Creates one protocol-valid SZL response packet.</summary>
    /// <param name="first">Whether the response is the first data unit.</param>
    /// <param name="last">Whether the response is the final data unit.</param>
    /// <param name="sequence">The response sequence number.</param>
    /// <param name="payload">The response payload.</param>
    /// <returns>The encoded SZL response.</returns>
    private static byte[] CreateSzlResponse(bool first, bool last, byte sequence, byte[] payload)
    {
        var payloadOffset = first ? FirstSzlPayloadOffset : ContinuationSzlPayloadOffset;
        var response = new byte[payloadOffset + payload.Length];
        response[0] = 0x03;
        BinaryPrimitives.WriteUInt16BigEndian(response.AsSpan(TpktLengthOffset), (ushort)response.Length);
        response[SzlSequenceOffset] = sequence;
        response[SzlLastDataUnitOffset] = last ? (byte)0 : (byte)1;
        response[SzlReturnCodeOffset] = S7Success;
        var encodedDataLength = payload.Length + (first ? FirstSzlMetadataLength : 0);
        BinaryPrimitives.WriteUInt16BigEndian(response.AsSpan(SzlDataLengthOffset), (ushort)encodedDataLength);
        if (first)
        {
            BinaryPrimitives.WriteUInt16BigEndian(
                response.AsSpan(SzlTotalLengthOffset),
                FirstSzlPayloadLength);
        }

        payload.CopyTo(response, payloadOffset);
        return response;
    }

    /// <summary>Records observer notifications for deterministic private-path assertions.</summary>
    /// <typeparam name="T">The observed value type.</typeparam>
    private sealed class RecordingObserver<T> : IObserver<T>
    {
        /// <summary>Gets the recorded values.</summary>
        public List<T> Values { get; } = [];

        /// <summary>Gets the recorded errors.</summary>
        public List<Exception> Errors { get; } = [];

        /// <inheritdoc/>
        public void OnCompleted()
        {
        }

        /// <inheritdoc/>
        public void OnError(Exception error) => Errors.Add(error);

        /// <inheritdoc/>
        public void OnNext(T value) => Values.Add(value);
    }
}
