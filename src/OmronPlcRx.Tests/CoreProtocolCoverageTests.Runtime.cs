// Copyright (c) 2019-2026 Chris Pulman and contributors. All rights reserved.
// Chris Pulman and contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using IoT.DriverCore.OmronPlcRx.Core;
using IoT.DriverCore.OmronPlcRx.Core.Requests;
using IoT.DriverCore.OmronPlcRx.Core.Results;
using IoT.DriverCore.OmronPlcRx.Core.Types;
using IoT.DriverCore.OmronPlcRx.Enums;
using IoT.DriverCore.OmronPlcRx.Results;
using IoT.DriverCore.OmronPlcRx.Tags;
using TUnit.Core;
using CoreTcpClient = IoT.DriverCore.OmronPlcRx.Core.TcpClient;
using CoreUdpClient = IoT.DriverCore.OmronPlcRx.Core.UdpClient;
using NetTcpListener = System.Net.Sockets.TcpListener;
using NetUdpClient = System.Net.Sockets.UdpClient;

namespace IoT.DriverCore.OmronPlcRx.Tests;

/// <summary>Tests runtime protocol and transport behavior.</summary>
public sealed partial class CoreProtocolCoverageTests
{
    /// <summary>Verifies simple value objects expose their initialized values.</summary>
    /// <returns>A task that represents the asynchronous test.</returns>
    [Test]
    public async Task ValueObjects_ExposeConstructorValuesAsync()
    {
        const short bcd16Value = 1234;
        const int bcd32Value = 12_345_678;
        const ushort bcdU16Value = 9876;
        const uint bcdU32Value = 87_654_321U;
        const int tagValue = 42;
        const string dataMemoryAddressText = "D100";
        const string nextDataMemoryAddressText = "D101";

        var bcd16 = new Bcd16(bcd16Value);
        var bcd32 = new Bcd32(bcd32Value);
        var bcdU16 = new BcdU16(bcdU16Value);
        var bcdU32 = new BcdU32(bcdU32Value);
        var tag = new PlcTag<int>(SpeedTagName, dataMemoryAddressText) { Value = tagValue };
        var attribute = new PlcTagAttribute(dataMemoryAddressText)
        {
            TagName = SpeedTagName,
            Register = false,
            Observe = false,
            Writable = true,
        };
        await Assert.That(bcd16.Value).IsEqualTo(bcd16Value);
        await Assert.That(bcd16.ToString()).IsEqualTo("1234");
        await Assert.That(bcd16.GetHashCode()).IsEqualTo(bcd16Value.GetHashCode());
        await Assert.That(bcd32.Value).IsEqualTo(bcd32Value);
        await Assert.That(bcd32.ToString()).IsEqualTo("12345678");
        await Assert.That(bcd32.GetHashCode()).IsEqualTo(bcd32Value.GetHashCode());
        await Assert.That(bcdU16.Value).IsEqualTo(bcdU16Value);
        await Assert.That(bcdU16.ToString()).IsEqualTo("9876");
        await Assert.That(bcdU16.GetHashCode()).IsEqualTo(bcdU16Value.GetHashCode());
        await Assert.That(bcdU32.Value).IsEqualTo(bcdU32Value);
        await Assert.That(bcdU32.ToString()).IsEqualTo("87654321");
        await Assert.That(bcdU32.GetHashCode()).IsEqualTo(bcdU32Value.GetHashCode());
        await Assert.That(tag.TagName).IsEqualTo(SpeedTagName);
        await Assert.That(tag.Address).IsEqualTo(dataMemoryAddressText);
        await Assert.That(tag.TagType).IsEqualTo(typeof(int));
        await Assert.That(tag.Value).IsEqualTo(tagValue);
        await Assert.That(((IPlcTag)tag).Value).IsEqualTo(tagValue);
        await Assert.That(attribute.Address).IsEqualTo(dataMemoryAddressText);
        await Assert.That(attribute.TagName).IsEqualTo(SpeedTagName);
        await Assert.That(attribute.Register).IsFalse();
        await Assert.That(attribute.Observe).IsFalse();
        await Assert.That(attribute.Writable).IsTrue();
        await Assert.That(new PlcTagAttribute(nextDataMemoryAddressText).Register).IsTrue();
        await Assert.That(new PlcTagAttribute(nextDataMemoryAddressText).Observe).IsTrue();
    }

    /// <summary>Verifies base channel processing maps a valid request attempt to metrics and response data.</summary>
    /// <returns>A task that represents the asynchronous test.</returns>
    [Test]
    public async Task BaseChannel_ProcessRequestReturnsMetricsAndParsedResponseAsync()
    {
        using var plc = CreateConnection();
        using var channel = new TestChannel();
        channel.SetResponseData([0x12, 0x34]);
        var request = ReadMemoryAreaWordRequest.CreateNew(
            plc,
            DataMemoryAddress,
            SingleWordCount,
            MemoryWordDataType.DataMemory);

        var result = await channel.ProcessRequestAsync(
            request,
            DataMemoryAddress,
            0,
            CancellationToken.None);

        await Assert.That(result.BytesSent).IsEqualTo(ExpectedBytesSent);
        await Assert.That(result.PacketsSent).IsEqualTo(SingleWordCount);
        await Assert.That(result.BytesReceived).IsEqualTo(ExpectedBytesReceived);
        await Assert.That(result.PacketsReceived).IsEqualTo(SingleWordCount);
        await Assert.That(result.Duration >= 0).IsTrue();
        await Assert.That(result.Response.ServiceID).IsEqualTo((byte)SingleWordCount);
        await Assert.That(Convert.ToHexString(result.Response.Data!)).IsEqualTo("1234");
        await Assert.That(channel.SendCount).IsEqualTo(SingleWordCount);
        await Assert.That(channel.ReceiveCount).IsEqualTo(SingleWordCount);
    }

    /// <summary>Verifies base channel processing reinitializes the client before retry attempts.</summary>
    /// <returns>A task that represents the asynchronous test.</returns>
    [Test]
    public async Task BaseChannel_ProcessRequestRetriesAfterTransientSendFailureAsync()
    {
        using var plc = CreateConnection();
        using var channel = new TestChannel { FailFirstSend = true };
        channel.SetResponseData([0x12, 0x34]);
        var request = ReadMemoryAreaWordRequest.CreateNew(
            plc,
            DataMemoryAddress,
            SingleWordCount,
            MemoryWordDataType.DataMemory);

        var result = await channel.ProcessRequestAsync(
            request,
            DataMemoryAddress,
            SingleWordCount,
            CancellationToken.None);

        await Assert.That(Convert.ToHexString(result.Response.Data!)).IsEqualTo("1234");
        await Assert.That(channel.SendCount).IsEqualTo(PairCount);
        await Assert.That(channel.DestroyCount).IsEqualTo(SingleWordCount);
    }

    /// <summary>Verifies base channel processing purges stale responses when service identifiers mismatch.</summary>
    /// <returns>A task that represents the asynchronous test.</returns>
    [Test]
    public async Task BaseChannel_ProcessRequestPurgesServiceIdMismatchesAsync()
    {
        using var plc = CreateConnection();
        using var channel = new TestChannel { ForceServiceIdMismatch = true, ThrowDuringPurge = true };
        channel.SetResponseData([0x12, 0x34]);
        var request = ReadMemoryAreaWordRequest.CreateNew(
            plc,
            DataMemoryAddress,
            SingleWordCount,
            MemoryWordDataType.DataMemory);

        var ex = await CaptureExceptionAsync<OmronPLCException>(
            () => channel.ProcessRequestAsync(request, DataMemoryAddress, 0, CancellationToken.None));

        await Assert.That(ex.Message).Contains("FINS Error Response");
        await Assert.That(channel.PurgeCount).IsEqualTo(SingleWordCount);
    }

    /// <summary>Verifies TCP client loopback send, receive, properties and endpoint helpers.</summary>
    /// <returns>A task that represents the asynchronous test.</returns>
    [Test]
    public async Task TcpClient_ConnectsSendsReceivesAndExposesSocketStateAsync()
    {
        var listener = new NetTcpListener(System.Net.IPAddress.Loopback, 0);
        listener.Start();
        try
        {
            var port = ((System.Net.IPEndPoint)listener.LocalEndpoint).Port;
            var serverTask = EchoTcpAsync(listener);
            using var client = new CoreTcpClient(LoopbackHost, port);

            client.NoDelay = true;
            client.LingerState = new(true, 0);
            client.KeepAliveEnabled = true;
            await client.ConnectAsync(SocketTimeoutMilliseconds, CancellationToken.None);
            var bytesSent = await client.SendAsync(
                [SingleWordCount, PairCount, TripleCount],
                SocketTimeoutMilliseconds,
                CancellationToken.None);
            var receiveBuffer = new byte[PairCount];
            var bytesReceived = await client.ReceiveAsync(
                receiveBuffer,
                SocketTimeoutMilliseconds,
                CancellationToken.None);
            var serverReceived = await serverTask.ConfigureAwait(false);
            var dnsEndpoint = new System.Net.DnsEndPoint(LocalHostName, TcpPort);

            await Assert.That(bytesSent).IsEqualTo(TripleCount);
            await Assert.That(bytesReceived).IsEqualTo(PairCount);
            await Assert.That(Convert.ToHexString(receiveBuffer)).IsEqualTo("0405");
            await Assert.That(serverReceived).IsEqualTo(TripleCount);
            await Assert.That(client.Connected).IsTrue();
            await Assert.That(client.Socket).IsNotNull();
            await Assert.That(client.NoDelay).IsTrue();
            await Assert.That(client.LingerState!.Enabled).IsTrue();
            await Assert.That(client.KeepAliveEnabled).IsTrue();
            await Assert.That(TcpSocketConfiguration.GetRemoteHostAndPort(dnsEndpoint))
                .IsEqualTo((LocalHostName, TcpPort));
            await Assert.That(TcpSocketConfiguration.GetRemoteHostAndPort(null)).IsEqualTo((string.Empty, 0));

            client.Dispose();

            await Assert.That(client.Connected).IsFalse();
            await Assert.That(client.Socket).IsNull();
            await Assert.That(client.NoDelay).IsFalse();
            await Assert.That(client.LingerState).IsNull();
            await Assert.That(client.KeepAliveEnabled).IsFalse();
            var disposedConnect = await CaptureExceptionAsync<ObjectDisposedException>(
                () => client.ConnectAsync(SingleWordCount, CancellationToken.None));
            await Assert.That(disposedConnect).IsNotNull();
        }
        finally
        {
            listener.Stop();
        }
    }

    /// <summary>Verifies UDP client loopback send, receive, properties and disposed guards.</summary>
    /// <returns>A task that represents the asynchronous test.</returns>
    [Test]
    public async Task UdpClient_SendsReceivesAndExposesSocketStateAsync()
    {
        using var server = new NetUdpClient(new System.Net.IPEndPoint(System.Net.IPAddress.Loopback, 0));
        var endpoint = (System.Net.IPEndPoint)server.Client.LocalEndPoint!;
        var serverTask = EchoUdpAsync(server);
        using var client = new CoreUdpClient(System.Net.IPAddress.Loopback, endpoint.Port);

        var bytesSent = await client.SendAsync(
            [SingleWordCount, PairCount, TripleCount],
            SocketTimeoutMilliseconds,
            CancellationToken.None);
        var receiveBuffer = new byte[PairCount];
        var bytesReceived = await client.ReceiveAsync(
            receiveBuffer,
            SocketTimeoutMilliseconds,
            CancellationToken.None);
        var serverReceived = await serverTask.ConfigureAwait(false);

        await Assert.That(bytesSent).IsEqualTo(TripleCount);
        await Assert.That(bytesReceived).IsEqualTo(PairCount);
        await Assert.That(Convert.ToHexString(receiveBuffer)).IsEqualTo("0708");
        await Assert.That(serverReceived).IsEqualTo(TripleCount);
        await Assert.That(client.Socket).IsNotNull();

        client.Dispose();

        await Assert.That(client.Available).IsEqualTo(0);
        await Assert.That(client.Socket).IsNull();
        var disposedSend = await CaptureExceptionAsync<ObjectDisposedException>(
            () => client.SendAsync([SingleWordCount], SingleWordCount, CancellationToken.None));
        await Assert.That(disposedSend).IsNotNull();
    }

    /// <summary>Verifies socket wrapper constructors validate their inputs.</summary>
    /// <returns>A task that represents the asynchronous test.</returns>
    [Test]
    public async Task SocketClients_ValidateConstructorInputsAsync()
    {
        await Assert.That(
                CaptureException<ArgumentNullException>(() => _ = CreateTcpClient((string)null!, TcpPort)))
            .IsNotNull();
        await Assert.That(
                CaptureException<ArgumentOutOfRangeException>(() => _ = CreateTcpClient(LoopbackHost, -1)))
            .IsNotNull();
        await Assert.That(
                CaptureException<ArgumentNullException>(
                    () => _ = CreateTcpClient((System.Net.IPAddress)null!, TcpPort)))
            .IsNotNull();
        await Assert.That(
                CaptureException<ArgumentOutOfRangeException>(
                    () => _ = CreateTcpClient(System.Net.IPAddress.Loopback, InvalidPort)))
            .IsNotNull();
        await Assert.That(
                CaptureException<ArgumentNullException>(() => _ = CreateUdpClient((string)null!, TcpPort)))
            .IsNotNull();
        await Assert.That(
                CaptureException<ArgumentOutOfRangeException>(() => _ = CreateUdpClient(LoopbackHost, -1)))
            .IsNotNull();
        await Assert.That(
                CaptureException<ArgumentNullException>(
                    () => _ = CreateUdpClient((System.Net.IPAddress)null!, TcpPort)))
            .IsNotNull();
        await Assert.That(
                CaptureException<ArgumentOutOfRangeException>(
                    () => _ = CreateUdpClient(System.Net.IPAddress.Loopback, InvalidPort)))
            .IsNotNull();
    }

    /// <summary>Verifies socket cleanup cancellation behavior.</summary>
    /// <returns>A task that represents the asynchronous test.</returns>
    [Test]
    public async Task SocketOperationCleanup_ObservesCancellationExceptionsAsync()
    {
        using var delayCts = new CancellationTokenSource();
        var delayTask = Task.Delay(TimeSpan.FromMinutes(1), delayCts.Token);

        await SocketOperationCleanup.CancelDelayAsync(delayCts, delayTask);

        using var operationCts = new CancellationTokenSource();
        var operationTask = Task.Delay(TimeSpan.FromMinutes(1), operationCts.Token);

        await SocketOperationCleanup.CancelSocketOperationAsync(operationCts, operationTask);
    }

    /// <summary>Verifies injected PLC connections initialize by reading controller information.</summary>
    /// <returns>A task that represents the asynchronous test.</returns>
    [Test]
    public async Task OmronPlcConnection_InitializeReadsControllerInformationAsync()
    {
        using var channel = new TestChannel();
        channel.SetResponseData(BuildCpuUnitData(ControllerModel, ControllerVersion));
        using var plc = CreateInjectedConnection(channel, isInitialized: false);

        await plc.InitializeAsync(CancellationToken.None);

        await Assert.That(plc.IsInitialized).IsTrue();
        await Assert.That(plc.ControllerModel).IsEqualTo(ControllerModel);
        await Assert.That(plc.ControllerVersion).IsEqualTo(ControllerVersion);
        await Assert.That(plc.PlcType).IsEqualTo(PlcType.C_Series);
        await Assert.That(channel.InitializeCount).IsEqualTo(SingleWordCount);
    }

    /// <summary>Verifies injected PLC connections execute read and write operations through the channel.</summary>
    /// <returns>A task that represents the asynchronous test.</returns>
    [Test]
    public async Task OmronPlcConnection_ReadsAndWritesThroughInjectedChannelAsync()
    {
        const MemoryWordDataType dataMemoryWordType = MemoryWordDataType.DataMemory;

        using var readWordsChannel = new TestChannel();
        readWordsChannel.SetResponseData([0x12, 0x34, 0xFF, 0xFE]);
        using var readWordsPlc = CreateInjectedConnection(readWordsChannel);
        var words = await readWordsPlc.ReadWordsAsync(DataMemoryAddress, PairCount, dataMemoryWordType, default);
        using var readBitsChannel = new TestChannel();
        readBitsChannel.SetResponseData([1, 0]);
        using var readBitsPlc = CreateInjectedConnection(readBitsChannel);
        var bits = await readBitsPlc.ReadBitsAsync(
            DataMemoryAddress,
            0,
            PairCount,
            MemoryBitDataType.CommonIO,
            CancellationToken.None);
        using var clockChannel = new TestChannel();
        clockChannel.SetResponseData([0x26, 0x06, 0x30, 0x14, 0x25, 0x59, 0x02]);
        using var clockPlc = CreateInjectedConnection(clockChannel);
        var clock = await clockPlc.ReadClockAsync(CancellationToken.None);

        using var cycleChannel = new TestChannel();
        cycleChannel.SetResponseData([0x00, 0x00, 0x01, 0x23, 0x00, 0x00, 0x04, 0x56, 0x00, 0x00, 0x00, 0x00]);
        using var cyclePlc = CreateInjectedConnection(cycleChannel);
        var cycle = await cyclePlc.ReadCycleTimeAsync(CancellationToken.None);

        using var writeWordsChannel = new TestChannel();
        using var writeWordsPlc = CreateInjectedConnection(writeWordsChannel);
        var writeWords = await writeWordsPlc.WriteWordsAsync(
            [SingleWordCount, PairCount],
            DataMemoryAddress,
            MemoryWordDataType.DataMemory,
            CancellationToken.None);

        using var writeBitsChannel = new TestChannel();
        using var writeBitsPlc = CreateInjectedConnection(writeBitsChannel);
        var writeBits = await writeBitsPlc.WriteBitsAsync(
            [true, false],
            DataMemoryAddress,
            0,
            MemoryBitDataType.CommonIO,
            CancellationToken.None);

        using var writeClockChannel = new TestChannel();
        using var writeClockPlc = CreateInjectedConnection(writeClockChannel);
        var writeClock = await writeClockPlc.WriteClockAsync(
            ProtocolDateTime,
            Weekday,
            CancellationToken.None);

        await Assert.That(Convert.ToHexString(ToBigEndianBytes(words.Values))).IsEqualTo("1234FFFE");
        await Assert.That(ToBitText(bits.Values)).IsEqualTo("1,0");
        await Assert.That(clock.Clock).IsEqualTo(
            new DateTime(2026, 6, 30, 14, 25, 59, DateTimeKind.Utc));
        await Assert.That(clock.DayOfWeek).IsEqualTo(Weekday);
        await Assert.That(cycle.AverageCycleTime).IsEqualTo(ExpectedAverageCycleTime);
        await Assert.That(cycle.MaximumCycleTime).IsEqualTo(ExpectedMaximumCycleTime);
        await Assert.That(cycle.MinimumCycleTime).IsEqualTo(0D);
        await Assert.That(writeWords.BytesSent > 0).IsTrue();
        await Assert.That(writeBits.BytesSent > 0).IsTrue();
        await Assert.That(writeClock.BytesSent > 0).IsTrue();
    }

    /// <summary>Verifies PLC connection validation rejects invalid operation inputs.</summary>
    /// <returns>A task that represents the asynchronous test.</returns>
    [Test]
    public async Task OmronPlcConnection_RejectsInvalidOperationInputsAsync()
    {
        using var channel = new TestChannel();
        using var plc = CreateInjectedConnection(channel);
        using var uninitialized = CreateInjectedConnection(
            new TestChannel(),
            isInitialized: false);
        using var seriesPlc = CreateInjectedConnection(new TestChannel(), plcType: PlcType.NX102);

        await AssertInvalidReadOperationsAsync(plc, uninitialized);
        await AssertInvalidWriteOperationsAsync(plc);
        await AssertThrowsAsync<OmronPLCException>(() => seriesPlc.ReadCycleTimeAsync(CancellationToken.None));
    }
}
