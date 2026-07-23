// Copyright (c) 2019-2026 Chris Pulman and contributors. All rights reserved.
// Chris Pulman and contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Reflection;
using IoT.DriverCore.Core;
using Microsoft.Data.Sqlite;
using TUnit.Assertions;
using TUnit.Core;

namespace IoT.DriverCore.Core.Tests;

/// <summary>Exercises validation, failure, compatibility, and migration paths in the shared core.</summary>
public sealed class LogicalTagCoreCoverageTests
{
    /// <summary>The common logical tag data type.</summary>
    private const string Int32DataType = "Int32";

    /// <summary>The name used for absent entities.</summary>
    private const string MissingName = "missing";

    /// <summary>The transport route used for value equality.</summary>
    private const string RouteName = "Route";

    /// <summary>The simulated transport error.</summary>
    private const string OfflineError = "offline";

    /// <summary>A reusable value of two.</summary>
    private const int Two = 2;

    /// <summary>A reusable value of three.</summary>
    private const int Three = 3;

    /// <summary>A reusable value of four.</summary>
    private const int Four = 4;

    /// <summary>A reusable value of six.</summary>
    private const int Six = 6;

    /// <summary>A reusable value of eight.</summary>
    private const int Eight = 8;

    /// <summary>A reusable value of ten.</summary>
    private const int Ten = 10;

    /// <summary>The exclusive end of the sample transport address.</summary>
    private const long ExpectedEndOffset = 5;

    /// <summary>Verifies transport addresses reject invalid coordinates and implement value equality.</summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test.</returns>
    [Test]
    public async Task TransportAddressValidatesAndImplementsValueSemanticsAsync()
    {
        _ = Assert.Throws<ArgumentException>(() => CreateAddress(partition: " "));
        _ = Assert.Throws<ArgumentException>(() => CreateAddress(memoryArea: " "));
        _ = Assert.Throws<ArgumentException>(() => CreateAddress(encoding: " "));
        _ = Assert.Throws<ArgumentOutOfRangeException>(() => CreateAddress(access: (TagTransferAccess)99));
        _ = Assert.Throws<ArgumentNullException>(() => CreateAddress(route: null!));
        _ = Assert.Throws<ArgumentOutOfRangeException>(() => CreateAddress(offset: -1));
        _ = Assert.Throws<ArgumentOutOfRangeException>(() => CreateAddress(length: 0));
        _ = Assert.Throws<OverflowException>(() => CreateAddress(offset: long.MaxValue));

        var address = CreateAddress(route: $" {RouteName} ", offset: Two, length: Three);
        var equal = CreateAddress(route: RouteName, offset: Two, length: Three);
        var addressHash = address.GetHashCode();
        var equalHash = equal.GetHashCode();
        await Assert.That(address.Route).IsEqualTo(RouteName);
        await Assert.That(address.EndOffset).IsEqualTo(ExpectedEndOffset);
        await Assert.That(address.Equals(equal)).IsTrue();
        await Assert.That(address.Equals((object)equal)).IsTrue();
        await Assert.That(address.Equals(null)).IsFalse();
        await Assert.That(object.Equals(address, RouteName)).IsFalse();
        await Assert.That(addressHash).IsEqualTo(equalHash);
    }

    /// <summary>Verifies planners and plans reject malformed requests and correlations.</summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test.</returns>
    [Test]
    public async Task PlannerAndPlanValidateAllInputsAsync()
    {
        _ = Assert.Throws<ArgumentOutOfRangeException>(() => _ = new TagTransferCapabilities(0));
        _ = Assert.Throws<ArgumentOutOfRangeException>(() => _ = new TagTransferCapabilities(1, 0));
        _ = Assert.Throws<ArgumentNullException>(() => _ = new TagTransferPlanner(null!));
        var planner = new TagTransferPlanner(new TagTransferCapabilities(Four));
        _ = Assert.Throws<ArgumentNullException>(() => planner.Plan(null!));
        _ = Assert.Throws<ArgumentException>(() => planner.Plan([null!]));

        var empty = planner.Plan([]);
        await Assert.That(empty.InputCount).IsEqualTo(0);
        await Assert.That(empty.Ranges.Count).IsEqualTo(0);
        _ = Assert.Throws<ArgumentNullException>(() => empty.OrderResults<int>(null!));

        var plan = planner.Plan([new TagTransferRequest("A", CreateAddress())]);
        _ = Assert.Throws<ArgumentException>(() => plan.OrderResults<int>([null!]));
        _ = Assert.Throws<ArgumentOutOfRangeException>(() => plan.OrderResults([new TagIndexedResult<int>(1, 1)]));
        _ = Assert.Throws<ArgumentException>(() => plan.OrderResults(
        [
            new TagIndexedResult<int>(0, 1),
            new TagIndexedResult<int>(0, Two),
        ]));
    }

    /// <summary>Verifies CSV overloads and all parser error paths.</summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test.</returns>
    [Test]
    public async Task CsvValidatesArgumentsAndMalformedDocumentsAsync()
    {
        using var writer = new StringWriter();
        await Assert.That(await ThrowsAsync<ArgumentNullException>(() => LogicalTagCsv.ExportAsync(null!, writer))).IsTrue();
        await Assert.That(await ThrowsAsync<ArgumentNullException>(() => LogicalTagCsv.ExportAsync([], null!))).IsTrue();
        await Assert.That(await ThrowsAsync<ArgumentOutOfRangeException>(
            () => LogicalTagCsv.ExportAsync([], writer, '"'))).IsTrue();
        await Assert.That(await ThrowsAsync<ArgumentException>(() => LogicalTagCsv.ExportAsync([null!], writer))).IsTrue();
        await Assert.That(await ThrowsAsync<ArgumentNullException>(() => LogicalTagCsv.ImportAsync(null!))).IsTrue();

        var empty = await LogicalTagCsv.ImportAsync(new StringReader(string.Empty));
        await Assert.That(empty.Count).IsEqualTo(0);
        await Assert.That(await ImportFailsAsync("Wrong\r\n")).IsTrue();
        await Assert.That(await ImportFailsAsync(Csv("A,B"))).IsTrue();
        await Assert.That(await ImportFailsAsync(Csv($"A,A,{Int32DataType},,,,Invalid,"))).IsTrue();
        await Assert.That(await ImportFailsAsync(Csv($"A,A,{Int32DataType},,,,ReadWrite,0"))).IsTrue();
        await Assert.That(await ImportFailsAsync("\"unterminated")).IsTrue();
        await Assert.That(await ImportFailsAsync("a\"b")).IsTrue();
        await Assert.That(await ImportFailsAsync("\"a\"b")).IsTrue();

        using var customWriter = new StringWriter();
        await LogicalTagCsv.ExportAsync([new LogicalTag("A", "D0", Int32DataType)], customWriter, ';');
        var imported = await LogicalTagCsv.ImportAsync(new StringReader(customWriter.ToString()), ';');
        await Assert.That(imported[0].ScanInterval).IsNull();
    }

    /// <summary>Verifies model, key, and catalog edge cases.</summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test.</returns>
    [Test]
    public async Task ModelsAndCatalogValidateLifecycleEdgesAsync()
    {
        _ = Assert.Throws<ArgumentNullException>(() => _ = new LogicalTag("A", "A", Int32DataType, null!));
        _ = Assert.Throws<ArgumentOutOfRangeException>(() => _ = new LogicalTag(
            "A",
            "A",
            Int32DataType,
            new() { ScanInterval = TimeSpan.Zero }));
        _ = Assert.Throws<ArgumentException>(() => _ = new LogicalTag(
            "A",
            "A",
            Int32DataType,
            new() { Metadata = new Dictionary<string, string> { [" "] = "value" } }));

        var tag = new LogicalTag(
            " A ",
            " D0 ",
            $" {Int32DataType} ",
            new() { Metadata = new Dictionary<string, string> { ["unit"] = null! } });
        await Assert.That(tag.WithDataType(" Double ").DataType).IsEqualTo("Double");
        await Assert.That(tag.Metadata["unit"]).IsEqualTo(string.Empty);
        await Assert.That(new LogicalTagGroup("A").Description).IsEqualTo(string.Empty);
        await Assert.That(new LogicalTagGroup("A", " Description ").Description).IsEqualTo("Description");
        await Assert.That(new LogicalTagKey<int>(" A ").Name).IsEqualTo("A");
        await Assert.That(LogicalTagKey<int>.ValueType).IsEqualTo(typeof(int));
        _ = Assert.Throws<ArgumentNullException>(() => _ = new LogicalTagKey<int>((LogicalTag)null!));

        var catalog = new LogicalTagCatalog();
        _ = Assert.Throws<ArgumentNullException>(() => catalog.TryAdd(null!));
        _ = Assert.Throws<ArgumentNullException>(() => catalog.Upsert(null!));
        await Assert.That(catalog.TryGet(MissingName, out _)).IsFalse();
        await Assert.That(catalog.TryRemove(MissingName, out _)).IsFalse();
        catalog.Dispose();
        catalog.Dispose();
        _ = Assert.Throws<ObjectDisposedException>(() => catalog.List());
    }

    /// <summary>Verifies typed helper failures and enumerable validation.</summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test.</returns>
    [Test]
    public async Task TypedMixinsPreserveFailuresAndValidateCollectionsAsync()
    {
        var key = new LogicalTagKey<int>("A");
        var now = TimeProvider.System.GetUtcNow();
        var failingReader = new ConfigurableReader(TagOperationResult<LogicalTagValue>.Failure(OfflineError));
        var wrongReader = new ConfigurableReader(TagOperationResult<LogicalTagValue>.Success(
            new LogicalTagValue("A", "wrong", now)));
        var nullReader = new ConfigurableReader(TagOperationResult<LogicalTagValue>.Success(
            new LogicalTagValue("A", null, now)));
        var failingWriter = new ConfigurableWriter(false);

        await Assert.That((await failingReader.ReadAsync(key)).Error).IsEqualTo(OfflineError);
        await Assert.That((await wrongReader.ReadAsync(key)).Succeeded).IsFalse();
        await Assert.That((await nullReader.ReadAsync(new LogicalTagKey<string>("A"))).Succeeded).IsTrue();
        await Assert.That((await failingWriter.WriteAsync(key, 1)).Error).IsEqualTo(OfflineError);
        await Assert.That(await ThrowsAsync<ArgumentNullException>(() => failingReader.ReadAsync<int>(null!))).IsTrue();
        await Assert.That(await ThrowsAsync<ArgumentNullException>(() => failingWriter.WriteAsync(null!, 1))).IsTrue();
        await Assert.That(await ThrowsAsync<ArgumentNullException>(() => failingReader.ReadAllAsync(null!))).IsTrue();
        await Assert.That(await ThrowsAsync<ArgumentException>(() => failingReader.ReadAllAsync([" "]))).IsTrue();
        await Assert.That(await ThrowsAsync<ArgumentNullException>(() => failingWriter.WriteAllAsync(null!))).IsTrue();
        await Assert.That(await ThrowsAsync<ArgumentException>(() => failingWriter.WriteAllAsync([null!]))).IsTrue();
    }

    /// <summary>Verifies SQLite list, update, missing-row, and legacy-schema migration behavior.</summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test.</returns>
    [Test]
    public async Task SqliteCoversListsMissingRowsAndLegacyMigrationsAsync()
    {
        var file = Path.Combine(Path.GetTempPath(), $"cp-iot-core-legacy-{Guid.NewGuid():N}.db");
        try
        {
            await CreateLegacyDatabaseAsync(file);
            var store = new LogicalTagSqliteStore($"Data Source={file};Pooling=False");
            await store.InitializeAsync();
            await store.InitializeAsync();
            await store.UpsertGroupAsync(new LogicalTagGroup("B"));
            await store.UpsertGroupAsync(new LogicalTagGroup("A"));
            await store.UpsertTagAsync(new LogicalTag("B", "D1", Int32DataType));
            await store.UpsertTagAsync(new LogicalTag("A", "D0", Int32DataType));

            await Assert.That((await store.ListGroupsAsync()).Select(static group => group.Name).ToArray())
                .IsEquivalentTo(["A", "B"]);
            await Assert.That((await store.ListTagsAsync()).Select(static listedTag => listedTag.Name).ToArray())
                .IsEquivalentTo(["A", "B"]);
            await Assert.That(await store.UpdateTagAsync(new LogicalTag("A", "D2", Int32DataType))).IsTrue();
            await Assert.That(await store.EditTagAsync(new LogicalTag(MissingName, "D9", Int32DataType))).IsFalse();
            await Assert.That(await store.GetGroupAsync(MissingName)).IsNull();
            await Assert.That(await store.DeleteTagAsync(MissingName)).IsFalse();
            await Assert.That(await store.DeleteGroupAsync(MissingName)).IsFalse();
            await Assert.That(await ThrowsAsync<ArgumentNullException>(() => store.UpsertTagAsync(null!))).IsTrue();
            await Assert.That(await ThrowsAsync<ArgumentNullException>(() => store.EditTagAsync(null!))).IsTrue();
            await Assert.That(await ThrowsAsync<ArgumentNullException>(() => store.UpsertGroupAsync(null!))).IsTrue();
        }
        finally
        {
            DeleteDatabase(file);
        }
    }

    /// <summary>Executes the internal netstandard index/range compatibility types through reflection.</summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test.</returns>
    [Test]
    public async Task NetStandardIndexAndRangePolyfillsBehaveLikeRuntimeTypesAsync()
    {
        var assembly = typeof(LogicalTag).Assembly;
        var indexType = assembly.GetType("System.Index", throwOnError: true)!;
        var rangeType = assembly.GetType("System.Range", throwOnError: true)!;
        var zero = Activator.CreateInstance(indexType, [0])!;
        var two = Activator.CreateInstance(indexType, [Two])!;
        var fromEnd = Activator.CreateInstance(indexType, [Two, true])!;
        _ = Assert.Throws<TargetInvocationException>(() => Activator.CreateInstance(indexType, [-1]));
        _ = Assert.Throws<TargetInvocationException>(() => Activator.CreateInstance(indexType, [-1, true]));

        await Assert.That(fromEnd.ToString()).IsEqualTo("^2");
        await Assert.That(two.Equals(Activator.CreateInstance(indexType, [Two]))).IsTrue();
        await Assert.That(two.Equals("2")).IsFalse();
        await Assert.That(two.GetHashCode()).IsEqualTo(Two);
        await Assert.That(Invoke(indexType, fromEnd, "GetOffset", Ten)).IsEqualTo(Eight);
        await Assert.That(Invoke(indexType, null, "op_Implicit", Three)!.ToString()).IsEqualTo(Three.ToString());

        var range = Activator.CreateInstance(rangeType, [two, fromEnd])!;
        var equalRange = Activator.CreateInstance(rangeType, [two, fromEnd])!;
        await Assert.That(range.ToString()).IsEqualTo("2..^2");
        await Assert.That(range.Equals(equalRange)).IsTrue();
        await Assert.That(range.Equals("range")).IsFalse();
        await Assert.That(range.GetHashCode()).IsEqualTo(equalRange.GetHashCode());
        var offsetAndLength = Invoke(rangeType, range, "GetOffsetAndLength", Ten)!;
        await Assert.That(ReadTupleValue(offsetAndLength, "Item1")).IsEqualTo(Two);
        await Assert.That(ReadTupleValue(offsetAndLength, "Item2")).IsEqualTo(Six);
        _ = Assert.Throws<TargetInvocationException>(() => Invoke(rangeType, range, "GetOffsetAndLength", 1));
        await Assert.That(Invoke(rangeType, null, "EndAt", fromEnd)!.ToString()).IsEqualTo("0..^2");
        await Assert.That(Invoke(rangeType, null, "StartAt", zero)!.ToString()).IsEqualTo("0..^0");
        await Assert.That(GetStaticProperty(rangeType, "All")!.ToString()).IsEqualTo("0..^0");
    }

    /// <summary>Creates a valid transport address with selected overrides.</summary>
    /// <param name="partition">The transport partition.</param>
    /// <param name="memoryArea">The memory area.</param>
    /// <param name="encoding">The transport encoding.</param>
    /// <param name="access">The transfer access.</param>
    /// <param name="route">The transport route.</param>
    /// <param name="offset">The address offset.</param>
    /// <param name="length">The address length.</param>
    /// <returns>The constructed transport address.</returns>
    private static TagTransportAddress CreateAddress(
        string partition = "Controller",
        string memoryArea = "Holding",
        string encoding = "UInt16",
        TagTransferAccess access = TagTransferAccess.Read,
        string route = "",
        long offset = 0,
        long length = 1) =>
        new(partition, memoryArea, encoding, access, route, offset, length);

    /// <summary>Creates a complete CSV document from one data row.</summary>
    /// <param name="row">The data row.</param>
    /// <returns>The complete CSV document.</returns>
    private static string Csv(string row) =>
        $"Name,Address,DataType,GroupName,Description,Metadata,AccessMode,ScanIntervalMilliseconds\r\n{row}\r\n";

    /// <summary>Returns whether importing malformed CSV throws a format exception.</summary>
    /// <param name="text">The CSV document to import.</param>
    /// <returns><see langword="true"/> when a format exception is thrown.</returns>
    private static async Task<bool> ImportFailsAsync(string text)
    {
        try
        {
            _ = await LogicalTagCsv.ImportAsync(new StringReader(text));
            return false;
        }
        catch (FormatException)
        {
            return true;
        }
    }

    /// <summary>Returns whether an asynchronous operation throws the expected exception type.</summary>
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

    /// <summary>Creates the pre-migration schema without access-mode and scan-interval columns.</summary>
    /// <param name="file">The database file path.</param>
    /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
    private static async Task CreateLegacyDatabaseAsync(string file)
    {
        await using var connection = new SqliteConnection($"Data Source={file};Pooling=False");
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = """
            CREATE TABLE logical_tag_groups (
                name TEXT NOT NULL PRIMARY KEY, description TEXT NOT NULL, metadata TEXT NOT NULL,
                created_utc TEXT NOT NULL, modified_utc TEXT NOT NULL);
            CREATE TABLE logical_tags (
                name TEXT NOT NULL PRIMARY KEY, address TEXT NOT NULL, data_type TEXT NOT NULL,
                group_name TEXT NOT NULL, description TEXT NOT NULL, metadata TEXT NOT NULL,
                created_utc TEXT NOT NULL, modified_utc TEXT NOT NULL);
            """;
        _ = await command.ExecuteNonQueryAsync();
    }

    /// <summary>Deletes a SQLite database and its journal files.</summary>
    /// <param name="file">The database file path.</param>
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

    /// <summary>Invokes an internal compatibility member.</summary>
    /// <param name="type">The reflected type.</param>
    /// <param name="instance">The instance for an instance member, or <see langword="null"/> for a static member.</param>
    /// <param name="name">The member name.</param>
    /// <param name="arguments">The invocation arguments.</param>
    /// <returns>The invocation result.</returns>
    private static object? Invoke(Type type, object? instance, string name, params object[] arguments) =>
        type.GetMethod(name, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance)!
            .Invoke(instance, arguments);

    /// <summary>Gets an internal static compatibility property.</summary>
    /// <param name="type">The reflected type.</param>
    /// <param name="name">The property name.</param>
    /// <returns>The property value.</returns>
    private static object? GetStaticProperty(Type type, string name) =>
        type.GetProperty(name, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static)!.GetValue(null);

    /// <summary>Reads a named field from a reflected value tuple.</summary>
    /// <param name="tuple">The reflected tuple.</param>
    /// <param name="fieldName">The tuple field name.</param>
    /// <returns>The integer field value.</returns>
    private static int ReadTupleValue(object tuple, string fieldName) =>
        (int)tuple.GetType().GetField(fieldName)!.GetValue(tuple)!;

    /// <summary>Configurable reader used to exercise typed conversion failures.</summary>
    /// <param name="result">The result returned for every read.</param>
    private sealed class ConfigurableReader(TagOperationResult<LogicalTagValue> result) : ILogicalTagReader
    {
        /// <inheritdoc/>
        public Task<TagOperationResult<LogicalTagValue>> ReadAsync(string tagName, CancellationToken cancellationToken) =>
            Task.FromResult(result);

        /// <inheritdoc/>
        public Task<IReadOnlyList<TagOperationResult<LogicalTagValue>>> ReadManyAsync(
            IReadOnlyCollection<string> tagNames,
            CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<TagOperationResult<LogicalTagValue>>>([]);
    }

    /// <summary>Configurable writer used to exercise typed write failures.</summary>
    /// <param name="succeeds">Whether writes succeed.</param>
    private sealed class ConfigurableWriter(bool succeeds) : ILogicalTagWriter
    {
        /// <inheritdoc/>
        public Task<TagOperationResult<LogicalTagValue>> WriteAsync(
            LogicalTagValue value,
            CancellationToken cancellationToken) =>
            Task.FromResult(succeeds
                ? TagOperationResult<LogicalTagValue>.Success(value)
                : TagOperationResult<LogicalTagValue>.Failure(OfflineError));

        /// <inheritdoc/>
        public Task<IReadOnlyList<TagOperationResult<LogicalTagValue>>> WriteManyAsync(
            IReadOnlyCollection<LogicalTagValue> values,
            CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<TagOperationResult<LogicalTagValue>>>([]);
    }
}
