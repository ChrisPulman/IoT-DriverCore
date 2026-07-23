// Copyright (c) 2019-2026 Chris Pulman and contributors. All rights reserved.
// Chris Pulman and contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

#if REACTIVE_SHIM

namespace IoT.DriverCore.MitsubishiRx.Reactive.Tests;

#else

namespace IoT.DriverCore.MitsubishiRx.Tests;

#endif

/// <summary>Completes client lifecycle, compatibility, conversion, and observable coverage.</summary>
internal sealed class MitsubishiClientCompletionTests
{
    /// <summary>Stores the deterministic loopback port.</summary>
    private const int LoopbackPort = 5000;

    /// <summary>Stores a positive timeout in milliseconds.</summary>
    private const int TimeoutMilliseconds = 100;

    /// <summary>Stores the polling interval in seconds.</summary>
    private const int PollSeconds = 5;

    /// <summary>Stores the scheduler advance in milliseconds.</summary>
    private const int SchedulerAdvanceMilliseconds = 20;

    /// <summary>Stores the expected operation count.</summary>
    private const int ExpectedRawOperationCount = 3;

    /// <summary>Stores the loopback host.</summary>
    private const string LoopbackHost = "127.0.0.1";

    /// <summary>Stores the fixed raw response length.</summary>
    private const int RawResponseLength = 2;

    /// <summary>Stores the string tag word length.</summary>
    private const int StringWordLength = 2;

    /// <summary>Stores an engineering value for scaled writes.</summary>
    private const double EngineeringValue = 5.0;

    /// <summary>Stores the deterministic bit tag name.</summary>
    private const string BitTagName = "BitTag";

    /// <summary>Stores the deterministic integer tag name.</summary>
    private const string Int32TagName = "Int32Tag";

    /// <summary>Stores the string tag without inferred length.</summary>
    private const string StringNoLengthTagName = "StringNoLength";

    /// <summary>Stores the length-qualified string tag.</summary>
    private const string StringTagName = "StringTag";

    /// <summary>Stores the floating-point tag name.</summary>
    private const string FloatTagName = "FloatTag";

    /// <summary>Stores the unsigned double-word tag name.</summary>
    private const string DWordTagName = "DWordTag";

    /// <summary>Stores the signed word tag name.</summary>
    private const string Int16TagName = "Int16Tag";

    /// <summary>Stores the raw word tag name.</summary>
    private const string RawWordTagName = "RawWord";

    /// <summary>Stores the unsigned word tag name.</summary>
    private const string WordTagName = "WordTag";

    /// <summary>Stores the scaled word tag name.</summary>
    private const string ScaledWordTagName = "ScaledWord";

    /// <summary>Stores the scaled signed word tag name.</summary>
    private const string ScaledInt16TagName = "ScaledInt16";

    /// <summary>Stores the scaled unsigned double-word tag name.</summary>
    private const string ScaledDWordTagName = "ScaledDWord";

    /// <summary>Stores the scaled floating-point tag name.</summary>
    private const string ScaledFloatTagName = "ScaledFloat";

    /// <summary>Stores a compatible scaled engineering value.</summary>
    private const double ScaledEngineeringValue = 3.0D;

    /// <summary>Stores the zero-scale test engineering value.</summary>
    private const double ZeroScaleEngineeringValue = 2.0D;

    /// <summary>Stores the all-types group name.</summary>
    private const string AllTypesGroupName = "AllTypes";

    /// <summary>Stores the unsigned word data type.</summary>
    private const string UInt16DataType = "UInt16";

    /// <summary>Stores a word payload long enough for scalar projections.</summary>
    private static readonly byte[] WordPayload =
    [
        0x34, 0x12, 0x78, 0x56, 0x00, 0x00, 0x80, 0x3F,
    ];

    /// <summary>Exercises compatibility constructors, state/log properties, lifecycle, and sync disposal.</summary>
    /// <returns>A task that represents the asynchronous test.</returns>
    [Test]
    internal async Task CompatibilityLifecycleAndDiagnosticPropertiesAreOperationalAsync()
    {
        using var legacyOneE = new MitsubishiRx(
            CpuType.ASeries,
            LoopbackHost,
            LoopbackPort,
            TimeoutMilliseconds);
        using var legacyThreeE = new MitsubishiRx(
            CpuType.QSeries,
            LoopbackHost,
            LoopbackPort,
            TimeoutMilliseconds);
        await Assert.That(legacyOneE.Options.FrameType).IsEqualTo(MitsubishiFrameType.OneE);
        await Assert.That(legacyThreeE.Options.FrameType).IsEqualTo(MitsubishiFrameType.ThreeE);

        var options = CreateOptions();
        var simulator = CreateSimulator(options);
        var client = new MitsubishiRx(options, simulator, Scheduler.Immediate);
        var states = new List<MitsubishiConnectionState>();
        var logs = new List<MitsubishiOperationLog>();
        using var stateSubscription = client.ConnectionStates.Subscribe(states.Add);
        using var logSubscription = client.OperationLogs.Subscribe(logs.Add);

        await Assert.That(client.Open().IsSucceed).IsTrue();
        await Assert.That(client.Connected).IsTrue();
        await Assert.That(client.Close().IsSucceed).IsTrue();
        await Assert.That((await client.OpenAsync(CancellationToken.None)).IsSucceed).IsTrue();
        await Assert.That((await client.CloseAsync(CancellationToken.None)).IsSucceed).IsTrue();
        client.Dispose();
        client.Dispose();

        await Assert.That(states).Contains(MitsubishiConnectionState.Connected);
        await Assert.That(logs.Any(static log => log.Description.Contains("Close", StringComparison.Ordinal)))
            .IsTrue();
    }

    /// <summary>Exercises failed open and the idempotent asynchronous disposal branch.</summary>
    /// <returns>A task that represents the asynchronous test.</returns>
    [Test]
    internal async Task OpenFailureAndRepeatedAsyncDisposalAreReportedAsync()
    {
        var options = CreateOptions();
        var simulator = CreateSimulator(options);
        simulator.EnqueueConnectFault(new InvalidOperationException("connect fault"));
        var client = new MitsubishiRx(options, simulator, Scheduler.Immediate);

        var failed = await client.OpenAsync(CancellationToken.None);
        await client.DisposeAsync();
        await client.DisposeAsync();

        await Assert.That(failed.IsSucceed).IsFalse();
        await Assert.That(failed.Exception).IsTypeOf<InvalidOperationException>();
    }

    /// <summary>Exercises legacy raw wrappers through simulator framing.</summary>
    /// <returns>A task that represents the asynchronous test.</returns>
    [Test]
    internal async Task LegacyRawPackageWrappersUseSimulatorFramingAsync()
    {
        var options = CreateOptions();
        await using var simulator = CreateSimulator(options);
        await using var client = new MitsubishiRx(options, simulator, Scheduler.Immediate);
        var command = new byte[]
        {
            0x50, 0x00, 0x00, 0xFF, 0xFF, 0x03, 0x00,
            0x04, 0x00, 0x10, 0x00,
        };

        var fixedLength = client.SendPackage(command, RawResponseLength);
        var single = client.SendPackageSingle(command);
        var reliable = client.SendPackageReliable(command);

        await Assert.That(fixedLength.IsSucceed).IsTrue();
        await Assert.That(single.IsSucceed).IsTrue();
        await Assert.That(reliable.IsSucceed).IsTrue();
        await Assert.That(simulator.Requests).Count().IsEqualTo(ExpectedRawOperationCount);
    }

    /// <summary>Exercises generated bit APIs, Int32 reads, and scaled write dispatch.</summary>
    /// <returns>A task that represents the asynchronous test.</returns>
    [Test]
    internal async Task GeneratedAndScaledTagOperationsDispatchByDeclaredTypeAsync()
    {
        var options = CreateOptions();
        await using var simulator = CreateSimulator(options);
        await using var client = CreateTaggedClient(options, simulator, Scheduler.Immediate);

        var generated = await client.ReadGeneratedBitTagAsync(BitTagName, CancellationToken.None);
        var generatedWrite = await client.WriteGeneratedBitTagAsync(
            BitTagName,
            true,
            CancellationToken.None);
        var int32 = await client.ReadInt32ByTagAsync(Int32TagName, CancellationToken.None);
        var dword = await client.WriteScaledDoubleByTagAsync(
            ScaledDWordTagName,
            EngineeringValue,
            CancellationToken.None);
        var floating = await client.WriteScaledDoubleByTagAsync(
            ScaledFloatTagName,
            EngineeringValue,
            CancellationToken.None);
        var unsupported = await client.WriteScaledDoubleByTagAsync(
            ScaledInt16TagName,
            EngineeringValue,
            CancellationToken.None);

        await Assert.That(generated.Value).IsTrue();
        await Assert.That(generatedWrite.IsSucceed).IsTrue();
        await Assert.That(int32.IsSucceed).IsTrue();
        await Assert.That(dword.IsSucceed).IsTrue();
        await Assert.That(floating.IsSucceed).IsTrue();
        await Assert.That(unsupported.IsSucceed).IsFalse();
    }

    /// <summary>Exercises string overload guards and generated bit failure conversion.</summary>
    /// <returns>A task that represents the asynchronous test.</returns>
    [Test]
    internal async Task StringAndGeneratedFailureGuardsAreDeterministicAsync()
    {
        var options = CreateOptions();
        await using var simulator = CreateSimulator(options);
        await using var client = CreateTaggedClient(options, simulator, Scheduler.Immediate);

        _ = Assert.Throws<InvalidOperationException>(
            () => client.ReadStringByTagAsync(StringNoLengthTagName, CancellationToken.None));
        _ = Assert.Throws<InvalidOperationException>(
            () => client.WriteStringByTagAsync(
                StringNoLengthTagName,
                "text",
                CancellationToken.None));
        await Assert.That(await ThrowsAsync<ArgumentOutOfRangeException>(
            () => client.ReadStringByTagAsync(StringTagName, 0, CancellationToken.None)))
            .IsTrue();
        _ = Assert.Throws<ArgumentNullException>(
            () => client.WriteStringByTagAsync(StringTagName, null!, 1, CancellationToken.None));
        _ = Assert.Throws<ArgumentOutOfRangeException>(
            () => client.WriteStringByTagAsync(StringTagName, "text", 0, CancellationToken.None));

        simulator.EnqueueResponse(MitsubishiSimulatorTransport.CreateErrorResponse(options, 0xC051));
        var failed = await client.ReadGeneratedBitTagAsync(BitTagName, CancellationToken.None);
        await Assert.That(failed.IsSucceed).IsFalse();
    }

    /// <summary>Exercises bit, stale, latest, diagnostic, and health observable wrappers.</summary>
    /// <returns>A task that represents the asynchronous test.</returns>
    [Test]
    internal async Task ObservableWrappersUseTheConfiguredSchedulerAsync()
    {
        var scheduler = new TestScheduler();
        var options = CreateOptions();
        await using var simulator = CreateSimulator(options);
        await using var client = new MitsubishiRx(options, simulator, scheduler);
        var bits = new List<Responce<bool[]>>();
        using var bitSubscription = client
            .ObserveBits("M0", 1, TimeSpan.FromSeconds(PollSeconds), null)
            .Take(1)
            .Subscribe(bits.Add);
        using var staleSubscription = client
            .ObserveWordsStale("D0", 1, TimeSpan.FromSeconds(PollSeconds), TimeSpan.FromSeconds(1), null)
            .Take(1)
            .Subscribe(_ => { });
        using var latestSubscription = client
            .ObserveWordsLatest("D0", 1, Observable.Return(Unit.Default, scheduler))
            .Take(1)
            .Subscribe(_ => { });
        using var diagnostics = client.SampleDiagnostics(Observable.Never<object>()).Subscribe(_ => { });
        using var health = client.ObserveConnectionHealth(TimeSpan.FromSeconds(1)).Subscribe(_ => { });

        TestSchedulerDriver.AdvanceBy(
            scheduler,
            TimeSpan.FromMilliseconds(SchedulerAdvanceMilliseconds).Ticks);

        await Assert.That(bits).Count().IsEqualTo(1);
        await Assert.That(bits[0].Value).IsEquivalentTo([true]);
        _ = Assert.Throws<ArgumentNullException>(
            () => client.ObserveWordsLatest("D0", 1, null!));
        _ = Assert.Throws<ArgumentNullException>(() => client.SampleDiagnostics(null!));
    }

    /// <summary>Exercises all untyped read and write dispatch branches.</summary>
    /// <returns>A task that represents the asynchronous test.</returns>
    [Test]
    internal async Task UntypedReadAndWriteMatricesDispatchEveryDeclaredTypeAsync()
    {
        var options = CreateOptions();
        await using var simulator = CreateSimulator(options);
        await using var client = CreateTaggedClient(options, simulator, Scheduler.Immediate);
        var readNames = new[]
        {
            BitTagName, StringTagName, FloatTagName, DWordTagName,
            Int32TagName, Int16TagName, WordTagName, RawWordTagName,
        };
        var reads = new List<Responce<object?>>();
        foreach (var name in readNames)
        {
            reads.Add(await client.ReadTagAsync(name, CancellationToken.None));
        }

        var writes = new[]
        {
            await client.WriteTagAsync(BitTagName, true, CancellationToken.None),
            await client.WriteTagAsync(StringTagName, "OK", CancellationToken.None),
            await client.WriteTagAsync(FloatTagName, 1.0F, CancellationToken.None),
            await client.WriteTagAsync(DWordTagName, 1U, CancellationToken.None),
            await client.WriteTagAsync(Int32TagName, 1, CancellationToken.None),
            await client.WriteTagAsync(Int16TagName, (short)1, CancellationToken.None),
            await client.WriteTagAsync(WordTagName, (ushort)1, CancellationToken.None),
            await client.WriteTagAsync(RawWordTagName, (ushort)1, CancellationToken.None),
            await client.WriteTagAsync(
                ScaledWordTagName,
                ScaledEngineeringValue,
                CancellationToken.None),
        };

        await Assert.That(reads.All(static result => result.IsSucceed)).IsTrue();
        await Assert.That(writes.All(static result => result.IsSucceed)).IsTrue();
    }

    /// <summary>Exercises group value validation and all numeric scaled-read projections.</summary>
    /// <returns>A task that represents the asynchronous test.</returns>
    [Test]
    internal async Task GroupValidationAndScaledReadMatricesCoverAllTypesAsync()
    {
        var options = CreateOptions();
        await using var simulator = CreateSimulator(options);
        await using var client = CreateTaggedClient(options, simulator, Scheduler.Immediate);
        var valid = client.ValidateTagGroupWrite(
            AllTypesGroupName,
            new Dictionary<string, object?>
            {
                [BitTagName] = true,
                [StringTagName] = "OK",
                [FloatTagName] = 1.0F,
                [DWordTagName] = 1U,
                [Int32TagName] = 1,
                [Int16TagName] = (short)1,
                [WordTagName] = (ushort)1,
                [RawWordTagName] = (ushort)1,
                [ScaledWordTagName] = ScaledEngineeringValue,
            });
        var invalid = client.ValidateTagGroupWrite(
            AllTypesGroupName,
            new Dictionary<string, object?>
            {
                [BitTagName] = null,
                [StringTagName] = 1,
                ["Outside"] = 1,
            });
        var scaledNames = new[]
        {
            ScaledWordTagName, ScaledInt16TagName, "ScaledUInt16", ScaledDWordTagName,
            "ScaledInt32", "ScaledUInt32", ScaledFloatTagName,
        };
        foreach (var name in scaledNames)
        {
            await Assert.That(
                (await client.ReadScaledDoubleByTagAsync(name, CancellationToken.None)).IsSucceed)
                .IsTrue();
        }

        await Assert.That(valid.IsSucceed).IsTrue();
        await Assert.That(invalid.IsSucceed).IsFalse();
    }

    /// <summary>Exercises conversion, encoding, compatibility, and database forwarding failures.</summary>
    /// <returns>A task that represents the asynchronous test.</returns>
    [Test]
    internal async Task ConversionAndDatabaseFailurePathsAreReportedAsync()
    {
        var options = CreateOptions();
        await using var simulator = CreateSimulator(options);
        await using var client = CreateTaggedClient(options, simulator, Scheduler.Immediate);
        simulator.EnqueueResponse(MitsubishiSimulatorTransport.CreateSuccessResponse(options, [0x34, 0x12]));
        var shortInt = await client.ReadInt32ByTagAsync(Int32TagName, CancellationToken.None);
        var tooLong = await client.WriteTagAsync(
            StringTagName,
            "text exceeds capacity",
            CancellationToken.None);
        var zeroScale = await client.WriteTagAsync(
            "ZeroScale",
            ZeroScaleEngineeringValue,
            CancellationToken.None);
        var incompatible = await client.WriteTagAsync(BitTagName, "not a bit", CancellationToken.None);
        var missingLoad = client.LoadAndValidateTagDatabase(
            Path.Combine(Path.GetTempPath(), $"missing-{Guid.NewGuid():N}.json"));
        var missingPreview = client.PreviewTagDatabaseDiff(
            Path.Combine(Path.GetTempPath(), $"missing-{Guid.NewGuid():N}.json"));

        await Assert.That(shortInt.IsSucceed).IsFalse();
        await Assert.That(tooLong.IsSucceed).IsFalse();
        await Assert.That(zeroScale.IsSucceed).IsFalse();
        await Assert.That(incompatible.IsSucceed).IsFalse();
        await Assert.That(missingLoad.IsSucceed).IsFalse();
        await Assert.That(missingPreview.IsSucceed).IsFalse();
    }

    /// <summary>Exercises grouped operation failures, reload guards, and empty 1C random reads.</summary>
    /// <returns>A task that represents the asynchronous test.</returns>
    [Test]
    internal async Task GroupedReloadAndOneCGuardPathsAreReportedAsync()
    {
        var options = CreateOptions();
        await using var simulator = CreateSimulator(options);
        await using var client = CreateTaggedClient(options, simulator, Scheduler.Immediate);
        await using var bareTransport = CreateSimulator(options);
        await using var bareClient = new MitsubishiRx(options, bareTransport, Scheduler.Immediate);

        var noDatabase = bareClient.ValidateTagGroupWrite(
            AllTypesGroupName,
            new Dictionary<string, object?>());
        var invalidWrite = await client.WriteTagGroupValuesAsync(
            AllTypesGroupName,
            new Dictionary<string, object?> { [BitTagName] = "bad" },
            CancellationToken.None);
        simulator.EnqueueResponse(MitsubishiSimulatorTransport.CreateErrorResponse(options, 0xC051));
        var failedWrite = await client.WriteTagGroupValuesAsync(
            AllTypesGroupName,
            new Dictionary<string, object?> { [BitTagName] = true },
            CancellationToken.None);
        simulator.EnqueueResponse(MitsubishiSimulatorTransport.CreateErrorResponse(options, 0xC052));
        var failedRead = await client.ReadTagGroupSnapshotAsync(
            AllTypesGroupName,
            CancellationToken.None);

        _ = Assert.Throws<ArgumentOutOfRangeException>(
            () => client.ObserveTagDatabaseDiff("tags.json", TimeSpan.Zero, false));
        _ = Assert.Throws<ArgumentOutOfRangeException>(
            () => client.ObserveTagDatabaseReload("tags.json", TimeSpan.Zero, false));
        var serialOptions = CreateSerialOneCOptions();
        await using var serialSimulator = CreateSimulator(serialOptions);
        await using var serialClient = new MitsubishiRx(
            serialOptions,
            serialSimulator,
            Scheduler.Immediate);
        var emptyRandom = await serialClient.RandomReadWordsAsync([], CancellationToken.None);

        await Assert.That(noDatabase.IsSucceed).IsFalse();
        await Assert.That(invalidWrite.IsSucceed).IsFalse();
        await Assert.That(failedWrite.IsSucceed).IsFalse();
        await Assert.That(failedRead.IsSucceed).IsFalse();
        await Assert.That(emptyRandom.IsSucceed).IsFalse();
    }

    /// <summary>Creates a client with tags covering each dispatch branch.</summary>
    /// <param name="options">The client options.</param>
    /// <param name="simulator">The simulator transport.</param>
    /// <param name="scheduler">The scheduler.</param>
    /// <returns>The tagged client.</returns>
    private static MitsubishiRx CreateTaggedClient(
        MitsubishiClientOptions options,
        MitsubishiSimulatorTransport simulator,
        IScheduler scheduler)
    {
        var client = new MitsubishiRx(options, simulator, scheduler)
        {
            TagDatabase = new(
            [
                new MitsubishiTagDefinition(BitTagName, "M0", "Bit"),
                new MitsubishiTagDefinition(Int32TagName, "D0", "Int32"),
                new MitsubishiTagDefinition(ScaledDWordTagName, "D2", "DWord", Scale: 2.0, Offset: 1.0),
                new MitsubishiTagDefinition(ScaledFloatTagName, "D4", "Float", Scale: 2.0, Offset: 1.0),
                new MitsubishiTagDefinition(ScaledInt16TagName, "D6", "Int16", Scale: 2.0, Offset: 1.0),
                new MitsubishiTagDefinition(StringNoLengthTagName, "D7", "String"),
                new MitsubishiTagDefinition(StringTagName, "D8", "String", Length: StringWordLength),
                new MitsubishiTagDefinition(WordTagName, "D10", UInt16DataType),
                new MitsubishiTagDefinition(FloatTagName, "D11", "Float"),
                new MitsubishiTagDefinition(DWordTagName, "D13", "DWord"),
                new MitsubishiTagDefinition(Int16TagName, "D15", "Int16"),
                new MitsubishiTagDefinition(RawWordTagName, "D16"),
                new MitsubishiTagDefinition(ScaledWordTagName, "D17", "Word", Scale: 2.0),
                new MitsubishiTagDefinition("ScaledUInt16", "D18", UInt16DataType, Scale: 2.0),
                new MitsubishiTagDefinition("ScaledInt32", "D19", "Int32", Scale: 2.0),
                new MitsubishiTagDefinition("ScaledUInt32", "D21", "UInt32", Scale: 2.0),
                new MitsubishiTagDefinition("ZeroScale", "D23", "Word", Scale: 0.0),
            ]),
        };
        client.TagDatabase.AddGroup(
            new MitsubishiTagGroupDefinition(
                AllTypesGroupName,
                [
                    BitTagName, StringTagName, FloatTagName, DWordTagName,
                    Int32TagName, Int16TagName, WordTagName, RawWordTagName,
                    ScaledWordTagName,
                ]));
        return client;
    }

    /// <summary>Creates a simulator returning deterministic bit and word payloads.</summary>
    /// <param name="options">The client options.</param>
    /// <returns>The simulator transport.</returns>
    private static MitsubishiSimulatorTransport CreateSimulator(MitsubishiClientOptions options) =>
        new(request => MitsubishiSimulatorTransport.CreateSuccessResponse(
            options,
            request.Description.Contains("bits", StringComparison.OrdinalIgnoreCase)
                ? [0x01]
                : WordPayload));

    /// <summary>Creates deterministic MC options.</summary>
    /// <returns>The options.</returns>
    private static MitsubishiClientOptions CreateOptions() =>
        new(
            LoopbackHost,
            LoopbackPort,
            MitsubishiFrameType.ThreeE,
            CommunicationDataCode.Binary,
            MitsubishiTransportKind.Tcp);

    /// <summary>Creates deterministic 1C serial options.</summary>
    /// <returns>The serial options.</returns>
    private static MitsubishiClientOptions CreateSerialOneCOptions() =>
        new(
            "SIM",
            0,
            MitsubishiFrameType.OneC,
            CommunicationDataCode.Ascii,
            MitsubishiTransportKind.Serial,
            CpuType: CpuType.Fx3,
            Serial: new MitsubishiSerialOptions("SIM"));

    /// <summary>Determines whether an asynchronous action throws the expected exception.</summary>
    /// <typeparam name="TException">The expected exception type.</typeparam>
    /// <param name="action">The asynchronous action.</param>
    /// <returns><see langword="true"/> when the expected exception is observed.</returns>
    private static async Task<bool> ThrowsAsync<TException>(Func<Task> action)
        where TException : Exception
    {
        try
        {
            await action();
            return false;
        }
        catch (TException)
        {
            return true;
        }
    }
}
