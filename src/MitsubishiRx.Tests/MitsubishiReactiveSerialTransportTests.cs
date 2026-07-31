// Copyright (c) 2019-2026 Chris Pulman and contributors. All rights reserved.
// Chris Pulman and contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.IO.Ports;

#if REACTIVE_SHIM
using InMemoryPortPair = IoT.Driver.Serial.Reactive.InMemoryPortRxPair;
#else
using InMemoryPortPair = IoT.Driver.Serial.InMemoryPortRxPair;
#endif

#if REACTIVE_SHIM

namespace IoT.Driver.MitsubishiRx.Reactive.Tests;

#else

namespace IoT.Driver.MitsubishiRx.Tests;

#endif

/// <summary>Exercises reactive serial transport logic over the deterministic in-memory byte link.</summary>
internal sealed class MitsubishiReactiveSerialTransportTests
{
    /// <summary>Stores the primary in-memory port name.</summary>
    private const string PrimaryPortName = "MITSUBISHI-A";

    /// <summary>Stores the number of bytes in adapter round trips.</summary>
    private const int AdapterPayloadLength = 3;

    /// <summary>Stores the representative TCP port used by validation options.</summary>
    private const int ValidationPort = 5000;

    /// <summary>Stores the deterministic serial baud rate.</summary>
    private const int SerialBaudRate = 9600;

    /// <summary>Stores the deterministic serial data-bit count.</summary>
    private const int SerialDataBits = 7;

    /// <summary>Stores the deterministic serial operation timeout.</summary>
    private static readonly TimeSpan SerialTimeout = TimeSpan.FromSeconds(5);

    /// <summary>Stores the adapter request payload.</summary>
    private static readonly byte[] AdapterRequest = [0x01, 0x02, 0x03];

    /// <summary>Stores the adapter response payload.</summary>
    private static readonly byte[] AdapterResponse = [0x04, 0x05, 0x06];

    /// <summary>Verifies adapter reads, writes, signals, buffers, and lifecycle.</summary>
    /// <returns>A task that represents the asynchronous test.</returns>
    [Test]
    internal async Task AdapterRoundTripsThroughInMemorySerialPairAsync()
    {
        using var pair = new InMemoryPortPair(PrimaryPortName, "MITSUBISHI-B");
        var options = CreateOptions().ResolvedSerial;
        using var adapter = new ReactiveSerialPortAdapter(options, pair.First);
        var written = new List<byte[]>();
        var received = new List<byte[]>();
        using var writtenSubscription = adapter.WrittenBytes.Subscribe(written.Add);
        using var receivedSubscription = adapter.ReceivedBytes.Subscribe(received.Add);

        pair.Second.EnableAutoDataReceive = false;
        await pair.Second.OpenAsync();
        await adapter.OpenAsync();
        adapter.DiscardInBuffer();
        adapter.DiscardOutBuffer();
        adapter.Write(AdapterRequest);

        var peerBuffer = new byte[AdapterPayloadLength];
        var peerRead = await pair.Second
            .ReadAsync(peerBuffer, 0, peerBuffer.Length)
            .WaitAsync(SerialTimeout);
        pair.Second.Write(AdapterResponse, 0, AdapterPayloadLength);
        await WaitUntilAsync(() => received.Count >= AdapterPayloadLength);

        await Assert.That(adapter.IsOpen).IsTrue();
        await Assert.That(peerRead).IsEqualTo(peerBuffer.Length);
        await Assert.That(peerBuffer).IsEquivalentTo(AdapterRequest);
        await Assert.That(written).Count().IsEqualTo(1);
        await Assert.That(written[0]).IsEquivalentTo(peerBuffer);
        var receivedBytes = new List<byte>();
        foreach (var receivedChunk in received)
        {
            receivedBytes.AddRange(receivedChunk);
        }

        await Assert.That(receivedBytes).IsEquivalentTo(AdapterResponse);

        adapter.Close();
        await Assert.That(adapter.IsOpen).IsFalse();
        _ = Assert.Throws<ArgumentNullException>(() => adapter.Write(null!));
    }

    /// <summary>Verifies full transport exchange, configuration validation, reconnect, and disposal.</summary>
    /// <returns>A task that represents the asynchronous test.</returns>
    [Test]
    internal async Task TransportRoundTripsAndValidatesLifecycleAsync()
    {
        var options = CreateOptions();
        using var pair = new InMemoryPortPair("MITSUBISHI-C", "MITSUBISHI-D");
        pair.Second.EnableAutoDataReceive = false;
        await pair.Second.OpenAsync();
        var response = MitsubishiSimulatorTransport.CreateSuccessResponse(
            options,
            [0x31, 0x32, 0x33, 0x34]);
        await using var transport = new ReactiveSerialMitsubishiTransport(
            serialOptions => new ReactiveSerialPortAdapter(serialOptions, pair.First));
        await transport.ConnectAsync(options, CancellationToken.None);
        await transport.ConnectAsync(options, CancellationToken.None);
        var request = new byte[] { 0x05, 0x30 };
        var requestBuffer = new byte[request.Length];
        var exchangeTask = transport.ExchangeAsync(
            new(request, null, "In-memory serial exchange"),
            CancellationToken.None).AsTask();
        await WaitUntilAsync(() => pair.Second.BytesToRead >= request.Length);
        var peerRead = await pair.Second
            .ReadAsync(requestBuffer, 0, requestBuffer.Length)
            .WaitAsync(SerialTimeout);
        await Assert.That(peerRead).IsEqualTo(request.Length);
        await Assert.That(requestBuffer).IsEquivalentTo(request);
        pair.Second.Write(response, 0, response.Length);
        var result = await exchangeTask;

        await Assert.That(result).IsEquivalentTo(response);
        await Assert.That(transport.IsConnected).IsTrue();
        await transport.DisconnectAsync(CancellationToken.None);
        await Assert.That(transport.IsConnected).IsFalse();
    }

    /// <summary>Verifies serial transport guardrails before any physical-port work occurs.</summary>
    /// <returns>A task that represents the asynchronous test.</returns>
    [Test]
    internal async Task TransportGuardrailsAreDeterministicAsync()
    {
        var unconfigured = new ReactiveSerialMitsubishiTransport();
        _ = await Assert.ThrowsAsync<InvalidOperationException>(
            async () => await unconfigured.ExchangeAsync(
                new([1], null, "Unconfigured serial"),
                CancellationToken.None));
        _ = await Assert.ThrowsAsync<InvalidOperationException>(
            async () => await unconfigured.ConnectAsync(
                new(
                    "127.0.0.1",
                    ValidationPort,
                    MitsubishiFrameType.ThreeE,
                    CommunicationDataCode.Binary,
                    MitsubishiTransportKind.Tcp),
                CancellationToken.None));
        _ = await Assert.ThrowsAsync<ArgumentNullException>(
            async () => await unconfigured.ConnectAsync(null!, CancellationToken.None));
        _ = await Assert.ThrowsAsync<ArgumentNullException>(
            async () => await unconfigured.ExchangeAsync(null!, CancellationToken.None));
        await unconfigured.DisconnectAsync(CancellationToken.None);
        unconfigured.Dispose();

        _ = Assert.Throws<ArgumentNullException>(
            static () =>
            {
                var unexpected = new ReactiveSerialMitsubishiTransport(null!);
                GC.KeepAlive(unexpected);
            });
    }

    /// <summary>Waits until an asynchronous in-memory serial observation is satisfied.</summary>
    /// <param name="condition">The completion condition.</param>
    /// <returns>A task that represents the asynchronous wait.</returns>
    private static async Task WaitUntilAsync(Func<bool> condition)
    {
        using var cancellation = new CancellationTokenSource(SerialTimeout);
        using var timer = new PeriodicTimer(TimeSpan.FromMilliseconds(1));
        while (!condition())
        {
            await timer.WaitForNextTickAsync(cancellation.Token);
        }
    }

    /// <summary>Creates deterministic 1C serial options.</summary>
    /// <returns>The serial client options.</returns>
    private static MitsubishiClientOptions CreateOptions() =>
        new(
            PrimaryPortName,
            0,
            MitsubishiFrameType.OneC,
            CommunicationDataCode.Ascii,
            MitsubishiTransportKind.Serial,
            Timeout: SerialTimeout,
            CpuType: CpuType.Fx5,
            Serial: new MitsubishiSerialOptions(
                PrimaryPortName,
                SerialBaudRate,
                SerialDataBits,
                Parity.Even,
                StopBits.One,
                Handshake.None,
                MessageFormat: MitsubishiSerialMessageFormat.Format1));
}
