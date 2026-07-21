// Copyright (c) 2019-2026 Chris Pulman and contributors. All rights reserved.
// Chris Pulman and contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using CP.IoT.Core;

#if REACTIVE_SHIM

namespace MitsubishiRx.Reactive.Tests;
#else

namespace MitsubishiRx.Tests;
#endif

/// <summary>Provides the MitsubishiReactiveTagGroupTests type.</summary>
internal sealed class MitsubishiReactiveTagGroupTests
{
    /// <summary>Stores the <c>RecipeNumberTagName</c> test value.</summary>
    private const string RecipeNumberTagName = "RecipeNumber";

    /// <summary>Stores the <c>UInt16DataType</c> test value.</summary>
    private const string UInt16DataType = "UInt16";

    /// <summary>Stores the <c>RecipeTagName</c> test value.</summary>
    private const string RecipeTagName = "Recipe";

    /// <summary>Stores the <c>LoopbackHost</c> test value.</summary>
    private const string LoopbackHost = "127.0.0.1";

    /// <summary>Stores the tag-group polling interval in seconds.</summary>
    private const int PollingIntervalSeconds = 5;

    /// <summary>Stores the heartbeat and stale threshold in seconds.</summary>
    private const int HeartbeatOrStaleThresholdSeconds = 2;

    /// <summary>Stores the test heartbeat interval in milliseconds.</summary>
    private const int HeartbeatIntervalMilliseconds = 10;

    /// <summary>Stores the expected heartbeat sample count.</summary>
    private const int ExpectedHeartbeatSampleCount = 3;

    /// <summary>Stores the scheduler advance duration for heartbeat observations in seconds.</summary>
    private const int HeartbeatAdvanceSeconds = 6;

    /// <summary>Stores the expected stale sample count.</summary>
    private const int ExpectedStaleSampleCount = 2;

    /// <summary>Stores the scheduler advance duration for stale observations in seconds.</summary>
    private const int StaleAdvanceSeconds = 3;

    /// <summary>Executes the ObserveTagGroupHeartbeatEmitsHeartbeatBetweenPolls operation.</summary>
    /// <returns>The ObserveTagGroupHeartbeatEmitsHeartbeatBetweenPolls operation result.</returns>
    [Test]
    internal async Task ObserveTagGroupHeartbeatEmitsHeartbeatBetweenPollsAsync()
    {
        var scheduler = new TestScheduler();
        await using var transport = new FakeTransport(
        [
            [0xD0, 0x00, 0x00, 0xFF, 0xFF, 0x03, 0x00, 0x04, 0x00, 0x00, 0x00, 0x34, 0x12],
            [0xD0, 0x00, 0x00, 0xFF, 0xFF, 0x03, 0x00, 0x04, 0x00, 0x00, 0x00, 0x35, 0x12],
        ]);

        var database = new MitsubishiTagDatabase(
        [
            new MitsubishiTagDefinition(RecipeNumberTagName, "D300", DataType: UInt16DataType),
        ]);
        database.AddGroup(new MitsubishiTagGroupDefinition(RecipeTagName, [RecipeNumberTagName]));

        var options = new MitsubishiClientOptions(
            Host: LoopbackHost,
            Port: 5037,
            FrameType: MitsubishiFrameType.ThreeE,
            DataCode: CommunicationDataCode.Binary,
            TransportKind: MitsubishiTransportKind.Tcp,
            Route: MitsubishiRoute.Default);

        await using var client = new MitsubishiRx(options, transport, scheduler)
        {
            TagDatabase = database,
        };

        var received = new List<Heartbeat<Responce<MitsubishiTagGroupSnapshot>>>();

        using var subscription = client
            .ObserveTagGroupHeartbeat(
                RecipeTagName,
                TimeSpan.FromSeconds(PollingIntervalSeconds),
                TimeSpan.FromSeconds(HeartbeatOrStaleThresholdSeconds),
                TimeSpan.FromMilliseconds(HeartbeatIntervalMilliseconds))
            .Take(ExpectedHeartbeatSampleCount)
            .Subscribe(received.Add);

        TestSchedulerDriver.AdvanceBy(scheduler, TimeSpan.FromSeconds(HeartbeatAdvanceSeconds).Ticks);

        await Assert.That(received.Count).IsEqualTo(ExpectedHeartbeatSampleCount);
        await Assert.That(received[0].IsHeartbeat).IsFalse();
        var firstUpdate = received[0].Update;
        if (firstUpdate is null || firstUpdate.Value is not MitsubishiTagGroupSnapshot firstSnapshot)
        {
            throw new InvalidOperationException(
                "Expected first grouped heartbeat sample to contain a snapshot value.");
        }

        await Assert.That(
                firstSnapshot.GetRequired(new LogicalTagKey<ushort>(RecipeNumberTagName)))
            .IsEqualTo((ushort)0x1234);
        await Assert.That(received[1].IsHeartbeat).IsTrue();
        await Assert.That(received[2].IsHeartbeat).IsTrue();
    }

    /// <summary>Executes the ObserveTagGroupStaleMarksStreamWhenUpdatesGoQuiet operation.</summary>
    /// <returns>The ObserveTagGroupStaleMarksStreamWhenUpdatesGoQuiet operation result.</returns>
    [Test]
    internal async Task ObserveTagGroupStaleMarksStreamWhenUpdatesGoQuietAsync()
    {
        var scheduler = new TestScheduler();
        await using var transport = new FakeTransport(
        [
            [0xD0, 0x00, 0x00, 0xFF, 0xFF, 0x03, 0x00, 0x04, 0x00, 0x00, 0x00, 0x34, 0x12],
            [0xD0, 0x00, 0x00, 0xFF, 0xFF, 0x03, 0x00, 0x04, 0x00, 0x00, 0x00, 0x35, 0x12],
        ]);

        var database = new MitsubishiTagDatabase(
        [
            new MitsubishiTagDefinition(RecipeNumberTagName, "D300", DataType: UInt16DataType),
        ]);
        database.AddGroup(new MitsubishiTagGroupDefinition(RecipeTagName, [RecipeNumberTagName]));

        var options = new MitsubishiClientOptions(
            Host: LoopbackHost,
            Port: 5038,
            FrameType: MitsubishiFrameType.ThreeE,
            DataCode: CommunicationDataCode.Binary,
            TransportKind: MitsubishiTransportKind.Tcp,
            Route: MitsubishiRoute.Default);

        await using var client = new MitsubishiRx(options, transport, scheduler)
        {
            TagDatabase = database,
        };

        var received = new List<Stale<Responce<MitsubishiTagGroupSnapshot>>>();

        using var subscription = client
            .ObserveTagGroupStale(
                RecipeTagName,
                TimeSpan.FromSeconds(PollingIntervalSeconds),
                TimeSpan.FromSeconds(HeartbeatOrStaleThresholdSeconds),
                TimeSpan.FromMilliseconds(HeartbeatIntervalMilliseconds))
            .Take(ExpectedStaleSampleCount)
            .Subscribe(received.Add);

        TestSchedulerDriver.AdvanceBy(scheduler, TimeSpan.FromSeconds(StaleAdvanceSeconds).Ticks);

        await Assert.That(received.Count).IsEqualTo(ExpectedStaleSampleCount);
        await Assert.That(received[0].IsStale).IsFalse();
        await Assert.That(received[1].IsStale).IsTrue();
    }

    /// <summary>Executes the ObserveTagGroupLatestUsesLatestCompletedSnapshot operation.</summary>
    /// <returns>The ObserveTagGroupLatestUsesLatestCompletedSnapshot operation result.</returns>
    [Test]
    internal async Task ObserveTagGroupLatestUsesLatestCompletedSnapshotAsync()
    {
        var scheduler = new TestScheduler();
        await using var transport = new FakeTransport(
        [
            [0xD0, 0x00, 0x00, 0xFF, 0xFF, 0x03, 0x00, 0x04, 0x00, 0x00, 0x00, 0x34, 0x12],
            [0xD0, 0x00, 0x00, 0xFF, 0xFF, 0x03, 0x00, 0x04, 0x00, 0x00, 0x00, 0x35, 0x12],
        ]);

        var database = new MitsubishiTagDatabase(
        [
            new MitsubishiTagDefinition(RecipeNumberTagName, "D300", DataType: UInt16DataType),
        ]);
        database.AddGroup(new MitsubishiTagGroupDefinition(RecipeTagName, [RecipeNumberTagName]));

        var options = new MitsubishiClientOptions(
            Host: LoopbackHost,
            Port: 5039,
            FrameType: MitsubishiFrameType.ThreeE,
            DataCode: CommunicationDataCode.Binary,
            TransportKind: MitsubishiTransportKind.Tcp,
            Route: MitsubishiRoute.Default);

        await using var client = new MitsubishiRx(options, transport, scheduler)
        {
            TagDatabase = database,
        };

        using var trigger = new Signal<Unit>();
        var received = new List<Responce<MitsubishiTagGroupSnapshot>>();

        using var subscription = client
            .ObserveTagGroupLatest(RecipeTagName, trigger)
            .Take(ExpectedStaleSampleCount)
            .Subscribe(received.Add);

        trigger.OnNext(Unit.Default);
        trigger.OnNext(Unit.Default);
        TestSchedulerDriver.AdvanceBy(scheduler, 1);

        await Assert.That(received.Count).IsEqualTo(ExpectedStaleSampleCount);
        if (received[0].Value is not MitsubishiTagGroupSnapshot firstLatest)
        {
            throw new InvalidOperationException("Expected first latest-only grouped read to contain a snapshot.");
        }

        if (received[1].Value is not MitsubishiTagGroupSnapshot secondLatest)
        {
            throw new InvalidOperationException("Expected second latest-only grouped read to contain a snapshot.");
        }

        await Assert.That(
                firstLatest.GetRequired(new LogicalTagKey<ushort>(RecipeNumberTagName)))
            .IsEqualTo((ushort)0x1234);
        await Assert.That(
                secondLatest.GetRequired(new LogicalTagKey<ushort>(RecipeNumberTagName)))
            .IsEqualTo((ushort)0x1235);
    }
}
