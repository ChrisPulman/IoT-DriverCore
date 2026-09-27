// Copyright (c) 2019-2026 Chris Pulman and contributors. All rights reserved.
// Chris Pulman and contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using IoT.Driver.Core;
using Microsoft.Data.Sqlite;
using TUnit.Assertions;
using TUnit.Core;

namespace IoT.Driver.Core.Tests;

/// <summary>Exercises residual core branches that are not reached by protocol-level tests.</summary>
public sealed class CoreResidualCoverageTests
{
    /// <summary>The maximum range length used by planner ordering tests.</summary>
    private const int PlannerMaximumRangeLength = 8;

    /// <summary>The expected number of independently ordered ranges.</summary>
    private const int ExpectedPartitionRangeCount = 4;

    /// <summary>The expected number of tie-breaker ranges.</summary>
    private const int ExpectedTieBreakerRangeCount = 1;

    /// <summary>The expected number of items in the tie-breaker range.</summary>
    private const int ExpectedTieBreakerItemCount = 3;

    /// <summary>The expected number of case-sensitive catalog entries.</summary>
    private const int ExpectedCatalogCount = 2;

    /// <summary>The maximum range length used by tie-breaker tests.</summary>
    private const int TieBreakerMaximumRangeLength = 16;

    /// <summary>The shared partition used by tie-breaker scenarios.</summary>
    private const string TieBreakerPartition = "Partition";

    /// <summary>The shared route used by tie-breaker scenarios.</summary>
    private const string TieBreakerRoute = "Route";

    /// <summary>The zero-based offset used by tie-breaker scenarios.</summary>
    private const int TieBreakerOffset = 0;

    /// <summary>The longer range length used by tie-breaker scenarios.</summary>
    private const int LongRangeLength = 2;

    /// <summary>The shorter range length used by tie-breaker scenarios.</summary>
    private const int ShortRangeLength = 1;

    /// <summary>The common unsigned 16-bit transfer encoding.</summary>
    private const string UInt16Encoding = "UInt16";

    /// <summary>The shared partition used by ordering scenarios.</summary>
    private const string PartitionA = "PartitionA";

    /// <summary>The shared route used by ordering scenarios.</summary>
    private const string RouteA = "RouteA";

    /// <summary>The deterministic timestamp used by these tests.</summary>
    private static readonly DateTimeOffset StartUtc = new(2026, 7, 23, 8, 0, 0, TimeSpan.Zero);

    /// <summary>Verifies cancellation registration is disposed when an immediate delay already completed.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task ManualClockDisposesCompletedCancellationRegistrationAsync()
    {
        var clock = new ManualSimulatorClock(StartUtc);
        using var cancellation = new CancellationTokenSource();

        await clock.DelayAsync(TimeSpan.Zero, cancellation.Token);
        await cancellation.CancelAsync();
        await Assert.That(clock.PendingDelayCount).IsZero();
    }

    /// <summary>Verifies CSV import skips an empty record between valid rows.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task CsvImportSkipsBlankRecordsAsync()
    {
        const string document =
            "Name,Address,DataType,GroupName,Description,Metadata,AccessMode,ScanIntervalMilliseconds\r\n"
            + "A,D0,Int32,,,,Read,\r\n"
            + "\r\n";

        var tags = await LogicalTagCsv.ImportAsync(new StringReader(document));

        await Assert.That(tags.Count).IsEqualTo(1);
        await Assert.That(tags[0].Name).IsEqualTo("A");
    }

    /// <summary>Verifies transfer planning remains deterministic across every address partition.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task TransferPlannerOrdersDistinctAddressPartitionsAsync()
    {
        var planner = new TagTransferPlanner(new TagTransferCapabilities(PlannerMaximumRangeLength));
        var plan = planner.Plan(
        [
            Request("route", "PartitionB", "Area", UInt16Encoding, TagTransferAccess.Read, "RouteB"),
            Request("area", PartitionA, "AreaB", UInt16Encoding, TagTransferAccess.Read, RouteA),
            Request("encoding", PartitionA, "AreaA", "UInt32", TagTransferAccess.Read, RouteA),
            Request("access", PartitionA, "AreaA", UInt16Encoding, TagTransferAccess.Write, RouteA),
        ]);

        await Assert.That(plan.Ranges.Count).IsEqualTo(ExpectedPartitionRangeCount);
        await Assert.That(plan.Ranges[0].Items[0].Request.TagName).IsEqualTo("access");
        await Assert.That(plan.Ranges[1].Items[0].Request.TagName).IsEqualTo("encoding");
        await Assert.That(plan.Ranges[2].Items[0].Request.TagName).IsEqualTo("area");
        await Assert.That(plan.Ranges[3].Items[0].Request.TagName).IsEqualTo("route");
    }

    /// <summary>Verifies catalog names retain ordinal, case-sensitive identity semantics.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task CatalogUsesOrdinalNameComparisonAsync()
    {
        using var catalog = new LogicalTagCatalog();
        var upperTag = new LogicalTag("Tag", "D0", "Int32");
        var lowerTag = new LogicalTag("tag", "D1", "Int32");

        await Assert.That(catalog.TryAdd(upperTag)).IsTrue();
        await Assert.That(catalog.TryAdd(lowerTag)).IsTrue();
        await Assert.That(catalog.List().Count).IsEqualTo(ExpectedCatalogCount);
    }

    /// <summary>Verifies persisted rows with invalid enum values fail explicitly.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task SqliteRejectsInvalidPersistedAccessModeAsync()
    {
        var file = Path.Combine(Path.GetTempPath(), $"cp-iot-core-invalid-{Guid.NewGuid():N}.db");
        try
        {
            var store = new LogicalTagSqliteStore($"Data Source={file};Pooling=False");
            await store.InitializeAsync();
            await using var connection = new SqliteConnection($"Data Source={file};Pooling=False");
            await connection.OpenAsync();
            await using var command = connection.CreateCommand();
            command.CommandText = """
                INSERT INTO logical_tags
                    (name, address, data_type, group_name, description, metadata,
                     access_mode, scan_interval_ms, created_utc, modified_utc)
                VALUES ('Bad', 'D0', 'Int32', '', '', '{}', 'Invalid', NULL, $now, $now);
                """;
            var now = StartUtc.ToString("O");
            _ = command.Parameters.AddWithValue("$now", now);
            _ = await command.ExecuteNonQueryAsync();

            await Assert.That(await ThrowsAsync<FormatException>(
                () => store.GetTagAsync("Bad"))).IsTrue();
        }
        finally
        {
            DeleteDatabase(file);
        }
    }

    /// <summary>Verifies SQLite connection failures dispose the failed connection before rethrowing.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task SqliteInitializationRethrowsOpenFailureAsync()
    {
        var store = new LogicalTagSqliteStore(
            $"Data Source={Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"))};Mode=ReadOnly");

        await Assert.That(await ThrowsAsync<SqliteException>(
            () => store.InitializeAsync())).IsTrue();
    }

    /// <summary>Verifies metadata serialization emits separators for multiple entries.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task CsvRoundTripPreservesMultipleMetadataEntriesAsync()
    {
        var source = new LogicalTag(
            "Metadata",
            "D0",
            "String",
            new LogicalTagOptions
            {
                Metadata = new Dictionary<string, string>
                {
                    ["first"] = "one",
                    ["second"] = "two",
                },
            });
        await using var writer = new StringWriter();

        await LogicalTagCsv.ExportAsync([source], writer);
        var imported = await LogicalTagCsv.ImportAsync(new StringReader(writer.ToString()));

        await Assert.That(imported[0].Metadata["first"]).IsEqualTo("one");
        await Assert.That(imported[0].Metadata["second"]).IsEqualTo("two");
    }

    /// <summary>Verifies planner ordering compares range length and then input position.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task TransferPlannerOrdersEqualCoordinatesByLengthAndInputAsync()
    {
        var planner = new TagTransferPlanner(new TagTransferCapabilities(TieBreakerMaximumRangeLength));
        var plan = planner.Plan(
        [
            new(
                "long",
                new(
                    TieBreakerPartition,
                    "Area",
                    UInt16Encoding,
                    TagTransferAccess.Read,
                    TieBreakerRoute,
                    TieBreakerOffset,
                    LongRangeLength)),
            new(
                "short",
                new(
                    TieBreakerPartition,
                    "Area",
                    UInt16Encoding,
                    TagTransferAccess.Read,
                    TieBreakerRoute,
                    TieBreakerOffset,
                    ShortRangeLength)),
            new(
                "same",
                new(
                    TieBreakerPartition,
                    "Area",
                    UInt16Encoding,
                    TagTransferAccess.Read,
                    TieBreakerRoute,
                    TieBreakerOffset,
                    ShortRangeLength)),
        ]);

        await Assert.That(plan.Ranges.Count).IsEqualTo(ExpectedTieBreakerRangeCount);
        await Assert.That(plan.Ranges[0].Items.Count).IsEqualTo(ExpectedTieBreakerItemCount);
        await Assert.That(plan.Ranges[0].Items[0].Request.TagName).IsEqualTo("short");
        await Assert.That(plan.Ranges[0].Items[1].Request.TagName).IsEqualTo("same");
        await Assert.That(plan.Ranges[0].Items[2].Request.TagName).IsEqualTo("long");
    }

    /// <summary>Creates a transfer request with a distinct planning coordinate.</summary>
    /// <param name="name">The request name.</param>
    /// <param name="partition">The transport partition.</param>
    /// <param name="memoryArea">The memory area.</param>
    /// <param name="encoding">The value encoding.</param>
    /// <param name="access">The transfer access.</param>
    /// <param name="route">The transport route.</param>
    /// <returns>The constructed request.</returns>
    private static TagTransferRequest Request(
        string name,
        string partition,
        string memoryArea,
        string encoding,
        TagTransferAccess access,
        string route) =>
        new(
            name,
            new TagTransportAddress(partition, memoryArea, encoding, access, route, 0, 1));

    /// <summary>Deletes a SQLite database and its sidecar files.</summary>
    /// <param name="file">The database path.</param>
    private static void DeleteDatabase(string file)
    {
        foreach (var candidate in new[] { file, $"{file}-shm", $"{file}-wal" })
        {
            if (File.Exists(candidate))
            {
                File.Delete(candidate);
            }
        }
    }

    /// <summary>Returns whether an asynchronous operation throws the expected exception.</summary>
    /// <typeparam name="TException">The expected exception type.</typeparam>
    /// <param name="action">The asynchronous operation.</param>
    /// <returns><see langword="true"/> when the expected exception is thrown.</returns>
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
