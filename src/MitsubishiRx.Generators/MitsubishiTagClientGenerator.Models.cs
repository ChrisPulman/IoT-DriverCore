// Copyright (c) 2019-2026 Chris Pulman and contributors. All rights reserved.
// Chris Pulman and contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Collections.Generic;
using System.Text.Json;

namespace IoT.DriverCore.MitsubishiRx;

/// <summary>Contains schema-model types used by the Mitsubishi tag source generator.</summary>
public sealed partial class MitsubishiTagClientGenerator
{
    /// <summary>Parsed generator schema.</summary>
    internal sealed class SchemaModel
    {
        /// <summary>Initializes a new instance of the <see cref="SchemaModel"/> class.</summary>
        /// <param name="tags">Parsed tags.</param>
        /// <param name="groups">Parsed groups.</param>
        private SchemaModel(IReadOnlyList<TagModel> tags, IReadOnlyList<GroupModel> groups)
        {
            Tags = tags;
            Groups = groups;
        }

        /// <summary>Gets the parsed tags.</summary>
        internal IReadOnlyList<TagModel> Tags { get; }

        /// <summary>Gets the parsed groups.</summary>
        internal IReadOnlyList<GroupModel> Groups { get; }

        /// <summary>Parses schema JSON.</summary>
        /// <param name="json">Schema JSON.</param>
        /// <returns>Parsed schema model.</returns>
        internal static SchemaModel Parse(string json)
        {
            using var document = JsonDocument.Parse(json);
            var root = document.RootElement;
            return new SchemaModel(ParseTags(root), ParseGroups(root));
        }

        /// <summary>Parses tag entries from the schema root.</summary>
        /// <param name="root">Schema root.</param>
        /// <returns>The parsed tags.</returns>
        private static List<TagModel> ParseTags(JsonElement root)
        {
            var tags = new List<TagModel>();
            if (!TryGetArray(root, nameof(tags), out var tagsElement))
            {
                return tags;
            }

            foreach (var tag in tagsElement.EnumerateArray())
            {
                tags.Add(new TagModel(
                    GetStringProperty(tag, "name") ?? string.Empty,
                    GetStringProperty(tag, "dataType")));
            }

            return tags;
        }

        /// <summary>Parses group entries from the schema root.</summary>
        /// <param name="root">Schema root.</param>
        /// <returns>The parsed groups.</returns>
        private static List<GroupModel> ParseGroups(JsonElement root)
        {
            var groups = new List<GroupModel>();
            if (!TryGetArray(root, nameof(groups), out var groupsElement))
            {
                return groups;
            }

            foreach (var group in groupsElement.EnumerateArray())
            {
                groups.Add(new GroupModel(
                    GetStringProperty(group, "name") ?? string.Empty,
                    ParseTagNames(group)));
            }

            return groups;
        }

        /// <summary>Parses tag names from a group entry.</summary>
        /// <param name="group">Group element.</param>
        /// <returns>The parsed tag names.</returns>
        private static List<string> ParseTagNames(JsonElement group)
        {
            var tagNames = new List<string>();
            if (!TryGetArray(group, nameof(tagNames), out var tagNamesElement))
            {
                return tagNames;
            }

            foreach (var tagName in tagNamesElement.EnumerateArray())
            {
                tagNames.Add(tagName.GetString() ?? string.Empty);
            }

            return tagNames;
        }

        /// <summary>Gets a string property from a JSON element.</summary>
        /// <param name="element">Source element.</param>
        /// <param name="propertyName">Property name.</param>
        /// <returns>The property value, when present.</returns>
        private static string? GetStringProperty(JsonElement element, string propertyName)
            => element.TryGetProperty(propertyName, out var value) ? value.GetString() : null;

        /// <summary>Gets an array property from a JSON element.</summary>
        /// <param name="element">Source element.</param>
        /// <param name="propertyName">Property name.</param>
        /// <param name="value">The discovered array value.</param>
        /// <returns><c>true</c> when the array exists.</returns>
        private static bool TryGetArray(JsonElement element, string propertyName, out JsonElement value)
        {
            if (element.TryGetProperty(propertyName, out value) && value.ValueKind == JsonValueKind.Array)
            {
                return true;
            }

            value = default;
            return false;
        }
    }

    /// <summary>Parsed tag schema entry.</summary>
    internal sealed class TagModel
    {
        /// <summary>Initializes a new instance of the <see cref="TagModel"/> class.</summary>
        /// <param name="name">Tag name.</param>
        /// <param name="dataType">Tag data type.</param>
        public TagModel(string name, string? dataType) => (Name, DataType) = (name, dataType);

        /// <summary>Gets the tag name.</summary>
        internal string Name { get; }

        /// <summary>Gets the tag data type.</summary>
        internal string? DataType { get; }
    }

    /// <summary>Parsed group schema entry.</summary>
    internal sealed class GroupModel
    {
        /// <summary>Initializes a new instance of the <see cref="GroupModel"/> class.</summary>
        /// <param name="name">Group name.</param>
        /// <param name="tagNames">Names of tags in the group.</param>
        public GroupModel(string name, IReadOnlyList<string> tagNames) => (Name, TagNames) = (name, tagNames);

        /// <summary>Gets the group name.</summary>
        internal string Name { get; }

        /// <summary>Gets the group tag names.</summary>
        internal IReadOnlyList<string> TagNames { get; }
    }

    /// <summary>Describes one property-level generated binding.</summary>
    internal sealed class PropertyBindingModel
    {
        /// <summary>Initializes a new instance of the <see cref="PropertyBindingModel"/> class.</summary>
        /// <param name="namespaceName">The containing namespace.</param>
        /// <param name="typeName">The containing type.</param>
        /// <param name="propertyName">The declared property.</param>
        /// <param name="propertyType">The property type.</param>
        /// <param name="tagName">The logical tag name.</param>
        /// <param name="clientMemberName">The logical client member.</param>
        public PropertyBindingModel(
            string namespaceName,
            string typeName,
            string propertyName,
            string propertyType,
            string tagName,
            string clientMemberName)
        {
            NamespaceName = namespaceName;
            TypeName = typeName;
            PropertyName = propertyName;
            PropertyType = propertyType;
            TagName = tagName;
            ClientMemberName = clientMemberName;
        }

        /// <summary>Gets the containing namespace.</summary>
        internal string NamespaceName { get; }

        /// <summary>Gets the containing type name.</summary>
        internal string TypeName { get; }

        /// <summary>Gets the declared property name.</summary>
        internal string PropertyName { get; }

        /// <summary>Gets the fully qualified property type.</summary>
        internal string PropertyType { get; }

        /// <summary>Gets the logical tag name.</summary>
        internal string TagName { get; }

        /// <summary>Gets the logical client member name.</summary>
        internal string ClientMemberName { get; }
    }
}
