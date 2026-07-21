// Copyright (c) 2022-2026 Chris Pulman. All rights reserved.
// Chris Pulman licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

#if NET9_0_OR_GREATER
using System.Diagnostics.CodeAnalysis;
#endif
using System.Reactive.Subjects;
using CP.IoT.Core;
using LeanBridge = CP.TwinCatRx.ObservableBridgeExtensions;

namespace TwinCATRx.Tests.Rx;

/// <summary>Tests the CP.IoT logical-tag adapter over the event-driven TwinCAT client.</summary>
public sealed class TwinCatLogicalTagClientTests
{
    /// <summary>The native direct tag address.</summary>
    private const string DirectAddress = ".Machine.Speed";

    /// <summary>The native structure root.</summary>
    private const string StructureRoot = ".Machine.State";

    /// <summary>The direct logical tag name.</summary>
    private const string SpeedName = "Speed";

    /// <summary>The count member name.</summary>
    private const string CountName = "Count";

    /// <summary>The enabled member name.</summary>
    private const string EnabledName = "Enabled";

    /// <summary>The correlated read value.</summary>
    private const int ReadValue = 42;

    /// <summary>The direct write value.</summary>
    private const int WriteValue = 43;

    /// <summary>The observed event value.</summary>
    private const int ObservedValue = 44;

    /// <summary>The initial structure count.</summary>
    private const int InitialCount = 7;

    /// <summary>The updated structure count.</summary>
    private const int UpdatedCount = 8;

    /// <summary>The number of structure-backed tags.</summary>
    private const int StructuredTagCount = 2;

    /// <summary>The asynchronous observation timeout in seconds.</summary>
    private const int ObservationTimeoutSeconds = 5;

    /// <summary>Verifies direct operations correlate native events and both observation APIs.</summary>
    /// <returns>The test task.</returns>
    [Test]
#if NET9_0_OR_GREATER
    [RequiresUnreferencedCode("The logical adapter supports HashTableRx structure materialization.")]
#endif
    public async Task Direct_Operations_And_Observations_Are_Event_CorrelatedAsync()
    {
        using var data = new Subject<(string Variable, object? Data, string? Id)>();
        using var native = new RxFakeClient(data);
        using var client = new TwinCatLogicalTagClient(native);
        client.RegisterTag(new LogicalTag(SpeedName, DirectAddress, "DINT"));

        var readTask = client.ReadAsync(SpeedName);
        var readCall = native.ReadCalls.Single();
        await TUnitAssert.That(readCall.Id).StartsWith("TwinCatLogicalTagClient:read:");
        data.OnNext((DirectAddress, ReadValue, "unrelated"));
        await TUnitAssert.That(readTask.IsCompleted).IsFalse();
        data.OnNext((DirectAddress, ReadValue, readCall.Id));
        var read = await readTask;

        var write = await client.WriteAsync(CreateValue(SpeedName, WriteValue));
        var writeCall = native.WriteCalls.Single();
        await TUnitAssert.That(read.Succeeded).IsTrue();
        await TUnitAssert.That(read.Value!.Value).IsEqualTo(ReadValue);
        await TUnitAssert.That(write.Succeeded).IsTrue();
        await TUnitAssert.That(writeCall.Variable).IsEqualTo(DirectAddress);
        await TUnitAssert.That(writeCall.Id).StartsWith("TwinCatLogicalTagClient:write:");

        var observed = new List<LogicalTagValue>();
        Func<string, IObservable<LogicalTagValue>> observe = client.Observe;
        using var subscription = LeanBridge.SubscribeTo(observe(SpeedName), observed.Add);
        using var cancellation = new CancellationTokenSource(TimeSpan.FromSeconds(ObservationTimeoutSeconds));
        var asyncEnumerator = client.ObserveAsync(SpeedName, cancellation.Token).GetAsyncEnumerator();
        try
        {
            var moveNext = asyncEnumerator.MoveNextAsync().AsTask();
            data.OnNext((DirectAddress, ObservedValue, null));
            await TUnitAssert.That(await moveNext).IsTrue();
            await TUnitAssert.That(observed.Single().Value).IsEqualTo(ObservedValue);
            await TUnitAssert.That(asyncEnumerator.Current.Value).IsEqualTo(ObservedValue);
        }
        finally
        {
            await asyncEnumerator.DisposeAsync();
        }
    }

    /// <summary>Verifies structure-backed bulk operations use one root read and one root write.</summary>
    /// <returns>The test task.</returns>
    [Test]
#if NET9_0_OR_GREATER
    [RequiresUnreferencedCode("The logical adapter supports HashTableRx structure materialization.")]
#endif
    public async Task Structured_Bulk_Operations_Are_Grouped_By_RootAsync()
    {
        using var data = new Subject<(string Variable, object? Data, string? Id)>();
        using var native = new RxFakeClient(data);
        using var client = new TwinCatLogicalTagClient(native);
        client.RegisterTag(CreateStructuredTag(CountName, CountName));
        client.RegisterTag(CreateStructuredTag(EnabledName, EnabledName));

        var readTask = client.ReadManyAsync([CountName, EnabledName]);
        var readCall = native.ReadCalls.Single();
        data.OnNext((StructureRoot, new TestStructure { Count = InitialCount, Enabled = true }, readCall.Id));
        var reads = await readTask;

        await TUnitAssert.That(native.ReadCalls.Count).IsEqualTo(1);
        await TUnitAssert.That(reads.Count).IsEqualTo(StructuredTagCount);
        await TUnitAssert.That(reads[0].Value!.Value).IsEqualTo(InitialCount);
        await TUnitAssert.That((bool)reads[1].Value!.Value!).IsTrue();

        var writeTask = client.WriteManyAsync([CreateValue(CountName, UpdatedCount), CreateValue(EnabledName, false)]);
        var structureRead = native.ReadCalls[1];
        data.OnNext((StructureRoot, new TestStructure { Count = InitialCount, Enabled = true }, structureRead.Id));
        var writes = await writeTask;
        var rootWrite = native.WriteCalls.Single();
        var structure = (TestStructure)rootWrite.Value;

        await TUnitAssert.That(native.ReadCalls.Count).IsEqualTo(StructuredTagCount);
        await TUnitAssert.That(rootWrite.Variable).IsEqualTo(StructureRoot);
        await TUnitAssert.That(structure.Count).IsEqualTo(UpdatedCount);
        await TUnitAssert.That(structure.Enabled).IsFalse();
        await TUnitAssert.That(writes.All(static result => result.Succeeded)).IsTrue();
    }

    /// <summary>Verifies CSV and SQLite definitions update the live catalog dynamically.</summary>
    /// <returns>The test task.</returns>
    [Test]
#if NET9_0_OR_GREATER
    [RequiresUnreferencedCode("The logical adapter supports HashTableRx structure materialization.")]
#endif
    public async Task Csv_And_Sqlite_Crud_Synchronize_The_Live_CatalogAsync()
    {
        var databasePath = Path.Combine(Path.GetTempPath(), $"TwinCATRx_LogicalTags_{Guid.NewGuid()}.db");
        try
        {
            using var native = new RxFakeClient(Observable.Empty<(string Variable, object? Data, string? Id)>());
            var store = new LogicalTagSqliteStore($"Data Source={databasePath};Pooling=False");
            using var client = new TwinCatLogicalTagClient(native, store: store);
            await client.InitializeStoreAsync();

            var tag = new LogicalTag(SpeedName, DirectAddress, "DINT", accessMode: LogicalTagAccessMode.Read);
            await client.UpsertTagAsync(tag);
            var persisted = await client.GetTagAsync(SpeedName);
            var edited = tag.With(description: "Line speed", accessMode: LogicalTagAccessMode.ReadWrite);
            await TUnitAssert.That(await client.EditTagAsync(edited)).IsTrue();
            await TUnitAssert.That(persisted!.Address).IsEqualTo(DirectAddress);

#if NET9_0_OR_GREATER
            await using var writer = new StringWriter(System.Globalization.CultureInfo.InvariantCulture);
#else
            using var writer = new StringWriter(System.Globalization.CultureInfo.InvariantCulture);
#endif
            await client.ExportCsvAsync(writer);
            await TUnitAssert.That(writer.ToString()).Contains(SpeedName);

            using var loaded = new TwinCatLogicalTagClient(native, store: store);
            var loadedTags = await loaded.LoadTagsAsync();
            await TUnitAssert.That(loadedTags.Single().Description).IsEqualTo("Line speed");
            await TUnitAssert.That(loaded.Catalog.TryGet(SpeedName, out _)).IsTrue();

            using var reader = new StringReader(writer.ToString());
            var imported = await loaded.ImportCsvAsync(reader);
            await TUnitAssert.That(imported.Single().Name).IsEqualTo(SpeedName);
            await TUnitAssert.That(await loaded.DeleteTagAsync(SpeedName)).IsTrue();
            await TUnitAssert.That(loaded.Catalog.TryGet(SpeedName, out _)).IsFalse();
        }
        finally
        {
            File.Delete(databasePath);
        }
    }

    /// <summary>Creates a structure-backed logical tag.</summary>
    /// <param name="name">The logical name.</param>
    /// <param name="member">The relative structure member.</param>
    /// <returns>The logical tag.</returns>
    private static LogicalTag CreateStructuredTag(string name, string member) =>
        new(
            name,
            $"{StructureRoot}.{member}",
            member == CountName ? "DINT" : "BOOL",
            metadata: new Dictionary<string, string>
            {
                ["TwinCAT.StructureRoot"] = StructureRoot,
                ["TwinCAT.MemberAddress"] = member,
            });

    /// <summary>Creates a current logical tag value.</summary>
    /// <param name="name">The logical name.</param>
    /// <param name="value">The payload.</param>
    /// <returns>The logical value.</returns>
    private static LogicalTagValue CreateValue(string name, object value) =>
        new(name, value, DateTimeOffset.UtcNow, "Good");

    /// <summary>Simple reflected structure used by HashTableRx.</summary>
    private sealed class TestStructure
    {
        /// <summary>Gets or sets the count.</summary>
        public int Count { get; set; }

        /// <summary>Gets or sets whether the state is enabled.</summary>
        public bool Enabled { get; set; }
    }
}
