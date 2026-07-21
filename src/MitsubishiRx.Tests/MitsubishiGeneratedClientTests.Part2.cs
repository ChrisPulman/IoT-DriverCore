// Copyright (c) 2019-2026 Chris Pulman and contributors. All rights reserved.
// Chris Pulman and contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using CP.IoT.Core;

#if REACTIVE_SHIM

namespace MitsubishiRx.Reactive.Tests;
#else

namespace MitsubishiRx.Tests;
#endif

/// <summary>Provides additional Mitsubishi generated-client tests.</summary>
internal sealed partial class MitsubishiGeneratedClientTests
{
    /// <summary>Stores the generated-client group name.</summary>
    private const string Line1GroupName = "Line1";

    /// <summary>Stores the generated-client tag name.</summary>
    private const string MotorSpeedTagName = "MotorSpeed";

    /// <summary>Stores the sample motor-speed value used by optional snapshot tests.</summary>
    private const float OptionalSnapshotMotorSpeed = 123.4F;

    /// <summary>Executes the IncrementalGeneratorSanitizesInvalidIdentifiers operation.</summary>
    /// <returns>The IncrementalGeneratorSanitizesInvalidIdentifiers operation result.</returns>
    [Test]
    internal async Task IncrementalGeneratorSanitizesInvalidIdentifiersAsync()
    {
        const string schema = """
        {
          "tags": [
            {
              "name": "Motor Speed",
              "address": "D100",
              "dataType": "Float"
            },
            {
              "name": "9Mode",
              "address": "D101",
              "dataType": "UInt16"
            }
          ],
          "groups": [
            {
              "name": "Line 1 Overview",
              "tagNames": ["Motor Speed", "9Mode"]
            }
          ]
        }
        """;

        var source = $$"""
        using MitsubishiRx;

        /// <summary>Provides the SchemaMarker type.</summary>
        [MitsubishiTagClientSchema({{ToLiteral(schema)}})]
        internal sealed class SchemaMarker { }
        """;

        var generated = RunGenerator(source);

        await Assert.That(generated.Contains("public MotorSpeedTag MotorSpeed => new(_owner);")).IsTrue();
        await Assert.That(generated.Contains("public _9ModeTag _9Mode => new(_owner);")).IsTrue();
        await Assert.That(generated.Contains("public Line1OverviewGroup Line1Overview => new(_owner);")).IsTrue();
    }

    /// <summary>Tests optional snapshot helpers with missing or incorrectly typed values.</summary>
    /// <returns>
    /// The IncrementalGeneratorOptionalSnapshotHelpersReturnNullWhenValuesAreMissingOrWrongType operation result.
    /// </returns>
    [Test]
    internal async Task IncrementalGeneratorOptionalSnapshotHelpersReturnNullWhenValuesAreMissingOrWrongTypeAsync()
    {
        var missingMode = new MitsubishiTagGroupSnapshot(
            Line1GroupName,
            new Dictionary<string, object?>
            {
                [MotorSpeedTagName] = OptionalSnapshotMotorSpeed,
            });
        var wrongMode = new MitsubishiTagGroupSnapshot(
            Line1GroupName,
            new Dictionary<string, object?>
            {
                [MotorSpeedTagName] = OptionalSnapshotMotorSpeed,
                ["Mode"] = "bad-type",
            });

        await Assert.That(missingMode.GetOptional(new LogicalTagKey<ushort>("Mode"))).IsEqualTo(default(ushort));
        await Assert.That(wrongMode.GetOptional(new LogicalTagKey<ushort>("Mode"))).IsEqualTo(default(ushort));
    }

    /// <summary>Executes the IncrementalGeneratorReportsDiagnosticForDuplicateTagNames operation.</summary>
    /// <returns>The IncrementalGeneratorReportsDiagnosticForDuplicateTagNames operation result.</returns>
    [Test]
    internal async Task IncrementalGeneratorReportsDiagnosticForDuplicateTagNamesAsync()
    {
        const string schema = $$"""
        {
          "tags": [
            { "name": "{{MotorSpeedTagName}}", "address": "D100", "dataType": "Float" },
            { "name": "{{MotorSpeedTagName}}", "address": "D101", "dataType": "UInt16" }
          ],
          "groups": []
        }
        """;

        var source = $$"""
        using MitsubishiRx;

        /// <summary>Provides the SchemaMarker type.</summary>
        [MitsubishiTagClientSchema({{ToLiteral(schema)}})]
        internal sealed class SchemaMarker { }
        """;

        var result = RunGeneratorCompilation(source);
        var diagnostics = result.Diagnostics.Where(static d => d.Id == "MRTXGEN002").ToArray();

        if (diagnostics.Length == 0)
        {
            throw new InvalidOperationException(
                string.Join(
                    Environment.NewLine,
                    result.Diagnostics.Select(static diagnostic => diagnostic.ToString())));
        }

        await Assert.That(
                diagnostics.Any(
                    static d => d.GetMessage().Contains(MotorSpeedTagName, StringComparison.Ordinal)))
            .IsTrue();
    }

    /// <summary>Executes the IncrementalGeneratorReportsDiagnosticForUnknownGroupTagReference operation.</summary>
    /// <returns>The IncrementalGeneratorReportsDiagnosticForUnknownGroupTagReference operation result.</returns>
    [Test]
    internal async Task IncrementalGeneratorReportsDiagnosticForUnknownGroupTagReferenceAsync()
    {
        const string schema = $$"""
        {
          "tags": [
            { "name": "{{MotorSpeedTagName}}", "address": "D100", "dataType": "Float" }
          ],
          "groups": [
            { "name": "{{Line1GroupName}}", "tagNames": ["{{MotorSpeedTagName}}", "MissingTag"] }
          ]
        }
        """;

        var source = $$"""
        using MitsubishiRx;

        /// <summary>Provides the SchemaMarker type.</summary>
        [MitsubishiTagClientSchema({{ToLiteral(schema)}})]
        internal sealed class SchemaMarker { }
        """;

        var result = RunGeneratorCompilation(source);
        var diagnostics = result.Diagnostics.Where(static d => d.Id == "MRTXGEN003").ToArray();

        if (diagnostics.Length == 0)
        {
            throw new InvalidOperationException(
                string.Join(
                    Environment.NewLine,
                    result.Diagnostics.Select(static diagnostic => diagnostic.ToString())));
        }

        await Assert.That(
                diagnostics.Any(
                    static d => d.GetMessage().Contains("MissingTag", StringComparison.Ordinal)))
            .IsTrue();
    }

    /// <summary>Executes the IncrementalGeneratorReportsDiagnosticForUnsupportedDataType operation.</summary>
    /// <returns>The IncrementalGeneratorReportsDiagnosticForUnsupportedDataType operation result.</returns>
    [Test]
    internal async Task IncrementalGeneratorReportsDiagnosticForUnsupportedDataTypeAsync()
    {
        const string schema = $$"""
        {
          "tags": [
            { "name": "{{MotorSpeedTagName}}", "address": "D100", "dataType": "Decimal128" }
          ],
          "groups": []
        }
        """;

        var source = $$"""
        using MitsubishiRx;

        /// <summary>Provides the SchemaMarker type.</summary>
        [MitsubishiTagClientSchema({{ToLiteral(schema)}})]
        internal sealed class SchemaMarker { }
        """;

        var result = RunGeneratorCompilation(source);
        var diagnostics = result.Diagnostics.Where(static d => d.Id == "MRTXGEN004").ToArray();

        if (diagnostics.Length == 0)
        {
            throw new InvalidOperationException(
                string.Join(
                    Environment.NewLine,
                    result.Diagnostics.Select(static diagnostic => diagnostic.ToString())));
        }

        await Assert.That(
                diagnostics.Any(
                    static d => d.GetMessage().Contains("Decimal128", StringComparison.Ordinal)))
            .IsTrue();
    }

    /// <summary>Tests generator diagnostics for sanitized identifier collisions.</summary>
    /// <returns>The IncrementalGeneratorReportsDiagnosticForSanitizedIdentifierCollisions operation result.</returns>
    [Test]
    internal async Task IncrementalGeneratorReportsDiagnosticForSanitizedIdentifierCollisionsAsync()
    {
        const string schema = """
        {
          "tags": [
            { "name": "Motor Speed", "address": "D100", "dataType": "Float" },
            { "name": "Motor-Speed", "address": "D101", "dataType": "UInt16" }
          ],
          "groups": []
        }
        """;

        var source = $$"""
        using MitsubishiRx;

        /// <summary>Provides the SchemaMarker type.</summary>
        [MitsubishiTagClientSchema({{ToLiteral(schema)}})]
        internal sealed class SchemaMarker { }
        """;

        var result = RunGeneratorCompilation(source);
        var diagnostics = result.Diagnostics.Where(static d => d.Id == "MRTXGEN005").ToArray();

        if (diagnostics.Length == 0)
        {
            throw new InvalidOperationException(
                string.Join(
                    Environment.NewLine,
                    result.Diagnostics.Select(static diagnostic => diagnostic.ToString())));
        }

        await Assert.That(
                diagnostics.Any(
                    static d => d.GetMessage().Contains(MotorSpeedTagName, StringComparison.Ordinal)))
            .IsTrue();
    }

    /// <summary>Executes the IncrementalGeneratorReportsDiagnosticForEmptyTagName operation.</summary>
    /// <returns>The IncrementalGeneratorReportsDiagnosticForEmptyTagName operation result.</returns>
    [Test]
    internal async Task IncrementalGeneratorReportsDiagnosticForEmptyTagNameAsync()
    {
        const string schema = """
        {
          "tags": [
            { "name": "", "address": "D100", "dataType": "Float" }
          ],
          "groups": []
        }
        """;

        var source = $$"""
        using MitsubishiRx;

        /// <summary>Provides the SchemaMarker type.</summary>
        [MitsubishiTagClientSchema({{ToLiteral(schema)}})]
        internal sealed class SchemaMarker { }
        """;

        var result = RunGeneratorCompilation(source);
        var diagnostics = result.Diagnostics.Where(static d => d.Id == "MRTXGEN006").ToArray();

        if (diagnostics.Length == 0)
        {
            throw new InvalidOperationException(
                string.Join(
                    Environment.NewLine,
                    result.Diagnostics.Select(static diagnostic => diagnostic.ToString())));
        }

        await Assert.That(
                diagnostics.Any(
                    static d => d.GetMessage().Contains("Tag name", StringComparison.Ordinal)))
            .IsTrue();
    }

    /// <summary>Executes the IncrementalGeneratorReportsDiagnosticForEmptyGroupName operation.</summary>
    /// <returns>The IncrementalGeneratorReportsDiagnosticForEmptyGroupName operation result.</returns>
    [Test]
    internal async Task IncrementalGeneratorReportsDiagnosticForEmptyGroupNameAsync()
    {
        const string schema = $$"""
        {
          "tags": [
            { "name": "{{MotorSpeedTagName}}", "address": "D100", "dataType": "Float" }
          ],
          "groups": [
            { "name": "", "tagNames": ["{{MotorSpeedTagName}}"] }
          ]
        }
        """;

        var source = $$"""
        using MitsubishiRx;

        /// <summary>Provides the SchemaMarker type.</summary>
        [MitsubishiTagClientSchema({{ToLiteral(schema)}})]
        internal sealed class SchemaMarker { }
        """;

        var result = RunGeneratorCompilation(source);
        var diagnostics = result.Diagnostics.Where(static d => d.Id == "MRTXGEN007").ToArray();

        if (diagnostics.Length == 0)
        {
            throw new InvalidOperationException(
                string.Join(
                    Environment.NewLine,
                    result.Diagnostics.Select(static diagnostic => diagnostic.ToString())));
        }

        await Assert.That(
                diagnostics.Any(
                    static d => d.GetMessage().Contains("Group name", StringComparison.Ordinal)))
            .IsTrue();
    }

    /// <summary>Executes the IncrementalGeneratorReportsDiagnosticForEmptyGroupMembership operation.</summary>
    /// <returns>The IncrementalGeneratorReportsDiagnosticForEmptyGroupMembership operation result.</returns>
    [Test]
    internal async Task IncrementalGeneratorReportsDiagnosticForEmptyGroupMembershipAsync()
    {
        const string schema = $$"""
        {
          "tags": [
            { "name": "{{MotorSpeedTagName}}", "address": "D100", "dataType": "Float" }
          ],
          "groups": [
            { "name": "{{Line1GroupName}}", "tagNames": [] }
          ]
        }
        """;

        var source = $$"""
        using MitsubishiRx;

        /// <summary>Provides the SchemaMarker type.</summary>
        [MitsubishiTagClientSchema({{ToLiteral(schema)}})]
        internal sealed class SchemaMarker { }
        """;

        var result = RunGeneratorCompilation(source);
        var diagnostics = result.Diagnostics.Where(static d => d.Id == "MRTXGEN008").ToArray();

        if (diagnostics.Length == 0)
        {
            throw new InvalidOperationException(
                string.Join(
                    Environment.NewLine,
                    result.Diagnostics.Select(static diagnostic => diagnostic.ToString())));
        }

        await Assert.That(
                diagnostics.Any(
                    static d => d.GetMessage().Contains(Line1GroupName, StringComparison.Ordinal)))
            .IsTrue();
    }

    /// <summary>Executes the IncrementalGeneratorReportsDiagnosticForDuplicateGroupNames operation.</summary>
    /// <returns>The IncrementalGeneratorReportsDiagnosticForDuplicateGroupNames operation result.</returns>
    [Test]
    internal async Task IncrementalGeneratorReportsDiagnosticForDuplicateGroupNamesAsync()
    {
        const string schema = $$"""
        {
          "tags": [
            { "name": "{{MotorSpeedTagName}}", "address": "D100", "dataType": "Float" }
          ],
          "groups": [
            { "name": "{{Line1GroupName}}", "tagNames": ["{{MotorSpeedTagName}}"] },
            { "name": "{{Line1GroupName}}", "tagNames": ["{{MotorSpeedTagName}}"] }
          ]
        }
        """;

        var source = $$"""
        using MitsubishiRx;

        /// <summary>Provides the SchemaMarker type.</summary>
        [MitsubishiTagClientSchema({{ToLiteral(schema)}})]
        internal sealed class SchemaMarker { }
        """;

        var result = RunGeneratorCompilation(source);
        var diagnostics = result.Diagnostics.Where(static d => d.Id == "MRTXGEN009").ToArray();

        if (diagnostics.Length == 0)
        {
            throw new InvalidOperationException(
                string.Join(
                    Environment.NewLine,
                    result.Diagnostics.Select(static diagnostic => diagnostic.ToString())));
        }

        await Assert.That(
                diagnostics.Any(
                    static d => d.GetMessage().Contains(Line1GroupName, StringComparison.Ordinal)))
            .IsTrue();
    }

    /// <summary>Executes the IncrementalGeneratorReportsDiagnosticForEmptyGroupTagReference operation.</summary>
    /// <returns>The IncrementalGeneratorReportsDiagnosticForEmptyGroupTagReference operation result.</returns>
    [Test]
    internal async Task IncrementalGeneratorReportsDiagnosticForEmptyGroupTagReferenceAsync()
    {
        const string schema = $$"""
        {
          "tags": [
            { "name": "{{MotorSpeedTagName}}", "address": "D100", "dataType": "Float" }
          ],
          "groups": [
            { "name": "{{Line1GroupName}}", "tagNames": [""] }
          ]
        }
        """;

        var source = $$"""
        using MitsubishiRx;

        /// <summary>Provides the SchemaMarker type.</summary>
        [MitsubishiTagClientSchema({{ToLiteral(schema)}})]
        internal sealed class SchemaMarker { }
        """;

        var result = RunGeneratorCompilation(source);
        var diagnostics = result.Diagnostics.Where(static d => d.Id == "MRTXGEN010").ToArray();

        if (diagnostics.Length == 0)
        {
            throw new InvalidOperationException(
                string.Join(
                    Environment.NewLine,
                    result.Diagnostics.Select(static diagnostic => diagnostic.ToString())));
        }

        await Assert.That(
                diagnostics.Any(
                    static d => d.GetMessage().Contains(Line1GroupName, StringComparison.Ordinal)))
            .IsTrue();
    }

    /// <summary>Executes the IncrementalGeneratorReportsDiagnosticForDuplicateGroupTagReference operation.</summary>
    /// <returns>The IncrementalGeneratorReportsDiagnosticForDuplicateGroupTagReference operation result.</returns>
    [Test]
    internal async Task IncrementalGeneratorReportsDiagnosticForDuplicateGroupTagReferenceAsync()
    {
        const string schema = $$"""
        {
          "tags": [
            { "name": "{{MotorSpeedTagName}}", "address": "D100", "dataType": "Float" }
          ],
          "groups": [
            { "name": "{{Line1GroupName}}", "tagNames": ["{{MotorSpeedTagName}}", "{{MotorSpeedTagName}}"] }
          ]
        }
        """;

        var source = $$"""
        using MitsubishiRx;

        /// <summary>Provides the SchemaMarker type.</summary>
        [MitsubishiTagClientSchema({{ToLiteral(schema)}})]
        internal sealed class SchemaMarker { }
        """;

        var result = RunGeneratorCompilation(source);
        var diagnostics = result.Diagnostics.Where(static d => d.Id == "MRTXGEN011").ToArray();

        if (diagnostics.Length == 0)
        {
            throw new InvalidOperationException(
                string.Join(
                    Environment.NewLine,
                    result.Diagnostics.Select(static diagnostic => diagnostic.ToString())));
        }

        await Assert.That(
                diagnostics.Any(
                    static d => d.GetMessage().Contains(MotorSpeedTagName, StringComparison.Ordinal)))
            .IsTrue();
    }
}
