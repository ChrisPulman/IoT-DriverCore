// Copyright (c) 2019-2026 Chris Pulman and contributors. All rights reserved.
// Chris Pulman and contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Text;

namespace MitsubishiRx;

/// <summary>Provides common source emission helpers.</summary>
internal static partial class MitsubishiTagClientEmitter
{
    /// <summary>Appends generated reactive snapshot mapping members.</summary>
    /// <param name="builder">Source builder.</param>
    /// <param name="snapshotType">Generated snapshot type name.</param>
    private static void AppendReactiveMappingMembers(StringBuilder builder, string snapshotType)
    {
        AppendReactiveMappingMember(builder, snapshotType, false);
        AppendLine(builder);
        AppendReactiveMappingMember(builder, snapshotType, true);
    }

    /// <summary>Appends one generated reactive snapshot mapping member.</summary>
    /// <param name="builder">Source builder.</param>
    /// <param name="snapshotType">Generated snapshot type name.</param>
    /// <param name="optional">Whether the mapping is optional.</param>
    private static void AppendReactiveMappingMember(StringBuilder builder, string snapshotType, bool optional)
    {
        string nullableMarker = optional ? "?" : string.Empty;
        string methodName = optional ? "MapReactiveOptional" : "MapReactive";
        string mapperName = optional ? "TryFromSnapshot" : "FromSnapshot";
        AppendMemberDocumentation(builder, "Maps a reactive group snapshot to a typed snapshot.");
        AppendLine(builder, $"    public static MitsubishiReactiveValue<{snapshotType}{nullableMarker}> {methodName}(");
        AppendLine(builder, "        MitsubishiReactiveValue<MitsubishiTagGroupSnapshot> value)");
        AppendLine(builder, "    {");
        AppendLine(builder, "        ArgumentNullException.ThrowIfNull(value);");
        if (optional)
        {
            AppendLine(builder, $"        return new MitsubishiReactiveValue<{snapshotType}?>(");
            AppendLine(builder, $"            {mapperName}(value.Value), value.TimestampUtc, value.Quality,");
            AppendLine(builder, "            value.IsHeartbeat, value.IsStale, value.Source, value.Error,");
            AppendLine(builder, "            value.ErrorCode, value.Exception);");
            AppendLine(builder, "    }");
            return;
        }

        AppendReactiveRequiredMappingBody(builder, snapshotType, mapperName);
        AppendLine(builder, "    }");
    }

    /// <summary>Appends the required reactive snapshot mapping body.</summary>
    /// <param name="builder">Source builder.</param>
    /// <param name="snapshotType">Generated snapshot type name.</param>
    /// <param name="mapperName">Generated snapshot mapper name.</param>
    private static void AppendReactiveRequiredMappingBody(
        StringBuilder builder,
        string snapshotType,
        string mapperName)
    {
        AppendLine(builder, "        if (value.Value is null)");
        AppendLine(builder, NestedOpenBrace);
        AppendLine(builder, $"            return new MitsubishiReactiveValue<{snapshotType}>(default,");
        AppendLine(builder, "                value.TimestampUtc, value.Quality, value.IsHeartbeat, value.IsStale,");
        AppendLine(builder, "                value.Source, value.Error, value.ErrorCode, value.Exception);");
        AppendLine(builder, NestedCloseBrace);
        AppendLine(builder, "        try");
        AppendLine(builder, NestedOpenBrace);
        AppendLine(builder, $"            return new MitsubishiReactiveValue<{snapshotType}>(");
        AppendLine(builder, $"                {mapperName}(value.Value), value.TimestampUtc, value.Quality,");
        AppendLine(builder, "                value.IsHeartbeat, value.IsStale, value.Source, value.Error,");
        AppendLine(builder, "                value.ErrorCode, value.Exception);");
        AppendLine(builder, NestedCloseBrace);
        AppendLine(builder, "        catch (Exception ex)");
        AppendLine(builder, NestedOpenBrace);
        AppendLine(builder, $"            return new MitsubishiReactiveValue<{snapshotType}>(default,");
        AppendLine(builder, "                value.TimestampUtc, MitsubishiReactiveQuality.Error,");
        AppendLine(builder, "                Source: value.Source,");
        AppendLine(builder, "                Error: ex.Message, ErrorCode: value.ErrorCode, Exception: ex);");
        AppendLine(builder, NestedCloseBrace);
    }

    /// <summary>Resolves the generated read value type.</summary>
    /// <param name="dataType">Schema data type.</param>
    /// <returns>Generated read value type.</returns>
    private static string ResolveReadType(string? dataType)
        => dataType switch
        {
            "Bit" => "bool",
            "String" => "string",
            "Float" => "float",
            "DWord" or "UInt32" => "uint",
            "Int32" => "int",
            "Int16" => "short",
            "UInt16" or "Word" or null => "ushort",
            _ => "ushort",
        };

    /// <summary>Resolves the generated read method name.</summary>
    /// <param name="dataType">Schema data type.</param>
    /// <returns>Generated read method name.</returns>
    private static string ResolveReadMethod(string? dataType)
        => dataType switch
        {
            "Bit" => "ReadGeneratedBitTagAsync",
            "String" => "ReadStringByTagAsync",
            "Float" => "ReadFloatByTagAsync",
            "DWord" or "UInt32" => "ReadDWordByTagAsync",
            "Int32" => "ReadInt32ByTagAsync",
            "Int16" => "ReadInt16ByTagAsync",
            "UInt16" or "Word" or null => "ReadUInt16ByTagAsync",
            _ => "ReadUInt16ByTagAsync",
        };

    /// <summary>Resolves the generated write value type.</summary>
    /// <param name="dataType">Schema data type.</param>
    /// <returns>Generated write value type.</returns>
    private static string ResolveWriteType(string? dataType) => ResolveReadType(dataType);

    /// <summary>Resolves the generated write method name.</summary>
    /// <param name="dataType">Schema data type.</param>
    /// <returns>Generated write method name.</returns>
    private static string ResolveWriteMethod(string? dataType)
        => dataType switch
        {
            "Bit" => "WriteGeneratedBitTagAsync",
            "String" => "WriteStringByTagAsync",
            "Float" => "WriteFloatByTagAsync",
            "DWord" or "UInt32" => "WriteDWordByTagAsync",
            "Int32" => "WriteInt32ByTagAsync",
            "Int16" => "WriteInt16ByTagAsync",
            "UInt16" or "Word" or null => "WriteUInt16ByTagAsync",
            _ => "WriteUInt16ByTagAsync",
        };

    /// <summary>Appends an empty generated source line.</summary>
    /// <param name="builder">Source builder.</param>
    private static void AppendLine(StringBuilder builder) => _ = builder.AppendLine();

    /// <summary>Appends a generated source line.</summary>
    /// <param name="builder">Source builder.</param>
    /// <param name="value">Source line.</param>
    private static void AppendLine(StringBuilder builder, string value) => _ = builder.AppendLine(value);

    /// <summary>Appends a generated XML documentation summary.</summary>
    /// <param name="builder">The target generated-source builder.</param>
    /// <param name="summary">The summary text.</param>
    /// <param name="indentation">The generated source indentation.</param>
    private static void AppendDocumentation(StringBuilder builder, string summary, int indentation = 0)
    {
        string indent = new(' ', indentation);
        AppendLine(builder, $"{indent}/// <summary>{summary}</summary>");
    }

    /// <summary>Appends a generated XML documentation summary for a type member.</summary>
    /// <param name="builder">The target generated-source builder.</param>
    /// <param name="summary">The summary text.</param>
    private static void AppendMemberDocumentation(StringBuilder builder, string summary)
        => AppendDocumentation(builder, summary, MemberIndentation);
}
