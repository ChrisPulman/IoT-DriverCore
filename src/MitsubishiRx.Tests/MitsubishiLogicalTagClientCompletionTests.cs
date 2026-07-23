// Copyright (c) 2019-2026 Chris Pulman and contributors. All rights reserved.
// Chris Pulman and contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using IoT.DriverCore.Core;

#if REACTIVE_SHIM

namespace IoT.DriverCore.MitsubishiRx.Reactive.Tests;

#else

namespace IoT.DriverCore.MitsubishiRx.Tests;

#endif

/// <summary>Completes logical-tag persistence, metadata, observation, and guard coverage.</summary>
internal sealed class MitsubishiLogicalTagClientCompletionTests
{
    /// <summary>Stores the loopback port.</summary>
    private const int LoopbackPort = 5000;

    /// <summary>Stores the deterministic word value.</summary>
    private const ushort WordValue = 0x1234;

    /// <summary>Stores the deterministic tag name.</summary>
    private const string TagName = "Value";

    /// <summary>Stores the unsigned word data type.</summary>
    private const string UInt16DataType = "UInt16";

    /// <summary>Stores the secondary observed tag name.</summary>
    private const string OtherTagName = "Other";

    /// <summary>Stores invalid metadata text.</summary>
    private const string InvalidMetadata = "not-a-value";

    /// <summary>Stores the expected metadata scale.</summary>
    private const double MetadataScale = 2.5;

    /// <summary>Stores the expected metadata offset.</summary>
    private const double MetadataOffset = 1.5;

    /// <summary>Stores the expected metadata length.</summary>
    private const int MetadataLength = 2;

    /// <summary>Stores the expected merged observation count.</summary>
    private const int ExpectedMergedObservationCount = 3;

    /// <summary>Stores the virtual scheduler step.</summary>
    private const long SchedulerStep = 1;

    /// <summary>Exercises constructor guards, a pre-populated catalog, aliases, and metadata mapping.</summary>
    /// <returns>A task that represents the asynchronous test.</returns>
    [Test]
    internal async Task ConstructorAndMetadataPathsSynchronizeDatabaseAsync()
    {
        var options = CreateOptions();
        await using var transport = CreateSimulator(options);
        await using var owner = new MitsubishiRx(options, transport, Scheduler.Immediate);
        using var catalog = new LogicalTagCatalog();
        _ = catalog.TryAdd(CreateMetadataTag("Existing"));
        using var logical = new MitsubishiLogicalTagClient(
            owner,
            catalog,
            TimeSpan.FromSeconds(1),
            null);

        logical.Register(CreateMetadataTag(TagName));
        logical.RegisterTag(new LogicalTag("BadMetadata", "D2", UInt16DataType, new LogicalTagOptions
        {
            Description = " ",
            Metadata = new Dictionary<string, string>
            {
                ["Scale"] = InvalidMetadata,
                ["Offset"] = InvalidMetadata,
                ["Length"] = InvalidMetadata,
                ["Signed"] = InvalidMetadata,
                ["Encoding"] = " ",
            },
        }));

        var mapped = owner.TagDatabase!.GetRequired(TagName);
        await Assert.That(owner.TagDatabase.GetRequired("Existing").Address).IsEqualTo("D1");
        await Assert.That(mapped.Scale).IsEqualTo(MetadataScale);
        await Assert.That(mapped.Offset).IsEqualTo(MetadataOffset);
        await Assert.That(mapped.Length).IsEqualTo(MetadataLength);
        await Assert.That(mapped.Signed).IsTrue();
        await Assert.That(owner.TagDatabase.GetRequiredGroup("Primary").ResolvedTagNames)
            .Contains(TagName);
        await Assert.That(owner.TagDatabase.GetRequiredGroup("Secondary Group").ResolvedTagNames)
            .Contains(TagName);
        ValidateConstructorGuards(owner);
    }

    /// <summary>Exercises CSV forwarding and every configured-store edit/delete outcome.</summary>
    /// <returns>A task that represents the asynchronous test.</returns>
    [Test]
    internal async Task CsvAndStoreForwardersCoverSuccessAndMissesAsync()
    {
        var path = Path.Combine(Path.GetTempPath(), $"mitsubishi-logical-complete-{Guid.NewGuid():N}.db");
        try
        {
            var store = new LogicalTagSqliteStore($"Data Source={path};Pooling=False");
            var options = CreateOptions();
            await using var transport = CreateSimulator(options);
            await using var owner = new MitsubishiRx(options, transport, Scheduler.Immediate);
            using var logical = owner.CreateLogicalTagClient(null, null, store);
            logical.RegisterTag(new LogicalTag(TagName, "D0", UInt16DataType));

            using var writer = new StringWriter(System.Globalization.CultureInfo.InvariantCulture);
            await logical.ExportCsvAsync(writer, ';', CancellationToken.None);
            using var reader = new StringReader(writer.ToString());
            var imported = await logical.ImportCsvAsync(reader, ';', CancellationToken.None);

            await logical.InitializeStoreAsync(CancellationToken.None);
            await logical.UpsertTagAsync(imported[0], CancellationToken.None);
            var edited = await logical.EditTagAsync(
                new LogicalTag(TagName, "D10", UInt16DataType),
                CancellationToken.None);
            var missingEdit = await logical.EditTagAsync(
                new LogicalTag("Missing", "D11", UInt16DataType),
                CancellationToken.None);
            var missingDelete = await logical.DeleteTagAsync("Missing", CancellationToken.None);

            await Assert.That(imported).Count().IsEqualTo(1);
            await Assert.That(edited).IsTrue();
            await Assert.That(missingEdit).IsFalse();
            await Assert.That(missingDelete).IsFalse();
            await Assert.That(logical.Catalog.TryGet(TagName, out var changed)).IsTrue();
            await Assert.That(changed!.Address).IsEqualTo("D10");
        }
        finally
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
    }

    /// <summary>Exercises untyped, typed, merged, and asynchronous observation adapters.</summary>
    /// <returns>A task that represents the asynchronous test.</returns>
    [Test]
    internal async Task ObservableAdaptersProjectSimulatorValuesAsync()
    {
        var scheduler = new TestScheduler();
        var options = CreateOptions();
        await using var transport = CreateSimulator(options);
        await using var owner = new MitsubishiRx(options, transport, scheduler);
        using var logical = owner.CreateLogicalTagClient(null, TimeSpan.FromSeconds(1), null);
        logical.RegisterRange(
        [
            new LogicalTag(TagName, "D0", UInt16DataType),
            new LogicalTag(OtherTagName, "D1", UInt16DataType),
        ]);

        var values = new List<LogicalTagValue>();
        var typed = new List<ushort>();
        Exception? typeError = null;
        using var first = logical.Observe(TagName).Take(1).Subscribe(values.Add);
        using var second = logical.Observe(new LogicalTagKey<ushort>(TagName)).Take(1).Subscribe(typed.Add);
        using var wrong = logical
            .Observe(new LogicalTagKey<string>(TagName))
            .Take(1)
            .Subscribe(_ => { }, error => typeError = error);
        using var many = logical
            .ObserveMany([TagName, OtherTagName])
            .Take(MetadataLength)
            .Subscribe(values.Add);
        TestSchedulerDriver.AdvanceBy(scheduler, SchedulerStep);

        await Assert.That(values).Count().IsEqualTo(ExpectedMergedObservationCount);
        await Assert.That(typed).IsEquivalentTo([WordValue]);
        await Assert.That(typeError).IsTypeOf<InvalidCastException>();
        await Assert.That(values.All(static value => value.Quality == "Good")).IsTrue();
        _ = Assert.Throws<ArgumentNullException>(
            () => logical.ObserveMany([TagName]).Subscribe((IObserver<LogicalTagValue>)null!));
        await ValidateAsyncObserversAsync(logical, scheduler);
    }

    /// <summary>Exercises no-store guards and transport response/exception failures.</summary>
    /// <returns>A task that represents the asynchronous test.</returns>
    [Test]
    internal async Task StoreAndTransportFailuresReturnDeterministicResultsAsync()
    {
        var options = CreateOptions();
        await using var transport = CreateSimulator(options);
        await using var owner = new MitsubishiRx(options, transport, Scheduler.Immediate);
        using var logical = owner.CreateLogicalTagClient(null, null, null);
        logical.RegisterTag(new LogicalTag(TagName, "D0", UInt16DataType));

        _ = Assert.Throws<InvalidOperationException>(
            () => logical.InitializeStoreAsync(CancellationToken.None));
        _ = Assert.Throws<InvalidOperationException>(
            () => logical.GetTagAsync(TagName, CancellationToken.None));
        await Assert.That(await ThrowsAsync<InvalidOperationException>(
            () => logical.UpsertTagAsync(
                new LogicalTag("Store", "D1", UInt16DataType),
                CancellationToken.None))).IsTrue();

        transport.EnqueueResponse(MitsubishiSimulatorTransport.CreateErrorResponse(options, 0xC051));
        var failedRead = await logical.ReadAsync(TagName, CancellationToken.None);
        transport.EnqueueFault(new InvalidOperationException("read fault"));
        var exceptionRead = await logical.ReadAsync(TagName, CancellationToken.None);
        transport.EnqueueResponse(MitsubishiSimulatorTransport.CreateErrorResponse(options, 0xC052));
        var failedWrite = await logical.WriteAsync(CreateValue(), CancellationToken.None);
        transport.EnqueueFault(new InvalidOperationException("write fault"));
        var exceptionWrite = await logical.WriteAsync(CreateValue(), CancellationToken.None);

        await Assert.That(failedRead.Succeeded).IsFalse();
        await Assert.That(exceptionRead.Succeeded).IsTrue();
        await Assert.That(failedWrite.Succeeded).IsFalse();
        await Assert.That(exceptionWrite.Succeeded).IsTrue();
    }

    /// <summary>Validates constructor and public method guards.</summary>
    /// <param name="owner">The live owner.</param>
    private static void ValidateConstructorGuards(MitsubishiRx owner)
    {
        _ = Assert.Throws<ArgumentNullException>(
            () => _ = new MitsubishiLogicalTagClient(null!, null, null, null));
        _ = Assert.Throws<ArgumentNullException>(
            () => _ = new MitsubishiLogicalTagClient(owner, null, null, null, null!));
        _ = Assert.Throws<ArgumentOutOfRangeException>(
            () => _ = new MitsubishiLogicalTagClient(owner, null, TimeSpan.Zero, null));
        using var logical = owner.CreateLogicalTagClient(null, null, null);
        _ = Assert.Throws<ArgumentNullException>(() => logical.RegisterTag(null!));
        _ = Assert.Throws<ArgumentNullException>(() => logical.RegisterRange(null!));
        _ = Assert.Throws<ArgumentNullException>(() => logical.CreateTag(null!));
        _ = Assert.Throws<ArgumentNullException>(
            () => logical.Observe((LogicalTagKey<ushort>)null!));
        _ = Assert.Throws<ArgumentNullException>(
            () => logical.ObserveMany(null!));
        logical.Dispose();
        logical.Dispose();
    }

    /// <summary>Validates asynchronous observation adapters.</summary>
    /// <param name="logical">The logical client.</param>
    /// <param name="scheduler">The virtual scheduler.</param>
    /// <returns>A task that represents the asynchronous test.</returns>
    private static async Task ValidateAsyncObserversAsync(
        MitsubishiLogicalTagClient logical,
        TestScheduler scheduler)
    {
        await using var single = logical.ObserveAsync(TagName).GetAsyncEnumerator();
        var singleNext = single.MoveNextAsync().AsTask();
        TestSchedulerDriver.AdvanceBy(scheduler, SchedulerStep);
        await Assert.That(await singleNext).IsTrue();
        await Assert.That(single.Current.Value).IsEqualTo(WordValue);

        await using var typed = logical
            .ObserveAsync(new LogicalTagKey<ushort>(TagName), CancellationToken.None)
            .GetAsyncEnumerator();
        var typedNext = typed.MoveNextAsync().AsTask();
        TestSchedulerDriver.AdvanceBy(scheduler, SchedulerStep);
        await Assert.That(await typedNext).IsTrue();
        await Assert.That(typed.Current).IsEqualTo(WordValue);

        await using var many = logical
            .ObserveManyAsync([TagName, OtherTagName])
            .GetAsyncEnumerator();
        var manyNext = many.MoveNextAsync().AsTask();
        TestSchedulerDriver.AdvanceBy(scheduler, SchedulerStep);
        await Assert.That(await manyNext).IsTrue();
    }

    /// <summary>Creates a tag with every Mitsubishi metadata setting.</summary>
    /// <param name="name">The logical tag name.</param>
    /// <returns>The metadata-rich tag.</returns>
    private static LogicalTag CreateMetadataTag(string name) =>
        new(name, name == TagName ? "D0" : "D1", UInt16DataType, new LogicalTagOptions
        {
            GroupName = "Primary",
            Description = "Metadata tag",
            Metadata = new Dictionary<string, string>
            {
                ["Scale"] = "2.5",
                ["Offset"] = "1.5",
                ["Length"] = "2",
                ["Encoding"] = "ASCII",
                ["Units"] = "rpm",
                ["Signed"] = "true",
                ["ByteOrder"] = "LittleEndian",
                ["Notes"] = "test",
                ["Groups"] = "Secondary%20Group|Primary",
            },
        });

    /// <summary>Creates a simulator returning one deterministic word.</summary>
    /// <param name="options">The client options.</param>
    /// <returns>The simulator transport.</returns>
    private static MitsubishiSimulatorTransport CreateSimulator(MitsubishiClientOptions options) =>
        new(request => MitsubishiSimulatorTransport.CreateSuccessResponse(
            options,
            request.Description.StartsWith("Read", StringComparison.Ordinal)
                ? [0x34, 0x12]
                : []));

    /// <summary>Creates deterministic client options.</summary>
    /// <returns>The client options.</returns>
    private static MitsubishiClientOptions CreateOptions() =>
        new(
            "127.0.0.1",
            LoopbackPort,
            MitsubishiFrameType.ThreeE,
            CommunicationDataCode.Binary,
            MitsubishiTransportKind.Tcp);

    /// <summary>Creates one logical word value.</summary>
    /// <returns>The logical value.</returns>
    private static LogicalTagValue CreateValue() =>
        new(TagName, WordValue, DateTimeOffset.UnixEpoch);

    /// <summary>Determines whether an asynchronous action throws the expected exception.</summary>
    /// <typeparam name="TException">The expected exception type.</typeparam>
    /// <param name="action">The asynchronous action.</param>
    /// <returns><see langword="true"/> when the exception is observed.</returns>
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
