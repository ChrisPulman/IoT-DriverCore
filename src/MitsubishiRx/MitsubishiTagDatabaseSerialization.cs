// Copyright (c) 2019-2026 Chris Pulman and contributors. All rights reserved.
// Chris Pulman and contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Text.Json;
using System.Text.Json.Serialization;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

#if REACTIVE_SHIM

namespace IoT.Driver.MitsubishiRx.Reactive;

#else

namespace IoT.Driver.MitsubishiRx;

#endif

/// <summary>Provides the MitsubishiTagDatabaseSerialization type.</summary>
internal static class MitsubishiTagDatabaseSerialization
{
    /// <summary>Stores the JsonOptions field.</summary>
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    /// <summary>Stores the YamlSerializer field.</summary>
    private static readonly ISerializer YamlSerializer = new SerializerBuilder()
        .IncludeNonPublicProperties()
        .WithNamingConvention(CamelCaseNamingConvention.Instance)
        .ConfigureDefaultValuesHandling(DefaultValuesHandling.OmitNull)
        .Build();

    /// <summary>Stores the YamlDeserializer field.</summary>
    private static readonly IDeserializer YamlDeserializer = new DeserializerBuilder()
        .IncludeNonPublicProperties()
        .WithNamingConvention(CamelCaseNamingConvention.Instance)
        .IgnoreUnmatchedProperties()
        .Build();

    /// <summary>Executes the ToJson operation.</summary>
    /// <param name="database">The database parameter.</param>
    /// <returns>The ToJson operation result.</returns>
    internal static string ToJson(MitsubishiTagDatabase database)
    {
        ArgumentNullException.ThrowIfNull(database);
        return JsonSerializer.Serialize(ToDocument(database), JsonOptions);
    }

    /// <summary>Executes the FromJson operation.</summary>
    /// <param name="json">The json parameter.</param>
    /// <returns>The FromJson operation result.</returns>
    internal static MitsubishiTagDatabase FromJson(string json)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(json);
        var document =
            JsonSerializer.Deserialize<MitsubishiTagDatabaseDocument>(json, JsonOptions)
            ?? new MitsubishiTagDatabaseDocument();
        return FromDocument(document);
    }

    /// <summary>Executes the ToYaml operation.</summary>
    /// <param name="database">The database parameter.</param>
    /// <returns>The ToYaml operation result.</returns>
    internal static string ToYaml(MitsubishiTagDatabase database)
    {
        ArgumentNullException.ThrowIfNull(database);
        return YamlSerializer.Serialize(ToDocument(database));
    }

    /// <summary>Executes the FromYaml operation.</summary>
    /// <param name="yaml">The yaml parameter.</param>
    /// <returns>The FromYaml operation result.</returns>
    internal static MitsubishiTagDatabase FromYaml(string yaml)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(yaml);
        var document =
            YamlDeserializer.Deserialize<MitsubishiTagDatabaseDocument>(yaml)
            ?? new MitsubishiTagDatabaseDocument();
        return FromDocument(document);
    }

    /// <summary>Executes the ToDocument operation.</summary>
    /// <param name="database">The database parameter.</param>
    /// <returns>The ToDocument operation result.</returns>
    private static MitsubishiTagDatabaseDocument ToDocument(MitsubishiTagDatabase database)
    {
        var tags = new List<MitsubishiTagDefinitionDocument>(database.Tags.Count);
        foreach (var tag in database.Tags)
        {
            tags.Add(MitsubishiTagDefinitionDocument.FromModel(tag));
        }

        var groups = new List<MitsubishiTagGroupDefinitionDocument>(database.Groups.Count);
        foreach (var group in database.Groups)
        {
            groups.Add(MitsubishiTagGroupDefinitionDocument.FromModel(group));
        }

        return new MitsubishiTagDatabaseDocument
        {
            Tags = tags,
            Groups = groups,
        };
    }

    /// <summary>Executes the FromDocument operation.</summary>
    /// <param name="document">The document parameter.</param>
    /// <returns>The FromDocument operation result.</returns>
    private static MitsubishiTagDatabase FromDocument(MitsubishiTagDatabaseDocument document)
    {
        var definitions = new List<MitsubishiTagDefinition>();
        foreach (var tag in document.Tags ?? [])
        {
            definitions.Add(tag.ToModel());
        }

        var database = new MitsubishiTagDatabase(definitions);
        foreach (var group in document.Groups ?? new List<MitsubishiTagGroupDefinitionDocument>())
        {
            database.AddGroup(group.ToModel());
        }

        return database;
    }
}
