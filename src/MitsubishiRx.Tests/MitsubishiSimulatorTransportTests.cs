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

/// <summary>Exercises the production deterministic Mitsubishi transport simulator.</summary>
internal sealed class MitsubishiSimulatorTransportTests
{
    /// <summary>Stores the number of words used by multi-word reads.</summary>
    private const int MultipleWordCount = 2;

    /// <summary>Stores the number of advanced operations issued by the acknowledgement test.</summary>
    private const int AdvancedOperationCount = 5;

    /// <summary>Stores the number of bits used by standard batch scenarios.</summary>
    private const int BatchBitCount = 4;

    /// <summary>Stores the number of bits used by advanced block scenarios.</summary>
    private const int AdvancedBitCount = 3;

    /// <summary>Stores the number of memory seed-write operations.</summary>
    private const int MemorySeedWriteCount = 4;

    /// <summary>Stores the expanded memory snapshot size.</summary>
    private const int ExpandedSnapshotCount = 6;

    /// <summary>Stores a two-word ASCII payload.</summary>
    private const string AsciiWordPayload = "12345678";

    /// <summary>Stores the simulated request description.</summary>
    private const string SimulationRequestDescription = "Simulator request";

    /// <summary>Verifies scripted responses, request snapshots, and lifecycle state.</summary>
    /// <returns>A task that represents the asynchronous test.</returns>
    [Test]
    internal async Task ScriptedTransportCapturesImmutableRequestsAndLifecycleAsync()
    {
        var queuedResponse = new byte[] { 0x01, 0x02, 0x03 };
        await using var simulator = new MitsubishiSimulatorTransport([queuedResponse]);
        var options = CreateMcOptions(MitsubishiFrameType.ThreeE, CommunicationDataCode.Binary);

        await simulator.ConnectAsync(options, CancellationToken.None);
        var payload = new byte[] { 0x04, 0x05, 0x06 };
        var response = await simulator.ExchangeAsync(
            new MitsubishiTransportRequest(payload, null, SimulationRequestDescription),
            CancellationToken.None);
        payload[0] = 0;
        queuedResponse[0] = 0;
        response[0] = 0;

        await Assert.That(simulator.IsConnected).IsTrue();
        await Assert.That(simulator.ConnectedOptions).IsEqualTo(options);
        await Assert.That(simulator.ConnectCount).IsEqualTo(1);
        await Assert.That(simulator.Requests.Count).IsEqualTo(1);
        await Assert.That(
                simulator.Requests[0].Payload.Select(static value => (int)value).ToArray())
            .IsEquivalentTo([0x04, 0x05, 0x06]);

        simulator.ClearRequests();
        await simulator.DisconnectAsync(CancellationToken.None);

        await Assert.That(simulator.Requests).IsEmpty();
        await Assert.That(simulator.IsConnected).IsFalse();
        await Assert.That(simulator.DisconnectCount).IsEqualTo(1);
    }

    /// <summary>Verifies binary MC framing for each supported Ethernet frame.</summary>
    /// <param name="frameType">The MC frame type.</param>
    /// <returns>A task that represents the asynchronous test.</returns>
    [Test]
    [Arguments(MitsubishiFrameType.OneE)]
    [Arguments(MitsubishiFrameType.ThreeE)]
    [Arguments(MitsubishiFrameType.FourE)]
    internal async Task BinaryMcResponsesRoundTripThroughClientAsync(MitsubishiFrameType frameType)
    {
        var options = CreateMcOptions(frameType, CommunicationDataCode.Binary);
        await using var simulator = new MitsubishiSimulatorTransport(
            [MitsubishiSimulatorTransport.CreateSuccessResponse(options, [0x34, 0x12, 0x78, 0x56])]);
        await using var client = new MitsubishiRx(options, simulator, Scheduler.Immediate);

        var result = await client.ReadWordsAsync("D100", MultipleWordCount, CancellationToken.None);

        await Assert.That(result.IsSucceed).IsTrue();
        await Assert.That(result.Value).IsEquivalentTo([(ushort)0x1234, (ushort)0x5678]);
        await Assert.That(simulator.Requests[0].Description).IsEqualTo("Read words D100");
    }

    /// <summary>Verifies ASCII MC framing for each supported Ethernet frame.</summary>
    /// <param name="frameType">The MC frame type.</param>
    /// <returns>A task that represents the asynchronous test.</returns>
    [Test]
    [Arguments(MitsubishiFrameType.OneE)]
    [Arguments(MitsubishiFrameType.ThreeE)]
    [Arguments(MitsubishiFrameType.FourE)]
    internal async Task AsciiMcResponsesRoundTripThroughClientAsync(MitsubishiFrameType frameType)
    {
        var options = CreateMcOptions(frameType, CommunicationDataCode.Ascii);
        var response = MitsubishiSimulatorTransport.CreateSuccessResponse(
            options,
            Encoding.ASCII.GetBytes(AsciiWordPayload));
        await using var simulator = new MitsubishiSimulatorTransport([response]);
        await using var client = new MitsubishiRx(options, simulator, Scheduler.Immediate);

        var result = await client.ReadWordsAsync("D100", MultipleWordCount, CancellationToken.None);

        await Assert.That(result.IsSucceed).IsTrue();
        await Assert.That(result.Value).IsEquivalentTo([(ushort)0x1234, (ushort)0x5678]);
    }

    /// <summary>Verifies serial ASCII and binary framing through the client decoder.</summary>
    /// <param name="frameType">The serial frame type.</param>
    /// <param name="dataCode">The serial data code.</param>
    /// <param name="messageFormat">The serial message format.</param>
    /// <returns>A task that represents the asynchronous test.</returns>
    [Test]
    [Arguments(
        MitsubishiFrameType.OneC,
        CommunicationDataCode.Ascii,
        MitsubishiSerialMessageFormat.Format1)]
    [Arguments(
        MitsubishiFrameType.ThreeC,
        CommunicationDataCode.Ascii,
        MitsubishiSerialMessageFormat.Format4)]
    [Arguments(
        MitsubishiFrameType.FourC,
        CommunicationDataCode.Binary,
        MitsubishiSerialMessageFormat.Format5)]
    internal async Task SerialResponsesRoundTripThroughClientAsync(
        MitsubishiFrameType frameType,
        CommunicationDataCode dataCode,
        MitsubishiSerialMessageFormat messageFormat)
    {
        var options = CreateSerialOptions(frameType, dataCode, messageFormat);
        var decodedPayload = dataCode == CommunicationDataCode.Ascii
            ? Encoding.ASCII.GetBytes(AsciiWordPayload)
            : new byte[] { 0x34, 0x12, 0x78, 0x56 };
        await using var simulator = new MitsubishiSimulatorTransport(
            [MitsubishiSimulatorTransport.CreateSuccessResponse(options, decodedPayload)]);
        await using var client = new MitsubishiRx(options, simulator, Scheduler.Immediate);

        var result = await client.ReadWordsAsync("D100", MultipleWordCount, CancellationToken.None);

        await Assert.That(result.IsSucceed).IsTrue();
        await Assert.That(result.Value).IsEquivalentTo([(ushort)0x1234, (ushort)0x5678]);
    }

    /// <summary>Verifies protocol error response generation and invalid response requests.</summary>
    /// <returns>A task that represents the asynchronous test.</returns>
    [Test]
    internal async Task ProtocolErrorsAndInvalidFramingAreDeterministicAsync()
    {
        var options = CreateMcOptions(MitsubishiFrameType.ThreeE, CommunicationDataCode.Binary);
        await using var simulator = new MitsubishiSimulatorTransport(
            [MitsubishiSimulatorTransport.CreateErrorResponse(options, 0xC051)]);
        await using var client = new MitsubishiRx(options, simulator, Scheduler.Immediate);

        var result = await client.ReadWordsAsync("D100", 1, CancellationToken.None);

        await Assert.That(result.IsSucceed).IsFalse();
        await Assert.That(result.ErrCode).IsEqualTo(0xC051);
        _ = Assert.Throws<ArgumentOutOfRangeException>(
            () => MitsubishiSimulatorTransport.CreateErrorResponse(options, 0));
        _ = Assert.Throws<ArgumentException>(
            () => MitsubishiSimulatorTransport.CreateSuccessResponse(
                CreateMcOptions(MitsubishiFrameType.ThreeE, CommunicationDataCode.Ascii),
                [0x31]));
        _ = Assert.Throws<NotSupportedException>(
            () => MitsubishiSimulatorTransport.CreateSuccessResponse(
                CreateSerialOptions(
                    MitsubishiFrameType.ThreeC,
                    CommunicationDataCode.Binary,
                    MitsubishiSerialMessageFormat.Format5),
                []));
    }

    /// <summary>Verifies simulator factory guardrails and every serial error response shape.</summary>
    /// <returns>A task that represents the asynchronous test.</returns>
    [Test]
    internal async Task FactoryGuardrailsAndSerialErrorsAreDeterministicAsync()
    {
        _ = Assert.Throws<ArgumentNullException>(
            () =>
            {
                var unexpectedSimulator = new MitsubishiSimulatorTransport(
                    (Func<MitsubishiTransportRequest, byte[]>)null!);
                GC.KeepAlive(unexpectedSimulator);
            });

        foreach (var frameType in new[]
                 {
                     MitsubishiFrameType.OneC,
                     MitsubishiFrameType.ThreeC,
                     MitsubishiFrameType.FourC,
                 })
        {
            var serialOptions = CreateSerialOptions(
                frameType,
                CommunicationDataCode.Ascii,
                MitsubishiSerialMessageFormat.Format1);
            var response = MitsubishiSimulatorTransport.CreateErrorResponse(serialOptions, 0x0051);

            await Assert.That(response).IsNotEmpty();
            await Assert.That(response[0]).IsEqualTo((byte)0x15);
        }

        _ = Assert.Throws<ArgumentOutOfRangeException>(
            () => MitsubishiSimulatorTransport.CreateSuccessResponse(
                CreateMcOptions((MitsubishiFrameType)int.MaxValue, CommunicationDataCode.Binary),
                []));
        _ = Assert.Throws<ArgumentOutOfRangeException>(
            () => MitsubishiSimulatorTransport.CreateSuccessResponse(
                CreateMcOptions((MitsubishiFrameType)int.MaxValue, CommunicationDataCode.Ascii),
                []));
        _ = Assert.Throws<ArgumentOutOfRangeException>(
            () => MitsubishiSimulatorTransport.CreateSuccessResponse(
                CreateSerialOptions(
                    (MitsubishiFrameType)int.MaxValue,
                    CommunicationDataCode.Ascii,
                    MitsubishiSerialMessageFormat.Format1),
                []));
        _ = Assert.Throws<NotSupportedException>(
            () => MitsubishiSimulatorTransport.CreateSuccessResponse(
                CreateSerialOptions(
                    MitsubishiFrameType.FourC,
                    CommunicationDataCode.Ascii,
                    (MitsubishiSerialMessageFormat)int.MaxValue),
                []));

        var nullFactorySimulator = new MitsubishiSimulatorTransport(static _ => null!);
        await nullFactorySimulator.ConnectAsync(
            CreateMcOptions(MitsubishiFrameType.ThreeE, CommunicationDataCode.Binary),
            CancellationToken.None);
        _ = Assert.Throws<InvalidOperationException>(
            () => nullFactorySimulator.ExchangeAsync(
                new MitsubishiTransportRequest([], null, SimulationRequestDescription),
                CancellationToken.None).AsTask().GetAwaiter().GetResult());
        await nullFactorySimulator.DisposeAsync();
        await nullFactorySimulator.DisconnectAsync(CancellationToken.None);
    }

    /// <summary>Verifies exchange faults disconnect the simulator and client retries reconnect it.</summary>
    /// <returns>A task that represents the asynchronous test.</returns>
    [Test]
    internal async Task ExchangeFaultTriggersClientReconnectAsync()
    {
        var options = CreateMcOptions(MitsubishiFrameType.ThreeE, CommunicationDataCode.Binary);
        await using var simulator = new MitsubishiSimulatorTransport();
        simulator.EnqueueFault(new IOException("Deterministic link loss."));
        simulator.EnqueueResponse(
            MitsubishiSimulatorTransport.CreateSuccessResponse(options, [0x34, 0x12]));
        await using var client = new MitsubishiRx(options, simulator, Scheduler.Immediate);

        var result = await client.ReadWordsAsync("D100", 1, CancellationToken.None);

        await Assert.That(result.IsSucceed).IsTrue();
        await Assert.That(result.Value).IsEquivalentTo([(ushort)0x1234]);
        await Assert.That(simulator.ConnectCount).IsEqualTo(MultipleWordCount);
        await Assert.That(simulator.Requests.Count).IsEqualTo(MultipleWordCount);
        await Assert.That(simulator.IsConnected).IsTrue();
    }

    /// <summary>Verifies connect faults, cancellation, disconnected exchange, and disposal behavior.</summary>
    /// <returns>A task that represents the asynchronous test.</returns>
    [Test]
    internal async Task DirectFaultCancellationAndDisposalPathsAreDeterministicAsync()
    {
        var options = CreateMcOptions(MitsubishiFrameType.ThreeE, CommunicationDataCode.Binary);
        var simulator = new MitsubishiSimulatorTransport(static _ => [0x01, 0x02, 0x03]);
        simulator.EnqueueConnectFault(new IOException("Deterministic connect failure."));

        _ = Assert.Throws<IOException>(
            () => simulator.ConnectAsync(options, CancellationToken.None).AsTask().GetAwaiter().GetResult());
        _ = Assert.Throws<InvalidOperationException>(
            () => simulator.ExchangeAsync(
                new MitsubishiTransportRequest([], null, SimulationRequestDescription),
                CancellationToken.None).AsTask().GetAwaiter().GetResult());

        await simulator.ConnectAsync(options, CancellationToken.None);
        using var cancelled = new CancellationTokenSource();
        await cancelled.CancelAsync();
        _ = Assert.Throws<OperationCanceledException>(
            () => simulator.ExchangeAsync(
                new MitsubishiTransportRequest([], null, SimulationRequestDescription),
                cancelled.Token).AsTask().GetAwaiter().GetResult());

        await simulator.DisposeAsync();
        simulator.Dispose();

        await Assert.That(simulator.IsConnected).IsFalse();
        _ = Assert.Throws<ObjectDisposedException>(
            () => simulator.ConnectAsync(options, CancellationToken.None).AsTask().GetAwaiter().GetResult());
    }

    /// <summary>Verifies the default automatic acknowledgement supports advanced write/control operations.</summary>
    /// <returns>A task that represents the asynchronous test.</returns>
    [Test]
    internal async Task AutomaticAcknowledgementSupportsAdvancedOperationsAsync()
    {
        var options = CreateMcOptions(MitsubishiFrameType.ThreeE, CommunicationDataCode.Binary);
        await using var simulator = new MitsubishiSimulatorTransport();
        await using var client = new MitsubishiRx(options, simulator, Scheduler.Immediate);

        var write = await client.WriteWordsAsync("D100", [(ushort)0x1234], CancellationToken.None);
        var randomWrite = await client.RandomWriteWordsAsync(
            [new KeyValuePair<string, ushort>("D101", 0x5678)],
            CancellationToken.None);
        var monitor = await client.RegisterMonitorAsync(["D100"], CancellationToken.None);
        var run = await client.RemoteRunAsync(true, false, CancellationToken.None);
        var stop = await client.RemoteStopAsync(CancellationToken.None);

        await Assert.That(write.IsSucceed).IsTrue();
        await Assert.That(randomWrite.IsSucceed).IsTrue();
        await Assert.That(monitor.IsSucceed).IsTrue();
        await Assert.That(run.IsSucceed).IsTrue();
        await Assert.That(stop.IsSucceed).IsTrue();
        await Assert.That(simulator.Requests.Count).IsEqualTo(AdvancedOperationCount);
    }

    /// <summary>Exercises advanced 4C command framing with both ASCII and binary message formats.</summary>
    /// <param name="dataCode">The serial data code.</param>
    /// <param name="messageFormat">The serial message format.</param>
    /// <returns>A task that represents the asynchronous test.</returns>
    [Test]
    [Arguments(CommunicationDataCode.Ascii, MitsubishiSerialMessageFormat.Format1)]
    [Arguments(CommunicationDataCode.Binary, MitsubishiSerialMessageFormat.Format5)]
    internal async Task FourCAdvancedCommandsUseSimulatorResponsesAsync(
        CommunicationDataCode dataCode,
        MitsubishiSerialMessageFormat messageFormat)
    {
        var options = CreateSerialOptions(MitsubishiFrameType.FourC, dataCode, messageFormat);
        var decodedPayload = dataCode == CommunicationDataCode.Ascii
            ? Encoding.ASCII.GetBytes(AsciiWordPayload)
            : new byte[] { 0x34, 0x12, 0x78, 0x56 };
        await using var simulator = new MitsubishiSimulatorTransport(
            _ => MitsubishiSimulatorTransport.CreateSuccessResponse(options, decodedPayload));
        await using var client = new MitsubishiRx(options, simulator, Scheduler.Immediate);
        var blockRequest = new MitsubishiBlockRequest(
            [
                new MitsubishiWordBlock(
                    MitsubishiDeviceAddress.Parse("D100", XyAddressNotation.Octal),
                    new ushort[MultipleWordCount]),
            ],
            [
                new MitsubishiBitBlock(
                    MitsubishiDeviceAddress.Parse("M10", XyAddressNotation.Octal),
                    new bool[MultipleWordCount]),
            ]);

        var writeWords = await client.WriteWordsAsync("D100", [(ushort)0x1234], CancellationToken.None);
        var writeBits = await client.WriteBitsAsync("M10", [true, false], CancellationToken.None);
        var randomRead = await client.RandomReadWordsAsync(["D100", "D101"], CancellationToken.None);
        var randomWrite = await client.RandomWriteWordsAsync(
            [new KeyValuePair<string, ushort>("D100", 0x1234)],
            CancellationToken.None);
        var blockRead = await client.ReadBlocksAsync(blockRequest, CancellationToken.None);
        var blockWrite = await client.WriteBlocksAsync(blockRequest, CancellationToken.None);
        var register = await client.RegisterMonitorAsync(["D100"], CancellationToken.None);
        var monitor = await client.ExecuteMonitorAsync(CancellationToken.None);
        var run = await client.RemoteRunAsync(false, true, CancellationToken.None);
        var pause = await client.RemotePauseAsync(CancellationToken.None);
        var reset = await client.RemoteResetAsync(CancellationToken.None);
        var raw = await client.ExecuteRawAsync(
            new MitsubishiRawCommandRequest(0x0001, 0x0000, [0x31, 0x32], "Raw simulation"),
            CancellationToken.None);

        await AssertAllSucceededAsync(
        [
            writeWords, writeBits, randomRead, randomWrite, blockRead, blockWrite,
            register, monitor, run, pause, reset, raw,
        ]);
    }

    /// <summary>Exercises advanced 4E MC command framing with both ASCII and binary data codes.</summary>
    /// <param name="dataCode">The MC data code.</param>
    /// <returns>A task that represents the asynchronous test.</returns>
    [Test]
    [Arguments(CommunicationDataCode.Ascii)]
    [Arguments(CommunicationDataCode.Binary)]
    internal async Task FourEAdvancedCommandsUseSimulatorResponsesAsync(
        CommunicationDataCode dataCode)
    {
        var options = CreateMcOptions(MitsubishiFrameType.FourE, dataCode);
        var decodedPayload = dataCode == CommunicationDataCode.Ascii
            ? Encoding.ASCII.GetBytes(AsciiWordPayload)
            : new byte[] { 0x34, 0x12, 0x78, 0x56 };
        await using var simulator = new MitsubishiSimulatorTransport(
            _ => MitsubishiSimulatorTransport.CreateSuccessResponse(options, decodedPayload));
        await using var client = new MitsubishiRx(options, simulator, Scheduler.Immediate);
        var blockRequest = new MitsubishiBlockRequest(
            [
                new MitsubishiWordBlock(
                    MitsubishiDeviceAddress.Parse("D100", XyAddressNotation.Octal),
                    new ushort[MultipleWordCount]),
            ],
            []);

        var randomRead = await client.RandomReadWordsAsync(["D100", "D101"], CancellationToken.None);
        var randomWrite = await client.RandomWriteWordsAsync(
            [new KeyValuePair<string, ushort>("D100", 0x1234)],
            CancellationToken.None);
        var blockRead = await client.ReadBlocksAsync(blockRequest, CancellationToken.None);
        var blockWrite = await client.WriteBlocksAsync(blockRequest, CancellationToken.None);
        var register = await client.RegisterMonitorAsync(["D100"], CancellationToken.None);
        var monitor = await client.ExecuteMonitorAsync(CancellationToken.None);
        var typeName = await client.ReadTypeNameAsync(CancellationToken.None);
        var run = await client.RemoteRunAsync(false, true, CancellationToken.None);
        var stop = await client.RemoteStopAsync(CancellationToken.None);
        var pause = await client.RemotePauseAsync(CancellationToken.None);
        var latchClear = await client.RemoteLatchClearAsync(CancellationToken.None);
        var reset = await client.RemoteResetAsync(CancellationToken.None);
        var unlock = await client.UnlockAsync("1234", CancellationToken.None);
        var @lock = await client.LockAsync("1234", CancellationToken.None);
        var clear = await client.ClearErrorAsync(CancellationToken.None);
        var memoryRead = await client.ReadMemoryAsync(
            MitsubishiCommands.MemoryRead,
            0x0100,
            MultipleWordCount,
            CancellationToken.None);
        var memoryWrite = await client.WriteMemoryAsync(
            MitsubishiCommands.MemoryWrite,
            0x0100,
            [(ushort)0x1234],
            CancellationToken.None);

        await AssertAllSucceededAsync(
        [
            randomRead, randomWrite, blockRead, blockWrite, register, monitor, typeName,
            run, stop, pause, latchClear, reset, unlock, @lock, clear, memoryRead, memoryWrite,
        ]);
    }

    /// <summary>Verifies the public memory image is deterministic, detached, and kind-safe.</summary>
    /// <returns>A task that represents the asynchronous test.</returns>
    [Test]
    internal async Task StatefulMemoryImageSupportsSeedingSnapshotsAndResetAsync()
    {
        var memory = new MitsubishiSimulatorMemory();

        await Assert.That(memory.ReadWords("D100", MultipleWordCount))
            .IsEquivalentTo([(ushort)0, (ushort)0]);
        await Assert.That(memory.ReadBits("M10", MultipleWordCount))
            .IsEquivalentTo([false, false]);

        memory.WriteWords("D100", [(ushort)0x1234, (ushort)0x5678]);
        memory.WriteBits("M10", [true, false]);
        memory.WriteBit("M12", true);
        memory.WriteBit("X10", true, XyAddressNotation.Hexadecimal);
        var snapshot = memory.Snapshot();

        await Assert.That(memory.Version).IsEqualTo(MemorySeedWriteCount);
        await Assert.That(memory.ReadWord("D101")).IsEqualTo((ushort)0x5678);
        await Assert.That(memory.ReadBit("M10")).IsTrue();
        await Assert.That(memory.ReadBit("M12")).IsTrue();
        await Assert.That(memory.ReadBit("X10", XyAddressNotation.Hexadecimal)).IsTrue();
        await Assert.That(snapshot.Count).IsEqualTo(ExpandedSnapshotCount);
        await Assert.That(snapshot[0].Symbol).IsEqualTo("D");
        await Assert.That(snapshot[^1].Symbol).IsEqualTo("X");

        memory.WriteWord("D100", 0);
        await Assert.That(snapshot[0].Value).IsEqualTo((ushort)0x1234);
        _ = Assert.Throws<ArgumentException>(() => memory.ReadWords("M10", 1));
        _ = Assert.Throws<ArgumentException>(() => memory.WriteBits("D100", [true]));
        _ = Assert.Throws<ArgumentOutOfRangeException>(() => memory.ReadWords("D100", 0));
        _ = Assert.Throws<ArgumentException>(() => memory.WriteWords("D100", []));
        _ = Assert.Throws<ArgumentException>(() => memory.WriteBits("M10", []));
        var negativeAddress = new MitsubishiDeviceAddress(
            "D",
            -1,
            XyAddressNotation.Octal,
            "D-1");
        _ = Assert.Throws<ArgumentOutOfRangeException>(() =>
            memory.ReadWords(negativeAddress, 1));

        memory.Clear();
        memory.Clear();

        await Assert.That(memory.Snapshot()).IsEmpty();
        await Assert.That(memory.Version).IsEqualTo(ExpandedSnapshotCount);
    }

    /// <summary>Verifies stateful batch word and bit round trips for every MC framing family.</summary>
    /// <param name="frameType">The MC frame type.</param>
    /// <param name="dataCode">The MC data code.</param>
    /// <returns>A task that represents the asynchronous test.</returns>
    [Test]
    [Arguments(MitsubishiFrameType.OneE, CommunicationDataCode.Binary)]
    [Arguments(MitsubishiFrameType.OneE, CommunicationDataCode.Ascii)]
    [Arguments(MitsubishiFrameType.ThreeE, CommunicationDataCode.Binary)]
    [Arguments(MitsubishiFrameType.ThreeE, CommunicationDataCode.Ascii)]
    [Arguments(MitsubishiFrameType.FourE, CommunicationDataCode.Binary)]
    [Arguments(MitsubishiFrameType.FourE, CommunicationDataCode.Ascii)]
    internal async Task StatefulMcBatchOperationsRoundTripAsync(
        MitsubishiFrameType frameType,
        CommunicationDataCode dataCode)
    {
        var options = CreateMcOptions(frameType, dataCode);
        var memory = new MitsubishiSimulatorMemory();
        memory.WriteWords("D100", [(ushort)0x1234, (ushort)0x5678]);
        memory.WriteBits("M10", [true, false, true, false]);
        await using var simulator = new MitsubishiSimulatorTransport(memory);
        await using var client = new MitsubishiRx(options, simulator, Scheduler.Immediate);

        var initialWords = await client.ReadWordsAsync("D100", MultipleWordCount, CancellationToken.None);
        var initialBits = await client.ReadBitsAsync("M10", BatchBitCount, CancellationToken.None);
        var writeWords = await client.WriteWordsAsync(
            "D101",
            [(ushort)0xABCD, (ushort)0xEF01],
            CancellationToken.None);
        var writeBits = await client.WriteBitsAsync(
            "M11",
            [true, true, false],
            CancellationToken.None);
        var updatedWords = await client.ReadWordsAsync("D101", MultipleWordCount, CancellationToken.None);
        var updatedBits = await client.ReadBitsAsync("M11", AdvancedBitCount, CancellationToken.None);

        await Assert.That(initialWords.IsSucceed).IsTrue();
        await Assert.That(initialWords.Value).IsEquivalentTo([(ushort)0x1234, (ushort)0x5678]);
        await Assert.That(initialBits.Value).IsEquivalentTo([true, false, true, false]);
        await Assert.That(writeWords.IsSucceed)
            .IsTrue()
            .Because($"{writeWords.Exception} Response: {writeWords.Response}");
        await Assert.That(writeBits.IsSucceed)
            .IsTrue()
            .Because($"{writeBits.Err} Response: {writeBits.Response}");
        await Assert.That(updatedWords.Value).IsEquivalentTo([(ushort)0xABCD, (ushort)0xEF01]);
        await Assert.That(updatedBits.Value).IsEquivalentTo([true, true, false]);
        await Assert.That(simulator.Memory).IsSameReferenceAs(memory);
    }

    /// <summary>Verifies stateful serial batch word and bit round trips.</summary>
    /// <param name="frameType">The serial frame type.</param>
    /// <param name="dataCode">The serial data code.</param>
    /// <param name="messageFormat">The serial message format.</param>
    /// <returns>A task that represents the asynchronous test.</returns>
    [Test]
    [Arguments(
        MitsubishiFrameType.OneC,
        CommunicationDataCode.Ascii,
        MitsubishiSerialMessageFormat.Format1)]
    [Arguments(
        MitsubishiFrameType.ThreeC,
        CommunicationDataCode.Ascii,
        MitsubishiSerialMessageFormat.Format4)]
    [Arguments(
        MitsubishiFrameType.FourC,
        CommunicationDataCode.Ascii,
        MitsubishiSerialMessageFormat.Format1)]
    [Arguments(
        MitsubishiFrameType.FourC,
        CommunicationDataCode.Binary,
        MitsubishiSerialMessageFormat.Format5)]
    internal async Task StatefulSerialBatchOperationsRoundTripAsync(
        MitsubishiFrameType frameType,
        CommunicationDataCode dataCode,
        MitsubishiSerialMessageFormat messageFormat)
    {
        var options = CreateSerialOptions(frameType, dataCode, messageFormat);
        await using var simulator = new MitsubishiSimulatorTransport();
        await using var client = new MitsubishiRx(options, simulator, Scheduler.Immediate);

        var writeWords = await client.WriteWordsAsync(
            "D100",
            [(ushort)0x1234, (ushort)0x5678],
            CancellationToken.None);
        var writeBits = await client.WriteBitsAsync(
            "M10",
            [true, false, true, false],
            CancellationToken.None);
        var readWords = await client.ReadWordsAsync("D100", MultipleWordCount, CancellationToken.None);
        var readBits = await client.ReadBitsAsync("M10", BatchBitCount, CancellationToken.None);

        await Assert.That(writeWords.IsSucceed)
            .IsTrue()
            .Because($"{writeWords.Exception} Response: {writeWords.Response}");
        await Assert.That(writeBits.IsSucceed)
            .IsTrue()
            .Because($"{writeBits.Err} Response: {writeBits.Response}");
        await Assert.That(readWords.Value)
            .IsEquivalentTo([(ushort)0x1234, (ushort)0x5678])
            .Because($"{readWords.Exception} Response: {readWords.Response}");
        await Assert.That(readBits.Value).IsEquivalentTo([true, false, true, false]);
    }

    /// <summary>Verifies advanced MC commands operate against persistent simulator state.</summary>
    /// <param name="dataCode">The MC data code.</param>
    /// <returns>A task that represents the asynchronous test.</returns>
    [Test]
    [Arguments(CommunicationDataCode.Binary)]
    [Arguments(CommunicationDataCode.Ascii)]
    internal async Task StatefulAdvancedMcOperationsUsePersistentStateAsync(
        CommunicationDataCode dataCode)
    {
        var options = CreateMcOptions(MitsubishiFrameType.FourE, dataCode);
        await using var simulator = new MitsubishiSimulatorTransport
        {
            ModelName = "SIM-Q",
            ModelCode = 0x4321,
        };
        simulator.Memory.WriteWords("D100", [(ushort)0x1001, (ushort)0x1002]);
        simulator.SetControllerError(0xC051);
        await using var client = new MitsubishiRx(options, simulator, Scheduler.Immediate);

        var randomRead = await client.RandomReadWordsAsync(["D100", "D101"], CancellationToken.None);
        var randomWrite = await client.RandomWriteWordsAsync(
            [
                new KeyValuePair<string, ushort>("D100", 0x2001),
                new KeyValuePair<string, ushort>("D101", 0x2002),
            ],
            CancellationToken.None);
        var blockWrite = await client.WriteBlocksAsync(
            CreateAdvancedWriteBlockRequest(),
            CancellationToken.None);
        var blockRead = await client.ReadBlocksAsync(
            CreateAdvancedReadBlockRequest(),
            CancellationToken.None);
        var register = await client.RegisterMonitorAsync(["D100", "D101"], CancellationToken.None);
        var monitor = await client.ExecuteMonitorAsync(CancellationToken.None);
        var (memoryWrite, memoryRead) = await ExecuteAdvancedMemoryRoundTripAsync(client);
        var typeName = await client.ReadTypeNameAsync(CancellationToken.None);
        var stop = await client.RemoteStopAsync(CancellationToken.None);
        var run = await client.RemoteRunAsync(true, false, CancellationToken.None);
        var clear = await client.ClearErrorAsync(CancellationToken.None);
        var loopback = await client.LoopbackAsync([0x41, 0x42], CancellationToken.None);

        await Assert.That(randomRead.Value).IsEquivalentTo([(ushort)0x1001, (ushort)0x1002]);
        await Assert.That(randomWrite.IsSucceed).IsTrue();
        await Assert.That(simulator.Memory.ReadWords("D100", MultipleWordCount))
            .IsEquivalentTo([(ushort)0x2001, (ushort)0x2002]);
        await Assert.That(blockWrite.IsSucceed).IsTrue();
        await Assert.That(blockRead.IsSucceed).IsTrue();
        await Assert.That(simulator.Memory.ReadWords("D110", MultipleWordCount))
            .IsEquivalentTo([(ushort)0x3001, (ushort)0x3002]);
        await Assert.That(simulator.Memory.ReadBits("M20", AdvancedBitCount))
            .IsEquivalentTo([true, false, true]);
        await Assert.That(register.IsSucceed).IsTrue();
        await Assert.That(monitor.IsSucceed).IsTrue();
        await Assert.That(memoryWrite.IsSucceed).IsTrue();
        await Assert.That(memoryRead.Value).IsEquivalentTo([(ushort)0x4001, (ushort)0x4002]);
        await Assert.That(typeName.Value?.ModelName).IsEqualTo("SIM-Q");
        await Assert.That(typeName.Value?.ModelCode).IsEqualTo((ushort)0x4321);
        await Assert.That(stop.IsSucceed).IsTrue();
        await Assert.That(run.IsSucceed).IsTrue();
        await Assert.That(simulator.IsCpuRunning).IsTrue();
        await Assert.That(clear.IsSucceed).IsTrue();
        await Assert.That(simulator.ControllerError).IsEqualTo((ushort)0);
        await Assert.That(loopback.Value).IsEquivalentTo([(byte)0x41, (byte)0x42]);
    }

    /// <summary>Creates the advanced block-write request.</summary>
    /// <returns>The populated word and bit block request.</returns>
    private static MitsubishiBlockRequest CreateAdvancedWriteBlockRequest() =>
        new(
            [
                new MitsubishiWordBlock(
                    MitsubishiDeviceAddress.Parse("D110", XyAddressNotation.Octal),
                    new ushort[] { 0x3001, 0x3002 }),
            ],
            [
                new MitsubishiBitBlock(
                    MitsubishiDeviceAddress.Parse("M20", XyAddressNotation.Octal),
                    new[] { true, false, true }),
            ]);

    /// <summary>Creates the advanced block-read request.</summary>
    /// <returns>The empty word and bit block request.</returns>
    private static MitsubishiBlockRequest CreateAdvancedReadBlockRequest() =>
        new(
            [
                new MitsubishiWordBlock(
                    MitsubishiDeviceAddress.Parse("D110", XyAddressNotation.Octal),
                    new ushort[MultipleWordCount]),
            ],
            [
                new MitsubishiBitBlock(
                    MitsubishiDeviceAddress.Parse("M20", XyAddressNotation.Octal),
                    new bool[AdvancedBitCount]),
            ]);

    /// <summary>Executes the advanced buffer-memory round trip.</summary>
    /// <param name="client">The connected Mitsubishi client.</param>
    /// <returns>The write and read responses.</returns>
    private static async Task<(Responce Write, Responce<ushort[]> Read)>
        ExecuteAdvancedMemoryRoundTripAsync(MitsubishiRx client)
    {
        var write = await client.WriteMemoryAsync(
            MitsubishiCommands.MemoryWrite,
            0x0100,
            [(ushort)0x4001, (ushort)0x4002],
            CancellationToken.None);
        var read = await client.ReadMemoryAsync(
            MitsubishiCommands.MemoryRead,
            0x0100,
            MultipleWordCount,
            CancellationToken.None);
        return (write, read);
    }

    /// <summary>Asserts that every Mitsubishi response succeeded.</summary>
    /// <param name="responses">The responses to verify.</param>
    /// <returns>A task that represents the asynchronous assertions.</returns>
    private static async Task AssertAllSucceededAsync(IEnumerable<Responce> responses)
    {
        foreach (var response in responses)
        {
            await Assert.That(response.IsSucceed).IsTrue();
        }
    }

    /// <summary>Creates deterministic Ethernet MC options.</summary>
    /// <param name="frameType">The frame type.</param>
    /// <param name="dataCode">The data code.</param>
    /// <returns>The client options.</returns>
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

    /// <summary>Creates deterministic serial options.</summary>
    /// <param name="frameType">The frame type.</param>
    /// <param name="dataCode">The data code.</param>
    /// <param name="messageFormat">The serial message format.</param>
    /// <returns>The client options.</returns>
    private static MitsubishiClientOptions CreateSerialOptions(
        MitsubishiFrameType frameType,
        CommunicationDataCode dataCode,
        MitsubishiSerialMessageFormat messageFormat) =>
        new(
            Host: "COM-SIMULATED",
            Port: 0,
            FrameType: frameType,
            DataCode: dataCode,
            TransportKind: MitsubishiTransportKind.Serial,
            CpuType: CpuType.Fx5,
            Serial: new MitsubishiSerialOptions(
                PortName: "COM-SIMULATED",
                BaudRate: 9600,
                DataBits: 7,
                Parity: Parity.Even,
                StopBits: StopBits.One,
                Handshake: Handshake.None,
                MessageFormat: messageFormat));
}
