// Copyright (c) 2019-2026 Chris Pulman and contributors. All rights reserved.
// Chris Pulman and contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace IoT.DriverCore.Serial.Tests;

/// <summary>Tests for TCP and UDP reactive port adapters.</summary>
[NotInParallel]
public sealed class NetworkPortRxTests
{
    /// <summary>The numeric IPv4 loopback host name.</summary>
    private const string Ipv4LoopbackHost = "127.0.0.1";

    /// <summary>Verifies TCP loopback data is published to byte and batch streams.</summary>
    /// <param name="cancellationToken">The TUnit timeout cancellation token.</param>
    /// <returns>A task representing the asynchronous unit test.</returns>
    [Test]
    [Timeout(5000)]
    public async Task TcpClientRx_Open_ReadsLoopbackBytesAsync(CancellationToken cancellationToken)
    {
        using var listenerScope = StartTcpListener();
        var listener = listenerScope.Listener;
        var endpoint = (IPEndPoint)listener.LocalEndpoint;
        using var client = new TcpClientRx();
        client.Connect(IPAddress.Loopback, endpoint.Port);
        using var server = await AcceptTcpClientAsync(listener, cancellationToken);

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
        using var listenerScope = StartTcpListener();
        var listener = listenerScope.Listener;
        var endpoint = (IPEndPoint)listener.LocalEndpoint;
        using var client = new TcpClientRx();
        client.Connect(IPAddress.Loopback, endpoint.Port);
        using var server = await AcceptTcpClientAsync(listener, cancellationToken);
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

    /// <summary>Verifies TCP direct reads honor target offsets and publish the actual read bytes.</summary>
    /// <param name="cancellationToken">The TUnit timeout cancellation token.</param>
    /// <returns>A task representing the asynchronous unit test.</returns>
    [Test]
    [Timeout(5000)]
    public async Task TcpClientRx_ReadAsync_UsesOffsetAndPublishesBytesAsync(CancellationToken cancellationToken)
    {
        using var listenerScope = StartTcpListener();
        var listener = listenerScope.Listener;
        var endpoint = (IPEndPoint)listener.LocalEndpoint;
        using var client = new TcpClientRx(AddressFamily.InterNetwork);
        client.Connect(endpoint);
        using var server = await AcceptTcpClientAsync(listener, cancellationToken);
        var observed = new List<int>();
        using var subscription = client.BytesReceived.Subscribe(observed.Add);
        byte[] payload = [ByteNine, ByteEight];
        await server.GetStream().WriteAsync(payload, cancellationToken);
        var target = new byte[Four];

        var read = await client.ReadAsync(target, 1, Two);

        await Assert.That(read).IsEqualTo(Two);
        await Assert.That(target[1]).IsEqualTo(ByteNine);
        await Assert.That(target[2]).IsEqualTo(ByteEight);
        await Assert.That(observed).IsEquivalentTo([Nine, Eight]);
        await Assert.That(client.Client.AddressFamily).IsEqualTo(AddressFamily.InterNetwork);
        await Assert.That(() => client.DiscardInBuffer()).ThrowsNothing();
    }

    /// <summary>Verifies UDP direct reads retain remaining datagram bytes and honor target offsets.</summary>
    /// <param name="cancellationToken">The TUnit timeout cancellation token.</param>
    /// <returns>A task representing the asynchronous unit test.</returns>
    [Test]
    [Timeout(5000)]
    public async Task UdpClientRx_ReadAsync_UsesOffsetAndRetainsDatagramRemainderAsync(
        CancellationToken cancellationToken)
    {
        using var receiverSocket = new UdpClient(new IPEndPoint(IPAddress.Loopback, 0));
        var endpoint = (IPEndPoint)(receiverSocket.Client.LocalEndPoint ??
            throw new InvalidOperationException("The UDP receiver did not expose an endpoint."));
        using var receiver = new UdpClientRx(receiverSocket);
        using var sender = new UdpClient(AddressFamily.InterNetwork);
        var observed = new List<int>();
        using var subscription = receiver.BytesReceived.Subscribe(observed.Add);
        byte[] payload = [ByteSeven, ByteEight, ByteNine];
        await sender.SendAsync(payload, endpoint, cancellationToken);
        var target = new byte[Five];

        var firstRead = await receiver.ReadAsync(target, 1, Two);
        var secondRead = await receiver.ReadAsync(target, Three, 1);

        await Assert.That(firstRead).IsEqualTo(Two);
        await Assert.That(secondRead).IsEqualTo(1);
        await Assert.That(target[1]).IsEqualTo(ByteSeven);
        await Assert.That(target[2]).IsEqualTo(ByteEight);
        await Assert.That(target[3]).IsEqualTo(ByteNine);
        await Assert.That(observed).IsEquivalentTo([Seven, Eight, Nine]);
    }

    /// <summary>Verifies connected UDP writes and socket options use the IPv4 operating-system path.</summary>
    /// <param name="cancellationToken">The TUnit timeout cancellation token.</param>
    /// <returns>A task representing the asynchronous unit test.</returns>
    [Test]
    [Timeout(5000)]
    public async Task UdpClientRx_ConnectedWriteAndOptions_UseIpv4LoopbackAsync(
        CancellationToken cancellationToken)
    {
        using var receiver = new UdpClient(new IPEndPoint(IPAddress.Loopback, 0));
        var endpoint = (IPEndPoint)(receiver.Client.LocalEndPoint ??
            throw new InvalidOperationException("The UDP receiver did not expose an endpoint."));
        using var sender = new UdpClientRx(AddressFamily.InterNetwork)
        {
            Ttl = Two,
            DontFragment = true,
            EnableBroadcast = false,
            MulticastLoopback = true,
            ReadTimeout = Hundred,
            WriteTimeout = Hundred,
        };
        sender.Connect(IPAddress.Loopback, endpoint.Port);
        byte[] payload = [0, ByteLetterA, ByteLetterB, 0];

        sender.Write(payload, 1, Two);
        var result = await receiver.ReceiveAsync(cancellationToken);

        await Assert.That(result.Buffer).IsEquivalentTo([ByteLetterA, ByteLetterB]);
        await Assert.That(sender.Ttl).IsEqualTo((short)Two);
        await Assert.That(sender.DontFragment).IsTrue();
        await Assert.That(sender.EnableBroadcast).IsFalse();
        await Assert.That(sender.MulticastLoopback).IsTrue();
        await Assert.That(sender.ReadTimeout).IsEqualTo(Hundred);
        await Assert.That(sender.WriteTimeout).IsEqualTo(Hundred);
        await Assert.That(sender.InfiniteTimeout).IsEqualTo(Timeout.Infinite);
    }

    /// <summary>Verifies TCP constructor, connect, option, cache, reopen, and empty-read surfaces over IPv4.</summary>
    /// <param name="cancellationToken">The TUnit timeout cancellation token.</param>
    /// <returns>A task representing the asynchronous unit test.</returns>
    [Test]
    [Timeout(5000)]
    public async Task TcpClientRx_AdditionalIpv4Surfaces_AreDeterministicAsync(
        CancellationToken cancellationToken)
    {
        using var listenerScope = StartTcpListener();
        var listener = listenerScope.Listener;
        var endpoint = (IPEndPoint)listener.LocalEndpoint;
        using var wrappedSocket = new TcpClient(AddressFamily.InterNetwork);
        using var wrapped = new TcpClientRx(wrappedSocket);
        using var local = new TcpClientRx(new IPEndPoint(IPAddress.Loopback, 0));
        using var byName = new TcpClientRx(Ipv4LoopbackHost, endpoint.Port);
        using var byNameServer = await AcceptTcpClientAsync(listener, cancellationToken);
        using var defaultClient = new TcpClientRx();
        var defaultAccept = AcceptTcpClientAsync(listener, cancellationToken);
        defaultClient.Connect(Ipv4LoopbackHost, endpoint.Port);
        using var defaultServer = await defaultAccept;
        using var addressesClient = new TcpClientRx();
        var addressesAccept = AcceptTcpClientAsync(listener, cancellationToken);
        addressesClient.Connect([IPAddress.Loopback], endpoint.Port);
        using var addressesServer = await addressesAccept;

        defaultClient.ReadTimeout = Hundred;
        defaultClient.WriteTimeout = Hundred;
        await Assert.That(defaultClient.InfiniteTimeout).IsEqualTo(Timeout.Infinite);
        await Assert.That(defaultClient.ReadTimeout).IsEqualTo(Hundred);
        await Assert.That(defaultClient.WriteTimeout).IsEqualTo(Hundred);
        await defaultClient.OpenAsync();
        await defaultClient.OpenAsync();
        defaultClient.Close();
        await defaultClient.OpenAsync();
        defaultClient.Close();

        await Assert.That(wrapped.Client.AddressFamily).IsEqualTo(AddressFamily.InterNetwork);
        await Assert.That(local.Client.LocalEndPoint).IsNotNull();
        await Assert.That(byName.Client.Connected).IsTrue();
        await Assert.That(addressesClient.Client.Connected).IsTrue();
        await Assert.That(ReferenceEquals(byName.DataReceivedAsync, byName.DataReceivedAsync)).IsTrue();
        await Assert.That(ReferenceEquals(byName.DataReceivedBatchesAsync, byName.DataReceivedBatchesAsync)).IsTrue();
        await Assert.That(ReferenceEquals(byName.BytesReceivedAsync, byName.BytesReceivedAsync)).IsTrue();
        defaultClient.Dispose();
        defaultClient.Dispose();
    }

    /// <summary>Verifies UDP constructors, connect overloads, async caches, direct APIs, and lifecycle surfaces.</summary>
    /// <param name="cancellationToken">The TUnit timeout cancellation token.</param>
    /// <returns>A task representing the asynchronous unit test.</returns>
    [Test]
    [Timeout(5000)]
    public async Task UdpClientRx_AdditionalIpv4Surfaces_AreDeterministicAsync(
        CancellationToken cancellationToken)
    {
        using var target = new UdpClient(new IPEndPoint(IPAddress.Loopback, 0));
        var endpoint = (IPEndPoint)(target.Client.LocalEndPoint ??
            throw new InvalidOperationException("The UDP target did not expose an endpoint."));
        using var defaultClient = new UdpClientRx();
        using var localClient = new UdpClientRx(new IPEndPoint(IPAddress.Loopback, 0));
        using var portClient = new UdpClientRx(0);
        using var familyPortClient = new UdpClientRx(0, AddressFamily.InterNetwork);
        using var namedClient = new UdpClientRx(Ipv4LoopbackHost, endpoint.Port);
        using var endpointClient = new UdpClientRx(AddressFamily.InterNetwork);
        endpointClient.Connect(endpoint);
        using var hostClient = new UdpClientRx(AddressFamily.InterNetwork);
        hostClient.Connect(Ipv4LoopbackHost, endpoint.Port);

        defaultClient.ExclusiveAddressUse = true;
        await defaultClient.OpenAsync();
        await defaultClient.OpenAsync();
        defaultClient.Close();
        await defaultClient.OpenAsync();
        defaultClient.Close();

        byte[] sent = [ByteLetterA];
        var sentCount = await localClient.SendAsync(sent, 1, endpoint);
        var received = await target.ReceiveAsync(cancellationToken);

        using var receiveSender = new UdpClient(AddressFamily.InterNetwork);
        var localEndpoint = (IPEndPoint)(localClient.Client.LocalEndPoint ??
            throw new InvalidOperationException("The UDP local client did not expose an endpoint."));
        await receiveSender.SendAsync(sent, localEndpoint, cancellationToken);
        var directReceive = await localClient.ReceiveAsync();

        using var shortReceiver = new UdpClientRx(new IPEndPoint(IPAddress.Loopback, 0));
        var shortEndpoint = (IPEndPoint)(shortReceiver.Client.LocalEndPoint ??
            throw new InvalidOperationException("The UDP short receiver did not expose an endpoint."));
        await receiveSender.SendAsync(sent, shortEndpoint, cancellationToken);
        var shortBuffer = new byte[Two];

        await Assert.That(() => shortReceiver.ReadAsync(shortBuffer, 0, Two))
            .Throws<InvalidOperationException>();
        await Assert.That(sentCount).IsEqualTo(1);
        await Assert.That(received.Buffer).IsEquivalentTo(sent);
        await Assert.That(directReceive.Buffer).IsEquivalentTo(sent);
        await Assert.That(defaultClient.ExclusiveAddressUse).IsTrue();
        await Assert.That(defaultClient.Available).IsEqualTo(0);
        await Assert.That(portClient.Client.AddressFamily).IsEqualTo(AddressFamily.InterNetwork);
        await Assert.That(familyPortClient.Client.AddressFamily).IsEqualTo(AddressFamily.InterNetwork);
        await Assert.That(namedClient.Client.Connected).IsTrue();
        await Assert.That(endpointClient.Client.Connected).IsTrue();
        await Assert.That(hostClient.Client.Connected).IsTrue();
        await Assert.That(ReferenceEquals(localClient.DataReceivedAsync, localClient.DataReceivedAsync)).IsTrue();
        await Assert.That(ReferenceEquals(localClient.DataReceivedBatchesAsync, localClient.DataReceivedBatchesAsync))
            .IsTrue();
        await Assert.That(ReferenceEquals(localClient.BytesReceivedAsync, localClient.BytesReceivedAsync)).IsTrue();
        await Assert.That(() => localClient.DiscardInBuffer()).ThrowsNothing();
        defaultClient.Dispose();
        defaultClient.Dispose();
    }

    /// <summary>Starts a TCP listener with framework-independent disposal semantics.</summary>
    /// <returns>The started listener scope.</returns>
    private static TcpListenerScope StartTcpListener()
    {
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        return new TcpListenerScope(listener);
    }

    /// <summary>Accepts a TCP client while honoring cancellation on every supported framework.</summary>
    /// <param name="listener">The listening socket.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The accepted TCP client.</returns>
    private static async Task<TcpClient> AcceptTcpClientAsync(TcpListener listener, CancellationToken cancellationToken)
    {
#if NETFRAMEWORK
        var acceptTask = listener.AcceptTcpClientAsync();
        var cancellationTask = Task.Delay(Timeout.Infinite, cancellationToken);
        if (await Task.WhenAny(acceptTask, cancellationTask).ConfigureAwait(false) != acceptTask)
        {
            throw new OperationCanceledException(cancellationToken);
        }

        return await acceptTask.ConfigureAwait(false);
#else
        return await listener.AcceptTcpClientAsync(cancellationToken).ConfigureAwait(false);
#endif
    }

    /// <summary>Owns a TCP listener and stops it when the test completes.</summary>
    private sealed class TcpListenerScope : IDisposable
    {
        /// <summary>Initializes a new instance of the <see cref="TcpListenerScope"/> class.</summary>
        /// <param name="listener">The listener to own.</param>
        internal TcpListenerScope(TcpListener listener) => Listener = listener;

        /// <summary>Gets the owned listener.</summary>
        internal TcpListener Listener { get; }

        /// <inheritdoc/>
        public void Dispose() => Listener.Stop();
    }
}
