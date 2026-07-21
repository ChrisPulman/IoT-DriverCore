// Copyright (c) 2019-2026 Chris Pulman and contributors. All rights reserved.
// Chris Pulman and contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Text.Json.Serialization;

#if REACTIVE_SHIM

namespace MitsubishiRx.Reactive;

#else

namespace MitsubishiRx;

#endif

/// <summary>Provides the MitsubishiTagDatabaseDocument type.</summary>
internal sealed class MitsubishiTagDatabaseDocument
{
    /// <summary>Gets the Tags property.</summary>
    [JsonInclude]
    internal List<MitsubishiTagDefinitionDocument>? Tags { get; init; }

    /// <summary>Gets the Groups property.</summary>
    [JsonInclude]
    internal List<MitsubishiTagGroupDefinitionDocument>? Groups { get; init; }
}
