// Copyright (c) 2019-2026 Chris Pulman and contributors. All rights reserved.
// Chris Pulman and contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Text.Json.Serialization;

#if REACTIVE_SHIM

namespace IoT.DriverCore.MitsubishiRx.Reactive;

#else

namespace IoT.DriverCore.MitsubishiRx;

#endif

/// <summary>Provides the MitsubishiTagDefinitionDocument type.</summary>
internal sealed class MitsubishiTagDefinitionDocument
{
    /// <summary>Gets or sets the Name property.</summary>
    [JsonInclude]
    internal string? Name { get; set; }

    /// <summary>Gets or sets the Address property.</summary>
    [JsonInclude]
    internal string? Address { get; set; }

    /// <summary>Gets or sets the DataType property.</summary>
    [JsonInclude]
    internal string? DataType { get; set; }

    /// <summary>Gets or sets the Description property.</summary>
    [JsonInclude]
    internal string? Description { get; set; }

    /// <summary>Gets or sets the Scale property.</summary>
    [JsonInclude]
    internal double Scale { get; set; } = 1.0;

    /// <summary>Gets or sets the Offset property.</summary>
    [JsonInclude]
    internal double Offset { get; set; }

    /// <summary>Gets or sets the Length property.</summary>
    [JsonInclude]
    internal int? Length { get; set; }

    /// <summary>Gets or sets the Encoding property.</summary>
    [JsonInclude]
    internal string? Encoding { get; set; }

    /// <summary>Gets or sets the Units property.</summary>
    [JsonInclude]
    internal string? Units { get; set; }

    /// <summary>Gets or sets the Signed property.</summary>
    [JsonInclude]
    internal bool Signed { get; set; }

    /// <summary>Gets or sets the ByteOrder property.</summary>
    [JsonInclude]
    internal string? ByteOrder { get; set; }

    /// <summary>Gets or sets the Notes property.</summary>
    [JsonInclude]
    internal string? Notes { get; set; }

    /// <summary>Executes the FromModel operation.</summary>
    /// <param name="model">The model parameter.</param>
    /// <returns>The FromModel operation result.</returns>
    internal static MitsubishiTagDefinitionDocument FromModel(MitsubishiTagDefinition model) =>
        new()
        {
            Name = model.Name,
            Address = model.Address,
            DataType = model.DataType,
            Description = model.Description,
            Scale = model.Scale,
            Offset = model.Offset,
            Length = model.Length,
            Encoding = model.Encoding,
            Units = model.Units,
            Signed = model.Signed,
            ByteOrder = model.ByteOrder,
            Notes = model.Notes,
        };

    /// <summary>Executes the ToModel operation.</summary>
    /// <returns>The ToModel operation result.</returns>
    internal MitsubishiTagDefinition ToModel() =>
        new(
            Name ?? string.Empty,
            Address ?? string.Empty,
            DataType,
            Description,
            Scale,
            Offset,
            Length,
            Encoding,
            Units,
            Signed,
            ByteOrder,
            Notes);
}
