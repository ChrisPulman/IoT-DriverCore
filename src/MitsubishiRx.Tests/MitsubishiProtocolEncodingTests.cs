// Copyright (c) 2019-2026 Chris Pulman and contributors. All rights reserved.
// Chris Pulman and contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

#if REACTIVE_SHIM

namespace MitsubishiRx.Reactive.Tests;
#else

namespace MitsubishiRx.Tests;
#endif

/// <summary>Provides the MitsubishiProtocolEncodingTests type.</summary>
internal sealed class MitsubishiProtocolEncodingTests
{
    /// <summary>Stores the <c>LoopbackHost</c> test value.</summary>
    private const string LoopbackHost = "127.0.0.1";

    /// <summary>Stores the word count used by two-word read requests.</summary>
    private const int TwoWordReadCount = 2;

    /// <summary>Stores the expected one-E response length.</summary>
    private const int ExpectedOneEResponseLength = 6;

    /// <summary>Stores the word count used by memory-read requests.</summary>
    private const int MemoryReadWordCount = 3;

    /// <summary>Executes the ReadWordsAsyncEncodesBinary3ERequest operation.</summary>
    /// <returns>The ReadWordsAsyncEncodesBinary3ERequest operation result.</returns>
    [Test]
    internal async Task ReadWordsAsyncEncodesBinary3ERequestAsync()
    {
        await using var transport = new FakeTransport(
        [
            [
                0xD0, 0x00, 0x00, 0xFF, 0xFF, 0x03, 0x00, 0x06, 0x00, 0x00, 0x00, 0x34, 0x12, 0x78, 0x56,
            ],
        ]);

        var options = new MitsubishiClientOptions(
            Host: LoopbackHost,
            Port: 5000,
            FrameType: MitsubishiFrameType.ThreeE,
            DataCode: CommunicationDataCode.Binary,
            TransportKind: MitsubishiTransportKind.Tcp,
            Route: MitsubishiRoute.Default);

        await using var client = new MitsubishiRx(options, transport, Scheduler.Immediate);
        var result = await client.ReadWordsAsync("D100", TwoWordReadCount, CancellationToken.None);

        await Assert.That(result.IsSucceed).IsTrue();
        await Assert.That(result.Value!.Select(static value => (int)value).ToArray()).IsEquivalentTo([0x1234, 0x5678]);
        await Assert.That(transport.Requests.Count).IsEqualTo(1);
        await Assert.That(transport.Requests[0].Payload.Select(static value => (int)value).ToArray()).IsEquivalentTo(
        [
            0x50, 0x00, 0x00, 0xFF, 0xFF, 0x03, 0x00, 0x0C, 0x00, 0x10, 0x00,
            0x01, 0x04, 0x00, 0x00, 0x64, 0x00, 0x00, 0xA8, 0x02, 0x00,
        ]);
    }

    /// <summary>Executes the ExecuteRawAsyncEncodesBinary4ERequestWithSerialNumber operation.</summary>
    /// <returns>The ExecuteRawAsyncEncodesBinary4ERequestWithSerialNumber operation result.</returns>
    [Test]
    internal async Task ExecuteRawAsyncEncodesBinary4ERequestWithSerialNumberAsync()
    {
        await using var transport = new FakeTransport(
        [
            [
                0xD4, 0x00, 0x34, 0x12, 0x00, 0x00, 0x00, 0xFF, 0xFF, 0x03, 0x00, 0x02, 0x00, 0x00, 0x00,
            ],
        ]);

        var options = new MitsubishiClientOptions(
            Host: LoopbackHost,
            Port: 5001,
            FrameType: MitsubishiFrameType.FourE,
            DataCode: CommunicationDataCode.Binary,
            TransportKind: MitsubishiTransportKind.Tcp,
            Route: MitsubishiRoute.Default,
            MonitoringTimer: 0x0010,
            SerialNumberProvider: () => 0x1234);

        await using var client = new MitsubishiRx(options, transport, Scheduler.Immediate);
        var result = await client.ExecuteRawAsync(
            new MitsubishiRawCommandRequest(0x0101, 0x0000),
            CancellationToken.None);

        await Assert.That(result.IsSucceed).IsTrue();
        await Assert.That(transport.Requests.Count).IsEqualTo(1);
        await Assert.That(transport.Requests[0].Payload.Select(static value => (int)value).ToArray()).IsEquivalentTo(
        [
            0x54, 0x00, 0x34, 0x12, 0x00, 0x00, 0x00, 0xFF, 0xFF, 0x03, 0x00,
            0x06, 0x00, 0x10, 0x00, 0x01, 0x01, 0x00, 0x00,
        ]);
    }

    /// <summary>Executes the ReadWordsAsyncEncodesBinary1ERequest operation.</summary>
    /// <returns>The ReadWordsAsyncEncodesBinary1ERequest operation result.</returns>
    [Test]
    internal async Task ReadWordsAsyncEncodesBinary1ERequestAsync()
    {
        await using var transport = new FakeTransport(
        [
            [0x81, 0x00, 0x34, 0x12, 0x78, 0x56],
        ]);

        var options = new MitsubishiClientOptions(
            Host: LoopbackHost,
            Port: 5002,
            FrameType: MitsubishiFrameType.OneE,
            DataCode: CommunicationDataCode.Binary,
            TransportKind: MitsubishiTransportKind.Tcp);

        await using var client = new MitsubishiRx(options, transport, Scheduler.Immediate);
        var result = await client.ReadWordsAsync("D100", TwoWordReadCount, CancellationToken.None);

        await Assert.That(result.IsSucceed).IsTrue();
        await Assert.That(result.Value!.Select(static value => (int)value).ToArray()).IsEquivalentTo([0x1234, 0x5678]);
        await Assert.That(transport.Requests.Count).IsEqualTo(1);
        await Assert.That(transport.Requests[0].Payload.Select(static value => (int)value).ToArray()).IsEquivalentTo(
        [
            0x01, 0xFF, 0x10, 0x00, 0x64, 0x00, 0x00, 0x00, 0x20, 0x44, 0x02, 0x00,
        ]);
        await Assert.That(transport.Requests[0].ExpectedResponseLength).IsEqualTo(ExpectedOneEResponseLength);
    }

    /// <summary>Executes the ReadWordsAsyncParsesAscii3EResponse operation.</summary>
    /// <returns>The ReadWordsAsyncParsesAscii3EResponse operation result.</returns>
    [Test]
    internal async Task ReadWordsAsyncParsesAscii3EResponseAsync()
    {
        var asciiResponse = System.Text.Encoding.ASCII.GetBytes("D00000FF03FF000006000012345678");
        await using var transport = new FakeTransport([asciiResponse]);
        var options = new MitsubishiClientOptions(
            Host: LoopbackHost,
            Port: 5004,
            FrameType: MitsubishiFrameType.ThreeE,
            DataCode: CommunicationDataCode.Ascii,
            TransportKind: MitsubishiTransportKind.Tcp,
            Route: MitsubishiRoute.Default);

        await using var client = new MitsubishiRx(options, transport, Scheduler.Immediate);
        var result = await client.ReadWordsAsync("D100", TwoWordReadCount, CancellationToken.None);
        if (!result.IsSucceed)
        {
            throw new InvalidOperationException(result.Err);
        }

        await Assert.That(result.IsSucceed).IsTrue();
        await Assert.That(result.Value!.Select(static value => (int)value).ToArray()).IsEquivalentTo([0x1234, 0x5678]);
        await Assert.That(System.Text.Encoding.ASCII.GetString(transport.Requests[0].Payload))
            .IsEqualTo("500000FF03FF000012001004010000000064D*0002");
    }

    /// <summary>Executes the ReadMemoryAsyncEncodesRequestedLength operation.</summary>
    /// <returns>The ReadMemoryAsyncEncodesRequestedLength operation result.</returns>
    [Test]
    internal async Task ReadMemoryAsyncEncodesRequestedLengthAsync()
    {
        await using var transport = new FakeTransport(
        [
            [0xD0, 0x00, 0x00, 0xFF, 0xFF, 0x03, 0x00, 0x08, 0x00, 0x00, 0x00, 0x34, 0x12, 0x78, 0x56, 0xBC, 0x9A],
        ]);

        var options = new MitsubishiClientOptions(
            Host: LoopbackHost,
            Port: 5005,
            FrameType: MitsubishiFrameType.ThreeE,
            DataCode: CommunicationDataCode.Binary,
            TransportKind: MitsubishiTransportKind.Tcp,
            Route: MitsubishiRoute.Default);

        await using var client = new MitsubishiRx(options, transport, Scheduler.Immediate);
        var result = await client.ReadMemoryAsync(
            MitsubishiCommands.MemoryRead,
            0x2000,
            MemoryReadWordCount,
            CancellationToken.None);

        await Assert.That(result.IsSucceed).IsTrue();
        await Assert.That(result.Value!.Select(static value => (int)value).ToArray())
            .IsEquivalentTo([0x1234, 0x5678, 0x9ABC]);
        await Assert.That(transport.Requests.Count).IsEqualTo(1);
        var payload = transport.Requests[0].Payload;
        await Assert.That(payload[15]).IsEqualTo((byte)0x00);
        await Assert.That(payload[16]).IsEqualTo((byte)0x20);
        await Assert.That(payload[17]).IsEqualTo((byte)0x03);
        await Assert.That(payload[18]).IsEqualTo((byte)0x00);
    }
}
