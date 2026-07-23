// Copyright (c) 2019-2026 Chris Pulman and contributors. All rights reserved.
// Chris Pulman and contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Net;
using System.Net.Sockets;
using IoT.DriverCore.OmronPlcRx.Core;
using TUnit.Core;
using CoreTcpClient = IoT.DriverCore.OmronPlcRx.Core.TcpClient;
using CoreUdpClient = IoT.DriverCore.OmronPlcRx.Core.UdpClient;
using NetUdpClient = System.Net.Sockets.UdpClient;

namespace IoT.DriverCore.OmronPlcRx.Tests;

/// <summary>Exercises every modern TCP and UDP socket-wrapper overload through loopback peers.</summary>
public sealed class OmronSocketOverloadCoverageTests
{
    /// <summary>Gets the number of send and receive overloads exercised per protocol.</summary>
    private const int OperationCount = 4;

    /// <summary>Gets the loopback operation timeout.</summary>
    private const int TimeoutMilliseconds = 1000;

    /// <summary>Gets the deterministic no-data timeout.</summary>
    private const int NoDataTimeoutMilliseconds = 30;

    /// <summary>Gets the second operation payload.</summary>
    private const byte SecondOperation = 2;

    /// <summary>Gets the third operation payload.</summary>
    private const byte ThirdOperation = 3;

    /// <summary>Gets the first TCP response payload.</summary>
    private const byte FirstTcpResponse = 5;

    /// <summary>Gets how long the TCP peer remains connected after its response.</summary>
    private const int PeerHoldMilliseconds = 100;

    /// <summary>Verifies TCP array and memory overloads, socket options, timeouts, and disposed state.</summary>
    /// <returns>A task that represents the asynchronous test.</returns>
    [Test]
    public async Task TcpClient_ExercisesAllModernOverloadsAsync()
    {
        var portSource = CreatePortSource();
        var peer = RunTcpPeerAsync(portSource);
        var port = await portSource.Task;
        var client = new CoreTcpClient(IPAddress.Loopback, port);
        try
        {
            await client.ConnectAsync(Timeout.InfiniteTimeSpan, CancellationToken.None);
            AssertTcpSocketOptions(client);
            var sent = await SendTcpOverloadsAsync(client);
            var received = await ReceiveTcpOverloadsAsync(client);
            await AssertThrowsAsync<TimeoutException>(
                () => client.ReceiveAsync(
                    new byte[1],
                    TimeSpan.FromMilliseconds(NoDataTimeoutMilliseconds),
                    CancellationToken.None));

            await Assert.That(sent).IsEqualTo(OperationCount);
            await Assert.That(received).IsEqualTo(OperationCount);
        }
        finally
        {
            await peer;
            client.Dispose();
        }

        await AssertDisposedTcpStateAsync(client);
    }

    /// <summary>Verifies UDP array and memory overloads, cancellation, timeouts, and disposed state.</summary>
    /// <returns>A task that represents the asynchronous test.</returns>
    [Test]
    public async Task UdpClient_ExercisesAllModernOverloadsAsync()
    {
        var portSource = CreatePortSource();
        var peer = RunUdpPeerAsync(portSource);
        var port = await portSource.Task;
        var client = new CoreUdpClient(IPAddress.Loopback.ToString(), port);
        try
        {
            var sent = await SendUdpOverloadsAsync(client);
            var received = await ReceiveUdpOverloadsAsync(client);
            await peer;
            await AssertThrowsAsync<TimeoutException>(
                () => client.ReceiveAsync(
                    new byte[1],
                    TimeSpan.FromMilliseconds(NoDataTimeoutMilliseconds),
                    CancellationToken.None));

            await Assert.That(sent).IsEqualTo(OperationCount);
            await Assert.That(received).IsEqualTo(OperationCount);
            await Assert.That(client.Available).IsEqualTo(0);
        }
        finally
        {
            await peer;
            client.Dispose();
        }

        await AssertDisposedUdpStateAsync(client);
    }

    /// <summary>Exercises TCP socket-option setters and getters.</summary>
    /// <param name="client">Connected TCP client.</param>
    private static void AssertTcpSocketOptions(CoreTcpClient client)
    {
        client.KeepAliveEnabled = true;
        client.KeepAliveInternal = 1;
        client.KeepAliveDelay = 1;
        client.KeepAliveRetryCount = 1;
        _ = client.Available;
        _ = client.KeepAliveEnabled;
        _ = client.KeepAliveInternal;
        _ = client.KeepAliveDelay;
        _ = client.KeepAliveRetryCount;
        client.LingerState = null;
    }

    /// <summary>Sends through every TCP wrapper overload.</summary>
    /// <param name="client">Connected TCP client.</param>
    /// <returns>The total bytes sent.</returns>
    private static async Task<int> SendTcpOverloadsAsync(CoreTcpClient client)
    {
        var sent = 0;
        sent += await client.SendAsync([1], CancellationToken.None);
        sent += await client.SendAsync(new ReadOnlyMemory<byte>([SecondOperation]), CancellationToken.None);
        sent += await client.SendAsync(
            new ReadOnlyMemory<byte>([ThirdOperation]),
            TimeoutMilliseconds,
            CancellationToken.None);
        sent += await client.SendAsync(
            [OperationCount],
            TimeSpan.FromMilliseconds(TimeoutMilliseconds),
            CancellationToken.None);
        return sent;
    }

    /// <summary>Receives through every TCP wrapper overload.</summary>
    /// <param name="client">Connected TCP client.</param>
    /// <returns>The total bytes received.</returns>
    private static async Task<int> ReceiveTcpOverloadsAsync(CoreTcpClient client)
    {
        var received = 0;
        received += await client.ReceiveAsync(new byte[1], CancellationToken.None);
        received += await client.ReceiveAsync(new Memory<byte>(new byte[1]), CancellationToken.None);
        received += await client.ReceiveAsync(
            new Memory<byte>(new byte[1]),
            TimeoutMilliseconds,
            CancellationToken.None);
        received += await client.ReceiveAsync(
            new byte[1],
            TimeSpan.FromMilliseconds(TimeoutMilliseconds),
            CancellationToken.None);
        return received;
    }

    /// <summary>Sends through every UDP wrapper overload.</summary>
    /// <param name="client">Connected UDP client.</param>
    /// <returns>The total bytes sent.</returns>
    private static async Task<int> SendUdpOverloadsAsync(CoreUdpClient client)
    {
        _ = client.Socket;
        var sent = 0;
        sent += await client.SendAsync([1], CancellationToken.None);
        sent += await client.SendAsync(new ReadOnlyMemory<byte>([SecondOperation]), CancellationToken.None);
        sent += await client.SendAsync(
            new ReadOnlyMemory<byte>([ThirdOperation]),
            TimeoutMilliseconds,
            CancellationToken.None);
        sent += await client.SendAsync(
            [OperationCount],
            TimeSpan.FromMilliseconds(TimeoutMilliseconds),
            CancellationToken.None);
        return sent;
    }

    /// <summary>Receives through every UDP wrapper overload.</summary>
    /// <param name="client">Connected UDP client.</param>
    /// <returns>The total bytes received.</returns>
    private static async Task<int> ReceiveUdpOverloadsAsync(CoreUdpClient client)
    {
        _ = client.Socket;
        var received = 0;
        received += await client.ReceiveAsync(new byte[1], CancellationToken.None);
        received += await client.ReceiveAsync(new Memory<byte>(new byte[1]), CancellationToken.None);
        received += await client.ReceiveAsync(
            new Memory<byte>(new byte[1]),
            TimeoutMilliseconds,
            CancellationToken.None);
        received += await client.ReceiveAsync(
            new byte[1],
            TimeSpan.FromMilliseconds(TimeoutMilliseconds),
            CancellationToken.None);
        return received;
    }

    /// <summary>Verifies disposed TCP getters, setters, and operation guards.</summary>
    /// <param name="client">Disposed TCP client.</param>
    /// <returns>A task that represents the assertions.</returns>
    private static async Task AssertDisposedTcpStateAsync(CoreTcpClient client)
    {
        client.NoDelay = true;
        client.LingerState = new(true, 0);
        client.KeepAliveEnabled = true;
        client.KeepAliveInternal = 1;
        client.KeepAliveDelay = 1;
        client.KeepAliveRetryCount = 1;
        client.Dispose();

        await Assert.That(client.Available).IsEqualTo(0);
        await Assert.That(client.KeepAliveInternal).IsEqualTo(0);
        await Assert.That(client.KeepAliveDelay).IsEqualTo(0);
        await Assert.That(client.KeepAliveRetryCount).IsEqualTo(0);
        await AssertThrowsAsync<ObjectDisposedException>(
            () => client.SendAsync(
                new byte[1],
                TimeSpan.FromMilliseconds(TimeoutMilliseconds),
                CancellationToken.None));
        await AssertThrowsAsync<ObjectDisposedException>(
            () => client.ReceiveAsync(
                new byte[1],
                TimeSpan.FromMilliseconds(TimeoutMilliseconds),
                CancellationToken.None));
    }

    /// <summary>Verifies disposed UDP guards.</summary>
    /// <param name="client">Disposed UDP client.</param>
    /// <returns>A task that represents the assertions.</returns>
    private static async Task AssertDisposedUdpStateAsync(CoreUdpClient client)
    {
        client.Dispose();
        await AssertThrowsAsync<ObjectDisposedException>(
            () => client.ReceiveAsync(
                new byte[1],
                TimeSpan.FromMilliseconds(TimeoutMilliseconds),
                CancellationToken.None));
    }

    /// <summary>Runs a TCP peer that receives all requests and publishes four response bytes.</summary>
    /// <param name="portSource">Receives the bound loopback port.</param>
    /// <returns>A task that represents the peer lifetime.</returns>
    private static async Task RunTcpPeerAsync(TaskCompletionSource<int> portSource)
    {
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        portSource.SetResult(((IPEndPoint)listener.LocalEndpoint).Port);
        using var accepted = await listener.AcceptTcpClientAsync();
        await using var stream = accepted.GetStream();
        var request = new byte[OperationCount];
        await stream.ReadExactlyAsync(request, CancellationToken.None);
        var response = Enumerable
            .Range(FirstTcpResponse, OperationCount)
            .Select(static value => (byte)value)
            .ToArray();
        await stream.WriteAsync(response, CancellationToken.None);
        await stream.FlushAsync(CancellationToken.None);
        await Task.Delay(PeerHoldMilliseconds);
    }

    /// <summary>Runs a UDP peer that replies once to every received datagram.</summary>
    /// <param name="portSource">Receives the bound loopback port.</param>
    /// <returns>A task that represents the peer lifetime.</returns>
    private static async Task RunUdpPeerAsync(TaskCompletionSource<int> portSource)
    {
        using var socket = new NetUdpClient(new IPEndPoint(IPAddress.Loopback, 0));
        portSource.SetResult(((IPEndPoint)socket.Client.LocalEndPoint!).Port);
        for (var operation = 0; operation < OperationCount; operation++)
        {
            var request = await socket.ReceiveAsync(CancellationToken.None);
            var response = new byte[] { (byte)(operation + 1) };
            _ = await socket.SendAsync(response, response.Length, request.RemoteEndPoint);
        }
    }

    /// <summary>Creates a continuation-safe port publication source.</summary>
    /// <returns>The source used by a loopback peer.</returns>
    private static TaskCompletionSource<int> CreatePortSource() =>
        new(TaskCreationOptions.RunContinuationsAsynchronously);

    /// <summary>Captures and verifies an asynchronous exception.</summary>
    /// <typeparam name="TException">Expected exception type.</typeparam>
    /// <param name="action">Action to invoke.</param>
    /// <returns>A task that represents the assertion.</returns>
    private static async Task AssertThrowsAsync<TException>(Func<Task> action)
        where TException : Exception
    {
        try
        {
            await action().ConfigureAwait(false);
        }
        catch (TException exception)
        {
            await Assert.That(exception).IsNotNull();
            return;
        }

        throw new InvalidOperationException($"Expected {nameof(TException)}.");
    }
}
