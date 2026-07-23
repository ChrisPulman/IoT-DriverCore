// Copyright (c) 2019-2026 Chris Pulman and contributors. All rights reserved.
// Chris Pulman and contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

#if REACTIVE_SHIM

namespace IoT.DriverCore.MitsubishiRx.Reactive.Tests;
#else

namespace IoT.DriverCore.MitsubishiRx.Tests;
#endif

/// <summary>Provides the MitsubishiReactivePollingTests type.</summary>
internal sealed class MitsubishiReactivePollingTests
{
    /// <summary>Stores the initial polling interval in seconds.</summary>
    private const int InitialPollIntervalSeconds = 5;

    /// <summary>Stores the heartbeat interval in seconds.</summary>
    private const int HeartbeatIntervalSeconds = 2;

    /// <summary>Stores the polling interval in milliseconds.</summary>
    private const int PollingIntervalMilliseconds = 10;

    /// <summary>Stores the polling timeout in seconds.</summary>
    private const int PollingTimeoutSeconds = 30;

    /// <summary>Stores the expected number of heartbeat values.</summary>
    private const int ExpectedHeartbeatCount = 3;

    /// <summary>Stores the scheduler advancement duration in seconds.</summary>
    private const int SchedulerAdvanceSeconds = 6;

    /// <summary>Executes the ObserveWordsHeartbeatEmitsHeartbeatBetweenPolls operation.</summary>
    /// <returns>The ObserveWordsHeartbeatEmitsHeartbeatBetweenPolls operation result.</returns>
    [Test]
    internal async Task ObserveWordsHeartbeatEmitsHeartbeatBetweenPollsAsync()
    {
        var scheduler = new TestScheduler();
        await using var transport = new FakeTransport(
        [
            [0xD0, 0x00, 0x00, 0xFF, 0xFF, 0x03, 0x00, 0x04, 0x00, 0x00, 0x00, 0x34, 0x12],
            [0xD0, 0x00, 0x00, 0xFF, 0xFF, 0x03, 0x00, 0x04, 0x00, 0x00, 0x00, 0x35, 0x12],
        ]);

        var options = new MitsubishiClientOptions(
            Host: "127.0.0.1",
            Port: 5003,
            FrameType: MitsubishiFrameType.ThreeE,
            DataCode: CommunicationDataCode.Binary,
            TransportKind: MitsubishiTransportKind.Tcp,
            Route: MitsubishiRoute.Default);

        await using var client = new MitsubishiRx(options, transport, scheduler);
        var received = new List<Heartbeat<Responce<ushort[]>>>();

        using var subscription = client
            .ObserveWordsHeartbeat(
                "D100",
                1,
                TimeSpan.FromSeconds(InitialPollIntervalSeconds),
                TimeSpan.FromSeconds(HeartbeatIntervalSeconds),
                TimeSpan.FromMilliseconds(PollingIntervalMilliseconds),
                TimeSpan.FromSeconds(PollingTimeoutSeconds))
            .Take(ExpectedHeartbeatCount)
            .Subscribe(received.Add);

        TestSchedulerDriver.AdvanceBy(scheduler, TimeSpan.FromSeconds(SchedulerAdvanceSeconds).Ticks);

        await Assert.That(received.Count).IsEqualTo(ExpectedHeartbeatCount);
        await Assert.That(received[0].IsHeartbeat).IsFalse();
        await Assert.That(received[1].IsHeartbeat).IsTrue();
        await Assert.That(received[2].IsHeartbeat).IsTrue();
    }
}
