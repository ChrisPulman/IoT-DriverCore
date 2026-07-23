// Copyright (c) 2019-2026 Chris Pulman and contributors. All rights reserved.
// Chris Pulman and contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

#if REACTIVE_SHIM

namespace IoT.DriverCore.MitsubishiRx.Reactive.Tests;
#else

namespace IoT.DriverCore.MitsubishiRx.Tests;
#endif

/// <summary>Provides the MitsubishiTagDatabaseDiffTests type.</summary>
internal sealed class MitsubishiTagDatabaseDiffTests
{
    /// <summary>Stores the database-diff polling interval in seconds.</summary>
    private const int DiffPollSeconds = 5;

    /// <summary>Stores the <c>OperatorMessageTagName</c> test value.</summary>
    private const string OperatorMessageTagName = "OperatorMessage";

    /// <summary>Stores the <c>LegacyTextTagName</c> test value.</summary>
    private const string LegacyTextTagName = "LegacyText";

    /// <summary>Stores the <c>MotorSpeedTagName</c> test value.</summary>
    private const string MotorSpeedTagName = "MotorSpeed";

    /// <summary>Stores the <c>OverviewGroupName</c> test value.</summary>
    private const string OverviewGroupName = "Overview";

    /// <summary>Executes the CompareToReportsAddedRemovedAndChangedTagsAndGroups operation.</summary>
    /// <returns>The CompareToReportsAddedRemovedAndChangedTagsAndGroups operation result.</returns>
    [Test]
    internal async Task CompareToReportsAddedRemovedAndChangedTagsAndGroupsAsync()
    {
        var current = CreateCurrentDatabase();
        var updated = CreateUpdatedDatabase();

        var diff = current.CompareWith(updated);

        await Assert.That(diff.HasChanges).IsTrue();
        await Assert.That(diff.AddedTags.Count).IsEqualTo(1);
        await Assert.That(diff.RemovedTags.Count).IsEqualTo(1);
        await Assert.That(diff.ChangedTags.Count).IsEqualTo(1);
        await Assert.That(diff.AddedGroups.Count).IsEqualTo(1);
        await Assert.That(diff.RemovedGroups.Count).IsEqualTo(1);
        await Assert.That(diff.ChangedGroups.Count).IsEqualTo(1);

        await Assert.That(diff.AddedTags[0].Name).IsEqualTo(OperatorMessageTagName);
        await Assert.That(diff.RemovedTags[0].Name).IsEqualTo(LegacyTextTagName);
        await Assert.That(diff.ChangedTags[0].Name).IsEqualTo(MotorSpeedTagName);
        await Assert.That(diff.ChangedTags[0].Previous!.Address).IsEqualTo("D100");
        await Assert.That(diff.ChangedTags[0].Current!.Address).IsEqualTo("D101");

        await Assert.That(diff.AddedGroups[0].Name).IsEqualTo("Diagnostics");
        await Assert.That(diff.RemovedGroups[0].Name).IsEqualTo("Legacy");
        await Assert.That(diff.ChangedGroups[0].Name).IsEqualTo(OverviewGroupName);
    }

    /// <summary>Executes the PreviewTagDatabaseDiffReturnsChangesWithoutReplacingCurrentDatabase operation.</summary>
    /// <returns>The PreviewTagDatabaseDiffReturnsChangesWithoutReplacingCurrentDatabase operation result.</returns>
    [Test]
    internal async Task PreviewTagDatabaseDiffReturnsChangesWithoutReplacingCurrentDatabaseAsync()
    {
        var path = CreateTempPath("json");
        CreateUpdatedDatabase().Save(path);

        await using var client = CreateClient(Scheduler.Immediate);
        client.TagDatabase = CreateCurrentDatabase();

        try
        {
            var result = client.PreviewTagDatabaseDiff(path);

            await Assert.That(result.IsSucceed).IsTrue();
            await Assert.That(result.Value is not null).IsTrue();
            await Assert.That(result.Value!.ChangedTags.Count).IsEqualTo(1);
            await Assert.That(result.Value.AddedTags[0].Name).IsEqualTo(OperatorMessageTagName);
            await Assert.That(client.TagDatabase!.GetRequired(MotorSpeedTagName).Address).IsEqualTo("D100");
            await Assert.That(client.TagDatabase.TryGet(OperatorMessageTagName, out _)).IsFalse();
        }
        finally
        {
            DeleteIfExists(path);
        }
    }

    /// <summary>Executes the ObserveTagDatabaseDiffEmitsSemanticChangesAndAppliesSuccessfulReloads operation.</summary>
    /// <returns>The ObserveTagDatabaseDiffEmitsSemanticChangesAndAppliesSuccessfulReloads operation result.</returns>
    [Test]
    internal async Task ObserveTagDatabaseDiffEmitsSemanticChangesAndAppliesSuccessfulReloadsAsync()
    {
        var scheduler = new TestScheduler();
        var path = CreateTempPath("json");
        CreateCurrentDatabase().Save(path);

        await using var client = CreateClient(scheduler);
        client.TagDatabase = CreateCurrentDatabase();
        var received = new List<Responce<MitsubishiTagDatabaseDiff>>();

        try
        {
            using var subscription = client
                .ObserveTagDatabaseDiff(path, TimeSpan.FromSeconds(DiffPollSeconds), emitInitial: false)
                .Take(1)
                .Subscribe(received.Add);

            CreateUpdatedDatabase().Save(path);
            TestSchedulerDriver.AdvanceBy(
                scheduler,
                TimeSpan.FromSeconds(DiffPollSeconds).Ticks + 1);

            await Assert.That(received.Count).IsEqualTo(1);
            await Assert.That(received[0].IsSucceed).IsTrue();
            await Assert.That(received[0].Value is not null).IsTrue();
            await Assert.That(received[0].Value!.ChangedTags.Count).IsEqualTo(1);
            await Assert.That(received[0].Value!.AddedTags[0].Name).IsEqualTo(OperatorMessageTagName);
            await Assert.That(received[0].Value!.RemovedTags[0].Name).IsEqualTo(LegacyTextTagName);
            await Assert.That(client.TagDatabase!.GetRequired(MotorSpeedTagName).Address).IsEqualTo("D101");
            await Assert.That(client.TagDatabase.TryGet(OperatorMessageTagName, out _)).IsTrue();
        }
        finally
        {
            DeleteIfExists(path);
        }
    }

    /// <summary>Verifies invalid reloads preserve the active tag database.</summary>
    /// <returns>
    /// The ObserveTagDatabaseDiffEmitsFailureAndPreservesLastValidDatabaseOnInvalidReload operation result.
    /// </returns>
    [Test]
    internal async Task ObserveTagDatabaseDiffEmitsFailureAndPreservesLastValidDatabaseOnInvalidReloadAsync()
    {
        var scheduler = new TestScheduler();
        var path = CreateTempPath("json");
        CreateCurrentDatabase().Save(path);

        await using var client = CreateClient(scheduler);
        client.TagDatabase = CreateCurrentDatabase();
        var received = new List<Responce<MitsubishiTagDatabaseDiff>>();

        try
        {
            using var subscription = client
                .ObserveTagDatabaseDiff(path, TimeSpan.FromSeconds(DiffPollSeconds), emitInitial: false)
                .Take(1)
                .Subscribe(received.Add);

            await File.WriteAllTextAsync(
                path,
                """
                {
                  "tags": [
                    {
                      "name": "BrokenText",
                      "address": "D600",
                      "dataType": "String"
                    }
                  ]
                }
                """,
                CancellationToken.None);
            TestSchedulerDriver.AdvanceBy(
                scheduler,
                TimeSpan.FromSeconds(DiffPollSeconds).Ticks + 1);

            await Assert.That(received.Count).IsEqualTo(1);
            await Assert.That(received[0].IsSucceed).IsFalse();
            await Assert.That(
                    received[0].Err.Contains(
                        "must define a positive Length",
                        StringComparison.OrdinalIgnoreCase))
                .IsTrue();
            await Assert.That(client.TagDatabase!.GetRequired(MotorSpeedTagName).Address).IsEqualTo("D100");
            await Assert.That(client.TagDatabase.TryGet(OperatorMessageTagName, out _)).IsFalse();
        }
        finally
        {
            DeleteIfExists(path);
        }
    }

    /// <summary>Executes the CreateClient operation.</summary>
    /// <param name="scheduler">The scheduler parameter.</param>
    /// <returns>The CreateClient operation result.</returns>
    private static MitsubishiRx CreateClient(IScheduler scheduler)
    {
        var options = new MitsubishiClientOptions(
            Host: "127.0.0.1",
            Port: 5041,
            FrameType: MitsubishiFrameType.ThreeE,
            DataCode: CommunicationDataCode.Binary,
            TransportKind: MitsubishiTransportKind.Tcp,
            Route: MitsubishiRoute.Default);

        return new MitsubishiRx(options, null, scheduler);
    }

    /// <summary>Executes the CreateCurrentDatabase operation.</summary>
    /// <returns>The CreateCurrentDatabase operation result.</returns>
    private static MitsubishiTagDatabase CreateCurrentDatabase()
    {
        var database = new MitsubishiTagDatabase(
        [
            new MitsubishiTagDefinition(MotorSpeedTagName, "D100", DataType: "Word", Scale: 0.1, Units: "rpm"),
            new MitsubishiTagDefinition(LegacyTextTagName, "D200", DataType: "String", Length: 2, Encoding: "Ascii"),
        ]);

        database.AddGroup(new MitsubishiTagGroupDefinition(OverviewGroupName, [MotorSpeedTagName, LegacyTextTagName]));
        database.AddGroup(new MitsubishiTagGroupDefinition("Legacy", [LegacyTextTagName]));
        return database;
    }

    /// <summary>Executes the CreateUpdatedDatabase operation.</summary>
    /// <returns>The CreateUpdatedDatabase operation result.</returns>
    private static MitsubishiTagDatabase CreateUpdatedDatabase()
    {
        var database = new MitsubishiTagDatabase(
        [
            new MitsubishiTagDefinition(MotorSpeedTagName, "D101", DataType: "Word", Scale: 0.1, Units: "rpm"),
            new MitsubishiTagDefinition(
                OperatorMessageTagName,
                "D600",
                DataType: "String",
                Length: 2,
                Encoding: "Utf8"),
        ]);

        database.AddGroup(
            new MitsubishiTagGroupDefinition(OverviewGroupName, [ MotorSpeedTagName, OperatorMessageTagName]));
        database.AddGroup(new MitsubishiTagGroupDefinition("Diagnostics", [OperatorMessageTagName]));
        return database;
    }

    /// <summary>Executes the CreateTempPath operation.</summary>
    /// <param name="extension">The extension parameter.</param>
    /// <returns>The CreateTempPath operation result.</returns>
    private static string CreateTempPath(string extension)
        => Path.Combine(Path.GetTempPath(), $"mitsubishirx-diff-{Guid.NewGuid():N}.{extension}");

    /// <summary>Executes the DeleteIfExists operation.</summary>
    /// <param name="path">The path parameter.</param>
    private static void DeleteIfExists(string path)
    {
        if (!File.Exists(path))
        {
            return;
        }

        File.Delete(path);
    }
}
