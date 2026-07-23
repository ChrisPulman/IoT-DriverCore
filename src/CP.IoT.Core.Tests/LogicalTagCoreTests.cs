// Copyright (c) 2019-2026 Chris Pulman and contributors. All rights reserved.
// Chris Pulman and contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.
using IoT.DriverCore.Core;
using TUnit.Assertions;
using TUnit.Core;

namespace IoT.DriverCore.Core.Tests;

/// <summary>Direct coverage for logical tag models, catalog, CSV, persistence, and contracts.</summary>
public sealed class LogicalTagCoreTests
{
    /// <summary>The Int32 data-type string reused across tests.</summary>
    private const string Int32DataType = "Int32";

    /// <summary>The Counter tag name reused across tests.</summary>
    private const string CounterTagName = "Counter";

    /// <summary>The value written and read back in contract-helper tests.</summary>
    private const int ExpectedWriteValue = 42;

    /// <summary>The expected number of tag names in batch-read assertions.</summary>
    private const int TagNameCount = 2;

    /// <summary>The expected number of catalog change events after add, upsert, and remove.</summary>
    private const int CatalogChangeCount = 3;

    /// <summary>The fast poll interval in milliseconds used in SQLite tests.</summary>
    private const int FastPollMilliseconds = 100;

    /// <summary>The scan interval in milliseconds used in CSV round-trip tests.</summary>
    private const int CsvScanMilliseconds = 250;

    /// <summary>The expected number of ranges produced by the planner scenarios.</summary>
    private const int ExpectedTransferRangeCount = 3;

    /// <summary>The length of the first fully coalesced transfer range.</summary>
    private const long ExpectedCoalescedLength = 6;

    /// <summary>The input position of the overlapping request.</summary>
    private const int OverlapInputIndex = 2;

    /// <summary>The input position of the request in the other memory area.</summary>
    private const int OtherAreaInputIndex = 3;

    /// <summary>The input position of the write request.</summary>
    private const int WriteInputIndex = 4;

    /// <summary>The largest range length in the constrained planner scenario.</summary>
    private const long ConstrainedRangeLength = 4;

    /// <summary>The final offset in the constrained planner scenario.</summary>
    private const long FinalConstrainedOffset = 10;

    /// <summary>An out-of-range access mode value used to verify constructor validation.</summary>
    private const LogicalTagAccessMode InvalidAccessMode = (LogicalTagAccessMode)999;

    /// <summary>The tag names used in contract-helper tests.</summary>
    private static readonly string[] TagNames = ["A", "B"];

    /// <summary>Verifies validation and immutable metadata snapshots.</summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test.</returns>
    [Test]
    public async Task ModelsValidateAndCopyMetadataAsync()
    {
        LogicalTag? invalidTag = null;
        _ = Assert.Throws<ArgumentException>(() => invalidTag = new(" ", "A", Int32DataType));
        _ = Assert.Throws<ArgumentOutOfRangeException>(
            () => invalidTag = new("A", "A", Int32DataType, new() { AccessMode = InvalidAccessMode }));
        await Assert.That(invalidTag).IsNull();
        var source = new Dictionary<string, string> { ["unit"] = "C" };
        var tag = new LogicalTag(
            "Temperature",
            "DB1,DBD0",
            "Double",
            new()
            {
                GroupName = "Process",
                Description = "A \"quoted\" value",
                Metadata = source,
                AccessMode = LogicalTagAccessMode.Read,
                ScanInterval = TimeSpan.FromSeconds(1),
            });
        source["unit"] = "F";

        await Assert.That(tag.Metadata["unit"]).IsEqualTo("C");
        await Assert.That(tag.AccessMode).IsEqualTo(LogicalTagAccessMode.Read);
        await Assert.That(tag.ScanInterval).IsEqualTo(TimeSpan.FromSeconds(1));
        var updatedOptions = tag.CurrentOptions();
        updatedOptions.Description = "Changed";
        await Assert.That(tag.WithOptions(updatedOptions).Description).IsEqualTo("Changed");
        await Assert.That(TagOperationResult<int>.Success(ExpectedWriteValue).Succeeded).IsTrue();
        await Assert.That(TagOperationResult<int>.Failure("offline").Error).IsEqualTo("offline");
    }

    /// <summary>Verifies catalog mutations are visible and reported after each successful change.</summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test.</returns>
    [Test]
    public async Task CatalogSupportsCrudSnapshotsAndEventsAsync()
    {
        using var catalog = new LogicalTagCatalog();
        var changes = new List<LogicalTagChangeKind>();
        catalog.Changed += (_, args) => changes.Add(args.Kind);
        var original = new LogicalTag(CounterTagName, "N7:0", Int32DataType);
        var updated = original.WithAddress("N7:1");

        await Assert.That(catalog.TryAdd(original)).IsTrue();
        await Assert.That(catalog.TryAdd(original)).IsFalse();
        catalog.Upsert(updated);
        await Assert.That(catalog.TryGet(CounterTagName, out var found)).IsTrue();
        await Assert.That(found!.Address).IsEqualTo("N7:1");
        await Assert.That(catalog.List().Count).IsEqualTo(1);
        await Assert.That(catalog.TryRemove(CounterTagName, out var removed)).IsTrue();
        await Assert.That(removed!.Name).IsEqualTo(CounterTagName);
        await Assert.That(changes.Count).IsEqualTo(CatalogChangeCount);
        await Assert.That(changes[0]).IsEqualTo(LogicalTagChangeKind.Added);
        await Assert.That(changes[1]).IsEqualTo(LogicalTagChangeKind.Updated);
        await Assert.That(changes[2]).IsEqualTo(LogicalTagChangeKind.Removed);
    }

    /// <summary>Verifies delimiters, quotes, and embedded newlines round-trip through RFC 4180 CSV.</summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test.</returns>
    [Test]
    public async Task CsvRoundTripsRfc4180EdgeFieldsAsync()
    {
        var tag = new LogicalTag(
            "Display,Name",
            "DB1,DBD0",
            "String",
            new()
            {
                GroupName = "Production\nLine",
                Description = "Contains a comma, a \"quote\", and a newline\nnext line.",
                Metadata = new Dictionary<string, string> { ["format"] = "A&B=quoted" },
                AccessMode = LogicalTagAccessMode.Read,
                ScanInterval = TimeSpan.FromMilliseconds(CsvScanMilliseconds),
            });
        using var writer = new StringWriter();

        await LogicalTagCsv.ExportAsync([tag], writer);
        var imported = await LogicalTagCsv.ImportAsync(new StringReader(writer.ToString()));

        await Assert.That(imported.Count).IsEqualTo(1);
        await Assert.That(imported[0].Name).IsEqualTo(tag.Name);
        await Assert.That(imported[0].GroupName).IsEqualTo(tag.GroupName);
        await Assert.That(imported[0].Description).IsEqualTo(tag.Description);
        await Assert.That(imported[0].Metadata["format"]).IsEqualTo("A&B=quoted");
        await Assert.That(imported[0].AccessMode).IsEqualTo(LogicalTagAccessMode.Read);
        await Assert.That(imported[0].ScanInterval).IsEqualTo(TimeSpan.FromMilliseconds(CsvScanMilliseconds));
    }

    /// <summary>Verifies SQLite tag and group CRUD preserves metadata.</summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test.</returns>
    [Test]
    public async Task SqliteCrudPreservesMetadataAsync()
    {
        var file = TemporaryDatabasePath();
        try
        {
            var store = new LogicalTagSqliteStore($"Data Source={file};Pooling=False");
            await store.InitializeAsync();
            var group = new LogicalTagGroup(
                "Fast",
                "Fast polling",
                new Dictionary<string, string> { ["period"] = "100" });
            var tag = new LogicalTag(
                CounterTagName,
                "N7:0",
                Int32DataType,
                new()
                {
                    GroupName = "Fast",
                    Description = "Count",
                    Metadata = new Dictionary<string, string> { ["scale"] = "1" },
                    AccessMode = LogicalTagAccessMode.ReadWrite,
                    ScanInterval = TimeSpan.FromMilliseconds(FastPollMilliseconds),
                });
            await store.UpsertGroupAsync(group);
            await store.UpsertTagAsync(tag);

            var loadedGroup = await store.GetGroupAsync("Fast");
            var loaded = await store.GetTagAsync(CounterTagName);
            await Assert.That(loadedGroup!.Metadata["period"]).IsEqualTo("100");
            await Assert.That(loaded!.Metadata["scale"]).IsEqualTo("1");
            await Assert.That(loaded.ScanInterval).IsEqualTo(TimeSpan.FromMilliseconds(FastPollMilliseconds));
            await Assert.That(await store.EditTagAsync(tag.WithAddress("N7:1"))).IsTrue();
            await Assert.That((await store.GetTagAsync(CounterTagName))!.Address).IsEqualTo("N7:1");
            await Assert.That(await store.DeleteTagAsync(CounterTagName)).IsTrue();
            await Assert.That(await store.GetTagAsync(CounterTagName)).IsNull();
            await Assert.That(await store.DeleteGroupAsync("Fast")).IsTrue();
        }
        finally
        {
            DeleteTemporaryDatabase(file);
        }
    }

    /// <summary>Verifies a persisted definition can dynamically seed a catalog.</summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test.</returns>
    [Test]
    public async Task SqliteLoadsDynamicCatalogAsync()
    {
        var file = TemporaryDatabasePath();
        try
        {
            var store = new LogicalTagSqliteStore($"Data Source={file};Pooling=False");
            await store.InitializeAsync();
            await store.UpsertTagAsync(new LogicalTag("B", "B:0", "Boolean"));
            await store.UpsertTagAsync(new LogicalTag("A", "A:0", "Boolean"));
            using var catalog = await store.LoadCatalogAsync();

            await Assert.That(catalog.List().Select(static tag => tag.Name).ToArray()).IsEquivalentTo(["A", "B"]);
            await Assert.That(catalog.TryGet("B", out var tag)).IsTrue();
            await Assert.That(tag!.Address).IsEqualTo("B:0");
        }
        finally
        {
            DeleteTemporaryDatabase(file);
        }
    }

    /// <summary>Verifies contract helpers materialize enumerables and delegate to bulk operations.</summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test.</returns>
    [Test]
    public async Task ContractHelpersDelegateBulkOperationsAsync()
    {
        var timeProvider = TimeProvider.System;
        var reader = new StubReader(timeProvider);
        var writer = new StubWriter();
        var values = new[] { new LogicalTagValue("A", 1, timeProvider.GetUtcNow()) };
        var key = new LogicalTagKey<int>(new LogicalTag("A", "DB1.DBW0", Int32DataType));

        var typedRead = await reader.ReadAsync(key);
        var typedWrite = await writer.WriteAsync(key, ExpectedWriteValue);
        var reads = await reader.ReadAllAsync(TagNames);
        var writes = await writer.WriteAllAsync(values);

        await Assert.That(typedRead.Value).IsEqualTo(1);
        await Assert.That(typedWrite.Value).IsEqualTo(ExpectedWriteValue);
        await Assert.That(reader.LastNames!.Count).IsEqualTo(TagNameCount);
        await Assert.That(reads.Count).IsEqualTo(TagNameCount);
        await Assert.That(writer.LastValues!.Count).IsEqualTo(1);
        await Assert.That(writes[0].Value!.TagName).IsEqualTo("A");
    }

    /// <summary>Verifies protocol-neutral planning groups only compatible contiguous and overlapping addresses.</summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test.</returns>
    [Test]
    public async Task TransferPlannerCoalescesCompatibleRangesAndRestoresInputOrderAsync()
    {
        var planner = new TagTransferPlanner(new TagTransferCapabilities(maximumRangeLength: 8, maximumItemsPerRange: 3));
        var plan = planner.Plan(
        [
            Request("Later", offset: 4, length: 2),
            Request("First", offset: 0, length: 2),
            Request("Overlap", offset: 1, length: 3),
            Request("OtherArea", offset: 0, length: 1, memoryArea: "Inputs"),
            Request("Write", offset: 0, length: 1, access: TagTransferAccess.Write),
        ]);

        await Assert.That(plan.Ranges.Count).IsEqualTo(ExpectedTransferRangeCount);
        await Assert.That(plan.Ranges[0].Offset).IsEqualTo(0L);
        await Assert.That(plan.Ranges[0].Length).IsEqualTo(ExpectedCoalescedLength);
        await Assert.That(plan.Ranges[0].Items[0].Request.TagName).IsEqualTo("First");
        await Assert.That(plan.Ranges[0].Items[1].Request.TagName).IsEqualTo("Overlap");
        await Assert.That(plan.Ranges[0].Items[2].Request.TagName).IsEqualTo("Later");
        await Assert.That(plan.Ranges[0].InputIndices.ToArray()).IsEquivalentTo([0, 1, OverlapInputIndex]);
        await Assert.That(plan.Ranges[1].Address.Access).IsEqualTo(TagTransferAccess.Write);
        await Assert.That(plan.Ranges[2].Address.MemoryArea).IsEqualTo("Inputs");

        var ordered = plan.OrderResults(
        [
            new TagIndexedResult<string>(WriteInputIndex, "write"),
            new TagIndexedResult<string>(1, "first"),
            new TagIndexedResult<string>(OtherAreaInputIndex, "other"),
            new TagIndexedResult<string>(0, "later"),
            new TagIndexedResult<string>(OverlapInputIndex, "overlap"),
        ]);

        await Assert.That(ordered.ToArray()).IsEquivalentTo(["later", "first", "overlap", "other", "write"]);
    }

    /// <summary>Verifies capability limits prevent invalid coalescing while preserving deterministic numeric ordering.</summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test.</returns>
    [Test]
    public async Task TransferPlannerHonoursLimitsAndRejectsInvalidResultCorrelationAsync()
    {
        var planner = new TagTransferPlanner(new TagTransferCapabilities(maximumRangeLength: 4, maximumItemsPerRange: 2));
        var plan = planner.Plan(
        [
            Request("AtTen", offset: 10, length: 2),
            Request("AtZero", offset: 0, length: 2),
            Request("AtTwo", offset: 2, length: 2),
            Request("AtFour", offset: 4, length: 1),
        ]);

        await Assert.That(plan.Ranges.Count).IsEqualTo(ExpectedTransferRangeCount);
        await Assert.That(plan.Ranges[0].Offset).IsEqualTo(0L);
        await Assert.That(plan.Ranges[0].Length).IsEqualTo(ConstrainedRangeLength);
        await Assert.That(plan.Ranges[1].Offset).IsEqualTo(ConstrainedRangeLength);
        await Assert.That(plan.Ranges[2].Offset).IsEqualTo(FinalConstrainedOffset);
        _ = Assert.Throws<ArgumentException>(() => plan.OrderResults([new TagIndexedResult<int>(0, 1)]));
        _ = Assert.Throws<ArgumentException>(() => planner.Plan([Request("TooLarge", offset: 0, length: 5)]));
    }

    /// <summary>Returns a unique file-system path for a temporary SQLite database.</summary>
    /// <returns>The temporary database file path.</returns>
    private static string TemporaryDatabasePath() =>
        Path.Combine(Path.GetTempPath(), $"cp-iot-core-{Guid.NewGuid():N}.db");

    /// <summary>Creates an adapter-parsed request used by transfer planner tests.</summary>
    /// <param name="name">The logical tag name.</param>
    /// <param name="offset">The numeric memory offset.</param>
    /// <param name="length">The number of addressable units.</param>
    /// <param name="memoryArea">The opaque memory-area partition.</param>
    /// <param name="access">The transfer direction.</param>
    /// <returns>A parser-provided transfer request.</returns>
    private static TagTransferRequest Request(
        string name,
        long offset,
        int length,
        string memoryArea = "Holding",
        TagTransferAccess access = TagTransferAccess.Read) =>
        new(
            name,
            new TagTransportAddress("ControllerA", memoryArea, "UInt16", access, "Rack0", offset, length));

    /// <summary>Deletes a temporary SQLite database and any associated journal files.</summary>
    /// <param name="file">The base database file path to remove.</param>
    private static void DeleteTemporaryDatabase(string file)
    {
        foreach (var candidate in new[] { file, $"{file}-shm", $"{file}-wal" })
        {
            if (File.Exists(candidate))
            {
                File.Delete(candidate);
            }
        }
    }

    /// <summary>Minimal <see cref="ILogicalTagReader"/> stub that records the most recent batch of tag names.</summary>
    private sealed class StubReader : ILogicalTagReader
    {
        /// <summary>Time provider used to stamp returned tag values.</summary>
        private readonly TimeProvider _timeProvider;

        /// <summary>Initializes a new instance of the <see cref="StubReader"/> class.</summary>
        /// <param name="timeProvider">The time provider to use for value timestamps.</param>
        public StubReader(TimeProvider timeProvider) => _timeProvider = timeProvider;

        /// <summary>Gets the tag names received by the most recent <see cref="ReadManyAsync"/> call.</summary>
        public IReadOnlyCollection<string>? LastNames { get; private set; }

        /// <inheritdoc/>
        public Task<TagOperationResult<LogicalTagValue>> ReadAsync(
            string tagName,
            CancellationToken cancellationToken)
        {
            var value = new LogicalTagValue(tagName, tagName == "A" ? 1 : null, _timeProvider.GetUtcNow());
            return Task.FromResult(TagOperationResult<LogicalTagValue>.Success(value));
        }

        /// <inheritdoc/>
        public Task<IReadOnlyList<TagOperationResult<LogicalTagValue>>> ReadManyAsync(
            IReadOnlyCollection<string> tagNames,
            CancellationToken cancellationToken)
        {
            LastNames = tagNames;
            IReadOnlyList<TagOperationResult<LogicalTagValue>> results = tagNames
                .Select(name => TagOperationResult<LogicalTagValue>.Success(
                    new LogicalTagValue(name, null, _timeProvider.GetUtcNow())))
                .ToArray();
            return Task.FromResult(results);
        }
    }

    /// <summary>Minimal <see cref="ILogicalTagWriter"/> stub that records the most recent batch of values.</summary>
    private sealed class StubWriter : ILogicalTagWriter
    {
        /// <summary>Gets the values received by the most recent <see cref="WriteManyAsync"/> call.</summary>
        public IReadOnlyCollection<LogicalTagValue>? LastValues { get; private set; }

        /// <inheritdoc/>
        public Task<TagOperationResult<LogicalTagValue>> WriteAsync(
            LogicalTagValue value,
            CancellationToken cancellationToken) =>
            Task.FromResult(TagOperationResult<LogicalTagValue>.Success(value));

        /// <inheritdoc/>
        public Task<IReadOnlyList<TagOperationResult<LogicalTagValue>>> WriteManyAsync(
            IReadOnlyCollection<LogicalTagValue> values,
            CancellationToken cancellationToken)
        {
            LastValues = values;
            IReadOnlyList<TagOperationResult<LogicalTagValue>> results =
                values.Select(TagOperationResult<LogicalTagValue>.Success).ToArray();
            return Task.FromResult(results);
        }
    }
}
