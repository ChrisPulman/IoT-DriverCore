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

/// <summary>Exercises residual MC and serial protocol encoding validation paths.</summary>
internal sealed class MitsubishiProtocolSerialResidualCoverageTests
{
    /// <summary>Stores the ASCII checksum width in characters.</summary>
    private const int ChecksumCharacterCount = 2;

    /// <summary>Stores the deterministic MC endpoint port.</summary>
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

    /// <summary>Stores a two-character device-symbol address.</summary>
    private static readonly MitsubishiDeviceAddress ExtendedWordAddress =
        MitsubishiDeviceAddress.Parse("ZR100", XyAddressNotation.Octal);

    /// <summary>Exercises MC decoder error and ASCII payload-boundary validation.</summary>
    /// <returns>A task that represents the asynchronous test.</returns>
    [Test]
    internal async Task McDecoderValidatesCompleteErrorAndAdjustedAsciiPayloadFramesAsync()
    {
        var binaryOneE = CreateMcOptions(MitsubishiFrameType.OneE, CommunicationDataCode.Binary);
        var asciiOneE = CreateMcOptions(MitsubishiFrameType.OneE, CommunicationDataCode.Ascii);
        var asciiThreeE = CreateMcOptions(MitsubishiFrameType.ThreeE, CommunicationDataCode.Ascii);
        var request = new MitsubishiTransportRequest([], null, "Residual decode validation");

        var binaryError = MitsubishiProtocolEncoding.Decode(binaryOneE, request, [0x81, 0x5B, 0x34, 0x12]);
        var asciiError = MitsubishiProtocolEncoding.Decode(
            asciiOneE,
            request,
            Encoding.ASCII.GetBytes("815B1234"));
        var modernError = MitsubishiProtocolEncoding.Decode(
            asciiThreeE,
            request,
            Encoding.ASCII.GetBytes("D00000FF03FF0000020034"));
        var missingPayloadPrefix = MitsubishiProtocolEncoding.Decode(
            asciiThreeE,
            request,
            Encoding.ASCII.GetBytes("D00000FF03FF0000040000"));
        var extraPayloadPrefix = MitsubishiProtocolEncoding.Decode(
            asciiThreeE,
            request,
            Encoding.ASCII.GetBytes("D00000FF03FF0000040000CAFEDEAD"));

        await Assert.That(binaryError.IsSucceed).IsFalse();
        await Assert.That(binaryError.ErrCode).IsEqualTo(0x1234);
        await Assert.That(asciiError.IsSucceed).IsFalse();
        await Assert.That(asciiError.ErrCode).IsEqualTo(0x1234);
        await Assert.That(modernError.IsSucceed).IsFalse();
        await Assert.That(modernError.ErrCode).IsEqualTo(0x0034);
        await Assert.That(missingPayloadPrefix.IsSucceed).IsTrue();
        await Assert.That(Encoding.ASCII.GetString(missingPayloadPrefix.Value!)).IsEqualTo("0000");
        await Assert.That(extraPayloadPrefix.IsSucceed).IsTrue();
        await Assert.That(Encoding.ASCII.GetString(extraPayloadPrefix.Value!)).IsEqualTo("DEAD");
    }

    /// <summary>Exercises MC response-length fallbacks and legacy address encodings.</summary>
    /// <returns>A task that represents the asynchronous test.</returns>
    [Test]
    internal async Task McLengthFallbacksAndLegacyAddressFormsAreDeterministicAsync()
    {
        var binaryOneE = CreateMcOptions(MitsubishiFrameType.OneE, CommunicationDataCode.Binary);
        var asciiOneE = CreateMcOptions(MitsubishiFrameType.OneE, CommunicationDataCode.Ascii);

        var binary = MitsubishiProtocolEncoding.EncodeDeviceBatchRead(binaryOneE, WordAddress, 1, false);
        var ascii = MitsubishiProtocolEncoding.EncodeDeviceBatchRead(asciiOneE, WordAddress, 1, false);

        await Assert.That(binary).IsNotEmpty();
        await Assert.That(ascii).IsNotEmpty();
        await Assert.That(
                MitsubishiProtocolEncoding.GetFixedResponseLength(
                    MitsubishiFrameType.OneE,
                    CommunicationDataCode.Binary,
                    0xFFFF,
                    0,
                    0))
            .IsNull();
        await Assert.That(
                MitsubishiProtocolEncoding.GetFixedResponseLength(
                    MitsubishiFrameType.OneE,
                    CommunicationDataCode.Ascii,
                    MitsubishiCommandCodes.DeviceRead,
                    0,
                    -1))
            .IsNull();
        await Assert.That(
                MitsubishiProtocolEncoding.GetFixedResponseLength(
                    (MitsubishiFrameType)int.MaxValue,
                    CommunicationDataCode.Binary,
                    MitsubishiCommandCodes.DeviceRead,
                    0,
                    1))
            .IsNull();
    }

    /// <summary>Exercises unsupported serial message-format dispatches without a transport.</summary>
    /// <returns>A task that represents the asynchronous test.</returns>
    [Test]
    internal Task SerialUnsupportedFormatDispatchesThrowAsync()
    {
        var invalidFourC = CreateSerialOptions(
            MitsubishiFrameType.FourC,
            CommunicationDataCode.Ascii,
            (MitsubishiSerialMessageFormat)int.MaxValue);
        var invalidThreeC = CreateSerialOptions(
            MitsubishiFrameType.ThreeC,
            CommunicationDataCode.Ascii,
            MitsubishiSerialMessageFormat.Format5);
        var values = new[] { new MitsubishiDeviceValue(WordAddress, 0x1234) };

        _ = Assert.Throws<NotSupportedException>(
            () => MitsubishiSerialProtocolEncoding.EncodeWordReadRequest(invalidThreeC, WordAddress, 1));
        _ = Assert.Throws<NotSupportedException>(
            () => MitsubishiSerialProtocolEncoding.EncodeWordWriteRequest(invalidFourC, WordAddress, [0x1234]));
        _ = Assert.Throws<NotSupportedException>(
            () => MitsubishiSerialProtocolEncoding.EncodeBitWriteRequest(invalidFourC, BitAddress, [true]));
        _ = Assert.Throws<NotSupportedException>(
            () => MitsubishiSerialProtocolEncoding.EncodeRandomReadRequest(invalidFourC, [WordAddress]));
        _ = Assert.Throws<NotSupportedException>(
            () => MitsubishiSerialProtocolEncoding.EncodeRandomWriteRequest(invalidFourC, values));
        _ = Assert.Throws<NotSupportedException>(
            () => MitsubishiSerialProtocolEncoding.EncodeBlockReadRequest(invalidFourC, CreateBlocks()));
        _ = Assert.Throws<NotSupportedException>(
            () => MitsubishiSerialProtocolEncoding.EncodeBlockWriteRequest(invalidFourC, CreateBlocks()));
        _ = Assert.Throws<NotSupportedException>(
            () => MitsubishiSerialProtocolEncoding.EncodeMonitorRegistrationRequest(invalidFourC, [WordAddress]));
        _ = Assert.Throws<NotSupportedException>(
            () => MitsubishiSerialProtocolEncoding.EncodeExecuteMonitorRequest(invalidFourC));
        _ = Assert.Throws<NotSupportedException>(
            () => MitsubishiSerialProtocolEncoding.EncodeRemoteOperationRequest(
                invalidFourC,
                MitsubishiCommandCodes.RemoteRun));
        _ = Assert.Throws<NotSupportedException>(
            () => MitsubishiSerialProtocolEncoding.EncodeRawRequest(
                invalidFourC,
                new MitsubishiRawCommandRequest(0x1234, 0x0000, [0xCA])));
        return Task.CompletedTask;
    }

    /// <summary>Exercises serial ASCII and binary body construction paths.</summary>
    /// <returns>A task that represents the asynchronous test.</returns>
    [Test]
    internal async Task SerialAsciiAndBinaryBodiesEncodeAcrossFrameEdgesAsync()
    {
        var oneC = CreateSerialOptions(
            MitsubishiFrameType.OneC,
            CommunicationDataCode.Ascii,
            MitsubishiSerialMessageFormat.Format1);
        var fourCAscii = CreateSerialOptions(
            MitsubishiFrameType.FourC,
            CommunicationDataCode.Ascii,
            MitsubishiSerialMessageFormat.Format4);
        var fourCBinary = CreateSerialOptions(
            MitsubishiFrameType.FourC,
            CommunicationDataCode.Binary,
            MitsubishiSerialMessageFormat.Format5);

        var encoded = new[]
        {
            MitsubishiSerialProtocolEncoding.EncodeWordReadRequest(oneC, ExtendedWordAddress, 1),
            MitsubishiSerialProtocolEncoding.EncodeLoopbackRequest(fourCAscii, Encoding.ASCII.GetBytes("AB")),
            MitsubishiSerialProtocolEncoding.EncodeLoopbackRequest(fourCBinary, [0x41, 0x42]),
            MitsubishiSerialProtocolEncoding.EncodeMemoryAccessRequest(
                fourCAscii,
                MitsubishiCommands.MemoryWrite,
                0x2000,
                1,
                [0x1234]),
            MitsubishiSerialProtocolEncoding.EncodeMemoryAccessRequest(
                fourCBinary,
                MitsubishiCommands.MemoryWrite,
                0x2000,
                1,
                [0x1234]),
        };

        foreach (var request in encoded)
        {
            await Assert.That(request).IsNotEmpty();
        }
    }

    /// <summary>Exercises serial ASCII and binary completion validation boundaries.</summary>
    /// <returns>A task that represents the asynchronous test.</returns>
    [Test]
    internal async Task SerialFrameCompletionRejectsMalformedAndIncompleteFramesAsync()
    {
        var oneC = CreateSerialOptions(
            MitsubishiFrameType.OneC,
            CommunicationDataCode.Ascii,
            MitsubishiSerialMessageFormat.Format1);
        var fourCFormat4 = CreateSerialOptions(
            MitsubishiFrameType.FourC,
            CommunicationDataCode.Ascii,
            MitsubishiSerialMessageFormat.Format4);
        var fourCFormat5 = CreateSerialOptions(
            MitsubishiFrameType.FourC,
            CommunicationDataCode.Binary,
            MitsubishiSerialMessageFormat.Format5);
        var validAscii = BuildAsciiFrame("\u000600", wrapped: false);
        var validWrappedAscii = BuildAsciiFrame("\u0006F80000FF03", wrapped: true);
        byte[] validBinary = [0x10, 0x02, 0x02, 0x00, 0xF8, 0x00, 0x10, 0x03, 0x00, 0x00];

        await Assert.That(MitsubishiSerialProtocolEncoding.IsExpectedFrameComplete(oneC, [0x04, 0x30, 0x30, 0x30, 0x30]))
            .IsFalse();
        await Assert.That(MitsubishiSerialProtocolEncoding.IsExpectedFrameComplete(oneC, validAscii)).IsTrue();
        await Assert.That(
                MitsubishiSerialProtocolEncoding.IsExpectedFrameComplete(
                    fourCFormat4,
                    validWrappedAscii[..^ChecksumCharacterCount]))
            .IsFalse();
        await Assert.That(MitsubishiSerialProtocolEncoding.IsExpectedFrameComplete(fourCFormat4, validWrappedAscii))
            .IsTrue();
        await Assert.That(MitsubishiSerialProtocolEncoding.IsExpectedFrameComplete(fourCFormat5, [0x10, 0x02]))
            .IsFalse();
        await Assert.That(
                MitsubishiSerialProtocolEncoding.IsExpectedFrameComplete(
                    fourCFormat5,
                    [0x10, 0x03, 0x02, 0x00, 0xF8, 0x00, 0x10, 0x03]))
            .IsFalse();
        await Assert.That(MitsubishiSerialProtocolEncoding.IsExpectedFrameComplete(fourCFormat5, validBinary[..^1]))
            .IsFalse();
        await Assert.That(MitsubishiSerialProtocolEncoding.IsExpectedFrameComplete(fourCFormat5, validBinary))
            .IsTrue();
    }

    /// <summary>Creates a representative mixed block request.</summary>
    /// <returns>The block request.</returns>
    private static MitsubishiBlockRequest CreateBlocks() => new(
        [new MitsubishiWordBlock(WordAddress, new ushort[] { 0x1234 })],
        [new MitsubishiBitBlock(BitAddress, new bool[] { true })]);

    /// <summary>Creates a checksum-bearing ASCII serial frame.</summary>
    /// <param name="body">The frame body.</param>
    /// <param name="wrapped">Whether to apply the format-four CR/LF wrapper.</param>
    /// <returns>The encoded frame.</returns>
    private static byte[] BuildAsciiFrame(string body, bool wrapped)
    {
        var checksum = (Encoding.ASCII.GetBytes(body).Aggregate(0, static (sum, value) => sum + value) & 0xFF)
            .ToString("X2", System.Globalization.CultureInfo.InvariantCulture);
        return Encoding.ASCII.GetBytes(wrapped ? $"\r\n{body}{checksum}\r\n" : body + checksum);
    }

    /// <summary>Creates deterministic MC options.</summary>
    /// <param name="frameType">The frame type.</param>
    /// <param name="dataCode">The data code.</param>
    /// <returns>The client options.</returns>
    private static MitsubishiClientOptions CreateMcOptions(
        MitsubishiFrameType frameType,
        CommunicationDataCode dataCode) => new(
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
        MitsubishiSerialMessageFormat messageFormat) => new(
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
