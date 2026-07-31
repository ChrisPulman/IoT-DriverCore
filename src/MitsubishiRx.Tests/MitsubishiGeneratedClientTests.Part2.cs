// Copyright (c) 2019-2026 Chris Pulman and contributors. All rights reserved.
// Chris Pulman and contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using IoT.Driver.Core;
using Microsoft.CodeAnalysis;

#if REACTIVE_SHIM

namespace IoT.Driver.MitsubishiRx.Reactive.Tests;
#else

namespace IoT.Driver.MitsubishiRx.Tests;
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
        using IoT.Driver.MitsubishiRx;

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
        using IoT.Driver.MitsubishiRx;

        /// <summary>Provides the SchemaMarker type.</summary>
        [MitsubishiTagClientSchema({{ToLiteral(schema)}})]
        internal sealed class SchemaMarker { }
        """;

        var result = RunGeneratorCompilation(source);
        var diagnostics = GetDiagnosticsById(result.Diagnostics, "MRTXGEN002");

        if (diagnostics.Length == 0)
        {
            throw new InvalidOperationException(
                FormatDiagnostics(result.Diagnostics));
        }

        await Assert.That(
                ContainsDiagnosticMessage(diagnostics, MotorSpeedTagName))
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
        using IoT.Driver.MitsubishiRx;

        /// <summary>Provides the SchemaMarker type.</summary>
        [MitsubishiTagClientSchema({{ToLiteral(schema)}})]
        internal sealed class SchemaMarker { }
        """;

        var result = RunGeneratorCompilation(source);
        var diagnostics = GetDiagnosticsById(result.Diagnostics, "MRTXGEN003");

        if (diagnostics.Length == 0)
        {
            throw new InvalidOperationException(
                FormatDiagnostics(result.Diagnostics));
        }

        await Assert.That(
                ContainsDiagnosticMessage(diagnostics, "MissingTag"))
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
        using IoT.Driver.MitsubishiRx;

        /// <summary>Provides the SchemaMarker type.</summary>
        [MitsubishiTagClientSchema({{ToLiteral(schema)}})]
        internal sealed class SchemaMarker { }
        """;

        var result = RunGeneratorCompilation(source);
        var diagnostics = GetDiagnosticsById(result.Diagnostics, "MRTXGEN004");

        if (diagnostics.Length == 0)
        {
            throw new InvalidOperationException(
                FormatDiagnostics(result.Diagnostics));
        }

        await Assert.That(
                ContainsDiagnosticMessage(diagnostics, "Decimal128"))
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
        using IoT.Driver.MitsubishiRx;

        /// <summary>Provides the SchemaMarker type.</summary>
        [MitsubishiTagClientSchema({{ToLiteral(schema)}})]
        internal sealed class SchemaMarker { }
        """;

        var result = RunGeneratorCompilation(source);
        var diagnostics = GetDiagnosticsById(result.Diagnostics, "MRTXGEN005");

        if (diagnostics.Length == 0)
        {
            throw new InvalidOperationException(
                FormatDiagnostics(result.Diagnostics));
        }

        await Assert.That(
                ContainsDiagnosticMessage(diagnostics, MotorSpeedTagName))
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
        using IoT.Driver.MitsubishiRx;

        /// <summary>Provides the SchemaMarker type.</summary>
        [MitsubishiTagClientSchema({{ToLiteral(schema)}})]
        internal sealed class SchemaMarker { }
        """;

        var result = RunGeneratorCompilation(source);
        var diagnostics = GetDiagnosticsById(result.Diagnostics, "MRTXGEN006");

        if (diagnostics.Length == 0)
        {
            throw new InvalidOperationException(
                FormatDiagnostics(result.Diagnostics));
        }

        await Assert.That(
                ContainsDiagnosticMessage(diagnostics, "Tag name"))
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
        using IoT.Driver.MitsubishiRx;

        /// <summary>Provides the SchemaMarker type.</summary>
        [MitsubishiTagClientSchema({{ToLiteral(schema)}})]
        internal sealed class SchemaMarker { }
        """;

        var result = RunGeneratorCompilation(source);
        var diagnostics = GetDiagnosticsById(result.Diagnostics, "MRTXGEN007");

        if (diagnostics.Length == 0)
        {
            throw new InvalidOperationException(
                FormatDiagnostics(result.Diagnostics));
        }

        await Assert.That(
                ContainsDiagnosticMessage(diagnostics, "Group name"))
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
        using IoT.Driver.MitsubishiRx;

        /// <summary>Provides the SchemaMarker type.</summary>
        [MitsubishiTagClientSchema({{ToLiteral(schema)}})]
        internal sealed class SchemaMarker { }
        """;

        var result = RunGeneratorCompilation(source);
        var diagnostics = GetDiagnosticsById(result.Diagnostics, "MRTXGEN008");

        if (diagnostics.Length == 0)
        {
            throw new InvalidOperationException(
                FormatDiagnostics(result.Diagnostics));
        }

        await Assert.That(
                ContainsDiagnosticMessage(diagnostics, Line1GroupName))
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
        using IoT.Driver.MitsubishiRx;

        /// <summary>Provides the SchemaMarker type.</summary>
        [MitsubishiTagClientSchema({{ToLiteral(schema)}})]
        internal sealed class SchemaMarker { }
        """;

        var result = RunGeneratorCompilation(source);
        var diagnostics = GetDiagnosticsById(result.Diagnostics, "MRTXGEN009");

        if (diagnostics.Length == 0)
        {
            throw new InvalidOperationException(
                FormatDiagnostics(result.Diagnostics));
        }

        await Assert.That(
                ContainsDiagnosticMessage(diagnostics, Line1GroupName))
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
        using IoT.Driver.MitsubishiRx;

        /// <summary>Provides the SchemaMarker type.</summary>
        [MitsubishiTagClientSchema({{ToLiteral(schema)}})]
        internal sealed class SchemaMarker { }
        """;

        var result = RunGeneratorCompilation(source);
        var diagnostics = GetDiagnosticsById(result.Diagnostics, "MRTXGEN010");

        if (diagnostics.Length == 0)
        {
            throw new InvalidOperationException(
                FormatDiagnostics(result.Diagnostics));
        }

        await Assert.That(
                ContainsDiagnosticMessage(diagnostics, Line1GroupName))
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
        using IoT.Driver.MitsubishiRx;

        /// <summary>Provides the SchemaMarker type.</summary>
        [MitsubishiTagClientSchema({{ToLiteral(schema)}})]
        internal sealed class SchemaMarker { }
        """;

        var result = RunGeneratorCompilation(source);
        var diagnostics = GetDiagnosticsById(result.Diagnostics, "MRTXGEN011");

        if (diagnostics.Length == 0)
        {
            throw new InvalidOperationException(
                FormatDiagnostics(result.Diagnostics));
        }

        await Assert.That(
                ContainsDiagnosticMessage(diagnostics, MotorSpeedTagName))
            .IsTrue();
    }

    /// <summary>Gets diagnostics with the specified identifier.</summary>
    /// <param name="diagnostics">The diagnostics to inspect.</param>
    /// <param name="id">The diagnostic identifier to match.</param>
    /// <returns>The matching diagnostics.</returns>
    private static Diagnostic[] GetDiagnosticsById(IReadOnlyList<Diagnostic> diagnostics, string id)
    {
        var matches = new List<Diagnostic>();
        foreach (Diagnostic diagnostic in diagnostics)
        {
            if (diagnostic.Id == id)
            {
                matches.Add(diagnostic);
            }
        }

        return [.. matches];
    }

    /// <summary>Gets error-severity diagnostics.</summary>
    /// <param name="diagnostics">The diagnostics to inspect.</param>
    /// <returns>The error diagnostics.</returns>
    private static Diagnostic[] GetErrorDiagnostics(IReadOnlyList<Diagnostic> diagnostics)
    {
        var errors = new List<Diagnostic>();
        foreach (Diagnostic diagnostic in diagnostics)
        {
            if (diagnostic.Severity == DiagnosticSeverity.Error)
            {
                errors.Add(diagnostic);
            }
        }

        return [.. errors];
    }

    /// <summary>Determines whether a diagnostic message contains the expected fragment.</summary>
    /// <param name="diagnostics">The diagnostics to inspect.</param>
    /// <param name="expectedFragment">The expected message fragment.</param>
    /// <returns><see langword="true"/> when a matching diagnostic exists; otherwise, <see langword="false"/>.</returns>
    private static bool ContainsDiagnosticMessage(IReadOnlyList<Diagnostic> diagnostics, string expectedFragment)
    {
        foreach (Diagnostic diagnostic in diagnostics)
        {
            if (diagnostic.GetMessage().Contains(expectedFragment, StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Formats diagnostics for an exception message.</summary>
    /// <param name="diagnostics">The diagnostics to format.</param>
    /// <returns>The newline-delimited diagnostic messages.</returns>
    private static string FormatDiagnostics(IReadOnlyList<Diagnostic> diagnostics)
    {
        var messages = new string[diagnostics.Count];
        for (var index = 0; index < diagnostics.Count; index++)
        {
            messages[index] = diagnostics[index].ToString();
        }

        return string.Join(Environment.NewLine, messages);
    }
}
