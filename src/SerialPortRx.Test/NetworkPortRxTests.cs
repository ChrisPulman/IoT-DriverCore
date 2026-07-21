// Copyright (c) 2019-2026 Chris Pulman and contributors. All rights reserved.
// Chris Pulman and contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace CP.IO.Ports.Tests;

/// <summary>Tests for TCP and UDP reactive port adapters.</summary>
[NotInParallel]
public sealed class NetworkPortRxTests
{
    /// <summary>Verifies TCP loopback data is published to byte and batch streams.</summary>
    /// <param name="cancellationToken">The TUnit timeout cancellation token.</param>
    /// <returns>A task representing the asynchronous unit test.</returns>
    [Test]
    [Timeout(5000)]
    public async Task TcpClientRx_Open_ReadsLoopbackBytesAsync(CancellationToken cancellationToken)
    {
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var endpoint = (IPEndPoint)listener.LocalEndpoint;
        using var client = new TcpClientRx();
        var acceptTask = listener.AcceptTcpClientAsync(cancellationToken);

        client.Connect(IPAddress.Loopback, endpoint.Port);
        using var server = await acceptTask;

        var values = new List<int>();
        var batches = new List<byte[]>();
        var received = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var batchReceived = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        using var dataSubscription = client.DataReceived.Subscribe(value =>
        {
            values.Add(value);
            if (values.Count < 3)
            {
                return;
            }

            _ = received.TrySetResult(true);
        });
        using var batchSubscription = client.DataReceivedBatches.Subscribe(batch =>
        {
            batches.Add(batch);
            _ = batchReceived.TrySetResult(true);
        });

        await client.OpenAsync();
        byte[] payload = [1, 2, 3];
        await server.GetStream().WriteAsync(payload, cancellationToken);
        await received.Task.WaitAsync(TimeSpan.FromSeconds(Two), cancellationToken);
        await batchReceived.Task.WaitAsync(TimeSpan.FromSeconds(Two), cancellationToken);

        await Assert.That(values.Count).IsEqualTo(Three);
        await Assert.That(values[0]).IsEqualTo(1);
        await Assert.That(values[1]).IsEqualTo(Two);
        await Assert.That(values[2]).IsEqualTo(Three);
        await Assert.That(batches.Count).IsEqualTo(1);

        client.Close();
    }

    /// <summary>Verifies TCP Write sends data to the connected socket.</summary>
    /// <param name="cancellationToken">The TUnit timeout cancellation token.</param>
    /// <returns>A task representing the asynchronous unit test.</returns>
    [Test]
    [Timeout(5000)]
    public async Task TcpClientRx_Write_SendsBytesAsync(CancellationToken cancellationToken)
    {
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var endpoint = (IPEndPoint)listener.LocalEndpoint;
        using var client = new TcpClientRx();
        var acceptTask = listener.AcceptTcpClientAsync(cancellationToken);

        client.Connect(IPAddress.Loopback, endpoint.Port);
        using var server = await acceptTask;
        var buffer = new byte[3];

        client.Write([Nine, Eight, Seven], 0, Three);
        var bytesRead = await server.GetStream().ReadAsync(buffer, cancellationToken);

        await Assert.That(bytesRead).IsEqualTo(Three);
        await Assert.That(buffer[0]).IsEqualTo(ByteNine);
        await Assert.That(buffer[1]).IsEqualTo(ByteEight);
        await Assert.That(buffer[2]).IsEqualTo(ByteSeven);
    }

    /// <summary>Verifies UDP loopback datagrams are published to byte and batch streams.</summary>
    /// <param name="cancellationToken">The TUnit timeout cancellation token.</param>
    /// <returns>A task representing the asynchronous unit test.</returns>
    [Test]
    [Timeout(5000)]
    public async Task UdpClientRx_Open_ReadsLoopbackDatagramsAsync(CancellationToken cancellationToken)
    {
        using var receiverSocket = new UdpClient(new IPEndPoint(IPAddress.Loopback, 0));
        var endpoint = receiverSocket.Client.LocalEndPoint as IPEndPoint ??
            throw new InvalidOperationException("The UDP receiver did not expose an IP endpoint.");
        using var receiver = new UdpClientRx(receiverSocket);
        using var sender = new UdpClient();

        var values = new List<int>();
        var batches = new List<byte[]>();
        var received = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var batchReceived = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        using var dataSubscription = receiver.DataReceived.Subscribe(value =>
        {
            values.Add(value);
            if (values.Count < 2)
            {
                return;
            }

            _ = received.TrySetResult(true);
        });
        using var batchSubscription = receiver.DataReceivedBatches.Subscribe(batch =>
        {
            batches.Add(batch);
            _ = batchReceived.TrySetResult(true);
        });

        await receiver.OpenAsync();
        byte[] payload = [4, 5];
        await sender.SendAsync(payload, endpoint, cancellationToken);
        await received.Task.WaitAsync(TimeSpan.FromSeconds(Two), cancellationToken);
        await batchReceived.Task.WaitAsync(TimeSpan.FromSeconds(Two), cancellationToken);

        await Assert.That(values.Count).IsEqualTo(Two);
        await Assert.That(values[0]).IsEqualTo(Four);
        await Assert.That(values[1]).IsEqualTo(Five);
        await Assert.That(batches.Count).IsEqualTo(1);

        receiver.Close();
    }

    /// <summary>Verifies UDP ReadAsync validates arguments.</summary>
    /// <returns>A task representing the asynchronous unit test.</returns>
    [Test]
    public async Task UdpClientRx_ReadAsync_WhenArgumentsAreInvalid_ThrowsAsync()
    {
        using var udp = new UdpClientRx(new UdpClient(0));
        var buffer = new byte[4];

        await Assert.That(() => udp.ReadAsync(null, 0, 1)).Throws<ArgumentNullException>();
        await Assert.That(() => udp.ReadAsync(buffer, -1, 1)).Throws<ArgumentOutOfRangeException>();
        await Assert.That(() => udp.ReadAsync(buffer, Five, 1)).Throws<ArgumentOutOfRangeException>();
        await Assert.That(() => udp.ReadAsync(buffer, 0, -1)).Throws<ArgumentOutOfRangeException>();
        await Assert.That(() => udp.ReadAsync(buffer, 0, Five)).Throws<ArgumentOutOfRangeException>();
    }

    /// <summary>Verifies UDP Write validates arguments.</summary>
    /// <returns>A task representing the asynchronous unit test.</returns>
    [Test]
    public async Task UdpClientRx_Write_WhenArgumentsAreInvalid_ThrowsAsync()
    {
        using var udp = new UdpClientRx(new UdpClient(0));
        var buffer = new byte[4];

        await Assert.That(() => udp.Write(null, 0, 1)).Throws<ArgumentNullException>();
        await Assert.That(() => udp.Write(buffer, -1, 1)).Throws<ArgumentOutOfRangeException>();
        await Assert.That(() => udp.Write(buffer, Five, 1)).Throws<ArgumentOutOfRangeException>();
        await Assert.That(() => udp.Write(buffer, 0, -1)).Throws<ArgumentOutOfRangeException>();
        await Assert.That(() => udp.Write(buffer, 0, Five)).Throws<ArgumentOutOfRangeException>();
    }
}
