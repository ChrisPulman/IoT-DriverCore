// Copyright (c) 2019-2026 Chris Pulman and contributors. All rights reserved.
// Chris Pulman and contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace IoT.DriverCore.Serial.Tests;

/// <summary>Deterministic operation-count acceptance tests for the serial and network receive hot paths.</summary>
[NotInParallel]
public sealed class TransportPerformanceAcceptanceTests
{
    /// <summary>The number of datagrams used to exercise the UDP receive loop.</summary>
    private const int DatagramCount = Four;

    /// <summary>The number of bytes carried by each in-memory serial write.</summary>
    private const int SerialPayloadLength = 128;

    /// <summary>Verifies one in-memory write produces one immutable receive batch without serial hardware.</summary>
    /// <returns>A task representing the asynchronous unit test.</returns>
    [Test]
    public async Task InMemorySerial_Write_UsesOneDeliveryAndOneImmutableBatchAsync()
    {
        using var pair = new InMemoryPortRxPair();
        var batches = new List<byte[]>();
        using var subscription = pair.Second.DataReceivedBatches.Subscribe(batches.Add);
        await pair.First.OpenAsync();
        await pair.Second.OpenAsync();
        var payload = Enumerable.Range(0, SerialPayloadLength).Select(value => (byte)value).ToArray();

        pair.First.Write(payload, 0, payload.Length);

        await Assert.That(batches.Count).IsEqualTo(1);
        await Assert.That(batches[0]).IsEquivalentTo(payload);
        await Assert.That(ReferenceEquals(batches[0], payload)).IsFalse();
    }

    /// <summary>Verifies loopback UDP preserves one owned batch per datagram with no timing threshold.</summary>
    /// <param name="cancellationToken">The TUnit timeout cancellation token.</param>
    /// <returns>A task representing the asynchronous unit test.</returns>
    [Test]
    [Timeout(5000)]
    public async Task UdpLoopback_UsesOnePublishedBatchPerDatagramAsync(CancellationToken cancellationToken)
    {
        using var socket = new UdpClient(new IPEndPoint(IPAddress.Loopback, 0));
        var endpoint = (IPEndPoint)(socket.Client.LocalEndPoint ??
            throw new InvalidOperationException("The UDP receiver did not expose an endpoint."));
        using var receiver = new UdpClientRx(socket);
        using var sender = new UdpClient(AddressFamily.InterNetwork);
        var batches = new ConcurrentQueue<byte[]>();
        var allReceived = new TaskCompletionSource<object?>(TaskCreationOptions.RunContinuationsAsynchronously);
        using var subscription = receiver.DataReceivedBatches.Subscribe(batch =>
        {
            batches.Enqueue(batch);
            if (batches.Count != DatagramCount)
            {
                return;
            }

            _ = allReceived.TrySetResult(null);
        });
        await receiver.OpenAsync();

        for (var index = 0; index < DatagramCount; index++)
        {
            byte[] datagram = [(byte)index, ByteLetterA];
            await sender.SendAsync(datagram, endpoint, cancellationToken);
        }

        await allReceived.Task.WaitAsync(TimeSpan.FromSeconds(Two), cancellationToken);
        var received = batches.ToArray();

        await Assert.That(received.Length).IsEqualTo(DatagramCount);
        await Assert.That(received.Select(batch => batch.Length)).IsEquivalentTo(Enumerable.Repeat(Two, DatagramCount));
        await Assert.That(received.Select(batch => batch[1])).IsEquivalentTo(Enumerable.Repeat(ByteLetterA, DatagramCount));
        await Assert.That(received.Distinct().Count()).IsEqualTo(DatagramCount);
    }
}
