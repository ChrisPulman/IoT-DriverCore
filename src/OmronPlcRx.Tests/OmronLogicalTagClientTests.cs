// Copyright (c) 2019-2026 Chris Pulman and contributors. All rights reserved.
// Chris Pulman and contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using IoT.DriverCore.Core;
using IoT.DriverCore.OmronPlcRx.Tags;
using ReactiveUI.Primitives;
using TUnit.Core;

namespace IoT.DriverCore.OmronPlcRx.Tests;

/// <summary>Tests the composed logical-tag adapter and modern generated property bindings.</summary>
public sealed class OmronLogicalTagClientTests
{
    /// <summary>Initial speed published to the test PLC.</summary>
    private const short InitialSpeed = 42;

    /// <summary>Speed written through the single-value API.</summary>
    private const short WrittenSpeed = 84;

    /// <summary>Speed written through the bulk API.</summary>
    private const short BulkWrittenSpeed = 126;

    /// <summary>Initial conveyor speed published to the test PLC.</summary>
    private const short InitialConveyorSpeed = 15;

    /// <summary>Conveyor speed written through the generated API.</summary>
    private const short UpdatedConveyorSpeed = 30;

    /// <summary>Final conveyor speed written through the generated API.</summary>
    private const short FinalConveyorSpeed = 45;

    /// <summary>CSV delimiter used by import and export tests.</summary>
    private const char CsvDelimiter = ',';

    /// <summary>Logical name of the speed tag.</summary>
    private const string SpeedTagName = "Speed";

    /// <summary>Logical name of the running tag.</summary>
    private const string RunningTagName = "Running";

    /// <summary>Logical name of the persisted temperature tag.</summary>
    private const string TemperatureTagName = "Temperature";

    /// <summary>Timeout used while observing tag values.</summary>
    private const int ObservationTimeoutSeconds = 5;

    /// <summary>Expected number of tags and writes in logical-tag tests.</summary>
    private const int ExpectedTagCount = 2;

    /// <summary>Verifies awaited logical operations, bulk operations, and both observation contracts.</summary>
    /// <returns>A task that represents the asynchronous test.</returns>
    [Test]
    public async Task LogicalClient_RegistersReadsWritesAndObservesByNameAsync()
    {
        using var plc = new FakeOmronPlcRx();
        using var client = new OmronLogicalTagClient(plc);
        var speedTag = new PlcTag<short>(SpeedTagName, "D100");
        var runningTag = new PlcTag<bool>(RunningTagName, "D100.0");
        _ = client.CreateTag(speedTag);
        _ = client.CreateTag(runningTag);

        plc.Publish(SpeedTagName, InitialSpeed);
        plc.Publish(RunningTagName, true);
        var read = await client.ReadAsync(new LogicalTagKey<short>(speedTag.TagName), CancellationToken.None);
        var bulk = await client.ReadManyAsync([SpeedTagName, RunningTagName], CancellationToken.None);
        var write = await client.WriteAsync(new LogicalTagKey<short>(SpeedTagName), WrittenSpeed, CancellationToken.None);
        var bulkWrite = await client.WriteManyAsync(
            [
                new LogicalTagValue(SpeedTagName, BulkWrittenSpeed, TimeProvider.System.GetUtcNow()),
                new LogicalTagValue(RunningTagName, false, TimeProvider.System.GetUtcNow())
            ],
            CancellationToken.None);

        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(ObservationTimeoutSeconds));
        await using var values = client
            .ObserveAsync(SpeedTagName, timeout.Token)
            .GetAsyncEnumerator(timeout.Token);
        var moved = await values.MoveNextAsync();

        await Assert.That(read.Succeeded).IsTrue();
        await Assert.That(read.Value).IsEqualTo(InitialSpeed);
        await Assert.That(bulk.Count).IsEqualTo(ExpectedTagCount);
        await Assert.That(bulk.All(static result => result.Succeeded)).IsTrue();
        await Assert.That(write.Succeeded).IsTrue();
        await Assert.That(bulkWrite.All(static result => result.Succeeded)).IsTrue();
        await Assert.That(moved).IsTrue();
        await Assert.That(values.Current.Value).IsEqualTo(BulkWrittenSpeed);
    }

    /// <summary>Verifies CSV loading and SQLite CRUD with dynamic registration.</summary>
    /// <returns>A task that represents the asynchronous test.</returns>
    [Test]
    public async Task LogicalClient_ImportsCsvAndLoadsSqliteDefinitionsAsync()
    {
        const string Csv =
            "Name,Address,DataType,GroupName,Description,Metadata,AccessMode,ScanIntervalMilliseconds\r\n"
            + "Temperature,D200,System.Int16,Process,Temperature,,ReadWrite,250\r\n";
        using var plc = new FakeOmronPlcRx();
        using var csvClient = new OmronLogicalTagClient(plc);
        var imported = await csvClient.ImportCsvAsync(new StringReader(Csv), CsvDelimiter, CancellationToken.None);
        await using var exported = new StringWriter();
        await csvClient.ExportCsvAsync(exported, CsvDelimiter, CancellationToken.None);

        var databasePath = Path.Combine(Path.GetTempPath(), $"omron-logical-tags-{Guid.NewGuid():N}.db");
        try
        {
            var connectionString = $"Data Source={databasePath};Pooling=False";
            using (var stored = new OmronLogicalTagClient(plc, connectionString))
            {
                await stored.InitializeStoreAsync(CancellationToken.None);
                await stored.UpsertGroupAsync(new LogicalTagGroup("Process"), CancellationToken.None);
                await stored.UpsertTagAsync(imported[0], CancellationToken.None);
                var tagOpts = imported[0].CurrentOptions();
                tagOpts.Description = "Process temperature";
                var edited = await stored.EditTagAsync(
                    imported[0].WithOptions(tagOpts),
                    CancellationToken.None);
                await Assert.That(edited).IsTrue();
            }

            using var loadedPlc = new FakeOmronPlcRx();
            using var loaded = new OmronLogicalTagClient(loadedPlc, connectionString);
            var tags = await loaded.LoadTagsAsync(CancellationToken.None);
            var persisted = await loaded.GetTagAsync(TemperatureTagName, CancellationToken.None);
            var deleted = await loaded.DeleteTagAsync(TemperatureTagName, CancellationToken.None);

            await Assert.That(tags.Count).IsEqualTo(1);
            await Assert.That(persisted?.Description).IsEqualTo("Process temperature");
            await Assert.That(loadedPlc.Registrations.Count).IsEqualTo(1);
            await Assert.That(deleted).IsTrue();
            await Assert.That(loaded.Catalog.TryGet(TemperatureTagName, out _)).IsFalse();
        }
        finally
        {
            if (File.Exists(databasePath))
            {
                File.Delete(databasePath);
            }
        }

        const string expectedCsvTag = "Temperature,D200,System.Int16";
        var exportedCsv = exported.ToString();
        await Assert.That(exportedCsv.Contains(expectedCsvTag, StringComparison.Ordinal)).IsTrue();
    }

    /// <summary>Verifies generated property tags expose observables, awaited helpers, and legacy aliases.</summary>
    /// <returns>A task that represents the asynchronous test.</returns>
    [Test]
    public async Task PropertyBindings_GenerateReadWriteAndObservableMembersAsync()
    {
        using var plc = new FakeOmronPlcRx();
        var state = new GeneratedPropertyMachineState();
        var observed = new List<short>();
        using var subscription = state.ConveyorSpeedObservable.SubscribeSafe(
            observed.Add,
            static error => throw error);
        using var binding = state.BindPlcTags(plc);

        plc.Publish("ConveyorSpeed", InitialConveyorSpeed);
        plc.Publish("RecipeName", "Batch A");
        var read = await state.ReadConveyorSpeedAsync(CancellationToken.None);
        var write = await state.WriteConveyorSpeedAsync(UpdatedConveyorSpeed, CancellationToken.None);
        await state.WriteConveyorSpeedAsync(FinalConveyorSpeed, CancellationToken.None);

        await Assert.That(state.ConveyorSpeed).IsEqualTo(FinalConveyorSpeed);
        await Assert.That(state.RecipeName).IsEqualTo("Batch A");
        await Assert.That(read.Succeeded).IsTrue();
        await Assert.That(write.Succeeded).IsTrue();
        await Assert.That(observed.Contains(InitialConveyorSpeed)).IsTrue();
        await Assert.That(plc.Writes.Count).IsEqualTo(ExpectedTagCount);
    }
}
