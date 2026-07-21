// Copyright (c) 2019-2026 Chris Pulman and contributors. All rights reserved.
// Chris Pulman and contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

#if REACTIVE_SHIM

namespace MitsubishiRx.Reactive.Tests;
#else

namespace MitsubishiRx.Tests;
#endif

/// <summary>Provides the MitsubishiTagSchemaFileIoTests type.</summary>
internal sealed class MitsubishiTagSchemaFileIoTests
{
    /// <summary>Stores the CSV operator-message register length.</summary>
    private const int CsvOperatorMessageLength = 4;

    /// <summary>Stores the expected CSV tag count.</summary>
    private const int CsvTagCount = 2;

    /// <summary>Stores the expected standard schema tag count.</summary>
    private const int StandardSchemaTagCount = 3;

    /// <summary>Stores the <c>OperatorMessageTagName</c> test value.</summary>
    private const string OperatorMessageTagName = "OperatorMessage";

    /// <summary>Stores the <c>OverviewGroupName</c> test value.</summary>
    private const string OverviewGroupName = "Overview";

    /// <summary>Stores the <c>SignedTempTagName</c> test value.</summary>
    private const string SignedTempTagName = "SignedTemp";

    /// <summary>Stores the <c>TotalCountTagName</c> test value.</summary>
    private const string TotalCountTagName = "TotalCount";

    /// <summary>Stores the <c>MotorSpeedTagName</c> test value.</summary>
    private const string MotorSpeedTagName = "MotorSpeed";

    /// <summary>Executes the SaveAndLoadJsonRoundTripPreservesTagsAndGroups operation.</summary>
    /// <returns>The SaveAndLoadJsonRoundTripPreservesTagsAndGroups operation result.</returns>
    [Test]
    internal async Task SaveAndLoadJsonRoundTripPreservesTagsAndGroupsAsync()
    {
        var database = CreateSchemaDatabase();
        var path = CreateTempPath("json");

        try
        {
            database.Save(path);
            var loaded = MitsubishiTagDatabase.Load(path);

            await Assert.That(File.Exists(path)).IsTrue();
            await Assert.That(loaded.Count).IsEqualTo(StandardSchemaTagCount);
            await Assert.That(loaded.GroupCount).IsEqualTo(1);
            await Assert.That(loaded.GetRequired(OperatorMessageTagName).Encoding).IsEqualTo("Utf8");
            await Assert.That(loaded.GetRequiredGroup(OverviewGroupName).ResolvedTagNames)
                .IsEquivalentTo([ SignedTempTagName, TotalCountTagName, OperatorMessageTagName]);
        }
        finally
        {
            DeleteIfExists(path);
        }
    }

    /// <summary>Executes the SaveAndLoadYamlRoundTripPreservesTagsAndGroups operation.</summary>
    /// <returns>The SaveAndLoadYamlRoundTripPreservesTagsAndGroups operation result.</returns>
    [Test]
    internal async Task SaveAndLoadYamlRoundTripPreservesTagsAndGroupsAsync()
    {
        var database = CreateSchemaDatabase();
        var path = CreateTempPath("yaml");

        try
        {
            database.Save(path);
            var loaded = MitsubishiTagDatabase.Load(path);

            var contents = await File.ReadAllTextAsync(path, CancellationToken.None);
            await Assert.That(contents.Contains("groups:", StringComparison.OrdinalIgnoreCase)).IsTrue();
            await Assert.That(loaded.Count).IsEqualTo(StandardSchemaTagCount);
            await Assert.That(loaded.GroupCount).IsEqualTo(1);
            await Assert.That(loaded.GetRequired(TotalCountTagName).ByteOrder).IsEqualTo("LittleEndian");
        }
        finally
        {
            DeleteIfExists(path);
        }
    }

    /// <summary>Executes the SaveAndLoadYamlWithYmlExtensionUsesYamlFormatDetection operation.</summary>
    /// <returns>The SaveAndLoadYamlWithYmlExtensionUsesYamlFormatDetection operation result.</returns>
    [Test]
    internal async Task SaveAndLoadYamlWithYmlExtensionUsesYamlFormatDetectionAsync()
    {
        var database = CreateSchemaDatabase();
        var path = CreateTempPath("yml");

        try
        {
            database.Save(path);
            var loaded = MitsubishiTagDatabase.Load(path);

            var contents = await File.ReadAllTextAsync(path, CancellationToken.None);
            await Assert.That(contents.Contains("groups:", StringComparison.OrdinalIgnoreCase)).IsTrue();
            await Assert.That(loaded.Count).IsEqualTo(StandardSchemaTagCount);
            await Assert.That(loaded.GroupCount).IsEqualTo(1);
            await Assert.That(loaded.GetRequired(OperatorMessageTagName).ByteOrder).IsEqualTo("BigEndian");
        }
        finally
        {
            DeleteIfExists(path);
        }
    }

    /// <summary>Executes the SaveAndLoadCsvUsesCsvFormatDetection operation.</summary>
    /// <returns>The SaveAndLoadCsvUsesCsvFormatDetection operation result.</returns>
    [Test]
    internal async Task SaveAndLoadCsvUsesCsvFormatDetectionAsync()
    {
        var database = new MitsubishiTagDatabase(
        [
            new MitsubishiTagDefinition(MotorSpeedTagName, "D100", DataType: "Word", Scale: 0.1, Units: "rpm"),
            new MitsubishiTagDefinition(
                OperatorMessageTagName,
                "D600",
                DataType: "String",
                Length: 4,
                Encoding: "Utf8",
                Notes: "Shown on HMI"),
        ]);

        database.AddGroup(
            new MitsubishiTagGroupDefinition(OverviewGroupName, [ MotorSpeedTagName, OperatorMessageTagName]));
        var path = CreateTempPath("csv");

        try
        {
            database.Save(path);
            var loaded = MitsubishiTagDatabase.Load(path);

            var csv = await File.ReadAllTextAsync(path, CancellationToken.None);
            await Assert.That(csv.Contains("Name,Address,DataType", StringComparison.Ordinal)).IsTrue();
            await Assert.That(loaded.Count).IsEqualTo(CsvTagCount);
            await Assert.That(loaded.GroupCount).IsEqualTo(1);
            await Assert.That(loaded.GetRequired(MotorSpeedTagName).Units).IsEqualTo("rpm");
            await Assert.That(loaded.GetRequired(OperatorMessageTagName).Length)
                .IsEqualTo(CsvOperatorMessageLength);
        }
        finally
        {
            DeleteIfExists(path);
        }
    }

    /// <summary>Executes the LoadRejectsUnsupportedSchemaExtensions operation.</summary>
    /// <returns>The LoadRejectsUnsupportedSchemaExtensions operation result.</returns>
    [Test]
    internal async Task LoadRejectsUnsupportedSchemaExtensionsAsync()
    {
        var path = CreateTempPath("txt");

        try
        {
            await File.WriteAllTextAsync(path, "unsupported", CancellationToken.None);

            var exception = Assert.Throws<NotSupportedException>(
                                () => MitsubishiTagDatabase.Load(path))
                            ?? throw new InvalidOperationException(
                                "Expected Load to reject unsupported schema extensions.");

            await Assert.That(exception.Message.Contains(".txt", StringComparison.OrdinalIgnoreCase)).IsTrue();
        }
        finally
        {
            DeleteIfExists(path);
        }
    }

    /// <summary>Executes the CreateSchemaDatabase operation.</summary>
    /// <returns>The CreateSchemaDatabase operation result.</returns>
    private static MitsubishiTagDatabase CreateSchemaDatabase()
    {
        var database = new MitsubishiTagDatabase(
        [
            new MitsubishiTagDefinition(SignedTempTagName, "D700", DataType: "Int16", Units: "°C", Signed: true),
            new MitsubishiTagDefinition(
                TotalCountTagName,
                "D400",
                DataType: "UInt32",
                Units: "items",
                ByteOrder: "LittleEndian"),
            new MitsubishiTagDefinition(
                OperatorMessageTagName,
                "D600",
                DataType: "String",
                Length: 2,
                Encoding: "Utf8",
                ByteOrder: "BigEndian"),
        ]);

        database.AddGroup(
            new MitsubishiTagGroupDefinition(
                OverviewGroupName,
                [ SignedTempTagName, TotalCountTagName, OperatorMessageTagName]));
        return database;
    }

    /// <summary>Executes the CreateTempPath operation.</summary>
    /// <param name="extension">The extension parameter.</param>
    /// <returns>The CreateTempPath operation result.</returns>
    private static string CreateTempPath(string extension)
        => Path.Combine(Path.GetTempPath(), $"mitsubishirx-schema-{Guid.NewGuid():N}.{extension}");

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
