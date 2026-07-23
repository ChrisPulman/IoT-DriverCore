// Copyright (c) 2019-2026 Chris Pulman and contributors. All rights reserved.
// Chris Pulman and contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using IoT.DriverCore.Core;

#if REACTIVE_SHIM

namespace IoT.DriverCore.MitsubishiRx.Reactive.Tests;

#else

namespace IoT.DriverCore.MitsubishiRx.Tests;

#endif

/// <summary>Completes deterministic reactive tag and group projection coverage.</summary>
internal sealed class MitsubishiReactiveCompletionTests
{
    /// <summary>Stores the deterministic MC port.</summary>
    private const int McPort = 5000;

    /// <summary>Stores the polling interval in seconds.</summary>
    private const int PollIntervalSeconds = 5;

    /// <summary>Stores the string tag word length.</summary>
    private const int StringWordLength = 2;

    /// <summary>Stores the unsigned 16-bit data type name.</summary>
    private const string UInt16DataType = "UInt16";

    /// <summary>Stores the unsigned 16-bit matrix tag name.</summary>
    private const string UInt16TagName = "UInt16Tag";

    /// <summary>Stores the contiguous group name.</summary>
    private const string ContiguousGroupName = "ContiguousGroup";

    /// <summary>Stores the scheduler advancement in milliseconds.</summary>
    private const int SchedulerAdvanceMilliseconds = 1;

    /// <summary>Stores the observation timeout.</summary>
    private static readonly TimeSpan ObservationTimeout = TimeSpan.FromSeconds(5);

    /// <summary>Stores a payload long enough for every tag projection in the matrix.</summary>
    private static readonly byte[] WordPayload =
    [
        0x34, 0x12, 0x78, 0x56, 0x00, 0x3F, 0x80, 0x3F,
        0xFF, 0x7F, 0x00, 0x80, 0x41, 0x42, 0x43, 0x44,
    ];

    /// <summary>Exercises bit, string, floating-point, integer, scaled, signed, and fallback projections.</summary>
    /// <returns>A task that represents the asynchronous test.</returns>
    [Test]
    internal async Task ReactiveTagTypeMatrixProjectsThroughSimulatorAsync()
    {
        var scheduler = new TestScheduler();
        var options = CreateOptions();
        await using var simulator = CreateSimulator(options);
        await using var client = new MitsubishiRx(options, simulator, scheduler)
        {
            TagDatabase = CreateTypeMatrixDatabase(),
        };

        var names = new[]
        {
            "BitTag", "StringTag", "FloatTag", "DWordTag", "Int32Tag",
            "Int16Tag", UInt16TagName, "ScaledTag", "SignedWordTag",
            "WordTag",
        };
        foreach (var name in names)
        {
            var value = await ObserveTagOnceAsync<object?>(client, scheduler, name);
            await Assert.That(value.Quality).IsEqualTo(MitsubishiReactiveQuality.Good);
            await Assert.That(value.Value).IsNotNull();
        }

        var wrongType = await ObserveTagOnceAsync<string>(client, scheduler, UInt16TagName);
        await Assert.That(wrongType.Quality).IsEqualTo(MitsubishiReactiveQuality.Error);
        await Assert.That(wrongType.Exception).IsNotNull();

        client.TagDatabase.Add(new MitsubishiTagDefinition("MissingLength", "D20", "String"));
        _ = Assert.Throws<InvalidOperationException>(
            () => client.ObserveReactiveTag(
                new LogicalTagKey<string>("MissingLength"),
                TimeSpan.FromSeconds(PollIntervalSeconds),
                null));
    }

    /// <summary>Exercises non-contiguous, mixed-device, bit, and failed contiguous group paths.</summary>
    /// <returns>A task that represents the asynchronous test.</returns>
    [Test]
    internal async Task ReactiveGroupFallbackAndFailurePathsUseSimulatorAsync()
    {
        var scheduler = new TestScheduler();
        var options = CreateOptions();
        await using var simulator = CreateSimulator(options);
        await using var client = new MitsubishiRx(options, simulator, scheduler)
        {
            TagDatabase = CreateGroupDatabase(),
        };

        var bitGroup = await ObserveGroupOnceAsync(client, scheduler, "BitGroup");
        var gapGroup = await ObserveGroupOnceAsync(client, scheduler, "GapGroup");
        var mixedGroup = await ObserveGroupOnceAsync(client, scheduler, "MixedGroup");

        await Assert.That(bitGroup.Quality).IsEqualTo(MitsubishiReactiveQuality.Good);
        await Assert.That(gapGroup.Quality).IsEqualTo(MitsubishiReactiveQuality.Good);
        await Assert.That(mixedGroup.Quality).IsEqualTo(MitsubishiReactiveQuality.Good);

        simulator.EnqueueResponse(MitsubishiSimulatorTransport.CreateErrorResponse(options, 0xC051));
        var failed = await ObserveGroupOnceAsync(client, scheduler, ContiguousGroupName);
        await Assert.That(failed.Quality).IsEqualTo(MitsubishiReactiveQuality.Error);
        await Assert.That(failed.ErrorCode).IsEqualTo(0xC051);
    }

    /// <summary>Exercises reactive error mapping and a short contiguous group payload.</summary>
    /// <returns>A task that represents the asynchronous test.</returns>
    [Test]
    internal async Task ReactiveErrorsPreserveProtocolAndProjectionDetailsAsync()
    {
        var scheduler = new TestScheduler();
        var options = CreateOptions();
        await using var simulator = new MitsubishiSimulatorTransport(
            _ => MitsubishiSimulatorTransport.CreateSuccessResponse(options, [0x34, 0x12]));
        await using var client = new MitsubishiRx(options, simulator, scheduler)
        {
            TagDatabase = CreateGroupDatabase(),
        };
        client.TagDatabase.Add(
            new MitsubishiTagDefinition(UInt16TagName, "D20", UInt16DataType));

        simulator.EnqueueResponse(MitsubishiSimulatorTransport.CreateErrorResponse(options, 0xC053));
        var protocolError = await ObserveTagOnceAsync<ushort>(
            client,
            scheduler,
            UInt16TagName);
        var shortGroup = await ObserveGroupOnceAsync(
            client,
            scheduler,
            ContiguousGroupName);

        await Assert.That(protocolError.Quality).IsEqualTo(MitsubishiReactiveQuality.Error);
        await Assert.That(protocolError.ErrorCode).IsEqualTo(0xC053);
        await Assert.That(shortGroup.Quality).IsEqualTo(MitsubishiReactiveQuality.Error);
        await Assert.That(shortGroup.Exception).IsTypeOf<InvalidOperationException>();
    }

    /// <summary>Observes one typed tag value using a virtual scheduler.</summary>
    /// <typeparam name="T">The requested tag type.</typeparam>
    /// <param name="client">The Mitsubishi client.</param>
    /// <param name="scheduler">The virtual scheduler.</param>
    /// <param name="tagName">The tag name.</param>
    /// <returns>The first reactive value.</returns>
    private static async Task<MitsubishiReactiveValue<T>> ObserveTagOnceAsync<T>(
        MitsubishiRx client,
        TestScheduler scheduler,
        string tagName)
    {
        var completion = new TaskCompletionSource<MitsubishiReactiveValue<T>>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        using var subscription = client
            .ObserveReactiveTag(
                new LogicalTagKey<T>(tagName),
                TimeSpan.FromSeconds(PollIntervalSeconds),
                TimeSpan.FromMilliseconds(SchedulerAdvanceMilliseconds))
            .Take(1)
            .Subscribe(value => completion.TrySetResult(value));
        TestSchedulerDriver.AdvanceBy(
            scheduler,
            TimeSpan.FromMilliseconds(SchedulerAdvanceMilliseconds).Ticks);
        return await completion.Task.WaitAsync(ObservationTimeout);
    }

    /// <summary>Observes one group value using a virtual scheduler.</summary>
    /// <param name="client">The Mitsubishi client.</param>
    /// <param name="scheduler">The virtual scheduler.</param>
    /// <param name="groupName">The group name.</param>
    /// <returns>The first group value.</returns>
    private static async Task<MitsubishiReactiveValue<MitsubishiTagGroupSnapshot>>
        ObserveGroupOnceAsync(
            MitsubishiRx client,
            TestScheduler scheduler,
            string groupName)
    {
        var completion =
            new TaskCompletionSource<MitsubishiReactiveValue<MitsubishiTagGroupSnapshot>>(
                TaskCreationOptions.RunContinuationsAsynchronously);
        using var subscription = client
            .ObserveReactiveTagGroup(
                groupName,
                TimeSpan.FromSeconds(PollIntervalSeconds),
                TimeSpan.FromMilliseconds(SchedulerAdvanceMilliseconds))
            .Take(1)
            .Subscribe(value => completion.TrySetResult(value));
        TestSchedulerDriver.AdvanceBy(
            scheduler,
            TimeSpan.FromMilliseconds(SchedulerAdvanceMilliseconds).Ticks);
        return await completion.Task.WaitAsync(ObservationTimeout);
    }

    /// <summary>Creates a simulator that distinguishes bit and word reads.</summary>
    /// <param name="options">The MC options.</param>
    /// <returns>The deterministic transport.</returns>
    private static MitsubishiSimulatorTransport CreateSimulator(MitsubishiClientOptions options) =>
        new(request => MitsubishiSimulatorTransport.CreateSuccessResponse(
            options,
            request.Description.StartsWith("Read bits", StringComparison.Ordinal)
                ? [0x10]
                : WordPayload));

    /// <summary>Creates the database used by the tag projection matrix.</summary>
    /// <returns>The tag database.</returns>
    private static MitsubishiTagDatabase CreateTypeMatrixDatabase() =>
        new(
        [
            new MitsubishiTagDefinition("BitTag", "M0", "Bit"),
            new MitsubishiTagDefinition(
                "StringTag",
                "D0",
                "String",
                Length: StringWordLength),
            new MitsubishiTagDefinition("FloatTag", "D2", "Float"),
            new MitsubishiTagDefinition("DWordTag", "D4", "DWord"),
            new MitsubishiTagDefinition("Int32Tag", "D6", "Int32"),
            new MitsubishiTagDefinition("Int16Tag", "D8", "Int16"),
            new MitsubishiTagDefinition(UInt16TagName, "D9", UInt16DataType),
            new MitsubishiTagDefinition("ScaledTag", "D10", "Word", Scale: 2.0, Offset: 1.0),
            new MitsubishiTagDefinition("SignedWordTag", "D11", "Word", Signed: true),
            new MitsubishiTagDefinition("WordTag", "D12", "Word"),
        ]);

    /// <summary>Creates grouped tags for each group-planning branch.</summary>
    /// <returns>The grouped tag database.</returns>
    private static MitsubishiTagDatabase CreateGroupDatabase()
    {
        var database = new MitsubishiTagDatabase(
        [
            new MitsubishiTagDefinition("Bit", "M0", "Bit"),
            new MitsubishiTagDefinition("GapA", "D0", UInt16DataType),
            new MitsubishiTagDefinition("GapB", "D2", UInt16DataType),
            new MitsubishiTagDefinition("MixedA", "D4", UInt16DataType),
            new MitsubishiTagDefinition("MixedB", "W5", UInt16DataType),
            new MitsubishiTagDefinition("ContiguousA", "D10", UInt16DataType),
            new MitsubishiTagDefinition("ContiguousB", "D11", UInt16DataType),
        ]);
        database.AddGroup(new MitsubishiTagGroupDefinition("BitGroup", ["Bit"]));
        database.AddGroup(new MitsubishiTagGroupDefinition("GapGroup", ["GapA", "GapB"]));
        database.AddGroup(new MitsubishiTagGroupDefinition("MixedGroup", ["MixedA", "MixedB"]));
        database.AddGroup(
            new MitsubishiTagGroupDefinition(
                ContiguousGroupName,
                ["ContiguousA", "ContiguousB"]));
        return database;
    }

    /// <summary>Creates deterministic MC options.</summary>
    /// <returns>The client options.</returns>
    private static MitsubishiClientOptions CreateOptions() =>
        new(
            "127.0.0.1",
            McPort,
            MitsubishiFrameType.ThreeE,
            CommunicationDataCode.Binary,
            MitsubishiTransportKind.Tcp);
}
