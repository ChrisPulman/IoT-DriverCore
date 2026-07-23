// Copyright (c) 2019-2026 Chris Pulman and contributors. All rights reserved.
// Chris Pulman and contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Net;
using System.Net.Sockets;
using IoT.DriverCore.S7PlcRx.Core;
using IoT.DriverCore.S7PlcRx.Enums;
using TUnitAssert = TUnit.Assertions.Assert;

namespace IoT.DriverCore.S7PlcRx.Tests.Core;

/// <summary>Provides deterministic background-lifecycle coverage for the S7 socket transport.</summary>
public sealed partial class S7TransportCoreDeterministicCoverageTests
{
    /// <summary>Verifies availability hysteresis, port probing, and single-flight scheduling.</summary>
    /// <returns>A task that represents the asynchronous assertions.</returns>
    [Test]
    public async Task SocketAvailabilityHysteresisAndNegotiationFallbacksAreDeterministicAsync()
    {
        await VerifyAvailabilityFailureHysteresisAsync();
        await VerifyAvailabilityObserverAndNegotiationAsync();
        await VerifyPortProbingAsync();
        await VerifyAvailabilityProbeSingleFlightAsync();
    }

    /// <summary>Verifies handshake failures and cancellation of a pending peer response.</summary>
    /// <returns>A task that represents the asynchronous assertions.</returns>
    [Test]
    public async Task SocketHandshakeRetriesAllProfilesAndRejectsMalformedFramesAsync()
    {
        await VerifyAllTsapProfilesAreRejectedAsync();
        await VerifyMalformedTpktFramesAsync();
        await VerifyConnectionInitializationGuardsAsync();
        await VerifyPendingHandshakeCancellationAsync();
    }

    /// <summary>Verifies timer callbacks cannot overlap availability probes.</summary>
    /// <returns>A task that represents the asynchronous assertions.</returns>
    private static async Task VerifyAvailabilityProbeSingleFlightAsync()
    {
        var socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
        var transport = new S7SocketRx(
            " ",
            CpuType.S71500,
            RackNumber,
            SlotNumber,
            socket,
            TimeProvider.System);
        var observer = new BlockingObserver<bool>();
        try
        {
            InvokePrivate(
                transport,
                "ProbeAvailabilityAndNotify",
                [typeof(IObserver<bool>)],
                observer);
            var firstProbe = GetPrivateField<Task>(transport, "_availabilityProbeTask");
            try
            {
                await AsyncCompatibility.WaitAsync(observer.Entered, LoopbackTimeout);

                InvokePrivate(
                    transport,
                    "ProbeAvailabilityAndNotify",
                    [typeof(IObserver<bool>)],
                    observer);
                var secondProbe = GetPrivateField<Task>(transport, "_availabilityProbeTask");

                observer.Release();
                await secondProbe;
                await firstProbe;
                await TUnitAssert.That(ReferenceEquals(firstProbe, secondProbe)).IsTrue();
                await TUnitAssert.That(observer.NotificationCount).IsEqualTo(1);
            }
            finally
            {
                observer.Release();
                await firstProbe;
            }
        }
        finally
        {
            ((IDisposable)transport).Dispose();
            socket.Dispose();
        }
    }

    /// <summary>Verifies lifetime cancellation drains a handshake waiting for a peer response.</summary>
    /// <returns>A task that represents the asynchronous assertions.</returns>
    private static async Task VerifyPendingHandshakeCancellationAsync()
    {
        var listener = new TcpListener(IPAddress.Loopback, 0);
        using var listenerLifetime = NetworkCompatibility.StopOnDispose(listener);
        listener.Start();
        var endpoint = (IPEndPoint)listener.LocalEndpoint;
        var socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
        var transport = new S7SocketRx(
            IPAddress.Loopback.ToString(),
            CpuType.S71500,
            RackNumber,
            SlotNumber,
            socket,
            TimeProvider.System,
            endpoint.Port);
        Socket? acceptedSocket = null;
        var lifetime = GetPrivateField<CancellationTokenSource>(transport, "_lifetimeCancellation");
        var profile = GetPrivateTsapProfile("PG");
        var connectTask = InvokePrivateTaskAsync<Socket?>(
            transport,
            "ConnectWithProfileAsync",
            [profile.GetType()],
            profile);
        try
        {
            acceptedSocket = await AsyncCompatibility.WaitAsync(listener.AcceptSocketAsync(), LoopbackTimeout);

            lifetime.Cancel();
            await TUnitAssert.That(
                await AsyncCompatibility.WaitAsync(connectTask, LoopbackTimeout)).IsNull();
            _ = await connectTask;
        }
        finally
        {
            lifetime.Cancel();
            try
            {
                _ = await connectTask;
            }
            finally
            {
                acceptedSocket?.Dispose();
                ((IDisposable)transport).Dispose();
                socket.Dispose();
            }
        }
    }

    /// <summary>Blocks observer notifications so single-flight scheduling can be asserted deterministically.</summary>
    /// <typeparam name="T">The observed value type.</typeparam>
    private sealed class BlockingObserver<T> : IObserver<T>
    {
        /// <summary>Stores the notification-entry completion source.</summary>
        private readonly TaskCompletionSource<object?> _entered =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        /// <summary>Stores the notification release completion source.</summary>
        private readonly TaskCompletionSource<object?> _release =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        /// <summary>Stores the notification count.</summary>
        private int _notificationCount;

        /// <summary>Gets a task that completes when a notification begins.</summary>
        public Task Entered => _entered.Task;

        /// <summary>Gets the number of notifications received.</summary>
        public int NotificationCount => Volatile.Read(ref _notificationCount);

        /// <summary>Releases the blocked notification.</summary>
        public void Release() => _ = _release.TrySetResult(null);

        /// <inheritdoc/>
        public void OnCompleted()
        {
        }

        /// <inheritdoc/>
        public void OnError(Exception error)
        {
        }

        /// <inheritdoc/>
        public void OnNext(T value)
        {
            _ = Interlocked.Increment(ref _notificationCount);
            _ = _entered.TrySetResult(null);
            _ = _release.Task.Wait(LoopbackTimeout);
        }
    }
}
