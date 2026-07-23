// Copyright (c) 2019-2026 Chris Pulman and contributors. All rights reserved.
// Chris Pulman and contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using IoT.DriverCore.Core;
using IoT.DriverCore.S7PlcRx.Enums;
using IoT.DriverCore.S7PlcRx.LogicalTags;

namespace IoT.DriverCore.S7PlcRx.Tests.LogicalTags;

/// <summary>Exercises the concrete logical-tag batch paths through their internal operation boundary.</summary>
public sealed class S7LogicalBatchSeamTests
{
    /// <summary>Defines the first readable and writable tag name.</summary>
    private const string FirstTagName = "First";

    /// <summary>Defines the second readable and writable tag name.</summary>
    private const string SecondTagName = "Second";

    /// <summary>Defines the tag name added after client construction.</summary>
    private const string DynamicTagName = "Dynamic";

    /// <summary>Defines a tag name absent from the catalog.</summary>
    private const string MissingTagName = "Missing";

    /// <summary>Defines the read-only tag name.</summary>
    private const string ReadOnlyTagName = "ReadOnly";

    /// <summary>Defines the write-only tag name.</summary>
    private const string WriteOnlyTagName = "WriteOnly";

    /// <summary>Defines the scripted batch-read error message.</summary>
    private const string ReadFailureMessage = "Scripted batch read failure.";

    /// <summary>Defines the disconnected PLC endpoint.</summary>
    private const string LoopbackAddress = "127.0.0.1";

    /// <summary>Defines the first scripted read value.</summary>
    private const ushort FirstReadValue = 11;

    /// <summary>Defines the second scripted read value.</summary>
    private const ushort SecondReadValue = 22;

    /// <summary>Defines the value returned for a dynamically added tag.</summary>
    private const ushort DynamicReadValue = 77;

    /// <summary>Defines the first successful write value.</summary>
    private const ushort FirstWriteValue = 101;

    /// <summary>Defines the second successful write value.</summary>
    private const ushort SecondWriteValue = 202;

    /// <summary>Defines the rejected read-only tag write value.</summary>
    private const ushort ReadOnlyWriteValue = 303;

    /// <summary>Defines the first failed batch write value.</summary>
    private const ushort FirstFailedWriteValue = 404;

    /// <summary>Defines the second failed batch write value.</summary>
    private const ushort SecondFailedWriteValue = 505;

    /// <summary>Defines the value associated with a missing tag.</summary>
    private const ushort MissingWriteValue = 999;

    /// <summary>Defines the number of transport-eligible items in the ordered batches.</summary>
    private const int BatchItemCount = 2;

    /// <summary>Defines the number of requested results in the mixed read batch.</summary>
    private const int MixedReadResultCount = 4;

    /// <summary>Defines the test PLC rack number.</summary>
    private const short RackNumber = 0;

    /// <summary>Defines the test PLC slot number.</summary>
    private const short SlotNumber = 1;

    /// <summary>Stores the timestamp returned by the fixed test clock.</summary>
    private static readonly DateTimeOffset FixedTimestamp =
        new(2026, 7, 23, 12, 34, 56, TimeSpan.Zero);

    /// <summary>Verifies one grouped read preserves request order, validation failures, and partial-value fallback.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task ReadManyUsesOneOrderedBatchAndPreservesPartialResultsAsync()
    {
        using var plc = CreatePlc();
        using var catalog = CreateCatalog();
        var operations = new ScriptedBatchOperations
        {
            ReadValues = new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                [FirstTagName] = FirstReadValue,
            },
        };
        using var client = CreateClient(plc, catalog, operations);
        plc.TagList[SecondTagName]!.Value = SecondReadValue;

        var results = await client.ReadManyAsync(
            [SecondTagName, MissingTagName, FirstTagName, WriteOnlyTagName],
            CancellationToken.None);

        await TUnit.Assertions.Assert.That(operations.ReadCallCount).IsEqualTo(1);
        await TUnit.Assertions.Assert.That(operations.ReadTagNames.Length).IsEqualTo(BatchItemCount);
        await TUnit.Assertions.Assert.That(operations.ReadTagNames[0]).IsEqualTo(SecondTagName);
        await TUnit.Assertions.Assert.That(operations.ReadTagNames[1]).IsEqualTo(FirstTagName);
        await TUnit.Assertions.Assert.That(results.Count).IsEqualTo(MixedReadResultCount);
        await TUnit.Assertions.Assert.That(results[0].Value?.Value).IsEqualTo(SecondReadValue);
        await TUnit.Assertions.Assert.That(results[1].Succeeded).IsFalse();
        await TUnit.Assertions.Assert.That(results[2].Value?.Value).IsEqualTo(FirstReadValue);
        await TUnit.Assertions.Assert.That(results[3].Succeeded).IsFalse();
        await TUnit.Assertions.Assert.That(results[0].Value?.TimestampUtc).IsEqualTo(FixedTimestamp);
    }

    /// <summary>Verifies a batch read exception becomes ordered per-tag failures without replacing validation failures.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task ReadManyConvertsBatchFailureForOnlyPendingTagsAsync()
    {
        using var plc = CreatePlc();
        using var catalog = CreateCatalog();
        var operations = new ScriptedBatchOperations
        {
            ReadException = new InvalidOperationException(ReadFailureMessage),
        };
        using var client = CreateClient(plc, catalog, operations);

        var results = await client.ReadManyAsync(
            [FirstTagName, MissingTagName, SecondTagName],
            CancellationToken.None);

        await TUnit.Assertions.Assert.That(operations.ReadCallCount).IsEqualTo(1);
        await TUnit.Assertions.Assert.That(results[0].Succeeded).IsFalse();
        await TUnit.Assertions.Assert.That(results[0].Error).IsEqualTo(ReadFailureMessage);
        await TUnit.Assertions.Assert.That(results[1].Succeeded).IsFalse();
        await TUnit.Assertions.Assert.That(results[1].Error)
            .Contains(MissingTagName);
        await TUnit.Assertions.Assert.That(results[2].Succeeded).IsFalse();
        await TUnit.Assertions.Assert.That(results[2].Error).IsEqualTo(ReadFailureMessage);
    }

    /// <summary>Verifies grouped writes retain request order and report both validation and transport failures.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task WriteManyUsesOneOrderedBatchAndReportsFalseResultAsync()
    {
        using var plc = CreatePlc();
        using var catalog = CreateCatalog();
        var operations = new ScriptedBatchOperations();
        using var client = CreateClient(plc, catalog, operations);
        var values = new[]
        {
            CreateValue(SecondTagName, SecondWriteValue),
            CreateValue(MissingTagName, MissingWriteValue),
            CreateValue(FirstTagName, FirstWriteValue),
            CreateValue(ReadOnlyTagName, ReadOnlyWriteValue),
        };

        var succeeded = await client.WriteManyAsync(values, CancellationToken.None);

        await TUnit.Assertions.Assert.That(operations.WriteCallCount).IsEqualTo(1);
        await TUnit.Assertions.Assert.That(operations.WriteTagNames.Length).IsEqualTo(BatchItemCount);
        await TUnit.Assertions.Assert.That(operations.WriteTagNames[0]).IsEqualTo(SecondTagName);
        await TUnit.Assertions.Assert.That(operations.WriteTagNames[1]).IsEqualTo(FirstTagName);
        await TUnit.Assertions.Assert.That(operations.WrittenValues[0]).IsEqualTo(SecondWriteValue);
        await TUnit.Assertions.Assert.That(operations.WrittenValues[1]).IsEqualTo(FirstWriteValue);
        await TUnit.Assertions.Assert.That(succeeded[0].Value?.Value).IsEqualTo(SecondWriteValue);
        await TUnit.Assertions.Assert.That(succeeded[1].Succeeded).IsFalse();
        await TUnit.Assertions.Assert.That(succeeded[2].Value?.Value).IsEqualTo(FirstWriteValue);
        await TUnit.Assertions.Assert.That(succeeded[3].Succeeded).IsFalse();

        operations.WriteSucceeds = false;
        var failed = await client.WriteManyAsync(
            [
                CreateValue(FirstTagName, FirstFailedWriteValue),
                CreateValue(SecondTagName, SecondFailedWriteValue),
            ],
            CancellationToken.None);

        await TUnit.Assertions.Assert.That(operations.WriteCallCount).IsEqualTo(BatchItemCount);
        await TUnit.Assertions.Assert.That(failed[0].Succeeded).IsFalse();
        await TUnit.Assertions.Assert.That(failed[0].Error).Contains(FirstTagName);
        await TUnit.Assertions.Assert.That(failed[1].Succeeded).IsFalse();
        await TUnit.Assertions.Assert.That(failed[1].Error).Contains(SecondTagName);
    }

    /// <summary>Verifies cancellation and disposal stop the batch adapter before any transport operation.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task CancellationAndDisposalDoNotInvokeBatchOperationsAsync()
    {
        using var plc = CreatePlc();
        using var catalog = CreateCatalog();
        var operations = new ScriptedBatchOperations();
        var client = CreateClient(plc, catalog, operations);
        using var cancellation = new CancellationTokenSource();
        await AsyncCompatibility.CancelAsync(cancellation);
        Func<Task> read = async () =>
            _ = await client.ReadManyAsync([FirstTagName], cancellation.Token);
        Func<Task> write = async () =>
            _ = await client.WriteManyAsync([CreateValue(FirstTagName, 1)], cancellation.Token);

        await TUnit.Assertions.Assert.That(read).Throws<OperationCanceledException>();
        await TUnit.Assertions.Assert.That(write).Throws<OperationCanceledException>();
        await TUnit.Assertions.Assert.That(operations.ReadCallCount).IsEqualTo(0);
        await TUnit.Assertions.Assert.That(operations.WriteCallCount).IsEqualTo(0);

        client.Dispose();
        Func<Task> disposedRead = async () =>
            _ = await client.ReadManyAsync([FirstTagName], CancellationToken.None);
        Func<Task> disposedWrite = async () =>
            _ = await client.WriteManyAsync(
                [CreateValue(FirstTagName, 1)],
                CancellationToken.None);
        await TUnit.Assertions.Assert.That(disposedRead).Throws<ObjectDisposedException>();
        await TUnit.Assertions.Assert.That(disposedWrite).Throws<ObjectDisposedException>();
        await TUnit.Assertions.Assert.That(operations.ReadCallCount).IsEqualTo(0);
        await TUnit.Assertions.Assert.That(operations.WriteCallCount).IsEqualTo(0);
    }

    /// <summary>Verifies live catalog additions and removals determine the exact runtime batch.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task CatalogChangesUpdateTheBatchRegistrationSetAsync()
    {
        using var plc = CreatePlc();
        using var catalog = CreateCatalog();
        var operations = new ScriptedBatchOperations
        {
            ReadValues = new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                [DynamicTagName] = DynamicReadValue,
            },
        };
        using var client = CreateClient(plc, catalog, operations);
        catalog.Upsert(new LogicalTag(DynamicTagName, "DB2.DBW0", "WORD"));

        var added = await client.ReadManyAsync(
            [DynamicTagName, FirstTagName],
            CancellationToken.None);
        _ = catalog.TryRemove(DynamicTagName, out _);
        var removed = await client.ReadManyAsync([DynamicTagName], CancellationToken.None);

        await TUnit.Assertions.Assert.That(added[0].Value?.Value).IsEqualTo(DynamicReadValue);
        await TUnit.Assertions.Assert.That(operations.ReadTagNames.Length).IsEqualTo(BatchItemCount);
        await TUnit.Assertions.Assert.That(operations.ReadTagNames[0]).IsEqualTo(DynamicTagName);
        await TUnit.Assertions.Assert.That(operations.ReadTagNames[1]).IsEqualTo(FirstTagName);
        await TUnit.Assertions.Assert.That(plc.TagList[DynamicTagName]).IsNull();
        await TUnit.Assertions.Assert.That(removed[0].Succeeded).IsFalse();
        await TUnit.Assertions.Assert.That(operations.ReadCallCount).IsEqualTo(1);
    }

    /// <summary>Creates a logical client wired to the scripted batch adapter.</summary>
    /// <param name="plc">The in-memory S7 connection.</param>
    /// <param name="catalog">The logical-tag catalog.</param>
    /// <param name="operations">The scripted batch operations.</param>
    /// <returns>The configured logical-tag client.</returns>
    private static S7LogicalTagClient CreateClient(
        IRxS7 plc,
        ILogicalTagCatalog catalog,
        IS7LogicalBatchOperations operations) =>
        new(
            plc,
            catalog,
            store: null,
            new FixedTimeProvider(FixedTimestamp),
            operations);

    /// <summary>Creates a disconnected concrete S7 client for registration-path testing.</summary>
    /// <returns>The disconnected S7 client.</returns>
    private static RxS7 CreatePlc() =>
        new(new(new(CpuType.S71500, LoopbackAddress, RackNumber, SlotNumber)));

    /// <summary>Creates the catalog used by batch-path tests.</summary>
    /// <returns>The populated logical-tag catalog.</returns>
    private static LogicalTagCatalog CreateCatalog()
    {
        var catalog = new LogicalTagCatalog();
        catalog.Upsert(new LogicalTag(FirstTagName, "DB1.DBW0", "WORD"));
        catalog.Upsert(new LogicalTag(SecondTagName, "DB1.DBW2", "WORD"));
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

    /// <summary>Creates an unsigned request value for a WORD tag.</summary>
    /// <param name="name">The logical-tag name.</param>
    /// <param name="value">The unsigned WORD value.</param>
    /// <returns>The logical-tag value.</returns>
    private static LogicalTagValue CreateValue(string name, ushort value) =>
        new(name, value, FixedTimestamp);

    /// <summary>Provides a deterministic timestamp to logical operation results.</summary>
    /// <param name="timestamp">The fixed UTC timestamp.</param>
    private sealed class FixedTimeProvider(DateTimeOffset timestamp) : TimeProvider
    {
        /// <inheritdoc />
        public override DateTimeOffset GetUtcNow() => timestamp;
    }

    /// <summary>Provides deterministic multi-variable results and records each request.</summary>
    private sealed class ScriptedBatchOperations : IS7LogicalBatchOperations
    {
        /// <summary>Gets the number of read batches.</summary>
        public int ReadCallCount { get; private set; }

        /// <summary>Gets the number of write batches.</summary>
        public int WriteCallCount { get; private set; }

        /// <summary>Gets the names passed to the latest read batch.</summary>
        public string[] ReadTagNames { get; private set; } = [];

        /// <summary>Gets the names passed to the latest write batch.</summary>
        public string[] WriteTagNames { get; private set; } = [];

        /// <summary>Gets the values passed to the latest write batch.</summary>
        public object?[] WrittenValues { get; private set; } = [];

        /// <summary>Gets the scripted read result.</summary>
        public IReadOnlyDictionary<string, object?>? ReadValues { get; init; } =
            new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                [FirstTagName] = FirstReadValue,
                [SecondTagName] = SecondReadValue,
            };

        /// <summary>Gets the optional read exception.</summary>
        public Exception? ReadException { get; init; }

        /// <summary>Gets or sets a value indicating whether writes succeed.</summary>
        public bool WriteSucceeds { get; set; } = true;

        /// <inheritdoc />
        public IReadOnlyDictionary<string, object?>? ReadMultiple(IReadOnlyList<Tag> tags)
        {
            ReadCallCount++;
            ReadTagNames = tags.Select(static tag => tag.Name!).ToArray();
            if (ReadException is not null)
            {
                throw ReadException;
            }

            return ReadValues;
        }

        /// <inheritdoc />
        public bool WriteMultiple(IReadOnlyList<Tag> tags)
        {
            WriteCallCount++;
            WriteTagNames = tags.Select(static tag => tag.Name!).ToArray();
            WrittenValues = tags.Select(static tag => tag.NewValue).ToArray();
            return WriteSucceeds;
        }
    }
}
