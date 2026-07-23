// Copyright (c) 2019-2026 Chris Pulman and contributors. All rights reserved.
// Chris Pulman and contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using IoT.DriverCore.Core;

#if REACTIVE_SHIM

namespace IoT.DriverCore.MitsubishiRx.Reactive;

#else

namespace IoT.DriverCore.MitsubishiRx;

#endif

/// <summary>Composes common logical-tag operations with Mitsubishi protocol transports.</summary>
public sealed partial class MitsubishiLogicalTagClient
{
    /// <summary>Stores the largest MC random-word request count.</summary>
    private const int MaximumRandomWordCount = byte.MaxValue;

    /// <summary>Stores the largest contiguous word range planned for one request.</summary>
    private const int MaximumContiguousWordCount = 960;

    /// <summary>Stores the bulk read operation name.</summary>
    private const string BulkReadOperation = "read";

    /// <summary>Stores the bulk write operation name.</summary>
    private const string BulkWriteOperation = "write";

    /// <summary>Stores the signed word database type.</summary>
    private const string Int16DataType = "Int16";

    /// <summary>Stores the unsigned word database type.</summary>
    private const string UInt16DataType = "UInt16";

    /// <summary>Stores the generic word database type and transfer encoding.</summary>
    private const string WordDataType = "Word";

    /// <summary>Plans compatible Mitsubishi word addresses into contiguous memory-area ranges.</summary>
    private static readonly TagTransferPlanner BulkTransferPlanner =
        new(new TagTransferCapabilities(MaximumContiguousWordCount));

    /// <summary>Maps a common tag to its Mitsubishi definition.</summary>
    /// <param name="tag">The common tag.</param>
    /// <returns>The Mitsubishi definition.</returns>
    private static MitsubishiTagDefinition ToMitsubishiTag(LogicalTag tag) =>
        new(
            tag.Name,
            tag.Address,
            tag.DataType,
            EmptyToNull(tag.Description),
            ParseDouble(tag.Metadata, "Scale", 1.0),
            ParseDouble(tag.Metadata, "Offset", 0.0),
            ParseNullableInt(tag.Metadata, "Length"),
            GetMetadata(tag.Metadata, "Encoding"),
            GetMetadata(tag.Metadata, "Units"),
            ParseBool(tag.Metadata, "Signed"),
            GetMetadata(tag.Metadata, "ByteOrder"),
            GetMetadata(tag.Metadata, "Notes"));

    /// <summary>Gets all declared group names.</summary>
    /// <param name="tag">The common tag.</param>
    /// <returns>The distinct group names.</returns>
    private static string[] GetGroupNames(LogicalTag tag)
    {
        var names = new List<string>();
        if (!string.IsNullOrWhiteSpace(tag.GroupName))
        {
            names.Add(tag.GroupName);
        }

        if (tag.Metadata.TryGetValue("Groups", out var groups))
        {
            names.AddRange(
                groups
                    .Split('|', StringSplitOptions.RemoveEmptyEntries)
                    .Select(Uri.UnescapeDataString));
        }

        return names.Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
    }

    /// <summary>Gets optional metadata.</summary>
    /// <param name="metadata">The metadata dictionary.</param>
    /// <param name="name">The metadata key.</param>
    /// <returns>The optional value.</returns>
    private static string? GetMetadata(IReadOnlyDictionary<string, string> metadata, string name) =>
        metadata.TryGetValue(name, out var value) ? EmptyToNull(value) : null;

    /// <summary>Converts empty values to null.</summary>
    /// <param name="value">The source value.</param>
    /// <returns>The non-empty value or null.</returns>
    private static string? EmptyToNull(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value;

    /// <summary>Parses floating-point metadata.</summary>
    /// <param name="metadata">The metadata dictionary.</param>
    /// <param name="name">The metadata key.</param>
    /// <param name="defaultValue">The fallback value.</param>
    /// <returns>The parsed value.</returns>
    private static double ParseDouble(
        IReadOnlyDictionary<string, string> metadata,
        string name,
        double defaultValue) =>
        metadata.TryGetValue(name, out var value)
        && double.TryParse(
            value,
            System.Globalization.NumberStyles.Float,
            System.Globalization.CultureInfo.InvariantCulture,
            out var result)
            ? result
            : defaultValue;

    /// <summary>Parses optional integer metadata.</summary>
    /// <param name="metadata">The metadata dictionary.</param>
    /// <param name="name">The metadata key.</param>
    /// <returns>The parsed value or null.</returns>
    private static int? ParseNullableInt(
        IReadOnlyDictionary<string, string> metadata,
        string name) =>
        metadata.TryGetValue(name, out var value)
        && int.TryParse(
            value,
            System.Globalization.NumberStyles.Integer,
            System.Globalization.CultureInfo.InvariantCulture,
            out var result)
            ? result
            : null;

    /// <summary>Parses Boolean metadata.</summary>
    /// <param name="metadata">The metadata dictionary.</param>
    /// <param name="name">The metadata key.</param>
    /// <returns>The parsed value.</returns>
    private static bool ParseBool(IReadOnlyDictionary<string, string> metadata, string name) =>
        metadata.TryGetValue(name, out var value) && bool.TryParse(value, out var result) && result;

    /// <summary>Represents one caller-indexed, single-word Mitsubishi transfer.</summary>
    /// <param name="Index">The caller-defined result index.</param>
    /// <param name="Tag">The common logical tag.</param>
    /// <param name="Definition">The rich Mitsubishi database definition.</param>
    /// <param name="Address">The parsed Mitsubishi device address.</param>
    /// <param name="Value">The optional write value.</param>
    /// <param name="Word">The optional encoded write word.</param>
    private sealed record BulkWordRequest(
        int Index,
        LogicalTag Tag,
        MitsubishiTagDefinition Definition,
        MitsubishiDeviceAddress Address,
        LogicalTagValue? Value,
        ushort? Word);

    /// <summary>Represents a logical tag name that requires typed per-tag fallback.</summary>
    /// <param name="Index">The caller-defined result index.</param>
    /// <param name="TagName">The logical tag name.</param>
    private sealed record IndexedTagName(int Index, string TagName);

    /// <summary>Represents a logical tag value that requires typed per-tag fallback.</summary>
    /// <param name="Index">The caller-defined result index.</param>
    /// <param name="Value">The logical tag value.</param>
    private sealed record IndexedTagValue(int Index, LogicalTagValue Value);
}
