// Copyright (c) 2019-2026 Chris Pulman and contributors. All rights reserved.
// Chris Pulman and contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

#if REACTIVE_SHIM

namespace MitsubishiRx.Reactive.Tests;
#else

namespace MitsubishiRx.Tests;
#endif

/// <summary>Provides the MitsubishiTagDatabaseRolloutPolicyTests type.</summary>
internal sealed class MitsubishiTagDatabaseRolloutPolicyTests
{
    /// <summary>Stores the rollout polling interval in seconds.</summary>
    private const int RolloutPollSeconds = 5;

    /// <summary>Stores the <c>OperatorMessageTagName</c> test value.</summary>
    private const string OperatorMessageTagName = "OperatorMessage";

    /// <summary>Stores the <c>MotorSpeedTagName</c> test value.</summary>
    private const string MotorSpeedTagName = "MotorSpeed";

    /// <summary>Stores the <c>ProcessValueTagName</c> test value.</summary>
    private const string ProcessValueTagName = "ProcessValue";

    /// <summary>Stores the <c>UpdatedHmiText</c> test value.</summary>
    private const string UpdatedHmiText = "Updated HMI text";

    /// <summary>Stores the <c>OverviewGroupName</c> test value.</summary>
    private const string OverviewGroupName = "Overview";

    /// <summary>Stores the <c>MainSpindleRpmNotes</c> test value.</summary>
    private const string MainSpindleRpmNotes = "Main spindle RPM";

    /// <summary>Stores the <c>RawProcessValueNotes</c> test value.</summary>
    private const string RawProcessValueNotes = "Raw process value";

    /// <summary>Stores the <c>StringDataType</c> test value.</summary>
    private const string StringDataType = "String";

    /// <summary>Executes the CompareWithClassifiesMetadataAddressDatatypeAndGroupMembershipChanges operation.</summary>
    /// <returns>The CompareWithClassifiesMetadataAddressDatatypeAndGroupMembershipChanges operation result.</returns>
    [Test]
    internal async Task CompareWithClassifiesMetadataAddressDatatypeAndGroupMembershipChangesAsync()
    {
        var current = CreatePolicyCurrentDatabase();
        var updated = CreatePolicyUpdatedDatabase();

        var diff = current.CompareWith(updated);

        await Assert.That(diff.ChangeKinds.HasFlag(MitsubishiSchemaChangeKind.MetadataOnly)).IsTrue();
        await Assert.That(diff.ChangeKinds.HasFlag(MitsubishiSchemaChangeKind.AddressChange)).IsTrue();
        await Assert.That(diff.ChangeKinds.HasFlag(MitsubishiSchemaChangeKind.DataTypeChange)).IsTrue();
        await Assert.That(diff.ChangeKinds.HasFlag(MitsubishiSchemaChangeKind.GroupMembershipChange)).IsTrue();

        var metadataChange = diff.ChangedTags.Single(change => change.Name == OperatorMessageTagName);
        var addressChange = diff.ChangedTags.Single(change => change.Name == MotorSpeedTagName);
        var dataTypeChange = diff.ChangedTags.Single(change => change.Name == ProcessValueTagName);

        await Assert.That(metadataChange.ChangeKinds).IsEqualTo(MitsubishiSchemaChangeKind.MetadataOnly);
        await Assert.That(addressChange.ChangeKinds.HasFlag(MitsubishiSchemaChangeKind.AddressChange)).IsTrue();
        await Assert.That(dataTypeChange.ChangeKinds.HasFlag(MitsubishiSchemaChangeKind.DataTypeChange)).IsTrue();
        await Assert.That(diff.ChangedGroups.Single().ChangeKinds)
            .IsEqualTo(MitsubishiSchemaChangeKind.GroupMembershipChange);
    }

    /// <summary>Verifies safe rollout policies reject address and data type changes.</summary>
    /// <returns>
    /// The PreviewTagDatabaseDiffWithSafeMetadataAndGroupsPolicyRejectsAddressAndDatatypeChanges operation result.
    /// </returns>
    [Test]
    internal async Task PreviewTagDatabaseDiffWithSafeMetadataAndGroupsPolicyRejectsAddressAndDatatypeChangesAsync()
    {
        var path = CreateTempPath("json");
        CreatePolicyUpdatedDatabase().Save(path);

        await using var client = CreateClient(Scheduler.Immediate);
        client.TagDatabase = CreatePolicyCurrentDatabase();

        try
        {
            var result = client.PreviewTagDatabaseDiff(path, MitsubishiTagRolloutPolicy.SafeMetadataAndGroups);

            await Assert.That(result.IsSucceed).IsFalse();
            await Assert.That(result.Value is not null).IsTrue();
            await Assert.That(result.Value!.ChangeKinds.HasFlag(MitsubishiSchemaChangeKind.AddressChange)).IsTrue();
            await Assert.That(result.Value.ChangeKinds.HasFlag(MitsubishiSchemaChangeKind.DataTypeChange)).IsTrue();
            await Assert.That(result.Err.Contains("AddressChange", StringComparison.OrdinalIgnoreCase)).IsTrue();
            await Assert.That(result.Err.Contains("DataTypeChange", StringComparison.OrdinalIgnoreCase)).IsTrue();
        }
        finally
        {
            DeleteIfExists(path);
        }
    }

    /// <summary>Verifies safe rollout policies apply allowed metadata and group changes.</summary>
    /// <returns>
    /// The LoadAndValidateTagDatabaseWithSafeMetadataAndGroupsPolicyAppliesAllowedChanges operation result.
    /// </returns>
    [Test]
    internal async Task LoadAndValidateTagDatabaseWithSafeMetadataAndGroupsPolicyAppliesAllowedChangesAsync()
    {
        var path = CreateTempPath("json");
        CreateMetadataAndGroupOnlyDatabase().Save(path);

        await using var client = CreateClient(Scheduler.Immediate);
        client.TagDatabase = CreatePolicyCurrentDatabase();

        try
        {
            var result = client.LoadAndValidateTagDatabase(path, MitsubishiTagRolloutPolicy.SafeMetadataAndGroups);

            await Assert.That(result.IsSucceed).IsTrue();
            await Assert.That(client.TagDatabase!.GetRequired(OperatorMessageTagName).Description)
                .IsEqualTo(UpdatedHmiText);
            await Assert.That(client.TagDatabase.GetRequiredGroup(OverviewGroupName).ResolvedTagNames)
                .IsEquivalentTo([ MotorSpeedTagName, OperatorMessageTagName]);
        }
        finally
        {
            DeleteIfExists(path);
        }
    }

    /// <summary>Verifies rejected address changes preserve the active tag database.</summary>
    /// <returns>
    /// The
    /// ObserveTagDatabaseReloadWithSafeMetadataAndGroupsPolicyRejectsAddressChangeAndPreservesDatabase
    /// operation result.
    /// </returns>
    [Test]
    internal async Task
        ObserveTagDatabaseReloadWithSafeMetadataAndGroupsPolicyRejectsAddressChangeAndPreservesDatabaseAsync()
    {
        var scheduler = new TestScheduler();
        var path = CreateTempPath("json");
        CreatePolicyCurrentDatabase().Save(path);

        await using var client = CreateClient(scheduler);
        client.TagDatabase = CreatePolicyCurrentDatabase();
        var received = new List<Responce<MitsubishiTagDatabase>>();

        try
        {
            using var subscription = client
                .ObserveTagDatabaseReload(
                    path,
                    TimeSpan.FromSeconds(RolloutPollSeconds),
                    emitInitial: false,
                    policy: MitsubishiTagRolloutPolicy.SafeMetadataAndGroups)
                .Take(1)
                .Subscribe(received.Add);

            CreateAddressOnlyUpdatedDatabase().Save(path);
            TestSchedulerDriver.AdvanceBy(
                scheduler,
                TimeSpan.FromSeconds(RolloutPollSeconds).Ticks + 1);

            await Assert.That(received.Count).IsEqualTo(1);
            await Assert.That(received[0].IsSucceed).IsFalse();
            await Assert.That(received[0].Err.Contains("AddressChange", StringComparison.OrdinalIgnoreCase)).IsTrue();
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
            Port: 5042,
            FrameType: MitsubishiFrameType.ThreeE,
            DataCode: CommunicationDataCode.Binary,
            TransportKind: MitsubishiTransportKind.Tcp,
            Route: MitsubishiRoute.Default);

        return new MitsubishiRx(options, null, scheduler);
    }

    /// <summary>Executes the CreatePolicyCurrentDatabase operation.</summary>
    /// <returns>The CreatePolicyCurrentDatabase operation result.</returns>
    private static MitsubishiTagDatabase CreatePolicyCurrentDatabase()
    {
        var database = new MitsubishiTagDatabase(
        [
            new MitsubishiTagDefinition(
                MotorSpeedTagName,
                "D100",
                DataType: "Word",
                Description: MainSpindleRpmNotes,
                Scale: 0.1,
                Units: "rpm"),
            new MitsubishiTagDefinition(
                ProcessValueTagName,
                "D300",
                DataType: "Word",
                Description: RawProcessValueNotes),
            new MitsubishiTagDefinition(
                OperatorMessageTagName,
                "D600",
                DataType: StringDataType,
                Description: "Current HMI text",
                Length: 2,
                Encoding: "Utf8"),
        ]);

        database.AddGroup(
            new MitsubishiTagGroupDefinition(OverviewGroupName, [ MotorSpeedTagName, ProcessValueTagName]));
        return database;
    }

    /// <summary>Executes the CreatePolicyUpdatedDatabase operation.</summary>
    /// <returns>The CreatePolicyUpdatedDatabase operation result.</returns>
    private static MitsubishiTagDatabase CreatePolicyUpdatedDatabase()
    {
        var database = new MitsubishiTagDatabase(
        [
            new MitsubishiTagDefinition(
                MotorSpeedTagName,
                "D101",
                DataType: "Word",
                Description: MainSpindleRpmNotes,
                Scale: 0.1,
                Units: "rpm"),
            new MitsubishiTagDefinition(
                ProcessValueTagName,
                "D300",
                DataType: "Float",
                Description: "Engineering process value"),
            new MitsubishiTagDefinition(
                OperatorMessageTagName,
                "D600",
                DataType: StringDataType,
                Description: UpdatedHmiText,
                Length: 2,
                Encoding: "Utf8"),
        ]);

        database.AddGroup(
            new MitsubishiTagGroupDefinition(OverviewGroupName, [ MotorSpeedTagName, OperatorMessageTagName]));
        return database;
    }

    /// <summary>Executes the CreateMetadataAndGroupOnlyDatabase operation.</summary>
    /// <returns>The CreateMetadataAndGroupOnlyDatabase operation result.</returns>
    private static MitsubishiTagDatabase CreateMetadataAndGroupOnlyDatabase()
    {
        var database = new MitsubishiTagDatabase(
        [
            new MitsubishiTagDefinition(
                MotorSpeedTagName,
                "D100",
                DataType: "Word",
                Description: MainSpindleRpmNotes,
                Scale: 0.1,
                Units: "rpm"),
            new MitsubishiTagDefinition(
                ProcessValueTagName,
                "D300",
                DataType: "Word",
                Description: RawProcessValueNotes),
            new MitsubishiTagDefinition(
                OperatorMessageTagName,
                "D600",
                DataType: StringDataType,
                Description: UpdatedHmiText,
                Length: 2,
                Encoding: "Utf8"),
        ]);

        database.AddGroup(
            new MitsubishiTagGroupDefinition(OverviewGroupName, [ MotorSpeedTagName, OperatorMessageTagName]));
        return database;
    }

    /// <summary>Executes the CreateAddressOnlyUpdatedDatabase operation.</summary>
    /// <returns>The CreateAddressOnlyUpdatedDatabase operation result.</returns>
    private static MitsubishiTagDatabase CreateAddressOnlyUpdatedDatabase()
    {
        var database = new MitsubishiTagDatabase(
        [
            new MitsubishiTagDefinition(
                MotorSpeedTagName,
                "D101",
                DataType: "Word",
                Description: MainSpindleRpmNotes,
                Scale: 0.1,
                Units: "rpm"),
            new MitsubishiTagDefinition(
                ProcessValueTagName,
                "D300",
                DataType: "Word",
                Description: RawProcessValueNotes),
            new MitsubishiTagDefinition(
                OperatorMessageTagName,
                "D600",
                DataType: StringDataType,
                Description: "Current HMI text",
                Length: 2,
                Encoding: "Utf8"),
        ]);

        database.AddGroup(
            new MitsubishiTagGroupDefinition(OverviewGroupName, [ MotorSpeedTagName, ProcessValueTagName]));
        return database;
    }

    /// <summary>Executes the CreateTempPath operation.</summary>
    /// <param name="extension">The extension parameter.</param>
    /// <returns>The CreateTempPath operation result.</returns>
    private static string CreateTempPath(string extension)
        => Path.Combine(Path.GetTempPath(), $"mitsubishirx-policy-{Guid.NewGuid():N}.{extension}");

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
