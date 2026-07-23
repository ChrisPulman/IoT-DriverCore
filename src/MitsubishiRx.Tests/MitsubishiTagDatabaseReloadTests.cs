// Copyright (c) 2019-2026 Chris Pulman and contributors. All rights reserved.
// Chris Pulman and contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

#if REACTIVE_SHIM

namespace IoT.DriverCore.MitsubishiRx.Reactive.Tests;
#else

namespace IoT.DriverCore.MitsubishiRx.Tests;
#endif

/// <summary>Provides the MitsubishiTagDatabaseReloadTests type.</summary>
internal sealed class MitsubishiTagDatabaseReloadTests
{
    /// <summary>Stores the initial tag database count.</summary>
    private const int InitialTagCount = 2;

    /// <summary>Stores the expected reload notification count.</summary>
    private const int ReloadNotificationCount = 2;

    /// <summary>Stores the reload polling interval in seconds.</summary>
    private const int ReloadPollSeconds = 5;

    /// <summary>Stores the <c>MotorSpeedTagName</c> test value.</summary>
    private const string MotorSpeedTagName = "MotorSpeed";

    /// <summary>Stores the <c>OperatorMessageTagName</c> test value.</summary>
    private const string OperatorMessageTagName = "OperatorMessage";

    /// <summary>Executes the LoadAndValidateAppliesLoadedDatabaseWhenSchemaIsValid operation.</summary>
    /// <returns>The LoadAndValidateAppliesLoadedDatabaseWhenSchemaIsValid operation result.</returns>
    [Test]
    internal async Task LoadAndValidateAppliesLoadedDatabaseWhenSchemaIsValidAsync()
    {
        var path = CreateTempPath("json");
        var database = new MitsubishiTagDatabase(
        [
            new MitsubishiTagDefinition(MotorSpeedTagName, "D100", DataType: "Word", Scale: 0.1, Units: "rpm"),
            new MitsubishiTagDefinition(
                OperatorMessageTagName,
                "D600",
                DataType: "String",
                Length: 2,
                Encoding: "Utf8"),
        ]);
        database.AddGroup(new MitsubishiTagGroupDefinition("Overview", [MotorSpeedTagName, OperatorMessageTagName]));
        database.Save(path);

        await using var client = CreateClient(Scheduler.Immediate);

        try
        {
            var result = client.LoadAndValidateTagDatabase(path);

            await Assert.That(result.IsSucceed).IsTrue();
            await Assert.That(client.TagDatabase is not null).IsTrue();
            await Assert.That(client.TagDatabase!.Count).IsEqualTo(InitialTagCount);
            await Assert.That(client.TagDatabase.GroupCount).IsEqualTo(1);
            await Assert.That(client.TagDatabase.GetRequired(OperatorMessageTagName).Encoding).IsEqualTo("Utf8");
        }
        finally
        {
            DeleteIfExists(path);
        }
    }

    /// <summary>Verifies invalid schemas do not replace the active tag database.</summary>
    /// <returns>
    /// The LoadAndValidateReturnsErrorsAndDoesNotReplaceExistingDatabaseWhenSchemaIsInvalid operation result.
    /// </returns>
    [Test]
    internal async Task LoadAndValidateReturnsErrorsAndDoesNotReplaceExistingDatabaseWhenSchemaIsInvalidAsync()
    {
        var path = CreateTempPath("json");
        await File.WriteAllTextAsync(
            path,
            """
            {
              "tags": [
                {
                  "name": "BadString",
                  "address": "D100",
                  "dataType": "String"
                }
              ]
            }
            """,
            CancellationToken.None);

        var existing = new MitsubishiTagDatabase(
        [
            new MitsubishiTagDefinition("ExistingTag", "D200", DataType: "UInt16"),
        ]);

        await using var client = CreateClient(Scheduler.Immediate);
        client.TagDatabase = existing;

        try
        {
            var result = client.LoadAndValidateTagDatabase(path);

            await Assert.That(result.IsSucceed).IsFalse();
            await Assert.That(result.Err.Contains("must define a positive Length", StringComparison.OrdinalIgnoreCase))
                .IsTrue();
            await Assert.That(ReferenceEquals(client.TagDatabase, existing)).IsTrue();
            await Assert.That(client.TagDatabase!.GetRequired("ExistingTag").Address).IsEqualTo("D200");
        }
        finally
        {
            DeleteIfExists(path);
        }
    }

    /// <summary>Executes the ObserveTagDatabaseReloadEmitsSuccessfulReloadsWhenSchemaChanges operation.</summary>
    /// <returns>The ObserveTagDatabaseReloadEmitsSuccessfulReloadsWhenSchemaChanges operation result.</returns>
    [Test]
    internal async Task ObserveTagDatabaseReloadEmitsSuccessfulReloadsWhenSchemaChangesAsync()
    {
        var scheduler = new TestScheduler();
        var path = CreateTempPath("json");
        CreateDatabase(MotorSpeedTagName, "D100").Save(path);

        await using var client = CreateClient(scheduler);
        var received = new List<Responce<MitsubishiTagDatabase>>();

        try
        {
            using var subscription = client
                .ObserveTagDatabaseReload(path, TimeSpan.FromSeconds(ReloadPollSeconds), emitInitial: true)
                .Take(ReloadNotificationCount)
                .Subscribe(received.Add);

            TestSchedulerDriver.AdvanceBy(scheduler, 1);
            CreateDatabase(MotorSpeedTagName, "D101").Save(path);
            TestSchedulerDriver.AdvanceBy(
                scheduler,
                TimeSpan.FromSeconds(ReloadPollSeconds).Ticks + 1);

            await Assert.That(received.Count).IsEqualTo(ReloadNotificationCount);
            await Assert.That(received[0].IsSucceed).IsTrue();
            await Assert.That(received[1].IsSucceed).IsTrue();
            await Assert.That(received[0].Value!.GetRequired(MotorSpeedTagName).Address).IsEqualTo("D100");
            await Assert.That(received[1].Value!.GetRequired(MotorSpeedTagName).Address).IsEqualTo("D101");
            await Assert.That(client.TagDatabase!.GetRequired(MotorSpeedTagName).Address).IsEqualTo("D101");
        }
        finally
        {
            DeleteIfExists(path);
        }
    }

    /// <summary>Verifies invalid tag database reloads preserve the last valid database.</summary>
    /// <returns>
    /// The ObserveTagDatabaseReloadEmitsFailureForInvalidUpdateAndPreservesLastValidDatabase operation result.
    /// </returns>
    [Test]
    internal async Task ObserveTagDatabaseReloadEmitsFailureForInvalidUpdateAndPreservesLastValidDatabaseAsync()
    {
        var scheduler = new TestScheduler();
        var path = CreateTempPath("json");
        CreateDatabase(MotorSpeedTagName, "D100").Save(path);

        await using var client = CreateClient(scheduler);
        var received = new List<Responce<MitsubishiTagDatabase>>();

        try
        {
            using var subscription = client
                .ObserveTagDatabaseReload(path, TimeSpan.FromSeconds(ReloadPollSeconds), emitInitial: true)
                .Take(ReloadNotificationCount)
                .Subscribe(received.Add);

            TestSchedulerDriver.AdvanceBy(scheduler, 1);
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
                TimeSpan.FromSeconds(ReloadPollSeconds).Ticks + 1);

            await Assert.That(received.Count).IsEqualTo(ReloadNotificationCount);
            await Assert.That(received[0].IsSucceed).IsTrue();
            await Assert.That(received[1].IsSucceed).IsFalse();
            await Assert.That(
                    received[1].Err.Contains(
                        "must define a positive Length",
                        StringComparison.OrdinalIgnoreCase))
                .IsTrue();
            await Assert.That(client.TagDatabase!.GetRequired(MotorSpeedTagName).Address).IsEqualTo("D100");
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
            Port: 5040,
            FrameType: MitsubishiFrameType.ThreeE,
            DataCode: CommunicationDataCode.Binary,
            TransportKind: MitsubishiTransportKind.Tcp,
            Route: MitsubishiRoute.Default);

        return new MitsubishiRx(options, null, scheduler);
    }

    /// <summary>Executes the CreateDatabase operation.</summary>
    /// <param name="tagName">The tagName parameter.</param>
    /// <param name="address">The address parameter.</param>
    /// <returns>The CreateDatabase operation result.</returns>
    private static MitsubishiTagDatabase CreateDatabase(string tagName, string address)
    {
        var database = new MitsubishiTagDatabase(
        [
            new MitsubishiTagDefinition(tagName, address, DataType: "Word", Scale: 0.1, Units: "rpm"),
        ]);
        database.AddGroup(new MitsubishiTagGroupDefinition("Overview", [tagName]));
        return database;
    }

    /// <summary>Executes the CreateTempPath operation.</summary>
    /// <param name="extension">The extension parameter.</param>
    /// <returns>The CreateTempPath operation result.</returns>
    private static string CreateTempPath(string extension)
        => Path.Combine(Path.GetTempPath(), $"mitsubishirx-reload-{Guid.NewGuid():N}.{extension}");

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
