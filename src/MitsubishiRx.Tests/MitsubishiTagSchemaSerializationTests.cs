// Copyright (c) 2019-2026 Chris Pulman and contributors. All rights reserved.
// Chris Pulman and contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

#if REACTIVE_SHIM

namespace IoT.DriverCore.MitsubishiRx.Reactive.Tests;
#else

namespace IoT.DriverCore.MitsubishiRx.Tests;
#endif

/// <summary>Provides the MitsubishiTagSchemaSerializationTests type.</summary>
internal sealed class MitsubishiTagSchemaSerializationTests
{
    /// <summary>Stores the expected CSV tag count.</summary>
    private const int CsvTagCount = 2;

    /// <summary>Stores the expected round-tripped group count.</summary>
    private const int RoundTrippedGroupCount = 2;

    /// <summary>Stores the expected round-tripped tag count.</summary>
    private const int RoundTrippedTagCount = 3;

    /// <summary>Stores the operator-message register length.</summary>
    private const int OperatorMessageLength = 2;

    /// <summary>Stores the <c>SignedTempTagName</c> test value.</summary>
    private const string SignedTempTagName = "SignedTemp";

    /// <summary>Stores the <c>OperatorMessageTagName</c> test value.</summary>
    private const string OperatorMessageTagName = "OperatorMessage";

    /// <summary>Stores the <c>OverviewGroupName</c> test value.</summary>
    private const string OverviewGroupName = "Overview";

    /// <summary>Stores the <c>TotalCountTagName</c> test value.</summary>
    private const string TotalCountTagName = "TotalCount";

    /// <summary>Executes the ToJsonAndFromJsonRoundTripPreservesTagsAndGroups operation.</summary>
    /// <returns>The ToJsonAndFromJsonRoundTripPreservesTagsAndGroups operation result.</returns>
    [Test]
    internal async Task ToJsonAndFromJsonRoundTripPreservesTagsAndGroupsAsync()
    {
        var database = CreateSchemaDatabase();

        var json = database.ToJson();
        var roundTripped = MitsubishiTagDatabase.FromJson(json);

        await Assert.That(json.Contains("\"groups\"", StringComparison.OrdinalIgnoreCase)).IsTrue();
        await Assert.That(roundTripped.Count).IsEqualTo(RoundTrippedTagCount);
        await Assert.That(roundTripped.GroupCount).IsEqualTo(1);

        var signedTemp = roundTripped.GetRequired(SignedTempTagName);
        await Assert.That(signedTemp.DataType).IsEqualTo("Int16");
        await Assert.That(signedTemp.Signed).IsTrue();

        var operatorMessage = roundTripped.GetRequired(OperatorMessageTagName);
        await Assert.That(operatorMessage.Length).IsEqualTo(OperatorMessageLength);
        await Assert.That(operatorMessage.Encoding).IsEqualTo("Utf8");
        await Assert.That(operatorMessage.ByteOrder).IsEqualTo("BigEndian");

        var group = roundTripped.GetRequiredGroup(OverviewGroupName);
        await Assert.That(group.ResolvedTagNames)
            .IsEquivalentTo([ SignedTempTagName, TotalCountTagName, OperatorMessageTagName]);
    }

    /// <summary>Executes the ToYamlAndFromYamlRoundTripPreservesTagsAndGroups operation.</summary>
    /// <returns>The ToYamlAndFromYamlRoundTripPreservesTagsAndGroups operation result.</returns>
    [Test]
    internal async Task ToYamlAndFromYamlRoundTripPreservesTagsAndGroupsAsync()
    {
        var database = CreateSchemaDatabase();

        var yaml = database.ToYaml();
        var roundTripped = MitsubishiTagDatabase.FromYaml(yaml);

        await Assert.That(yaml.Contains("groups:", StringComparison.OrdinalIgnoreCase)).IsTrue();
        await Assert.That(roundTripped.Count).IsEqualTo(RoundTrippedTagCount);
        await Assert.That(roundTripped.GroupCount).IsEqualTo(1);
        await Assert.That(roundTripped.GetRequired(TotalCountTagName).ByteOrder).IsEqualTo("LittleEndian");
        await Assert.That(roundTripped.GetRequiredGroup(OverviewGroupName).ResolvedTagNames[0])
            .IsEqualTo(SignedTempTagName);
    }

    /// <summary>Verifies CSV persistence retains membership in multiple named groups.</summary>
    /// <returns>The ToCsvAndFromCsvRoundTripPreservesGroupMembership operation result.</returns>
    [Test]
    internal async Task ToCsvAndFromCsvRoundTripPreservesGroupMembershipAsync()
    {
        var database = CreateSchemaDatabase();
        database.AddGroup(new MitsubishiTagGroupDefinition("Operator|View", [OperatorMessageTagName]));

        var csv = database.ToCsv();
        var roundTripped = MitsubishiTagDatabase.FromCsv(csv);

        await Assert.That(csv.Contains(",Groups", StringComparison.Ordinal)).IsTrue();
        await Assert.That(roundTripped.GroupCount).IsEqualTo(RoundTrippedGroupCount);
        await Assert.That(roundTripped.GetRequiredGroup(OverviewGroupName).ResolvedTagNames)
            .IsEquivalentTo([ SignedTempTagName, TotalCountTagName, OperatorMessageTagName]);
        await Assert.That(roundTripped.GetRequiredGroup("Operator|View").ResolvedTagNames)
            .IsEquivalentTo([ OperatorMessageTagName]);
    }

    /// <summary>Executes the FromYamlParsesManualSchemaDocumentWithGroups operation.</summary>
    /// <returns>The FromYamlParsesManualSchemaDocumentWithGroups operation result.</returns>
    [Test]
    internal async Task FromYamlParsesManualSchemaDocumentWithGroupsAsync()
    {
        const string yaml = """
        tags:
          - name: SignedTemp
            address: D700
            dataType: Int16
            signed: true
            units: °C
          - name: OperatorMessage
            address: D600
            dataType: String
            length: 2
            encoding: Utf8
            byteOrder: BigEndian
        groups:
          - name: Overview
            tagNames:
              - SignedTemp
              - OperatorMessage
        """;

        var database = MitsubishiTagDatabase.FromYaml(yaml);

        await Assert.That(database.Count).IsEqualTo(CsvTagCount);
        await Assert.That(database.GroupCount).IsEqualTo(1);
        await Assert.That(database.GetRequired(SignedTempTagName).Signed).IsTrue();
        await Assert.That(database.GetRequired(OperatorMessageTagName).Encoding).IsEqualTo("Utf8");
        await Assert.That(database.GetRequiredGroup(OverviewGroupName).ResolvedTagNames)
            .IsEquivalentTo([ SignedTempTagName, OperatorMessageTagName]);
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
}
