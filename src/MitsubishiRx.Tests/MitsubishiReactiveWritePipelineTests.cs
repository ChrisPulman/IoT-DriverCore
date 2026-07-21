// Copyright (c) 2019-2026 Chris Pulman and contributors. All rights reserved.
// Chris Pulman and contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using CP.IoT.Core;

#if REACTIVE_SHIM

namespace MitsubishiRx.Reactive.Tests;
#else

namespace MitsubishiRx.Tests;
#endif

/// <summary>Provides the MitsubishiReactiveWritePipelineTests type.</summary>
internal sealed class MitsubishiReactiveWritePipelineTests
{
    /// <summary>Stores the <c>RecipeNumberTagName</c> test value.</summary>
    private const string RecipeNumberTagName = "RecipeNumber";

    /// <summary>Stores the second queued word value.</summary>
    private const ushort SecondQueuedWordValue = 2;

    /// <summary>Stores the third queued word value.</summary>
    private const ushort ThirdQueuedWordValue = 3;

    /// <summary>Stores the expected queued write count.</summary>
    private const int ExpectedQueuedWriteCount = 3;

    /// <summary>Stores the coalescing window duration in milliseconds.</summary>
    private const int CoalescingWindowMilliseconds = 50;

    /// <summary>Stores the coalescing intermediate delay in milliseconds.</summary>
    private const int CoalescingIntermediateDelayMilliseconds = 20;

    /// <summary>Stores the initial coalescing word value.</summary>
    private const ushort InitialCoalescingWordValue = 7;

    /// <summary>Stores the latest coalescing word value.</summary>
    private const ushort LatestCoalescingWordValue = 8;

    /// <summary>Executes the QueuedReactiveWordWritePipelinePreservesWriteOrder operation.</summary>
    /// <returns>The QueuedReactiveWordWritePipelinePreservesWriteOrder operation result.</returns>
    [Test]
    internal async Task QueuedReactiveWordWritePipelinePreservesWriteOrderAsync()
    {
        var scheduler = new TestScheduler();
        await using var transport = new FakeTransport(
        [
            Ack(),
            Ack(),
            Ack(),
        ]);

        await using var client = CreateClient(transport, scheduler);
        var pipeline = client.CreateReactiveWordWritePipeline("D100", MitsubishiReactiveWriteMode.Queued, null);
        var results = new List<MitsubishiReactiveWriteResult>();
        using var subscription = pipeline.Results.Subscribe(results.Add);

        pipeline.Post([(ushort)1]);
        pipeline.Post([SecondQueuedWordValue]);
        pipeline.Post([ThirdQueuedWordValue]);
        TestSchedulerDriver.AdvanceBy(scheduler, 1);

        await Assert.That(transport.Requests.Count).IsEqualTo(ExpectedQueuedWriteCount);
        await Assert.That(results.Count).IsEqualTo(ExpectedQueuedWriteCount);

        await using var baselineTransport = new FakeTransport([Ack(), Ack(), Ack()]);
        await using var baselineClient = CreateClient(baselineTransport, scheduler);
        await Assert.That(
                (await baselineClient.WriteWordsAsync(
                    "D100",
                    [(ushort)1],
                    CancellationToken.None)).IsSucceed)
            .IsTrue();
        await Assert.That(
                (await baselineClient.WriteWordsAsync(
                    "D100",
                    [SecondQueuedWordValue],
                    CancellationToken.None)).IsSucceed)
            .IsTrue();
        await Assert.That(
                (await baselineClient.WriteWordsAsync(
                    "D100",
                    [ThirdQueuedWordValue],
                    CancellationToken.None)).IsSucceed)
            .IsTrue();

        await Assert.That(transport.Requests.Select(static request => Convert.ToHexString(request.Payload)).ToArray())
            .IsEquivalentTo(
                baselineTransport.Requests
                    .Select(static request => Convert.ToHexString(request.Payload))
                    .ToArray());
    }

    /// <summary>Executes the LatestWinsReactiveWordWritePipelineCollapsesBurstToFinalValue operation.</summary>
    /// <returns>The LatestWinsReactiveWordWritePipelineCollapsesBurstToFinalValue operation result.</returns>
    [Test]
    internal async Task LatestWinsReactiveWordWritePipelineCollapsesBurstToFinalValueAsync()
    {
        var scheduler = new TestScheduler();
        await using var transport = new FakeTransport([Ack()]);
        await using var client = CreateClient(transport, scheduler);
        var pipeline = client.CreateReactiveWordWritePipeline("D100", MitsubishiReactiveWriteMode.LatestWins, null);
        var results = new List<MitsubishiReactiveWriteResult>();
        using var subscription = pipeline.Results.Subscribe(results.Add);

        pipeline.Post([(ushort)1]);
        pipeline.Post([SecondQueuedWordValue]);
        pipeline.Post([ThirdQueuedWordValue]);
        TestSchedulerDriver.AdvanceBy(scheduler, 1);

        await Assert.That(transport.Requests.Count).IsEqualTo(1);
        await Assert.That(results.Count).IsEqualTo(1);

        await using var baselineTransport = new FakeTransport([Ack()]);
        await using var baselineClient = CreateClient(baselineTransport, scheduler);
        await Assert.That(
                (await baselineClient.WriteWordsAsync(
                    "D100",
                    [ThirdQueuedWordValue],
                    CancellationToken.None)).IsSucceed)
            .IsTrue();
        await Assert.That(Convert.ToHexString(transport.Requests[0].Payload))
            .IsEqualTo(Convert.ToHexString(baselineTransport.Requests[0].Payload));
    }

    /// <summary>Executes the CoalescingReactiveWordWritePipelineEmitsLatestValueAfterWindow operation.</summary>
    /// <returns>The CoalescingReactiveWordWritePipelineEmitsLatestValueAfterWindow operation result.</returns>
    [Test]
    internal async Task CoalescingReactiveWordWritePipelineEmitsLatestValueAfterWindowAsync()
    {
        var scheduler = new TestScheduler();
        await using var transport = new FakeTransport([Ack()]);
        await using var client = CreateClient(transport, scheduler);
        var pipeline = client.CreateReactiveWordWritePipeline(
            "D100",
            MitsubishiReactiveWriteMode.Coalescing,
            TimeSpan.FromMilliseconds(CoalescingWindowMilliseconds));
        var results = new List<MitsubishiReactiveWriteResult>();
        using var subscription = pipeline.Results.Subscribe(results.Add);

        pipeline.Post([InitialCoalescingWordValue]);
        TestSchedulerDriver.AdvanceBy(
            scheduler,
            TimeSpan.FromMilliseconds(CoalescingIntermediateDelayMilliseconds).Ticks);
        pipeline.Post([LatestCoalescingWordValue]);
        TestSchedulerDriver.AdvanceBy(
            scheduler,
            TimeSpan.FromMilliseconds(CoalescingIntermediateDelayMilliseconds).Ticks);
        pipeline.Post([LatestCoalescingWordValue]);
        TestSchedulerDriver.AdvanceBy(
            scheduler,
            TimeSpan.FromMilliseconds(CoalescingWindowMilliseconds).Ticks + 1);

        await Assert.That(transport.Requests.Count).IsEqualTo(1);
        await Assert.That(results.Count).IsEqualTo(1);

        await using var baselineTransport = new FakeTransport([Ack()]);
        await using var baselineClient = CreateClient(baselineTransport, scheduler);
        await Assert.That(
                (await baselineClient.WriteWordsAsync(
                    "D100",
                    [LatestCoalescingWordValue],
                    CancellationToken.None)).IsSucceed)
            .IsTrue();
        await Assert.That(Convert.ToHexString(transport.Requests[0].Payload))
            .IsEqualTo(Convert.ToHexString(baselineTransport.Requests[0].Payload));
    }

    /// <summary>Executes the QueuedReactiveTagWritePipelineDelegatesThroughTypedTagWriter operation.</summary>
    /// <returns>The QueuedReactiveTagWritePipelineDelegatesThroughTypedTagWriter operation result.</returns>
    [Test]
    internal async Task QueuedReactiveTagWritePipelineDelegatesThroughTypedTagWriterAsync()
    {
        var scheduler = new TestScheduler();
        await using var transport = new FakeTransport([Ack()]);
        await using var client = CreateClient(transport, scheduler);
        client.TagDatabase = new(
        [
            new MitsubishiTagDefinition(RecipeNumberTagName, "D300", DataType: "UInt16"),
        ]);

        var pipeline = client.CreateReactiveTagWritePipeline(
            new LogicalTagKey<ushort>(RecipeNumberTagName),
            MitsubishiReactiveWriteMode.Queued,
            null);
        var results = new List<MitsubishiReactiveWriteResult>();
        using var subscription = pipeline.Results.Subscribe(results.Add);

        pipeline.Post(InitialCoalescingWordValue);
        TestSchedulerDriver.AdvanceBy(scheduler, 1);

        await Assert.That(transport.Requests.Count).IsEqualTo(1);
        await Assert.That(results.Count).IsEqualTo(1);
        await Assert.That(results[0].Target).IsEqualTo("Tag:RecipeNumber");
        await Assert.That(results[0].Success).IsTrue();

        await using var baselineTransport = new FakeTransport([Ack()]);
        await using var baselineClient = CreateClient(baselineTransport, scheduler);
        baselineClient.TagDatabase = client.TagDatabase;
        await Assert.That(
                (await baselineClient.WriteUInt16ByTagAsync(
                    RecipeNumberTagName,
                    InitialCoalescingWordValue,
                    CancellationToken.None)).IsSucceed)
            .IsTrue();
        await Assert.That(Convert.ToHexString(transport.Requests[0].Payload))
            .IsEqualTo(Convert.ToHexString(baselineTransport.Requests[0].Payload));
    }

    /// <summary>Executes the CreateClient operation.</summary>
    /// <param name="transport">The transport parameter.</param>
    /// <param name="scheduler">The scheduler parameter.</param>
    /// <returns>The CreateClient operation result.</returns>
    private static MitsubishiRx CreateClient(FakeTransport transport, TestScheduler scheduler)
        => new(
            new MitsubishiClientOptions(
                Host: "127.0.0.1",
                Port: 5003,
                FrameType: MitsubishiFrameType.ThreeE,
                DataCode: CommunicationDataCode.Binary,
                TransportKind: MitsubishiTransportKind.Tcp,
                Route: MitsubishiRoute.Default),
            transport,
            scheduler);

    /// <summary>Executes the Ack operation.</summary>
    /// <returns>The Ack operation result.</returns>
    private static byte[] Ack()
        => [0xD0, 0x00, 0x00, 0xFF, 0xFF, 0x03, 0x00, 0x02, 0x00, 0x00, 0x00];
}
