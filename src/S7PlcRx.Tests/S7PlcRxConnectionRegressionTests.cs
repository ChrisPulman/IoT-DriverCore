// Copyright (c) 2019-2026 Chris Pulman and contributors. All rights reserved.
// Chris Pulman and contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers.Binary;
using System.Net;
using System.Net.Sockets;
using MockS7Plc;
using S7PlcRx.Enums;
using TUnit.Assertions.Extensions;
using TUnitAssert = TUnit.Assertions.Assert;

namespace S7PlcRx.Tests;

/// <summary>Regression tests for connection readiness and watchdog behavior.</summary>
[NotInParallel]
[System.Diagnostics.DebuggerDisplay("{DebuggerDisplay,nq}")]
public sealed class S7PlcRxConnectionRegressionTests
{
    /// <summary>Gets the live PLC address.</summary>
    private const string LivePlcIp = "172.16.13.1";

    /// <summary>Gets the default PLC polling interval in milliseconds.</summary>
    private const int DefaultPollingIntervalMilliseconds = 50;

    /// <summary>Gets the short mock connection timeout in seconds.</summary>
    private const int MockConnectionTimeoutSeconds = 5;

    /// <summary>Gets the live PLC connection timeout in seconds.</summary>
    private const int LivePlcConnectionTimeoutSeconds = 15;

    /// <summary>Gets the watchdog connection timeout in seconds.</summary>
    private const int WatchdogConnectionTimeoutSeconds = 10;

    /// <summary>Gets the CPU information field count expected by the test.</summary>
    private const int MinimumCpuInfoFieldCount = 9;

    /// <summary>Gets the CPU identity field index.</summary>
    private const int CpuIdentityFieldIndex = 5;

    /// <summary>Gets the configured watchdog data-block size.</summary>
    private const int WatchdogDataBlockSize = 16;

    /// <summary>Gets the offset of the watchdog word in the data block.</summary>
    private const int WatchdogWordOffset = 0;

    /// <summary>Gets the watchdog word length in bytes.</summary>
    private const int WatchdogWordLength = sizeof(ushort);

    /// <summary>Gets the watchdog polling interval in milliseconds.</summary>
    private const int WatchdogPollingIntervalMilliseconds = 50;

    /// <summary>Gets the watchdog wait timeout in seconds.</summary>
    private const int WatchdogWaitTimeoutSeconds = 5;

    /// <summary>Gets the watchdog polling delay in milliseconds.</summary>
    private const int WatchdogPollingDelayMilliseconds = 50;

    /// <summary>Gets the successful mock-server start result.</summary>
    private const int MockServerStartSuccess = 0;

    /// <summary>Gets the debugger display text.</summary>
    [System.Diagnostics.DebuggerBrowsable(System.Diagnostics.DebuggerBrowsableState.Never)]
    private string DebuggerDisplay
    {
        get => ToString() ?? string.Empty;
    }

    /// <summary>Ensures connection readiness after the ISO/S7 setup handshake completes.</summary>
    /// <returns>A task representing the asynchronous test operation.</returns>
    [Test]
    public async Task IsConnected_WhenSetupHandshakeSucceedsWithoutSzlReadiness_ShouldBecomeTrueAsync()
    {
        _ = DebuggerDisplay;
        using var server = new HandshakeOnlyS7Server();
        using var plc = S71500.Create(
            MockServer.Localhost,
            0,
            1,
            null,
            interval: DefaultPollingIntervalMilliseconds);

        var connected = await plc.IsConnected
            .Where(static x => x)
            .Take(1)
            .Timeout(TimeSpan.FromSeconds(MockConnectionTimeoutSeconds))
            .FirstAsync();

        await TUnitAssert.That(connected).IsTrue();
        await TUnitAssert.That(server.HandshakeCount).IsGreaterThanOrEqualTo(1);
        await TUnitAssert.That(server.UnsupportedRequestCount).IsEqualTo(0);
    }

    /// <summary>Ensures handshake frames can arrive fragmented across multiple TCP reads.</summary>
    /// <returns>A task representing the asynchronous test operation.</returns>
    [Test]
    public async Task IsConnected_WhenHandshakeResponsesAreFragmented_ShouldBecomeTrueAsync()
    {
        _ = DebuggerDisplay;
        using var server = new HandshakeOnlyS7Server(fragmentHandshakeResponses: true);
        using var plc = S71500.Create(
            MockServer.Localhost,
            0,
            1,
            null,
            interval: DefaultPollingIntervalMilliseconds);

        var connected = await plc.IsConnected
            .Where(static x => x)
            .Take(1)
            .Timeout(TimeSpan.FromSeconds(MockConnectionTimeoutSeconds))
            .FirstAsync();

        await TUnitAssert.That(connected).IsTrue();
        await TUnitAssert.That(server.HandshakeCount).IsGreaterThanOrEqualTo(1);
        await TUnitAssert.That(server.UnsupportedRequestCount).IsEqualTo(0);
    }

    /// <summary>Ensures a live S7-1500 can reach connected state without any program-specific DB reads.</summary>
    /// <returns>A task representing the asynchronous test operation.</returns>
    [Test]
    [Explicit]
    [Category("LivePLC")]
    public async Task IsConnected_ToLiveS71500_ShouldBecomeTrueAsync()
    {
        _ = DebuggerDisplay;
        using var plc = S71500.Create(LivePlcIp, interval: DefaultPollingIntervalMilliseconds);

        var connected = await plc.IsConnected
            .Where(static x => x)
            .Take(1)
            .Timeout(TimeSpan.FromSeconds(LivePlcConnectionTimeoutSeconds))
            .FirstAsync();

        await TUnitAssert.That(connected).IsTrue();
    }

    /// <summary>Ensures live CPU diagnostics complete without any program-specific DB reads.</summary>
    /// <returns>A task representing the asynchronous test operation.</returns>
    [Test]
    [Explicit]
    [Category("LivePLC")]
    public async Task GetCpuInfo_ToLiveS71500_ShouldCompleteAndReturnIdentityFieldsAsync()
    {
        _ = DebuggerDisplay;
        using var plc = S71500.Create(LivePlcIp, interval: DefaultPollingIntervalMilliseconds);

        var cpuInfo = await plc.GetCpuInfo()
            .Take(1)
            .Timeout(TimeSpan.FromSeconds(LivePlcConnectionTimeoutSeconds))
            .FirstAsync();

        await TUnitAssert.That(cpuInfo).IsNotNull();
        await TUnitAssert.That(cpuInfo.Length).IsGreaterThanOrEqualTo(MinimumCpuInfoFieldCount);
        await TUnitAssert.That(cpuInfo.Any(static value => !string.IsNullOrWhiteSpace(value))).IsTrue();
        await TUnitAssert.That(cpuInfo[CpuIdentityFieldIndex]).IsNotNull();
        await TUnitAssert.That(cpuInfo[CpuIdentityFieldIndex].Trim()).IsNotEmpty();
    }

    /// <summary>Ensures watchdog writes start once a normal MockS7Plc connection is established.</summary>
    /// <returns>A task representing the asynchronous test operation.</returns>
    [Test]
    public async Task Watchdog_WhenConnectedToMockPlc_ShouldWriteConfiguredWordAsync()
    {
        _ = DebuggerDisplay;
        const ushort watchdogValue = 1234;

        using var server = new MockServer { DefaultDb1Size = WatchdogDataBlockSize };
        await TUnitAssert.That(server.Start()).IsEqualTo(MockServerStartSuccess);

        using var plc = new RxS7(
            new(
                new(CpuType.S71500, MockServer.Localhost, 0, 1),
                new(WatchdogPollingIntervalMilliseconds),
                new("DB1.DBW0", watchdogValue, 1)));

        var connected = await plc.IsConnected
            .Where(static x => x)
            .Take(1)
            .Timeout(TimeSpan.FromSeconds(WatchdogConnectionTimeoutSeconds))
            .FirstAsync();

        await TUnitAssert.That(connected).IsTrue();

        var watchdogWritten = await WaitUntilAsync(
            () => BinaryPrimitives.ReadUInt16BigEndian(
                server.DefaultDb1!.AsSpan(WatchdogWordOffset, WatchdogWordLength)) == watchdogValue,
            TimeSpan.FromSeconds(WatchdogWaitTimeoutSeconds));

        await TUnitAssert.That(watchdogWritten).IsTrue();
        var writtenWatchdogValue = BinaryPrimitives.ReadUInt16BigEndian(
            server.DefaultDb1!.AsSpan(WatchdogWordOffset, WatchdogWordLength));
        await TUnitAssert.That(writtenWatchdogValue).IsEqualTo(watchdogValue);
    }

    /// <summary>Waits until a predicate succeeds or the timeout elapses.</summary>
    /// <param name="predicate">The predicate to evaluate.</param>
    /// <param name="timeout">The maximum time to wait.</param>
    /// <param name="timeProvider">The time provider to use for deadline tracking; defaults to <see cref="TimeProvider.System"/>.</param>
    /// <returns><see langword="true"/> when the predicate succeeds; otherwise, <see langword="false"/>.</returns>
    private static async Task<bool> WaitUntilAsync(Func<bool> predicate, TimeSpan timeout, TimeProvider? timeProvider = null)
    {
        var tp = timeProvider ?? TimeProvider.System;
        var deadline = tp.GetUtcNow().UtcDateTime + timeout;
        while (tp.GetUtcNow().UtcDateTime < deadline)
        {
            if (predicate())
            {
                return true;
            }

            await Task.Delay(WatchdogPollingDelayMilliseconds).ConfigureAwait(false);
        }

        return predicate();
    }

    /// <summary>Provides the minimum TCP server behavior required for the S7 setup handshake.</summary>
    private sealed class HandshakeOnlyS7Server : IDisposable
    {
        /// <summary>Gets the listener TCP port.</summary>
        private const int ListenerPort = 102;

        /// <summary>Gets the TPKT header length.</summary>
        private const int TpktHeaderLength = 4;

        /// <summary>Gets the per-byte write size for fragmented frames.</summary>
        private const int FragmentedWriteSize = 1;

        /// <summary>Gets the delay between fragmented writes in milliseconds.</summary>
        private const int FragmentedWriteDelayMilliseconds = 2;

        /// <summary>Gets the accept-loop shutdown wait timeout in seconds.</summary>
        private const int AcceptLoopShutdownTimeoutSeconds = 1;

        /// <summary>Gets the connection-confirmation frame.</summary>
        private static readonly byte[] ConnectionConfirm =
        [
            0x03, 0x00, 0x00, 0x16, 0x11, 0xD0, 0x00, 0x01, 0x00, 0x2E, 0x00,
            0xC0, 0x01, 0x09, 0xC1, 0x02, 0x03, 0x01, 0xC2, 0x02, 0x01, 0x00
        ];

        /// <summary>Gets the setup-confirmation frame.</summary>
        private static readonly byte[] SetupConfirm =
        [
            0x03, 0x00, 0x00, 0x1B, 0x02, 0xF0, 0x80, 0x32, 0x03, 0x00, 0x00,
            0x04, 0x00, 0x00, 0x08, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
            0xF0, 0x00, 0x00, 0x03, 0xC0
        ];

        /// <summary>Gets the response for unsupported diagnostic requests.</summary>
        private static readonly byte[] UnsupportedDiagnosticResponse =
        [
            0x03, 0x00, 0x00, 0x10, 0x02, 0xF0, 0x80, 0x32,
            0x03, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00
        ];

        /// <summary>Controls cancellation of the server loops.</summary>
        private readonly CancellationTokenSource _cancellationTokenSource = new();

        /// <summary>Listens for client connections.</summary>
        private readonly TcpListener _listener;

        /// <summary>Runs the client accept loop.</summary>
        private readonly Task _acceptLoop;

        /// <summary>Indicates whether handshake frames should be fragmented.</summary>
        private readonly bool _fragmentHandshakeResponses;

        /// <summary>Indicates whether the server has been disposed.</summary>
        private bool _disposed;

        /// <summary>Stores the completed handshake count.</summary>
        private int _handshakeCount;

        /// <summary>Stores the unsupported-request count.</summary>
        private int _unsupportedRequestCount;

        /// <summary>Initializes a new instance of the <see cref="HandshakeOnlyS7Server"/> class.</summary>
        /// <param name="fragmentHandshakeResponses">Whether handshake frames should be fragmented.</param>
        public HandshakeOnlyS7Server(bool fragmentHandshakeResponses = false)
        {
            _fragmentHandshakeResponses = fragmentHandshakeResponses;
            _listener = new(IPAddress.Loopback, ListenerPort);
            _listener.Start();
            _acceptLoop = AcceptLoopAsync();
        }

        /// <summary>Gets the number of completed handshakes.</summary>
        public int HandshakeCount => Volatile.Read(ref _handshakeCount);

        /// <summary>Gets the number of unsupported requests received.</summary>
        public int UnsupportedRequestCount => Volatile.Read(ref _unsupportedRequestCount);

        /// <inheritdoc/>
        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            _cancellationTokenSource.Cancel();
            _listener.Stop();
            ((IDisposable)_listener).Dispose();

            try
            {
                _ = _acceptLoop.Wait(TimeSpan.FromSeconds(AcceptLoopShutdownTimeoutSeconds));
            }
            catch (AggregateException)
            {
            }

            _cancellationTokenSource.Dispose();
        }

        /// <summary>Reads one TPKT frame from the stream.</summary>
        /// <param name="stream">The stream to read.</param>
        /// <param name="buffer">The frame buffer.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>The frame length, or zero when a complete frame cannot be read.</returns>
        private static async Task<int> ReadTpktAsync(
            NetworkStream stream,
            byte[] buffer,
            CancellationToken cancellationToken)
        {
            if (!await ReadExactAsync(
                stream,
                buffer,
                WatchdogWordOffset,
                TpktHeaderLength,
                cancellationToken).ConfigureAwait(false))
            {
                return 0;
            }

            var length = (buffer[2] << 8) | buffer[3];
            if (length < TpktHeaderLength || length > buffer.Length)
            {
                return 0;
            }

            return await ReadExactAsync(
                stream,
                buffer,
                TpktHeaderLength,
                length - TpktHeaderLength,
                cancellationToken).ConfigureAwait(false)
                ? length
                : 0;
        }

        /// <summary>Reads exactly the requested number of bytes from the stream.</summary>
        /// <param name="stream">The stream to read.</param>
        /// <param name="buffer">The destination buffer.</param>
        /// <param name="offset">The offset at which to begin writing.</param>
        /// <param name="count">The number of bytes to read.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>
        /// <see langword="true"/> when the requested bytes are read; otherwise, <see langword="false"/>.
        /// </returns>
        private static async Task<bool> ReadExactAsync(
            NetworkStream stream,
            byte[] buffer,
            int offset,
            int count,
            CancellationToken cancellationToken)
        {
            var total = 0;
            while (total < count)
            {
#if NETSTANDARD2_1_OR_GREATER || NETCOREAPP2_1_OR_GREATER
                var read = await stream.ReadAsync(
                    buffer.AsMemory(offset + total, count - total),
                    cancellationToken).ConfigureAwait(false);
#else
                var read = await stream.ReadAsync(
                    buffer,
                    offset + total,
                    count - total,
                    cancellationToken).ConfigureAwait(false);
#endif
                if (read <= 0)
                {
                    return false;
                }

                total += read;
            }

            return true;
        }

        /// <summary>Writes a complete frame to the stream.</summary>
        /// <param name="stream">The stream to write.</param>
        /// <param name="buffer">The frame to write.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>A task representing the asynchronous write operation.</returns>
        private static async Task WriteFrameAsync(
            NetworkStream stream,
            byte[] buffer,
            CancellationToken cancellationToken)
        {
#if NETSTANDARD2_1_OR_GREATER || NETCOREAPP2_1_OR_GREATER
            await stream.WriteAsync(buffer.AsMemory(0, buffer.Length), cancellationToken).ConfigureAwait(false);
#else
            await stream.WriteAsync(buffer, 0, buffer.Length, cancellationToken).ConfigureAwait(false);
#endif
        }

        /// <summary>Writes a handshake frame, optionally one byte at a time.</summary>
        /// <param name="stream">The stream to write.</param>
        /// <param name="buffer">The handshake frame.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>A task representing the asynchronous write operation.</returns>
        private async Task WriteHandshakeFrameAsync(
            NetworkStream stream,
            byte[] buffer,
            CancellationToken cancellationToken)
        {
            if (!_fragmentHandshakeResponses)
            {
                await WriteFrameAsync(stream, buffer, cancellationToken).ConfigureAwait(false);
                return;
            }

            for (var i = 0; i < buffer.Length; i++)
            {
#if NETSTANDARD2_1_OR_GREATER || NETCOREAPP2_1_OR_GREATER
                await stream.WriteAsync(
                    buffer.AsMemory(i, FragmentedWriteSize),
                    cancellationToken).ConfigureAwait(false);
#else
                await stream.WriteAsync(buffer, i, FragmentedWriteSize, cancellationToken).ConfigureAwait(false);
#endif
                await stream.FlushAsync(cancellationToken).ConfigureAwait(false);
                await Task.Delay(FragmentedWriteDelayMilliseconds, cancellationToken).ConfigureAwait(false);
            }
        }

        /// <summary>Accepts incoming clients until cancellation is requested.</summary>
        /// <returns>A task representing the asynchronous accept loop.</returns>
        private async Task AcceptLoopAsync()
        {
            while (!_cancellationTokenSource.IsCancellationRequested)
            {
                TcpClient client;
                try
                {
                    client = await _listener.AcceptTcpClientAsync().ConfigureAwait(false);
                }
                catch (ObjectDisposedException) when (_cancellationTokenSource.IsCancellationRequested)
                {
                    break;
                }
                catch (SocketException) when (_cancellationTokenSource.IsCancellationRequested)
                {
                    break;
                }

                _ = HandleClientAsync(client, _cancellationTokenSource.Token);
            }
        }

        /// <summary>Handles one connected client.</summary>
        /// <param name="client">The connected client.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>A task representing the asynchronous client operation.</returns>
        private async Task HandleClientAsync(TcpClient client, CancellationToken cancellationToken)
        {
            using (client)
            {
                client.NoDelay = true;
                var stream = client.GetStream();
                var buffer = new byte[256];

                if (await ReadTpktAsync(stream, buffer, cancellationToken).ConfigureAwait(false) <= 0)
                {
                    return;
                }

                await WriteHandshakeFrameAsync(stream, ConnectionConfirm, cancellationToken).ConfigureAwait(false);

                if (await ReadTpktAsync(stream, buffer, cancellationToken).ConfigureAwait(false) <= 0)
                {
                    return;
                }

                await WriteHandshakeFrameAsync(stream, SetupConfirm, cancellationToken).ConfigureAwait(false);
                _ = Interlocked.Increment(ref _handshakeCount);

                while (!cancellationToken.IsCancellationRequested)
                {
                    if (await ReadTpktAsync(stream, buffer, cancellationToken).ConfigureAwait(false) <= 0)
                    {
                        return;
                    }

                    _ = Interlocked.Increment(ref _unsupportedRequestCount);
                    await WriteFrameAsync(
                        stream,
                        UnsupportedDiagnosticResponse,
                        cancellationToken).ConfigureAwait(false);
                }
            }
        }
    }
}
