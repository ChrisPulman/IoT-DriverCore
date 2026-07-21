// Copyright (c) 2019-2026 Chris Pulman and contributors. All rights reserved.
// Chris Pulman and contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.IO.Ports;
using System.Text;

#if REACTIVE_SHIM

namespace MitsubishiRx.Reactive.Tests;
#else

namespace MitsubishiRx.Tests;
#endif

/// <summary>Provides the MitsubishiSerialRandomTests type.</summary>
internal sealed class MitsubishiSerialRandomTests
{
    /// <summary>Stores the serial request timeout in seconds.</summary>
    private const int RequestTimeoutSeconds = 2;

    /// <summary>Tests the 3C format 1 random-read request and its parsed response.</summary>
    /// <returns>The RandomReadWordsAsyncSerial3CFormat1EncodesExpectedRequestAndParsesResponse operation
    /// result.</returns>
    [Test]
    internal async Task RandomReadWordsAsyncSerial3CFormat1EncodesExpectedRequestAndParsesResponseAsync()
    {
        await using var transport = new FakeTransport(
            [BuildAsciiRandomReadResponse(
                MitsubishiFrameType.ThreeC,
                MitsubishiSerialMessageFormat.Format1,
                "12345678")]);
        var options = CreateSerialOptions(
            MitsubishiFrameType.ThreeC,
            CommunicationDataCode.Ascii,
            MitsubishiSerialMessageFormat.Format1);
        await using var client = new MitsubishiRx(options, transport, Scheduler.Immediate);

        var result = await client.RandomReadWordsAsync(["D100", "D300"], CancellationToken.None);

        await Assert.That(result.IsSucceed).IsTrue();
        await Assert.That(result.Value!.Select(static value => (int)value).ToArray()).IsEquivalentTo([ 0x1234, 0x5678]);
        await Assert.That(transport.Requests.Count).IsEqualTo(1);
        await Assert.That(Encoding.ASCII.GetString(transport.Requests[0].Payload))
            .IsEqualTo("\u0005F90000FF000403000000020000000064D*00012CD*70");
    }

    /// <summary>Executes the RandomWriteWordsAsyncSerial3CFormat1EncodesExpectedRequest operation.</summary>
    /// <returns>The RandomWriteWordsAsyncSerial3CFormat1EncodesExpectedRequest operation result.</returns>
    [Test]
    internal async Task RandomWriteWordsAsyncSerial3CFormat1EncodesExpectedRequestAsync()
    {
        await using var transport = new FakeTransport(
            [BuildAsciiAckResponse(
                MitsubishiFrameType.ThreeC,
                MitsubishiSerialMessageFormat.Format1)]);
        var options = CreateSerialOptions(
            MitsubishiFrameType.ThreeC,
            CommunicationDataCode.Ascii,
            MitsubishiSerialMessageFormat.Format1);
        await using var client = new MitsubishiRx(options, transport, Scheduler.Immediate);

        var result = await client.RandomWriteWordsAsync(
        [
            new KeyValuePair<string, ushort>("D100", 0x1234),
            new KeyValuePair<string, ushort>("D300", 0x5678),
        ], CancellationToken.None);

        await Assert.That(result.IsSucceed).IsTrue();
        await Assert.That(transport.Requests.Count).IsEqualTo(1);
        await Assert.That(Encoding.ASCII.GetString(transport.Requests[0].Payload))
            .IsEqualTo("\u0005F90000FF001402000000020000000064D*123400012CD*567814");
    }

    /// <summary>Tests the 4C format 5 random-read request and its parsed response.</summary>
    /// <returns>The RandomReadWordsAsyncSerial4CFormat5EncodesExpectedRequestAndParsesResponse operation
    /// result.</returns>
    [Test]
    internal async Task RandomReadWordsAsyncSerial4CFormat5EncodesExpectedRequestAndParsesResponseAsync()
    {
        await using var transport = new FakeTransport([BuildBinaryRandomReadResponse()]);
        var options = CreateSerialOptions(
            MitsubishiFrameType.FourC,
            CommunicationDataCode.Binary,
            MitsubishiSerialMessageFormat.Format5);
        await using var client = new MitsubishiRx(options, transport, Scheduler.Immediate);

        var result = await client.RandomReadWordsAsync(["D100", "D300"], CancellationToken.None);

        await Assert.That(result.IsSucceed).IsTrue();
        await Assert.That(result.Value!.Select(static value => (int)value).ToArray()).IsEquivalentTo([ 0x1234, 0x5678]);
        await Assert.That(transport.Requests.Count).IsEqualTo(1);
        await Assert.That(Convert.ToHexString(transport.Requests[0].Payload))
            .IsEqualTo("10021800F80000FFFF0300000304000002000000640000A82C0100A810034642");
    }

    /// <summary>Executes the RandomWriteWordsAsyncSerial4CFormat5EncodesExpectedRequest operation.</summary>
    /// <returns>The RandomWriteWordsAsyncSerial4CFormat5EncodesExpectedRequest operation result.</returns>
    [Test]
    internal async Task RandomWriteWordsAsyncSerial4CFormat5EncodesExpectedRequestAsync()
    {
        await using var transport = new FakeTransport([BuildBinaryAckResponse()]);
        var options = CreateSerialOptions(
            MitsubishiFrameType.FourC,
            CommunicationDataCode.Binary,
            MitsubishiSerialMessageFormat.Format5);
        await using var client = new MitsubishiRx(options, transport, Scheduler.Immediate);

        var result = await client.RandomWriteWordsAsync(
        [
            new KeyValuePair<string, ushort>("D100", 0x1234),
            new KeyValuePair<string, ushort>("D300", 0x5678),
        ], CancellationToken.None);

        await Assert.That(result.IsSucceed).IsTrue();
        await Assert.That(transport.Requests.Count).IsEqualTo(1);
        await Assert.That(Convert.ToHexString(transport.Requests[0].Payload))
            .IsEqualTo("10021C00F80000FFFF0300000214000002000000640000A834122C0100A8785610033232");
    }

    /// <summary>Executes the CreateSerialOptions operation.</summary>
    /// <param name="frameType">The frameType parameter.</param>
    /// <param name="dataCode">The dataCode parameter.</param>
    /// <param name="messageFormat">The messageFormat parameter.</param>
    /// <returns>The CreateSerialOptions operation result.</returns>
    private static MitsubishiClientOptions CreateSerialOptions(
        MitsubishiFrameType frameType,
        CommunicationDataCode dataCode,
        MitsubishiSerialMessageFormat messageFormat)
        => new(
            Host: "COM3",
            Port: 0,
            FrameType: frameType,
            DataCode: dataCode,
            TransportKind: MitsubishiTransportKind.Serial,
            Timeout: TimeSpan.FromSeconds(RequestTimeoutSeconds),
            CpuType: CpuType.Fx5,
            Serial: new MitsubishiSerialOptions(
                PortName: "COM3",
                BaudRate: 9600,
                DataBits: 7,
                Parity: Parity.Even,
                StopBits: StopBits.One,
                Handshake: Handshake.None,
                MessageFormat: messageFormat));

    /// <summary>Executes the BuildAsciiRandomReadResponse operation.</summary>
    /// <param name="frameType">The frameType parameter.</param>
    /// <param name="format">The format parameter.</param>
    /// <param name="payload">The payload parameter.</param>
    /// <returns>The BuildAsciiRandomReadResponse operation result.</returns>
    private static byte[] BuildAsciiRandomReadResponse(
        MitsubishiFrameType frameType,
        MitsubishiSerialMessageFormat format,
        string payload)
    {
        var body = frameType switch
        {
            MitsubishiFrameType.OneC => $"\u000600{payload}",
            MitsubishiFrameType.ThreeC => $"\u0006F900{payload}",
            MitsubishiFrameType.FourC => $"\u0006F80000FF03{payload}",
            _ => throw new ArgumentOutOfRangeException(nameof(frameType)),
        };

        var checksum = ComputeChecksum(body);
        return format switch
        {
            MitsubishiSerialMessageFormat.Format1 => Encoding.ASCII.GetBytes(string.Concat(body, checksum)),
            MitsubishiSerialMessageFormat.Format4 => Encoding.ASCII.GetBytes($"\r\n{body}{checksum}\r\n"),
            _ => throw new ArgumentOutOfRangeException(nameof(format)),
        };
    }

    /// <summary>Executes the BuildAsciiAckResponse operation.</summary>
    /// <param name="frameType">The frameType parameter.</param>
    /// <param name="format">The format parameter.</param>
    /// <returns>The BuildAsciiAckResponse operation result.</returns>
    private static byte[] BuildAsciiAckResponse(MitsubishiFrameType frameType, MitsubishiSerialMessageFormat format)
    {
        var body = frameType switch
        {
            MitsubishiFrameType.OneC => "\u000600FF",
            MitsubishiFrameType.ThreeC => "\u0006F90000FF",
            MitsubishiFrameType.FourC => "\u0006F80000FF03FF00",
            _ => throw new ArgumentOutOfRangeException(nameof(frameType)),
        };

        var checksum = ComputeChecksum(body);
        return format switch
        {
            MitsubishiSerialMessageFormat.Format1 => Encoding.ASCII.GetBytes(string.Concat(body, checksum)),
            MitsubishiSerialMessageFormat.Format4 => Encoding.ASCII.GetBytes($"\r\n{body}{checksum}\r\n"),
            _ => throw new ArgumentOutOfRangeException(nameof(format)),
        };
    }

    /// <summary>Executes the BuildBinaryRandomReadResponse operation.</summary>
    /// <returns>The BuildBinaryRandomReadResponse operation result.</returns>
    private static byte[] BuildBinaryRandomReadResponse()
        => Convert.FromHexString("10021000F80000FFFF030000FFFF00003412785610033142");

    /// <summary>Executes the BuildBinaryAckResponse operation.</summary>
    /// <returns>The BuildBinaryAckResponse operation result.</returns>
    private static byte[] BuildBinaryAckResponse()
        => Convert.FromHexString("10020C00F80000FFFF030000FFFF000010034137");

    /// <summary>Executes the ComputeChecksum operation.</summary>
    /// <param name="body">The body parameter.</param>
    /// <returns>The ComputeChecksum operation result.</returns>
    private static string ComputeChecksum(string body)
        => (Encoding.ASCII.GetBytes(body).Aggregate(0, static (sum, value) => sum + value) & 0xFF).ToString("X2");
}
