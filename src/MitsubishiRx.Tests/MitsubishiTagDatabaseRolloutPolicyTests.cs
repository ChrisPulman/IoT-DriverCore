// Copyright (c) 2019-2026 Chris Pulman and contributors. All rights reserved.
// Chris Pulman and contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

#if REACTIVE_SHIM

namespace IoT.Driver.MitsubishiRx.Reactive.Tests;
#else

namespace IoT.Driver.MitsubishiRx.Tests;
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

        await Assert.That((diff.ChangeKinds & MitsubishiSchemaChangeKind.MetadataOnly) == MitsubishiSchemaChangeKind.MetadataOnly).IsTrue();
        await Assert.That((diff.ChangeKinds & MitsubishiSchemaChangeKind.AddressChange) == MitsubishiSchemaChangeKind.AddressChange).IsTrue();
        await Assert.That((diff.ChangeKinds & MitsubishiSchemaChangeKind.DataTypeChange) == MitsubishiSchemaChangeKind.DataTypeChange).IsTrue();
        await Assert.That((diff.ChangeKinds & MitsubishiSchemaChangeKind.GroupMembershipChange) == MitsubishiSchemaChangeKind.GroupMembershipChange).IsTrue();

        var metadataChange = GetSingle(diff.ChangedTags, static change => change.Name == OperatorMessageTagName);
        var addressChange = GetSingle(diff.ChangedTags, static change => change.Name == MotorSpeedTagName);
        var dataTypeChange = GetSingle(diff.ChangedTags, static change => change.Name == ProcessValueTagName);

        await Assert.That(metadataChange.ChangeKinds).IsEqualTo(MitsubishiSchemaChangeKind.MetadataOnly);
        await Assert.That((addressChange.ChangeKinds & MitsubishiSchemaChangeKind.AddressChange) == MitsubishiSchemaChangeKind.AddressChange).IsTrue();
        await Assert.That((dataTypeChange.ChangeKinds & MitsubishiSchemaChangeKind.DataTypeChange) == MitsubishiSchemaChangeKind.DataTypeChange).IsTrue();
        await Assert.That(GetSingle(diff.ChangedGroups).ChangeKinds)
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
            await Assert.That((result.Value!.ChangeKinds & MitsubishiSchemaChangeKind.AddressChange) == MitsubishiSchemaChangeKind.AddressChange).IsTrue();
            await Assert.That((result.Value.ChangeKinds & MitsubishiSchemaChangeKind.DataTypeChange) == MitsubishiSchemaChangeKind.DataTypeChange).IsTrue();
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

        return new(options, null, scheduler);
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
            new(OverviewGroupName, [ MotorSpeedTagName, ProcessValueTagName]));
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
            new(OverviewGroupName, [ MotorSpeedTagName, OperatorMessageTagName]));
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
            new(OverviewGroupName, [ MotorSpeedTagName, OperatorMessageTagName]));
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
            new(OverviewGroupName, [ MotorSpeedTagName, ProcessValueTagName]));
        return database;
    }

    /// <summary>Executes the CreateTempPath operation.</summary>
    /// <param name="extension">The extension parameter.</param>
    /// <returns>The CreateTempPath operation result.</returns>
    private static string CreateTempPath(string extension)
        => Path.Combine(Path.GetTempPath(), $"mitsubishirx-policy-{Guid.NewGuid():N}.{extension}");

    /// <summary>Gets the sole item that satisfies a condition.</summary>
    /// <typeparam name="T">The item type.</typeparam>
    /// <param name="values">The values to inspect.</param>
    /// <param name="predicate">The condition that the item must satisfy.</param>
    /// <returns>The sole matching item.</returns>
    private static T GetSingle<T>(IEnumerable<T> values, Func<T, bool> predicate)
    {
        var found = false;
        var result = default(T)!;

        foreach (var value in values)
        {
            if (!predicate(value))
            {
                continue;
            }

            if (found)
            {
                throw new InvalidOperationException("Sequence contains more than one matching element.");
            }

            result = value;
            found = true;
        }

        return found ? result : throw new InvalidOperationException("Sequence contains no matching element.");
    }

    /// <summary>Gets the sole item in a collection.</summary>
    /// <typeparam name="T">The item type.</typeparam>
    /// <param name="values">The values to inspect.</param>
    /// <returns>The sole item.</returns>
    private static T GetSingle<T>(IEnumerable<T> values)
    {
        using var enumerator = values.GetEnumerator();
        if (!enumerator.MoveNext())
        {
            throw new InvalidOperationException("Sequence contains no elements.");
        }

        var result = enumerator.Current;
        if (enumerator.MoveNext())
        {
            throw new InvalidOperationException("Sequence contains more than one element.");
        }

        return result;
    }

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
