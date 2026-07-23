// Copyright (c) 2019-2026 Chris Pulman and contributors. All rights reserved.
// Chris Pulman and contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

#if REACTIVE_SHIM

namespace IoT.DriverCore.MitsubishiRx.Reactive.Tests;

#else

namespace IoT.DriverCore.MitsubishiRx.Tests;

#endif

/// <summary>Completes tag-database validation, CSV, format, and diff coverage.</summary>
internal sealed class MitsubishiTagDatabaseCompletionTests
{
    /// <summary>Stores the expected number of structural and semantic changes.</summary>
    private const int ExpectedChangeCount = 6;

    /// <summary>Stores the unsigned word data type.</summary>
    private const string UInt16DataType = "UInt16";

    /// <summary>Stores the removed diff tag name.</summary>
    private const string RemovedTagName = "Removed";

    /// <summary>Stores the added diff tag name.</summary>
    private const string AddedTagName = "Added";

    /// <summary>Stores the changed diff tag name.</summary>
    private const string ChangedTagName = "Changed";

    /// <summary>Exercises direct tag/group validation and required lookup failures.</summary>
    /// <returns>A task that represents the asynchronous test.</returns>
    [Test]
    internal async Task DirectMutationGuardsRejectInvalidDefinitionsAsync()
    {
        var database = new MitsubishiTagDatabase([]);
        database.Add(new MitsubishiTagDefinition("Plain", "D0"));

        _ = Assert.Throws<ArgumentNullException>(() => database.Add(null!));
        _ = Assert.Throws<ArgumentException>(
            () => database.Add(new MitsubishiTagDefinition(" ", "D0")));
        _ = Assert.Throws<ArgumentException>(
            () => database.Add(new MitsubishiTagDefinition("NoAddress", " ")));
        _ = Assert.Throws<ArgumentOutOfRangeException>(
            () => database.Add(new MitsubishiTagDefinition("Length", "D0", Length: 0)));
        _ = Assert.Throws<FormatException>(
            () => database.Add(new MitsubishiTagDefinition("BadType", "D0", "Unsupported")));
        _ = Assert.Throws<FormatException>(
            () => database.Add(new MitsubishiTagDefinition("Encoding", "D0", Encoding: "EBCDIC")));
        _ = Assert.Throws<FormatException>(
            () => database.Add(new MitsubishiTagDefinition("Order", "D0", ByteOrder: "Middle")));
        _ = Assert.Throws<KeyNotFoundException>(() => database.GetRequired("Missing"));
        _ = Assert.Throws<KeyNotFoundException>(() => database.GetRequiredGroup("Missing"));
        _ = Assert.Throws<ArgumentException>(
            () => database.AddGroup(new MitsubishiTagGroupDefinition("Empty", [])));
        _ = Assert.Throws<ArgumentException>(
            () => database.AddGroup(new MitsubishiTagGroupDefinition("Blank", [" "])));
        _ = Assert.Throws<ArgumentNullException>(() => database.AddGroup(null!));

        await Assert.That(database.GetRequired("Plain").DataType).IsNull();
    }

    /// <summary>Exercises CSV quoting, empty rows, and required/normalized field errors.</summary>
    /// <returns>A task that represents the asynchronous test.</returns>
    [Test]
    internal async Task CsvParserCoversQuotedAndMalformedRowsAsync()
    {
        var quoted = MitsubishiTagDatabase.FromCsv(
            """
            Name,Address,Description,Groups
            Quoted,D0,"A ""quoted"", value",Group%20One
            """);
        var empty = MitsubishiTagDatabase.FromCsv(
            """
            Name,Address
            ,,
            """);

        await Assert.That(quoted.GetRequired("Quoted").Description)
            .IsEqualTo("A \"quoted\", value");
        await Assert.That(quoted.GetRequiredGroup("Group One").ResolvedTagNames)
            .IsEquivalentTo(["Quoted"]);
        await Assert.That(empty.Tags).IsEmpty();

        _ = Assert.Throws<FormatException>(() => MitsubishiTagDatabase.FromCsv("Name,Address"));
        _ = Assert.Throws<FormatException>(
            () => MitsubishiTagDatabase.FromCsv("Name\nOnly"));
        _ = Assert.Throws<FormatException>(
            () => MitsubishiTagDatabase.FromCsv("Name,Address\nOnly,"));
        _ = Assert.Throws<FormatException>(
            () => MitsubishiTagDatabase.FromCsv("Name,Address,DataType\nOnly,D0,Bad"));
        _ = Assert.Throws<FormatException>(
            () => MitsubishiTagDatabase.FromCsv("Name,Address,Encoding\nOnly,D0,Bad"));
        _ = Assert.Throws<FormatException>(
            () => MitsubishiTagDatabase.FromCsv("Name,Address,ByteOrder\nOnly,D0,Bad"));
    }

    /// <summary>Exercises unsupported file paths for both persistence directions.</summary>
    /// <returns>A task that represents the asynchronous test.</returns>
    [Test]
    internal async Task PersistenceRejectsMissingAndUnsupportedExtensionsAsync()
    {
        var database = new MitsubishiTagDatabase(
        [
            new MitsubishiTagDefinition("Value", "D0", UInt16DataType),
        ]);
        var noExtension = Path.Combine(Path.GetTempPath(), $"mitsubishi-{Guid.NewGuid():N}");
        var unsupported = $"{noExtension}.xml";

        _ = Assert.Throws<NotSupportedException>(() => database.Save(noExtension));
        _ = Assert.Throws<NotSupportedException>(() => database.Save(unsupported));
        await File.WriteAllTextAsync(noExtension, "tags", CancellationToken.None);
        await File.WriteAllTextAsync(unsupported, "<tags />", CancellationToken.None);
        try
        {
            _ = Assert.Throws<NotSupportedException>(() => MitsubishiTagDatabase.Load(noExtension));
            _ = Assert.Throws<NotSupportedException>(() => MitsubishiTagDatabase.Load(unsupported));
        }
        finally
        {
            File.Delete(noExtension);
            File.Delete(unsupported);
        }

        await Assert.That(database.Tags).Count().IsEqualTo(1);
    }

    /// <summary>Exercises all diff collections and aggregate change-kind properties.</summary>
    /// <returns>A task that represents the asynchronous test.</returns>
    [Test]
    internal async Task DiffAggregatesStructureAndSemanticChangesAsync()
    {
        var previous = CreateDiffDatabase(
            RemovedTagName,
            "D0",
            "OldGroup",
            [RemovedTagName, ChangedTagName]);
        previous.Add(new MitsubishiTagDefinition(ChangedTagName, "D1", UInt16DataType));
        var current = CreateDiffDatabase(
            AddedTagName,
            "D2",
            "NewGroup",
            [AddedTagName, ChangedTagName]);
        current.Add(new MitsubishiTagDefinition(ChangedTagName, "D3", UInt16DataType));
        previous.AddGroup(new MitsubishiTagGroupDefinition("ChangedGroup", [RemovedTagName]));
        current.AddGroup(new MitsubishiTagGroupDefinition("ChangedGroup", [AddedTagName]));

        var diff = previous.CompareWith(current);

        await Assert.That(diff.ChangeCount).IsEqualTo(ExpectedChangeCount);
        await Assert.That(diff.HasChanges).IsTrue();
        await Assert.That(diff.ChangeKinds.HasFlag(MitsubishiSchemaChangeKind.StructureChange)).IsTrue();
        await Assert.That(diff.ChangedTags[0].ChangeKinds).IsNotEqualTo(MitsubishiSchemaChangeKind.None);
        await Assert.That(diff.ChangedGroups[0].ChangeKinds).IsNotEqualTo(MitsubishiSchemaChangeKind.None);
        await Assert.That(MitsubishiTagDatabaseDiff.Empty.HasChanges).IsFalse();
        await Assert.That(MitsubishiTagDatabaseDiff.Empty.ChangeCount).IsEqualTo(0);
        await Assert.That(MitsubishiTagDatabaseDiff.Empty.ChangeKinds)
            .IsEqualTo(MitsubishiSchemaChangeKind.None);
        _ = Assert.Throws<ArgumentNullException>(() => previous.CompareWith(null!));
    }

    /// <summary>Creates a database with one tag and one group.</summary>
    /// <param name="tagName">The tag name.</param>
    /// <param name="address">The tag address.</param>
    /// <param name="groupName">The group name.</param>
    /// <param name="members">The group members.</param>
    /// <returns>The database.</returns>
    private static MitsubishiTagDatabase CreateDiffDatabase(
        string tagName,
        string address,
        string groupName,
        string[] members)
    {
        var database = new MitsubishiTagDatabase(
        [
            new MitsubishiTagDefinition(tagName, address, UInt16DataType),
        ]);
        database.AddGroup(new MitsubishiTagGroupDefinition(groupName, members));
        return database;
    }
}
