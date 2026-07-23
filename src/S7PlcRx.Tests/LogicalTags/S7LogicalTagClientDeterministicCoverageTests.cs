// Copyright (c) 2019-2026 Chris Pulman and contributors. All rights reserved.
// Chris Pulman and contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using IoT.DriverCore.Core;
using IoT.DriverCore.S7PlcRx.LogicalTags;

namespace IoT.DriverCore.S7PlcRx.Tests.LogicalTags;

/// <summary>Provides deterministic coverage of the logical-tag client's public composition surface.</summary>
public sealed class S7LogicalTagClientDeterministicCoverageTests
{
    /// <summary>Defines the readable logical tag name.</summary>
    private const string ReadTagName = "ReadValue";

    /// <summary>Defines the writable logical tag name.</summary>
    private const string WriteTagName = "WriteValue";

    /// <summary>Defines the read-only logical tag name.</summary>
    private const string ReadOnlyTagName = "ReadOnly";

    /// <summary>Defines the write-only logical tag name.</summary>
    private const string WriteOnlyTagName = "WriteOnly";

    /// <summary>Defines the missing logical tag name.</summary>
    private const string MissingTagName = "Missing";

    /// <summary>Defines the transient logical tag name.</summary>
    private const string TransientTagName = "Transient";

    /// <summary>Defines the tag imported through the comma-delimited overload.</summary>
    private const string CommaImportedTagName = "ImportedComma";

    /// <summary>Defines the tag imported through the custom-delimiter overload.</summary>
    private const string SemicolonImportedTagName = "ImportedSemicolon";

    /// <summary>Defines the persisted group name.</summary>
    private const string ProcessGroupName = "Process";

    /// <summary>Defines the initial fallback read value.</summary>
    private const ushort InitialReadValue = 12;

    /// <summary>Defines the read-only fallback value.</summary>
    private const ushort ReadOnlyValue = 21;

    /// <summary>Defines the first successful write value.</summary>
    private const ushort FirstWriteValue = 42;

    /// <summary>Defines the bulk successful write value.</summary>
    private const ushort BulkWriteValue = 43;

    /// <summary>Defines the observed value.</summary>
    private const ushort ObservedValue = 99;

    /// <summary>Defines the observation timeout in seconds.</summary>
    private const int ObservationTimeoutSeconds = 5;

    /// <summary>Defines the array logical tag name.</summary>
    private const string ArrayTagName = "ArrayValue";

    /// <summary>Defines the array length metadata value.</summary>
    private const string ArrayLengthText = "2";

    /// <summary>Defines the simulated fallback read error message.</summary>
    private const string ReadFailureMessage = "Deterministic read failure";

    /// <summary>Defines an incompatible payload for array conversion validation.</summary>
    private const string InvalidArrayPayload = "not-an-array";

    /// <summary>Defines the expected number of bulk reads.</summary>
    private const int BulkReadCount = 4;

    /// <summary>Defines the expected number of bulk writes.</summary>
    private const int BulkWriteCount = 3;

    /// <summary>Verifies public catalog registration, removal, import, and export operations.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task PublicCatalogAndCsvOperationsSynchronizeRegistrationsAsync()
    {
        using var plc = new S7PlcRxAsyncExtensionsTests.TestPlc();
        using var catalog = new LogicalTagCatalog();
        using var client = new S7LogicalTagClient(plc, catalog, TimeProvider.System);
        var transient = new LogicalTag(TransientTagName, "DB1.DBW0", "WORD");
        const string registeredTagName = "Registered";
        using var commaReader = new StringReader(
            $"Name,Address,DataType,GroupName,Description,Metadata,AccessMode,ScanIntervalMilliseconds\r\n{CommaImportedTagName},DB1.DBW6,WORD,Process,Imported tag,,ReadWrite,100\r\n");
        using var semicolonReader = new StringReader(
            $"Name;Address;DataType;GroupName;Description;Metadata;AccessMode;ScanIntervalMilliseconds\r\n{SemicolonImportedTagName};DB1.DBW8;WORD;Process;Imported tag;;ReadWrite;100\r\n");
        using var commaWriter = new StringWriter();
        using var semicolonWriter = new StringWriter();

        var created = client.CreateTag(transient);
        client.RegisterTag(new LogicalTag(registeredTagName, "DB1.DBW4", "WORD"));
        var commaImported = await client.ImportCsvAsync(commaReader);
        var semicolonImported = await client.ImportCsvAsync(semicolonReader, ';');
        await client.ExportCsvAsync(commaWriter);
        await client.ExportCsvAsync(semicolonWriter, ';');

        await TUnit.Assertions.Assert.That(created).IsEqualTo(transient);
        await TUnit.Assertions.Assert.That(catalog.TryGet(registeredTagName, out _)).IsTrue();
        await TUnit.Assertions.Assert.That(commaImported.Count).IsEqualTo(1);
        await TUnit.Assertions.Assert.That(semicolonImported.Count).IsEqualTo(1);
        await TUnit.Assertions.Assert.That(
            commaWriter.ToString().Contains(CommaImportedTagName, StringComparison.Ordinal)).IsTrue();
        await TUnit.Assertions.Assert.That(
            semicolonWriter.ToString().Contains(SemicolonImportedTagName, StringComparison.Ordinal)).IsTrue();
        await TUnit.Assertions.Assert.That(client.RemoveTag(TransientTagName)).IsTrue();
        await TUnit.Assertions.Assert.That(client.RemoveTag(TransientTagName)).IsFalse();
    }

    /// <summary>Verifies SQLite CRUD, group forwarding, and load operations synchronize the live catalog.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task StoreCrudAndLoadOperationsSynchronizeTheLiveCatalogAsync()
    {
        var databasePath = Path.Combine(Path.GetTempPath(), $"s7-logical-tags-{Guid.NewGuid():N}.db");
        try
        {
            using var plc = new S7PlcRxAsyncExtensionsTests.TestPlc();
            using var catalog = new LogicalTagCatalog();
            using var client = new S7LogicalTagClient(plc, catalog, TimeProvider.System);
            var store = new LogicalTagSqliteStore($"Data Source={databasePath};Pooling=False");
            var persisted = CreatePersistedTag();
            var edited = CreateEditedTag(persisted);

            await client.InitializeStoreAsync(store);
            await client.UpsertGroupAsync(new LogicalTagGroup(ProcessGroupName, "Production values"));
            await client.UpsertTagAsync(persisted);

            await TUnit.Assertions.Assert.That(
                (await client.GetTagAsync(ReadTagName))?.Description).IsEqualTo("Initial");
            await TUnit.Assertions.Assert.That(
                (await client.ListTagsAsync()).Count).IsEqualTo(1);
            await TUnit.Assertions.Assert.That(
                (await client.GetGroupAsync(ProcessGroupName))?.Description).IsEqualTo("Production values");
            await TUnit.Assertions.Assert.That(
                (await client.ListGroupsAsync()).Count).IsEqualTo(1);
            await TUnit.Assertions.Assert.That(await client.EditTagAsync(edited)).IsTrue();
            await TUnit.Assertions.Assert.That(await client.UpdateTagAsync(edited)).IsTrue();

            _ = catalog.TryRemove(ReadTagName, out _);
            var loaded = await client.LoadTagsAsync();
            await TUnit.Assertions.Assert.That(loaded.Count).IsEqualTo(1);
            await TUnit.Assertions.Assert.That(catalog.TryGet(ReadTagName, out var loadedTag)).IsTrue();
            await TUnit.Assertions.Assert.That(loadedTag?.Description).IsEqualTo("Edited");
            await TUnit.Assertions.Assert.That(
                await client.DeleteGroupAsync(ProcessGroupName)).IsTrue();
            await TUnit.Assertions.Assert.That(await client.DeleteTagAsync(ReadTagName)).IsTrue();
            await TUnit.Assertions.Assert.That(await client.DeleteTagAsync(ReadTagName)).IsFalse();
        }
        finally
        {
            if (File.Exists(databasePath))
            {
                File.Delete(databasePath);
            }
        }
    }

    /// <summary>Verifies individual fallback reads and writes preserve order and report invalid logical definitions.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task ReadAndWriteFallbacksHandleBulkAccessAndMissingTagsAsync()
    {
        using var plc = new S7PlcRxAsyncExtensionsTests.TestPlc();
        using var catalog = CreateCatalog();
        SeedFallbackRuntimeTags(plc, catalog);
        using var client = new S7LogicalTagClient(plc, catalog, TimeProvider.System);
        plc.SetAsyncValue(ReadTagName, InitialReadValue);
        plc.SetAsyncValue(ReadOnlyTagName, ReadOnlyValue);
        var timestamp = TimeProvider.System.GetUtcNow();

        var read = await client.ReadAsync(ReadTagName, CancellationToken.None);
        var reads = await client.ReadManyAsync(
            [ReadTagName, MissingTagName, WriteOnlyTagName, ReadOnlyTagName],
            CancellationToken.None);
        var write = await client.WriteAsync(
            new LogicalTagValue(WriteTagName, FirstWriteValue.ToString(), timestamp),
            CancellationToken.None);
        var invalidWrite = await client.WriteAsync(
            new LogicalTagValue(WriteTagName, "not-a-word", timestamp),
            CancellationToken.None);
        var writes = await client.WriteManyAsync(
            [
                new LogicalTagValue(WriteTagName, BulkWriteValue.ToString(), timestamp),
                new LogicalTagValue(ReadOnlyTagName, ReadOnlyValue, timestamp),
                new LogicalTagValue(MissingTagName, FirstWriteValue, timestamp),
            ],
            CancellationToken.None);

        await TUnit.Assertions.Assert.That(read.Succeeded).IsTrue();
        await TUnit.Assertions.Assert.That(read.Value?.Value).IsEqualTo(InitialReadValue);
        await TUnit.Assertions.Assert.That(reads.Count).IsEqualTo(BulkReadCount);
        await TUnit.Assertions.Assert.That(reads[0].Succeeded).IsTrue();
        await TUnit.Assertions.Assert.That(reads[1].Succeeded).IsFalse();
        await TUnit.Assertions.Assert.That(reads[2].Succeeded).IsFalse();
        await TUnit.Assertions.Assert.That(reads[3].Value?.Value).IsEqualTo(ReadOnlyValue);
        await TUnit.Assertions.Assert.That(write.Succeeded).IsTrue();
        await TUnit.Assertions.Assert.That(write.Value?.Value).IsEqualTo(FirstWriteValue);
        await TUnit.Assertions.Assert.That(invalidWrite.Succeeded).IsFalse();
        await TUnit.Assertions.Assert.That(writes.Count).IsEqualTo(BulkWriteCount);
        await TUnit.Assertions.Assert.That(writes[0].Succeeded).IsTrue();
        await TUnit.Assertions.Assert.That(writes[1].Succeeded).IsFalse();
        await TUnit.Assertions.Assert.That(writes[2].Succeeded).IsFalse();
        await TUnit.Assertions.Assert.That(plc.WrittenValues[WriteTagName]).IsEqualTo(BulkWriteValue);
    }

    /// <summary>Verifies cancellation and invalid arguments are propagated rather than converted to operation failures.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task ReadWriteAndBulkOperationsPropagateCancellationAndValidateArgumentsAsync()
    {
        using var plc = new S7PlcRxAsyncExtensionsTests.TestPlc();
        using var catalog = CreateCatalog();
        using var client = new S7LogicalTagClient(plc, catalog, TimeProvider.System);
        using var cancellation = new CancellationTokenSource();
        await AsyncCompatibility.CancelAsync(cancellation);
        var value = new LogicalTagValue(WriteTagName, FirstWriteValue, TimeProvider.System.GetUtcNow());
        Func<Task> canceledRead = async () => _ = await client.ReadAsync(ReadTagName, cancellation.Token);
        Func<Task> canceledWrite = async () => _ = await client.WriteAsync(value, cancellation.Token);
        Func<Task> canceledBulkRead = async () => _ = await client.ReadManyAsync([ReadTagName], cancellation.Token);
        Func<Task> canceledBulkWrite = async () => _ = await client.WriteManyAsync([value], cancellation.Token);
        Func<Task> invalidName = async () => _ = await client.ReadAsync(" ", CancellationToken.None);
        Func<Task> nullReads = async () => _ = await client.ReadManyAsync(null!, CancellationToken.None);
        Func<Task> nullWrites = async () => _ = await client.WriteManyAsync(null!, CancellationToken.None);
        Func<Task> nullWriteEntry = async () => _ = await client.WriteManyAsync([null!], CancellationToken.None);

        await TUnit.Assertions.Assert.That(canceledRead).Throws<OperationCanceledException>();
        await TUnit.Assertions.Assert.That(canceledWrite).Throws<OperationCanceledException>();
        await TUnit.Assertions.Assert.That(canceledBulkRead).Throws<OperationCanceledException>();
        await TUnit.Assertions.Assert.That(canceledBulkWrite).Throws<OperationCanceledException>();
        await TUnit.Assertions.Assert.That(invalidName).Throws<ArgumentException>();
        await TUnit.Assertions.Assert.That(nullReads).Throws<ArgumentNullException>();
        await TUnit.Assertions.Assert.That(nullWrites).Throws<ArgumentNullException>();
        await TUnit.Assertions.Assert.That(nullWriteEntry).Throws<ArgumentException>();
    }

    /// <summary>Verifies observable and async-enumerable projections use the deterministic in-memory PLC signal.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task ObservationContractsProjectPublishedLogicalTagValuesAsync()
    {
        using var plc = new S7PlcRxAsyncExtensionsTests.TestPlc();
        using var catalog = CreateCatalog();
        using var client = new S7LogicalTagClient(plc, catalog, TimeProvider.System);
        var observed = new List<LogicalTagValue>();
        using var subscription = client.ObserveMany([ReadTagName, ReadOnlyTagName]).SubscribeSafe(
            observed.Add,
            static error => throw error);
        using var cancellation = new CancellationTokenSource(TimeSpan.FromSeconds(ObservationTimeoutSeconds));
        await using var enumerator = client
            .ObserveAsync(ReadTagName, cancellation.Token)
            .GetAsyncEnumerator(cancellation.Token);
        await using var manyEnumerator = client
            .ObserveManyAsync([ReadTagName, ReadOnlyTagName], cancellation.Token)
            .GetAsyncEnumerator(cancellation.Token);
        var moveNext = enumerator.MoveNextAsync().AsTask();
        var moveManyNext = manyEnumerator.MoveNextAsync().AsTask();
        await Task.Yield();
        plc.PublishObservedValue(ReadTagName, ObservedValue, typeof(ushort));

        await TUnit.Assertions.Assert.That(await moveNext).IsTrue();
        await TUnit.Assertions.Assert.That(await moveManyNext).IsTrue();
        await TUnit.Assertions.Assert.That(enumerator.Current.TagName).IsEqualTo(ReadTagName);
        await TUnit.Assertions.Assert.That(manyEnumerator.Current.TagName).IsEqualTo(ReadTagName);
        await TUnit.Assertions.Assert.That(enumerator.Current.Value).IsEqualTo(ObservedValue);
        await TUnit.Assertions.Assert.That(observed.Count).IsEqualTo(1);
        await TUnit.Assertions.Assert.That(observed[0].Value).IsEqualTo(ObservedValue);
        await TUnit.Assertions.Assert.That(() => client.Observe(MissingTagName))
            .Throws<KeyNotFoundException>();
        await TUnit.Assertions.Assert.That(() => client.Observe(WriteOnlyTagName))
            .Throws<KeyNotFoundException>();
        await TUnit.Assertions.Assert.That(() => client.ObserveMany(null!))
            .Throws<ArgumentNullException>();
    }

    /// <summary>Verifies fallback conversion failures, faulted reads, missing stores, and disposal lifecycle behavior.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task FallbackFailuresAndDisposalLifecycleRemainDeterministicAsync()
    {
        using var plc = new S7PlcRxAsyncExtensionsTests.TestPlc();
        using var catalog = new LogicalTagCatalog();
        var arrayTag = new LogicalTag(
            ArrayTagName,
            "DB1.DBB0",
            "WORD[]",
            new LogicalTagOptions
            {
                Metadata = new Dictionary<string, string> { ["ArrayLength"] = ArrayLengthText },
            });
        using var client = new S7LogicalTagClient(plc, catalog);
        Func<Task> missingStore = async () => _ = await client.ListTagsAsync();

        catalog.Upsert(arrayTag);
        plc.SetAsyncValue(ArrayTagName, new ushort[] { InitialReadValue, ReadOnlyValue });
        var arrayRead = await client.ReadAsync(ArrayTagName, CancellationToken.None);
        var invalidArrayWrite = await client.WriteAsync(
            new LogicalTagValue(ArrayTagName, InvalidArrayPayload, TimeProvider.System.GetUtcNow()),
            CancellationToken.None);
        plc.SetAsyncFactory(
            ArrayTagName,
            static _ => Task.FromException<object?>(new InvalidOperationException(ReadFailureMessage)));
        var faultedRead = await client.ReadAsync(ArrayTagName, CancellationToken.None);

        await TUnit.Assertions.Assert.That(missingStore).Throws<InvalidOperationException>();
        await TUnit.Assertions.Assert.That(arrayRead.Succeeded).IsTrue();
        await TUnit.Assertions.Assert.That(arrayRead.Value?.Value).IsEquivalentTo(
            new ushort[] { InitialReadValue, ReadOnlyValue });
        await TUnit.Assertions.Assert.That(invalidArrayWrite.Succeeded).IsFalse();
        await TUnit.Assertions.Assert.That(faultedRead.Succeeded).IsFalse();

        client.Dispose();
        client.Dispose();
        catalog.Upsert(arrayTag);
        Func<Task> disposedRead = async () => _ = await client.ReadAsync(ArrayTagName, CancellationToken.None);
        await TUnit.Assertions.Assert.That(disposedRead).Throws<ObjectDisposedException>();
    }

    /// <summary>Creates a persisted logical tag for store forwarding tests.</summary>
    /// <returns>A logical tag suitable for persistence.</returns>
    private static LogicalTag CreatePersistedTag() => new(
        ReadTagName,
        "DB1.DBW2",
        "WORD",
        new LogicalTagOptions { GroupName = ProcessGroupName, Description = "Initial" });

    /// <summary>Creates an edited copy of a persisted logical tag.</summary>
    /// <param name="tag">The tag to edit.</param>
    /// <returns>An edited tag definition.</returns>
    private static LogicalTag CreateEditedTag(LogicalTag tag)
    {
        var options = tag.CurrentOptions();
        options.Description = "Edited";
        return tag.WithOptions(options);
    }

    /// <summary>Creates the logical definitions consumed by deterministic fallback tests.</summary>
    /// <returns>A catalog with read/write access variants.</returns>
    private static LogicalTagCatalog CreateCatalog()
    {
        var catalog = new LogicalTagCatalog();
        catalog.Upsert(new LogicalTag(ReadTagName, "DB1.DBW0", "WORD"));
        catalog.Upsert(new LogicalTag(WriteTagName, "DB1.DBW2", "WORD"));
        catalog.Upsert(new LogicalTag(
            ReadOnlyTagName,
            "DB1.DBW4",
            "WORD",
            new LogicalTagOptions { AccessMode = LogicalTagAccessMode.Read }));
        catalog.Upsert(new LogicalTag(
            WriteOnlyTagName,
            "DB1.DBW6",
            "WORD",
            new LogicalTagOptions { AccessMode = LogicalTagAccessMode.Write }));
        return catalog;
    }

    /// <summary>Seeds the existing in-memory fake with the runtime registrations required by bulk fallback paths.</summary>
    /// <param name="plc">The in-memory PLC test fake.</param>
    /// <param name="catalog">The logical definitions to seed.</param>
    private static void SeedFallbackRuntimeTags(
        S7PlcRxAsyncExtensionsTests.TestPlc plc,
        LogicalTagCatalog catalog)
    {
        foreach (var definition in catalog.List())
        {
            plc.TagList.Add(new Tag(definition.Name, definition.Address, typeof(ushort)));
        }
    }
}
