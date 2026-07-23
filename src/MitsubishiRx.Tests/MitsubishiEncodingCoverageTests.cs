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

/// <summary>Exercises deterministic encoding and decoding guardrails across every frame family.</summary>
internal sealed class MitsubishiEncodingCoverageTests
{
    /// <summary>Stores the number of values used by pair operations.</summary>
    private const int PairCount = 2;

    /// <summary>Stores the representative binary request body length.</summary>
    private const int BinaryBodyLength = 6;

    /// <summary>Stores the explicit response length.</summary>
    private const int ExplicitResponseLength = 17;

    /// <summary>Stores a deliberately incomplete ASCII serial response length.</summary>
    private const int IncompleteAsciiLength = 4;

    /// <summary>Stores the deterministic MC port.</summary>
    private const int McPort = 5000;

    /// <summary>Stores the deterministic serial baud rate.</summary>
    private const int SerialBaudRate = 9600;

    /// <summary>Stores the deterministic serial data-bit count.</summary>
    private const int SerialDataBits = 7;

    /// <summary>Stores the representative word address.</summary>
    private static readonly MitsubishiDeviceAddress WordAddress =
        MitsubishiDeviceAddress.Parse("D100", XyAddressNotation.Octal);

    /// <summary>Stores the representative bit address.</summary>
    private static readonly MitsubishiDeviceAddress BitAddress =
        MitsubishiDeviceAddress.Parse("M10", XyAddressNotation.Octal);

    /// <summary>Stores a request containing word and bit blocks.</summary>
    private static readonly MitsubishiBlockRequest Blocks = new(
        [
            new MitsubishiWordBlock(
                WordAddress,
                new ushort[] { 0x1234, 0x5678 }),
        ],
        [
            new MitsubishiBitBlock(BitAddress, new bool[] { true, false }),
        ]);

    /// <summary>Exercises every serial entry point for 1C and the remaining 4C ASCII reads.</summary>
    /// <returns>A task that represents the asynchronous test.</returns>
    [Test]
    internal async Task SerialEntryPointsExerciseLegacyAndModernFramesAsync()
    {
        var oneC = CreateSerialOptions(
            MitsubishiFrameType.OneC,
            CommunicationDataCode.Ascii,
            MitsubishiSerialMessageFormat.Format1);
        var fourCAscii = CreateSerialOptions(
            MitsubishiFrameType.FourC,
            CommunicationDataCode.Ascii,
            MitsubishiSerialMessageFormat.Format4);
        var deviceValues = new[] { new MitsubishiDeviceValue(WordAddress, 0x1234) };

        var encoded = new List<byte[]>
        {
            MitsubishiSerialProtocolEncoding.EncodeWordReadRequest(oneC, WordAddress, PairCount),
            MitsubishiSerialProtocolEncoding.EncodeWordWriteRequest(oneC, WordAddress, [0x1234]),
            MitsubishiSerialProtocolEncoding.EncodeBitReadRequest(oneC, BitAddress, PairCount),
            MitsubishiSerialProtocolEncoding.EncodeBitWriteRequest(oneC, BitAddress, [true, false]),
            MitsubishiSerialProtocolEncoding.EncodeRandomReadRequest(oneC, [WordAddress]),
            MitsubishiSerialProtocolEncoding.EncodeRandomWriteRequest(oneC, deviceValues),
            MitsubishiSerialProtocolEncoding.EncodeBlockReadRequest(oneC, Blocks),
            MitsubishiSerialProtocolEncoding.EncodeBlockWriteRequest(oneC, Blocks),
            MitsubishiSerialProtocolEncoding.EncodeMonitorRegistrationRequest(oneC, [WordAddress]),
            MitsubishiSerialProtocolEncoding.EncodeExecuteMonitorRequest(oneC),
            MitsubishiSerialProtocolEncoding.EncodeRemoteOperationRequest(
                oneC,
                MitsubishiCommandCodes.RemoteRun,
                force: false,
                clearMode: true),
            MitsubishiSerialProtocolEncoding.EncodeReadTypeNameRequest(oneC),
            MitsubishiSerialProtocolEncoding.EncodeLoopbackRequest(oneC, [0x31, 0x32]),
            MitsubishiSerialProtocolEncoding.EncodeMemoryAccessRequest(
                oneC,
                MitsubishiCommands.MemoryRead,
                0x0100,
                1,
                []),
            MitsubishiSerialProtocolEncoding.EncodeMemoryAccessRequest(
                oneC,
                MitsubishiCommands.MemoryWrite,
                0x0100,
                1,
                [(ushort)0x1234]),
            MitsubishiSerialProtocolEncoding.EncodeRawRequest(
                oneC,
                new MitsubishiRawCommandRequest(0x0001, 0x0000, [0x31])),
            MitsubishiSerialProtocolEncoding.EncodeWordReadRequest(
                fourCAscii,
                WordAddress,
                PairCount),
            MitsubishiSerialProtocolEncoding.EncodeBitReadRequest(
                fourCAscii,
                BitAddress,
                PairCount),
        };

        foreach (var request in encoded)
        {
            await Assert.That(request).IsNotEmpty();
        }
    }

    /// <summary>Exercises serial empty-input guardrails.</summary>
    /// <returns>A task that represents the asynchronous test.</returns>
    [Test]
    internal Task SerialEmptyInputsThrowAsync()
    {
        var oneC = CreateSerialOptions(
            MitsubishiFrameType.OneC,
            CommunicationDataCode.Ascii,
            MitsubishiSerialMessageFormat.Format1);

        _ = Assert.Throws<ArgumentException>(
            () => MitsubishiSerialProtocolEncoding.EncodeWordWriteRequest(
                oneC,
                WordAddress,
                []));
        _ = Assert.Throws<ArgumentException>(
            () => MitsubishiSerialProtocolEncoding.EncodeBitWriteRequest(
                oneC,
                BitAddress,
                []));
        _ = Assert.Throws<ArgumentException>(
            () => MitsubishiSerialProtocolEncoding.EncodeRandomReadRequest(oneC, []));
        _ = Assert.Throws<ArgumentException>(
            () => MitsubishiSerialProtocolEncoding.EncodeRandomWriteRequest(oneC, []));
        _ = Assert.Throws<ArgumentException>(
            () => MitsubishiSerialProtocolEncoding.EncodeMonitorRegistrationRequest(oneC, []));
        _ = Assert.Throws<ArgumentException>(
            () => MitsubishiSerialProtocolEncoding.EncodeLoopbackRequest(oneC, []));
        return Task.CompletedTask;
    }

    /// <summary>Exercises each serial invalid-frame branch.</summary>
    /// <returns>A task that represents the asynchronous test.</returns>
    [Test]
    internal Task SerialInvalidFramesThrowAsync()
    {
        var invalid = CreateSerialOptions(
            (MitsubishiFrameType)int.MaxValue,
            CommunicationDataCode.Ascii,
            MitsubishiSerialMessageFormat.Format1);
        var invalidFormat = CreateSerialOptions(
            MitsubishiFrameType.FourC,
            CommunicationDataCode.Ascii,
            (MitsubishiSerialMessageFormat)int.MaxValue);
        var deviceValues = new[] { new MitsubishiDeviceValue(WordAddress, 0x1234) };
        var raw = new MitsubishiRawCommandRequest(0x0001, 0x0000, [0x31]);
        var invalidCalls = new Action[]
        {
            () => MitsubishiSerialProtocolEncoding.EncodeWordReadRequest(invalid, WordAddress, 1),
            () => MitsubishiSerialProtocolEncoding.EncodeWordWriteRequest(invalid, WordAddress, [0x1234]),
            () => MitsubishiSerialProtocolEncoding.EncodeBitReadRequest(invalid, BitAddress, 1),
            () => MitsubishiSerialProtocolEncoding.EncodeBitWriteRequest(invalid, BitAddress, [true]),
            () => MitsubishiSerialProtocolEncoding.EncodeRandomReadRequest(invalid, [WordAddress]),
            () => MitsubishiSerialProtocolEncoding.EncodeRandomWriteRequest(invalid, deviceValues),
            () => MitsubishiSerialProtocolEncoding.EncodeBlockReadRequest(invalid, Blocks),
            () => MitsubishiSerialProtocolEncoding.EncodeBlockWriteRequest(invalid, Blocks),
            () => MitsubishiSerialProtocolEncoding.EncodeMonitorRegistrationRequest(invalid, [WordAddress]),
            () => MitsubishiSerialProtocolEncoding.EncodeExecuteMonitorRequest(invalid),
            () => MitsubishiSerialProtocolEncoding.EncodeRemoteOperationRequest(
                invalid,
                MitsubishiCommandCodes.RemoteStop),
            () => MitsubishiSerialProtocolEncoding.EncodeReadTypeNameRequest(invalid),
            () => MitsubishiSerialProtocolEncoding.EncodeLoopbackRequest(invalid, [0x31]),
            () => MitsubishiSerialProtocolEncoding.EncodeMemoryAccessRequest(
                invalid,
                MitsubishiCommands.MemoryRead,
                0,
                1,
                []),
            () => MitsubishiSerialProtocolEncoding.EncodeRawRequest(invalid, raw),
            () => MitsubishiSerialProtocolEncoding.Decode(invalid, []),
        };
        foreach (var invalidCall in invalidCalls)
        {
            _ = Assert.Throws<NotSupportedException>(invalidCall);
        }

        _ = Assert.Throws<NotSupportedException>(
            () => MitsubishiSerialProtocolEncoding.EncodeWordReadRequest(
                invalidFormat,
                WordAddress,
                1));
        return Task.CompletedTask;
    }

    /// <summary>Exercises serial frame-completion decisions.</summary>
    /// <returns>A task that represents the asynchronous test.</returns>
    [Test]
    internal async Task SerialCompletionDecisionsAreCoveredAsync()
    {
        var invalid = CreateSerialOptions(
            (MitsubishiFrameType)int.MaxValue,
            CommunicationDataCode.Ascii,
            MitsubishiSerialMessageFormat.Format1);
        var invalidFormat = CreateSerialOptions(
            MitsubishiFrameType.FourC,
            CommunicationDataCode.Ascii,
            (MitsubishiSerialMessageFormat)int.MaxValue);
        var oneC = CreateSerialOptions(
            MitsubishiFrameType.OneC,
            CommunicationDataCode.Ascii,
            MitsubishiSerialMessageFormat.Format1);
        var threeCFormat4 = CreateSerialOptions(
            MitsubishiFrameType.ThreeC,
            CommunicationDataCode.Ascii,
            MitsubishiSerialMessageFormat.Format4);
        var fourCBinary = CreateSerialOptions(
            MitsubishiFrameType.FourC,
            CommunicationDataCode.Binary,
            MitsubishiSerialMessageFormat.Format5);
        var oneCResponse = MitsubishiSimulatorTransport.CreateSuccessResponse(oneC, [0x31, 0x32]);
        var threeCResponse = MitsubishiSimulatorTransport.CreateSuccessResponse(
            threeCFormat4,
            [0x31, 0x32]);
        var fourCResponse = MitsubishiSimulatorTransport.CreateSuccessResponse(
            fourCBinary,
            [0x34, 0x12]);

        await Assert.That(MitsubishiSerialProtocolEncoding.IsExpectedFrameComplete(oneC, []))
            .IsFalse();
        await Assert.That(
                MitsubishiSerialProtocolEncoding.IsExpectedFrameComplete(
                    oneC,
                    oneCResponse.AsSpan(
                        0,
                        Math.Min(IncompleteAsciiLength, oneCResponse.Length))))
            .IsFalse();
        await Assert.That(MitsubishiSerialProtocolEncoding.IsExpectedFrameComplete(oneC, oneCResponse))
            .IsTrue();
        await Assert.That(
                MitsubishiSerialProtocolEncoding.IsExpectedFrameComplete(
                    threeCFormat4,
                    threeCResponse))
            .IsTrue();
        await Assert.That(
                MitsubishiSerialProtocolEncoding.IsExpectedFrameComplete(
                    fourCBinary,
                    fourCResponse))
            .IsTrue();
        await Assert.That(
                MitsubishiSerialProtocolEncoding.IsExpectedFrameComplete(invalid, oneCResponse))
            .IsFalse();
        await Assert.That(
                MitsubishiSerialProtocolEncoding.IsExpectedFrameComplete(invalidFormat, oneCResponse))
            .IsFalse();
    }

    /// <summary>Exercises MC legacy and modern encoding variants.</summary>
    /// <returns>A task that represents the asynchronous test.</returns>
    [Test]
    internal async Task McEncodingVariantsAreCoveredAsync()
    {
        var oneEBinary = CreateMcOptions(MitsubishiFrameType.OneE, CommunicationDataCode.Binary);
        var oneEAscii = CreateMcOptions(MitsubishiFrameType.OneE, CommunicationDataCode.Ascii);
        var threeEBinary = CreateMcOptions(MitsubishiFrameType.ThreeE, CommunicationDataCode.Binary);
        var threeEAscii = CreateMcOptions(MitsubishiFrameType.ThreeE, CommunicationDataCode.Ascii);
        var requests = new[]
        {
            MitsubishiProtocolEncoding.EncodeDeviceBatchRead(oneEAscii, WordAddress, 1, false),
            MitsubishiProtocolEncoding.EncodeDeviceBatchRead(oneEBinary, BitAddress, 1, true),
            MitsubishiProtocolEncoding.EncodeDeviceBatchWrite(
                oneEAscii,
                WordAddress,
                [(ushort)0x1234],
                false),
            MitsubishiProtocolEncoding.EncodeDeviceBatchWrite(
                oneEBinary,
                BitAddress,
                [(ushort)1, (ushort)0],
                true),
            MitsubishiProtocolEncoding.EncodeLoopback(oneEAscii, [0x31, 0x32]),
            MitsubishiProtocolEncoding.EncodeLoopback(oneEBinary, [0x31, 0x32]),
            MitsubishiProtocolEncoding.EncodeLoopback(threeEAscii, [0x31, 0x32]),
            MitsubishiProtocolEncoding.EncodeLoopback(threeEBinary, [0x31, 0x32]),
            MitsubishiProtocolEncoding.EncodeRemoteOperation(
                oneEAscii,
                MitsubishiCommandCodes.RemoteRun,
                force: false,
                clearMode: true),
        };

        foreach (var encoded in requests)
        {
            await Assert.That(encoded).IsNotEmpty();
        }
    }

    /// <summary>Exercises MC validation and unsupported legacy operations.</summary>
    /// <returns>A task that represents the asynchronous test.</returns>
    [Test]
    internal Task McGuardrailsAreCoveredAsync()
    {
        var oneEBinary = CreateMcOptions(MitsubishiFrameType.OneE, CommunicationDataCode.Binary);
        var threeEBinary = CreateMcOptions(MitsubishiFrameType.ThreeE, CommunicationDataCode.Binary);
        var invalid = CreateMcOptions(
            (MitsubishiFrameType)int.MaxValue,
            CommunicationDataCode.Binary);
        var request = new MitsubishiTransportRequest([], null, "Decode coverage");

        _ = Assert.Throws<ArgumentException>(
            () => MitsubishiProtocolEncoding.EncodeDeviceBatchWrite(
                threeEBinary,
                WordAddress,
                [],
                false));
        _ = Assert.Throws<ArgumentException>(
            () => MitsubishiProtocolEncoding.EncodeRandomRead(threeEBinary, []));
        _ = Assert.Throws<ArgumentException>(
            () => MitsubishiProtocolEncoding.EncodeRandomWrite(threeEBinary, []));
        _ = Assert.Throws<ArgumentException>(
            () => MitsubishiProtocolEncoding.EncodeMonitorRegistration(threeEBinary, []));
        _ = Assert.Throws<ArgumentException>(
            () => MitsubishiProtocolEncoding.EncodeLoopback(threeEBinary, []));
        _ = Assert.Throws<ArgumentException>(
            () => MitsubishiProtocolEncoding.EncodeRemotePassword(
                threeEBinary,
                MitsubishiCommandCodes.Unlock,
                " "));
        _ = Assert.Throws<ArgumentOutOfRangeException>(
            () => MitsubishiProtocolEncoding.EncodeRemoteOperation(threeEBinary, 0xFFFF));
        _ = Assert.Throws<ArgumentOutOfRangeException>(
            () => MitsubishiProtocolEncoding.Encode(
                invalid,
                new MitsubishiRawCommandRequest(1, 0)));
        _ = Assert.Throws<ArgumentOutOfRangeException>(
            () => MitsubishiProtocolEncoding.Decode(invalid, request, []));

        _ = Assert.Throws<NotSupportedException>(
            () => MitsubishiProtocolEncoding.EncodeRandomRead(oneEBinary, [WordAddress]));
        _ = Assert.Throws<NotSupportedException>(
            () => MitsubishiProtocolEncoding.EncodeRandomWrite(
                oneEBinary,
                [new MitsubishiDeviceValue(WordAddress, 0x1234)]));
        _ = Assert.Throws<NotSupportedException>(
            () => MitsubishiProtocolEncoding.EncodeMonitorRegistration(oneEBinary, [WordAddress]));
        _ = Assert.Throws<NotSupportedException>(
            () => MitsubishiProtocolEncoding.EncodeBlockRead(oneEBinary, Blocks));
        _ = Assert.Throws<NotSupportedException>(
            () => MitsubishiProtocolEncoding.EncodeBlockWrite(oneEBinary, Blocks));
        _ = Assert.Throws<NotSupportedException>(
            () => MitsubishiProtocolEncoding.EncodeMemoryAccess(
                oneEBinary,
                MitsubishiCommands.MemoryRead,
                0,
                1,
                []));
        return Task.CompletedTask;
    }

    /// <summary>Exercises deterministic decoder failures and fixed-length decisions.</summary>
    /// <returns>A task that represents the asynchronous test.</returns>
    [Test]
    internal async Task McDecoderFailuresAndLengthsAreCoveredAsync()
    {
        var oneEBinary = CreateMcOptions(MitsubishiFrameType.OneE, CommunicationDataCode.Binary);
        var oneEAscii = CreateMcOptions(MitsubishiFrameType.OneE, CommunicationDataCode.Ascii);
        var threeEBinary = CreateMcOptions(MitsubishiFrameType.ThreeE, CommunicationDataCode.Binary);
        var threeEAscii = CreateMcOptions(MitsubishiFrameType.ThreeE, CommunicationDataCode.Ascii);
        var request = new MitsubishiTransportRequest([], null, "Decode coverage");
        var decodeResults = new[]
        {
            MitsubishiProtocolEncoding.Decode(oneEBinary, request, [0x81]),
            MitsubishiProtocolEncoding.Decode(oneEBinary, request, [0x81, 0x5B]),
            MitsubishiProtocolEncoding.Decode(oneEAscii, request, Encoding.ASCII.GetBytes("81")),
            MitsubishiProtocolEncoding.Decode(oneEAscii, request, Encoding.ASCII.GetBytes("815B")),
            MitsubishiProtocolEncoding.Decode(threeEBinary, request, [0xD0, 0x00]),
            MitsubishiProtocolEncoding.Decode(
                threeEBinary,
                request,
                [0xD4, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00]),
            MitsubishiProtocolEncoding.Decode(
                threeEBinary,
                request,
                [0xD0, 0x00, 0x00, 0xFF, 0xFF, 0x03, 0x00, 0x04, 0x00, 0x00, 0x00]),
            MitsubishiProtocolEncoding.Decode(
                threeEAscii,
                request,
                Encoding.ASCII.GetBytes("D000")),
            MitsubishiProtocolEncoding.Decode(
                threeEAscii,
                request,
                Encoding.ASCII.GetBytes("D00000FF03FF000004000012")),
        };
        foreach (var result in decodeResults)
        {
            await Assert.That(result.IsSucceed).IsFalse();
        }

        await AssertFixedLengthsAsync();
    }

    /// <summary>Verifies explicit and inferred fixed response lengths.</summary>
    /// <returns>A task that represents the asynchronous assertions.</returns>
    private static async Task AssertFixedLengthsAsync()
    {
        await Assert.That(
                MitsubishiProtocolEncoding.GetFixedResponseLength(
                    MitsubishiFrameType.OneE,
                    CommunicationDataCode.Binary,
                    MitsubishiCommands.DeviceRead,
                    0,
                    BinaryBodyLength))
            .IsNotNull();
        await Assert.That(
                MitsubishiProtocolEncoding.GetFixedResponseLength(
                    MitsubishiFrameType.OneE,
                    CommunicationDataCode.Ascii,
                    MitsubishiCommands.LoopbackTest,
                    0,
                    PairCount))
            .IsEqualTo(BinaryBodyLength);
        await Assert.That(
                MitsubishiProtocolEncoding.GetFixedResponseLength(
                    MitsubishiFrameType.ThreeE,
                    CommunicationDataCode.Binary,
                    0,
                    0,
                    0,
                    ExplicitResponseLength))
            .IsEqualTo(ExplicitResponseLength);
    }

    /// <summary>Creates deterministic MC options.</summary>
    /// <param name="frameType">The frame type.</param>
    /// <param name="dataCode">The data code.</param>
    /// <returns>The client options.</returns>
    private static MitsubishiClientOptions CreateMcOptions(
        MitsubishiFrameType frameType,
        CommunicationDataCode dataCode) =>
        new(
            "127.0.0.1",
            McPort,
            frameType,
            dataCode,
            MitsubishiTransportKind.Tcp,
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
            "COM-SIMULATED",
            0,
            frameType,
            dataCode,
            MitsubishiTransportKind.Serial,
            CpuType: CpuType.Fx5,
            Serial: new MitsubishiSerialOptions(
                "COM-SIMULATED",
                SerialBaudRate,
                SerialDataBits,
                Parity.Even,
                StopBits.One,
                Handshake.None,
                MessageFormat: messageFormat));
}
