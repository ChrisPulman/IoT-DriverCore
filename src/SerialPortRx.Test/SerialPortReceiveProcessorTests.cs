// Copyright (c) 2019-2026 Chris Pulman and contributors. All rights reserved.
// Chris Pulman and contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace IoT.DriverCore.Serial.Tests;

/// <summary>Tests raw-byte processing used by automatic serial receive events.</summary>
public sealed class SerialPortReceiveProcessorTests
{
    /// <summary>Gets the raw bytes used to test multi-buffer receive draining.</summary>
    private static byte[] MultiChunkSource { get; } = [1, Two, Three, Four, Five];

    /// <summary>Gets the batches expected when the multi-buffer source is drained with a two-byte buffer.</summary>
    private static byte[][] ExpectedMultiChunkBatches { get; } = [[1, Two], [Three, Four], [Five]];

    /// <summary>Verifies automatic receive processing publishes the exact raw bytes read from the transport.</summary>
    /// <returns>A task representing the asynchronous unit test.</returns>
    [Test]
    public async Task ReadAndPublish_PublishesRawBytesWithoutCharacterRoundTripAsync()
    {
        byte[] source = [0x00, 0x80, 0xff];
        var bytes = new List<byte>();
        var characters = new List<char>();
        var batches = new List<byte[]>();
        var buffer = new byte[8];

        var bytesRead = SerialPortReceiveProcessor.ReadAndPublish(
            source.Length,
            buffer,
            (destination, offset, count) =>
            {
                Array.Copy(source, 0, destination, offset, count);
                return count;
            },
            bytes.Add,
            characters.Add,
            batches.Add);

        await Assert.That(bytesRead).IsEqualTo(source.Length);
        await Assert.That(bytes).IsEquivalentTo(source);
        await Assert.That(characters).IsEquivalentTo(['\0', '\u0080', '\u00ff']);
        await Assert.That(batches.Count).IsEqualTo(1);
        await Assert.That(batches[0]).IsEquivalentTo(source);
        await Assert.That(ReferenceEquals(batches[0], buffer)).IsFalse();
    }

    /// <summary>Verifies receive processing does not call the raw reader when no bytes are available.</summary>
    /// <returns>A task representing the asynchronous unit test.</returns>
    [Test]
    public async Task ReadAndPublish_WhenNoBytesAreAvailable_DoesNotReadOrPublishAsync()
    {
        var readCount = 0;
        var publishedByteCount = 0;
        var publishedBatchCount = 0;

        var bytesRead = SerialPortReceiveProcessor.ReadAndPublish(
            0,
            new byte[1],
            (_, _, _) =>
            {
                readCount++;
                return 0;
            },
            _ => publishedByteCount++,
            _ => { },
            _ => publishedBatchCount++);

        await Assert.That(bytesRead).IsEqualTo(0);
        await Assert.That(readCount).IsEqualTo(0);
        await Assert.That(publishedByteCount).IsEqualTo(0);
        await Assert.That(publishedBatchCount).IsEqualTo(0);
    }

    /// <summary>Verifies a single receive event drains all available data in immutable buffer-sized batches.</summary>
    /// <returns>A task representing the asynchronous unit test.</returns>
    [Test]
    public async Task DrainAndPublish_WhenDataExceedsBuffer_DrainsEveryRawByteAsync()
    {
        var sourceOffset = 0;
        var bytes = new List<byte>();
        var batches = new List<byte[]>();
        var buffer = new byte[ExpectedMultiChunkBatches[0].Length];

        var bytesRead = SerialPortReceiveProcessor.DrainAndPublish(
            () => MultiChunkSource.Length - sourceOffset,
            buffer,
            (destination, offset, count) =>
            {
                Array.Copy(MultiChunkSource, sourceOffset, destination, offset, count);
                sourceOffset += count;
                return count;
            },
            bytes.Add,
            _ => { },
            batches.Add);

        await Assert.That(bytesRead).IsEqualTo(MultiChunkSource.Length);
        await Assert.That(bytes).IsEquivalentTo(MultiChunkSource);
        await Assert.That(batches.Count).IsEqualTo(ExpectedMultiChunkBatches.Length);
        await Assert.That(batches[0]).IsEquivalentTo(ExpectedMultiChunkBatches[0]);
        await Assert.That(batches[1]).IsEquivalentTo(ExpectedMultiChunkBatches[1]);
        await Assert.That(batches[2]).IsEquivalentTo(ExpectedMultiChunkBatches[2]);

        buffer[0] = 0;
        await Assert.That(batches[0]).IsEquivalentTo(ExpectedMultiChunkBatches[0]);
    }

    /// <summary>Verifies draining makes one availability request per batch plus its terminating probe.</summary>
    /// <returns>A task representing the asynchronous unit test.</returns>
    [Test]
    public async Task DrainAndPublish_UsesOneAvailabilityRequestPerBatchPlusTerminatingProbeAsync()
    {
        byte[] source = [1, Two, Three, Four, Five];
        var sourceOffset = 0;
        var availabilityRequests = 0;
        var readRequests = 0;
        var publishedBatches = new List<byte[]>();

        var bytesRead = SerialPortReceiveProcessor.DrainAndPublish(
            () =>
            {
                availabilityRequests++;
                return source.Length - sourceOffset;
            },
            new byte[Two],
            (destination, offset, count) =>
            {
                readRequests++;
                Array.Copy(source, sourceOffset, destination, offset, count);
                sourceOffset += count;
                return count;
            },
            _ => { },
            _ => { },
            publishedBatches.Add);

        await Assert.That(bytesRead).IsEqualTo(source.Length);
        await Assert.That(readRequests).IsEqualTo(Three);
        await Assert.That(availabilityRequests).IsEqualTo(readRequests + 1);
        await Assert.That(publishedBatches.Count).IsEqualTo(readRequests);
        await Assert.That(publishedBatches.SelectMany(batch => batch)).IsEquivalentTo(source);
    }

    /// <summary>Verifies all transport adapters expose the common batch receive contract.</summary>
    /// <returns>A task representing the asynchronous unit test.</returns>
    [Test]
    public async Task ReceivePorts_ImplementBatchReceiveContractAsync()
    {
        using var serial = new SerialPortRx();
        using var tcp = new TcpClientRx();
        using var udp = new UdpClientRx(new UdpClient(0));

        await AssertBatchReceiveContractAsync(serial);
        await AssertBatchReceiveContractAsync(tcp);
        await AssertBatchReceiveContractAsync(udp);
    }

    /// <summary>Verifies a port exposes a batch observable through the common contract.</summary>
    /// <param name="port">The receive port to verify.</param>
    /// <returns>A task representing the asynchronous assertion.</returns>
    private static async Task AssertBatchReceiveContractAsync(IReceiveBatchPortRx port) =>
        await Assert.That(port.DataReceivedBatches).IsNotNull();
}
