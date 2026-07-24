// Copyright (c) 2019-2026 Chris Pulman and contributors. All rights reserved.
// Chris Pulman and contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.IO.Ports;
using System.Text;

#if REACTIVE_SHIM

namespace IoT.DriverCore.MitsubishiRx.Reactive.Tests;

#else

namespace IoT.DriverCore.MitsubishiRx.Tests;

#endif

/// <summary>Exercises residual deterministic simulator transport and state-control paths.</summary>
internal sealed class MitsubishiSimulatorRunnerResidualCoverageTests
{
    /// <summary>Stores the largest point count representable by a legacy batch request.</summary>
    private const int MaximumLegacyPointCount = 256;

    /// <summary>Verifies public simulator state controls and legacy error response shapes.</summary>
    /// <returns>A task that represents the asynchronous test.</returns>
    [Test]
    internal async Task StateControlsAndLegacyErrorResponsesAreDeterministicAsync()
    {
        _ = Assert.Throws<ArgumentNullException>(
            () =>
            {
                var unexpectedSimulator = new MitsubishiSimulatorTransport(
                    (MitsubishiSimulatorMemory)null!);
                GC.KeepAlive(unexpectedSimulator);
            });

        await using var simulator = new MitsubishiSimulatorTransport
        {
            ModelName = "SIM-RUNNER",
            ModelCode = 0x1234,
        };

        await Assert.That(simulator.ModelName).IsEqualTo("SIM-RUNNER");
        await Assert.That(simulator.ModelCode).IsEqualTo((ushort)0x1234);
        _ = Assert.Throws<ArgumentException>(() => simulator.WriteBufferMemory(0, []));

        var binary = MitsubishiSimulatorTransport.CreateErrorResponse(
            CreateMcOptions(MitsubishiFrameType.OneE, CommunicationDataCode.Binary),
            0xC051);
        var ascii = MitsubishiSimulatorTransport.CreateErrorResponse(
            CreateMcOptions(MitsubishiFrameType.OneE, CommunicationDataCode.Ascii),
            0xC051);

        await Assert.That(binary.Select(static value => (int)value).ToArray())
            .IsEquivalentTo([0x81, 0x5B, 0x51, 0xC0]);
        await Assert.That(Encoding.ASCII.GetString(ascii)).IsEqualTo("815BC051");
    }

    /// <summary>Verifies serial fallbacks and malformed batch frames have deterministic outcomes.</summary>
    /// <returns>A task that represents the asynchronous test.</returns>
    [Test]
    internal async Task SerialFallbackAndMalformedBatchFramesAreDeterministicAsync()
    {
        var options = CreateSerialOptions(MitsubishiFrameType.OneC);
        await using var simulator = new MitsubishiSimulatorTransport();
        await simulator.ConnectAsync(options, CancellationToken.None);

        var fallback = await simulator.ExchangeAsync(
            new MitsubishiTransportRequest([], null, "Unsupported serial operation"),
            CancellationToken.None);

        await Assert.That(fallback).IsNotEmpty();
        _ = Assert.Throws<InvalidDataException>(
            () => simulator.ExchangeAsync(
                new MitsubishiTransportRequest([0x05], null, "Read words D100"),
                CancellationToken.None).AsTask().GetAwaiter().GetResult());
    }

    /// <summary>Verifies legacy zero point counts decode to the supported maximum batch size.</summary>
    /// <param name="dataCode">The legacy MC data encoding.</param>
    /// <returns>A task that represents the asynchronous test.</returns>
    [Test]
    [Arguments(CommunicationDataCode.Binary)]
    [Arguments(CommunicationDataCode.Ascii)]
    internal async Task LegacyZeroPointCountReadsMaximumBatchAsync(CommunicationDataCode dataCode)
    {
        var options = CreateMcOptions(MitsubishiFrameType.OneE, dataCode);
        await using var simulator = new MitsubishiSimulatorTransport();
        await using var client = new MitsubishiRx(options, simulator, Scheduler.Immediate);

        var result = await client.ReadWordsAsync("D100", MaximumLegacyPointCount, CancellationToken.None);

        await Assert.That(result.IsSucceed).IsTrue();
        await Assert.That(result.Value!.Length).IsEqualTo(MaximumLegacyPointCount);
        await Assert.That(result.Value).IsEquivalentTo(new ushort[MaximumLegacyPointCount]);
    }

    /// <summary>Creates deterministic legacy MC client options.</summary>
    /// <param name="frameType">The MC frame type.</param>
    /// <param name="dataCode">The MC data encoding.</param>
    /// <returns>The configured client options.</returns>
    private static MitsubishiClientOptions CreateMcOptions(
        MitsubishiFrameType frameType,
        CommunicationDataCode dataCode) =>
        new(
            Host: "127.0.0.1",
            Port: 5000,
            FrameType: frameType,
            DataCode: dataCode,
            TransportKind: MitsubishiTransportKind.Tcp,
            Route: MitsubishiRoute.Default,
            SerialNumberProvider: static () => 0x1234);

    /// <summary>Creates deterministic legacy serial client options.</summary>
    /// <param name="frameType">The serial frame type.</param>
    /// <returns>The configured client options.</returns>
    private static MitsubishiClientOptions CreateSerialOptions(MitsubishiFrameType frameType) =>
        new(
            Host: "COM-SIMULATED",
            Port: 0,
            FrameType: frameType,
            DataCode: CommunicationDataCode.Ascii,
            TransportKind: MitsubishiTransportKind.Serial,
            CpuType: CpuType.Fx5,
            Serial: new MitsubishiSerialOptions(
                PortName: "COM-SIMULATED",
                BaudRate: 9600,
                DataBits: 7,
                Parity: Parity.Even,
                StopBits: StopBits.One,
                Handshake: Handshake.None,
                MessageFormat: MitsubishiSerialMessageFormat.Format1));
}
