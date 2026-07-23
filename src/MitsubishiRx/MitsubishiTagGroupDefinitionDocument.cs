// Copyright (c) 2019-2026 Chris Pulman and contributors. All rights reserved.
// Chris Pulman and contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Text.Json.Serialization;

#if REACTIVE_SHIM

namespace IoT.DriverCore.MitsubishiRx.Reactive;

#else

namespace IoT.DriverCore.MitsubishiRx;

#endif

/// <summary>Provides the MitsubishiTagGroupDefinitionDocument type.</summary>
internal sealed class MitsubishiTagGroupDefinitionDocument
{
    /// <summary>Gets or sets the Name property.</summary>
    [JsonInclude]
    internal string? Name { get; set; }

    /// <summary>Gets the TagNames property.</summary>
    [JsonInclude]
    internal List<string>? TagNames { get; init; }

    /// <summary>Executes the FromModel operation.</summary>
    /// <param name="model">The model parameter.</param>
    /// <returns>The FromModel operation result.</returns>
    internal static MitsubishiTagGroupDefinitionDocument FromModel(
        MitsubishiTagGroupDefinition model) => new() { Name = model.Name, TagNames = model.ResolvedTagNames.ToList() };

    /// <summary>Executes the ToModel operation.</summary>
    /// <returns>The ToModel operation result.</returns>
    internal MitsubishiTagGroupDefinition ToModel() =>
        new(Name ?? string.Empty, TagNames ?? new List<string>());
}
